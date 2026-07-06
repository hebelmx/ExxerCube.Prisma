using System.Text.Json;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// A single field's expected extraction outcome, as recorded in a synthetic gold manifest
/// (E6.S6.2.1 — see <c>docs/planning-artifacts/E6-S6.2-synthetic-generator-design.md</c> §6).
/// </summary>
/// <param name="ClrType">
/// Disambiguates the CLR type of <see cref="Value"/> for generic dispatch
/// (e.g. <c>"decimal"</c>, <c>"string"</c>, <c>"int"</c>, <c>"DateOnly"</c>).
/// </param>
/// <param name="ExpectedStatus">
/// String matching <see cref="ExxerCube.Prisma.Veriqan.Domain.Extraction.ExtractionStatus"/>
/// (e.g. <c>"Extracted"</c>, <c>"NotExtracted"</c>) — including legitimate abstentions.
/// </param>
/// <param name="Value">
/// Raw JSON value for deferred typed conversion via <see cref="GetValue{T}"/>.
/// <see langword="null"/> when the manifest records no expected value (e.g. a
/// <c>NotExtracted</c> field).
/// </param>
/// <param name="Reason">
/// Optional human-readable explanation for a non-<c>Extracted</c> expectation
/// (e.g. why a field is a legitimate abstention in this fixture slice).
/// </param>
internal sealed record SyntheticFieldExpectation(
    string ClrType,
    string ExpectedStatus,
    JsonElement? Value,
    string? Reason = null)
{
    /// <summary>
    /// Deserializes <see cref="Value"/> into <typeparamref name="T"/>, or returns
    /// <see langword="default"/> when <see cref="Value"/> is <see langword="null"/>.
    /// </summary>
    public T? GetValue<T>() => Value is { } v ? v.Deserialize<T>() : default;
}

/// <summary>
/// God's-eye gold manifest for a single synthetic estado-de-cuenta PDF (E6.S6.2.1).
/// Loaded from the sibling <c>*.manifest.json</c> file next to the synthetic PDF fixture.
/// </summary>
/// <remarks>
/// <c>ArithmeticChecks</c> is intentionally NOT a member of this record for the S6.2.1 slice
/// (owner ruling: extraction-fidelity only, no verdict-level assertion). It is expected to be
/// added at S6.2.3 when a verdict-level test actually consumes it — see the design doc §6.2.
/// </remarks>
internal sealed record SyntheticGoldManifest(
    int SchemaVersion,
    string SourceProvenance,
    string? Defect,
    IReadOnlyDictionary<string, SyntheticFieldExpectation> Fields);

/// <summary>
/// Loads a <see cref="SyntheticGoldManifest"/> from disk. Never throws — malformed or missing
/// manifests are reported as a <see cref="Result{T}"/> failure per project convention.
/// </summary>
internal static class SyntheticGoldManifestLoader
{
    /// <summary>
    /// Loads and parses the synthetic gold manifest at <paramref name="manifestPath"/>.
    /// </summary>
    /// <param name="manifestPath">Absolute or relative path to the <c>*.manifest.json</c> file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

            var manifest = new SyntheticGoldManifest(schemaVersion, sourceProvenance, defect, fields);
            return Result<SyntheticGoldManifest>.WithSuccess(manifest);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return Result<SyntheticGoldManifest>.WithFailure(
                $"Failed to load synthetic gold manifest '{manifestPath}': {ex.Message}");
        }
    }
}
