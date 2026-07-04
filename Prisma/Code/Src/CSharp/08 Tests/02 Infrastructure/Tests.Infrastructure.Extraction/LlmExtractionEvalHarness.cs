using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using ExxerCube.Prisma.Infrastructure.Classification.Llm;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Llm;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using ExxerCube.Prisma.Infrastructure.Extraction.Txt;
using ExxerCube.Prisma.Infrastructure.Extraction.Txt.Llm;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Manual-only evaluation harness that measures field-extraction accuracy/coverage across PRP1 gold
/// fixtures for the deterministic, LLM-text, and LLM-vision tracks, and emits a committed baseline
/// artifact (JSON + Markdown). This is S4-A (see
/// <c>docs/implementation-artifacts/spec-llm-hybrid-extractor-S4A.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// This is NOT a CI gate — it is a MEASUREMENT harness. The test is always skipped in automated runs
/// (<c>[Fact(Skip=...)]</c>); the metric-computation logic itself (<see cref="LlmExtractionMetrics"/>)
/// is unit-tested independently, deterministically, and IS a CI gate (see
/// <c>LlmExtractionMetricsTests</c>).
/// </para>
/// <para>
/// <strong>To run manually</strong> (requires a real local Ollama at <c>localhost:11434</c> with the
/// <see cref="TextModelTag"/> / <see cref="VisionModelTag"/> models pulled, and a real Tesseract
/// install with <c>spa.traineddata</c>):
/// <code>
/// dotnet test --filter "LlmExtractionEvalHarness"
/// </code>
/// Then edit the <c>[Fact(Skip=...)]</c> attribute to remove the Skip argument.
/// </para>
/// <para>
/// <strong>Tracks</strong> (per fixture): Deterministic = <see cref="PdfOcrFieldExtractor"/> (real
/// Tesseract OCR → <see cref="AdaptiveTxtFieldExtractor"/>, the bar to beat); LLM-text =
/// <see cref="LlmTxtFieldExtractor"/> over the EXACT SAME OCR text the deterministic track produced
/// (apples-to-apples — the committed <c>*.ocr.txt</c> companion is only a fallback when the
/// deterministic track produces no OCR text at all); LLM-vision = <see cref="LlmVisionFieldExtractor"/>
/// over the committed page images. All three are constructed by hand (no DI container).
/// </para>
/// <para>
/// <strong>Gold data sources:</strong>
/// <list type="bullet">
///   <item><c>Prisma/Fixtures/PRP1/parsed_documents.json</c> — per-document paragraphs and XML content.</item>
///   <item><c>Prisma/Fixtures/PRP1/prp1_summary.json</c> — authority name and expected field list per profile.</item>
/// </list>
/// Ground-truth values are extracted from the XML companion files embedded in
/// <c>parsed_documents.json</c> (keys like <c>222AAA-44444444442025.xml</c>).
/// </para>
/// </remarks>
public sealed class LlmExtractionEvalHarness
{
    // ── Gold data paths (relative to repo root; test locates them at runtime) ──
    private const string ParsedDocumentsFile = "Prisma/Fixtures/PRP1/parsed_documents.json";
    private const string PrpSummaryFile = "Prisma/Fixtures/PRP1/prp1_summary.json";
    private const string PdfFixtureDir = "Prisma/Fixtures/PRP1";

    // ── PRP1-golden: trustworthy corpus with a generator-stamped ground_truth.json per fixture
    // (see Prisma/Fixtures/PRP1-golden/README.md) — unlike PRP1/ above, its gold is guaranteed to be
    // rendered into the document body, so it is not subject to the "gold never recoverable by OCR"
    // defect the client PRP1/ set has. ──────────────────────────────────────────────────────────────
    private const string GoldenCorpusDir = "Prisma/Fixtures/PRP1-golden";

    // ── Ollama endpoint probed before running LLM tracks ──────────────────────
    private const string OllamaHealthUrl = "http://localhost:11434/api/tags";
    private const string OllamaBaseUrl = "http://localhost:11434";

    // ── Model overrides (MANDATORY on this build box — llama3.2 / minicpm-v, the spec defaults,
    // are NOT installed; see spec-llm-hybrid-extractor-S4A.md "Environment"). ─────────────────────
    private const string TextModelTag = "llama3.1:8b";
    private const string VisionModelTag = "gemma3:12b";

    // ── Committed baseline artifact paths (relative to repo root) — D3 in the S4-A spec ─────────
    private const string ArtifactJsonRelativePath = "docs/evaluation/llm-hybrid-extraction-baseline-2026-07.json";
    private const string ArtifactMarkdownRelativePath = "docs/evaluation/llm-hybrid-extraction-baseline-2026-07.md";

    // ── Committed baseline artifact paths for the PRP1-golden run ────────────────────────────────
    private const string GoldenArtifactJsonRelativePath = "docs/evaluation/llm-hybrid-extraction-baseline-golden-2026-07.json";
    private const string GoldenArtifactMarkdownRelativePath = "docs/evaluation/llm-hybrid-extraction-baseline-golden-2026-07.md";

    private const string SmallNCaveat =
        "SMALL-N / DIRECTIONAL — this is a baseline, not a statistical claim. Per-field evaluable N " +
        "varies by track (see the 'Evaluable' column below; a track is skipped when its gate rejects " +
        "or its provider is unreachable). Enough to catch gross regressions and rank tracks, not to certify accuracy.";

    // ── Vision-track on-the-fly PDF rasterization fallback ──────────────────────────────────
    // Some fixture sets (e.g. PRP1-golden) ship only *.pdf + ground_truth.json with no
    // pre-rendered page-image files on disk, which structurally skips the vision track. When
    // LoadFixturePageImages finds nothing, we rasterize the fixture PDF via the SAME converter
    // the deterministic OCR track uses (PdfToImageConverter). Lower DPI than the OCR default
    // (300) to bound the base64 vision payload / Ollama latency — golden docs are short.
    private const int VisionRasterDpi = 150;
    private const int VisionRasterMaxPages = 5;

    // ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the full eval harness: invokes all three extraction tracks over the PRP1 gold fixtures,
    /// computes per-field metrics via the pure <see cref="LlmExtractionMetrics"/> engine, and writes
    /// the JSON + Markdown baseline artifacts. Skip attribute prevents CI execution.
    /// </summary>
    [Fact(Skip = "eval-only: run manually — remove Skip to execute (live Ollama + ~2min)")]
    public async Task Eval_PRP1Fixtures_ComputesFieldMetricsAndEmitsBaseline()
    {
        var ct = TestContext.Current.CancellationToken;
        var output = TestContext.Current.TestOutputHelper;
        output?.WriteLine("=== PRP1 Field-Extraction Eval Harness (S4-A) ===");

        // ── 1. Locate repo root ────────────────────────────────────────────────
        var repoRoot = LocateRepoRoot();
        // Fail LOUDLY (not a silent green pass) when the harness is deliberately un-skipped
        // but can't find the repo root — a no-op pass that writes no artifact is exactly the
        // trap this harness exists to avoid. The only legitimate degradation is Ollama being
        // down (LLM tracks → TrackSkipped; deterministic still runs and an artifact is written).
        // Explicit throw (not ShouldNotBeNull) so nullable flow analysis narrows repoRoot below.
        if (repoRoot is null)
        {
            throw new ShouldAssertException(
                "could not locate repo root (no directory containing " + ParsedDocumentsFile +
                " found from " + AppContext.BaseDirectory + ").");
        }

        // ── 2. Load gold data ──────────────────────────────────────────────────
        var parsedDocsPath = Path.Combine(repoRoot, ParsedDocumentsFile);
        var summaryPath = Path.Combine(repoRoot, PrpSummaryFile);

        File.Exists(parsedDocsPath).ShouldBeTrue($"gold JSON not found: {parsedDocsPath}");
        File.Exists(summaryPath).ShouldBeTrue($"gold JSON not found: {summaryPath}");

        var goldMap = BuildGoldMap(parsedDocsPath, summaryPath, output);

        // ── 3. Check Ollama availability ────────────────────────────────────────
        var ollamaReachable = await IsOllamaReachableAsync(ct);
        output?.WriteLine($"Ollama reachable at localhost:11434: {ollamaReachable} (text={TextModelTag}, vision={VisionModelTag})");
        if (!ollamaReachable)
        {
            output?.WriteLine("  → LLM tracks will be TrackSkipped for every fixture (deterministic still runs).");
        }

        // ── 4. Build real services by hand (no DI container) ───────────────────
        using var ocrExecutor = new TesseractOcrExecutor(XUnitLogger.CreateLogger<TesseractOcrExecutor>(output!));
        var imagePreprocessor = new PassthroughImagePreprocessor();
        var pdfToImageConverter = new PdfToImageConverter(XUnitLogger.CreateLogger<PdfToImageConverter>(output!));
        var adaptiveTxtExtractor = new AdaptiveTxtFieldExtractor(XUnitLogger.CreateLogger<AdaptiveTxtFieldExtractor>(output!));
        var detExtractor = new PdfOcrFieldExtractor(
            ocrExecutor, imagePreprocessor, pdfToImageConverter, adaptiveTxtExtractor,
            XUnitLogger.CreateLogger<PdfOcrFieldExtractor>(output!));

        ILlmExpedienteExtractor<TxtSource>? llmTextExtractor = null;
        ILlmExpedienteExtractor<ImageSource>? llmVisionExtractor = null;

        if (ollamaReachable)
        {
            var llmOptions = new LlmProvidersOptions
            {
                Active = "Ollama",
                Ollama = new OllamaProviderOptions
                {
                    BaseUrl = OllamaBaseUrl,
                    Model = TextModelTag,
                    VisionModel = VisionModelTag,
                    TimeoutSeconds = 120,
                },
            };
            var optionsMonitor = new StaticOptionsMonitor<LlmProvidersOptions>(llmOptions);
            var ollamaProvider = new OllamaProvider(
                new DirectHttpClientFactory(), optionsMonitor, XUnitLogger.CreateLogger<OllamaProvider>(output!));
            var providerFactory = new LlmProviderFactory([ollamaProvider], optionsMonitor);

            llmTextExtractor = new LlmTxtFieldExtractor(
                providerFactory, Options.Create(llmOptions), XUnitLogger.CreateLogger<LlmTxtFieldExtractor>(output!));
            llmVisionExtractor = new LlmVisionFieldExtractor(
                providerFactory, XUnitLogger.CreateLogger<LlmVisionFieldExtractor>(output!));
        }

        // ── 5-8. Run per-fixture evaluation, aggregate, and emit artifacts (shared helper) ──────
        var pdfDir = Path.Combine(repoRoot, PdfFixtureDir);

        await RunEvalAsync(
            repoRoot,
            goldMap,
            id => Path.Combine(pdfDir, $"{id}.pdf"),
            ArtifactJsonRelativePath,
            ArtifactMarkdownRelativePath,
            ollamaReachable,
            llmTextExtractor,
            llmVisionExtractor,
            detExtractor,
            pdfToImageConverter,
            output,
            ct);
    }

