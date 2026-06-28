using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Unit tests for <see cref="Section23AbonoLinkageRule"/> (CheckId = LAW-§23-ABONO-LINK).
/// </summary>
/// <remarks>
/// All tests construct <see cref="StatementModel"/> and <see cref="VerificationContext"/>
/// objects directly — no real PDF extraction is performed.
/// Rules are discovered via Scrutor DI (the same path used in production).
/// </remarks>
public sealed class Section23AbonoLinkageRuleTests
{
    private const string CheckId = "LAW-§23-ABONO-LINK";
    private const string ProductId = "TC-S23-LINK-TEST";

    // -----------------------------------------------------------------------
    // Shared fixture helpers
    // -----------------------------------------------------------------------

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static VecReferenceBundle MinimalBundle() =>
        new(
            BundleMetadata: Metadata(),
            Products: [Product()],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    private static FieldLocator P1() => FieldLocator.PageHint(1);
    private static FieldLocator NoPage() => FieldLocator.NoPage();

    private static IVecValidationRule GetRule()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddVeriqanValidation();
        var sp = services.BuildServiceProvider();

        foreach (var rule in sp.GetServices<IVecValidationRule>())
            if (rule.CheckId == CheckId)
                return rule;

        throw new InvalidOperationException(
            $"Rule '{CheckId}' not found in DI — check Scrutor registration.");
    }

