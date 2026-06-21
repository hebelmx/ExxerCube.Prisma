# Phase-2 — Adversarial Verification Notes (orchestrator ground-truth checks)

These are first-hand ground-truth checks run during THIS review to confirm/refute the most consequential reviewer verdicts and reconcile inter-reviewer conflicts. Each is reproducible. No prior assessment was used.

## V1 — The live pipeline DID execute (corrects R4's premise)
R4 stated "the E2E capstone failed at Step 1 … no download/OCR/classification/export executed." That describes the **first** run only. A **second** run (`e2e/e2e-rerun.log`, `e2e/RERUN-SUMMARY.md`) was performed after resetting the simulator's served-case state, and the pipeline executed end-to-end up to the Excel-export step:
- Login → real SIARA case ingest → OCR → **fusion handoff written** (`...fusion.json`, 1075 B) → classification → **SIRO XML export succeeded** (`Successfully exported SIRO XML for expediente: EXP-9626-2021`, 643 B) → audit rows persisted (`INSERT INTO [AuditRecords]` ×9, shared CorrelationId).
- **Therefore:** ingestion, OCR, fusion/field-matching, classification, SIRO XML export, and audit persistence HAVE current-run runtime evidence and must not be dispositioned NOT TESTED on the "nothing ran" basis. R1/R2/R5 correctly used the re-run.

## V2 — Live route protection (anon, http://localhost:5172)
Probed unauthenticated:
- `/` → 200 · `/health` → 503 · `/health/ready` → 503 · `/health/live` → **404 (not registered)** — confirms EX-04.
- `/sla-dashboard` → **200**, body 66,569 B containing real "SLA Dashboard / deadline / escalation" content (NOT a 4 KB login redirect). **Confirms EX-05: the SLA dashboard route has no `[Authorize]` and renders to anonymous users.** (Underlying case data was empty only because the DB is disconnected this run; the access-control gap is real regardless.)
- `/sla`, `/audit`, `/review`, `/export`, `/processing` → **404** — those exact paths are not real routes; the Phase-1 evidence "login-redirect" screenshots for these were route-name mismatches, not proof of protection. Actual protected-route names must be enumerated from `@page` directives before asserting they are protected.

## V3 — "7 absent PRP interfaces" (confirms R4, with a scope nuance)
`grep "interface IXxx"` across production `01 Core / 02 Infrastructure / 03 Orchestration / 04 Services`:
- `IFieldMatcher`, `IRuleScorer`, `IScanDetector`, `IScanCleaner`, `IReportGenerator`, `IUIBundle`, `IFieldAgreement` — **0 declarations each.** Confirmed absent **as named interfaces**.
- **Nuance:** "named PRP interface absent" ≠ "capability absent." Field consolidation runs in production via `FusionExpedienteService` (V1: fusion handoff produced live). So the PRP's ITDD interface inventory is **not realized as-specified**, but the Stage-2 consolidation capability is partially present through fusion. Report should state the interface-inventory gap precisely, not "feature missing" wholesale.

## V4 — Audit immutability conflict (R2 FR17 PASS vs R4 INV-4 FAIL)
Both observations are factual: (a) audit rows are appended through an append-only logger (no in-place update API) — R2; (b) `AuditRetentionBackgroundService` deletes rows older than the retention cutoff via `SaveChangesAsync`, and there is no DB-level append-only guard — R4. These are not contradictory; they describe "append-only by convention + policy-based 7-yr retention deletion" vs "no hard-enforced immutability." FR17 says "immutable"; FR32/NFR9 mandate retention/deletion. **Disposition:** the literal "immutable" invariant is **NEEDS HUMAN REVIEW** (append-only is met for writes; hard immutability/tamper-evidence is not enforced, and sanctioned retention deletion exists) — not a clean PASS or FAIL. Owner must rule on what "immutable" requires.

## V5 — Cross-reviewer corroboration (high confidence)
- **FR18 Excel/DatosCargaOficio export does not complete** — independently found by R1, R2, R5 + `e2e-rerun.log` line 426 (starts, no completion, no event within 60 s). High confidence FAIL.
- **FR31 / INV-5 non-notification enforcement absent** — R2 + R4 agree; no enforcement code located. High confidence gap.
- **Low-confidence classification does not block Stage-5 export** (EX-06 / INV-6) — R4 + R5; `e2e-rerun.log` shows review-case flagging yet SIRO XML still emitted. High confidence finding.
