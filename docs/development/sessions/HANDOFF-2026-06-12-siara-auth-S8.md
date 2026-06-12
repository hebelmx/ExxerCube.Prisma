# Handoff — SIARA Auth: S5–S8.3 done (+ arch-test fix) → next: S8.4/S8.5 / S9

**Date:** 2026-06-12 · **Branch:** `Kt2` · **Supersedes:** `HANDOFF-2026-06-11-siara-auth-S5-S7.md`
**Scope:** MVP-PATH Workstream 1.1 prerequisite — the SIARA auth seam (ADR-010).

---

## TL;DR

The SIARA auth **seam is complete and selectable** (3 providers + fail-closed resolver + keyed DI), and the
**S8 security/audit hardening is mostly done**: credential log-hygiene (P5) and mandatory+trustworthy actor
identity (P2) are in, ITDD-first and adversarially reviewed. What remains: **P6/P8 isolation (S8.4/S8.5)**,
**Stryker mutation (S9)**, then the real downloader (MVP-PATH **1.1**).

**Everything builds 0/0. Tests: Domain.Interfaces 307 · Infrastructure.BrowserAutomation 114 · Architecture 20/20.**

### Commits since the S5–S7 handoff (all on `Kt2`, pushed)
| Commit | What |
|---|---|
| `ba256dc` `1af9826` `41e8113` | S6 Interactive / S6b Automated (+`ISiaraCredentialSource`,`SiaraCredential`, P3 circuit-breaker) / S7 resolver + `AddSiaraAuthentication` |
| `9b07efb` | S8.1/8.2 credential log-hygiene (P5) + the `bmad-orchestrator` skill |
| `b8100bc` | **Permanent fix** of the `Contract_Bases` arch-test flake (see below) |
| `54f9002` | S8.3 mandatory + trustworthy actor identity (`ISiaraActorIdentityProvider`, P2) |
| `b4fdd4f` | S8.3 fixups from the adversarial-review gate (audit on ensure/release, fail-closed tests, etc.) |

---

## What's done (state)

- **Three providers**, config-selectable behind `ISiaraSessionProviderResolver` (fail-closed) via
  `AddSiaraAuthentication(config)`: `SessionPassthrough` / `InteractiveLogin` / `AutomatedLogin`.
- **`AutomatedLogin` safety:** `ISiaraCredentialSource` (vault/config, transient, disposed) +
  `SiaraCredential` (self-clearing `char[]`, redacted) + **P3 `SiaraLoginCircuitBreaker`** (exponential
  backoff + hard stop-and-alert; `TimeProvider`-tested).
- **P5 log-hygiene:** `SiaraLoginService` no longer logs the username; `CredentialLogScrubTests` proves the
  AutomatedLogin path emits no creds; no Playwright tracing/HAR/video exists anywhere (verified).
- **P2 actor identity:** `SiaraSession.AcquiredBy` is a **required `SiaraActor`** resolved by
  `ISiaraActorIdentityProvider` (impl `ConfiguredSiaraActorIdentityProvider`, fail-closed, service-account
  from config). Every acquire/ensure/release is actor+session audit-logged; AutomatedLogin audits the
  credential read (session-bound). All three providers fail closed if the actor can't be resolved (tested).
- **All SIARA Domain ports are OFF the arch allowlist** — they have production adapters, verified directly.

### ⚠️ The `Contract_Bases` arch test is FIXED — no longer "flaky"
The recurring intermittent failure ("Testing.Contracts assembly must be discoverable") was root-caused
(`FindAssembliesByPattern` only used `Assembly.LoadFrom`; when Testing.Contracts was already loaded,
LoadFrom of every on-disk copy threw "already loaded", and obj/ref copies threw BadImageFormat → null) and
**fixed** (`b8100bc`: prefer already-loaded assemblies + skip obj/). **Treat a red here as a real failure now.**

---

## NEXT TASKS

### S8.4 — `ISiaraLoginService` isolation (P6)
Ensure the raw-credential form driver can never receive *persisted* app-config credentials — structurally,
not by convention. Today it's registered in general browser DI and only ever invoked with the transient
vault-sourced creds (method params). Likely a guardrail test/arch rule + a doc note rather than a big change;
confirm no composition root binds config creds into it.

