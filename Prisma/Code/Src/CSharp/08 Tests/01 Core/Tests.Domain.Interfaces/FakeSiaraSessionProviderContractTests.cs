namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Reference-fake instance of <see cref="SiaraSessionProviderContract"/> (ADR-005 §6): proves the
/// stateful <see cref="FakeSiaraSessionProvider"/> honors the contract, so it is a trustworthy stand-in
/// for the downloader (MVP-PATH 1.1) and watch-loop (1.2) tests before a live browser exists.
/// </summary>
public sealed class FakeSiaraSessionProviderContractTests : SiaraSessionProviderContract
{
    /// <summary>Initializes the reference-fake instance.</summary>
    public FakeSiaraSessionProviderContractTests()
        : base(new FakeSiaraSessionProvider())
    {
    }
}
