using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Application service port for submitting a PDF statement for compliance verification.
/// Implements idempotent ingestion: submitting the same bytes more than once returns
/// the existing <see cref="VerificationJob"/> without creating a duplicate.
/// </summary>
public interface IStatementIngestionService
{
    /// <summary>
    /// Ingests the supplied PDF bytes and returns a <see cref="VerificationJob"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>The method validates that <paramref name="content"/> is a valid PDF
    ///         (checks <c>%PDF-</c> magic header). Non-PDF or corrupt bytes produce a failure
    ///         result — no job is created.</item>
    ///   <item>A SHA-256 content hash is computed over <paramref name="content"/>. If a job
    ///         already exists for that hash the existing job is returned unchanged (idempotency).</item>
    ///   <item>On a pre-signalled cancellation token the method returns a cancelled result
    ///         immediately — no IO is performed.</item>
    /// </list>
    /// </remarks>
    /// <param name="content">Raw PDF bytes to ingest.</param>
    /// <param name="fileName">Original filename supplied by the caller (informational only).</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A <see cref="Result{T}"/> containing the new or existing job on success.</returns>
    Task<Result<VerificationJob>> IngestAsync(
        byte[] content,
        string fileName,
        CancellationToken cancellationToken = default);
}
