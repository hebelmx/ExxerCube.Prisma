using ExxerCube.Prisma.Infrastructure.BrowserAutomation.Services;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Verifies that the SIARA login driver enforces the P8 production-host guardrail (ADR-010)
/// <strong>before</strong> entering any credentials: against the real SIARA host it fails closed and never
/// fills the form, unless the deployment has explicitly opted in.
/// </summary>
public sealed class SiaraLoginServiceHostGuardTests
{
    private const string ProductionLoginUrl = "https://siara.cnbv.gob.mx/login";
    private const string SimulatorLoginUrl = "http://localhost:5001/login";

    [Fact]
    public async Task LoginAsync_ProductionHost_NotAllowed_FailsClosed_WithoutEnteringCredentials()
    {
        var agent = BuildLoginCapableAgentMock(currentUrl: ProductionLoginUrl);
        var sut = new SiaraLoginService(CreateHostPolicy(allowProductionHost: false), Substitute.For<ILogger<SiaraLoginService>>());

        var result = await sut.LoginAsync(agent, "user", "pass", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse("the production host must be barred when AllowProductionHost is false");

        await agent.DidNotReceive().FillInputAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await agent.DidNotReceive().ClickElementAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ProductionHost_Allowed_ProceedsAndEntersCredentials()
    {
        var agent = BuildLoginCapableAgentMock(currentUrl: ProductionLoginUrl);
        var sut = new SiaraLoginService(CreateHostPolicy(allowProductionHost: true), Substitute.For<ILogger<SiaraLoginService>>());

        var result = await sut.LoginAsync(agent, "user", "pass", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("opting in must allow the login to proceed");
        await agent.Received().FillInputAsync("input[name='username']", "user", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_SimulatorHost_Proceeds_WithoutOptIn()
    {
        var agent = BuildLoginCapableAgentMock(currentUrl: SimulatorLoginUrl);
        var sut = new SiaraLoginService(CreateHostPolicy(allowProductionHost: false), Substitute.For<ILogger<SiaraLoginService>>());

        var result = await sut.LoginAsync(agent, "user", "pass", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("the simulator/localhost host is always allowed");
    }

    [Fact]
    public async Task LoginAsync_CurrentUrlUnavailable_FailsClosed_WithoutEnteringCredentials()
    {
        var agent = BuildLoginCapableAgentMock(currentUrl: SimulatorLoginUrl);
        agent.GetCurrentUrlAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Result<string>.WithFailure("Browser session not launched."));

        var sut = new SiaraLoginService(CreateHostPolicy(allowProductionHost: false), Substitute.For<ILogger<SiaraLoginService>>());

        var result = await sut.LoginAsync(agent, "user", "pass", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse("if the host cannot be confirmed, the login must fail closed");
        await agent.DidNotReceive().FillInputAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static SiaraHostPolicy CreateHostPolicy(bool allowProductionHost) =>
        new(
            Options.Create(new SiaraAuthOptions { AllowProductionHost = allowProductionHost }),
            Substitute.For<ILogger<SiaraHostPolicy>>());

    private static IBrowserAutomationAgent BuildLoginCapableAgentMock(string currentUrl)
    {
        var agent = Substitute.For<IBrowserAutomationAgent>();

        agent.GetCurrentUrlAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Result<string>.Success(currentUrl));

        agent.WaitForSelectorAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success());

        agent.FillInputAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success());

        agent.ClickElementAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success());

        return agent;
    }
}
