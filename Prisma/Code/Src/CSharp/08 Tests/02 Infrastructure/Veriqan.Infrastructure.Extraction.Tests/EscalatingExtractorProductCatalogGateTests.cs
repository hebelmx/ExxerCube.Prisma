using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using IndQuestResults;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Story 3.3a — end-to-end coverage of the Product catalog gate through
/// <see cref="EscalatingStatementFieldExtractor.ExtractFullAsync"/> itself: proves (1) the gate is
/// completely inert when no <see cref="VecReferenceBundle"/> is supplied (every caller as of this
/// story) and (2) an OCR-recovered Product token that does not resolve in a supplied catalog
/// abstains rather than being returned.
/// </summary>
/// <remarks>
/// Uses hand-rolled single-field ladder/stage-provider test doubles (rather than the real
/// production <see cref="DefaultFieldStageProvider"/> and its native Tesseract engine) so this
/// suite is fast, deterministic, and independent of native OCR availability — the OCR text→regex
/// recognition path itself is already covered by <c>SyntheticHeaderOcrProductTests</c> /
/// <c>HeaderImageOcrStageLiveOcrCanaryTests</c> (tagged <c>Category=LiveOcr</c>). This suite's job
/// is strictly the catalog-gating wiring added in this story.
/// </remarks>
public sealed class EscalatingExtractorProductCatalogGateTests
{
    // ── Test doubles ────────────────────────────────────────────────────────

    private sealed class SingleFieldLadderRegistry(FieldKind kind, FieldEscalationLadder ladder) : IFieldEscalationLadderRegistry
    {
        public FieldEscalationLadder GetLadder(FieldKind fieldKind) =>
            fieldKind == kind ? ladder : FieldEscalationLadder.PositionalOnly(fieldKind);
    }

    private sealed class FixedStage<TValue>(StageId stage, FieldCandidate<TValue> candidate) : IFieldResolutionStage<TValue>
    {
        public StageId Stage => stage;

        public Task<Result<FieldCandidate<TValue>>> TryResolveAsync(
            FieldResolutionContext<TValue> context, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<FieldCandidate<TValue>>.WithSuccess(candidate));
    }

    /// <summary>Hands out one fixed stage for exactly one <see cref="FieldKind"/>/<c>TValue</c> pair; empty otherwise.</summary>
    private sealed class SingleFieldStageProvider(FieldKind kind, IFieldResolutionStage<string> stage) : IFieldStageProvider
    {
        public IReadOnlyList<IFieldResolutionStage<TValue>> GetHigherStages<TValue>(FieldKind fieldKind)
        {
            if (fieldKind == kind && stage is IFieldResolutionStage<TValue> typed)
                return [typed];
            return Array.Empty<IFieldResolutionStage<TValue>>();
        }
    }

    private const string OcrRecoveredText = "Spurious Header Banner Text";

    /// <summary>
    /// Builds an <see cref="EscalatingStatementFieldExtractor"/> whose inner extractor always
    /// reports Product as NotExtracted (Missing) — mirroring the neutered positional
    /// card-number-band fallback — and whose Product ladder always escalates
    /// (<see cref="EscalationTrigger.StatusGate"/>) to a fixed HeaderImageOcr candidate carrying
    /// <see cref="OcrRecoveredText"/>, exactly like the real header-image OCR stage would recover
    /// some text.
    /// </summary>
    private static EscalatingStatementFieldExtractor CreateEscalating(IProductResolver resolver)
    {
        var fakeInner = Substitute.For<IStatementFieldExtractor>();
        fakeInner
            .ExtractFullAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>(), Arg.Any<VecReferenceBundle?>())
            .Returns(_ => Task.FromResult(Result<StatementModel>.WithSuccess(BuildModelWithMissingProduct())));

        var recovered = FieldCandidate<string>.Found(OcrRecoveredText, 0.9, StageId.HeaderImageOcr, FieldLocator.PageHint(1));
        var ladder = new FieldEscalationLadder(
            FieldKind.Product,
            ConfidenceFloor: 0.0,
            Rungs: [new FieldEscalationRung(StageId.HeaderImageOcr, EscalationTrigger.StatusGate)]);

        var registry = new SingleFieldLadderRegistry(FieldKind.Product, ladder);
        var stageProvider = new SingleFieldStageProvider(
            FieldKind.Product, new FixedStage<string>(StageId.HeaderImageOcr, recovered));

        var orchestrator = new FieldResolutionOrchestrator(
            registry, stageProvider, XUnitLogger.CreateLogger<FieldResolutionOrchestrator>());

