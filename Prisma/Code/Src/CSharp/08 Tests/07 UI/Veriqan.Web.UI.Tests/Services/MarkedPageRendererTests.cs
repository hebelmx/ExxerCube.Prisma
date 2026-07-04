using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Web.UI.Services;
using IndQuestResults.Operations;
using Meziantou.Extensions.Logging.Xunit.v3;
using PdfSharp.Pdf;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Tests.Services;

/// <summary>
/// Unit tests for <see cref="MarkedPageRenderer"/> (VLD-S3).
/// </summary>
/// <remarks>
/// Input PDFs are built entirely in-memory with PdfSharp (the same package
/// <c>Veriqan.Infrastructure.Reporting</c> already depends on, transitively available here
/// through the Web.UI → Orchestration → Reporting chain) so this test project stays
/// independent of the demo fixture corpus.
/// </remarks>
public sealed class MarkedPageRendererTests
{
    private readonly IMarkedPageRenderer _renderer;

    public MarkedPageRendererTests()
    {
        var logger = XUnitLogger.CreateLogger<MarkedPageRenderer>(TestContext.Current.TestOutputHelper);
        _renderer = new MarkedPageRenderer(logger);
    }

    // -----------------------------------------------------------------------
    // Fixtures / helpers
    // -----------------------------------------------------------------------

    /// <summary>Builds a minimal multi-page A4 PDF using PdfSharp — no fixture dependency.</summary>
    private static byte[] BuildTestPdf(int pageCount)
    {
        using var doc = new PdfDocument();
        for (var i = 0; i < pageCount; i++)
        {
            var page = doc.AddPage();
            page.Width = PdfSharp.Drawing.XUnit.FromPoint(595);
            page.Height = PdfSharp.Drawing.XUnit.FromPoint(842);
        }

        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }

    private static RuleFinding FailOnPage(string checkId, int pageNumber) =>
        RuleFinding.Fail(
            checkId: checkId,
            technique: TechniqueClass.Deterministic,
            severity: FindingSeverity.Critical,
            engineVersion: "1.0.0",
            expected: "expected",
            observed: "observed",
            locator: FieldLocator.PageHint(pageNumber));

    private static RuleFinding PassFinding(string checkId = "CL-OK") =>
        RuleFinding.Pass(
            checkId: checkId,
            technique: TechniqueClass.Deterministic,
            engineVersion: "1.0.0",
            locator: FieldLocator.PageHint(1));

    /// <summary>PNG file signature (first 8 bytes of every valid PNG).</summary>
    private static readonly byte[] PngMagicHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static void ShouldBeNonEmptyPng(byte[] bytes)
    {
        bytes.ShouldNotBeNull();
        bytes.Length.ShouldBeGreaterThan(PngMagicHeader.Length);
        bytes.Take(PngMagicHeader.Length).ToArray().ShouldBe(PngMagicHeader);
    }

    // -----------------------------------------------------------------------
    // RenderFindingPages_FindingsOnPages2And4_ReturnsThoseTwoPngs
    // -----------------------------------------------------------------------

    [Fact]
    public void RenderFindingPages_FindingsOnPages2And4_ReturnsThoseTwoPngs()
    {
        var pdf = BuildTestPdf(5);
        var findings = new List<RuleFinding>
        {
            PassFinding("CL-OK"),
            FailOnPage("CL-P2", pageNumber: 2),
            FailOnPage("CL-P4", pageNumber: 4),
        };

        var result = _renderer.RenderFindingPages(pdf, findings, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue($"Render must succeed for a valid multi-page PDF: {result.Error}");
        result.Value.ShouldNotBeNull();
        result.Value!.Keys.OrderBy(k => k).ShouldBe(new[] { 2, 4 });

        ShouldBeNonEmptyPng(result.Value[2]);
        ShouldBeNonEmptyPng(result.Value[4]);
    }

    // -----------------------------------------------------------------------
    // RenderFindingPages_NoFailFindings_ReturnsPageOneOnly
    // -----------------------------------------------------------------------

    [Fact]
    public void RenderFindingPages_NoFailFindings_ReturnsPageOneOnly()
    {
        var pdf = BuildTestPdf(3);
        var findings = new List<RuleFinding> { PassFinding("CL-OK") };

        var result = _renderer.RenderFindingPages(pdf, findings, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue($"Render must succeed with no Fail findings (GREEN case): {result.Error}");
        result.Value.ShouldNotBeNull();
        result.Value!.Keys.ShouldBe(new[] { 1 });
        ShouldBeNonEmptyPng(result.Value[1]);
    }

    [Fact]
    public void RenderFindingPages_EmptyFindingsList_ReturnsPageOneOnly()
    {
        var pdf = BuildTestPdf(1);

        var result = _renderer.RenderFindingPages(pdf, new List<RuleFinding>(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Keys.ShouldBe(new[] { 1 });
    }

    // -----------------------------------------------------------------------
    // RenderFindingPages_InvalidPdfBytes_ReturnsFailure
    // -----------------------------------------------------------------------

    [Fact]
    public void RenderFindingPages_InvalidPdfBytes_ReturnsFailure()
    {
        var junk = "NOT A PDF"u8.ToArray();

        var result = _renderer.RenderFindingPages(junk, new List<RuleFinding>(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue("Corrupt (non-empty) bytes must yield a typed failure, not a throw.");
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void RenderFindingPages_EmptyPdfBytes_ReturnsFailure()
    {
        var result = _renderer.RenderFindingPages(
            Array.Empty<byte>(), new List<RuleFinding>(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue("Empty byte array must yield a typed failure.");
        result.Error.ShouldNotBeNullOrEmpty();
    }

    // -----------------------------------------------------------------------
    // RenderFindingPages_OutOfRangePageNumber_SkipsGracefully
    // -----------------------------------------------------------------------

    [Fact]
    public void RenderFindingPages_OutOfRangePageNumber_SkipsGracefully()
    {
        var pdf = BuildTestPdf(3); // pages 1..3 only

        var findings = new List<RuleFinding>
        {
            FailOnPage("CL-VALID", pageNumber: 2),
            FailOnPage("CL-TOO-FAR", pageNumber: 99), // out of range — must be skipped, not fail the render
            RuleFinding.Fail(
                checkId: "CL-NOPAGE",
                technique: TechniqueClass.Deterministic,
                severity: FindingSeverity.Warning,
                engineVersion: "1.0.0",
                locator: FieldLocator.NoPage()), // PageNumber == 0 sentinel — must be skipped
        };

        var result = _renderer.RenderFindingPages(pdf, findings, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(
            $"An out-of-range or NoPage finding must be silently skipped, not fail the whole render: {result.Error}");
        result.Value.ShouldNotBeNull();
        result.Value!.Keys.ShouldBe(new[] { 2 });
        ShouldBeNonEmptyPng(result.Value[2]);
    }

    // -----------------------------------------------------------------------
    // RenderFindingPages_Cancelled_ReturnsCancelledResult
    // -----------------------------------------------------------------------

    [Fact]
    public void RenderFindingPages_Cancelled_ReturnsCancelledResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var pdf = BuildTestPdf(2);
        var findings = new List<RuleFinding> { FailOnPage("CL-P1", pageNumber: 1) };

        var result = _renderer.RenderFindingPages(pdf, findings, cts.Token);

        result.IsCancelled().ShouldBeTrue("A pre-cancelled token must produce a Cancelled result, never a throw.");
    }
}
