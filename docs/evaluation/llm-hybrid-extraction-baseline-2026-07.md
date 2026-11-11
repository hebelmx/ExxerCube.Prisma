# LLM/Hybrid Extraction Baseline — PRP1 Gold Fixtures

Generated: 2026-07-04 04:24:41Z

> **SMALL-N / DIRECTIONAL (N=3) — this is a baseline, not a statistical claim. 3 fixtures is enough to catch gross regressions, not to certify accuracy.**

> ⚠️ **READ THIS BEFORE THE NUMBERS (manually-added interpretation — the tables below are machine-generated and will be overwritten on any re-run).**
> The ~0% accuracy applies to **both** the deterministic oracle and the LLM tracks, which means the
> *measurement*, not the extractor, is what failed. The eval gold values (`A/AS1-1111-222222-AAA`,
> `222/AAA/-4444444444/2025`, `SUBDELEGACION 8 SAN ANGEL`) are **not present in the fixtures' OCR text at
> all** (verified by grep against the committed `222AAA-…_page-0001.ocr.txt`); they are synthetic
> filename/XML-derived IDs never rendered into the PDF body. The extractors are correctly reading the
> real strings that *are* in the document (oficio `AGAFADAFSON2/2025/000084`, authority
> `Comisión Nacional Bancaria y de Valores`). So this run certifies only that **the harness runs
> end-to-end and the honesty gate holds** (5/6 LLM cases correctly abstained rather than emit a wrong
> expediente). It does **not** yet certify or refute either LLM track. Full analysis + graduation
> consequences: `docs/architecture/adr/ADR-024-llm-hybrid-graduation-criteria.md` → "D3 measurement-validity finding".

## Run configuration

- Ollama base URL: `http://localhost:11434`
- Ollama reachable: **True**
- Text model: `llama3.1:8b`
- Vision model: `gemma3:12b`
- Fixtures evaluated: 3 (222AAA-44444444442025, 333BBB-44444444442025, 333ccc-6666666662025)

## Per-fixture, per-track, per-field results

