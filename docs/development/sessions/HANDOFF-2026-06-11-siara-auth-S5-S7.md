# Handoff — SIARA Authentication Seam (S5–S7 done) → next: S8 / S9

**Date:** 2026-06-11 · **Branch:** `Kt2` · **Author:** prior agent session
**Scope of this session:** MVP-PATH Workstream 1.1 prerequisite — the SIARA auth seam (ADR-010).
**Supersedes for SIARA-auth detail:** `HANDOFF-2026-06-11-siara-auth-S0-S4.md`.

---

## TL;DR

The SIARA authentication **seam is functionally complete**: all three credential-handling strategies are
built, ITDD-first, behind one fail-closed resolver selected by configuration. What remains is the
**security/audit hardening (S8)** and **mutation testing (S9)**, then the hand-off to the real downloader
(MVP-PATH **1.1**).

**Everything builds 0/0. Tests: BrowserAutomation 101/101 · Domain.Interfaces 301/301 · Architecture 20/20.**

### Commits this session (all on `Kt2`, pushed: `81f07d7..41e8113`)
| Commit | What |
|---|---|
| `ba256dc` | **S6**: `InteractiveLoginSiaraSessionProvider` (human logs in once; capture storage-state) |
| `1af9826` | **S6b**: `AutomatedLoginSiaraSessionProvider` + `ISiaraCredentialSource` + `SiaraCredential` + **P3 `SiaraLoginCircuitBreaker`** |
| `41e8113` | **S7**: fail-closed `SiaraSessionProviderResolver` + `AddSiaraAuthentication` keyed DI |

(S5 `SessionPassthroughSiaraSessionProvider` landed earlier at `89c7c5e`.)

---

## The seam as it stands (all three modes selectable by config)

| `SiaraAuthMode` | Provider | Notes |
|---|---|---|
| `SessionPassthrough` (default) | `SessionPassthroughSiaraSessionProvider` (S5) | rides an external context (storage-state import / CDP); never logs in |
| `InteractiveLogin` | `InteractiveLoginSiaraSessionProvider` (S6) | headed; human logs in once on SIARA's page; capture + keep warm |
| `AutomatedLogin` ⚠️ | `AutomatedLoginSiaraSessionProvider` (S6b) | unattended; vault creds via `ISiaraCredentialSource`, transient + discarded; **P3 circuit-breaker gated** |

Selection: `ISiaraSessionProviderResolver` (S7) reads `IOptions<SiaraAuthOptions>.AuthMode` and returns the
keyed provider, **fail-closed** on an unknown/unregistered mode. Wire with `AddSiaraAuthentication(config)`
alongside `AddBrowserAutomationServices`.

---

## File map (what exists now)

**Domain (`01 Core/Domain/`)**
- `Enum/SiaraAuthMode.cs` — `SessionPassthrough=0, InteractiveLogin=1, AutomatedLogin=2`.
- `ValueObjects/SiaraSession.cs` — credential-free session handle. **⚠️ `AcquiredBy` is still an optional
  `string?` — S8 must make actor identity mandatory + trustworthy (ADR-010 audit + P2).**
- `ValueObjects/SiaraSessionRequest.cs`, `ValueObjects/SiaraCredential.cs` (IDisposable, `char[]`, redacted
  `ToString`, scoped `UseAsync`, no string credential members — P5).
- `Interfaces/ISiaraSessionProvider.cs`, `ISiaraSessionProviderResolver.cs`, `IBrowserSessionContext.cs`,
  `ISiaraCredentialSource.cs`.

**Infrastructure (`02 Infrastructure/Infrastructure.BrowserAutomation/`)**
- `Siara/SessionPassthroughSiaraSessionProvider.cs`, `InteractiveLoginSiaraSessionProvider.cs`,
  `AutomatedLoginSiaraSessionProvider.cs`, `ConfiguredSiaraCredentialSource.cs`,
  `SiaraLoginCircuitBreaker.cs` (P3), `SiaraSessionProviderResolver.cs`, `SiaraAuthOptions.cs`
  (Passthrough / Interactive / Automated sub-options).
- `DependencyInjection/SiaraAuthenticationServiceCollectionExtensions.cs` — `AddSiaraAuthentication`.
- `Services/SiaraLoginService.cs` — the retained form driver. **⚠️ It logs the username in plaintext
  (`LogInformation("...{Username}", username)`) — S8 log-scrub target (P5).**
- `PlaywrightBrowserAutomationAdapter.cs` — implements `IBrowserAutomationAgent` + `IBrowserSessionContext`.

**Testing (`09 Testing/01 Abstractions/Testing/Contracts/`)** — contract bases, factories, fakes:
`SiaraSessionProviderContract` + mock factory + `FakeSiaraSessionProvider`; `BrowserSessionContextContract`;
`SiaraCredentialSourceContract` + mock factory + `FakeSiaraCredentialSource`;
`SiaraSessionProviderResolverContract` + mock factory.

**Tests** — `08 Tests/01 Core/Tests.Domain.Interfaces/` (mock/fake blueprints) ·
`08 Tests/02 Infrastructure/Tests.Infrastructure.BrowserAutomation/` (unit-tier: provider contract
inheritors + mechanics, circuit-breaker w/ `FakeTimeProvider`, resolver real-DI tests) ·
`08 Tests/05 System/Tests.Infrastructure.BrowserAutomation.E2E/` (real-browser round-trips).

---

## NEXT TASKS