    /// <summary>
    /// Runs the full eval harness against the PRP1-golden trustworthy corpus (see
    /// <c>Prisma/Fixtures/PRP1-golden/README.md</c>): each fixture ships a generator-stamped
    /// <c>ground_truth.json</c> whose gold values are guaranteed to be rendered into the document
    /// body (unlike the client PRP1/ fixtures, whose XML-derived gold is often not recoverable by
    /// OCR at all). Same three tracks, same pure metric engine, same artifact shape as
    /// <see cref="Eval_PRP1Fixtures_ComputesFieldMetricsAndEmitsBaseline"/> — only the gold source
    /// and PDF resolution differ.
    /// </summary>
    [Fact(Skip = "eval-only: run manually — remove Skip to execute (live Ollama + ~2min)")]
    public async Task Eval_PRP1Golden_ComputesFieldMetricsAndEmitsBaseline()
    {
        var ct = TestContext.Current.CancellationToken;
        var output = TestContext.Current.TestOutputHelper;
        output?.WriteLine("=== PRP1-golden Field-Extraction Eval Harness ===");

        // ── 1. Locate repo root ────────────────────────────────────────────────
        var repoRoot = LocateRepoRoot();
        if (repoRoot is null)
        {
            throw new ShouldAssertException(
                "could not locate repo root (no directory containing " + ParsedDocumentsFile +
                " found from " + AppContext.BaseDirectory + ").");
        }

        // ── 2. Load gold data from the PRP1-golden corpus ───────────────────────
        var goldenRoot = Path.Combine(repoRoot, GoldenCorpusDir);
        if (!Directory.Exists(goldenRoot))
        {
            throw new ShouldAssertException($"golden corpus directory not found: {goldenRoot}");
        }

        var goldMap = BuildGoldMapFromGoldenDir(goldenRoot, output);
        goldMap.ShouldNotBeEmpty($"no gold fixtures loaded from {goldenRoot}");

        // ── 3. Check Ollama availability ────────────────────────────────────────
        var ollamaReachable = await IsOllamaReachableAsync(ct);
        output?.WriteLine($"Ollama reachable at localhost:11434: {ollamaReachable} (text={TextModelTag}, vision={VisionModelTag})");
        if (!ollamaReachable)
        {
            output?.WriteLine("  → LLM tracks will be TrackSkipped for every fixture (deterministic still runs).");
        }

        // ── 4. Build real services by hand (no DI container) ───────────────────
        using var ocrExecutor = new TesseractOcrExecutor(XUnitLogger.CreateLogger<TesseractOcrExecutor>(output!));
        var imagePreprocessor = new PassthroughImagePreprocessor();
        var pdfToImageConverter = new PdfToImageConverter(XUnitLogger.CreateLogger<PdfToImageConverter>(output!));
        var adaptiveTxtExtractor = new AdaptiveTxtFieldExtractor(XUnitLogger.CreateLogger<AdaptiveTxtFieldExtractor>(output!));
        var detExtractor = new PdfOcrFieldExtractor(
            ocrExecutor, imagePreprocessor, pdfToImageConverter, adaptiveTxtExtractor,
            XUnitLogger.CreateLogger<PdfOcrFieldExtractor>(output!));

        ILlmExpedienteExtractor<TxtSource>? llmTextExtractor = null;
        ILlmExpedienteExtractor<ImageSource>? llmVisionExtractor = null;

        if (ollamaReachable)
        {
            var llmOptions = new LlmProvidersOptions
            {
                Active = "Ollama",
                Ollama = new OllamaProviderOptions
                {
                    BaseUrl = OllamaBaseUrl,
                    Model = TextModelTag,
                    VisionModel = VisionModelTag,
                    TimeoutSeconds = 120,
                },
            };
            var optionsMonitor = new StaticOptionsMonitor<LlmProvidersOptions>(llmOptions);
            var ollamaProvider = new OllamaProvider(
                new DirectHttpClientFactory(), optionsMonitor, XUnitLogger.CreateLogger<OllamaProvider>(output!));
            var providerFactory = new LlmProviderFactory([ollamaProvider], optionsMonitor);

            llmTextExtractor = new LlmTxtFieldExtractor(
                providerFactory, Options.Create(llmOptions), XUnitLogger.CreateLogger<LlmTxtFieldExtractor>(output!));
            llmVisionExtractor = new LlmVisionFieldExtractor(
                providerFactory, XUnitLogger.CreateLogger<LlmVisionFieldExtractor>(output!));
        }

        // ── 5-8. Run per-fixture evaluation, aggregate, and emit artifacts (shared helper) ──────
        await RunEvalAsync(
            repoRoot,
            goldMap,
            id => goldMap.TryGetValue(id, out var g) ? g.PdfPath : null,
            GoldenArtifactJsonRelativePath,
            GoldenArtifactMarkdownRelativePath,
            ollamaReachable,
            llmTextExtractor,
            llmVisionExtractor,
            detExtractor,
            pdfToImageConverter,
            output,
            ct);
    }

