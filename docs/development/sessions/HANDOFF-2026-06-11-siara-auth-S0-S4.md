# Handoff — SIARA Authentication Seam (S0–S4 + decisions + sim) → next: S5

**Date:** 2026-06-11 · **Branch:** `Kt2` · **Author:** prior agent session
**Scope of this session:** MVP-PATH Workstream 1.1 prerequisite — the SIARA auth seam (ADR-010).
**Supersedes for SIARA-auth detail:** `HANDOFF-2026-06-11-mvp-workstream1.md` (still the WS1 overview).

---

## TL;DR

The SIARA authentication **seam** is built ITDD-first through **S4**, the **three auth modes are
decided**, an **adversarial liability review** is on file, and the **simulator is now cookie-faithful**
(so the storage-state flow is E2E-testable). The next concrete work is the three **providers**
(S5/S6/S6b), then the **resolver + DI** (S7), then **security/mutation** (S8/S9), then hand off to the
real downloader (MVP-PATH **1.1**).

**Everything builds 0/0; Tests.Domain.Interfaces 286/286; the simulator cookie flow is runtime-verified.**

### Commits this session (all on `Kt2`, pushed: `bb02aae..743a1ba`)
| Commit | What |
|---|---|
| `16a108b` | **S0–S3**: ADR-010 + Domain port `ISiaraSessionProvider` + VOs + `SiaraAuthMode` + resolver port + contract base + mock blueprint + stateful `FakeSiaraSessionProvider` |
| `dc841f4` | **S4**: companion port `IBrowserSessionContext` + Playwright impl (storage-state/CDP) + contract + real-browser E2E round-trip |
| `349df64` | **Decisions settled** + ADR-010 revised + `SiaraAuthMode.AutomatedLogin` added |
| `da71738` | **Adversarial liability review** of ADR-010 (folded P1–P8 + title/bearer-secret fixes) |
| `743a1ba` | **Simulator** demo auto-login → real cookie session + cred enforcement + lockout + Spanish errors |

---

## Canonical docs (read these first)
1. `docs/architecture/adr/ADR-010-SIARA-Authentication-Strategies.md` — the governing decision (3 modes, **§ Legal preconditions & residual risk P1–P8**).
2. `docs/architecture/adr/ADR-010-adversarial-review-2026-06-11.md` — the full graded liability findings.
3. `docs/planning/gap-analysis/SIARA-AUTH-DESIGN-2026-06.md` — the detailed ITDD/DI/config design + task list S0–S9 (note: §14 open items are now **resolved** in ADR-010; a third mode + S6b were added).
4. `docs/architecture/adr/ADR-005-itdd-contract-tests-injected-sut.md` — the contract-test shape every new port must follow.

---

## The three auth modes (settled, client-config-selected, none persists raw credentials)
| `SiaraAuthMode` | Mechanism | Status |
|---|---|---|
| `SessionPassthrough` | Ride an externally-authenticated context — **storage-state import (default)** or **CDP attach (opt-in)** via `Siara:Passthrough:Transport` | port + capability ready (S5 builds the provider) |
| `InteractiveLogin` | Human logs in once on the real page; capture + keep warm | port + capability ready (S6) |
| `AutomatedLogin` | **NEW.** Unattended; creds from a client vault via `ISiaraCredentialSource`, used transiently, **never persisted** | enum landed; S6b builds the provider + the credential port |

**Why three:** real SIARA (`siara.cnbv.gob.mx`) is a username/password web form + session cookie + lockout, **no API, no access until deployment**. Human/external modes are robust to whatever auth it presents; `AutomatedLogin` is the only mode that makes the unattended 24/7 watch loop viable.

---

## What exists now (file map)

**Domain (`01 Core/Domain/`)**
- `Enum/SiaraAuthMode.cs` — `SessionPassthrough=0, InteractiveLogin=1, AutomatedLogin=2`.
- `ValueObjects/SiaraSession.cs` — credential-free session handle (`SessionId, Mode, StorageStateRef, ExpiresAt?, AcquiredBy?`). **NOTE (audit gap, see P-set):** `AcquiredBy` is an optional `string?` — must become mandatory/trustworthy.
- `ValueObjects/SiaraSessionRequest.cs` — `ExistingContextEndpoint?, MaxWaitForHuman?, RequestedBy?`.
- `Interfaces/ISiaraSessionProvider.cs` — `Mode; AcquireAsync; EnsureValidAsync; ReleaseAsync` (Result<T> + CancellationToken).
- `Interfaces/ISiaraSessionProviderResolver.cs` — `Result<ISiaraSessionProvider> Resolve()` (fail-closed).
- `Interfaces/IBrowserSessionContext.cs` — `ExportStorageStateAsync / LoadStorageStateAsync / ConnectToExistingContextAsync / IsAuthenticatedAsync`.

