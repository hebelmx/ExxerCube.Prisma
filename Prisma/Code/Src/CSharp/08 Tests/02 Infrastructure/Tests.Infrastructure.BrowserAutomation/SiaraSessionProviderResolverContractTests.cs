namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Implementation instance of <see cref="SiaraSessionProviderResolverContract"/> for the production
/// <see cref="ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara.SiaraSessionProviderResolver"/> over
/// a real <c>AddSiaraAuthentication</c> container (ADR-005, ADR-010, SIARA-AUTH-DESIGN S7).
/// </summary>
/// <remarks>
/// Configured for the bank-safe default <see cref="SiaraAuthMode.SessionPassthrough"/>; the per-mode and
/// fail-closed mechanics live in <see cref="SiaraSessionProviderResolverTests"/>.
/// </remarks>
public sealed class SiaraSessionProviderResolverContractTests : SiaraSessionProviderResolverContract
{
    /// <summary>Initializes the implementation instance with the real resolver over a real container.</summary>
    public SiaraSessionProviderResolverContractTests()
        : base(
            SiaraResolverTestFactory.CreateResolver(SiaraAuthMode.SessionPassthrough),
            SiaraAuthMode.SessionPassthrough)
    {
    }
}
