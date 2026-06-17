using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Port for obtaining the VEC reference-data bundle for a given statement context.
/// Implementations include <c>CsvReferenceDataAdapter</c>, <c>DatabaseReferenceDataAdapter</c>,
/// and <c>ApiReferenceDataAdapter</c> — all return the same <see cref="VecReferenceBundle"/>.
/// </summary>
public interface IVecReferenceDataProvider
{
    /// <summary>
    /// Retrieves the reference-data bundle that matches the given statement context.
    /// </summary>
    /// <param name="key">Selects the correct bundle by institution, period, and optional account/product scope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the bundle on success,
    /// or a failure describing why the bundle could not be produced
    /// (missing files, schema validation errors, I/O problems, etc.).
    /// </returns>
    Task<Result<VecReferenceBundle>> GetBundleAsync(StatementContextKey key, CancellationToken ct);
}
