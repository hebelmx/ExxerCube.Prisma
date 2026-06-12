# Handoff — SIARA Authentication Seam (S5 done) → next: S6

**Date:** 2026-06-11 · **Branch:** `Kt2` · **Author:** prior agent session
**Scope:** MVP-PATH Workstream 1.1 prerequisite — the SIARA auth seam (ADR-010).
**Supersedes for SIARA-auth detail:** `HANDOFF-2026-06-11-siara-auth-S0-S4.md` (still the canonical S6/S6b/S7 detail).

---

## TL;DR

The SIARA authentication **seam** is built ITDD-first through **S5**: the Domain port + contract base +
reference fake (S0–S3), the `IBrowserSessionContext` Playwright capability (S4), the three settled auth
modes, the adversarial liability review, the cookie-faithful simulator, and now the **first production
provider** (`SessionPassthroughSiaraSessionProvider`, S5). The next work is the **remaining providers
and the resolver**: S6 (interactive), S6b (automated — liability-gated), S7 (resolver + keyed DI), then
S8/S9 (security/mutation), then hand off to the real downloader (MVP-PATH **1.1**).

**Everything builds 0/0; `Tests.Infrastructure.BrowserAutomation` 24/24; Tests.Architecture 20/20;
Tests.Domain.Interfaces 286/286.**

### Commit this session (on `Kt2`, pushed: `2dc73d7..89c7c5e`)
| Commit | What |
|---|---|
| `89c7c5e` | **S5**: `SessionPassthroughSiaraSessionProvider` over `IBrowserSessionContext` + `SiaraAuthOptions`/`SiaraPassthroughOptions` + new unit-tier `Tests.Infrastructure.BrowserAutomation` project (contract inheritor + 12 mechanics tests) + removed `ISiaraSessionProvider` from the arch allowlist |

(Prior commits this workstream: `16a108b` S0–S3, `dc841f4` S4, `349df64` decisions, `da71738` adversarial review, `743a1ba` simulator cookie-upgrade.)

---

## Canonical docs (read these first)
1. `docs/architecture/adr/ADR-010-SIARA-Authentication-Strategies.md` — governing decision (3 modes, **§ Legal preconditions & residual risk P1–P8**).
2. `docs/architecture/adr/ADR-010-adversarial-review-2026-06-11.md` — full graded liability findings.
3. `docs/development/sessions/HANDOFF-2026-06-11-siara-auth-S0-S4.md` — file map + the canonical S6/S6b/S7 task detail.
4. `docs/planning/gap-analysis/SIARA-AUTH-DESIGN-2026-06.md` — full ITDD/DI/config design + task list S0–S9.
5. `docs/architecture/adr/ADR-005-itdd-contract-tests-injected-sut.md` — the contract-test shape every port follows.

---

## The three auth modes (settled, client-config-selected, none persists raw credentials)
| `SiaraAuthMode` | Mechanism | Status |
|---|---|---|
| `SessionPassthrough` | Ride an externally-authenticated context — storage-state import (default) or CDP attach (opt-in) via `Siara:Passthrough:Transport` | **✅ provider built (S5)** |
| `InteractiveLogin` | Human logs in once on the real page; capture + keep warm | port + capability ready (**S6 — next**) |
| `AutomatedLogin` | Unattended; creds from a client vault via `ISiaraCredentialSource`, used transiently, **never persisted** | enum landed; **S6b** builds provider + credential port |

---

## What S5 landed (study these as templates)

