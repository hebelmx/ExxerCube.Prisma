using System.Globalization;
using System.Text.RegularExpressions;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Txt.Llm;

/// <summary>
/// PURE static gate that validates the structural integrity of an <see cref="LlmExpedienteDto"/>
/// before it is mapped to domain objects.  No I/O; deterministic; fast to unit-test.
/// </summary>
public static partial class LlmExtractionGate
{
    // -----------------------------------------------------------------------
    // Compiled regexes (GeneratedRegex — zero-overhead at call time)
    // -----------------------------------------------------------------------

    [GeneratedRegex(@"^\d{3,6}/\d{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex ExpedienteRegex();

    [GeneratedRegex(@"^[A-Z&Ñ]{3,4}\d{6}[A-Z0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex RfcRegex();

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns <see langword="null"/> when the DTO is valid; otherwise returns a human-readable
    /// rejection reason.
    /// </summary>
    /// <param name="dto">The DTO to validate. May be <see langword="null"/>.</param>
    public static string? Validate(LlmExpedienteDto? dto)
    {
        if (dto is null)
        {
            return "DTO is null.";
        }

        // Reject if ALL identifying fields are absent / empty.
        var hasAnyCore = !string.IsNullOrWhiteSpace(dto.Expediente)
            || !string.IsNullOrWhiteSpace(dto.Solicitante)
            || !string.IsNullOrWhiteSpace(dto.Monto)
            || (dto.Partes is { Length: > 0 });

        if (!hasAnyCore)
        {
            return "DTO contains no usable fields (all-null).";
        }

        // Expediente format: nnn/yyyy  (3-6 digits, slash, 4-digit year)
        if (!string.IsNullOrWhiteSpace(dto.Expediente)
            && !ExpedienteRegex().IsMatch(dto.Expediente))
        {
            return $"Expediente '{dto.Expediente}' does not match required format ddd/yyyy.";
        }

        // RFC format — 3-4 letters/symbols + 6 digits + 3 alphanumeric
        if (!string.IsNullOrWhiteSpace(dto.Rfc)
            && !RfcRegex().IsMatch(dto.Rfc))
        {
            return $"RFC '{dto.Rfc}' does not match required format.";
        }

        // Monto must parse to a positive decimal within a plausible range.
        if (!string.IsNullOrWhiteSpace(dto.Monto))
        {
            if (!decimal.TryParse(dto.Monto, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                return $"Monto '{dto.Monto}' is not a valid decimal number.";
            }

            if (amount <= 0m || amount >= 100_000_000m)
            {
                return $"Monto {amount} is outside the allowed range (0, 100,000,000).";
            }
        }

        return null; // valid
    }

    /// <summary>
    /// Returns <see langword="true"/> when the DTO passes all structural checks.
    /// </summary>
    /// <param name="dto">The DTO to validate.</param>
    /// <param name="reason">
    /// When this method returns <see langword="false"/>, contains the rejection reason;
    /// otherwise <see langword="null"/>.
    /// </param>
    public static bool IsValid(LlmExpedienteDto? dto, out string? reason)
    {
        reason = Validate(dto);
        return reason is null;
    }

    /// <summary>
    /// Convenience overload without an out-param reason (for callers that only need the boolean).
    /// </summary>
    public static bool IsValid(LlmExpedienteDto? dto) => Validate(dto) is null;
}
