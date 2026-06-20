// <copyright file="IReportWriter.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Reporting;

/// <summary>
/// Serializes a <see cref="HarnessRunSummary"/> to a specific output format
/// (Markdown, HTML, or JSON) and writes the result to a file.
/// </summary>
/// <remarks>
/// Each format is a separate implementation registered with
/// <c>AddQaHarness().AddReportWriter&lt;TWriter&gt;()</c>.
/// Implementations must not throw for I/O failures — return a failure
/// <see cref="Result{T}"/> instead.  The returned string on success is the
/// absolute path of the written file.
/// </remarks>
public interface IReportWriter
{
    /// <summary>
    /// Gets the format identifier produced by this writer.
    /// Conventional values: <c>"markdown"</c>, <c>"html"</c>, <c>"json"</c>.
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Renders <paramref name="summary"/> and writes the output to <paramref name="outputPath"/>.
    /// </summary>
    /// <param name="summary">The complete harness run summary to render.</param>
    /// <param name="outputPath">
    /// Absolute path to the output file.  The file is created or overwritten.
    /// The directory must exist.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the write operation.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing the absolute path of the written file;
    /// a failure result when the file cannot be written.
    /// </returns>
    Task<Result<string>> WriteAsync(
        HarnessRunSummary summary,
        string outputPath,
        CancellationToken cancellationToken = default);
}
