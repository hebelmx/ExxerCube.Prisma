using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.Sources;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Prisma.Athena.Processing;

/// <summary>
/// The <em>Extractor</em> half of the processing pipeline (MVP-PATH 1.4 Reconciliator edge, ADR-011): runs
/// Stage 1 Quality → Stage 2 OCR → Stage 3 Fusion and returns the fused result. This is the work that runs in
/// the Athena Extractor process; the downstream Classification → Export runs in the separate Reconciliator
/// process (<see cref="ReconciliationOrchestrator"/>). The monolithic <see cref="ProcessingOrchestrator"/>
/// composes both halves so the in-process path is preserved.
/// </summary>
/// <remarks>
/// Stage logic and the emitted domain events are unchanged from the original monolith — only their home moved.
/// Quality rejection short-circuits (no OCR/Fusion) and is signalled via
/// <see cref="ExtractionResult.QualityRejected"/>; the caller owns the completion event and the defensive
/// try/catch.
/// </remarks>
public sealed class ExtractionOrchestrator
{
    private readonly IImageQualityAnalyzer? _qualityAnalyzer;
    private readonly IOcrExecutor? _ocrExecutor;
    private readonly IFusionExpediente? _fusionService;
    private readonly IFileLoader? _fileLoader;
    private readonly IFieldExtractor<TxtSource>? _txtFieldExtractor;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger _logger;

    /// <summary>Quality confidence threshold below which documents are rejected (Q1_Poor.Value = 1).</summary>
    private const int QualityRejectionThreshold = 2;

