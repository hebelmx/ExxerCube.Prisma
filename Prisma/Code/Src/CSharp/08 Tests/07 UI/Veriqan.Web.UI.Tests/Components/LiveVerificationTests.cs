using System.IO;
using Bunit;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Web.UI.Components.Pages;
using ExxerCube.Prisma.Veriqan.Web.UI.Models;
using ExxerCube.Prisma.Veriqan.Web.UI.Services;
using IndQuestResults;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Tests.Components;

/// <summary>
/// Render tests for <see cref="LiveVerification"/> (VLD-S5b) — the first UI surface that drives
/// <see cref="IDemoRunner.RunAsync"/>. <see cref="IDemoRunner"/> and <see cref="IWebHostEnvironment"/>
/// are NSubstitute mocks throughout; these tests exercise the page's own rendering/wiring, not the
/// real pipeline or the real wwwroot fixture bytes (covered elsewhere: <c>DemoRunnerTests</c> for
/// the runner branching, <c>LivePipelineWiringProofTests</c> for the real pipeline).
/// </summary>
public sealed class LiveVerificationTests
{
    private static Bunit.BunitContext CreateContext(
        IDemoRunner demoRunner, IWebHostEnvironment webHostEnvironment, IPipelineReadiness? readiness = null)
    {
        var ctx = new Bunit.BunitContext();
        // MudBlazor components invoke JS interop from OnAfterRenderAsync; "Loose" mode is the
        // standard bUnit setup for headlessly testing MudBlazor components (see VerdictResultTests).
        ctx.JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton(demoRunner);
        ctx.Services.AddSingleton(webHostEnvironment);
        ctx.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<LiveVerification>>(NullLogger<LiveVerification>.Instance);
        // Default to "warmed up" so pre-existing wiring tests (unrelated to VLD-S5c FIX 2) keep
        // exercising the run controls in their enabled state.
        ctx.Services.AddSingleton(readiness ?? CreateReadiness(isReady: true));
        return ctx;
    }

    private static IPipelineReadiness CreateReadiness(bool isReady)
    {
        var readiness = Substitute.For<IPipelineReadiness>();
        readiness.IsReady.Returns(isReady);
        return readiness;
    }

    /// <summary>
    /// Builds an <see cref="IWebHostEnvironment"/> substitute whose <c>WebRootFileProvider</c>
    /// resolves ANY path to a fake, always-existing PDF-shaped byte array — the page never needs
    /// to touch the real wwwroot/demo-fixtures content to prove its own click-through wiring.
    /// </summary>
    private static IWebHostEnvironment CreateFakeWebHost()
    {
        var fileInfo = Substitute.For<IFileInfo>();
        fileInfo.Exists.Returns(true);
        fileInfo.CreateReadStream().Returns(_ => new MemoryStream([0x25, 0x50, 0x44, 0x46]));

        var fileProvider = Substitute.For<IFileProvider>();
        fileProvider.GetFileInfo(Arg.Any<string>()).Returns(fileInfo);

        var webHost = Substitute.For<IWebHostEnvironment>();
        webHost.WebRootFileProvider.Returns(fileProvider);
        return webHost;
    }

    private static DemoStatementCase BuildCase(VerdictSignal signal) => new()
    {
        CaseName = $"Caso {signal}",
        FileName = "good.pdf",
        Signal = signal,
        CondusefTierVerdict = signal,
        BankTierVerdict = signal,
        TotalChecks = 12,
        PassCount = 12,
        FailCount = 0,
        InsufficientDataCount = 0,
        Findings = [],
        ProcessingDuration = TimeSpan.FromSeconds(0.8),
        MarkedPagePngs = new Dictionary<int, byte[]>(),
    };

    [Fact]
    public async Task RendersFixtureCards_WithoutThrowing()
    {
        var demoRunner = Substitute.For<IDemoRunner>();
        await using var ctx = CreateContext(demoRunner, CreateFakeWebHost());

        var cut = ctx.Render<LiveVerification>();

        cut.Markup.ShouldContain("Caso conforme");
        cut.Markup.ShouldContain("Falla de tipografía (CL-35)");
        cut.Markup.ShouldContain("Falla aritmética (CL-21)");
        cut.Markup.ShouldContain("Documento escaneado");
    }

