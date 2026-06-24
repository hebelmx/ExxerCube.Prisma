using System.Text.Json;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Serialization;
using ExxerCube.Prisma.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Domain.Serialization;

/// <summary>
/// Pinpoints the 2026-06-24 max-fidelity gate stall: a <see cref="DocumentDownloadedEvent"/> carrying
/// companion <see cref="CaseFileReference"/> entries is silently dropped on the Athena SignalR ingestion
/// client, so the Extractor never runs. These pure System.Text.Json round-trips use the same options shape
/// the SignalR JSON protocol uses (Web defaults = camelCase + case-insensitive) plus the
/// <see cref="EnumModelJsonConverterFactory"/> the real client registers — no SignalR/host needed.
/// </summary>
public sealed class CaseFilesWireRoundTripTests
{
    private static JsonSerializerOptions WireOptions()
    {
        // Mirror SignalR's JsonHubProtocol payload options (Web defaults) + the SmartEnum converter.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new EnumModelJsonConverterFactory());
        return options;
    }

    [Fact]
    public void CaseFileReference_WithSmartEnumFormat_RoundTrips()
    {
        var options = WireOptions();
        var original = new CaseFileReference
        {
            RelativePath = "2026/06/24/case/expediente.xml",
            Format = FileFormat.Xml,
        };

        var json = JsonSerializer.Serialize(original, options);
        var back = JsonSerializer.Deserialize<CaseFileReference>(json, options);

        back.ShouldNotBeNull($"CaseFileReference must round-trip through the wire options. JSON was: {json}");
        back!.RelativePath.ShouldBe(original.RelativePath);
        back.Format.ShouldBe(FileFormat.Xml);
    }

    [Fact]
    public void DocumentDownloadedEvent_WithCompanionCaseFiles_RoundTrips()
    {
        var options = WireOptions();
        var original = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "expediente.pdf",
            Source = "SIARA",
            FileSizeBytes = 1234,
            Format = FileFormat.Pdf,
            CorrelationId = Guid.NewGuid(),
            CaseFiles = new List<CaseFileReference>
            {
                new() { RelativePath = "2026/06/24/case/expediente.pdf", Format = FileFormat.Pdf },
                new() { RelativePath = "2026/06/24/case/expediente.xml", Format = FileFormat.Xml },
                new() { RelativePath = "2026/06/24/case/expediente.docx", Format = FileFormat.Docx },
            },
            IsComplete = true,
        };

        var json = JsonSerializer.Serialize(original, options);
        var back = JsonSerializer.Deserialize<DocumentDownloadedEvent>(json, options);

        back.ShouldNotBeNull($"DocumentDownloadedEvent must round-trip. JSON was: {json}");
        back!.FileId.ShouldBe(original.FileId);
        back.CaseFiles.Count.ShouldBe(3, $"companion case files must survive the wire. JSON was: {json}");
        back.CaseFiles.Select(f => f.Format).ShouldBe(new[] { FileFormat.Pdf, FileFormat.Xml, FileFormat.Docx });
    }
}
