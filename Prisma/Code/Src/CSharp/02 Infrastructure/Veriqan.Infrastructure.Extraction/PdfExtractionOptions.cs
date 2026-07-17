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
    /// <b>VERIQAN C1 killswitch — ships DARK (default <see langword="false"/>).</b> When
    /// <see langword="true"/>, <see cref="PdfPigStatementFieldExtractor"/>'s <c>ExtractTasaAndCat</c>
    /// reports the geometric-plausibility confidence (see <c>Confidence.GeometricPlausibilityScorer</c>)
    /// on the Tasa/Cat fields instead of the pre-C1.2 constant <c>1.0</c>; when <see langword="false"/>
    /// (the default) the pre-C1 behaviour is byte-identical.
    /// <para>
    /// <b>Why dark by default (owner ruling 2026-07-16):</b> the C1.3 arming gate proved the scorer
    /// separates clean from swapped picks only on the <em>synthetic</em> corpus — on the real demo
    /// bank's layout, <c>ExtractTasaAndCat</c> does not match and Tasa/Cat are <c>NotExtracted</c>, so
    /// the 5-demo verdict-diff never exercised the scorer's false-abstain claim on real data. Arming
    /// therefore has no benefit on the current demo and the cardinal false-abstain risk is unproven on
    /// any bank whose layout <em>does</em> extract Tasa/Cat. The full scorer + this config seam stay in
    /// place: set this to <see langword="true"/> (via <c>Veriqan:PdfExtraction:EmitGeometricConfidence</c>
    /// — e.g. the <c>Veriqan__PdfExtraction__EmitGeometricConfidence</c> environment variable) to arm,
    /// once real Tasa/Cat data validates no clean-pick mis-rating. See the design of record,
    /// <c>docs/planning-artifacts/SCOPING-veriqan-c1-geometric-extraction-confidence.md</c>
    /// §"Design decision 4 — ship-dark arming gate".
    /// </para>
    /// <para>
    /// This is the production/config-driven counterpart of
    /// <see cref="PdfPigStatementFieldExtractor"/>'s <c>emitGeometricConfidence</c> constructor
    /// parameter, whose own default also stays <see langword="false"/> for behaviour-neutral direct
    /// construction (e.g. in tests) — arming happens here, at the composition root
    /// (<c>VeriqanExtractionExtensions.AddVeriqanExtraction</c>), not by flipping the ctor default.
    /// </para>
    /// </summary>
    public bool EmitGeometricConfidence { get; set; }
}