    /// <summary>Initializes a new instance of the <see cref="ExtractionOrchestrator"/> class.</summary>
    /// <param name="eventPublisher">The event publisher for domain events.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="qualityAnalyzer">Optional: Stage 1 quality analysis service.</param>
    /// <param name="ocrExecutor">Optional: Stage 2 OCR execution service.</param>
    /// <param name="fusionService">Optional: Stage 3 fusion service.</param>
    /// <param name="fileLoader">Optional: File loader for reading images from disk.</param>
    /// <param name="txtFieldExtractor">Optional: Field extractor turning Stage 2 OCR text into an Expediente for Stage 3 fusion.</param>
    public ExtractionOrchestrator(
        IEventPublisher eventPublisher,
        ILogger logger,
        IImageQualityAnalyzer? qualityAnalyzer = null,
        IOcrExecutor? ocrExecutor = null,
        IFusionExpediente? fusionService = null,
        IFileLoader? fileLoader = null,
        IFieldExtractor<TxtSource>? txtFieldExtractor = null)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _qualityAnalyzer = qualityAnalyzer;
        _ocrExecutor = ocrExecutor;
        _fusionService = fusionService;
        _fileLoader = fileLoader;
        _txtFieldExtractor = txtFieldExtractor;
    }

    /// <summary>
    /// Runs Stage 1 Quality → Stage 2 OCR → Stage 3 Fusion for the given document.
    /// </summary>
    /// <param name="downloadEvent">The document downloaded event triggering extraction.</param>
    /// <param name="cancellationToken">Cancellation token; honored between stages.</param>
    /// <returns>The extraction result (image/ocr/fusion data, rejection flag, stages completed 0–3).</returns>
    public async Task<ExtractionResult> ExtractAsync(
        DocumentDownloadedEvent downloadEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(downloadEvent);
        cancellationToken.ThrowIfCancellationRequested();

        var fileId = downloadEvent.FileId;
        var correlationId = downloadEvent.CorrelationId;
        var stagesCompleted = 0;

        // STAGE 1: Quality Analysis
        var qualityPassed = await ExecuteStage1QualityAnalysisAsync(
            downloadEvent, fileId, correlationId, cancellationToken);

        var imageData = qualityPassed.imageData;
        if (imageData != null)
        {
            stagesCompleted++;
        }

        if (qualityPassed.rejected)
        {
            return new ExtractionResult(imageData, null, null, QualityRejected: true, stagesCompleted);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // STAGE 2: OCR Execution
        var ocrResult = await ExecuteStage2OcrAsync(imageData, fileId, correlationId, cancellationToken);
        if (ocrResult != null)
        {
            stagesCompleted++;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // STAGE 3: Fusion/Reconciliation
        var fusionResult = await ExecuteStage3FusionAsync(ocrResult, fileId, correlationId, cancellationToken);
        if (fusionResult != null)
        {
            stagesCompleted++;
        }

        return new ExtractionResult(imageData, ocrResult, fusionResult, QualityRejected: false, stagesCompleted);
    }

    // ========================================================================
    // Stage 1: Quality Analysis
    // ========================================================================

    private async Task<(ImageData? imageData, bool rejected)> ExecuteStage1QualityAnalysisAsync(
        DocumentDownloadedEvent downloadEvent,
        Guid fileId,
        Guid? correlationId,
        CancellationToken cancellationToken)
    {
        if (_qualityAnalyzer == null || _fileLoader == null)
        {
            _logger.LogWarning("Stage 1 skipped: Quality analyzer or file loader not configured");
            return (null, false);
        }

        _logger.LogInformation("Stage 1: Quality Analysis - FileId: {FileId}", fileId);

        var loadResult = await _fileLoader.LoadImageAsync(downloadEvent.FileName, cancellationToken);
        if (loadResult.IsFailure)
        {
            _logger.LogWarning("Stage 1: Failed to load image - {Error}", loadResult.Error);
            EmitProcessingError(fileId, correlationId, "FileLoader", loadResult.Error ?? "Unknown error");
            return (null, false);
        }

        var imageData = loadResult.Value!;
        var qualityResult = await _qualityAnalyzer.AnalyzeAsync(imageData);
        if (qualityResult.IsFailure)
        {
            _logger.LogWarning("Stage 1: Quality analysis failed - {Error}", qualityResult.Error);
            EmitProcessingError(fileId, correlationId, "QualityAnalyzer", qualityResult.Error ?? "Unknown error");
            return (imageData, false);
        }

        var assessment = qualityResult.Value!;

        if ((int)assessment.QualityLevel < QualityRejectionThreshold)
        {
            _logger.LogWarning(
                "Stage 1: Quality rejected. FileId: {FileId}, Level: {Level}, Confidence: {Confidence}",
                fileId, assessment.QualityLevel.Name, assessment.Confidence);

            var rejectedEvent = new QualityRejectedEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId,
                FileId = fileId,
                Score = (decimal)assessment.Confidence,
                Reason = $"Quality level {assessment.QualityLevel.Name} below threshold"
            };
            _eventPublisher.Publish(rejectedEvent);

            return (imageData, true);
        }

        var qualityEvent = new QualityAnalysisCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            QualityLevel = assessment.QualityLevel,
            BlurScore = (decimal)assessment.BlurScore,
            NoiseScore = (decimal)assessment.NoiseLevel,
            ContrastScore = (decimal)assessment.ContrastLevel,
            SharpnessScore = (decimal)assessment.SharpnessLevel
        };
        _eventPublisher.Publish(qualityEvent);

        _logger.LogInformation("Stage 1 complete: Quality analysis - FileId: {FileId}, Level: {Level}",
            fileId, assessment.QualityLevel.Name);

        return (imageData, false);
    }

    // ========================================================================
    // Stage 2: OCR Execution
    // ========================================================================

    private async Task<OCRResult?> ExecuteStage2OcrAsync(
        ImageData? imageData,
        Guid fileId,
        Guid? correlationId,
        CancellationToken cancellationToken)
    {
        if (_ocrExecutor == null || _fileLoader == null)
        {
            _logger.LogWarning("Stage 2 skipped: OCR executor or file loader not configured");
            return null;
        }

        if (imageData == null)
        {
            _logger.LogWarning("Stage 2 skipped: No image data from Stage 1");
            return null;
        }

        _logger.LogInformation("Stage 2: OCR Execution - FileId: {FileId}", fileId);

        var ocrConfig = new OCRConfig();
        var ocrStopwatch = Stopwatch.StartNew();
        var ocrResult = await _ocrExecutor.ExecuteOcrAsync(imageData, ocrConfig);
        ocrStopwatch.Stop();

        if (ocrResult.IsFailure)
        {
            _logger.LogWarning("Stage 2: OCR failed - {Error}", ocrResult.Error);
            EmitProcessingError(fileId, correlationId, "OCR", ocrResult.Error ?? "Unknown error");
            return null;
        }

        var result = ocrResult.Value!;

        var ocrEvent = new OcrCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            OcrEngine = "Tesseract",
            Confidence = (decimal)result.ConfidenceAvg,
            ExtractedTextLength = result.Text.Length,
            ProcessingTime = ocrStopwatch.Elapsed,
            FallbackTriggered = false
        };
        _eventPublisher.Publish(ocrEvent);

        _logger.LogInformation("Stage 2 complete: OCR execution - FileId: {FileId}, Confidence: {Confidence}",
            fileId, result.ConfidenceAvg);

        return result;
    }

    // ========================================================================
    // Stage 3: Fusion/Reconciliation
    // ========================================================================

    private async Task<FusionResult?> ExecuteStage3FusionAsync(
        OCRResult? ocrResult,
        Guid fileId,
        Guid? correlationId,
        CancellationToken cancellationToken)
    {
        if (_fusionService == null)
        {
            _logger.LogWarning("Stage 3 skipped: Fusion service not configured");
            return null;
        }

        _logger.LogInformation("Stage 3: Fusion/Reconciliation - FileId: {FileId}", fileId);

        var (pdfExpediente, pdfMetadata) = await BuildPdfExpedienteFromOcrAsync(ocrResult, cancellationToken);
        var xmlMetadata = new ExtractionMetadata();
        var docxMetadata = new ExtractionMetadata();

        var fusionResultObj = await _fusionService.FuseAsync(
            null, pdfExpediente, null,
            xmlMetadata, pdfMetadata, docxMetadata,
            cancellationToken);

        if (fusionResultObj.IsFailure)
        {
            _logger.LogWarning("Stage 3: Fusion failed - {Error}", fusionResultObj.Error);
            EmitProcessingError(fileId, correlationId, "Fusion", fusionResultObj.Error ?? "Unknown error");
            return null;
        }

        var result = fusionResultObj.Value!;

        foreach (var conflictField in result.ConflictingFields)
        {
            var conflictEvent = new ConflictDetectedEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId,
                FileId = fileId,
                FieldName = conflictField,
                ConflictSeverity = "Medium"
            };
            _eventPublisher.Publish(conflictEvent);
        }

        var fusionEvent = new FusionCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            ExpedienteId = Guid.NewGuid(),
            FieldsFused = result.FieldResults.Count,
            ConflictsDetected = result.ConflictingFields.Count
        };
        _eventPublisher.Publish(fusionEvent);

        _logger.LogInformation("Stage 3 complete: Fusion - FileId: {FileId}, Conflicts: {Conflicts}",
            fileId, result.ConflictingFields.Count);

        return result;
    }

    // ========================================================================
    // Helper methods (shared with ProcessingOrchestrator's ROP path)
    // ========================================================================

    /// <summary>
    /// Turns Stage 2 OCR output into a PDF-source <see cref="Expediente"/> plus extraction metadata for Stage 3
    /// fusion. Returns (null, empty metadata) when no field extractor is configured or no OCR text exists —
    /// preserving the legacy behavior of feeding fusion empty inputs. Internal so the monolith ROP path reuses it.
    /// </summary>
    internal async Task<(Expediente? Expediente, ExtractionMetadata Metadata)> BuildPdfExpedienteFromOcrAsync(
        OCRResult? ocrResult,
        CancellationToken cancellationToken)
    {
        if (_txtFieldExtractor == null || string.IsNullOrWhiteSpace(ocrResult?.Text))
        {
            return (null, new ExtractionMetadata());
        }

        var confidence = (float)(ocrResult!.ConfidenceAvg / 100.0);
        var txtSource = new TxtSource(
            textContent: ocrResult.Text,
            ocrConfidence: confidence,
            sourceFilePath: null);

        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("NumeroOficio"),
            new FieldDefinition("AutoridadNombre"),
            new FieldDefinition("Causa"),
            new FieldDefinition("AccionSolicitada"),
        };

        var extractionResult = await _txtFieldExtractor.ExtractFieldsAsync(txtSource, fieldDefinitions);
        if (extractionResult.IsFailure || extractionResult.Value == null)
        {
            _logger.LogWarning(
                "Stage 3: OCR field extraction produced no fields ({Error}); fusion will run without PDF input",
                extractionResult.Error);
            return (null, new ExtractionMetadata());
        }

        var fields = extractionResult.Value;
        var expediente = MapExtractedFieldsToExpediente(fields);
        var metadata = new ExtractionMetadata
        {
            Source = SourceType.PDF_OCR_CNBV,
            MeanConfidence = confidence,
            TotalFieldsExtracted = CountExtractedFields(expediente),
        };

        _logger.LogInformation(
            "Stage 3: Built PDF Expediente from OCR - NumeroExpediente: {NumeroExpediente}, FieldsExtracted: {Count}",
            expediente.NumeroExpediente, metadata.TotalFieldsExtracted);

        return (expediente, metadata);
    }

    private static Expediente MapExtractedFieldsToExpediente(ExtractedFields fields)
    {
        var additional = fields.AdditionalFields ?? new Dictionary<string, string?>();

        var expediente = new Expediente
        {
            NumeroExpediente = fields.Expediente ?? string.Empty,
            NumeroOficio = additional.GetValueOrDefault("NumeroOficio") ?? string.Empty,
            AutoridadNombre = additional.GetValueOrDefault("AutoridadNombre") ?? string.Empty,
            Referencia1 = fields.Causa ?? string.Empty,
            Referencia2 = fields.AccionSolicitada ?? string.Empty,
        };

        foreach (var kvp in additional)
        {
            if (kvp.Value != null && !expediente.AdditionalFields.ContainsKey(kvp.Key))
            {
                expediente.AdditionalFields[kvp.Key] = kvp.Value;
            }
        }

        return expediente;
    }

    private static int CountExtractedFields(Expediente expediente)
    {
        int count = 0;
        if (!string.IsNullOrWhiteSpace(expediente.NumeroExpediente)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.NumeroOficio)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.AutoridadNombre)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.Referencia1)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.Referencia2)) count++;
        return count;
    }

    private void EmitProcessingError(Guid fileId, Guid? correlationId, string component, string errorMessage)
    {
        var errorEvent = new ProcessingErrorEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            ErrorMessage = errorMessage,
            Component = component
        };
        _eventPublisher.Publish(errorEvent);
    }
}

/// <summary>
/// The outcome of the Extractor half (Stages 1–3): the intermediate data plus whether quality rejected the
/// document and how many of the three stages completed. <see cref="ProcessingOrchestrator"/> and the Athena
/// Extractor worker use this to decide the handoff to the Reconciliator.
/// </summary>
/// <param name="ImageData">The loaded image (null when Stage 1 was skipped or load failed).</param>
/// <param name="OcrResult">The OCR result (null when skipped/failed).</param>
/// <param name="FusionResult">The fused result (null when skipped/failed) — the handoff payload.</param>
/// <param name="QualityRejected">True when Stage 1 rejected the document (pipeline short-circuited).</param>
/// <param name="StagesCompleted">How many of Stages 1–3 completed.</param>
public sealed record ExtractionResult(
    ImageData? ImageData,
    OCRResult? OcrResult,
    FusionResult? FusionResult,
    bool QualityRejected,
    int StagesCompleted);
