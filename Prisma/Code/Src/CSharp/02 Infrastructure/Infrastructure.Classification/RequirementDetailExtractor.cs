using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Pure static helper that extracts structured sub-answer fields from Spanish legal document text.
/// Shared between <see cref="SemanticAnalyzerService"/> and <see cref="LegalDirectiveClassifierService"/>
/// so regex patterns live in ONE place (no duplication).
/// All methods are best-effort: unmatched fields return null/empty, never throw.
/// </summary>
internal static class RequirementDetailExtractor
{
    // ---------------------------------------------------------------------------
    // Shared compiled patterns (static, allocated once)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Matches explicit account-number references like "cuenta 1234567890".
    /// Requires the word "cuenta" to distinguish from random long digits.
    /// </summary>
    private static readonly Regex AccountPattern =
        new(@"cuenta\s+(\d{4,})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>18-digit CLABE (Mexican interbank routing code).</summary>
    private static readonly Regex ClabePattern =
        new(@"\b(\d{18})\b", RegexOptions.Compiled);

    /// <summary>Dollar-sign prefixed monetary amounts: $1,000,000.00</summary>
    private static readonly Regex AmountDollarSign =
        new(@"\$\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Amounts preceded by monetary keywords (monto/cantidad/importe/suma).</summary>
    private static readonly Regex AmountKeyword =
        new(@"(?:monto|cantidad|importe|suma)\s+(?:de\s+)?\$?\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Amounts followed by explicit currency words (pesos/dólares/dolares).</summary>
    private static readonly Regex AmountCurrencyWord =
        new(@"(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\s+(?:pesos|d[oó]lares)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Amounts with at least two comma groups (distinguishes from plain account numbers).
    /// e.g. 1,000,000.00
    /// </summary>
    private static readonly Regex AmountMultiComma =
        new(@"(\d{1,3}(?:,\d{3}){2,}(?:\.\d{2})?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Detects explicit "pesos" currency marker.</summary>
    private static readonly Regex PesosPattern =
        new(@"\bpesos\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Detects explicit "dólares" / "dolares" currency marker.</summary>
    private static readonly Regex DolaresPattern =
        new(@"\bd[oó]lares\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Partial-freeze keywords: "parcial", "hasta por el monto", "importe de".
    /// Presence of an explicit amount also implies partial.
    /// </summary>
    private static readonly Regex ParcialKeyword =
        new(@"\b(?:parcial|hasta\s+por\s+el\s+monto|hasta\s+por\s+un\s+monto|importe\s+de)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Expediente/oficio identifiers.
    /// Covers: A/AS1-1111-222222-AAA, 222/AAA/-4444444444/2025, B/CDEF-1234-567890-ABC,
    /// IMSSCOB/40/01/001283/2025, AGAFADAFSON2/2025/000084.
    /// Pattern: one or more alphanumeric segments separated by / or -, with at least two segments.
    /// </summary>
    private static readonly Regex ExpedienteIdPattern =
        new(@"\b([A-Z0-9]{1,20}(?:[/\-][A-Z0-9\-]{1,20}){2,})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>ISO 8601 or common Spanish date formats: 2025-06-05, 05/06/2025, 5 de junio de 2025.</summary>
    private static readonly Regex IsoDatePattern =
        new(@"\b(\d{4}-\d{2}-\d{2})\b", RegexOptions.Compiled);

    private static readonly Regex SlashDatePattern =
        new(@"\b(\d{1,2}/\d{1,2}/\d{4})\b", RegexOptions.Compiled);

    private static readonly Regex SpanishDatePattern =
        new(
            @"\b(\d{1,2})\s+de\s+(enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre)\s+de\s+(\d{4})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ---------------------------------------------------------------------------
    // Public extraction methods — one per requirement category
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Extracts Bloqueo sub-fields: accounts, amount+currency, products, partial flag.
    /// </summary>
    /// <param name="text">Raw document text (any case).</param>
    /// <param name="req">Requirement object to populate in-place.</param>
    public static void PopulateBloqueo(string text, BloqueoRequirement req)
    {
        req.CuentasEspecificas = ExtractAccountNumbers(text);
        (req.Monto, req.Moneda) = ExtractAmountAndCurrency(text);
        req.ProductosEspecificos = ExtractProducts(text);
        req.EsParcial = req.Monto.HasValue || ParcialKeyword.IsMatch(text);
    }

    /// <summary>
    /// Extracts Desbloqueo sub-fields: original-block expediente/oficio reference.
    /// Picks the expediente id that differs from the current case id (when available).
    /// </summary>
    /// <param name="text">Raw document text.</param>
    /// <param name="currentExpediente">The current case id to exclude (may be null).</param>
    /// <param name="req">Requirement object to populate in-place.</param>
    public static void PopulateDesbloqueo(string text, string? currentExpediente, DesbloqueoRequirement req)
    {
        req.ExpedienteBloqueoOriginal = ExtractReferencedExpedienteId(text, currentExpediente);
    }

    /// <summary>
    /// Extracts Documentacion sub-fields: document types + date ranges.
    /// </summary>
    /// <param name="text">Raw document text.</param>
    /// <param name="req">Requirement object to populate in-place.</param>
    public static void PopulateDocumentacion(string text, DocumentacionRequirement req)
    {
        req.TiposDocumento = ExtractDocumentTypes(text);
    }

    /// <summary>
    /// Extracts Transferencia sub-fields: destination account (CLABE preferred), amount.
    /// </summary>
    /// <param name="text">Raw document text.</param>
    /// <param name="req">Requirement object to populate in-place.</param>
    public static void PopulateTransferencia(string text, TransferenciaRequirement req)
    {
        // Prefer CLABE (18-digit) as destination; fall back to cuenta pattern
        var clabe = ClabePattern.Match(text);
        req.CuentaDestino = clabe.Success
            ? clabe.Groups[1].Value
            : ExtractFirstAccountNumber(text);

        (req.Monto, _) = ExtractAmountAndCurrency(text);
    }

    /// <summary>
    /// Extracts Informacion sub-fields: best-effort sentence/phrase pull for InformacionSolicitada.
    /// E2 (Ollama) will enrich this further; this provides a deterministic baseline.
    /// </summary>
    /// <param name="text">Raw document text.</param>
    /// <param name="req">Requirement object to populate in-place.</param>
    public static void PopulateInformacion(string text, InformacionGeneralRequirement req)
    {
        req.InformacionSolicitada = ExtractInformacionPhrase(text);
    }

    // ---------------------------------------------------------------------------
    // Shared helpers — called by BOTH SemanticAnalyzerService and LegalDirectiveClassifierService
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Extracts all account numbers (4+ digit sequences preceded by "cuenta").
    /// </summary>
    public static List<string> ExtractAccountNumbers(string text)
    {
        var result = new List<string>();
        foreach (Match m in AccountPattern.Matches(text))
        {
            var num = m.Groups[1].Value;
            if (!result.Contains(num))
                result.Add(num);
        }
        return result;
    }

    /// <summary>Returns the first "cuenta NNNN" match or null.</summary>
    public static string? ExtractFirstAccountNumber(string text)
    {
        var m = AccountPattern.Match(text);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>
    /// Extracts product types (TARJETA, CUENTA) found in text.
    /// Returns a deduplicated list preserving match order.
    /// </summary>
    public static List<string> ExtractProducts(string text)
    {
        var result = new List<string>();
        if (Regex.IsMatch(text, @"\bTARJETA\b", RegexOptions.IgnoreCase) && !result.Contains("TARJETA"))
            result.Add("TARJETA");
        if (Regex.IsMatch(text, @"\bCUENTA\b", RegexOptions.IgnoreCase) && !result.Contains("CUENTA"))
            result.Add("CUENTA");
        return result;
    }

    /// <summary>
    /// Extracts the best monetary amount and its currency from text.
    /// Priority: dollar-sign > keyword context > currency word > multi-comma.
    /// Returns (null, null) when no amount found.
    /// </summary>
    public static (decimal? Amount, string? Currency) ExtractAmountAndCurrency(string text)
    {
        // Try patterns in priority order (mirrors LegalDirectiveClassifierService.ExtractActionDetails)
        var patterns = new[] { AmountDollarSign, AmountKeyword, AmountCurrencyWord, AmountMultiComma };

        Match? best = null;
        int bestPriority = int.MaxValue;

        for (int i = 0; i < patterns.Length; i++)
        {
            foreach (Match m in patterns[i].Matches(text))
            {
                var digits = m.Groups[1].Value.Replace(",", "").Replace(".", "");
                // Skip long all-digit strings without monetary context (likely account numbers)
                if (digits.Length >= 10
                    && !m.Value.Contains("$")
                    && !ContainsMonetaryKeyword(m.Value))
                    continue;

                if (best is null || i < bestPriority ||
                    (i == bestPriority && m.Value.Length > best.Value.Length))
                {
                    best = m;
                    bestPriority = i;
                }
            }
        }

        if (best is null)
            return (null, null);

        var amountStr = best.Groups[1].Value.Replace(",", string.Empty);
        decimal? amount = decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var a)
            ? a : null;

        // Determine currency from surrounding text
        string? currency = null;
        if (DolaresPattern.IsMatch(text))
            currency = "USD";
        else if (PesosPattern.IsMatch(text) || best.Value.Contains("$"))
            currency = "MXN";

        return (amount, currency);
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private static bool ContainsMonetaryKeyword(string fragment) =>
        fragment.Contains("monto", StringComparison.OrdinalIgnoreCase)
        || fragment.Contains("cantidad", StringComparison.OrdinalIgnoreCase)
        || fragment.Contains("importe", StringComparison.OrdinalIgnoreCase)
        || fragment.Contains("pesos", StringComparison.OrdinalIgnoreCase)
        || fragment.Contains("dolares", StringComparison.OrdinalIgnoreCase)
        || fragment.Contains("dólares", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Finds an expediente/oficio id in text that is distinct from <paramref name="currentExpediente"/>.
    /// Returns null when none found.
    /// </summary>
    private static string? ExtractReferencedExpedienteId(string text, string? currentExpediente)
    {
        var normalizedCurrent = currentExpediente?.Trim().ToUpperInvariant();
        foreach (Match m in ExpedienteIdPattern.Matches(text))
        {
            var candidate = m.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
                continue;
            if (!string.IsNullOrEmpty(normalizedCurrent) &&
                candidate.ToUpperInvariant() == normalizedCurrent)
                continue;
            return candidate;
        }
        return null;
    }

    /// <summary>
    /// Detects document sub-types from curated vocabulary (aligned with ClassificationDictionary style).
    /// Dates are extracted and paired with each doc type when a range is present in the text.
    /// </summary>
    private static List<DocumentoRequerido> ExtractDocumentTypes(string text)
    {
        // Curated doc-type keyword groups (order = detection priority)
        var docTypePatterns = new (string Tipo, Regex Pattern)[]
        {
            ("Estado de cuenta",    new Regex(@"\bestados?\s+de\s+cuenta\b",    RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Identificación",      new Regex(@"\b(?:identificaci[oó]n|INE|IFE|credencial\s+de\s+elector)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Comprobante de domicilio", new Regex(@"\bcomprobante\s+de\s+domicilio\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Contrato",            new Regex(@"\bcontrato\b",                 RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Muestra de firma",    new Regex(@"\b(?:muestra\s+de\s+firma|firma\s+de\s+muestra|tarjet[oó]n\s+de\s+firma)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Cheque",              new Regex(@"\bcheque\b",                   RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Expediente de apertura", new Regex(@"\bexpediente\s+de\s+apertura\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        };

        // Extract date range from the whole text (shared across detected types)
        var (periodoInicio, periodoFin) = ExtractDateRange(text);

        var result = new List<DocumentoRequerido>();
        foreach (var (tipo, pattern) in docTypePatterns)
        {
            if (pattern.IsMatch(text))
            {
                result.Add(new DocumentoRequerido
                {
                    Tipo = tipo,
                    PeriodoInicio = periodoInicio,
                    PeriodoFin = periodoFin
                });
            }
        }
        return result;
    }

    /// <summary>
    /// Parses date pairs from text. Returns the earliest and latest dates found (or null when fewer than 2).
    /// </summary>
    private static (DateTime? Start, DateTime? End) ExtractDateRange(string text)
    {
        var dates = new List<DateTime>();

        // ISO 8601: 2025-06-05
        foreach (Match m in IsoDatePattern.Matches(text))
        {
            if (DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                dates.Add(d);
        }

        // Slash format: 05/06/2025
        foreach (Match m in SlashDatePattern.Matches(text))
        {
            if (DateTime.TryParseExact(m.Groups[1].Value, "d/M/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                dates.Add(d);
        }

        // Spanish prose: "5 de junio de 2025"
        foreach (Match m in SpanishDatePattern.Matches(text))
        {
            var combined = $"{m.Groups[1].Value} de {m.Groups[2].Value} de {m.Groups[3].Value}";
            if (TryParseSpanishDate(combined, out var d))
                dates.Add(d);
        }

        if (dates.Count < 2)
            return (null, null);

        dates.Sort();
        return (dates[0], dates[^1]);
    }

    private static readonly Dictionary<string, int> SpanishMonths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enero"] = 1, ["febrero"] = 2, ["marzo"] = 3, ["abril"] = 4,
        ["mayo"] = 5, ["junio"] = 6, ["julio"] = 7, ["agosto"] = 8,
        ["septiembre"] = 9, ["octubre"] = 10, ["noviembre"] = 11, ["diciembre"] = 12
    };

    private static bool TryParseSpanishDate(string spanishDate, out DateTime result)
    {
        result = default;
        // "5 de junio de 2025"
        var parts = spanishDate.Split(new[] { " de " }, StringSplitOptions.None);
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0].Trim(), out var day)) return false;
        if (!SpanishMonths.TryGetValue(parts[1].Trim(), out var month)) return false;
        if (!int.TryParse(parts[2].Trim(), out var year)) return false;
        try
        {
            result = new DateTime(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>
    /// Best-effort pull for InformacionSolicitada: finds the first sentence containing
    /// information-request indicators. Returns null when nothing found.
    /// E2 (Ollama) will refine this with an LLM prompt.
    /// </summary>
    private static readonly Regex InfoRequestPhrase =
        new(@"(?:solicit[ao]|requier[eo]|inform[ae]r?|proporcionar|reporte)\s+(?:sobre\s+)?(.{10,120}?)(?:[.;]|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);

    private static string? ExtractInformacionPhrase(string text)
    {
        var m = InfoRequestPhrase.Match(text);
        if (!m.Success) return null;
        var phrase = m.Value.Trim();
        // Cap at 200 chars to avoid pulling huge paragraphs
        return phrase.Length > 200 ? phrase[..200] : phrase;
    }
}
