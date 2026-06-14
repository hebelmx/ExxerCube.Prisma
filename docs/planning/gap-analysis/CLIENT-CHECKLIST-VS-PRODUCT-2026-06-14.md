# Client Demo Checklist vs. Product — Ground-Truth Gap Analysis (2026-06-14)

**Source checklist:** `docs/legal/samples/Checklist+2+Demo+AA.xlsx` (client "Atención a Autoridades" demo, 7 steps / 4 etapas).
**Method:** 4 parallel read-only tracers (one per etapa) → each high-impact claim **spot-checked from code by the orchestrator** (not trusted from agent prose). Branch `Kt2`.

> **Headline:** The hard, infrastructure-heavy half is built and exceeds the ask (auto-download of 3-file
> case packages, 3-source fusion with confidence, a 5-category classifier, the 3-process security spine).
> The demo's **client-visible deliverables** have real gaps: (Step 3) no manifest reconciliation, (Step 6)
> the **Excel "Datos Carga de Oficio" layout is not produced** — the pipeline emits SIRO **XML**, and the
> existing Excel generator has the wrong columns and isn't wired into Stage 5 — and (Step 7) the 5-category
> summary detects the category but extracts **none** of the per-category sub-answers (all `// TODO`).

---

## Verdict matrix

| Step | Etapa | Requirement | Verdict | Evidence (file:line) |
|------|-------|-------------|---------|----------------------|
| 1 | 1 | Auto-download the 3 docs (PDF/XML/Word) of an oficio from a site | **✅ Built (exceeds)** | `SiaraDocumentDownloader.cs:72`, `SiaraDocumentSource.cs:79` (`FilePatterns=[*.pdf,*.xml,*.docx]`), `SiaraCaseGrouping.cs:29` (bundles to one case), wired `Orion.Worker/Program.cs:62,66,184` |
| 2 | 1 | Generate a list of downloaded files (name + extension) | **🟡 Partial (implicit)** | Lives inside `DocumentDownloadedEvent.CaseFiles` + cumulative `FileIngestionJournal` (JSONL). No per-cycle "list report" surface. |
| 3 | 1 | Compare downloaded list vs expected "Listado"; report **missing + extra (sobra)** | **❌ Missing** | No expected-manifest loader, no missing/surplus reconciliation. `IsComplete` only tracks within a *discovered* case, not vs an external expected set. |
| 4 | 2 | Extract the SIRO fields from the 3 docs | **🟡 Partial** | See per-field table below. Core ids built; several XML fields + the Word signature are not. |
| 5 | 2 | Cross-validate 2 docs; **alert (alertamiento) on mismatch** | **🟡 Partial** | Conflicts ARE computed (`FusionExpedienteService` `ConflictingFields`, `FieldMatchingService`) but **never surfaced** — no UI/API alert payload. |
| 6 | 3 | Generate the **Excel** "Datos Carga de Oficio" layout | **❌ Missing (misaligned)** | Pipeline Stage 5 emits **SIRO XML only** (`ReconciliationOrchestrator.cs:290 ExportSiroXmlAsync`). `ExcelLayoutGenerator.cs:79-90` = 12-col "SIRO Registration", **not** the 19-field client layout, and **not called** in the worker. |
| 7 | 4 | 5-category interpreted PDF summary + sub-answers | **🟡 Partial** | Categories **built** (`SemanticAnalyzerService.cs:54-246`, fuzzy phrase match). All sub-answer extraction (accounts/products/amounts/doc-types/beneficiary) is **`// TODO`** (lines 143,168,193,218,243). |

Legend: ✅ Built · 🟡 Partial · ❌ Missing.

---

## Etapa 2 — field-by-field (Step 4)

