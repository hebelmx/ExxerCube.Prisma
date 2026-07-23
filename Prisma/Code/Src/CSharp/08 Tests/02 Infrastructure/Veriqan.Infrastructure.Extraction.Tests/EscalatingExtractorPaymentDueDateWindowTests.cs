using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using IndQuestResults;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Story 3.2/3.3a — end-to-end coverage of the PaymentDueDate <em>period-relative</em>
/// plausibility window through <see cref="EscalatingStatementFieldExtractor.ExtractFullAsync"/>
/// itself: proves the override validator <c>EscalateAsync</c> builds from the already-resolved
/// <c>PeriodCutDate</c> — <c>new PaymentDueDatePlausibilityValidator(periodCutDate.Value)</c> —
/// actually reaches <see cref="FieldResolutionOrchestrator.ResolveAsync{TValue}"/>'s
/// terminal-validator-abstain rule and changes the returned value, rather than only being proven
/// against a hand-built validator instance in isolation.
/// </summary>
/// <remarks>
/// <para>
/// Ladder/stage-provider construction choice: unlike the sibling
/// <c>EscalatingExtractorProductCatalogGateTests</c> (which hand-rolls a single-field ladder
/// registry because it needs to force a fixed <c>HeaderImageOcr</c> candidate through), this suite
/// uses the <b>real production</b> <see cref="FieldEscalationLadderRegistry"/> (parameterless
/// ctor) and the <b>real production</b> <see cref="EmptyFieldStageProvider"/> — the least
/// artificial construction available. Every scenario below supplies an already-<c>Extracted</c>
/// positional <c>PaymentDueDate</c> candidate, so the production ladder's sole rung
/// (<see cref="EscalationTrigger.StatusGate"/> → <see cref="StageId.FuzzyLabel"/>) never fires
/// (<c>ShouldEscalate</c> only escalates a <c>StatusGate</c> rung when the candidate has no
/// value) — no higher-stage implementation is ever invoked, so <see cref="EmptyFieldStageProvider"/>
/// is exactly as inert here as the real <c>DefaultFieldStageProvider</c> would be. This means the
/// only test doubles in play are the inner <see cref="IStatementFieldExtractor"/> (never
/// re-implement PDF parsing in a unit test) and <see cref="IProductResolver"/> (never exercised —
/// no <see cref="VecReferenceBundle"/> is supplied, so <c>EscalateAsync</c> never constructs a
/// <c>ProductCatalogMembershipValidator</c>, matching every caller as of this story). Everything
/// else — ladder lookup, stage-provider lookup, the orchestrator's rung loop, and (the point of
/// this suite) the terminal-validator-abstain rule — is 100% production code.
/// </para>
/// </remarks>
public sealed class EscalatingExtractorPaymentDueDateWindowTests
{
    private static readonly byte[] MinimalPdfBytes = [0x25, 0x50, 0x44, 0x46]; // "%PDF"

    // ── Test doubles ────────────────────────────────────────────────────────

    private static EscalatingStatementFieldExtractor CreateEscalating(StatementModel innerModel)
    {
        var fakeInner = Substitute.For<IStatementFieldExtractor>();
        fakeInner
            .ExtractFullAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>(), Arg.Any<VecReferenceBundle?>())
            .Returns(_ => Task.FromResult(Result<StatementModel>.WithSuccess(innerModel)));

        // Real production wiring — see class remarks for why this is the least-artificial choice.
        var orchestrator = new FieldResolutionOrchestrator(
            new FieldEscalationLadderRegistry(),
            new EmptyFieldStageProvider(),
            XUnitLogger.CreateLogger<FieldResolutionOrchestrator>());

