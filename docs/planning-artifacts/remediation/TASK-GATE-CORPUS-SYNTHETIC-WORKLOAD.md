# TASK — Synthetic SIARA corpus: clean consistency cases (for §2 green) + an edge-case workload

**Owner:** dedicated troubleshooting agent · **Branch:** `Liv` · **Authored:** 2026-06-25
**Status:** OPEN — needed for the §2 max-fidelity gate to reach a GREEN verdict (separate from the OCR segfault — see `TASK-OCR-SEGFAULT-LINUX.md`, which must land first or in parallel).
**Type:** test-data / synthetic workload generation.

---

## 1. Objective

Produce a synthetic SIARA document corpus, generated to mirror real CNBV requirement packages, with **two tiers**:

1. **Clean consistency cases (a handful, e.g. 5-10):** each a 3-companion package (PDF + DOCX + XML) whose companions are **mutually consistent** (same NumeroExpediente / NumeroOficio / authority / fields across all formats). These must fuse to **high confidence, zero conflicts, NextAction != ManualReviewRequired** so the export gate lets Stage 5 emit the SIRO XML + DatosCarga xlsx → **the §2 gate goes green.**
2. **Edge-case workload (many, e.g. 100s-1000s):** diverse cases across authorities, requirement types, and "chaos" levels (missing fields, OCR-noise, layout drift, conflicting companions, scanned-only, advertising pages, etc.) to exercise the pipeline's robustness, the fusion conflict/manual-review paths, and load/soak (`PRISMA-E2-S7` SIARA session soak, dashboard throughput).

Wire tier-1 so the gate consumes a clean case deterministically; keep tier-2 available for soak/robustness runs.

---

## 2. Why this is needed (ground truth, 2026-06-25)

The §2 gate (`MaxFidelityGateFullPipelineE2ETests`) discovers a full-companion case off the SIARA simulator and asserts a clean SIRO XML + DatosCarga xlsx export. Export only fires if the Reconciliator's **`ExportGatePolicy`** does NOT block (`Prisma/Code/Src/CSharp/04 Services/Athena/Prisma.Athena.Processing/ExportGatePolicy.cs`):
- `BlockOnLowConfidence` — classification confidence ≥ 70% required.
- `BlockOnFusionManualReviewRequired` — fusion `NextAction` must NOT be `ManualReviewRequired` (set when overall confidence < 0.70 OR missing required fields OR critical field conflicts — `Domain/Enum/NextAction.cs:15`, `Domain/ValueObjects/FusionResult.cs`, thresholds in `Domain/ValueObjects/FusionCoefficients.cs:123-134`).
- `BlockOnUnresolvedConflicts` — zero unresolved field conflicts required.

**What was observed with an improvised corpus** (PRP1 fixtures `222AAA`/`333BBB` copied into per-case dirs): even at 90% *classification* confidence, **fusion** reported confidence 0.68 + 1 conflict + `ManualReviewRequired` → **export BLOCKED**. Root cause: the real PRP1 fixtures are independent samples — their PDF/DOCX/XML **disagree** (e.g. XML has `NumeroExpediente H/IN1-…`, the DOCX body has no structured expediente number). The pipeline is *correctly* routing them to manual review. The 2026-06-14 green gate used the **generated-consistent** `bulk_generated_documents_all_formats` corpus, which no longer exists on disk.

So: a green verdict needs **co-generated, internally-consistent** companions — exactly what the document generator produces.

---

## 3. Resources that already exist (use these — do not build a generator from scratch)

- **Bulk orchestrator:** `scripts/generators/generate_bulk_documents.py` — generates N documents in low-load batches into `bulk_generated_documents_all_formats`, calling a per-document generator with diversity knobs:
  - `CHAOS_LEVELS = ["none", "low", "medium", "high"]` ← **`none` = clean/consistent (tier-1 green cases); `low/medium/high` = edge cases (tier-2).**
  - `REQUIREMENT_TYPES = ["fiscal","judicial","pld","aseguramiento","informacion"]`
  - `AUTHORITIES = ["IMSS","SAT","UIF","FGR","SEIDO","PJF","INFONAVIT","SHCP","CONDUSEF"]`
  - `FORMATS = ["html","pdf","docx","xml"]` (all 4 per document → consistent companions).
  - ⚠️ The script points `GENERATOR_SCRIPT = "Prisma/Fixtures/generators/AAAV2_refactored/main_generator.py"` but the tree has the generator under **`Prisma/PRP/PRP1/research/generators/AAAV2_refactored/`** (also `AAA/`, `AAAV2/`). **Reconcile this path first** — find the real `main_generator.py`, confirm its `--output` + format flags, and fix the script's path (or run the generator directly).
