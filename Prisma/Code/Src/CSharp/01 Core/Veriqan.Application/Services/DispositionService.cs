using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Application.Services;

/// <summary>
/// Default implementation of <see cref="IDispositionService"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Human-actor invariant:</b> Both <see cref="DispositionFindingAsync"/> and
/// <see cref="DispositionStatementAsync"/> validate that <c>actor</c> is a non-empty,
/// non-whitespace string before any row is written. There is no overload or code path
/// that accepts a null/blank actor — this class never auto-dispositions on behalf of the
/// system.
/// </para>
/// <para>
/// <b>Append-only invariant:</b> Every call appends a new <see cref="Disposition"/> row via
/// <see cref="IDispositionRepository.AppendAsync"/>. This service never calls an update or
/// delete operation. Calling the method twice on the same finding therefore produces two
/// independent rows in the audit table — the earlier row is unaffected.
/// </para>
/// </remarks>
internal sealed class DispositionService : IDispositionService
{
    private readonly IDispositionRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DispositionService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="DispositionService"/>.
    /// </summary>
    /// <param name="repository">Append-only disposition repository port.</param>
    /// <param name="timeProvider">Clock abstraction for stamping disposition timestamps.</param>
    /// <param name="logger">Logger for structured diagnostics.</param>
    public DispositionService(
        IDispositionRepository repository,
        TimeProvider timeProvider,
        ILogger<DispositionService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<Disposition>> DispositionFindingAsync(
        Guid jobId,
        Guid findingId,
        DispositionAction action,
        string actor,
        string? notes = null,
        string? beforeState = null,
        string? afterState = null,
        string? engineVersion = null,
        string? referenceBundleVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<Disposition>();

        // Human-actor invariant: reject immediately if actor is absent.
        // VEC never auto-dispositions — a real human user id is always required.
        if (string.IsNullOrWhiteSpace(actor))
        {
            _logger.LogWarning(
                "DispositionFinding rejected: actor is null or whitespace for Job {JobId} Finding {FindingId}.",
                jobId, findingId);
            return Result<Disposition>.WithFailure(
                "A non-empty actor is required. VEC never auto-dispositions — a human reviewer must be identified.");
        }

        return await AppendDispositionAsync(
            jobId: jobId,
            findingId: findingId,
            action: action,
            actor: actor,
            notes: notes,
            beforeState: beforeState,
            afterState: afterState,
            engineVersion: engineVersion,
            referenceBundleVersion: referenceBundleVersion,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<Disposition>> DispositionStatementAsync(
        Guid jobId,
        DispositionAction action,
        string actor,
        string? notes = null,
        string? beforeState = null,
        string? afterState = null,
        string? engineVersion = null,
        string? referenceBundleVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<Disposition>();

        // Human-actor invariant: reject immediately if actor is absent.
        // VEC never auto-dispositions — a real human user id is always required.
        if (string.IsNullOrWhiteSpace(actor))
        {
            _logger.LogWarning(
                "DispositionStatement rejected: actor is null or whitespace for Job {JobId}.",
                jobId);
            return Result<Disposition>.WithFailure(
                "A non-empty actor is required. VEC never auto-dispositions — a human reviewer must be identified.");
        }

        // findingId = null signals a statement-level (aggregate) disposition.
        return await AppendDispositionAsync(
            jobId: jobId,
            findingId: null,
            action: action,
            actor: actor,
            notes: notes,
            beforeState: beforeState,
            afterState: afterState,
            engineVersion: engineVersion,
            referenceBundleVersion: referenceBundleVersion,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<Disposition>>> GetDispositionsForJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<Disposition>>());

        return _repository.GetForJobAsync(jobId, cancellationToken);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Constructs and appends a <see cref="Disposition"/> row.
    /// Caller is responsible for actor validation before invoking this method.
    /// </summary>
    private async Task<Result<Disposition>> AppendDispositionAsync(
        Guid jobId,
        Guid? findingId,
        DispositionAction action,
        string actor,
        string? notes,
        string? beforeState,
        string? afterState,
        string? engineVersion,
        string? referenceBundleVersion,
        CancellationToken cancellationToken)
    {
        var disposition = new Disposition(
            id: Guid.NewGuid(),
            verificationJobId: jobId,
            findingId: findingId,
            action: action,
            actor: actor,
            dispositionedAtUtc: _timeProvider.GetUtcNow(),
            beforeState: beforeState,
            afterState: afterState,
            notes: notes,
            engineVersion: engineVersion,
            referenceBundleVersion: referenceBundleVersion);

        _logger.LogInformation(
            "Appending {Action} disposition by {Actor} on Job {JobId} Finding {FindingId}.",
            action, actor, jobId, findingId?.ToString() ?? "<statement>");

        return await _repository.AppendAsync(disposition, cancellationToken).ConfigureAwait(false);
    }
}
