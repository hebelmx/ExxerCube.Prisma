# TRACKER — S4-A/S4-B: Calibrate + Extend + Graduation Criteria (LLM/Hybrid extractor)

Branch `Liv`. Spec: `docs/implementation-artifacts/spec-llm-hybrid-extractor-S4A.md`.
Orchestrated 2026-07-03. Owner ruling: S4 = Option A (measure before graduating).

## PIVOT 2026-07-03 (owner ruled B after adversarial gate): the measure-only S4-A surfaced that the DARK
## LLM extractor only attempts partes(+wrong-format expediente) — it never produces NumeroOficio/AutoridadNombre
## (C1/C2, confirmed from code + gold). Owner chose to EXTEND the extractor (functional, S4-B) THEN measure,
## rather than baseline the gap. New sequence: [design panel → S4-B spec] → S4-B extractor extension (#6) +
## eval bug fixes (#7) → live baseline (#3) → ADR-024 finalize (#4 done, refresh numbers) → smoke (#5).
## S4-B is a functional change to the dark path (flags default false → zero production runtime impact).

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

## Adversarial-review gate — RAN 2026-07-03 (qa skeptic). VERDICT: baseline NOT trustworthy yet. Findings triaged:
- **C1 (confirmed):** LLM gate regex `^\d{3,6}/\d{4}$` rejects the real CNBV expediente format
  (`A/AS1-1111-222222-AAA`). LLM track cannot match gold expediente. (Deterministic AdaptiveTxt handles it.)
- **C2 (confirmed):** `LlmExpedienteDto` has NO NumeroOficio / AutoridadNombre — LLM extractor never attempts
  them → structural 0% on 2 of 4 fields. LLM DTO actually targets: Expediente, Solicitante, Monto, Cuenta,
  Rfc, Curp, Partes. Headline = Partes (deterministic path produces NONE → this is the real value-add to measure).
- **C3 (confirmed engine bug):** `AggregateAccuracy` doesn't exclude TrackSkipped from denominator. Untested.
- **C4 (confirmed harness bug):** per-fixture LLM failure recorded as Missing, not TrackSkipped → infra flakiness
  misattributed as model miss.
- **M1:** 222AAA fed committed `.ocr.txt` to LLM-text but fresh Tesseract to deterministic → not apples-to-apples.
- **M2:** no diacritic folding in authority normalize (Comisión≠Comision).
- **M3:** NormalizeCaseReference structure-blind → constructible boundary-shift false match (unguarded, untested).

### Fix plan (all measure-only, tests+docs):
Engine/harness bugs C3+C4+M1+M2(+M3 test) → fix regardless.
Field-set reframe (C1/C2) → OWNER-GATED (see below). Recommended: measure honestly what the LLM ACTUALLY
attempts (Partes headline + expediente-with-known-format-defect + Rfc/Curp/Monto where gold exists), label
NumeroOficio/AutoridadNombre as `NotAttemptedByDesign` for LLM tracks (excluded from accuracy denom), and
log the extractor-incompleteness as a discovered finding → recommend follow-up story S4-B (extractor hardening,
functional, OUT of S4-A). Do NOT change the production extractor DTO/prompt/gate in S4-A.
