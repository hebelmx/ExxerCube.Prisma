using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

/// <summary>
/// Default <see cref="IVerificationPipeline"/> implementation that orchestrates all
/// verification stages in sequence: ingest → extract → bind → run rules → aggregate verdict.
/// </summary>
/// <remarks>
/// Registered as <b>Scoped</b> so each HTTP request / batch item gets its own instance,
/// keeping the scoped <c>VeriqanDbContext</c> (accessed via the ingestion service) properly bounded.
/// </remarks>
internal sealed class VerificationPipeline : IVerificationPipeline
{
    private readonly IStatementIngestionService _ingestion;
    private readonly IStatementFieldExtractor _extractor;
    private readonly IBundleBinder _binder;
    private readonly IVecValidationEngine _engine;
    private readonly IVerdictAggregator _aggregator;
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
        ILogger<VerificationPipeline> logger)
    {
        _ingestion = ingestion ?? throw new ArgumentNullException(nameof(ingestion));
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _binder = binder ?? throw new ArgumentNullException(nameof(binder));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
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

        _logger.LogInformation(
            "Pipeline starting for {FileName}",
            submission.FileName);

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

                return Result<VerificationOutcome>.WithSuccess(
                    new VerificationOutcome(job, blockedVerdictResult.Value!, Array.Empty<RuleFinding>()));
            }

            // Unexpected non-BLOCKED bind failure
            _logger.LogError(
                "Unexpected binding failure for {FileName}: {Error}",
                submission.FileName,
                bindResult.Error);
            return Result<VerificationOutcome>.WithFailure(bindResult.Error ?? "Binding failed");
        }

        var bindCtx = bindResult.Value!;

        // Stage 5 — Build FINAL VerificationContext with the extracted StatementModel
        var finalCtx = new VerificationContext(
            bindCtx.Bundle,
            bindCtx.ResolvedProduct,
            bindCtx.Availability,
            bindCtx.PriorStatement,
            bindCtx.ToleranceConfig,
            statementModel);

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
        var verdictResult = _aggregator.Aggregate(findings, blocked: null, ct: ct);

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
        _logger.LogInformation(
            "Pipeline complete for {FileName}. Signal={VerdictSignal} Fails={FailCount}",
            submission.FileName,
            summary.Signal,
            summary.FailCount);

        return Result<VerificationOutcome>.WithSuccess(new VerificationOutcome(job, summary, findings));
    }
}
