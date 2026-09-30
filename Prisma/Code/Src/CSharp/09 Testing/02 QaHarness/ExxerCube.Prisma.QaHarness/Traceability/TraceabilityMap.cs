// <copyright file="TraceabilityMap.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace ExxerCube.Prisma.QaHarness.Traceability;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ITraceabilityMap"/> backed by a
/// <see cref="ConcurrentBag{T}"/>.
/// </summary>
/// <remarks>
/// Entries are accumulated during a QA harness run by workflow runners and domain validators.
/// The map can be serialized to JSON via <see cref="SerializeToJsonAsync"/> for archival or
/// machine consumption by a downstream QA agent.
/// </remarks>
public sealed class TraceabilityMap : ITraceabilityMap
{
    // ConcurrentBag provides thread-safe add; ordering is not guaranteed but is acceptable
    // for accumulation. GetAllEntries() returns a stable snapshot at the time of the call.
    private readonly ConcurrentBag<TraceabilityEntry> _entries = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <inheritdoc/>
    public void AddEntry(TraceabilityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
    }

    /// <inheritdoc/>
    public IReadOnlyList<TraceabilityEntry> GetEntriesFor(string refId)
    {
        ArgumentNullException.ThrowIfNull(refId);

        return _entries
            .Where(e =>
                e.Requirements.Any(r => string.Equals(r.Id, refId, StringComparison.OrdinalIgnoreCase)) ||
                e.Features.Any(f => string.Equals(f.Id, refId, StringComparison.OrdinalIgnoreCase)) ||
                e.Invariants.Any(i => string.Equals(i.Id, refId, StringComparison.OrdinalIgnoreCase)))
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc/>
    public IReadOnlyList<TraceabilityEntry> GetAllEntries() =>
        _entries.ToList().AsReadOnly();

    /// <inheritdoc/>
    public async Task<Result> SerializeToJsonAsync(
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Result.WithFailure("SerializeToJsonAsync was cancelled.");

        if (string.IsNullOrWhiteSpace(outputPath))
            return Result.WithFailure("outputPath must not be null or empty.");

        try
        {
            var snapshot = GetAllEntries();

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await using var stream = File.Create(outputPath);
            await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return Result.WithFailure("Traceability map serialization was cancelled.");
        }
        catch (Exception ex)
        {
            return Result.WithFailure($"Failed to serialize traceability map: {ex.Message}");
        }
    }
}
