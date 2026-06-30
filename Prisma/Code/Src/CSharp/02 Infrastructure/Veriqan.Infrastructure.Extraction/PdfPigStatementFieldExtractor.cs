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
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using CoenM.ImageHash.HashAlgorithms;
using PDFtoImage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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

    /// <summary>
    /// Matches a masked card number displayed in four space- or hyphen-separated groups
    /// where the first three groups are mask glyphs (X, x, *, •, ·, ●) and the last group
    /// is the four visible digits (e.g. "XXXX XXXX XXXX 1234", "**** **** **** 1234").
    /// </summary>
    /// <remarks>
    /// The first three groups intentionally require mask characters — NOT digits — to prevent
    /// an all-digit transaction reference printed in 4-4-4-4 format (e.g. "1234 5678 9012 1234")
    /// from satisfying the fallback and producing a false-PASS on CL-34.
    /// A genuine unmasked full card number is matched by the primary 16-digit path instead.
    /// </remarks>
    private static readonly Regex CardGroupPattern = new(
        @"[Xx\*•·●]{4}[\s\-–][Xx\*•·●]{4}[\s\-–][Xx\*•·●]{4}[\s\-–](\d{4})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

    /// <summary>
    /// Inline currency amount: an optional "$" followed by digit groups (comma-separated)
    /// and a mandatory two-decimal-place fraction.
    /// Used by the §23 best-effort dispute-row extractor to locate amounts within the
    /// normalized, whitespace-collapsed SectionText (e.g. "500.00", "1,234.56", "$99.00").
    /// The regex is NOT anchored so it can match amounts embedded in a longer text string.
    /// </summary>
    private static readonly Regex DisputeRowAmountPattern = new(
        @"\$?([\d,]+\.\d{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ILogger<PdfPigStatementFieldExtractor> _logger;
    private readonly PdfExtractionOptions _options;
    private readonly IPasswordProvider _passwordProvider;
    private readonly TimeProvider _timeProvider;
    private readonly bool _enableCatalogImageHashing;

    /// <summary>Initializes a new <see cref="PdfPigStatementFieldExtractor"/>.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="options">PDF extraction options.</param>
    /// <param name="passwordProvider">Password provider for encrypted PDFs.</param>
    /// <param name="timeProvider">
    /// Clock abstraction used when repairing truncated dates whose year cannot be inferred
    /// from document context.  Defaults to <see cref="TimeProvider.System"/> when
    /// <see langword="null"/> or omitted so existing construction sites and DI registrations
    /// require no changes.
    /// </param>
    /// <param name="enableCatalogImageHashing">
    /// When <see langword="true"/>, <see cref="ExtractFullAsync"/> renders every PDF page via
    /// PDFtoImage and computes a CoenM <c>PerceptualHash</c> (64-bit ulong) per page, stored
    /// in <see cref="StatementModel.PagePerceptualHashes"/>.
    /// <para>
    /// Default is <see langword="false"/> (opt-in) because rendering every page is costly.
    /// Enable once a real catalog-image reference bundle and a corpus-calibrated Hamming
    /// threshold are available (VERIQAN-E5 / CPA-2 corpus gate).
    /// </para>
    /// </param>
    public PdfPigStatementFieldExtractor(
        ILogger<PdfPigStatementFieldExtractor> logger,
        IOptions<PdfExtractionOptions> options,
        IPasswordProvider passwordProvider,
        TimeProvider? timeProvider = null,
        bool enableCatalogImageHashing = false)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _passwordProvider = passwordProvider ?? throw new ArgumentNullException(nameof(passwordProvider));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _enableCatalogImageHashing = enableCatalogImageHashing;
    }

    /// <summary>
    /// Carries the detected number-format convention for a single statement parse session.
    /// Not thread-safe by design — one instance per <see cref="ExtractFromOpenDocument"/> call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mexican VEC statements always use US/MX format (<c>1,234.56</c>) so the first
    /// unambiguous amount token will set the format for the rest of the parse.
    /// </para>
    /// <para>
    /// Detection is lazy: the first unambiguous token sets the format; subsequent tokens
    /// use it.  "Unambiguous" means a token that contains <em>both</em> a thousands separator
    /// and a decimal separator so the role of each character is unequivocal:
    /// <list type="bullet">
    ///   <item><c>1,234.56</c> — comma is thousands, period is decimal → US/MX format.</item>
    ///   <item><c>1.234,56</c> — period is thousands, comma is decimal → European format.</item>
    /// </list>
    /// An isolated number such as <c>1.234</c> is ambiguous (could be 1234 European or
    /// 1.234 US) and will NOT be used to set the format.
    /// </para>
    /// </remarks>
    internal sealed class AmountNumberFormatSession
    {
        private static readonly System.Globalization.NumberFormatInfo s_usMxFormat;
        private static readonly System.Globalization.NumberFormatInfo s_euroFormat;

        static AmountNumberFormatSession()
        {
            var usMx = new System.Globalization.NumberFormatInfo
            {
                NumberDecimalSeparator = ".",
                NumberGroupSeparator = ",",
            };
            s_usMxFormat = usMx;

            var euro = new System.Globalization.NumberFormatInfo
            {
                NumberDecimalSeparator = ",",
                NumberGroupSeparator = ".",
            };
            s_euroFormat = euro;
        }

        /// <summary>Detected format; <see langword="null"/> = not yet determined.</summary>
        private System.Globalization.NumberFormatInfo? _detected;

        /// <summary>
        /// Attempts to detect the number format from <paramref name="rawToken"/> and, if
        /// unambiguous, stores the result so subsequent calls to <see cref="TryParse"/> use it.
        /// </summary>
        private void DetectFromToken(string rawToken)
        {
            if (_detected is not null)
                return;

            var span = rawToken.AsSpan().TrimStart("+−$- ".AsSpan());

            int commaIdx = span.IndexOf(',');
            int dotIdx   = span.IndexOf('.');

            if (commaIdx < 0 && dotIdx < 0)
                return;

            if (commaIdx >= 0 && dotIdx >= 0)
            {
                if (dotIdx > commaIdx)
                    _detected = s_usMxFormat;
                else
                    _detected = s_euroFormat;
                return;
            }

            // Only one separator — ambiguous. Do NOT set format.
        }

        /// <summary>
        /// Tries to parse <paramref name="stripped"/> into a <see cref="decimal"/>.
        /// </summary>
        /// <remarks>
        /// This is the <em>format-detecting</em> entry point.  It will abstain (return
        /// <see langword="false"/>) when no format has yet been detected AND the token is
        /// genuinely ambiguous (contains only one separator so its role is unclear).
        /// Use this path only for raw unfiltered tokens where European format is possible
        /// (e.g. the §20 <c>ParseSignedAmountCell</c> path).
        /// For tokens that have already been validated by <c>AmountPattern</c> /
        /// <c>DesgloseAmountPattern</c> (US/MX shape guaranteed), use
        /// <see cref="TryParseUsMx"/> instead, which never abstains.
        /// </remarks>
        public bool TryParse(string stripped, out decimal value)
        {
            value = 0m;

            if (string.IsNullOrWhiteSpace(stripped))
                return false;

            DetectFromToken(stripped);

            if (_detected is not null)
            {
                var normalised = stripped.Replace(
                    _detected.NumberGroupSeparator,
                    string.Empty,
                    StringComparison.Ordinal);
                return decimal.TryParse(
                    normalised,
                    System.Globalization.NumberStyles.Number,
                    _detected,
                    out value);
            }

            return false;
        }

        /// <summary>
        /// Parses <paramref name="stripped"/> unconditionally as US/MX format
        /// (comma = thousands separator, period = decimal separator) — the same
        /// legacy behaviour as <c>decimal.Parse(InvariantCulture)</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this method at call sites where the token has <em>already</em> been
        /// validated by a US/MX-shaped regex (<c>AmountPattern</c> /
        /// <c>DesgloseAmountPattern</c>: <c>^\$?([\d,]+(?:\.\d+)?)$</c>).  Those
        /// regexes only match tokens whose comma (if any) precedes the dot, which is
        /// unambiguous US/MX.  Routing such tokens through <see cref="TryParse"/> would
        /// abstain when no both-separator token had been seen yet — causing false-negatives
        /// on statements whose amounts are all &lt; $1,000 (no thousands commas).
        /// </para>
        /// <para>
        /// This method also seeds <c>_detected</c> to US/MX on first success, so subsequent
        /// calls to <see cref="TryParse"/> (§20 path) benefit from the established context.
        /// </para>
        /// <para>
        /// Returns <see langword="false"/> only when <paramref name="stripped"/> is not a
        /// valid number after removing commas — never abstains on format grounds.
        /// </para>
        /// </remarks>
        public bool TryParseUsMx(string stripped, out decimal value)
        {
            value = 0m;

            if (string.IsNullOrWhiteSpace(stripped))
                return false;

            // Remove thousands commas, then parse with US/MX decimal rules.
            var normalised = stripped.Replace(",", string.Empty, StringComparison.Ordinal);
            if (!decimal.TryParse(
                    normalised,
                    System.Globalization.NumberStyles.Number,
                    s_usMxFormat,
                    out value))
                return false;

            // Seed the session format so the §20 detecting path can leverage the context.
            _detected ??= s_usMxFormat;
            return true;
        }
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
    public async Task<Result<StatementModel>> ExtractFullAsync(
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<StatementModel>();

        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0)
            return Result<StatementModel>.WithFailure("PDF bytes are empty.");

        // ---- Guard 1: file-size limit (issue #59 / poison-PDF DoS) ------
        if (pdf.Length > _options.MaxSizeBytes)
        {
            _logger.LogWarning(
                "PDF rejected — size {SizeBytes} bytes exceeds limit {LimitBytes} bytes (FileSizeLimitExceeded).",
                pdf.Length,
                _options.MaxSizeBytes);
            return Result<StatementModel>.WithFailure(
                $"FileSizeLimitExceeded: PDF size {pdf.Length} bytes exceeds the configured limit of {_options.MaxSizeBytes} bytes.");
        }

        // ---- Guard 2 + 3: parse timeout + password protection -----------
        // PdfPig is fully synchronous so the parse runs on a thread-pool thread.
        // A linked CTS combines the caller's token and a local timeout deadline.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.ParseTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var linkedToken = linkedCts.Token;

        try
        {
            return await Task.Run(() => ExtractFullSync(pdf), linkedToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Distinguish: was it the CALLER's token or the TIMEOUT that fired?
            if (cancellationToken.IsCancellationRequested)
                return ResultExtensions.Cancelled<StatementModel>();

            // Only the timeout CTS fired.
            _logger.LogWarning(
                "PDF parse timed out after {TimeoutSeconds} s ({ByteCount} bytes).",
                _options.ParseTimeoutSeconds,
                pdf.Length);
            return Result<StatementModel>.WithFailure(
                $"Timeout: PDF parse exceeded the configured limit of {_options.ParseTimeoutSeconds} s.");
        }
    }

    /// <summary>
    /// Synchronous full-extraction body — runs on a thread-pool thread so the caller's
    /// async timeout wrapper can cancel it when the deadline fires.
    /// Handles the password-exception path inline: if <c>PdfDocument.Open</c> throws a
    /// password-related exception the method returns a <c>PasswordProtected</c> failure result.
    /// </summary>
    private Result<StatementModel> ExtractFullSync(byte[] pdf)
    {
        try
        {
            using var doc = TryOpenDocument(pdf, out var passwordFailure);
            if (doc is null)
            {
                // Password exception was caught inside TryOpenDocument — propagate as failure.
                return passwordFailure
                    ?? Result<StatementModel>.WithFailure("PasswordProtected — add institution password to config.");
            }

            return ExtractFromOpenDocument(doc, pdf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract full fields from PDF ({ByteCount} bytes).", pdf.Length);
            return Result<StatementModel>.WithFailure($"PDF full extraction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to open a <see cref="PdfDocument"/>, handling password-protection transparently.
    /// Returns <see langword="null"/> when the document is password-protected and no password
    /// is available; in that case <paramref name="failure"/> is set to the failure result.
    /// </summary>
    private PdfDocument? TryOpenDocument(byte[] pdf, out Result<StatementModel>? failure)
    {
        failure = null;

        try
        {
            return PdfDocument.Open(pdf);
        }
        catch (Exception ex) when (IsPasswordException(ex))
        {
            // Try a configured password (per-institution, synchronous via .GetAwaiter().GetResult()
            // because we are already on a thread-pool thread — no deadlock risk).
            // StatementContextKey is not available here; we use an empty institution key so the
            // NullPasswordProvider returns a failure immediately without a lookup round-trip.
            var passwordResult = _passwordProvider.GetPasswordAsync(
                institutionKey: string.Empty,
                cancellationToken: CancellationToken.None).GetAwaiter().GetResult();

            if (passwordResult.IsSuccess && passwordResult.Value is { } password)
            {
                try
                {
                    return PdfDocument.Open(pdf, new ParsingOptions { Password = password });
                }
                catch (Exception retryEx) when (IsPasswordException(retryEx))
                {
                    // Configured password also failed — fall through to PasswordProtected.
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx,
                        "PDF open with configured password failed ({ByteCount} bytes).", pdf.Length);
                    failure = Result<StatementModel>.WithFailure(
                        $"PDF full extraction failed: {retryEx.Message}");
                    return null;
                }
            }

            _logger.LogWarning(
                "PDF is password-protected and no institution password is configured ({ByteCount} bytes).",
                pdf.Length);
            failure = Result<StatementModel>.WithFailure(
                "PasswordProtected — add institution password to config.");
            return null;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="ex"/> indicates a PDF password
    /// (encryption) requirement.  PdfPig surfaces this as an exception whose message contains
    /// "password" or "encrypt" (case-insensitive); the concrete type is internal to PdfPig.
    /// </summary>
    private static bool IsPasswordException(Exception ex)
    {
        var msg = ex.Message;
        return msg.Contains("password", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("encrypt", StringComparison.OrdinalIgnoreCase)
            || ex.GetType().Name.Contains("Encrypt", StringComparison.OrdinalIgnoreCase)
            || ex.GetType().Name.Contains("Password", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Core extraction logic operating on an already-opened <see cref="PdfDocument"/>.
    /// Extracted from <see cref="ExtractFullAsync"/> to allow reuse after a password-retry open.
    /// </summary>
    private Result<StatementModel> ExtractFromOpenDocument(PdfDocument doc, byte[] pdf)
    {
        try
        {
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
            var amtFmt = new AmountNumberFormatSession();
            var periodSummary = ExtractPeriodSummary(allPage1Words, amtFmt);

            // ---- DESGLOSE DE MOVIMIENTOS DEL PERIODO — Story 4.4 ----------
            // Scan all pages for the DESGLOSE section header, then reconstruct
            // transaction rows using Y-band grouping + X-column assignment.
            //
            // Determinism anchor (NFR-5 / gap #36): pass the statement's own period year
            // so that truncated movement-date year repair is anchored to the statement's
            // declared period, not the processing clock.  The same PDF will therefore
            // always repair truncated dates to the same year regardless of when it is
            // reprocessed (December vs. January, 2025 vs. 2026).
            var periodYearAnchor = periodSummary.PeriodCutDate.Status == ExtractionStatus.Extracted
                ? (int?)periodSummary.PeriodCutDate.Value.Year
                : null;
            var (movements, movementsStatus, totalCargos, totalAbonos) = ExtractMovements(doc, periodYearAnchor, amtFmt);

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

            // ---- Inter-section blank gaps — Story 10.2 ----------------------
            // Compute same-page vertical whitespace between consecutive present sections.
            var sectionGaps = ComputeSectionGaps(doc, detectedSections);

            // ---- Financial regulatory tables — Story 11.1 -------------------
            // Extract §8, §19, §20, §16 grids into typed rows/cells.
            // Single-pass: reuses the open doc (no second PDF open).
            var financialTables = ExtractFinancialTables(doc, detectedSections, amtFmt);

            // ---- Word-level typography samples — Epic 12 --------------------
            // Collect rendered point-size and font-name per word across all pages.
            var (typographySamples, typographyExtractionStatus) = ExtractTypographySamples(doc);

            // ---- §23 structured dispute rows — Story E10.C4′ -------------------
            // Best-effort extraction from §23 SectionText (normalized, whitespace-collapsed).
            // Uses status-token scanning + backward amount lookup.  Returns NoRowsParsed
            // when the section is absent/not-applicable or when no row pairs are found.
            var (disputeRows, disputeRowsStatus) = ExtractDisputeRows(detectedSections);

            // ---- Per-page perceptual hashes — VERIQAN-E2-S4 (CLIENT-IMG-CATALOG) ----
            // Only when the opt-in flag is enabled — rendering every page is expensive.
            // Leave the list empty (rule abstains) when the flag is off.
            var pagePerceptualHashes = _enableCatalogImageHashing
                ? ComputePagePerceptualHashes(pdf, doc)
                : (IReadOnlyList<ulong>)[];

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
                SectionGaps = sectionGaps,
                FinancialTables = financialTables,
                TypographySamples = typographySamples,
                TypographyExtractionStatus = typographyExtractionStatus,
                PagePerceptualHashes = pagePerceptualHashes,
                DisputeRows = disputeRows,
                DisputeRowsStatus = disputeRowsStatus,
            };

            return Result<StatementModel>.WithSuccess(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract full fields from PDF ({ByteCount} bytes).", pdf.Length);
            return Result<StatementModel>.WithFailure($"PDF full extraction failed: {ex.Message}");
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
    private PeriodSummary ExtractPeriodSummary(List<Word> allWords, AmountNumberFormatSession amtFmt)
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
        var pagoNoInt = ExtractPagoParaNoGenerarIntereses(sorted, bands, amtFmt);
        var pagoMinMeses = ExtractPagoMinimoMasMeses(sorted, bands, amtFmt);
        var pagoMin = ExtractPagoMinimo(sorted, bands, amtFmt);

        // ---- TASA and CAT (lower section, Y≈264) ------------------------
        // The percentage values appear on a band below the heading labels.
        // In fixture #1: "28.86% sin IVA 27.36%" at Y=264.0
        // CAT = first percent token (28.86%), TASA ORDINARIA FIJA = second (27.36%)
        var (tasa, cat) = ExtractTasaAndCat(sorted, bands);

        // ---- Saldo Deudor Total at Y≈161 --------------------------------
        var saldoDeudor = ExtractSaldoDeudorTotal(sorted, bands, amtFmt);

        // ---- Crédito Disponible at Y≈139 --------------------------------
        var creditoDisponible = ExtractCreditoDisponible(sorted, bands, amtFmt);

        // ---- RESUMEN DE CARGOS Y ABONOS DEL PERIODO (Story 4.2) ---------
        // Right-side block at Y ≈ 292–357.
        var adeudoPeriodoAnterior = ExtractResumenField(sorted, bands,
            ["Adeudo", "del", "periodo", "anterior"], amtFmt);
        var cargosRegularesNoMeses = ExtractResumenField(sorted, bands,
            ["Cargos", "regulares", "(no"], amtFmt);
        var cargosComprasAMesesCapital = ExtractResumenField(sorted, bands,
            ["Cargos", "compras", "a", "meses", "(capital)"], amtFmt);
        var montoIntereses = ExtractResumenField(sorted, bands,
            ["Monto", "de", "Intereses"], amtFmt);
        var montoComisiones = ExtractResumenField(sorted, bands,
            ["Monto", "de", "comisiones"], amtFmt);
        var ivaInteresesYComisiones = ExtractResumenField(sorted, bands,
            ["IVA", "de", "Intereses"], amtFmt);
        var pagosYAbonos = ExtractResumenField(sorted, bands,
            ["Pagos", "y", "abonos"], amtFmt);

        // ---- NIVEL DE USO DE TU TARJETA (Story 4.2) ---------------------
        // Right-side block at Y ≈ 171–183.
        var saldoCargosRegulares = ExtractNivelDeUsoField(sorted, bands,
            ["Saldo", "cargos", "regulares:"], amtFmt);
        var saldoCargosAMeses = ExtractNivelDeUsoField(sorted, bands,
            ["Saldo", "cargos", "a", "meses:"], amtFmt);

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
        // Dummie VEC layout:  "Periodo 5-jul-2025 al 04-ago-2025"  (X < 100, left column)
        // Real Banamex layout: "Periodo: 4-mar-2026 al 01-abr-2026" (X ≈ 309, centre column)
        // Both are handled by accepting "Periodo" or "Periodo:" with no column restriction.
        foreach (var w in sorted)
        {
            if (!string.Equals(w.Text.TrimEnd(':'), "Periodo", StringComparison.OrdinalIgnoreCase))
                continue;

            var bandY = w.BoundingBox.Bottom;
            var band = GetBand(bands, bandY);
            var locator = BoundingBoxOf(band, 1);

            // Tokens after "Periodo[:]": date1, "al", date2
            var idx = band.FindIndex(x =>
                string.Equals(x.Text.TrimEnd(':'), "Periodo", StringComparison.OrdinalIgnoreCase));
            if (idx < 0 || idx + 1 >= band.Count)
                return ExtractedField<DateOnly>.Missing(locator);

            // First token after "Periodo[:]" is the start date.
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
        // Dummie VEC:  "Número de días en el periodo: 31 días"  (X < 100, left column)
        // Real layout: "Número de días en el periodo: 29 días"  (X ≈ 309, centre column)
        // Discriminator: the band must contain "días" — unique to the day-count row.
        for (var i = 0; i + 1 < sorted.Count; i++)
        {
            if (!string.Equals(sorted[i].Text, "Número", StringComparison.OrdinalIgnoreCase))
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
        // Dummie VEC:  "Fecha de Corte 04 de ago 2025"  (separate "Corte" token)
        // Real layout: "Fecha de corte: 01-abr-2026"    ("corte:" with fused colon)
        // TrimEnd(':') normalises both variants for matching.
        for (var i = 0; i + 2 < sorted.Count; i++)
        {
            if (!string.Equals(sorted[i].Text, "Fecha", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(sorted[i + 1].Text, "de", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(sorted[i + 2].Text.TrimEnd(':'), "Corte", StringComparison.OrdinalIgnoreCase))
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
        Dictionary<double, List<Word>> bands,
        AmountNumberFormatSession amtFmt)
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
            return FindAmountInBand(band, locator, amtFmt, maxX: 300);
        }

        return ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Pago mínimo + compras y cargos diferidos a meses
    // -----------------------------------------------------------------------

    private static ExtractedField<decimal> ExtractPagoMinimoMasMeses(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands,
        AmountNumberFormatSession amtFmt)
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

            return FindAmountInBand(band, locator, amtFmt, maxX: 300);
        }

        return ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Pago mínimo (alone)
    // -----------------------------------------------------------------------

    private static ExtractedField<decimal> ExtractPagoMinimo(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands,
        AmountNumberFormatSession amtFmt)
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

            // Dummie VEC: value is close to the label (X ≤ 300).
            // Real layout: value is in the far-right column (X ≈ 528).
            // Try constrained first; fall back to unconstrained if nothing found.
            var result = FindAmountInBand(band, locator, amtFmt, maxX: 300);
            if (result.Status != ExtractionStatus.Extracted)
                result = FindAmountInBand(band, locator, amtFmt);
            return result;
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
        Dictionary<double, List<Word>> bands,
        AmountNumberFormatSession amtFmt)
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

            return FindAmountInBandSplitDollar(band, locator, amtFmt);
        }

        return ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Crédito Disponible
    // -----------------------------------------------------------------------

    private static ExtractedField<decimal> ExtractCreditoDisponible(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands,
        AmountNumberFormatSession amtFmt)
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

            var result = FindAmountInBandSplitDollar(band, locator, amtFmt);
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
    /// <param name="amtFmt">Session-scoped number-format detector.</param>
    /// <returns>
    /// An <see cref="ExtractedField{T}"/> containing the parsed amount
    /// (<see cref="ExtractionStatus.Extracted"/>), or
    /// <see cref="ExtractedField{T}.InvalidFormat"/> when the label was found but the
    /// adjacent amount could not be parsed (e.g. OCR-corrupted digit or unusual locale),
    /// or <see cref="ExtractedField{T}.Missing"/> only when the label was not found in
    /// either column.
    /// </returns>
    private static ExtractedField<decimal> ExtractResumenField(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands,
        string[] labelTokens,
        AmountNumberFormatSession amtFmt)
    {
        // RESUMEN rows appear in either the right column (Dummie VEC, label X ≥ ~280,
        // amount X ≥ 430) or the left column (real Banamex Visa layout, label X ≈ 25,
        // amount X ≈ 200).
        //
        // Two-pass strategy:
        //   Pass 1 — RIGHT column (original behaviour): label at X ≥ 280, amount unconstrained.
        //   Pass 2 — LEFT column (real Banamex Visa fallback): label at X < 280, amount
        //            capped at X ≤ 280 so we don't accidentally pick up a right-column amount
        //            that leaked into the band via Y-band tolerance.
        //
        // Result precedence (F1 honesty fix):
        //   Extracted         → return immediately (short-circuit after pass 1).
        //   ExtractedInvalidFormat → label was seen but amount unparseable; remember and continue.
        //   NotExtracted      → label absent in this column; continue to next pass.
        //   If EITHER pass produced ExtractedInvalidFormat and NEITHER was Extracted, return
        //   the InvalidFormat result so the caller knows "label found, amount unreadable" —
        //   distinct from "label never appeared" (NotExtracted/Missing).

        var rightResult = ScanResumenColumn(sorted, bands, labelTokens, amtFmt,
            labelMinX: 280.0, labelMaxX: double.MaxValue, amtMaxX: double.MaxValue);
        if (rightResult.Status == ExtractionStatus.Extracted)
            return rightResult;

        var leftResult = ScanResumenColumn(sorted, bands, labelTokens, amtFmt,
            labelMinX: 0.0, labelMaxX: 280.0, amtMaxX: 280.0);
        if (leftResult.Status == ExtractionStatus.Extracted)
            return leftResult;

        // Neither pass produced a clean Extracted value.  Prefer InvalidFormat over Missing
        // so the distinction is not lost: "label present, amount unreadable" ≠ "label absent".
        if (rightResult.Status == ExtractionStatus.ExtractedInvalidFormat)
            return rightResult;
        if (leftResult.Status == ExtractionStatus.ExtractedInvalidFormat)
            return leftResult;

        return ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
    }

    /// <summary>
    /// Inner helper for <see cref="ExtractResumenField"/>: scans <paramref name="sorted"/>
    /// for a RESUMEN label within [labelMinX, labelMaxX) and returns the first leftmost amount
    /// in [labelRight, amtMaxX].
    /// </summary>
    /// <returns>
    /// <see cref="ExtractionStatus.Extracted"/> when the label and a parseable amount were found.
    /// <see cref="ExtractionStatus.ExtractedInvalidFormat"/> when the label was matched at least
    /// once but no parseable amount was found in any matching band — the raw value is <c>0m</c>
    /// (not consumed by callers in this path; chosen to satisfy the non-null type constraint).
    /// <see cref="ExtractionStatus.NotExtracted"/> (<see cref="ExtractedField{T}.Missing"/>) when
    /// the label did not appear in this column at all.
    /// </returns>
    private static ExtractedField<decimal> ScanResumenColumn(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands,
        string[] labelTokens,
        AmountNumberFormatSession amtFmt,
        double labelMinX,
        double labelMaxX,
        double amtMaxX)
    {
        // Track whether we found at least one complete label match but could not parse the amount
        // (case b: label typeset, amount OCR-corrupted or locale-unrecognisable).
        // Distinct from case (a): label never appeared (Banamex zero-row suppression → NotExtracted).
        FieldLocator? invalidFormatLocator = null;

        for (var i = 0; i + labelTokens.Length - 1 < sorted.Count; i++)
        {
            if (sorted[i].BoundingBox.Left < labelMinX || sorted[i].BoundingBox.Left >= labelMaxX)
                continue;

            if (!string.Equals(sorted[i].Text, labelTokens[0], StringComparison.OrdinalIgnoreCase))
                continue;

            var bandY = sorted[i].BoundingBox.Bottom;
            var band = GetBand(bands, bandY);
            var bandSorted = band.OrderBy(x => x.BoundingBox.Left).ToList();

            // Locate the first label token in the expected column.
            var labelStart = bandSorted.FindIndex(x =>
                string.Equals(x.Text, labelTokens[0], StringComparison.OrdinalIgnoreCase)
                && x.BoundingBox.Left >= labelMinX
                && x.BoundingBox.Left < labelMaxX);

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

            // Amount must be to the right of the label and within the column ceiling.
            // findLeftmost=true: picks the amount nearest to the label, not the one
            // farthest right, so cross-column amounts that leaked in via Y-band tolerance
            // are naturally skipped (they're farther right).
            var labelRight = bandSorted[labelStart + labelTokens.Length - 1].BoundingBox.Right;
            var locator = BoundingBoxOf(band, 1);

            var result = FindAmountInBandSplitDollar(band, locator, amtFmt,
                minX: labelRight, maxX: amtMaxX, findLeftmost: true);
            if (result.Status == ExtractionStatus.Extracted)
                return result;

            // Label matched but amount did not parse.  Remember the locator so we can
            // return InvalidFormat at the end rather than Missing (F1 honesty fix).
            invalidFormatLocator ??= locator;
        }

        // If any label match was found without a parseable amount, signal InvalidFormat.
        // The raw value 0m is a placeholder — callers (CL-21 rule) check Status first and
        // abstain on InvalidFormat before ever reading the value.
        if (invalidFormatLocator is not null)
            return ExtractedField<decimal>.InvalidFormat(0m, invalidFormatLocator);

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
    /// <param name="amtFmt">Session-scoped number-format detector.</param>
    private static ExtractedField<decimal> ExtractNivelDeUsoField(
        List<Word> sorted,
        Dictionary<double, List<Word>> bands,
        string[] labelTokens,
        AmountNumberFormatSession amtFmt)
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
            return FindAmountInBandSplitDollar(band, locator, amtFmt);
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
    /// <param name="periodYear">
    /// Statement period year anchored from <c>PeriodCutDate</c> (NFR-5 / gap #36).
    /// Passed through to <see cref="TryParseMovementRow"/> as the primary reference
    /// for truncated-date year-repair.  <see langword="null"/> when the period date
    /// was not extracted; the fallback hierarchy in <c>TryParseMovementRow</c> applies.
    /// </param>
    /// <param name="amtFmt">Session-scoped number-format detector shared across all extraction calls.</param>
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
        ExtractMovements(PdfDocument doc, int? periodYear, AmountNumberFormatSession amtFmt)
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
                var row = TryParseMovementRow(bandWords, pageIndex, periodYear, amtFmt);

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
                var totalResult = TryParseTotalRow(bandWords, pageIndex, amtFmt);
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
    /// <param name="amtFmt">Session-scoped number-format detector.</param>
    /// <returns>
    /// A tuple of (isCharge, <see cref="ExtractedField{T}"/> amount) when the band matches
    /// a total row; <see langword="null"/> otherwise.
    /// </returns>
    private static (bool isCharge, ExtractedField<decimal> amount)?
        TryParseTotalRow(List<Word> bandWords, int pageNumber, AmountNumberFormatSession amtFmt)
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
            if (TryParseAmount(m.Groups[1].Value, amtFmt, out var parsed))
                return (isCharge.Value, ExtractedField<decimal>.Found(parsed, locator));
        }

        return null;
    }

    /// <summary>
    /// Attempts to parse a Y-band as a DESGLOSE data row.
    /// Returns <see langword="null"/> for non-data bands (headers, footers, totals, FX rows).
    /// </summary>
    /// <param name="bandWords">Words on the candidate band.</param>
    /// <param name="pageNumber">Page number for the locator.</param>
    /// <param name="periodYear">
    /// The statement's own period year (from <c>PeriodCutDate</c> or <c>PeriodStart</c>),
    /// used as the primary anchor for truncated-date year-repair (NFR-5 / gap #36).
    /// When <see langword="null"/> the fallback hierarchy applies: operation-date year →
    /// <c>_timeProvider.GetUtcNow().Year</c>.
    /// </param>
    /// <param name="amtFmt">Session-scoped number-format detector.</param>
    private StatementMovement? TryParseMovementRow(List<Word> bandWords, int pageNumber, int? periodYear, AmountNumberFormatSession amtFmt)
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
                // Attempt year-repair: "07-jul-202" → try appending the missing last digit.
                //
                // Anchor precedence (NFR-5 / gap #36 — determinism across reprocessing):
                //   1. Statement period year (periodYear parameter, from PeriodCutDate).
                //      The same PDF has the same period regardless of when it is processed,
                //      so this anchor makes year-repair fully deterministic: the same
                //      statement reprocessed in December 2025 and January 2026 produces
                //      the same repaired year.
                //   2. Operation-date year (same row — already parsed from this band).
                //      Keeps intra-row consistency when the period anchor is unavailable.
                //   3. TimeProvider local-date year (America/Mexico_City) — last resort /
                //      wall-clock fallback.  Used only when neither (1) nor (2) is available
                //      (e.g. synthetic PDFs in tests that provide no period header, or
                //      statements with corrupt period fields).
                //      The instant is converted to Mexico City local time before taking the
                //      year so that a Dec 31 cut-date at 23:30 local (= Jan 1 UTC) repairs
                //      to the correct local year rather than the UTC year (off-by-one risk).
                //
                // NOTE: periodYear is NOT used to repair the period cut-date itself —
                // that field is extracted independently from the header (ExtractFechaDeCorte)
                // before ExtractMovements is called. Repairing movement dates with the
                // period year is therefore not circular.
                var clockLocalDate = TimeZoneInfo.ConvertTime(
                    _timeProvider.GetUtcNow(),
                    ExxerCube.Prisma.Veriqan.Domain.VeriqanConstants.MexicoCityTimezone);
                var repairedYear = periodYear ?? operationDate?.Year ?? clockLocalDate.Year;
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
            if (TryParseAmount(m.Groups[1].Value, amtFmt, out var parsed))
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
    /// Parses a raw amount token (may include leading "$") whose shape has already been
    /// validated by <c>AmountPattern</c> / <c>DesgloseAmountPattern</c>, which guarantees
    /// US/MX format (comma = thousands, period = decimal).  Delegates to
    /// <see cref="AmountNumberFormatSession.TryParseUsMx"/> so it never abstains on format
    /// grounds — including statements whose amounts are all &lt; $1,000 (no thousands comma).
    /// </summary>
    /// <remarks>
    /// Do <em>not</em> call this for raw, unfiltered text from the §20 path
    /// (<c>ParseSignedAmountCell</c> / <c>BuildSection20ValueCells</c>); those sites may
    /// encounter European-format amounts and must use
    /// <see cref="AmountNumberFormatSession.TryParse"/> directly.
    /// </remarks>
    private static bool TryParseAmount(
        string rawToken,
        AmountNumberFormatSession session,
        out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(rawToken))
            return false;

        var stripped = rawToken.TrimStart('+', '-', '−', '$');
        return session.TryParseUsMx(stripped, out value);
    }

    /// <summary>
    /// Finds and parses the rightmost amount token on a band.
    /// Amount tokens match <see cref="AmountPattern"/> (e.g. "$32,446.69", "2,160.00").
    /// Footnote markers (single digits not matching amount pattern) are skipped.
    /// </summary>
    /// <param name="band">Words on the target band, sorted left-to-right.</param>
    /// <param name="locator">Fallback locator for Missing results.</param>
    /// <param name="amtFmt">Session-scoped number-format detector.</param>
    /// <param name="maxX">
    /// Optional upper bound on <c>BoundingBox.Left</c> for candidate amount tokens.
    /// Pass a value (e.g. 300) to restrict to the left column and avoid picking up
    /// right-column numeric text (CLABE, account numbers) that landed on the same band
    /// due to band-merging tolerance.  Defaults to <c>double.MaxValue</c> (no filter).
    /// </param>
    /// <param name="minX">
    /// Optional lower bound on <c>BoundingBox.Left</c> for candidate amount tokens.
    /// Pass the right edge of the label to avoid picking up amounts that belong to a
    /// different column that merged into this band via Y-band tolerance.
    /// Defaults to <c>0.0</c> (no filter).
    /// </param>
    /// <param name="findLeftmost">
    /// When <see langword="true"/> returns the leftmost matching amount token instead of
    /// the rightmost.  Use <see langword="true"/> for RESUMEN/label-adjacent amounts to
    /// avoid picking up cross-column amounts that leaked into the band.
    /// Defaults to <see langword="false"/> (rightmost, consistent with original behaviour).
    /// </param>
    private static ExtractedField<decimal> FindAmountInBand(
        List<Word> band,
        FieldLocator locator,
        AmountNumberFormatSession amtFmt,
        double maxX = double.MaxValue,
        double minX = 0.0,
        bool findLeftmost = false)
    {
        // Find the rightmost (or leftmost when requested) token matching AmountPattern
        // within the X constraints.
        //
        // Use findLeftmost=true when extracting amounts that are immediately adjacent to
        // a label (e.g. RESUMEN rows), to avoid picking up cross-column amounts that
        // merged into the band via Y-band tolerance.  Single-digit tokens are excluded
        // in leftmost mode because they are footnote markers (e.g. "8"), not amounts.
        var candidates = band
            .Where(x => x.BoundingBox.Left >= minX
                     && x.BoundingBox.Left <= maxX
                     && AmountPattern.IsMatch(x.Text)
                     && !(findLeftmost && IsSingleDigit(x.Text)));

        var amountWord = findLeftmost
            ? candidates.OrderBy(x => x.BoundingBox.Left).FirstOrDefault()
            : candidates.OrderByDescending(x => x.BoundingBox.Left).FirstOrDefault();

        if (amountWord is null)
            return ExtractedField<decimal>.Missing(locator);

        return ParseAmountToken(amountWord.Text, BoundingBoxOf([amountWord], 1), amtFmt);
    }

    /// <summary>
    /// Finds and parses an amount where the "$" sign and digits may be separate tokens
    /// (e.g. "$ 52,387.85" → two tokens "$" and "52,387.85").
    /// </summary>
    /// <param name="band">Words on the target band, sorted left-to-right.</param>
    /// <param name="locator">Fallback locator for Missing results.</param>
    /// <param name="amtFmt">Session-scoped number-format detector.</param>
    /// <param name="minX">
    /// Optional lower bound on <c>BoundingBox.Left</c> for candidate amount tokens.
    /// Pass the right edge of the label to avoid picking up amounts that belong to
    /// a different column that merged into this band via Y-band tolerance.
    /// Defaults to <c>0.0</c> (no filter).
    /// </param>
    /// <param name="maxX">
    /// Optional upper bound on <c>BoundingBox.Left</c> for candidate amount tokens.
    /// Use this to restrict the amount search to a specific column.
    /// Defaults to <c>double.MaxValue</c> (no filter).
    /// </param>
    /// <param name="findLeftmost">
    /// When <see langword="true"/> selects the leftmost eligible amount token rather than
    /// the rightmost.  Propagated to <see cref="FindAmountInBand"/>.
    /// Defaults to <see langword="false"/>.
    /// </param>
    private static ExtractedField<decimal> FindAmountInBandSplitDollar(
        List<Word> band,
        FieldLocator locator,
        AmountNumberFormatSession amtFmt,
        double minX = 0.0,
        double maxX = double.MaxValue,
        bool findLeftmost = false)
    {
        // Try combined amount tokens first (respecting minX / maxX / findLeftmost).
        var combined = FindAmountInBand(band, locator, amtFmt,
            maxX: maxX, minX: minX, findLeftmost: findLeftmost);
        if (combined.Status == ExtractionStatus.Extracted)
            return combined;

        // Look for a bare "$" followed by a numeric token within [minX, maxX].
        // When findLeftmost, pick the first "$" to the right of minX.
        var dollarCandidates = band.Where(x => x.Text == "$"
            && x.BoundingBox.Left >= minX && x.BoundingBox.Left <= maxX);
        var dollarWord = findLeftmost
            ? dollarCandidates.OrderBy(x => x.BoundingBox.Left).FirstOrDefault()
            : dollarCandidates.OrderByDescending(x => x.BoundingBox.Left).FirstOrDefault();
        var dollarIdx = dollarWord is not null ? band.IndexOf(dollarWord) : -1;
        if (dollarIdx >= 0 && dollarIdx + 1 < band.Count)
        {
            var numToken = band[dollarIdx + 1];
            // Skip single-digit footnote markers.
            if (!IsSingleDigit(numToken.Text))
            {
                if (TryParseAmount(numToken.Text, amtFmt, out var val))
                    return ExtractedField<decimal>.Found(val, BoundingBoxOf([band[dollarIdx], numToken], 1));
            }

            // If there's a footnote marker between "$" and the number, try skipping it.
            if (dollarIdx + 2 < band.Count && IsSingleDigit(band[dollarIdx + 1].Text))
            {
                var numToken2 = band[dollarIdx + 2];
                if (TryParseAmount(numToken2.Text, amtFmt, out var val2))
                    return ExtractedField<decimal>.Found(val2, BoundingBoxOf([band[dollarIdx], numToken2], 1));
            }
        }

        return ExtractedField<decimal>.Missing(locator);
    }

    private static ExtractedField<decimal> ParseAmountToken(string token, FieldLocator locator, AmountNumberFormatSession amtFmt)
    {
        var m = AmountPattern.Match(token);
        if (!m.Success)
            return ExtractedField<decimal>.Missing(locator);

        if (TryParseAmount(m.Groups[1].Value, amtFmt, out var val))
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
    // Client name extraction (positional — right column first, then left column
    // fallback for statements where the name sits at low X values)
    // -----------------------------------------------------------------------

    private static ExtractedField<ExtractedClientName> ExtractClientName(List<Word> headerWords)
    {
        // Primary strategy: right column (Dummie VEC / classic layout, X ≥ ClientNameXMin).
        var nameWords = headerWords
            .Where(w => w.BoundingBox.Left >= ClientNameXMin
                     && w.BoundingBox.Bottom >= ClientNameYMin)
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ToList();

        if (nameWords.Count > 0)
        {
            var topY = nameWords[0].BoundingBox.Bottom;
            var nameBandWords = nameWords
                .Where(w => Math.Abs(w.BoundingBox.Bottom - topY) <= YBandTolerance)
                .OrderBy(w => w.BoundingBox.Left)
                .ToList();

            var fullName = string.Join(" ", nameBandWords.Select(w => w.Text)).Trim();
            // If the extracted text contains digits it is likely a date ("4-mar-2026") or
            // account number rather than a client name — fall through to the left-column path.
            if (!string.IsNullOrWhiteSpace(fullName) && !fullName.Any(char.IsDigit))
                return BuildClientNameField(fullName, nameBandWords);
        }

        // Fallback strategy: left column (real Banamex Visa layout, X < 200).
        // The client name lives at the highest Y in that area (top of the header block).
        var leftNameWords = headerWords
            .Where(w => w.BoundingBox.Left < 200
                     && w.BoundingBox.Bottom >= ClientNameYMin)
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ToList();

        if (leftNameWords.Count > 0)
        {
            var leftTopY = leftNameWords[0].BoundingBox.Bottom;
            var leftBandWords = leftNameWords
                .Where(w => Math.Abs(w.BoundingBox.Bottom - leftTopY) <= YBandTolerance)
                // Filter out single-digit tokens (vertical account-number digits printed at
                // the page margin that land on the same Y band as the client name).
                .Where(w => !IsSingleDigit(w.Text))
                .OrderBy(w => w.BoundingBox.Left)
                .ToList();

            var leftName = string.Join(" ", leftBandWords.Select(w => w.Text)).Trim();
            if (!string.IsNullOrWhiteSpace(leftName) && !leftName.Any(char.IsDigit))
                return BuildClientNameField(leftName, leftBandWords);
        }

        return ExtractedField<ExtractedClientName>.Missing(FieldLocator.PageHint(1));
    }

    private static ExtractedField<ExtractedClientName> BuildClientNameField(
        string fullName, List<Word> nameBandWords)
    {
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
    // Generic labeled-field extraction (header zone — handles both right-column
    // and left-column layouts)
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

                // Value must be to the right of the label (no fixed column minimum —
                // handles both the right-column Dummie VEC layout and the left-column
                // real Banamex Visa layout where labels are at X≈25 and values at X≈102).
                var valueWords = sorted
                    .Where(w => w.BoundingBox.Left > labelRight)
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
    /// Collects word-level typography samples from every page of the PDF (Epic 12).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses PdfPig's built-in word grouper (<c>page.GetWords()</c>) to reconstruct words
    /// from individual letter glyphs.  For each word, the rendered point size and raw font
    /// name are taken from the word's first letter (<c>word.Letters[0]</c>) — this is
    /// consistent with how <c>ExtractFontRuns</c> reads per-letter metrics.
    /// </para>
    /// <para>
    /// <see cref="TextTypographySample.PointSize"/> is the CTM-accounted rendered size
    /// as reported by PdfPig's <c>letter.PointSize</c> property.
    /// </para>
    /// <para>
    /// Never throws — individual word failures are silently skipped (mirror of the
    /// <c>ExtractFontRuns</c> "individual letter failures silently skipped" contract).
    /// </para>
    /// </remarks>
    /// <param name="doc">The open <see cref="PdfDocument"/>.</param>
    /// <returns>
    /// A tuple of the word-level typography sample list and the extraction status.
    /// </returns>
    private static (IReadOnlyList<TextTypographySample> samples, TypographyExtractionStatus status)
        ExtractTypographySamples(PdfDocument doc)
    {
        var samples = new List<TextTypographySample>();
        var anyWordFound = false;

        for (var pageIndex = 1; pageIndex <= doc.NumberOfPages; pageIndex++)
        {
            UglyToad.PdfPig.Content.Page page;
            try
            {
                page = doc.GetPage(pageIndex);
            }
            catch (Exception)
            {
                // Skip unreadable pages (silently — mirrors ExtractFontRuns behaviour).
                continue;
            }

            foreach (var word in page.GetWords())
            {
                // Skip empty words and pure-whitespace tokens.
                if (word.Letters.Count == 0 || string.IsNullOrWhiteSpace(word.Text))
                    continue;

                anyWordFound = true;

                var firstLetter = word.Letters[0];
                var rawFontName = firstLetter.FontName ?? string.Empty;
                var pointSize = firstLetter.PointSize;
                var isBold = rawFontName.Contains("bold", StringComparison.OrdinalIgnoreCase);

                var bb = word.BoundingBox;
                var locator = new FieldLocator(
                    pageIndex,
                    bb.BottomLeft.X,
                    bb.BottomLeft.Y,
                    bb.Width,
                    bb.Height);

                samples.Add(new TextTypographySample(
                    Text: word.Text,
                    PointSize: pointSize,
                    FontName: rawFontName,
                    IsBold: isBold,
                    PageNumber: pageIndex,
                    Locator: locator));
            }
        }

        if (!anyWordFound)
            return (Array.Empty<TextTypographySample>(), TypographyExtractionStatus.NotFound);

        return (samples, TypographyExtractionStatus.Extracted);
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

                // S11 (CL-48): filter out whitespace-only / empty tokens before HasContent decision.
                // PdfPig occasionally emits Word objects whose Text is entirely whitespace or empty
                // (invisible glyphs, zero-width spaces, pure-space runs).  These must not make a
                // visually blank page report HasContent = true.
                var visibleWords = words
                    .Where(static w => !string.IsNullOrWhiteSpace(w.Text))
                    .ToList();

                // HasContent: at least one visible (non-whitespace) word on the page.
                var hasContent = visibleWords.Count > 0;

                // S11 (CL-48): compute the maximum intra-page vertical gap between consecutive
                // content lines.  Steps:
                //   1. Group visibleWords into Y-bands using the extractor's YBandTolerance (5 pt).
                //   2. For each band, take the representative Y as the maximum Bottom (top of the
                //      band in PdfPig's bottom-left coordinate system) and the minimum Bottom as
                //      the low edge — so band span = max(Bottom) − min(Bottom) of words in band.
                //      We use a single representative Y per band (the average Bottom of words in
                //      that band); gap = distance between the top of one band and the bottom of
                //      the next band above it.
                //   3. Sort bands descending by Y (top-to-bottom reading order).
                //   4. A gap between band[i] and band[i+1] = bandTop[i+1] − bandBottom[i]
                //      where bandTop = max(BoundingBox.Top) and bandBottom = min(BoundingBox.Bottom)
                //      within that band.  We measure from the TOP of the lower band to the BOTTOM
                //      of the upper band (the white space between them).
                //   5. Margins are NOT counted: only inter-line gaps between content lines matter;
                //      the space above the first line or below the last line is ignored.
                var maxVerticalGapPoints = ComputeMaxVerticalGap(visibleWords);

                // ImageCount: number of embedded images (proxy for logo).
                var imageCount = page.GetImages().Count();

                // Collect all page text (stripped of spaces, lower-case) for card-number check.
                var pageTextStripped = string.Concat(words.Select(w => w.Text))
                    .Replace(" ", string.Empty, StringComparison.Ordinal)
                    .ToLowerInvariant();

                // Raw page text (spaces preserved) for masked card-group pattern matching.
                var pageTextRaw = string.Join(" ", words.Select(w => w.Text));

                // ContainsCardNumber: full 16-digit match, OR masked last-4 via 4-digit-group pattern.
                // The pattern guard prevents a stray 4-digit sequence (e.g. a year "2024" or date
                // fragment "12/34") from satisfying the last-4 check — the last-4 must appear as
                // the final group of a "XXXX XXXX XXXX DDDD" formatted card presentation.
                bool containsCardNumber;
                if (string.IsNullOrEmpty(cardDigits))
                {
                    containsCardNumber = false;
                }
                else
                {
                    var cardDigitsLower = cardDigits.ToLowerInvariant();

                    // Primary check: all 16 digits present in page text (normal, unmasked card).
                    var fullMatch = pageTextStripped.Contains(
                        cardDigitsLower, StringComparison.OrdinalIgnoreCase);

                    // Fallback check: last-4 digits appear as the final group of a masked-card
                    // pattern (e.g. "XXXX XXXX XXXX 1234" or "**** **** **** 1234").
                    // Only runs when the full match fails (masked pages, graphic-only header).
                    // Routed through TryMatchMaskedCard so the logic is unit-testable without a PDF.
                    var last4 = cardDigits.Length >= 4
                        ? cardDigits[^4..]
                        : cardDigits;
                    var maskedMatch = !fullMatch && last4.Length == 4
                        && TryMatchMaskedCard(pageTextRaw, last4);

                    containsCardNumber = fullMatch || maskedMatch;
                }

                // PaginationCurrent / PaginationTotal: parse "N de M" preferring the
                // footer band (bottom 10% of page height) to avoid false matches from
                // body phrases such as "5 de 10 pagos".
                // PdfPig uses a bottom-left coordinate origin, so "bottom 10%" means
                // word.BoundingBox.Bottom < page.Height * 0.10.
                // Within the footer band, prefer the LAST match (rightmost/lowest) as
                // an additional safeguard against incidental text in the band.
                //
                // FALLBACK: if the footer band contains no valid "N de M" match (e.g.
                // the printer placed the page number at Y ≈ 11–13% of page height,
                // just above the 10% threshold), we fall back to a full-page scan so
                // that recall is at least as good as the pre-S7 page-wide approach.
                // This means the footer band is a PREFERENCE, not a hard filter.
                var footerYThreshold = page.Height * 0.10;
                var footerWords = words
                    .Where(w => w.BoundingBox.Bottom < footerYThreshold)
                    .ToList();
                var footerText = string.Join(" ", footerWords.Select(w => w.Text));
                int? paginationCurrent = null;
                int? paginationTotal = null;

                // Step 1 — try footer band first.
                var footerMatches = PaginationPattern.Matches(footerText);
                var paginationMatch = footerMatches.Count > 0
                    ? footerMatches[footerMatches.Count - 1]
                    : null;

                // Step 2 — fall back to page-wide scan if footer band had no hit.
                // Build a space-joined full-page string (same format as footerText) so the
                // regex can match "N de M" even when the label sits just above the 10% band.
                if (paginationMatch is null)
                {
                    var fullPageText = string.Join(" ", words.Select(w => w.Text));
                    var pageWideMatches = PaginationPattern.Matches(fullPageText);
                    if (pageWideMatches.Count > 0)
                        paginationMatch = pageWideMatches[pageWideMatches.Count - 1];
                }

                if (paginationMatch is not null
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

                // Story 10.1: capture page geometry (PDF points).
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
                    Height: pageHeight,
                    MaxVerticalGapPoints: maxVerticalGapPoints));
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
    // Vertical-gap computation (Story S11 — CL-48 "sin espacio > 2 cm")
    // -----------------------------------------------------------------------

    /// <summary>
    /// Computes the maximum vertical gap (in PDF points) between consecutive content
    /// lines on a page, for the CL-48 "sin espacio en blanco mayor a 2 cm" check.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Algorithm:
    /// <list type="number">
    ///   <item>Group the supplied visible words into Y-bands using <see cref="YBandTolerance"/>
    ///         (5 pt), same grouping logic used throughout the extractor.</item>
    ///   <item>For each band, record the band's <c>top</c> (maximum <c>BoundingBox.Top</c>)
    ///         and <c>bottom</c> (minimum <c>BoundingBox.Bottom</c>) from its words.</item>
    ///   <item>Sort bands descending by their representative Y (top-down reading order).</item>
    ///   <item>For each consecutive pair of bands, the gap is:
    ///         <c>upperBand.bottom − lowerBand.top</c>.  In PdfPig's bottom-left coordinate
    ///         system this is the white space between the two ink rows.</item>
    ///   <item>Return the maximum positive gap.  Negative gaps (overlapping bands) are
    ///         clamped to zero.  Margins (above first line, below last line) are NOT counted.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Returns <c>0.0</c> when <paramref name="visibleWords"/> has fewer than two distinct
    /// Y-bands (i.e. a single-line page or a blank page).
    /// </para>
    /// </remarks>
    /// <param name="visibleWords">
    /// Words already filtered to exclude whitespace-only tokens (see caller).
    /// </param>
    /// <returns>Maximum inter-line vertical gap in PDF points, or 0.0.</returns>
    private static double ComputeMaxVerticalGap(List<UglyToad.PdfPig.Content.Word> visibleWords)
    {
        if (visibleWords.Count < 2)
            return 0.0;

        // Build band map: representative Y → list of words in that band.
        // Reuse the same greedy first-match grouping as GroupIntoBandsWithTolerance.
        var bandMap = new Dictionary<double, (double BandTop, double BandBottom)>();

        foreach (var word in visibleWords)
        {
            var wordBottom = word.BoundingBox.Bottom;
            var wordTop = word.BoundingBox.Top;

            var matched = false;
            foreach (var key in bandMap.Keys)
            {
                if (Math.Abs(key - wordBottom) <= YBandTolerance)
                {
                    var existing = bandMap[key];
                    bandMap[key] = (
                        BandTop: Math.Max(existing.BandTop, wordTop),
                        BandBottom: Math.Min(existing.BandBottom, wordBottom));
                    matched = true;
                    break;
                }
            }

            if (!matched)
                bandMap[wordBottom] = (BandTop: wordTop, BandBottom: wordBottom);
        }

        if (bandMap.Count < 2)
            return 0.0;

        // Sort bands top-to-bottom: descending by representative Y key (= word Bottom baseline).
        var sortedBands = bandMap
            .OrderByDescending(static kv => kv.Key)
            .Select(static kv => kv.Value)
            .ToList();

        // Measure the white space between consecutive bands: gap = upperBand.bottom - lowerBand.top.
        var maxGap = 0.0;
        for (var i = 0; i < sortedBands.Count - 1; i++)
        {
            var gap = sortedBands[i].BandBottom - sortedBands[i + 1].BandTop;
            if (gap > maxGap)
                maxGap = gap;
        }

        return maxGap;
    }

    // -----------------------------------------------------------------------
    // Normalized full text (Story 6.1 — CL-32/46)
    // -----------------------------------------------------------------------

    // -----------------------------------------------------------------------
    // Per-page perceptual hashing (VERIQAN-E2-S4 — CLIENT-IMG-CATALOG)
    // -----------------------------------------------------------------------

    /// <summary>
    /// DPI at which pages are rendered for perceptual hashing (VERIQAN-E2-S4).
    /// 72 DPI gives a ~595×842 raster for a typical A4 PDF page — more than enough
    /// for a 64×64 pHash DCT pass.  Keep lower than <see cref="FiscalPageRenderDpi"/>
    /// to limit memory pressure when hashing all pages.
    /// </summary>
    private const int PerceptualHashRenderDpi = 72;

    /// <summary>
    /// Renders every page of <paramref name="doc"/> via PDFtoImage (SkiaSharp path) and
    /// computes a CoenM <c>PerceptualHash</c> (64-bit ulong) for each, returning one hash
    /// per page ordered by 1-based page number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rendering strategy: the same <c>Conversion.ToImage</c> SkiaSharp overload used by
    /// <see cref="ExtractFiscalBlock"/> is reused here to keep the native-library dependency
    /// surface consistent (no second renderer required).  The resulting <c>SKBitmap.Bytes</c>
    /// (BGRA32 row-major) is reinterpreted as <c>Image&lt;Bgra32&gt;</c> via
    /// <c>Image.LoadPixelData&lt;Bgra32&gt;</c> and then cloned to <c>Image&lt;Rgba32&gt;</c>
    /// before being fed to <c>PerceptualHash.Hash</c>.
    /// </para>
    /// <para>
    /// Per-page failures are silently skipped (best-effort): a render or hash error on one page
    /// does NOT fail extraction — a zero placeholder is NOT inserted; the page is simply omitted.
    /// The <c>CatalogImagePresenceRule</c> already handles a partial list gracefully.
    /// </para>
    /// </remarks>
    /// <param name="pdfBytes">Raw PDF bytes — passed to PDFtoImage (requires a stream).</param>
    /// <param name="doc">Open PdfPig document — used only to read <c>NumberOfPages</c>.</param>
    /// <returns>
    /// A list of ulong perceptual hashes, one per successfully rendered page,
    /// ordered by 1-based page index.  Returns an empty list on complete failure.
    /// </returns>
    private IReadOnlyList<ulong> ComputePagePerceptualHashes(byte[] pdfBytes, UglyToad.PdfPig.PdfDocument doc)
    {
        var hashes = new List<ulong>(doc.NumberOfPages);
        var hasher = new PerceptualHash();

        for (var pageIndex0 = 0; pageIndex0 < doc.NumberOfPages; pageIndex0++)
        {
            try
            {
                using var pdfStream = new MemoryStream(pdfBytes);
#pragma warning disable CA1416 // PDFtoImage is cross-platform
                using var skBitmap = Conversion.ToImage(
                    pdfStream,
                    leaveOpen: false,
                    page: pageIndex0,
                    options: new RenderOptions(Dpi: PerceptualHashRenderDpi));
#pragma warning restore CA1416

                if (skBitmap is null || skBitmap.Width <= 0 || skBitmap.Height <= 0)
                {
                    _logger.LogDebug(
                        "PerceptualHash: page {Page} rendered to null/empty bitmap — skipping.",
                        pageIndex0 + 1);
                    continue;
                }

                // Guarantee Bgra8888 color layout before reading bytes.
                // PDFium (via PDFtoImage) returns Bgra8888 on Windows but RGBA8888 on
                // some Linux builds.  If the channels are not normalized here, the R/B
                // bytes swap and every perceptual hash differs from a catalog hash
                // computed on a different platform → every catalog image would appear
                // absent (false-RED) once the feature is enabled with a real bundle.
                // We use a nullable 'using' for the converted copy: when the color type
                // is already Bgra8888 no copy is made and 'bitmapCopy' is null/not-disposed.
                using var bitmapCopy = skBitmap.ColorType == SkiaSharp.SKColorType.Bgra8888
                    ? null
                    : skBitmap.Copy(SkiaSharp.SKColorType.Bgra8888);
                var normalizedBitmap = bitmapCopy ?? skBitmap;

                var bgraBytes = normalizedBitmap.Bytes;
                var expectedLength = normalizedBitmap.Width * normalizedBitmap.Height * 4;

                if (bgraBytes is null || bgraBytes.Length != expectedLength)
                {
                    _logger.LogDebug(
                        "PerceptualHash: page {Page} bitmap byte count mismatch (got {Got}, expected {Expected}) — skipping.",
                        pageIndex0 + 1, bgraBytes?.Length ?? 0, expectedLength);
                    continue;
                }

                // Reinterpret BGRA32 bytes as an ImageSharp Image<Bgra32> (zero-copy pixel read).
                using var bgra32Image = Image.LoadPixelData<Bgra32>(bgraBytes, normalizedBitmap.Width, normalizedBitmap.Height);

                // CoenM PerceptualHash.Hash requires Image<Rgba32>.
                using var rgba32Image = bgra32Image.CloneAs<Rgba32>();

                var hash = hasher.Hash(rgba32Image);
                hashes.Add(hash);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "PerceptualHash: failed to render or hash page {Page} — skipping.",
                    pageIndex0 + 1);
            }
        }

        _logger.LogDebug(
            "PerceptualHash: computed {HashCount}/{PageCount} page hashes.",
            hashes.Count, doc.NumberOfPages);

        return hashes;
    }

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
    // §1–28 Mandatory CONDUSEF section detection (Story 10.1 / R1)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Anchor table: (SectionNumber, CanonicalName, NormalizedAnchor, IsConditional, IsIndeterminate).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Anchors are the normalized (upper+accent-stripped) minimum phrase that reliably
    /// identifies each section heading in a heading band of the Dummie VEC fixture PDFs.
    /// Presence is determined ONLY by finding the anchor in a per-page word band — a bare
    /// occurrence anywhere in the document body (e.g. in §27 Glosario de Términos) is
    /// intentionally ignored to avoid false positives.
    /// </para>
    /// <para>
    /// <b>IsConditional = true</b>: section is only required when its trigger condition is
    /// present (§16 other credit lines, §23 disputed charges, §25 debt restructuring) OR
    /// the section is explicitly optional per the Acuerdo (§21, §28 "sección opcional libre").
    /// When absent, <see cref="DetectedSection.IsApplicable"/> = false → never counted missing.
    /// </para>
    /// <para>
    /// <b>IsIndeterminate = true</b>: section has no text anchor (§1 Logo del Banco is a visual
    /// image, not detectable from the text layer). These sections are treated as
    /// <see cref="SectionDetectionStatus.Indeterminate"/> and IsApplicable = false → rule abstains.
    /// </para>
    /// <para>
    /// <b>Anchor design rationale for ambiguous sections (R1 fixes):</b>
    /// <list type="bullet">
    ///   <item>§9 "CAT": old anchor "CAT" matched the §27 Glosario line "CAT: COSTO ANUAL
    ///     TOTAL …". New anchor "COSTO ANUAL TOTAL" is the full heading phrase; confirmed
    ///     against the fixture comment "Y=285.8: TASA and CAT labels" which shows "COSTO ANUAL
    ///     TOTAL" on the heading band. The glosario reference "CAT: Costo Anual Total …"
    ///     normalizes to "CAT: COSTO ANUAL TOTAL …" (with colon prefix), distinct enough;
    ///     however the anchor "COSTO ANUAL TOTAL" does appear there too — so the band-only
    ///     constraint is the primary guard. The glosario entry is typically a single long line
    ///     beginning "CAT:" whereas the §9 heading band contains only "COSTO ANUAL TOTAL" or
    ///     "COSTO ANUAL TOTAL (CAT)". If both match, the first band hit (§9 heading appears
    ///     before §27) wins, which is correct.</item>
    ///   <item>§10 "Tasa de interés anual ordinaria": old anchor "TASA DE INTERES ANUAL"
    ///     matched §27 Glosario terms "Tasa de interés moratoria/ordinaria: Tasa de interés
    ///     anual…". New anchor "TASA DE INTERES ANUAL ORDINARIA" is the full section heading.
    ///     The glosario line begins "TASA DE INTERES ORDINARIA:" or similar — the full phrase
    ///     "TASA DE INTERES ANUAL ORDINARIA" is the heading, not a glosario definition.</item>
    ///   <item>§2 "Paginación": old anchor "PAGINA" was a loose substring. New anchor
    ///     "PAGINA" is retained because with band-only detection there is no body text that
    ///     creates a band containing only "PAGINA"; the pagination header appears on each page
    ///     as a distinct heading band "PAGINA X DE Y" and is reliably discriminated.</item>
    ///   <item>§1 "Logo del Banco": purely visual — marked IsIndeterminate.</item>
    ///   <item>§21, §28 "Sección opcional libre": explicitly optional per the Acuerdo
    ///     ("sección opcional libre") — marked IsConditional so absence never triggers Fail.</item>
    /// </list>
    /// </para>
    /// </remarks>
    private static readonly (int Number, string Name, string NormalizedAnchor, bool IsConditional, bool IsIndeterminate)[] s_sectionAnchors =
    [
        // §1: Logo del Banco — VISUAL element (image), not in the text layer.
        //     IsIndeterminate=true → DetectionStatus=Indeterminate, IsApplicable=false → rule abstains.
        (1,  "Logo del Banco",                                  "",                                                      false, true),

        // §2: Paginación "PAGINA X DE Y" — appears as a heading band on every page.
        //     Band-only detection discriminates this from body occurrences of "página".
        (2,  "Paginación (Página X de Y)",                      "PAGINA",                                                false, false),

        (3,  "Datos de envío",                                   "DATOS DE ENVIO",                                        false, false),
        (4,  "Identificación del producto",                      "IDENTIFICACION DEL PRODUCTO",                           false, false),
        (5,  "Tu pago requerido",                               "TU PAGO REQUERIDO",                                     false, false),
        (6,  "Cuánto pagarías por tus compras",                 "CUANTO PAGARIAS POR TUS COMPRAS",                       false, false),
        (7,  "Resumen de cargos y abonos",                      "RESUMEN DE CARGOS Y ABONOS",                            false, false),
        (8,  "Indicadores del costo anual",                     "INDICADORES DEL COSTO ANUAL",                           false, false),

        // §9: CAT — old anchor "CAT" produced false positives via §27 glosario "CAT: COSTO ANUAL TOTAL".
        //     New anchor "COSTO ANUAL TOTAL" matches the §9 section heading band.
        //     Band-only detection ensures glosario body text is ignored.
        (9,  "CAT",                                              "COSTO ANUAL TOTAL",                                     false, false),

        // §10: Tasa de interés anual ordinaria — old "TASA DE INTERES ANUAL" matched §27 glosario.
        //      New "TASA DE INTERES ANUAL ORDINARIA" is the full heading phrase.
        (10, "Tasa de interés anual ordinaria",                 "TASA DE INTERES ANUAL ORDINARIA",                       false, false),

        (11, "Compara tu tarjeta",                              "COMPARA TU TARJETA",                                    false, false),
        (12, "Mensajes importantes",                            "MENSAJES IMPORTANTES",                                   false, false),
        (13, "Nivel de uso de tu tarjeta",                      "NIVEL DE USO DE TU TARJETA",                            false, false),
        (14, "Notas al calce",                                  "NOTAS AL CALCE",                                        false, false),
        (15, "Número de cuenta (página 2+)",                    "NUMERO DE CUENTA",                                      false, false),
        (16, "Información de otras líneas de crédito",          "OTRAS LINEAS DE CREDITO",                               true,  false),
        (17, "Mensajes adicionales",                            "MENSAJES ADICIONALES",                                   false, false),
        (18, "Programas de beneficios",                         "PROGRAMAS DE BENEFICIOS",                               false, false),
        (19, "Saldo sobre el que se calcularon los intereses",  "SALDO SOBRE EL QUE SE CALCULARON LOS INTERESES",        false, false),
        (20, "Distribución de tu último pago",                  "DISTRIBUCION DE TU ULTIMO PAGO",                        false, false),

        // §21: "Sección opcional libre" — explicitly optional per the CONDUSEF Acuerdo.
        //      IsConditional=true → absent never triggers a missing-section Fail.
        (21, "Sección opcional libre (§21)",                    "SECCION OPCIONAL",                                      true,  false),

        (22, "Desglose de movimientos",                         "DESGLOSE DE MOVIMIENTOS",                               false, false),
        (23, "Cargos no reconocidos",                           "CARGOS NO RECONOCIDOS",                                 true,  false),
        (24, "Atención de quejas",                              "ATENCION DE QUEJAS",                                    false, false),
        (25, "Reestructura de tu deuda",                        "REESTRUCTURA",                                          true,  false),
        (26, "Notas aclaratorias",                              "NOTAS ACLARATORIAS",                                    false, false),
        (27, "Glosario de términos",                            "GLOSARIO DE TERMINOS",                                  false, false),

        // §28: "Sección opcional libre" — explicitly optional per the CONDUSEF Acuerdo.
        //      IsConditional=true → absent never triggers a missing-section Fail.
        (28, "Sección opcional libre (§28)",                    "SECCION LIBRE",                                         true,  false),
    ];

    /// <summary>
    /// Detects the 28 mandatory CONDUSEF <i>Acuerdo</i> sections by scanning per-page
    /// word bands (heading-band-only detection — Story 10.1 R1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Algorithm (R1 — heading-band-only):</b>
    /// <list type="number">
    ///   <item>Make a single pass over all pages collecting all bands (page, bandY, bandText, words)
    ///     into a flat ordered list of <c>BandEntry</c> records (reading order: page asc, Y desc).</item>
    ///   <item>For each section anchor, find the first band whose normalized text contains the
    ///     anchor → that band's locator is the section heading.  A bare occurrence of the anchor
    ///     anywhere in the full document text (e.g. §27 Glosario body) is NOT sufficient — the
    ///     anchor must appear in a band.</item>
    ///   <item>After all sections are located, compute <see cref="DetectedSection.SectionText"/>
    ///     by collecting all band words from a section's heading band to the next detected section's
    ///     heading band (within the same reading-order sequence, across pages if needed).</item>
    ///   <item>§1 Logo del Banco is a visual image — no text anchor exists.  It is marked
    ///     <see cref="SectionDetectionStatus.Indeterminate"/> and IsApplicable = false.</item>
    /// </list>
    /// </para>
    /// <para>
    /// The method reuses the open <see cref="PdfDocument"/> (no second PDF open).
    /// Never throws — individual page/band failures are silently swallowed.
    /// </para>
    /// </remarks>
    /// <param name="doc">Open PdfPig document (pages are read without re-opening).</param>
    /// <param name="normalizedFullText">
    /// Already-computed normalized full text (kept for API compatibility; no longer used for presence
    /// detection — band-scan is the sole presence signal).  Used only for the no-text-layer guard.
    /// </param>
    /// <returns>
    /// Exactly 28 <see cref="DetectedSection"/> entries ordered by section number.
    /// Never throws — individual page/band failures are silently swallowed.
    /// </returns>
    private static IReadOnlyList<DetectedSection> ExtractDetectedSections(
        PdfDocument doc,
        string normalizedFullText)
    {
        // Guard: if no text layer, all sections absent/indeterminate.
        if (string.IsNullOrWhiteSpace(normalizedFullText))
            return BuildAllAbsent();

        // ---- Single-pass: collect all bands from all pages in reading order ----
        // BandEntry: (PageNumber 1-based, BandY descending = top first, NormalizedText, Words left-to-right)
        var allBands = CollectAllBandsInReadingOrder(doc);

        if (allBands.Count == 0)
            return BuildAllAbsent();

        // ---- Locate each anchor in the first matching band ----
        // sectionLocations[i] = index into allBands for s_sectionAnchors[i], or -1 if not found.
        var sectionLocations = new int[s_sectionAnchors.Length];
        for (var i = 0; i < s_sectionAnchors.Length; i++)
        {
            var (_, _, anchor, _, isIndeterminate) = s_sectionAnchors[i];
            if (isIndeterminate || string.IsNullOrEmpty(anchor))
            {
                sectionLocations[i] = -1;
                continue;
            }

            var found = -1;
            for (var b = 0; b < allBands.Count; b++)
            {
                if (allBands[b].NormalizedText.Contains(anchor, StringComparison.Ordinal))
                {
                    found = b;
                    break;
                }
            }

            sectionLocations[i] = found;
        }

        // ---- Compute SectionText: words from this section's heading band to the next ----
        // Build an ordered list of (bandIndex, sectionIndex) for present sections.
        var presentSections = new List<(int BandIndex, int SectionIndex)>();
        for (var i = 0; i < s_sectionAnchors.Length; i++)
        {
            if (sectionLocations[i] >= 0)
                presentSections.Add((sectionLocations[i], i));
        }
        // Sort by band position (reading order).
        presentSections.Sort((a, b) => a.BandIndex.CompareTo(b.BandIndex));

        // ---- Build the 28 DetectedSection entries ----
        var results = new List<DetectedSection>(28);

        for (var i = 0; i < s_sectionAnchors.Length; i++)
        {
            var (number, name, _, isConditional, isIndeterminate) = s_sectionAnchors[i];

            if (isIndeterminate)
            {
                // §1 Logo del Banco: visual element — not detectable from the text layer.
                results.Add(new DetectedSection(
                    SectionNumber: number,
                    Name: name,
                    IsPresent: false,
                    IsApplicable: false,
                    Locator: FieldLocator.NoPage())
                {
                    DetectionStatus = SectionDetectionStatus.Indeterminate,
                    SectionText = string.Empty,
                });
                continue;
            }

            var bandIndex = sectionLocations[i];

            if (bandIndex < 0)
            {
                // Anchor not found in any heading band.
                results.Add(new DetectedSection(
                    SectionNumber: number,
                    Name: name,
                    IsPresent: false,
                    IsApplicable: !isConditional,
                    Locator: FieldLocator.NoPage())
                {
                    DetectionStatus = SectionDetectionStatus.Absent,
                    SectionText = string.Empty,
                });
                continue;
            }

            // Section is present: build locator from the heading band's words.
            var headingBand = allBands[bandIndex];
            var locator = headingBand.Words.Count > 0
                ? BoundingBoxOf(headingBand.Words, headingBand.PageNumber)
                : FieldLocator.PageHint(headingBand.PageNumber);

            // Compute SectionText: collect all band words from this band's index up to (but not
            // including) the band index of the next present section in reading order.
            var sectionText = ComputeSectionText(allBands, bandIndex, presentSections, i);

            results.Add(new DetectedSection(
                SectionNumber: number,
                Name: name,
                IsPresent: true,
                IsApplicable: true,
                Locator: locator)
            {
                DetectionStatus = SectionDetectionStatus.Present,
                SectionText = sectionText,
            });
        }

        return results;
    }

    /// <summary>
    /// Represents one horizontal word band from a single page, in document reading order.
    /// </summary>
    private sealed class BandEntry
    {
        public int PageNumber { get; init; }
        public double BandY { get; init; }
        public string NormalizedText { get; init; } = string.Empty;
        public List<Word> Words { get; init; } = [];
    }

    /// <summary>
    /// Makes a single pass over all pages and collects all word bands in reading order
    /// (page ascending, then Y descending = top of page first).
    /// </summary>
    private static List<BandEntry> CollectAllBandsInReadingOrder(PdfDocument doc)
    {
        var allBands = new List<BandEntry>();

        for (var pageIndex = 1; pageIndex <= doc.NumberOfPages; pageIndex++)
        {
            try
            {
                var page = doc.GetPage(pageIndex);
                var words = page.GetWords().ToList();

                if (words.Count == 0)
                    continue;

                var bands = GroupIntoBandsWithTolerance(words, YBandTolerance);

                // Add bands in top-to-bottom order (descending Y in PDF coords).
                foreach (var (bandY, bandWords) in bands.OrderByDescending(kv => kv.Key))
                {
                    var sorted = bandWords.OrderBy(w => w.BoundingBox.Left).ToList();
                    var normalizedText = NormalizeText(string.Join(" ", sorted.Select(w => w.Text)));

                    allBands.Add(new BandEntry
                    {
                        PageNumber = pageIndex,
                        BandY = bandY,
                        NormalizedText = normalizedText,
                        Words = sorted,
                    });
                }
            }
            catch (Exception)
            {
                // Silently skip unreadable pages.
            }
        }

        return allBands;
    }

    /// <summary>
    /// Computes the <see cref="DetectedSection.SectionText"/> for a present section by
    /// concatenating all band texts from the section's heading band (inclusive) up to the
    /// next present section's heading band (exclusive) in reading order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Multi-column layout note:</b> VEC statements use a 2-column layout where multiple
    /// section headings may appear on the same Y-band (e.g. §7 and §8 side-by-side on the
    /// same line).  In that case <paramref name="headingBandIndex"/> equals the next section's
    /// band index, making the naive range [head, next) empty.  The method ensures at least the
    /// heading band itself is always included by clamping the stop to
    /// <c>max(nextBandIndex, headingBandIndex + 1)</c>.
    /// </para>
    /// </remarks>
    /// <param name="allBands">All bands in reading order.</param>
    /// <param name="headingBandIndex">Index into <paramref name="allBands"/> of this section's heading.</param>
    /// <param name="presentSections">Present sections sorted by band index (reading order).</param>
    /// <param name="sectionAnchorIndex">Index of the current section in <see cref="s_sectionAnchors"/>.</param>
    private static string ComputeSectionText(
        List<BandEntry> allBands,
        int headingBandIndex,
        List<(int BandIndex, int SectionIndex)> presentSections,
        int sectionAnchorIndex)
    {
        // Find the end boundary: the band index of the next present section after this one
        // in reading order (not necessarily the §N+1 in anchor order — strictly by band position).
        var nextBandIndex = allBands.Count; // default: end of document

        // Find our position in the reading-order list.
        for (var j = 0; j < presentSections.Count - 1; j++)
        {
            if (presentSections[j].SectionIndex == sectionAnchorIndex)
            {
                nextBandIndex = presentSections[j + 1].BandIndex;
                break;
            }
        }

        // Ensure at least the heading band itself is included even when two section anchors
        // share the same band (2-column layout: e.g. §7 and §8 heading on the same Y-line).
        // Without this clamp, the range [headingBandIndex, headingBandIndex) is empty.
        var stopIndex = Math.Max(nextBandIndex, headingBandIndex + 1);

        // Collect band texts from headingBandIndex (inclusive) to stopIndex (exclusive).
        var sb = new StringBuilder();
        for (var b = headingBandIndex; b < stopIndex && b < allBands.Count; b++)
        {
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(allBands[b].NormalizedText);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the 28-section list with all sections absent or indeterminate (used when the
    /// text layer is empty or no bands were collected).
    /// </summary>
    private static IReadOnlyList<DetectedSection> BuildAllAbsent()
    {
        var results = new List<DetectedSection>(28);
        foreach (var (number, name, _, isConditional, isIndeterminate) in s_sectionAnchors)
        {
            if (isIndeterminate)
            {
                results.Add(new DetectedSection(
                    SectionNumber: number,
                    Name: name,
                    IsPresent: false,
                    IsApplicable: false,
                    Locator: FieldLocator.NoPage())
                {
                    DetectionStatus = SectionDetectionStatus.Indeterminate,
                });
            }
            else
            {
                results.Add(new DetectedSection(
                    SectionNumber: number,
                    Name: name,
                    IsPresent: false,
                    IsApplicable: !isConditional,
                    Locator: FieldLocator.NoPage())
                {
                    DetectionStatus = SectionDetectionStatus.Absent,
                });
            }
        }

        return results;
    }

    // -----------------------------------------------------------------------
    // Financial regulatory table extraction (Story 11.1)
    // §8  — INDICADORES DEL COSTO ANUAL DE LA TARJETA
    // §19 — SALDO SOBRE EL QUE SE CALCULARON LOS INTERESES DEL PERIODO
    // §20 — DISTRIBUCIÓN DE TU ÚLTIMO PAGO
    // §16 — INFORMACIÓN DE OTRAS LÍNEAS DE CRÉDITO  (conditional — absent → NotFound)
    // -----------------------------------------------------------------------

    // ---- §19 row labels (fixed, 6 rows) -----------------------------------
    // Normalized anchor fragments used to detect each row in the §19 grid.
    // Matching is done against the normalized (upper+accent-stripped) band text.
    private static readonly string[] s_sec19RowLabels =
    [
        "ORDINARIOS",
        "MORATORIO",
        "DE SALDO REVOLVENTE A TASA PREFERENCIAL",
        "DE COMPRAS Y CARGOS DIFERIDOS A MESES CON INTERESES",
        "POR DISPOSICIONES DE EFECTIVO",
        "POR DISPOSICIONES DE EFECTIVO DE OTRAS LINEAS",
    ];

    // Canonical display names (same order as anchors above).
    private static readonly string[] s_sec19RowNames =
    [
        "Ordinarios",
        "Moratorio",
        "De saldo revolvente a tasa preferencial",
        "De compras y cargos diferidos a meses con intereses",
        "Por disposiciones de efectivo",
        "Por disposiciones de efectivo de otras líneas de crédito",
    ];

    // ---- §19 column X-ranges (PDF points, bottom-left origin) -----------
    // Empirically measured from Dummie VEC fixtures.
    // Row label: X ≈ 15–195
    // Saldo base: X ≈ 196–268
    // Núm. de días: X ≈ 269–305
    // Tasa de interés anual: X ≈ 306–365
    // Monto de intereses: X ≈ 366–530
    private const double Sec19LabelXMax = 195.0;
    private const double Sec19SaldoXMin = 196.0;
    private const double Sec19SaldoXMax = 268.0;
    private const double Sec19DiasXMin = 269.0;
    private const double Sec19DiasXMax = 305.0;
    private const double Sec19TasaXMin = 306.0;
    private const double Sec19TasaXMax = 365.0;
    private const double Sec19MontoXMin = 366.0;

    // ---- §20 column X-ranges (PDF points) --------------------------------
    // §20 "DISTRIBUCIÓN DE TU ÚLTIMO PAGO" — 7 value columns.
    // Column headers wrap 2-3 lines; values are on separate bands.
    // Empirically: the single data row has 7 amount tokens spread across the page.
    // Column layout (approximate, may vary):
    //   Pagos y abonos:        X ≈ 15–100
    //   Compras regulares:     X ≈ 101–175
    //   A meses sin intereses: X ≈ 176–245
    //   A meses con intereses: X ≈ 246–310
    //   Intereses y comis.:    X ≈ 311–375
    //   IVA de intereses:      X ≈ 376–440
    //   Saldo a favor:         X ≈ 441–530
    private static readonly (double XMin, double XMax, string ColName)[] s_sec20Columns =
    [
        (  0.0, 100.0, "Pagos y abonos"),
        (101.0, 175.0, "Compras y cargos regulares"),
        (176.0, 245.0, "Compras y cargos diferidos a meses sin intereses"),
        (246.0, 310.0, "Compras y cargos diferidos a meses con intereses"),
        (311.0, 375.0, "Intereses y comisiones"),
        (376.0, 440.0, "IVA de intereses y comisiones"),
        (441.0, 540.0, "Saldo a favor"),
    ];

    // ---- §8 label anchors (3 indicators) ----------------------------------
    // Normalized text fragments that start each indicator row in the §8 block.
    private static readonly string[] s_sec8RowAnchors =
    [
        "MONTO DE INTERESES PAGADOS EN LOS ULTIMOS 12 MESES",
        "MONTO DE COMISIONES TOTALES PAGADAS EN LOS ULTIMOS 12 MESES",
        "MONTO DE ANUALIDAD O COMISIONES",
    ];

    private static readonly string[] s_sec8RowNames =
    [
        "Monto de intereses pagados en los últimos 12 meses",
        "Monto de comisiones totales pagadas en los últimos 12 meses",
        "Monto de anualidad o comisiones por administración pagadas en los últimos 12 meses",
    ];

    // §8 is in the LEFT column; the RESUMEN block is in the RIGHT column.
    // Guard: §8 values have X ≤ ~280 (left-column boundary).
    private const double Sec8ValueXMax = 280.0;

    // ---- Signed-amount pattern for §20 (may start with −/$) --------------
    private static readonly Regex SignedAmountPattern = new(
        @"^[+\-−]?\$?([\d,]+(?:\.\d+)?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Extracts the financial regulatory tables §8, §19, §20 (and §16 when present)
    /// from the already-open <see cref="PdfDocument"/>.
    /// </summary>
    /// <remarks>
    /// Reuses the open document — no second PDF open.
    /// Uses <paramref name="detectedSections"/> to locate heading bands for each section
    /// (page number + bounding-box Y) so that the extractor can scope its word-range scan.
    /// Never throws — individual table failures yield Indeterminate or NotFound.
    /// </remarks>
    private IReadOnlyList<FinancialTable> ExtractFinancialTables(
        PdfDocument doc,
        IReadOnlyList<DetectedSection> detectedSections,
        AmountNumberFormatSession amtFmt)
    {
        var tables = new List<FinancialTable>(5);

        // Collect all bands in reading order once (reuse pattern from §-detection).
        var allBands = CollectAllBandsInReadingOrder(doc);

        tables.Add(ExtractSection8Table(allBands, detectedSections, amtFmt));
        tables.Add(ExtractSection19Table(allBands, detectedSections, amtFmt));
        tables.Add(ExtractSection20Table(allBands, detectedSections, amtFmt));
        tables.Add(ExtractSection16Table(allBands, detectedSections, amtFmt));
        tables.Add(ExtractSection6Table(allBands, detectedSections, amtFmt));

        return tables;
    }

    // -----------------------------------------------------------------------
    // §8 — INDICADORES DEL COSTO ANUAL DE LA TARJETA
    // -----------------------------------------------------------------------

    /// <summary>
    /// Extracts the §8 "Indicadores del costo anual" block (3 label : amount indicator rows).
    /// </summary>
    /// <remarks>
    /// §8 and §7 (RESUMEN DE CARGOS) share the same Y-bands in a two-column layout.
    /// We guard against right-column bleed by capping X at <see cref="Sec8ValueXMax"/>.
    /// </remarks>
    private FinancialTable ExtractSection8Table(
        List<BandEntry> allBands,
        IReadOnlyList<DetectedSection> detectedSections,
        AmountNumberFormatSession amtFmt)
    {
        const int secNum = 8;
        const string secName = "Indicadores del costo anual de la tarjeta";

        var sec = detectedSections.FirstOrDefault(s => s.SectionNumber == secNum);
        if (sec is null || !sec.IsPresent)
            return FinancialTable.NotFound(secNum, secName);

        var headingLocator = sec.Locator;

        try
        {
            // Find the band-index range for §8: from heading band to next present section band.
            var headingBandIdx = FindBandIndexForSection(allBands, sec);
            var (startBandIdx, endBandIdx) = GetSectionBandRange(allBands, detectedSections, secNum);

            if (startBandIdx < 0)
                return FinancialTable.Indeterminate(secNum, secName, headingLocator);

            // Get all words in the section band range.
            var sectionWords = GetWordsInBandRange(allBands, startBandIdx, endBandIdx);

            // Re-group into bands with tolerance.
            var bands = GroupIntoBandsWithTolerance(sectionWords, YBandTolerance);
            var sortedBands = bands.OrderByDescending(kv => kv.Key).ToList();

            var rows = new List<TableRow>();

            foreach (var (anchorNorm, rowName) in s_sec8RowAnchors.Zip(s_sec8RowNames))
            {
                // Find the band(s) containing this row label.
                // §8 rows can wrap across 2 bands. We find the first band whose normalized
                // text contains the anchor fragment.
                var matchBand = sortedBands.FirstOrDefault(
                    kv => NormalizeText(BandText(kv.Value)).Contains(anchorNorm, StringComparison.Ordinal));

                if (matchBand.Value is null)
                {
                    // Row not found — produce a missing row but don't fail the whole table.
                    var missingLoc = headingLocator;
                    rows.Add(new TableRow(
                        TableCell.LabelCell(rowName, missingLoc),
                        [TableCell.Missing(missingLoc)]));
                    continue;
                }

                var bandWords = matchBand.Value;
                var bandLocator = BoundingBoxOf(bandWords, allBands.FirstOrDefault(b => b.Words == bandWords)?.PageNumber ?? headingLocator.PageNumber);

                var labelCell = TableCell.LabelCell(rowName, bandLocator);

                // Amount is in the left column (X ≤ Sec8ValueXMax).
                // It appears as a split-dollar "$ 1,234.56" or combined "$1,234.56".
                var leftColWords = bandWords.Where(w => w.BoundingBox.Left <= Sec8ValueXMax).ToList();
                var amountCell = ParseAmountCellFromWords(leftColWords, bandLocator, amtFmt);

                // If not found on this band, check 1-2 bands below (multi-line row).
                if (amountCell.Kind == CellKind.Empty && amountCell.Confidence == 0.0)
                {
                    var bandIdx = sortedBands.IndexOf(matchBand);
                    for (var nextIdx = bandIdx + 1; nextIdx < Math.Min(bandIdx + 3, sortedBands.Count); nextIdx++)
                    {
                        var nextBandWords = sortedBands[nextIdx].Value
                            .Where(w => w.BoundingBox.Left <= Sec8ValueXMax)
                            .ToList();
                        var nextLoc = BoundingBoxOf(nextBandWords.Count > 0 ? nextBandWords : sortedBands[nextIdx].Value,
                            allBands.FirstOrDefault(b => b.Words == sortedBands[nextIdx].Value)?.PageNumber ?? headingLocator.PageNumber);
                        var candidate = ParseAmountCellFromWords(nextBandWords, nextLoc, amtFmt);
                        if (candidate.Kind == CellKind.Amount || candidate.Kind == CellKind.NotApplicable)
                        {
                            amountCell = candidate;
                            break;
                        }
                    }
                }

                rows.Add(new TableRow(labelCell, [amountCell]));
            }

            if (rows.Count == 0)
                return FinancialTable.NoRows(secNum, secName, headingLocator);

            return new FinancialTable(secNum, secName, TableExtractionStatus.Extracted, rows, headingLocator);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "§8 table extraction failed; returning Indeterminate.");
            return FinancialTable.Indeterminate(secNum, secName, headingLocator);
        }
    }

    // -----------------------------------------------------------------------
    // §19 — SALDO SOBRE EL QUE SE CALCULARON LOS INTERESES DEL PERIODO
    // -----------------------------------------------------------------------

    /// <summary>
    /// Extracts the §19 interest-basis grid (6 rows × 4 value columns).
    /// </summary>
    /// <remarks>
    /// Columns: Saldo base | Núm. de días | Tasa anual | Monto de intereses.
    /// Many cells are "NA" in fixtures where only one product class is active.
    /// </remarks>
    private FinancialTable ExtractSection19Table(
        List<BandEntry> allBands,
        IReadOnlyList<DetectedSection> detectedSections,
        AmountNumberFormatSession amtFmt)
    {
        const int secNum = 19;
        const string secName = "Saldo sobre el que se calcularon los intereses del periodo";

        var sec = detectedSections.FirstOrDefault(s => s.SectionNumber == secNum);
        if (sec is null || !sec.IsPresent)
            return FinancialTable.NotFound(secNum, secName);

        var headingLocator = sec.Locator;

        try
        {
            var (startBandIdx, endBandIdx) = GetSectionBandRange(allBands, detectedSections, secNum);
            if (startBandIdx < 0)
                return FinancialTable.Indeterminate(secNum, secName, headingLocator);

            var sectionWords = GetWordsInBandRange(allBands, startBandIdx, endBandIdx);
            var bands = GroupIntoBandsWithTolerance(sectionWords, YBandTolerance);
            var sortedBands = bands.OrderByDescending(kv => kv.Key).ToList();

            var rows = new List<TableRow>();

            for (var rIdx = 0; rIdx < s_sec19RowLabels.Length; rIdx++)
            {
                var anchorNorm = s_sec19RowLabels[rIdx];
                var rowName = s_sec19RowNames[rIdx];

                // Find the band whose text starts with or contains the row anchor.
                // Row labels may span across 2 bands (long text wraps) — use the first band
                // containing the anchor's start word.
                var matchBand = sortedBands.FirstOrDefault(
                    kv => NormalizeText(BandText(kv.Value)).Contains(anchorNorm, StringComparison.Ordinal));

                int pageNum = headingLocator.PageNumber;
                if (matchBand.Value is null)
                {
                    // Partial anchor match: try individual leading words.
                    var anchorWords = anchorNorm.Split(' ');
                    if (anchorWords.Length > 0)
                    {
                        matchBand = sortedBands.FirstOrDefault(
                            kv => NormalizeText(BandText(kv.Value)).Contains(anchorWords[0], StringComparison.Ordinal)
                                && NormalizeText(BandText(kv.Value)).Contains(anchorWords[Math.Min(1, anchorWords.Length - 1)], StringComparison.Ordinal));
                    }
                }

                if (matchBand.Value is null)
                {
                    rows.Add(new TableRow(
                        TableCell.LabelCell(rowName, headingLocator),
                        [
                            TableCell.Missing(headingLocator),
                            TableCell.Missing(headingLocator),
                            TableCell.Missing(headingLocator),
                            TableCell.Missing(headingLocator),
                        ]));
                    continue;
                }

                var bandWords = matchBand.Value;
                // Determine page number from allBands.
                var bandEntryPage = allBands.FirstOrDefault(b => b.BandY == matchBand.Key && b.PageNumber == headingLocator.PageNumber)?.PageNumber
                    ?? allBands.FirstOrDefault(b => b.Words.Count > 0 && b.Words[0].BoundingBox.Bottom == matchBand.Key)?.PageNumber
                    ?? headingLocator.PageNumber;
                pageNum = bandEntryPage;

                var labelLoc = BoundingBoxOf(bandWords.Where(w => w.BoundingBox.Left <= Sec19LabelXMax).ToList(), pageNum);
                if (!labelLoc.HasBoundingBox) labelLoc = headingLocator;
                var labelCell = TableCell.LabelCell(rowName, labelLoc);

                // Value cells — extract by X-column range.
                var saldoCell = ExtractSec19Cell(bandWords, Sec19SaldoXMin, Sec19SaldoXMax, CellKind.Amount, pageNum, amtFmt);
                var diasCell = ExtractSec19Cell(bandWords, Sec19DiasXMin, Sec19DiasXMax, CellKind.Days, pageNum, amtFmt);
                var tasaCell = ExtractSec19Cell(bandWords, Sec19TasaXMin, Sec19TasaXMax, CellKind.Rate, pageNum, amtFmt);
                var montoCell = ExtractSec19Cell(bandWords, Sec19MontoXMin, double.MaxValue, CellKind.Amount, pageNum, amtFmt);

                rows.Add(new TableRow(labelCell, [saldoCell, diasCell, tasaCell, montoCell]));
            }

            if (rows.Count == 0)
                return FinancialTable.NoRows(secNum, secName, headingLocator);

            return new FinancialTable(secNum, secName, TableExtractionStatus.Extracted, rows, headingLocator);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "§19 table extraction failed; returning Indeterminate.");
            return FinancialTable.Indeterminate(secNum, secName, headingLocator);
        }
    }

    /// <summary>
    /// Extracts a single §19 value cell from a band's words within an X-column range.
    /// </summary>
    private static TableCell ExtractSec19Cell(
        List<Word> bandWords,
        double xMin,
        double xMax,
        CellKind expectedKind,
        int pageNum,
        AmountNumberFormatSession amtFmt)
    {
        var colWords = bandWords
            .Where(w => w.BoundingBox.Left >= xMin && w.BoundingBox.Left <= xMax)
            .OrderBy(w => w.BoundingBox.Left)
            .ToList();

        if (colWords.Count == 0)
            return TableCell.Missing(FieldLocator.PageHint(pageNum));

        var rawText = string.Join(" ", colWords.Select(w => w.Text)).Trim();
        var locator = BoundingBoxOf(colWords, pageNum);

        // NA check (present but not applicable).
        if (string.Equals(rawText, "NA", StringComparison.OrdinalIgnoreCase)
            || rawText.StartsWith("NA", StringComparison.OrdinalIgnoreCase))
            return TableCell.NotApplicableCell(rawText, locator);

        // Try to parse based on expected kind.
        if (expectedKind == CellKind.Days)
        {
            // Day count: integer or decimal (e.g. "31").
            var normalized = rawText.Replace(",", string.Empty, StringComparison.Ordinal)
                                    .TrimStart('$');
            if (decimal.TryParse(normalized, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var days))
                return TableCell.Days(days, rawText, locator);

            return TableCell.ParseFailure(rawText, locator);
        }

        if (expectedKind == CellKind.Rate)
        {
            // Rate: may be "27.36%" or "0.2736".
            var rateStr = rawText.TrimEnd('%');
            if (decimal.TryParse(rateStr, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var rateParsed))
            {
                // If > 1, it's in percent form — divide by 100.
                var rateVal = rawText.EndsWith('%') ? rateParsed / 100m : rateParsed;
                return TableCell.Rate(rateVal, rawText, locator);
            }

            return TableCell.ParseFailure(rawText, locator);
        }

        // Amount: use format-aware parsing.
        if (TryParseAmount(rawText.TrimStart('$'), amtFmt, out var amt))
            return TableCell.Amount(amt, rawText, locator);

        // Split-dollar: check for bare "$" token followed by number.
        if (colWords.Count >= 2)
        {
            var dollarIdx = colWords.FindIndex(w => w.Text == "$");
            if (dollarIdx >= 0 && dollarIdx + 1 < colWords.Count)
            {
                if (TryParseAmount(colWords[dollarIdx + 1].Text, amtFmt, out var splitAmt))
                    return TableCell.Amount(splitAmt, rawText, locator);
            }
        }

        return TableCell.ParseFailure(rawText, locator);
    }

    // -----------------------------------------------------------------------
    // §20 — DISTRIBUCIÓN DE TU ÚLTIMO PAGO
    // -----------------------------------------------------------------------

    /// <summary>
    /// Extracts the §20 payment-distribution row (1 row with 7 value columns).
    /// </summary>
    /// <remarks>
    /// The column headers wrap across 2-3 text bands; the actual values sit on a separate
    /// band below the headers.  Column assignment is by X-range (geometry-based), not
    /// text-order.  Values are signed currency amounts (e.g. "-$67,796.35").
    /// </remarks>
    private FinancialTable ExtractSection20Table(
        List<BandEntry> allBands,
        IReadOnlyList<DetectedSection> detectedSections,
        AmountNumberFormatSession amtFmt)
    {
        const int secNum = 20;
        const string secName = "Distribución de tu último pago";

        var sec = detectedSections.FirstOrDefault(s => s.SectionNumber == secNum);
        if (sec is null || !sec.IsPresent)
            return FinancialTable.NotFound(secNum, secName);

        var headingLocator = sec.Locator;

        try
        {
            var (startBandIdx, endBandIdx) = GetSectionBandRange(allBands, detectedSections, secNum);
            if (startBandIdx < 0)
                return FinancialTable.Indeterminate(secNum, secName, headingLocator);

            var sectionWords = GetWordsInBandRange(allBands, startBandIdx, endBandIdx);
            var bands = GroupIntoBandsWithTolerance(sectionWords, YBandTolerance);
            var sortedBands = bands.OrderByDescending(kv => kv.Key).ToList();

            // Find the value row: the first band (below the heading) that contains
            // amount-looking tokens (signed currency).  The column header bands contain
            // only word text (no amounts).
            List<Word>? valueBandWords = null;
            var valueBandPage = headingLocator.PageNumber;

            foreach (var (bandY, bandWords) in sortedBands)
            {
                // Skip the heading band itself (it contains the section anchor).
                var normText = NormalizeText(BandText(bandWords));
                if (normText.Contains("DISTRIBUCION DE TU ULTIMO PAGO", StringComparison.Ordinal))
                    continue;

                // Check whether this band has at least 3 proper currency amount tokens
                // (must contain a decimal point, e.g. "67,796.35" or "$0.00" or "-$5.79").
                // This guards against picking the card-number band or column-header bands.
                var amtCount = bandWords.Count(w => IsCurrencyAmountToken(w.Text));
                if (amtCount >= 3)
                {
                    valueBandWords = bandWords;
                    // Try to determine page number.
                    var matchingBand = allBands.FirstOrDefault(b => Math.Abs(b.BandY - bandY) < 1.0);
                    if (matchingBand is not null)
                        valueBandPage = matchingBand.PageNumber;
                    break;
                }
            }

            if (valueBandWords is null)
                return FinancialTable.NoRows(secNum, secName, headingLocator);

            // Build the single data row with 7 value cells.
            // Strategy: collect ALL value tokens from the band sorted left-to-right,
            // then try to assign them to the 7 columns in order.
            // §20 values may be split-dollar ("$ 67,796.35") or combined ("-$67,796.35").
            // We group consecutive tokens that form one amount (sign + $ + digits) together.
            var valueCells = BuildSection20ValueCells(valueBandWords, valueBandPage, amtFmt);

            var rowLabel = TableCell.LabelCell("Distribución", headingLocator);
            var row = new TableRow(rowLabel, valueCells);

            return new FinancialTable(secNum, secName, TableExtractionStatus.Extracted, [row], headingLocator);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "§20 table extraction failed; returning Indeterminate.");
            return FinancialTable.Indeterminate(secNum, secName, headingLocator);
        }
    }

    // -----------------------------------------------------------------------
    // §16 — INFORMACIÓN DE OTRAS LÍNEAS DE CRÉDITO (conditional)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Extracts the §16 table. Since §16 is conditional (only present when the
    /// account has other credit lines), absence is the expected case for most fixtures.
    /// Returns <see cref="FinancialTable.NotFound"/> when absent — never a failure.
    /// </summary>
    private FinancialTable ExtractSection16Table(
        List<BandEntry> allBands,
        IReadOnlyList<DetectedSection> detectedSections,
        AmountNumberFormatSession amtFmt)
    {
        const int secNum = 16;
        const string secName = "Información de otras líneas de crédito";

        var sec = detectedSections.FirstOrDefault(s => s.SectionNumber == secNum);
        if (sec is null || !sec.IsPresent)
            return FinancialTable.NotFound(secNum, secName);

        var headingLocator = sec.Locator;

        try
        {
            var (startBandIdx, endBandIdx) = GetSectionBandRange(allBands, detectedSections, secNum);
            if (startBandIdx < 0)
                return FinancialTable.Indeterminate(secNum, secName, headingLocator);

            var sectionWords = GetWordsInBandRange(allBands, startBandIdx, endBandIdx);
            var bands = GroupIntoBandsWithTolerance(sectionWords, YBandTolerance);

            // §16 table structure varies by product; extract all amount-bearing rows.
            var rows = new List<TableRow>();
            foreach (var (_, bandWords) in bands.OrderByDescending(kv => kv.Key))
            {
                // Skip header bands.
                var normText = NormalizeText(BandText(bandWords));
                if (normText.Contains("OTRAS LINEAS DE CREDITO", StringComparison.Ordinal))
                    continue;

                // Rows with at least one amount token.
                var hasAmt = bandWords.Any(w => AmountPattern.IsMatch(w.Text) || IsAmountOrSignedAmountToken(w.Text));
                if (!hasAmt)
                    continue;

                var pageNum = allBands.FirstOrDefault(b => b.Words.Count > 0
                    && Math.Abs(b.BandY - bandWords[0].BoundingBox.Bottom) < YBandTolerance)?.PageNumber
                    ?? headingLocator.PageNumber;

                var labelWords = bandWords.Where(w => w.BoundingBox.Left < 200.0).ToList();
                var rawLabel = string.Join(" ", labelWords.Select(w => w.Text)).Trim();
                var labelLoc = labelWords.Count > 0 ? BoundingBoxOf(labelWords, pageNum) : FieldLocator.PageHint(pageNum);
                var labelCell = TableCell.LabelCell(string.IsNullOrWhiteSpace(rawLabel) ? "(row)" : rawLabel, labelLoc);

                var amtWords = bandWords.Where(w => w.BoundingBox.Left >= 200.0).ToList();
                var amtLoc = amtWords.Count > 0 ? BoundingBoxOf(amtWords, pageNum) : FieldLocator.PageHint(pageNum);
                var rawAmt = string.Join(" ", amtWords.Select(w => w.Text)).Trim();
                var amtCell = ParseAmountCellFromWords(amtWords, amtLoc, amtFmt);

                rows.Add(new TableRow(labelCell, [amtCell]));
            }

            if (rows.Count == 0)
                return FinancialTable.NoRows(secNum, secName, headingLocator);

            return new FinancialTable(secNum, secName, TableExtractionStatus.Extracted, rows, headingLocator);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "§16 table extraction failed; returning Indeterminate.");
            return FinancialTable.Indeterminate(secNum, secName, headingLocator);
        }
    }

    // -----------------------------------------------------------------------
    // §6 — ¿CUÁNTO PAGARÍAS? PAYMENT-SIMULATION TABLE
    // -----------------------------------------------------------------------

    // ⚠️ UNCALIBRATED — no §6 PDF fixture exists in the PRP2 corpus (all three
    // Dummie VEC PDFs omit this section).  The extractor is implemented following
    // the §19/§20 pattern so it activates automatically when a real §6 statement
    // is present; its cell-shape is driven entirely by Section6PaymentSimulationRule's
    // contract.  Real-statement accuracy is CORPUS-GATED — verify with a real §6 PDF
    // before treating any Extracted result as calibrated.
    //
    // Contract with Section6PaymentSimulationRule:
    //   table6.Rows[0..2] → scenarios k=1, k=2, k=5 (pago mínimo multipliers)
    //   row.Values[ColMonths=0]   → months-to-pay  (CellKind.Days or NotApplicable)
    //   row.Values[ColInterest=1] → total ordinary interest (CellKind.Amount or NotApplicable)
    //   Confidence threshold from TenantProfile is applied by the rule, not the extractor.

    /// <summary>
    /// Normalized anchor fragments for the three §6 payment scenarios.
    /// The §6 heading anchor "CUANTO PAGARIAS POR TUS COMPRAS" is already in
    /// <see cref="s_sectionAnchors"/>; these are the per-row scenario labels.
    /// Matching uses NormalizeText + Contains (ordinal, already upper+stripped).
    /// ⚠️ UNCALIBRATED: derived from the Acuerdo §6 spec, not from a real fixture scan.
    /// </summary>
    private static readonly string[] s_sec6ScenarioAnchors =
    [
        "PAGANDO EL PAGO MINIMO",       // k=1: minimum payment
        "2 VECES EL PAGO MINIMO",       // k=2: double minimum payment
        "5 VECES EL PAGO MINIMO",       // k=5: 5× minimum payment
    ];

    /// <summary>
    /// Canonical display names for the three §6 scenarios (same order as
    /// <see cref="s_sec6ScenarioAnchors"/>).
    /// </summary>
    private static readonly string[] s_sec6ScenarioNames =
    [
        "Pagando el pago mínimo (k=1)",
        "Pagando 2 veces el pago mínimo (k=2)",
        "Pagando 5 veces el pago mínimo (k=5)",
    ];

    // §6 column X-ranges (PDF points, bottom-left origin).
    // ⚠️ UNCALIBRATED — values below are Acuerdo-guided estimates only.
    // Must be recalibrated against a real §6 PDF before relying on extraction.
    //   Row label (scenario name):  X ≈ 0–200
    //   Months to pay:              X ≈ 200–340  (CellKind.Days)
    //   Total ordinary interest:    X ≈ 341–540  (CellKind.Amount)
    private const double Sec6LabelXMax    = 200.0;
    private const double Sec6MonthsXMin   = 200.0;
    private const double Sec6MonthsXMax   = 340.0;
    private const double Sec6InterestXMin = 341.0;

    /// <summary>
    /// Extracts the §6 "¿Cuánto pagarías?" payment-simulation table (3 scenario rows).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>UNCALIBRATED (corpus-gated):</b> no §6 PDF fixture exists in the PRP2 corpus.
    /// On all three current Dummie VEC fixtures §6 is absent, so this method returns
    /// <see cref="FinancialTable.NotFound"/> (the safe abstain path).  The recursion/
    /// comparison logic in <c>Section6PaymentSimulationRule</c> will only run
    /// once <see cref="TableExtractionStatus.Extracted"/> is returned here.
    /// </para>
    /// <para>
    /// <b>Row contract (consumed by <c>Section6PaymentSimulationRule</c>):</b>
    /// <list type="bullet">
    ///   <item><c>row.Values[0]</c> — months to pay (<see cref="CellKind.Days"/> or <see cref="CellKind.NotApplicable"/>)</item>
    ///   <item><c>row.Values[1]</c> — total ordinary pre-IVA interest (<see cref="CellKind.Amount"/> or <see cref="CellKind.NotApplicable"/>)</item>
    /// </list>
    /// Three rows in order: k=1 (pago mínimo), k=2 (2×), k=5 (5×).
    /// </para>
    /// <para>
    /// <b>Abstain safety:</b> heading not found → NotFound; heading found but rows
    /// unresolvable → NoRowsParsed / Indeterminate.  Never guesses rows.
    /// </para>
    /// </remarks>
    private FinancialTable ExtractSection6Table(
        List<BandEntry> allBands,
        IReadOnlyList<DetectedSection> detectedSections,
        AmountNumberFormatSession amtFmt)
    {
        const int secNum = 6;
        const string secName = "¿Cuánto pagarías? (simulación de pagos)";

        // §6 absent in all current PRP2 fixtures — NotFound is the expected path.
        var sec = detectedSections.FirstOrDefault(s => s.SectionNumber == secNum);
        if (sec is null || !sec.IsPresent)
            return FinancialTable.NotFound(secNum, secName);

        var headingLocator = sec.Locator;

        try
        {
            var (startBandIdx, endBandIdx) = GetSectionBandRange(allBands, detectedSections, secNum);
            if (startBandIdx < 0)
                return FinancialTable.Indeterminate(secNum, secName, headingLocator);

            var sectionWords = GetWordsInBandRange(allBands, startBandIdx, endBandIdx);
            var bands = GroupIntoBandsWithTolerance(sectionWords, YBandTolerance);
            var sortedBands = bands.OrderByDescending(kv => kv.Key).ToList();

            var rows = new List<TableRow>();

            for (var scenarioIdx = 0; scenarioIdx < s_sec6ScenarioAnchors.Length; scenarioIdx++)
            {
                var anchorNorm = s_sec6ScenarioAnchors[scenarioIdx];
                var rowName    = s_sec6ScenarioNames[scenarioIdx];

                // Locate the band whose normalized text contains this scenario label.
                var matchBand = sortedBands.FirstOrDefault(
                    kv => NormalizeText(BandText(kv.Value)).Contains(anchorNorm, StringComparison.Ordinal));

                if (matchBand.Value is null)
                {
                    // Scenario row absent — produce a Missing row; do not fail the whole table.
                    rows.Add(new TableRow(
                        TableCell.LabelCell(rowName, headingLocator),
                        [
                            TableCell.Missing(headingLocator),
                            TableCell.Missing(headingLocator),
                        ]));
                    continue;
                }

                var bandWords = matchBand.Value;
                var pageNum = allBands
                    .FirstOrDefault(b => Math.Abs(b.BandY - matchBand.Key) < YBandTolerance
                                         && b.PageNumber == headingLocator.PageNumber)?.PageNumber
                    ?? allBands
                    .FirstOrDefault(b => Math.Abs(b.BandY - matchBand.Key) < YBandTolerance)?.PageNumber
                    ?? headingLocator.PageNumber;

                var labelLoc = BoundingBoxOf(
                    bandWords.Where(w => w.BoundingBox.Left <= Sec6LabelXMax).ToList(), pageNum);
                if (!labelLoc.HasBoundingBox) labelLoc = headingLocator;
                var labelCell = TableCell.LabelCell(rowName, labelLoc);

                // Months cell (col 0): integer count of months, treated as Days kind.
                var monthsCell = ExtractSec19Cell(
                    bandWords, Sec6MonthsXMin, Sec6MonthsXMax, CellKind.Days, pageNum, amtFmt);

                // Interest cell (col 1): total ordinary interest amount (pre-IVA).
                var interestCell = ExtractSec19Cell(
                    bandWords, Sec6InterestXMin, double.MaxValue, CellKind.Amount, pageNum, amtFmt);

                rows.Add(new TableRow(labelCell, [monthsCell, interestCell]));
            }

            if (rows.Count == 0)
                return FinancialTable.NoRows(secNum, secName, headingLocator);

            // Require at least one scenario row with at least one non-Empty cell before
            // declaring Extracted — guards against a spurious heading match with empty content.
            // (ParseFailure cells have Kind=Amount with Confidence=0.7; Empty has Kind=Empty.)
            var hasUsableRow = rows.Any(r =>
                r.Values.Any(c => c.Kind != CellKind.Empty));

            if (!hasUsableRow)
                return FinancialTable.NoRows(secNum, secName, headingLocator);

            return new FinancialTable(secNum, secName, TableExtractionStatus.Extracted, rows, headingLocator);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "§6 table extraction failed; returning Indeterminate.");
            return FinancialTable.Indeterminate(secNum, secName, headingLocator);
        }
    }

    // -----------------------------------------------------------------------
    // Financial table helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds the 7 value cells for a §20 row by grouping consecutive tokens into amounts.
    /// </summary>
    /// <remarks>
    /// Handles three token patterns for each amount:
    /// <list type="bullet">
    ///   <item>"$67,796.35" — single token (combined)</item>
    ///   <item>"$ 67,796.35" — two tokens (split dollar)</item>
    ///   <item>"-$67,796.35" or "-" then "$" then "67,796.35" — sign-prefixed</item>
    ///   <item>"=", "$", "67,796.35" — equality-sign-prefixed (right-hand side of equation)</item>
    /// </list>
    /// Words are sorted left-to-right on the band. We scan forward and greedily
    /// consume sign/dollar/digit tokens into groups, then parse each group.
    /// Exactly 7 cells are returned (padded with Missing or trimmed to 7).
    /// </remarks>
    private static IReadOnlyList<TableCell> BuildSection20ValueCells(List<Word> bandWords, int pageNum, AmountNumberFormatSession amtFmt)
    {
        // Sort left-to-right.
        var sorted = bandWords.OrderBy(w => w.BoundingBox.Left).ToList();

        // Group tokens into "amount groups": a group is a sequence of consecutive tokens
        // that together form one monetary value.  Heuristic:
        //   - Start a new group when a token looks like a sign (+/-/−/=), a "$", or a digit-amount.
        //   - Continue accumulating if next token is "$" or a digit-amount (no gap > 10 pt between tokens).
        var groups = new List<List<Word>>();
        var current = new List<Word>();

        for (var i = 0; i < sorted.Count; i++)
        {
            var w = sorted[i];
            var text = w.Text;

            // The "=" glyph is a STRUCTURAL separator between "Pagos y abonos" (LHS)
            // and the component columns (RHS) — it is NOT a value sign. Skip it entirely
            // so it does not occupy a column slot (which previously pushed the real
            // 7th column "Saldo a favor" past the 7-cell cap and silently dropped it).
            if (text == "=")
                continue;

            var isSign = text is "+" or "-" or "−";
            var isDollar = text == "$";
            var isAmount = IsCurrencyAmountToken(text);
            var isCombinedSignedAmount = text.Length > 2
                && (text[0] is '+' or '-' or '−')
                && IsCurrencyAmountToken(text[1..].TrimStart('$'));

            if (isSign || isDollar || isAmount || isCombinedSignedAmount)
            {
                // Check if this should extend the current group (within 15 pt of previous token).
                bool extendsCurrent = current.Count > 0
                    && (w.BoundingBox.Left - current[^1].BoundingBox.Right) <= 15.0;

                if (extendsCurrent && (isDollar || isAmount))
                {
                    current.Add(w);
                }
                else
                {
                    // Save previous group (if non-empty) and start a new one.
                    if (current.Count > 0)
                        groups.Add(current);
                    current = [w];
                }
            }
            // Non-amount tokens (column header text that leaked in) are skipped.
        }

        // Save last group.
        if (current.Count > 0)
            groups.Add(current);

        // Parse each group into a TableCell.
        var cells = new List<TableCell>(7);
        foreach (var group in groups)
        {
            var groupWords = group.OrderBy(w => w.BoundingBox.Left).ToList();
            var rawText = string.Join(" ", groupWords.Select(w => w.Text)).Trim();
            var loc = BoundingBoxOf(groupWords, pageNum);
            var cell = ParseSignedAmountCell(rawText, loc, amtFmt);
            cells.Add(cell);
        }

        // Pad or trim to exactly 7.
        while (cells.Count < 7)
            cells.Add(TableCell.Missing(FieldLocator.PageHint(pageNum)));

        // If more than 7 (rare layout artifacts), keep the 7 leftmost.
        if (cells.Count > 7)
            cells = cells.Take(7).ToList();

        return cells;
    }

    /// <summary>
    /// Returns (startBandIdx, endBandIdx) — the range of <paramref name="allBands"/>
    /// indices that belong to section <paramref name="sectionNumber"/>.
    /// startBandIdx = index of the section's heading band.
    /// endBandIdx = index of the next present section's heading band (exclusive), or allBands.Count.
    /// Returns (-1, -1) when the section heading cannot be located.
    /// </summary>
    private static (int startBandIdx, int endBandIdx) GetSectionBandRange(
        List<BandEntry> allBands,
        IReadOnlyList<DetectedSection> detectedSections,
        int sectionNumber)
    {
        // Find the section anchor text.
        var anchorEntry = s_sectionAnchors.FirstOrDefault(a => a.Number == sectionNumber);
        if (anchorEntry == default || string.IsNullOrEmpty(anchorEntry.NormalizedAnchor))
            return (-1, -1);

        var anchor = anchorEntry.NormalizedAnchor;

        // Find the heading band.
        var headingIdx = -1;
        for (var i = 0; i < allBands.Count; i++)
        {
            if (allBands[i].NormalizedText.Contains(anchor, StringComparison.Ordinal))
            {
                headingIdx = i;
                break;
            }
        }

        if (headingIdx < 0)
            return (-1, -1);

        // Find the next present section's heading band (reading-order successor).
        // Build a sorted list of present section band indices.
        var presentBandIndices = new List<int>();
        foreach (var s in detectedSections.Where(s => s.IsPresent))
        {
            var a = s_sectionAnchors.FirstOrDefault(x => x.Number == s.SectionNumber);
            if (string.IsNullOrEmpty(a.NormalizedAnchor))
                continue;

            for (var i = 0; i < allBands.Count; i++)
            {
                if (allBands[i].NormalizedText.Contains(a.NormalizedAnchor, StringComparison.Ordinal))
                {
                    presentBandIndices.Add(i);
                    break;
                }
            }
        }

        presentBandIndices.Sort();

        // Find the next band index after headingIdx.
        var endBandIdx = allBands.Count;
        foreach (var idx in presentBandIndices)
        {
            if (idx > headingIdx)
            {
                endBandIdx = idx;
                break;
            }
        }

        return (headingIdx, endBandIdx);
    }

    /// <summary>
    /// Finds the band index in <paramref name="allBands"/> for the heading of
    /// <paramref name="section"/> using its locator page + Y coordinate.
    /// </summary>
    private static int FindBandIndexForSection(List<BandEntry> allBands, DetectedSection section)
    {
        if (!section.IsPresent)
            return -1;

        var anchorEntry = s_sectionAnchors.FirstOrDefault(a => a.Number == section.SectionNumber);
        if (string.IsNullOrEmpty(anchorEntry.NormalizedAnchor))
            return -1;

        for (var i = 0; i < allBands.Count; i++)
        {
            if (allBands[i].NormalizedText.Contains(anchorEntry.NormalizedAnchor, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Returns all words from bands in the range [startIdx, endIdx).
    /// </summary>
    private static List<Word> GetWordsInBandRange(
        List<BandEntry> allBands,
        int startIdx,
        int endIdx)
    {
        var words = new List<Word>();
        for (var i = startIdx; i < endIdx && i < allBands.Count; i++)
            words.AddRange(allBands[i].Words);
        return words;
    }

    /// <summary>
    /// Parses an amount cell from a list of words using the split-dollar pattern.
    /// Returns Missing(0.0) when no amount is found.
    /// </summary>
    private static TableCell ParseAmountCellFromWords(List<Word> words, FieldLocator locator, AmountNumberFormatSession amtFmt)
    {
        if (words.Count == 0)
            return TableCell.Missing(locator);

        var rawText = string.Join(" ", words.Select(w => w.Text)).Trim();

        // NA check.
        if (string.Equals(rawText, "NA", StringComparison.OrdinalIgnoreCase))
            return TableCell.NotApplicableCell(rawText, locator);

        // Try combined AmountPattern first.
        var amtWord = words.FirstOrDefault(w => AmountPattern.IsMatch(w.Text));
        if (amtWord is not null)
        {
            var m = AmountPattern.Match(amtWord.Text);
            if (TryParseAmount(m.Groups[1].Value, amtFmt, out var amt))
                return TableCell.Amount(amt, rawText, locator);
        }

        // Try split-dollar.
        var dollarIdx = words.FindIndex(w => w.Text == "$");
        if (dollarIdx >= 0 && dollarIdx + 1 < words.Count)
        {
            var numToken = words[dollarIdx + 1];
            if (!IsSingleDigit(numToken.Text))
            {
                if (TryParseAmount(numToken.Text, amtFmt, out var val))
                    return TableCell.Amount(val, rawText, locator);
            }

            if (dollarIdx + 2 < words.Count && IsSingleDigit(words[dollarIdx + 1].Text))
            {
                if (TryParseAmount(words[dollarIdx + 2].Text, amtFmt, out var val))
                    return TableCell.Amount(val, rawText, locator);
            }
        }

        if (string.IsNullOrWhiteSpace(rawText))
            return TableCell.Missing(locator);

        return TableCell.ParseFailure(rawText, locator);
    }

    /// <summary>
    /// Parses a signed amount cell from raw text (e.g. "-$67,796.35", "+$60,041.34", "$0.00").
    /// </summary>
    private static TableCell ParseSignedAmountCell(string rawText, FieldLocator locator, AmountNumberFormatSession amtFmt)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return TableCell.Missing(locator);

        if (string.Equals(rawText, "NA", StringComparison.OrdinalIgnoreCase))
            return TableCell.NotApplicableCell(rawText, locator);

        // Strip sign and $; pass to session (which handles group separator removal).
        var negative = rawText.StartsWith('-') || rawText.StartsWith('−');
        var stripped = rawText.TrimStart('+', '-', '−', '$');

        if (amtFmt.TryParse(stripped, out var val))
        {
            var signed = negative ? -val : val;
            return TableCell.Amount(signed, rawText, locator);
        }

        // Try signed-amount pattern.
        var m = SignedAmountPattern.Match(rawText);
        if (m.Success)
        {
            if (TryParseAmount(m.Groups[1].Value, amtFmt, out var parsed))
            {
                var signed = negative ? -parsed : parsed;
                return TableCell.Amount(signed, rawText, locator);
            }
        }

        return TableCell.ParseFailure(rawText, locator);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the token looks like a currency amount
    /// (with or without sign) — used to detect the §20 value band.
    /// </summary>
    private static bool IsAmountOrSignedAmountToken(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var stripped = text.TrimStart('+', '-', '−');
        return AmountPattern.IsMatch(stripped) || AmountPattern.IsMatch(text);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the token is a currency amount that contains
    /// a decimal point (e.g. "67,796.35", "$0.00", "-$5.79").
    /// This is stricter than <see cref="IsAmountOrSignedAmountToken"/> and is used to
    /// avoid matching card-number bands (digits without decimals).
    /// </summary>
    private static bool IsCurrencyAmountToken(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.Contains('.'))
            return false;

        var stripped = text.TrimStart('+', '-', '−', '=').TrimStart('$');
        return AmountPattern.IsMatch(stripped) || AmountPattern.IsMatch(text.TrimStart('+', '-', '−', '='));
    }

    // -----------------------------------------------------------------------
    // §23 dispute-row extraction — Story E10.C4′
    // -----------------------------------------------------------------------

    /// <summary>
    /// Best-effort extraction of structured §23 <i>Cargos no reconocidos</i> dispute rows
    /// from the normalized <see cref="DetectedSection.SectionText"/> of the §23 section.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Approach:</b> the SectionText is the normalized (upper-case, accent-stripped,
    /// whitespace-collapsed) concatenation of all horizontal word bands in the §23 section.
    /// Because line boundaries are replaced with single spaces during normalization, row
    /// structure is NOT directly recoverable from the SectionText string.  Instead, the
    /// method uses a status-token / backward-amount scan:
    /// <list type="number">
    ///   <item>
    ///     Find every occurrence of the three CONDUSEF status tokens
    ///     (PENDIENTE EN REVISION, CONCLUIDA PROCEDENTE, CONCLUIDA IMPROCEDENTE) in
    ///     the SectionText, ordered by their character position.
    ///   </item>
    ///   <item>
    ///     For each token, look backward in the text segment between the previous token
    ///     end and the current token start, and extract the last amount matching
    ///     <c>\$?([\d,]+\.\d{2})</c>.
    ///   </item>
    ///   <item>
    ///     If both an amount and a status token are found, emit one
    ///     <see cref="DisputeRow"/> (amount, status, null date, null description, section locator).
    ///   </item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Limitations (acknowledged):</b>
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="DisputeRow.OperationDate"/> is always <see langword="null"/> — date
    ///     tokens in the normalized text cannot be reliably linked to a specific row because
    ///     the line structure is lost.
    ///   </item>
    ///   <item>
    ///     <see cref="DisputeRow.Description"/> is always <see langword="null"/> for the
    ///     same reason.
    ///   </item>
    ///   <item>
    ///     <see cref="DisputeRow.Locator"/> is the §23 section-level locator, not a
    ///     per-row bounding box.
    ///   </item>
    ///   <item>
    ///     If no amount precedes a status token in its segment, that token is silently
    ///     skipped.  The method returns <see cref="DisputeRowsExtractionStatus.NoRowsParsed"/>
    ///     when all tokens are skipped.
    ///   </item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Safety:</b> never throws; all parsing is guarded by try-parse and null checks.
    /// </para>
    /// </remarks>
    /// <param name="detectedSections">
    /// The 28-entry list produced by <see cref="ExtractDetectedSections"/>.
    /// </param>
    /// <returns>
    /// A tuple of (dispute rows, extraction status).  Rows is always non-null; status is
    /// one of <see cref="DisputeRowsExtractionStatus"/>.
    /// </returns>
    private static (IReadOnlyList<DisputeRow> Rows, DisputeRowsExtractionStatus Status)
        ExtractDisputeRows(IReadOnlyList<DetectedSection> detectedSections)
    {
        static (IReadOnlyList<DisputeRow>, DisputeRowsExtractionStatus) NotFound()
            => ([], DisputeRowsExtractionStatus.SectionNotFound);

        static (IReadOnlyList<DisputeRow>, DisputeRowsExtractionStatus) NoRows()
            => ([], DisputeRowsExtractionStatus.NoRowsParsed);

        // Locate §23 in the detected-section list.
        DetectedSection? section23 = null;
        foreach (var s in detectedSections)
        {
            if (s.SectionNumber == 23)
            {
                section23 = s;
                break;
            }
        }

        // Section absent, not applicable, or indeterminate → SectionNotFound.
        if (section23 is null
            || !section23.IsApplicable
            || section23.DetectionStatus == SectionDetectionStatus.Indeterminate
            || !section23.IsPresent)
        {
            return NotFound();
        }

        var sectionText = section23.SectionText;
        if (string.IsNullOrWhiteSpace(sectionText))
            return NoRows();

        // Status tokens (normalized — upper-case, accent-stripped, reuse from Section23 rule).
        // Defined inline to avoid a hard compile-time dependency on the Validation assembly;
        // these are simply the string literals the Acuerdo §23 mandates.
        const string tokenPendiente    = "PENDIENTE EN REVISION";
        const string tokenProcedente   = "CONCLUIDA PROCEDENTE";
        const string tokenImprocedente = "CONCLUIDA IMPROCEDENTE";

        // Build a list of (charPosition, statusToken, DisputeStatus) for every occurrence
        // in the section text, then sort by position.
        var hits = new List<(int Position, string Token, DisputeStatus Status)>();

        AddHits(hits, sectionText, tokenPendiente,    DisputeStatus.Pendiente);
        AddHits(hits, sectionText, tokenProcedente,   DisputeStatus.ConcluidaProcedente);
        AddHits(hits, sectionText, tokenImprocedente, DisputeStatus.ConcluidaImprocedente);

        if (hits.Count == 0)
            return NoRows();

        hits.Sort(static (a, b) => a.Position.CompareTo(b.Position));

        // Section-level locator — per-row locators are not recoverable from normalized text.
        var sectionLocator = section23.Locator;

        var rows = new List<DisputeRow>(hits.Count);
        var segmentStart = 0;

        foreach (var (pos, token, status) in hits)
        {
            // The "segment" for this token is the text between the previous token end
            // and the start of the current token.
            var segment = sectionText[segmentStart..pos];

            // Find the last amount in the segment (US/MX format: comma-thousands, dot-decimal,
            // optional leading "$"; the normalized text preserves these characters verbatim).
            var amountMatches = DisputeRowAmountPattern.Matches(segment);
            if (amountMatches.Count == 0)
            {
                // No amount found before this status token — skip the row (conservative).
                segmentStart = pos + token.Length;
                continue;
            }

            var lastMatch = amountMatches[amountMatches.Count - 1];
            var rawAmount = lastMatch.Groups[1].Value; // captured group (no "$")

            // Parse US/MX format: remove thousands commas, then decimal parse.
            var normalised = rawAmount.Replace(",", string.Empty, StringComparison.Ordinal);
            if (!decimal.TryParse(
                    normalised,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var amount))
            {
                // Unparseable amount — skip (conservative).
                segmentStart = pos + token.Length;
                continue;
            }

            rows.Add(new DisputeRow(
                Amount: amount,
                Status: status,
                OperationDate: null,   // not recoverable from normalized text
                Description: null,     // not recoverable from normalized text
                Locator: sectionLocator));

            // Advance the segment boundary past the current token.
            segmentStart = pos + token.Length;
        }

        return rows.Count > 0
            ? (rows, DisputeRowsExtractionStatus.Extracted)
            : NoRows();
    }

    /// <summary>
    /// Appends all (position, token, status) hits for <paramref name="token"/> found in
    /// <paramref name="text"/> to <paramref name="hits"/>.
    /// </summary>
    private static void AddHits(
        List<(int Position, string Token, DisputeStatus Status)> hits,
        string text,
        string token,
        DisputeStatus status)
    {
        var idx = 0;
        while (true)
        {
            var pos = text.IndexOf(token, idx, StringComparison.Ordinal);
            if (pos < 0)
                break;
            hits.Add((pos, token, status));
            idx = pos + token.Length;
        }
    }

    // -----------------------------------------------------------------------
    // §-inter-section blank gap computation (Story 10.2)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Computes the vertical blank gap between each pair of consecutive present sections
    /// that share the same page (Story 10.2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Algorithm:</b>
    /// <list type="number">
    ///   <item>Group present sections by page (from <see cref="DetectedSection.Locator"/>).</item>
    ///   <item>On each page with ≥ 2 present sections, sort them top-to-bottom by heading
    ///     Y-coordinate (descending Bottom in PDF points = top-of-page first).</item>
    ///   <item>For each adjacent pair, collect all word bottom-Y values on that page that
    ///     fall between the two heading bands, then find the largest empty vertical band
    ///     (the widest gap between consecutive Y-values in the stripe).</item>
    ///   <item>The "stripe" is defined as the region below the first heading's bottom edge
    ///     and above the second heading's top edge (Bottom + Height when HasBoundingBox).
    ///     When bounding-box data is absent, the page words collected during the existing
    ///     pass are still used to bound the measurement conservatively.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Gaps across page breaks are not recorded. Page-break whitespace is always excluded.
    /// Never throws — per-page failures produce an empty list contribution.
    /// </para>
    /// </remarks>
    /// <param name="doc">Open PdfPig document (sections headings already located).</param>
    /// <param name="detectedSections">
    /// The 28-entry list produced by <see cref="ExtractDetectedSections"/>.
    /// </param>
    /// <returns>
    /// List of <see cref="SectionGap"/> entries (may be empty).  All
    /// <see cref="SectionGap.GapPoints"/> values are ≥ 0.
    /// </returns>
    private static IReadOnlyList<SectionGap> ComputeSectionGaps(
        PdfDocument doc,
        IReadOnlyList<DetectedSection> detectedSections)
    {
        if (detectedSections.Count == 0)
            return [];

        // Only consider present sections that have a real page assignment.
        var present = detectedSections
            .Where(s => s.IsPresent && s.Locator.PageNumber > 0)
            .OrderBy(s => s.SectionNumber)
            .ToList();

        if (present.Count < 2)
            return [];

        var gaps = new List<SectionGap>();

        // Group by page number — gaps only computed for same-page adjacencies.
        var byPage = present
            .GroupBy(s => s.Locator.PageNumber)
            .Where(g => g.Count() >= 2)
            .ToList();

        foreach (var pageGroup in byPage)
        {
            var pageNumber = pageGroup.Key;

            // Sort sections top-to-bottom (highest Y = nearest top-of-page in PDF coords).
            var sectionsOnPage = pageGroup
                .OrderByDescending(s => s.Locator.Bottom ?? 0.0)
                .ToList();

            // Pre-collect all words on this page for gap measurement.
            List<Word>? pageWords = null;
            try
            {
                pageWords = doc.GetPage(pageNumber).GetWords().ToList();
            }
            catch (Exception)
            {
                // Cannot read this page — skip gap computation for it.
                continue;
            }

            if (pageWords is null || pageWords.Count == 0)
                continue;

            // Sorted unique Y-bottom values of all words on this page.
            var wordYBottoms = pageWords
                .Select(w => w.BoundingBox.Bottom)
                .Distinct()
                .OrderDescending()  // top-of-page first
                .ToList();

            // Measure gap between each consecutive pair.
            for (var i = 0; i + 1 < sectionsOnPage.Count; i++)
            {
                var upper = sectionsOnPage[i];
                var lower = sectionsOnPage[i + 1];

                // Upper section's content bottom:
                //   If the heading has a bounding box, the heading BOTTOM is the lower edge of
                //   the heading band (PDF origin = bottom-left, so heading top = Bottom + Height).
                //   Content of the upper section extends down from heading toward the lower section.
                //   We use the heading's Bottom as a conservative upper bound of content start.
                //   Words in the stripe between the two headings are the content between them.
                //
                // Lower section's heading top = Bottom + Height (when HasBoundingBox).
                // Fallback: use Bottom alone.
                double stripeTop;    // Y below which (and above stripeBottom) the gap exists
                double stripeBottom; // Y above which (and below stripeTop) the gap exists

                if (upper.Locator.HasBoundingBox)
                    stripeTop = upper.Locator.Bottom!.Value; // heading bottom edge = stripe starts here
                else
                    stripeTop = upper.Locator.Bottom ?? double.MaxValue;

                if (lower.Locator.HasBoundingBox)
                    stripeBottom = lower.Locator.Bottom!.Value + (lower.Locator.Height ?? 0.0);
                else
                    stripeBottom = lower.Locator.Bottom ?? 0.0;

                // Sanity: stripeTop must be above stripeBottom (higher Y = higher on page).
                if (stripeTop <= stripeBottom)
                    continue;

                // Collect all word Y-bottom values in the stripe [stripeBottom, stripeTop].
                // In PDF coords origin is bottom-left; words in the inter-section stripe have
                // Bottom values between stripeBottom and stripeTop.
                var stripeYValues = wordYBottoms
                    .Where(y => y > stripeBottom && y < stripeTop)
                    .OrderDescending()
                    .ToList();

                double gapPoints;

                if (stripeYValues.Count == 0)
                {
                    // No words in the stripe — the entire stripe is blank.
                    gapPoints = stripeTop - stripeBottom;
                }
                else
                {
                    // The gap is the largest empty vertical band within the stripe.
                    // Insert the stripe boundaries and find the largest interval.
                    var boundaries = new List<double> { stripeTop };
                    boundaries.AddRange(stripeYValues);
                    boundaries.Add(stripeBottom);

                    // boundaries is descending (top → bottom).
                    gapPoints = 0.0;
                    for (var j = 0; j + 1 < boundaries.Count; j++)
                    {
                        var interval = boundaries[j] - boundaries[j + 1];
                        if (interval > gapPoints)
                            gapPoints = interval;
                    }
                }

                if (gapPoints < 0.0)
                    gapPoints = 0.0;

                gaps.Add(new SectionGap(
                    AfterSectionNumber: upper.SectionNumber,
                    BeforeSectionNumber: lower.SectionNumber,
                    PageNumber: pageNumber,
                    GapPoints: gapPoints));
            }
        }

        return gaps;
    }

    // -----------------------------------------------------------------------
    // Internal helpers (unit-testable without a PDF)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="pageText"/> contains a masked
    /// card presentation whose last-four visible digits match <paramref name="cardLast4"/>.
    /// </summary>
    /// <remarks>
    /// A masked presentation requires the first three groups to consist exclusively of mask
    /// glyphs (X, x, *, •, ·, ●).  All-digit references (e.g. transaction codes printed in
    /// 4-4-4-4 format) do NOT satisfy this check, which prevents false-PASS on CL-34.
    /// </remarks>
    /// <param name="pageText">Raw page text with spaces preserved.</param>
    /// <param name="cardLast4">Exactly four digit characters taken from the end of the card number.</param>
    internal static bool TryMatchMaskedCard(string pageText, string cardLast4)
    {
        var m = CardGroupPattern.Match(pageText);
        return m.Success &&
               m.Groups[1].Value.Equals(cardLast4, StringComparison.OrdinalIgnoreCase);
    }
}