    private static VerificationContext Ctx(StatementModel? model)
    {
        var bundle = MinimalBundle();
        return new VerificationContext(
            bundle: bundle,
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model);
    }

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> with fully configured dispute rows,
    /// movements, and (optionally) a §23 section entry.
    /// </summary>
    private static StatementModel ModelWith(
        IReadOnlyList<DisputeRow> disputeRows,
        DisputeRowsExtractionStatus disputeStatus,
        IReadOnlyList<StatementMovement> movements,
        MovementsExtractionStatus movementsStatus,
        IReadOnlyList<DetectedSection>? sections = null)
    {
        var missingStr  = ExtractedField<string>.Missing(P1());
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1());
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1());

        return new StatementModel(
            clientName:    missingName,
            address:       missingAddr,
            branchNumber:  missingStr,
            cardNumber:    missingStr,
            clabe:         missingStr,
            clientNumber:  missingStr,
            rfc:           missingStr)
        {
            DisputeRows        = disputeRows,
            DisputeRowsStatus  = disputeStatus,
            Movements          = movements,
            MovementsStatus    = movementsStatus,
            Sections           = sections ?? [],
        };
    }

    /// <summary>
    /// Builds a single <see cref="StatementMovement"/> with the given absolute amount.
    /// Sign defaults to <see cref="MovementSign.Charge"/>; all other fields are defaults.
    /// </summary>
    private static StatementMovement Movement(decimal amount, MovementSign sign = MovementSign.Charge) =>
        new(
            operationDate: null,
            chargeDate:    null,
            description:   "TEST MOVEMENT",
            amount:        amount,
            sign:          sign,
            locator:       P1());

    /// <summary>
    /// Builds a single <see cref="DisputeRow"/> with the given amount and status.
    /// </summary>
    private static DisputeRow DisputeRow(decimal amount, DisputeStatus status) =>
        new(Amount: amount, Status: status, OperationDate: null, Description: null, Locator: P1());

    // -----------------------------------------------------------------------
    // Rule metadata
    // -----------------------------------------------------------------------

    [Fact]
    public void Rule_CheckId_IsCorrect()
    {
        var rule = GetRule();
        rule.CheckId.ShouldBe(CheckId);
    }

    [Fact]
    public void Rule_DofNumeral_IsNotEmpty()
    {
        var rule = GetRule();
        rule.DofNumeral.ShouldNotBeNullOrWhiteSpace("DofNumeral must be non-empty per IVecValidationRule contract.");
    }

    [Fact]
    public void Rule_Classification_IsBaselineLocked()
    {
        var rule = GetRule();
        rule.Classification.ShouldBe(RuleClassification.BaselineLocked);
    }

    [Fact]
    public void Rule_Technique_IsDeterministic()
    {
        var rule = GetRule();
        rule.Technique.ShouldBe(TechniqueClass.Deterministic);
    }

    // -----------------------------------------------------------------------
    // Cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Cancelled_ReturnsCancelled()
    {
        var rule = GetRule();
        var model = ModelWith([], DisputeRowsExtractionStatus.SectionNotFound, [], MovementsExtractionStatus.SectionNotFound);
        var ctx = Ctx(model);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue("Cancelled token must produce Cancelled, not throw.");
    }

    // -----------------------------------------------------------------------
    // InsufficientData abstain paths
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var ctx = Ctx(model: null);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Null StatementModel must yield InsufficientData, never Fail.");
        result.Value.CheckId.ShouldBe(CheckId);
    }

    [Fact]
    public void Evaluate_DisputeRowsNotExtracted_ReturnsInsufficientData()
    {
        var rule  = GetRule();
        var model = ModelWith(
            disputeRows:     [],
            disputeStatus:   DisputeRowsExtractionStatus.NoRowsParsed,   // <-- not Extracted
            movements:       [Movement(500m)],
            movementsStatus: MovementsExtractionStatus.Extracted);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "DisputeRowsStatus != Extracted must yield InsufficientData — rule cannot verify linkage.");
    }

    [Fact]
    public void Evaluate_DisputeRowsSectionNotFound_ReturnsInsufficientData()
    {
        var rule  = GetRule();
        var model = ModelWith(
            disputeRows:     [],
            disputeStatus:   DisputeRowsExtractionStatus.SectionNotFound,  // <-- not Extracted
            movements:       [Movement(500m)],
            movementsStatus: MovementsExtractionStatus.Extracted);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "DisputeRowsStatus = SectionNotFound must yield InsufficientData.");
    }

    [Fact]
    public void Evaluate_MovementsNotExtracted_ReturnsInsufficientData()
    {
        var rule  = GetRule();
        var model = ModelWith(
            disputeRows:     [DisputeRow(500m, DisputeStatus.ConcluidaProcedente)],
            disputeStatus:   DisputeRowsExtractionStatus.Extracted,
            movements:       [],
            movementsStatus: MovementsExtractionStatus.SectionNotFound);  // <-- not Extracted
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "MovementsStatus != Extracted must yield InsufficientData — cannot match without transaction table.");
    }

    // -----------------------------------------------------------------------
    // §23 not applicable — Pass (not applicable)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section23NotApplicable_ReturnsPassNotApplicable()
    {
        // §23 is in the sections list with IsApplicable=false.
        // The rule should return Pass (not applicable) regardless of dispute row content.
        var rule = GetRule();
        var sections = Enumerable.Range(1, 28).Select(n =>
        {
            var isConditional = n is 16 or 23 or 25;
            return new DetectedSection(n, $"Section {n}",
                IsPresent: !isConditional,
                IsApplicable: !isConditional,
                Locator: isConditional ? NoPage() : P1());
        }).ToList();

        var model = ModelWith(
            disputeRows:     [DisputeRow(500m, DisputeStatus.ConcluidaProcedente)],
            disputeStatus:   DisputeRowsExtractionStatus.Extracted,
            movements:       [],   // no movements (but should never reach linkage check)
            movementsStatus: MovementsExtractionStatus.SectionNotFound,
            sections:        sections);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "§23 not applicable (IsApplicable=false) must return Pass, not Fail or InsufficientData.");
        result.Value.Observed!.ShouldContain("not applicable", Case.Insensitive);
    }

    // -----------------------------------------------------------------------
    // Pass paths
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_ConcluidaProcedente_WithMatchingMovement_ReturnsPass()
    {
        // Primary happy path: a CONCLUIDA PROCEDENTE row whose amount appears in DESGLOSE.
        var rule  = GetRule();
        var model = ModelWith(
            disputeRows:     [DisputeRow(500m, DisputeStatus.ConcluidaProcedente)],
            disputeStatus:   DisputeRowsExtractionStatus.Extracted,
            movements:       [Movement(500m)],
            movementsStatus: MovementsExtractionStatus.Extracted);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Concluida-procedente row with a matching DESGLOSE movement → Pass.");
    }

    [Fact]
    public void Evaluate_ConcluidaImprocedente_WithMatchingMovement_ReturnsPass()
    {
        // CONCLUIDA IMPROCEDENTE row also requires a DESGLOSE entry.
        var rule  = GetRule();
        var model = ModelWith(
            disputeRows:     [DisputeRow(299.99m, DisputeStatus.ConcluidaImprocedente)],
            disputeStatus:   DisputeRowsExtractionStatus.Extracted,
            movements:       [Movement(299.99m)],
            movementsStatus: MovementsExtractionStatus.Extracted);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Concluida-improcedente row with a matching DESGLOSE movement → Pass.");
    }

    [Fact]
    public void Evaluate_MatchWithinTolerance_ReturnsPass()
    {
        // Amount differs by exactly the tolerance (0.02 MXN) — should still Pass.
        var rule  = GetRule();
        var model = ModelWith(
            disputeRows:     [DisputeRow(100.00m, DisputeStatus.ConcluidaProcedente)],
            disputeStatus:   DisputeRowsExtractionStatus.Extracted,
            movements:       [Movement(100.02m)],
            movementsStatus: MovementsExtractionStatus.Extracted);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            $"Amount difference ≤ {Section23AbonoLinkageRule.AmountMatchTolerance} MXN must still match.");
    }

    [Fact]
    public void Evaluate_PendienteRowsOnly_ReturnsPass_NothingToLink()
    {
        // Pendiente rows are excluded from the linkage check — no concluded rows → Pass.
        var rule  = GetRule();
        var model = ModelWith(
            disputeRows:
            [
                DisputeRow(150m, DisputeStatus.Pendiente),
                DisputeRow(200m, DisputeStatus.Pendiente),
            ],
            disputeStatus:   DisputeRowsExtractionStatus.Extracted,
            movements:       [],   // empty DESGLOSE — but should not matter
            movementsStatus: MovementsExtractionStatus.Extracted);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Pendiente rows only (none concluded) → nothing to link → Pass.");
    }

    [Fact]
    public void Evaluate_MultipleConcluidaRows_AllMatched_ReturnsPass()
    {
        // Multiple concluded rows — all have matching DESGLOSE entries.
        var rule  = GetRule();
        var model = ModelWith(
            disputeRows:
            [
                DisputeRow(500m,   DisputeStatus.ConcluidaProcedente),
                DisputeRow(1200m,  DisputeStatus.ConcluidaImprocedente),
            ],
            disputeStatus:   DisputeRowsExtractionStatus.Extracted,
            movements:
            [
                Movement(500m),
                Movement(1200m),
                Movement(75m),   // extra movement — should not affect result
            ],
            movementsStatus: MovementsExtractionStatus.Extracted);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "All concluded rows with matching movements → Pass.");
    }

    // -----------------------------------------------------------------------
    // Fail paths
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_ConcluidaRow_NoMatchingMovement_ReturnsFailCritical()
    {
        // Primary failure path: a concluded row with no matching DESGLOSE amount.
        var rule  = GetRule();
        var model = ModelWith(
            disputeRows:     [DisputeRow(500m, DisputeStatus.ConcluidaProcedente)],
            disputeStatus:   DisputeRowsExtractionStatus.Extracted,
            movements:       [Movement(999m)],  // different amount — no match
            movementsStatus: MovementsExtractionStatus.Extracted);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "Concluded row with no matching DESGLOSE amount → Fail.");
        finding.Severity.ShouldBe(FindingSeverity.Critical,
            "Missing DESGLOSE linkage for a concluded dispute is Critical.");
        finding.Observed.ShouldNotBeNull();
        finding.Observed!.ShouldContain("500.00", Case.Sensitive,
            "The Fail message must name the unmatched amount.");
    }

    [Fact]
    public void Evaluate_ConcluidaRow_NoMatchingMovement_EmptyMovements_ReturnsFailCritical()
    {
        // Concluded row when movements list is empty (but status = Extracted).
        var rule  = GetRule();
        var model = ModelWith(
            disputeRows:     [DisputeRow(750m, DisputeStatus.ConcluidaImprocedente)],
            disputeStatus:   DisputeRowsExtractionStatus.Extracted,
            movements:       [],  // extracted but empty — unusual but possible
            movementsStatus: MovementsExtractionStatus.Extracted);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "Concluded row with empty (but Extracted) movements → no match → Fail.");
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.Observed!.ShouldContain("750.00");
    }

    [Fact]
    public void Evaluate_PartialMatch_OneConcludedUnlinked_ReturnsFailCritical()
    {
        // Two concluded rows: one matches, one does not.
        var rule  = GetRule();
        var model = ModelWith(
            disputeRows:
            [
                DisputeRow(500m, DisputeStatus.ConcluidaProcedente),    // matches movement 500m
                DisputeRow(800m, DisputeStatus.ConcluidaImprocedente),  // no movement for 800m
            ],
            disputeStatus:   DisputeRowsExtractionStatus.Extracted,
            movements:       [Movement(500m)],
            movementsStatus: MovementsExtractionStatus.Extracted);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "One matched row does not save the other unmatched row — any unmatched → Fail.");
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Observed!.ShouldContain("800.00", Case.Sensitive,
            "Fail message must name the unmatched amount (800.00), not the matched one.");
        // 500.00 was matched — it should NOT appear in the unmatched-amounts message.
        finding.Observed!.ShouldNotContain("500.00");
    }

    [Fact]
    public void Evaluate_AmountDiffAboveTolerance_ReturnsFailCritical()
    {
        // Amount differs by more than the tolerance (0.03 > 0.02) — must Fail.
        var rule  = GetRule();
        var model = ModelWith(
            disputeRows:     [DisputeRow(100.00m, DisputeStatus.ConcluidaProcedente)],
            disputeStatus:   DisputeRowsExtractionStatus.Extracted,
            movements:       [Movement(100.03m)],
            movementsStatus: MovementsExtractionStatus.Extracted);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            $"Amount difference (0.03) > tolerance ({Section23AbonoLinkageRule.AmountMatchTolerance}) → no match → Fail.");
    }
}
