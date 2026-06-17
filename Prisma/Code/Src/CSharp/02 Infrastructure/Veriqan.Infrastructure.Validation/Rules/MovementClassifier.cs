using System.Text.RegularExpressions;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// Static helper for classifying DESGLOSE movements by description content.
/// Used by CL-18, CL-19, and CL-20 to distinguish MSI installment charges
/// from regular charges.
/// </summary>
internal static class MovementClassifier
{
    /// <summary>
    /// Pattern that identifies an MSI ("meses sin intereses") installment row.
    /// Matches the "NNN de NNN" fragment that Banamex prints in the description,
    /// e.g. "DON COLCHON CUMBRES 005 de 012".
    /// </summary>
    private static readonly Regex MsiPattern = new(
        @"\b\d{1,3}\s+de\s+\d{1,3}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the movement description contains the MSI
    /// installment marker pattern "NNN de NNN" (e.g. "DON COLCHON CUMBRES 005 de 012").
    /// </summary>
    /// <param name="description">Movement description text from the DESGLOSE table.</param>
    public static bool IsMsi(string description) => MsiPattern.IsMatch(description);
}
