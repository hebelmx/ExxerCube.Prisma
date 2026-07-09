using System.Text.Json;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Honesty;

/// <summary>
/// A single field's expected extraction outcome, as recorded in a synthetic gold manifest
/// (E6.S6.2.1 — see <c>docs/planning-artifacts/E6-S6.2-synthetic-generator-design.md</c> §6).
/// </summary>
/// <remarks>
/// <b>S3.1 structural note:</b> this is a deliberate small local copy of the equivalent types in
/// <c>Veriqan.Infrastructure.Extraction.Tests/SyntheticGoldManifest.cs</c>, NOT a shared/promoted
/// type. Those types are <c>internal</c> to that project and there is no existing shared
/// <c>09 Testing</c> project for Veriqan test-support code (only a Prisma-OCR-side
/// <c>01 Abstractions/Testing</c> project exists, a different vertical). Promoting to a new
/// shared project would mean a new <c>.csproj</c>, solution registration, and package-version
/// alignment for ~170 lines of dependency-free JSON parsing. Per the S3.1 brief this is
/// acceptable option (b) — re-read <c>corpus-manifest.json</c>/<c>*.manifest.json</c> directly
/// here with a small local loader — and is called out explicitly in the story's return report.
/// </remarks>
internal sealed record SyntheticFieldExpectation(
    string ClrType,
    string ExpectedStatus,
    JsonElement? Value,
    string? Reason = null)
{
    public T? GetValue<T>() => Value is { } v ? v.Deserialize<T>() : default;
}

/// <summary>God's-eye bundle hints recorded in a synthetic gold manifest's <c>bundle</c> block.</summary>
internal sealed record SyntheticBundleHints(
    string ProductId,
    string ProductName,
    string PeriodStart,
    string PeriodEnd,
    decimal CreditLine);

/// <summary>God's-eye gold manifest for a single synthetic estado-de-cuenta PDF (E6.S6.2.1).</summary>
internal sealed record SyntheticGoldManifest(
    int SchemaVersion,
    string SourceProvenance,
    string? Defect,
    SyntheticBundleHints? Bundle,
    IReadOnlyDictionary<string, SyntheticFieldExpectation> Fields);

/// <summary>
/// Loads a <see cref="SyntheticGoldManifest"/> from disk. Never throws — malformed or missing
/// manifests are reported as a <see cref="Result{T}"/> failure per project convention.
/// </summary>
internal static class SyntheticGoldManifestLoader
{
    /// <summary>
    /// Synchronous variant used by xUnit <c>[MemberData]</c> sources, which must return an
    /// eagerly-materialized sequence at test-discovery time (no async context available). Throws
    /// on a malformed/missing manifest — same "fail discovery loudly" rationale as
    /// <see cref="SyntheticCorpusIndexLoader.Load"/>, not the <c>Result&lt;T&gt;</c> convention
    /// used by <see cref="LoadAsync"/> (which callers use mid-test, where a graceful failure
    /// assertion is possible).
    /// </summary>
    public static SyntheticGoldManifest LoadSync(string manifestPath)
    {
        var result = LoadAsync(manifestPath, CancellationToken.None).GetAwaiter().GetResult();
        if (result.IsFailure)
            throw new InvalidOperationException($"Failed to load gold manifest '{manifestPath}': {result.Error}");
        return result.Value!;
    }

