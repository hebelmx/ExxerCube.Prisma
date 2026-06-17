using System;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Unit tests for the fiscal-block validation rules CL-50 through CL-53 (Story 6.2).
/// All rules are tested via synthetic <see cref="StatementModel"/> / <see cref="FiscalBlock"/>
/// and <see cref="PeriodSummary"/> instances — no real PDF or network access.
/// Rules are tested directly (not via DI) to keep the tests self-contained.
/// </summary>
public sealed class FiscalBlockRuleTests
{
    // -----------------------------------------------------------------------
    // Fixture helpers
    // -----------------------------------------------------------------------

    private static readonly BundleMetadata TestMetadata =
        new("1.0.0", "Test Bank", null, null, null, null);

    private static readonly VecProduct TestProduct =
        new(ProductId: "TEST", ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static readonly VecReferenceBundle TestBundle =
        new(BundleMetadata: TestMetadata,
            Products: [TestProduct],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    private static VerificationContext MakeContext(StatementModel? sm) =>
        new(
            bundle: TestBundle,
            resolvedProduct: TestProduct,
            availability: ReferenceDataAvailability.FromBundle(TestBundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: sm);

    private static FiscalBlock FiscalBlockWith(
        bool blockPresent,
        bool qrDecoded,
        string? fiscalCode = null,
        string? issuerRfc = null,
        string? receiverRfc = null,
        string? qrPayload = null) =>
        new(
            BlockPresent: blockPresent,
            QrDecoded: qrDecoded,
            QrPayload: qrPayload,
            FiscalCode: fiscalCode,
            IssuerRfc: issuerRfc,
            ReceiverRfc: receiverRfc,
            Locator: FieldLocator.PageHint(8));

    private static PeriodSummary MakePs(decimal montoComisiones = 0m, decimal ivaIntereses = 0m)
    {
        var p1 = FieldLocator.PageHint(1);
        var missingDec = ExtractedField<decimal>.Missing(p1);
        var missingDate = ExtractedField<DateOnly>.Missing(p1);
        var missingInt = ExtractedField<int>.Missing(p1);
        var missingStr = ExtractedField<string>.Missing(p1);
        var dayCount = new DayCountVerification(
            PrintedDays: 0,
            ComputedSpanDays: 0,
            IsConsistent: false);

        var comisionesField = montoComisiones > 0
            ? ExtractedField<decimal>.Found(montoComisiones, p1)
            : missingDec;
        var ivaField = ivaIntereses > 0
            ? ExtractedField<decimal>.Found(ivaIntereses, p1)
            : missingDec;

        return new PeriodSummary(
            product: missingStr,
            periodStart: missingDate,
            periodCutDate: missingDate,
            paymentDueDate: missingDate,
            dayCountPrinted: missingInt,
            dayCount: dayCount,
            pagoParaNoGenerarIntereses: missingDec,
            pagoMinimo: missingDec,
            pagoMinimoMasMeses: missingDec,
            tasa: missingDec,
            cat: missingDec,
            saldoDeudorTotal: missingDec,
            creditoDisponible: missingDec,
            montoComisiones: comisionesField,
            ivaInteresesYComisiones: ivaField);
    }

    private static StatementModel MakeModel(FiscalBlock fb, decimal montoComisiones = 0m, decimal ivaIntereses = 0m)
    {
        var p1 = FieldLocator.PageHint(1);
        var missingStr = ExtractedField<string>.Missing(p1);
        var missingName = ExtractedField<ExtractedClientName>.Missing(p1);
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(p1);

        return new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            PeriodSummary = MakePs(montoComisiones, ivaIntereses),
            FiscalBlock = fb,
        };
    }

    // -----------------------------------------------------------------------
    // CL-50: QR decode rule
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl50_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = new Cl50FiscalQrRule();
        var ctx = MakeContext(null);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.CheckId.ShouldBe("CL-50");
    }

    [Fact]
    public void Cl50_NullFiscalBlock_ReturnsInsufficientData()
    {
        var rule = new Cl50FiscalQrRule();
        var p1 = FieldLocator.PageHint(1);
        var sm = new StatementModel(
            clientName: ExtractedField<ExtractedClientName>.Missing(p1),
            address: ExtractedField<ExtractedAddress>.Missing(p1),
            branchNumber: ExtractedField<string>.Missing(p1),
            cardNumber: ExtractedField<string>.Missing(p1),
            clabe: ExtractedField<string>.Missing(p1),
            clientNumber: ExtractedField<string>.Missing(p1),
            rfc: ExtractedField<string>.Missing(p1));
        // FiscalBlock intentionally left null (header-only extraction)
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl50_NotRequired_BlockAbsent_ReturnsPass()
    {
        var rule = new Cl50FiscalQrRule();
        var fb = FiscalBlock.NotPresent();
        var sm = MakeModel(fb, montoComisiones: 0m, ivaIntereses: 0m);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass, "no-fiscal-block statement must NOT fail CL-50");
        result.Value.CheckId.ShouldBe("CL-50");
    }

    [Fact]
    public void Cl50_Required_QrDecoded_ReturnsPass()
    {
        var rule = new Cl50FiscalQrRule();
        var fb = FiscalBlockWith(blockPresent: true, qrDecoded: true,
            fiscalCode: "A1B2C3D4-0000-0000-0000-000000000001",
            issuerRfc: "ABC010101AAA", receiverRfc: "XYZ020202BBB",
            qrPayload: "https://sat.gob.mx/...");
        var sm = MakeModel(fb, montoComisiones: 100m);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    [Fact]
    public void Cl50_Required_QrNotDecoded_ReturnsFail()
    {
        var rule = new Cl50FiscalQrRule();
        var fb = FiscalBlockWith(blockPresent: true, qrDecoded: false);
        var sm = MakeModel(fb, montoComisiones: 150m);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    [Fact]
    public void Cl50_RequiredViaIva_QrNotDecoded_ReturnsFail()
    {
        var rule = new Cl50FiscalQrRule();
        var fb = FiscalBlock.NotPresent();
        var sm = MakeModel(fb, ivaIntereses: 50m);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "IVA > 0 makes fiscal block required; absent QR must fail");
    }

    [Fact]
    public void Cl50_Cancellation_ReturnsCancelled()
    {
        var rule = new Cl50FiscalQrRule();
        var ctx = MakeContext(null);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // CL-51: Fiscal code rule
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl51_NotRequired_BlockAbsent_ReturnsPass()
    {
        var rule = new Cl51FiscalCodeRule();
        var fb = FiscalBlock.NotPresent();
        var sm = MakeModel(fb, montoComisiones: 0m, ivaIntereses: 0m);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass, "no-fiscal-block statement must NOT fail CL-51");
    }

    [Fact]
    public void Cl51_BlockPresent_FiscalCodeFound_ReturnsPass()
    {
        var rule = new Cl51FiscalCodeRule();
        var fb = FiscalBlockWith(blockPresent: true, qrDecoded: true,
            fiscalCode: "A1B2C3D4-EEEE-FFFF-0000-000000000001");
        var sm = MakeModel(fb);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.Observed.ShouldBe("A1B2C3D4-EEEE-FFFF-0000-000000000001");
    }

    [Fact]
    public void Cl51_BlockPresent_NoFiscalCode_ReturnsFail()
    {
        var rule = new Cl51FiscalCodeRule();
        var fb = FiscalBlockWith(blockPresent: true, qrDecoded: false, fiscalCode: null);
        var sm = MakeModel(fb, montoComisiones: 200m);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    [Fact]
    public void Cl51_Cancellation_ReturnsCancelled()
    {
        var rule = new Cl51FiscalCodeRule();
        var ctx = MakeContext(null);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // CL-52: Issuer RFC rule
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl52_NotRequired_BlockAbsent_ReturnsPass()
    {
        var rule = new Cl52IssuerRfcRule();
        var fb = FiscalBlock.NotPresent();
        var sm = MakeModel(fb);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass, "no-fiscal-block statement must NOT fail CL-52");
    }

    [Fact]
    public void Cl52_ValidIssuerRfc_ReturnsPass()
    {
        var rule = new Cl52IssuerRfcRule();
        var fb = FiscalBlockWith(blockPresent: true, qrDecoded: true,
            issuerRfc: "ABCD010101AAA");
        var sm = MakeModel(fb);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.Observed.ShouldBe("ABCD010101AAA");
    }

    [Fact]
    public void Cl52_MissingIssuerRfc_ReturnsFail()
    {
        var rule = new Cl52IssuerRfcRule();
        var fb = FiscalBlockWith(blockPresent: true, qrDecoded: true, issuerRfc: null);
        var sm = MakeModel(fb, montoComisiones: 50m);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
    }

    [Fact]
    public void Cl52_MalformedIssuerRfc_ReturnsFail()
    {
        var rule = new Cl52IssuerRfcRule();
        var fb = FiscalBlockWith(blockPresent: true, qrDecoded: true, issuerRfc: "INVALID-RFC");
        var sm = MakeModel(fb, montoComisiones: 50m);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Observed.ShouldBe("INVALID-RFC");
    }

    [Fact]
    public void Cl52_Cancellation_ReturnsCancelled()
    {
        var rule = new Cl52IssuerRfcRule();
        var ctx = MakeContext(null);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // CL-53: Receiver RFC rule
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl53_NotRequired_BlockAbsent_ReturnsPass()
    {
        var rule = new Cl53ReceiverRfcRule();
        var fb = FiscalBlock.NotPresent();
        var sm = MakeModel(fb);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass, "no-fiscal-block statement must NOT fail CL-53");
    }

    [Fact]
    public void Cl53_ValidReceiverRfc_ReturnsPass()
    {
        var rule = new Cl53ReceiverRfcRule();
        var fb = FiscalBlockWith(blockPresent: true, qrDecoded: true,
            receiverRfc: "XYZ800101BBB");
        var sm = MakeModel(fb);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    [Fact]
    public void Cl53_MissingReceiverRfc_ReturnsFail()
    {
        var rule = new Cl53ReceiverRfcRule();
        var fb = FiscalBlockWith(blockPresent: true, qrDecoded: true, receiverRfc: null);
        var sm = MakeModel(fb, montoComisiones: 75m);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
    }

    [Fact]
    public void Cl53_MalformedReceiverRfc_ReturnsFail()
    {
        var rule = new Cl53ReceiverRfcRule();
        var fb = FiscalBlockWith(blockPresent: true, qrDecoded: true, receiverRfc: "12345");
        var sm = MakeModel(fb, ivaIntereses: 10m);
        var ctx = MakeContext(sm);
        var ct = TestContext.Current.CancellationToken;

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Observed.ShouldBe("12345");
    }

    [Fact]
    public void Cl53_Cancellation_ReturnsCancelled()
    {
        var rule = new Cl53ReceiverRfcRule();
        var ctx = MakeContext(null);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }
}
