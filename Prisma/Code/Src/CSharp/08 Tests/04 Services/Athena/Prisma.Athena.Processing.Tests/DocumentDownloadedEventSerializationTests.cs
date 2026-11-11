using System.Collections.Generic;
using System.Text.Json;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Domain.Serialization;
using Shouldly;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Pins the System.Text.Json round-trip of <see cref="DocumentDownloadedEvent"/> across the SignalR
/// ingestion wire (Orion Downloader → Athena Extractor, MVP-PATH 1.3).
/// </summary>
/// <remarks>
/// The max-fidelity gate (#5) surfaced that the companion <see cref="DocumentDownloadedEvent.CaseFiles"/>
/// arrived with every <see cref="CaseFileReference.Format"/> collapsed to <c>Pdf</c> at the Extractor —
/// because <see cref="FileFormat"/> is a SmartEnum (<see cref="EnumModel"/>) that System.Text.Json
/// serializes as an object and cannot reconstruct (its <c>Value</c> is <c>init</c>-only). The Xml/Docx
/// companions were therefore invisible to Stage-3 fusion, degrading the live run to single-source PDF
/// fusion with an empty expediente number, so SIRO export refused. The fix registers
/// <see cref="EnumModelJsonConverterFactory"/> on the SignalR JSON protocol at both ends of each edge.
/// These tests reproduce the wire round-trip in milliseconds so the fix is verified without a live run:
/// <see cref="RoundTrip_WithoutConverter_LosesSmartEnumFormat"/> pins the broken behaviour, and the two
/// converter tests pin the fix.
/// </remarks>
public sealed class DocumentDownloadedEventSerializationTests
{
    private static DocumentDownloadedEvent CreateThreeFileCaseEvent() => new()
    {
        FileId = Guid.NewGuid(),
        FileName = "case.pdf",
        Source = "SIARA",
        Format = FileFormat.Pdf,
        Path = "2026/06/14/CASE-1/case.pdf",
        ClearanceToken = "token",
        CorrelationId = Guid.NewGuid(),
        CaseFiles = new List<CaseFileReference>
        {
            new() { RelativePath = "2026/06/14/CASE-1/case.pdf",  Format = FileFormat.Pdf },
            new() { RelativePath = "2026/06/14/CASE-1/case.xml",  Format = FileFormat.Xml },
            new() { RelativePath = "2026/06/14/CASE-1/case.docx", Format = FileFormat.Docx },
        },
        IsComplete = true,
    };

    private static JsonSerializerOptions WithConverter(JsonSerializerOptions options)
    {
        options.Converters.Add(new EnumModelJsonConverterFactory());
        return options;
    }

    [Fact]
    public void RoundTrip_WithoutConverter_LosesSmartEnumFormat()
    {
        // Documents the defect: plain STJ keeps the 3 CaseFiles entries but cannot reconstruct the
        // SmartEnum, so every Format collapses to the zero-value singleton (Pdf) — the Xml/Docx
        // companions become invisible to fusion. This is exactly what the gate observed on the wire.
        var original = CreateThreeFileCaseEvent();

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<DocumentDownloadedEvent>(json);

        restored.ShouldNotBeNull();
        restored!.CaseFiles.Count.ShouldBe(3);
        restored.CaseFiles.ShouldNotContain(f => f.Format == FileFormat.Xml);
        restored.CaseFiles.ShouldNotContain(f => f.Format == FileFormat.Docx);
    }

    [Fact]
    public void RoundTrip_WithConverter_DefaultOptions_PreservesCaseFileFormats()
    {
        var options = WithConverter(new JsonSerializerOptions());
        var original = CreateThreeFileCaseEvent();

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<DocumentDownloadedEvent>(json, options);

        restored.ShouldNotBeNull();
        restored!.CaseFiles.Count.ShouldBe(3);
        restored.CaseFiles.ShouldContain(f => f.Format == FileFormat.Xml);
        restored.CaseFiles.ShouldContain(f => f.Format == FileFormat.Docx);
    }

    [Fact]
    public void RoundTrip_WithConverter_WebDefaults_PreservesEverything()
    {
        // SignalR's JsonHubProtocol uses Web defaults (camelCase + case-insensitive).
        var options = WithConverter(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var original = CreateThreeFileCaseEvent();

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<DocumentDownloadedEvent>(json, options);

        restored.ShouldNotBeNull();
        restored!.FileId.ShouldBe(original.FileId);
        restored.Path.ShouldBe(original.Path);
        restored.ClearanceToken.ShouldBe(original.ClearanceToken);
        restored.Format.ShouldBe(FileFormat.Pdf);
        restored.IsComplete.ShouldBeTrue();
        restored.CaseFiles.Count.ShouldBe(3);
        var xml = restored.CaseFiles.FirstOrDefault(f => f.Format == FileFormat.Xml);
        xml.ShouldNotBeNull();
        xml!.RelativePath.ShouldBe("2026/06/14/CASE-1/case.xml");
    }
}
