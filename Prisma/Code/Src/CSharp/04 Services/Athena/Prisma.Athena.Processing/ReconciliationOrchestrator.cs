using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Prisma.Athena.Processing;

/// <summary>
/// The <em>Reconciliator</em> half of the processing pipeline (MVP-PATH 1.4 Reconciliator edge, ADR-011): runs
/// Stage 4 Classification → Stage 5 Export over the fused expediente produced by the Extractor
/// (<see cref="ExtractionOrchestrator"/>). This is the work that runs in the separate Reconciliator process;
/// the monolithic <see cref="ProcessingOrchestrator"/> composes it with the Extractor half so the in-process
/// path is preserved.
/// </summary>
/// <remarks>
/// Stage logic and emitted domain events are unchanged from the original monolith — only their home moved. The
/// caller owns the completion event and the defensive try/catch.
/// </remarks>
public sealed class ReconciliationOrchestrator
{
    private readonly IFileClassifier? _classifier;
    private readonly IResponseExporter? _exporter;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger _logger;

    /// <summary>Classification confidence threshold below which documents are flagged for review.</summary>
    private const int ClassificationConfidenceThreshold = 70;

    /// <summary>Initializes a new instance of the <see cref="ReconciliationOrchestrator"/> class.</summary>
    /// <param name="eventPublisher">The event publisher for domain events.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="classifier">Optional: Stage 4 classification service.</param>
    /// <param name="exporter">Optional: Stage 5 SIRO XML export service. When null Stage 5 is skipped.</param>
    public ReconciliationOrchestrator(
        IEventPublisher eventPublisher,
        ILogger logger,
        IFileClassifier? classifier = null,
        IResponseExporter? exporter = null)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _classifier = classifier;
        _exporter = exporter;
    }

    /// <summary>
    /// Runs Stage 4 Classification → Stage 5 Export over the Extractor's fused result.
    /// </summary>
    /// <param name="ocrResult">The OCR result from the Extractor (currently unused by Stage 4; kept for fidelity/future use).</param>
    /// <param name="fusionResult">The fused result from the Extractor (its expediente feeds classification + export).</param>
    /// <param name="fileId">The file id (stable across the pipeline).</param>
    /// <param name="correlationId">The correlation id.</param>
    /// <param name="cancellationToken">Cancellation token; honored between stages.</param>
    /// <returns>How many of Stages 4–5 completed.</returns>
    public async Task<int> ReconcileAsync(
        OCRResult? ocrResult,
        FusionResult? fusionResult,
        Guid fileId,
        Guid? correlationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stagesCompleted = 0;

        // STAGE 4: Classification
        var classificationResult = await ExecuteStage4ClassificationAsync(
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

        return stagesCompleted;
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
    // Stage 5: Export (SIRO XML — MVP-PATH #8, ADR-011)
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

        // Guard: if the fused expediente is null we cannot produce a SIRO XML export.
        if (fusionResult?.FusedExpediente == null)
        {
            _logger.LogWarning("Stage 5 skipped: FusedExpediente is null — no SIRO XML produced. FileId: {FileId}", fileId);
            return false;
        }

        _logger.LogInformation("Stage 5: SIRO XML Export - FileId: {FileId}", fileId);

        // Build the UnifiedMetadataRecord wrapper that SiroXmlExporter expects (MVP-PATH #8 §2.1).
        var metadata = new UnifiedMetadataRecord { Expediente = fusionResult.FusedExpediente };

        // Export to an in-memory stream; ExportedSizeBytes is captured for the event (R4: file
        // write to shared storage is deferred to post-MVP per design §2.4).
        using var stream = new MemoryStream();
        var exportResult = await _exporter.ExportSiroXmlAsync(metadata, stream, cancellationToken);

        if (exportResult.IsFailure)
        {
            _logger.LogWarning("Stage 5: SIRO XML export failed - {Error}", exportResult.Error);
            EmitProcessingError(fileId, correlationId, "Export", exportResult.Error ?? "Unknown error");
            return false;
        }

        var exportedSizeBytes = (int)stream.Length;

        var exportEvent = new ExportCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            Destination = $"exports/{fileId}.siro.xml",
            Format = "SiroXml",
            ExportedSizeBytes = exportedSizeBytes
        };
        _eventPublisher.Publish(exportEvent);

        _logger.LogInformation("Stage 5 complete: SIRO XML Export - FileId: {FileId}, Size: {Size} bytes",
            fileId, exportedSizeBytes);

        return true;
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
