using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// E6.S6.2.1 golden round-trip test: drives the REAL, unmodified
/// <see cref="PdfPigStatementFieldExtractor"/> over a reproducibly code-generated synthetic
/// PDF (<c>s6211-baseline.pdf</c>, cloning the proven "Dummie-VEC" layout — see
/// <c>docs/planning-artifacts/E6-S6.2-synthetic-generator-design.md</c>) and asserts every
/// field named in the sibling god's-eye manifest (<c>s6211-baseline.manifest.json</c>)
/// against its expected (status, value).
/// </summary>
/// <remarks>
/// <para>
/// Scope (owner ruling 2026-07-06): extraction-fidelity only — no verdict-level assertion,
/// no <c>arithmeticChecks</c>. This slice doubles as a regression canary for TASA/CAT/RESUMEN/
/// NIVEL-DE-USO positional resolution — the exact fields the E2.3 real-corpus investigation
/// proved fragile.
/// </para>
/// <para>
/// The manifest is the single source of truth for expected values; this test never hand-copies
/// a number — see <see cref="SyntheticGoldManifestLoader"/> and
/// <see cref="StatementModelFieldAccessors"/>.
/// </para>
/// </remarks>
public sealed class SyntheticGoldenRoundTripTests
{
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic");

    private static readonly string BaselinePdfPath =
        Path.Combine(FixturesDir, "s6211-baseline.pdf");

    private static readonly string BaselineManifestPath =
        Path.Combine(FixturesDir, "s6211-baseline.manifest.json");

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(
            XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider());

    [Fact]
    public async Task SyntheticGolden_S6211Baseline_AllManifestFieldsMatchExpectedOutcome()
    {
        var ct = TestContext.Current.CancellationToken;

        File.Exists(BaselinePdfPath).ShouldBeTrue($"Synthetic fixture not found: {BaselinePdfPath}");
        File.Exists(BaselineManifestPath).ShouldBeTrue($"Synthetic manifest not found: {BaselineManifestPath}");

        var manifestResult = await SyntheticGoldManifestLoader.LoadAsync(BaselineManifestPath, ct);
        manifestResult.IsSuccess.ShouldBeTrue($"Manifest must load. Error: {manifestResult.Error}");
        var manifest = manifestResult.Value!;
        manifest.Fields.Count.ShouldBeGreaterThan(0, "Manifest must declare at least one field expectation");

        var pdfBytes = await File.ReadAllBytesAsync(BaselinePdfPath, ct);
        var extractor = CreateExtractor();
        var extractResult = await extractor.ExtractFullAsync(pdfBytes, ct);
        extractResult.IsSuccess.ShouldBeTrue($"ExtractFullAsync must succeed. Error: {extractResult.Error}");
        var model = extractResult.Value!;

        var mismatches = new List<string>();

        foreach (var (fieldName, expectation) in manifest.Fields.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!StatementModelFieldAccessors.Map.TryGetValue(fieldName, out var accessor))
            {
                mismatches.Add($"{fieldName}: no accessor registered in StatementModelFieldAccessors.Map");
                continue;
            }

            if (!Enum.TryParse<ExtractionStatus>(expectation.ExpectedStatus, out var expectedStatus))
            {
                mismatches.Add($"{fieldName}: manifest expectedStatus '{expectation.ExpectedStatus}' is not a valid ExtractionStatus");
                continue;
            }

            var (actualValue, actualStatus) = accessor(model);

            if (actualStatus != expectedStatus)
            {
                mismatches.Add(
                    $"{fieldName}: status expected {expectedStatus} but was {actualStatus} (actual value={actualValue ?? "<null>"})");
                continue;
            }

            if (expectedStatus != ExtractionStatus.Extracted)
            {
                // Legitimate abstention (e.g. TotalCargos in S6.2.1) — status match is sufficient,
                // there is no expected value to compare.
                continue;
            }

            var expectedValue = expectation.ClrType switch
            {
                "decimal" => (object?)expectation.GetValue<decimal>(),
                "string" => expectation.GetValue<string>(),
                "int" => (object?)expectation.GetValue<int>(),
                "DateOnly" => (object?)expectation.GetValue<DateOnly>(),
                _ => throw new NotSupportedException(
                    $"Unsupported manifest clrType '{expectation.ClrType}' for field '{fieldName}'"),
            };

            if (!Equals(actualValue, expectedValue))
            {
                mismatches.Add($"{fieldName}: value expected {expectedValue} but was {actualValue}");
            }
        }

        mismatches.ShouldBeEmpty(
            $"Synthetic golden round-trip regressed on {mismatches.Count} field(s):\n" +
            string.Join("\n", mismatches));
    }
}
