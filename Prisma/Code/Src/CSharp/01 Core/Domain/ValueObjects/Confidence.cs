namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a normalised confidence score on the [0, 1] scale
/// together with the pipeline stage that produced it.
/// </summary>
/// <remarks>
/// <para>
/// All values are automatically clamped to [0, 1] in the constructor via
/// <see cref="Math.Clamp(double, double, double)"/>, so callers never need to guard
/// against out-of-range inputs.
/// </para>
/// <para>
/// Use the named factory methods (<see cref="FromOcr"/>, <see cref="FromInt"/>,
/// <see cref="FromFusion"/>, <see cref="FromQuality"/>) as the preferred API.
/// The public constructor is available when the value is already on the [0, 1] scale
/// and the source is explicitly known.
/// </para>
/// <para>
/// Implicit numeric conversion operators are intentionally omitted: a bare
/// <c>float</c> or <c>double</c> could represent either a 0–1 or a 0–100 scale,
/// which would be a silent footgun. Always call a factory or the explicit constructor.
/// </para>
/// <para>
/// Note on <c>with</c> expressions: because the properties are <c>init</c>-settable
/// (required for record-struct mutation via <c>with</c>), a <c>with { Value = … }</c>
/// expression bypasses the constructor clamp. Avoid assigning <see cref="Value"/>
/// directly via a <c>with</c> expression in production code.
/// </para>
/// </remarks>
public readonly record struct Confidence
{
    /// <summary>
    /// Gets the normalised confidence score, guaranteed to be within [0, 1].
    /// </summary>
    public double Value { get; init; }

    /// <summary>
    /// Gets the pipeline stage that produced this confidence score.
    /// </summary>
    public ConfidenceSource Source { get; init; }

    /// <summary>
    /// Initialises a new <see cref="Confidence"/> with the given raw value and source.
    /// <paramref name="value"/> is clamped to [0, 1] via
    /// <see cref="Math.Clamp(double, double, double)"/>.
    /// </summary>
    /// <param name="value">
    /// The raw confidence value. Values below 0 are raised to 0; values above 1 are
    /// lowered to 1.
    /// </param>
    /// <param name="source">The pipeline stage that produced this score.</param>
    public Confidence(double value, ConfidenceSource source)
    {
        Value = Math.Clamp(value, 0.0, 1.0);
        Source = source;
    }

    // ── Named factory methods ────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="Confidence"/> from an OCR engine score on the 0–100 scale.
    /// The value is divided by 100 and clamped to [0, 1].
    /// </summary>
    /// <param name="zeroTo100">OCR confidence in the range [0, 100].</param>
    /// <returns>
    /// A <see cref="Confidence"/> with <see cref="Source"/> equal to
    /// <see cref="ConfidenceSource.Ocr"/>.
    /// </returns>
    public static Confidence FromOcr(float zeroTo100) =>
        new((double)zeroTo100 / 100.0, ConfidenceSource.Ocr);

    /// <summary>
    /// Creates a <see cref="Confidence"/> from a classification score on the 0–100
    /// integer scale. The value is divided by 100 and clamped to [0, 1].
    /// </summary>
    /// <param name="zeroTo100">Classification confidence in the range [0, 100].</param>
    /// <returns>
    /// A <see cref="Confidence"/> with <see cref="Source"/> equal to
    /// <see cref="ConfidenceSource.Classification"/>.
    /// </returns>
    public static Confidence FromInt(int zeroTo100) =>
        new(zeroTo100 / 100.0, ConfidenceSource.Classification);

    /// <summary>
    /// Creates a <see cref="Confidence"/> from a fusion stage score already on the
    /// [0, 1] scale.
    /// </summary>
    /// <param name="zeroTo1">Fusion confidence in the range [0, 1].</param>
    /// <returns>
    /// A <see cref="Confidence"/> with <see cref="Source"/> equal to
    /// <see cref="ConfidenceSource.Fusion"/>.
    /// </returns>
    public static Confidence FromFusion(double zeroTo1) =>
        new(zeroTo1, ConfidenceSource.Fusion);

    /// <summary>
    /// Creates a <see cref="Confidence"/> from a quality analysis score already on the
    /// [0, 1] scale.
    /// </summary>
    /// <param name="zeroTo1">Quality confidence in the range [0, 1].</param>
    /// <returns>
    /// A <see cref="Confidence"/> with <see cref="Source"/> equal to
    /// <see cref="ConfidenceSource.Quality"/>.
    /// </returns>
    public static Confidence FromQuality(double zeroTo1) =>
        new(zeroTo1, ConfidenceSource.Quality);

    // ── Combination helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="Confidence"/> with the lowest <see cref="Value"/> from
    /// <paramref name="sources"/>, preserving that item's original <see cref="Source"/>.
    /// </summary>
    /// <param name="sources">The confidences to compare. May be empty.</param>
    /// <returns>
    /// The minimum-value confidence found in <paramref name="sources"/>; or
    /// <c>new Confidence(0, <see cref="ConfidenceSource.Quality"/>)</c> when the
    /// collection is empty.
    /// </returns>
    public static Confidence Min(IEnumerable<Confidence> sources)
    {
        using var enumerator = sources.GetEnumerator();
        if (!enumerator.MoveNext())
            return new Confidence(0.0, ConfidenceSource.Quality);

        Confidence min = enumerator.Current;
        while (enumerator.MoveNext())
        {
            if (enumerator.Current.Value < min.Value)
                min = enumerator.Current;
        }
        return min;
    }

    /// <summary>
    /// Computes a weighted average of the supplied confidences.
    /// </summary>
    /// <param name="weighted">
    /// Pairs of (<see cref="Confidence"/>, weight). A zero or negative total weight
    /// returns zero.
    /// </param>
    /// <param name="resultSource">
    /// The <see cref="ConfidenceSource"/> stamped on the result.
    /// </param>
    /// <returns>
    /// A new <see cref="Confidence"/> whose <see cref="Value"/> is
    /// <c>Σ(value × weight) / Σ(weight)</c>; or
    /// <c>new Confidence(0, <paramref name="resultSource"/>)</c> when
    /// <paramref name="weighted"/> is empty or the total weight is zero.
    /// </returns>
    public static Confidence WeightedAverage(
        IEnumerable<(Confidence Confidence, double Weight)> weighted,
        ConfidenceSource resultSource)
    {
        double weightedSum = 0.0;
        double totalWeight = 0.0;

        foreach (var (confidence, weight) in weighted)
        {
            weightedSum += confidence.Value * weight;
            totalWeight += weight;
        }

        double value = totalWeight == 0.0 ? 0.0 : weightedSum / totalWeight;
        return new Confidence(value, resultSource);
    }

    /// <summary>
    /// Conservative aggregation: returns the minimum confidence from
    /// <paramref name="sources"/>. Delegates to <see cref="Min"/>.
    /// </summary>
    /// <param name="sources">The confidences to combine. May be empty.</param>
    /// <returns>
    /// The minimum-value confidence in the collection; or
    /// <c>new Confidence(0, <see cref="ConfidenceSource.Quality"/>)</c> when empty.
    /// </returns>
    public static Confidence Combine(IEnumerable<Confidence> sources) => Min(sources);

    // ── Formatting ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a log-friendly representation such as <c>"0.95 (Ocr)"</c>.
    /// </summary>
    /// <returns>A formatted string showing value to two decimal places and the source.</returns>
    public override string ToString() => $"{Value:F2} ({Source})";
}
