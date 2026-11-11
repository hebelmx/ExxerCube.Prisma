namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Implementation instance of <see cref="SiaraSessionProviderContract"/> for the production
/// <see cref="InteractiveLoginSiaraSessionProvider"/> (ADR-005, ADR-010, SIARA-AUTH-DESIGN S6).
/// </summary>
/// <remarks>
/// The SUT is the real provider over a mocked <see cref="IBrowserAutomationAgent"/> and
/// <see cref="IBrowserSessionContext"/> that model a successful headed login (see
/// <see cref="InteractiveLoginTestFactory"/>), so the universal contract — Result semantics, cancellation,
/// fail-closed-on-expiry, release idempotency, and the no-credential session invariant — runs with no live
/// browser. The human-wait, timeout, and re-hydrate mechanics live in
/// <see cref="InteractiveLoginSiaraSessionProviderTests"/>.
/// </remarks>
public sealed class InteractiveLoginSiaraSessionProviderContractTests : SiaraSessionProviderContract
{
    /// <summary>Initializes the implementation instance with the real provider over successful mocks.</summary>
    public InteractiveLoginSiaraSessionProviderContractTests()
        : base(InteractiveLoginTestFactory.CreateProvider())
    {
    }
}