        return new EscalatingStatementFieldExtractor(
            fakeInner, orchestrator, resolver, XUnitLogger.CreateLogger<EscalatingStatementFieldExtractor>());
    }

    private static StatementModel BuildModelWithMissingProduct()
    {
        var missingString = ExtractedField<string>.Missing(FieldLocator.PageHint(1));
        var missingDate = ExtractedField<DateOnly>.Missing(FieldLocator.PageHint(1));
        var missingInt = ExtractedField<int>.Missing(FieldLocator.PageHint(1));
        var missingDecimal = ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));

        var periodSummary = new PeriodSummary(
            product: missingString,
            periodStart: missingDate,
            periodCutDate: missingDate,
            paymentDueDate: missingDate,
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

    private static VecReferenceBundle CatalogWithout(string ocrText) => new(
        BundleMetadata: new BundleMetadata("1.0.0", "Test Bank", null, null, null, null),
        Products:
        [
            new VecProduct(
                ProductId: "TC-NL",
                ProductName: "Tarjeta de Crédito NL",
                Aliases: ["NL"],
                HasRewardsProgram: false,
                CardImage: null,
                ImportantMessageImage: null,
                Tariffs: null),
        ],
        InterestRates: null,
        MandatoryLegends: null,
        SequentialImages: null,
        Promotions: null,
        ClientAccounts: null,
        PriorStatements: null,
        ExpectedTransactions: null,
        ToleranceConfig: null,
        ValidationConstants: null);

    private static IProductResolver CreateRealResolver()
    {
        var services = new ServiceCollection();
        services.AddVeriqanBinding();
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IProductResolver>();
    }

    private static readonly byte[] MinimalPdfBytes = [0x25, 0x50, 0x44, 0x46]; // "%PDF"

    // ── Tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractFullAsync_NoReferenceBundleSupplied_ProductResolutionUnchanged()
    {
        var escalating = CreateEscalating(CreateRealResolver());
        var ct = TestContext.Current.CancellationToken;

        // Every caller as of this story — no bundle argument at all.
        var result = await escalating.ExtractFullAsync(MinimalPdfBytes, ct);

        result.IsSuccess.ShouldBeTrue();
        var product = result.Value!.PeriodSummary!.Product;

        // No bundle -> no validatorOverride -> the Story 3.3a terminal gate is never constructed,
        // so the HeaderImageOcr recovery is returned exactly as-is, regardless of catalog
        // membership (there being no catalog in play at all).
        product.Status.ShouldBe(ExtractionStatus.ExtractedByInference);
        product.Provenance.Stage.ShouldBe(StageId.HeaderImageOcr);
        product.Value.ShouldBe(OcrRecoveredText);
    }

    [Fact]
    public async Task ExtractFullAsync_ReferenceBundleSupplied_ProductNotInCatalog_Abstains()
    {
        var escalating = CreateEscalating(CreateRealResolver());
        var ct = TestContext.Current.CancellationToken;
        var bundle = CatalogWithout(OcrRecoveredText);

        var result = await escalating.ExtractFullAsync(MinimalPdfBytes, ct, referenceBundle: bundle);

        result.IsSuccess.ShouldBeTrue();
        var product = result.Value!.PeriodSummary!.Product;

        product.Status.ShouldBe(ExtractionStatus.NotExtracted,
            $"OCR text '{OcrRecoveredText}' does not resolve against the supplied catalog, so extraction must abstain — "
            + "the same outcome the downstream binder's catalog gate would reach.");
    }

    [Fact]
    public async Task ExtractFullAsync_ReferenceBundleSupplied_ProductInCatalog_StillReturned()
    {
        var escalating = CreateEscalating(CreateRealResolver());
        var ct = TestContext.Current.CancellationToken;

        var bundle = new VecReferenceBundle(
            BundleMetadata: new BundleMetadata("1.0.0", "Test Bank", null, null, null, null),
            Products:
            [
                new VecProduct(
                    ProductId: "TC-SPURIOUS",
                    ProductName: OcrRecoveredText,
                    Aliases: null,
                    HasRewardsProgram: false,
                    CardImage: null,
                    ImportantMessageImage: null,
                    Tariffs: null),
            ],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

        var result = await escalating.ExtractFullAsync(MinimalPdfBytes, ct, referenceBundle: bundle);

        result.IsSuccess.ShouldBeTrue();
        var product = result.Value!.PeriodSummary!.Product;

        product.Status.ShouldBe(ExtractionStatus.ExtractedByInference);
        product.Value.ShouldBe(OcrRecoveredText);
    }
}
