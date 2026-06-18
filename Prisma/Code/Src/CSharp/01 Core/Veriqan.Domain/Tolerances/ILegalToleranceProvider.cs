namespace ExxerCube.Prisma.Veriqan.Domain.Tolerances;

/// <summary>
/// Provides the legally-mandated <see cref="Tolerance"/> specification for each
/// computation rule identified by its check ID.
/// </summary>
/// <remarks>
/// <para>
/// The legal specifications (default, min, max) encode the rounding math allowed by the
/// CONDUSEF Acuerdo. Tenant/bundle overrides are applied on top of these specs via
/// <see cref="Tolerance.Resolve"/>.
/// </para>
/// <para>
/// A later story (9.3) may replace the default in-code implementation with an
/// encrypted, persisted store. Callers should always resolve through this interface
/// rather than hard-coding tolerances.
/// </para>
/// </remarks>
public interface ILegalToleranceProvider
{
    /// <summary>
    /// Returns the <see cref="Tolerance"/> specification for the rule identified by
    /// <paramref name="checkId"/>.
    /// </summary>
    /// <param name="checkId">
    /// The rule check identifier (e.g. <c>"CL-10"</c>, <c>"CL-17"</c>, <c>"ITEM-58"</c>).
    /// </param>
    /// <returns>The <see cref="Tolerance"/> for the specified check.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when no specification is registered for <paramref name="checkId"/>.
    /// Use <see cref="Has"/> to guard before calling when the check ID is not
    /// statically known to be valid.
    /// </exception>
    Tolerance For(string checkId);

    /// <summary>
    /// Returns <c>true</c> when a <see cref="Tolerance"/> specification is registered
    /// for <paramref name="checkId"/>; <c>false</c> otherwise.
    /// </summary>
    /// <param name="checkId">The rule check identifier to probe.</param>
    bool Has(string checkId);
}
