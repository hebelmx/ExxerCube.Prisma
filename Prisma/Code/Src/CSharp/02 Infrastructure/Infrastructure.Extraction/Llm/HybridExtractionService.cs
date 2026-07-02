using ExxerCube.Prisma.Domain.Llm;
using ExxerCube.Prisma.Infrastructure.Classification.Llm;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Llm;

/// <summary>
/// Orchestrates the hybrid (deterministic + optional LLM) field-extraction pipeline.
/// </summary>
/// <remarks>
/// <para>Three tracks, in order:</para>
/// <list type="number">
///   <item>Deterministic — always runs via <see cref="IFieldExtractor{PdfSource}"/>.</item>
///   <item>LLM-text — gated by <c>LlmProviders:TextExtractorEnabled</c>; uses the OCR text
///         surfaced by the deterministic track in <c>AdditionalFields["_OcrText"]</c>.</item>
///   <item>LLM-vision — gated by <c>LlmProviders:VisionExtractorEnabled</c>; rasterises every
///         PDF page via <see cref="IPdfToImageConverter"/> and sends them to the vision LLM.</item>
/// </list>
/// <para>All candidates are passed to <see cref="IExtractionReconciler"/> regardless of their
/// <see cref="TrackStatus"/> so the reconciler receives a complete audit trail.</para>
/// <para>Ships DARK: both LLM flags default to <see langword="false"/> in
/// <c>appsettings.json</c> — the deterministic runtime path is unchanged.</para>
/// </remarks>
public sealed class HybridExtractionService : IHybridExtractionService
{
    private readonly IFieldExtractor<PdfSource> _deterministicPdf;
    private readonly IFieldExtractor<TxtSource> _llmText;
    private readonly IFieldExtractor<ImageSource> _llmVision;
    private readonly IPdfToImageConverter _converter;
    private readonly IExtractionReconciler _reconciler;
    private readonly IOptionsMonitor<LlmProvidersOptions> _options;
    private readonly ILogger<HybridExtractionService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="HybridExtractionService"/>.
    /// </summary>
    /// <param name="deterministicPdf">
    /// The active deterministic PDF extractor (bound to <c>PdfOcrFieldExtractor</c> in DI).
    /// </param>
    /// <param name="llmText">
    /// The LLM text extractor (concrete <c>LlmTxtFieldExtractor</c> resolved by type in DI;
    /// distinct from the <c>IFieldExtractor&lt;TxtSource&gt;</c> binding which targets
    /// <c>AdaptiveTxtFieldExtractor</c>).
    /// </param>
    /// <param name="llmVision">
    /// The LLM vision extractor (concrete <see cref="LlmVisionFieldExtractor"/> resolved by type in DI).
    /// </param>
    /// <param name="converter">PDF-to-image rasteriser used by the vision track.</param>
    /// <param name="reconciler">Per-field merge engine.</param>
    /// <param name="options">Live-switchable provider options (reads <c>CurrentValue</c> per call).</param>
    /// <param name="logger">Structured logger.</param>
    public HybridExtractionService(
        IFieldExtractor<PdfSource> deterministicPdf,
        IFieldExtractor<TxtSource> llmText,
        IFieldExtractor<ImageSource> llmVision,
        IPdfToImageConverter converter,
        IExtractionReconciler reconciler,
        IOptionsMonitor<LlmProvidersOptions> options,
        ILogger<HybridExtractionService> logger)
    {
        _deterministicPdf = deterministicPdf ?? throw new ArgumentNullException(nameof(deterministicPdf));
        _llmText = llmText ?? throw new ArgumentNullException(nameof(llmText));
        _llmVision = llmVision ?? throw new ArgumentNullException(nameof(llmVision));
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<ReconciliationResult>> ExtractAsync(
        byte[] pdfBytes,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<ReconciliationResult>();

        if (pdfBytes is null || pdfBytes.Length == 0)
            return Result<ReconciliationResult>.WithFailure("PDF bytes cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(documentId))
            return Result<ReconciliationResult>.WithFailure("Document ID cannot be null or empty.");

        var opts = _options.CurrentValue;
        var candidates = new List<LabelledExtraction>();

        // ─── Track 1: Deterministic (always runs) ───────────────────────────────
        var pdfSource = new PdfSource { FileContent = pdfBytes, FilePath = documentId };

        _logger.LogDebug(
            "HybridExtractionService: starting deterministic track for '{DocumentId}'.", documentId);

        Result<ExtractedFields> detResult;
        try
        {
            detResult = await _deterministicPdf
                .ExtractFieldsAsync(pdfSource, [])
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HybridExtractionService: deterministic extractor threw unexpectedly.");
            detResult = Result<ExtractedFields>.WithFailure($"Deterministic extractor threw: {ex.Message}");
        }

        if (detResult.IsSuccess && detResult.Value is not null)
        {
            var detExpediente = MapToExpediente(detResult.Value);
            candidates.Add(new LabelledExtraction("deterministic", detExpediente, TrackStatus.Available));
            _logger.LogDebug("HybridExtractionService: deterministic track succeeded.");
        }
        else
        {
            var errMsg = detResult.Errors?.FirstOrDefault() ?? "deterministic extraction failed";
            _logger.LogWarning("HybridExtractionService: deterministic track failed — {Error}.", errMsg);
            candidates.Add(new LabelledExtraction("deterministic", null, TrackStatus.Failed, errMsg));
        }

        // ─── Track 2: LLM-text (gated by TextExtractorEnabled) ──────────────────
        if (opts.TextExtractorEnabled)
        {
            string ocrText = string.Empty;
            if (detResult.IsSuccess
                && detResult.Value?.AdditionalFields.TryGetValue("_OcrText", out var rawText) == true)
            {
                ocrText = rawText ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(ocrText))
            {
                const string noTextMsg =
                    "No OCR text available from the deterministic track; skipping llm-text.";
                _logger.LogWarning("HybridExtractionService: {Message}", noTextMsg);
                candidates.Add(new LabelledExtraction("llm-text", null, TrackStatus.Failed, noTextMsg));
            }
            else
            {
                _logger.LogDebug(
                    "HybridExtractionService: starting llm-text track ({Length} chars).",
                    ocrText.Length);

                var txtSource = new TxtSource(ocrText);

                Result<ExtractedFields> txtResult;
                try
                {
                    txtResult = await _llmText
                        .ExtractFieldsAsync(txtSource, [])
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "HybridExtractionService: llm-text extractor threw unexpectedly.");
                    txtResult = Result<ExtractedFields>.WithFailure($"LLM text extractor threw: {ex.Message}");
                }

                if (txtResult.IsSuccess && txtResult.Value is not null)
                {
                    candidates.Add(new LabelledExtraction(
                        "llm-text",
                        MapToExpediente(txtResult.Value),
                        TrackStatus.Available));
                }
                else
                {
                    var errMsg = txtResult.Errors?.FirstOrDefault() ?? "llm-text extraction failed";
                    candidates.Add(new LabelledExtraction("llm-text", null, TrackStatus.Failed, errMsg));
                }
            }
        }
        else
        {
            _logger.LogDebug("HybridExtractionService: llm-text track is disabled by flag.");
        }

        // ─── Track 3: LLM-vision (gated by VisionExtractorEnabled) ─────────────
        if (opts.VisionExtractorEnabled)
        {
            _logger.LogDebug(
                "HybridExtractionService: rasterising PDF for llm-vision track ('{DocumentId}').",
                documentId);

            Result<IReadOnlyList<byte[]>> convResult;
            try
            {
                convResult = await _converter
                    .ConvertToImagesAsync(pdfBytes, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HybridExtractionService: PDF-to-image conversion threw.");
                convResult = Result<IReadOnlyList<byte[]>>.WithFailure(
                    $"PDF-to-image conversion threw: {ex.Message}");
            }

            if (!convResult.IsSuccess || convResult.Value is null || convResult.Value.Count == 0)
            {
                var errMsg = (!convResult.IsSuccess)
                    ? convResult.Errors?.FirstOrDefault() ?? "PDF conversion failed"
                    : "PDF produced no pages";
                candidates.Add(new LabelledExtraction("llm-vision", null, TrackStatus.Failed, errMsg));
            }
            else
            {
                var imageSource = new ImageSource(documentId, convResult.Value);

                Result<ExtractedFields> visResult;
                try
                {
                    visResult = await _llmVision
                        .ExtractFieldsAsync(imageSource, [])
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "HybridExtractionService: llm-vision extractor threw unexpectedly.");
                    visResult = Result<ExtractedFields>.WithFailure(
                        $"LLM vision extractor threw: {ex.Message}");
                }

                if (visResult.IsSuccess && visResult.Value is not null)
                {
                    candidates.Add(new LabelledExtraction(
                        "llm-vision",
                        MapToExpediente(visResult.Value),
                        TrackStatus.Available));
                }
                else
                {
                    var errMsg = visResult.Errors?.FirstOrDefault() ?? "llm-vision extraction failed";
                    // Distinguish provider capability gap from a general runtime failure.
                    var status = errMsg.Contains("VisionGenerate", StringComparison.OrdinalIgnoreCase)
                        ? TrackStatus.SkippedNoCapability
                        : TrackStatus.Failed;
                    candidates.Add(new LabelledExtraction("llm-vision", null, status, errMsg));
                }
            }
        }
        else
        {
            _logger.LogDebug("HybridExtractionService: llm-vision track is disabled by flag.");
        }

        // ─── Reconcile ────────────────────────────────────────────────────────────
        _logger.LogInformation(
            "HybridExtractionService: reconciling {CandidateCount} candidate(s) for '{DocumentId}'.",
            candidates.Count, documentId);

        return await _reconciler
            .ReconcileAsync(candidates, cancellationToken)
            .ConfigureAwait(false);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Maps an <see cref="ExtractedFields"/> value object to an <see cref="Expediente"/> entity.
    /// Follows the same field-to-property mapping that
    /// <c>PdfProcessingService.MapExtractedFieldsToExpediente</c> uses in the Web.UI layer.
    /// </summary>
    private static Expediente MapToExpediente(ExtractedFields fields)
    {
        var additional = fields.AdditionalFields;

        var expediente = new Expediente
        {
            NumeroExpediente = fields.Expediente ?? string.Empty,
            NumeroOficio = additional.GetValueOrDefault("NumeroOficio") ?? string.Empty,
            SolicitudSiara = additional.GetValueOrDefault("SolicitudSiara") ?? string.Empty,
            AreaDescripcion = additional.GetValueOrDefault("AreaDescripcion") ?? string.Empty,
            AutoridadNombre = additional.GetValueOrDefault("AutoridadNombre") ?? string.Empty,
            AutoridadEspecificaNombre = additional.GetValueOrDefault("AutoridadEspecificaNombre"),
            NombreSolicitante = additional.GetValueOrDefault("NombreSolicitante"),
            FundamentoLegal = additional.GetValueOrDefault("FundamentoLegal") ?? string.Empty,
            Referencia1 = fields.Causa ?? string.Empty,
            Referencia2 = fields.AccionSolicitada ?? string.Empty,
        };

        if (additional.TryGetValue("TieneAseguramiento", out var asegStr)
            && bool.TryParse(asegStr, out var tieneAseg))
        {
            expediente.TieneAseguramiento = tieneAseg;
        }

        if (additional.TryGetValue("FechaPublicacion", out var fechaPubStr)
            && DateTime.TryParse(
                fechaPubStr,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var fechaPub))
        {
            expediente.FechaPublicacion = fechaPub;
        }

        if (additional.TryGetValue("DiasPlazo", out var diasStr)
            && int.TryParse(diasStr, out var dias))
        {
            expediente.DiasPlazo = dias;
        }

        // Carry forward all additional fields that are not already mapped.
        foreach (var kvp in additional)
        {
            if (kvp.Value is not null && !expediente.AdditionalFields.ContainsKey(kvp.Key))
            {
                expediente.AdditionalFields[kvp.Key] = kvp.Value;
            }
        }

        return expediente;
    }
}
