namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Implementation instance of <see cref="SiaraActorIdentityProviderContract"/> for the production
/// <see cref="ConfiguredSiaraActorIdentityProvider"/> (ADR-005, ADR-010 P2).
/// </summary>
/// <remarks>
/// The SUT is the real provider configured with a non-empty ActorId so the contract's success-path tests
/// can pass. The fail-closed (empty ActorId) and success mechanics live in
/// <see cref="ConfiguredSiaraActorIdentityProviderTests"/>.
/// </remarks>
public sealed class ConfiguredSiaraActorIdentityProviderContractTests : SiaraActorIdentityProviderContract
{
    /// <summary>Initializes the implementation instance with the real provider over a test configuration.</summary>
    public ConfiguredSiaraActorIdentityProviderContractTests()
        : base(new ConfiguredSiaraActorIdentityProvider(
            Options.Create(new SiaraAuthOptions
            {
                Actor = new SiaraActorOptions { ActorId = "siara-service" },
            }),
            Substitute.For<ILogger<ConfiguredSiaraActorIdentityProvider>>()))
    {
    }
}
