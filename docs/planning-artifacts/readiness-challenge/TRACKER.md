# Readiness Challenge — Execution Tracker

**Anchor:** `docs/planning-artifacts/READINESS-CHALLENGE-BRIEF-2026-06-18.md` (intended-solution doc — refute against THIS).
**Branch:** `Liv` · **Started:** 2026-06-18 · **Orchestrator:** bmad-orchestrator loop.

## Locked decisions (do not re-litigate)
- Bar = **full production**. Sequence = **Veriqan (Track A) first, then whole Prisma MVP (Track B)**.
- This is a **scope-vs-build readiness challenge**, not a code review. Demand **end-to-end evidence**; refute every "done"; classify gated-vs-buildable.
- **Scope anchor (owner-confirmed 2026-06-18):** brief DEFAULT set — FR-1..39 + NFR-1..8 (epics.md) + VEC PRD (`prds/prd-veriqan-vec-2026-06-16/prd.md`) + GAP-MATRIX-2026-06-11 / MVP-DEFINITION-2026-06 / PRD-RECONCILIATION-2026-06 + missions.
- **Security scope (owner-confirmed 2026-06-18):** FLAG security/compliance (A1–A6, CNBV CUB, ISO/SOC, key mgmt, audit immutability) gaps in the matrix, but the DEEP audit is a SEPARATE dedicated pass — not this run.

## Gotchas
- E: filesystem SLOW (builds 2–4 min) — prefer `git ls-files`. `dotnet test <csproj>` plain, never `--nologo`. Believe wiring + ground-truth runs, never comments/prose. Owner edits same local repo (fetch first).

## Key paths
- Composition root (Track A): `04 Services/Veriqan.Worker/Program.cs` + `03 Orchestration/Veriqan.Orchestration/DependencyInjection/VeriqanOrchestrationExtensions.cs`.
- Calibration harness: `08 Tests/03 Orchestration/Veriqan.Orchestration.Tests/Calibration/`.
- Outputs land in this folder: `docs/planning-artifacts/readiness-challenge/`.

## Status
| Task | What | Status |
|------|------|--------|
| RC.0 | Ground truths (scope register + reality map) — Veriqan | ✅ DONE (verified) |
| RC.1 | Requirement→evidence trace — Veriqan (6 clusters) | ✅ DONE (verified) |
| RC.2 | Adversarial refutation — Veriqan (2 skeptics) | ✅ DONE |
| RC.3 | Negative-space lenses — Veriqan (3 lenses) | ✅ DONE |
| RC.4 | Synthesis: Veriqan readiness matrix + path-to-production | ✅ DONE → `RC4-VERIQAN-READINESS-MATRIX.md` |
| RC.5 | Repeat 0–4 for whole Prisma MVP (Track B) | pending |
| RC.6 | Merge: one cross-cutting path-to-production + unknown-unknowns | pending |

## Orchestrator-verified crux findings (ground truth, not agent prose)
1. **Pipeline stops at verdict — nothing downstream runs or persists.** `VerificationPipeline.ProcessAsync` (Orchestration/Pipeline/VerificationPipeline.cs) runs ingest→extract→bind→tenant-resolve→validate→verdict(Stage 7)→**returns**. NO marked-PDF, NO email/alert, NO disposition, and persists ONLY the `VerificationJob` (Stage 1) — findings + verdict are returned in-memory and discarded. Confirmed by full read.
2. **Worker has no statement entry point** — `Veriqan.Worker/Program.cs` (21 lines) maps only `/health` + `/health/live`. No HTTP POST, BackgroundService, watcher, or queue. System is reachable only from tests.
3. **Scanned/image-only PDF → false-RED (cardinal-rule violation).** `BuildAllAbsent()` (Extraction/PdfPigStatementFieldExtractor.cs:3224) returns 28 POPULATED sections with mandatory ones `IsApplicable=true,IsPresent=false`. `MandatorySectionsPresenceRule`'s no-text-layer abstain guard only fires on `sections.Count==0` (line 81), so it's DEFEATED → rule Fails Critical → RED. The documented "text layer unreadable → abstain" net is silently bypassed. Resolves the RC.3 inter-agent contradiction (scale-lens said GREEN, critic said RED — critic correct). Buildable fix, not corpus-gated.
4. **As-shipped Worker = 100% InMemory** (no appsettings → SQL/CSV/SMTP branches all fall back); resume-state + reprocess-audit have NO EF impl at all → durability + audit-immutability gaps in every config.

## Artifacts produced
- `RC0a-scope-register.md` — 121 intended requirements (90 technical / 17 corpus-gated / 7 E13-gated / 7 business-gated). FR/NFR source = `epics.md` Requirements Inventory. 8 scope conflicts (NFR-7/8 undefined, PCI-DSS card scope open, no corpus owner, E13 no gate-clearing criterion).
- `RC0b-reality-map.md` — per-stage wiring map + test-substrate inventory.

## RC.0 ground-truth verification (orchestrator, not agent prose)
- **VERIFIED — Worker is a health-check-only shell.** `Veriqan.Worker/Program.cs` maps only `/health` + `/health/live`; NO IHostedService/BackgroundService/submission endpoint/queue listener. The composition root cannot process a real statement without new code. **= headline readiness gap.**
- **VERIFIED — single-tenant hardcoded** (`TenantProfile.LegalBaseline()`), CSV reference root + SQL conn-string + SMTP all absent from Worker config (falls back to in-memory). Calibration corpus ABSENT (3 KnownSynthetic non-compliant fixtures only).
- **VERIFIED RED → FIXED — persistence integration tests (2 failing on `Liv`).** Reality-map agent was RIGHT: `LegalBaselineSeeder.BuildSeedRecords()` yields **19** (11-item array CL-10…ITEM-58 **+ 5 Epic-11 `LAW-§20/§19/§6/§8/§16` recompute rules** + CL-39/37/36), and `DefaultLegalToleranceProvider` independently confirms the same 19. The integration test `LegalBaselineEncryptedStoreTests` still asserted `ShouldBe(14)` in 2 places → red. **Root cause = stale test drift from Epic 11; production seeder is canonical.** (My own intermediate count of "14" was an artifact of a grep pattern that didn't match the `LAW-§` prefix — corrected.)
  - **Fix applied (test-only, auto-OK):** updated round-trip + idempotency assertions 14→19 and the rule-list message/log strings. Re-running to confirm green.
