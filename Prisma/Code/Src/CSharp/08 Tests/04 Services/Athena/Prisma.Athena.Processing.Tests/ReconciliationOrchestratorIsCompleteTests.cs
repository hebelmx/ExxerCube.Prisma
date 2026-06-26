using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Tests that <see cref="ReconciliationOrchestrator"/> threads the <c>isComplete</c> flag through
/// to <see cref="IManualReviewerPanel.IdentifyReviewCasesAsync"/> (GH #6).
/// </summary>
/// <remarks>
/// These are pure unit tests: a real <see cref="IFileClassifier"/> substitute drives Stage 4 to
/// produce a valid <see cref="ClassificationResult"/>, and an NSubstitute
/// <see cref="IManualReviewerPanel"/> captures the call so we can assert the flag value.
/// The scope factory is built from a real <see cref="ServiceProvider"/> so the
/// <c>using var scope = factory.CreateScope()</c> path in production code is exercised exactly.
/// </remarks>
public sealed class ReconciliationOrchestratorIsCompleteTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="ReconciliationOrchestrator"/> wired to:
    /// <list type="bullet">
    ///   <item>A real <see cref="IFileClassifier"/> NSubstitute that returns a high-confidence result.</item>
    ///   <item>A real <see cref="IManualReviewerPanel"/> NSubstitute captured for later assertions.</item>
    ///   <item>A real <see cref="IServiceScopeFactory"/> backed by a <see cref="ServiceProvider"/> so
    ///         <c>scope.ServiceProvider.GetService&lt;IManualReviewerPanel&gt;()</c> returns the
    ///         NSubstitute instance.</item>
    /// </list>
    /// </summary>
    private static (ReconciliationOrchestrator orchestrator, IManualReviewerPanel panelSub)
        CreateSutWithReviewPanel()
    {
        // NSubstitute classifier that returns a valid, high-confidence result (Stage 4 succeeds)
        var classifier = Substitute.For<IFileClassifier>();
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(new ClassificationResult
            {
                Level1 = ExxerCube.Prisma.Domain.Enum.ClassificationLevel1.Aseguramiento,
                Confidence = Confidence.FromInt(90),
            }));

        // NSubstitute panel that captures IdentifyReviewCasesAsync calls
        var panelSub = Substitute.For<IManualReviewerPanel>();
        panelSub.IdentifyReviewCasesAsync(
                Arg.Any<string>(),
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<ClassificationResult>(),
                Arg.Any<bool>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(new List<ReviewCase>()));

        // Real ServiceProvider / ServiceScopeFactory so the production scope-factory path runs
        var services = new ServiceCollection();
        services.AddScoped<IManualReviewerPanel>(_ => panelSub);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var eventPublisher = Substitute.For<IEventPublisher>();

        var orchestrator = new ReconciliationOrchestrator(
            eventPublisher,
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: classifier,
            exporter: null, // Stage 5 not under test here
            reviewCaseScopeFactory: scopeFactory);

        return (orchestrator, panelSub);
    }

    /// <summary>A minimal fused result with a valid expediente so Stage 4 can run.</summary>
    private static FusionResult MinimalFusionResult() => new()
    {
        FusedExpediente = new Expediente
        {
            NumeroExpediente = "A/AS1-2505-001-TST",
            NumeroOficio = "214-1-00000001/2026",
        },
        Confidence = Confidence.FromFusion(0.9),
        ConflictingFields = new List<string>(),
    };

    // -------------------------------------------------------------------------
    // TC-1: isComplete=false is forwarded to the panel
    // -------------------------------------------------------------------------

    /// <summary>
    /// Given a <see cref="ReconciliationOrchestrator"/> wired to a real scope factory that
    /// provides an <see cref="IManualReviewerPanel"/> substitute,
    /// when <see cref="ReconciliationOrchestrator.ReconcileAsync"/> is called with
    /// <c>isComplete: false</c>,
    /// then the panel's <c>IdentifyReviewCasesAsync</c> is called exactly once with
    /// <c>isComplete == false</c>.
    /// This is the core threading test for GH #6.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_WithIsCompleteFalse_CallsPanelWithIsCompleteFalse()
    {
        // Arrange
        var (orchestrator, panelSub) = CreateSutWithReviewPanel();
        var fileId = Guid.NewGuid();

        // Act
        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: MinimalFusionResult(),
            fileId: fileId,
            correlationId: Guid.NewGuid(),
            isComplete: false,
            cancellationToken: Ct);

        // Assert — panel received exactly one call with isComplete=false
        // Use Arg.Is<T> for EVERY same-type argument to avoid NSubstitute AmbiguousArgumentsException.
        await panelSub.Received(1).IdentifyReviewCasesAsync(
            Arg.Is<string>(id => id == fileId.ToString()),
            Arg.Is<UnifiedMetadataRecord>(m => m != null),
            Arg.Is<ClassificationResult>(c => c != null),
            Arg.Is<bool>(b => b == false),          // <-- the flag under test
            Arg.Any<string?>(),                      // handoffPath (G-C2b)
            Arg.Is<CancellationToken>(ct => true));
    }

    // -------------------------------------------------------------------------
    // TC-2: isComplete=true is forwarded to the panel
    // -------------------------------------------------------------------------

    /// <summary>
    /// Symmetric counterpart to TC-1: when <c>isComplete: true</c> is passed,
    /// the panel's <c>IdentifyReviewCasesAsync</c> is called with <c>isComplete == true</c>.
    /// This verifies the flag is not hardcoded to false in the plumbing.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_WithIsCompleteTrue_CallsPanelWithIsCompleteTrue()
    {
        // Arrange
        var (orchestrator, panelSub) = CreateSutWithReviewPanel();
        var fileId = Guid.NewGuid();

        // Act
        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: MinimalFusionResult(),
            fileId: fileId,
            correlationId: Guid.NewGuid(),
            isComplete: true,
            cancellationToken: Ct);

        // Assert
        await panelSub.Received(1).IdentifyReviewCasesAsync(
            Arg.Is<string>(id => id == fileId.ToString()),
            Arg.Is<UnifiedMetadataRecord>(m => m != null),
            Arg.Is<ClassificationResult>(c => c != null),
            Arg.Is<bool>(b => b == true),           // <-- the flag under test
            Arg.Any<string?>(),                      // handoffPath (G-C2b)
            Arg.Is<CancellationToken>(ct => true));
    }

    // -------------------------------------------------------------------------
    // TC-3b: Null classifier → Stage 4 yields null → panel still called with
    //         non-null ClassificationResult (substituted new ClassificationResult())
    // -------------------------------------------------------------------------

    /// <summary>
    /// When <c>classifier</c> is <see langword="null"/> Stage 4 is skipped and
    /// <c>classificationResult</c> is <see langword="null"/> inside
    /// <c>PersistReviewCaseAsync</c>.  The orchestrator must substitute
    /// <c>new ClassificationResult()</c> so that the <c>IncompleteCase</c> guarantee is
    /// preserved even when no classifier is wired — the panel must receive a non-null
    /// classification argument and <c>isComplete == false</c>.
    /// This guards the <c>effectiveClassification = classificationResult ?? new ClassificationResult()</c>
    /// branch (GH #6).
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_WithNullClassifierAndIsCompleteFalse_CallsPanelWithNonNullClassification()
    {
        // Arrange — no classifier (Stage 4 is skipped → classificationResult == null)
        var panelSub = Substitute.For<IManualReviewerPanel>();
        panelSub.IdentifyReviewCasesAsync(
                Arg.Any<string>(),
                Arg.Any<UnifiedMetadataRecord>(),
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

        // classifier: null → Stage 4 produces null ClassificationResult
        var orchestrator = new ReconciliationOrchestrator(
            eventPublisher,
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: null,
            exporter: null,
            reviewCaseScopeFactory: scopeFactory);

        var fileId = Guid.NewGuid();

        // Act
        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: MinimalFusionResult(),
            fileId: fileId,
            correlationId: Guid.NewGuid(),
            isComplete: false,
            cancellationToken: Ct);

        // Assert — panel received exactly one call ...
        await panelSub.Received(1).IdentifyReviewCasesAsync(
            Arg.Is<string>(id => id == fileId.ToString()),           // correct fileId
            Arg.Is<UnifiedMetadataRecord>(m => m != null),           // metadata not null
            Arg.Is<ClassificationResult>(c => c != null),            // substituted non-null classification
            Arg.Is<bool>(b => b == false),                           // isComplete=false forwarded
            Arg.Any<string?>(),                                       // handoffPath (G-C2b)
            Arg.Is<CancellationToken>(ct => true));
    }

    // -------------------------------------------------------------------------
    // TC-3: No scope factory → panel never called (silent no-op)
    // -------------------------------------------------------------------------

    /// <summary>
    /// When <c>reviewCaseScopeFactory</c> is <see langword="null"/> (no DB wired),
    /// <c>IdentifyReviewCasesAsync</c> is never called — the orchestrator silently skips
    /// review-case persistence, consistent with the fail-open / no-DB pattern.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_WithNoScopeFactory_DoesNotCallPanel()
    {
        // Arrange — panel substitute NOT registered in any scope factory
        var classifier = Substitute.For<IFileClassifier>();
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(new ClassificationResult
            {
                Level1 = ExxerCube.Prisma.Domain.Enum.ClassificationLevel1.Aseguramiento,
                Confidence = Confidence.FromInt(90),
            }));

        var panelSub = Substitute.For<IManualReviewerPanel>();
        var eventPublisher = Substitute.For<IEventPublisher>();

        // reviewCaseScopeFactory = null → no review-case persistence
        var orchestrator = new ReconciliationOrchestrator(
            eventPublisher,
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: classifier,
            exporter: null,
            reviewCaseScopeFactory: null);

        // Act
        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: MinimalFusionResult(),
            fileId: Guid.NewGuid(),
            correlationId: null,
            isComplete: false,
            cancellationToken: Ct);

        // Assert — the substitute was never called because it was never in scope
        await panelSub.DidNotReceive().IdentifyReviewCasesAsync(
            Arg.Any<string>(),
            Arg.Any<UnifiedMetadataRecord>(),
            Arg.Any<ClassificationResult>(),
            Arg.Any<bool>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }
}
