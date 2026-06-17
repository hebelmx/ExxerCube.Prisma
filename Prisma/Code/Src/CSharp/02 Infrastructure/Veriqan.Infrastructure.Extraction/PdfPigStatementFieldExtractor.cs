using System;
using System.Collections.Generic;
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
/// <b>Label→value association strategy:</b> the VEC statement header uses a two-column layout.
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
/// All validation (16-digit card, 18-digit CLABE, RFC pattern) is performed after extraction.
/// Format violations produce <see cref="ExtractionStatus.ExtractedInvalidFormat"/> rather than
/// exceptions; the raw value is always preserved.
/// </para>
/// </remarks>
public sealed class PdfPigStatementFieldExtractor : IStatementFieldExtractor
{
    // -----------------------------------------------------------------------
    // Constants / patterns
    // -----------------------------------------------------------------------

    /// <summary>Mexican RFC pattern (individuals: 4-char prefix; corporations: 3-char prefix).</summary>
    private static readonly Regex RfcPattern = new(
        @"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DigitsOnly = new(@"\D", RegexOptions.Compiled);

    /// <summary>5-digit Mexican postal code.</summary>
    private static readonly Regex PostalCodePattern = new(@"^\d{5}$", RegexOptions.Compiled);

    // Header layout constants (PDF points, page-coordinate system where Y=0 is bottom-left).
    // These were derived empirically from the three Dummie VEC fixtures.
    private const double HeaderYMin = 530.0;   // lower bound of header zone
    private const double HeaderYMax = 700.0;   // upper bound (below bank logo)
    private const double LabelColumnXMin = 285.0; // labels start here
    private const double ValueColumnXMin = 410.0; // values start here
    private const double ClientNameXMin = 330.0;  // name / address are in right column
    private const double ClientNameYMin = 630.0;  // name is near Y~681
    private const double AddressYMax = 680.0;     // address lines are below Y~668

    /// <summary>Y-band tolerance (pt) for treating words as on the same line.</summary>
    private const double YBandTolerance = 5.0;

    private readonly ILogger<PdfPigStatementFieldExtractor> _logger;

    /// <summary>
    /// Initializes a new <see cref="PdfPigStatementFieldExtractor"/>.
    /// </summary>
    public PdfPigStatementFieldExtractor(ILogger<PdfPigStatementFieldExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<Result<StatementModel>> ExtractHeaderAsync(
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<StatementModel>());

        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0)
            return Task.FromResult(
                Result<StatementModel>.WithFailure("PDF bytes are empty."));

        try
        {
            var model = ExtractHeader(pdf);
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
    // Core extraction (synchronous — PdfPig is synchronous)
    // -----------------------------------------------------------------------

    private StatementModel ExtractHeader(byte[] pdfBytes)
    {
        using var doc = PdfDocument.Open(pdfBytes);
        var page = doc.GetPage(1);

        // Collect all words on page 1 with their bounding boxes.
        var allWords = page.GetWords().ToList();

        // Filter to header zone only.
        var headerWords = allWords
            .Where(w => w.BoundingBox.Bottom >= HeaderYMin && w.BoundingBox.Bottom <= HeaderYMax)
            .ToList();

        // Group into horizontal bands (Y ±5 pt) for label↔value association.
        var bands = GroupIntoBands(headerWords);

        // Extract each field.
        var clientName = ExtractClientName(headerWords);
        var address = ExtractAddress(headerWords);
        var branchNumber = ExtractLabeledField(bands, new[] { "Número", "de", "sucursal" });
        var cardNumber = ExtractAndValidateCardNumber(bands);
        var clabe = ExtractAndValidateClabe(bands);
        var clientNumber = ExtractLabeledField(bands, new[] { "Número", "de", "cliente" });
        var rfc = ExtractAndValidateRfc(bands);

        return new StatementModel(
            clientName: clientName,
            address: address,
            branchNumber: branchNumber,
            cardNumber: cardNumber,
            clabe: clabe,
            clientNumber: clientNumber,
            rfc: rfc);
    }

    // -----------------------------------------------------------------------
    // Client name extraction (positional — right column, top of header)
    // -----------------------------------------------------------------------

    private static ExtractedField<ExtractedClientName> ExtractClientName(List<Word> headerWords)
    {
        // The client name sits at Y~681 in all three fixtures, X ≥ ClientNameXMin.
        // Find the band closest to Y=681 that is entirely in the right column.
        var nameWords = headerWords
            .Where(w => w.BoundingBox.Left >= ClientNameXMin
                     && w.BoundingBox.Bottom >= ClientNameYMin)
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ToList();

        if (nameWords.Count == 0)
            return ExtractedField<ExtractedClientName>.Missing(FieldLocator.PageHint(1));

        // Take the top-most band.
        var topY = nameWords[0].BoundingBox.Bottom;
        var nameBandWords = nameWords
            .Where(w => Math.Abs(w.BoundingBox.Bottom - topY) <= YBandTolerance)
            .OrderBy(w => w.BoundingBox.Left)
            .ToList();

        var fullName = string.Join(" ", nameBandWords.Select(w => w.Text)).Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            return ExtractedField<ExtractedClientName>.Missing(FieldLocator.PageHint(1));

        // Best-effort first/last name split: for Mexican names the convention is
        // first-names (1 or 2 tokens) followed by two family names.
        // With 3 tokens: first=token[0], last=token[1]+token[2].
        // With 2 tokens: first=token[0], last=token[1].
        // With 1 token: first=token[0], last=null.
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
        var value = new ExtractedClientName(fullName, firstNames, lastNames);
        return ExtractedField<ExtractedClientName>.Found(value, locator);
    }

    // -----------------------------------------------------------------------
    // Address extraction (positional — right column, below client name)
    // -----------------------------------------------------------------------

    private static ExtractedField<ExtractedAddress> ExtractAddress(List<Word> headerWords)
    {
        // Address appears across 3-4 lines in the right column, Y between ~641 and ~669.
        // All lines are at X ≥ ClientNameXMin.
        var addressWords = headerWords
            .Where(w => w.BoundingBox.Left >= ClientNameXMin
                     && w.BoundingBox.Bottom >= HeaderYMin
                     && w.BoundingBox.Bottom <= AddressYMax)
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        if (addressWords.Count == 0)
            return ExtractedField<ExtractedAddress>.Missing(FieldLocator.PageHint(1));

        // Group into lines (bands).
        var addressBands = GroupIntoBands(addressWords);

        // Build raw address from all bands top→bottom.
        var lines = addressBands
            .OrderByDescending(b => b.Key)
            .Select(b => string.Join(" ", b.Value.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)))
            .ToList();

        var rawAddress = string.Join(" ", lines).Trim();
        if (string.IsNullOrWhiteSpace(rawAddress))
            return ExtractedField<ExtractedAddress>.Missing(FieldLocator.PageHint(1));

        // Decompose: look for postal code (5-digit token), state (follows postal code), etc.
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
                // Take the next token(s) before any "C.R." annotation as state.
                if (i + 1 < allTokens.Length)
                {
                    // "CDMX, CDMX C.R.00001" → state = "CDMX"
                    var candidate = allTokens[i + 1].TrimEnd(',');
                    if (!candidate.StartsWith("C.R.", StringComparison.OrdinalIgnoreCase))
                        state = candidate;
                }

                break;
            }
        }

