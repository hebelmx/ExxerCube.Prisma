namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr;

/// <summary>
/// Service for batch processing documents with XML, OCR, and comparison.
/// Optimized for stakeholder demos with small batches (max 4) and Tesseract-only OCR.
/// </summary>
public class BulkProcessingService : IBulkProcessingService
{
    private readonly IXmlNullableParser<Expediente> _xmlParser;
    private readonly IOcrProcessingService _ocrService;
    private readonly IDocumentComparisonService _comparisonService;
    private readonly OcrSanitizationService _sanitization;
    private readonly ILogger<BulkProcessingService> _logger;

    private const int MaxBatchSize = 4; // Stakeholder demo limit
    private const float OcrConfidenceThreshold = 75.0f; // Tesseract only

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkProcessingService"/> class.
    /// </summary>
    /// <param name="xmlParser">The XML parser for extracting expediente data.</param>
    /// <param name="ocrService">The OCR processing service.</param>
    /// <param name="comparisonService">The document comparison service.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="sanitization">Helper for best-effort OCR sanitization.</param>
    public BulkProcessingService(
        IXmlNullableParser<Expediente> xmlParser,
        IOcrProcessingService ocrService,
        IDocumentComparisonService comparisonService,
        ILogger<BulkProcessingService> logger,
        OcrSanitizationService sanitization)
    {
        _xmlParser = xmlParser;
        _ocrService = ocrService;
        _comparisonService = comparisonService;
        _logger = logger;
        _sanitization = sanitization;
    }

