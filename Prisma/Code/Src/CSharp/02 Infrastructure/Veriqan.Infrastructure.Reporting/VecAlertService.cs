using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;

/// <summary>
/// Default implementation of <see cref="IVecAlertService"/> (FR-17, CL-54).
/// </summary>
/// <remarks>
/// <para>
/// <b>RED policy:</b> exactly one alert email is composed and dispatched via
/// <see cref="IEmailSender"/>.  Transient failures are retried using an exponential
/// back-off Polly pipeline (up to <see cref="AlertOptions.MaxRetryAttempts"/> total attempts).
/// If all attempts fail the error is logged at <c>Error</c> level and a typed
/// <see cref="Result"/> failure is returned — the failure is <b>never silently dropped</b>.
/// </para>
/// <para>
/// <b>Non-RED policy:</b> GREEN and BLOCKED verdicts produce no email.
/// A <see cref="Result.Success"/> is returned immediately; the caller can inspect
/// the outcome by checking <see cref="Result.IsSuccess"/>.
/// </para>
/// </remarks>
public sealed class VecAlertService : IVecAlertService
{
    private readonly IEmailSender _emailSender;
    private readonly AlertOptions _options;
    private readonly ILogger<VecAlertService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="VecAlertService"/>.
    /// </summary>
    /// <param name="emailSender">Transport used to dispatch the alert email.</param>
    /// <param name="options">Alert configuration (retry counts, recipients).</param>
    /// <param name="logger">Structured logger for diagnostic and error output.</param>
    public VecAlertService(
        IEmailSender emailSender,
        IOptions<AlertOptions> options,
        ILogger<VecAlertService> logger)
    {
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result> SendRedAlertAsync(
        VerdictSummary verdict,
        AlertContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        ArgumentNullException.ThrowIfNull(context);

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled();

        // -----------------------------------------------------------------------
        // Non-RED: no email — return success with a diagnostic note.
        // -----------------------------------------------------------------------
        if (verdict.Signal != VerdictSignal.Red)
        {
            _logger.LogDebug(
                "VecAlertService: signal is {Signal} for statement {StatementId} — no alert email sent.",
                verdict.Signal, context.StatementId);

            // Success: no alert is the correct outcome for non-RED verdicts.
            return Result.Success();
        }

        // -----------------------------------------------------------------------
        // RED: compose exactly one email, dispatch with retry.
        // -----------------------------------------------------------------------
        var message = ComposeRedAlert(verdict, context);

        _logger.LogInformation(
            "VEC RED alert triggered for statement {StatementId}: FailCount={FailCount}, FailCheckIds=[{FailCheckIds}].",
            context.StatementId,
            verdict.FailCount,
            string.Join(", ", verdict.FailCheckIds));

        var pipeline = BuildRetryPipeline(context.StatementId, cancellationToken);

        Result sendResult = Result.WithFailure("Alert not attempted.");

        try
        {
            sendResult = await pipeline.ExecuteAsync(
                ct => new ValueTask<Result>(_emailSender.SendAsync(message, ct)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "VecAlertService: RED alert for statement {StatementId} was cancelled during retry pipeline.",
                context.StatementId);
            return ResultExtensions.Cancelled();
        }

        if (sendResult.IsCancelled())
            return ResultExtensions.Cancelled();

        if (sendResult.IsFailure)
        {
            // Permanent failure after all retries — log at Error level (never silently dropped).
            _logger.LogError(
                "VEC RED alert FAILED permanently for statement {StatementId} after {MaxAttempts} attempt(s): {Error}",
                context.StatementId,
                _options.MaxRetryAttempts,
                sendResult.Error);

            return Result.WithFailure(
                $"RED alert for statement '{context.StatementId}' could not be delivered after {_options.MaxRetryAttempts} attempt(s): {sendResult.Error}");
        }

        _logger.LogInformation(
            "VEC RED alert delivered successfully for statement {StatementId}.",
            context.StatementId);

        return Result.Success();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Composes the RED-alert <see cref="EmailMessage"/> from the verdict and context.
    /// </summary>
    private static EmailMessage ComposeRedAlert(VerdictSummary verdict, AlertContext context)
    {
        var failIds = verdict.FailCheckIds.Any()
            ? string.Join(", ", verdict.FailCheckIds)
            : "(none recorded)";

        var body =
            $"VEC RED ALERT — Statement: {context.StatementId}\r\n" +
            $"\r\n" +
            $"Signal          : {verdict.Signal}\r\n" +
            $"Failed checks   : {verdict.FailCount}  [{failIds}]\r\n" +
            $"Passed checks   : {verdict.PassCount}\r\n" +
            $"Insufficient    : {verdict.InsufficientDataCount}\r\n" +
            $"Total evaluated : {verdict.Total}\r\n" +
            $"\r\n" +
            $"This alert was generated automatically by the VEC verification engine.\r\n" +
            $"Review the marked PDF and initiate corrective action as required.";

        return new EmailMessage(
            To: context.Recipients,
            Subject: $"VEC RED — {context.StatementId}",
            Body: body);
    }

    /// <summary>
    /// Builds a Polly <see cref="ResiliencePipeline"/> that retries on a failed (non-cancelled)
    /// <see cref="Result"/> return value, using exponential back-off.
    /// </summary>
    /// <remarks>
    /// The pipeline treats a <see cref="Result.IsFailure"/> return as a handled outcome and
    /// retries up to <c>MaxRetryAttempts - 1</c> additional times (total attempts =
    /// <see cref="AlertOptions.MaxRetryAttempts"/>).  Cancelled results short-circuit — they
    /// are not retried.
    /// </remarks>
    private ResiliencePipeline<Result> BuildRetryPipeline(
        string statementId,
        CancellationToken cancellationToken)
    {
        var maxRetries = Math.Max(0, _options.MaxRetryAttempts - 1); // attempts beyond the first
        var baseDelay = TimeSpan.FromMilliseconds(Math.Max(0, _options.BaseRetryDelayMs));

        return new ResiliencePipelineBuilder<Result>()
            .AddRetry(new RetryStrategyOptions<Result>
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = baseDelay,
                UseJitter = false,
                // Retry only on failure results; cancelled results short-circuit.
                ShouldHandle = args =>
                    ValueTask.FromResult(
                        args.Outcome.Result is not null &&
                        args.Outcome.Result.IsFailure &&
                        !cancellationToken.IsCancellationRequested),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "VEC RED alert send attempt {Attempt} of {Max} failed for statement {StatementId}: {Error} — retrying in {Delay}.",
                        args.AttemptNumber + 1,
                        _options.MaxRetryAttempts,
                        statementId,
                        args.Outcome.Result?.Error ?? "(exception)",
                        args.RetryDelay);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }
}
