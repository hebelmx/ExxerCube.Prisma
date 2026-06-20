// <copyright file="JsonReportWriter.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExxerCube.Prisma.QaHarness.Reporting;

/// <summary>
/// Serializes a <see cref="HarnessRunSummary"/> to a JSON file using
/// <see cref="System.Text.Json"/> for machine consumption by downstream QA agents or CI pipelines.
/// </summary>
/// <remarks>
/// The <see cref="ExxerCube.Prisma.QaHarness.Traceability.ITraceabilityMap"/> member of
/// <see cref="HarnessRunSummary"/> is serialized via a custom converter that calls
/// <see cref="ExxerCube.Prisma.QaHarness.Traceability.ITraceabilityMap.GetAllEntries"/> since
/// interfaces are not directly serializable by <c>System.Text.Json</c>.
/// Never throws for I/O or serialization failures — returns a failure result instead.
/// </remarks>
public sealed class JsonReportWriter : IReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
            new TraceabilityMapJsonConverter(),
        },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <inheritdoc/>
    public string Format => "json";

    /// <inheritdoc/>
    public async Task<Result<string>> WriteAsync(
        HarnessRunSummary summary,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Result<string>.WithFailure("WriteAsync was cancelled.");

        ArgumentNullException.ThrowIfNull(summary);
        if (string.IsNullOrWhiteSpace(outputPath))
            return Result<string>.WithFailure("outputPath must not be null or empty.");

        try
        {
            var dto = BuildDto(summary);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await using var stream = File.Create(outputPath);
            await JsonSerializer.SerializeAsync(stream, dto, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return Result<string>.WithSuccess(outputPath);
        }
        catch (OperationCanceledException)
        {
            return Result<string>.WithFailure("JSON report write was cancelled.");
        }
        catch (Exception ex)
        {
            return Result<string>.WithFailure($"Failed to write JSON report: {ex.Message}");
        }
    }

    // -----------------------------------------------------------------------
    // DTO projection (avoids interface serialization issues)
    // -----------------------------------------------------------------------

    private static HarnessRunSummaryDto BuildDto(HarnessRunSummary summary) =>
        new(
            summary.RunId,
            summary.ProductVersion,
            summary.StartedAt,
            summary.FinishedAt,
            summary.ProvisioningResult,
            summary.WorkflowResults,
            summary.ValidationResults,
            summary.Evidence,
            summary.TraceabilityMap.GetAllEntries());

    /// <summary>
    /// A JSON-serializable projection of <see cref="HarnessRunSummary"/> that replaces
    /// the <see cref="ExxerCube.Prisma.QaHarness.Traceability.ITraceabilityMap"/> interface
    /// reference with its concrete entry list.
    /// </summary>
    private sealed record HarnessRunSummaryDto(
        string RunId,
        string ProductVersion,
        DateTimeOffset StartedAt,
        DateTimeOffset FinishedAt,
        ExxerCube.Prisma.QaHarness.Provisioning.EnvironmentProvisioningResult ProvisioningResult,
        IReadOnlyList<ExxerCube.Prisma.QaHarness.Workflows.WorkflowResult> WorkflowResults,
        IReadOnlyList<ExxerCube.Prisma.QaHarness.Validators.ValidationResult> ValidationResults,
        ExxerCube.Prisma.QaHarness.Evidence.EvidencePackage Evidence,
        IReadOnlyList<ExxerCube.Prisma.QaHarness.Traceability.TraceabilityEntry> TraceabilityEntries);

    /// <summary>
    /// Custom converter that serializes <see cref="ExxerCube.Prisma.QaHarness.Traceability.ITraceabilityMap"/>
    /// as its flat entry list (interface serialization is not supported by System.Text.Json).
    /// </summary>
    private sealed class TraceabilityMapJsonConverter :
        JsonConverter<ExxerCube.Prisma.QaHarness.Traceability.ITraceabilityMap>
    {
        public override ExxerCube.Prisma.QaHarness.Traceability.ITraceabilityMap Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            throw new NotSupportedException("Deserialization of ITraceabilityMap is not supported.");

        public override void Write(
            Utf8JsonWriter writer,
            ExxerCube.Prisma.QaHarness.Traceability.ITraceabilityMap value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.GetAllEntries(), options);
        }
    }
}
