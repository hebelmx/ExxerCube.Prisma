using System.IO;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;
using ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Web.UI.Services;
using IndQuestResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PDFtoImage;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Tests;

/// <summary>
/// VLD-S1 proof test — the "first visible win" the epic asks for.
/// </summary>
/// <remarks>
/// Proves, via ONE checked-in automated test, that the real <c>AddVeriqan(config)</c>
/// composition root — wired exactly as <c>Program.cs</c> wires it — can drive
/// <c>good.pdf</c> through the full VEC pipeline (ingest → extract → bind → validate →
/// aggregate), that <see cref="IMarkedPdfGenerator"/> can mark up the resulting Fail
/// findings on the original PDF bytes, and that the marked PDF rasterizes to a real,
/// non-empty <c>SKBitmap</c> via <c>PDFtoImage.Conversion.ToImage</c> — the same
/// ingest→render path the live Blazor page (VLD-S5) will eventually drive. This is
/// deliberately a thin vertical slice ("first visible win"), not full coverage — VLD-S2
/// adds the dedicated branch-coverage test project for the <c>IDemoRunner</c> seam.
/// </remarks>
public sealed class LivePipelineWiringProofTests
{
    /// <summary>Repo-root-relative directory containing the 4 anonymized demo fixture PDFs.</summary>
    private const string DemoCorpusRelativePath = "Prisma/Fixtures/PRP2/demo";

    /// <summary>Repo-root-relative directory containing the demo CSV reference-data bundle.</summary>
    private const string ReferenceBundleRelativePath = "Prisma/Fixtures/PRP2/demo/reference-bundle";

    /// <summary>
    /// Statement context key matching the one used by <c>VecChecklistDemoE2ETests</c> — resolves
    /// the <c>Demo_Bank_(Iqubica)</c> reference bundle sub-directory and the Mar-Abr 2026 period.
    /// </summary>
    private static readonly StatementContextKey DemoContextKey =
        new("Demo Bank (Iqubica)", PeriodLabel: "Mar-Abr 2026");

    [Fact]
    public async Task RealPipeline_GoodPdf_ProducesRedVerdict_MarkedPdf_RasterizesToRealBitmap()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // ------------------------------------------------------------------
        // Arrange — locate the repo root + demo fixtures via the SAME resolver Program.cs
        // uses (DemoCorpusPathResolver), so this test proves the actual startup resolution
        // path rather than a parallel hardcoded one.
        // ------------------------------------------------------------------
        var repoRoot = DemoCorpusPathResolver.FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
            Assert.Skip("Repository root (CLAUDE.md) could not be located from the test assembly's base directory.");

        var goodPdfPath = Path.Combine(repoRoot, DemoCorpusRelativePath, "good.pdf");
        if (!File.Exists(goodPdfPath))
            Assert.Skip($"Demo fixture 'good.pdf' not present at '{goodPdfPath}'.");

        var referenceBundleRoot = Path.Combine(repoRoot, ReferenceBundleRelativePath);
        if (!Directory.Exists(referenceBundleRoot))
            Assert.Skip($"Reference bundle root not found at '{referenceBundleRoot}'.");

        // ------------------------------------------------------------------
        // Build the DI container exactly the way Program.cs composes it:
        // AddVeriqan(config) with Veriqan:CsvReferenceData:RootDirectory resolved to an
        // absolute path and ConnectionStrings:VeriqanDb OMITTED (VLD-S1 decision — falls
        // back to AddVeriqanInMemoryPersistence(), no SQL Server dependency for this proof).
        // ------------------------------------------------------------------
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Veriqan:CsvReferenceData:RootDirectory"] = referenceBundleRoot,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        // WebApplicationBuilder registers IConfiguration itself; a plain ServiceCollection does
        // not, and AddVeriqan's ISecretProvider registration (ConfigurationSecretProvider)
        // resolves IConfiguration from DI — so it must be registered explicitly here.
        services.AddSingleton<IConfiguration>(configuration);
        services.AddVeriqan(configuration);

        await using var provider = services.BuildServiceProvider();

        var pdfBytes = await File.ReadAllBytesAsync(goodPdfPath, cancellationToken);
        var submission = new StatementSubmission(
            Pdf: pdfBytes,
            FileName: "good.pdf",
            ContextKey: DemoContextKey);

        // ------------------------------------------------------------------
        // Act 1 — IVerificationPipeline.ProcessAsync, resolved from a fresh scope (mirrors
        // the Blazor-Server scope-per-call rule the live page must follow in VLD-S5, since
        // IVerificationPipeline is registered Scoped).
        // ------------------------------------------------------------------
        Result<VerificationOutcome> pipelineResult;
        await using (var scope = provider.CreateAsyncScope())
        {
            var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
            pipelineResult = await pipeline.ProcessAsync(submission, cancellationToken);
        }

        pipelineResult.IsSuccess.ShouldBeTrue(
            $"Pipeline returned a Result failure for good.pdf: {pipelineResult.Error ?? "<none>"}");

        var outcome = pipelineResult.Value!;
        outcome.Summary.Signal.ShouldBe(
            VerdictSignal.Red,
            "good.pdf is a known-true RED verdict (13 structural FailCheckIds, per " +
            $"VecChecklistDemoE2ETests). FailCheckIds=[{string.Join(", ", outcome.Summary.FailCheckIds)}].");

        // ------------------------------------------------------------------
        // Act 2 — IMarkedPdfGenerator.Generate draws amber/red highlight boxes for every
        // Fail finding onto the original PDF bytes (never mutates the source).
        // ------------------------------------------------------------------
        using var reportingScope = provider.CreateScope();
        var markedPdfGenerator = reportingScope.ServiceProvider.GetRequiredService<IMarkedPdfGenerator>();
        var markResult = markedPdfGenerator.Generate(pdfBytes, outcome.Findings, cancellationToken);

        markResult.IsSuccess.ShouldBeTrue(
            $"IMarkedPdfGenerator.Generate failed: {markResult.Error ?? "<none>"}");

        var markedPdfBytes = markResult.Value!;
        markedPdfBytes.ShouldNotBeNull();
        markedPdfBytes.Length.ShouldBeGreaterThan(0);

        // ------------------------------------------------------------------
        // Act 3 — PDFtoImage.Conversion.ToImage rasterizes page 1 of the marked PDF to a
        // real SKBitmap. Exact pattern already used at
        // PdfPigStatementFieldExtractor.cs:3447 / :3635 — copied, not reinvented.
        // ------------------------------------------------------------------
#pragma warning disable CA1416 // PDFtoImage is cross-platform
        using var pdfStream = new MemoryStream(markedPdfBytes);
        using var bitmap = Conversion.ToImage(
            pdfStream,
            leaveOpen: false,
            page: 0,
            options: new RenderOptions(Dpi: 150));
#pragma warning restore CA1416

        bitmap.ShouldNotBeNull("PDFtoImage.Conversion.ToImage returned null for the marked PDF.");
        bitmap.Width.ShouldBeGreaterThan(0);
        bitmap.Height.ShouldBeGreaterThan(0);
    }
}
