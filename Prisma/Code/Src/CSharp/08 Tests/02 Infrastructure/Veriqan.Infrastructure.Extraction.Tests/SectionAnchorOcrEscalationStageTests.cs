using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for <see cref="SectionAnchorOcrEscalationStage"/> (RC1.S6 — §-anchor OCR escalation
/// ladder). Exercises the trigger condition, anchor re-matching against synthetic OCR-noise
/// strings, the honest (page-only, never fabricated) geometry of an OCR-upgraded section, and the
/// never-throws / cancellation-propagation discipline — all against a FAKE
/// <see cref="IHeaderProductOcrEngine"/> (no real Tesseract call; mirrors
/// <c>HeaderImageOcrStageTests</c>'s "canned OCR text stands in for a committed snapshot" style).
/// </summary>
public sealed class SectionAnchorOcrEscalationStageTests
{
    // -----------------------------------------------------------------------
    // Synthetic multi-page PDF (content is irrelevant to the assertions below — the fake engine
    // supplies OCR text directly per page-render call; the PDF only needs to render successfully
    // via PDFtoImage so the per-page render→OCR loop actually iterates pageCount times).
    // -----------------------------------------------------------------------

    private static byte[] BuildNPagePdf(int pageCount)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (var i = 0; i < pageCount; i++)
        {
            var page = builder.AddPage(612, 792);
            page.AddText($"Page {i + 1} placeholder content", 12, new PdfPoint(50, 750), font);
        }

        return builder.Build();
    }

    private static SectionAnchorOcrEscalationStage CreateStage(IHeaderProductOcrEngine engine) =>
        new(engine, NullLogger<SectionAnchorOcrEscalationStage>.Instance);

    /// <summary>
    /// A fake engine that returns <paramref name="perPageText"/>[i] for the i-th call (page
    /// render order), and empty text for any call beyond the supplied array — simulating pages
    /// whose OCR text carries none of the anchors being searched for.
    /// </summary>
    private static IHeaderProductOcrEngine BuildEngine(params string[] perPageText)
    {
        var engine = Substitute.For<IHeaderProductOcrEngine>();
        var callIndex = 0;
        engine.RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var text = callIndex < perPageText.Length ? perPageText[callIndex] : string.Empty;
                callIndex++;
                return Task.FromResult(Result<string>.WithSuccess(text));
            });
        return engine;
    }

    // -----------------------------------------------------------------------
    // Section-list builders
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds the 28-entry "all absent/indeterminate" baseline exactly as
    /// <c>PdfPigStatementFieldExtractor.ExtractDetectedSections</c> would when the text layer
    /// finds nothing — reusing the SAME anchor table (never a second, hand-maintained one).
    /// </summary>
    private static List<DetectedSection> AllAbsentSections()
    {
        var list = new List<DetectedSection>();
        foreach (var (number, name, _, isConditional, isIndeterminate) in PdfPigStatementFieldExtractor.SectionAnchors)
        {
            if (isIndeterminate)
            {
                list.Add(new DetectedSection(number, name, IsPresent: false, IsApplicable: false, Locator: FieldLocator.NoPage())
                {
                    DetectionStatus = SectionDetectionStatus.Indeterminate,
                });
                continue;
            }

            list.Add(new DetectedSection(number, name, IsPresent: false, IsApplicable: !isConditional, Locator: FieldLocator.NoPage())
            {
                DetectionStatus = SectionDetectionStatus.Absent,
            });
        }

        return list;
    }

    private static List<DetectedSection> WithPresent(List<DetectedSection> baseline, params int[] presentNumbers)
    {
        var set = presentNumbers.ToHashSet();
        return baseline.Select(s => set.Contains(s.SectionNumber)
                ? s with { IsPresent = true, IsApplicable = true, DetectionStatus = SectionDetectionStatus.Present, Locator = FieldLocator.PageHint(1) with { Bottom = 700 } }
                : s)
            .ToList();
    }

    private static string AnchorFor(int sectionNumber) =>
        PdfPigStatementFieldExtractor.SectionAnchors.First(a => a.Number == sectionNumber).NormalizedAnchor;

    // -----------------------------------------------------------------------
    // Trigger condition — never fires at ≥ 2 present, always considers firing at < 2
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EscalateAsync_TwoOrMorePresentSections_ReturnsInputUnchanged_NeverCallsEngine()
    {
        var baseline = WithPresent(AllAbsentSections(), 7, 8);
        var engine = BuildEngine("COMPARA TU TARJETA"); // would match §11 if the stage ever looked.
        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.EscalateAsync(baseline, BuildNPagePdf(1), pageCount: 1, ct);

        result.IsSuccess.ShouldBeTrue();
        ReferenceEquals(result.Value, baseline).ShouldBeTrue(
            "≥ 2 present sections must skip escalation and return the exact same list instance.");
        await engine.DidNotReceive().RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EscalateAsync_ExactlyOnePresentSection_TriggersEscalation()
    {
        var baseline = WithPresent(AllAbsentSections(), 7);
        var engine = BuildEngine("COMPARA TU TARJETA");
        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.EscalateAsync(baseline, BuildNPagePdf(1), pageCount: 1, ct);

        result.IsSuccess.ShouldBeTrue();
        await engine.Received(1).RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());

        var section11 = result.Value!.Single(s => s.SectionNumber == 11);
        section11.IsPresent.ShouldBeTrue();
        section11.Source.ShouldBe(SectionDetectionSource.Ocr);
    }

    [Fact]
    public async Task EscalateAsync_ZeroPresentSections_TriggersEscalation()
    {
        var baseline = AllAbsentSections();
        var engine = BuildEngine("COMPARA TU TARJETA");
        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.EscalateAsync(baseline, BuildNPagePdf(1), pageCount: 1, ct);

        result.Value!.Single(s => s.SectionNumber == 11).IsPresent.ShouldBeTrue();
    }

    [Fact]
    public async Task EscalateAsync_PageCountZero_ReturnsInputUnchanged_NeverCallsEngine()
    {
        // Mirrors ExtractHeaderAsync (Sections and PageCount both default/empty) — must be a
        // guaranteed no-op even though the section-count trigger alone would fire.
        var baseline = AllAbsentSections();
        var engine = BuildEngine("COMPARA TU TARJETA");
        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.EscalateAsync(baseline, BuildNPagePdf(1), pageCount: 0, ct);

        ReferenceEquals(result.Value, baseline).ShouldBeTrue();
        await engine.DidNotReceive().RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Anchor matching — reuses IsHeadingLikeMatch; tolerant of OCR noise, never invents wordings
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EscalateAsync_AnchorOnItsOwnLineAmongNoise_Matches()
    {
        var baseline = AllAbsentSections();
        var engine = BuildEngine("BANAMEX\nCOMPARA TU TARJETA\nEstado de cuenta pie de pagina");
        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.EscalateAsync(baseline, BuildNPagePdf(1), pageCount: 1, ct);

        result.Value!.Single(s => s.SectionNumber == 11).IsPresent.ShouldBeTrue();
    }

    [Fact]
    public async Task EscalateAsync_AccentDroppedByOcr_StillMatches()
    {
        // §15 anchor is "NUMERO DE CUENTA" — Tesseract sometimes drops the tilde on Ñ/accents;
        // VecTextNormalizer strips accents from BOTH the OCR line and the anchor is already
        // accent-free, so a lowercase/accented OCR read of "Número de Cuenta" must still match.
        var baseline = AllAbsentSections();
        var engine = BuildEngine("Número de Cuenta");
        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.EscalateAsync(baseline, BuildNPagePdf(1), pageCount: 1, ct);

        result.Value!.Single(s => s.SectionNumber == 15).IsPresent.ShouldBeTrue();
    }

    [Fact]
    public async Task EscalateAsync_CrossReferenceSentenceEmbeddingAnchor_DoesNotMatch()
    {
        // Reuses the exact same IsHeadingLikeMatch cross-reference guard the text-layer pass
        // uses — an OCR line that merely NAMES the section in a footer sentence must not count.
        var baseline = AllAbsentSections();
        var engine = BuildEngine("Consulta la seccion \"COMPARA TU TARJETA\" para mas informacion.");
        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.EscalateAsync(baseline, BuildNPagePdf(1), pageCount: 1, ct);

        result.Value!.Single(s => s.SectionNumber == 11).IsPresent.ShouldBeFalse(
            "A cross-reference sentence naming the section is not a genuine heading — same guard as the text-layer pass.");
    }

    [Fact]
    public async Task EscalateAsync_GenuinelyAbsentAnchor_StaysAbsent_NeverInvented()
    {
        // §3 "Datos de envio" is one of the anchors the real-corpus probe found genuinely
        // reworded/absent — OCR text with no trace of it must leave the section Absent, not
        // fabricate a match.
        var baseline = AllAbsentSections();
        var engine = BuildEngine("COMPARA TU TARJETA");
        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.EscalateAsync(baseline, BuildNPagePdf(1), pageCount: 1, ct);

        var section3 = result.Value!.Single(s => s.SectionNumber == 3);
        section3.IsPresent.ShouldBeFalse();
        section3.Source.ShouldBe(SectionDetectionSource.TextLayer, "an untouched section keeps its original TextLayer source marker.");
    }

    // -----------------------------------------------------------------------
    // Honest geometry — never fabricates a bounding box
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EscalateAsync_OcrUpgradedSection_HasPageOnlyLocator_NoBoundingBox()
    {
        var baseline = AllAbsentSections();
        var engine = BuildEngine("COMPARA TU TARJETA");
        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.EscalateAsync(baseline, BuildNPagePdf(1), pageCount: 1, ct);

        var section11 = result.Value!.Single(s => s.SectionNumber == 11);
        section11.Locator.PageNumber.ShouldBe(1);
        section11.Locator.Bottom.ShouldBeNull();
        section11.Locator.HasBoundingBox.ShouldBeFalse();
        section11.SectionText.ShouldBe(string.Empty,
            "OCR gives no per-word geometry to reconstruct a scoped section body — leaving it empty is honest, not a fabrication.");
    }

    [Fact]
    public async Task EscalateAsync_MatchOnPageTwo_LocatorPageNumberIsTwo()
    {
        var baseline = AllAbsentSections();
        var engine = BuildEngine(string.Empty, "COMPARA TU TARJETA"); // page 1 empty, page 2 has it.
        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.EscalateAsync(baseline, BuildNPagePdf(2), pageCount: 2, ct);

        result.Value!.Single(s => s.SectionNumber == 11).Locator.PageNumber.ShouldBe(2);
    }

    // -----------------------------------------------------------------------
    // Never-throws / degraded-abstain discipline
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EscalateAsync_EngineFailsOnOnePage_SkipsThatPage_ContinuesOthers()
    {
        var baseline = AllAbsentSections();
        var engine = Substitute.For<IHeaderProductOcrEngine>();
        var callIndex = 0;
        engine.RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var index = callIndex++;
                return index == 0
                    ? Task.FromResult(Result<string>.WithFailure("native engine crashed"))
                    : Task.FromResult(Result<string>.WithSuccess("COMPARA TU TARJETA"));
            });
        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.EscalateAsync(baseline, BuildNPagePdf(2), pageCount: 2, ct);

        result.IsSuccess.ShouldBeTrue("a per-page OCR failure must degrade to skipping that page, never fail the whole call.");
        result.Value!.Single(s => s.SectionNumber == 11).IsPresent.ShouldBeTrue();
    }

    [Fact]
    public async Task EscalateAsync_NoAnchorsMatchOnAnyPage_ReturnsInputUnchanged()
    {
        var baseline = AllAbsentSections();
        var engine = BuildEngine("nothing recognizable here");
        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.EscalateAsync(baseline, BuildNPagePdf(1), pageCount: 1, ct);

        ReferenceEquals(result.Value, baseline).ShouldBeTrue(
            "zero OCR matches must return the exact same list instance, not a value-identical rebuild.");
    }

    // -----------------------------------------------------------------------
    // Cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EscalateAsync_PreCancelledToken_ReturnsCancelledWithoutCallingEngine()
    {
        var baseline = AllAbsentSections();
        var engine = BuildEngine("COMPARA TU TARJETA");
        var stage = CreateStage(engine);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await stage.EscalateAsync(baseline, BuildNPagePdf(1), pageCount: 1, cts.Token);

        result.IsCancelled().ShouldBeTrue();
        await engine.DidNotReceive().RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EscalateAsync_EngineReportsCancelled_PropagatesCancelled()
    {
        var baseline = AllAbsentSections();
        var engine = Substitute.For<IHeaderProductOcrEngine>();
        engine.RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ResultExtensions.Cancelled<string>()));
        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.EscalateAsync(baseline, BuildNPagePdf(1), pageCount: 1, ct);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Performance discipline — at most one render+OCR call per page per invocation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EscalateAsync_MultiPageDocument_CallsEngineAtMostOncePerPage()
    {
        var baseline = AllAbsentSections();
        var engine = BuildEngine("page one text", "page two text", "page three text");
        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        await stage.EscalateAsync(baseline, BuildNPagePdf(3), pageCount: 3, ct);

        await engine.Received(3).RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }
}
