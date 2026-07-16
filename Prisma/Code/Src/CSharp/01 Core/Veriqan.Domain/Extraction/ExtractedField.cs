namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Generic wrapper for a single field extracted from a statement PDF.
/// Every field — whether found, missing, or format-invalid — carries
/// a <see cref="FieldLocator"/>, a <see cref="Confidence"/> score, and a <see cref="Status"/>.
/// A missing field is never silently blank; it is represented as
/// <see cref="ExtractionStatus.NotExtracted"/> with a page-level locator hint.
/// </summary>
/// <typeparam name="T">The type of the extracted value (e.g. <see cref="string"/>, <see cref="int"/>).</typeparam>
public sealed class ExtractedField<T>
{
    /// <summary>
    /// Initializes an <see cref="ExtractedField{T}"/> with all components.
    /// </summary>
    /// <param name="value">Extracted (possibly normalized) value; <see langword="null"/> when <paramref name="status"/> is <see cref="ExtractionStatus.NotExtracted"/>.</param>
    /// <param name="confidence">Extraction confidence in the range [0.0, 1.0].  Use 0.0 for <see cref="ExtractionStatus.NotExtracted"/>.</param>
    /// <param name="locator">Where the field was found (or expected).  Never <see langword="null"/>.</param>
    /// <param name="status">Outcome of the extraction attempt.</param>
    /// <param name="provenance">
    /// How the value was obtained. Defaults to <see cref="ExtractionProvenance.Positional"/> when
    /// omitted — every extractor predating the progressive fallback-extraction chain (E1) only
    /// ever produces positional values, so this parameter is optional and behavior-neutral.
    /// </param>
    public ExtractedField(T? value, double confidence, FieldLocator locator, ExtractionStatus status, ExtractionProvenance? provenance = null)
    {
        ArgumentNullException.ThrowIfNull(locator);
        if (confidence is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence must be in [0.0, 1.0].");

        Value = value;
        Confidence = confidence;
        Locator = locator;
        Status = status;
        Provenance = provenance ?? ExtractionProvenance.Positional;
    }

    /// <summary>
    /// The extracted (and possibly normalized) value, or <see langword="null"/> when
    /// <see cref="Status"/> is <see cref="ExtractionStatus.NotExtracted"/>.
    /// When <see cref="Status"/> is <see cref="ExtractionStatus.ExtractedInvalidFormat"/>,
    /// the raw (non-normalized) value is preserved so that downstream checks can report it.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Confidence score for this extraction in the range [0.0, 1.0].
    /// <list type="bullet">
    ///   <item><description>1.0 — label found, value present, all format rules satisfied.</description></item>
    ///   <item><description>0.7 — label found, value present but format rule violated (invalid format).</description></item>
    ///   <item><description>0.0 — field not found at all.</description></item>
    /// </list>
    /// </summary>
    public double Confidence { get; }

    /// <summary>
    /// The bounding-box region where this field was found in the PDF,
    /// or a page-level hint when the field was not found.
    /// Never <see langword="null"/>.
    /// </summary>
    public FieldLocator Locator { get; }

    /// <summary>
    /// The outcome of the extraction attempt.
    /// </summary>
    public ExtractionStatus Status { get; }

    /// <summary>
    /// How this value was obtained — which resolution stage produced it, and (for the LLM
    /// stage only) content-hash diagnostics. Defaults to <see cref="ExtractionProvenance.Positional"/>
    /// for every field produced before the progressive fallback-extraction chain (E1) exists.
    /// </summary>
    public ExtractionProvenance Provenance { get; }

    // -----------------------------------------------------------------------
    // Factory helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a successfully-extracted field with full confidence and a known locator.
    /// </summary>
    public static ExtractedField<T> Found(T value, FieldLocator locator, ExtractionProvenance? provenance = null) =>
        Found(value, locator, 1.0, provenance);

    /// <summary>
    /// Creates a successfully-extracted field with an explicit confidence and a known locator.
    /// Used to carry a geometric-plausibility confidence for positional extraction (the C1
    /// lever) — e.g. a lower score when the extracted digits are geometrically implausible
    /// even though the label/value pair matched. The default extraction path (<see cref="Found(T, FieldLocator, ExtractionProvenance?)"/>)
    /// still reports full confidence (1.0); this overload is opt-in and behavior-neutral for
    /// all existing callers.
    /// </summary>
    public static ExtractedField<T> Found(T value, FieldLocator locator, double confidence, ExtractionProvenance? provenance = null) =>
        new(value, confidence, locator, ExtractionStatus.Extracted, provenance);

    /// <summary>
    /// Creates a field that was found but whose value violates a format rule.
    /// Confidence is set to 0.7 (found but suspect).
    /// </summary>
    public static ExtractedField<T> InvalidFormat(T rawValue, FieldLocator locator, ExtractionProvenance? provenance = null) =>
        new(rawValue, 0.7, locator, ExtractionStatus.ExtractedInvalidFormat, provenance);

    /// <summary>
    /// Creates a not-found field with a best-effort page-level locator hint.
    /// </summary>
    public static ExtractedField<T> Missing(FieldLocator? hint = null, ExtractionProvenance? provenance = null) =>
        new(default, 0.0, hint ?? FieldLocator.PageHint(), ExtractionStatus.NotExtracted, provenance);
}
