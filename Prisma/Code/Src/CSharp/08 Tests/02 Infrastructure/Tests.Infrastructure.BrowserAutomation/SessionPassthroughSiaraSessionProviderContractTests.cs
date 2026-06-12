namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Implementation instance of <see cref="SiaraSessionProviderContract"/> for the production
/// <see cref="SessionPassthroughSiaraSessionProvider"/> (ADR-005, ADR-010, SIARA-AUTH-DESIGN S5).
/// </summary>
/// <remarks>
/// The SUT is the real provider over a mocked <see cref="IBrowserSessionContext"/> that models a healthy
/// authenticated external context (see <see cref="SessionPassthroughTestFactory"/>), so the universal
/// contract — Result semantics, cancellation, fail-closed-on-expiry, release idempotency, and the
/// no-credential session invariant — runs with no live browser. The transport-selection and
/// fail-closed-on-probe mechanics live in <see cref="SessionPassthroughSiaraSessionProviderTests"/>.
/// </remarks>
public sealed class SessionPassthroughSiaraSessionProviderContractTests : SiaraSessionProviderContract
{
    /// <summary>Initializes the implementation instance with the real provider over a healthy mock context.</summary>
    public SessionPassthroughSiaraSessionProviderContractTests()
        : base(SessionPassthroughTestFactory.CreateProvider(
            SessionPassthroughTestFactory.CreateAuthenticatedContextMock()))
    {
    }
}
