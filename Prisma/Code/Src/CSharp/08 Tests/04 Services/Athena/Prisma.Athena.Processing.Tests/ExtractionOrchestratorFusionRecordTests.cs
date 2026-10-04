using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// After fusion the Extractor saves the per-field fusion detail under the case FileId, because only the fused
/// expediente crosses into the Reconciliator and the review page needs the rest. Saving is fail-open.
/// </summary>
/// <remarks>
/// The case is XML-primary, so Stages 1–2 are skipped and only the fusion substitute and the store are involved.
/// The scope factory is a real <see cref="ServiceProvider"/>, so the production scope path runs unchanged.
/// </remarks>
public sealed class ExtractionOrchestratorFusionRecordTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExtractAsync_FusionSucceeds_SavesTheFusionRecordUnderTheCaseFileId()
    {
        var store = AcceptingStore();
        var orchestrator = CreateSut(FusionReturning(ConflictedFusion()), store);
        var downloadEvent = XmlPrimaryCase();

        var extraction = await orchestrator.ExtractAsync(downloadEvent, Ct);

        extraction.FusionResult.ShouldNotBeNull();
        await store.Received(1).SaveAsync(
            downloadEvent.FileId.ToString(),
            Arg.Is<UnifiedMetadataRecord>(r =>
                r.Expediente != null
                && r.Expediente.NumeroOficio == "214-1-55555555/2025"
                && r.MatchedFields != null
                && r.MatchedFields.FieldMatches["NumeroOficio"].HasConflict
                && r.MatchedFields.FieldMatches["NumeroOficio"].AllValues.Count == 2
                && r.FieldConflictAlerts.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtractAsync_FusionFails_SavesNothing()
    {
        var store = AcceptingStore();
        var fusion = Substitute.For<IFusionExpediente>();
        fusion.FuseAsync(
                Arg.Any<Expediente?>(), Arg.Any<Expediente?>(), Arg.Any<Expediente?>(),
                Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<FusionResult>.WithFailure("no sources"));
        var orchestrator = CreateSut(fusion, store);

        await orchestrator.ExtractAsync(XmlPrimaryCase(), Ct);

        await store.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<UnifiedMetadataRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtractAsync_StoreRejectsTheRecord_StillReturnsTheFusionResult()
    {
        var store = Substitute.For<IUnifiedMetadataStore>();
        store.SaveAsync(Arg.Any<string>(), Arg.Any<UnifiedMetadataRecord>(), Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("database unavailable"));
        var orchestrator = CreateSut(FusionReturning(ConflictedFusion()), store);

        var extraction = await orchestrator.ExtractAsync(XmlPrimaryCase(), Ct);

        extraction.FusionResult.ShouldNotBeNull();
        extraction.StagesCompleted.ShouldBe(1);
    }

    [Fact]
    public async Task ExtractAsync_StoreThrows_StillReturnsTheFusionResult()
    {
        var store = Substitute.For<IUnifiedMetadataStore>();
        store.SaveAsync(Arg.Any<string>(), Arg.Any<UnifiedMetadataRecord>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result>>(_ => throw new InvalidOperationException("boom"));
        var orchestrator = CreateSut(FusionReturning(ConflictedFusion()), store);

        var extraction = await orchestrator.ExtractAsync(XmlPrimaryCase(), Ct);

        extraction.FusionResult.ShouldNotBeNull();
        extraction.StagesCompleted.ShouldBe(1);
    }

    private static ExtractionOrchestrator CreateSut(IFusionExpediente fusion, IUnifiedMetadataStore store)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => store);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new ExtractionOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ExtractionOrchestrator>.Instance,
            fusionService: fusion,
            metadataScopeFactory: scopeFactory);
    }

    private static IUnifiedMetadataStore AcceptingStore()
    {
        var store = Substitute.For<IUnifiedMetadataStore>();
        store.SaveAsync(Arg.Any<string>(), Arg.Any<UnifiedMetadataRecord>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        return store;
    }

    private static IFusionExpediente FusionReturning(FusionResult result)
    {
        var fusion = Substitute.For<IFusionExpediente>();
        fusion.FuseAsync(
                Arg.Any<Expediente?>(), Arg.Any<Expediente?>(), Arg.Any<Expediente?>(),
                Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<FusionResult>.Success(result));
        return fusion;
    }

    private static DocumentDownloadedEvent XmlPrimaryCase() => new()
    {
        FileId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        FileName = "333ccc-6666666662025.xml",
        Source = "SIARA",
        Format = FileFormat.Xml,
        CaseFiles = new List<CaseFileReference>(),
    };

    private static FusionResult ConflictedFusion() => new()
    {
        FusedExpediente = new Expediente { NumeroExpediente = "A/AS1-4444-5555555-HHHH", NumeroOficio = "214-1-55555555/2025" },
        Confidence = Confidence.FromFusion(0.7),
        ConflictingFields = new List<string> { "NumeroOficio" },
        FieldResults = new Dictionary<string, FieldFusionResult>
        {
            ["NumeroOficio"] = new()
            {
                Value = "214-1-55555555/2025",
                Confidence = 0.55,
                Decision = FusionDecision.Conflict,
                ContributingSources = new List<SourceType> { SourceType.XML_HandFilled, SourceType.PDF_OCR_CNBV },
                ConflictingValues = new List<(SourceType Source, string? Value)>
                {
                    (SourceType.XML_HandFilled, "214-1-55555555/2025"),
                    (SourceType.PDF_OCR_CNBV, "214-1-55555558/2025"),
                },
            },
        },
    };
}
