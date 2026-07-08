using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using IndQuestResults;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Proves the <see cref="EscalatingStatementFieldExtractor"/> rebuild path — dormant since E1.B,
/// activated for the first time by E2.2 once <see cref="FieldKind.PaymentDueDate"/> gets a
/// non-empty ladder — preserves every structural/facet field of <see cref="StatementModel"/> by
/// REFERENCE, changing only the resolved <see cref="PeriodSummary.PaymentDueDate"/> field.
/// </summary>
/// <remarks>
/// <para>
/// Before E2.2 every ladder was empty, so <c>anyEscalated</c> in
/// <see cref="EscalatingStatementFieldExtractor"/> was always <see langword="false"/> and the
/// rebuild branch (constructing a fresh <see cref="StatementModel"/>) was dead code — the E1
/// adversarial-review Finding 2. Giving <see cref="FieldKind.PaymentDueDate"/> a real ladder means
/// <see cref="FieldResolutionOrchestrator.ResolveAsync{TValue}"/> now ALWAYS returns a
/// freshly-constructed <see cref="ExtractedField{T}"/> instance for that one field — even when the
/// fuzzy stage does not fire — so this rebuild path now runs on every document with a
/// <see cref="StatementModel.PeriodSummary"/>. This suite is the regression proof that it copies
/// every other facet across unchanged.
/// </para>
/// <para>
/// Uses an <see cref="IStatementFieldExtractor"/> test double that returns one specific,
/// already-computed <see cref="Result{T}"/> (captured from a single real
/// <see cref="PdfPigStatementFieldExtractor"/> run) so every facet the decorator does not
/// explicitly resolve can be asserted REFERENCE-identical — not just value-equal — to the
/// positional model, which is a stronger and more precise proof than deep equality (several
/// facet types, e.g. <see cref="FinancialTable"/>, do not override <c>Equals</c>).
/// </para>
/// <para>
/// <b>E7.S7.2/S7.3 update:</b> <see cref="FieldKind.Product"/> also gets a non-empty ladder
/// (header-image OCR), so <c>Product</c> is no longer guaranteed reference-identical either —
/// it is asserted separately at the end of the test, alongside PaymentDueDate, using the same
/// "unchanged OR honest recovery" shape. This class runs the real
/// <see cref="TesseractHeaderProductOcrEngine"/>, so it belongs to
/// <see cref="VeriqanHeaderOcrCollection"/>.
/// </para>
/// </remarks>
[Trait("Category", "LiveOcr")]
[Collection(VeriqanHeaderOcrCollection.Name)]
public sealed class EscalatingExtractorRebuildPathTests
{
    private static readonly string[] FixtureNames =
    [
        "good.pdf",
        "compliant-master.pdf",
        "bad-math-cl21.pdf",
        "bad-font-cl35.pdf",
    ];

    public static IEnumerable<object[]> Fixtures() => FixtureNames.Select(name => new object[] { name });

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo", fileName);

    // Shared real OCR engine — one per test class run, mirroring the singleton production
    // lifecycle (the engine deadlocks on a second concurrent instantiation).
    private static readonly TesseractHeaderProductOcrEngine OcrEngine =
        new(NullLogger<TesseractHeaderProductOcrEngine>.Instance);

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task ExtractFullAsync_RebuildPath_ReusesEveryFacetReference_ChangesOnlyPaymentDueDate(string fixtureName)
    {
        var ct = TestContext.Current.CancellationToken;
        var path = FixturePath(fixtureName);
        if (!File.Exists(path))
            Assert.Skip($"Demo corpus fixture not found at: {path}");

        var pdf = await File.ReadAllBytesAsync(path, ct);

        // Run the REAL positional extractor exactly once and capture its result. The fake below
        // hands the decorator this SAME Result<StatementModel> instance, so any facet the
        // decorator does not explicitly resolve must be reference-identical to prove the rebuild
        // path truly reuses (rather than reconstructs) it.
        var positionalExtractor = new PdfPigStatementFieldExtractor(
            XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider());
        var innerResult = await positionalExtractor.ExtractFullAsync(pdf, ct);
        innerResult.IsSuccess.ShouldBeTrue();
        var innerModel = innerResult.Value!;

        var fakeInner = Substitute.For<IStatementFieldExtractor>();
        fakeInner.ExtractFullAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(innerResult));

        var orchestrator = new FieldResolutionOrchestrator(
            new FieldEscalationLadderRegistry(),
            new DefaultFieldStageProvider(OcrEngine, NullLoggerFactory.Instance),
            XUnitLogger.CreateLogger<FieldResolutionOrchestrator>());
        var escalating = new EscalatingStatementFieldExtractor(
            fakeInner, orchestrator, XUnitLogger.CreateLogger<EscalatingStatementFieldExtractor>());

