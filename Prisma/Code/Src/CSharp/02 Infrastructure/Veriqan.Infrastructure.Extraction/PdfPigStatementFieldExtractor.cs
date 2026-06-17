using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;

/// <summary>
/// PdfPig-based implementation of <see cref="IStatementFieldExtractor"/> for VEC statements.
/// </summary>
/// <remarks>
/// <para>
/// <b>Header label→value association strategy:</b> the VEC statement header uses a two-column layout.
/// Labels appear in the right half of the page (X ≥ ~290) and their corresponding values appear
/// further to the right on the <em>same horizontal band</em> (within a Y tolerance of ±5 pt).
/// The extractor groups words into horizontal bands, identifies known label phrases by matching
/// words whose text matches a label keyword sequence, then collects all words on the same band
/// that start to the right of the label's right edge.
/// </para>
/// <para>
/// Client name and address occupy the top-right quadrant of the header (Y ≥ ~640 in PDF points,
/// X ≥ ~330) without an explicit label prefix — they are extracted by position heuristics.
/// </para>
/// <para>
/// <b>Period/summary extraction (Story 3.2):</b> the period/summary block occupies the
/// LEFT column of the same page-1 area as the header (Y ≈ 523–610), plus the lower portion
/// of the page for TASA/CAT/saldo data (Y ≈ 160–292).  All words from page 1 (and page 2
/// if present) are scanned.  Label matching uses ordinal case-insensitive comparison against
/// known Spanish phrase patterns.
/// </para>
/// <para>
/// <b>Layout findings from empirical calibration (Dummie VEC fixture #1):</b>
/// <list type="bullet">
///   <item><description>Y=648.4: "Tarjeta de Crédito BSSB" (product name)</description></item>
///   <item><description>Y=601.6: "Fecha de Corte 04 de ago 2025" (right column)</description></item>
///   <item><description>Y=588.1: "Periodo 5-jul-2025 al 04-ago-2025" (left column)</description></item>
///   <item><description>Y=577.3: "Número de días en el periodo: 31 días" (left column)</description></item>
///   <item><description>Y=566.5: "Fecha límite de pago 1 lunes, 25-ago-2025" (left — has spurious "1" token)</description></item>
///   <item><description>Y=555.7: "Pago para no generar intereses 2 $32,446.69" (left — has spurious "2" token)</description></item>
///   <item><description>Y=544.9+534.1: "Pago mínimo + compras … (wrap) diferidos a meses: 3 $3,145.39"</description></item>
///   <item><description>Y=523.3: "Pago mínimo: 4 $2,160.00"</description></item>
///   <item><description>Y=290.6+285.8: TASA and CAT labels (caps, multi-word)</description></item>
///   <item><description>Y=264.0: "28.86% sin IVA 27.36%" (CAT = 28.86%, TASA ordinary = 27.36%)</description></item>
///   <item><description>Y=280.9: "PAGO PARA NO GENERAR INTERESES … = $32,446.69" (second occurrence)</description></item>
///   <item><description>Y=161.0: "Saldo deudor total: 11 $ 52,387.85"</description></item>
///   <item><description>Y=139.4: "Crédito disponible: $ 47,612.15"</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class PdfPigStatementFieldExtractor : IStatementFieldExtractor
{
    // -----------------------------------------------------------------------
    // Constants / patterns — header identity fields
    // -----------------------------------------------------------------------

    /// <summary>Mexican RFC pattern (individuals: 4-char prefix; corporations: 3-char prefix).</summary>
    private static readonly Regex RfcPattern = new(
        @"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DigitsOnly = new(@"\D", RegexOptions.Compiled);

    /// <summary>5-digit Mexican postal code.</summary>
    private static readonly Regex PostalCodePattern = new(@"^\d{5}$", RegexOptions.Compiled);

    // Header layout constants (PDF points, origin bottom-left).
    private const double HeaderYMin = 530.0;
    private const double HeaderYMax = 700.0;
    private const double LabelColumnXMin = 285.0;
    private const double ValueColumnXMin = 410.0;
    private const double ClientNameXMin = 330.0;
    private const double ClientNameYMin = 630.0;
    private const double AddressYMax = 680.0;

    /// <summary>Y-band tolerance (pt) for treating words as on the same line.</summary>
    private const double YBandTolerance = 5.0;

    // -----------------------------------------------------------------------
    // Constants / patterns — period/summary (Story 3.2)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Spanish month abbreviation → 1-based month number map.
    /// </summary>
    private static readonly Dictionary<string, int> SpanishMonthMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ene"] = 1,  ["enero"] = 1,
            ["feb"] = 2,  ["febrero"] = 2,
            ["mar"] = 3,  ["marzo"] = 3,
            ["abr"] = 4,  ["abril"] = 4,
            ["may"] = 5,  ["mayo"] = 5,
            ["jun"] = 6,  ["junio"] = 6,
            ["jul"] = 7,  ["julio"] = 7,
            ["ago"] = 8,  ["agosto"] = 8,
            ["sep"] = 9,  ["septiembre"] = 9, ["sept"] = 9,
            ["oct"] = 10, ["octubre"] = 10,
            ["nov"] = 11, ["noviembre"] = 11,
            ["dic"] = 12, ["diciembre"] = 12,
        };

    /// <summary>"d-mmm-yyyy" date format (e.g. "5-jul-2025", "04-ago-2025").</summary>
    private static readonly Regex DateDashFormat = new(
        @"^(\d{1,2})-([a-záéíóúñü]+)-(\d{4})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Amount token: optional "$", digit-groups with commas, optional decimal.
    /// e.g. "$32,446.69", "2,160.00", "$0.00".
    /// </summary>
    private static readonly Regex AmountPattern = new(
        @"^\$?([\d,]+(?:\.\d+)?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Percentage: digits, optional decimal, then "%". e.g. "19.75%", "28.86%".</summary>
    private static readonly Regex PercentPattern = new(
        @"^(\d+(?:\.\d+)?)%$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ILogger<PdfPigStatementFieldExtractor> _logger;

    /// <summary>Initializes a new <see cref="PdfPigStatementFieldExtractor"/>.</summary>
    public PdfPigStatementFieldExtractor(ILogger<PdfPigStatementFieldExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // -----------------------------------------------------------------------
    // IStatementFieldExtractor — ExtractHeaderAsync (Story 3.1)
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public Task<Result<StatementModel>> ExtractHeaderAsync(
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<StatementModel>());

        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0)
            return Task.FromResult(Result<StatementModel>.WithFailure("PDF bytes are empty."));

        try
        {
            var model = ExtractHeaderOnly(pdf);
            return Task.FromResult(Result<StatementModel>.WithSuccess(model));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract header fields from PDF ({ByteCount} bytes).", pdf.Length);
            return Task.FromResult(
                Result<StatementModel>.WithFailure($"PDF extraction failed: {ex.Message}"));
        }
    }

    // -----------------------------------------------------------------------
    // IStatementFieldExtractor — ExtractFullAsync (Story 3.2)
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public Task<Result<StatementModel>> ExtractFullAsync(
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<StatementModel>());

        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0)
            return Task.FromResult(Result<StatementModel>.WithFailure("PDF bytes are empty."));

        try
        {
            using var doc = PdfDocument.Open(pdf);

            // ---- Page 1 words -------------------------------------------
            var page1 = doc.GetPage(1);
            var allPage1Words = page1.GetWords().ToList();

            // ---- Header extraction (right column only) -------------------
            var headerWords = allPage1Words
                .Where(w => w.BoundingBox.Bottom >= HeaderYMin && w.BoundingBox.Bottom <= HeaderYMax)
                .ToList();
            var headerBands = GroupIntoBands(headerWords);

            var clientName = ExtractClientName(headerWords);
            var address = ExtractAddress(headerWords);
            var branchNumber = ExtractLabeledField(headerBands, ["Número", "de", "sucursal"]);
            var cardNumber = ExtractAndValidateCardNumber(headerBands);
            var clabe = ExtractAndValidateClabe(headerBands);
            var clientNumber = ExtractLabeledField(headerBands, ["Número", "de", "cliente"]);
            var rfc = ExtractAndValidateRfc(headerBands);

            // ---- Period/summary extraction — page 1 only -------------------
            // All period/summary fields (periodo, fechas, importes, TASA, saldo)
            // are on page 1 of the VEC statement.  Including page-2 words would
            // introduce Y-coordinate values that collide with page-1 bands and
            // corrupt band grouping (PdfPig resets Y per page).
            var periodSummary = ExtractPeriodSummary(allPage1Words);

            var model = new StatementModel(
                clientName: clientName,
                address: address,
                branchNumber: branchNumber,
                cardNumber: cardNumber,
                clabe: clabe,
                clientNumber: clientNumber,
                rfc: rfc)
            {
                PeriodSummary = periodSummary,
            };

            return Task.FromResult(Result<StatementModel>.WithSuccess(model));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract full fields from PDF ({ByteCount} bytes).", pdf.Length);
            return Task.FromResult(
                Result<StatementModel>.WithFailure($"PDF full extraction failed: {ex.Message}"));
        }
    }

    // -----------------------------------------------------------------------
    // Header-only extraction (synchronous — PdfPig is synchronous)
    // -----------------------------------------------------------------------

    private StatementModel ExtractHeaderOnly(byte[] pdfBytes)
    {
        using var doc = PdfDocument.Open(pdfBytes);
        var allWords = doc.GetPage(1).GetWords().ToList();

        var headerWords = allWords
            .Where(w => w.BoundingBox.Bottom >= HeaderYMin && w.BoundingBox.Bottom <= HeaderYMax)
            .ToList();
        var bands = GroupIntoBands(headerWords);

        return new StatementModel(
            clientName: ExtractClientName(headerWords),
            address: ExtractAddress(headerWords),
            branchNumber: ExtractLabeledField(bands, ["Número", "de", "sucursal"]),
            cardNumber: ExtractAndValidateCardNumber(bands),
            clabe: ExtractAndValidateClabe(bands),
            clientNumber: ExtractLabeledField(bands, ["Número", "de", "cliente"]),
            rfc: ExtractAndValidateRfc(bands));
    }

    // -----------------------------------------------------------------------
    // Period/summary extraction (Story 3.2)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Extracts the period/summary block from all collected page words.
    /// Operates on the full word list from page 1 (+ page 2 if present) — no Y filtering —
    /// since the period/summary data spans both the header-adjacent zone (Y 523–610)
    /// and the lower section of the page (Y 139–292).
    /// </summary>
    private PeriodSummary ExtractPeriodSummary(List<Word> allWords)
    {
        // Sort top-to-bottom, left-to-right for sequential scanning.
        var sorted = allWords
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        // Band dictionary for same-line value extraction.
        var bands = GroupIntoBands(allWords);

        // ---- Product name -----------------------------------------------
        // "Tarjeta de Crédito BSSB" at Y≈648 on the left side of the header.
        var product = ExtractProductName(sorted, bands);

        // ---- Periodo: "Periodo 5-jul-2025 al 04-ago-2025" at Y≈588 -----
        var periodStart = ExtractPeriodStart(sorted, bands);

        // ---- Número de días en el periodo at Y≈577 ----------------------
        var dayCountPrinted = ExtractDayCount(sorted, bands);

        // ---- Fecha de Corte at Y≈601 (right column) ---------------------
        var periodCutDate = ExtractFechaDeCorte(sorted, bands);

        // ---- Fecha límite de pago at Y≈566 ------------------------------
        // Note: fixture has a spurious "1" token between label and "lunes,"
        var paymentDueDate = ExtractFechaLimiteDePago(sorted, bands);

        // ---- Summary amounts (left column, Y≈555, 544, 523) ----
        var pagoNoInt = ExtractPagoParaNoGenerarIntereses(sorted, bands);
        var pagoMinMeses = ExtractPagoMinimoMasMeses(sorted, bands);
        var pagoMin = ExtractPagoMinimo(sorted, bands);

        // ---- TASA and CAT (lower section, Y≈264) ------------------------
        // The percentage values appear on a band below the heading labels.
        // In fixture #1: "28.86% sin IVA 27.36%" at Y=264.0
        // CAT = first percent token (28.86%), TASA ORDINARIA FIJA = second (27.36%)
        var (tasa, cat) = ExtractTasaAndCat(sorted, bands);

        // ---- Saldo Deudor Total at Y≈161 --------------------------------
        var saldoDeudor = ExtractSaldoDeudorTotal(sorted, bands);

        // ---- Crédito Disponible at Y≈139 --------------------------------
        var creditoDisponible = ExtractCreditoDisponible(sorted, bands);

        // ---- Day-count verification -------------------------------------
        var dayCount = DayCountVerification.Compute(periodStart, periodCutDate, dayCountPrinted);

        return new PeriodSummary(
            product: product,
            periodStart: periodStart,
            periodCutDate: periodCutDate,
            paymentDueDate: paymentDueDate,
            dayCountPrinted: dayCountPrinted,
            dayCount: dayCount,
            pagoParaNoGenerarIntereses: pagoNoInt,
            pagoMinimo: pagoMin,
            pagoMinimoMasMeses: pagoMinMeses,
            tasa: tasa,
            cat: cat,
            saldoDeudorTotal: saldoDeudor,
            creditoDisponible: creditoDisponible);
    }

    // -----------------------------------------------------------------------
    // Product name
    // -----------------------------------------------------------------------

    private static ExtractedField<string> ExtractProductName(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands)
    {
        // "Tarjeta de Crédito BSSB" — find band starting with "Tarjeta"
        // at X < ~200 (left column) to avoid the right-column "Número de Tarjeta" header label.
        foreach (var w in sorted)
        {
            if (!string.Equals(w.Text, "Tarjeta", StringComparison.OrdinalIgnoreCase))
                continue;
            // Left-column only: the product heading is at X ≈ 15-175 in the fixture.
            if (w.BoundingBox.Left > 200)
                continue;

            var bandY = w.BoundingBox.Bottom;
            var band = GetBand(bands, bandY);
            var text = BandText(band);

            if (!string.IsNullOrWhiteSpace(text))
                return ExtractedField<string>.Found(text, BoundingBoxOf(band, 1));
        }

        return ExtractedField<string>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Period start
    // -----------------------------------------------------------------------

    private static ExtractedField<DateOnly> ExtractPeriodStart(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands)
    {
        // "Periodo 5-jul-2025 al 04-ago-2025" — find "Periodo" in left column.
        foreach (var w in sorted)
        {
            if (!string.Equals(w.Text, "Periodo", StringComparison.OrdinalIgnoreCase))
                continue;
            if (w.BoundingBox.Left > 100)  // left-column guard
                continue;

            var bandY = w.BoundingBox.Bottom;
            var band = GetBand(bands, bandY);
            var locator = BoundingBoxOf(band, 1);

            // Tokens after "Periodo": date1, "al", date2
            var idx = band.FindIndex(x => string.Equals(x.Text, "Periodo", StringComparison.OrdinalIgnoreCase));
            if (idx < 0 || idx + 1 >= band.Count)
                return ExtractedField<DateOnly>.Missing(locator);

            // First token after "Periodo" is the start date.
            var startToken = band[idx + 1].Text;
            if (TryParseSpanishDate(startToken, out var startDate))
                return ExtractedField<DateOnly>.Found(startDate, locator);

            return ExtractedField<DateOnly>.Missing(locator);
        }

        return ExtractedField<DateOnly>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Day count
    // -----------------------------------------------------------------------

    private static ExtractedField<int> ExtractDayCount(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands)
    {
        // "Número de días en el periodo: 31 días" — find "Número" in left column
        // followed by "de" and "días".
        for (var i = 0; i + 1 < sorted.Count; i++)
        {
            if (!string.Equals(sorted[i].Text, "Número", StringComparison.OrdinalIgnoreCase))
                continue;
            if (sorted[i].BoundingBox.Left > 100)  // left-column guard
                continue;

            var bandY = sorted[i].BoundingBox.Bottom;
            var band = GetBand(bands, bandY);

            // Verify "días" is on this band.
            if (!band.Any(x => string.Equals(x.Text, "días", StringComparison.OrdinalIgnoreCase)))
                continue;

            var locator = BoundingBoxOf(band, 1);

            // Find the first purely-numeric token on this band (the count).
            // "periodo:" is not a digit, "31" is.
            var numToken = band
                .FirstOrDefault(x => int.TryParse(x.Text.TrimEnd(':'),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                    && !string.Equals(x.Text.TrimEnd(':'), string.Empty, StringComparison.Ordinal));

            if (numToken is null)
                return ExtractedField<int>.Missing(locator);

            var txt = numToken.Text.TrimEnd(':');
            if (int.TryParse(txt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days))
                return ExtractedField<int>.Found(days, BoundingBoxOf([numToken], 1));

            return ExtractedField<int>.Missing(locator);
        }

        return ExtractedField<int>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Fecha de Corte
    // -----------------------------------------------------------------------

    private static ExtractedField<DateOnly> ExtractFechaDeCorte(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands)
    {
        // "Fecha de Corte 04 de ago 2025" — in right column (X ≈ 298).
        for (var i = 0; i + 2 < sorted.Count; i++)
        {
            if (!string.Equals(sorted[i].Text, "Fecha", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(sorted[i + 1].Text, "de", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(sorted[i + 2].Text, "Corte", StringComparison.OrdinalIgnoreCase))
                continue;

            var bandY = sorted[i].BoundingBox.Bottom;
            var band = GetBand(bands, bandY);
            var locator = BoundingBoxOf(band, 1);

            // Value words: to the right of "Corte".
            var labelRight = sorted[i + 2].BoundingBox.Right;
            var valueWords = band
                .Where(x => x.BoundingBox.Left > labelRight)
                .OrderBy(x => x.BoundingBox.Left)
                .ToList();

            if (valueWords.Count == 0)
                return ExtractedField<DateOnly>.Missing(locator);

            // Reconstruct the date string from all value tokens.
            // e.g. ["04", "de", "ago", "2025"] → "04 de ago 2025"
            var dateStr = string.Join(" ", valueWords.Select(x => x.Text)).Trim();
            if (TryParseSpanishDate(dateStr, out var date))
                return ExtractedField<DateOnly>.Found(date, BoundingBoxOf(valueWords, 1));

            return ExtractedField<DateOnly>.Missing(locator);
        }

        return ExtractedField<DateOnly>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Fecha límite de pago
    // -----------------------------------------------------------------------

    private static ExtractedField<DateOnly> ExtractFechaLimiteDePago(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands)
    {
        // "Fecha límite de pago 1 lunes, 25-ago-2025"
        // Note: the "1" is a superscript footnote marker — skip it.
        for (var i = 0; i + 3 < sorted.Count; i++)
        {
            if (!string.Equals(sorted[i].Text, "Fecha", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(sorted[i + 1].Text, "límite", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(sorted[i + 2].Text, "de", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(sorted[i + 3].Text, "pago", StringComparison.OrdinalIgnoreCase))
                continue;

            var bandY = sorted[i].BoundingBox.Bottom;
            var band = GetBand(bands, bandY);
            var locator = BoundingBoxOf(band, 1);

            var labelRight = sorted[i + 3].BoundingBox.Right;
            var valueWords = band
                .Where(x => x.BoundingBox.Left > labelRight)
                .OrderBy(x => x.BoundingBox.Left)
                .ToList();

            if (valueWords.Count == 0)
                return ExtractedField<DateOnly>.Missing(locator);

            // Filter out: single-digit footnote markers and day-name tokens ending in comma.
            // Keep only tokens that look like part of a date.
            var dateTokens = valueWords
                .Select(x => x.Text)
                .Where(t => !IsSingleDigit(t))               // skip footnote "1", "2", etc.
                .Where(t => !t.EndsWith(",", StringComparison.Ordinal))  // skip "lunes,"
                .ToList();

            // Try concatenated (e.g. "25-ago-2025") or space-joined.
            var dateStr = string.Concat(dateTokens).Trim();
            if (TryParseSpanishDate(dateStr, out var date))
                return ExtractedField<DateOnly>.Found(date, BoundingBoxOf(valueWords, 1));

            dateStr = string.Join(" ", dateTokens).Trim();
            if (TryParseSpanishDate(dateStr, out date))
                return ExtractedField<DateOnly>.Found(date, BoundingBoxOf(valueWords, 1));

            return ExtractedField<DateOnly>.Missing(locator);
        }

        return ExtractedField<DateOnly>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Pago para no generar intereses
    // -----------------------------------------------------------------------

    private static ExtractedField<decimal> ExtractPagoParaNoGenerarIntereses(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands)
    {
        // "Pago para no generar intereses 2 $32,446.69"
        // The "2" is a footnote marker — skip it; find the amount token.
        for (var i = 0; i + 4 < sorted.Count; i++)
        {
            if (!string.Equals(sorted[i].Text, "Pago", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(sorted[i + 1].Text, "para", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(sorted[i + 2].Text, "no", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(sorted[i + 3].Text, "generar", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(sorted[i + 4].Text, "intereses", StringComparison.OrdinalIgnoreCase))
                continue;

            // This might be the lowercase "Pago para no generar intereses" (Y≈555)
            // or the uppercase "PAGO PARA NO GENERAR INTERESES" (Y≈280).
            // We want the lowercase one (left column, smaller font).
            // Guard: left column (X < 200).
            if (sorted[i].BoundingBox.Left > 200)
                continue;

            var bandY = sorted[i].BoundingBox.Bottom;
            var band = GetBand(bands, bandY);
            var locator = BoundingBoxOf(band, 1);

            // maxX=300 keeps us in the left column — avoids picking up right-column
            // values (e.g. CLABE "9876543210123") that merged into this band.
            return FindAmountInBand(band, locator, maxX: 300);
        }

        return ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Pago mínimo + compras y cargos diferidos a meses
    // -----------------------------------------------------------------------

    private static ExtractedField<decimal> ExtractPagoMinimoMasMeses(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands)
    {
        // "Pago mínimo + compras y cargos" at Y≈544.9 followed by
        // "diferidos a meses: 3 $3,145.39" at Y≈534.1 (word-wrap continuation).
        // The amount "$3,145.39" is on the FIRST band (Y≈544.9).
        for (var i = 0; i + 3 < sorted.Count; i++)
        {
            if (!string.Equals(sorted[i].Text, "Pago", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(sorted[i + 1].Text, "mínimo", StringComparison.OrdinalIgnoreCase))
                continue;
            if (sorted[i + 2].Text != "+")
                continue;
            if (!string.Equals(sorted[i + 3].Text, "compras", StringComparison.OrdinalIgnoreCase))
                continue;

            var bandY = sorted[i].BoundingBox.Bottom;
            var band = GetBand(bands, bandY);
            var locator = BoundingBoxOf(band, 1);

            return FindAmountInBand(band, locator, maxX: 300);
        }

        return ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Pago mínimo (alone)
    // -----------------------------------------------------------------------

    private static ExtractedField<decimal> ExtractPagoMinimo(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands)
    {
        // "Pago mínimo: 4 $2,160.00" at Y≈523.3
        // "mínimo:" ends with a colon — use that to distinguish from "Pago mínimo +" line.
        for (var i = 0; i + 1 < sorted.Count; i++)
        {
            if (!string.Equals(sorted[i].Text, "Pago", StringComparison.OrdinalIgnoreCase))
                continue;

            // Check next token is "mínimo:" (with colon) OR "mínimo" where next+1 is NOT "+"
            var nextText = sorted[i + 1].Text;
            bool isMinimoLine = string.Equals(nextText, "mínimo:", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(nextText, "mínimo", StringComparison.OrdinalIgnoreCase)
                    && (i + 2 >= sorted.Count || sorted[i + 2].Text != "+"));

            if (!isMinimoLine)
                continue;

            var bandY = sorted[i].BoundingBox.Bottom;
            var band = GetBand(bands, bandY);

            // Make sure this band doesn't have "+" (if it does, it's the longer label).
            if (band.Any(x => x.Text == "+"))
                continue;

            var locator = BoundingBoxOf(band, 1);
            return FindAmountInBand(band, locator, maxX: 300);
        }

        return ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // TASA and CAT
    // -----------------------------------------------------------------------

    private static (ExtractedField<decimal> tasa, ExtractedField<decimal> cat)
        ExtractTasaAndCat(List<Word> sorted, Dictionary<double, List<Word>> bands)
    {
        // Layout (from fixture calibration):
        //   Y≈285.8: "CAT" (left label)
        //   Y≈290.6: "TASA DE INTERES ANUAL" (right label, actually at higher Y)
        //   Y≈281.0: "ORDINARIA FIJA" (continuation)
        //   Y≈264.0: "28.86% sin IVA  27.36%" — CAT value (28.86%), TASA ORDINARIA value (27.36%)
        //
        // Strategy: find the band at Y≈264 containing two percentage tokens.
        //   First token = CAT, second token = TASA ORDINARIA FIJA.

        // Find the "TASA" label in the lower section (Y < 350).
        double? tasaLabelY = null;
        double? catLabelY = null;

        foreach (var w in sorted)
        {
            if (w.BoundingBox.Bottom > 350)
                continue;  // only look in lower section

            if (string.Equals(w.Text, "TASA", StringComparison.OrdinalIgnoreCase) && tasaLabelY is null)
                tasaLabelY = w.BoundingBox.Bottom;

            if (string.Equals(w.Text, "CAT", StringComparison.OrdinalIgnoreCase) && catLabelY is null)
                catLabelY = w.BoundingBox.Bottom;

            if (tasaLabelY.HasValue && catLabelY.HasValue)
                break;
        }

        if (tasaLabelY is null && catLabelY is null)
        {
            return (
                ExtractedField<decimal>.Missing(FieldLocator.PageHint(1)),
                ExtractedField<decimal>.Missing(FieldLocator.PageHint(1)));
        }

        // Find the percent-value band below the labels (typically 10–30 pt below).
        // The value band has percent tokens.
        var refY = (tasaLabelY ?? catLabelY)!.Value;

        // Search bands below refY for percent tokens.
        var percentBands = bands
            .Where(kv => kv.Key < refY && kv.Key > refY - 60)  // within 60 pt below
            .OrderByDescending(kv => kv.Key)  // closest below first
            .ToList();

        foreach (var (_, bandWords) in percentBands)
        {
            var pctTokens = bandWords
                .Where(x => PercentPattern.IsMatch(x.Text))
                .OrderBy(x => x.BoundingBox.Left)
                .ToList();

            if (pctTokens.Count == 0)
                continue;

            // CAT = first percent token, TASA ORDINARIA = second (if present).
            var catLocator = BoundingBoxOf([pctTokens[0]], 1);
            var catPct = ParsePercent(pctTokens[0].Text);
            var catField = catPct.HasValue
                ? ExtractedField<decimal>.Found(catPct.Value, catLocator)
                : ExtractedField<decimal>.Missing(catLocator);

            ExtractedField<decimal> tasaField;
            if (pctTokens.Count >= 2)
            {
                var tasaLocator = BoundingBoxOf([pctTokens[1]], 1);
                var tasaPct = ParsePercent(pctTokens[1].Text);
                tasaField = tasaPct.HasValue
                    ? ExtractedField<decimal>.Found(tasaPct.Value, tasaLocator)
                    : ExtractedField<decimal>.Missing(tasaLocator);
            }
            else
            {
                tasaField = ExtractedField<decimal>.Missing(
                    catLocator with { Left = null, Bottom = null, Width = null, Height = null });
            }

            return (tasaField, catField);
        }

        return (
            ExtractedField<decimal>.Missing(FieldLocator.PageHint(1)),
            ExtractedField<decimal>.Missing(FieldLocator.PageHint(1)));
    }

    // -----------------------------------------------------------------------
    // Saldo Deudor Total
    // -----------------------------------------------------------------------

    private static ExtractedField<decimal> ExtractSaldoDeudorTotal(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands)
    {
        // "Saldo deudor total: 11 $ 52,387.85" at Y≈161
        // The "$" and amount may be separate tokens: "$" then "52,387.85"
        for (var i = 0; i + 1 < sorted.Count; i++)
        {
            if (!string.Equals(sorted[i].Text, "Saldo", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(sorted[i + 1].Text, "deudor", StringComparison.OrdinalIgnoreCase))
                continue;

            var bandY = sorted[i].BoundingBox.Bottom;
            var band = GetBand(bands, bandY);
            var locator = BoundingBoxOf(band, 1);

            return FindAmountInBandSplitDollar(band, locator);
        }

        return ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Crédito Disponible
    // -----------------------------------------------------------------------

    private static ExtractedField<decimal> ExtractCreditoDisponible(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands)
    {
        // "Crédito disponible: $ 47,612.15" at Y≈139
        // Note: there are multiple "Crédito disponible" lines (efectivo, transferencia).
        // We want the FIRST one (highest Y = top one).
        for (var i = 0; i + 1 < sorted.Count; i++)
        {
            if (!string.Equals(sorted[i].Text, "Crédito", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(sorted[i + 1].Text, "disponible:", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(sorted[i + 1].Text, "disponible", StringComparison.OrdinalIgnoreCase))
                continue;

            var bandY = sorted[i].BoundingBox.Bottom;
            var band = GetBand(bands, bandY);
            var locator = BoundingBoxOf(band, 1);

            var result = FindAmountInBandSplitDollar(band, locator);
            if (result.Status == ExtractionStatus.Extracted)
                return result;
            // If not found on this band, try next occurrence.
        }

        return ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Spanish date parsing
    // -----------------------------------------------------------------------

    /// <summary>
    /// Parses Spanish-format dates:
    /// <list type="bullet">
    ///   <item><description>"d-mmm-yyyy" / "dd-mmm-yyyy": e.g. "5-jul-2025"</description></item>
    ///   <item><description>"dd de mmm yyyy": e.g. "04 de ago 2025"</description></item>
    /// </list>
    /// Day-name prefixes ("lunes,") must be stripped by callers.
    /// </summary>
    private static bool TryParseSpanishDate(string? raw, out DateOnly result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();

        // Format 1: "d-mmm-yyyy"
        var m = DateDashFormat.Match(s);
        if (m.Success && TryGetMonth(m.Groups[2].Value, out var mo1))
        {
            if (int.TryParse(m.Groups[1].Value, out var d1)
                && int.TryParse(m.Groups[3].Value, out var y1))
            {
                result = new DateOnly(y1, mo1, d1);
                return true;
            }
        }

        // Format 2: "dd de mmm yyyy" (must have exactly 4 tokens)
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4
            && string.Equals(parts[1], "de", StringComparison.OrdinalIgnoreCase)
            && TryGetMonth(parts[2], out var mo2)
            && int.TryParse(parts[0], out var d2)
            && int.TryParse(parts[3], out var y2))
        {
            result = new DateOnly(y2, mo2, d2);
            return true;
        }

        return false;
    }

    private static bool TryGetMonth(string abbr, out int month)
        => SpanishMonthMap.TryGetValue(abbr, out month);

    // -----------------------------------------------------------------------
    // Amount parsing helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Finds and parses the rightmost amount token on a band.
    /// Amount tokens match <see cref="AmountPattern"/> (e.g. "$32,446.69", "2,160.00").
    /// Footnote markers (single digits not matching amount pattern) are skipped.
    /// </summary>
    /// <param name="band">Words on the target band, sorted left-to-right.</param>
    /// <param name="locator">Fallback locator for Missing results.</param>
    /// <param name="maxX">
    /// Optional upper bound on <c>BoundingBox.Left</c> for candidate amount tokens.
    /// Pass a value (e.g. 300) to restrict to the left column and avoid picking up
    /// right-column numeric text (CLABE, account numbers) that landed on the same band
    /// due to band-merging tolerance.  Defaults to <c>double.MaxValue</c> (no filter).
    /// </param>
    private static ExtractedField<decimal> FindAmountInBand(
        List<Word> band,
        FieldLocator locator,
        double maxX = double.MaxValue)
    {
        // Find rightmost token matching AmountPattern within the X constraint.
        var amountWord = band
            .Where(x => x.BoundingBox.Left <= maxX && AmountPattern.IsMatch(x.Text))
            .OrderByDescending(x => x.BoundingBox.Left)
            .FirstOrDefault();

        if (amountWord is null)
            return ExtractedField<decimal>.Missing(locator);

        return ParseAmountToken(amountWord.Text, BoundingBoxOf([amountWord], 1));
    }

    /// <summary>
    /// Finds and parses an amount where the "$" sign and digits may be separate tokens
    /// (e.g. "$ 52,387.85" → two tokens "$" and "52,387.85").
    /// </summary>
    private static ExtractedField<decimal> FindAmountInBandSplitDollar(
        List<Word> band,
        FieldLocator locator)
    {
        // Try combined amount tokens first.
        var combined = FindAmountInBand(band, locator);
        if (combined.Status == ExtractionStatus.Extracted)
            return combined;

        // Look for a bare "$" followed by a numeric token.
        var dollarIdx = band.FindIndex(x => x.Text == "$");
        if (dollarIdx >= 0 && dollarIdx + 1 < band.Count)
        {
            var numToken = band[dollarIdx + 1];
            // Skip single-digit footnote markers.
            if (!IsSingleDigit(numToken.Text))
            {
                var numericText = numToken.Text.Replace(",", string.Empty, StringComparison.Ordinal);
                if (decimal.TryParse(numericText, NumberStyles.Number, CultureInfo.InvariantCulture, out var val))
                    return ExtractedField<decimal>.Found(val, BoundingBoxOf([band[dollarIdx], numToken], 1));
            }

            // If there's a footnote marker between "$" and the number, try skipping it.
            if (dollarIdx + 2 < band.Count && IsSingleDigit(band[dollarIdx + 1].Text))
            {
                var numToken2 = band[dollarIdx + 2];
                var numericText = numToken2.Text.Replace(",", string.Empty, StringComparison.Ordinal);
                if (decimal.TryParse(numericText, NumberStyles.Number, CultureInfo.InvariantCulture, out var val2))
                    return ExtractedField<decimal>.Found(val2, BoundingBoxOf([band[dollarIdx], numToken2], 1));
            }
        }

        return ExtractedField<decimal>.Missing(locator);
    }

    private static ExtractedField<decimal> ParseAmountToken(string token, FieldLocator locator)
    {
        var m = AmountPattern.Match(token);
        if (!m.Success)
            return ExtractedField<decimal>.Missing(locator);

        var normalized = m.Groups[1].Value.Replace(",", string.Empty, StringComparison.Ordinal);
        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var val))
            return ExtractedField<decimal>.Found(val, locator);

        return ExtractedField<decimal>.Missing(locator);
    }

    private static decimal? ParsePercent(string token)
    {
        var m = PercentPattern.Match(token);
        if (!m.Success)
            return null;

        if (decimal.TryParse(m.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var pct))
            return pct / 100m;

        return null;
    }

    // -----------------------------------------------------------------------
    // Client name extraction (positional — right column, top of header)
    // -----------------------------------------------------------------------

    private static ExtractedField<ExtractedClientName> ExtractClientName(List<Word> headerWords)
    {
        var nameWords = headerWords
            .Where(w => w.BoundingBox.Left >= ClientNameXMin
                     && w.BoundingBox.Bottom >= ClientNameYMin)
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ToList();

        if (nameWords.Count == 0)
            return ExtractedField<ExtractedClientName>.Missing(FieldLocator.PageHint(1));

        var topY = nameWords[0].BoundingBox.Bottom;
        var nameBandWords = nameWords
            .Where(w => Math.Abs(w.BoundingBox.Bottom - topY) <= YBandTolerance)
            .OrderBy(w => w.BoundingBox.Left)
            .ToList();

        var fullName = string.Join(" ", nameBandWords.Select(w => w.Text)).Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            return ExtractedField<ExtractedClientName>.Missing(FieldLocator.PageHint(1));

        var tokens = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string? firstNames = null;
        string? lastNames = null;
        if (tokens.Length >= 3)
        {
            firstNames = tokens[0];
            lastNames = string.Join(" ", tokens.Skip(1));
        }
        else if (tokens.Length == 2)
        {
            firstNames = tokens[0];
            lastNames = tokens[1];
        }
        else
        {
            firstNames = tokens[0];
        }

        var locator = BoundingBoxOf(nameBandWords, 1);
        return ExtractedField<ExtractedClientName>.Found(
            new ExtractedClientName(fullName, firstNames, lastNames), locator);
    }

    // -----------------------------------------------------------------------
    // Address extraction (positional — right column, below client name)
    // -----------------------------------------------------------------------

    private static ExtractedField<ExtractedAddress> ExtractAddress(List<Word> headerWords)
    {
        var addressWords = headerWords
            .Where(w => w.BoundingBox.Left >= ClientNameXMin
                     && w.BoundingBox.Bottom >= HeaderYMin
                     && w.BoundingBox.Bottom <= AddressYMax)
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        if (addressWords.Count == 0)
            return ExtractedField<ExtractedAddress>.Missing(FieldLocator.PageHint(1));

        var addressBands = GroupIntoBands(addressWords);
        var lines = addressBands
            .OrderByDescending(b => b.Key)
            .Select(b => string.Join(" ", b.Value.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)))
            .ToList();

        var rawAddress = string.Join(" ", lines).Trim();
        if (string.IsNullOrWhiteSpace(rawAddress))
            return ExtractedField<ExtractedAddress>.Missing(FieldLocator.PageHint(1));

        var allTokens = rawAddress.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string? postalCode = null;
        string? state = null;
        int postalCodeIdx = -1;

        for (var i = 0; i < allTokens.Length; i++)
        {
            if (PostalCodePattern.IsMatch(allTokens[i]))
            {
                postalCode = allTokens[i];
                postalCodeIdx = i;
                if (i + 1 < allTokens.Length)
                {
                    var candidate = allTokens[i + 1].TrimEnd(',');
                    if (!candidate.StartsWith("C.R.", StringComparison.OrdinalIgnoreCase))
                        state = candidate;
                }

                break;
            }
        }

        var topBandY = addressBands.Keys.Max();
        var firstLineBand = addressBands[topBandY];
        var streetAndNumber = string.Join(" ",
            firstLineBand.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)).Trim();

        string? neighborhood = null;
        if (postalCodeIdx > 0)
        {
            var neighborhoodTokens = allTokens
                .Skip(streetAndNumber.Split(' ').Length)
                .Take(postalCodeIdx - streetAndNumber.Split(' ').Length)
                .ToArray();
            if (neighborhoodTokens.Length > 0)
                neighborhood = string.Join(" ", neighborhoodTokens).Trim();
        }

        var locator = BoundingBoxOf(addressWords, 1);
        return ExtractedField<ExtractedAddress>.Found(
            new ExtractedAddress(rawAddress, streetAndNumber, neighborhood, postalCode, state), locator);
    }

    // -----------------------------------------------------------------------
    // Generic labeled-field extraction (header zone, right column)
    // -----------------------------------------------------------------------

    private static ExtractedField<string> ExtractLabeledField(
        Dictionary<double, List<Word>> bands,
        string[] labelTokens)
    {
        foreach (var (bandY, bandWords) in bands.OrderByDescending(b => b.Key))
        {
            if (bandY < HeaderYMin || bandY > HeaderYMax)
                continue;

            var sorted = bandWords.OrderBy(w => w.BoundingBox.Left).ToList();

            for (var i = 0; i <= sorted.Count - labelTokens.Length; i++)
            {
                if (!MatchesLabel(sorted, i, labelTokens))
                    continue;

                var labelRight = sorted[i + labelTokens.Length - 1].BoundingBox.Right;
                var locator = BoundingBoxOf(sorted.Skip(i).Take(labelTokens.Length).ToList(), 1);

                var valueWords = sorted
                    .Where(w => w.BoundingBox.Left > labelRight
                             && w.BoundingBox.Left >= ValueColumnXMin - 10)
                    .OrderBy(w => w.BoundingBox.Left)
                    .ToList();

                if (valueWords.Count == 0)
                    return ExtractedField<string>.Missing(locator);

                var value = string.Join(" ", valueWords.Select(w => w.Text)).Trim();
                return ExtractedField<string>.Found(value, BoundingBoxOf(valueWords, 1));
            }
        }

        return ExtractedField<string>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Validated field extractors (card, CLABE, RFC)
    // -----------------------------------------------------------------------

    private static ExtractedField<string> ExtractAndValidateCardNumber(Dictionary<double, List<Word>> bands)
    {
        var raw = ExtractLabeledField(bands, ["Número", "de", "Tarjeta"]);
        if (raw.Status == ExtractionStatus.NotExtracted) return raw;

        var digits = DigitsOnly.Replace(raw.Value!, string.Empty);
        return digits.Length == 16
            ? ExtractedField<string>.Found(digits, raw.Locator)
            : ExtractedField<string>.InvalidFormat(string.IsNullOrEmpty(digits) ? raw.Value! : digits, raw.Locator);
    }

    private static ExtractedField<string> ExtractAndValidateClabe(Dictionary<double, List<Word>> bands)
    {
        var raw = ExtractLabeledField(bands, ["CLABE", "Interbancaria"]);
        if (raw.Status == ExtractionStatus.NotExtracted) return raw;

        var digits = DigitsOnly.Replace(raw.Value!, string.Empty);
        return digits.Length == 18
            ? ExtractedField<string>.Found(digits, raw.Locator)
            : ExtractedField<string>.InvalidFormat(string.IsNullOrEmpty(digits) ? raw.Value! : digits, raw.Locator);
    }

    private static ExtractedField<string> ExtractAndValidateRfc(Dictionary<double, List<Word>> bands)
    {
        var raw = ExtractLabeledField(bands, ["RFC"]);
        if (raw.Status == ExtractionStatus.NotExtracted) return raw;

        var value = raw.Value!.Trim().ToUpperInvariant();
        return RfcPattern.IsMatch(value)
            ? ExtractedField<string>.Found(value, raw.Locator)
            : ExtractedField<string>.InvalidFormat(value, raw.Locator);
    }

    // -----------------------------------------------------------------------
    // Band grouping utilities
    // -----------------------------------------------------------------------

    private static Dictionary<double, List<Word>> GroupIntoBands(List<Word> words)
    {
        var result = new Dictionary<double, List<Word>>();

        foreach (var word in words)
        {
            var y = word.BoundingBox.Bottom;
            var matched = false;

            foreach (var key in result.Keys)
            {
                if (Math.Abs(key - y) <= YBandTolerance)
                {
                    result[key].Add(word);
                    matched = true;
                    break;
                }
            }

            if (!matched)
                result[y] = [word];
        }

        return result;
    }

    /// <summary>Returns the band closest to <paramref name="y"/>, sorted left-to-right.</summary>
    private static List<Word> GetBand(Dictionary<double, List<Word>> bands, double y)
    {
        return bands
            .Where(kv => Math.Abs(kv.Key - y) <= YBandTolerance)
            .SelectMany(kv => kv.Value)
            .OrderBy(w => w.BoundingBox.Left)
            .ToList();
    }

    private static string BandText(List<Word> band) =>
        string.Join(" ", band.Select(w => w.Text)).Trim();

    // -----------------------------------------------------------------------
    // Label matching
    // -----------------------------------------------------------------------

    private static bool MatchesLabel(List<Word> sortedBandWords, int startIndex, string[] labelTokens)
    {
        for (var i = 0; i < labelTokens.Length; i++)
        {
            if (!string.Equals(sortedBandWords[startIndex + i].Text, labelTokens[i],
                    StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static bool IsSingleDigit(string s) =>
        s.Length == 1 && char.IsDigit(s[0]);

    private static FieldLocator BoundingBoxOf(IReadOnlyList<Word> words, int pageNumber)
    {
        if (words.Count == 0)
            return FieldLocator.PageHint(pageNumber);

        var left = words.Min(w => w.BoundingBox.Left);
        var bottom = words.Min(w => w.BoundingBox.Bottom);
        var right = words.Max(w => w.BoundingBox.Right);
        var top = words.Max(w => w.BoundingBox.Top);

        return new FieldLocator(pageNumber, left, bottom, right - left, top - bottom);
    }
}
