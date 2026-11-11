# PRP1 Generator Refactor Guide

This document explains the new Python package located at `Prisma/Code/Src/CSharp/Python/prp1_generator/`, how to parse the PRP1 fixtures, and how to run the end-to-end generator (including Ollama via Docker), tests, and validation tooling.

## Module Layout
- `config.py` – loads entities, prompt template, fallback samples, schema, and the parsed PRP1 summary into `GeneratorConfig` + `OutputOptions`.
- `context.py` – turns entities + PRP1 requirement profiles into structured metadata payloads with batch selection, SLA ranges, and intentional human-like mistakes.
- `ollama_client.py` – thin HTTP client that targets the Dockerized Ollama endpoint (`http://localhost:11434` by default).
- `fallback.py` – renders deterministic templates whenever Ollama is offline or errors.
- `fixtures.py` – produces PNG/PDF mockups and schema-aligned XML files per record, mirroring PRP1 sections (header, autoridad, instrucciones, apercibimiento, firmas).
- `exporters.py` – writes JSON/MD corpora and appends JSONL audit records for traceability.
- `validators.py` – validates metadata against `requerimientos_schema.json` plus PRP1-derived required fields.
- `tests/` – pytest coverage for context sampling/validation.

## Parsing Fixtures & Building Profiles
1. Run `python Prisma/Code/Src/CSharp/Python/parse_prp1_documents.py --folder Prisma/Fixtures/PRP1 --summary-output Prisma/Fixtures/PRP1/prp1_summary.json --output Prisma/Fixtures/PRP1/parsed_documents.json`.
2. The script reads DOCX/XLSX/PDF/XML fixtures, stores raw extracts (`parsed_documents.json`), and assembles a summary JSON with:
   - XML-derived requirement profiles (authority, CNBV area, SLA days, aseguramiento flag, mandatory fields, hints).
   - Excel-derived profiles (keywords from checklist/feature specs used as prompt hints and batching categories).
   - Global metadata field frequency counts for quick validation audits.
3. Pass `--prp1-summary Prisma/Fixtures/PRP1/prp1_summary.json` to the generator (defaults to this path if present).

## Running the Generator
1. Ensure Dockerized Ollama is up (example):
   ```powershell
   docker run -d --name ollama -p 11434:11434 ollama/ollama
   docker exec -it ollama ollama pull llama3.2:latest
   ```
2. Generate dummy fixtures:
   ```powershell
   python Prisma/Code/Src/CSharp/Python/generate_corpus.py `
       --num 5 `
       --output Prisma/Code/Src/CSharp/Python/test_output/test_corpus.json `
       --fixtures-output Fixtures/Prp1Dummy `
       --fixtures-format both `
       --audit-log Prisma/Code/Src/CSharp/Python/test_output/audit.jsonl `
       --batch CNBV_Bloqueo `
       --debug
   ```
3. Outputs:
   - JSON corpus + Markdown twin.
   - PNG/PDF/XML fixtures that copy PRP1 structure but contain fictional-yet-credible legal data (with intentional but non-fatal mistakes).
   - JSONL audit log capturing per-record seed/profile/template usage and Ollama error data.
4. CLI flags:
   - `--batch` filters requirement profile IDs (derived from summary) to produce granular datasets.
   - `--fixtures-format png|pdf|both` controls mockup format.
   - `--prp1-summary` overrides the default summary path.
   - `--debug-prompts` stores prompts inside records/audit for traceability.

## Validations & Tests
- Metadata is validated automatically against `requerimientos_schema.json` and profile-specific mandatory fields before export. Failures abort generation.
- Pytest coverage lives inside `Prisma/Code/Src/CSharp/Python/tests`:
  ```powershell
  $env:PYTHONPATH += ";$(Resolve-Path 'Prisma/Code/Src/CSharp/Python')"
  pytest Prisma/Code/Src/CSharp/Python/tests
  ```
- Extend tests with new requirement profiles to guard against regressions in context sampling or validator logic.

## Crafting High-Quality Dummy Data
- Profiles derived from XML/XLSX ensure the generator knows which authorities, SLA windows, and aseguramiento toggles to respect.
- Context sampler injects subtle mistakes (typos, abrupt abbreviations) to emulate “junior lawyer under time pressure” characteristics while preserving intent and schema compliance.
- Use Excel keywords (`bloqueo`, `transferencia`, etc.) as `--batch` filters to create balanced datasets per requirement class for training/testing.
- Fixture renderer writes XML files with the same element names as PRP1 references plus visual mockups covering header -> instrucciones -> apercibimiento -> firmas.
- Adjust `fictitious_requerimientos_raw.md` to seed additional fallback templates when new authorities or tones are needed.
