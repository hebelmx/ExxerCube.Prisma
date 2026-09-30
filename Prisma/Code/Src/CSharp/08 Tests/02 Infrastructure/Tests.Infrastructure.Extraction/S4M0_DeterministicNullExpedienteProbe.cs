using System.Text.Json;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using ExxerCube.Prisma.Infrastructure.Extraction.Txt;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// S4-M.0 MAKE-OR-BREAK measurement probe (see
/// <c>docs/planning-artifacts/spec-llm-hybrid-extractor-S4M.md</c> / the Gate-B measurement harness
/// story). Answers ONE question from ground truth: over the 20-fixture PRP1-golden corpus, how many
/// fixtures does the real deterministic track (Tesseract OCR → <see cref="AdaptiveTxtFieldExtractor"/>,
/// via <see cref="PdfOcrFieldExtractor"/>) return NULL/empty for <c>NumeroExpediente</c>? That count is
/// the "deterministic-NULL slice" the S4 LLM fallback would ever run on — the spec claims ~3/20.
/// </summary>
/// <remarks>
/// This is a MEASUREMENT-ONLY probe, not a CI gate and not a regression test: it makes no behavioral
/// assertion about which fixtures are NULL (that would bake today's OCR variance into a brittle
/// expectation). It only asserts the probe itself completed (ran all 20 fixtures without an unhandled
/// native crash) and prints the per-fixture verdict + a loud GO/GROW-corpus verdict to test output.
/// No production code is touched; no committed baseline artifact is written (unlike
/// <see cref="LlmExtractionEvalHarness"/>, which writes JSON/MD baselines — deliberately not reused
/// here to avoid clobbering those committed files).
/// </remarks>
public sealed class S4M0_DeterministicNullExpedienteProbe
{
    private const string GoldenCorpusRelativeDir = "Prisma/Fixtures/PRP1-golden";

    /// <summary>
    /// Rule of thumb from ADR-024 D6: the eventual conditional-precision slice wants N&gt;=30 to be
    /// decidable. A 20-fixture corpus can therefore never itself satisfy the bar; the real question
    /// this probe answers is whether the deterministic-NULL SUBSET is anywhere close, or whether it is
    /// so small that the next story must grow the corpus before any conditional metric is meaningful.
    /// </summary>
    private const int MinDecidableSliceSize = 30;

