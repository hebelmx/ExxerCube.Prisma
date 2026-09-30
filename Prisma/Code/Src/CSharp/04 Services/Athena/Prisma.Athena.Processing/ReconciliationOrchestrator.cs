using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.Services;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory? _reviewCaseScopeFactory;
    private readonly IDatosCargaOficioLayoutGenerator? _datosCargaGenerator;
    private readonly IStoragePathResolver? _storagePathResolver;
    private readonly ExportGatePolicy _exportGatePolicy;

    /// <summary>Initializes a new instance of the <see cref="ReconciliationOrchestrator"/> class.</summary>
    /// <param name="eventPublisher">The event publisher for domain events.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="classifier">Optional: Stage 4 classification service.</param>
    /// <param name="exporter">Optional: Stage 5 SIRO XML export service. When null Stage 5 is skipped.</param>
    /// <param name="reviewCaseScopeFactory">
    /// Optional scope factory used to resolve <see cref="IManualReviewerPanel"/> per document (avoids captive
    /// dependency: IManualReviewerPanel is scoped, ReconciliationOrchestrator is singleton). When
    /// <see langword="null"/> review-case persistence is a silent no-op (no DB wired). GH #6.
    /// </param>
    /// <param name="datosCargaGenerator">
    /// Optional generator for the "Datos Carga de Oficio" Excel layout (FR-A, Item #7). When non-null,
    /// Stage 5 additionally generates an xlsx and emits a
    /// <see cref="ExportCompletedEvent"/> with <c>Format="DatosCargaOficioXlsx"</c>. When
    /// <see langword="null"/> only the SIRO XML export runs (existing behaviour preserved).
    /// </param>
    /// <param name="storagePathResolver">
    /// Optional resolver for writing the xlsx bytes to shared storage. When non-null the xlsx is written
    /// to the resolved path; when <see langword="null"/> the bytes remain in-memory (size is captured for
    /// the event). Either way the event is published.
    /// </param>
    /// <param name="exportGatePolicy">
    /// Optional export-gate policy that controls when Stage-5 export is blocked pending human review.
    /// When <see langword="null"/> the default policy is used (all gates active — matches the owner
    /// ruling that unreviewed/low-confidence/conflicted cases MUST NOT produce regulatory output).
    /// See <see cref="ExportGatePolicy"/> for configurable conditions.
    /// </param>
    public ReconciliationOrchestrator(
        IEventPublisher eventPublisher,
        ILogger logger,
        IFileClassifier? classifier = null,
        IResponseExporter? exporter = null,
        IServiceScopeFactory? reviewCaseScopeFactory = null,
        IDatosCargaOficioLayoutGenerator? datosCargaGenerator = null,
        IStoragePathResolver? storagePathResolver = null,
        ExportGatePolicy? exportGatePolicy = null)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _classifier = classifier;
        _exporter = exporter;
        _reviewCaseScopeFactory = reviewCaseScopeFactory;
        _datosCargaGenerator = datosCargaGenerator;
        _storagePathResolver = storagePathResolver;
        _exportGatePolicy = exportGatePolicy ?? new ExportGatePolicy();
    }

    /// <summary>
    /// Runs Stage 4 Classification → Stage 5 Export over the Extractor's fused result.
    /// After Stage 4, persists a review case reflecting the <paramref name="isComplete"/> flag (GH #6,
    /// fail-open: a persistence failure is logged at Warning and never throws).
    /// </summary>
    /// <param name="ocrResult">The OCR result from the Extractor. Its body text is fed into <see cref="ExxerCube.Prisma.Domain.ValueObjects.ExtractedMetadata.LegalReferences"/> at Stage 4 so keyword scoring sees the full OCR body.</param>
    /// <param name="fusionResult">The fused result from the Extractor (its expediente feeds classification + export).</param>
    /// <param name="fileId">The file id (stable across the pipeline).</param>
    /// <param name="correlationId">The correlation id.</param>
    /// <param name="isComplete">
    /// Whether the source case package was complete. When <see langword="false"/> an
    /// <c>IncompleteCase</c> review case is persisted (idempotent). When <see langword="true"/> any
    /// existing pending incomplete-case row is healed. Defaults to <see langword="true"/> so existing
    /// callers compile unchanged. GH #6.
    /// </param>
    /// <param name="handoffPath">
    /// Optional storage-relative path of the fused expediente handoff artifact (e.g.
    /// <c>2026/06/12/{fileId}.fusion.json</c>). When set, this path is embedded in any
    /// <see cref="ExportHeldForReviewEvent"/> so the review-approval handler can reload the
    /// expediente via <c>IExpedienteHandoffStore</c> after the reviewer approves the case (G-C2b).
    /// <see langword="null"/> in the in-process / single-service path.
    /// </param>
    /// <param name="approvedByReviewer">
    /// When <see langword="true"/> the export gate is bypassed because a human reviewer has
    /// explicitly approved this case. The gate still evaluates — so blocked-case diagnostics
    /// continue to work — but the block is not applied and Stage 5 runs unconditionally.
    /// <see langword="false"/> by default (normal pipeline behaviour; gate applies).
    /// </param>
    /// <param name="cancellationToken">Cancellation token; honored between stages.</param>
    /// <returns>How many of Stages 4–5 completed.</returns>
    public async Task<int> ReconcileAsync(
        OCRResult? ocrResult,
        FusionResult? fusionResult,
        Guid fileId,
        Guid? correlationId,
        bool isComplete = true,
        string? handoffPath = null,
        bool approvedByReviewer = false,
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

        // REVIEW CASE PERSISTENCE (GH #6): run after Stage 4 so we have classificationResult.
        // Fail-open: any failure is logged at Warning and never throws — the pipeline must continue.
        // G-C2b: pass handoffPath so the ReviewCase rows carry it for the reviewer-approval handler.
        await PersistReviewCaseAsync(fileId, fusionResult, classificationResult, isComplete, handoffPath, cancellationToken)
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        // EXPORT GATE (G-C2 / FR14 / FR20 / INV-6 — owner ruling binding 2026-06-20):
        // Block Stage-5 export when the case requires human review.
        // All conditions are evaluated (not short-circuited) so callers receive complete diagnostic
        // information via BlockReasons (helpful for the reviewer UI).
        //
        // G-C2b: When approvedByReviewer is true the gate diagnostics still run (so BlockReasons
        // is populated for logging/audit), but the block is NOT applied — Stage 5 proceeds because
        // the human reviewer has explicitly cleared the case.  This is the only legitimate bypass.
        // OCR confidence for the gate aggregate: live in the monolith path (ocrResult non-null),
        // else carried on the handoff Expediente in the 3-process path (OcrConfidence) — mirrors the
        // BodyText fallback below. Null when OCR did not run.
        var ocrConfidence = ocrResult?.Confidence ?? fusionResult?.FusedExpediente?.OcrConfidence;
        var blockReasons = EvaluateExportGate(fusionResult, classificationResult, ocrConfidence);
        if (blockReasons.Count > 0 && !approvedByReviewer)
        {
            _logger.LogWarning(
                "Stage 5 BLOCKED by export gate for FileId {FileId}: {Reasons}",
                fileId, string.Join("; ", blockReasons));

            var heldEvent = new ExportHeldForReviewEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId,
                FileId = fileId,
                BlockReasons = blockReasons,
                ClassificationConfidence = classificationResult is null ? (int?)null : (int)Math.Round(classificationResult.Confidence.Value * 100),
                HandoffPath = handoffPath,
            };
            _eventPublisher.Publish(heldEvent);

            return stagesCompleted; // Stage 5 intentionally skipped — not an error.
        }

        if (blockReasons.Count > 0 && approvedByReviewer)
        {
            // Gate would have blocked but reviewer override is in effect — log at Info so there
            // is an explicit audit trail that human approval lifted the block for this case.
            _logger.LogInformation(
                "Stage 5 gate conditions detected for FileId {FileId} but proceeding: reviewer override is active. Conditions: {Reasons}",
                fileId, string.Join("; ", blockReasons));
        }

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
    // Export Gate Evaluation (G-C2)
    // ========================================================================

    /// <summary>
    /// Evaluates all conditions in the <see cref="ExportGatePolicy"/> and returns the list of
    /// human-readable block reasons.  An empty list means export is allowed to proceed.
    /// All conditions are evaluated (not short-circuited) so the caller receives complete
    /// diagnostic information via <see cref="ExportHeldForReviewEvent.BlockReasons"/>.
    /// </summary>
    /// <param name="fusionResult">The fusion result (may be null — null counts as ManualReviewRequired).</param>
    /// <param name="classificationResult">The classification result (may be null — treated as low-confidence).</param>
    /// <param name="ocrConfidence">The OCR-stage confidence for the aggregate (may be null — OCR did not run, or the 3-process handoff carried none).</param>
    /// <returns>Non-empty list of reasons when export should be blocked; empty list when safe to export.</returns>
    private List<string> EvaluateExportGate(
        FusionResult? fusionResult,
        ClassificationResult? classificationResult,
        Confidence? ocrConfidence)
    {
        var reasons = new List<string>();

        // Gate 1: Low-confidence classification.
        // Only applies when a classifier is wired AND produced a result: a null classificationResult
        // means Stage 4 was skipped (no classifier configured) — in that case this gate is a no-op so
        // export-only configurations (no Stage 4) continue to work. If Stage 4 ran and confidence is
        // low, the gate blocks.
        if (_exportGatePolicy.BlockOnLowConfidence && classificationResult is not null)
        {
            if (classificationResult.Confidence.Value < _exportGatePolicy.ClassificationConfidenceThreshold)
            {
                var pct = (int)Math.Round(classificationResult.Confidence.Value * 100);
                var thresholdPct = (int)Math.Round(_exportGatePolicy.ClassificationConfidenceThreshold * 100);
                reasons.Add(
                    $"Classification confidence {pct}% is below the required threshold of {thresholdPct}% (BlockOnLowConfidence)");
            }
        }

        // Gate 1b: Low weighted document-confidence aggregate (ADR-023 D2). Combines the available stage
        // signals (classification + OCR + fusion) by their configured weights; a missing signal's weight is
        // redistributed proportionally because WeightedAverage divides by the sum of the weights present.
        // This holds a document whose classification passes but whose OCR/fusion evidence is weak.
        if (_exportGatePolicy.BlockOnLowAggregateConfidence)
        {
            var weighted = new List<(Confidence Confidence, double Weight)>();
            if (classificationResult is not null)
                weighted.Add((classificationResult.Confidence, _exportGatePolicy.Weights.Classification));
            if (ocrConfidence is not null)
                weighted.Add((ocrConfidence.Value, _exportGatePolicy.Weights.Ocr));
            if (fusionResult is not null)
                weighted.Add((fusionResult.Confidence, _exportGatePolicy.Weights.Fusion));

            // Only evaluate with at least one present signal carrying positive weight (a fully-null pipeline
            // is already handled by gates 1/2; an all-zero-weight config means "aggregate gate disabled").
            if (weighted.Count > 0 && weighted.Sum(w => w.Weight) > 0)
            {
                var aggregate = Confidence.WeightedAverage(weighted, ConfidenceSource.Aggregate);
                if (aggregate.Value < _exportGatePolicy.AggregateConfidenceThreshold)
                {
                    var pct = (int)Math.Round(aggregate.Value * 100);
                    var thresholdPct = (int)Math.Round(_exportGatePolicy.AggregateConfidenceThreshold * 100);
                    reasons.Add(
                        $"Document-confidence aggregate {pct}% is below the required threshold of {thresholdPct}% (BlockOnLowAggregateConfidence)");
                }
            }
        }

        // Gate 2: Fusion NextAction == ManualReviewRequired.
        // A null fusionResult (extraction stage failed) is also treated as ManualReviewRequired.
        if (_exportGatePolicy.BlockOnFusionManualReviewRequired)
        {
            var nextAction = fusionResult?.NextAction ?? NextAction.ManualReviewRequired;
            if (nextAction.Equals(NextAction.ManualReviewRequired))
            {
                reasons.Add(
                    $"Fusion NextAction is '{nextAction.Name}' — mandatory human review required before export (BlockOnFusionManualReviewRequired)");
            }
        }

        // Gate 3: Unresolved field conflicts in the fusion result.
        if (_exportGatePolicy.BlockOnUnresolvedConflicts && fusionResult is not null)
        {
            if (fusionResult.ConflictingFields.Count > 0)
            {
                var fields = string.Join(", ", fusionResult.ConflictingFields);
                reasons.Add(
                    $"Fusion has {fusionResult.ConflictingFields.Count} unresolved field conflict(s): [{fields}] (BlockOnUnresolvedConflicts)");
            }
        }

        return reasons;
    }

    /// <summary>
    /// Persists a review case for the document after Stage 4.  Fail-open: any error is logged at
    /// Warning and swallowed — a review-persistence failure must not crash the pipeline.
    /// </summary>
    private async Task PersistReviewCaseAsync(
        Guid fileId,
        FusionResult? fusionResult,
        ClassificationResult? classificationResult,
        bool isComplete,
        string? handoffPath,
        CancellationToken cancellationToken)
    {
        if (_reviewCaseScopeFactory is null)
        {
            return; // No DB wired — silent no-op.
        }

        try
        {
            // When classificationResult is null (classifier absent/failed) but !isComplete we still
            // want to flag the incomplete case.  Use a minimal ClassificationResult (Confidence 0)
            // so the IncompleteCase row is never silently dropped.
            // Note: ClassificationResult has a public parameterless ctor and Confidence defaults to 0,
            // so this is safe to construct directly.
            var effectiveClassification = classificationResult ?? new ClassificationResult();

            // Build field-conflict alerts (alertamiento — Item C, #9) from the fusion result.
            // FieldConflictAlertBuilder.From is pure/sync; empty when all fields agree.
            var conflictAlerts = FieldConflictAlertBuilder.From(fusionResult);

            var metadata = new UnifiedMetadataRecord
            {
                Expediente = fusionResult?.FusedExpediente,
                FieldConflictAlerts = conflictAlerts,
            };

            using var scope = _reviewCaseScopeFactory.CreateScope();
            var panel = scope.ServiceProvider.GetService<IManualReviewerPanel>();
            if (panel is null)
            {
                _logger.LogDebug(
                    "IManualReviewerPanel not registered in scope — review-case persistence skipped for file {FileId}",
                    fileId);
                return;
            }

            var result = await panel.IdentifyReviewCasesAsync(
                fileId.ToString(),
                metadata,
                effectiveClassification,
                isComplete,
                handoffPath: handoffPath,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Review-case persistence returned failure for file {FileId}: {Error} (pipeline continues)",
                    fileId, result.Error);
            }
            else
            {
                _logger.LogDebug(
                    "Review-case persistence succeeded for file {FileId}: {Count} case(s), isComplete={IsComplete}",
                    fileId, result.Value?.Count ?? 0, isComplete);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Review-case persistence threw for file {FileId} (fail-open — pipeline continues)", fileId);
        }
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

        // Story 2.1b: fall back to the Expediente's BodyText when ocrResult is null (3-process path).
        // The Extractor populates BodyText before handoff so the Reconciliator can keyword-score the
        // full OCR body without the raw OCR result crossing the process boundary (ADR-011).
        var ocrBodyText = ocrResult?.Text ?? fusionResult?.FusedExpediente?.BodyText;
        var metadata = new ExtractedMetadata
        {
            Expediente = fusionResult?.FusedExpediente,
            LegalReferences = string.IsNullOrWhiteSpace(ocrBodyText)
                ? Array.Empty<string>()
                : new[] { ocrBodyText }
        };

        var classResult = await _classifier.ClassifyAsync(metadata, cancellationToken);

        if (classResult.IsFailure)
        {
            _logger.LogWarning("Stage 4: Classification failed - {Error}", classResult.Error);
            EmitProcessingError(fileId, correlationId, "Classification", classResult.Error ?? "Unknown error");
            return null;
        }

        var result = classResult.Value!;

        if (result.Confidence.Value < _exportGatePolicy.ClassificationConfidenceThreshold)
        {
            var pct = (int)Math.Round(result.Confidence.Value * 100);
            var thresholdPct = (int)Math.Round(_exportGatePolicy.ClassificationConfidenceThreshold * 100);
            _logger.LogWarning(
                "Stage 4: Low confidence classification. FileId: {FileId}, Confidence: {Confidence}",
                fileId, result.Confidence.Value);

            var flagEvent = new DocumentFlaggedForReviewEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId,
                FileId = fileId,
                Reasons = new List<string> { $"Classification confidence {pct}% below threshold {thresholdPct}%" },
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
            RequiresManualReview = result.Confidence.Value < _exportGatePolicy.ClassificationConfidenceThreshold,
            RelationType = "NewRequirement"
        };
        _eventPublisher.Publish(classificationEvent);

        _logger.LogInformation("Stage 4 complete: Classification - FileId: {FileId}, Type: {Type}, Confidence: {Confidence}",
            fileId, result.Level1.Name, result.Confidence.Value);

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
        // NOTE (ComplianceActions — Task 4): ComplianceActions are NOT available at this stage. Neither
        // ClassificationResult nor FusionResult carries them; they are populated in the Application layer
        // (FieldMatchingService / ExportService) which does not run in the Reconciliator path. As a result
        // DatosCargaOficioProjection.From() falls back to the TieneAseguramiento/AreaDescripcion heuristics
        // for TipoAsunto derivation. Threading ComplianceActions here would require either (a) adding them
        // to FusionResult/ClassificationResult (domain change) or (b) running the Application-layer
        // field-matching step before export. Deferred as a post-MVP improvement.
        var metadata = new UnifiedMetadataRecord { Expediente = fusionResult.FusedExpediente };

        // Export to an in-memory stream; size is captured for the event, then the stream is
        // written to shared storage when IStoragePathResolver is wired (fail-open: any I/O error
        // is logged at Warning and the pipeline continues — the SIRO XML export already succeeded).
        using var stream = new MemoryStream();
        var exportResult = await _exporter.ExportSiroXmlAsync(metadata, stream, cancellationToken);

        if (exportResult.IsFailure)
        {
            _logger.LogWarning("Stage 5: SIRO XML export failed - {Error}", exportResult.Error);
            EmitProcessingError(fileId, correlationId, "Export", exportResult.Error ?? "Unknown error");
            return false;
        }

        var exportedSizeBytes = (int)stream.Length;
        var destination = $"exports/{fileId}.siro.xml";

        // Persist SIRO XML to shared storage when the resolver is wired (mirrors Stage 5b xlsx pattern).
        if (_storagePathResolver is not null)
        {
            var resolveResult = _storagePathResolver.Resolve(destination);
            if (resolveResult.IsSuccess && !string.IsNullOrWhiteSpace(resolveResult.Value))
            {
                try
                {
                    var absPath = resolveResult.Value!;
                    var dir = Path.GetDirectoryName(absPath);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    stream.Position = 0;
                    await using var fileOut = new FileStream(absPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await stream.CopyToAsync(fileOut, cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation(
                        "Stage 5: SIRO XML written to {Path} ({Size} bytes)",
                        absPath, exportedSizeBytes);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Stage 5: Failed to write SIRO XML to storage for FileId {FileId} (pipeline continues)",
                        fileId);
                }
            }
        }

        var exportEvent = new ExportCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            Destination = destination,
            Format = "SiroXml",
            ExportedSizeBytes = exportedSizeBytes
        };
        _eventPublisher.Publish(exportEvent);

        _logger.LogInformation("Stage 5 complete: SIRO XML Export - FileId: {FileId}, Size: {Size} bytes",
            fileId, exportedSizeBytes);

        // Stage 5b: "Datos Carga de Oficio" Excel layout (FR-A, Item #7)
        // Fail-open: any failure is logged at Warning — the SIRO XML export already succeeded.
        if (_datosCargaGenerator is not null)
        {
            await ExecuteStage5DatosCargaAsync(metadata, fileId, correlationId, cancellationToken)
                .ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Generates the "Datos Carga de Oficio" xlsx and emits a
    /// <see cref="ExportCompletedEvent"/> with <c>Format="DatosCargaOficioXlsx"</c>.
    /// Fail-open: any error is logged at Warning and swallowed — the pipeline must continue.
    /// </summary>
    private async Task ExecuteStage5DatosCargaAsync(
        UnifiedMetadataRecord metadata,
        Guid fileId,
        Guid? correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var xlsxStream = new MemoryStream();
            var genResult = await _datosCargaGenerator!
                .GenerateAsync(metadata, xlsxStream, cancellationToken)
                .ConfigureAwait(false);

            if (genResult.IsFailure)
            {
                _logger.LogWarning(
                    "Stage 5b: DatosCargaOficio generation failed for FileId {FileId}: {Error} (pipeline continues)",
                    fileId, genResult.Error);
                return;
            }

            var destination = $"exports/{fileId}.datos-carga-oficio.xlsx";
            var sizeBytes = (int)xlsxStream.Length;

            // Optional: persist to shared storage when resolver is wired
            if (_storagePathResolver is not null)
            {
                var resolveResult = _storagePathResolver.Resolve(destination);
                if (resolveResult.IsSuccess && !string.IsNullOrWhiteSpace(resolveResult.Value))
                {
                    try
                    {
                        var absPath = resolveResult.Value!;
                        var dir = Path.GetDirectoryName(absPath);
                        if (!string.IsNullOrWhiteSpace(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        xlsxStream.Position = 0;
                        await using var fileOut = new FileStream(absPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        await xlsxStream.CopyToAsync(fileOut, cancellationToken).ConfigureAwait(false);

                        _logger.LogInformation(
                            "Stage 5b: DatosCargaOficio xlsx written to {Path} ({Size} bytes)",
                            absPath, sizeBytes);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Stage 5b: Failed to write DatosCargaOficio xlsx to storage for FileId {FileId} (pipeline continues)",
                            fileId);
                    }
                }
            }

            var datosCargaEvent = new ExportCompletedEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId,
                FileId = fileId,
                Destination = destination,
                Format = "DatosCargaOficioXlsx",
                ExportedSizeBytes = sizeBytes,
            };
            _eventPublisher.Publish(datosCargaEvent);

            _logger.LogInformation(
                "Stage 5b complete: DatosCargaOficio xlsx - FileId: {FileId}, Size: {Size} bytes",
                fileId, sizeBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Stage 5b: DatosCargaOficio generation threw for FileId {FileId} (fail-open — pipeline continues)",
                fileId);
        }
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