    /// <summary>
    /// Shared per-fixture evaluation loop: for each fixture in <paramref name="goldMap"/>, resolves
    /// its PDF via <paramref name="resolvePdfPath"/>, runs all three tracks (deterministic, LLM-text,
    /// LLM-vision), scores them via the pure <see cref="LlmExtractionMetrics"/> engine, and writes
    /// the JSON + Markdown baseline artifacts. Used by both
    /// <see cref="Eval_PRP1Fixtures_ComputesFieldMetricsAndEmitsBaseline"/> (client PRP1/ corpus) and
    /// <see cref="Eval_PRP1Golden_ComputesFieldMetricsAndEmitsBaseline"/> (PRP1-golden corpus). When a
    /// fixture has no committed page images on disk, the LLM-vision track rasterizes the fixture PDF
    /// on the fly via <paramref name="pdfToImageConverter"/> (the same converter the deterministic
    /// OCR track uses) instead of structurally skipping.
    /// </summary>
    private static async Task RunEvalAsync(
        string repoRoot,
        IReadOnlyDictionary<string, GoldFixture> goldMap,
        Func<string, string?> resolvePdfPath,
        string artifactJsonRel,
        string artifactMdRel,
        bool ollamaReachable,
        ILlmExpedienteExtractor<TxtSource>? llmText,
        ILlmExpedienteExtractor<ImageSource>? llmVision,
        PdfOcrFieldExtractor det,
        PdfToImageConverter pdfToImageConverter,
        ITestOutputHelper? output,
        CancellationToken ct)
    {
        var allFieldResults = new List<FieldEvalResult>();
        var evaluatedFixtures = new List<string>();

        foreach (var (fixtureId, gold) in goldMap)
        {
            var pdfPath = resolvePdfPath(fixtureId);
            if (pdfPath is null || !File.Exists(pdfPath))
            {
                output?.WriteLine($"[{fixtureId}] PDF not found on disk — skipping fixture entirely.");
                continue;
            }

            // Fixture assets (committed .ocr.txt / page images, if any) live alongside the PDF —
            // this generalizes correctly for both the flat client PdfFixtureDir (one shared dir) and
            // the PRP1-golden per-fixture subdirectories.
            var fixtureDir = Path.GetDirectoryName(pdfPath) ?? repoRoot;

            output?.WriteLine($"\n[{fixtureId}] Evaluating…");
            evaluatedFixtures.Add(fixtureId);
            var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct);

            // ── Track 1: Deterministic (real Tesseract OCR → AdaptiveTxtFieldExtractor) ─────
            string? detExpediente = null;
            string? detOficio = null;
            string? detAuthority = null;
            string? detOcrText = null;
            string? detFailureReason = null;

            try
            {
                var detResult = await det.ExtractFieldsAsync(new PdfSource(pdfBytes), Array.Empty<FieldDefinition>());
                if (detResult.IsSuccess && detResult.Value is not null)
                {
                    detExpediente = detResult.Value.Expediente;
                    detResult.Value.AdditionalFields.TryGetValue("NumeroOficio", out detOficio);
                    detResult.Value.AdditionalFields.TryGetValue("AutoridadNombre", out detAuthority);
                    detResult.Value.AdditionalFields.TryGetValue("_OcrText", out detOcrText);
                }
                else
                {
                    detFailureReason = detResult.Error ?? "deterministic extraction returned no value";
                }
            }
            catch (Exception ex)
            {
                // Never let a native OCR failure throw out of the harness.
                detFailureReason = $"exception: {ex.Message}";
            }

            AddDeterministicFieldResults(fixtureId, gold, detExpediente, detOficio, detAuthority, detFailureReason, allFieldResults, output);

            // ── Resolve OCR text feed for the LLM-text track (M1: apples-to-apples with the
            // deterministic track — feed it the SAME OCR text the deterministic extractor actually
            // produced, so both tracks are compared on identical input). The committed `.ocr.txt`
            // companion (only present for 222AAA) is used ONLY as a fallback when the deterministic
            // track genuinely produced no OCR text at all. ──────────────────────────────────────
            string? ocrTextForLlmText;
            string ocrTextSource;
            if (!string.IsNullOrWhiteSpace(detOcrText))
            {
                ocrTextForLlmText = detOcrText;
                ocrTextSource = "deterministic-track-ocr (apples-to-apples)";
            }
            else
            {
                ocrTextForLlmText = TryLoadCommittedOcrText(fixtureDir, fixtureId);
                ocrTextSource = ocrTextForLlmText is not null
                    ? "committed-.ocr.txt (fallback: deterministic track produced no OCR text)"
                    : "(none available)";
            }

            output?.WriteLine($"  [LlmText] OCR text source: {ocrTextSource}");

            string? textTrackSkipReason = null;
            if (llmText is null)
            {
                textTrackSkipReason = "Ollama unreachable";
            }
            else if (string.IsNullOrWhiteSpace(ocrTextForLlmText))
            {
                textTrackSkipReason = detFailureReason is not null
                    ? $"deterministic OCR failed ({detFailureReason}) and no committed .ocr.txt available"
                    : "no OCR text available and no committed .ocr.txt";
            }

            // ── Track 2: LLM-text ───────────────────────────────────────────────────────────
            if (textTrackSkipReason is not null)
            {
                AddExpedienteFieldResults(fixtureId, EvalTrack.LlmText, gold, null, textTrackSkipReason, allFieldResults, output);
            }
            else
            {
                Expediente? llmTextExpediente = null;
                string? llmTextFailureReason = null;
                try
                {
                    var txtSource = new TxtSource(ocrTextForLlmText!);
                    var llmTextResult = await llmText!.ExtractExpedienteAsync(txtSource, ct);
                    if (llmTextResult.IsSuccess && llmTextResult.Value is not null)
                    {
                        llmTextExpediente = llmTextResult.Value;
                    }
                    else
                    {
                        llmTextFailureReason = llmTextResult.Error ?? "llm-text extraction returned no value";
                    }
                }
                catch (Exception ex)
                {
                    llmTextFailureReason = $"exception: {ex.Message}";
                }

                // C4: a per-fixture LLM call that throws, times out, returns a failed Result, fails
                // JSON parsing, or is gate-rejected is an infra/gate SKIP (TrackSkipped), NOT a model
                // miss (Missing) — passing a real skipReason here (instead of null) makes that
                // attributable in the artifact rather than silently blaming the model's accuracy.
                var llmTextSkipReason = llmTextExpediente is null ? llmTextFailureReason : null;
                if (llmTextSkipReason is not null)
                {
                    output?.WriteLine($"  [LlmText] attempted but failed: {llmTextSkipReason}");
                }

                AddExpedienteFieldResults(fixtureId, EvalTrack.LlmText, gold, llmTextExpediente, llmTextSkipReason, allFieldResults, output);
            }

            // ── Track 3: LLM-vision ─────────────────────────────────────────────────────────
            var pageImages = LoadFixturePageImages(fixtureDir, fixtureId);
            string? visionTrackSkipReason = null;
            if (llmVision is null)
            {
                visionTrackSkipReason = "Ollama unreachable";
            }
            else if (pageImages.Count == 0)
            {
                // No pre-rendered page images committed for this fixture (e.g. PRP1-golden ships
                // only *.pdf + ground_truth.json, no page images) — rasterize the fixture PDF on
                // the fly via the SAME converter the deterministic OCR track used above, so the
                // vision track can actually run instead of structurally skipping.
                var rasterResult = await pdfToImageConverter.ConvertToImagesAsync(pdfBytes, VisionRasterDpi, ct);
                if (rasterResult.IsSuccess && rasterResult.Value is { Count: > 0 } rasterized)
                {
                    if (rasterized.Count > VisionRasterMaxPages)
                    {
                        output?.WriteLine(
                            $"  [LlmVision] rasterized {rasterized.Count} page(s) — truncating to first {VisionRasterMaxPages} to bound payload.");
                        pageImages = rasterized.Take(VisionRasterMaxPages).ToList();
                    }
                    else
                    {
                        pageImages = rasterized;
                    }

                    output?.WriteLine(
                        $"  [LlmVision] no on-disk page images — rasterized {pageImages.Count} page(s) from PDF at {VisionRasterDpi} DPI (fallback).");
                }
                else
                {
                    visionTrackSkipReason =
                        $"no on-disk images; pdf rasterization failed: {rasterResult.Error ?? "empty result"}";
                }
            }

            if (visionTrackSkipReason is not null)
            {
                AddExpedienteFieldResults(fixtureId, EvalTrack.LlmVision, gold, null, visionTrackSkipReason, allFieldResults, output);
            }
            else
            {
                Expediente? llmVisionExpediente = null;
                string? llmVisionFailureReason = null;
                try
                {
                    var imageSource = new ImageSource(fixtureId, pageImages);
                    var llmVisionResult = await llmVision!.ExtractExpedienteAsync(imageSource, ct);
                    if (llmVisionResult.IsSuccess && llmVisionResult.Value is not null)
                    {
                        llmVisionExpediente = llmVisionResult.Value;
                    }
                    else
                    {
                        llmVisionFailureReason = llmVisionResult.Error ?? "llm-vision extraction returned no value";
                    }
                }
                catch (Exception ex)
                {
                    llmVisionFailureReason = $"exception: {ex.Message}";
                }

                // C4: same reasoning as the LLM-text track above — a per-fixture failure is an
                // infra/gate skip, not a model miss.
                var llmVisionSkipReason = llmVisionExpediente is null ? llmVisionFailureReason : null;
                if (llmVisionSkipReason is not null)
                {
                    output?.WriteLine($"  [LlmVision] attempted but failed: {llmVisionSkipReason}");
                }

                AddExpedienteFieldResults(fixtureId, EvalTrack.LlmVision, gold, llmVisionExpediente, llmVisionSkipReason, allFieldResults, output);
            }
        }