- Other generators: `scripts/generators/generate_test_batch.py`, `generate_bulk_documents.ps1`, and the rich generator tree at `Prisma/Code/Src/Python/Prisma-dumy-generator-AAA/` (has `Fixtures/`, README, client summary — this is "the document generator" per CLAUDE.md).
- **Roadmap (read this):** `docs/planning-artifacts/siara-simulator-load-roadmap.md` (commit 28f24f0c) — the planned synthetic-workload + burst-arrival model (~2,500/day in bursts, scale to ~10k). Tier-2 should align with it.
- **Known-good reference case:** `Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E/AllRealWireThreeHostE2ETests.cs` — the fast harness (green 29/29, stubbed download) drives a case that exports cleanly. Use it to understand the *shape* of a case that passes the export gate (or to extract consistent fixture bytes for a tier-1 case as a shortcut).

---

## 4. How the simulator consumes the corpus (so cases are discoverable)

`tools/Siara.Simulator` (`Services/CaseService.cs`, `Program.cs`):
- `DocumentSourcePath` (config, default `../bulk_generated_documents_all_formats`) must contain **one subdirectory per case**; the **folder name = case ID**. `CaseService.DiscoverAvailableCases` lists subdirectories; `OnTimerElapsed` scans each case dir's files by extension into PdfPath/DocxPath/XmlPath/HtmlPath.
- The static file server is hardcoded to `ContentRootPath/../bulk_generated_documents_all_formats` and **must exist at sim startup** to be mounted (`Program.cs:109`), serving `/document_store/{caseId}/{fileName}` (gated behind the auth cookie).
- When run from the published app, `ContentRootPath = Deployments/Siara.Simulator/app`, so the corpus belongs at **`Deployments/Siara.Simulator/bulk_generated_documents_all_formats/<caseId>/{pdf,docx,xml}`** (gitignored). The gate's `IsFullCompanionPackage` requires a case to expose a `.pdf` AND `.docx` AND `.xml` URL.

Run the sim http-only (so the gate's plain HttpClient probe works): `Kestrel__Endpoints__Https__Url=http://localhost:5002 ASPNETCORE_ENVIRONMENT=Development dotnet Siara.Simulator.dll` from `Deployments/Siara.Simulator/app`. Full recipe: `EXECUTION-TRACKER.md` 2026-06-25 handoff.

---

## 5. Plan of work

1. **Reconcile + smoke-test the generator** — find the real `AAAV2_refactored/main_generator.py`, run it once with `chaos=none` for ONE document/all formats, confirm it emits a consistent PDF+DOCX+XML triple (same NumeroExpediente/NumeroOficio/authority across formats).
2. **Tier-1 (green cases):** generate ~5-10 `chaos=none` cases. For each, verify the companions are mutually consistent (diff the structured fields). Lay them out as per-case dirs under the sim corpus path. Run the §2 gate against one and confirm fusion → high confidence, **0 conflicts, NextAction != ManualReviewRequired**, export fires. (Requires the OCR segfault fixed first — `TASK-OCR-SEGFAULT-LINUX.md`.)
3. **Tier-2 (edge/workload):** generate a larger diverse set (vary chaos low/medium/high, all authorities/types) per the load roadmap. These intentionally exercise conflict/manual-review/abstain paths — they are NOT expected to export green; they validate robustness, SLA/dashboard throughput, and the soak test (`PRISMA-E2-S7`).
4. **Make tier-1 reproducible** — script the generation + layout (a make target or a `scripts/generators/` entry) so any box can produce the green corpus deterministically; document the seed/params. Decide corpus storage (keep gitignored + a regen script, per the existing `bulk_generated_documents_all_formats` convention — do NOT commit 11GB).
5. **Document** which case IDs are the canonical green cases and how the gate selects one (consider trimming the served set or seeding the sim so the gate deterministically picks a tier-1 case).

---

## 6. Definition of done

- A reproducible script/command generates a small **tier-1** set of internally-consistent 3-companion cases.
- With the OCR segfault resolved, the §2 gate (`RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit`) reaches **green** against a tier-1 case: SIRO XML written (root `{http://siro.regulatory.namespace}SiroResponse`, populated `NumeroExpediente`/`NumeroOficio`), DatosCarga xlsx with 24 headers, audit rows from ≥2 processes with non-null `ProcessId`.
- A **tier-2** edge/workload set (or the script to produce it) exists and is wired for soak/robustness, aligned with `siara-simulator-load-roadmap.md`.
- Generation is documented + reproducible; large corpora stay gitignored with a regen recipe.

---

## 7. Gotchas

- Don't hand-author PDFs/DOCX — use the generator so companions are co-derived (that consistency is the whole point).
- The PRP1 fixtures (`Prisma/Fixtures/PRP1/*`) are real, mutually-INCONSISTENT samples — fine for unit tests, wrong for the gate's clean-export assertion.
- Generation can be heavy (full bulk = ~11.5GB / 5-7h). Tier-1 is tiny — generate only what the gate needs; reserve the heavy bulk for deliberate soak runs.
- `chaos=none` is the consistency lever; confirm empirically that it yields conflict-free fusion before scaling.
