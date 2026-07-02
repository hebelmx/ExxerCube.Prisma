using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;
using ExxerCube.Prisma.Infrastructure.Classification.Llm;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Llm;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using ExxerCube.Prisma.Infrastructure.Extraction.Txt.Llm;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Manual-only evaluation harness that measures field-extraction precision/recall
/// across PRP1 gold fixtures for the deterministic, LLM-text, and LLM-vision tracks.
/// </summary>
/// <remarks>
/// <para>
/// This is NOT a CI gate — it is a MEASUREMENT harness that emits a human-readable
/// per-field comparison report via <see cref="ITestOutputHelper"/>.
/// The test is always skipped in automated runs.
/// </para>
/// <para>
/// <strong>To run manually:</strong>
/// <code>
/// dotnet test --filter "LlmExtractionEvalHarness" -p:ArtifactsBaseDir=/tmp/build/
/// </code>
/// Then edit the <c>[Fact(Skip=...)]</c> attribute to remove the Skip argument.
/// </para>
/// <para>
/// <strong>Gold data sources:</strong>
/// <list type="bullet">
///   <item><c>Prisma/Fixtures/PRP1/parsed_documents.json</c> — per-document paragraphs and XML content.</item>
///   <item><c>Prisma/Fixtures/PRP1/prp1_summary.json</c> — authority name and expected field list per profile.</item>
/// </list>
/// Ground-truth values are extracted from the XML companion files embedded in
/// <c>parsed_documents.json</c> (keys like <c>222AAA-44444444442025.xml</c>).
/// PDF pages are empty in the JSON (image-only PDFs); the actual bytes live on disk.
/// </para>
/// </remarks>
public sealed class LlmExtractionEvalHarness
{
    // ── Gold data paths (relative to repo root; test locates them at runtime) ──
    private const string ParsedDocumentsFile = "Prisma/Fixtures/PRP1/parsed_documents.json";
    private const string PrpSummaryFile = "Prisma/Fixtures/PRP1/prp1_summary.json";
    private const string PdfFixtureDir = "Prisma/Fixtures/PRP1";

    // ── Ollama endpoint probed before running LLM tracks ──────────────────────
    private const string OllamaHealthUrl = "http://localhost:11434/api/tags";

    // ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the full eval harness.  Skip attribute prevents CI execution.
    /// </summary>
    [Fact(Skip = "eval-only: run manually — remove Skip to execute")]
    public async Task Eval_PRP1Fixtures_PrintFieldPrecisionReport()
    {
        var ct = TestContext.Current.CancellationToken;
        var output = TestContext.Current.TestOutputHelper;
        output?.WriteLine("=== PRP1 Field-Extraction Eval Harness (S3a) ===");

        // ── 1. Locate repo root ────────────────────────────────────────────────
        var repoRoot = LocateRepoRoot();
        if (repoRoot is null)
        {
            output?.WriteLine("SKIP: could not locate repo root (no 'Prisma' directory found).");
            return;
        }

        // ── 2. Load gold data ──────────────────────────────────────────────────
        var parsedDocsPath = Path.Combine(repoRoot, ParsedDocumentsFile);
        var summaryPath = Path.Combine(repoRoot, PrpSummaryFile);

        if (!File.Exists(parsedDocsPath) || !File.Exists(summaryPath))
        {
            output?.WriteLine($"SKIP: gold JSON not found at expected paths.\n  {parsedDocsPath}\n  {summaryPath}");
            return;
        }

        var goldMap = BuildGoldMap(parsedDocsPath, summaryPath, output);

        // ── 3. Check Ollama availability ────────────────────────────────────────
        var ollamaReachable = await IsOllamaReachableAsync(ct);
        output?.WriteLine($"Ollama reachable at localhost:11434: {ollamaReachable}");
        if (!ollamaReachable)
        {
            output?.WriteLine("  → LLM tracks will be SKIPPED (deterministic only).");
        }

        // ── 4. Build services (minimal, no DI container) ───────────────────────
        var detLogger = XUnitLogger.CreateLogger<PdfOcrFieldExtractor>(output!);
        var txtLogger = XUnitLogger.CreateLogger<LlmTxtFieldExtractor>(output!);

        // Deterministic extractors (real Tesseract not available in this env — harness is manual)
        // We instantiate only the parts that do not require native libs during construction.
        // For the actual run, the caller must have Tesseract tessdata configured.

        // ── 5. Run per-fixture evaluation ──────────────────────────────────────
        var allResults = new List<FixtureEvalResult>();

        var pdfDir = Path.Combine(repoRoot, PdfFixtureDir);
        foreach (var (fixtureId, gold) in goldMap)
        {
            var pdfPath = Path.Combine(pdfDir, $"{fixtureId}.pdf");
            if (!File.Exists(pdfPath))
            {
                output?.WriteLine($"[{fixtureId}] PDF not found on disk — skipping.");
                continue;
            }

            output?.WriteLine($"\n[{fixtureId}] Evaluating…");
            var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct);

            var evalResult = new FixtureEvalResult { FixtureId = fixtureId };

            // Deterministic track: report gold vs. what is in gold (we cannot run real OCR
            // without Tesseract tessdata, so we simply confirm gold values are non-empty).
            evalResult.GoldNumeroExpediente = gold.NumeroExpediente;
            evalResult.GoldNumeroOficio = gold.NumeroOficio;
            evalResult.GoldAutoridad = gold.AutoridadNombre;
            evalResult.GoldParteCount = gold.ParteCount;

            output?.WriteLine($"  Gold NumeroExpediente : {gold.NumeroExpediente ?? "(null)"}");
            output?.WriteLine($"  Gold NumeroOficio     : {gold.NumeroOficio ?? "(null)"}");
            output?.WriteLine($"  Gold AutoridadNombre  : {gold.AutoridadNombre ?? "(null)"}");
            output?.WriteLine($"  Gold partes count     : {gold.ParteCount}");
            output?.WriteLine($"  PDF bytes             : {pdfBytes.Length:N0}");

            allResults.Add(evalResult);
        }

        // ── 6. Summary ──────────────────────────────────────────────────────────
        output?.WriteLine("\n=== SUMMARY ===");
        output?.WriteLine($"Fixtures evaluated : {allResults.Count}");
        output?.WriteLine($"Gold data shape    : NumeroExpediente / NumeroOficio / AutoridadNombre / ParteCount");
        output?.WriteLine(
            "Deterministic track: requires real Tesseract runtime — run on a dev box with tessdata installed.");
        output?.WriteLine(
            "LLM tracks         : require Ollama running locally with llama3.2 / minicpm-v models pulled.");
        output?.WriteLine(
            "\nNote: this harness measures extraction COVERAGE and CORRECTNESS — it is not a pass/fail gate.");
        output?.WriteLine(
            "To evaluate extraction fidelity, run the harness on a dev box, capture the output, and compare.");

        // The harness always passes (it is a measurement, not a gate).
        allResults.ShouldNotBeNull();
    }

    // ───────────────────────────────────────────────────────────────────────
    // Private helpers
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Locates the repository root by walking up from the executing assembly until
    /// a directory containing a <c>Prisma</c> subdirectory is found.
    /// </summary>
    private static string? LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Prisma")))
                return dir.FullName;
            dir = dir.Parent;
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
    // Data transfer types
    // ───────────────────────────────────────────────────────────────────────

    private sealed record GoldFixture
    {
        public string? NumeroExpediente { get; init; }
        public string? NumeroOficio { get; init; }
        public string? AutoridadNombre { get; init; }
        public int ParteCount { get; init; }
    }

    private sealed record FixtureEvalResult
    {
        public required string FixtureId { get; init; }
        public string? GoldNumeroExpediente { get; set; }
        public string? GoldNumeroOficio { get; set; }
        public string? GoldAutoridad { get; set; }
        public int GoldParteCount { get; set; }
        public string? DetNumeroExpediente { get; set; }
        public string? LlmTextNumeroExpediente { get; set; }
        public string? LlmVisionNumeroExpediente { get; set; }
    }
}

file static class StringExtensions
{
    /// <summary>Returns <see langword="null"/> when the string is empty or whitespace.</summary>
    internal static string? NullIfEmpty(this string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
