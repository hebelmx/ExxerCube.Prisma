# ExxerCube.Prisma Documentation

Master index for all project documentation.

## Structure

| Directory | Contents |
|-----------|----------|
| [`legal/`](legal/) | Regulatory PDFs, legal requirements, sample SIARA documents |
| [`architecture/`](architecture/) | ADRs, system design, Python/CSnakes architecture |
| [`development/`](development/) | Guides, implementation status, lessons learned |
| [`reference/`](reference/) | Result/ROP manual, xUnit v3 patterns, OCR pipeline docs |
| [`assets/`](assets/) | Screenshots and visual resources |

## Legal

- **`legal/regulations/`** -- Official CNBV/SIARA PDFs (R29, disposiciones, manuals)
- **`legal/requirements/`** -- Our interpretation: MandatoryFields, ClassificationRules, SmartEnum types
- **`legal/samples/`** -- Synthetic SIARA document samples (docx/pdf/xml) used as reference fixtures

## Architecture

- **`architecture/adr/`** -- Architecture Decision Records (ADR-001 through ADR-008)
- **`architecture/system-design/`** -- Data model, entity catalog, system flow, folder structure
- **`architecture/python/`** -- Python OCR pipeline architecture, hexagonal design
- **`architecture/python/csnakes/`** -- CSnakes C#/Python interop documentation

## Development

- **`development/guides/`** -- Hands-on guides, demo flows, troubleshooting, Python setup
- **`development/implementation/`** -- Implementation status, refactoring summaries, task plans
- **`development/lessons-learned/`** -- Lessons learned across all phases
- **`development/archive/`** -- Completed sprint artifacts, archived items

## Reference

- **`reference/rop-manual/`** -- Result&lt;T&gt; / ROP pattern documentation
- **`reference/xunit-v3/`** -- xUnit v3 configuration patterns
- **`reference/ocr-pipeline/`** -- OCR pipeline manual, filter optimization, Python API
- **`reference/data/`** -- Extracted reference datasets (authority dictionaries, entity catalog) -- derived data, not consumed by code

## Repository structure (outside docs/)

- **`scripts/`** (repo root) -- Operational scripts by purpose: `docker/`, `build/`, `db/`, `generators/`, `data-extraction/`. (Project-internal tooling lives separately under `Prisma/scripts/`.)
- **`tools/`** (repo root) -- Standalone dev tools, e.g. `tools/Siara.Simulator/` (simulates the external SIARA portal; has its own `.sln`).
- See [`development/archive/root-cleanup-2026-06.md`](development/archive/root-cleanup-2026-06.md) for the June-2026 reorganization record (what moved/was removed and why).
