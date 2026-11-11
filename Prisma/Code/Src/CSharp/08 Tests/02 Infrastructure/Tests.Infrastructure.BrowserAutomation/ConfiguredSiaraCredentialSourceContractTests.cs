namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Implementation instance of <see cref="SiaraCredentialSourceContract"/> for the production
/// <see cref="ConfiguredSiaraCredentialSource"/> (ADR-005, ADR-010 P5, SIARA-AUTH-DESIGN S6b).
/// </summary>
/// <remarks>
/// The SUT is the real source over a mocked <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>
/// that serves healthy credentials, so the universal contract — cancellation, usable/non-leaking/self-
/// clearing credential, and the no-string-credential-property invariant — runs with no real secret store.
/// The missing-key and configured-value mechanics live in
/// <see cref="ConfiguredSiaraCredentialSourceTests"/>.
/// </remarks>
public sealed class ConfiguredSiaraCredentialSourceContractTests : SiaraCredentialSourceContract
{
    /// <summary>Initializes the implementation instance with the real source over a healthy config mock.</summary>
    public ConfiguredSiaraCredentialSourceContractTests()
        : base(ConfiguredSiaraCredentialSourceTestFactory.CreateSource())
    {
    }
}
