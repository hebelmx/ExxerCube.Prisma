// <copyright file="InMemoryLogBuffer.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace ExxerCube.Prisma.QaHarness.Evidence;

/// <summary>
/// A thread-safe in-memory buffer that accumulates log lines for later flushing
/// by <see cref="LogEvidenceCollector"/>.
/// </summary>
/// <remarks>
/// Wire this up via a custom <c>ILoggerProvider</c> (see <c>InMemoryLogBufferProvider</c>)
/// so that any <see cref="Microsoft.Extensions.Logging.ILogger"/> instance in the harness
/// feeds into the same buffer without requiring a Serilog dependency.
/// </remarks>
public sealed class InMemoryLogBuffer
{
    private readonly ConcurrentQueue<string> _lines = new();

    /// <summary>
    /// Appends a formatted log line to the buffer.
    /// </summary>
    /// <param name="line">The formatted log line to append.</param>
    public void Append(string line) => _lines.Enqueue(line);

    /// <summary>
    /// Drains and returns all accumulated lines, clearing the buffer.
    /// </summary>
    /// <returns>A list of all buffered log lines in insertion order.</returns>
    public IReadOnlyList<string> Flush()
    {
        var result = new List<string>(_lines.Count);
        while (_lines.TryDequeue(out var line))
            result.Add(line);
        return result;
    }

    /// <summary>
    /// Returns a snapshot of all buffered lines without clearing the buffer.
    /// </summary>
    /// <returns>A read-only snapshot of buffered log lines.</returns>
    public IReadOnlyList<string> Peek() => _lines.ToArray();
}
