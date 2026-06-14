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
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IResponseExporter? _exporter;
    private readonly IFileLoader? _fileLoader;
    private readonly IFieldExtractor<TxtSource>? _txtFieldExtractor;
    private readonly IFieldExtractor<XmlSource>? _xmlFieldExtractor;
    private readonly IFieldExtractor<DocxSource>? _docxFieldExtractor;
    private readonly IEventPublisher _eventPublisher;
    private readonly IExxerHub<DocumentProcessingCompletedEvent>? _eventHub;
    private readonly ILogger<ProcessingOrchestrator> _logger;
    private readonly ExtractionOrchestrator _extractionOrchestrator;
    private readonly ReconciliationOrchestrator _reconciliationOrchestrator;
    private IDisposable? _eventSubscription;
    private readonly IServiceScopeFactory? _scopeFactory;

    /// <summary>
    /// Gets a value indicating whether the orchestrator has started and subscribed to the document stream.
    /// Drives readiness probes (a started orchestrator is ready to process; a constructed-but-not-started one is not).
    /// </summary>
    public bool IsStarted { get; private set; }

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
    /// <param name="exporter">Optional: SIRO XML export service (IResponseExporter) for generating SIRO-conformant XML output.</param>
    /// <param name="fileLoader">Optional: File loader for reading images from disk.</param>
    /// <param name="txtFieldExtractor">Optional: Field extractor used to turn Stage 2 OCR text into an Expediente that feeds Stage 3 fusion. When null, fusion runs without OCR-derived input (legacy behavior).</param>
    /// <param name="xmlFieldExtractor">Optional: Field extractor for XML companion case files (MVP-PATH 2.1 multi-source fusion).</param>
    /// <param name="docxFieldExtractor">Optional: Field extractor for DOCX companion case files (MVP-PATH 2.1 multi-source fusion).</param>
    /// <param name="scopeFactory">
    /// Optional scope factory forwarded to <see cref="ReconciliationOrchestrator"/> for review-case
    /// persistence (GH #6). When <see langword="null"/> review-case persistence is a silent no-op.
    /// </param>
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
        IResponseExporter? exporter = null,
        IFileLoader? fileLoader = null,
        IFieldExtractor<TxtSource>? txtFieldExtractor = null,
        IFieldExtractor<XmlSource>? xmlFieldExtractor = null,
        IFieldExtractor<DocxSource>? docxFieldExtractor = null,
        IServiceScopeFactory? scopeFactory = null)
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
        _xmlFieldExtractor = xmlFieldExtractor;
        _docxFieldExtractor = docxFieldExtractor;
        _scopeFactory = scopeFactory;

        // Compose the two pipeline halves (MVP-PATH 1.4 Reconciliator edge): the in-process monolith runs both,
        // while the 3-process split hosts ExtractionOrchestrator (Extractor) and ReconciliationOrchestrator
        // (Reconciliator) in separate processes. Stage logic lives in the halves; this class composes them.
        _extractionOrchestrator = new ExtractionOrchestrator(
            eventPublisher, logger, qualityAnalyzer, ocrExecutor, fusionService, fileLoader, txtFieldExtractor,
            xmlFieldExtractor, docxFieldExtractor);
        _reconciliationOrchestrator = new ReconciliationOrchestrator(
            eventPublisher, logger, classifier, exporter, reviewCaseScopeFactory: scopeFactory);
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

        _logger.LogInformation(
            "Starting document processing pipeline. FileId: {FileId}, CorrelationId: {CorrelationId}, FileName: {FileName}",
            fileId,
            correlationId,
            downloadEvent.FileName);

        try
        {
            // Extractor half (Stages 1–3): Quality → OCR → Fusion.
            var extraction = await _extractionOrchestrator.ExtractAsync(downloadEvent, cancellationToken);
            stagesCompleted = extraction.StagesCompleted;

            if (extraction.QualityRejected)
            {
                // Short-circuit: quality too low
                stopwatch.Stop();
                EmitCompletionEvent(fileId, correlationId, stopwatch.Elapsed, stagesCompleted, autoProcessed: false);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Reconciliator half (Stages 4–5): Classification → Export.
            stagesCompleted += await _reconciliationOrchestrator.ReconcileAsync(
                extraction.OcrResult, extraction.FusionResult, fileId, correlationId,
                isComplete: downloadEvent.IsComplete,
                cancellationToken);

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

        IsStarted = true;
        return Task.CompletedTask;
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

        var (pdfExpediente, pdfMetadata) = await _extractionOrchestrator.BuildPdfExpedienteFromOcrAsync(ctx.OcrResult, cancellationToken);
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

        // Guard: null FusedExpediente cannot produce a SIRO XML export — skip with success (mirror ReconciliationOrchestrator §2.1).
        if (ctx.FusionResult?.FusedExpediente == null)
        {
            _logger.LogWarning("Stage 5 (ROP): FusedExpediente is null — no SIRO XML produced. FileId: {FileId}", ctx.FileId);
            return Result<ProcessingContext>.Success(ctx with { StagesCompleted = ctx.StagesCompleted + 1 });
        }

        // SIRO XML export (MVP-PATH #8): build UnifiedMetadataRecord and write to in-memory stream.
        var metadata = new UnifiedMetadataRecord { Expediente = ctx.FusionResult.FusedExpediente };
        using var stream = new MemoryStream();
        var exportResult = await _exporter.ExportSiroXmlAsync(metadata, stream, cancellationToken).ConfigureAwait(false);
        if (exportResult.IsFailure)
        {
            return Result<ProcessingContext>.WithFailure($"Export failed: {exportResult.Error}");
        }

        var exportedSizeBytes = (int)stream.Length;
        _eventPublisher.Publish(new ExportCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = ctx.CorrelationId,
            FileId = ctx.FileId,
            Destination = $"exports/{ctx.FileId}.siro.xml",
            Format = "SiroXml",
            ExportedSizeBytes = exportedSizeBytes
        });

        return Result<ProcessingContext>.Success(ctx with { StagesCompleted = ctx.StagesCompleted + 1 });
    }

    // ========================================================================
    // Helper methods
    // ========================================================================

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
