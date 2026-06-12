namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Verifies the P8 production-host guardrail (ADR-010): the real SIARA host (and its subdomains) is barred
/// fail-closed unless a deployment explicitly opts in, while non-production hosts (the simulator, test
/// hosts) are always allowed.
/// </summary>
public sealed class SiaraHostPolicyTests
{
    private static SiaraHostPolicy CreatePolicy(bool allowProductionHost) =>
        new(
            Options.Create(new SiaraAuthOptions { AllowProductionHost = allowProductionHost }),
            Substitute.For<ILogger<SiaraHostPolicy>>());

    [Theory]
    [InlineData("https://siara.cnbv.gob.mx/")]
    [InlineData("https://siara.cnbv.gob.mx/login")]
    [InlineData("https://SIARA.CNBV.GOB.MX/login")] // case-insensitive
    [InlineData("https://portal.siara.cnbv.gob.mx/login")] // subdomain
    public void Validate_ProductionHost_FailsClosed_WhenNotAllowed(string url)
    {
        var policy = CreatePolicy(allowProductionHost: false);

        var result = policy.Validate(url);

        result.IsSuccess.ShouldBeFalse("the real SIARA production host must be barred unless explicitly opted in");
        result.Error!.ShouldContain("production host");
    }

    [Theory]
    [InlineData("https://siara.cnbv.gob.mx/login")]
    [InlineData("https://portal.siara.cnbv.gob.mx/login")]
    public void Validate_ProductionHost_Allowed_WhenOptedIn(string url)
    {
        var policy = CreatePolicy(allowProductionHost: true);

        var result = policy.Validate(url);

        result.IsSuccess.ShouldBeTrue("opting in via AllowProductionHost must permit the production host");
    }

    [Theory]
    [InlineData("http://localhost:5001/login")]
    [InlineData("https://siara.example/login")]
    [InlineData("https://siara.cnbv.gob.mx.evil.test/login")] // not a real subdomain of the prod host
    [InlineData("https://notsiara.cnbv.gob.mx.test/")]
    public void Validate_NonProductionHost_Allowed_EvenWhenNotOptedIn(string url)
    {
        var policy = CreatePolicy(allowProductionHost: false);

        var result = policy.Validate(url);

        result.IsSuccess.ShouldBeTrue($"non-production host '{url}' must be allowed");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("/relative/path")]
    public void Validate_UnparseableUrl_FailsClosed(string? url)
    {
        var policy = CreatePolicy(allowProductionHost: false);

        var result = policy.Validate(url);

        result.IsSuccess.ShouldBeFalse("an unparseable target URL must fail closed, never default to allowed");
        result.Error!.ShouldContain("could not parse");
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SiaraHostPolicy(null!, Substitute.For<ILogger<SiaraHostPolicy>>()));
    }

    [Fact]
    public void ProductionHost_IsTheRealSiaraHost()
    {
        // Pins the constant so the guardrail cannot be silently weakened by editing the host string.
        SiaraHostPolicy.ProductionHost.ShouldBe("siara.cnbv.gob.mx");
    }
}
