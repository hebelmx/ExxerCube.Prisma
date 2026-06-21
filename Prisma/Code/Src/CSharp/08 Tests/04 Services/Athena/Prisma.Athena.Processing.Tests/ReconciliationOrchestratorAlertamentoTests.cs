using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Tests that <see cref="ReconciliationOrchestrator"/> populates
/// <see cref="UnifiedMetadataRecord.FieldConflictAlerts"/> from the fusion result
/// and passes them to <see cref="IManualReviewerPanel.IdentifyReviewCasesAsync"/> (Item C, #9).
/// </summary>
public sealed class ReconciliationOrchestratorAlertamentoTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Captures the <see cref="UnifiedMetadataRecord"/> argument received by the panel.
    /// </summary>
    private static (ReconciliationOrchestrator orchestrator, List<UnifiedMetadataRecord> capturedMetadata)
        CreateSutCapturingMetadata()
    {
        var classifier = Substitute.For<IFileClassifier>();
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(new ClassificationResult
            {
                Level1 = ClassificationLevel1.Aseguramiento,
                Confidence = 90,
            }));

        var capturedMetadata = new List<UnifiedMetadataRecord>();

        var panelSub = Substitute.For<IManualReviewerPanel>();
        panelSub.IdentifyReviewCasesAsync(
                Arg.Any<string>(),
                Arg.Do<UnifiedMetadataRecord>(m => capturedMetadata.Add(m)),
                Arg.Any<ClassificationResult>(),
                Arg.Any<bool>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(new List<ReviewCase>()));

        var services = new ServiceCollection();
        services.AddScoped<IManualReviewerPanel>(_ => panelSub);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var eventPublisher = Substitute.For<IEventPublisher>();

        var orchestrator = new ReconciliationOrchestrator(
            eventPublisher,
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: classifier,
            exporter: null,
            reviewCaseScopeFactory: scopeFactory);

        return (orchestrator, capturedMetadata);
    }

    /// <summary>
    /// Returns a <see cref="FusionResult"/> with a single Conflict field so that
    /// <c>FieldConflictAlertBuilder.From</c> produces a non-empty alert list.
    /// </summary>
    private static FusionResult FusionWithConflict()
    {
        var result = new FusionResult
        {
            FusedExpediente = new Expediente
            {
                NumeroExpediente = "A/AS1-2505-001-TST",
                NumeroOficio = "214-1-00000001/2026",
            },
            OverallConfidence = 0.5,
        };

        var conflictField = new FieldFusionResult
        {
            Decision = FusionDecision.Conflict,
            Confidence = 0.0,
        };
        conflictField.ConflictingValues.Add((SourceType.XML_HandFilled, "RFC-A"));
        conflictField.ConflictingValues.Add((SourceType.PDF_OCR_CNBV, "RFC-B"));
        result.FieldResults["RFC"] = conflictField;

        return result;
    }

    /// <summary>
    /// Returns a <see cref="FusionResult"/> where all fields agree (no conflicts).
    /// </summary>
    private static FusionResult FusionAllAgree()
    {
        var result = new FusionResult
        {
            FusedExpediente = new Expediente
            {
                NumeroExpediente = "A/AS1-2505-002-TST",
                NumeroOficio = "214-1-00000002/2026",
            },
            OverallConfidence = 0.97,
        };

        var agreeField = new FieldFusionResult
        {
            Decision = FusionDecision.AllAgree,
            Confidence = 0.97,
        };
        result.FieldResults["NumeroExpediente"] = agreeField;

        return result;
    }

    // -----------------------------------------------------------------------
    // TC-OA-1: Conflicting fusion result → metadata has non-empty FieldConflictAlerts
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the fusion result contains a Conflict field,
    /// <c>PersistReviewCaseAsync</c> must pass a <see cref="UnifiedMetadataRecord"/> whose
    /// <see cref="UnifiedMetadataRecord.FieldConflictAlerts"/> is non-empty to the panel.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_ConflictingFusion_PassesNonEmptyFieldConflictAlerts()
    {
        var (orchestrator, capturedMetadata) = CreateSutCapturingMetadata();

        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: FusionWithConflict(),
            fileId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            isComplete: true,
            cancellationToken: Ct);

        capturedMetadata.Count.ShouldBe(1, "panel must be called exactly once");

        var metadata = capturedMetadata[0];
        metadata.FieldConflictAlerts.ShouldNotBeNull();
        metadata.FieldConflictAlerts.Count.ShouldBeGreaterThan(0,
            "a conflicting fusion result must produce at least one FieldConflictAlert in the metadata");

        var rfcAlert = metadata.FieldConflictAlerts.Single(a => a.FieldName == "RFC");
        rfcAlert.ConflictingValues.Count.ShouldBe(2,
            "both conflicting source values must be present in the alert");
    }

    // -----------------------------------------------------------------------
    // TC-OA-2: All-agree fusion result → metadata has empty FieldConflictAlerts
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the fusion result has no conflicting fields,
    /// <c>PersistReviewCaseAsync</c> must pass a <see cref="UnifiedMetadataRecord"/> whose
    /// <see cref="UnifiedMetadataRecord.FieldConflictAlerts"/> is empty.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_AllAgreeFusion_PassesEmptyFieldConflictAlerts()
    {
        var (orchestrator, capturedMetadata) = CreateSutCapturingMetadata();

        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: FusionAllAgree(),
            fileId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            isComplete: true,
            cancellationToken: Ct);

        capturedMetadata.Count.ShouldBe(1, "panel must be called exactly once");

        var metadata = capturedMetadata[0];
        metadata.FieldConflictAlerts.ShouldNotBeNull();
        metadata.FieldConflictAlerts.Count.ShouldBe(0,
            "an all-agree fusion result must produce zero FieldConflictAlerts in the metadata");
    }

    // -----------------------------------------------------------------------
    // TC-OA-3: Null fusion result → metadata has empty FieldConflictAlerts (fail-open)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When fusionResult is null (e.g., extraction stage failed),
    /// <c>PersistReviewCaseAsync</c> must not throw and the metadata's alerts list must be empty.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_NullFusion_PassesEmptyFieldConflictAlerts()
    {
        var (orchestrator, capturedMetadata) = CreateSutCapturingMetadata();

        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: null,
            fileId: Guid.NewGuid(),
            correlationId: null,
            isComplete: true,
            cancellationToken: Ct);

        capturedMetadata.Count.ShouldBe(1, "panel must be called even when fusionResult is null");

        var metadata = capturedMetadata[0];
        metadata.FieldConflictAlerts.ShouldNotBeNull();
        metadata.FieldConflictAlerts.Count.ShouldBe(0,
            "null fusion result must produce zero FieldConflictAlerts");
    }
}
