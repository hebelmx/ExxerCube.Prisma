namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Implementation instance of <see cref="PersonIdentityResolverContract"/> for
/// <see cref="PersonIdentityResolverService"/> (ADR-005), using the default injected-Sut mechanism.
/// </summary>
/// <remarks>
/// Net-new contract coverage for the real service. The service's mutation kill power lives in the
/// untouched <c>PersonIdentityResolverServiceTests</c> / <c>…EdgeCaseTests</c> / <c>…MutationTests</c>
/// suites, which remain alongside this contract instance (ADR-005 §5: mutation classes are never merged
/// into a base). FindByRfcAsync is a documented DB stub today — the contract pins its current behaviour.
/// </remarks>
public sealed class PersonIdentityResolverServiceContractTests : PersonIdentityResolverContract
{
    /// <summary>Initializes the implementation instance with the real service over a test logger.</summary>
    /// <param name="output">xUnit test output sink for the service's logger.</param>
    public PersonIdentityResolverServiceContractTests(ITestOutputHelper output)
        : base(new PersonIdentityResolverService(XUnitLogger.CreateLogger<PersonIdentityResolverService>(output)))
    {
    }
}
