# Task: Refactor Python Generator to Produce PRP1-like Dummy Requerimientos

## Context
- The only functional corpus tooling lives under `Prisma/Code/Src/CSharp/Python/` (`generate_corpus.py`, `generate_test_corpus.py`, `simulate_documents.py`).
- The attempted migration to `Prisma/Code/Src/Python/prisma-document-generator` was removed (commit 65490e1) because it lacked configs/tests and duplicated code.
- `Prisma/Fixtures/PRP1` now holds reference DOCX/PDF/XML/XLSX requerimientos from CNBV/IMSS flows. They define the structure we need to emulate.
- We need an actionable plan so another agent can refactor the Python pipeline to generate dummy documents similar to PRP1 fixtures.

## Goals
1. Modernize the generator into a small package (e.g., `prp1_generator/`) inside `Prisma/Code/Src/CSharp/Python/` with modules for config loading, context sampling, Ollama calls, fallback templates, and exporters.
2. Use the restored PRP1 fixtures (parsed via `parse_prp1_documents.py`) to derive required metadata fields and validation rules. Generated metadata must satisfy `requerimientos_schema.json`.
3. Provide CLI and test coverage that ensures `python generate_corpus.py --num 5 --fixtures-output Fixtures/Prp1Dummy` works end-to-end, produces JSON/MD outputs, and optionally emits PNG/PDF mockups with the PRP1 structure.
4. Document the workflow so future agents can run the generator, parsing helpers, and validation scripts.

## Tasks
1. **Create package skeleton**
   - Under `Prisma/Code/Src/CSharp/Python/`, add `prp1_generator/` with modules: `config.py`, `context.py`, `ollama_client.py`, `fallback.py`, `exporters.py`, `fixtures.py`.
   - Move existing logic from `generate_corpus.py` into these modules. Keep `generate_corpus.py` as an entrypoint that wires parsers/args to the package.

2. **Schema + fixtures alignment**
   - Extend `parse_prp1_documents.py` so it emits a structured summary (JSON) describing required metadata fields from the XML/DOCX samples.
   - Implement validators that compare generated metadata against `requerimientos_schema.json` and the PRP1-derived requirements (e.g., oficio numbering, SLA days, aseguramiento flags when applicable).
   - Add unit tests (pytest/unittest) verifying context sampling and metadata validation.

3. **Dummy document synthesis**
   - Refactor `simulate_documents.py` (or create `fixtures.py`) to generate simple PNG/PDF outputs using the metadata. Focus on matching sections seen in PRP1 (header, autoridad block, instructions, apercibimiento, signatures). Advanced degradation is optional.
   - Add CLI arguments `--fixtures-output` and `--fixtures-format (png|pdf|both)`.

4. **Traceability + logging**
   - When generating each record, log random seeds, sampled context, chosen template (Ollama vs fallback), and file hashes into a JSONL audit file.
   - Ensure errors from Ollama fall back gracefully and are recorded.

5. **Documentation**
   - Create `docs/python/refactor-prp1-generator.md` describing the new module layout, how to run the generator, how to parse fixtures, run tests, and verify outputs.
   - Include references to PRP1 fixtures as the gold standard.

## Acceptance Criteria
- Running `python Prisma/Code/Src/CSharp/Python/generate_corpus.py --num 5 --fixtures-output Fixtures/Prp1Dummy` completes without errors, produces JSON/MD corpora and optional dummy images.
- Generated metadata passes schema validation and contains the mandatory PRP1 fields.
- Unit tests covering context sampling/validation pass.
- Documentation explains setup and usage.
