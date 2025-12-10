namespace ExxerCube.Prisma.Web.UI.Services;

using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Web.UI.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Coordinates comparison and intelligent fusion of Expediente entities from multiple sources.
/// Orchestrates both simple 2-way comparison and intelligent 3-way fusion.
/// </summary>
public sealed class DocumentComparisonCoordinator
{
    private readonly IDocumentComparisonService _comparisonService;
    private readonly IFusionExpediente _fusionService;
    private readonly ILogger<DocumentComparisonCoordinator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentComparisonCoordinator"/> class.
    /// </summary>
    /// <param name="comparisonService">Document comparison service</param>
    /// <param name="fusionService">Intelligent fusion service</param>
    /// <param name="logger">Logger instance</param>
    public DocumentComparisonCoordinator(
        IDocumentComparisonService comparisonService,
        IFusionExpediente fusionService,
        ILogger<DocumentComparisonCoordinator> logger)
    {
        _comparisonService = comparisonService;
        _fusionService = fusionService;
        _logger = logger;
    }

    /// <summary>
    /// Compares and fuses Expediente entities from XML and PDF sources.
    /// Performs both simple comparison and intelligent 3-way fusion.
    /// </summary>
    /// <param name="viewModel">Document processing view model containing source data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing comparison and fusion results</returns>
    public async Task<ComparisonAndFusionResult> CompareAndFuseAsync(
        DocumentProcessingViewModel viewModel,
        CancellationToken cancellationToken = default)
    {
        // Validate state
        if (viewModel.XmlState.Expediente == null)
        {
            throw new InvalidOperationException("XML Expediente is required for comparison");
        }

        if (viewModel.PdfState.Expediente == null)
        {
            throw new InvalidOperationException("PDF Expediente is required for comparison");
        }

        try
        {
            _logger.LogInformation("Starting comparison: XML vs PDF");

            // Perform simple 2-way comparison
            var comparisonResult = await _comparisonService.CompareExpedientesAsync(
                viewModel.XmlState.Expediente,
                viewModel.PdfState.Expediente);

            _logger.LogInformation(
                "Comparison complete: {MatchCount}/{TotalFields} matches ({MatchPercentage:F0}%)",
                comparisonResult.MatchCount, comparisonResult.TotalFields, comparisonResult.MatchPercentage);

            // Perform intelligent 3-way fusion
            _logger.LogDebug("Performing intelligent 3-way fusion...");

            // Use existing metadata from states, or create default
            var xmlMetadata = viewModel.XmlState.Metadata ?? CreateXmlMetadata(viewModel.XmlState);
            var pdfMetadata = viewModel.PdfState.Metadata ?? CreatePdfMetadata(viewModel.PdfState);
            var docxMetadata = new ExtractionMetadata // Placeholder for future DOCX support
            {
                Source = SourceType.DOCX_OCR_Authority,
                RegexMatches = 0,
                TotalFieldsExtracted = 0,
                PatternViolations = 0,
                CatalogValidations = 0
            };

            // Call fusion service (DOCX is null - 2-way fusion for now)
            var fusionResultResponse = await _fusionService.FuseAsync(
                viewModel.XmlState.Expediente,
                viewModel.PdfState.Expediente,
                null, // DOCX not used for demo
                xmlMetadata,
                pdfMetadata,
                docxMetadata,
                cancellationToken);

            FusionResult? fusionResult = null;
            if (fusionResultResponse.IsSuccess && fusionResultResponse.Value != null)
            {
                fusionResult = fusionResultResponse.Value;
                _logger.LogInformation(
                    "Fusion complete: OverallConfidence={Confidence:F2}, NextAction={NextAction}",
                    fusionResult.OverallConfidence, fusionResult.NextAction);
            }
            else
            {
                _logger.LogWarning("Fusion failed: {Error}", fusionResultResponse.Error);
            }

            return new ComparisonAndFusionResult
            {
                Comparison = comparisonResult,
                Fusion = fusionResult
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during comparison and fusion");
            throw;
        }
    }

    /// <summary>
    /// Creates default XML extraction metadata.
    /// </summary>
    private ExtractionMetadata CreateXmlMetadata(XmlProcessingState xmlState)
    {
        return new ExtractionMetadata
        {
            Source = SourceType.XML_HandFilled,
            RegexMatches = 0,
            TotalFieldsExtracted = xmlState.FieldCount,
            PatternViolations = 0,
            CatalogValidations = 0
        };
    }

    /// <summary>
    /// Creates default PDF extraction metadata.
    /// </summary>
    private ExtractionMetadata CreatePdfMetadata(PdfProcessingState pdfState)
    {
        var ocrConfidence = pdfState.OcrResult?.OCRResult.ConfidenceAvg ?? 0.0f;

        return new ExtractionMetadata
        {
            Source = SourceType.PDF_OCR_CNBV,
            MeanConfidence = ocrConfidence / 100.0,
            QualityIndex = 0.8,
            RegexMatches = 0,
            TotalFieldsExtracted = pdfState.Expediente != null ? CountPdfFields(pdfState.Expediente) : 0,
            PatternViolations = 0,
            CatalogValidations = 0
        };
    }

    /// <summary>
    /// Counts extracted fields in PDF Expediente.
    /// </summary>
    private int CountPdfFields(Expediente expediente)
    {
        int count = 0;

        if (!string.IsNullOrWhiteSpace(expediente.NumeroExpediente)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.NumeroOficio)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.SolicitudSiara)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.AutoridadNombre)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.NombreSolicitante)) count++;
        if (expediente.FechaPublicacion != DateTime.MinValue) count++;
        if (expediente.DiasPlazo != 0) count++;

        return count;
    }
}

/// <summary>
/// Result of comparison and fusion operations.
/// </summary>
public sealed record ComparisonAndFusionResult
{
    /// <summary>
    /// Result of simple 2-way comparison.
    /// </summary>
    public required ComparisonResult Comparison { get; init; }

    /// <summary>
    /// Result of intelligent 3-way fusion (may be null if fusion failed).
    /// </summary>
    public FusionResult? Fusion { get; init; }
}