        // ── Aggregate metrics via the PURE engine ───────────────────────────────
        var accuracy = LlmExtractionMetrics.AggregateAccuracy(allFieldResults);
        var coverage = LlmExtractionMetrics.AggregateCoverage(allFieldResults);

        // ── Emit JSON + Markdown baseline artifacts ─────────────────────────────
        var generatedAt = DateTimeOffset.UtcNow;
        var jsonPath = Path.Combine(repoRoot, artifactJsonRel);
        var mdPath = Path.Combine(repoRoot, artifactMdRel);

        await WriteJsonArtifactAsync(
            jsonPath, generatedAt, ollamaReachable, evaluatedFixtures, allFieldResults, accuracy, coverage, ct);
        await WriteMarkdownArtifactAsync(
            mdPath, generatedAt, ollamaReachable, evaluatedFixtures, allFieldResults, accuracy, coverage, ct);

        // ── Summary ──────────────────────────────────────────────────────────────
        output?.WriteLine("\n=== SUMMARY ===");
        output?.WriteLine($"Fixtures evaluated : {evaluatedFixtures.Count} ({string.Join(", ", evaluatedFixtures)})");
        output?.WriteLine($"Field evaluations  : {allFieldResults.Count}");
        foreach (var a in accuracy.OrderBy(a => a.Field).ThenBy(a => a.Track))
        {
            output?.WriteLine($"  Accuracy  [{a.Field,-17} / {a.Track,-13}] {a.Matches}/{a.Evaluable} = {a.Accuracy:P0}");
        }

        foreach (var c in coverage.OrderBy(c => c.Track))
        {
            output?.WriteLine($"  Coverage  [{c.Track,-13}] {c.NonNullCandidates}/{c.TotalEvaluations} = {c.Coverage:P0}");
        }

        output?.WriteLine($"\n{SmallNCaveat}");
        output?.WriteLine($"\nArtifact (JSON): {jsonPath}");
        output?.WriteLine($"Artifact (Markdown): {mdPath}");

