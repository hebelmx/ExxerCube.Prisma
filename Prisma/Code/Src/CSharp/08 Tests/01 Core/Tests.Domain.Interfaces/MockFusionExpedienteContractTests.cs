namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Blueprint instance of <see cref="FusionExpedienteContract"/> — the mock-backed inheritor
/// that encodes the design specification (ADR-005 §6).
/// </summary>
/// <remarks>
/// Created in Phase 4 of the ITDD refactor when the conflated
/// <c>FusionExpedienteServiceContractTests</c> was split into a behavioural base + a real-impl
/// deriving class. There was no pre-existing mock blueprint (the class was always real-SUT), so this
/// blueprint is backed by <see cref="FusionExpedienteMockFactory"/>'s reference fusion engine and
/// uses the contract's minimal-plausible default thresholds.
/// </remarks>
public sealed class MockFusionExpedienteContractTests : FusionExpedienteContract
{
    /// <inheritdoc />
    protected override IFusionExpediente CreateSut()
        => FusionExpedienteMockFactory.CreateContractConformingMock();
}
