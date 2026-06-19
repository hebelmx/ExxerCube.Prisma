# Remediation Planning — Tracker

**Anchor (intended-solution doc):** `docs/planning-artifacts/readiness-challenge/REMEDIATION-ORCHESTRATION-KICKOFF.md`
**Backlog source of truth:** `RC5-PRISMA-READINESS-MATRIX.md` (44 gaps) · `RC4-VERIQAN-READINESS-MATRIX.md` (64 gaps) · `RC6-CROSS-CUTTING-PATH-TO-PRODUCTION.md` (W0–W6 wave order).
**Branch:** `Liv` · **Started:** 2026-06-18 · **Mode:** PLAN-ONLY → owner approval gate (do NOT implement production fixes this run).

## Handoff (top — update on each context clear)
**PLANNING COMPLETE — AT OWNER APPROVAL GATE (2026-06-18).** All 4 deliverables written + ground-truth-verified (every Blocks gap maps to ≥1 story; zero orphans; all referenced story IDs resolve to real headings). Prisma: 6 epics / 36 stories. Veriqan: 6 epics / 54 stories. Coverage + sprint plan synthesized by orchestrator. **NEXT = owner reviews & approves the plan; on approval, execute unified Wave 0** (do NOT implement production fixes until approved — this run is plan-only). Resume point if cleared: re-read kickoff + this tracker + the 4 deliverables.

## Owner-confirmed forks (2026-06-18, this session — do not re-litigate)
1. **Executor = Orchestrator + subagents.** Stories = tight self-contained agent briefs (exact files, single-project build/test DoD, E2E evidence AC).
2. **Security = FULLY SPECCED now** (not stubbed). Engineering stories with ACs written in-plan; non-engineering unlocks (Key Vault provisioning, counsel sign-off, policy decisions) still carry the gate tag + BLOCKED where the unlock is non-eng. The separate deep audit (A1–A6 / CNBV CUB / ISO 27001 / SOC 2) is still a distinct pass — reference it, don't re-spec the audit itself.
3. **Timeline = dependency-ordered backlog** (no calendar dates). Effort rollups (S/M/L) + critical path + 3 non-eng unlocks named.
4. **Veriqan §20 (#2) / §16 (#3) = buildable-now + corpus-verify gate** (abstain-guard half built now; corpus-verify AC held open; corpus acquisition assumed driven in parallel).

## Locked decisions from kickoff (do not re-litigate)
- End-state = PLAN-ONLY, then approval gate. Order = PRISMA MVP first, then Veriqan VEC.
- Gated items = BLOCKED placeholders with unlock named. Three gates: **E13 buyer-gate (issue #17)**, **live-SIARA legal gate (P1 counsel; `Siara:AllowProductionHost=false`)**, **real CONDUSEF corpus acquisition**.
- Bar = full production. Every story AC encodes the end-to-end evidence bar (a green unit test is NOT readiness).

## Deliverables (this folder)
| File | What | Status |
|------|------|--------|
| `PRISMA-REMEDIATION-EPICS.md` | Prisma epics + stories (from RC5, 44 gaps) | ✅ DONE — 6 epics / 36 stories; P1–P16 all mapped |
| `VERIQAN-REMEDIATION-EPICS.md` | Veriqan epics + stories (from RC4, 64 gaps) | ✅ DONE — 6 epics / 54 stories; #1–#28 (#16 RESOLVED) all mapped |
| `SPRINT-PLAN.md` | Unified W0→W6 wave/sprint plan + critical path | ✅ DONE — dependency-ordered, 3 unlocks named |
| `GAP-COVERAGE-MATRIX.md` | gap-ID → epic/story → status (no orphan Blocks gaps) | ✅ DONE — 0 orphans verified (grep + read) |

## Coverage obligation (approval-gate proof)
- Every Blocks-production gap maps to ≥1 story: **16 Prisma (P1–P16)** + **28 Veriqan (#1–#15, #17–#28; note #16 RESOLVED)**.
- Gated items present as BLOCKED placeholders with unlock named.
- Any deliberate deferral named with reason.

## Gotchas (from challenge run)
- E: filesystem SLOW. `git ls-files` over `find`. `dotnet test <csproj>` plain. Believe wiring/ground-truth, not prose. Owner edits same repo — `git fetch`/status first. Don't treat dormant Python (CSnakes/VLM) as a gap. Tesseract is OCR engine of record.
- Veriqan gap #16 (2 persistence tests) is RESOLVED (commit d0d9ef65) — do NOT re-plan it.
- Prisma "Resolved since 2026-06-11" list (RC5 §3) — do NOT re-plan those (A1–A6, B1, B2, etc.).
