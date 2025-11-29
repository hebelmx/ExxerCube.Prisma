namespace ExxerCube.Prisma.Application.Parsing;

/// <summary>
/// Parses measure hints and account snippets into structured compliance actions.
/// </summary>
public static class MeasureParser
{
    /// <summary>
    /// Maps raw text to a Measure/Action kind.
    /// </summary>
    public static ComplianceActionKind ParseActionKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ComplianceActionKind.Unknown;
        }

        var text = raw.ToUpperInvariant();
        if (text.Contains("BLOQ")) return ComplianceActionKind.Block;
        if (text.Contains("DESBLO") || text.Contains("LIBER")) return ComplianceActionKind.Unblock;
        if (text.Contains("TRANS") || text.Contains("TRASP")) return ComplianceActionKind.Transfer;
        if (text.Contains("INFO")) return ComplianceActionKind.Information;
        if (text.Contains("DOC")) return ComplianceActionKind.Document;
        if (text.Contains("IGNOR")) return ComplianceActionKind.Ignore;
        return ComplianceActionKind.Other;
    }

    /// <summary>
    /// Parses a loose account string into a structured account value object.
    /// </summary>
    public static Cuenta? ParseCuenta(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cleaned = raw.Replace(" ", string.Empty).Trim();
        if (cleaned.Length < 6)
        {
            return null;
        }

        var cuenta = new Cuenta
        {
            Numero = cleaned
        };

        return cuenta;
    }
}
