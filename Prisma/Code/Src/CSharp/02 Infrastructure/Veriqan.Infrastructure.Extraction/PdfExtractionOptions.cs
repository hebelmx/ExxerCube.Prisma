namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;

/// <summary>
/// Configuration options for PDF-level safeguards in <see cref="PdfPigStatementFieldExtractor"/>.
/// </summary>
/// <remarks>
/// Bound from the <c>Veriqan:PdfExtraction</c> configuration section.
/// </remarks>
public sealed class PdfExtractionOptions
{
    /// <summary>Configuration section path.</summary>
    public const string Section = "Veriqan:PdfExtraction";

    /// <summary>
    /// Default maximum PDF file size in bytes (50 MB).
    /// PDFs larger than this value are rejected before <c>PdfDocument.Open</c> is called.
    /// </summary>
    public const long DefaultMaxSizeBytes = 50 * 1024 * 1024;

    /// <summary>
    /// Default parse timeout in seconds (30 s).
    /// If <c>ExtractFullAsync</c> exceeds this deadline the operation is cancelled and a
    /// <c>Timeout</c> failure result is returned.
    /// </summary>
    public const int DefaultParseTimeoutSeconds = 30;

    /// <summary>
    /// Maximum allowed PDF size in bytes. Defaults to <see cref="DefaultMaxSizeBytes"/> (50 MB).
    /// </summary>
    public long MaxSizeBytes { get; set; } = DefaultMaxSizeBytes;

    /// <summary>
    /// Maximum time in seconds allowed for a single PDF parse. Defaults to
    /// <see cref="DefaultParseTimeoutSeconds"/> (30 s).
    /// </summary>
    public int ParseTimeoutSeconds { get; set; } = DefaultParseTimeoutSeconds;

    /// <summary>
    /// <b>VERIQAN C1 killswitch — ARMED (default <see langword="true"/>, owner ruling 2026-07-17).</b>
    /// When <see langword="true"/> (the default), <see cref="PdfPigStatementFieldExtractor"/>'s
    /// <c>ExtractTasaAndCat</c>, <c>ExtractResumenField</c> AND <c>TryParseTotalRow</c> report the
    /// geometric-plausibility confidence (see <c>Confidence.GeometricPlausibilityScorer</c>) on the
    /// Tasa/Cat fields, the 7 dual-pass RESUMEN money fields, and TotalCargos/TotalAbonos respectively,
    /// so the already-wired 0.8 guard converts a low-plausibility geometric pick into an honest
    /// <c>InsufficientData</c> abstention instead of a confident-wrong verdict. Set to
    /// <see langword="false"/> (via <c>Veriqan:PdfExtraction:EmitGeometricConfidence</c> — e.g. the
    /// <c>Veriqan__PdfExtraction__EmitGeometricConfidence</c> environment variable) as a runtime
    /// KILLSWITCH to restore the byte-identical pre-C1 behaviour without a code change or redeploy.
    /// ONE flag arms/disarms all three slices together.
    /// <para>
    /// <b>Arming evidence + the honest caveat:</b> arming was gated on the demo verdict-diff harnesses
    /// (<c>GeometricConfidence*ArmingGateE2ETests</c>): on the 5 real demo fixtures no clean verdict or
    /// confidence changes off→on. The RESUMEN slice is a genuinely NON-vacuous proof (5 of its 7 fields
    /// extract on the real demo and score at ceiling armed). The Tasa/Cat and Total slices, by contrast,
    /// are <c>NotExtracted</c> on this demo bank's layout, so their false-abstain safety is proven on the
    /// SYNTHETIC corpus only (`GeometricPlausibility*CalibrationTests`, margin ≥0.35) — arming is harmless
    /// for them on this bank but unvalidated on other banks whose layout extracts those fields; the
    /// killswitch is the mitigation. Single-bank limitation applies. See the design of record,
    /// <c>docs/planning-artifacts/SCOPING-veriqan-c1-geometric-extraction-confidence.md</c>
    /// §"Design decision 4 — ship-dark arming gate".
    /// </para>
    /// <para>
    /// This is the production/config-driven counterpart of
    /// <see cref="PdfPigStatementFieldExtractor"/>'s <c>emitGeometricConfidence</c> constructor
    /// parameter, whose own default stays <see langword="false"/> for behaviour-neutral direct
    /// construction (e.g. in tests) — arming happens here, at the composition root
    /// (<c>VeriqanExtractionExtensions.AddVeriqanExtraction</c>), not by flipping the ctor default.
    /// </para>
    /// </summary>
    public bool EmitGeometricConfidence { get; set; } = true;
}
