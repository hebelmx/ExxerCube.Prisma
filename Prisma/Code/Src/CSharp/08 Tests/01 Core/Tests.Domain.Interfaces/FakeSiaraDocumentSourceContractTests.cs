namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Reference-fake instance of <see cref="SiaraDocumentSourceContract"/> (ADR-005 §6): proves the
/// <see cref="FakeSiaraDocumentSource"/> honors the port contract, so it is a trustworthy stand-in for the
/// watch-loop tests before a live browser exists.
/// </summary>
public sealed class FakeSiaraDocumentSourceContractTests : SiaraDocumentSourceContract
{
    /// <summary>Initializes the reference-fake instance.</summary>
    public FakeSiaraDocumentSourceContractTests()
        : base(new FakeSiaraDocumentSource())
    {
    }
}
