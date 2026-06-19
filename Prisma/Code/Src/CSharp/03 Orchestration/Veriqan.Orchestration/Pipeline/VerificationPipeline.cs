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
using ExxerCube.Prisma.Veriqan.Domain.Enums;
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
    /// row for auditability (NFR-7).
    /// </summary>
    private const string EngineVersion = "1.0.0";

    private readonly IStatementIngestionService _ingestion;
    private readonly IStatementFieldExtractor _extractor;
    private readonly IBundleBinder _binder;
    private readonly IVecValidationEngine _engine;
    private readonly IVerdictAggregator _aggregator;
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

        // Stage 1 — Ingestion
        var ingestResult = await _ingestion.IngestAsync(submission.Pdf, submission.FileName, ct)
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

        // Stage 2 — Field Extraction
        var extractResult = await _extractor.ExtractFullAsync(submission.Pdf, ct)
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

        // Stage 3 — Resolve product token from extracted model, with fallback to context key
        var productToken = statementModel.PeriodSummary?.Product.Value
                           ?? submission.ContextKey.ProductId
                           ?? string.Empty;

        _logger.LogInformation(
            "Resolved product token '{ProductToken}' for {FileName}",
            productToken,
            submission.FileName);

        // Stage 4 — Context Binding
        var bindResult = await _binder.BindAsync(job, submission.ContextKey, productToken, ct)
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
                _metrics.RecordStatement(blockedDuration.TotalMilliseconds, VerdictSignal.Blocked);
                _logger.LogInformation(
                    "Pipeline blocked for {FileName} in {DurationMs:F1} ms JobId={JobId}",
                    submission.FileName,
                    blockedDuration.TotalMilliseconds,
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
        var engineResult = await _engine.RunAsync(finalCtx, ct).ConfigureAwait(false);

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

        // Stage 7 — Verdict Aggregation
        // Thread resolved.Deviations into the aggregator so compliance reviewers can see
        // which tenant overrides were reverted to the legal baseline.
        var verdictResult = _aggregator.Aggregate(
            findings,
            blocked: null,
            ct: ct,
            tenantDeviations: resolvedProfile.Deviations);

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
        var persistResult = await _verdictPersistence.PersistAsync(
            jobId: job.Id,
            signal: summary.Signal,
            findings: findings,
            engineVersion: EngineVersion,
            cancellationToken: ct).ConfigureAwait(false);

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
        // Run on RED or BLOCKED so reviewers always get an annotated copy when the
        // verdict is non-green.  A generator failure appends a diagnostic finding but
        // does NOT change the already-determined verdict or make the pipeline fail.
        var reportFindings = new List<RuleFinding>(findings);

        if (summary.Signal is VerdictSignal.Red or VerdictSignal.Blocked)
        {
            try
            {
                var reportResult = _reportGenerator.Generate(
                    submission.Pdf,
                    findings,
                    ct);

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

        // Stage 10 — Notify (RED alert email): best-effort, non-fatal.
        // Only RED verdicts trigger an email; BLOCKED and GREEN are silent.
        // TODO(E2-S12): duplicate-alert guard (idempotency key per job) is a separate story.
        if (summary.Signal == VerdictSignal.Red)
        {
            try
            {
                var alertContext = new AlertContext(
                    StatementId: submission.FileName,
                    Recipients: _alertOptions.Recipients);

                var alertResult = await _alertService.SendRedAlertAsync(summary, alertContext, ct)
                    .ConfigureAwait(false);

                if (alertResult.IsCancelled())
                {
                    _logger.LogWarning(
                        "Pipeline cancelled during RED alert for {FileName} JobId={JobId}",
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
                        "RED alert send failed for {FileName} JobId={JobId}: {Error} — continuing (best-effort)",
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
                        "RED alert dispatched for {FileName} JobId={JobId}",
                        submission.FileName,
                        job.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "RED alert threw an exception for {FileName} JobId={JobId} — continuing (best-effort)",
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
}
