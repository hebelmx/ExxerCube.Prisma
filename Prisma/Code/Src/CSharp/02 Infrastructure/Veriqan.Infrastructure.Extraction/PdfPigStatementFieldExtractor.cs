using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
using PDFtoImage;
using ZXing;

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

            // ---- Font runs — Story 5.1 (CL-35) ----------------------------
            // Collect distinct (normalized-family, page) font runs from ALL pages.
            var (fontRuns, fontExtractionStatus) = ExtractFontRuns(doc);

            // ---- Text-overlap incidents — Story 5.2 (CL-28) ---------------
            // Detect word pairs on the same horizontal band whose X-extents
            // intersect by more than the extraction epsilon (2.0 PDF points).
            var textOverlapIncidents = ExtractTextOverlapIncidents(doc);

            // ---- Section-header styles — Story 5.2 (CL-29) ----------------
            // Match known VEC section titles against page text and capture
            // whether each detected header is bold and/or uppercase.
            var sectionHeaderStyles = ExtractSectionHeaderStyles(doc);

            // ---- Per-page inspection facts — Story 5.3 (CL-31/33/34/48) ------
            var (pages, pageCount) = ExtractPageInspectionFacts(doc, cardNumber);

            // ---- Normalized full text — Story 6.1 (CL-32/46 legend checks) ----
            // Concatenate all page words, upper-case, strip diacritics, collapse whitespace.
            var normalizedFullText = BuildNormalizedFullText(doc);

            // ---- Fiscal block — Story 6.2 (CL-50..53) ----------------------
            // Find the CFDI fiscal legend page, render it, scan for QR, extract text fields.
            var fiscalBlock = ExtractFiscalBlock(doc, pdf, normalizedFullText);

            // ---- §1–28 mandatory CONDUSEF section map — Story 10.1 ----------
            // Detect all 28 Acuerdo sections using the normalized full text
            // (avoids a second PDF open) and per-page word bands collected above.
            var detectedSections = ExtractDetectedSections(doc, normalizedFullText);

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
                FontRuns = fontRuns,
                FontExtractionStatus = fontExtractionStatus,
                TextOverlapIncidents = textOverlapIncidents,
                SectionHeaderStyles = sectionHeaderStyles,
                Pages = pages,
                PageCount = pageCount,
                NormalizedFullText = normalizedFullText,
                FiscalBlock = fiscalBlock,
                Sections = detectedSections,
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

    // -----------------------------------------------------------------------
    // Font run extraction (Story 5.1 — CL-35)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Style suffixes stripped from embedded font names when normalising to a family name.
    /// Ordered longest-first so that "-BoldItalic" is tried before "-Bold" / "-Italic".
    /// </summary>
    private static readonly string[] s_fontStyleSuffixes =
        ["-BoldItalic", "-Bold", "-Italic", "-Light", "-SemiBold", "-Medium", "-Regular", "-Thin"];

    /// <summary>
    /// Collects distinct (normalized-family, page) font runs from ALL pages of the document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Normalization:</b>
    /// <list type="bullet">
    ///   <item>Strip the 6-uppercase-letter + '+' subset prefix (e.g. "ABCDEF+" → stripped).</item>
    ///   <item>Strip known style suffixes ("-Bold", "-Italic", etc.) to get the family name.</item>
    /// </list>
    /// </para>
    /// <para>
    /// One <see cref="FontUsage"/> is produced per distinct (normalized-family, pageNumber) pair.
    /// The <see cref="FontUsage.FontName"/> is the raw name of the <em>first</em> letter on that
    /// page that maps to the given normalized family; the <see cref="FontUsage.Locator"/> is that
    /// letter's bounding rectangle.
    /// </para>
    /// </remarks>
    /// <param name="doc">The open <see cref="PdfDocument"/>.</param>
    /// <returns>
    /// A tuple of the distinct font-run list and the extraction status.
    /// Never throws — individual letter failures are silently skipped.
    /// </returns>
    private static (IReadOnlyList<FontUsage> fontRuns, FontExtractionStatus status)
        ExtractFontRuns(PdfDocument doc)
    {
        // Two-level dictionary: normalizedFamily → (pageNumber → first FontUsage).
        // OrdinalIgnoreCase on the outer key ensures "Aptos" == "aptos" grouping.
        var seenKeys = new Dictionary<string, Dictionary<int, FontUsage>>(StringComparer.OrdinalIgnoreCase);

        var anyLetterFound = false;

        for (var pageIndex = 1; pageIndex <= doc.NumberOfPages; pageIndex++)
        {
            var page = doc.GetPage(pageIndex);

            foreach (var letter in page.Letters)
            {
                var rawName = letter.FontName;
                if (string.IsNullOrEmpty(rawName))
                    continue;

                anyLetterFound = true;

                var normalizedFamily = NormalizeFontFamily(rawName);

                if (!seenKeys.TryGetValue(normalizedFamily, out var pageMap))
                {
                    pageMap = [];
                    seenKeys[normalizedFamily] = pageMap;
                }

                if (!pageMap.ContainsKey(pageIndex))
                {
                    // First occurrence of (normalizedFamily, page) — capture the locator.
                    var bb = letter.BoundingBox;
                    var locator = new FieldLocator(
                        pageIndex,
                        bb.BottomLeft.X,
                        bb.BottomLeft.Y,
                        bb.Width,
                        bb.Height);

                    pageMap[pageIndex] = new FontUsage(rawName, pageIndex, locator);
                }
            }
        }

        if (!anyLetterFound)
            return (Array.Empty<FontUsage>(), FontExtractionStatus.NotFound);

        // Flatten to a stable (page-ascending, family-alphabetical) list.
        var runs = seenKeys
            .SelectMany(kv => kv.Value.Values)
            .OrderBy(r => r.PageNumber)
            .ThenBy(r => r.FontName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (runs, FontExtractionStatus.Extracted);
    }

    /// <summary>
    /// Normalises a raw PDF font name to its family name.
    /// </summary>
    /// <param name="fontName">Raw embedded font name (e.g. <c>"ABCDEF+Aptos-Bold"</c>).</param>
    /// <returns>
    /// The normalized family name (e.g. <c>"Aptos"</c>).
    /// </returns>
    private static string NormalizeFontFamily(string fontName)
    {
        var name = fontName;

        // Strip 6-uppercase-letter + '+' subset prefix (e.g. "ABCDEF+").
        if (name.Length > 7 && name[6] == '+' && IsUpperAlpha(name.AsSpan(0, 6)))
            name = name[7..];

        // Strip known style suffixes (longest first to avoid partial strip of "-BoldItalic").
        foreach (var suffix in s_fontStyleSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
                break;
            }
        }

        return name;
    }

    /// <summary>Returns <see langword="true"/> when all characters in the span are A–Z.</summary>
    private static bool IsUpperAlpha(ReadOnlySpan<char> span)
    {
        foreach (var c in span)
        {
            if (c is < 'A' or > 'Z')
                return false;
        }

        return true;
    }

    // -----------------------------------------------------------------------
    // Text-overlap incident extraction (Story 5.2 — CL-28)
    // -----------------------------------------------------------------------

    /// <summary>
    /// The minimum X-axis intersection magnitude (in PDF points) required for two words
    /// to be recorded as a <see cref="TextOverlapIncident"/>.
    /// </summary>
    /// <remarks>
    /// Set to <b>2.0 PDF points</b> (~0.7 mm) to exclude normal kerning and glyph
    /// touching (adjacent letters that share a boundary but do not physically overlap).
    /// Only intersections that exceed this epsilon represent genuine layout-level overlap.
    /// </remarks>
    private const double TextOverlapEpsilon = 2.0;

    /// <summary>
    /// Y-band tolerance used when grouping words for overlap detection.
    /// Slightly tighter than the header tolerance to avoid merging separate lines
    /// that happen to sit close together vertically.
    /// </summary>
    private const double OverlapBandTolerance = 4.0;

    /// <summary>
    /// Scans every page of <paramref name="doc"/> for word pairs on the same horizontal
    /// band whose X-extents intersect by more than <see cref="TextOverlapEpsilon"/> points.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Algorithm:</b> per page, group all words into horizontal Y-bands using
    /// <see cref="OverlapBandTolerance"/>.  Within each band, sort words left-to-right.
    /// For each consecutive pair (wordA, wordB) where wordB.Left &lt; wordA.Right,
    /// the intersection magnitude is <c>wordA.Right − wordB.Left</c>.
    /// Only pairs where this exceeds <see cref="TextOverlapEpsilon"/> are recorded.
    /// </para>
    /// <para>
    /// Checking only consecutive sorted pairs is sufficient because genuine layout
    /// overlap is a local phenomenon — a word that overlaps a non-adjacent word
    /// also overlaps the intervening ones.
    /// </para>
    /// </remarks>
    /// <param name="doc">The open <see cref="PdfDocument"/>.</param>
    /// <returns>
    /// A (possibly empty) list of <see cref="TextOverlapIncident"/> records.
    /// Never throws — exceptions per page are swallowed silently.
    /// </returns>
    private static IReadOnlyList<TextOverlapIncident> ExtractTextOverlapIncidents(PdfDocument doc)
    {
        var incidents = new List<TextOverlapIncident>();

        for (var pageIndex = 1; pageIndex <= doc.NumberOfPages; pageIndex++)
        {
            try
            {
                var page = doc.GetPage(pageIndex);
                var words = page.GetWords().ToList();

                if (words.Count == 0)
                    continue;

                var bands = GroupIntoBandsWithTolerance(words, OverlapBandTolerance);

                foreach (var (_, bandWords) in bands)
                {
                    // Sort left-to-right by the left edge of each word's bounding box.
                    var sorted = bandWords
                        .OrderBy(w => w.BoundingBox.Left)
                        .ToList();

                    for (var i = 0; i + 1 < sorted.Count; i++)
                    {
                        var a = sorted[i];
                        var b = sorted[i + 1];

                        // X-axis overlap: how far b's left edge is inside a's right edge.
                        var overlap = a.BoundingBox.Right - b.BoundingBox.Left;

                        if (overlap <= TextOverlapEpsilon)
                            continue;

                        // Build a locator spanning both words.
                        var left = Math.Min(a.BoundingBox.Left, b.BoundingBox.Left);
                        var bottom = Math.Min(a.BoundingBox.Bottom, b.BoundingBox.Bottom);
                        var right = Math.Max(a.BoundingBox.Right, b.BoundingBox.Right);
                        var top = Math.Max(a.BoundingBox.Top, b.BoundingBox.Top);
                        var locator = new FieldLocator(pageIndex, left, bottom, right - left, top - bottom);

                        var sample = $"'{a.Text}' ∩ '{b.Text}'";
                        incidents.Add(new TextOverlapIncident(pageIndex, overlap, locator, sample));
                    }
                }
            }
            catch (Exception)
            {
                // Silently skip pages that cannot be read; the rule will report
                // InsufficientData only when the entire model is absent.
            }
        }

        return incidents;
    }

    // -----------------------------------------------------------------------
    // Section-header style extraction (Story 5.2 — CL-29)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Known VEC section-title strings used for header detection (CL-29).
    /// Matching is performed case-insensitively against the normalized page text.
    /// </summary>
    /// <remarks>
    /// The list covers the primary section headings defined by the VEC template.
    /// A band that begins with any of these tokens (after normalization) is recorded
    /// as a <see cref="SectionHeaderStyle"/>.
    /// </remarks>
    private static readonly string[] s_knownHeaderPhrases =
    [
        "RESUMEN DE CARGOS Y ABONOS DEL PERIODO",
        "NIVEL DE USO DE TU TARJETA",
        "DESGLOSE DE MOVIMIENTOS DEL PERIODO",
        "PROGRAMAS DE BENEFICIOS DE LA TARJETA",
        "COMPRAS Y CARGOS DIFERIDOS A MESES SIN INTERESES",
        "PROMOCIONES",
    ];

    /// <summary>
    /// Scans every page of <paramref name="doc"/> for bands whose concatenated text
    /// starts with one of the known section-title phrases.  For each match, records
    /// whether the first letter of the band is rendered in a Bold font and whether
    /// all alphabetic characters in the printed text are uppercase.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bold detection:</b> the raw font name of the <em>first letter glyph</em>
    /// within the matched band is checked for the sub-string <c>"Bold"</c>
    /// (case-insensitive).
    /// </para>
    /// <para>
    /// <b>Uppercase detection:</b> applied to the concatenated band text (all word
    /// tokens joined with a space) — if every alphabetic character is uppercase the
    /// header is considered all-caps.
    /// </para>
    /// <para>
    /// Band grouping uses <see cref="YBandTolerance"/> (5 pt) to match the rest of
    /// the extractor, so that header text spread across a few fractional-Y positions
    /// is still captured on a single band.
    /// </para>
    /// </remarks>
    /// <param name="doc">The open <see cref="PdfDocument"/>.</param>
    /// <returns>
    /// A list of <see cref="SectionHeaderStyle"/> records for each detected header.
    /// Each known phrase appears at most once (first occurrence wins).
    /// Never throws.
    /// </returns>
    private static IReadOnlyList<SectionHeaderStyle> ExtractSectionHeaderStyles(PdfDocument doc)
    {
        var results = new List<SectionHeaderStyle>();

        // Track which phrases have already been matched (first-occurrence wins).
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var pageIndex = 1; pageIndex <= doc.NumberOfPages; pageIndex++)
        {
            try
            {
                var page = doc.GetPage(pageIndex);
                var words = page.GetWords().ToList();

                if (words.Count == 0)
                    continue;

                // Build a letter-level lookup: map each word's bounding-box bottom to
                // the first letter found in that vicinity so we can determine the font.
                var lettersByBandY = BuildLetterBandMap(page, words);

                var bands = GroupIntoBandsWithTolerance(words, YBandTolerance);

                // Process bands top-to-bottom (descending Y).
                foreach (var (bandY, bandWords) in bands.OrderByDescending(kv => kv.Key))
                {
                    if (matched.Count == s_knownHeaderPhrases.Length)
                        break; // all phrases found — stop scanning

                    var sorted = bandWords.OrderBy(w => w.BoundingBox.Left).ToList();
                    var bandText = string.Join(" ", sorted.Select(w => w.Text)).Trim();

                    // Check whether the band text starts with any known header phrase.
                    string? matchedPhrase = null;
                    foreach (var phrase in s_knownHeaderPhrases)
                    {
                        if (matched.Contains(phrase))
                            continue;

                        if (bandText.StartsWith(phrase, StringComparison.OrdinalIgnoreCase)
                            || NormalizeHeaderText(bandText).StartsWith(
                                NormalizeHeaderText(phrase), StringComparison.OrdinalIgnoreCase))
                        {
                            matchedPhrase = phrase;
                            break;
                        }
                    }

                    if (matchedPhrase is null)
                        continue;

                    matched.Add(matchedPhrase);

                    // Determine bold: find the first letter glyph on this band.
                    var isBold = IsBandBold(bandY, lettersByBandY);

                    // Determine uppercase: evaluate only the MATCHED CANONICAL PHRASE portion
                    // of the band text — not the full band text which may contain trailing
                    // date-range suffixes (e.g. "DESGLOSE DE MOVIMIENTOS DEL PERIODO 5-jul-2025
                    // al 04-ago-2025") that include lowercase characters and would incorrectly
                    // force IsUppercase = false for a compliant all-caps section title.
                    var isUppercase = IsAllUppercase(matchedPhrase);

                    // Record the canonical matched phrase as the header text (not the full band
                    // text with date suffixes) so downstream rules and findings reference the
                    // section title itself rather than the title + trailing annotation.
                    var locator = BoundingBoxOf(sorted, pageIndex);
                    results.Add(new SectionHeaderStyle(matchedPhrase, isBold, isUppercase, pageIndex, locator));
                }
            }
            catch (Exception)
            {
                // Silently skip pages that cannot be read.
            }
        }

        return results;
    }

    /// <summary>
    /// Builds a map from approximate Y-band coordinate to the first letter glyph
    /// found in that band, used for bold detection in section-header extraction.
    /// </summary>
    private static Dictionary<double, UglyToad.PdfPig.Content.Letter> BuildLetterBandMap(
        UglyToad.PdfPig.Content.Page page,
        List<Word> wordsOnPage)
    {
        // Collect band Y keys from the word bands so we can snap letter Y values to them.
        var bands = GroupIntoBandsWithTolerance(wordsOnPage, YBandTolerance);
        var bandKeys = bands.Keys.ToList();

        var result = new Dictionary<double, UglyToad.PdfPig.Content.Letter>();

        foreach (var letter in page.Letters)
        {
            if (string.IsNullOrEmpty(letter.FontName))
                continue;

            var letterY = letter.BoundingBox.BottomLeft.Y;

            // Snap to nearest band key.
            var nearestKey = double.NaN;
            var nearestDist = double.MaxValue;
            foreach (var key in bandKeys)
            {
                var dist = Math.Abs(key - letterY);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestKey = key;
                }
            }

            if (double.IsNaN(nearestKey) || nearestDist > YBandTolerance)
                continue;

            // Store only the first letter found for each band.
            result.TryAdd(nearestKey, letter);
        }

        return result;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the first letter glyph on the given band Y
    /// uses a font that contains <c>"Bold"</c> in its name (case-insensitive).
    /// </summary>
    private static bool IsBandBold(
        double bandY,
        Dictionary<double, UglyToad.PdfPig.Content.Letter> letterBandMap)
    {
        // Find the letter closest to bandY within the tolerance.
        var nearestKey = double.NaN;
        var nearestDist = double.MaxValue;
        foreach (var key in letterBandMap.Keys)
        {
            var dist = Math.Abs(key - bandY);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestKey = key;
            }
        }

        if (double.IsNaN(nearestKey) || nearestDist > YBandTolerance)
            return false;

        return letterBandMap.TryGetValue(nearestKey, out var letter)
            && letter.FontName is not null
            && letter.FontName.Contains("Bold", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns <see langword="true"/> when every alphabetic character in
    /// <paramref name="text"/> is uppercase (digits, spaces, and punctuation are ignored).
    /// </summary>
    private static bool IsAllUppercase(string text)
    {
        var hasAlpha = false;
        foreach (var c in text)
        {
            if (!char.IsLetter(c))
                continue;
            hasAlpha = true;
            if (char.IsLower(c))
                return false;
        }

        return hasAlpha; // empty/no-alpha → false (can't confirm uppercase)
    }

    /// <summary>
    /// Collapses multiple spaces and trims the string for loose header matching.
    /// </summary>
    private static string NormalizeHeaderText(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"\s+", " ");

    // -----------------------------------------------------------------------
    // Per-page inspection facts (Story 5.3)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Pagination "N de M" pattern: matches labels like "1 de 4", "2 de 4", etc.
    /// </summary>
    private static readonly Regex PaginationPattern = new(
        @"\b(\d+)\s+de\s+(\d+)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Collects per-page structural metadata from all pages of the document.
    /// </summary>
    /// <param name="doc">The open <see cref="PdfDocument"/>.</param>
    /// <param name="cardNumber">
    /// The extracted card number field. Used to check card-number presence per page.
    /// The card number is compared digits-only (spaces stripped) against page text.
    /// </param>
    /// <returns>
    /// A tuple of the per-page facts list (ordered by page number) and the document page count.
    /// Never throws — individual page failures are silently skipped (HasContent = false, ImageCount = 0).
    /// </returns>
    private static (IReadOnlyList<PageInspectionFacts> pages, int pageCount)
        ExtractPageInspectionFacts(PdfDocument doc, ExtractedField<string> cardNumber)
    {
        var pageCount = doc.NumberOfPages;
        var pages = new List<PageInspectionFacts>(pageCount);

        // Extract digits-only card number for contains check.
        var cardDigits = string.Empty;
        if (cardNumber.Status != ExtractionStatus.NotExtracted)
        {
            var raw = cardNumber.Value ?? string.Empty;
            cardDigits = DigitsOnly.Replace(raw, string.Empty);
        }

        for (var pageIndex = 1; pageIndex <= pageCount; pageIndex++)
        {
            try
            {
                var page = doc.GetPage(pageIndex);
                var words = page.GetWords().ToList();

                // HasContent: any words on the page.
                var hasContent = words.Count > 0;

                // ImageCount: number of embedded images (proxy for logo).
                var imageCount = page.GetImages().Count();

                // Collect all page text (stripped of spaces, lower-case) for card-number check.
                var pageTextStripped = string.Concat(words.Select(w => w.Text))
                    .Replace(" ", string.Empty, StringComparison.Ordinal)
                    .ToLowerInvariant();

                // ContainsCardNumber: card digits appear in page text.
                bool containsCardNumber;
                if (string.IsNullOrEmpty(cardDigits))
                {
                    containsCardNumber = false;
                }
                else
                {
                    var cardDigitsLower = cardDigits.ToLowerInvariant();
                    containsCardNumber = pageTextStripped.Contains(
                        cardDigitsLower, StringComparison.OrdinalIgnoreCase);
                }

                // PaginationCurrent / PaginationTotal: parse "N de M" from page text.
                var pageText = string.Join(" ", words.Select(w => w.Text));
                int? paginationCurrent = null;
                int? paginationTotal = null;
                var paginationMatch = PaginationPattern.Match(pageText);
                if (paginationMatch.Success
                    && int.TryParse(paginationMatch.Groups[1].Value,
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var current)
                    && int.TryParse(paginationMatch.Groups[2].Value,
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var total))
                {
                    paginationCurrent = current;
                    paginationTotal = total;
                }

                var locator = FieldLocator.PageHint(pageIndex);

                // Story 10.1: capture page geometry (PDF points) for Story 10.2 gap computation.
                var pageWidth = page.Width;
                var pageHeight = page.Height;

                pages.Add(new PageInspectionFacts(
                    PageNumber: pageIndex,
                    HasContent: hasContent,
                    ImageCount: imageCount,
                    ContainsCardNumber: containsCardNumber,
                    PaginationCurrent: paginationCurrent,
                    PaginationTotal: paginationTotal,
                    Locator: locator,
                    Width: pageWidth,
                    Height: pageHeight));
            }
            catch (Exception)
            {
                // Silently skip unreadable pages with safe defaults.
                pages.Add(new PageInspectionFacts(
                    PageNumber: pageIndex,
                    HasContent: false,
                    ImageCount: 0,
                    ContainsCardNumber: false,
                    PaginationCurrent: null,
                    PaginationTotal: null,
                    Locator: FieldLocator.PageHint(pageIndex),
                    Width: 0.0,
                    Height: 0.0));
            }
        }

        return (pages, pageCount);
    }

    // -----------------------------------------------------------------------
    // Normalized full text (Story 6.1 — CL-32/46)
    // -----------------------------------------------------------------------

    // -----------------------------------------------------------------------
    // Fiscal block extraction constants (Story 6.2 — CL-50..53)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Normalized legend text used to locate the fiscal block page.
    /// Matches "REPRESENTACIÓN IMPRESA SIN VALIDEZ FISCAL" after NormalizeText().
    /// </summary>
    private const string FiscalLegendNormalized = "REPRESENTACION IMPRESA SIN VALIDEZ FISCAL";

    /// <summary>
    /// DPI at which the fiscal-block page is rendered for QR scanning.
    /// 150 DPI balances speed vs. QR readability.
    /// </summary>
    private const int FiscalPageRenderDpi = 150;

    /// <summary>
    /// Pattern for extracting RFC tokens from fiscal-block page text.
    /// Matches Mexican RFC for both persons (4-char) and companies (3-char).
    /// </summary>
    private static readonly Regex FiscalRfcTokenPattern = new(
        @"\b([A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Pattern for extracting a UUID-style fiscal code (folio fiscal / UUID CFDI):
    /// 8-4-4-4-12 hex groups separated by hyphens.
    /// </summary>
    private static readonly Regex FiscalCodeUuidPattern = new(
        @"\b([0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // -----------------------------------------------------------------------
    // Fiscal block extraction (Story 6.2 — CL-50..53)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Locates the CFDI fiscal block page, renders it via PDFtoImage, scans for a QR code
    /// using ZXing, and extracts the fiscal code + issuer/receiver RFC from page text.
    /// </summary>
    /// <param name="doc">The open PdfPig document (for per-page text).</param>
    /// <param name="pdfBytes">The raw PDF bytes (needed by PDFtoImage for rendering).</param>
    /// <param name="normalizedFullText">Pre-built normalized full text for quick legend search.</param>
    /// <returns>
    /// A <see cref="FiscalBlock"/> record. Never throws — all render/decode failures are caught
    /// and result in <see cref="FiscalBlock.QrDecoded"/> == <see langword="false"/>.
    /// </returns>
    private FiscalBlock ExtractFiscalBlock(
        UglyToad.PdfPig.PdfDocument doc,
        byte[] pdfBytes,
        string normalizedFullText)
    {
        // Fast path: if the legend is not in the normalized full text, block is absent.
        if (!normalizedFullText.Contains(FiscalLegendNormalized, StringComparison.Ordinal))
            return FiscalBlock.NotPresent();

        // Find which page has the CFDI legend.
        int fiscalPageNumber = -1;
        for (var i = 1; i <= doc.NumberOfPages; i++)
        {
            try
            {
                var page = doc.GetPage(i);
                var pageWords = page.GetWords();
                var pageText = string.Join(" ", pageWords.Select(w => w.Text));
                var normalized = NormalizeText(pageText);
                if (normalized.Contains(FiscalLegendNormalized, StringComparison.Ordinal))
                {
                    fiscalPageNumber = i;
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read page {Page} while searching for fiscal legend.", i);
            }
        }

        if (fiscalPageNumber < 0)
        {
            // Full-text said yes but per-page scan found nothing — treat as absent.
            return FiscalBlock.NotPresent();
        }

        var locator = FieldLocator.PageHint(fiscalPageNumber);

        // ---- Extract text fields (fiscal code + RFCs) from page text --------
        string? fiscalCode = null;
        string? issuerRfc = null;
        string? receiverRfc = null;

        try
        {
            var fiscalPage = doc.GetPage(fiscalPageNumber);
            var allPageText = string.Join(" ", fiscalPage.GetWords().Select(w => w.Text));

            // UUID-style fiscal code (folio fiscal / UUID CFDI).
            var uuidMatch = FiscalCodeUuidPattern.Match(allPageText);
            if (uuidMatch.Success)
                fiscalCode = uuidMatch.Groups[1].Value.ToUpperInvariant();

            // RFC tokens — first occurrence = issuer, second = receiver
            // (order on CFDI representation: Emisor RFC then Receptor RFC).
            var rfcMatches = FiscalRfcTokenPattern.Matches(allPageText);
            if (rfcMatches.Count >= 1)
                issuerRfc = rfcMatches[0].Groups[1].Value.ToUpperInvariant();
            if (rfcMatches.Count >= 2)
                receiverRfc = rfcMatches[1].Groups[1].Value.ToUpperInvariant();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract text fields from fiscal page {Page}.", fiscalPageNumber);
        }

        // ---- Render page and scan for QR with ZXing -------------------------
        bool qrDecoded = false;
        string? qrPayload = null;

        try
        {
            // PDFtoImage page index is 0-based.
            var pageIndex0 = fiscalPageNumber - 1;
            using var pdfStream = new MemoryStream(pdfBytes);
#pragma warning disable CA1416 // PDFtoImage is cross-platform
            using var bitmap = Conversion.ToImage(
                pdfStream,
                leaveOpen: false,
                page: pageIndex0,
                options: new RenderOptions(Dpi: FiscalPageRenderDpi));
#pragma warning restore CA1416

            if (bitmap is not null && bitmap.Width > 0 && bitmap.Height > 0)
            {
                // SKBitmap.Bytes gives BGRA32 row-major data.
                var bgraBytes = bitmap.Bytes;

                if (bgraBytes is not null && bgraBytes.Length == bitmap.Width * bitmap.Height * 4)
                {
                    // Convert BGRA → BGR for RGBLuminanceSource.
                    var bgrBytes = ConvertBgraToRgbForZXing(bgraBytes, bitmap.Width, bitmap.Height);

                    var luminance = new RGBLuminanceSource(
                        bgrBytes,
                        bitmap.Width,
                        bitmap.Height,
                        RGBLuminanceSource.BitmapFormat.BGR24);

                    var reader = new BarcodeReaderGeneric
                    {
                        AutoRotate = true,
                        Options = new ZXing.Common.DecodingOptions
                        {
                            TryHarder = true,
                            PossibleFormats = [ZXing.BarcodeFormat.QR_CODE],
                        },
                    };

                    var decoded = reader.Decode(luminance);
                    if (decoded is not null && !string.IsNullOrWhiteSpace(decoded.Text))
                    {
                        qrDecoded = true;
                        qrPayload = decoded.Text;

                        // If QR payload contains a UUID and we didn't find one in page text, extract it.
                        if (fiscalCode is null)
                        {
                            var uuidInQr = FiscalCodeUuidPattern.Match(qrPayload);
                            if (uuidInQr.Success)
                                fiscalCode = uuidInQr.Groups[1].Value.ToUpperInvariant();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "QR scan failed for fiscal page {Page}; QrDecoded will be false.", fiscalPageNumber);
        }

        _logger.LogInformation(
            "Fiscal block: page={Page} blockPresent=true qrDecoded={QrDecoded} "
            + "fiscalCode={FiscalCode} issuerRfc={IssuerRfc} receiverRfc={ReceiverRfc}",
            fiscalPageNumber, qrDecoded, fiscalCode ?? "null", issuerRfc ?? "null", receiverRfc ?? "null");

        return new FiscalBlock(
            BlockPresent: true,
            QrDecoded: qrDecoded,
            QrPayload: qrPayload,
            FiscalCode: fiscalCode,
            IssuerRfc: issuerRfc,
            ReceiverRfc: receiverRfc,
            Locator: locator);
    }

    /// <summary>
    /// Converts a BGRA byte array to a BGR byte array for ZXing <c>RGBLuminanceSource</c>.
    /// Each 4-byte BGRA pixel becomes a 3-byte BGR pixel (alpha channel dropped).
    /// </summary>
    private static byte[] ConvertBgraToRgbForZXing(byte[] bgra, int width, int height)
    {
        var bgr = new byte[width * height * 3];
        for (var i = 0; i < width * height; i++)
        {
            var srcBase = i * 4;
            var dstBase = i * 3;
            bgr[dstBase]     = bgra[srcBase];     // B
            bgr[dstBase + 1] = bgra[srcBase + 1]; // G
            bgr[dstBase + 2] = bgra[srcBase + 2]; // R
        }

        return bgr;
    }

    /// <summary>
    /// Builds a normalized concatenation of all page text in the document for legend-presence checks.
    /// </summary>
    /// <remarks>
    /// Normalization is applied via <see cref="VecTextNormalizer.Normalize"/> — the canonical
    /// shared implementation used by both this assembly and the Validation assembly.
    /// Returns <see cref="string.Empty"/> when the document has no pages or no text layer.
    /// Never throws — individual page failures produce no contribution.
    /// </remarks>
    /// <param name="doc">The open PdfPig document.</param>
    /// <returns>Normalized full-text string.</returns>
    private static string BuildNormalizedFullText(PdfDocument doc)
    {
        var sb = new StringBuilder();

        for (var i = 1; i <= doc.NumberOfPages; i++)
        {
            try
            {
                var page = doc.GetPage(i);
                foreach (var word in page.GetWords())
                {
                    if (sb.Length > 0)
                        sb.Append(' ');
                    sb.Append(word.Text);
                }
            }
            catch (Exception)
            {
                // Silently skip unreadable pages.
            }
        }

        return NormalizeText(sb.ToString());
    }

    /// <summary>
    /// Normalizes a text string for legend-presence matching.
    /// Delegates to <see cref="VecTextNormalizer.Normalize"/> — the canonical single-source
    /// implementation shared with the Validation assembly.
    /// </summary>
    /// <param name="text">The raw text to normalize. May be null or empty.</param>
    /// <returns>
    /// Normalized string, or <see cref="string.Empty"/> when the input is null or whitespace.
    /// </returns>
    internal static string NormalizeText(string? text) => VecTextNormalizer.Normalize(text);

    // -----------------------------------------------------------------------
    // §1–28 Mandatory CONDUSEF section detection (Story 10.1)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Anchor table: (SectionNumber, CanonicalName, NormalizedAnchor, IsConditional).
    /// Anchors are the normalized (upper+accent-stripped) minimum phrase that reliably
    /// identifies each section heading in the Dummie VEC fixture PDFs.
    /// §16, §23, §25 are conditional: they are not counted missing when absent.
    /// </summary>
    private static readonly (int Number, string Name, string NormalizedAnchor, bool IsConditional)[] s_sectionAnchors =
    [
        (1,  "Logo del Banco",                                  "LOGO",                                                  false),
        (2,  "Paginación (Página X de Y)",                      "PAGINA",                                                false),
        (3,  "Datos de envío",                                   "DATOS DE ENVIO",                                        false),
        (4,  "Identificación del producto",                      "IDENTIFICACION DEL PRODUCTO",                           false),
        (5,  "Tu pago requerido",                               "TU PAGO REQUERIDO",                                     false),
        (6,  "Cuánto pagarías por tus compras",                 "CUANTO PAGARIAS POR TUS COMPRAS",                       false),
        (7,  "Resumen de cargos y abonos",                      "RESUMEN DE CARGOS Y ABONOS",                            false),
        (8,  "Indicadores del costo anual",                     "INDICADORES DEL COSTO ANUAL",                           false),
        (9,  "CAT",                                              "CAT",                                                   false),
        (10, "Tasa de interés anual ordinaria",                 "TASA DE INTERES ANUAL",                                 false),
        (11, "Compara tu tarjeta",                              "COMPARA TU TARJETA",                                    false),
        (12, "Mensajes importantes",                            "MENSAJES IMPORTANTES",                                   false),
        (13, "Nivel de uso de tu tarjeta",                      "NIVEL DE USO DE TU TARJETA",                            false),
        (14, "Notas al calce",                                  "NOTAS AL CALCE",                                        false),
        (15, "Número de cuenta (página 2+)",                    "NUMERO DE CUENTA",                                      false),
        (16, "Información de otras líneas de crédito",          "OTRAS LINEAS DE CREDITO",                               true),
        (17, "Mensajes adicionales",                            "MENSAJES ADICIONALES",                                   false),
        (18, "Programas de beneficios",                         "PROGRAMAS DE BENEFICIOS",                               false),
        (19, "Saldo sobre el que se calcularon los intereses",  "SALDO SOBRE EL QUE SE CALCULARON LOS INTERESES",        false),
        (20, "Distribución de tu último pago",                  "DISTRIBUCION DE TU ULTIMO PAGO",                        false),
        (21, "Sección opcional libre (§21)",                    "SECCION OPCIONAL",                                      false),
        (22, "Desglose de movimientos",                         "DESGLOSE DE MOVIMIENTOS",                               false),
        (23, "Cargos no reconocidos",                           "CARGOS NO RECONOCIDOS",                                 true),
        (24, "Atención de quejas",                              "ATENCION DE QUEJAS",                                    false),
        (25, "Reestructura de tu deuda",                        "REESTRUCTURA",                                          true),
        (26, "Notas aclaratorias",                              "NOTAS ACLARATORIAS",                                    false),
        (27, "Glosario de términos",                            "GLOSARIO DE TERMINOS",                                  false),
        (28, "Sección opcional libre (§28)",                    "SECCION LIBRE",                                         false),
    ];

    /// <summary>
    /// Detects the 28 mandatory CONDUSEF <i>Acuerdo</i> sections in the document.
    /// Uses the normalized full text for fast containment checks, then scans per-page
    /// word bands for a precise heading locator when found.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The method reuses the open <see cref="PdfDocument"/> (no second PDF open).
    /// Section anchors are matched against the normalized full text
    /// (<see cref="VecTextNormalizer.Normalize"/>).
    /// </para>
    /// <para>
    /// Conditional sections (§16, §23, §25) are marked <see cref="DetectedSection.IsApplicable"/>
    /// = <see langword="false"/> when their anchor is absent — they are never counted missing.
    /// </para>
    /// </remarks>
    /// <param name="doc">Open PdfPig document (pages are read without re-opening).</param>
    /// <param name="normalizedFullText">Already-computed normalized full text of the document.</param>
    /// <returns>
    /// Exactly 28 <see cref="DetectedSection"/> entries ordered by section number.
    /// Never throws — individual page/band failures are silently swallowed.
    /// </returns>
    private static IReadOnlyList<DetectedSection> ExtractDetectedSections(
        PdfDocument doc,
        string normalizedFullText)
    {
        // Guard: if no text layer, all sections absent and conditionals are not applicable.
        if (string.IsNullOrWhiteSpace(normalizedFullText))
        {
            return BuildAllAbsent();
        }

        // Pre-scan: quick containment check per anchor against the full text.
        // For anchors that are found, do a per-page scan to capture the locator.
        // For anchors not found, build a NotPresent result immediately.

        var results = new List<DetectedSection>(28);

        foreach (var (number, name, anchor, isConditional) in s_sectionAnchors)
        {
            if (!normalizedFullText.Contains(anchor, StringComparison.Ordinal))
            {
                // Not in document at all.
                // Conditional: IsApplicable = false (not counted missing).
                // Unconditional: IsApplicable = true but IsPresent = false (counted missing).
                results.Add(new DetectedSection(
                    SectionNumber: number,
                    Name: name,
                    IsPresent: false,
                    IsApplicable: !isConditional,
                    Locator: FieldLocator.NoPage()));
                continue;
            }

            // Anchor is in the full text — find its first occurrence in a page band to get the locator.
            var locator = FindSectionLocator(doc, anchor);

            results.Add(new DetectedSection(
                SectionNumber: number,
                Name: name,
                IsPresent: true,
                IsApplicable: true,
                Locator: locator));
        }

        return results;
    }

    /// <summary>
    /// Scans all pages for the first band whose normalized text contains
    /// <paramref name="anchor"/> and returns a <see cref="FieldLocator"/> for that band.
    /// Falls back to <see cref="FieldLocator.PageHint(int)"/> when no page-level locator can be derived.
    /// </summary>
    private static FieldLocator FindSectionLocator(PdfDocument doc, string anchor)
    {
        for (var pageIndex = 1; pageIndex <= doc.NumberOfPages; pageIndex++)
        {
            try
            {
                var page = doc.GetPage(pageIndex);
                var words = page.GetWords().ToList();

                if (words.Count == 0)
                    continue;

                var bands = GroupIntoBandsWithTolerance(words, YBandTolerance);

                // Scan bands top-to-bottom.
                foreach (var (_, bandWords) in bands.OrderByDescending(kv => kv.Key))
                {
                    var sorted = bandWords.OrderBy(w => w.BoundingBox.Left).ToList();
                    var bandText = NormalizeText(string.Join(" ", sorted.Select(w => w.Text)));

                    if (bandText.Contains(anchor, StringComparison.Ordinal))
                        return BoundingBoxOf(sorted, pageIndex);
                }
            }
            catch (Exception)
            {
                // Silently skip unreadable pages.
            }
        }

        // Anchor was found in the full text but not locatable per-band — give a page-1 hint.
        return FieldLocator.PageHint(1);
    }

    /// <summary>
    /// Builds the 28-section list with all sections absent (used when the text layer is empty).
    /// </summary>
    private static IReadOnlyList<DetectedSection> BuildAllAbsent()
    {
        var results = new List<DetectedSection>(28);
        foreach (var (number, name, _, isConditional) in s_sectionAnchors)
        {
            results.Add(new DetectedSection(
                SectionNumber: number,
                Name: name,
                IsPresent: false,
                IsApplicable: !isConditional,
                Locator: FieldLocator.NoPage()));
        }

        return results;
    }
}
