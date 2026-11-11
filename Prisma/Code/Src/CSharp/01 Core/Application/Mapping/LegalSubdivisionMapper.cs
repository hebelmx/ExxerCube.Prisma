namespace ExxerCube.Prisma.Application.Mapping;

/// <summary>
/// Maps CNBV area codes/descriptions to legal subdivision smart enum.
/// </summary>
public static class LegalSubdivisionMapper
{
    /// <summary>
    /// Maps area code/description to a subdivision kind, defaulting to Unknown.
    /// </summary>
    public static LegalSubdivisionKind FromArea(int areaClave, string? areaDescripcion)
    {
        var desc = areaDescripcion?.ToUpperInvariant() ?? string.Empty;

        // Known codes take precedence
        return areaClave switch
        {
            1 => LegalSubdivisionKind.A_AS,
            2 => LegalSubdivisionKind.A_DE,
            3 => LegalSubdivisionKind.A_TF,
            4 => LegalSubdivisionKind.A_IN,
            5 => LegalSubdivisionKind.J_AS,
            6 => LegalSubdivisionKind.J_DE,
            7 => LegalSubdivisionKind.J_IN,
            8 => LegalSubdivisionKind.H_IN,
            9 => LegalSubdivisionKind.E_AS,
            10 => LegalSubdivisionKind.E_DE,
            11 => LegalSubdivisionKind.E_IN,
            _ => MapByText(desc)
        };
    }

    private static LegalSubdivisionKind MapByText(string desc)
    {
        if (string.IsNullOrWhiteSpace(desc))
        {
            return LegalSubdivisionKind.Unknown;
        }

        if (desc.Contains("ASEGURAMIENTO", StringComparison.OrdinalIgnoreCase))
        {
            if (desc.Contains("JUD", StringComparison.OrdinalIgnoreCase) || desc.Contains("J/", StringComparison.OrdinalIgnoreCase))
            {
                return LegalSubdivisionKind.J_AS;
            }

            if (desc.Contains("ILICIT", StringComparison.OrdinalIgnoreCase) || desc.Contains("E/", StringComparison.OrdinalIgnoreCase))
            {
                return LegalSubdivisionKind.E_AS;
            }

            return LegalSubdivisionKind.A_AS;
        }

        if (desc.Contains("DESEMBARGO", StringComparison.OrdinalIgnoreCase))
        {
            if (desc.Contains("JUD", StringComparison.OrdinalIgnoreCase) || desc.Contains("J/", StringComparison.OrdinalIgnoreCase))
            {
                return LegalSubdivisionKind.J_DE;
            }

            if (desc.Contains("ILICIT", StringComparison.OrdinalIgnoreCase) || desc.Contains("E/", StringComparison.OrdinalIgnoreCase))
            {
                return LegalSubdivisionKind.E_DE;
            }

            return LegalSubdivisionKind.A_DE;
        }

        if (desc.Contains("TRANSFER", StringComparison.OrdinalIgnoreCase))
        {
            return LegalSubdivisionKind.A_TF;
        }

        if (desc.Contains("INFORM", StringComparison.OrdinalIgnoreCase) || desc.Contains("DOCUMENT", StringComparison.OrdinalIgnoreCase))
        {
            if (desc.Contains("JUD", StringComparison.OrdinalIgnoreCase) || desc.Contains("J/", StringComparison.OrdinalIgnoreCase))
            {
                return LegalSubdivisionKind.J_IN;
            }

            if (desc.Contains("HAC", StringComparison.OrdinalIgnoreCase) || desc.Contains("H/", StringComparison.OrdinalIgnoreCase))
            {
                return LegalSubdivisionKind.H_IN;
            }

            if (desc.Contains("ILICIT", StringComparison.OrdinalIgnoreCase) || desc.Contains("E/", StringComparison.OrdinalIgnoreCase))
            {
                return LegalSubdivisionKind.E_IN;
            }

            return LegalSubdivisionKind.A_IN;
        }

        return LegalSubdivisionKind.Unknown;
    }
}
