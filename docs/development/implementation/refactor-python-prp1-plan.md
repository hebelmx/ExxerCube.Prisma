# PRP1 Generator Refactor Plan

This plan tracks work needed to refactor the Python generator so it produces PRP1-like dummy requerimientos that satisfy schema, validation, and document quality requirements.

## Legend
- [ ] Not started
- [ ]➡️ In progress (mark with emoji when active)
- [x] Complete

## Phase 0 – Recon & Strategy
- [x] Audit existing scripts (`generate_corpus.py`, `simulate_documents.py`, `parse_prp1_documents.py`, validators) and note owner modules/functions to keep or discard.
- [x] Inventory PRP1 fixtures (DOCX/PDF/XML/XLSX) and summarize unique requirement types + metadata fields required per flow.
- [x] Review Excel workbook in fixtures directory to extract requirement categories and attributes that must inform prompt templates.
- [ ] Identify Ollama models + current prompts in use; capture failure scenarios that need fallbacks.

## Phase 1 – Package Skeleton & Config
- [x] Create `prp1_generator/` package under `Prisma/Code/Src/CSharp/Python/` with modules: `__init__.py`, `config.py`, `context.py`, `ollama_client.py`, `fallback.py`, `fixtures.py`, `exporters.py`, `validators.py`.
- [x] Move logic from `generate_corpus.py` into the package (config loading, context sampling, generator orchestration) while keeping CLI entrypoint thin.
- [x] Define structured settings (Pydantic/dataclasses) for data paths, schema references, Ollama endpoints, fixture output knobs.
- [x] Add deterministic seeding capabilities per run and per record, stored centrally.

- [x] Extend `parse_prp1_documents.py` to emit JSON summaries of metadata fields, constraints, and sample values derived from XML/DOCX fixtures.
- [x] Build validators that enforce `requerimientos_schema.json` plus PRP1-derived business rules (oficio numbering, SLA windows, aseguramiento toggles, authority-specific clauses).
- [x] Ensure new fixtures match the reference XML schemas (structure, element names, required attributes) while carrying fictional but credible Mexican legal content.
- [x] Encode Excel-derived requirement categories into machine-readable configs so prompts/templates can be batched per requirement type.
- [x] Write unit tests (pytest/unittest) for:
  - context sampling coverage per requirement batch
  - metadata validation rules (schema + PRP1 constraints)
  - parsing pipeline output format.

## Phase 3 – Dummy Document Synthesis
- [x] Refactor `simulate_documents.py` (or new `fixtures.py`) to produce PNG/PDF mockups that mirror PRP1 layout sections (header, autoridad block, instrucciones, apercibimiento, firmas).
- [x] Plug metadata into DOCX-to-PDF/Pillow templates to guarantee alphabetical + numerical fields land in correct zones of the mock documents.
- [x] Add CLI args `--fixtures-output` and `--fixtures-format (png|pdf|both)` to `generate_corpus.py`.
- [x] Verify generated XML stays identical in schema to references, while textual content varies: distinct fictional case numbers, authorities, sanctions, amounts.
- [x] Implement random human-like mistakes (semantic/typographical, no gross legal errors) that remain consistent with the requerimiento’s intent.
- [x] Ensure batches per requirement type introduce meaningful variation for training/testing (authority, cause, document tone, deadlines).

## Phase 4 – Traceability & Exporters
- [x] Enhance exporters so JSON/MD corpora and optional PNG/PDF assets all come from the same metadata snapshot.
- [x] Write JSONL audit logging capturing per-record: random seed, sampled context source, template (Ollama vs fallback), exception info, file hashes, validation status.
- [x] Instrument Ollama client to retry, fall back to template text, and record failure reasons in the audit log.
- [x] Provide hooks to dump prompt/response pairs for debugging when a `--debug` flag is set.

## Phase 5 – CLI, Tests, Automation
- [x] Update `generate_corpus.py` CLI to expose new knobs (fixtures output paths, fixture formats, audit log path, validation strictness, batch selection).
- [x] Ensure `python generate_corpus.py --num 5 --fixtures-output Fixtures/Prp1Dummy` succeeds end-to-end with validation gates enabled.
- [x] Add CI-friendly test entrypoints (e.g., `pytest Prisma/Code/Src/CSharp/Python/tests`) covering context sampling, validators, exporters, and document synthesis stubs.
- [x] Wire schema validation + audit log checks into tests to catch regressions.

- [x] Create `docs/python/refactor-prp1-generator.md` covering:
  - package layout and module responsibilities
  - how to parse fixtures + update requirement summaries
  - CLI usage examples (including fixture output options)
  - test execution and validation instructions
  - guidance on crafting realistic dummy data, leveraging Excel categories, and injecting controlled mistakes.
- [ ] Document batching strategy for requirement types (e.g., CNBV sanction vs IMSS compliance) so prompts/templates stay granular.
- [ ] Note reference fixtures as gold standard and outline QA checklist for comparing dummy outputs to PRP1 originals.

## Data Realism & Variation Checklist
- [x] Generated metadata remains faithful to PRP1 XML schema definitions (element ordering, attribute names, namespaces).
- [x] Distinct fictional yet credible legal content referencing Mexican authorities, laws, and procedural steps within plausible ranges.
- [x] Human-like mistakes applied sparingly—typos, slight semantic drift, rushed phrasing—but never contradicting the legal request or breaking schema.
- [x] Variation strategy defined: batch generation per requirement type (from Excel), rotate authorities, penalties, SLA, and context-specific wording.
- [x] Prompts/templates enriched with requirement-type hints to keep outputs diverse yet well-formed.

## Open Questions / To Refine
- [ ] Confirm which Ollama model versions are approved for production use and whether GPU acceleration is available locally.
- [ ] Determine tooling for image/PDF synthesis (Pillow, reportlab, docx -> pdf) and licensing constraints.
- [ ] Decide where to store parsed PRP1 summaries (versioned JSON in repo or generated artifacts).
- [ ] Clarify acceptable level of degradation (stamps, smudges) vs. focus on structural fidelity.

_Update this document as tasks progress; convert `[ ]` to `[x]` (or `[ ]➡️` when in-flight) and add notes/dates where helpful._
