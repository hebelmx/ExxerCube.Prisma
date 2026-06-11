# SIARA Authentication — Feature Design (planning level)

**Date:** 2026-06-11 · **Branch:** Kt2 · **Status:** DESIGN / pre-implementation
**Feeds:** MVP-PATH Workstream **1.1** (real SIARA downloader) — this is the *auth seam* that 1.1 depends on.
**Owner ruling:** provide **both** auth mechanisms; the **client chooses** via configuration which to use — it is a **technical-legal control** (the client's own safety policy decides), so neither is hardcoded. Both **must never store raw credentials**.
**Recommend promoting to:** `ADR-010-SIARA-Authentication-Strategies.md` (architectural + legal significance).

---

## 1. Purpose & framing

SIARA has **no API and no auth API** — login is a basic web form, and (owner constraint) **the system must never hold raw credentials**. We provide two sanctioned, credential-free ways to obtain an authenticated SIARA browser session, selectable by config:

| Mode | Mechanism | Who authenticates | What we hold |
|---|---|---|---|
| **SessionPassthrough** | The downloader rides an **already-authenticated** browser session handed to it (sanctioned "session handoff" — attach to a live context / import its storage-state). | A human/another system, **outside** Prisma. | A session/storage-state handle only. |
| **InteractiveLogin** | A human authenticates **once** in a headed browser on the real SIARA page; we capture the resulting authenticated context and **keep it warm/refreshed** for subsequent headless downloads. | A human, typing into the **real SIARA page** (we never see the keystrokes). | A session/storage-state handle only. |

Both produce the **same artifact** — an authenticated browser context the scraper can use — so they sit behind **one port** with two implementations, chosen by configuration. This makes the choice a deployment-time, client-controlled, technical-legal decision.

---

## 2. Scope

**In scope (this design):** the authentication/session-acquisition seam — the port, the two strategies, the selection mechanism, the session value object, the browser-agent capability they need, security posture, and the full ITDD/DI/config plan.

**Out of scope (separate MVP-PATH items):** the document-download loop itself (the `IDocumentDownloader` adapter is **1.1**, the watch loop is **1.2**) — this design only defines the **seam** they consume. Credentialed auto-login is explicitly *excluded* (violates the no-credential-storage rule).

---

## 3. Existing building blocks (reconciled)

| Asset | File | Verdict for this design |
|---|---|---|
| `IBrowserAutomationAgent` | `01 Core/Domain/Interfaces/IBrowserAutomationAgent.cs` | Reuse. Has Launch/Navigate/Identify/Download/Close/Fill/Click/WaitForSelector — **but no session/context/storage-state** capability → **gap, see §7**. |
| `PlaywrightBrowserAutomationAdapter` | `02 Infrastructure/Infrastructure.BrowserAutomation/` | Extend to back the new session capability (Playwright `StorageState`/`BrowserContext`). |
| `SiaraNavigationTarget` | `…/NavigationTargets/SiaraNavigationTarget.cs` | Reuse for navigate + `RetrieveDocumentsAsync` *after* a session is acquired. |
| `ISiaraLoginService` / `SiaraLoginService` | `…/Services/SiaraLoginService.cs` | **⚠️ Takes raw `username, password`.** Conflicts with the no-credential rule. **Do NOT use on the MVP path.** Keep only as an internal helper the *InteractiveLogin* strategy may reuse to drive the form **with human-entered values it never persists**, or deprecate. Flag for an explicit decision. |
| `Result<T>` (`IndQuestResults`) | global usings | All new methods return `Result`/`Result<T>`; pre-cancel → `ResultExtensions.Cancelled<T>()`; `result.IsCancelled()`. |

---

## 4. The abstraction (Domain port + value objects)

New, in `ExxerCube.Prisma.Domain.Interfaces` and `…Domain.ValueObjects` / `…Domain.Enum`.

```csharp
// Domain/Enum/SiaraAuthMode.cs
public enum SiaraAuthMode
{
    SessionPassthrough = 0,
    InteractiveLogin   = 1,
}

// Domain/ValueObjects/SiaraSession.cs  — opaque, credential-free session handle
public sealed record SiaraSession
{
    public required string SessionId { get; init; }        // our correlation id
    public required SiaraAuthMode Mode { get; init; }
    public required string StorageStateRef { get; init; }  // opaque ref to storage-state (NOT raw cookies in logs)
    public DateTimeOffset? ExpiresAt { get; init; }        // null = unknown/until-invalid
    public string? AcquiredBy { get; init; }               // identity/process for AUDIT (ties to A6)
    // INVARIANT: contains NO username/password/credential fields.
}

// Domain/ValueObjects/SiaraSessionRequest.cs — what a caller asks for
public sealed record SiaraSessionRequest
{
    public string? ExistingContextEndpoint { get; init; }  // passthrough: CDP/ws endpoint or storage-state import ref
    public TimeSpan? MaxWaitForHuman { get; init; }         // interactive: how long to await one-time login
    public string? RequestedBy { get; init; }               // for audit
}

// Domain/Interfaces/ISiaraSessionProvider.cs — the strategy port
public interface ISiaraSessionProvider
{
    /// <summary>Which auth mode this provider implements (used by the resolver to honor config).</summary>
    SiaraAuthMode Mode { get; }

    /// <summary>Obtain an authenticated SIARA session. Never stores raw credentials.</summary>
    Task<Result<SiaraSession>> AcquireAsync(SiaraSessionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Validate / keep-warm / refresh a session (called by the watch loop, item 1.2).</summary>
    Task<Result<SiaraSession>> EnsureValidAsync(SiaraSession session, CancellationToken cancellationToken = default);

    /// <summary>Release/close the session and any browser context it owns.</summary>
    Task<Result> ReleaseAsync(SiaraSession session, CancellationToken cancellationToken = default);
}

// Domain/Interfaces/ISiaraSessionProviderResolver.cs — config-driven selection
public interface ISiaraSessionProviderResolver
{
    /// <summary>Returns the provider for the configured SiaraAuthMode (fails if not registered).</summary>
    Result<ISiaraSessionProvider> Resolve();
}
```

**Why a session *provider* (not "authenticator"):** the artifact is a reusable, refreshable session, not a one-shot login. `EnsureValidAsync` is what lets the **1.2 watch loop** run headless for hours off a single interactive login.

---

## 5. The two strategies

### 5.1 `SessionPassthroughSiaraSessionProvider` (`Mode = SessionPassthrough`)
- **`AcquireAsync`:** attaches to the externally-provided authenticated context (`request.ExistingContextEndpoint` — a Playwright/CDP connect endpoint, or an imported storage-state ref), verifies it lands on an authenticated SIARA page (probe a known post-login selector via the agent), returns a `SiaraSession` wrapping that context. **No login performed, no credentials touched.**
- **`EnsureValidAsync`:** re-probe; if the external session died, return failure (we can't re-auth — that's the external owner's job).
- **Security posture:** Prisma never authenticates; it borrows a live, already-cleared session. Strongest "we never touch the auth" story.

### 5.2 `InteractiveLoginSiaraSessionProvider` (`Mode = InteractiveLogin`)
- **`AcquireAsync`:** launches a **headed** browser at SIARA, waits (up to `MaxWaitForHuman`) for a human to complete login on the real page, detects success (post-login selector), **exports the resulting storage-state** into `StorageStateRef`, returns the session. We only ever observe that login *succeeded*, never the credentials.
- **`EnsureValidAsync`:** reload storage-state into a context, probe; if expired, return a typed failure so the loop can request a fresh one-time login.
- **Security posture:** human-in-the-loop once; session kept warm. Credentials are typed into SIARA's own page, never into Prisma.

> If `ISiaraLoginService` is reused here, it must be driven only with **human-supplied, non-persisted** values — or, preferably, the human types directly into the page and we just *detect* success. Decide in §14.

---

## 6. Selection mechanism (config-driven, client-controlled)

Follow the **keyed-services + resolver** precedent (`AddKeyedScoped<INavigationTarget,…>("siara")`):

- Register both providers **keyed by `SiaraAuthMode`**.
- `SiaraSessionProviderResolver` reads `IOptions<SiaraAuthOptions>.Mode` and resolves the keyed provider; returns `Result.WithFailure` if the configured mode isn't registered (fail-closed).
- The downloader depends on `ISiaraSessionProviderResolver`, not on a concrete provider — so switching is **pure config**, no redeploy of code.

```jsonc
// appsettings (Downloader process)
"Siara": {
  "AuthMode": "InteractiveLogin",          // or "SessionPassthrough" — the client's technical-legal choice
  "SessionTtlMinutes": 120,
  "RefreshIntervalMinutes": 20,
  "Passthrough": { "ContextEndpoint": "" },         // CDP/ws endpoint or storage-state import path
  "Interactive": { "MaxWaitForHumanSeconds": 300, "Headed": true }
}
```

---

## 7. Browser-agent capability gap (a real dependency)

Both strategies need session/context handling the agent doesn't have yet. **Add to `IBrowserAutomationAgent`** (or a focused companion port `IBrowserSessionContext` to keep the agent lean):

```csharp
Task<Result<string>> ExportStorageStateAsync(CancellationToken ct = default);          // capture authenticated context
Task<Result> LoadStorageStateAsync(string storageStateRef, CancellationToken ct = default); // restore it
Task<Result> ConnectToExistingContextAsync(string endpoint, CancellationToken ct = default); // passthrough attach
Task<Result<bool>> IsAuthenticatedAsync(string postLoginSelector, CancellationToken ct = default); // probe
```

Implemented in `PlaywrightBrowserAutomationAdapter` via Playwright `IBrowserContext.StorageStateAsync` / `NewContext(storageState:)` / `BrowserType.ConnectOverCDP`. **This is the one net-new infrastructure capability; size it into 1.1.**

---

## 8. Downloader integration seam (how 1.1 consumes this)

The real `SiaraDocumentDownloader : IDocumentDownloader` (built in **1.1**) depends on `ISiaraSessionProviderResolver` + `IBrowserAutomationAgent` + `SiaraNavigationTarget`:

```
DownloadAsync(documentId):
  provider = resolver.Resolve()                     // honors configured mode
  session  = provider.AcquireAsync(request)          // or reuse a cached warm session
  agent.LoadStorageStateAsync(session.StorageStateRef) / ConnectToExistingContext(...)
  siaraNavigationTarget.NavigateAsync(agent) → RetrieveDocumentsAsync(agent)
  agent.DownloadFileAsync(url) → bytes
  (1.2 loop calls provider.EnsureValidAsync between polls to keep the session warm)
```

> **Port wart to flag (not auth scope):** `IDocumentDownloader.DownloadAsync` returns `Task<byte[]>`, **not** `Result<byte[]>` — inconsistent with the codebase. Recommend evolving it to `Task<Result<byte[]>>` during 1.1 so auth/scrape failures surface as `Result`, not empty bytes. Decide in 1.1.

---

## 9. Security & technical-legal requirements (must-haves)

1. **No raw credential storage, ever** — neither mode accepts/persists username/password. `SiaraSession` invariant: no credential fields. Add an **architecture/unit test** asserting the session type and provider signatures expose no credential members.
2. **Session handle is a secret** — `StorageStateRef` must never be logged in full (log a hash/prefix). If persisted for keep-warm, encrypt at rest (DPAPI/Key Vault); prefer in-memory.
3. **Audit (ties to MVP A6)** — every `Acquire`/`EnsureValid`/`Release` emits an audit record: mode, `AcquiredBy`/process identity, session id, success — via `IAuditLogger`.
4. **Expiry + revoke** — honor `ExpiresAt`; provide `ReleaseAsync` to revoke; the loop must re-acquire on invalidation.
5. **Fail-closed** — resolver/providers return `Result.WithFailure` (never throw, never silently return empty) on any auth failure, so the downloader doesn't proceed unauthenticated.
6. **Documented as the client's control** — the chosen mode + rationale recorded per deployment (the ADR + runbook).

---

## 10. ITDD test plan (the "whole enchilada")

Follows ADR-005: an abstract `*Contract` in `ExxerCube.Prisma.Testing.Contracts`, a **mock blueprint** instance, a **stateful reference fake**, and **per-impl** inheritors. The arch guardrail (`Contract_Bases_Must_Be_Abstract_With_At_Least_One_Inheritor`) enforces pairing.

**Contract base** — `09 Testing/01 Abstractions/Testing/Contracts/SiaraSessionProviderContract.cs`:
```csharp
public abstract class SiaraSessionProviderContract
{
    protected abstract ISiaraSessionProvider CreateSut();         // impl supplies SUT
    protected abstract SiaraSessionRequest ValidRequest();        // a request that should succeed for this impl
    protected abstract void ArrangeExpiredSession(SiaraSession s);// hook to simulate expiry
}
```
**Contract behaviors (the [Fact]/[Theory] set every impl must satisfy):**
- `AcquireAsync_ValidRequest_ReturnsAuthenticatedSession` (Mode matches, non-empty StorageStateRef, no credential fields).
- `AcquireAsync_NullRequest_ReturnsFailure` (validate, never throw).
- `AcquireAsync_PreCancelledToken_ReturnsCancelled` (mandatory cancel contract).
- `EnsureValidAsync_ExpiredSession_RefreshesOrFailsClosed`.
- `EnsureValidAsync_PreCancelledToken_ReturnsCancelled`.
- `ReleaseAsync_IsIdempotent` (releasing twice ≠ throw).
- `Session_NeverExposesCredentials` (reflection assert: no username/password members).
- `Mode_MatchesProvider`.

**Inheritors:**
- `MockSiaraSessionProviderContractTests` (blueprint, in `Tests.Domain.Interfaces`) — over a contract-conforming NSubstitute mock via a `SiaraSessionProviderMockFactory`.
- `FakeSiaraSessionProviderContractTests` — over a hand-written **stateful in-memory `FakeSiaraSessionProvider`** (the reference fake: tracks acquired/released sessions, simulates expiry/refresh deterministically; also reusable by the downloader's tests so 1.1/1.2 don't need a live browser).
- `SessionPassthroughSiaraSessionProviderContractTests` (in `Tests.Infrastructure.BrowserAutomation`) — real impl over a **mocked `IBrowserAutomationAgent`** (assert it attaches + probes, never logs in).
- `InteractiveLoginSiaraSessionProviderContractTests` — real impl over a mocked agent (assert it waits for success + exports storage-state, never receives credentials).

**Resolver tests** — `SiaraSessionProviderResolver` selects by `SiaraAuthMode`; unknown/unregistered mode → fail-closed `Result.WithFailure`.

**Browser-agent capability tests** — contract/unit for `ExportStorageStateAsync`/`LoadStorageStateAsync`/`ConnectToExistingContextAsync`/`IsAuthenticatedAsync` on `PlaywrightBrowserAutomationAdapter` (Playwright-backed, may live in the existing BrowserAutomation.E2E tier).

**Security tests** — the no-credentials assertions (§9.1) as explicit tests; a log-scrubbing test that `StorageStateRef` isn't emitted in full.

**Mutation testing** — after green, scope Stryker over the two providers + resolver (deterministic logic: probing, expiry, selection); exclude the live-Playwright glue (same policy as other I/O adapters).

**ITDD authoring order:** contract base → mock blueprint + reference fake (both green, no impl yet) → impl A (passthrough) → impl B (interactive) → resolver → agent capability → DI → integration into 1.1 → mutation.

---

## 11. DI wiring + config

New extension in `Infrastructure.BrowserAutomation` (or a new `Infrastructure.Siara` if we want it isolated):
```csharp
public static IServiceCollection AddSiaraAuthentication(
    this IServiceCollection services, Action<SiaraAuthOptions>? configure = null)
{
    services.Configure(configure ?? (_ => {}));
    services.AddKeyedScoped<ISiaraSessionProvider, SessionPassthroughSiaraSessionProvider>(SiaraAuthMode.SessionPassthrough);
    services.AddKeyedScoped<ISiaraSessionProvider, InteractiveLoginSiaraSessionProvider>(SiaraAuthMode.InteractiveLogin);
    services.AddScoped<ISiaraSessionProviderResolver, SiaraSessionProviderResolver>();
    return services;
}
```
The **Downloader process** composition root (today `Orion.Worker/Program.cs`; the dedicated Downloader after the 1.4 split) calls `AddBrowserAutomationServices()` + `AddSiaraAuthentication(...)` and registers `IDocumentDownloader → SiaraDocumentDownloader` (replacing the stub at `Orion.Worker/Program.cs:17`).

---

## 12. New file / project layout

```
01 Core/Domain/
  Enum/SiaraAuthMode.cs
  ValueObjects/SiaraSession.cs
  ValueObjects/SiaraSessionRequest.cs
  Interfaces/ISiaraSessionProvider.cs
  Interfaces/ISiaraSessionProviderResolver.cs
  Interfaces/IBrowserAutomationAgent.cs        (+ 4 session methods, or new IBrowserSessionContext.cs)
02 Infrastructure/Infrastructure.BrowserAutomation/
  Siara/SessionPassthroughSiaraSessionProvider.cs
  Siara/InteractiveLoginSiaraSessionProvider.cs
  Siara/SiaraSessionProviderResolver.cs
  Siara/SiaraAuthOptions.cs
  DependencyInjection/ServiceCollectionExtensions.cs   (+ AddSiaraAuthentication)
  PlaywrightBrowserAutomationAdapter.cs                (+ session methods)
04 Services/Orion/Prisma.Orion.Ingestion/
  SiaraDocumentDownloader.cs                            (1.1 — consumes the resolver)
09 Testing/01 Abstractions/Testing/Contracts/
  SiaraSessionProviderContract.cs
09 Testing/.../Fakes/FakeSiaraSessionProvider.cs
08 Tests/01 Core/Tests.Domain.Interfaces/
  MockSiaraSessionProviderContractTests.cs
  FakeSiaraSessionProviderContractTests.cs
  SiaraSessionProviderMockFactory.cs
08 Tests/02 Infrastructure/Tests.Infrastructure.BrowserAutomation/
  SessionPassthroughSiaraSessionProviderContractTests.cs
  InteractiveLoginSiaraSessionProviderContractTests.cs
  SiaraSessionProviderResolverTests.cs
```

---

## 13. Task breakdown (ITDD order, sized)

| # | Task | Size | Dep |
|---|---|---|---|
| S0 | Author `ADR-010` (two strategies as a client-controlled technical-legal control) | S | — |
| S1 | Domain: enum + VOs + `ISiaraSessionProvider` + resolver port | S | — |
| S2 | Contract base `SiaraSessionProviderContract` + mock blueprint + `SiaraSessionProviderMockFactory` (green, no impl) | S | S1 |
| S3 | Stateful **reference fake** `FakeSiaraSessionProvider` + its contract inheritor (green) | S | S2 |
| S4 | Agent capability: add session methods to port + Playwright impl + tests | M | S1 |
| S5 | `SessionPassthroughSiaraSessionProvider` + contract inheritor | M | S2,S4 |
| S6 | `InteractiveLoginSiaraSessionProvider` + contract inheritor (incl. ISiaraLoginService decision) | M | S2,S4 |
| S7 | Resolver impl + selection tests + `AddSiaraAuthentication` DI + options/config | S | S5,S6 |
| S8 | Security tests (no-credentials, log-scrub) + audit wiring | S | S5,S6 |
| S9 | Mutation pass over providers + resolver | S | S5–S7 |
| → | **Hand-off to MVP-PATH 1.1**: `SiaraDocumentDownloader` consumes the resolver | — | S7 |

The reference fake (S3) **unblocks 1.1/1.2 test development** without a live browser — high leverage, do it early.

---

## 14. Open decisions / risks

1. **`ISiaraLoginService` fate** — reuse it inside InteractiveLogin (human-supplied, non-persisted values) **or** deprecate in favor of "human types into the page, we only detect success." *Recommend the latter for the cleanest no-credential story.*
2. **Passthrough handoff transport** — CDP `ConnectOverCDP` to a running browser, vs importing an exported storage-state file. Pick based on the client's environment (some won't allow CDP). May support both via `ExistingContextEndpoint` shape.
3. **`IDocumentDownloader` → `Result<byte[]>`** — evolve the port during 1.1 (recommended) or adapt at the boundary.
4. **Session persistence for keep-warm** — in-memory only (re-login on restart) vs encrypted persisted storage-state. MVP: in-memory + re-acquire; note for hardening.
5. **New module vs extend BrowserAutomation** — put Siara providers in `Infrastructure.BrowserAutomation/Siara/` (less ceremony) vs a new `Infrastructure.Siara` project (cleaner boundary for the security-sensitive code). *Lean: subfolder for MVP.*

---

## 15. One-paragraph summary

A single Domain port **`ISiaraSessionProvider`** with two config-selected implementations — **SessionPassthrough** (ride an external authenticated session) and **InteractiveLogin** (one-time human login, kept warm) — both producing a credential-free **`SiaraSession`** the scraper consumes. Selection is pure configuration via a fail-closed resolver, making it the client's technical-legal choice. The one net-new infrastructure dependency is **session/storage-state support on the browser agent**. Everything is authored ITDD-first (contract base + reference fake before impls), wired by a dedicated DI extension, audited per the A6 security model, and proven to **never store raw credentials**. This design is the seam MVP-PATH **1.1** plugs into.
