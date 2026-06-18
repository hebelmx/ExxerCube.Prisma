using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;

namespace ExxerCube.Prisma.Veriqan.Domain.Verification;

/// <summary>
/// Pure, stateless helper used by VEC validation rules to determine whether an
/// <see cref="ExtractedField{T}"/> has sufficient extraction confidence to be used
/// in arithmetic comparison.
/// </summary>
/// <remarks>
/// <para>
/// <b>Purpose (Story 9.5 — preventive pipeline gate):</b> a field whose confidence is
/// below the configured threshold must cause the rule to abstain
/// (<see cref="FindingVerdict.InsufficientData"/>) rather than produce a potentially
/// erroneous Pass or Fail verdict. A misread digit must yield "cannot verify", not
/// "bank non-compliant".
/// </para>
/// <para>
/// <b>Adoption pattern</b> (Story 9.6 retrofits the remaining rules):
/// <code>
/// if (ConfidenceGuard.BelowThreshold(ps.Cat, threshold))
///     return InsufficientData(ConfidenceGuard.Reason("CAT", ps.Cat.Confidence, threshold));
/// </code>
/// </para>
/// <para>
/// <b>Boundary rule:</b> a field whose confidence is exactly equal to the threshold passes
/// the guard (<c>field.Confidence &gt;= threshold</c>). The threshold is a minimum floor,
/// not a strict lower bound.
/// </para>
/// </remarks>
public static class ConfidenceGuard
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="field"/>'s confidence score
    /// is strictly below <paramref name="threshold"/>, indicating that the field should
    /// not be used in verification arithmetic.
    /// </summary>
    /// <typeparam name="T">The type of the extracted field value.</typeparam>
    /// <param name="field">The extracted field to test. Must not be <see langword="null"/>.</param>
    /// <param name="threshold">
    /// The minimum confidence score required for the field to be used.
    /// Must be in [0.0, 1.0]; use
    /// <see cref="TenantProfile.LegalMinFieldConfidenceDefault"/> (0.8) as the default.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when <c>field.Confidence &lt; threshold</c>;
    /// <see langword="false"/> when <c>field.Confidence &gt;= threshold</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="field"/> is <see langword="null"/>.
    /// </exception>
    public static bool BelowThreshold<T>(ExtractedField<T> field, double threshold)
    {
        ArgumentNullException.ThrowIfNull(field);
        return field.Confidence < threshold;
    }

    /// <summary>
    /// Builds a human-readable <see cref="RuleFinding.InsufficientData"/> reason string
    /// that identifies the field, its observed confidence, and the required threshold.
    /// </summary>
    /// <param name="fieldName">
    /// The role name of the field as it should appear in the reason text,
    /// e.g. <c>"CAT"</c> or <c>"TASA"</c>.
    /// Must not be null or white-space.
    /// </param>
    /// <param name="observedConfidence">
    /// The actual confidence score of the field (from <see cref="ExtractedField{T}.Confidence"/>).
    /// </param>
    /// <param name="requiredThreshold">
    /// The minimum confidence score that was required.
    /// </param>
    /// <returns>
    /// A string in the form <c>"CAT field confidence 0.70 &lt; required 0.80"</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="fieldName"/> is null or white-space.
    /// </exception>
    public static string Reason(string fieldName, double observedConfidence, double requiredThreshold)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        return $"{fieldName} field confidence {observedConfidence:F2} < required {requiredThreshold:F2}";
    }
}
