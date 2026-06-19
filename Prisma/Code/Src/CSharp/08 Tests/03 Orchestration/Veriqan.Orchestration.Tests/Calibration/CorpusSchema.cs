using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration;

// ---------------------------------------------------------------------------
// §5.1 — Labelling schema POCOs
// Deserialized from corpus-manifest.json (System.Text.Json).
// ---------------------------------------------------------------------------

/// <summary>
/// Whether the specimen is a known-conformant statement, a synthetic placeholder, or a
/// statement with a deliberately-injected defect.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SpecimenLabel
{
    /// <summary>
    /// The specimen is believed CONDUSEF-compliant and should pass all rules modulo
    /// <c>AllowedFails</c> that are purely omitted-optional-reference-data gaps.
    /// Do NOT label a statement KnownGood if it genuinely violates a format/typography/
    /// structure rule.
    /// </summary>
    KnownGood,

    /// <summary>
    /// A synthetic/placeholder fixture that is NOT a compliant statement and is known to
    /// violate format, typography, or structure rules.  Used to exercise the pipeline; never
    /// used as compliance evidence.  Treated like KnownGood for the no-new-Fail guard (no
    /// intended defects) but reported distinctly.
    /// </summary>
    KnownSynthetic,

    /// <summary>The specimen has intentional defects that rules must detect.</summary>
    KnownBroken,
}

/// <summary>
/// A deliberately-injected defect in a KnownBroken specimen.
/// </summary>
/// <param name="CheckId">The rule identifier expected to produce a Fail verdict.</param>
/// <param name="ExpectedVerdict">Always Fail for a declared defect.</param>
/// <param name="DefectNote">Human-readable explanation of the injected defect.</param>
public sealed record IntendedDefect(
    [property: JsonPropertyName("checkId")] string CheckId,
    [property: JsonPropertyName("expectedVerdict")] string ExpectedVerdict,
    [property: JsonPropertyName("defectNote")] string DefectNote);

/// <summary>
/// A single CheckId that is allowed to Fail for a specimen, together with the reason.
/// Used for two distinct suppression buckets — kept as a single record type to share
/// JSON shape; distinguished by which collection on <see cref="CorpusSpecimen"/> it lives in.
/// </summary>
/// <param name="CheckId">The rule identifier.</param>
/// <param name="Reason">Why this Fail is accepted.</param>
public sealed record AllowedFail(
    [property: JsonPropertyName("checkId")] string CheckId,
    [property: JsonPropertyName("reason")] string Reason);

/// <summary>
/// Reference-bundle parameters needed to bind the pipeline for this specimen.
/// Mirrors <c>BuildFakeBundle</c> in <c>VerificationPipelineEndToEndTests</c>.
/// </summary>
public sealed class SpecimenBundle
{
    [JsonPropertyName("institution")]    public string Institution { get; set; } = string.Empty;
    [JsonPropertyName("periodLabel")]    public string PeriodLabel { get; set; } = string.Empty;
    [JsonPropertyName("periodStart")]    public string PeriodStart { get; set; } = string.Empty;
    [JsonPropertyName("periodEnd")]      public string PeriodEnd { get; set; } = string.Empty;
    [JsonPropertyName("productId")]      public string ProductId { get; set; } = string.Empty;
    [JsonPropertyName("productName")]    public string ProductName { get; set; } = string.Empty;
    [JsonPropertyName("productToken")]   public string ProductToken { get; set; } = string.Empty;
    [JsonPropertyName("aliases")]        public List<string> Aliases { get; set; } = new();
    [JsonPropertyName("annualOrdinaryRate")] public decimal AnnualOrdinaryRate { get; set; }
    [JsonPropertyName("creditLine")]     public decimal CreditLine { get; set; }
    [JsonPropertyName("annualCommissionMxn")] public decimal AnnualCommissionMxn { get; set; }
    [JsonPropertyName("requiredFontFamily")] public string RequiredFontFamily { get; set; } = "Aptos";
    [JsonPropertyName("bankingYearDays")] public int BankingYearDays { get; set; } = 360;
}

/// <summary>
/// One labelled specimen in the corpus.
/// </summary>
public sealed class CorpusSpecimen
{
    [JsonPropertyName("fileName")]       public string FileName { get; set; } = string.Empty;
    [JsonPropertyName("label")]          public SpecimenLabel Label { get; set; }
    [JsonPropertyName("description")]    public string Description { get; set; } = string.Empty;
    [JsonPropertyName("notes")]          public string? Notes { get; set; }
    [JsonPropertyName("bundle")]         public SpecimenBundle Bundle { get; set; } = new();
    [JsonPropertyName("intendedDefects")] public List<IntendedDefect> IntendedDefects { get; set; } = new();

    /// <summary>
    /// Fails permitted ONLY because optional reference data was omitted from the synthetic
    /// bundle (e.g. faked RFC / rate / period).  These are NOT statement defects — they say
    /// nothing about the PDF's compliance.
    /// </summary>
    [JsonPropertyName("allowedFails")]   public List<AllowedFail> AllowedFails { get; set; } = new();

    /// <summary>
    /// Fails that are genuine non-compliance properties of the fixture PDF itself
    /// (sub-floor typography, non-Aptos font, missing mandatory sections, absent verbatim
    /// legends, pagination/overlap).  Kept in a SEPARATE bucket so a green test can NEVER
    /// be read as "this statement is compliant."  Only valid on <see cref="SpecimenLabel.KnownSynthetic"/>
    /// (and optionally <see cref="SpecimenLabel.KnownBroken"/>) specimens.
    /// </summary>
    [JsonPropertyName("knownFixtureDefects")] public List<AllowedFail> KnownFixtureDefects { get; set; } = new();
}

/// <summary>
/// Top-level corpus manifest.  Deserialized from <c>corpus-manifest.json</c>.
/// </summary>
public sealed class CorpusManifest
{
    /// <summary>
    /// Relative or absolute directory containing the PDF fixtures.
    /// Relative paths are resolved from the directory containing the manifest file.
    /// </summary>
    [JsonPropertyName("corpusDir")]      public string CorpusDir { get; set; } = string.Empty;

    [JsonPropertyName("specimens")]      public List<CorpusSpecimen> Specimens { get; set; } = new();
}
