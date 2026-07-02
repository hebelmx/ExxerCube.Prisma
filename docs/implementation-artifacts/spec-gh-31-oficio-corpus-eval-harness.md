---
title: 'GH#31 — Diverse oficio corpus + adaptive-classification measurement (deterministic baseline; agent seam)'
type: 'feature'
created: '2026-07-02'
status: 'done'
baseline_commit: 'd8fdda52'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Goal (bounded increment — owner chose "corpus + deterministic baseline"):** raise the `/oficio-summary`
demo's wow factor and *measure the classifier's adaptiveness* with a richer, labelled corpus of "Texto del
oficio" variants, plus a harness that quantifies the deterministic baseline and leaves a documented seam
for the client-requested agent-vs-deterministic comparison. Full live agent run (stand up Ollama + model)
was explicitly deferred.

**Approach:** (1) A diverse labelled corpus JSON (varied authorities/phrasings/entity-shapes + multi-label
+ negation/ambiguous edge cases) with ground-truth multi-labels. (2) An xUnit eval harness that runs the
real deterministic `SemanticAnalyzerService` (Ollama disabled) over the corpus, asserts primary-apartado
detection on the clear variants, and reports micro precision/recall/F1 + per-apartado recall. (3) The
agent track = the same service with `OllamaOptions.Enabled=true` + a real `IOllamaClient` — documented as
a skipped seam.

## Boundaries & Constraints

**Always:** Deterministic baseline = `SemanticAnalyzerService(textComparer, logger, ollama=null)`. Keep the
corpus schema reusable by the generator + agent track. Assert only the "clear" (assertPrimary) variants;
keep brittle/edge variants measure-only so the harness is a report, not a brittle gate.

**Never:** Do NOT stand up Ollama or run a live model here (deferred; not in the demo stack). Do NOT edit
the classifier to make the corpus pass — the harness must reflect real behaviour.

## Code Map

- `…/Tests.Infrastructure.Classification/OficioClassificationCorpus.json` — 21 labelled variants.
- `…/Tests.Infrastructure.Classification/OficioClassificationEvalHarness.cs` — harness (2 facts + skipped agent seam).
- `…/ExxerCube.Prisma.Tests.Infrastructure.Classification.csproj` — `<Content CopyToOutputDirectory>` for the corpus.
- `docs/evaluation/oficio-classification-baseline-2026-07.md` — findings/report.

## Tasks & Acceptance

- [x] Diverse labelled corpus (authorities, multi-label, negation/ambiguous edges).
- [x] Eval harness: assert primary detection on clear variants; report micro + per-apartado metrics.
- [x] Documented, skipped agent-comparison seam.

**Acceptance / Verified 2026-07-02:** full Classification test project green. Baseline: Bloqueo/Desbloqueo/
Documentación/Transferencia recall 1.000; **Información General recall 0.167 (brittle)**; micro P/R/F1
0.562/0.783/0.655. Two adaptiveness findings surfaced for the agent track: Información phrasing/accent
brittleness (classifier already has an LLM-enrichment hook there) and negation blindness
(`edge-negacion-bloqueo`). See the findings doc.

## Design Notes

Follow-ups (future sessions): extend the synthetic generator to emit into this corpus schema at scale;
run the live agent track once an Ollama endpoint/model is available and diff the reports (accuracy/latency/
cost/explainability). This increment establishes the corpus + measurement framework + the agent seam.