**Infrastructure (`02 Infrastructure/Infrastructure.BrowserAutomation/`)**
- `PlaywrightBrowserAutomationAdapter.cs` — now implements **both** `IBrowserAutomationAgent` and `IBrowserSessionContext` (tracks `IPlaywright`; export = `_page.Context.StorageStateAsync()`, restore = `NewContextAsync(StorageState=ref)`, CDP = `ConnectOverCDPAsync`, probe = `QuerySelectorAsync`).
- `DependencyInjection/ServiceCollectionExtensions.cs` — registers `IBrowserSessionContext` as the **same scoped adapter instance**. Still registers raw-cred `ISiaraLoginService` here (see P6 — needs isolation).

**Testing (`09 Testing/01 Abstractions/Testing/Contracts/`)** — contract bases + factories + the reusable fake:
- `SiaraSessionProviderContract.cs` + `SiaraSessionProviderMockFactory.cs`
- `FakeSiaraSessionProvider.cs` — **stateful reference fake; use it to unblock 1.1/1.2 tests without a live browser.**
- `BrowserSessionContextContract.cs` + `BrowserSessionContextMockFactory.cs`

**Tests** — `08 Tests/01 Core/Tests.Domain.Interfaces/` (Mock + Fake blueprints) · `08 Tests/05 System/Tests.Infrastructure.BrowserAutomation.E2E/` (real-adapter contract inheritor + real-browser round-trip; this project now references Testing.Contracts).

**Simulator (`tools/Siara.Simulator/`)** — cookie auth (`siara_session`), `SiaraCredentialValidator` (lockout + Spanish errors), `SiaraAuthOptions` (`Auth` section, default `BANAMEX`/`password123`). **Verified:** anon → /login; correct creds → cookie + dashboard; wrong → Spanish error; 5 fails → lockout.

---

## NEXT TASKS (in order)

> Each new Domain port gets a `*Contract` base + ≥1 inheritor (arch enforced). Each impl-less port is **allowlisted** in `All_Domain_Interfaces_Should_Have_At_Least_One_Implementation` (`HexagonalArchitectureTests.cs` ~line 842) until its adapter lands — **remove from the allowlist as you implement each.**

