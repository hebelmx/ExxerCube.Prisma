using System;
using System.Diagnostics;
using System.Reactive.Linq;
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
/// Orchestrates folder/event-driven processing pipeline (logic only, host-agnostic).
/// Coordinates: Quality → OCR → Fusion → Classification → Export pipeline.
/// </summary>
public sealed class ProcessingOrchestrator
{
    private readonly IImageQualityAnalyzer? _qualityAnalyzer;
    private readonly IOcrExecutor? _ocrExecutor;
    private readonly IFusionExpediente? _fusionService;
    private readonly IFileClassifier? _classifier;
    private readonly IAdaptiveExporter? _exporter;
    private readonly IFileLoader? _fileLoader;
    private readonly IFieldExtractor<TxtSource>? _txtFieldExtractor;
    private readonly IEventPublisher _eventPublisher;
    private readonly IExxerHub<DocumentProcessingCompletedEvent>? _eventHub;
    private readonly ILogger<ProcessingOrchestrator> _logger;
    private IDisposable? _eventSubscription;

    /// <summary>
    /// Quality confidence threshold below which documents are rejected.
    /// Images with quality level Q1_Poor are rejected.
    /// </summary>
    private const int QualityRejectionThreshold = 2; // Q1_Poor.Value = 1, anything below 2 is rejected