    [Fact(Skip = "eval-only: S4-M.0 measurement probe — run manually (real Tesseract OCR, ~90s), remove Skip to execute. " +
        "RESULT (2026-07-19, verified GREEN): 3/20 NULL slice, matches spec. Not a CI gate / not a regression test.")]
    public async Task Probe_DeterministicTrack_CountsNullNumeroExpedienteOverPRP1Golden()
    {
        var ct = TestContext.Current.CancellationToken;
        var output = TestContext.Current.TestOutputHelper;

        var repoRoot = LocateRepoRoot();
        repoRoot.ShouldNotBeNull(
            $"could not locate repo root (no directory containing {GoldenCorpusRelativeDir} found from source file location).");

        var goldenRoot = Path.Combine(repoRoot!, GoldenCorpusRelativeDir);
        Directory.Exists(goldenRoot).ShouldBeTrue($"golden corpus directory not found: {goldenRoot}");

        var fixtureDirs = Directory.GetDirectories(goldenRoot).OrderBy(d => d, StringComparer.Ordinal).ToList();
        fixtureDirs.ShouldNotBeEmpty($"no fixture subdirectories found under {goldenRoot}");

        using var ocrExecutor = new TesseractOcrExecutor(XUnitLogger.CreateLogger<TesseractOcrExecutor>(output!));
        var imagePreprocessor = new NoopImagePreprocessor();
        var pdfToImageConverter = new PdfToImageConverter(XUnitLogger.CreateLogger<PdfToImageConverter>(output!));
        var adaptiveTxtExtractor = new AdaptiveTxtFieldExtractor(XUnitLogger.CreateLogger<AdaptiveTxtFieldExtractor>(output!));
        var detExtractor = new PdfOcrFieldExtractor(
            ocrExecutor, imagePreprocessor, pdfToImageConverter, adaptiveTxtExtractor,
            XUnitLogger.CreateLogger<PdfOcrFieldExtractor>(output!));

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

        var results = new List<(string FixtureId, bool NullExpediente, string? DetValue, string? Reason)>();

        foreach (var dir in fixtureDirs)
        {
            var dirName = Path.GetFileName(dir);
            var groundTruthPath = Path.Combine(dir, "ground_truth.json");
            var pdfPath = Directory.GetFiles(dir, "*.pdf").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();

            if (!File.Exists(groundTruthPath) || pdfPath is null)
            {
                output?.WriteLine($"[{dirName}] SKIPPED — missing ground_truth.json or *.pdf.");
                continue;
            }

            GroundTruthDoc? truth;
            try
            {
                truth = JsonSerializer.Deserialize<GroundTruthDoc>(await File.ReadAllTextAsync(groundTruthPath, ct), jsonOptions);
            }
            catch (Exception ex)
            {
                output?.WriteLine($"[{dirName}] SKIPPED — could not parse ground_truth.json: {ex.Message}");
                continue;
            }

            var fixtureId = string.IsNullOrWhiteSpace(truth?.DocId) ? dirName : truth!.DocId!;
            var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct);

            string? detExpediente = null;
            string? failureReason = null;
            try
            {
                var detResult = await detExtractor.ExtractFieldsAsync(new PdfSource(pdfBytes), Array.Empty<FieldDefinition>());
                if (detResult.IsSuccess && detResult.Value is not null)
                {
                    detExpediente = detResult.Value.Expediente;
                }
                else
                {
                    failureReason = detResult.Error ?? "deterministic extraction returned no value";
                }
            }
            catch (Exception ex)
            {
                // Never let a native OCR failure throw out of the probe — record it as evidence instead.
                failureReason = $"exception: {ex.GetType().Name}: {ex.Message}";
            }

            var isNull = string.IsNullOrWhiteSpace(detExpediente);
            results.Add((fixtureId, isNull, detExpediente, failureReason));

            var goldExpediente = truth?.NumeroExpediente ?? "(no gold)";
            output?.WriteLine(
                $"[{fixtureId}] gold='{goldExpediente}' det='{detExpediente ?? "(NULL)"}' " +
                $"{(isNull ? "→ NULL-SLICE MEMBER" : "→ non-null")}" +
                (failureReason is not null ? $" reason='{failureReason}'" : string.Empty));
        }

        var nullSlice = results.Where(r => r.NullExpediente).ToList();
        var n = nullSlice.Count;
        var total = results.Count;

        output?.WriteLine(string.Empty);
        output?.WriteLine("=== S4-M.0 HEADLINE ===");
        output?.WriteLine($"Deterministic-NULL slice: {n}/{total}");
        output?.WriteLine($"NULL fixtures: {(nullSlice.Count == 0 ? "(none)" : string.Join(", ", nullSlice.Select(r => r.FixtureId)))}");
        foreach (var r in nullSlice)
        {
            output?.WriteLine($"  - {r.FixtureId}: reason='{r.Reason ?? "(no failure reason — extractor ran clean but Expediente was null/empty)"}'");
        }

        output?.WriteLine(string.Empty);
        if (n >= MinDecidableSliceSize)
        {
            output?.WriteLine($"VERDICT: GO — slice size {n} >= {MinDecidableSliceSize} (ADR-024 D6 rule of thumb). Conditional precision is decidable on this corpus.");
        }
        else
        {
            output?.WriteLine(
                $"VERDICT: GROW-CORPUS — slice size {n} < {MinDecidableSliceSize} (ADR-024 D6 rule of thumb). " +
                "S4-M.1 must grow the PRP1-golden corpus (or a similar deterministic-NULL-inducing set) before any conditional-precision number can be computed.");
        }

        // Sanity only: the probe must have actually evaluated fixtures (a silent 0/0 pass would be
        // exactly the trap this measurement exists to avoid).
        results.ShouldNotBeEmpty();
    }

    /// <summary>Real, no-op <see cref="IImagePreprocessor"/> — same pairing production/eval-harness use.</summary>
    private sealed class NoopImagePreprocessor : IImagePreprocessor
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

    private static string? LocateRepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
    {
        foreach (var start in new[] { sourceFilePath, Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            if (string.IsNullOrEmpty(start))
                continue;

            var startDir = File.Exists(start) ? Path.GetDirectoryName(start) : start;
            var dir = startDir is null ? null : new DirectoryInfo(startDir);
            while (dir is not null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, GoldenCorpusRelativeDir)))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }

        return null;
    }

    private sealed record GroundTruthDoc(string? DocId, string? NumeroExpediente);
}