        // The harness always passes (it is a measurement, not a gate) — sanity check only.
        allFieldResults.ShouldNotBeEmpty();
    }

    // ───────────────────────────────────────────────────────────────────────
    // Track result → FieldEvalResult wiring
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds the four field evaluations for the deterministic track. Deterministic never extracts
    /// <c>SolicitudPartes</c> today (architecturally out of scope), so <see cref="EvalField.ParteCount"/>
    /// is always <see cref="EvalMatchStatus.TrackSkipped"/> for this track regardless of OCR success.
    /// </summary>
    private static void AddDeterministicFieldResults(
        string fixtureId,
        GoldFixture gold,
        string? numeroExpediente,
        string? numeroOficio,
        string? autoridadNombre,
        string? skipReason,
        List<FieldEvalResult> sink,
        ITestOutputHelper? output)
    {
        const EvalTrack track = EvalTrack.Deterministic;

        if (skipReason is not null)
        {
            sink.Add(LlmExtractionMetrics.Evaluate(fixtureId, track, EvalField.NumeroExpediente, gold.NumeroExpediente, null, skipReason));
            sink.Add(LlmExtractionMetrics.Evaluate(fixtureId, track, EvalField.NumeroOficio, gold.NumeroOficio, null, skipReason));
            sink.Add(LlmExtractionMetrics.Evaluate(fixtureId, track, EvalField.AutoridadNombre, gold.AutoridadNombre, null, skipReason));
            output?.WriteLine($"  [Deterministic] SKIPPED — {skipReason}");
        }
        else
        {
            sink.Add(LlmExtractionMetrics.Evaluate(fixtureId, track, EvalField.NumeroExpediente, gold.NumeroExpediente, numeroExpediente));
            sink.Add(LlmExtractionMetrics.Evaluate(fixtureId, track, EvalField.NumeroOficio, gold.NumeroOficio, numeroOficio));
            sink.Add(LlmExtractionMetrics.Evaluate(fixtureId, track, EvalField.AutoridadNombre, gold.AutoridadNombre, autoridadNombre));
            output?.WriteLine(
                $"  [Deterministic] Expediente={numeroExpediente ?? "(null)"} Oficio={numeroOficio ?? "(null)"} Autoridad={autoridadNombre ?? "(null)"}");
        }

        // Structural skip — deterministic never attempts SolicitudPartes extraction at all.
        sink.Add(LlmExtractionMetrics.EvaluateParteCount(
            fixtureId, track, gold.ParteCount, null,
            "deterministic track does not extract SolicitudPartes (architecturally out of scope)"));
    }

    /// <summary>
    /// Adds the four field evaluations for an LLM track (text or vision) from a full
    /// <see cref="Expediente"/> candidate (or a skip reason when the track was not attempted).
    /// </summary>
    private static void AddExpedienteFieldResults(
        string fixtureId,
        EvalTrack track,
        GoldFixture gold,
        Expediente? candidate,
        string? skipReason,
        List<FieldEvalResult> sink,
        ITestOutputHelper? output)
    {
        if (skipReason is not null)
        {
            sink.Add(LlmExtractionMetrics.Evaluate(fixtureId, track, EvalField.NumeroExpediente, gold.NumeroExpediente, null, skipReason));
            sink.Add(LlmExtractionMetrics.Evaluate(fixtureId, track, EvalField.NumeroOficio, gold.NumeroOficio, null, skipReason));
            sink.Add(LlmExtractionMetrics.Evaluate(fixtureId, track, EvalField.AutoridadNombre, gold.AutoridadNombre, null, skipReason));
            sink.Add(LlmExtractionMetrics.EvaluateParteCount(fixtureId, track, gold.ParteCount, null, skipReason));
            output?.WriteLine($"  [{track}] SKIPPED — {skipReason}");
            return;
        }

        sink.Add(LlmExtractionMetrics.Evaluate(fixtureId, track, EvalField.NumeroExpediente, gold.NumeroExpediente, candidate?.NumeroExpediente));
        sink.Add(LlmExtractionMetrics.Evaluate(fixtureId, track, EvalField.NumeroOficio, gold.NumeroOficio, candidate?.NumeroOficio));
        sink.Add(LlmExtractionMetrics.Evaluate(fixtureId, track, EvalField.AutoridadNombre, gold.AutoridadNombre, candidate?.AutoridadNombre));
        sink.Add(LlmExtractionMetrics.EvaluateParteCount(fixtureId, track, gold.ParteCount, candidate?.SolicitudPartes.Count));

        output?.WriteLine(
            $"  [{track}] Expediente={candidate?.NumeroExpediente ?? "(null)"} Oficio={candidate?.NumeroOficio ?? "(null)"} " +
            $"Autoridad={candidate?.AutoridadNombre ?? "(null)"} Partes={(candidate is null ? "(null)" : candidate.SolicitudPartes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))}");
    }

    // ───────────────────────────────────────────────────────────────────────
    // Fixture asset loading (page images / committed OCR text)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a committed <c>*.ocr.txt</c> companion for a fixture, if any exists on disk (only
    /// <c>222AAA</c> has one today). This is used ONLY as a fallback for the LLM-text track when the
    /// deterministic track's own OCR text is genuinely unavailable (M1: apples-to-apples — the
    /// deterministic track's live OCR output is otherwise always preferred so both tracks see
    /// identical input).
    /// </summary>
    private static string? TryLoadCommittedOcrText(string fixtureDir, string fixtureId)
    {
        var files = Directory.GetFiles(fixtureDir, $"{fixtureId}_page*.ocr.txt")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            return null;
        }

        var combined = string.Join("\n\n", files.Select(File.ReadAllText));
        return string.IsNullOrWhiteSpace(combined) ? null : combined;
    }

    /// <summary>
    /// Loads committed page-image bytes for a fixture. Prefers the zero-padded, dash-numbered jpg
    /// sequence (genuine multi-page rendering, e.g. <c>_page-0001.jpg</c>); falls back to any
    /// single/unpadded page image (e.g. <c>_page1.png</c>).
    /// </summary>
    private static IReadOnlyList<byte[]> LoadFixturePageImages(string fixtureDir, string fixtureId)
    {
        var padded = Directory.GetFiles(fixtureDir, $"{fixtureId}_page-????.jpg")
            .Concat(Directory.GetFiles(fixtureDir, $"{fixtureId}_page-????.jpeg"))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var chosen = padded.Count > 0
            ? padded
            : Directory.GetFiles(fixtureDir, $"{fixtureId}_page*.png")
                .Concat(Directory.GetFiles(fixtureDir, $"{fixtureId}_page*.jpg"))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

        return chosen.Select(File.ReadAllBytes).ToList();
    }

    // ───────────────────────────────────────────────────────────────────────
    // Repo root / gold loading (unchanged helpers, reused from the earlier stub)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Locates the repository root: the directory that actually contains the gold fixture
    /// <see cref="ParsedDocumentsFile"/>.
    /// </summary>
    /// <remarks>
    /// Runtime paths are useless here: the build output goes to a SEPARATE tree
    /// (<c>.../BuildArtifacts/Prisma/bin/...</c>) that is NOT under the repo root, so walking up
    /// from <c>AppContext.BaseDirectory</c> or the CWD can never reach the repo — and worse, that
    /// BuildArtifacts tree also contains a "Prisma" directory, so a bare directory-name check
    /// resolves to the wrong root and the harness silently passes green having written no artifact.
    /// We therefore anchor first on the COMPILE-TIME source location of this file
    /// (<paramref name="sourceFilePath"/> via <see cref="CallerFilePathAttribute"/>), which lives in
    /// the real repo tree, and require the gold file to actually exist under the candidate root.
    /// CWD and BaseDirectory are kept as fallbacks. Valid because this manual harness is run on the
    /// same box it was built on.
    /// </remarks>
    private static string? LocateRepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
    {
        foreach (var start in new[] { sourceFilePath, Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            if (string.IsNullOrEmpty(start))
                continue;

            // For a file path, start walking from its containing directory.
            var startDir = File.Exists(start) ? Path.GetDirectoryName(start) : start;
            var dir = startDir is null ? null : new DirectoryInfo(startDir);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, ParsedDocumentsFile)))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds a dictionary from fixture ID (e.g. "222AAA-44444444442025") to
    /// <see cref="GoldFixture"/> by parsing the XML companion content embedded in
    /// <c>parsed_documents.json</c>.
    /// </summary>
    private static Dictionary<string, GoldFixture> BuildGoldMap(
        string parsedDocsPath,
        string summaryPath,
        ITestOutputHelper? output)
    {
        var result = new Dictionary<string, GoldFixture>(StringComparer.OrdinalIgnoreCase);

        JsonDocument parsedDocs;
        try
        {
            parsedDocs = JsonDocument.Parse(File.ReadAllText(parsedDocsPath));
        }
        catch (Exception ex)
        {
            output?.WriteLine($"WARNING: failed to parse {parsedDocsPath}: {ex.Message}");
            return result;
        }

        foreach (var prop in parsedDocs.RootElement.EnumerateObject())
        {
            // We only care about XML entries — they carry the ground truth.
            var key = prop.Name; // e.g. "222AAA-44444444442025.xml"
            if (!key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                continue;

            var fixtureId = Path.GetFileNameWithoutExtension(key);

            if (!prop.Value.TryGetProperty("content", out var contentProp))
                continue;

            var xmlContent = contentProp.GetString();
            if (string.IsNullOrWhiteSpace(xmlContent))
                continue;

            try
            {
                var gold = ParseGoldFromXml(xmlContent);
                result[fixtureId] = gold;
            }
            catch (Exception ex)
            {
                output?.WriteLine($"WARNING: could not parse XML for {fixtureId}: {ex.Message}");
            }
        }

        output?.WriteLine($"Gold fixtures loaded: {result.Count}");
        return result;
    }

    /// <summary>
    /// Builds a dictionary from fixture ID (docId, e.g. <c>"CNBV-2025-605483_20260704_050221"</c>) to
    /// <see cref="GoldFixture"/> by reading the generator-stamped <c>ground_truth.json</c> committed
    /// alongside each fixture under <c>Prisma/Fixtures/PRP1-golden/&lt;docDir&gt;/</c> (see the
    /// corpus README for the generation recipe and the source-containment gate).
    /// </summary>
    private static Dictionary<string, GoldFixture> BuildGoldMapFromGoldenDir(string goldenRoot, ITestOutputHelper? output)
    {
        var result = new Dictionary<string, GoldFixture>(StringComparer.OrdinalIgnoreCase);

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
        };

        foreach (var dir in Directory.GetDirectories(goldenRoot).OrderBy(d => d, StringComparer.Ordinal))
        {
            var dirName = Path.GetFileName(dir);
            var groundTruthPath = Path.Combine(dir, "ground_truth.json");
            if (!File.Exists(groundTruthPath))
            {
                output?.WriteLine($"[{dirName}] no ground_truth.json found — skipping.");
                continue;
            }

            var pdfPath = Directory.GetFiles(dir, "*.pdf")
                .OrderBy(f => f, StringComparer.Ordinal)
                .FirstOrDefault();
            if (pdfPath is null)
            {
                output?.WriteLine($"[{dirName}] no *.pdf found — skipping.");
                continue;
            }

            GroundTruthDocument? truth;
            try
            {
                truth = JsonSerializer.Deserialize<GroundTruthDocument>(File.ReadAllText(groundTruthPath), jsonOptions);
            }
            catch (Exception ex)
            {
                output?.WriteLine($"WARNING: could not parse {groundTruthPath}: {ex.Message}");
                continue;
            }

            if (truth is null)
            {
                output?.WriteLine($"WARNING: {groundTruthPath} parsed to null — skipping.");
                continue;
            }

            var fixtureId = truth.DocId.NullIfEmpty() ?? dirName;
            var parteCount = truth.SolicitudPartes.ValueKind == JsonValueKind.Array
                ? truth.SolicitudPartes.GetArrayLength()
                : 0;

            result[fixtureId] = new GoldFixture
            {
                NumeroExpediente = truth.NumeroExpediente,
                NumeroOficio = truth.NumeroOficio,
                AutoridadNombre = truth.AutoridadNombre,
                ParteCount = parteCount,
                PdfPath = pdfPath,
            };
        }

        output?.WriteLine($"Golden gold fixtures loaded: {result.Count}");
        return result;
    }

    /// <summary>
    /// Extracts key expected field values from a CNBV XML companion document.
    /// </summary>
    private static GoldFixture ParseGoldFromXml(string xmlContent)
    {
        // XDocument handles the CNBV ns0: namespace prefix without explicit NS resolution.
        var doc = XDocument.Parse(xmlContent);
        var root = doc.Root!;

        static string? Elem(XElement parent, string localName) =>
            parent.Descendants()
                  .FirstOrDefault(e => e.Name.LocalName == localName)?
                  .Value.Trim()
                  .NullIfEmpty();

        var parteNodes = root.Descendants()
            .Count(e => e.Name.LocalName == "SolicitudPartes");

        return new GoldFixture
        {
            NumeroExpediente = Elem(root, "Cnbv_NumeroExpediente"),
            NumeroOficio = Elem(root, "Cnbv_NumeroOficio"),
            AutoridadNombre = Elem(root, "AutoridadNombre"),
            ParteCount = parteNodes,
        };
    }

    /// <summary>
    /// Probes whether an Ollama instance is reachable at localhost:11434.
    /// </summary>
    private static async Task<bool> IsOllamaReachableAsync(CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var resp = await http.GetAsync(OllamaHealthUrl, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    // Artifact emission (JSON + Markdown)
    // ───────────────────────────────────────────────────────────────────────

    private static async Task WriteJsonArtifactAsync(
        string jsonPath,
        DateTimeOffset generatedAtUtc,
        bool ollamaReachable,
        IReadOnlyList<string> fixtures,
        IReadOnlyList<FieldEvalResult> results,
        IReadOnlyList<FieldAccuracy> accuracy,
        IReadOnlyList<TrackCoverage> coverage,
        CancellationToken ct)
    {
        var artifact = new EvalArtifact(
            generatedAtUtc.ToString("O"),
            TextModelTag,
            VisionModelTag,
            OllamaBaseUrl,
            ollamaReachable,
            SmallNCaveat,
            fixtures,
            results,
            accuracy,
            coverage);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        var json = JsonSerializer.Serialize(artifact, jsonOptions);
        var dir = Path.GetDirectoryName(jsonPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(jsonPath, json, ct).ConfigureAwait(false);
    }

    private static async Task WriteMarkdownArtifactAsync(
        string mdPath,
        DateTimeOffset generatedAtUtc,
        bool ollamaReachable,
        IReadOnlyList<string> fixtures,
        IReadOnlyList<FieldEvalResult> results,
        IReadOnlyList<FieldAccuracy> accuracy,
        IReadOnlyList<TrackCoverage> coverage,
        CancellationToken ct)
    {
        var markdown = BuildMarkdownReport(generatedAtUtc, ollamaReachable, fixtures, results, accuracy, coverage);
        var dir = Path.GetDirectoryName(mdPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(mdPath, markdown, ct).ConfigureAwait(false);
    }

    private static string BuildMarkdownReport(
        DateTimeOffset generatedAtUtc,
        bool ollamaReachable,
        IReadOnlyList<string> fixtures,
        IReadOnlyList<FieldEvalResult> results,
        IReadOnlyList<FieldAccuracy> accuracy,
        IReadOnlyList<TrackCoverage> coverage)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# LLM/Hybrid Extraction Baseline — PRP1 Gold Fixtures");
        sb.AppendLine();
        sb.AppendLine($"Generated: {generatedAtUtc:u}");
        sb.AppendLine();
        sb.AppendLine($"> **{SmallNCaveat}**");
        sb.AppendLine();
        sb.AppendLine("## Run configuration");
        sb.AppendLine();
        sb.AppendLine($"- Ollama base URL: `{OllamaBaseUrl}`");
        sb.AppendLine($"- Ollama reachable: **{ollamaReachable}**");
        sb.AppendLine($"- Text model: `{TextModelTag}`");
        sb.AppendLine($"- Vision model: `{VisionModelTag}`");
        sb.AppendLine($"- Fixtures evaluated: {fixtures.Count} ({string.Join(", ", fixtures)})");
        sb.AppendLine();
        sb.AppendLine("## Per-fixture, per-track, per-field results");
        sb.AppendLine();
        sb.AppendLine("| Fixture | Track | Field | Gold | Candidate | Status | Note |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var r in results
                     .OrderBy(r => r.FixtureId, StringComparer.Ordinal)
                     .ThenBy(r => r.Track)
                     .ThenBy(r => r.Field))
        {
            sb.AppendLine(
                $"| {r.FixtureId} | {r.Track} | {r.Field} | {Escape(r.GoldRaw)} | {Escape(r.CandidateRaw)} | {r.Status} | {Escape(r.SkipReason)} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Per-field, per-track accuracy (matches / fixtures-with-gold)");
        sb.AppendLine();
        sb.AppendLine("| Field | Track | Matches | Evaluable | Accuracy |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var a in accuracy.OrderBy(a => a.Field).ThenBy(a => a.Track))
        {
            sb.AppendLine($"| {a.Field} | {a.Track} | {a.Matches} | {a.Evaluable} | {a.Accuracy:P0} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Per-track coverage (non-null candidates / total evaluations)");
        sb.AppendLine();
        sb.AppendLine("| Track | Non-null | Total | Coverage |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var c in coverage.OrderBy(c => c.Track))
        {
            sb.AppendLine($"| {c.Track} | {c.NonNullCandidates} | {c.TotalEvaluations} | {c.Coverage:P0} |");
        }

        sb.AppendLine();
        sb.AppendLine(
            "_Generated by `LlmExtractionEvalHarness` (S4-A, manual-only — not a CI gate). See " +
            "`docs/implementation-artifacts/spec-llm-hybrid-extractor-S4A.md`._");

        return sb.ToString();
    }

    private static string Escape(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : s.Replace("|", "\\|", StringComparison.Ordinal).Replace('\n', ' ');

    // ───────────────────────────────────────────────────────────────────────
    // Hand-built real (non-mocked) service seams — no DI container in this harness
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>Real, no-op <see cref="IImagePreprocessor"/> — modern OCR engines (Tesseract) handle
    /// preprocessing internally, so this is the same pairing production uses for PDF-sourced OCR.</summary>
    private sealed class PassthroughImagePreprocessor : IImagePreprocessor
    {
        public Task<Result<ImageData>> PreprocessAsync(ImageData imageData, ProcessingConfig config) =>
            Task.FromResult(Result<ImageData>.WithSuccess(imageData));

        public Task<Result<ImageData>> RemoveWatermarkAsync(ImageData imageData) =>
            Task.FromResult(Result<ImageData>.WithSuccess(imageData));

        public Task<Result<ImageData>> DeskewAsync(ImageData imageData) =>
            Task.FromResult(Result<ImageData>.WithSuccess(imageData));

        public Task<Result<ImageData>> BinarizeAsync(ImageData imageData) =>
            Task.FromResult(Result<ImageData>.WithSuccess(imageData));
    }

    /// <summary>Real (not mocked) <see cref="IHttpClientFactory"/> that hands back a genuine
    /// <see cref="HttpClient"/> making real network calls — there is no DI container here to
    /// provide the ASP.NET Core factory, and <see cref="OllamaProvider"/> requires the interface.</summary>
    private sealed class DirectHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new() { Timeout = TimeSpan.FromMinutes(5) };
    }

    /// <summary>Minimal, real (not mocked) <see cref="IOptionsMonitor{T}"/> over a fixed value —
    /// no DI container / configuration reload is needed for a one-shot manual harness run.</summary>
    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue { get; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string?> listener) => NoopDisposable.Instance;

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    // Data transfer types
    // ───────────────────────────────────────────────────────────────────────

    private sealed record GoldFixture
    {
        public string? NumeroExpediente { get; init; }
        public string? NumeroOficio { get; init; }
        public string? AutoridadNombre { get; init; }
        public int ParteCount { get; init; }

        /// <summary>Full path to the fixture's PDF — only populated by <see cref="BuildGoldMapFromGoldenDir"/>
        /// (the client PRP1/ loader resolves its PDF path separately via a flat, shared directory).</summary>
        public string? PdfPath { get; init; }
    }

    /// <summary>Deserialization shape of a PRP1-golden <c>ground_truth.json</c> — only the fields this
    /// harness measures are modeled (solicitante/rfc/monto etc. are deliberately out of scope for now).</summary>
    private sealed record GroundTruthDocument(
        string? DocId,
        string? NumeroExpediente,
        string? NumeroOficio,
        string? AutoridadNombre,
        JsonElement SolicitudPartes);

    /// <summary>Serialized shape of the committed baseline artifact (D3).</summary>
    private sealed record EvalArtifact(
        string GeneratedAtUtc,
        string TextModel,
        string VisionModel,
        string OllamaBaseUrl,
        bool OllamaReachable,
        string Caveat,
        IReadOnlyList<string> FixturesEvaluated,
        IReadOnlyList<FieldEvalResult> Results,
        IReadOnlyList<FieldAccuracy> Accuracy,
        IReadOnlyList<TrackCoverage> Coverage);
}

file static class StringExtensions
{
    /// <summary>Returns <see langword="null"/> when the string is empty or whitespace.</summary>
    internal static string? NullIfEmpty(this string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
