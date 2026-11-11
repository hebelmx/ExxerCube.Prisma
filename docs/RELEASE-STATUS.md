# Release status

**Functional MVP, pre-production.** The full pipeline — SIARA ingestion (3-process split:
Downloader / Extractor / Reconciliator over `IndFusion.Ember`), quality analysis, Tesseract OCR,
multi-source fusion (XML + DOCX + PDF), classification, manual review, SLA tracking and export
(SIRO XML / Excel / PDF) — runs end to end against the bundled SIARA simulator
(`MaxFidelityGateFullPipelineE2ETests`).

Remaining work is deployment and operations hardening, not functional wiring: configuration
externalization, production secrets, real-network proof against the live portal (gated on the
adopting institution's legal/compliance sign-off), and the Sentinel monitoring host
(`04 Services/Sentinel/` is a library only — see `docs/operations/SENTINEL-STATUS-2026-06.md`).

Deliberate choices worth knowing:
- **OCR engine of record is Tesseract** (`TesseractOcrExecutor`). The Python/CSnakes VLM path
  (GOT-OCR2 etc.) is intentionally kept dormant as optionality — do not delete it. See
  `docs/architecture/python/adr-001-csnakes-vs-pythonnet.md`.
- **Quality filter models** use stub coefficients; the analyzers themselves are real.
- **Held-back package upgrades:** MudBlazor 9.x (major UI migration), SixLabors.ImageSharp 4.x
  (commercial license), Testcontainers 4.12 (obsoletes parameterless builders).
- **Emgu.CV on Linux** needs the distro-pinned `Emgu.CV.runtime.ubuntu-26.04-x64`,
  `UseRidGraph=true`, `Prisma/Code/Src/CSharp/eng/link-emgu-native.sh`, and the system libs
  `libvtk9.5 libhdf5-310 libavif16 libgeotiff5 liblapack3`.
- **Test fixtures are synthetic.** The legacy PRP1 oficios (`222AAA`, `333BBB`, `333ccc`,
  `555CCC`) are rendered by `scripts/generators/legacy-oficios/render_legacy_oficios.py`; the
  golden corpora come from the in-house SIARA corpus generators under `scripts/generators/`.