**Production (`02 Infrastructure/Infrastructure.BrowserAutomation/Siara/`)**
- `SessionPassthroughSiaraSessionProvider.cs` — `Mode => SessionPassthrough`, over `IBrowserSessionContext`, **never logs in**:
  - `AcquireAsync`: transport-selected attach (`StorageState` → `LoadStorageStateAsync` / `Cdp` → `ConnectToExistingContextAsync`) → **fail-closed** `IsAuthenticatedAsync` probe (unauthenticated/unverifiable ⇒ failure, never scrape) → captures a portable storage-state via `ExportStorageStateAsync`, falling back to the imported ref if capture is unavailable. `SessionId = $"passthrough-{Guid:N}"`, `ExpiresAt = null` (valid-until-probed), `AcquiredBy = request.RequestedBy`.
  - `EnsureValidAsync`: re-probes the live context; **fails closed** on a past `ExpiresAt` (passthrough can't re-auth).
  - `ReleaseAsync`: idempotent no-op (external owner owns the context).
  - `SiaraSession` stays credential-free; `StorageStateRef` is a **bearer session secret** (ADR-010 footnote 1) — never log it in full.
- `SiaraAuthOptions.cs` — `SiaraAuthOptions` (`AuthMode` + `Passthrough`) + `SiaraPassthroughOptions` (`Transport`, `StorageStateRef`, `PostLoginSelector` default `#dashboard`) + `SiaraPassthroughTransport` enum. **S7 extends** with Interactive/Automated sub-options + ttl/refresh.

**Tests (`08 Tests/02 Infrastructure/Tests.Infrastructure.BrowserAutomation/`)** — NEW **unit-tier** project, sln-wired. This is the home for S5/S6/S6b provider contract tests over a **mocked `IBrowserSessionContext`** (no browser) — resolves the S0–S4 handoff's "no unit-tier home" gotcha #5.
- `SessionPassthroughSiaraSessionProviderContractTests : SiaraSessionProviderContract` — 12 universal facts.
- `SessionPassthroughSiaraSessionProviderTests` — 12 mode-specific mechanics tests (transport selection via NSubstitute `Received`/`DidNotReceive`, fail-closed-on-probe-false, attach-failure short-circuits before probe, missing-ref-before-browser, configured-ref fallback, export capture + export-unavailable fallback, actor recording, dead-session re-validation).
- `SessionPassthroughTestFactory` — shared healthy-authenticated mock + provider builder.

**Arch:** removed `ISiaraSessionProvider` from `All_Domain_Interfaces_Should_Have_At_Least_One_Implementation` (now satisfied by the landed adapter); `ISiaraSessionProviderResolver` **stays allowlisted until S7** (`HexagonalArchitectureTests.cs` ~L843).

---

## NEXT TASKS (in order)

> Each new Domain port gets a `*Contract` base + mock blueprint + ≥1 inheritor (arch enforced). Each impl-less port is allowlisted until its adapter lands — **remove it as you implement.**

### S6 — `InteractiveLoginSiaraSessionProvider`  *(start here)*
- New `Infrastructure.BrowserAutomation/Siara/InteractiveLoginSiaraSessionProvider.cs`, `Mode => InteractiveLogin`, over `IBrowserSessionContext`.
- `AcquireAsync`: launch headed, wait up to `request.MaxWaitForHuman` for the post-login selector (human logs in on SIARA's own page), then `ExportStorageStateAsync` → `SiaraSession`.
- `EnsureValidAsync`: re-hydrate + re-probe; typed failure on expiry so the loop can request a fresh one-time login.
- Add `SiaraInteractiveOptions` (MaxWaitForHuman default, post-login selector) under `SiaraAuthOptions`.
- ITDD: `InteractiveLoginSiaraSessionProviderContractTests : SiaraSessionProviderContract` over a mocked `IBrowserSessionContext` **in `Tests.Infrastructure.BrowserAutomation`** (no browser) + mechanics tests for the human-wait/timeout path.

### S6b — `AutomatedLoginSiaraSessionProvider` + `ISiaraCredentialSource`  ⚠️ **liability-gated**
- New Domain port `ISiaraCredentialSource` (yields creds from a client secret store). **P5:** creds as cleared `char[]` (not `string`), never logged/serialized/`ToString`; no-leak test must cover the **login path**, not just `SiaraSession`.
- Provider: get creds transiently → drive the form via `ISiaraLoginService` (retained, not deprecated) → `ExportStorageStateAsync` → **discard creds**.
- **P3 (CRITICAL):** lockout/anomaly detection + exponential backoff + circuit-breaker + **stop-and-alert-after-N** — an unattended retry storm can lock the bank's real SIARA account → missed legal deadlines. Build it in, do not defer.
- **P6:** structurally isolate `ISiaraLoginService` from *persisted* creds (today it's just registered in the general browser DI).
- + port contract base + provider contract inheritor.

### S7 — Resolver + DI + config
- `SiaraSessionProviderResolver` (fail-closed) reading `IOptions<SiaraAuthOptions>.AuthMode`; `AddSiaraAuthentication(...)` keyed-by-`SiaraAuthMode` registration of all three providers; finish `SiaraAuthOptions` (Interactive/Automated sub-options, ttl/refresh).
- **Remove `ISiaraSessionProviderResolver` from the arch allowlist** once this lands.

### S8 / S9 — Security + mutation
- S8: no-credential reflection tests on the credential path (not just `SiaraSession`), log-scrub test, audit wiring (**mandatory trustworthy actor identity**, per-document non-repudiation, audit every `ISiaraCredentialSource` read, immutable+retained trail).
- S9: Stryker over providers + resolver, exclude live-Playwright glue.

### → Handoff to MVP-PATH 1.1
- `SiaraDocumentDownloader : IDocumentDownloader` consumes `ISiaraSessionProviderResolver`. **Evolve `IDocumentDownloader.DownloadAsync` `Task<byte[]>` → `Task<Result<byte[]>>`.** Replace `StubDocumentDownloader` at `Orion.Worker/Program.cs`. E2E against the cookie-faithful simulator. Keep raw-cred `ISiaraLoginService` OFF the MVP path.

---

## HARD CONSTRAINTS
- **No raw credential STORAGE, ever.** `SiaraSession` exposes no credential members (reflection test). `AutomatedLogin` creds are vault-sourced + transient + discarded.
- **Liability preconditions P1–P8 (ADR-010).** P1/P2/P4 = legal questions for counsel (recorded, not resolved). P3/P5/P6/P7/P8 = engineering preconditions you must satisfy in the relevant task (esp. P3 lockout/backoff before S6b ships).
- **ITDD (ADR-005):** every port → `*Contract` base (injected-Sut default) + mock blueprint + ≥1 inheritor.
- **Result<T> + CancellationToken** on every async method; pre-cancelled → `ResultExtensions.Cancelled<T>()`.
- **Tests:** xUnit v3 + MTP. `dotnet test <project.csproj>` (NO extra flags like `--nologo` — they break MTP, "Zero tests ran"). Build single projects when possible.

---

## GOTCHAS (S5-confirmed)
1. `Infrastructure.BrowserAutomation` GlobalUsings lacks `Domain.Enum` / `Domain.ValueObjects` / `IndQuestResults.Operations` → add explicit usings in new provider files.
2. `Result<string>.WithFailure(msg, default, ex)` is **CS0121-ambiguous** → use the 1-arg `WithFailure(msg)` for `Result<string>` / `Result<bool>`.
3. A no-`await` method (e.g. `ReleaseAsync`) trips **CS1998-as-error** → return `Task.FromResult(...)` (non-async signature).
4. New `.cs` under any `.E2E` dir is swallowed by `.gitignore:137 *.e2e` (case-insensitive) → `git add -f`. The new unit-tier `Tests.Infrastructure.BrowserAutomation` project is unaffected.
5. The `Contract_Bases_Must_Be_Abstract_With_At_Least_One_Inheritor` arch test is build-state-fragile (stale `BuildArtifacts\…\bin` DLL copies) — passed clean in S5 but flag, don't chase, if it false-flags.
6. New test project added to the solution via `dotnet sln add` (modeled on `Tests.Infrastructure.Metrics` csproj + `Microsoft.Extensions.Options` + Testing.Contracts ref).

---

## Verify the current state
```
dotnet build "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"                                   # 0/0
dotnet test  "Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Tests.Infrastructure.BrowserAutomation/ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation.csproj"   # 24/24
dotnet test  "Prisma/Code/Src/CSharp/08 Tests/01 Core/Tests.Domain.Interfaces/ExxerCube.Prisma.Tests.Domain.Interfaces.csproj"   # 286/286
```
Build artifacts: `E:\Dynamic\ExxerCubeBanamex\BuildArtifacts\Prisma\`.
