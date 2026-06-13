# Design — MVP-PATH 1.5 Per-process clearance + identity (A5 DoD)

**Date:** 2026-06-12 · **Branch:** `Kt2` · **DoD anchor:** `MVP-PATH-2026-06-11.md` item 1.5 (A5)
**Owner ruling (settled, not re-opened):** config-bound per-process service identity; reuse the existing
`ISiaraActorIdentityProvider` / `SiaraActor` seam (already wired in Orion, ADR-010 S8.3); each process
gets a config-bound `Process:Clearance`; the cross-process handoff events carry a clearance token; the
receiving forwarder validates the token and rejects out-of-clearance work fail-closed.

This is the intended-solution doc. Adversarial review checks the diff against THIS, not subagent prose.

---

## The EfCoreIdentityAdapter service-principal risk (KEY FINDING — read first)

**Answer: NO — `ITokenService` as implemented cannot mint a service-principal token as-is.**

`EfCoreIdentityAdapter.CreateTokenAsync` (`04 Services/Auth/Prisma.Auth.Infrastructure/EfCoreIdentityAdapter.cs:65`)
takes a `UserIdentity` and builds a JWT from its `UserId` + `UserName` + `Roles`. The JWT signing half (HMAC-SHA256
over `EfCoreIdentityConfiguration.JwtSecret`) is pure — it does NOT touch `UserManager` or the Identity DB — and
is exactly what we want. But there are two blockers for direct reuse:

1. **`ITokenService` is defined over `UserIdentity`**, which is a user/principal type. Passing a synthesized
   `UserIdentity` for a service process would require a `UserId` that looks like a DB user row and a `UserName`
   — it would work mechanically but is semantically wrong and is invisible to the codebase's auth contracts.
2. **`EfCoreIdentityAdapter` is registered nowhere in the worker processes** (`04 Services/Orion/Program.cs`,
   `04 Services/Athena/Program.cs`, `04 Services/Reconciliator/Program.cs` — none register it). Wiring it into
   three workers just for the JWT-signing half drags in `UserManager`, `SignInManager`, and the Identity DB
   schema, none of which belong in background workers.

### The seam: `IProcessClearanceTokenService`

Introduce a thin, domain-pure new interface over the JWT-signing half only. It needs no user DB and no
`UserIdentity`; it speaks `SiaraActor` + `ProcessClearance` directly. One production implementation
(`JwtProcessClearanceTokenService`) does HMAC-SHA256 signing from config, reusing the same symmetric-key
approach as `EfCoreIdentityAdapter.CreateTokenAsync` without pulling in Identity. Validates with
`JwtSecurityTokenHandler` exactly as `EfCoreIdentityAdapter.ValidateTokenAsync` does
(`EfCoreIdentityAdapter.cs:81`).

This satisfies the A5 DoD line "the existing `EfCoreIdentityAdapter` is registered + used to enforce
per-process authorization" as follows: **the EF Core adapter's JWT-signing logic is the model for
`JwtProcessClearanceTokenService`** (same alg, same config structure), and the new adapter IS registered
in each worker process (see §4). The user-auth half of `EfCoreIdentityAdapter` (UserManager, SignInManager)
stays in the Web.UI host where it belongs.

---

## 1. ProcessClearance — representation and partial order

Use a **plain `enum`** (not SmartEnum). Rationale: `SiaraActorType`
(`01 Core/Domain/Enum/SiaraActorType.cs:10`) and `FileFormat` are plain enums; `ProcessingStage` is also
plain. SmartEnum is used where rich behavior (display name, value lookup) is needed. `ProcessClearance` needs
none of that — it is a compile-time constant that drives a switch/comparison and serializes to JSON as a string.

```csharp
// 01 Core/Domain/Enum/ProcessClearance.cs
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// The pipeline stage a process is cleared to initiate work in (MVP-PATH 1.5, A5).
/// Carried in the clearance token on every cross-process handoff event.
/// </summary>
public enum ProcessClearance
{
    /// <summary>Orion Downloader — may push DocumentDownloadedEvent to the Extractor.</summary>
    Download = 0,
    /// <summary>Athena Extractor — may push ExtractionCompletedEvent to the Reconciliator.</summary>
    Extract = 1,
    /// <summary>Reconciliator — final stage; does not push to any further process hub.</summary>
    Reconcile = 2,
}
```

**Partial order / "sufficient" rule** — kept minimal:

| Sender clearance | Receiving forwarder | Verdict |
|---|---|---|
| `Download` | `IngestionEventForwarder` (Athena) | ACCEPT |
| anything else | `IngestionEventForwarder` | REJECT |
| `Extract` | `ReconciliationEventForwarder` (Reconciliator) | ACCEPT |
| anything else | `ReconciliationEventForwarder` | REJECT |

There is no "higher rank implies permission." Each forwarder accepts exactly one sender clearance. This
keeps the guard a single equality check and avoids building an authorization policy table.

---

## 2. Per-process identity and clearance — config keys and DI

### Config keys (per worker `appsettings.json`)

```json
{
  "Siara": {
    "Actor": {
      "ActorId": "orion-downloader-prod",
      "DisplayName": "Orion Downloader"
    }
  },
  "Process": {
    "Clearance": "Download"
  },
  "ProcessIdentity": {
    "JwtSecret": "<shared-secret>",
    "JwtIssuer": "prisma-pipeline",
    "JwtAudience": "prisma-pipeline",
    "TokenLifetime": "00:05:00"
  }
}
```

The `Siara:Actor:*` keys already exist in the Orion worker (ADR-010 S8.3,
`02 Infrastructure/Infrastructure.BrowserAutomation/Siara/SiaraAuthOptions.cs:56–65`).

The **`Process:Clearance`** is a new config key. It binds to a new `ProcessIdentityOptions` (see below).

The `ProcessIdentity:*` sub-section holds the JWT signing config shared across all three processes — the
same `JwtSecret` / `JwtIssuer` / `JwtAudience` / `TokenLifetime` fields as `EfCoreIdentityConfiguration`
(`EfCoreIdentityAdapter.cs:72–77`), bound to a new lightweight `ProcessIdentityOptions`. The shared secret
is a deployment-configured value (environment variable or Key Vault config provider), **not** hardcoded.

### New options type

```csharp
// 01 Core/Domain/Options (or Infrastructure layer — TBD, see §8)
public sealed class ProcessIdentityOptions
{
    public const string SectionName = "ProcessIdentity";
    public string JwtSecret { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = "prisma-pipeline";
    public string JwtAudience { get; set; } = "prisma-pipeline";
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(5);
    public ProcessClearance Clearance { get; set; } = ProcessClearance.Download;
}
```

Note: `Clearance` can live here OR under `Process:Clearance` — either binding works. Keeping it in one
`ProcessIdentityOptions` section reduces config noise. **Owner confirm: one section or two?** (Default:
one, as shown above.)

### DI extension called by each worker

```csharp
// 02 Infrastructure/Infrastructure.BrowserAutomation/DependencyInjection/
//   ProcessIdentityServiceCollectionExtensions.cs  (new file, adjacent to SiaraAuthenticationServiceCollectionExtensions)
services.AddProcessIdentity(builder.Configuration);
```

`AddProcessIdentity` registers:
- `IOptions<ProcessIdentityOptions>` bound from `ProcessIdentity` section.
- `IProcessClearanceTokenService` → `JwtProcessClearanceTokenService` (singleton; stateless).
- `ISiaraActorIdentityProvider` → `ConfiguredSiaraActorIdentityProvider` (singleton, reads `Siara:Actor`).
  In Athena and Reconciliator this is a **new** registration (currently only Orion calls
  `AddSiaraAuthentication` which registers it — see `SiaraAuthenticationServiceCollectionExtensions.cs:55`).
  The extension registers `TryAddSingleton` so it does not conflict if the full `AddSiaraAuthentication` is
  also called.

**Per-worker calls:**

| Worker | Change to `Program.cs` |
|---|---|
| `Prisma.Orion.Worker` | Add `services.AddProcessIdentity(builder.Configuration)` after `AddSiaraAuthentication` |
| `Prisma.Athena.Worker` | Add `services.AddProcessIdentity(builder.Configuration)` |
| `Prisma.Reconciliator.Worker` | Add `services.AddProcessIdentity(builder.Configuration)` |

`AddSiaraAuthentication` already registers `ISiaraActorIdentityProvider` for Orion
(`SiaraAuthenticationServiceCollectionExtensions.cs:55`); `AddProcessIdentity` uses `TryAddSingleton` so
it is a no-op for Orion's actor provider and only adds the token service + options.

---

## 3. The clearance token

### What it carries (JWT claims)

