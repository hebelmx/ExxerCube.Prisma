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

            // ---- DESGLOSE DE MOVIMIENTOS DEL PERIODO — Story 4.4 ----------
            // Scan all pages for the DESGLOSE section header, then reconstruct
            // transaction rows using Y-band grouping + X-column assignment.
            var (movements, movementsStatus, totalCargos, totalAbonos) = ExtractMovements(doc);

            // Rebuild PeriodSummary with DESGLOSE totals (extracted from DESGLOSE pages,
            // not page 1, so they are injected here rather than inside ExtractPeriodSummary).
            var periodSummaryWithTotals = new PeriodSummary(
                product: periodSummary.Product,
                periodStart: periodSummary.PeriodStart,
                periodCutDate: periodSummary.PeriodCutDate,
                paymentDueDate: periodSummary.PaymentDueDate,
                dayCountPrinted: periodSummary.DayCountPrinted,
                dayCount: periodSummary.DayCount,
                pagoParaNoGenerarIntereses: periodSummary.PagoParaNoGenerarIntereses,
                pagoMinimo: periodSummary.PagoMinimo,
                pagoMinimoMasMeses: periodSummary.PagoMinimoMasMeses,
                tasa: periodSummary.Tasa,
                cat: periodSummary.Cat,
                saldoDeudorTotal: periodSummary.SaldoDeudorTotal,
                creditoDisponible: periodSummary.CreditoDisponible,
                adeudoPeriodoAnterior: periodSummary.AdeudoPeriodoAnterior,
                cargosRegularesNoMeses: periodSummary.CargosRegularesNoMeses,
                cargosComprasAMesesCapital: periodSummary.CargosComprasAMesesCapital,
                montoIntereses: periodSummary.MontoIntereses,
                montoComisiones: periodSummary.MontoComisiones,
                ivaInteresesYComisiones: periodSummary.IvaInteresesYComisiones,
                pagosYAbonos: periodSummary.PagosYAbonos,
                saldoCargosRegulares: periodSummary.SaldoCargosRegulares,
                saldoCargosAMeses: periodSummary.SaldoCargosAMeses,
                totalCargos: totalCargos,
                totalAbonos: totalAbonos);

            var model = new StatementModel(
                clientName: clientName,
                address: address,
                branchNumber: branchNumber,
                cardNumber: cardNumber,
                clabe: clabe,
                clientNumber: clientNumber,
                rfc: rfc)
            {
                PeriodSummary = periodSummaryWithTotals,
                Movements = movements,
                MovementsStatus = movementsStatus,
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

        // ---- RESUMEN DE CARGOS Y ABONOS DEL PERIODO (Story 4.2) ---------
        // Right-side block at Y ≈ 292–357.
        var adeudoPeriodoAnterior = ExtractResumenField(sorted, bands,
            ["Adeudo", "del", "periodo", "anterior"]);
        var cargosRegularesNoMeses = ExtractResumenField(sorted, bands,
            ["Cargos", "regulares", "(no"]);
        var cargosComprasAMesesCapital = ExtractResumenField(sorted, bands,
            ["Cargos", "compras", "a", "meses", "(capital)"]);
        var montoIntereses = ExtractResumenField(sorted, bands,
            ["Monto", "de", "Intereses"]);
        var montoComisiones = ExtractResumenField(sorted, bands,
            ["Monto", "de", "comisiones"]);
        var ivaInteresesYComisiones = ExtractResumenField(sorted, bands,
            ["IVA", "de", "Intereses"]);
        var pagosYAbonos = ExtractResumenField(sorted, bands,
            ["Pagos", "y", "abonos"]);

        // ---- NIVEL DE USO DE TU TARJETA (Story 4.2) ---------------------
        // Right-side block at Y ≈ 171–183.
        var saldoCargosRegulares = ExtractNivelDeUsoField(sorted, bands,
            ["Saldo", "cargos", "regulares:"]);
        var saldoCargosAMeses = ExtractNivelDeUsoField(sorted, bands,
            ["Saldo", "cargos", "a", "meses:"]);

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
            creditoDisponible: creditoDisponible,
            adeudoPeriodoAnterior: adeudoPeriodoAnterior,
            cargosRegularesNoMeses: cargosRegularesNoMeses,
            cargosComprasAMesesCapital: cargosComprasAMesesCapital,
            montoIntereses: montoIntereses,
            montoComisiones: montoComisiones,
            ivaInteresesYComisiones: ivaInteresesYComisiones,
            pagosYAbonos: pagosYAbonos,
            saldoCargosRegulares: saldoCargosRegulares,
            saldoCargosAMeses: saldoCargosAMeses);
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
    // RESUMEN DE CARGOS Y ABONOS DEL PERIODO extraction (Story 4.2)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Extracts an amount from the right-side "RESUMEN DE CARGOS Y ABONOS DEL PERIODO" block
    /// by matching a label prefix (case-insensitive) on a band whose anchor word has X ≥ 283
    /// (right column).  The amount is to the right of the label, preceded by an optional
    /// sign token ("+", "-", "=") and an optional footnote-digit, followed by a "$" + digits.
    /// </summary>
    /// <param name="sorted">All page-1 words sorted top-to-bottom, left-to-right.</param>
    /// <param name="bands">Band dictionary for same-line extraction.</param>
    /// <param name="labelTokens">
    /// Leading token sequence that identifies the RESUMEN row
    /// (e.g. <c>["Adeudo", "del", "periodo", "anterior"]</c>).
    /// </param>
    /// <returns>
    /// An <see cref="ExtractedField{T}"/> containing the parsed amount, or
    /// <see cref="ExtractedField{T}.Missing"/> if the label is not found or the amount
    /// cannot be parsed.
    /// </returns>
    private static ExtractedField<decimal> ExtractResumenField(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands,
        string[] labelTokens)
    {
        // The RESUMEN block is in the right column (X ≥ ~283).
        // We iterate sorted words looking for the first token of the label in the right column.
        for (var i = 0; i + labelTokens.Length - 1 < sorted.Count; i++)
        {
            if (sorted[i].BoundingBox.Left < 280)
                continue;   // left-column guard — RESUMEN is on the right

            if (!string.Equals(sorted[i].Text, labelTokens[0], StringComparison.OrdinalIgnoreCase))
                continue;

            // Verify the rest of the label tokens appear in sorted order on the same band.
            var bandY = sorted[i].BoundingBox.Bottom;
            var band = GetBand(bands, bandY);
            var bandSorted = band.OrderBy(x => x.BoundingBox.Left).ToList();

            // Locate the first label token in the band.
            var labelStart = bandSorted.FindIndex(x =>
                string.Equals(x.Text, labelTokens[0], StringComparison.OrdinalIgnoreCase)
                && x.BoundingBox.Left >= 280);

            if (labelStart < 0)
                continue;

            // Verify subsequent label tokens match consecutively.
            var allMatch = true;
            for (var t = 1; t < labelTokens.Length; t++)
            {
                if (labelStart + t >= bandSorted.Count
                    || !string.Equals(bandSorted[labelStart + t].Text, labelTokens[t],
                        StringComparison.OrdinalIgnoreCase))
                {
                    allMatch = false;
                    break;
                }
            }

            if (!allMatch)
                continue;

            var locator = BoundingBoxOf(band, 1);

            // The amount in the RESUMEN block appears as a split "$ 32,446.69" pair
            // at the far right (X ≈ 435-480).  Footnote markers and sign tokens precede it.
            return FindAmountInBandSplitDollar(band, locator);
        }

        return ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
    }

    /// <summary>
    /// Extracts an amount from the "NIVEL DE USO DE TU TARJETA" block.
    /// These rows appear at Y ≈ 172–183 in the right column and use the same
    /// split-dollar pattern: "$ 32,446.69".
    /// </summary>
    /// <param name="sorted">All page-1 words sorted top-to-bottom, left-to-right.</param>
    /// <param name="bands">Band dictionary for same-line extraction.</param>
    /// <param name="labelTokens">
    /// Leading token sequence (e.g. <c>["Saldo", "cargos", "regulares:"]</c>).
    /// </param>
    private static ExtractedField<decimal> ExtractNivelDeUsoField(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands,
        string[] labelTokens)
    {
        // NIVEL-DE-USO rows are in the right column (X ≥ ~283) at Y ≈ 171–183.
        for (var i = 0; i + labelTokens.Length - 1 < sorted.Count; i++)
        {
            if (sorted[i].BoundingBox.Left < 280)
                continue;

            if (!string.Equals(sorted[i].Text, labelTokens[0], StringComparison.OrdinalIgnoreCase))
                continue;

            var bandY = sorted[i].BoundingBox.Bottom;
            var band = GetBand(bands, bandY);
            var bandSorted = band.OrderBy(x => x.BoundingBox.Left).ToList();

            var labelStart = bandSorted.FindIndex(x =>
                string.Equals(x.Text, labelTokens[0], StringComparison.OrdinalIgnoreCase)
                && x.BoundingBox.Left >= 280);

            if (labelStart < 0)
                continue;

            var allMatch = true;
            for (var t = 1; t < labelTokens.Length; t++)
            {
                if (labelStart + t >= bandSorted.Count
                    || !string.Equals(bandSorted[labelStart + t].Text, labelTokens[t],
                        StringComparison.OrdinalIgnoreCase))
                {
                    allMatch = false;
                    break;
                }
            }

            if (!allMatch)
                continue;

            var locator = BoundingBoxOf(band, 1);
            return FindAmountInBandSplitDollar(band, locator);
        }

        return ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // DESGLOSE DE MOVIMIENTOS DEL PERIODO extraction (Story 4.4)
    // -----------------------------------------------------------------------

    // Column X-range constants (empirically measured from Dummie VEC fixtures).
    // PDF-point coordinates; origin bottom-left. Page width ≈ 540 pt.
    //
    //   Fecha de la operación  — words whose Left ≤ 95
    //   Fecha de cargo         — words whose Left is 96–157
    //   Descripción            — words whose Left is 158–422
    //   Monto sign (+/−)       — words whose Left is 423–435
    //   Monto amount           — words whose Left is 436–530
    //
    // The DESGLOSE section header line contains the word "DESGLOSE" at Y≈663
    // on each table page.  Total/summary rows ("Total cargos", "Total abonos")
    // appear after the last data row with text starting at X≈346 and no sign token.

    private const double DesgloseOperationDateXMax = 95.0;
    private const double DeskglosechargeDateXMin = 96.0;
    private const double DesgloseChargeDateXMax = 157.0;
    private const double DesgloseDescriptionXMin = 158.0;
    private const double DesgloseDescriptionXMax = 422.0;
    private const double DesgloseSignXMin = 423.0;
    private const double DesgloseSignXMax = 438.0;
    private const double DesgloseAmountXMin = 436.0;

    // Y-band tolerance tighter than the header (14 pt row spacing; 4 pt avoids merging adjacent rows).
    private const double DesgloseBandTolerance = 4.0;

    // Sign tokens as they appear in the PDF.
    private const string SignChargeToken = "+";
    // Minus sign can appear as ASCII hyphen-minus or Unicode minus sign.
    private const string SignCreditAscii = "-";
    private const string SignCreditUnicode = "−";

    /// <summary>
    /// Amount with sign token, e.g. "+ $329.00", "- $6,523.00".
    /// Pattern: optional "$", digit groups with commas, optional decimal.
    /// </summary>
    private static readonly Regex DesgloseAmountPattern = new(
        @"^\$?([\d,]+(?:\.\d+)?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Loose date pattern used to identify operation/charge-date tokens in the table.
    /// Accepts truncated years (e.g. "07-jul-202") as well as full "dd-mmm-yyyy".
    /// </summary>
    private static readonly Regex DesgloseDatePattern = new(
        @"^\d{1,2}-[a-záéíóúñü]+-\d{2,4}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extracts all movement rows from the DESGLOSE DE MOVIMIENTOS DEL PERIODO table
    /// across all pages of the document, plus the printed "Total cargos" and "Total abonos"
    /// summary rows at the bottom of the last table page.
    /// </summary>
    /// <param name="doc">The open <see cref="PdfDocument"/>.</param>
    /// <returns>
    /// A tuple of the parsed movement list, the extraction status, and the two printed
    /// DESGLOSE totals (TotalCargos / TotalAbonos).
    /// The movement list is in top-to-bottom, page-ascending order.
    /// Never throws — individual row parse failures are silently skipped.
    /// </returns>
    private (IReadOnlyList<StatementMovement> movements,
             MovementsExtractionStatus status,
             ExtractedField<decimal> totalCargos,
             ExtractedField<decimal> totalAbonos)
        ExtractMovements(PdfDocument doc)
    {
        var movements = new List<StatementMovement>();
        var sectionFound = false;
        ExtractedField<decimal>? totalCargos = null;
        ExtractedField<decimal>? totalAbonos = null;

        for (var pageIndex = 1; pageIndex <= doc.NumberOfPages; pageIndex++)
        {
            var page = doc.GetPage(pageIndex);
            var words = page.GetWords().ToList();

            // Check whether this page contains the DESGLOSE section header.
            // The header line has "DESGLOSE" at Y≈663 (empirically measured).
            var hasDesgloseHeader = words.Any(w =>
                string.Equals(w.Text, "DESGLOSE", StringComparison.OrdinalIgnoreCase));

            if (!hasDesgloseHeader)
                continue;

            sectionFound = true;

            // Group all words into Y-bands using the tighter DESGLOSE tolerance.
            var bands = GroupIntoBandsWithTolerance(words, DesgloseBandTolerance);

            // Sort bands top-to-bottom (highest Y = top of page in PDF coordinates).
            var sortedBands = bands
                .OrderByDescending(kv => kv.Key)
                .ToList();

            StatementMovement? lastMovement = null;

            foreach (var (bandY, bandWords) in sortedBands)
            {
                var row = TryParseMovementRow(bandWords, pageIndex);

                if (row is not null)
                {
                    movements.Add(row);
                    lastMovement = row;
                    continue;
                }

                // Check for FX-rate continuation row (TC1*/TC2*/U.S. DOLLAR):
                // these words have no date column and no sign token but belong to
                // the description of the preceding row. We fold them into the description
                // by tracking lastMovement. Because StatementMovement is a record (immutable),
                // we replace the last entry with an updated description.
                if (lastMovement is not null && IsFxContinuationRow(bandWords))
                {
                    var fxText = BuildDescription(bandWords,
                        DesgloseDescriptionXMin, DesgloseDescriptionXMax);
                    if (!string.IsNullOrWhiteSpace(fxText))
                    {
                        var updated = new StatementMovement(
                            lastMovement.OperationDate,
                            lastMovement.ChargeDate,
                            lastMovement.Description + " | " + fxText,
                            lastMovement.Amount,
                            lastMovement.Sign,
                            lastMovement.Locator);
                        movements[movements.Count - 1] = updated;
                        lastMovement = updated;
                    }
                    continue;
                }

                // Check for "Total cargos" / "Total abonos" summary rows.
                // These have description text starting at X≈346 and NO sign token.
                var totalResult = TryParseTotalRow(bandWords, pageIndex);
                if (totalResult.HasValue)
                {
                    if (totalResult.Value.isCharge)
                        totalCargos = totalResult.Value.amount;
                    else
                        totalAbonos = totalResult.Value.amount;
                }
                // Other non-data bands (column headers, section title, page footer) are skipped.
            }
        }

        var missingTotal = ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));

        if (!sectionFound)
            return ([], MovementsExtractionStatus.SectionNotFound, missingTotal, missingTotal);

        if (movements.Count == 0)
            return ([], MovementsExtractionStatus.NoRowsParsed, totalCargos ?? missingTotal, totalAbonos ?? missingTotal);

        _logger.LogInformation(
            "DESGLOSE extraction: {Count} movements parsed across {Pages} page(s).",
            movements.Count, doc.NumberOfPages);

        return (movements, MovementsExtractionStatus.Extracted,
                totalCargos ?? missingTotal, totalAbonos ?? missingTotal);
    }

    /// <summary>
    /// Attempts to parse a "Total cargos" or "Total abonos" summary row from a DESGLOSE band.
    /// </summary>
    /// <param name="bandWords">Words on the candidate band.</param>
    /// <param name="pageNumber">Page number for the locator.</param>
    /// <returns>
    /// A tuple of (isCharge, <see cref="ExtractedField{T}"/> amount) when the band matches
    /// a total row; <see langword="null"/> otherwise.
    /// </returns>
    private static (bool isCharge, ExtractedField<decimal> amount)?
        TryParseTotalRow(List<Word> bandWords, int pageNumber)
    {
        // A "Total" row has:
        //  - A "Total" token in the description column (X 158–422)
        //  - Followed by "cargos" or "abonos" in the same column
        //  - NO sign token (X 423–438)
        //  - An amount token (X ≥ 436)

        var hasSignToken = bandWords.Any(w =>
            w.BoundingBox.Left >= DesgloseSignXMin
            && w.BoundingBox.Left <= DesgloseSignXMax
            && IsSignToken(w.Text));

        if (hasSignToken)
            return null;

        var descWords = bandWords
            .Where(w => w.BoundingBox.Left >= DesgloseDescriptionXMin
                     && w.BoundingBox.Left <= DesgloseDescriptionXMax)
            .OrderBy(w => w.BoundingBox.Left)
            .ToList();

        if (descWords.Count < 2)
            return null;

        // Find "Total" token
        var totalIdx = descWords.FindIndex(w =>
            string.Equals(w.Text, "Total", StringComparison.OrdinalIgnoreCase));
        if (totalIdx < 0 || totalIdx + 1 >= descWords.Count)
            return null;

        var nextToken = descWords[totalIdx + 1].Text;
        bool? isCharge = null;

        if (string.Equals(nextToken, "cargos", StringComparison.OrdinalIgnoreCase))
            isCharge = true;
        else if (string.Equals(nextToken, "abonos", StringComparison.OrdinalIgnoreCase))
            isCharge = false;

        if (isCharge is null)
            return null;

        // Parse amount (X ≥ 436)
        var amountWords = bandWords
            .Where(w => w.BoundingBox.Left >= DesgloseAmountXMin)
            .OrderBy(w => w.BoundingBox.Left)
            .ToList();

        if (amountWords.Count == 0)
            return null;

        var locator = BoundingBoxOf(bandWords, pageNumber);

        foreach (var aw in amountWords)
        {
            var m = DesgloseAmountPattern.Match(aw.Text);
            if (!m.Success)
                continue;
            var normalized = m.Groups[1].Value.Replace(",", string.Empty, StringComparison.Ordinal);
            if (decimal.TryParse(normalized, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                return (isCharge.Value, ExtractedField<decimal>.Found(parsed, locator));
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to parse a Y-band as a DESGLOSE data row.
    /// Returns <see langword="null"/> for non-data bands (headers, footers, totals, FX rows).
    /// </summary>
    private static StatementMovement? TryParseMovementRow(List<Word> bandWords, int pageNumber)
    {
        // A data row must have:
        //   1. At least one date-like token in the operation-date column (X ≤ 95)
        //   2. A sign token (+/−) in the Monto column (X 423–438)
        var operationDateWords = bandWords
            .Where(w => w.BoundingBox.Left <= DesgloseOperationDateXMax
                     && DesgloseDatePattern.IsMatch(w.Text))
            .OrderBy(w => w.BoundingBox.Left)
            .ToList();

        var signWords = bandWords
            .Where(w => w.BoundingBox.Left >= DesgloseSignXMin
                     && w.BoundingBox.Left <= DesgloseSignXMax
                     && IsSignToken(w.Text))
            .ToList();

        if (operationDateWords.Count == 0 || signWords.Count == 0)
            return null;

        // ---- Operation date ---------------------------------------------------
        DateOnly? operationDate = null;
        var opDateToken = operationDateWords[0].Text;
        if (TryParseSpanishDate(opDateToken, out var opDate))
            operationDate = opDate;

        // ---- Charge date (X 96–157; year may be truncated to "dd-mmm-202") ---
        var chargeDateWords = bandWords
            .Where(w => w.BoundingBox.Left >= DeskglosechargeDateXMin
                     && w.BoundingBox.Left <= DesgloseChargeDateXMax
                     && DesgloseDatePattern.IsMatch(w.Text))
            .OrderBy(w => w.BoundingBox.Left)
            .ToList();

        DateOnly? chargeDate = null;
        if (chargeDateWords.Count > 0)
        {
            var cdToken = chargeDateWords[0].Text;
            if (TryParseSpanishDate(cdToken, out var cd))
            {
                chargeDate = cd;
            }
            else
            {
                // Attempt year-repair: "07-jul-202" → try appending "5" from context.
                // The operation date year is the safest context; fall back to current year.
                var repairedYear = operationDate?.Year ?? DateTimeOffset.UtcNow.Year;
                var repaired = RepairTruncatedDate(cdToken, repairedYear);
                if (repaired is not null && TryParseSpanishDate(repaired, out var repairedDate))
                    chargeDate = repairedDate;
            }
        }

        // ---- Description (X 158–422) -----------------------------------------
        var description = BuildDescription(bandWords, DesgloseDescriptionXMin, DesgloseDescriptionXMax);

        // ---- Sign -----------------------------------------------------------
        var signToken = signWords[0].Text;
        var sign = IsCreditToken(signToken) ? MovementSign.Credit : MovementSign.Charge;

        // ---- Amount (X ≥ 436) -----------------------------------------------
        var amountWords = bandWords
            .Where(w => w.BoundingBox.Left >= DesgloseAmountXMin)
            .OrderBy(w => w.BoundingBox.Left)
            .ToList();

        decimal amount = 0m;
        foreach (var aw in amountWords)
        {
            var m = DesgloseAmountPattern.Match(aw.Text);
            if (!m.Success)
                continue;
            var normalized = m.Groups[1].Value.Replace(",", string.Empty, StringComparison.Ordinal);
            if (decimal.TryParse(normalized, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                amount = parsed;
                break;
            }
        }

        // ---- Locator --------------------------------------------------------
        var locator = BoundingBoxOf(bandWords, pageNumber);

        return new StatementMovement(
            operationDate,
            chargeDate,
            description,
            amount,
            sign,
            locator);
    }

    /// <summary>
    /// Builds a description string from words within the description column X-range.
    /// Words are sorted left-to-right and joined with a single space.
    /// </summary>
    private static string BuildDescription(List<Word> bandWords, double xMin, double xMax)
    {
        var descWords = bandWords
            .Where(w => w.BoundingBox.Left >= xMin && w.BoundingBox.Left <= xMax)
            .OrderBy(w => w.BoundingBox.Left)
            .Select(w => w.Text)
            .ToList();
        return string.Join(" ", descWords).Trim();
    }

    /// <summary>
    /// Returns <see langword="true"/> when the band looks like an FX-rate continuation row
    /// (contains "TC1*" or "TC2*" or "U.S." — no date/sign tokens).
    /// </summary>
    private static bool IsFxContinuationRow(List<Word> bandWords)
    {
        return bandWords.Any(w =>
            w.Text.StartsWith("TC1", StringComparison.OrdinalIgnoreCase)
            || w.Text.StartsWith("TC2", StringComparison.OrdinalIgnoreCase)
            || w.Text.Equals("U.S.", StringComparison.OrdinalIgnoreCase)
            || w.Text.Equals("DOLLAR", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns <see langword="true"/> when the token is a recognized sign token (+/−).
    /// </summary>
    private static bool IsSignToken(string text)
        => text == SignChargeToken || text == SignCreditAscii || text == SignCreditUnicode;

    /// <summary>
    /// Returns <see langword="true"/> when the token represents a credit (abono).
    /// </summary>
    private static bool IsCreditToken(string text)
        => text == SignCreditAscii || text == SignCreditUnicode;

    /// <summary>
    /// Attempts to repair a truncated date token where the year is missing its last digit,
    /// e.g. "07-jul-202" → "07-jul-2025" when <paramref name="contextYear"/> is 2025.
    /// </summary>
    /// <param name="raw">Raw date token that may be truncated.</param>
    /// <param name="contextYear">Year to use for repair (from operation date or current year).</param>
    /// <returns>Repaired token, or <see langword="null"/> if repair cannot be applied.</returns>
    private static string? RepairTruncatedDate(string raw, int contextYear)
    {
        // Pattern: "dd-mmm-YYY" — 3-digit year (missing last digit).
        var m = Regex.Match(raw, @"^(\d{1,2}-[a-záéíóúñü]+-\d{3})$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            return null;

        // Append the last digit of the context year.
        var lastDigit = (contextYear % 10).ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        return raw + lastDigit;
    }

    /// <summary>
    /// Groups words into horizontal Y-bands using a custom tolerance value.
    /// Uses the same first-match algorithm as <see cref="GroupIntoBands"/> but with
    /// a caller-specified tolerance instead of the header's <see cref="YBandTolerance"/>.
    /// </summary>
    private static Dictionary<double, List<Word>> GroupIntoBandsWithTolerance(
        List<Word> words, double tolerance)
    {
        var result = new Dictionary<double, List<Word>>();

        foreach (var word in words)
        {
            var y = word.BoundingBox.Bottom;
            var matched = false;

            foreach (var key in result.Keys)
            {
                if (Math.Abs(key - y) <= tolerance)
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
