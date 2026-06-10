# Handoff — Conflict-detection bug-fix session (the two reserved findings) (2026-06-10)

**For:** the next agent. **Branch:** `Kt2`. This session fixed the two reserved conflict-detection defects
that mutation testing surfaced, and — after discovering they lived in unused code — implemented the real
feature in the path production actually exercises.

---

## 1. What was fixed

### Finding A — NameMatchingPolicy self-pairing (HIGH) — ✅ RESOLVED (commit 153fc87)
`NameMatchingPolicy.SelectBestValueAsync` / `CalculateAgreementLevelAsync` scored every value against itself
(the diagonal); `ScorePair(x,x)=1.0` dominated the MAX, so it always returned the first value with agreement
1.0 and **never** flagged a conflict — the fuzzy/alias/accent logic (~70% of the file) was inert.
**Fix:** exclude the diagonal (distinct pairs by index); overall agreement = the **weakest** distinct pair;
winner = the **medoid** (highest total agreement with the others, so consensus beats an outlier); a single
value trivially agrees. Mutation score **36.9% → 95.29%, 0 killable survivors** (residual = defensive `??`,
blank-Normalize, the symmetric-alias reverse branch, split-options bitwise).

### Finding B — additional-field XML-vs-OCR conflict detection (Medium) — ✅ RESOLVED in the USED path (commit fd0afd1)
**Key discovery:** the finding's subject, `FieldMatcherService<T>`, is **registered but never invoked in
production**, and being single-source-type (`List<T> sources`) it *cannot* compare across XML/OCR within a call.
The real, production-wired orchestrator is the Application **`FieldMatchingService`** (receives Docx+Pdf+Xml
together), which previously never populated `AdditionalMerged`/`AdditionalConflicts`.
**Owner decision: implement it there ("derive origin from the source list").** Now
`MatchFieldsAndGenerateUnifiedRecordAsync`:
- collects non-core additional fields per source (origin-tagged), skipping blanks, the magic `"Origin"` key,
  and any field already reconciled as a defined/core field;
- runs each through the matching policy → populates `AdditionalMerged` (best value) + `AdditionalConflicts`
  (cross-source disagreement) — the metadata used upstream to disambiguate homonyms;
- routes NAME fields (key contains `NOMBRE`) to the name-aware policy in both the core loop and additional
  reconciliation;
- this legitimately **revives `DeriveSlaFromAdditional`** (now that `AdditionalMerged` is populated), so a
  reconciled `FechaPublicacion`/`DiasPlazo` drives the expediente SLA dates.

**New abstraction:** `INameMatchingPolicy : IMatchingPolicy` (Domain) lets the Application layer route name
fields without referencing Infrastructure; `NameMatchingPolicy` implements it; registered in DI; injected into
`FieldMatchingService` as an **optional** ctor param (falls back to the general policy, so existing callers/tests
are unaffected).

## 2. Tests
- `NameMatchingPolicyConflictTests` (+9): different names → conflict; outlier-among-consensus → consensus wins +
  conflict; distant nickname (no alias) → conflict; alias/accent → agree; single → trivially agrees; agreement
  weakest-pair; HasConflict on disagreement. (ITDD: the conflict/medoid cases were red on the pre-fix code.)
- `FieldMatchingServiceReconciliationTests` (+8): cross-source disagreement → `AdditionalConflicts`; agreement →
  merged no conflict; name key → name policy; non-name → general policy; defined field not double-counted;
  `"Origin"` excluded; `DeriveSlaFromAdditional` fires from reconciled `FechaPublicacion`/`DiasPlazo`; no-additional
  → empty.
- Full suites green: **Tests.Application 459/459, Tests.Infrastructure.Classification 283/283.** No regressions.

## 3. Left as-is (documented)
- **`FieldMatcherService<T>`** + `IFieldMatcher<T>` remain — registered for Docx/Pdf but never invoked, and
  structurally single-type. Superseded by the Application path. **Candidate for deletion** in a future cleanup
  (touches `Program.cs` registration + 2 E2E DI-validation tests that resolve `IFieldMatcher<DocxSource/PdfSource>`).
  Its `CollectAdditional` dead-code was NOT "fixed" (pointless in a single-type service).
- Minor dead-code still open (roadmap §3): unused private `MatchingPolicyService.GetConflictThreshold(string)`;
  the inverted `FechaEstimadaConclusion` `WarnIf` in `FieldMatchingService.AggregateValidation`
  (`docs/qa/findings/2026-06-09-fieldmatching-fechaestimada-warning-inverted.md`).

## 4. Verification
- NameMatchingPolicy Stryker: 95.29%, 0 killable survivors (commit confirmed).
- FieldMatchingService Stryker: re-run after the reconciliation/routing changes — see the roadmap §3 / guide
  update for the numbers and residual classification.

## 5. Pointers
- Findings (now marked RESOLVED): `docs/qa/findings/2026-06-08-namematchingpolicy-*.md`,
  `docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-*.md`.
- Mutation guide / roadmap: `docs/qa/test-plans/mutation-testing.md`, `…-roadmap.md` (§3).
- Prior handoff: `docs/development/sessions/HANDOFF-2026-06-10-mutation-campaign-COMPLETE.md`.