        var escalatedResult = await escalating.ExtractFullAsync(pdf, ct);
        escalatedResult.IsSuccess.ShouldBeTrue();
        var escalated = escalatedResult.Value!;

        // The rebuild branch must actually have run — this is the whole point of the test.
        ReferenceEquals(escalated, innerModel).ShouldBeFalse(
            "PaymentDueDate now has a non-empty ladder, so ResolveAsync always returns a fresh "
            + "ExtractedField instance for it — anyEscalated must be true and the model rebuilt.");

        // Header identity fields have empty ladders — reused by reference, unchanged.
        ReferenceEquals(escalated.ClientName, innerModel.ClientName).ShouldBeTrue();
        ReferenceEquals(escalated.Address, innerModel.Address).ShouldBeTrue();
        ReferenceEquals(escalated.BranchNumber, innerModel.BranchNumber).ShouldBeTrue();
        ReferenceEquals(escalated.CardNumber, innerModel.CardNumber).ShouldBeTrue();
        ReferenceEquals(escalated.Clabe, innerModel.Clabe).ShouldBeTrue();
        ReferenceEquals(escalated.ClientNumber, innerModel.ClientNumber).ShouldBeTrue();
        ReferenceEquals(escalated.Rfc, innerModel.Rfc).ShouldBeTrue();

        // Every structural/visual/table facet must be the SAME reference — the decorator copies
        // these across verbatim; it never reconstructs them.
        ReferenceEquals(escalated.Movements, innerModel.Movements).ShouldBeTrue();
        escalated.MovementsStatus.ShouldBe(innerModel.MovementsStatus);
        ReferenceEquals(escalated.TextOverlapIncidents, innerModel.TextOverlapIncidents).ShouldBeTrue();
        ReferenceEquals(escalated.SectionHeaderStyles, innerModel.SectionHeaderStyles).ShouldBeTrue();
        ReferenceEquals(escalated.FontRuns, innerModel.FontRuns).ShouldBeTrue();
        escalated.FontExtractionStatus.ShouldBe(innerModel.FontExtractionStatus);
        ReferenceEquals(escalated.NormalizedFullText, innerModel.NormalizedFullText).ShouldBeTrue();
        ReferenceEquals(escalated.FiscalBlock, innerModel.FiscalBlock).ShouldBeTrue();
        escalated.PageCount.ShouldBe(innerModel.PageCount);
        ReferenceEquals(escalated.Pages, innerModel.Pages).ShouldBeTrue();
        ReferenceEquals(escalated.Sections, innerModel.Sections).ShouldBeTrue();
        ReferenceEquals(escalated.SectionGaps, innerModel.SectionGaps).ShouldBeTrue();
        ReferenceEquals(escalated.TypographySamples, innerModel.TypographySamples).ShouldBeTrue();
        escalated.TypographyExtractionStatus.ShouldBe(innerModel.TypographyExtractionStatus);
        ReferenceEquals(escalated.FinancialTables, innerModel.FinancialTables).ShouldBeTrue();
        ReferenceEquals(escalated.DisputeRows, innerModel.DisputeRows).ShouldBeTrue();
        escalated.DisputeRowsStatus.ShouldBe(innerModel.DisputeRowsStatus);
        ReferenceEquals(escalated.PagePerceptualHashes, innerModel.PagePerceptualHashes).ShouldBeTrue();

        // PeriodSummary itself is a NEW instance (rebuilt to potentially swap in a resolved
        // PaymentDueDate/Product), but every field OTHER than PaymentDueDate/Product must be the
        // SAME ExtractedField reference the positional extractor produced — their ladders are
        // still empty, so the orchestrator's early pass-through returns the original instance.
        innerModel.PeriodSummary.ShouldNotBeNull();
        escalated.PeriodSummary.ShouldNotBeNull();
        var bps = innerModel.PeriodSummary!;
        var eps = escalated.PeriodSummary!;

