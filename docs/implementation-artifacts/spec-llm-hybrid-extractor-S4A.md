# Intended Solution — S4-A: Calibrate + Graduation Criteria (LLM/Hybrid extractor)

Anti-drift reference for the orchestrated S4-A build. Parent spec:
`docs/implementation-artifacts/spec-llm-hybrid-extractor.md` (S1–S3b, shipped DARK).
Owner ruling 2026-07-03 (`/bmad-orchestrator`): S4 = **Option A — measure before graduating**.

## Why S4-A exists (grounded findings, 2026-07-03)
1. The dark flags (`LlmProviders:TextExtractorEnabled` / `VisionExtractorEnabled`) gate **inside**
   `HybridExtractionService`, which is reachable **only** from the `/hybrid-extraction` demo page. The
   production Athena worker still binds `IFieldExtractor<TxtSource>` → `AdaptiveTxtFieldExtractor`
   unconditionally. Nothing in the pipeline calls `IHybridExtractionService`. → graduation (S4-C) is a
   real pipeline change, **out of scope here**.
2. `LlmExtractionEvalHarness` is a **STUB**: it loads gold data + probes Ollama but **never invokes any
   extractor**; the `Det*/LlmText*/LlmVision*` result fields are declared and never populated; there is
   **no match / precision / recall computation** and **no committed baseline**. So there is zero evidence
   the LLM path matches or beats deterministic. S4-A produces that evidence.

**Principle:** you cannot responsibly flip a flag on an unmeasured path. S4-A measures + defines the bar.

## Scope
IN: build out the eval harness to really run the three tracks over PRP1 gold, compute per-field metrics,
commit a baseline artifact, and ratify measurable graduation criteria (ADR). Optionally a live-provider
smoke test.
OUT (explicit): flipping any flag; touching the deterministic production path or DI bindings; wiring
`IHybridExtractionService` into the Athena pipeline (that is S4-C); the **oficio corpus** (that is a
*classification* eval — `SemanticAnalyzerService` — already baselined in
`docs/evaluation/oficio-classification-baseline-2026-07.md`; orthogonal to field extraction);
Gemini live run (no API key on the build box — Gemini track is structurally supported but reported as
`SkippedNoKey`).

## Blast radius
`Tests.Infrastructure.Extraction` (harness) + `Tests.Infrastructure.Classification` (optional smoke) +
`docs/evaluation/*` + one ADR. **No production `.cs` changes.** Deterministic path byte-identical.

## Environment (build box, verified 2026-07-03)
- Ollama UP at `localhost:11434`. Installed models: `llama3.1:8b`, `gemma3:12b` (multimodal → vision),
  `qwen2.5-coder:14b`, `qwen3-coder:30b`, `gemma:latest`. **NOT installed:** `llama3.2`, `minicpm-v`
  (the spec/config defaults). → the harness MUST accept model overrides. Baseline run uses:
  text=`llama3.1:8b`, vision=`gemma3:12b` (record exact tags in the artifact).
- Tesseract 5.5 + `spa.traineddata` at `/usr/share/tesseract-ocr/5/tessdata`. `TESSDATA_PREFIX` set for runs.
- PRP1 gold: `Prisma/Fixtures/PRP1/` — 3 companions (`222AAA-…`, `333BBB-…`, `333ccc-…`), each with
  `.pdf/.docx/.xml` + page images. Ground truth = the `.xml` companion. Only `222AAA` has a committed
  `.ocr.txt`; the other two must be OCR'd at run time.

## Gold fields measured (per fixture)
From the CNBV XML companion: `Cnbv_NumeroExpediente`, `Cnbv_NumeroOficio`, `AutoridadNombre`,
`ParteCount` (= count of `SolicitudPartes` nodes — the **headline** field per parent spec). Matching is
**normalized-exact** (trim, collapse whitespace, case-insensitive for authority; digit/format-normalized
for expediente/oficio). Record raw + normalized both.

## Tracks (all fed comparable input)
- **Deterministic** — `AdaptiveTxtFieldExtractor` over the page OCR text (real Tesseract; reuse committed
  `.ocr.txt` where present, else OCR the page images once and cache). This is the **bar to beat**.
- **LLM-text** — `LlmTxtFieldExtractor` over the SAME OCR text, live Ollama (text model override).
- **LLM-vision** — `LlmVisionFieldExtractor` over the page images (PNG bytes), live Ollama (vision override).

Per fixture, per track, per field: record `{gold, raw, normalized, match:bool, trackStatus}` where
`trackStatus ∈ {Matched, Mismatch, Missing, TrackSkipped(reason)}`. Aggregate to per-field
**accuracy** (matches / fixtures-with-gold) and per-track **coverage** (non-null / total). With N=3,
report is explicitly labelled **small-N / directional** — it is a baseline, not a statistical claim.

## Deliverables
- **D1** Harness build-out (`LlmExtractionEvalHarness`) that actually invokes all three tracks, computes
  the metric table, and writes a machine-readable `*.json` + human-readable `*.md` artifact. Stays
  `[Fact(Skip=…)]` manual-only (NOT a CI gate). Tesseract failure on a fixture downgrades that fixture's
  text tracks to committed `.ocr.txt` if present, else `TrackSkipped` (vision track still runs) — logged.
- **D2** A **deterministic** unit test (mocked `ILlmProvider` returning canned JSON, no live Ollama, no
  Tesseract) proving the **metric computation** (normalize + match + aggregate) is correct. This is the
  ground-truth verification anchor, since the live numbers themselves are not reproducible/CI-gated.
- **D3** Committed baseline artifact: `docs/evaluation/llm-hybrid-extraction-baseline-2026-07.md` (+ its
  JSON) from a real live-Ollama run on the build box, with exact model tags, timestamp, and the metric table.
- **D4** Graduation-criteria ADR: `docs/architecture/adr/ADR-024-llm-hybrid-graduation-criteria.md` —
  measurable thresholds an LLM track must meet before any flag flips (e.g. "≥ deterministic per-field
  accuracy on all 4 fields AND no SolicitudPartes regression AND coverage ≥ X"), plus the flip procedure,
  the demo-vs-pipeline distinction, and rollback. Cross-linked from this spec + the parent spec.
- **D5 (optional, if time/clean)** Skip-gated `[Fact]` live-provider smoke: `OllamaProvider` round-trips a
  real `GenerateAsync` and returns parseable JSON. Env-gated skip when Ollama is down.

## Standards
.NET 10, `Result<T>` (never throw for business logic), `ConfigureAwait(false)` in libraries,
warnings-as-errors, xUnit v3 + Shouldly + NSubstitute, test naming `Method_Scenario_Expected`,
`TestContext.Current.CancellationToken` in tests. The metric-computation logic must be pure/deterministic
and unit-tested independently of any live service (D2).

## Definition of done (epic)
Build 0/0 on `Tests.Infrastructure.Extraction`; D2 unit test green; D1 harness runs live on the box and
emits D3; D3 + D4 committed + pushed to `Liv`; deterministic prod path untouched (`git diff` shows no
production `.cs`). Nothing graduates — that is a future, separately-gated decision informed by D3/D4.
