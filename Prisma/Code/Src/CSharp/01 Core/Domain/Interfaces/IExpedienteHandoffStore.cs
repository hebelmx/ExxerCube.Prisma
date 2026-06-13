namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Port that persists and loads a fused <see cref="ExxerCube.Prisma.Domain.Entities.Expediente"/> across the
/// Extractor → Reconciliator process boundary via shared storage (MVP-PATH 1.4 Reconciliator edge, ADR-011).
/// </summary>
/// <remarks>
/// <para>
/// The owner-chosen handoff contract is a <em>shared-storage reference</em>: the Extractor (Athena) serializes
/// the fused expediente at a storage-<em>relative</em> path and broadcasts only that path on an
/// <see cref="ExxerCube.Prisma.Domain.Events.ExtractionCompletedEvent"/>; the Reconciliator resolves the path
/// against its own configured storage base and loads the expediente. The raw document never crosses this edge.
/// </para>
/// <para>
/// <strong>Contract:</strong> the relative path is confined to the configured storage base — the
/// security-sensitive traversal/confinement guard is single-sourced through
/// <see cref="IStoragePathResolver"/> (the same guard the document edge uses), never re-implemented here. A
/// blank path, an unconfigured base, an escaping path, or a missing/corrupt artifact all fail closed as a
/// <see cref="Result{T}"/> failure rather than throwing, so the caller can log-and-continue.
/// </para>
/// </remarks>
public interface IExpedienteHandoffStore
{
    /// <summary>
    /// Serializes and stores a fused expediente at the given storage-relative path, creating any parent
    /// directories under the configured base.
    /// </summary>
    /// <param name="expediente">The fused expediente to persist.</param>
    /// <param name="relativeStoragePath">
    /// The storage-relative target path (for example <c>2026/06/12/{fileId}.fusion.json</c>; either path
    /// separator is accepted).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A success result wrapping the storage-relative path that was written (the value to stamp on the
    /// handoff event), or a failure when the expediente is null, the path is blank/escaping, the base is
    /// unconfigured, or the write fails.
    /// </returns>
    Task<Result<string>> SaveAsync(Expediente expediente, string relativeStoragePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a storage-relative path against the configured base, reads it, and deserializes the fused
    /// expediente.
    /// </summary>
    /// <param name="relativeStoragePath">The storage-relative path of a previously saved handoff artifact.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A success result wrapping the deserialized expediente, or a failure when the path is blank/escaping,
    /// the base is unconfigured, the artifact is missing, or it cannot be deserialized.
    /// </returns>
    Task<Result<Expediente>> LoadAsync(string relativeStoragePath, CancellationToken cancellationToken = default);
}
