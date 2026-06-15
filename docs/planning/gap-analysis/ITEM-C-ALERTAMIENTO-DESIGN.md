# Item C (#9) — Surface cross-validation mismatch as alertamiento — Design

**Owner decision:** mismatch → **annotate the record AND route the case to manual review** (non-destructive,
reuses the GH #6 review-case path).

## Ground-truth finding (the actual gap)
Conflicts ARE computed: `FusionExpedienteService` exposes `FusionResult.FieldResults[name]` where
`Decision ∈ {Conflict, WeightedVoting}` and `ConflictingValues = List<(SourceType Source, string? Value)>` carries
the differing values + provenance. But `FusionResult.ConflictingFields` is only `List<string>` (names), and
`ReconciliationOrchestrator.PersistReviewCaseAsync` (`:139-200`) builds a metadata with **only `Expediente`** —
so the rich conflict detail is dropped and the existing conflict→review routing in `ManualReviewerService`
(`:582-607`, keyed on `metadata.MatchedFields.ConflictingFields`) **never fires from the orchestrator path**.

## Deliverables (exactly two testable outcomes + the surfacing)
1. **Annotate:** `UnifiedMetadataRecord.FieldConflictAlerts` populated from the fusion result — mismatch →
   non-empty (field + per-source values + agreement); match → empty.
2. **Flag in review dashboard:** when alerts are present the case is routed to manual review with a distinct
   `ReviewReason.FieldMismatch` ("alertamiento"); when none, no FieldMismatch case (and heal any pending one).
3. **Surface the values** on the dashboard annotation read path (best-effort, see §Scope).

## Components
1. **`FieldConflictAlert`** value object (Domain.ValueObjects):
   `{ string FieldName; IReadOnlyList<ConflictingSourceValue> ConflictingValues; float AgreementLevel }`
   where `ConflictingSourceValue { SourceType Source; string? Value }`.
2. **`UnifiedMetadataRecord.FieldConflictAlerts`** — new `List<FieldConflictAlert>` property (mutable record,
   matches existing style; default empty).
3. **Pure mapper** `FieldConflictAlertBuilder.From(FusionResult)` (Domain.Services, pure/sync) → the alert list:
   for each `FieldResults` entry whose `Decision` is `Conflict` or `WeightedVoting`, emit an alert from its
   `ConflictingValues` + `AgreementLevel`. Empty when all fields agree. This is the mutation-killable core.
4. **`ReviewReason.FieldMismatch`** — new SmartEnum member `new(4, "FieldMismatch", "Cross-validation field mismatch (alertamiento)")`.
   (Additive; SmartEnum stored by value, no migration.)
5. **`ManualReviewerService.IdentifyReviewCasesAsync`** — add a **FieldMismatch dimension** mirroring the
   existing IncompleteCase dimension (`:475-528`): if `metadata.FieldConflictAlerts` is non-empty → create one
   idempotent pending `ReviewCase{ RequiresReviewReason = FieldMismatch }` per file; if empty → heal (close) any
   pending FieldMismatch row. Keep the existing dimensions untouched.
6. **`ReconciliationOrchestrator.PersistReviewCaseAsync`** — before calling the panel, populate the metadata's
   `FieldConflictAlerts` from `fusionResult` via the pure builder (instead of passing Expediente-only). Stays
   fail-open. (Hook point is already correct: after Stage 4 classification, before Stage 5 export.)

## Scope note (deliverable 3)
`ManualReviewerService.GetFieldAnnotationsAsync` (`:292-425`) already hydrates field annotations from a
`UnifiedMetadataRecord` **when one is available**. If the persisted-record path is reachable in this method,
hydrate the per-source conflicting values from `FieldConflictAlerts` so the dashboard shows them. If wiring a
full `UnifiedMetadataRecord` persistence is required and not already present, do NOT expand into it — deliver 1+2
(alerts on the record + the FieldMismatch review case) and leave a one-line follow-up note. Do not invent a
ReviewCase schema/migration change.

## Tests (ITDD per ADR-005)
- **Unit** `FieldConflictAlertBuilderTests` (pure): 3-source agree → empty; one disagrees (WeightedVoting) → one
  alert with both/all source values; full disagree (Conflict) → alert; multiple conflicting fields → multiple
  alerts. Mutation-killable core.
- **Reviewer** extend `ManualReviewerService*Tests`: alerts present → a pending FieldMismatch ReviewCase (idempotent:
  second call doesn't duplicate); alerts cleared → heal; alerts absent → no FieldMismatch case; existing
  IncompleteCase/LowConfidence/etc. dimensions unaffected.
- **Orchestrator** extend `Prisma.Athena.Processing.Tests`: a fusion result with a conflict → PersistReviewCaseAsync
  passes a metadata whose `FieldConflictAlerts` is non-empty (and routes FieldMismatch); agreeing fusion → empty.

## Definition of done
Build 0/0 for touched projects; the builder + reviewer + orchestrator tests green; existing FusionExpediente
conflict tests, ManualReviewer IncompleteCase tests, and ReconciliationOrchestrator tests unchanged and green.
`git diff` confined to the new VO + builder, the UnifiedMetadataRecord/ReviewReason additions, the reviewer
dimension, the orchestrator metadata population, and the new tests.
