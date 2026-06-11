using ExxerCube.Prisma.Infrastructure.BrowserAutomation;
using ExxerCube.Prisma.Testing.Contracts;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Tests.System.BrowserAutomation.E2E;

/// <summary>
/// Implementation instance of <see cref="BrowserSessionContextContract"/> for the production
/// <see cref="PlaywrightBrowserAutomationAdapter"/> (ADR-005, ADR-010 §7).
/// </summary>
/// <remarks>
/// Every contract behavior short-circuits on validation/cancellation/no-session before Playwright is
/// touched, so this inheritor verifies the adapter honors the Result/cancellation/fail-closed contract
/// <strong>without launching a browser</strong>. The real capture/restore round-trip (which needs a
/// browser) is covered by <see cref="PlaywrightBrowserSessionContextE2ETests"/>.
/// </remarks>
public sealed class PlaywrightBrowserSessionContextContractTests : BrowserSessionContextContract
{
    /// <summary>Initializes the implementation instance with a real, un-launched adapter.</summary>
    public PlaywrightBrowserSessionContextContractTests()
        : base(new PlaywrightBrowserAutomationAdapter(
            Substitute.For<ILogger<PlaywrightBrowserAutomationAdapter>>(),
            Options.Create(new BrowserAutomationOptions())))
    {
    }
}