    /// <inheritdoc/>
    public async Task<Result<List<BulkDocument>>> GetRandomSampleAsync(
        int count,
        string sourceDirectory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Enforce max batch size
            var sampleSize = Math.Min(count, MaxBatchSize);

            _logger.LogInformation("Getting random sample of {Count} documents from {Directory}", sampleSize, sourceDirectory);

            if (!Directory.Exists(sourceDirectory))
            {
                return Result<List<BulkDocument>>.WithFailure($"Source directory not found: {sourceDirectory}");
            }

            // Get all subdirectories (each contains XML, PDF, DOCX, HTML)
            var allDirectories = Directory.GetDirectories(sourceDirectory);

            if (allDirectories.Length == 0)
            {
                return Result<List<BulkDocument>>.WithFailure("No documents found in bulk directory");
            }

            // Random sampling
            var random = new Random();
            var selectedDirs = allDirectories
                .OrderBy(_ => random.Next())
                .Take(sampleSize)
                .ToList();

            var bulkDocuments = new List<BulkDocument>();

            foreach (var dir in selectedDirs)
            {
                var dirName = Path.GetFileName(dir);
                var xmlPath = Directory.GetFiles(dir, "*.xml").FirstOrDefault();
                var pdfPath = Directory.GetFiles(dir, "*.pdf").FirstOrDefault();

                if (xmlPath != null && pdfPath != null)
                {
                    bulkDocuments.Add(new BulkDocument
                    {
                        Id = dirName,
                        XmlPath = xmlPath,
                        PdfPath = pdfPath,
                        Status = BulkProcessingStatus.Pending
                    });

                    _logger.LogDebug("Added document: {DocumentId}", dirName);
                }
                else
                {
                    _logger.LogWarning("Skipping {Directory}: missing XML or PDF", dirName);
                }
            }

            _logger.LogInformation("Sampled {Count} documents successfully", bulkDocuments.Count);
            return Result<List<BulkDocument>>.Success(bulkDocuments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get random sample");
            return Result<List<BulkDocument>>.WithFailure($"Sampling failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<BulkProcessingResult>> ProcessDocumentAsync(
        BulkDocument document,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new BulkProcessingResult
        {
            DocumentId = document.Id
        };

        try
        {
            _logger.LogInformation("Processing document: {DocumentId}", document.Id);

            // Step 1: Process XML
            document.Status = BulkProcessingStatus.ProcessingXml;
            var xmlContent = await File.ReadAllBytesAsync(document.XmlPath, cancellationToken);
            var xmlResult = await _xmlParser.ParseAsync(xmlContent, cancellationToken);

            if (!xmlResult.IsSuccess || xmlResult.Value == null)
            {
                result.Success = false;
                result.ErrorMessage = $"XML parsing failed: {xmlResult.Error}";
                _logger.LogWarning("XML parsing failed for {DocumentId}: {Error}", document.Id, xmlResult.Error);
                return Result<BulkProcessingResult>.Success(result);
            }

            result.XmlExpediente = xmlResult.Value;

            // Step 2: Process OCR (Tesseract only, no fallback)
            document.Status = BulkProcessingStatus.ProcessingOcr;
            var pdfBytes = await File.ReadAllBytesAsync(document.PdfPath, cancellationToken);

            var imageData = new ImageData
            {
                Data = pdfBytes,
                SourcePath = document.PdfPath,
                PageNumber = 1,
                TotalPages = 1
            };

            var ocrConfig = new ProcessingConfig
            {
                OCRConfig = new OCRConfig
                {
                    Language = "spa",
                    ConfidenceThreshold = OcrConfidenceThreshold
                },
                RemoveWatermark = true,
                Deskew = true,
                Binarize = true
            };

            var ocrResult = await _ocrService.ProcessDocumentAsync(imageData, ocrConfig, cancellationToken);

            if (!ocrResult.IsSuccess || ocrResult.Value == null)
            {
                result.Success = false;
                result.ErrorMessage = $"OCR processing failed: {ocrResult.Error}";
                _logger.LogWarning("OCR processing failed for {DocumentId}: {Error}", document.Id, ocrResult.Error);
                return Result<BulkProcessingResult>.Success(result);
            }

            result.OcrConfidence = ocrResult.Value.OCRResult.ConfidenceAvg;
            var sanitized = _sanitization.SanitizeAccountAndSwift(ocrResult.Value.OCRResult.Text);
            result.RawOcrText = sanitized.RawText;
            result.AccountSanitization = sanitized.Account;
            result.SwiftSanitization = sanitized.Swift;

            // Parse OCR to Expediente (simplified for demo)
            result.OcrExpediente = ParseOcrToExpediente(ocrResult.Value);

            // Step 3: Compare XML vs OCR
            document.Status = BulkProcessingStatus.Comparing;
            result.Comparison = await _comparisonService.CompareExpedientesAsync(
                result.XmlExpediente,
                result.OcrExpediente,
                cancellationToken);

            // Success
            document.Status = BulkProcessingStatus.Complete;
            result.Success = true;
            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Document {DocumentId} processed successfully in {Time}ms ({MatchRate:P0} match rate)",
                document.Id, result.ProcessingTimeMs, result.Comparison.MatchPercentage / 100f);

            return Result<BulkProcessingResult>.Success(result);
        }
        catch (Exception ex)
        {
            document.Status = BulkProcessingStatus.Error;
            result.Success = false;
            result.ErrorMessage = ex.Message;
            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogError(ex, "Failed to process document {DocumentId}", document.Id);
            return Result<BulkProcessingResult>.WithFailure($"Processing failed: {ex.Message}");
        }
    }

    private Expediente ParseOcrToExpediente(ProcessingResult ocrResult)
    {
        // Simplified parsing for demo - just use extracted expediente field
        return new Expediente
        {
            NumeroExpediente = ocrResult.ExtractedFields.Expediente ?? "",
            NumeroOficio = "",
            SolicitudSiara = "",
            Folio = 0,
            OficioYear = DateTime.Now.Year,
            AreaClave = 0,
            AreaDescripcion = "",
            FechaPublicacion = DateTime.MinValue,
            DiasPlazo = 0,
            AutoridadNombre = "",
            NombreSolicitante = null,
            Referencia = "",
            Referencia1 = "",
            Referencia2 = "",
            TieneAseguramiento = false,

            // Law-mandated fields - null until enriched by bank systems or classification engine
            LawMandatedFields = null,

            // Semantic analysis - null until classification engine runs
            SemanticAnalysis = null,

            // Future-proofing: capture unknown fields (not applicable for OCR extraction)
            AdditionalFields = new Dictionary<string, string>()
        };
    }
}
