# Orchestration Tracker — MVP-PATH 1.5 + 1.6 (+ owner-approved follow-ups)

**Branch:** `Kt2` · **Driver:** BMAD orchestrator (delegate→verify-from-ground-truth→commit→push→adversarial gate)
**Started:** 2026-06-12 · **Predecessor:** `HANDOFF-2026-06-12-mvp-path-1.4-reconciliator-edge.md`
**Source of truth for scope:** `docs/planning/gap-analysis/MVP-PATH-2026-06-11.md` items 1.5 (A5) + 1.6 (A6)

## Owner decisions (settled 2026-06-12 via AskUserQuestion)
1. **Identity/clearance source (1.5 design fork):** *config-bound per-process service identity* — extend the existing
   `ISiaraActorIdentityProvider`/`SiaraActor` (config-bound, ADR-010 S8.3, already in Orion) to Athena Extractor +
   Reconciliator; per-process `Process:Clearance` (Download|Extract|Reconcile) from config; `ITokenService`
   (EfCoreIdentityAdapter) mints/validates a per-process clearance token that rides the Ember handoff; receiver
   validates clearance >= required, else rejects. (Satisfies A5 DoD "EfCoreIdentityAdapter registered + used".)
2. **Run scope:** 1.5 + 1.6 **plus all four** owner-gated follow-ups — hub auth, SmartEnum JSON converter,
   real SIRO export in Reconciliator (sub-checkpoint on connStr), all-real-wire 3-host E2E.

## Grounding findings (Explore pass, 2026-06-12)
- `EfCoreIdentityAdapter` (`04 Services/Auth/Prisma.Auth.Infrastructure`) implements `IIdentityProvider`/
  `ITokenService`/`IUserContextAccessor` (Domain) — **registered NOWHERE**. `ITokenService` has
  `CreateTokenAsync`/`ValidateTokenAsync` (the clearance-token mechanism).
- `ISiaraActorIdentityProvider.GetCurrentActorAsync` → `Result<SiaraActor>` (`ActorId`, `ActorType`
  ServiceAccount|User, `DisplayName`). Impl `ConfiguredSiaraActorIdentityProvider` (reads `SiaraAuthOptions.Actor`),
  registered via `AddSiaraAuthentication()` in Web.UI + **Orion.Worker only**.
- `IAuditLogger.LogAuditAsync(actionType, stage, fileId, correlationId, userId, actionDetails, success,
  errorMessage, ct)` → `Result`; query side `GetAuditRecordsByFileId/ByCorrelationId/ByDateRange`. Impl
  `QueuedAuditLoggerService`→`AuditLoggerService` (EF `AuditRecords`). **CORRECTION to handoff:** LogAuditAsync
  is already CALLED from Application services (Export/Ingestion/DecisionLogic/MetadataExtraction), BUT **none of
  the 3 worker hosts register `IAuditLogger`/AddDatabaseServices** — that's the real 1.6 gap.
- Worker Program.cs: Orion = `04 Services/Orion/Prisma.Orion.Worker`; Extractor = `04 Services/Athena/
  Prisma.Athena.Worker`; Reconciliator = `04 Services/Reconciliator/Prisma.Reconciliator.Worker`. Only Orion
  registers `AddSiaraAuthentication`; none register IAuditLogger or the auth abstraction.

## Tasks (TaskCreate IDs)
| # | Task | DoD anchor | Blocked by |
|---|------|-----------|------------|
| 1 | Design note: 1.5 clearance/identity model (intended-solution doc) | — | — |
| 2 | 1.5a ProcessClearance + per-process identity foundation (Domain+config) | A5 | 1 |
| 3 | 1.5b Register auth abstraction + clearance-token mint/validate in 3 workers | A5 | 2 |
| 4 | 1.5c Enforce per-stage clearance on handoffs (stage rejects out-of-clearance) | **A5 gate** | 3 |
| 5 | 1.6 Per-process access audit (query answers who/which-process touched doc X) | **A6 gate** | 2 |
| 6 | Follow-up: hub auth on /hubs/ingestion + /hubs/reconciliation | — | 3 |
| 7 | Follow-up: SmartEnum JSON converter for handoff fidelity | — | — |
| 8 | Follow-up: real SIRO export in Reconciliator (CHECKPOINT connStr) | overlaps F1/5.2 | — |
| 9 | Follow-up: all-real-wire 3-host E2E | — | 4,5,6,8 |
| 10 | Docs + memory: ADR/handoff + auto-memory | — | 4,5,6,7,8,9 |

## HARD CONSTRAINTS (carry forward)
xUnit v3 + Shouldly + NSubstitute (no Moq/FluentAssertions) · `TestContext.Current.CancellationToken` ·
`Result<T>` + `CancellationToken` on every async · ITDD per ADR-005 · broadcast via `IHubContext` (DI-resolved
hub has null `Clients`) · `dotnet test <csproj>` no extra flags · single-project builds for speed · commit
code+tests separately from docs · push `Kt2`. Verify every chunk from ground truth (build + touched test project).

## Adversarial gate
Run after task 4 (1.5 complete), after task 5 (1.6 complete), and before task 10. Refute the diff against the
1.5 design note + the A5/A6 DoD, not against subagent prose.

## Progress log
- 2026-06-12: tracker created; owner decisions settled; grounding done. Starting task 1 (design note).
- 2026-06-12: **Item 1.5 (A5) COMPLETE + hardened.** Commits on Kt2: design 54df0a3; foundation 43bc888 (Domain.Interfaces 329→339); JWT+wiring+send 4854a80 (BrowserAutomation→195, workers 20/20); forwarder enforcement+A5 tests 9e5158e (Athena.Processing 65/65); hardening 1fecf73 (config-driven clearance + JWT rejection tests → BrowserAutomation 199, workers 22/22); doc fix f8714fb. Adversarial gate verdict: COMPLETE WITH GAPS (no Blocker/Major); 3 MINOR findings all closed (#2 dead-config→config-driven, #3 JWT rejection tests added, #1 jti-replay overstatement corrected + logged as accepted MVP limitation). All verified from ground truth (build 0/0 + test runs).
- NEXT: #6 hub auth → #5 1.6 audit → #7 SmartEnum → #8 SIRO export (checkpoint) → #9 E2E → #10 docs.