### S5 — `SessionPassthroughSiaraSessionProvider`  *(start here)*
- New `Infrastructure.BrowserAutomation/Siara/SessionPassthroughSiaraSessionProvider.cs` implementing `ISiaraSessionProvider`, `Mode => SessionPassthrough`.
- `AcquireAsync`: read transport from config (`StorageState` → `IBrowserSessionContext.LoadStorageStateAsync(ref)`; `Cdp` → `ConnectToExistingContextAsync(endpoint)`), then `IsAuthenticatedAsync(postLoginSelector)` to confirm, return a `SiaraSession`. **Never logs in.**
- `EnsureValidAsync`: re-probe; fail closed if the external session died.
- ITDD: `SessionPassthroughSiaraSessionProviderContractTests : SiaraSessionProviderContract` over a **mocked `IBrowserSessionContext`** (in the BrowserAutomation.E2E project or a new unit-tier test project — see gotcha #5).
- Remove `ISiaraSessionProvider` from the arch allowlist once this + S6 land… (or keep until the resolver/all providers exist — your call; the resolver `ISiaraSessionProviderResolver` is separate and stays allowlisted until S7).

### S6 — `InteractiveLoginSiaraSessionProvider`
- Launch headed, wait up to `MaxWaitForHuman` for the post-login selector, then `ExportStorageStateAsync` → `SiaraSession`. `EnsureValidAsync` re-hydrates + probes; typed failure on expiry so the loop can request a fresh one-time login.

### S6b — `AutomatedLoginSiaraSessionProvider` + `ISiaraCredentialSource`  ⚠️ **liability-gated**
- New Domain port `ISiaraCredentialSource` (yields creds from a client secret store). **Honor P5:** creds as cleared `char[]` (not `string`), never logged/serialized/`ToString`; no-leak test must cover the login path, not just `SiaraSession`.
- Provider: get creds transiently → drive the form via `ISiaraLoginService` → `ExportStorageStateAsync` → **discard creds**.
- **Honor P3 (CRITICAL):** lockout/anomaly detection + exponential backoff + circuit-breaker + **stop-and-alert-after-N** — an unattended retry storm can lock the bank's real SIARA account → missed legal deadlines. Build this in, do not defer.
- **Honor P6:** structurally isolate `ISiaraLoginService` from *persisted* creds (today it's just registered in the general browser DI).

### S7 — Resolver + DI + config
- `SiaraSessionProviderResolver` (fail-closed) reading `IOptions<SiaraAuthOptions>.AuthMode`; `AddSiaraAuthentication(...)` keyed-by-`SiaraAuthMode` registration of all three providers; `SiaraAuthOptions` (`AuthMode`, `Passthrough.Transport`+`Ref`, `Interactive.MaxWaitForHuman`, ttl/refresh). Remove the resolver from the arch allowlist.

### S8 / S9 — Security + mutation
- S8: the no-credential reflection tests on the credential path (not just `SiaraSession`), log-scrub test, audit wiring (**P-set audit:** mandatory trustworthy actor identity, per-document non-repudiation, audit every `ISiaraCredentialSource` read, immutable+retained trail). S9: Stryker over providers + resolver, exclude live-Playwright glue.

### → Handoff to MVP-PATH 1.1
- `SiaraDocumentDownloader : IDocumentDownloader` consumes `ISiaraSessionProviderResolver`. **Evolve `IDocumentDownloader.DownloadAsync` `Task<byte[]>` → `Task<Result<byte[]>>`** during 1.1. Replace `StubDocumentDownloader` at `Orion.Worker/Program.cs:17`. E2E it against the now-cookie-faithful simulator.

---

## HARD CONSTRAINTS (verify in CLAUDE.md + ADR-010)
- **No raw credential STORAGE, ever.** `SiaraSession` exposes no credential members (reflection test). `AutomatedLogin` creds are vault-sourced + transient + discarded.
- **Liability preconditions P1–P8 (ADR-010).** P1/P2/P4 are **legal questions for counsel** (recorded, not resolved). P3/P5/P6/P7/P8 are **engineering preconditions** you must satisfy in the relevant task (esp. P3 lockout/backoff before S6b ships).
- **ITDD (ADR-005):** every port → `*Contract` base (injected-Sut default) + mock blueprint + ≥1 inheritor.
- **Result<T> + CancellationToken** on every async method; pre-cancelled → `ResultExtensions.Cancelled<T>()`; the pre-cancelled-token contract test is mandatory.
- **Tests:** xUnit v3 + MTP. `dotnet test <project.csproj>` (NO extra flags like `--nologo` — they break MTP, "Zero tests ran"). Build single projects when possible.

---

## GOTCHAS / lessons from this session
1. `Result<string>.WithFailure(msg, default, ex)` is **CS0121 ambiguous** (string default vs IEnumerable<string> errors overload) → use the 1-arg `WithFailure(msg)` for `Result<string>`/`Result<bool>`.
2. **`.gitignore:137 *.e2e`** catches the `.E2E` test **directory** on Windows (case-insensitive) → new `.cs` files there are silently ignored. `git add -f` them (existing E2E tests are force-tracked too). Worth a real `.gitignore` fix.
3. **Blazor cookie sign-in:** the login page must be **static SSR** (no `@rendermode`) so `HttpContext.SignInAsync` can write the Set-Cookie header — an interactive circuit has no HttpContext to sign into. Needs `using Microsoft.AspNetCore.Authentication;` (the Cookies namespace alone is insufficient for `SignInAsync`/`SignOutAsync`).
4. `dotnet run` **ignores `--urls`** when `launchSettings.json` is present (the sim binds 5001/5002). To test, hit `https://localhost:5002` with `-SkipCertificateCheck`, or delete/override launchSettings.
5. **No unit-tier BrowserAutomation test project exists** — the adapter's test home is `Tests.Infrastructure.BrowserAutomation.E2E` (System tier). Contract tests that only hit guard/cancel paths run there fine without a browser. Consider adding a unit-tier project if you want the provider contract tests outside the E2E tier.
6. **Pre-existing arch-test fragility (NOT a regression):** `Contract_Bases_Must_Be_Abstract_With_At_Least_One_Inheritor` fails on this box because `FindAssembliesByPattern` can't `LoadFrom` the `Testing.Contracts.dll` copies (it false-flagged the established `FieldMergeStrategyContract` before any change). `BuildArtifacts\Prisma\bin` accumulates stale duplicate DLLs (40 copies were found, incl one from Dec-2025). The new contracts ARE compliant (abstract + passing inheritors). Don't chase it as your bug; flag for a separate infra fix.
7. PowerShell `$(x.Length>0)` is a **file redirect** (`>`), not a comparison — use `-gt`. (It created a stray `docs/0`, amended out.)

---

## Verify the current state
```
dotnet build "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"                                   # 0/0
dotnet test  "Prisma/Code/Src/CSharp/08 Tests/01 Core/Tests.Domain.Interfaces/ExxerCube.Prisma.Tests.Domain.Interfaces.csproj"   # 286/286
# Simulator: cd tools/Siara.Simulator && dotnet run  → https://localhost:5002 (BANAMEX / password123)
```
Build artifacts: `E:\Dynamic\ExxerCubeBanamex\BuildArtifacts\Prisma\`.
