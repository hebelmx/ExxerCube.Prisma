using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExxerCube.Prisma.Veriqan.Domain.Enums;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>
/// One catalogued row from the real check ledger (VLD-P2): the tier, human-readable label, and
/// DOF/legal-instrument citation for a single <c>RuleFinding.CheckId</c>.
/// </summary>
/// <param name="CheckId">Rule identifier, e.g. <c>"CL-21"</c> or <c>"LAW-TYPO-BOLD"</c>.</param>
/// <param name="Tier">Regulatory/contractual tier (Bank / Condusef / Both).</param>
/// <param name="Label">Plain-language description of what the check verifies (never the raw CheckId).</param>
/// <param name="DofNumeralDisplay">
/// The citation string to show on screen. For a brand check this is the honest
/// "(estándar de marca, no ley)" tag rather than the raw law-shaped DOF numeral string — a brand
/// rule must never be badged as law to the legal audience.
/// </param>
/// <param name="IsVisual">Whether this check is a visual/layout rule (vs. a data/arithmetic rule).</param>
/// <param name="IsBrand">Whether this check enforces a client brand standard rather than a CONDUSEF mandate.</param>
public sealed record RealCheckLedgerEntry(
    string CheckId,
    ChecklistTier Tier,
    string Label,
    string DofNumeralDisplay,
    bool IsVisual,
    bool IsBrand);

/// <summary>
/// Loads the real check ledger (VLD-P2 <c>veriqan-real-check-ledger-2026-07.json</c>) from an
/// embedded resource and exposes a lookup by <c>RuleFinding.CheckId</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Container-safe by design:</b> the ledger JSON is baked into the assembly as an
/// <c>EmbeddedResource</c> (see the .csproj) and read via
/// <see cref="Assembly.GetManifestResourceStream(string)"/> — never from a docs/ path on disk,
/// which would not exist in a published/containerized deployment.
/// </para>
/// <para>
/// Loading happens once, in the constructor. A failure to load the baked-in resource is a
/// genuine startup fault (a corrupted or missing embedded resource means the deployment itself
/// is broken), so this type is one of the few places allowed to throw rather than return a
/// <c>Result</c> — there is no sensible per-request fallback for "the ledger never loaded".
/// </para>
/// </remarks>
public sealed class RealCheckLedger
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Conservative default label shown when the ledger fails to catalogue a CheckId.</summary>
    public const string UncataloguedLabel = "(uncatalogued check — engineering gap, not a compliance signal)";

    /// <summary>Honest brand tag shown instead of a law-shaped DOF numeral for brand checks.</summary>
    public const string BrandTag = "(estándar de marca, no ley)";

    private readonly IReadOnlyDictionary<string, RealCheckLedgerEntry> _entriesByCheckId;

    /// <summary>
    /// Initializes a new <see cref="RealCheckLedger"/>, loading and parsing the embedded ledger
    /// resource immediately.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the embedded resource cannot be found or fails to parse — a baked-in resource
    /// that is missing or corrupt indicates a broken build/deployment, not a runtime condition
    /// callers can recover from.
    /// </exception>
    public RealCheckLedger()
    {
        _entriesByCheckId = LoadEntries();
    }

    /// <summary>
    /// Attempts to look up the ledger entry for <paramref name="checkId"/>.
    /// </summary>
    /// <param name="checkId">The <c>RuleFinding.CheckId</c> to look up.</param>
    /// <param name="entry">The matching entry, when found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="checkId"/> is catalogued in the ledger.</returns>
    public bool TryGet(string checkId, out RealCheckLedgerEntry? entry) =>
        _entriesByCheckId.TryGetValue(checkId, out entry);

    /// <summary>
    /// Builds a <c>CheckId</c> → <see cref="ChecklistTier"/> map for the given
    /// <paramref name="checkIds"/> (VLD-S5, for <c>IMarkedPdfGenerator</c>'s
    /// <c>checklistTiers</c> parameter — Bank-tier findings render amber, everything else red).
    /// </summary>
    /// <param name="checkIds">The <c>RuleFinding.CheckId</c> values to look up.</param>
    /// <returns>
    /// A map containing an entry for every catalogued <paramref name="checkIds"/> value.
    /// An uncatalogued CheckId is simply omitted — callers (and <c>IMarkedPdfGenerator</c>
    /// itself) already treat an absent key as the conservative default (red highlight).
    /// </returns>
    public IReadOnlyDictionary<string, ChecklistTier> TierMap(IEnumerable<string> checkIds)
    {
        var map = new Dictionary<string, ChecklistTier>(StringComparer.Ordinal);
        foreach (var checkId in checkIds)
        {
            if (TryGet(checkId, out var entry) && entry is not null)
                map[checkId] = entry.Tier;
        }

        return map;
    }

    // ── loading ──────────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, RealCheckLedgerEntry> LoadEntries()
    {
        var assembly = typeof(RealCheckLedger).Assembly;
        const string fileName = "veriqan-real-check-ledger-2026-07.json";

        // Resolve by suffix rather than a hardcoded full manifest name — robust to any future
        // RootNamespace override on the project.
        string? resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith(fileName, StringComparison.Ordinal));

        if (resourceName is null)
        {
            throw new InvalidOperationException(
                $"Embedded resource ending in '{fileName}' was not found in assembly " +
                $"'{assembly.FullName}'. Verify the .csproj <EmbeddedResource> entry.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' was listed but its stream could not be opened.");

        var document = JsonSerializer.Deserialize<LedgerFileDocument>(stream, SerializerOptions)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' deserialized to null.");

        var result = new Dictionary<string, RealCheckLedgerEntry>(StringComparer.Ordinal);
        foreach (var check in document.Checks)
        {
            result[check.CheckId] = new RealCheckLedgerEntry(
                CheckId: check.CheckId,
                Tier: MapTier(check.Tier),
                Label: check.CitationText,
                DofNumeralDisplay: check.Classification == "brand" ? BrandTag : check.DofNumeral,
                IsVisual: check.IsVisual,
                IsBrand: check.Classification == "brand");
        }

        return result;
    }

    /// <summary>
    /// Maps the ledger's raw tier string to <see cref="ChecklistTier"/>.
    /// <c>"VERIFY"</c> (an owner-unconfirmed row) maps conservatively to
    /// <see cref="ChecklistTier.Condusef"/>, mirroring the documented convention in
    /// <see cref="ChecklistTier"/>: an unmapped/uncertain rule counts toward the regulatory floor
    /// so it is never silently dropped from a RED outcome.
    /// </summary>
    private static ChecklistTier MapTier(string tier) => tier switch
    {
        "Bank" => ChecklistTier.Bank,
        "Condusef" => ChecklistTier.Condusef,
        "Both" => ChecklistTier.Both,
        "VERIFY" => ChecklistTier.Condusef,
        _ => ChecklistTier.Condusef,
    };

    // ── JSON DTOs (mirror docs/planning-artifacts/veriqan-real-check-ledger-2026-07.json) ──────

    private sealed record LedgerFileDocument(
        [property: JsonPropertyName("checks")] IReadOnlyList<LedgerFileCheck> Checks);

    private sealed record LedgerFileCheck(
        [property: JsonPropertyName("checkId")] string CheckId,
        [property: JsonPropertyName("dofNumeral")] string DofNumeral,
        [property: JsonPropertyName("tier")] string Tier,
        [property: JsonPropertyName("classification")] string Classification,
        [property: JsonPropertyName("isVisual")] bool IsVisual,
        [property: JsonPropertyName("citationText")] string CitationText,
        [property: JsonPropertyName("caveat")] string? Caveat);
}