### S8.5 — Simulator ↔ production guardrail (P8)
Structurally bar the test/sim login driver from targeting the real `siara.cnbv.gob.mx` host (e.g. an
allowed-host check that rejects the prod host in test/sim configs, or a guard in `ISiaraLoginService`/the
sim). **Mild design choice — surface the chosen mechanism to the owner.**

### S9 — Stryker mutation (token-heavy)
Scope: the 3 providers + `SiaraLoginCircuitBreaker` + `SiaraSessionProviderResolver` +
`ConfiguredSiaraCredentialSource` + `ConfiguredSiaraActorIdentityProvider` + `SiaraCredential`. **Exclude the
live-Playwright glue** (`PlaywrightBrowserAutomationAdapter`). Use `test-runner: mtp` + `StrykerCompat=true`
(see the `mutation-testing-stryker` memory / the mutation guide). High-value targets: the circuit-breaker
backoff math + the breaker-record decision branches in AutomatedLogin.

### → MVP-PATH 1.1
`SiaraDocumentDownloader : IDocumentDownloader` consumes `ISiaraSessionProviderResolver`. Evolve
`IDocumentDownloader.DownloadAsync` `Task<byte[]>` → `Task<Result<byte[]>>`. Replace `StubDocumentDownloader`
at `Orion.Worker/Program.cs`. **Wire `AcquiredBy` actor + session id onto each downloaded document** to
finally realize per-document non-repudiation (the actor is stamped on the session but not yet *consumed* —
noted gap). Keep the raw-cred `ISiaraLoginService` OFF the MVP path. E2E against the cookie-faithful sim.

---

## HARD CONSTRAINTS
- No raw credential **storage** ever (reflection-enforced on `SiaraSession`/`SiaraActor`/`SiaraCredential`).
- Honor ADR-010 P-set: P1/P2/P4 legal (recorded); P3 done; P5/P6/P7/P8 = engineering (P5/P2 done, P6/P8 = S8.4/8.5, P7 short-TTL/revocation still open).
- ITDD (ADR-005): every port → `*Contract` + mock blueprint + ≥1 inheritor.
- Result<T> + CancellationToken everywhere; pre-cancel → `ResultExtensions.Cancelled<T>()`.
- Tests: `dotnet test <project.csproj>` — NO extra flags (breaks MTP). Build single projects.

## Optional: run this with the orchestrator
`.claude/skills/bmad-orchestrator/SKILL.md` exists. To drive the rest with minimal supervision: delegate each
chunk to an isolated subagent, **verify every result from ground truth (build/test/git) — do not trust the
subagent's summary**, commit in small chunks + push, and run an adversarial-review pass at the phase boundary.
(In the S8 pilot this caught a false "20/20" claim and real audit gaps.)

---

## Short prompt for the next agent
> Continue the SIARA auth seam on branch `Kt2` (ADR-010). S5–S8.3 are done and pushed; build 0/0,
> Domain.Interfaces 307, Infrastructure.BrowserAutomation 114, Architecture 20/20. Read
> `docs/development/sessions/HANDOFF-2026-06-12-siara-auth-S8.md` and the auto-memory
> `siara-auth-implementation.md` first. Do **S8.4** (P6: structurally isolate `ISiaraLoginService` from
> persisted creds) and **S8.5** (P8: bar the sim/test login driver from the real `siara.cnbv.gob.mx` host —
> surface the mechanism choice), then **S9** (Stryker over the providers/breaker/resolver/credential+actor
> sources, excluding the Playwright glue). ITDD per ADR-005; Result<T>+CancellationToken; `dotnet test
> <csproj>` with no extra flags. The `Contract_Bases` arch test is FIXED — a red there is real now.

## Verify current state
```
dotnet build "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"                                                                              # 0/0
dotnet test  "Prisma/Code/Src/CSharp/08 Tests/01 Core/Tests.Domain.Interfaces/ExxerCube.Prisma.Tests.Domain.Interfaces.csproj"          # 307
dotnet test  "Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Tests.Infrastructure.BrowserAutomation/ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation.csproj"  # 114
dotnet test  "Prisma/Code/Src/CSharp/08 Tests/09 Architecture/Tests.Architecture/ExxerCube.Prisma.Tests.Architecture.csproj"            # 20/20
```
