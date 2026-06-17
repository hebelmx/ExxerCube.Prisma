using ExxerCube.Prisma.Veriqan.Domain.Extraction;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// Thin shim that forwards to <see cref="VecTextNormalizer.Normalize"/>.
/// </summary>
/// <remarks>
/// <para>
/// The canonical normalization algorithm lives in <see cref="VecTextNormalizer"/> (Veriqan.Domain).
/// Both the Validation rules (CL-32, CL-46) and the Extraction assembly call that single
/// implementation — there is no longer a duplicated copy in this assembly.
/// </para>
/// <para>
/// This shim exists only so that existing call sites within this assembly
/// (<c>Cl32ComparaTuTarjetaRule</c>, <c>Cl46MandatoryLegendsRule</c>) do not need
/// mechanical renaming; they continue to call <c>TextNormalizer.Normalize</c> and the
/// compiler inlines the single-line delegation at build time.
/// </para>
/// </remarks>
internal static class TextNormalizer
{
    /// <summary>
    /// Normalizes a text string for legend-presence matching.
    /// Delegates to <see cref="VecTextNormalizer.Normalize"/>.
    /// </summary>
    /// <param name="text">Raw text to normalize. May be <see langword="null"/> or empty.</param>
    /// <returns>
    /// Normalized string, or <see cref="string.Empty"/> when the input is <see langword="null"/>
    /// or whitespace.
    /// </returns>
    internal static string Normalize(string? text) => VecTextNormalizer.Normalize(text);
}
