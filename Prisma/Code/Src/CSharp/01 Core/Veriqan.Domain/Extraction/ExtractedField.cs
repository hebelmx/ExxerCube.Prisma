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
    public ExtractedField(T? value, double confidence, FieldLocator locator, ExtractionStatus status)
    {
        ArgumentNullException.ThrowIfNull(locator);
        if (confidence is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence must be in [0.0, 1.0].");

        Value = value;
        Confidence = confidence;
        Locator = locator;
        Status = status;
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

    // -----------------------------------------------------------------------
    // Factory helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a successfully-extracted field with full confidence and a known locator.
    /// </summary>
    public static ExtractedField<T> Found(T value, FieldLocator locator) =>
        new(value, 1.0, locator, ExtractionStatus.Extracted);

    /// <summary>
    /// Creates a field that was found but whose value violates a format rule.
    /// Confidence is set to 0.7 (found but suspect).
    /// </summary>
    public static ExtractedField<T> InvalidFormat(T rawValue, FieldLocator locator) =>
        new(rawValue, 0.7, locator, ExtractionStatus.ExtractedInvalidFormat);

    /// <summary>
    /// Creates a not-found field with a best-effort page-level locator hint.
    /// </summary>
    public static ExtractedField<T> Missing(FieldLocator? hint = null) =>
        new(default, 0.0, hint ?? FieldLocator.PageHint(), ExtractionStatus.NotExtracted);
}