| # | Field | Source in spec | Verdict | Notes (file:line) |
|---|-------|----------------|---------|-------------------|
| 1 | Numero de expediente | XML `<Cnbv_NumeroExpediente>`, Word | ✅ | `XmlFieldExtractor.cs:40`; Docx regex `DocxFieldExtractor.cs:189` |
| 2 | Oficio | XML `<Cnbv_NumeroOficio>`, Word | 🟡 | **XML path does NOT read `Cnbv_NumeroOficio`**; caught only from OCR text via `AdaptiveTxtFieldExtractor` |
| 3 | Días (plazo) | XML `<Cnbv_DiasPlazo>`, Word | ✅ | `XmlFieldExtractor.cs:52`; text pattern fallback |
| 4 | Subdivisión | XML `<Cnbv_AreaDescripcion>` → A/AS… codes | ✅ | `XmlFieldExtractor.cs:44-48` `MapSubdivision()` |
| 5 | Descripción (Paterno+Materno+Nombre) | XML, PDF | 🟡 | Parts exist; **no auto-composition** into one "Descripción" at XML layer |
| 6 | RFC | XML `<Rfc>`, PDF | ✅ | `XmlFieldExtractor` `CollectRfcVariants` (~240); PDF pattern |
| 7 | Dirección | XML `<Domicilio>`, PDF | 🟡 | **XML path does NOT read `<Domicilio>`**; only PDF address-pattern |
| 8 | Nombre del remitente (signing functionary) | Word — **inside an image** | ❌ | `DocxFieldExtractor.ExtractTextFromDocx:142` reads `<Text>` only; **no OCR of embedded Word images** |
| 9 | Numero Identificador del Requerimiento (`AGAFADAFSON2/2025/000083`) | XML, Word | 🟡 | Caught from **OCR text** (`AdaptiveTxtFieldExtractor` pattern `[A-Z]{4,}…/\d{4}/\d{6}`). **Docx `ExtractExpediente` regex (`:189`) does NOT match this shape** (the 2026-06-14 finding → issue #2). |

---

## Etapa 3 — Excel layout gaps (Step 6)

The required layout ("Datos Carga de Oficio") has ~24 columns. Current `ExcelLayoutGenerator` produces 12 unrelated columns and is **not** in the pipeline. Specific misses:
- **Not produced in pipeline:** Stage 5 = SIRO XML; the layout generator is only reachable ad-hoc via `ExportService`.
- **Fixed-value fields absent** (Procedencia, Estatus="registrado", Grupo, Área remitente, Entidad Financiera="Banco", Origen, Tipo de documento, Medio de seguimiento, abogados/despacho/estado/ciudad/zona).
- **Calculated dates absent:** Fecha de registro, Fecha de recepción, **Fecha estimada de conclusión**.
  - Business-day math exists (`FusionExpedienteService` `CalculateBusinessDays` ~2778, also `SLAEnforcerService` ~453) but **skips weekends only — no Mexican federal holidays** (explicit TODO). Holiday-aware SLA calendar is a documented deferral.
- **Tipo de asunto** (EMBARGO/DESEMBARGO/DOCUMENTACIÓN/INFORMACIÓN/TRANSFERENCIAS) not mapped into the layout (the classifier produces `ComplianceActionKind` but it isn't projected to the layout field).
- **Subdivisión** mapped at extraction but not emitted as a layout column.

---

## Etapa 4 — 5-category summary (Step 7)

- **Built:** category detection for all 5 (Bloqueo/Desbloqueo/Documentación/Transferencia/Información) via fuzzy phrase matching over a curated `ClassificationDictionary`. Precedence rules. Confidence scores.
- **Missing:** every per-category sub-answer the checklist asks for — specific accounts, products, amounts, the original-block expediente/oficio (desbloqueo), documentation sub-types (estado de cuenta / INE / comprobante / contrato / firma / cheque / expediente apertura) with their detail fields, transfer beneficiary/branch/contract/abono account, and the free-text "qué información". Value objects exist but are never populated from text (`SemanticAnalyzerService.cs:143,168,193,218,243`).
- **Path status:** Ollama/LLM + Python VLM scaffolding present but **dormant by design**; fuzzy matching is the active engine. Closing Step 7 needs context-aware extraction (regex for the easy sub-fields, LLM for the interpreted ones).

---

## Where the product is MORE advanced / more natural than the checklist asks

- **Case-package ingestion**: one event per oficio bundling all 3 files, with **SHA-256 dedup journal** and **best-effort partial-case** handling (a missing file never invalidates the request) + auto-routing of incomplete cases to the **manual-review dashboard** (`IsComplete` → `ReviewReason.IncompleteCase`).
- **3-source fusion** with weighted voting + confidence + reliability math (richer than "compare 2 docs").
- **Full SIARA auth seam** (3 modes: passthrough / interactive / automated-vault) + **3-process security spine** (Downloader/Extractor/Reconciliator, JWT per-process clearance, per-process audit) — beyond anything the checklist requires.
- **Real SIRO XML export** (the actual CNBV filing format) is wired end-to-end, where the checklist only asks for an Excel layout.

---

## Recommended demo-closing batch (smallest path to a clean 7-step demo)

1. **Step 6 — produce the real Excel "Datos Carga de Oficio" layout** (highest client visibility): build a layout/template with the 24 columns incl. fixed values + calculated dates + Tipo de asunto + Subdivisión, and **wire it into Stage 5** alongside SIRO XML. *(Holiday-aware conclusion date can be a fast-follow; weekend-only is a known gap.)*
2. **Step 3 — manifest reconciliation**: load the expected "Listado", compare to the downloaded set, report **missing + extra**.
3. **Step 5 — surface the mismatch alert**: expose the already-computed `ConflictingFields` as an alertamiento in the review UI/record.
4. **Step 7 sub-answers** (largest): start with regex sub-extraction for accounts/amounts/doc-types; LLM for the interpreted fields. *(Or scope the demo to category-level only.)*
5. **Step 4 polish**: read XML `Domicilio` + `Cnbv_NumeroOficio`; compose "Descripción"; (stretch) OCR the Word signature image for the remitente.

Out-of-scope/known deferrals that intersect this checklist: Mexican-holiday SLA calendar; deeper semantic field extraction; dormant VLM path.
