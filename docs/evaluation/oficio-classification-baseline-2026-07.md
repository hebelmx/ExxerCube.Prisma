# Oficio classification — deterministic baseline evaluation (GH#31)

**Date:** 2026-07-02 · **Scope:** bounded increment (diverse corpus + deterministic-baseline harness;
agent track scaffolded, not yet run).

## What this is

`/oficio-summary` interprets a "Texto del oficio" into five apartados (Bloqueo, Desbloqueo,
Documentación, Transferencia, Información General) via the deterministic `SemanticAnalyzerService`
(fuzzy phrase matching, Ollama disabled). To **measure how well it adapts** across authorities,
phrasings, entity shapes and edge cases — and to set up the client-requested **agent-vs-deterministic**
comparison — we added:

- **A diverse labelled corpus:** `…/Tests.Infrastructure.Classification/OficioClassificationCorpus.json`
  — 21 "Texto del oficio" variants across the 5 apartados (authorities: CNBV, SAT, FGR, UIF, Juzgado,
  CONSAR, Subdelegación; multi-label cases; negation/ambiguous edge cases), each with ground-truth
  multi-labels. Reusable by the synthetic generator and the agent track (same schema).
- **An eval harness:** `…/Tests.Infrastructure.Classification/OficioClassificationEvalHarness.cs` — runs
  the real deterministic classifier over the corpus and reports per-apartado + micro metrics.

## Baseline results (deterministic, 2026-07-02)

| Apartado | Recall | Notes |
|----------|:------:|-------|
| Bloqueo | **1.000** | robust across authorities/phrasings |
| Desbloqueo | **1.000** | robust |
| Documentación | **1.000** | robust |
| Transferencia | **1.000** | robust |
| **Información General** | **0.167** | **brittle** — only the canonical accented phrasing detects |

Micro-averaged over all 5 labels: precision **0.562**, recall **0.783**, F1 **0.655**; exact-match
**6/21**. (Precision is dragged down by *fuzzy cross-triggers* — e.g. "bloqueo" fuzzy-matching
"desbloqueo" — an inherent property of the phrase-matching baseline, reported not gated.)

## Headline findings (→ the case for an agent classifier)

1. **Información General is phrasing/accent-brittle.** The four "action" apartados fire reliably, but
   Información detection collapses on un-accented or reworded requests (recall 0.167). Notably the
   classifier *already* has a built-in LLM-enrichment hook for exactly this apartado
   (`SemanticAnalyzerService.EnrichInformacionWithLlmAsync`, gated on `OllamaOptions.Enabled`) — so
   Información is the natural first win for the agent track.
2. **Negation blindness.** `edge-negacion-bloqueo` ("…NO procede el bloqueo…") is classified as Bloqueo.
   Deterministic phrase matching cannot model negation/intent — a second clear agent-track target.

## Agent-classifier track (client ask) — how to run the comparison

The agent track is the **same** `SemanticAnalyzerService` with `OllamaOptions.Enabled = true` and a real
`IOllamaClient` (`OllamaHttpClient`) pointing at an Ollama service + model. To produce the comparison:

1. Stand up an Ollama endpoint + model (not in the demo stack today).
2. Build `new SemanticAnalyzerService(textComparer, logger, ollamaClient, Options.Create(new OllamaOptions{ Enabled = true, Model = "…" }))`.
3. Run it over the **same** corpus with the harness's `Predict()`/metrics and diff the two reports
   (accuracy, latency, cost, explainability). Seam: `OficioClassificationEvalHarness.AgentClassifierTrack_Comparison_RequiresOllamaEndpoint` (skipped).

Deterministic stays the baseline/oracle; agent is the candidate.