    /// <summary>
    /// Classification confidence threshold below which documents are flagged for review.
    /// </summary>
    private const int ClassificationConfidenceThreshold = 70;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingOrchestrator"/> class.
    /// </summary>
    /// <param name="eventPublisher">The event publisher for domain events.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="eventHub">Optional: Event hub for Railway-Oriented Programming event broadcasting.</param>
    /// <param name="qualityAnalyzer">Optional: Quality analysis service for image assessment.</param>
    /// <param name="ocrExecutor">Optional: OCR execution service for text extraction.</param>
    /// <param name="fusionService">Optional: Fusion service for data reconciliation.</param>
    /// <param name="classifier">Optional: Classification service for document categorization.</param>
    /// <param name="exporter">Optional: Export service for generating output files.</param>
    /// <param name="fileLoader">Optional: File loader for reading images from disk.</param>
    /// <param name="txtFieldExtractor">Optional: Field extractor used to turn Stage 2 OCR text into an Expediente that feeds Stage 3 fusion. When null, fusion runs without OCR-derived input (legacy behavior).</param>
    /// <remarks>
    /// Pipeline services are optional to support incremental testing.
    /// When null, that pipeline stage is skipped with a warning log.
    /// </remarks>
    public ProcessingOrchestrator(
        IEventPublisher eventPublisher,
        ILogger<ProcessingOrchestrator> logger,
        IExxerHub<DocumentProcessingCompletedEvent>? eventHub = null,
        IImageQualityAnalyzer? qualityAnalyzer = null,
        IOcrExecutor? ocrExecutor = null,
        IFusionExpediente? fusionService = null,
        IFileClassifier? classifier = null,
        IAdaptiveExporter? exporter = null,
        IFileLoader? fileLoader = null,
        IFieldExtractor<TxtSource>? txtFieldExtractor = null)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventHub = eventHub;
        _qualityAnalyzer = qualityAnalyzer;
        _ocrExecutor = ocrExecutor;
        _fusionService = fusionService;
        _classifier = classifier;
        _exporter = exporter;
        _fileLoader = fileLoader;
        _txtFieldExtractor = txtFieldExtractor;
    }

    /// <summary>
    /// Processes a document through the complete pipeline: Quality → OCR → Fusion → Classification → Export.
    /// </summary>
    /// <param name="downloadEvent">The document downloaded event triggering processing.</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when downloadEvent is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested.</exception>
    public async Task ProcessDocumentAsync(
        DocumentDownloadedEvent downloadEvent,
        CancellationToken cancellationToken = default)
    {
        if (downloadEvent is null)
        {
            throw new ArgumentNullException(nameof(downloadEvent));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();
        var fileId = downloadEvent.FileId;
        var correlationId = downloadEvent.CorrelationId;
        var stagesCompleted = 0;
        ImageData? imageData = null;
        OCRResult? ocrResult = null;
        FusionResult? fusionResult = null;
        ClassificationResult? classificationResult = null;

        _logger.LogInformation(
            "Starting document processing pipeline. FileId: {FileId}, CorrelationId: {CorrelationId}, FileName: {FileName}",
            fileId,
            correlationId,
            downloadEvent.FileName);

        try
        {
            // STAGE 1: Quality Analysis
            var qualityPassed = await ExecuteStage1QualityAnalysisAsync(
                downloadEvent, fileId, correlationId, cancellationToken);

            if (qualityPassed.imageData != null)
            {
                imageData = qualityPassed.imageData;
                stagesCompleted++;
            }

            if (qualityPassed.rejected)
            {
                // Short-circuit: quality too low
                stopwatch.Stop();
                EmitCompletionEvent(fileId, correlationId, stopwatch.Elapsed, stagesCompleted, autoProcessed: false);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // STAGE 2: OCR Execution
            ocrResult = await ExecuteStage2OcrAsync(imageData, fileId, correlationId, cancellationToken);
            if (ocrResult != null)
            {
                stagesCompleted++;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // STAGE 3: Fusion/Reconciliation
            fusionResult = await ExecuteStage3FusionAsync(ocrResult, fileId, correlationId, cancellationToken);
            if (fusionResult != null)
            {
                stagesCompleted++;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // STAGE 4: Classification
            classificationResult = await ExecuteStage4ClassificationAsync(
                ocrResult, fusionResult, fileId, correlationId, cancellationToken);
            if (classificationResult != null)
            {
                stagesCompleted++;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // STAGE 5: Export
            var exported = await ExecuteStage5ExportAsync(
                fusionResult, classificationResult, fileId, correlationId, cancellationToken);
            if (exported)
            {
                stagesCompleted++;
            }

            stopwatch.Stop();
            EmitCompletionEvent(fileId, correlationId, stopwatch.Elapsed, stagesCompleted, autoProcessed: true);

            _logger.LogInformation(
                "Document processing pipeline completed successfully. FileId: {FileId}, Duration: {Duration}ms, StagesCompleted: {Stages}",
                fileId,
                stopwatch.ElapsedMilliseconds,
                stagesCompleted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Document processing pipeline cancelled. FileId: {FileId}, Duration: {Duration}ms",
                fileId,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Document processing pipeline failed. FileId: {FileId}, CorrelationId: {CorrelationId}, Duration: {Duration}ms, Error: {ErrorMessage}",
                fileId,
                correlationId,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            var errorEvent = new ProcessingErrorEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId,
                FileId = fileId,
                ErrorMessage = ex.Message,
                StackTrace = ex.StackTrace ?? string.Empty,
                Component = "ProcessingOrchestrator"
            };

            _eventPublisher.Publish(errorEvent);
            _logger.LogWarning("Error event published, continuing operation (defensive mode)");
        }
    }

    /// <summary>
    /// Processes a document through the complete pipeline using Railway-Oriented Programming.
    /// Returns Result&lt;ProcessingResult&gt; instead of throwing exceptions.
    /// </summary>
    /// <param name="downloadEvent">The document downloaded event triggering processing.</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>A Result containing ProcessingResult on success, or error messages on failure.</returns>
    public async Task<Result<ProcessingResult>> ProcessDocumentWithResultAsync(
        DocumentDownloadedEvent downloadEvent,
        CancellationToken cancellationToken = default)
    {
        if (downloadEvent is null)
        {
            return Result<ProcessingResult>.WithFailure("Download event cannot be null");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<ProcessingResult>();
        }

        var stopwatch = Stopwatch.StartNew();
        var fileId = downloadEvent.FileId;
        var correlationId = downloadEvent.CorrelationId;

        _logger.LogInformation(
            "Starting document processing pipeline (ROP). FileId: {FileId}, CorrelationId: {CorrelationId}, FileName: {FileName}",
            fileId,
            correlationId,
            downloadEvent.FileName);

        try
        {
            // ROP chain: each stage returns Result<ProcessingContext>
            var initialContext = new ProcessingContext(fileId, correlationId, downloadEvent.FileName);

            var result = await LoadImageAsync(initialContext, cancellationToken)
                .ThenAsync(ctx => AnalyzeQualityAsync(ctx, cancellationToken))
                .ThenAsync(ctx => ExecuteOcrRopAsync(ctx, cancellationToken))
                .ThenAsync(ctx => FuseDataRopAsync(ctx, cancellationToken))
                .ThenAsync(ctx => ClassifyDocumentRopAsync(ctx, cancellationToken))
                .ThenAsync(ctx => ExportDocumentRopAsync(ctx, cancellationToken));

            stopwatch.Stop();

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Document processing pipeline failed (ROP). FileId: {FileId}, Errors: {Errors}",
                    fileId,
                    string.Join(", ", result.Errors));

                return Result<ProcessingResult>.WithFailure(result.Errors);
            }

            var ctx = result.Value!;
            var completionEvent = new DocumentProcessingCompletedEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId,
                FileId = fileId,
                TotalProcessingTime = stopwatch.Elapsed,
                AutoProcessed = true
            };

            if (_eventHub != null)
            {
                var broadcastResult = await _eventHub.SendToAllAsync(completionEvent, cancellationToken);
                if (broadcastResult.IsFailure)
                {
                    _logger.LogWarning("Event broadcast failed: {Errors}", broadcastResult.Errors);
                }
            }

            _logger.LogInformation(
                "Document processing pipeline completed successfully (ROP). FileId: {FileId}, Duration: {Duration}ms, StagesCompleted: {Stages}",
                fileId,
                stopwatch.ElapsedMilliseconds,
                ctx.StagesCompleted);

            return Result<ProcessingResult>.Success(new ProcessingResult(
                FileId: fileId,
                CorrelationId: correlationId,
                TotalProcessingTime: stopwatch.Elapsed,
                StagesCompleted: ctx.StagesCompleted,
                AutoProcessed: true));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Document processing pipeline cancelled (ROP). FileId: {FileId}, Duration: {Duration}ms",
                fileId,
                stopwatch.ElapsedMilliseconds);
            return ResultExtensions.Cancelled<ProcessingResult>();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Document processing pipeline failed (ROP). FileId: {FileId}, CorrelationId: {CorrelationId}, Duration: {Duration}ms",
                fileId,
                correlationId,
                stopwatch.ElapsedMilliseconds);

            return Result<ProcessingResult>.WithFailure($"Processing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts the processing orchestrator and subscribes to DocumentDownloadedEvent stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing orchestrator starting. Pipeline stages configured: Quality={QualityConfigured}, OCR={OcrConfigured}, Fusion={FusionConfigured}, Classification={ClassificationConfigured}, Export={ExportConfigured}",
            _qualityAnalyzer != null,
            _ocrExecutor != null,
            _fusionService != null,
            _classifier != null,
            _exporter != null);

        // Subscribe to DocumentDownloadedEvent stream from Orion
        _eventSubscription = _eventPublisher.GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(
                onNext: async downloadEvent =>
                {
                    try
                    {
                        _logger.LogInformation(
                            "Received DocumentDownloadedEvent. FileId: {FileId}, CorrelationId: {CorrelationId}",
                            downloadEvent.FileId,
                            downloadEvent.CorrelationId);

                        await ProcessDocumentAsync(downloadEvent, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error processing DocumentDownloadedEvent. FileId: {FileId}",
                            downloadEvent.FileId);
                    }
                },
                onError: ex =>
                {
                    _logger.LogError(ex, "Error in DocumentDownloadedEvent stream");
                });

        return Task.CompletedTask;
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

        // Check rejection threshold
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

        // Turn the Stage 2 OCR text into a PDF-source Expediente + metadata so fusion
        // reconciles real OCR-derived data instead of empty inputs.
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

        // Emit conflict events
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
    // Stage 4: Classification
    // ========================================================================

    private async Task<ClassificationResult?> ExecuteStage4ClassificationAsync(
        OCRResult? ocrResult,
        FusionResult? fusionResult,
        Guid fileId,
        Guid? correlationId,
        CancellationToken cancellationToken)
    {
        if (_classifier == null)
        {
            _logger.LogWarning("Stage 4 skipped: Classifier not configured");
            return null;
        }

        _logger.LogInformation("Stage 4: Classification - FileId: {FileId}", fileId);

        var metadata = new ExtractedMetadata
        {
            Expediente = fusionResult?.FusedExpediente
        };

        var classResult = await _classifier.ClassifyAsync(metadata, cancellationToken);

        if (classResult.IsFailure)
        {
            _logger.LogWarning("Stage 4: Classification failed - {Error}", classResult.Error);
            EmitProcessingError(fileId, correlationId, "Classification", classResult.Error ?? "Unknown error");
            return null;
        }

        var result = classResult.Value!;

        // Check confidence threshold
        if (result.Confidence < ClassificationConfidenceThreshold)
        {
            _logger.LogWarning(
                "Stage 4: Low confidence classification. FileId: {FileId}, Confidence: {Confidence}",
                fileId, result.Confidence);

            var flagEvent = new DocumentFlaggedForReviewEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId,
                FileId = fileId,
                Reasons = new List<string> { $"Classification confidence {result.Confidence}% below threshold {ClassificationConfidenceThreshold}%" },
                Priority = "High"
            };
            _eventPublisher.Publish(flagEvent);
        }

        var classificationEvent = new ClassificationCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            RequirementTypeId = (int)result.Level1,
            RequirementTypeName = result.Level1.Name,
            Confidence = result.Confidence,
            Warnings = new List<string>(),
            RequiresManualReview = result.Confidence < ClassificationConfidenceThreshold,
            RelationType = "NewRequirement"
        };
        _eventPublisher.Publish(classificationEvent);

        _logger.LogInformation("Stage 4 complete: Classification - FileId: {FileId}, Type: {Type}, Confidence: {Confidence}",
            fileId, result.Level1.Name, result.Confidence);

        return result;
    }

    // ========================================================================
    // Stage 5: Export
    // ========================================================================

    private async Task<bool> ExecuteStage5ExportAsync(
        FusionResult? fusionResult,
        ClassificationResult? classificationResult,
        Guid fileId,
        Guid? correlationId,
        CancellationToken cancellationToken)
    {
        if (_exporter == null)
        {
            _logger.LogWarning("Stage 5 skipped: Exporter not configured");
            return false;
        }

        _logger.LogInformation("Stage 5: Export - FileId: {FileId}", fileId);

        var sourceObject = fusionResult?.FusedExpediente ?? (object)new { FileId = fileId };
        var exportResult = await _exporter.ExportAsync(sourceObject, "Excel", cancellationToken);

        if (exportResult.IsFailure)
        {
            _logger.LogWarning("Stage 5: Export failed - {Error}", exportResult.Error);
            EmitProcessingError(fileId, correlationId, "Export", exportResult.Error ?? "Unknown error");
            return false;
        }

        var exportBytes = exportResult.Value!;

        var exportEvent = new ExportCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            Destination = $"exports/{fileId}.xlsx",
            Format = "Excel",
            ExportedSizeBytes = exportBytes.Length
        };
        _eventPublisher.Publish(exportEvent);

        _logger.LogInformation("Stage 5 complete: Export - FileId: {FileId}, Size: {Size} bytes",
            fileId, exportBytes.Length);

        return true;
    }

    // ========================================================================
    // ROP Stage Methods (for ProcessDocumentWithResultAsync chain)
    // ========================================================================

    private async Task<Result<ProcessingContext>> LoadImageAsync(
        ProcessingContext ctx,
        CancellationToken cancellationToken)
    {
        if (_fileLoader == null)
        {
            _logger.LogWarning("ROP Stage 1: File loader not configured, skipping");
            return Result<ProcessingContext>.Success(ctx with { StagesCompleted = ctx.StagesCompleted + 1 });
        }

        var loadResult = await _fileLoader.LoadImageAsync(ctx.FileName, cancellationToken);
        if (loadResult.IsFailure)
        {
            return Result<ProcessingContext>.WithFailure($"File load failed: {loadResult.Error}");
        }

        return Result<ProcessingContext>.Success(ctx with { ImageData = loadResult.Value });
    }

    private async Task<Result<ProcessingContext>> AnalyzeQualityAsync(
        ProcessingContext ctx,
        CancellationToken cancellationToken)
    {
        if (_qualityAnalyzer == null || ctx.ImageData == null)
        {
            return Result<ProcessingContext>.Success(ctx with { StagesCompleted = ctx.StagesCompleted + 1 });
        }

        var qualityResult = await _qualityAnalyzer.AnalyzeAsync(ctx.ImageData);
        if (qualityResult.IsFailure)
        {
            return Result<ProcessingContext>.WithFailure($"Quality analysis failed: {qualityResult.Error}");
        }

        var assessment = qualityResult.Value!;
        if ((int)assessment.QualityLevel < QualityRejectionThreshold)
        {
            return Result<ProcessingContext>.WithFailure(
                $"Quality rejected: level {assessment.QualityLevel.Name} below threshold");
        }

        _eventPublisher.Publish(new QualityAnalysisCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = ctx.CorrelationId,
            FileId = ctx.FileId,
            QualityLevel = assessment.QualityLevel,
            BlurScore = (decimal)assessment.BlurScore,
            NoiseScore = (decimal)assessment.NoiseLevel,
            ContrastScore = (decimal)assessment.ContrastLevel,
            SharpnessScore = (decimal)assessment.SharpnessLevel
        });

        return Result<ProcessingContext>.Success(ctx with
        {
            Assessment = assessment,
            StagesCompleted = ctx.StagesCompleted + 1
        });
    }

    private async Task<Result<ProcessingContext>> ExecuteOcrRopAsync(
        ProcessingContext ctx,
        CancellationToken cancellationToken)
    {
        if (_ocrExecutor == null || ctx.ImageData == null)
        {
            return Result<ProcessingContext>.Success(ctx with { StagesCompleted = ctx.StagesCompleted + 1 });
        }

        var ocrConfig = new OCRConfig();
        var ocrResult = await _ocrExecutor.ExecuteOcrAsync(ctx.ImageData, ocrConfig);
        if (ocrResult.IsFailure)
        {
            return Result<ProcessingContext>.WithFailure($"OCR failed: {ocrResult.Error}");
        }

        var result = ocrResult.Value!;
        _eventPublisher.Publish(new OcrCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = ctx.CorrelationId,
            FileId = ctx.FileId,
            OcrEngine = "Tesseract",
            Confidence = (decimal)result.ConfidenceAvg,
            ExtractedTextLength = result.Text.Length
        });

        return Result<ProcessingContext>.Success(ctx with
        {
            OcrResult = result,
            StagesCompleted = ctx.StagesCompleted + 1
        });
    }

    private async Task<Result<ProcessingContext>> FuseDataRopAsync(
        ProcessingContext ctx,
        CancellationToken cancellationToken)
    {
        if (_fusionService == null)
        {
            return Result<ProcessingContext>.Success(ctx with { StagesCompleted = ctx.StagesCompleted + 1 });
        }

        var (pdfExpediente, pdfMetadata) = await BuildPdfExpedienteFromOcrAsync(ctx.OcrResult, cancellationToken);
        var fusionResultObj = await _fusionService.FuseAsync(
            null, pdfExpediente, null,
            new ExtractionMetadata(), pdfMetadata, new ExtractionMetadata(),
            cancellationToken);

        if (fusionResultObj.IsFailure)
        {
            return Result<ProcessingContext>.WithFailure($"Fusion failed: {fusionResultObj.Error}");
        }

        var result = fusionResultObj.Value!;
        _eventPublisher.Publish(new FusionCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = ctx.CorrelationId,
            FileId = ctx.FileId,
            ExpedienteId = Guid.NewGuid(),
            FieldsFused = result.FieldResults.Count,
            ConflictsDetected = result.ConflictingFields.Count
        });

        return Result<ProcessingContext>.Success(ctx with
        {
            FusionResult = result,
            StagesCompleted = ctx.StagesCompleted + 1
        });
    }

    private async Task<Result<ProcessingContext>> ClassifyDocumentRopAsync(
        ProcessingContext ctx,
        CancellationToken cancellationToken)
    {
        if (_classifier == null)
        {
            return Result<ProcessingContext>.Success(ctx with { StagesCompleted = ctx.StagesCompleted + 1 });
        }

        var metadata = new ExtractedMetadata
        {
            Expediente = ctx.FusionResult?.FusedExpediente
        };

        var classResult = await _classifier.ClassifyAsync(metadata, cancellationToken);
        if (classResult.IsFailure)
        {
            return Result<ProcessingContext>.WithFailure($"Classification failed: {classResult.Error}");
        }

        var result = classResult.Value!;
        _eventPublisher.Publish(new ClassificationCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = ctx.CorrelationId,
            FileId = ctx.FileId,
            RequirementTypeId = (int)result.Level1,
            RequirementTypeName = result.Level1.Name,
            Confidence = result.Confidence,
            RequiresManualReview = result.Confidence < ClassificationConfidenceThreshold,
            RelationType = "NewRequirement"
        });

        return Result<ProcessingContext>.Success(ctx with
        {
            ClassificationResult = result,
            StagesCompleted = ctx.StagesCompleted + 1
        });
    }

    private async Task<Result<ProcessingContext>> ExportDocumentRopAsync(
        ProcessingContext ctx,
        CancellationToken cancellationToken)
    {
        if (_exporter == null)
        {
            return Result<ProcessingContext>.Success(ctx with { StagesCompleted = ctx.StagesCompleted + 1 });
        }

        var sourceObject = ctx.FusionResult?.FusedExpediente ?? (object)new { FileId = ctx.FileId };
        var exportResult = await _exporter.ExportAsync(sourceObject, "Excel", cancellationToken);
        if (exportResult.IsFailure)
        {
            return Result<ProcessingContext>.WithFailure($"Export failed: {exportResult.Error}");
        }

        var exportBytes = exportResult.Value!;
        _eventPublisher.Publish(new ExportCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = ctx.CorrelationId,
            FileId = ctx.FileId,
            Destination = $"exports/{ctx.FileId}.xlsx",
            Format = "Excel",
            ExportedSizeBytes = exportBytes.Length
        });

        return Result<ProcessingContext>.Success(ctx with { StagesCompleted = ctx.StagesCompleted + 1 });
    }

    // ========================================================================
    // Helper methods
    // ========================================================================

    /// <summary>
    /// Turns Stage 2 OCR output into a PDF-source <see cref="Expediente"/> plus extraction metadata
    /// for Stage 3 fusion. Returns (null, empty metadata) when no field extractor is configured or
    /// no OCR text exists — preserving the legacy behavior of feeding fusion empty inputs.
    /// </summary>
    /// <remarks>
    /// The ExtractedFields→Expediente mapping mirrors the UI's PdfProcessingService; consolidating
    /// both onto one shared mapper is a tracked DRY follow-up (see gap-analysis 2026-06).
    /// </remarks>
    private async Task<(Expediente? Expediente, ExtractionMetadata Metadata)> BuildPdfExpedienteFromOcrAsync(
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

    /// <summary>
    /// Maps OCR-derived <see cref="ExtractedFields"/> to an <see cref="Expediente"/> entity.
    /// </summary>
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

    /// <summary>
    /// Counts the populated core fields on an <see cref="Expediente"/> (used for extraction metadata).
    /// </summary>
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

    private void EmitCompletionEvent(Guid fileId, Guid? correlationId, TimeSpan elapsed, int stagesCompleted, bool autoProcessed)
    {
        var completionEvent = new DocumentProcessingCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            TotalProcessingTime = elapsed,
            AutoProcessed = autoProcessed
        };
        _eventPublisher.Publish(completionEvent);
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

    /// <summary>
    /// Internal context for passing data through the Railway-Oriented Programming pipeline.
    /// </summary>
    private sealed record ProcessingContext(
        Guid FileId,
        Guid? CorrelationId,
        string FileName)
    {
        public ImageData? ImageData { get; init; }
        public ImageQualityAssessment? Assessment { get; init; }
        public OCRResult? OcrResult { get; init; }
        public FusionResult? FusionResult { get; init; }
        public ClassificationResult? ClassificationResult { get; init; }
        public int StagesCompleted { get; init; }
    }
}
