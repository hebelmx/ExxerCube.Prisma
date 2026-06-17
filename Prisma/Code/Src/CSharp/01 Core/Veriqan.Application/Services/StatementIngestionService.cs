using System;
using System.Security.Cryptography;
using System.Text;
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
/// Default implementation of <see cref="IStatementIngestionService"/>.
/// Validates the submitted bytes, computes a SHA-256 content hash, and applies
/// idempotent persistence via <see cref="IVerificationJobRepository"/>.
/// </summary>
internal sealed class StatementIngestionService : IStatementIngestionService
{
    // PDF magic bytes: %PDF- (25 50 44 46 2D)
    private static readonly byte[] PdfMagic = Encoding.ASCII.GetBytes("%PDF-");

    private readonly IVerificationJobRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StatementIngestionService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="StatementIngestionService"/>.
    /// </summary>
    /// <param name="repository">Repository port used to query and persist jobs.</param>
    /// <param name="timeProvider">
    /// Clock abstraction used to stamp job creation time (inject
    /// <see cref="TimeProvider.System"/> in production; a fake provider in tests).
    /// </param>
    /// <param name="logger">Logger for structured diagnostics.</param>
    public StatementIngestionService(
        IVerificationJobRepository repository,
        TimeProvider timeProvider,
        ILogger<StatementIngestionService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<VerificationJob>> IngestAsync(
        byte[] content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<VerificationJob>();

        // Guard: null/empty bytes
        if (content is null || content.Length == 0)
        {
            _logger.LogWarning("Ingestion rejected for {FileName}: content is null or empty.", fileName);
            return Result<VerificationJob>.WithFailure("PDF content must not be null or empty.");
        }

        // Guard: not a PDF (magic-header check)
        if (!IsPdf(content))
        {
            _logger.LogWarning(
                "Ingestion rejected for {FileName}: content does not start with %PDF- magic header.",
                fileName);
            return Result<VerificationJob>.WithFailure(
                $"The submitted file '{fileName}' is not a valid PDF (missing %PDF- header).");
        }

        // Compute SHA-256 content hash
        var contentHash = ComputeSha256Hex(content);

        _logger.LogDebug("Ingesting statement {FileName} with hash {ContentHash}.", fileName, contentHash);

        // Idempotency: check for an existing job with the same content hash
        var findResult = await _repository.FindByContentHashAsync(contentHash, cancellationToken)
            .ConfigureAwait(false);

        if (findResult.IsCancelled())
            return ResultExtensions.Cancelled<VerificationJob>();

        if (findResult.IsFailure)
        {
            _logger.LogError(
                "Repository lookup failed for hash {ContentHash}: {Error}",
                contentHash,
                findResult.Error);
            return Result<VerificationJob>.WithFailure(
                findResult.Error ?? "Repository lookup failed.");
        }

        // IsSuccessMayBeNull: true when the query succeeded but no row was found (null value).
        if (findResult.IsSuccessMayBeNull && findResult.Value is not null)
        {
            var existing = findResult.Value;
            _logger.LogInformation(
                "Idempotent ingest: returning existing job {JobId} for hash {ContentHash}.",
                existing.Id,
                contentHash);
            return Result<VerificationJob>.WithSuccess(existing);
        }

        // Create a new job
        var now = _timeProvider.GetUtcNow();
        var newJob = new VerificationJob(
            id: Guid.NewGuid(),
            contentHash: contentHash,
            receivedAtUtc: now,
            status: VerificationJobStatus.Pending);

        var addResult = await _repository.AddAsync(newJob, cancellationToken)
            .ConfigureAwait(false);

        if (addResult.IsCancelled())
            return ResultExtensions.Cancelled<VerificationJob>();

        if (addResult.IsFailure)
        {
            _logger.LogError(
                "Failed to persist new job for hash {ContentHash}: {Error}",
                contentHash,
                addResult.Error);
            return Result<VerificationJob>.WithFailure(
                addResult.Error ?? "Failed to persist the new verification job.");
        }

        _logger.LogInformation(
            "New verification job {JobId} created for {FileName} (hash {ContentHash}).",
            addResult.Value!.Id,
            fileName,
            contentHash);

        return Result<VerificationJob>.WithSuccess(addResult.Value!);
    }

    /// <summary>
    /// Returns <c>true</c> when the first 5 bytes of <paramref name="bytes"/> match
    /// the PDF magic header <c>%PDF-</c>.
    /// </summary>
    private static bool IsPdf(byte[] bytes)
    {
        if (bytes.Length < PdfMagic.Length)
            return false;

        for (var i = 0; i < PdfMagic.Length; i++)
        {
            if (bytes[i] != PdfMagic[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Computes the SHA-256 digest of <paramref name="bytes"/> and returns it as a lowercase
    /// 64-character hex string.
    /// </summary>
    private static string ComputeSha256Hex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
