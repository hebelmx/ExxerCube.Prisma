using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Regression canary pinning the current, empirically-verified SAFE abstention of the E2.2 fuzzy
/// label-anchor recovery stage for <see cref="FieldKind.PaymentDueDate"/> on the five demo-corpus
/// fixtures.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EscalationSeamBehaviorNeutralTests"/> and
/// <see cref="EscalatingExtractorRebuildPathTests"/> already tolerate either outcome for
/// PaymentDueDate (honest abstention OR a plausible <see cref="StageId.FuzzyLabel"/> recovery) —
/// that is correct for THEM, since their job is to prove the decorator seam itself is
/// behavior-neutral / reference-preserving regardless of which outcome the fuzzy stage produces.
/// </para>
/// <para>
/// This suite is narrower and stricter on purpose: the E2.1/E2.2 adversarial review found that
/// <c>PaymentDueDate</c> has downstream Visual-rule consumers (<c>LAW-TYPO-MINSIZE</c> /
/// <c>LAW-TYPO-BOLD</c>) that branch on <see cref="ExtractionStatus.Extracted"/>. If the fuzzy
/// stage ever starts recovering this field on the demo corpus, those verdicts could silently
/// shift. This canary pins today's observed behavior (abstention on all 5 fixtures) as a hard
/// assertion, so that shift becomes a red test instead of a silent regression. If this test ever
/// fails because the fuzzy stage legitimately started recovering a plausible date, that is a real,
/// deliberate finding — do NOT weaken this assertion to make it pass; update the verdict-impact
/// analysis first, then intentionally relax this canary in the same change.
/// </para>
/// <para>
/// <b>E7.S7.2/S7.3 update:</b> <see cref="FieldKind.Product"/> now also has a non-empty ladder,
/// so <see cref="CreateEscalating"/> wires the real <see cref="TesseractHeaderProductOcrEngine"/>
/// — this class belongs to <see cref="VeriqanHeaderOcrCollection"/>.
/// </para>
/// </remarks>
[Trait("Category", "LiveOcr")]
[Collection(VeriqanHeaderOcrCollection.Name)]
public sealed class PaymentDueDateFuzzyRecoveryCanaryTests
{
    private static readonly string[] FixtureNames =
    [
        "good.pdf",
        "compliant-master.pdf",
        "bad-math-cl21.pdf",
        "bad-font-cl35.pdf",
        "scanned.pdf",
    ];

    public static IEnumerable<object[]> Fixtures() => FixtureNames.Select(name => new object[] { name });

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo", fileName);

    private static readonly TesseractHeaderProductOcrEngine OcrEngine =
        new(NullLogger<TesseractHeaderProductOcrEngine>.Instance);

    // Mirrors EscalationSeamBehaviorNeutralTests.CreateEscalating — the production wiring
    // (DefaultFieldStageProvider, not the empty provider) so this canary actually exercises the
    // fuzzy label-anchor stage registered for FieldKind.PaymentDueDate.
    private static EscalatingStatementFieldExtractor CreateEscalating()
    {
        var inner = new PdfPigStatementFieldExtractor(
            XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider());
        var registry = new FieldEscalationLadderRegistry();
        var stageProvider = new DefaultFieldStageProvider(OcrEngine, NullLoggerFactory.Instance);
        var orchestrator = new FieldResolutionOrchestrator(
            registry, stageProvider, XUnitLogger.CreateLogger<FieldResolutionOrchestrator>());
        // No referenceBundle is ever passed by this canary's ExtractFullAsync call, so the
        // resolver is never invoked — a bare substitute is sufficient (Story 3.3a).
        return new EscalatingStatementFieldExtractor(
            inner, orchestrator, Substitute.For<IProductResolver>(), XUnitLogger.CreateLogger<EscalatingStatementFieldExtractor>());
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task EscalatedExtract_DemoFixtures_PaymentDueDateStaysNotExtracted(string fixtureName)
    {
        var ct = TestContext.Current.CancellationToken;
        var path = FixturePath(fixtureName);
        if (!File.Exists(path))
            Assert.Skip($"Demo corpus fixture not found at: {path}");

        var pdf = await File.ReadAllBytesAsync(path, ct);

        var result = await CreateEscalating().ExtractFullAsync(pdf, ct);

        if (!result.IsSuccess)
        {
            // scanned.pdf has no extractable text layer, so the whole extraction can legitimately
            // fail — there is no PeriodSummary to pin an abstention on. Not this canary's concern.
            return;
        }

        var periodSummary = result.Value!.PeriodSummary;
        if (periodSummary is null)
            return;

        periodSummary.PaymentDueDate.Status.ShouldBe(
            ExtractionStatus.NotExtracted,
            $"PaymentDueDate fuzzy recovery status changed on '{fixtureName}' — see remarks on "
            + $"{nameof(PaymentDueDateFuzzyRecoveryCanaryTests)} before touching this assertion.");
    }
}
