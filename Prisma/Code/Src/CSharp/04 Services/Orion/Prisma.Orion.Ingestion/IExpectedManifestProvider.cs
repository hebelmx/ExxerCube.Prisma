using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Services.Manifest;
using IndQuestResults;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// Port for loading the operator-supplied expected-case manifest (Listado) that drives per-cycle
/// reconciliation (Item B #8).
/// </summary>
/// <remarks>
/// Implementations must be fault-tolerant: a missing file, blank path, or unparseable JSON
/// returns a <see cref="Result{T}"/> failure — they must never throw or crash the watch loop.
/// </remarks>
public interface IExpectedManifestProvider
{
    /// <summary>
    /// Loads and parses the expected manifest asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>
    /// A success result with the parsed <see cref="ExpectedManifest"/>, or a failure result when
    /// the manifest path is blank, the file does not exist, or the JSON is unparseable.
    /// </returns>
    Task<Result<ExpectedManifest>> LoadAsync(CancellationToken cancellationToken = default);
}