        return new EscalatingStatementFieldExtractor(
            fakeInner,
            orchestrator,
            Substitute.For<IProductResolver>(), // never exercised: no VecReferenceBundle supplied.
            new SectionAnchorOcrEscalationStage(SectionOcrEscalationTestHelpers.NoOpSectionOcrEngine(), NullLogger<SectionAnchorOcrEscalationStage>.Instance),
            XUnitLogger.CreateLogger<EscalatingStatementFieldExtractor>());
    }

    private static StatementModel BuildModel(
        ExtractedField<DateOnly> periodCutDate, ExtractedField<DateOnly> paymentDueDate)
    {
        var missingString = ExtractedField<string>.Missing(FieldLocator.PageHint(1));
        var missingDate = ExtractedField<DateOnly>.Missing(FieldLocator.PageHint(1));
        var missingInt = ExtractedField<int>.Missing(FieldLocator.PageHint(1));
        var missingDecimal = ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));

        var periodSummary = new PeriodSummary(
            product: missingString,
            periodStart: missingDate,
            periodCutDate: periodCutDate,
            paymentDueDate: paymentDueDate,
            dayCountPrinted: missingInt,
            dayCount: new DayCountVerification(null, null, false),
            pagoParaNoGenerarIntereses: missingDecimal,
            pagoMinimo: missingDecimal,
            pagoMinimoMasMeses: missingDecimal,
            tasa: missingDecimal,
            cat: missingDecimal,
            saldoDeudorTotal: missingDecimal,
            creditoDisponible: missingDecimal);

        return new StatementModel(
            clientName: ExtractedField<ExtractedClientName>.Missing(FieldLocator.PageHint(1)),
            address: ExtractedField<ExtractedAddress>.Missing(FieldLocator.PageHint(1)),
            branchNumber: missingString,
            cardNumber: missingString,
            clabe: missingString,
            clientNumber: missingString,
            rfc: missingString)
        {
            PeriodSummary = periodSummary,
        };
    }

    // ── Tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractFullAsync_PaymentDueDateOutsidePeriodRelativeWindow_Abstains()
    {
        var cutDate = new DateOnly(2025, 3, 15);
        // ~170 days after cut: inside the static [2020,2035] sanity window, outside [cut, cut+60].
        var dueDate = new DateOnly(2025, 9, 1);
        var model = BuildModel(
            periodCutDate: ExtractedField<DateOnly>.Found(cutDate, FieldLocator.PageHint(1)),
            paymentDueDate: ExtractedField<DateOnly>.Found(dueDate, FieldLocator.PageHint(1)));

        var escalating = CreateEscalating(model);
        var ct = TestContext.Current.CancellationToken;

        var result = await escalating.ExtractFullAsync(MinimalPdfBytes, ct);

        result.IsSuccess.ShouldBeTrue();
        var paymentDueDate = result.Value!.PeriodSummary!.PaymentDueDate;

        paymentDueDate.Status.ShouldBe(
            ExtractionStatus.NotExtracted,
            $"{dueDate} is outside the period-relative window [{cutDate}, {cutDate.AddDays(60)}] built by "
            + "EscalateAsync's real `new PaymentDueDatePlausibilityValidator(periodCutDate.Value)` override, even "
            + "though it is inside the static [2020,2035] sanity window — the Story 3.3a terminal-validator-abstain "
            + "rule must reject it end-to-end through the real extractor wiring, not just in isolation.");
    }

    [Fact]
    public async Task ExtractFullAsync_PaymentDueDateInsidePeriodRelativeWindow_StaysExtracted()
    {
        var cutDate = new DateOnly(2025, 3, 15);
        var dueDate = new DateOnly(2025, 4, 5); // 21 days after cut — inside [cut, cut+60].
        var model = BuildModel(
            periodCutDate: ExtractedField<DateOnly>.Found(cutDate, FieldLocator.PageHint(1)),
            paymentDueDate: ExtractedField<DateOnly>.Found(dueDate, FieldLocator.PageHint(1)));

        var escalating = CreateEscalating(model);
        var ct = TestContext.Current.CancellationToken;

        var result = await escalating.ExtractFullAsync(MinimalPdfBytes, ct);

        result.IsSuccess.ShouldBeTrue();
        var paymentDueDate = result.Value!.PeriodSummary!.PaymentDueDate;

        paymentDueDate.Status.ShouldBe(
            ExtractionStatus.Extracted,
            "a plausible, period-relative due date must not be falsely abstained by the tightened window.");
        paymentDueDate.Value.ShouldBe(dueDate);
    }

    [Fact]
    public async Task ExtractFullAsync_NoCutDateResolved_StaticWindowOnlyAppliesAndDateStaysExtracted()
    {
        // Outside any period-relative window, but inside the static [2020,2035] sanity window.
        var dueDate = new DateOnly(2025, 9, 1);
        var model = BuildModel(
            periodCutDate: ExtractedField<DateOnly>.Missing(FieldLocator.PageHint(1)),
            paymentDueDate: ExtractedField<DateOnly>.Found(dueDate, FieldLocator.PageHint(1)));

        var escalating = CreateEscalating(model);
        var ct = TestContext.Current.CancellationToken;

        var result = await escalating.ExtractFullAsync(MinimalPdfBytes, ct);

        result.IsSuccess.ShouldBeTrue();
        var paymentDueDate = result.Value!.PeriodSummary!.PaymentDueDate;

        paymentDueDate.Status.ShouldBe(
            ExtractionStatus.Extracted,
            "without a resolved cut date, EscalateAsync never builds the period-relative override — the ladder's own "
            + "static PaymentDueDatePlausibilityValidator (parameterless [2020,2035] ctor) applies unchanged and "
            + "accepts this date, so the tightening must be scoped to when a cut date is actually available.");
        paymentDueDate.Value.ShouldBe(dueDate);
    }
}
