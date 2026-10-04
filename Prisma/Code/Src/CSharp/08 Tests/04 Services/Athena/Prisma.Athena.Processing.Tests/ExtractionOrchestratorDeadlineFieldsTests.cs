using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.Sources;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// The XML companion's deadline fields (<c>Cnbv_DiasPlazo</c>, <c>Cnbv_FechaPublicacion</c>) must reach the typed
/// <see cref="Expediente.DiasPlazo"/> / <see cref="Expediente.FechaPublicacion"/> that fusion fuses and the SLA
/// reads. The XML extractor surfaces them as strings in <c>AdditionalFields</c>; before this fix they stayed
/// there, so every ingested case reached the Reconciliator with <c>DiasPlazo = 0</c> and got no SLA.
/// </summary>
public sealed class ExtractionOrchestratorDeadlineFieldsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("30/09/2026", 2026, 9, 30)] // generated corpus
    [InlineData("2025-06-04", 2025, 6, 4)]  // legacy oficio set
    public async Task ExtractAsync_XmlCarriesDeadlineFields_PassesThemTypedToFusion(string fechaPublicacion, int y, int m, int d)
    {
        var (orchestrator, fusion) = CreateSut(new Dictionary<string, string?>
        {
            ["DiasPlazo"] = "5",
            ["FechaPublicacion"] = fechaPublicacion,
        });

        await orchestrator.ExtractAsync(XmlCase(), Ct);

        await fusion.Received(1).FuseAsync(
            Arg.Is<Expediente?>(x => x != null && x.DiasPlazo == 5 && x.FechaPublicacion == new DateTime(y, m, d)),
            Arg.Any<Expediente?>(), Arg.Any<Expediente?>(),
            Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("cinco", "ayer")]
    [InlineData("-3", "31/02/2026")]
    public async Task ExtractAsync_XmlDeadlineFieldsMissingOrUnreadable_LeavesThemUnset(string? diasPlazo, string? fechaPublicacion)
    {
        var (orchestrator, fusion) = CreateSut(new Dictionary<string, string?>
        {
            ["DiasPlazo"] = diasPlazo,
            ["FechaPublicacion"] = fechaPublicacion,
        });

        await orchestrator.ExtractAsync(XmlCase(), Ct);

        await fusion.Received(1).FuseAsync(
            Arg.Is<Expediente?>(x => x != null && x.DiasPlazo == 0 && x.FechaPublicacion == default),
            Arg.Any<Expediente?>(), Arg.Any<Expediente?>(),
            Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
            Arg.Any<CancellationToken>());
    }

    private static (ExtractionOrchestrator, IFusionExpediente) CreateSut(Dictionary<string, string?> additional)
    {
        var xmlExtractor = Substitute.For<IFieldExtractor<XmlSource>>();
        xmlExtractor.ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(new ExtractedFields
            {
                Expediente = "A/AS1-2505-001-TST",
                AdditionalFields = additional,
            }));

        var fusion = Substitute.For<IFusionExpediente>();
        fusion.FuseAsync(
                Arg.Any<Expediente?>(), Arg.Any<Expediente?>(), Arg.Any<Expediente?>(),
                Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<FusionResult>.WithFailure("not under test"));

        var orchestrator = new ExtractionOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ExtractionOrchestrator>.Instance,
            fusionService: fusion,
            xmlFieldExtractor: xmlExtractor);
        return (orchestrator, fusion);
    }

    private static DocumentDownloadedEvent XmlCase() => new()
    {
        FileId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        FileName = "case.xml",
        Source = "SIARA",
        Format = FileFormat.Xml,
        CaseFiles = new List<CaseFileReference> { new() { RelativePath = "/data/case.xml", Format = FileFormat.Xml } },
    };
}
