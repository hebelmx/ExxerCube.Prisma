using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// The Reconciliator starts each case's SLA clock from the fused expediente (intake date + DiasPlazo business
/// days). Before this, <c>SLATrackingService.TrackSLAAsync</c> had no caller, so the SLA dashboard and the review
/// page's SLA timeline were empty for every ingested case.
/// </summary>
/// <remarks>
/// The scope factory is a real <see cref="ServiceProvider"/> holding an <see cref="ISLAEnforcer"/> substitute, so
/// the production path (scope → <c>SLATrackingService</c> → enforcer) runs unchanged.
/// </remarks>
public sealed class ReconciliationOrchestratorSlaTrackingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReconcileAsync_ExpedienteWithReceptionDate_TracksSlaFromReceptionDateAndDiasPlazo()
    {
        var enforcer = SuccessfulEnforcer();
        var orchestrator = CreateSut(enforcer);
        var fileId = Guid.NewGuid();
        var received = new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);

        await orchestrator.ReconcileAsync(null, Fused(new Expediente
        {
            NumeroExpediente = "A/AS1-2505-001-TST",
            DiasPlazo = 5,
            FechaRecepcion = received,
            FechaPublicacion = received.AddDays(-2),
        }), fileId, Guid.NewGuid(), cancellationToken: Ct);

        await enforcer.Received(1).CalculateSLAStatusAsync(fileId.ToString(), received, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcileAsync_ExpedienteWithOnlyPublicationDate_TracksSlaFromPublicationDate()
    {
        // The SIARA corpus XML carries Cnbv_FechaPublicacion and Cnbv_DiasPlazo, but no reception date.
        var enforcer = SuccessfulEnforcer();
        var orchestrator = CreateSut(enforcer);
        var fileId = Guid.NewGuid();
        var published = new DateTime(2025, 6, 4, 0, 0, 0, DateTimeKind.Utc);

        await orchestrator.ReconcileAsync(null, Fused(new Expediente
        {
            NumeroExpediente = "A/AS1-4444-5555555-HHHH",
            DiasPlazo = 3,
            FechaPublicacion = published,
        }), fileId, Guid.NewGuid(), cancellationToken: Ct);

        await enforcer.Received(1).CalculateSLAStatusAsync(fileId.ToString(), published, 3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcileAsync_ExpedienteWithoutDates_TracksSlaFromNow()
    {
        var enforcer = SuccessfulEnforcer();
        var orchestrator = CreateSut(enforcer);
        var fileId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        await orchestrator.ReconcileAsync(null, Fused(new Expediente { NumeroExpediente = "X", DiasPlazo = 10 }), fileId, Guid.NewGuid(), cancellationToken: Ct);

        var after = DateTime.UtcNow;
        await enforcer.Received(1).CalculateSLAStatusAsync(
            fileId.ToString(),
            Arg.Is<DateTime>(d => d >= before && d <= after),
            10,
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ReconcileAsync_ExpedienteWithoutPositiveDiasPlazo_DoesNotGuessAnSla(int diasPlazo)
    {
        var enforcer = SuccessfulEnforcer();
        var orchestrator = CreateSut(enforcer);

        await orchestrator.ReconcileAsync(null, Fused(new Expediente { NumeroExpediente = "X", DiasPlazo = diasPlazo }), Guid.NewGuid(), Guid.NewGuid(), cancellationToken: Ct);

        await enforcer.DidNotReceive().CalculateSLAStatusAsync(
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcileAsync_NoFusedExpediente_DoesNotTrackSla()
    {
        var enforcer = SuccessfulEnforcer();
        var orchestrator = CreateSut(enforcer);

        await orchestrator.ReconcileAsync(null, null, Guid.NewGuid(), Guid.NewGuid(), cancellationToken: Ct);

        await enforcer.DidNotReceive().CalculateSLAStatusAsync(
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcileAsync_SlaEnforcerFails_ReconciliationStillCompletesClassification()
    {
        var enforcer = Substitute.For<ISLAEnforcer>();
        enforcer.CalculateSLAStatusAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<SLAStatus>.WithFailure("database unavailable"));
        var orchestrator = CreateSut(enforcer);

        var stages = await orchestrator.ReconcileAsync(null, Fused(new Expediente { NumeroExpediente = "X", DiasPlazo = 5 }), Guid.NewGuid(), Guid.NewGuid(), cancellationToken: Ct);

        stages.ShouldBe(1); // Stage 4 ran; Stage 5 has no exporter in this setup.
    }

    [Fact]
    public async Task ReconcileAsync_SlaEnforcerThrows_ReconciliationStillCompletesClassification()
    {
        var enforcer = Substitute.For<ISLAEnforcer>();
        enforcer.CalculateSLAStatusAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<SLAStatus>>>(_ => throw new InvalidOperationException("boom"));
        var orchestrator = CreateSut(enforcer);

        var stages = await orchestrator.ReconcileAsync(null, Fused(new Expediente { NumeroExpediente = "X", DiasPlazo = 5 }), Guid.NewGuid(), Guid.NewGuid(), cancellationToken: Ct);

        stages.ShouldBe(1);
    }

    [Fact]
    public async Task ReconcileAsync_NoSlaEnforcerRegistered_ReconciliationStillCompletesClassification()
    {
        var orchestrator = CreateSut(enforcer: null);

        var stages = await orchestrator.ReconcileAsync(null, Fused(new Expediente { NumeroExpediente = "X", DiasPlazo = 5 }), Guid.NewGuid(), Guid.NewGuid(), cancellationToken: Ct);

        stages.ShouldBe(1);
    }

    private static ISLAEnforcer SuccessfulEnforcer()
    {
        var enforcer = Substitute.For<ISLAEnforcer>();
        enforcer.CalculateSLAStatusAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Result<SLAStatus>.Success(new SLAStatus { FileId = call.ArgAt<string>(0) }));
        return enforcer;
    }

    private static ReconciliationOrchestrator CreateSut(ISLAEnforcer? enforcer)
    {
        var classifier = Substitute.For<IFileClassifier>();
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(new ClassificationResult
            {
                Level1 = ExxerCube.Prisma.Domain.Enum.ClassificationLevel1.Aseguramiento,
                Confidence = Confidence.FromInt(90),
            }));

        var services = new ServiceCollection().AddLogging();
        if (enforcer is not null)
        {
            services.AddScoped(_ => enforcer);
        }

        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new ReconciliationOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: classifier,
            exporter: null,
            reviewCaseScopeFactory: scopeFactory);
    }

    private static FusionResult Fused(Expediente expediente) => new()
    {
        FusedExpediente = expediente,
        Confidence = Confidence.FromFusion(0.9),
        ConflictingFields = new List<string>(),
    };
}
