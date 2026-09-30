using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using ExxerCube.Prisma.Infrastructure.Extraction.Txt;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// S4-M.1 HARD VERIFICATION GATE (see
/// <c>docs/planning-artifacts/SCOPING-llm-hybrid-S4M-gateb-measurement.md</c>, "S4-M.1.0 Diversification
/// spike VERIFIED RESULT" + "M.1 build plan"). Confirms, through the LIVE .NET pipeline (real Tesseract OCR
/// → <see cref="AdaptiveTxtFieldExtractor"/>, via <see cref="PdfOcrFieldExtractor"/> — NOT a regex replay),
/// that the diversified <c>PRP1-golden-nullslice</c> corpus (grown by <c>S4-M.1</c> from the CLI-probed
/// grounded 3-mode recipe) actually nulls <c>NumeroExpediente</c> at N&gt;=30, with the 3-way mode split
/// holding (no single mode &gt;60% of the null slice), and every fixture's gold Expediente literally
/// recoverable from its own rendered PDF via <c>pdftotext</c> (D7.1 source-containment).
/// </summary>
/// <remarks>
/// This is a MEASUREMENT-ONLY probe, not a CI gate and not a regression test — mirrors
/// <see cref="S4M0_DeterministicNullExpedienteProbe"/>'s discipline exactly, just against the grown,
/// diversified corpus instead of the original 20-fixture PRP1-golden. No production code is touched; no
/// committed baseline artifact is written.
/// </remarks>
public sealed class S4M1_DeterministicNullDiversifiedCorpusProbe
{
    private const string NullSliceCorpusRelativeDir = "Prisma/Fixtures/PRP1-golden-nullslice";

    /// <summary>ADR-024 D6 rule of thumb: the eventual conditional-precision slice wants N&gt;=30.</summary>
    private const int MinDecidableSliceSize = 30;

    /// <summary>
    /// Owner ruling (S4-M.1.0 spike): diversify, don't monoculture — no single grounded failure mode may
    /// dominate the null slice, or the eventual precision number would just measure one bug repeatedly.
    /// </summary>
    private const double MaxSingleModeShare = 0.60;

    [Fact(Skip = "eval-only: S4-M.1 measurement probe — run manually (real Tesseract OCR over 36 fixtures, " +
        "~3 min), remove Skip to execute. RESULT pre-hardening (2026-07-19): 36/36 NULL (12/12/12 even " +
        "3-mode split), D7.1 36/36, VERDICT GO. RESULT post-Direction-#1 hardening (2026-07-21, `2aaf1b2b`): " +
        "13/36 NULL — mode1-FI1 0/12 (all recovered), mode3-spaced 1/12 (single fixture-specific OCR-noise " +
        "residual; CLI tesseract reads it fine, identical-shape siblings recovered), mode2-emdash 12/12 " +
        "(unhandled by design). D7.1 still 36/36; no SIGSEGV. Confirms the field-level Gate-B closure for " +
        "NumeroExpediente (evidence packet §9). Not a CI gate.")]
    public async Task Probe_DeterministicTrack_CountsNullNumeroExpedienteOverDiversifiedNullSlice()
    {
        var ct = TestContext.Current.CancellationToken;
        var output = TestContext.Current.TestOutputHelper;

        var repoRoot = LocateRepoRoot();
        repoRoot.ShouldNotBeNull(
            $"could not locate repo root (no directory containing {NullSliceCorpusRelativeDir} found from source file location).");

        var corpusRoot = Path.Combine(repoRoot!, NullSliceCorpusRelativeDir);
        Directory.Exists(corpusRoot).ShouldBeTrue($"null-slice corpus directory not found: {corpusRoot}");

        var fixtureDirs = Directory.GetDirectories(corpusRoot).OrderBy(d => d, StringComparer.Ordinal).ToList();
        fixtureDirs.ShouldNotBeEmpty($"no fixture subdirectories found under {corpusRoot}");

        using var ocrExecutor = new TesseractOcrExecutor(XUnitLogger.CreateLogger<TesseractOcrExecutor>(output!));
        var imagePreprocessor = new NoopImagePreprocessor();
        var pdfToImageConverter = new PdfToImageConverter(XUnitLogger.CreateLogger<PdfToImageConverter>(output!));
        var adaptiveTxtExtractor = new AdaptiveTxtFieldExtractor(XUnitLogger.CreateLogger<AdaptiveTxtFieldExtractor>(output!));
        var detExtractor = new PdfOcrFieldExtractor(
            ocrExecutor, imagePreprocessor, pdfToImageConverter, adaptiveTxtExtractor,
            XUnitLogger.CreateLogger<PdfOcrFieldExtractor>(output!));

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

        var results = new List<(string FixtureId, string Mode, string GoldExpediente, bool NullExpediente,
            string? DetValue, string? Reason, bool? PdfContained)>();

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
            var goldExpediente = truth?.NumeroExpediente ?? string.Empty;
            var mode = ClassifyMode(goldExpediente);

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

            // D7.1 source-containment: is the gold Expediente literally recoverable from THIS fixture's
            // own rendered PDF via `pdftotext` (independent of the deterministic extractor's regex)?
            bool? pdfContained = VerifyPdftotextContainment(pdfPath, goldExpediente, output);

            results.Add((fixtureId, mode, goldExpediente, isNull, detExpediente, failureReason, pdfContained));

            output?.WriteLine(
                $"[{fixtureId}] mode={mode} gold='{goldExpediente}' det='{detExpediente ?? "(NULL)"}' " +
                $"{(isNull ? "→ NULL-SLICE MEMBER" : "→ non-null")} pdfContained={pdfContained?.ToString() ?? "(unchecked)"}" +
                (failureReason is not null ? $" reason='{failureReason}'" : string.Empty));
        }

