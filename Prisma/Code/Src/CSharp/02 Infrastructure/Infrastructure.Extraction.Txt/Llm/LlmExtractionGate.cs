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

    // CNBV expediente format — anchored PARITY with the deterministic extractor
    // (AdaptiveTxtFieldExtractor.ExtractExpediente:316, unanchored search pattern
    // `[A-Z]/[A-Z]{1,4}\d*[-–]\d+[-–]\d+[-–][A-Z]+`). A value the deterministic
    // extractor would accept is exactly a value this gate accepts — no second source
    // of truth. Do NOT broaden this to bare alphanumeric (boundary-shift false-match risk).
    [GeneratedRegex(@"^[A-Z]/[A-Z]{1,4}\d*[-–]\d+[-–]\d+[-–][A-Z]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ExpedienteRegex();

    // NumeroOficio shape — anchored parity with AdaptiveTxtFieldExtractor.ExtractNumeroOficio:377.
    // Used as a FIELD-LEVEL guard (see IsPlausibleNumeroOficio) — a malformed oficio abstains
    // (mapper leaves the field unset) rather than rejecting the whole DTO.
    [GeneratedRegex(@"^[A-Z]{4,}[A-Z0-9]{0,10}/\d{4}/\d{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex OficioRegex();

    [GeneratedRegex(@"^[A-Z&Ñ]{3,4}\d{6}[A-Z0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex RfcRegex();

    // CURP: 18 chars — 4 letters, 6-digit date, sex (H/M), 5 letters (entity + consonants),
    // homoclave (alnum), check digit. Structural check to reject OCR-mangled / hallucinated CURPs.
    [GeneratedRegex(@"^[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$", RegexOptions.CultureInvariant)]
    private static partial Regex CurpRegex();

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

        // Expediente format: CNBV case-file shape, e.g. A/AS1-1111-222222-AAA
        // (anchored parity with AdaptiveTxtFieldExtractor.ExtractExpediente).
        if (!string.IsNullOrWhiteSpace(dto.Expediente)
            && !ExpedienteRegex().IsMatch(dto.Expediente))
        {
            return $"Expediente '{dto.Expediente}' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA).";
        }

        // RFC format — 3-4 letters/symbols + 6 digits + 3 alphanumeric
        if (!string.IsNullOrWhiteSpace(dto.Rfc)
            && !RfcRegex().IsMatch(dto.Rfc))
        {
            return $"RFC '{dto.Rfc}' does not match required format.";
        }

        // CURP format — reject malformed / OCR-mangled / hallucinated CURP (spec: gate rejects RFC/CURP/...)
        if (!string.IsNullOrWhiteSpace(dto.Curp)
            && !CurpRegex().IsMatch(dto.Curp))
        {
            return $"CURP '{dto.Curp}' does not match required format.";
        }

        // Monto must parse to a positive decimal within a plausible range.
        if (!string.IsNullOrWhiteSpace(dto.Monto))
        {
            if (!TryParseMonto(dto.Monto, out var amount))
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
    /// Parses a currency-formatted or plain Monto string into a decimal, preserving cents
    /// (no rounding — unlike <c>FieldSanitizer.SanitizeMonto</c> in the deterministic Domain
    /// path, which rounds to whole pesos). Strips a leading currency symbol (<c>$</c>), currency
    /// codes (<c>MXN</c>/<c>USD</c>/<c>EUR</c>, case-insensitive), thousands separators
    /// (<c>,</c>), and surrounding whitespace before parsing. The single shared implementation —
    /// both <see cref="Validate"/> and <c>LlmExpedienteMapper</c> call this so the accepted and
    /// stored values never drift.
    /// </summary>
    /// <param name="raw">The raw Monto string from the LLM DTO. May be <see langword="null"/> or whitespace.</param>
    /// <param name="amount">The parsed decimal amount, or <c>0m</c> when parsing fails.</param>
    /// <returns><see langword="true"/> when <paramref name="raw"/> parses to a valid decimal.</returns>
    public static bool TryParseMonto(string? raw, out decimal amount)
    {
        amount = 0m;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var cleaned = raw
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("MXN", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("USD", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("EUR", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
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

    // -----------------------------------------------------------------------
    // Field-level abstention guards (S4-B)
    // -----------------------------------------------------------------------
    // A plausible WRONG value is worse than an abstention (CNBV legal principle). These guards
    // do NOT reject the whole DTO — they let LlmExpedienteMapper decide, per field, whether to
    // set the value on Expediente or leave it at its default (abstain).

    /// <summary>
    /// Returns <see langword="true"/> only when <paramref name="value"/> matches the anchored
    /// deterministic oficio shape (mirrors <c>AdaptiveTxtFieldExtractor.ExtractNumeroOficio</c>,
    /// e.g. <c>AGAFADAFSON2/2025/000084</c>). Used by the mapper to decide whether to set
    /// <c>Expediente.NumeroOficio</c> or abstain (leave unset).
    /// </summary>
    public static bool IsPlausibleNumeroOficio(string? value) =>
        !string.IsNullOrWhiteSpace(value) && OficioRegex().IsMatch(value.Trim());

    /// <summary>
    /// Minimum honesty guard for a free-text authority name: non-empty after trim, at least 5
    /// characters, and containing at least one space (rejects single-token garbage like "XZ").
    /// Used by the mapper to decide whether to set <c>Expediente.AutoridadNombre</c> or abstain.
    /// </summary>
    public static bool IsPlausibleAutoridadNombre(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        return trimmed.Length >= 5 && trimmed.Contains(' ');
    }
}
