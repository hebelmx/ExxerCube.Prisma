using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// In the 3-process path the Reconciliator rebuilds the fusion result from the handoff: it knows how many
/// fields conflict, but not which or what each source said. Building the review's conflict alerts from that
/// produced none, so a case the export gate held for unresolved conflicts got no review case and sat stuck,
/// invisible to reviewers. The Extractor saves the real alerts; the Reconciliator now uses them.
/// </summary>
public sealed class ReconciliationOrchestratorStoredConflictAlertsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReconcileAsync_HandoffReportsConflicts_PassesTheExtractorsSavedAlertsToTheReviewPanel()
    {
        var fileId = Guid.NewGuid();
        var store = StoreHolding(fileId, SavedRecordWithAlert());
        var (orchestrator, panel) = CreateSut(store);

        await orchestrator.ReconcileAsync(null, HandoffWithConflicts(1), fileId, Guid.NewGuid(), cancellationToken: Ct);

        await panel.Received(1).IdentifyReviewCasesAsync(
            fileId.ToString(),
            Arg.Is<UnifiedMetadataRecord>(m =>
                m.FieldConflictAlerts.Count == 1
                && m.FieldConflictAlerts[0].FieldName == "NumeroOficio"
                && m.FieldConflictAlerts[0].ConflictingValues.Count == 2),
            Arg.Any<ClassificationResult>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcileAsync_HandoffReportsNoConflicts_DoesNotReadTheStore()
    {
        var fileId = Guid.NewGuid();
        var store = StoreHolding(fileId, SavedRecordWithAlert());
        var (orchestrator, panel) = CreateSut(store);

        await orchestrator.ReconcileAsync(null, HandoffWithConflicts(0), fileId, Guid.NewGuid(), cancellationToken: Ct);

        await store.DidNotReceive().GetByFileIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await panel.Received(1).IdentifyReviewCasesAsync(
            fileId.ToString(),
            Arg.Is<UnifiedMetadataRecord>(m => m.FieldConflictAlerts.Count == 0),
            Arg.Any<ClassificationResult>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcileAsync_StoreUnavailable_StillPersistsTheReviewCase()
    {
        var fileId = Guid.NewGuid();
        var store = Substitute.For<IUnifiedMetadataStore>();
        store.GetByFileIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<UnifiedMetadataRecord?>>>(_ => throw new InvalidOperationException("database unavailable"));
        var (orchestrator, panel) = CreateSut(store);

        await orchestrator.ReconcileAsync(null, HandoffWithConflicts(1), fileId, Guid.NewGuid(), cancellationToken: Ct);

        await panel.Received(1).IdentifyReviewCasesAsync(
            fileId.ToString(), Arg.Any<UnifiedMetadataRecord>(),
            Arg.Any<ClassificationResult>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    private static (ReconciliationOrchestrator, IManualReviewerPanel) CreateSut(IUnifiedMetadataStore store)
    {
        var classifier = Substitute.For<IFileClassifier>();
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(new ClassificationResult
            {
                Level1 = ClassificationLevel1.Aseguramiento,
                Confidence = Confidence.FromInt(90),
            }));

        var panel = Substitute.For<IManualReviewerPanel>();
        panel.IdentifyReviewCasesAsync(
                Arg.Any<string>(), Arg.Any<UnifiedMetadataRecord>(), Arg.Any<ClassificationResult>(),
                Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(new List<ReviewCase>()));

        var services = new ServiceCollection();
        services.AddScoped(_ => panel);
        services.AddScoped(_ => store);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var orchestrator = new ReconciliationOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: classifier,
            exporter: null,
            reviewCaseScopeFactory: scopeFactory);
        return (orchestrator, panel);
    }

    private static IUnifiedMetadataStore StoreHolding(Guid fileId, UnifiedMetadataRecord record)
    {
        var store = Substitute.For<IUnifiedMetadataStore>();
        store.GetByFileIdAsync(fileId.ToString(), Arg.Any<CancellationToken>())
            .Returns(Result<UnifiedMetadataRecord?>.Success(record));
        return store;
    }

    /// <summary>What ReconciliationPipelineService rebuilds from the handoff: a count of conflicts, no field detail.</summary>
    private static FusionResult HandoffWithConflicts(int conflicts) => new()
    {
        FusedExpediente = new Expediente { NumeroExpediente = "A/AS1-4444-5555555-HHHH" },
        NextAction = conflicts > 0 ? NextAction.ManualReviewRequired : NextAction.AutoProcess,
        ConflictingFields = Enumerable.Range(1, conflicts).Select(i => $"Field{i}").ToList(),
    };

    private static UnifiedMetadataRecord SavedRecordWithAlert() => new()
    {
        FieldConflictAlerts = new List<FieldConflictAlert>
        {
            new("NumeroOficio",
                new List<ConflictingSourceValue>
                {
                    new(SourceType.XML_HandFilled, "214-1-55555555/2025"),
                    new(SourceType.PDF_OCR_CNBV, "214-1-55555558/2025"),
                },
                0.55f),
        },
    };
}
