using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Port for persisting and retrieving the consolidated <see cref="UnifiedMetadataRecord"/> so that
/// reviewer overrides survive across requests (C2) and field annotations can be hydrated from real
/// data rather than stubs (C3).
/// </summary>
/// <remarks>
/// The record is keyed by file identifier and the store is an upsert: repeated saves for the same
/// <c>fileId</c> overwrite the previous payload. Implementations must serialise
/// <see cref="UnifiedMetadataRecord"/> (including any <c>EnumModel</c>-derived members) without
/// data loss and return a typed Result — never throw for business-logic errors.
/// </remarks>
public interface IUnifiedMetadataStore
{
    /// <summary>
    /// Persists (upserts) the supplied <paramref name="record"/> keyed by <paramref name="fileId"/>.
    /// </summary>
    /// <param name="fileId">The file identifier that owns this record.</param>
    /// <param name="record">The consolidated metadata record to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Success on success; failure on any persistence error. Never throws for business logic.
    /// </returns>
    Task<Result> SaveAsync(string fileId, UnifiedMetadataRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the <see cref="UnifiedMetadataRecord"/> previously persisted for <paramref name="fileId"/>.
    /// </summary>
    /// <param name="fileId">The file identifier to look up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Success with the record when a row is found; failure when no row exists for the supplied
    /// <paramref name="fileId"/> (callers treat IsFailure as "not yet stored" and degrade gracefully).
    /// Never returns success-with-null.
    /// </returns>
    Task<Result<UnifiedMetadataRecord?>> GetByFileIdAsync(string fileId, CancellationToken cancellationToken = default);
}