        var nullSlice = results.Where(r => r.NullExpediente).ToList();
        var n = nullSlice.Count;
        var total = results.Count;

        output?.WriteLine(string.Empty);
        output?.WriteLine("=== S4-M.1 HEADLINE ===");
        output?.WriteLine($"Deterministic-NULL slice: {n}/{total}");

        var byMode = nullSlice.GroupBy(r => r.Mode).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
        foreach (var group in byMode)
        {
            var share = n == 0 ? 0.0 : (double)group.Count() / n;
            output?.WriteLine($"  mode={group.Key}: {group.Count()}/{n} ({share:P0}) — {string.Join(", ", group.Select(r => r.FixtureId))}");
        }

        var notNulled = results.Where(r => !r.NullExpediente).ToList();
        if (notNulled.Count > 0)
        {
            output?.WriteLine(string.Empty);
            output?.WriteLine($"Fixtures that did NOT null ({notNulled.Count}) — det value recovered despite the injected mode:");
            foreach (var r in notNulled)
            {
                output?.WriteLine($"  - [{r.Mode}] {r.FixtureId}: gold='{r.GoldExpediente}' det='{r.DetValue}'");
            }
        }

        var notPdfContained = results.Where(r => r.PdfContained == false).ToList();
        output?.WriteLine(string.Empty);
        output?.WriteLine($"D7.1 source-containment (pdftotext): {results.Count(r => r.PdfContained == true)}/{results.Count} contained.");
        if (notPdfContained.Count > 0)
        {
            output?.WriteLine($"NOT pdftotext-contained ({notPdfContained.Count}):");
            foreach (var r in notPdfContained)
            {
                output?.WriteLine($"  - [{r.Mode}] {r.FixtureId}: gold='{r.GoldExpediente}'");
            }
        }

        output?.WriteLine(string.Empty);
        var sliceOk = n >= MinDecidableSliceSize;
        var maxShare = byMode.Count == 0 ? 0.0 : byMode.Max(g => (double)g.Count() / Math.Max(n, 1));
        var splitOk = byMode.Count >= 3 && maxShare <= MaxSingleModeShare;

        if (sliceOk && splitOk)
        {
            output?.WriteLine(
                $"VERDICT: GO — slice size {n} >= {MinDecidableSliceSize}, 3-way split holds (max mode share {maxShare:P0} <= {MaxSingleModeShare:P0}).");
        }
        else
        {
            output?.WriteLine(
                $"VERDICT: NOT-YET-DECIDABLE — slice size {n} (need >= {MinDecidableSliceSize}) or split does not hold " +
                $"(modes represented: {byMode.Count}, max share {maxShare:P0}, cap {MaxSingleModeShare:P0}).");
        }

        // Sanity only: the probe must have actually evaluated fixtures (a silent 0/0 pass would be
        // exactly the trap this measurement exists to avoid).
        results.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Classifies a gold Expediente string by which of the 3 S4-M.1.0-grounded generator knobs produced
    /// it — purely from the value's own shape (em-dash delimiter / spaced-hyphen delimiter / forced FI1
    /// area code with default delimiter), independent of and in addition to the live extractor's verdict.
    /// </summary>
    private static string ClassifyMode(string expediente)
    {
        if (expediente.Contains('—'))
            return "mode2-emdash";
        if (expediente.Contains(" - "))
            return "mode3-spaced";
        if (expediente.Contains("/FI1-"))
            return "mode1-FI1";
        return "UNKNOWN";
    }

    /// <summary>
    /// Runs the `pdftotext` CLI (poppler-utils) against the fixture's own rendered PDF and checks
    /// (whitespace-normalized) that the gold Expediente literally appears — the same D7.1 discipline the
    /// generator itself applies at generation time (see <c>main_generator.py:_check_body_containment</c>),
    /// re-verified here independently against the actual on-disk PDF. Returns <c>null</c> (unchecked, not a
    /// failure) if `pdftotext` isn't installed or invocation fails for any reason.
    /// </summary>
    private static bool? VerifyPdftotextContainment(string pdfPath, string expediente, Xunit.ITestOutputHelper? output)
    {
        try
        {
            var psi = new ProcessStartInfo("pdftotext")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add(pdfPath);
            psi.ArgumentList.Add("-");

            using var proc = Process.Start(psi);
            if (proc is null)
                return null;

            var text = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(30_000);
            if (proc.ExitCode != 0)
                return null;

            var normalizedText = Regex.Replace(text, @"\s+", " ");
            var normalizedExpediente = Regex.Replace(expediente, @"\s+", " ").Trim();
            return !string.IsNullOrEmpty(normalizedExpediente) && normalizedText.Contains(normalizedExpediente, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            output?.WriteLine($"   ⚠️ pdftotext check failed for {pdfPath}: {ex.Message}");
            return null;
        }
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
                if (Directory.Exists(Path.Combine(dir.FullName, NullSliceCorpusRelativeDir)))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }

        return null;
    }

    private sealed record GroundTruthDoc(string? DocId, string? NumeroExpediente);
}
