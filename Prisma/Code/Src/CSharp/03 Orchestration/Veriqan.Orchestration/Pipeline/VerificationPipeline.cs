using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Tenant;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;
using ExxerCube.Prisma.Veriqan.Orchestration.Observability;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

/// <summary>
/// Default <see cref="IVerificationPipeline"/> implementation that orchestrates all
/// verification stages in sequence: ingest → extract → bind → resolve tenant profile
/// → run rules → aggregate verdict.
/// </summary>
/// <remarks>
/// <para>
/// Registered as <b>Scoped</b> so each HTTP request / batch item gets its own instance,
/// keeping the scoped <c>VeriqanDbContext</c> (accessed via the ingestion service) properly bounded.
/// </para>
/// <para>
/// <b>Tenant profile wiring:</b> the active <see cref="TenantProfile"/> (injected from DI,
/// defaulting to <see cref="TenantProfile.LegalBaseline()"/>) is resolved once per pipeline run
/// via <see cref="ITenantProfileResolver"/> before the validation engine runs. The resolved
/// profile is threaded into <see cref="VerificationContext"/> and the resulting deviations are
/// passed to <see cref="IVerdictAggregator.Aggregate"/>. With the default legal-baseline profile
/// (no overrides) this yields all legal defaults and zero deviations — existing pipeline
/// behaviour (incl. the e2e RED verdict and finding count) is unchanged.
/// </para>
/// <para>
/// Real multi-tenant per-statement profile SELECTION is deferred to FR-39 / Epic 13; this
/// wiring uses a single configured active profile (default legal baseline).
/// </para>
/// </remarks>
internal sealed class VerificationPipeline : IVerificationPipeline
{
    /// <summary>
    /// Semantic version of the VEC engine stored on every <see cref="Domain.Entities.Finding"/>
    /// and <see cref="JobVerdict"/> row for auditability (NFR-7, VERIQAN-E3-S4).
    /// </summary>
    /// <remarks>
    /// Resolved once from this assembly at type-load time via <see cref="EngineVersionResolver"/>
    /// rather than hardcoded, so the stamped provenance tracks the actually-deployed
    /// <c>Veriqan.Orchestration</c> build's MinVer-computed SemVer (e.g. <c>1.4.0-rc.1.547+4a933ce8</c>),
    /// not the constant <c>MAJOR.0.0.0</c> assembly version MinVer deliberately pins. Falls back to
    /// the assembly version, then <c>"unknown"</c>, when no informational version is available (e.g.
    /// some test hosts). Length-capped to the <c>EngineVersion</c> column's 50-char limit.
    /// </remarks>
    private static readonly string EngineVersion =
        EngineVersionResolver.Resolve(typeof(VerificationPipeline).Assembly);

    /// <summary>
    /// Provenance stamp for <see cref="JobVerdict"/>.<c>ReferenceBundleVersion</c>: prefers the
    /// bundle's unique <c>BundleId</c> (discriminates bundle instances) over <c>SchemaVersion</c>
    /// (a schema constant, identical across all bundles), so a verdict row can be tied to the
    /// specific bundle that produced it (RC6 W2.4). Capped to the column's 50-char limit.
    /// </summary>
    private static string? BundleProvenanceVersion(VecReferenceBundle? bundle)
    {
        var value = bundle?.BundleMetadata.BundleId ?? bundle?.BundleMetadata.SchemaVersion;
        return value is { Length: > 50 } ? value[..50] : value;
    }

    /// <summary>
    /// ActivitySource for distributed-tracing spans emitted by each pipeline stage.
    /// The source name matches <see cref="VeriqanMetrics.MeterName"/> so a single
    /// <c>AddSource</c> / <c>AddMeter</c> registration in the host covers both signals.
    /// </summary>
    internal static readonly ActivitySource PipelineActivitySource =
        new(VeriqanMetrics.MeterName, EngineVersion);

    private readonly IStatementIngestionService _ingestion;
    private readonly IStatementFieldExtractor _extractor;
    private readonly IBundleBinder _binder;
    private readonly IVecValidationEngine _engine;
    private readonly IVerdictAggregator _aggregator;
    private readonly IVecReferenceDataProvider _referenceDataProvider;
    private readonly ITenantProfileResolver _tenantResolver;
    private readonly TenantProfile _activeTenantProfile;
    private readonly IReadOnlyList<IVecValidationRule> _rules;
    private readonly ILegalToleranceProvider _toleranceProvider;
    private readonly IVerdictPersistenceService _verdictPersistence;
    private readonly IMarkedPdfGenerator _reportGenerator;
    private readonly IVecAlertService _alertService;
    private readonly AlertOptions _alertOptions;
    private readonly VeriqanMetrics _metrics;
    private readonly ILogger<VerificationPipeline> _logger;

    /// <summary>
    /// Initializes a new <see cref="VerificationPipeline"/> with all required stage dependencies.
    /// </summary>
    public VerificationPipeline(
        IStatementIngestionService ingestion,
        IStatementFieldExtractor extractor,
        IBundleBinder binder,
        IVecValidationEngine engine,
        IVerdictAggregator aggregator,
        IVecReferenceDataProvider referenceDataProvider,
        ITenantProfileResolver tenantResolver,
        TenantProfile activeTenantProfile,
        IEnumerable<IVecValidationRule> rules,
        ILegalToleranceProvider toleranceProvider,
        IVerdictPersistenceService verdictPersistence,
        IMarkedPdfGenerator reportGenerator,
        IVecAlertService alertService,
        IOptions<AlertOptions> alertOptions,
        VeriqanMetrics metrics,
        ILogger<VerificationPipeline> logger)
    {
        _ingestion = ingestion ?? throw new ArgumentNullException(nameof(ingestion));
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _binder = binder ?? throw new ArgumentNullException(nameof(binder));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _referenceDataProvider = referenceDataProvider ?? throw new ArgumentNullException(nameof(referenceDataProvider));
        _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        _activeTenantProfile = activeTenantProfile ?? throw new ArgumentNullException(nameof(activeTenantProfile));
        _rules = (rules ?? throw new ArgumentNullException(nameof(rules))).ToList();
        _toleranceProvider = toleranceProvider ?? throw new ArgumentNullException(nameof(toleranceProvider));
        _verdictPersistence = verdictPersistence ?? throw new ArgumentNullException(nameof(verdictPersistence));
        _reportGenerator = reportGenerator ?? throw new ArgumentNullException(nameof(reportGenerator));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        ArgumentNullException.ThrowIfNull(alertOptions);
        _alertOptions = alertOptions.Value ?? throw new ArgumentNullException(nameof(alertOptions));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<VerificationOutcome>> ProcessAsync(
        StatementSubmission submission,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<VerificationOutcome>();

        // Correlation id for the call — used in structured logging before the job id is known.
        var correlationId = Guid.NewGuid();
        var sw = Stopwatch.StartNew();

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["VeriqanCorrelationId"] = correlationId,
        });

        _logger.LogInformation(
            "Pipeline starting for {FileName} CorrelationId={CorrelationId}",
            submission.FileName,
            correlationId);

        // Top-level span for the entire pipeline run.
        using var pipelineActivity = PipelineActivitySource.StartActivity("pipeline.process");
        pipelineActivity?.SetTag("veriqan.correlation_id", correlationId.ToString());
        pipelineActivity?.SetTag("veriqan.file_name", submission.FileName);

