using System.Text.RegularExpressions;

namespace ExxerCube.Prisma.Application.Parsing;

/// <summary>
/// Parses loose account text into structured account value objects.
/// </summary>
public static class AccountParser
{
    private static readonly Regex AccountRegex = new(@"\b\d{6,}\b", RegexOptions.Compiled);

    /// <summary>
    /// Extracts the first likely account number from raw text.
    /// </summary>
    public static Cuenta? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var match = AccountRegex.Match(raw);
        if (!match.Success)
        {
            return null;
        }

        return new Cuenta { Numero = match.Value };
    }
}