    public static async Task<Result<SyntheticGoldManifest>> LoadAsync(
        string manifestPath,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<SyntheticGoldManifest>();

        if (string.IsNullOrWhiteSpace(manifestPath))
            return Result<SyntheticGoldManifest>.WithFailure("Manifest path must not be null or empty.");

        if (!File.Exists(manifestPath))
            return Result<SyntheticGoldManifest>.WithFailure($"Manifest file not found: {manifestPath}");

        try
        {
            await using var stream = File.OpenRead(manifestPath);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var root = doc.RootElement;

            var schemaVersion = root.TryGetProperty("schemaVersion", out var svEl) ? svEl.GetInt32() : 0;
            var sourceProvenance = root.TryGetProperty("sourceProvenance", out var spEl)
                ? spEl.GetString() ?? string.Empty
                : string.Empty;
            var defect = root.TryGetProperty("defect", out var defectEl)
                && defectEl.ValueKind != JsonValueKind.Null
                    ? defectEl.GetString()
                    : null;

            SyntheticBundleHints? bundle = null;
            if (root.TryGetProperty("bundle", out var bundleEl) && bundleEl.ValueKind == JsonValueKind.Object)
            {
                bundle = new SyntheticBundleHints(
                    ProductId: bundleEl.GetProperty("productId").GetString() ?? string.Empty,
                    ProductName: bundleEl.GetProperty("productName").GetString() ?? string.Empty,
                    PeriodStart: bundleEl.GetProperty("periodStart").GetString() ?? string.Empty,
                    PeriodEnd: bundleEl.GetProperty("periodEnd").GetString() ?? string.Empty,
                    CreditLine: bundleEl.TryGetProperty("creditLine", out var clEl) ? clEl.GetDecimal() : 0m);
            }

            var fields = new Dictionary<string, SyntheticFieldExpectation>(StringComparer.Ordinal);
            if (root.TryGetProperty("fields", out var fieldsEl) && fieldsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in fieldsEl.EnumerateObject())
                {
                    var fieldObj = prop.Value;

                    var clrType = fieldObj.TryGetProperty("clrType", out var ctEl)
                        ? ctEl.GetString() ?? string.Empty
                        : string.Empty;
                    var expectedStatus = fieldObj.TryGetProperty("expectedStatus", out var esEl)
                        ? esEl.GetString() ?? string.Empty
                        : string.Empty;
                    JsonElement? value = fieldObj.TryGetProperty("value", out var valEl)
                        && valEl.ValueKind != JsonValueKind.Null
                            ? valEl.Clone()
                            : null;
                    var reason = fieldObj.TryGetProperty("reason", out var reasonEl)
                        && reasonEl.ValueKind != JsonValueKind.Null
                            ? reasonEl.GetString()
                            : null;

                    fields[prop.Name] = new SyntheticFieldExpectation(clrType, expectedStatus, value, reason);
                }
            }

            var manifest = new SyntheticGoldManifest(schemaVersion, sourceProvenance, defect, bundle, fields);
            return Result<SyntheticGoldManifest>.WithSuccess(manifest);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return Result<SyntheticGoldManifest>.WithFailure(
                $"Failed to load synthetic gold manifest '{manifestPath}': {ex.Message}");
        }
    }
}

/// <summary>A single row of the standing synthetic corpus index (<c>corpus-manifest.json</c>).</summary>
internal sealed record SyntheticCorpusSpecimen(string Id, string Pdf, string Manifest, string? Defect);

/// <summary>
/// Loads the standing synthetic corpus index that lists every specimen (a <c>*.pdf</c> + sibling
/// <c>*.manifest.json</c> pair) in <c>Prisma/Fixtures/PRP2/synthetic/</c>.
/// </summary>
internal static class SyntheticCorpusIndexLoader
{
    /// <summary>
    /// Reads and parses <c>corpus-manifest.json</c> from <paramref name="fixturesDir"/>.
    /// Deliberately propagates exceptions on a malformed/missing index — this is test-collection
    /// time infrastructure (an xUnit <c>[MemberData]</c> source), not production code, so a broken
    /// index must fail test discovery loudly, not be silently swallowed.
    /// </summary>
    public static IReadOnlyList<SyntheticCorpusSpecimen> Load(string fixturesDir)
    {
        var indexPath = Path.Combine(fixturesDir, "corpus-manifest.json");
        using var stream = File.OpenRead(indexPath);
        using var doc = JsonDocument.Parse(stream);

        var specimens = new List<SyntheticCorpusSpecimen>();
        foreach (var specimenEl in doc.RootElement.GetProperty("specimens").EnumerateArray())
        {
            specimens.Add(new SyntheticCorpusSpecimen(
                specimenEl.GetProperty("id").GetString() ?? string.Empty,
                specimenEl.GetProperty("pdf").GetString() ?? string.Empty,
                specimenEl.GetProperty("manifest").GetString() ?? string.Empty,
                specimenEl.TryGetProperty("defect", out var defEl) && defEl.ValueKind != JsonValueKind.Null
                    ? defEl.GetString()
                    : null));
        }

        return specimens;
    }

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> looking for the repository root
    /// (identified by <c>CLAUDE.md</c>, directly or as a sibling directory named
    /// <c>ExxerCube.Prisma</c> — mirrors <c>VecChecklistDemoE2ETests.ComputeDemoCorpusDir</c>)
    /// and returns the synthetic fixtures directory
    /// <c>Prisma/Fixtures/PRP2/synthetic</c> beneath it.
    /// </summary>
    public static string FindFixturesDir(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
                return Path.Combine(dir.FullName, "Prisma", "Fixtures", "PRP2", "synthetic");

            var siblingRepo = Path.Combine(dir.FullName, "ExxerCube.Prisma");
            if (Directory.Exists(siblingRepo) && File.Exists(Path.Combine(siblingRepo, "CLAUDE.md")))
                return Path.Combine(siblingRepo, "Prisma", "Fixtures", "PRP2", "synthetic");

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root (CLAUDE.md) walking up from '{startDirectory}'.");
    }
}
