# TRACKER — S4-A: Calibrate + Graduation Criteria (LLM/Hybrid extractor)

Branch `Liv`. Spec: `docs/implementation-artifacts/spec-llm-hybrid-extractor-S4A.md`.
Orchestrated 2026-07-03. Owner ruling: S4 = Option A (measure before graduating). Scope = ONE epic (S4-A).

| # | Story | Status | Verified by | Commit |
|---|-------|--------|-------------|--------|
| S4A-1 | Harness build-out: invoke 3 tracks + compute per-field metrics + emit JSON/MD artifact (D1) | TODO | build 0/0 + diff | |
| S4A-2 | Deterministic metric-computation unit test, mocked ILlmProvider (D2) — the verification anchor | TODO | test green | |
| S4A-3 | Live run on box → commit baseline artifact `docs/evaluation/llm-hybrid-extraction-baseline-2026-07.md` (+JSON) (D3) | TODO | artifact has real numbers + model tags | |
| S4A-4 | Graduation-criteria ADR-024 (D4) | TODO | ADR committed + cross-linked | |
| S4A-5 | (optional) Live-provider smoke test, skip-gated (D5) | TODO | test present + skips clean | |

## Notes / carried facts
- Models: text=`llama3.1:8b`, vision=`gemma3:12b` (spec defaults llama3.2/minicpm-v NOT installed → override).
- Only `222AAA` has committed `.ocr.txt`; harness OCRs `333BBB`/`333ccc` at run time (Tesseract 5.5 + spa).
- Gemini: no key → reported SkippedNoKey, not run.
- Oficio corpus is OUT (classification eval, separate baseline).
- NO production `.cs` changes. Deterministic path byte-identical.

## Adversarial-review gate
After S4A-1+S4A-2 land, before the live run (S4A-3): fan out a skeptic to refute the metric logic against
the spec (normalization correctness, match semantics, small-N honesty, no accidental prod-path coupling).