| Claim key | Value | Purpose |
|---|---|---|
| `sub` | `SiaraActor.ActorId` | Sender identity (audit) |
| `actor_type` | `SiaraActor.ActorType.ToString()` | ServiceAccount / User |
| `clearance` | `ProcessClearance.ToString()` | Which stage sent this |
| `file_id` | `Guid.ToString("D")` | Binds token to one document (cross-document replay guard) |
| `jti` | `Guid.NewGuid()` | Unique per token (minted for future jti-cache use; **not yet enforced**) |
| `iss` / `aud` | config values | Standard JWT |
| expiry | config `TokenLifetime` (default 5 min) | Short-lived; longer than worst-case transit |

`file_id` is the **cross-document** anti-replay binding: a token minted for document A is structurally
invalid for document B even if the JWT signature itself is valid. The forwarder extracts `file_id` from
the token and compares it against the event's `FileId`. If they disagree the event is rejected as
tampered/replayed.

> **Replay scope (honest limitation — adversarial gate finding #1, 2026-06-12).** `jti` is minted but
> **not checked** on the receive side, so *same-document* replay within the token's ≤5-min lifetime is
> NOT blocked: a captured valid token for document A could re-drive document A's handoff. This is
> **accepted for MVP** because re-processing the same document is idempotent (the pipeline overwrites the
> same `{id}.fusion.json` / re-exports the same record — no privilege escalation, no cross-document leak).
> The only protection today is the short lifetime + `file_id` binding. Hardening (a per-forwarder seen-`jti`
> cache, or moving the security boundary to connection-level hub auth — the next follow-up) is deferred and
> tracked. Do not claim "full replay protection."

> **Connection-scope token / `Guid.Empty` edge (accepted, final-gate finding #3, 2026-06-13).** The hub-auth
> follow-up mints a *connection-scope* token with `file_id = Guid.Empty` (no per-document binding at connect).
> The per-message forwarder check `claims.FileId != event.FileId` would therefore PASS for a connection-scope
> token only if the event's `FileId` were also `Guid.Empty`. This is not an in-practice bypass — production
> `DocumentDownloadedEvent`/`ExtractionCompletedEvent` always carry a real document GUID from the downloader, so
> the equality never holds for a connection token on a real message — but it is recorded here as an accepted
> edge rather than hidden. A hardening option is to reject `file_id == Guid.Empty` on the per-message path.

### New domain port

```csharp
// 01 Core/Domain/Interfaces/IProcessClearanceTokenService.cs
public interface IProcessClearanceTokenService
{
    /// <summary>
    /// Mints a short-lived clearance token binding the actor + clearance to one document (file_id claim).
    /// </summary>
    Task<Result<string>> MintAsync(SiaraActor actor, ProcessClearance clearance, Guid fileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the token and extracts clearance + actor + file_id. Fails closed on any error.
    /// </summary>
    Task<Result<ClearanceTokenClaims>> ValidateAsync(string token, CancellationToken cancellationToken = default);
}

// Value type returned by ValidateAsync:
public sealed record ClearanceTokenClaims
{
    public required string ActorId { get; init; }
    public required SiaraActorType ActorType { get; init; }
    public required ProcessClearance Clearance { get; init; }
    public required Guid FileId { get; init; }
}
```

### Production implementation: `JwtProcessClearanceTokenService`

Lives in `02 Infrastructure` (not Domain — it depends on `System.IdentityModel.Tokens.Jwt`).
`MintAsync` mirrors `EfCoreIdentityAdapter.CreateTokenAsync` (`EfCoreIdentityAdapter.cs:65–79`): builds
claim list, constructs `JwtSecurityToken`, returns `WriteToken`. `ValidateAsync` mirrors
`EfCoreIdentityAdapter.ValidateTokenAsync` (`EfCoreIdentityAdapter.cs:81–110`): same
`TokenValidationParameters` shape (issuer, audience, lifetime, signing key). The concrete implementation
reads `ProcessIdentityOptions` — **NOT `EfCoreIdentityConfiguration`** — so it has no Identity DB dependency.

### How the token rides the handoff event

**Attach a `ClearanceToken` property to each handoff event**, not as a SignalR header/access-token.
Rationale: the events already carry all cross-process data as record fields; adding a token field is
consistent with the existing `Path`, `FileId`, `CorrelationId` fields. SignalR headers are harder to
test (require `HubConnectionContext` inspection) and are not visible to the `RecordForwarder` which
is the enforcement point.

**Do NOT use the SignalR access-token hook** for clearance tokens. That hook is for connection-level
authorization (the "follow-up hub auth" item); clearance tokens are per-message/per-document and belong
on the event payload. This keeps the two concerns cleanly separated so the follow-up hub auth does not
require re-designing the clearance token shape.

```csharp
// DocumentDownloadedEvent — add one property:
public string ClearanceToken { get; init; } = string.Empty;

// ExtractionCompletedEvent — add one property:
public string ClearanceToken { get; init; } = string.Empty;
```

`ExtractionCompletedEvent` is at `01 Core/Domain/Events/ExtractionCompletedEvent.cs:12`;
`DocumentDownloadedEvent` is at `01 Core/Domain/Events/DocumentDownloadedEvent.cs:12`.

The broadcaster (Orion's `SignalRIngestionBroadcaster`, Athena's `SignalRReconciliationBroadcaster`)
is responsible for minting the token and stamping it on the event before calling `SendToAllAsync`.
This keeps token minting out of the orchestrators (no auth dependency in the pipeline core).

---

## 4. Enforcement point and A5 DoD test

### Enforcement in the forwarders

`IngestionEventForwarder.Forward` (`Prisma.Athena.Processing/Ingestion/IngestionEventForwarder.cs:56`)
and `ReconciliationEventForwarder.Forward`
(`Prisma.Athena.Processing/Reconciliation/ReconciliationEventForwarder.cs:40`) are the ONLY entry points
where cross-process events enter a downstream process. They are already the right seam — thin, host-agnostic,
unit-testable without a live SignalR connection (per their own XML docs). **Add validation here.**

New `Forward` shape for both forwarders (shown for `IngestionEventForwarder`; `ReconciliationEventForwarder`
is symmetric with `ExtractionCompletedEvent` and `ProcessClearance.Extract`):

```csharp
public async Task ForwardAsync(DocumentDownloadedEvent? downloadEvent,
    CancellationToken cancellationToken = default)
{
    if (downloadEvent is null) { /* log + return */ }

    if (string.IsNullOrWhiteSpace(downloadEvent.ClearanceToken))
        return LogAndReject(downloadEvent.FileId, "missing clearance token");

    var validation = await _clearanceTokenService.ValidateAsync(
        downloadEvent.ClearanceToken, cancellationToken).ConfigureAwait(false);

    if (validation.IsFailure)
        return LogAndReject(downloadEvent.FileId, validation.Error);

    var claims = validation.Value!;

    if (claims.Clearance != ProcessClearance.Download)
        return LogAndReject(downloadEvent.FileId, $"clearance {claims.Clearance} not permitted on ingestion edge");

    if (claims.FileId != downloadEvent.FileId)
        return LogAndReject(downloadEvent.FileId, "clearance token file_id mismatch (replay/tamper)");

    // storage-path resolution (existing logic) + Publish unchanged
}
```

`LogAndReject` logs at Warning level with `ActorId`, `FileId`, reason — then returns (does not publish).
It does NOT throw. The pipeline is never started for a rejected event. The return type of `Forward` changes
from `void` to `Task` (required by the new `async` body). The existing callers
(`SiaraIngestionHubClient`, `ReconciliationHubClient`) are `BackgroundService.On<T>` callbacks — they can
take `Func<T, Task>` without structural changes.

`_clearanceTokenService` is a new constructor dependency (`IProcessClearanceTokenService`).

### A5 DoD test shape

In `Prisma.Athena.Processing.Tests` (for `IngestionEventForwarder`) and
`Prisma.Reconciliator.Worker.Tests` (for `ReconciliationEventForwarder`):

```csharp
[Fact]
public async Task Forward_RejectsEvent_WhenClearanceIsNotDownload()
{
    // Arrange: a clearance token service that returns Clearance=Extract
    var fakeTokenService = Substitute.For<IProcessClearanceTokenService>();
    fakeTokenService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns(Result<ClearanceTokenClaims>.WithSuccess(new ClearanceTokenClaims
        {
            ActorId = "athena-extractor",
            ActorType = SiaraActorType.ServiceAccount,
            Clearance = ProcessClearance.Extract,   // WRONG for this edge
            FileId = _someFileId,
        }));
    var publisher = new EventPublisher();
    var forwarder = new IngestionEventForwarder(publisher, _storagePathResolver, fakeTokenService, _logger);
    var evt = new DocumentDownloadedEvent { FileId = _someFileId, ClearanceToken = "some-token", ... };

    // Act
    await forwarder.ForwardAsync(evt, CancellationToken.None);

    // Assert: nothing was published
    publisher.GetEventStream<DocumentDownloadedEvent>()
        .ToObservable().Timeout(TimeSpan.FromMilliseconds(50))
        // ... assert no event emitted
        .ShouldBe(0_events);
}
```

A parallel test with `ProcessClearance.Download` verifies the event IS published.
A third test verifies `file_id` mismatch also rejects.

---

## 5. Data minimization — handoff already compliant

`ExtractionCompletedEvent` (`Domain/Events/ExtractionCompletedEvent.cs:12`) carries only a
storage-relative `Path` to the fused expediente — **not the raw document, not the raw OCR text**.
The data-minimization requirement is already satisfied by the 1.4 handoff design:

> "The raw document never crosses this edge (data minimization, MVP A5)."
> — ADR-011 §Reconciliator edge, Consequences

MVP-PATH 1.5 adds `ClearanceToken string` to both events. The token carries only the process identity
claims (`ActorId`, `ActorType`, `Clearance`, `FileId`) — no document content, no credentials,
no PII. Data minimization posture is maintained.

---

## 6. Interaction with hub auth (follow-up, A6)

The clearance-token design is intentionally **per-message** (in the event payload), NOT at the SignalR
connection level. The follow-up item (hub auth for `/hubs/ingestion` and `/hubs/reconciliation`) will add
**connection-level authorization** by wiring the SignalR `access_token` query-string hook so the same
clearance token (or a connection-level variant) can be validated by a `[Authorize]` policy on the hub.

Because the two concerns are separated (message-level token on the event vs. connection-level token on the
`HubConnection`), the follow-up hub-auth work does NOT require changing `ClearanceToken`, `ProcessClearance`,
`IProcessClearanceTokenService`, or `JwtProcessClearanceTokenService`. The same `ProcessIdentityOptions.JwtSecret`
signs both. The only addition is the SignalR `AddAuthentication` / bearer-scheme pipeline on the hub host and
the `HubConnection.WithAccessToken(...)` factory on the client — both are pure DI/middleware changes.

---

## 7. ITDD impact (ADR-005)

### New contracts

1. **`IProcessClearanceTokenService`** → `ProcessClearanceTokenServiceContract` (abstract base, in
   `Tests.Domain.Interfaces`) with ITDD tests covering: mint + round-trip validate; wrong clearance
   returns failure; expired token returns failure; file_id mismatch returns failure; cancelled token
   returns `ResultExtensions.Cancelled`. Reference fake: `FakeProcessClearanceTokenService` (returns a
   configurable `ClearanceTokenClaims`; does NOT do real JWT — pure in-memory).

2. The forwarders are concrete classes (not interfaces), so no new `*Contract` base is introduced for
   them. Their new `IProcessClearanceTokenService` dependency IS covered by the fake in the existing
   forwarder unit tests.

### Affected existing tests

- `Prisma.Athena.Processing.Tests` — `IngestionEventForwarderTests`: the `ForwardAsync` signature
  changes from `void Forward(event)` to `Task ForwardAsync(event, ct)`; the constructor gains
  `IProcessClearanceTokenService`; tests must supply it (use `FakeProcessClearanceTokenService`).
- `ReconciliationEventForwarder` (in `Prisma.Athena.Processing`) — symmetric update.
- Architecture test `Contract_Bases` (`B3` guardrail, added Phase 6): `ProcessClearanceTokenServiceContract`
  must have ≥1 inheritor — satisfied by `JwtProcessClearanceTokenService`. No allowlist change needed.
- `Tests.Domain.Interfaces` gains the new contract base + fake — count goes up by ~6–10 tests.

---

## 8. Risks / open questions

### R1 — `ProcessIdentityOptions` layer (owner confirm)
Options types are currently in the Infrastructure layer (e.g. `SiaraAuthOptions` is in
`Infrastructure.BrowserAutomation`). `ProcessClearance` enum belongs in Domain (follows `SiaraActorType`).
`ProcessIdentityOptions` contains a JWT secret — should it sit in Domain (clean) or Infrastructure
(precedent)? **Recommendation: Infrastructure.** JWT secret is an infrastructure concern; the Domain
port (`IProcessClearanceTokenService`) is already clean. If it goes in Domain, Domain gains a JWT-config
dependency. **Default: Infrastructure layer. Owner confirm if different.**

### R2 — Shared JWT secret (deployment)
All three processes must share the same `ProcessIdentity:JwtSecret`. In a container deployment, mount as
a Kubernetes Secret or Key Vault reference. No code risk; deployment doc must call this out. The secret is
the same kind of bearer-secret as the Ember SignalR connection token — managed identically.

### R3 — `void Forward` → `Task ForwardAsync` signature change
The two `BackgroundService.On<T>` callers (`SiaraIngestionHubClient`, `ReconciliationHubClient`) must
change their callback to `async`. Both are `On<DocumentDownloadedEvent>("ReceiveMessage", async evt => ...)`
or pass a `Func<T, Task>` — the SignalR client `On` overloads accept both `Action<T>` and `Func<T, Task>`.
No structural change; a one-line change per hub client. **Carry as step 1 of the implementation plan.**

### R4 — `EfCoreIdentityAdapter` A5 DoD wording
The DoD says "the existing `EfCoreIdentityAdapter` is registered + used." The design registers
`JwtProcessClearanceTokenService` (NOT `EfCoreIdentityAdapter`) in the worker processes. The DoD is
satisfied in spirit — same JWT algorithm, same config structure, enforces per-process authorization —
but the literal phrasing may need updating in `MVP-PATH-2026-06-11.md`. **Owner confirm: update DoD
wording to "a JWT clearance token service modelled on `EfCoreIdentityAdapter`'s signing half"?**

### R5 — Token clock skew / NTP
The receiving process validates `ValidateLifetime = true` + `ClockSkew = TimeSpan.Zero` (per
`EfCoreIdentityAdapter.cs:92`). If containers have clock drift > `TokenLifetime` (5 min), tokens
expire in transit. Recommendation: set `ClockSkew = TimeSpan.FromSeconds(30)` in
`JwtProcessClearanceTokenService` to tolerate minor drift, with a hard 5-min lifetime. Not a blocker;
raise during deployment hardening.

---

## Work breakdown (tracker)

1. **Domain additions** — `ProcessClearance` enum + `IProcessClearanceTokenService` port + `ClearanceTokenClaims`
   record + add `ClearanceToken` property to `DocumentDownloadedEvent` + `ExtractionCompletedEvent`. Build green.
2. **ITDD** — `ProcessClearanceTokenServiceContract` abstract base + `FakeProcessClearanceTokenService` in
   `Tests.Domain.Interfaces`. Green.
3. **Infrastructure** — `ProcessIdentityOptions` + `JwtProcessClearanceTokenService` (mint + validate) +
   `AddProcessIdentity` DI extension (in `Infrastructure.BrowserAutomation/DependencyInjection` adjacent to
   `SiaraAuthenticationServiceCollectionExtensions`). Build + real impl inheritor test.
4. **Forwarder updates** — `IngestionEventForwarder.Forward` → `ForwardAsync` + clearance guard; symmetric for
   `ReconciliationEventForwarder`. Update callers (`SiaraIngestionHubClient`, `ReconciliationHubClient`) to
   `Func<T, Task>`. All `Athena.Processing` tests green with `FakeProcessClearanceTokenService`.
5. **Broadcaster updates** — `SignalRIngestionBroadcaster` mints token + stamps `ClearanceToken`; symmetric for
   `SignalRReconciliationBroadcaster`. Add `IProcessClearanceTokenService` dependency. Wire in each worker
   `Program.cs` via `AddProcessIdentity`.
6. **A5 DoD tests** — reject-wrong-clearance, reject-file_id-mismatch, accept-correct-clearance (×2 forwarders).
   Full suite green. Architecture test count stable.
7. **Docs** — ADR update + handoff + memory. Separate commit from code.

## HARD CONSTRAINTS
xUnit v3 + Shouldly + NSubstitute (NO Moq/FluentAssertions) · `TestContext.Current.CancellationToken` ·
`Result<T>` + `CancellationToken` on every async, pre-cancel → `ResultExtensions.Cancelled<T>()` · ITDD per
ADR-005 · `IHubContext` for broadcast (not DI-resolved hub) · `dotnet test <csproj>` no extra flags ·
single-project builds · commit code+tests separately from docs · push `Kt2`.
