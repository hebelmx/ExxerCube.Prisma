# PRP1 Generator – Lessons Learned

## Refactor Highlights
- **Modular pipeline**: splitting `generate_corpus.py` into config/context/LLM/fallback/export modules paid off. Each concern (sampling, validation, fixtures) can evolve independently without monolithic edits.
- **Authority-first, CNBV-second**: modeling the workflow as “raw oficio (DOCX)” + “CNBV-standard packet (PDF/XML)” mirrors process reality and made it easier to reason about transformations, validations, and stakeholder expectations.
- **Schema-first mindset**: emitting XML in canonical order with nullable fields avoids downstream surprises. Treating “missing data” as a first-class scenario (instead of an error) simplified handling of incomplete requerimientos.

## Tooling & Infrastructure
- **ReportLab over Office automation**: generating CNBV PDFs directly in Python removed the dependency on desktop Word, improved reproducibility, and unlocked precise template control (logo strip, identification frames, watermark).
- **GPU-aware Ollama**: running the Docker container with `--gpus all` dramatically reduced per-record latency once the WSL2/NVIDIA stack was enabled. Worth documenting early to avoid future CPU-only slowdowns.
- **Fixtures as design assets**: converting the restored PDFs to PNGs (`Prisma/Fixtures/PRP1/*_page1.png`) gave an exact reference for layout work and prevented guesswork when recreating tables and phrasing.

## Testing & Validation
- **Unit tests for sampling/validators** provide fast feedback, but end-to-end confidence still requires generating a few dummy packets and visually comparing them with the fixtures. Manual review remains part of “done”.
- **Audit log as telemetry**: logging seeds, chosen profiles, template origin, and fixture paths turned out to be essential for debugging LLM timeouts and verifying fallback behavior.
- **Long-run observability**: adding `job_progress.log` provides an always-on text log (start, periodic checkpoints, completion) so operators can monitor multi-hour batches without watching the console.

## Documentation Process
- **Single-source manual** (`docs/python/prp1-generator-manual.md`) evolved alongside the refactor. Keeping it updated with new dependencies (ReportLab, GPU instructions) prevented drift between code and usage guidance.
- **Plan tracking** (`docs/tasks/refactor-python-prp1-plan.md`) made it easy to mark progress and highlight unresolved questions (e.g., layout fidelity, data realism). This checklist should stay living documentation for future contributors.

## Opportunities / Follow-ups
- **Scenario coverage**: expand authority templates beyond CNBV/IMSS/judicial to capture other flows (UIF, SAT). Each template addition should start from an official fixture or design spec.
- **Synthetic degradation pipeline**: the legacy `simulate_documents.py` can evolve into a post-processing step that introduces scanning artifacts (noise, blur, stamps) on top of the new CNBV PDFs for OCR robustness testing.
- **CI Hooks**: automating `pytest` + a single-record generation smoke test in CI would catch dependency issues (e.g., missing ReportLab) before they hit users.
