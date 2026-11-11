using System.Text.RegularExpressions;
using ExxerCube.Prisma.Domain.Entities;

namespace ExxerCube.Prisma.Domain.Common.Validation;

/// <summary>
/// Best-effort validation helpers for identity data (RFC/CURP and related fields).
/// Adds warnings for missing/invalid values without hard-failing the entity.
/// </summary>
public static class IdentityValidator
{
    // RFC: 12 chars (moral) or 13 chars (fisica), alphanumeric, last 3 chars alphanumeric
    private static readonly Regex RfcRegex = new(@"^[A-Z&Ñ]{3,4}\d{6}[A-Z0-9]{3}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // CURP: 18 chars with specific structure
    private static readonly Regex CurpRegex = new(@"^[A-Z][AEIOU][A-Z]{2}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Adds warnings to the persona's ValidationState for missing or invalid RFC/CURP.
    /// Does not throw or hard-fail; meant to surface manual review.
    /// </summary>
    public static void FlagIdentityIssues(PersonaSolicitud persona)
    {
        if (persona == null) return;

        var hasRfc = !string.IsNullOrWhiteSpace(persona.Rfc) || persona.RfcVariantes.Count > 0;
        persona.Validation.WarnIf(hasRfc, "RFCMissing");

        if (!string.IsNullOrWhiteSpace(persona.Rfc) && !RfcRegex.IsMatch(persona.Rfc))
        {
            persona.Validation.Warn("RFCInvalid");
        }

        var hasCurp = !string.IsNullOrWhiteSpace(persona.Curp);
        persona.Validation.WarnIf(hasCurp, "CURPMissing");

        if (!string.IsNullOrWhiteSpace(persona.Curp) && !CurpRegex.IsMatch(persona.Curp))
        {
            persona.Validation.Warn("CURPInvalid");
        }
    }
}
