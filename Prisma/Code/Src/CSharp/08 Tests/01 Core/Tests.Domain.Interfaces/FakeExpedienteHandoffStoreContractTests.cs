using ExxerCube.Prisma.Testing.Contracts;

namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Reference-fake instance of <see cref="ExpedienteHandoffStoreContract"/> (ADR-005 §6): proves the
/// <see cref="FakeExpedienteHandoffStore"/> honors the port contract, so it is a trustworthy stand-in for the
/// Extractor → Reconciliator handoff tests that run without a real volume (MVP-PATH 1.4, ADR-011).
/// </summary>
public sealed class FakeExpedienteHandoffStoreContractTests : ExpedienteHandoffStoreContract
{
    /// <summary>Initializes the reference-fake instance.</summary>
    public FakeExpedienteHandoffStoreContractTests()
        : base(new FakeExpedienteHandoffStore())
    {
    }
}