        // Street and number: first line of the address (before neighborhood).
        // Neighborhood: tokens between the first line and the postal code.
        // First line = top band (highest Y).
        var topBandY = addressBands.Keys.Max();
        var firstLineBand = addressBands[topBandY];
        var streetAndNumber = string.Join(" ",
            firstLineBand.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)).Trim();

        // Neighborhood = tokens from lines 2..N that precede the postal code.
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
        var value = new ExtractedAddress(rawAddress, streetAndNumber, neighborhood, postalCode, state);
        return ExtractedField<ExtractedAddress>.Found(value, locator);
    }

    // -----------------------------------------------------------------------
    // Generic labeled-field extraction (label in right column, value to its right)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Finds a label consisting of consecutive words matching <paramref name="labelTokens"/>
    /// (case-insensitive, accent-insensitive) and returns the concatenated value words
    /// that appear on the same horizontal band and start to the right of the label.
    /// </summary>
    private static ExtractedField<string> ExtractLabeledField(
        Dictionary<double, List<Word>> bands,
        string[] labelTokens)
    {
        foreach (var (bandY, bandWords) in bands.OrderByDescending(b => b.Key))
        {
            // Only consider bands in the label/value zone.
            if (bandY < HeaderYMin || bandY > HeaderYMax)
                continue;

            // Sort left→right.
            var sorted = bandWords.OrderBy(w => w.BoundingBox.Left).ToList();

            // Try to find the label sequence starting at each position.
            for (var i = 0; i <= sorted.Count - labelTokens.Length; i++)
            {
                if (!MatchesLabel(sorted, i, labelTokens))
                    continue;

                // Label found — compute label's right edge.
                var labelRight = sorted[i + labelTokens.Length - 1].BoundingBox.Right;
                var locator = BoundingBoxOf(
                    sorted.Skip(i).Take(labelTokens.Length).ToList(), 1);

                // Collect value words: same band, starting at ValueColumnXMin or just right of label.
                var valueWords = sorted
                    .Where(w => w.BoundingBox.Left > labelRight
                             && w.BoundingBox.Left >= ValueColumnXMin - 10)
                    .OrderBy(w => w.BoundingBox.Left)
                    .ToList();

                if (valueWords.Count == 0)
                    return ExtractedField<string>.Missing(locator);

                var value = string.Join(" ", valueWords.Select(w => w.Text)).Trim();
                var valueLocator = BoundingBoxOf(valueWords, 1);
                return ExtractedField<string>.Found(value, valueLocator);
            }
        }

        return ExtractedField<string>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Validated field extractors
    // -----------------------------------------------------------------------

    private static ExtractedField<string> ExtractAndValidateCardNumber(Dictionary<double, List<Word>> bands)
    {
        var raw = ExtractLabeledField(bands, new[] { "Número", "de", "Tarjeta" });
        if (raw.Status == ExtractionStatus.NotExtracted)
            return raw;

        var digits = DigitsOnly.Replace(raw.Value!, string.Empty);
        if (digits.Length == 16)
            return ExtractedField<string>.Found(digits, raw.Locator);

        // Keep the normalized digits but mark as invalid format.
        return ExtractedField<string>.InvalidFormat(
            string.IsNullOrEmpty(digits) ? raw.Value! : digits,
            raw.Locator);
    }

    private static ExtractedField<string> ExtractAndValidateClabe(Dictionary<double, List<Word>> bands)
    {
        var raw = ExtractLabeledField(bands, new[] { "CLABE", "Interbancaria" });
        if (raw.Status == ExtractionStatus.NotExtracted)
            return raw;

        var digits = DigitsOnly.Replace(raw.Value!, string.Empty);
        if (digits.Length == 18)
            return ExtractedField<string>.Found(digits, raw.Locator);

        return ExtractedField<string>.InvalidFormat(
            string.IsNullOrEmpty(digits) ? raw.Value! : digits,
            raw.Locator);
    }

    private static ExtractedField<string> ExtractAndValidateRfc(Dictionary<double, List<Word>> bands)
    {
        var raw = ExtractLabeledField(bands, new[] { "RFC" });
        if (raw.Status == ExtractionStatus.NotExtracted)
            return raw;

        var value = raw.Value!.Trim().ToUpperInvariant();
        if (RfcPattern.IsMatch(value))
            return ExtractedField<string>.Found(value, raw.Locator);

        return ExtractedField<string>.InvalidFormat(value, raw.Locator);
    }

    // -----------------------------------------------------------------------
    // Band grouping utilities
    // -----------------------------------------------------------------------

    /// <summary>
    /// Groups words into horizontal bands where all words in a band have their
    /// bottom Y coordinate within <see cref="YBandTolerance"/> pt of each other.
    /// Returns a dictionary keyed by the representative Y coordinate of each band.
    /// </summary>
    private static Dictionary<double, List<Word>> GroupIntoBands(List<Word> words)
    {
        var result = new Dictionary<double, List<Word>>();

        foreach (var word in words)
        {
            var y = word.BoundingBox.Bottom;

            // Find an existing band whose representative Y is within tolerance.
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

    // -----------------------------------------------------------------------
    // Label matching (case-insensitive, accent-aware via ordinal comparison)
    // -----------------------------------------------------------------------

    private static bool MatchesLabel(List<Word> sortedBandWords, int startIndex, string[] labelTokens)
    {
        for (var i = 0; i < labelTokens.Length; i++)
        {
            if (!string.Equals(
                    sortedBandWords[startIndex + i].Text,
                    labelTokens[i],
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    // -----------------------------------------------------------------------
    // Locator helpers
    // -----------------------------------------------------------------------

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
