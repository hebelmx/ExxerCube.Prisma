namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Reference-fake instance of <see cref="SiaraCredentialSourceContract"/> (ADR-005 §6): proves the
/// stateful <see cref="FakeSiaraCredentialSource"/> honors the contract, so it is a trustworthy stand-in
/// for the AutomatedLogin provider/watch-loop tests before a real vault exists.
/// </summary>
public sealed class FakeSiaraCredentialSourceContractTests : SiaraCredentialSourceContract
{
    /// <summary>Initializes the reference-fake instance.</summary>
    public FakeSiaraCredentialSourceContractTests()
        : base(new FakeSiaraCredentialSource())
    {
    }
}