### S8 — Security & audit hardening  ⚠️ honor the P-set
1. **No-credential reflection on the credential PATH (P5), not just `SiaraSession`.** Done in part:
   `SiaraCredentialSourceContract` already asserts `SiaraCredential` exposes no string credential properties,
   redacts `ToString`, and zeroes on dispose, and the provider test proves the credential is disposed after
   login. Extend with a **log-scrub test** that drives `AutomatedLogin` through a capturing logger and asserts
   the username/password never appear in any log line.
2. **Scrub `SiaraLoginService` (P5/P6).** It currently logs the username in plaintext. Remove/redact it.
   Confirm Playwright tracing/HAR/video are **off** in credentialed modes (P5).
3. **Make actor identity mandatory + trustworthy (ADR-010 audit + P2).** `SiaraSession.AcquiredBy` is an
   optional caller-supplied `string?` today. Make the acquiring identity required and trustworthy; bind every
   `Acquire/EnsureValid/Release` and **every `ISiaraCredentialSource` read** to an immutable audit event;
   per-document non-repudiation ties to MVP A6. (Data-model change on `SiaraSession`/the audit seam.)
4. **`ISiaraLoginService` structural isolation (P6).** Ensure no prod composition root binds app-config
   credentials into it; it is only ever invoked with the transient vault-sourced creds.
5. **Simulator ↔ prod guardrail (P8).** The test login driver must be structurally barred from the real
   `siara.cnbv.gob.mx` host.

### S9 — Mutation testing
- Stryker over the providers + `SiaraLoginCircuitBreaker` + resolver + `ConfiguredSiaraCredentialSource` +
  `SiaraCredential`. **Exclude the live-Playwright glue** (`PlaywrightBrowserAutomationAdapter`).
- Use `test-runner: mtp` + `StrykerCompat=true` (see `mutation-testing-stryker` memory / the mutation guide).
  The circuit-breaker's backoff math + the breaker-record decision branches in the automated provider are the
  high-value targets.

### → Handoff to MVP-PATH 1.1
- `SiaraDocumentDownloader : IDocumentDownloader` consumes `ISiaraSessionProviderResolver`. **Evolve
  `IDocumentDownloader.DownloadAsync` `Task<byte[]>` → `Task<Result<byte[]>>`** during 1.1. Replace
  `StubDocumentDownloader` at `Orion.Worker/Program.cs`. Keep the raw-cred `ISiaraLoginService` OFF the MVP
  path (it is only the `AutomatedLogin` provider's internal form driver). E2E against the cookie-faithful
  simulator (`tools/Siara.Simulator`).

---

## HARD CONSTRAINTS (unchanged)
- **No raw credential STORAGE, ever.** Enforced structurally on `SiaraSession`; the `AutomatedLogin` cred
  flow is vault-sourced + transient + disposed (reflection/dispose-tested).
- **Liability preconditions P1–P8 (ADR-010).** P1/P2/P4 = legal (recorded, not resolved). P3 satisfied
  (circuit-breaker). P5/P6/P7/P8 are the S8 engineering work above.
- **ITDD (ADR-005):** every port → `*Contract` + mock blueprint + ≥1 inheritor. All SIARA ports are now
  **off** the arch allowlist (`HexagonalArchitectureTests.cs` ~L843) — they have production adapters.
- **Result<T> + CancellationToken** on every async method; pre-cancelled → `ResultExtensions.Cancelled<T>()`.
- **Tests:** xUnit v3 + MTP. `dotnet test <project.csproj>` (no extra flags). Build single projects when possible.

---

## GOTCHAS / lessons from this session
1. `Infrastructure.BrowserAutomation` GlobalUsings lacks `Domain.Enum` / `Domain.ValueObjects` /
   `IndQuestResults.Operations` — add explicit usings in new provider files.
2. `Result<string>`/`Result<bool>` `WithFailure(msg, default, ex)` is CS0121-ambiguous → use 1-arg `WithFailure(msg)`.
3. A no-`await` method (e.g. passthrough `ReleaseAsync`) trips CS1998-as-error → `Task.FromResult(...)` non-async.
4. `<see cref="Result.WithFailure(string)"/>` is CS1574 across assemblies → use plain text in XML docs.
5. `Tests.Domain.Interfaces` does **not** have `Domain.Enum` as a global using → add an explicit
   `using ExxerCube.Prisma.Domain.Enum;` when a file there names `SiaraAuthMode`.
6. `SiaraCredential` must be a **class, not a record** — a record's generated `ToString` would leak the
   credential.
7. The P3 circuit-breaker is a **concrete singleton collaborator** (not a port) so its failure/backoff state
   persists across attempts; the providers consuming it stay scoped (scoped→singleton is not a captive dep).
8. Keyed DI: `AddKeyedScoped<ISiaraSessionProvider, Impl>(SiaraAuthMode.X)` + resolver
   `IServiceProvider.GetKeyedService<ISiaraSessionProvider>(mode)`; boxed-enum keys compare by value.

## Verify the current state
```
dotnet build "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"                                                                              # 0/0
dotnet test  "Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Tests.Infrastructure.BrowserAutomation/ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation.csproj"  # 101/101
dotnet test  "Prisma/Code/Src/CSharp/08 Tests/01 Core/Tests.Domain.Interfaces/ExxerCube.Prisma.Tests.Domain.Interfaces.csproj"          # 301/301
dotnet test  "Prisma/Code/Src/CSharp/08 Tests/09 Architecture/Tests.Architecture/ExxerCube.Prisma.Tests.Architecture.csproj"            # 20/20
```
Build artifacts: `E:\Dynamic\ExxerCubeBanamex\BuildArtifacts\Prisma\`.
