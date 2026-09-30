// <copyright file="ITraceabilityMap.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Traceability;

/// <summary>
/// An in-memory map that accumulates <see cref="TraceabilityEntry"/> records during a
/// QA harness run and supports look-up by requirement, feature, or invariant reference ID.
/// </summary>
/// <remarks>
/// Implementations must be thread-safe because multiple workflow runners may populate
/// the map concurrently.  The default implementation uses a
/// <see cref="System.Collections.Concurrent.ConcurrentBag{T}"/> internally.
/// </remarks>
public interface ITraceabilityMap
{
    /// <summary>
    /// Adds a <see cref="TraceabilityEntry"/> to the map.
    /// </summary>
    /// <param name="entry">The entry to add.  Must not be <see langword="null"/>.</param>
    void AddEntry(TraceabilityEntry entry);

    /// <summary>
    /// Returns all entries that reference <paramref name="refId"/> in any of their
    /// <see cref="TraceabilityEntry.Requirements"/>, <see cref="TraceabilityEntry.Features"/>,
    /// or <see cref="TraceabilityEntry.Invariants"/> collections.
    /// </summary>
    /// <param name="refId">
    /// A requirement, feature, or invariant identifier (e.g. <c>"REQ-A1-01"</c>).
    /// </param>
    /// <returns>A read-only list of matching entries, or an empty list when none match.</returns>
    IReadOnlyList<TraceabilityEntry> GetEntriesFor(string refId);

    /// <summary>
    /// Returns all entries that have been added to the map, in insertion order.
    /// </summary>
    /// <returns>A read-only snapshot of all entries.</returns>
    IReadOnlyList<TraceabilityEntry> GetAllEntries();

    /// <summary>
    /// Serializes the entire map to a JSON file at <paramref name="outputPath"/>.
    /// </summary>
    /// <param name="outputPath">Absolute path to the output JSON file.  The file is created or overwritten.</param>
    /// <param name="cancellationToken">Token used to cancel the write operation.</param>
    /// <returns>A successful <see cref="Result"/> when the file is written; a failure result when the path is inaccessible or serialization fails.</returns>
    Task<Result> SerializeToJsonAsync(
        string outputPath,
        CancellationToken cancellationToken = default);
}