        // Stage 1 — Ingestion
        Result<VerificationJob> ingestResult;
        using (PipelineActivitySource.StartActivity("pipeline.stage.ingest"))
            ingestResult = await _ingestion.IngestAsync(submission.Pdf, submission.FileName, ct)
                .ConfigureAwait(false);

        if (ingestResult.IsCancelled())
        {
            _logger.LogWarning("Pipeline cancelled during ingestion for {FileName}", submission.FileName);
            return ResultExtensions.Cancelled<VerificationOutcome>();
        }

        if (ingestResult.IsFailure)
        {
            _logger.LogWarning(
                "Ingestion failed for {FileName}: {Error}",
                submission.FileName,
                ingestResult.Error);
            return Result<VerificationOutcome>.WithFailure(ingestResult.Error ?? "Ingestion failed");
        }

        var job = ingestResult.Value!;

        // Extend the ambient scope with the now-known job id.
        using var jobScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["VerificationJobId"] = job.Id,
        });

        _logger.LogInformation(
            "Ingestion succeeded for {FileName}. JobId={JobId}",
            submission.FileName,
            job.Id);

        // Stage 1b — pre-resolve the tenant product catalog for the extraction-side Product gate
        // (E3 S3.3b). The bundle is keyed by submission.ContextKey (institution/tenant), not by the
        // yet-to-be-extracted product, so it resolves here with the same key Stage 4 binding uses.
        // On success it lets Stage 2 abstain on an OCR-recovered Product that is not a catalog member
        // (via IProductResolver — the SAME matcher the binder applies), pre-binding. On failure we
        // pass null: extraction is then completely unchanged and the existing Stage 4 flow produces
        // the same BLOCKED/UnknownProduct outcome as today. Stage 4 binding is left untouched (it
        // re-resolves the bundle itself); the extra CSV-backed lookup is cheap and deterministic.
        // Defensive: IVecReferenceDataProvider is a swappable port; a degenerate null/failed result
        // must degrade to "no catalog" (extraction then behaves exactly as pre-E3, and Stage 4
        // binding still produces the BLOCKED/UnknownProduct outcome) rather than fault the pipeline.
        VecReferenceBundle? catalogBundle = null;
        var catalogResult = await _referenceDataProvider
            .GetBundleAsync(submission.ContextKey, ct)
            .ConfigureAwait(false);
        if (catalogResult.IsCancelled())
        {
            _logger.LogWarning("Pipeline cancelled during catalog pre-resolve for {FileName}", submission.FileName);
            return ResultExtensions.Cancelled<VerificationOutcome>();
        }

        if (catalogResult is { IsSuccessNotNull: true })
            catalogBundle = catalogResult.Value;

        // Stage 2 — Field Extraction
        Result<StatementModel> extractResult;
        using (PipelineActivitySource.StartActivity("pipeline.stage.extract"))
            extractResult = await _extractor.ExtractFullAsync(submission.Pdf, ct, catalogBundle)
                .ConfigureAwait(false);

        if (extractResult.IsCancelled())
        {
            _logger.LogWarning("Pipeline cancelled during extraction for {FileName}", submission.FileName);
            return ResultExtensions.Cancelled<VerificationOutcome>();
        }

        if (extractResult.IsFailure)
        {
            _logger.LogWarning(
                "Extraction failed for {FileName}: {Error}",
                submission.FileName,
                extractResult.Error);
            return Result<VerificationOutcome>.WithFailure(extractResult.Error ?? "Extraction failed");
        }

        var statementModel = extractResult.Value!;
        _logger.LogInformation(
            "Extraction succeeded for {FileName}",
            submission.FileName);

        // Stage 2b — Extraction-coverage floor guard (U2 defence, Story E1-S10).
        // If fewer than MinExtractionCoverageCount fields were extracted (Extracted or
        // ExtractedInvalidFormat) the PDF is likely encrypted, blank, or so layout-drifted that
        // the extractor yielded near-zero output.  Without this guard every section rule abstains
        // (InsufficientData) and VerdictAggregator counts only abstains → spurious GREEN (U2).
        // Fire BEFORE bind/validate; produce BLOCKED (never RED — honour the abstain-safety rule).
        var extractedFieldCount = CountExtractedFields(statementModel);
        var coverageFloor = _activeTenantProfile.MinExtractionCoverageCount;

        _logger.LogInformation(
            "Extraction coverage: {ExtractedFieldCount} field(s) extracted, floor={CoverageFloor} for {FileName}",
            extractedFieldCount,
            coverageFloor,
            submission.FileName);

        if (extractedFieldCount < coverageFloor)
        {
            _logger.LogWarning(
                "Extraction coverage below floor for {FileName}: {ExtractedFieldCount} < {CoverageFloor} — emitting ExtractionGap/{Reason}",
                submission.FileName,
                extractedFieldCount,
                coverageFloor,
                BlockReason.InsufficientExtractionCoverage);

            var coverageBlocked = new BlockedOutcome(
                BlockReason.InsufficientExtractionCoverage,
                $"Only {extractedFieldCount} field(s) extracted; minimum required is {coverageFloor}.");

            var coverageBlockedVerdictResult = _aggregator.Aggregate(
                findings: Array.Empty<RuleFinding>(),
                blocked: coverageBlocked,
                ct: ct);

            if (coverageBlockedVerdictResult.IsCancelled())
                return ResultExtensions.Cancelled<VerificationOutcome>();

            if (coverageBlockedVerdictResult.IsFailure)
                return Result<VerificationOutcome>.WithFailure(
                    coverageBlockedVerdictResult.Error ?? "Verdict aggregation failed (coverage floor)");

            sw.Stop();
            var coverageBlockedDuration = sw.Elapsed;
            // Story 4.2: ExtractionGap (not Blocked) — permanent system/capability gap.
            _metrics.RecordStatement(coverageBlockedDuration.TotalMilliseconds, coverageBlockedVerdictResult.Value!.Signal);

            _logger.LogInformation(
                "Pipeline ExtractionGap (InsufficientExtractionCoverage) for {FileName} in {DurationMs:F1} ms JobId={JobId}",
                submission.FileName,
                coverageBlockedDuration.TotalMilliseconds,
                job.Id);

            // Persist — fatal / non-optional on all paths.
            Result<JobVerdict> coveragePersistResult;
            using (PipelineActivitySource.StartActivity("pipeline.stage.persist"))
                coveragePersistResult = await _verdictPersistence.PersistAsync(
                    jobId: job.Id,
                    signal: coverageBlockedVerdictResult.Value!.Signal,
                    findings: Array.Empty<RuleFinding>(),
                    engineVersion: EngineVersion,
                    bankTierVerdict: coverageBlockedVerdictResult.Value!.BankTierVerdict,
                    condusefTierVerdict: coverageBlockedVerdictResult.Value!.CondusefTierVerdict,
                    cancellationToken: ct,
                    referenceBundleVersion: BundleProvenanceVersion(catalogBundle)).ConfigureAwait(false);

            if (coveragePersistResult.IsCancelled())
            {
                _logger.LogWarning(
                    "Pipeline cancelled during persist (InsufficientExtractionCoverage) for {FileName} JobId={JobId}",
                    submission.FileName,
                    job.Id);
                return ResultExtensions.Cancelled<VerificationOutcome>();
            }

            if (coveragePersistResult.IsFailure)
            {
                _logger.LogError(
                    "Verdict persist failed (InsufficientExtractionCoverage) for {FileName} JobId={JobId}: {Error}",
                    submission.FileName,
                    job.Id,
                    coveragePersistResult.Error);
                return Result<VerificationOutcome>.WithFailure(
                    coveragePersistResult.Error ?? "Verdict persistence failed");
            }

            _logger.LogInformation(
                "Verdict persisted (InsufficientExtractionCoverage) for {FileName} JobId={JobId}",
                submission.FileName,
                job.Id);

            // Report — best-effort.
            var coverageFindings = new List<RuleFinding>();
            try
            {
                var coverageReportResult = _reportGenerator.Generate(
                    submission.Pdf,
                    coverageFindings,
                    ct);

                if (coverageReportResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Marked-PDF generation failed (InsufficientExtractionCoverage) for {FileName} JobId={JobId}: {Error} — continuing (best-effort)",
                        submission.FileName,
                        job.Id,
                        coverageReportResult.Error);

                    coverageFindings.Add(new RuleFinding(
                        CheckId: "ReportGenerationFailure",
                        Verdict: FindingVerdict.InsufficientData,
                        Technique: TechniqueClass.Deterministic,
                        Severity: FindingSeverity.Warning,
                        EngineVersion: EngineVersion,
                        Observed: coverageReportResult.Error));
                }
                else
                {
                    _logger.LogInformation(
                        "Marked-PDF generated (InsufficientExtractionCoverage) for {FileName} JobId={JobId} ({Bytes} bytes)",
                        submission.FileName,
                        job.Id,
                        coverageReportResult.Value?.Length ?? 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Marked-PDF generation threw an exception (InsufficientExtractionCoverage) for {FileName} JobId={JobId} — continuing (best-effort)",
                    submission.FileName,
                    job.Id);

                coverageFindings.Add(new RuleFinding(
                    CheckId: "ReportGenerationFailure",
                    Verdict: FindingVerdict.InsufficientData,
                    Technique: TechniqueClass.Deterministic,
                    Severity: FindingSeverity.Warning,
                    EngineVersion: EngineVersion,
                    Observed: ex.Message));
            }

            return Result<VerificationOutcome>.WithSuccess(
                new VerificationOutcome(job, coverageBlockedVerdictResult.Value!, coverageFindings, coverageBlockedDuration));
        }

        // Stage 2c — Text-layer density guard (U3 defence, Story E2-S1).
        // A scanned / image-only PDF produces a near-zero text layer.  Without this guard
        // all 28 mandatory-section rules fail (the detected-section list is empty so the rule
        // falls through to Fail) → false RED on a scanned-but-compliant statement.
        // Fire BEFORE bind/validate; produce BLOCKED (never RED — honour the abstain-safety rule).
        var textLayerWordCount = CountTextLayerWords(statementModel);
        var textLayerFloor = _activeTenantProfile.MinTextLayerWordCount;

        _logger.LogInformation(
            "Text-layer density: {WordCount} word(s), floor={TextLayerFloor} for {FileName}",
            textLayerWordCount,
            textLayerFloor,
            submission.FileName);

        if (textLayerWordCount < textLayerFloor)
        {
            _logger.LogWarning(
                "Text-layer density below floor for {FileName}: {WordCount} < {TextLayerFloor} — emitting ExtractionGap/{Reason}",
                submission.FileName,
                textLayerWordCount,
                textLayerFloor,
                BlockReason.InsufficientTextLayer);

            var textLayerBlocked = new BlockedOutcome(
                BlockReason.InsufficientTextLayer,
                $"Only {textLayerWordCount} word(s) found in PDF text layer; minimum required is {textLayerFloor}. Likely a scanned / image-only PDF.");

            var textLayerBlockedVerdictResult = _aggregator.Aggregate(
                findings: Array.Empty<RuleFinding>(),
                blocked: textLayerBlocked,
                ct: ct);

            if (textLayerBlockedVerdictResult.IsCancelled())
                return ResultExtensions.Cancelled<VerificationOutcome>();

            if (textLayerBlockedVerdictResult.IsFailure)
                return Result<VerificationOutcome>.WithFailure(
                    textLayerBlockedVerdictResult.Error ?? "Verdict aggregation failed (text-layer floor)");

            sw.Stop();
            var textLayerBlockedDuration = sw.Elapsed;
            // Story 4.2: ExtractionGap (not Blocked) — permanent system/capability gap.
            _metrics.RecordStatement(textLayerBlockedDuration.TotalMilliseconds, textLayerBlockedVerdictResult.Value!.Signal);

            _logger.LogInformation(
                "Pipeline ExtractionGap (InsufficientTextLayer) for {FileName} in {DurationMs:F1} ms JobId={JobId}",
                submission.FileName,
                textLayerBlockedDuration.TotalMilliseconds,
                job.Id);

            // Persist — fatal / non-optional on all paths.
            Result<JobVerdict> textLayerPersistResult;
            using (PipelineActivitySource.StartActivity("pipeline.stage.persist"))
                textLayerPersistResult = await _verdictPersistence.PersistAsync(
                    jobId: job.Id,
                    signal: textLayerBlockedVerdictResult.Value!.Signal,
                    findings: Array.Empty<RuleFinding>(),
                    engineVersion: EngineVersion,
                    bankTierVerdict: textLayerBlockedVerdictResult.Value!.BankTierVerdict,
                    condusefTierVerdict: textLayerBlockedVerdictResult.Value!.CondusefTierVerdict,
                    cancellationToken: ct,
                    referenceBundleVersion: BundleProvenanceVersion(catalogBundle)).ConfigureAwait(false);

            if (textLayerPersistResult.IsCancelled())
            {
                _logger.LogWarning(
                    "Pipeline cancelled during persist (InsufficientTextLayer) for {FileName} JobId={JobId}",
                    submission.FileName,
                    job.Id);
                return ResultExtensions.Cancelled<VerificationOutcome>();
            }

            if (textLayerPersistResult.IsFailure)
            {
                _logger.LogError(
                    "Verdict persist failed (InsufficientTextLayer) for {FileName} JobId={JobId}: {Error}",
                    submission.FileName,
                    job.Id,
                    textLayerPersistResult.Error);
                return Result<VerificationOutcome>.WithFailure(
                    textLayerPersistResult.Error ?? "Verdict persistence failed");
            }

            _logger.LogInformation(
                "Verdict persisted (InsufficientTextLayer) for {FileName} JobId={JobId}",
                submission.FileName,
                job.Id);

            // Report — best-effort.
            var textLayerFindings = new List<RuleFinding>();
            try
            {
                var textLayerReportResult = _reportGenerator.Generate(
                    submission.Pdf,
                    textLayerFindings,
                    ct);

                if (textLayerReportResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Marked-PDF generation failed (InsufficientTextLayer) for {FileName} JobId={JobId}: {Error} — continuing (best-effort)",
                        submission.FileName,
                        job.Id,
                        textLayerReportResult.Error);

                    textLayerFindings.Add(new RuleFinding(
                        CheckId: "ReportGenerationFailure",
                        Verdict: FindingVerdict.InsufficientData,
                        Technique: TechniqueClass.Deterministic,
                        Severity: FindingSeverity.Warning,
                        EngineVersion: EngineVersion,
                        Observed: textLayerReportResult.Error));
                }
                else
                {
                    _logger.LogInformation(
                        "Marked-PDF generated (InsufficientTextLayer) for {FileName} JobId={JobId} ({Bytes} bytes)",
                        submission.FileName,
                        job.Id,
                        textLayerReportResult.Value?.Length ?? 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Marked-PDF generation threw an exception (InsufficientTextLayer) for {FileName} JobId={JobId} — continuing (best-effort)",
                    submission.FileName,
                    job.Id);

                textLayerFindings.Add(new RuleFinding(
                    CheckId: "ReportGenerationFailure",
                    Verdict: FindingVerdict.InsufficientData,
                    Technique: TechniqueClass.Deterministic,
                    Severity: FindingSeverity.Warning,
                    EngineVersion: EngineVersion,
                    Observed: ex.Message));
            }

            return Result<VerificationOutcome>.WithSuccess(
                new VerificationOutcome(job, textLayerBlockedVerdictResult.Value!, textLayerFindings, textLayerBlockedDuration));
        }

        // Stage 2d — Ambiguous-document-scope guard (U4 defence, Story 4.2-B).
        // A PDF containing multiple credit-card statements (e.g. a monthly archive) can cause
        // the extractor to read fields from the WRONG statement, producing a confident but wrong
        // verdict.  The coverage floor does NOT catch this (field yield is fine when any statement
        // is parseable).
        //
        // Heuristic (STUB — see TenantProfile.MaxStatementBoundarySignalCount XML doc):
        //   Use PDF page count as a cheap proxy for multi-statement detection.  A typical
        //   CONDUSEF single-statement PDF is 8–15 pages; a multi-statement archive bundle tends
        //   to exceed 20 pages.  If PageCount exceeds MaxStatementBoundarySignalCount (default: 20)
        //   the pipeline routes to ExtractionGap / AmbiguousDocumentScope before bind/validate.
        //
        // STUB LIMITATION: page count is a proxy only.  A legitimate single-statement PDF with
        //   more pages than the configured threshold would false-positive; a 2-statement bundle of
        //   very short statements might stay under the threshold.  Full detection (page-level
        //   structural analysis, distinct account-number anchors, period boundary cross-check) is
        //   deferred.  The threshold is configurable via TenantProfile so tests can disable the
        //   guard by passing int.MaxValue.
        var boundarySignalCount = CountStatementBoundarySignals(statementModel);
        var maxBoundarySignals = _activeTenantProfile.MaxStatementBoundarySignalCount;

        _logger.LogInformation(
            "Statement-boundary signal (page count): {BoundarySignalCount} page(s), max={MaxBoundarySignals} for {FileName}",
            boundarySignalCount,
            maxBoundarySignals,
            submission.FileName);

        if (boundarySignalCount > maxBoundarySignals)
        {
            _logger.LogWarning(
                "Ambiguous document scope for {FileName}: {BoundarySignalCount} page(s) exceeds max={MaxBoundarySignals} — emitting ExtractionGap/{Reason}",
                submission.FileName,
                boundarySignalCount,
                maxBoundarySignals,
                BlockReason.AmbiguousDocumentScope);

            var ambiguousBlocked = new BlockedOutcome(
                BlockReason.AmbiguousDocumentScope,
                $"PDF page count ({boundarySignalCount}) exceeds threshold ({maxBoundarySignals}); " +
                "document likely contains multiple statements — cannot safely isolate a single statement scope. " +
                "Full multi-statement detection deferred.");

            var ambiguousVerdictResult = _aggregator.Aggregate(
                findings: Array.Empty<RuleFinding>(),
                blocked: ambiguousBlocked,
                ct: ct);

            if (ambiguousVerdictResult.IsCancelled())
                return ResultExtensions.Cancelled<VerificationOutcome>();

            if (ambiguousVerdictResult.IsFailure)
                return Result<VerificationOutcome>.WithFailure(
                    ambiguousVerdictResult.Error ?? "Verdict aggregation failed (ambiguous document scope)");

            sw.Stop();
            var ambiguousDuration = sw.Elapsed;
            // Story 4.2: ExtractionGap (not Blocked) — AmbiguousDocumentScope is an
            // extraction/capability gap, not a document defect.
            _metrics.RecordStatement(ambiguousDuration.TotalMilliseconds, ambiguousVerdictResult.Value!.Signal);

            _logger.LogInformation(
                "Pipeline ExtractionGap (AmbiguousDocumentScope) for {FileName} in {DurationMs:F1} ms JobId={JobId}",
                submission.FileName,
                ambiguousDuration.TotalMilliseconds,
                job.Id);

            // Persist — fatal / non-optional on all paths.
            Result<JobVerdict> ambiguousPersistResult;
            using (PipelineActivitySource.StartActivity("pipeline.stage.persist"))
                ambiguousPersistResult = await _verdictPersistence.PersistAsync(
                    jobId: job.Id,
                    signal: ambiguousVerdictResult.Value!.Signal,
                    findings: Array.Empty<RuleFinding>(),
                    engineVersion: EngineVersion,
                    bankTierVerdict: ambiguousVerdictResult.Value!.BankTierVerdict,
                    condusefTierVerdict: ambiguousVerdictResult.Value!.CondusefTierVerdict,
                    cancellationToken: ct,
                    referenceBundleVersion: BundleProvenanceVersion(catalogBundle)).ConfigureAwait(false);

            if (ambiguousPersistResult.IsCancelled())
            {
                _logger.LogWarning(
                    "Pipeline cancelled during persist (AmbiguousDocumentScope) for {FileName} JobId={JobId}",
                    submission.FileName,
                    job.Id);
                return ResultExtensions.Cancelled<VerificationOutcome>();
            }

            if (ambiguousPersistResult.IsFailure)
            {
                _logger.LogError(
                    "Verdict persist failed (AmbiguousDocumentScope) for {FileName} JobId={JobId}: {Error}",
                    submission.FileName,
                    job.Id,
                    ambiguousPersistResult.Error);
                return Result<VerificationOutcome>.WithFailure(
                    ambiguousPersistResult.Error ?? "Verdict persistence failed");
            }

            _logger.LogInformation(
                "Verdict persisted (AmbiguousDocumentScope) for {FileName} JobId={JobId}",
                submission.FileName,
                job.Id);

            // Report — best-effort.
            var ambiguousFindings = new List<RuleFinding>();
            try
            {
                var ambiguousReportResult = _reportGenerator.Generate(
                    submission.Pdf,
                    ambiguousFindings,
                    ct);

                if (ambiguousReportResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Marked-PDF generation failed (AmbiguousDocumentScope) for {FileName} JobId={JobId}: {Error} — continuing (best-effort)",
                        submission.FileName,
                        job.Id,
                        ambiguousReportResult.Error);

                    ambiguousFindings.Add(new RuleFinding(
                        CheckId: "ReportGenerationFailure",
                        Verdict: FindingVerdict.InsufficientData,
                        Technique: TechniqueClass.Deterministic,
                        Severity: FindingSeverity.Warning,
                        EngineVersion: EngineVersion,
                        Observed: ambiguousReportResult.Error));
                }
                else
                {
                    _logger.LogInformation(
                        "Marked-PDF generated (AmbiguousDocumentScope) for {FileName} JobId={JobId} ({Bytes} bytes)",
                        submission.FileName,
                        job.Id,
                        ambiguousReportResult.Value?.Length ?? 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Marked-PDF generation threw an exception (AmbiguousDocumentScope) for {FileName} JobId={JobId} — continuing (best-effort)",
                    submission.FileName,
                    job.Id);

                ambiguousFindings.Add(new RuleFinding(
                    CheckId: "ReportGenerationFailure",
                    Verdict: FindingVerdict.InsufficientData,
                    Technique: TechniqueClass.Deterministic,
                    Severity: FindingSeverity.Warning,
                    EngineVersion: EngineVersion,
                    Observed: ex.Message));
            }

            return Result<VerificationOutcome>.WithSuccess(
                new VerificationOutcome(job, ambiguousVerdictResult.Value!, ambiguousFindings, ambiguousDuration));
        }

        // Stage 3 — Resolve product token from extracted model, with fallback to context key
        var productToken = statementModel.PeriodSummary?.Product.Value
                           ?? submission.ContextKey.ProductId
                           ?? string.Empty;

        _logger.LogInformation(
            "Resolved product token '{ProductToken}' for {FileName}",
            productToken,
            submission.FileName);

        // Stage 4 — Context Binding
        Result<VerificationContext> bindResult;
        using (PipelineActivitySource.StartActivity("pipeline.stage.bind"))
            bindResult = await _binder.BindAsync(job, submission.ContextKey, productToken, ct)
                .ConfigureAwait(false);

        if (bindResult.IsCancelled())
        {
            _logger.LogWarning("Pipeline cancelled during binding for {FileName}", submission.FileName);
            return ResultExtensions.Cancelled<VerificationOutcome>();
        }

        if (bindResult.IsFailure)
        {
            // Check if this is a BLOCKED business outcome (not an unexpected failure)
            if (BlockedOutcome.TryParse(bindResult.Error, out var blocked))
            {
                _logger.LogWarning(
                    "Binding blocked for {FileName}: {BlockReason} — {BlockDetail}",
                    submission.FileName,
                    blocked!.Reason,
                    blocked.Detail);

                // Aggregate a BLOCKED verdict — this is a valid business outcome, not an error
                var blockedVerdictResult = _aggregator.Aggregate(
                    findings: Array.Empty<RuleFinding>(),
                    blocked: blocked,
                    ct: ct);

                if (blockedVerdictResult.IsCancelled())
                    return ResultExtensions.Cancelled<VerificationOutcome>();

                if (blockedVerdictResult.IsFailure)
                    return Result<VerificationOutcome>.WithFailure(blockedVerdictResult.Error ?? "Verdict aggregation failed");

                sw.Stop();
                var blockedDuration = sw.Elapsed;
                // Story 4.2: ExtractionGap (not Blocked) for all currently-wired bind reasons
                // (UnknownProduct, InvalidBundle). Signal is read from the aggregated summary.
                _metrics.RecordStatement(blockedDuration.TotalMilliseconds, blockedVerdictResult.Value!.Signal);
                _logger.LogInformation(
                    "Pipeline {Signal} (bind: {BlockReason}) for {FileName} in {DurationMs:F1} ms JobId={JobId}",
                    blockedVerdictResult.Value!.Signal,
                    blocked!.Reason,
                    submission.FileName,
                    blockedDuration.TotalMilliseconds,
                    job.Id);

                // Stage 8 — Persist (ExtractionGap path): fatal, same as the main path.
                // An ExtractionGap verdict is a computed business outcome and must be durable.
                // Never silently discard it — a persist failure returns Result.WithFailure
                // so the caller knows the verdict was NOT written.
                Result<JobVerdict> blockedPersistResult;
                using (PipelineActivitySource.StartActivity("pipeline.stage.persist"))
                    blockedPersistResult = await _verdictPersistence.PersistAsync(
                        jobId: job.Id,
                        signal: blockedVerdictResult.Value!.Signal,
                        findings: Array.Empty<RuleFinding>(),
                        engineVersion: EngineVersion,
                        bankTierVerdict: blockedVerdictResult.Value!.BankTierVerdict,
                        condusefTierVerdict: blockedVerdictResult.Value!.CondusefTierVerdict,
                        cancellationToken: ct,
                        referenceBundleVersion: BundleProvenanceVersion(catalogBundle)).ConfigureAwait(false);

                if (blockedPersistResult.IsCancelled())
                {
                    _logger.LogWarning(
                        "Pipeline cancelled during persist (BLOCKED) for {FileName} JobId={JobId}",
                        submission.FileName,
                        job.Id);
                    return ResultExtensions.Cancelled<VerificationOutcome>();
                }

                if (blockedPersistResult.IsFailure)
                {
                    _logger.LogError(
                        "Verdict persist failed (BLOCKED) for {FileName} JobId={JobId}: {Error}",
                        submission.FileName,
                        job.Id,
                        blockedPersistResult.Error);
                    return Result<VerificationOutcome>.WithFailure(
                        blockedPersistResult.Error ?? "Verdict persistence failed");
                }

                _logger.LogInformation(
                    "Verdict persisted (BLOCKED) for {FileName} JobId={JobId}",
                    submission.FileName,
                    job.Id);

                // Stage 9 (Report) — best-effort for BLOCKED outcomes.
                // Notify (stage 10) is RED-only; BLOCKED does not trigger an alert.
                var blockedFindings = new List<RuleFinding>();
                try
                {
                    var blockedReportResult = _reportGenerator.Generate(
                        submission.Pdf,
                        blockedFindings,
                        ct);

                    if (blockedReportResult.IsFailure)
                    {
                        _logger.LogWarning(
                            "Marked-PDF generation failed (BLOCKED) for {FileName} JobId={JobId}: {Error} — continuing (best-effort)",
                            submission.FileName,
                            job.Id,
                            blockedReportResult.Error);

                        blockedFindings.Add(new RuleFinding(
                            CheckId: "ReportGenerationFailure",
                            Verdict: FindingVerdict.InsufficientData,
                            Technique: TechniqueClass.Deterministic,
                            Severity: FindingSeverity.Warning,
                            EngineVersion: EngineVersion,
                            Observed: blockedReportResult.Error));
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Marked-PDF generated (BLOCKED) for {FileName} JobId={JobId} ({Bytes} bytes)",
                            submission.FileName,
                            job.Id,
                            blockedReportResult.Value?.Length ?? 0);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Marked-PDF generation threw an exception (BLOCKED) for {FileName} JobId={JobId} — continuing (best-effort)",
                        submission.FileName,
                        job.Id);

                    blockedFindings.Add(new RuleFinding(
                        CheckId: "ReportGenerationFailure",
                        Verdict: FindingVerdict.InsufficientData,
                        Technique: TechniqueClass.Deterministic,
                        Severity: FindingSeverity.Warning,
                        EngineVersion: EngineVersion,
                        Observed: ex.Message));
                }

                return Result<VerificationOutcome>.WithSuccess(
                    new VerificationOutcome(job, blockedVerdictResult.Value!, blockedFindings, blockedDuration));
            }

            // Unexpected non-BLOCKED bind failure
            _logger.LogError(
                "Unexpected binding failure for {FileName}: {Error}",
                submission.FileName,
                bindResult.Error);
            return Result<VerificationOutcome>.WithFailure(bindResult.Error ?? "Binding failed");
        }

        var bindCtx = bindResult.Value!;

        // Stage 5a — Tenant profile resolution (Fix C).
        // Resolve the active profile against the legal tolerance spec and the registered rule set.
        // With the default legal-baseline profile (no overrides) this yields all legal defaults
        // and zero deviations — existing e2e behaviour (RED verdict, same finding count) unchanged.
        var resolveResult = _tenantResolver.Resolve(
            _activeTenantProfile, _toleranceProvider, _rules, ct);

        if (resolveResult.IsCancelled())
        {
            _logger.LogWarning("Pipeline cancelled during tenant-profile resolution for {FileName}", submission.FileName);
            return ResultExtensions.Cancelled<VerificationOutcome>();
        }

        if (resolveResult.IsFailure)
        {
            _logger.LogError(
                "Tenant-profile resolution failed for {FileName}: {Error}",
                submission.FileName,
                resolveResult.Error);
            return Result<VerificationOutcome>.WithFailure(resolveResult.Error ?? "Tenant profile resolution failed");
        }

        var resolvedProfile = resolveResult.Value!;

        if (resolvedProfile.HasDeviations)
        {
            _logger.LogInformation(
                "Tenant profile '{TenantId}' has {DeviationCount} override deviation(s) for {FileName}",
                resolvedProfile.TenantId,
                resolvedProfile.Deviations.Count,
                submission.FileName);
        }

        // Stage 5b — Build FINAL VerificationContext with the extracted StatementModel
        // and the resolved tenant profile.
        var finalCtx = new VerificationContext(
            bindCtx.Bundle,
            bindCtx.ResolvedProduct,
            bindCtx.Availability,
            bindCtx.PriorStatement,
            bindCtx.ToleranceConfig,
            statementModel,
            tenantProfile: resolvedProfile);

        // Stage 6 — Validation Engine
        Result<IReadOnlyList<RuleFinding>> engineResult;
        using (PipelineActivitySource.StartActivity("pipeline.stage.validate"))
            engineResult = await _engine.RunAsync(finalCtx, ct).ConfigureAwait(false);

        if (engineResult.IsCancelled())
        {
            _logger.LogWarning("Pipeline cancelled during rule execution for {FileName}", submission.FileName);
            return ResultExtensions.Cancelled<VerificationOutcome>();
        }

        if (engineResult.IsFailure)
        {
            _logger.LogError(
                "Validation engine failed for {FileName}: {Error}",
                submission.FileName,
                engineResult.Error);
            return Result<VerificationOutcome>.WithFailure(engineResult.Error ?? "Validation engine failed");
        }

        IReadOnlyList<RuleFinding> findings = engineResult.Value!;
        _logger.LogInformation(
            "Validation engine produced {FindingCount} findings for {FileName}",
            findings.Count,
            submission.FileName);

        // Stage 6b — Fetch per-tenant checklist-tier map (Story 1.3).
        // Partitions Fail findings into Bank / CONDUSEF tiers so BankTierVerdict and
        // CondusefTierVerdict can be computed independently.
        // Conservative fallback: any failure or empty map → pass null to Aggregate (legacy
        // single-tier path), treating all failures as CONDUSEF (regulatory-safe default).
        IReadOnlyDictionary<string, ChecklistTier>? checklistTiers = null;

        using (PipelineActivitySource.StartActivity("pipeline.stage.tier-map"))
        {
            var tierResult = await _referenceDataProvider
                .GetChecklistTiersAsync(submission.ContextKey, ct)
                .ConfigureAwait(false);

            if (tierResult is null || tierResult.IsFailure || tierResult.IsCancelled())
            {
                _logger.LogWarning(
                    "Checklist-tier data unavailable for {Institution} — verdict falls back to single-tier aggregation. Error={TierError}",
                    submission.ContextKey.Institution,
                    tierResult?.Error ?? "<null result>");
            }
            else if (tierResult.Value is null || tierResult.Value.Count == 0)
            {
                _logger.LogWarning(
                    "Checklist-tier map is empty for {Institution} — verdict falls back to single-tier aggregation.",
                    submission.ContextKey.Institution);
            }
            else
            {
                checklistTiers = tierResult.Value;
                _logger.LogInformation(
                    "Checklist-tier map loaded for {Institution}: {TierCount} entries.",
                    submission.ContextKey.Institution,
                    checklistTiers.Count);
            }
        }

        // Stage 7 — Verdict Aggregation
        // Thread resolved.Deviations into the aggregator so compliance reviewers can see
        // which tenant overrides were reverted to the legal baseline.
        // Thread checklistTiers (Story 1.3): non-null when tier data was available;
        // null triggers the legacy single-tier fallback inside the aggregator.
        Result<VerdictSummary> verdictResult;
        using (PipelineActivitySource.StartActivity("pipeline.stage.verdict"))
            verdictResult = _aggregator.Aggregate(
                findings,
                blocked: null,
                ct: ct,
                tenantDeviations: resolvedProfile.Deviations,
                checklistTiers: checklistTiers);

        if (verdictResult.IsCancelled())
        {
            _logger.LogWarning("Pipeline cancelled during verdict aggregation for {FileName}", submission.FileName);
            return ResultExtensions.Cancelled<VerificationOutcome>();
        }

        if (verdictResult.IsFailure)
        {
            _logger.LogError(
                "Verdict aggregation failed for {FileName}: {Error}",
                submission.FileName,
                verdictResult.Error);
            return Result<VerificationOutcome>.WithFailure(verdictResult.Error ?? "Verdict aggregation failed");
        }

        var summary = verdictResult.Value!;

        sw.Stop();
        var elapsed = sw.Elapsed;
        _metrics.RecordStatement(elapsed.TotalMilliseconds, summary.Signal);

        _logger.LogInformation(
            "Pipeline verdict for {FileName}. Signal={VerdictSignal} Fails={FailCount} DurationMs={DurationMs:F1} JobId={JobId}",
            submission.FileName,
            summary.Signal,
            summary.FailCount,
            elapsed.TotalMilliseconds,
            job.Id);

        // Stage 8 — Persist (verdict + findings durable before report/notify)
        // Non-optional: a persist failure returns Result.WithFailure so the caller knows the
        // verdict was NOT written.  Never silently discard a computed verdict.
        // Story 1.4: thread both tier verdicts and the tier map so each Finding is stamped
        // with its ChecklistTier and the JobVerdict carries BankTierVerdict + CondusefTierVerdict.
        Result<JobVerdict> persistResult;
        using (PipelineActivitySource.StartActivity("pipeline.stage.persist"))
            persistResult = await _verdictPersistence.PersistAsync(
                jobId: job.Id,
                signal: summary.Signal,
                findings: findings,
                engineVersion: EngineVersion,
                bankTierVerdict: summary.BankTierVerdict,
                condusefTierVerdict: summary.CondusefTierVerdict,
                checklistTiers: checklistTiers,
                cancellationToken: ct,
                referenceBundleVersion: BundleProvenanceVersion(catalogBundle)).ConfigureAwait(false);

        if (persistResult.IsCancelled())
        {
            _logger.LogWarning(
                "Pipeline cancelled during persist for {FileName} JobId={JobId}",
                submission.FileName,
                job.Id);
            return ResultExtensions.Cancelled<VerificationOutcome>();
        }

        if (persistResult.IsFailure)
        {
            _logger.LogError(
                "Verdict persist failed for {FileName} JobId={JobId}: {Error}",
                submission.FileName,
                job.Id,
                persistResult.Error);
            return Result<VerificationOutcome>.WithFailure(
                persistResult.Error ?? "Verdict persistence failed");
        }

        _logger.LogInformation(
            "Pipeline complete for {FileName}. Signal={VerdictSignal} Fails={FailCount} DurationMs={DurationMs:F1} JobId={JobId}",
            submission.FileName,
            summary.Signal,
            summary.FailCount,
            elapsed.TotalMilliseconds,
            job.Id);

        // Stage 9 — Report (marked PDF): best-effort, non-fatal.
        // Run on RED, YELLOW, or EXTRACTION_GAP so reviewers always get an annotated copy when
        // the verdict is non-green.  Owner policy: YELLOW (bank improvement opportunities)
        // generates the same marked-PDF report as RED.  ExtractionGap (e.g. scanned PDF) also
        // generates a report so reviewers see why the verdict could not be reached.
        // TransientFailure does NOT generate a report — it is retryable, not a final outcome.
        // A generator failure appends a diagnostic finding but does NOT change the
        // already-determined verdict or make the pipeline fail.
        var reportFindings = new List<RuleFinding>(findings);

        if (summary.Signal is VerdictSignal.Red or VerdictSignal.Yellow or VerdictSignal.ExtractionGap)
        {
            using var reportActivity = PipelineActivitySource.StartActivity("pipeline.stage.report");
            try
            {
                var reportResult = _reportGenerator.Generate(
                    submission.Pdf,
                    findings,
                    ct,
                    checklistTiers: checklistTiers);

                if (reportResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Marked-PDF generation failed for {FileName} JobId={JobId}: {Error} — continuing (best-effort)",
                        submission.FileName,
                        job.Id,
                        reportResult.Error);

                    reportFindings.Add(new RuleFinding(
                        CheckId: "ReportGenerationFailure",
                        Verdict: FindingVerdict.InsufficientData,
                        Technique: TechniqueClass.Deterministic,
                        Severity: FindingSeverity.Warning,
                        EngineVersion: EngineVersion,
                        Observed: reportResult.Error));
                }
                else
                {
                    _logger.LogInformation(
                        "Marked-PDF generated for {FileName} JobId={JobId} ({Bytes} bytes)",
                        submission.FileName,
                        job.Id,
                        reportResult.Value?.Length ?? 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Marked-PDF generation threw an exception for {FileName} JobId={JobId} — continuing (best-effort)",
                    submission.FileName,
                    job.Id);

                reportFindings.Add(new RuleFinding(
                    CheckId: "ReportGenerationFailure",
                    Verdict: FindingVerdict.InsufficientData,
                    Technique: TechniqueClass.Deterministic,
                    Severity: FindingSeverity.Warning,
                    EngineVersion: EngineVersion,
                    Observed: ex.Message));
            }
        }

        // Stage 10 — Notify (alert email): best-effort, non-fatal.
        // RED and YELLOW verdicts trigger an email; BLOCKED and GREEN are silent.
        // Owner policy: YELLOW (bank improvement opportunities) sends an alert just like RED,
        // but the email wording must not claim regulatory failure (handled in VecAlertService).
        // Duplicate-alert guard implemented in VecAlertService via AlertSentAt flag (Story E2-S15).
        if (summary.Signal is VerdictSignal.Red or VerdictSignal.Yellow)
        {
            using var notifyActivity = PipelineActivitySource.StartActivity("pipeline.stage.notify");
            try
            {
                var alertContext = new AlertContext(
                    StatementId: submission.FileName,
                    Recipients: _alertOptions.Recipients,
                    JobVerdictId: persistResult.Value?.Id);

                var alertResult = await _alertService.SendRedAlertAsync(summary, alertContext, ct)
                    .ConfigureAwait(false);

                if (alertResult.IsCancelled())
                {
                    _logger.LogWarning(
                        "Pipeline cancelled during {Signal} alert for {FileName} JobId={JobId}",
                        summary.Signal,
                        submission.FileName,
                        job.Id);

                    // Cancelled during notify: still return success with the outcome — the
                    // verdict is already persisted; the notify failure is non-fatal.
                    return Result<VerificationOutcome>.WithSuccess(
                        new VerificationOutcome(job, summary, reportFindings, elapsed));
                }

                if (alertResult.IsFailure)
                {
                    _logger.LogWarning(
                        "{Signal} alert send failed for {FileName} JobId={JobId}: {Error} — continuing (best-effort)",
                        summary.Signal,
                        submission.FileName,
                        job.Id,
                        alertResult.Error);

                    reportFindings.Add(new RuleFinding(
                        CheckId: "NotificationFailure",
                        Verdict: FindingVerdict.InsufficientData,
                        Technique: TechniqueClass.Deterministic,
                        Severity: FindingSeverity.Warning,
                        EngineVersion: EngineVersion,
                        Observed: alertResult.Error));
                }
                else
                {
                    _logger.LogInformation(
                        "{Signal} alert dispatched for {FileName} JobId={JobId}",
                        summary.Signal,
                        submission.FileName,
                        job.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "{Signal} alert threw an exception for {FileName} JobId={JobId} — continuing (best-effort)",
                    summary.Signal,
                    submission.FileName,
                    job.Id);

                reportFindings.Add(new RuleFinding(
                    CheckId: "NotificationFailure",
                    Verdict: FindingVerdict.InsufficientData,
                    Technique: TechniqueClass.Deterministic,
                    Severity: FindingSeverity.Warning,
                    EngineVersion: EngineVersion,
                    Observed: ex.Message));
            }
        }

        return Result<VerificationOutcome>.WithSuccess(
            new VerificationOutcome(job, summary, reportFindings, elapsed));
    }

    // -----------------------------------------------------------------------
    // Extraction-coverage floor helper (Story E1-S10)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Counts the number of fields in <paramref name="model"/> whose extraction status is
    /// <see cref="ExtractionStatus.Extracted"/> or <see cref="ExtractionStatus.ExtractedInvalidFormat"/>.
    /// Both statuses indicate that the extractor found the field in the PDF — even an
    /// invalid-format field means the text layer was readable; only
    /// <see cref="ExtractionStatus.NotExtracted"/> fields are excluded from the count.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The count covers the 7 mandatory header fields plus all non-null
    /// <see cref="PeriodSummary"/> fields (up to 22 additional fields when the full extraction
    /// pass ran successfully).  Collections such as <see cref="StatementModel.Movements"/>,
    /// <see cref="StatementModel.FontRuns"/>, and <see cref="StatementModel.TypographySamples"/>
    /// are intentionally excluded: they are bulk record sets, not named scalar fields, and
    /// their count would swamp the floor semantics.
    /// </para>
    /// <para>
    /// This is a <c>static</c> method so it can be tested independently of the pipeline
    /// instance without constructing all 15 ctor dependencies.
    /// </para>
    /// </remarks>
    internal static int CountExtractedFields(StatementModel model)
    {
        var count = 0;

        // ── Header fields (7 always-present ExtractedField<T> properties) ────
        if (IsExtracted(model.ClientName.Status)) count++;
        if (IsExtracted(model.Address.Status)) count++;
        if (IsExtracted(model.BranchNumber.Status)) count++;
        if (IsExtracted(model.CardNumber.Status)) count++;
        if (IsExtracted(model.Clabe.Status)) count++;
        if (IsExtracted(model.ClientNumber.Status)) count++;
        if (IsExtracted(model.Rfc.Status)) count++;

        // ── PeriodSummary fields (present after a full extraction pass) ──────
        if (model.PeriodSummary is { } ps)
        {
            if (IsExtracted(ps.Product.Status)) count++;
            if (IsExtracted(ps.PeriodStart.Status)) count++;
            if (IsExtracted(ps.PeriodCutDate.Status)) count++;
            if (IsExtracted(ps.PaymentDueDate.Status)) count++;
            if (IsExtracted(ps.DayCountPrinted.Status)) count++;
            if (IsExtracted(ps.PagoParaNoGenerarIntereses.Status)) count++;
            if (IsExtracted(ps.PagoMinimo.Status)) count++;
            if (IsExtracted(ps.PagoMinimoMasMeses.Status)) count++;
            if (IsExtracted(ps.Tasa.Status)) count++;
            if (IsExtracted(ps.Cat.Status)) count++;
            if (IsExtracted(ps.SaldoDeudorTotal.Status)) count++;
            if (IsExtracted(ps.CreditoDisponible.Status)) count++;
            if (IsExtracted(ps.AdeudoPeriodoAnterior.Status)) count++;
            if (IsExtracted(ps.CargosRegularesNoMeses.Status)) count++;
            if (IsExtracted(ps.CargosComprasAMesesCapital.Status)) count++;
            if (IsExtracted(ps.MontoIntereses.Status)) count++;
            if (IsExtracted(ps.MontoComisiones.Status)) count++;
            if (IsExtracted(ps.IvaInteresesYComisiones.Status)) count++;
            if (IsExtracted(ps.PagosYAbonos.Status)) count++;
            if (IsExtracted(ps.SaldoCargosRegulares.Status)) count++;
            if (IsExtracted(ps.SaldoCargosAMeses.Status)) count++;
            if (IsExtracted(ps.TotalCargos.Status)) count++;
            if (IsExtracted(ps.TotalAbonos.Status)) count++;
        }

        return count;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the status indicates the field was found positionally
    /// in the PDF's own text layer, whether the value was well-formed
    /// (<see cref="ExtractionStatus.Extracted"/>) or had a format defect
    /// (<see cref="ExtractionStatus.ExtractedInvalidFormat"/>).
    /// </summary>
    /// <remarks>
    /// <b>Provenance-aware by construction (E7.S7.2/S7.3 owner ruling 4):</b> a field resolved by
    /// an inference stage (semantic search, LLM extraction, or header-image OCR — status
    /// <see cref="ExtractionStatus.ExtractedByInference"/>) deliberately does NOT match either
    /// branch here and is therefore excluded from <see cref="CountExtractedFields"/>'s
    /// extraction-floor count. That floor exists to measure how much of the document's own text
    /// layer was readable; a value recovered from a second-source OCR pass over a rendered image
    /// says nothing about text-layer coverage and must not inflate it (e.g. a scanned/image-only
    /// PDF whose only recoverable field is an OCR-read Product must still fail the floor and
    /// resolve to <c>ExtractionGap</c>, never be pushed over the floor by that one field).
    /// </remarks>
    private static bool IsExtracted(ExtractionStatus status) =>
        status is ExtractionStatus.Extracted or ExtractionStatus.ExtractedInvalidFormat;

    // -----------------------------------------------------------------------
    // Text-layer density helper (Story E2-S1)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Counts the total number of words across all pages by splitting
    /// <see cref="StatementModel.NormalizedFullText"/> on ASCII spaces.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="StatementModel.NormalizedFullText"/> is produced by the extraction stage
    /// (Story 6.1): all page text is upper-cased, accent-stripped, and whitespace-collapsed
    /// to a single ASCII space.  Splitting on that space (with
    /// <see cref="StringSplitOptions.RemoveEmptyEntries"/>) therefore gives an accurate
    /// word count without re-running any extraction logic.
    /// </para>
    /// <para>
    /// An empty or whitespace-only <c>NormalizedFullText</c> (e.g. from a scanned / image-only
    /// PDF that has no text layer) yields a count of zero.
    /// </para>
    /// <para>
    /// This is a <c>static</c> method so it can be tested independently of the pipeline
    /// instance without constructing all ctor dependencies.
    /// </para>
    /// </remarks>
    internal static int CountTextLayerWords(StatementModel model)
    {
        if (string.IsNullOrWhiteSpace(model.NormalizedFullText))
            return 0;

        return model.NormalizedFullText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }

    // -----------------------------------------------------------------------
    // Ambiguous-document-scope helper (Story 4.2-B)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the PDF page count of <paramref name="model"/> as the heuristic
    /// multi-statement boundary signal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>STUB heuristic (Story 4.2-B):</b> page count is a cheap proxy for detecting
    /// archive-bundle PDFs that concatenate multiple monthly credit-card statements.  A typical
    /// CONDUSEF single-statement PDF is 8–15 pages; a bundle of two or more statements tends
    /// to exceed 20 pages.  When the page count exceeds
    /// <see cref="TenantProfile.MaxStatementBoundarySignalCount"/> the pipeline routes to
    /// <c>VerdictSignal.ExtractionGap</c> / <c>BlockReason.AmbiguousDocumentScope</c> BEFORE
    /// binding or running any rules.
    /// </para>
    /// <para>
    /// <b>Known limitation:</b> a legitimate single-statement PDF with more pages than the
    /// configured threshold would false-positive; a bundle of two very short statements might
    /// stay under the threshold.  Full detection (page-level structural analysis, distinct
    /// account-number anchors, period boundary cross-check) is deferred to a future story.
    /// The threshold is configurable via <see cref="TenantProfile.MaxStatementBoundarySignalCount"/>
    /// so integration tests can exercise later pipeline stages without triggering this guard.
    /// </para>
    /// <para>
    /// This is a <c>static</c> method so it can be tested independently of the pipeline
    /// instance without constructing all ctor dependencies.
    /// </para>
    /// </remarks>
    /// <param name="model">The extracted statement model.</param>
    /// <returns>
    /// <see cref="StatementModel.PageCount"/> of <paramref name="model"/>.
    /// Returns 0 when the model has not been fully extracted yet (before
    /// <c>IStatementFieldExtractor.ExtractFullAsync</c> completes).
    /// </returns>
    internal static int CountStatementBoundarySignals(StatementModel model)
        => model.PageCount;
}
