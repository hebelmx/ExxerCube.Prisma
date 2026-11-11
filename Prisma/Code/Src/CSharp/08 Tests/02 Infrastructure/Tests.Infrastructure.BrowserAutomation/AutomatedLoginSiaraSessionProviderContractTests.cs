namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Implementation instance of <see cref="SiaraSessionProviderContract"/> for the production
/// <see cref="AutomatedLoginSiaraSessionProvider"/> (ADR-005, ADR-010, SIARA-AUTH-DESIGN S6b).
/// </summary>
/// <remarks>
/// The SUT is the real provider over successful mocks/fakes (closed circuit, usable credentials, accepting
/// form driver, authenticated context — see <see cref="AutomatedLoginTestFactory"/>), so the universal
/// contract runs with no live browser. The circuit-gating, credential-disposal, and fail-closed mechanics
/// live in <see cref="AutomatedLoginSiaraSessionProviderTests"/>.
/// </remarks>
public sealed class AutomatedLoginSiaraSessionProviderContractTests : SiaraSessionProviderContract
{
    /// <summary>Initializes the implementation instance with the real provider over successful collaborators.</summary>
    public AutomatedLoginSiaraSessionProviderContractTests()
        : base(AutomatedLoginTestFactory.CreateProvider())
    {
    }
}