| Fixture | Track | Field | Gold | Candidate | Status | Note |
|---|---|---|---|---|---|---|
| 222AAA-44444444442025 | Deterministic | NumeroExpediente | A/AS1-1111-222222-AAA |  | Missing |  |
| 222AAA-44444444442025 | Deterministic | NumeroOficio | 222/AAA/-4444444444/2025 | AGAFADAFSON2/2025/000084 | Mismatch |  |
| 222AAA-44444444442025 | Deterministic | AutoridadNombre | SUBDELEGACION 8 SAN ANGEL | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| 222AAA-44444444442025 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| 222AAA-44444444442025 | LlmText | NumeroExpediente | A/AS1-1111-222222-AAA |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADAFSON2/2025/000084' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 222AAA-44444444442025 | LlmText | NumeroOficio | 222/AAA/-4444444444/2025 |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADAFSON2/2025/000084' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 222AAA-44444444442025 | LlmText | AutoridadNombre | SUBDELEGACION 8 SAN ANGEL |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADAFSON2/2025/000084' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 222AAA-44444444442025 | LlmText | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADAFSON2/2025/000084' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 222AAA-44444444442025 | LlmVision | NumeroExpediente | A/AS1-1111-222222-AAA |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADFSON/2025/000084' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 222AAA-44444444442025 | LlmVision | NumeroOficio | 222/AAA/-4444444444/2025 |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADFSON/2025/000084' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 222AAA-44444444442025 | LlmVision | AutoridadNombre | SUBDELEGACION 8 SAN ANGEL |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADFSON/2025/000084' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 222AAA-44444444442025 | LlmVision | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADFSON/2025/000084' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 333BBB-44444444442025 | Deterministic | NumeroExpediente | H/IN1-1111-222222-AAA |  | Missing |  |
| 333BBB-44444444442025 | Deterministic | NumeroOficio | 333/BBB/-4444444444/2025 | AGAFADAFSON2/2025/000083 | Mismatch |  |
| 333BBB-44444444442025 | Deterministic | AutoridadNombre | ADMINISTRACION DESCONCENTRADA DE AUDITORIA FISCAL DE SONORA "2" | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| 333BBB-44444444442025 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| 333BBB-44444444442025 | LlmText | NumeroExpediente | H/IN1-1111-222222-AAA |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADAFSON2/2025/000083' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 333BBB-44444444442025 | LlmText | NumeroOficio | 333/BBB/-4444444444/2025 |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADAFSON2/2025/000083' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 333BBB-44444444442025 | LlmText | AutoridadNombre | ADMINISTRACION DESCONCENTRADA DE AUDITORIA FISCAL DE SONORA "2" |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADAFSON2/2025/000083' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 333BBB-44444444442025 | LlmText | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADAFSON2/2025/000083' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 333BBB-44444444442025 | LlmVision | NumeroExpediente | H/IN1-1111-222222-AAA |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADFSON/2025/000083' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 333BBB-44444444442025 | LlmVision | NumeroOficio | 333/BBB/-4444444444/2025 |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADFSON/2025/000083' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 333BBB-44444444442025 | LlmVision | AutoridadNombre | ADMINISTRACION DESCONCENTRADA DE AUDITORIA FISCAL DE SONORA "2" |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADFSON/2025/000083' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 333BBB-44444444442025 | LlmVision | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADFSON/2025/000083' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 333ccc-6666666662025 | Deterministic | NumeroExpediente | A/AS1-4444-5555555-HHHH |  | Missing |  |
| 333ccc-6666666662025 | Deterministic | NumeroOficio | 333/ccc/-666666666/2025 | AGAFADAFSON2/2025/000085 | Mismatch |  |
| 333ccc-6666666662025 | Deterministic | AutoridadNombre | JUZGADO CUARTO DE LO MERCANTIL DE PRIMERA INSTANCIA EN MATERIA MERCANTIL EN EL PRIMER PARTIDO JUDICIAL, ZONA METROPOLITANA DE GUAD. | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| 333ccc-6666666662025 | Deterministic | ParteCount | 2 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| 333ccc-6666666662025 | LlmText | NumeroExpediente | A/AS1-4444-5555555-HHHH |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADAFSON2/2025/000085' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 333ccc-6666666662025 | LlmText | NumeroOficio | 333/ccc/-666666666/2025 |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADAFSON2/2025/000085' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 333ccc-6666666662025 | LlmText | AutoridadNombre | JUZGADO CUARTO DE LO MERCANTIL DE PRIMERA INSTANCIA EN MATERIA MERCANTIL EN EL PRIMER PARTIDO JUDICIAL, ZONA METROPOLITANA DE GUAD. |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADAFSON2/2025/000085' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 333ccc-6666666662025 | LlmText | ParteCount | 2 |  | TrackSkipped | Gate rejected LLM output: Expediente 'AGAFADAFSON2/2025/000085' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| 333ccc-6666666662025 | LlmVision | NumeroExpediente | A/AS1-4444-5555555-HHHH |  | Missing |  |
| 333ccc-6666666662025 | LlmVision | NumeroOficio | 333/ccc/-666666666/2025 | AGAFADFSON2/2025/000085 | Mismatch |  |
| 333ccc-6666666662025 | LlmVision | AutoridadNombre | JUZGADO CUARTO DE LO MERCANTIL DE PRIMERA INSTANCIA EN MATERIA MERCANTIL EN EL PRIMER PARTIDO JUDICIAL, ZONA METROPOLITANA DE GUAD. | Administración General de Auditoría Fiscal Federal Administración Desconcentrada de Auditoría Fiscal de Sonora "2" | Mismatch |  |
| 333ccc-6666666662025 | LlmVision | ParteCount | 2 | 0 | Mismatch |  |

## Per-field, per-track accuracy (matches / fixtures-with-gold)

| Field | Track | Matches | Evaluable | Accuracy |
|---|---|---|---|---|
| NumeroExpediente | Deterministic | 0 | 3 | 0% |
| NumeroExpediente | LlmVision | 0 | 1 | 0% |
| NumeroOficio | Deterministic | 0 | 3 | 0% |
| NumeroOficio | LlmVision | 0 | 1 | 0% |
| AutoridadNombre | Deterministic | 0 | 3 | 0% |
| AutoridadNombre | LlmVision | 0 | 1 | 0% |
| ParteCount | LlmVision | 0 | 1 | 0% |

## Per-track coverage (non-null candidates / total evaluations)

| Track | Non-null | Total | Coverage |
|---|---|---|---|
| Deterministic | 6 | 12 | 50% |
| LlmText | 0 | 12 | 0% |
| LlmVision | 3 | 12 | 25% |

_Generated by `LlmExtractionEvalHarness` (S4-A, manual-only — not a CI gate). See `docs/implementation-artifacts/spec-llm-hybrid-extractor-S4A.md`._