        ReferenceEquals(eps, bps).ShouldBeFalse("PeriodSummary must be rebuilt (PaymentDueDate/Product may have changed).");
        ReferenceEquals(eps.PeriodStart, bps.PeriodStart).ShouldBeTrue();
        ReferenceEquals(eps.PeriodCutDate, bps.PeriodCutDate).ShouldBeTrue();
        ReferenceEquals(eps.DayCountPrinted, bps.DayCountPrinted).ShouldBeTrue();
        eps.DayCount.ShouldBe(bps.DayCount);
        ReferenceEquals(eps.PagoParaNoGenerarIntereses, bps.PagoParaNoGenerarIntereses).ShouldBeTrue();
        ReferenceEquals(eps.PagoMinimo, bps.PagoMinimo).ShouldBeTrue();
        ReferenceEquals(eps.PagoMinimoMasMeses, bps.PagoMinimoMasMeses).ShouldBeTrue();
        ReferenceEquals(eps.Tasa, bps.Tasa).ShouldBeTrue();
        ReferenceEquals(eps.Cat, bps.Cat).ShouldBeTrue();
        ReferenceEquals(eps.SaldoDeudorTotal, bps.SaldoDeudorTotal).ShouldBeTrue();
        ReferenceEquals(eps.CreditoDisponible, bps.CreditoDisponible).ShouldBeTrue();
        ReferenceEquals(eps.AdeudoPeriodoAnterior, bps.AdeudoPeriodoAnterior).ShouldBeTrue();
        ReferenceEquals(eps.CargosRegularesNoMeses, bps.CargosRegularesNoMeses).ShouldBeTrue();
        ReferenceEquals(eps.CargosComprasAMesesCapital, bps.CargosComprasAMesesCapital).ShouldBeTrue();
        ReferenceEquals(eps.MontoIntereses, bps.MontoIntereses).ShouldBeTrue();
        ReferenceEquals(eps.MontoComisiones, bps.MontoComisiones).ShouldBeTrue();
        ReferenceEquals(eps.IvaInteresesYComisiones, bps.IvaInteresesYComisiones).ShouldBeTrue();
        ReferenceEquals(eps.PagosYAbonos, bps.PagosYAbonos).ShouldBeTrue();
        ReferenceEquals(eps.SaldoCargosRegulares, bps.SaldoCargosRegulares).ShouldBeTrue();
        ReferenceEquals(eps.SaldoCargosAMeses, bps.SaldoCargosAMeses).ShouldBeTrue();
        ReferenceEquals(eps.TotalCargos, bps.TotalCargos).ShouldBeTrue();
        ReferenceEquals(eps.TotalAbonos, bps.TotalAbonos).ShouldBeTrue();

        // PaymentDueDate: the only field allowed to differ, and only in the honest-recovery
        // direction (positional missed it, fuzzy stage recovered a plausible date) or unchanged
        // (both NotExtracted, or positional already succeeded so StatusGate never fired).
        if (bps.PaymentDueDate.Status != ExtractionStatus.NotExtracted)
        {
            eps.PaymentDueDate.Status.ShouldBe(bps.PaymentDueDate.Status);
            eps.PaymentDueDate.Value.ShouldBe(bps.PaymentDueDate.Value);
        }
        else if (eps.PaymentDueDate.Status == ExtractionStatus.NotExtracted)
        {
            // Fuzzy stage abstained too — unchanged. The honest, empirically-verified outcome on
            // this demo corpus (its page-1 layout never prints this field's value).
        }
        else
        {
            eps.PaymentDueDate.Status.ShouldBe(ExtractionStatus.Extracted);
            eps.PaymentDueDate.Provenance.Stage.ShouldBe(StageId.FuzzyLabel);
            PaymentDueDatePlausibilityValidator.IsPlausible(eps.PaymentDueDate.Value).ShouldBeTrue();

            TestContext.Current.SendDiagnosticMessage(
                $"[E2.2] {fixtureName}: PaymentDueDate recovered {eps.PaymentDueDate.Value:yyyy-MM-dd} "
                + $"(confidence {eps.PaymentDueDate.Confidence:0.00}).");
        }

        // Product: the other field allowed to differ (E7.S7.2/S7.3), and only in the honest-
        // recovery direction (positional's now-neutered card-number-band fallback missed it,
        // header-image OCR recovered a real product-name string) or unchanged (both
        // NotExtracted, or positional already succeeded so StatusGate never fired).
        if (bps.Product.Status != ExtractionStatus.NotExtracted)
        {
            eps.Product.Status.ShouldBe(bps.Product.Status);
            eps.Product.Value.ShouldBe(bps.Product.Value);
        }
        else if (eps.Product.Status == ExtractionStatus.NotExtracted)
        {
            // OCR stage abstained too — unchanged.
        }
        else
        {
            eps.Product.Status.ShouldBe(ExtractionStatus.ExtractedByInference);
            eps.Product.Provenance.Stage.ShouldBe(StageId.HeaderImageOcr);
            eps.Product.Value.ShouldNotBeNullOrWhiteSpace();

            TestContext.Current.SendDiagnosticMessage(
                $"[E7.S7.2/S7.3] {fixtureName}: Product recovered '{eps.Product.Value}' via "
                + $"HeaderImageOcr (confidence {eps.Product.Confidence:0.00}).");
        }
    }
}