    [Fact]
    public async Task LiveResult_ShowsEnVivoBadge()
    {
        var demoRunner = Substitute.For<IDemoRunner>();
        demoRunner.RunAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<DemoRunOutcome>.WithSuccess(
                new DemoRunOutcome(BuildCase(VerdictSignal.Green), IsLive: true))));

        await using var ctx = CreateContext(demoRunner, CreateFakeWebHost());
        var cut = ctx.Render<LiveVerification>();

        await cut.Find("#run-good-pdf").ClickAsync();

        // Scoped to the badge element only: the page title/placeholder text both contain the
        // lowercase phrase "en vivo", and Shouldly's string ShouldContain is case-insensitive by
        // default, so asserting against the whole page markup would false-positive on every case.
        var badgeText = cut.Find("#live-outcome-badge").TextContent;
        badgeText.ShouldContain("EN VIVO");
        badgeText.ShouldNotContain("DATOS DEMO");
    }

    [Fact]
    public async Task FallbackResult_ShowsDemoBadge()
    {
        var demoRunner = Substitute.For<IDemoRunner>();
        demoRunner.RunAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<DemoRunOutcome>.WithSuccess(
                new DemoRunOutcome(BuildCase(VerdictSignal.Green), IsLive: false))));

        await using var ctx = CreateContext(demoRunner, CreateFakeWebHost());
        var cut = ctx.Render<LiveVerification>();

        await cut.Find("#run-good-pdf").ClickAsync();

        // Scoped to the badge element only — see the comment in LiveResult_ShowsEnVivoBadge for
        // why asserting against the whole page markup would false-positive here.
        var badgeText = cut.Find("#live-outcome-badge").TextContent;
        badgeText.ShouldContain("DATOS DEMO (respaldo)");
        badgeText.ShouldNotContain("EN VIVO");
    }

    [Fact]
    public async Task FailedResult_ShowsErrorAlert_NotCrash()
    {
        var demoRunner = Substitute.For<IDemoRunner>();
        demoRunner.RunAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<DemoRunOutcome>.WithFailure("simulated total failure")));

        await using var ctx = CreateContext(demoRunner, CreateFakeWebHost());
        var cut = ctx.Render<LiveVerification>();

        await cut.Find("#run-good-pdf").ClickAsync();

        cut.Markup.ShouldContain("simulated total failure");
    }

    /// <summary>
    /// VLD-S5c FIX 1: an oversized (or otherwise unreadable) upload throws <see cref="IOException"/>
    /// from <c>IBrowserFile.OpenReadStream</c>. Pre-fix, this was uncaught and blanked the Blazor
    /// Server circuit. Post-fix, it must render a friendly error and never reach the runner.
    /// </summary>
    [Fact]
    public async Task LiveVerification_OversizedUpload_ShowsError_NeverCallsRunner()
    {
        var demoRunner = Substitute.For<IDemoRunner>();
        await using var ctx = CreateContext(demoRunner, CreateFakeWebHost());
        var cut = ctx.Render<LiveVerification>();

        var oversizedFile = Substitute.For<IBrowserFile>();
        oversizedFile.Name.Returns("huge.pdf");
        oversizedFile.OpenReadStream(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Throws(new IOException("Supplied file with size 30000000 bytes exceeds the maximum of 20971520 bytes."));

        var fileUpload = cut.FindComponent<MudFileUpload<IBrowserFile>>();
        await cut.InvokeAsync(() => fileUpload.Instance.FilesChanged.InvokeAsync(oversizedFile));

        cut.Markup.ShouldContain("El archivo excede el tamaño máximo (20 MB) o no se pudo leer.");
        _ = demoRunner.DidNotReceive().RunAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>VLD-S5c FIX 2: run controls stay disabled, with a visible indicator, until warm-up completes.</summary>
    [Fact]
    public async Task LiveVerification_NotWarmedUp_RunControlsDisabled()
    {
        var demoRunner = Substitute.For<IDemoRunner>();
        var readiness = CreateReadiness(isReady: false);
        await using var ctx = CreateContext(demoRunner, CreateFakeWebHost(), readiness);

        var cut = ctx.Render<LiveVerification>();

        cut.Markup.ShouldContain("Calentando el motor");
        cut.Find("#run-good-pdf").HasAttribute("disabled").ShouldBeTrue();
    }

    /// <summary>Counterpart of <see cref="LiveVerification_NotWarmedUp_RunControlsDisabled"/>: once ready, no indicator and enabled controls.</summary>
    [Fact]
    public async Task LiveVerification_WarmedUp_RunControlsEnabled_NoIndicator()
    {
        var demoRunner = Substitute.For<IDemoRunner>();
        var readiness = CreateReadiness(isReady: true);
        await using var ctx = CreateContext(demoRunner, CreateFakeWebHost(), readiness);

        var cut = ctx.Render<LiveVerification>();

        cut.Markup.ShouldNotContain("Calentando el motor");
        cut.Find("#run-good-pdf").HasAttribute("disabled").ShouldBeFalse();
    }
}
