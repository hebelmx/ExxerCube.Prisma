using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Targeted extraction tests for the real Banamex Visa (BSSB) credit-card layout,
/// using <c>good.pdf</c> from the production-quality golden-master demo corpus
/// (Mar-Apr 2026 statement, account B, card ending 0001).
/// </summary>
/// <remarks>
/// <para>
/// This fixture uses a left-aligned layout that differs fundamentally from the
/// "Dummie VEC" fixtures used in other test classes:
/// </para>
/// <list type="bullet">
///   <item><description>
///     Header labeled fields (RFC, CLABE, card number, etc.) are in the LEFT column
///     (label at X≈25, value at X≈102) rather than the right column (X≥285).
///   </description></item>
///   <item><description>
///     Period fields ("Periodo:", "Fecha de corte:", "Número de días …") use a colon
///     fused to the last label token ("corte:") and live in the CENTRE column (X≈309).
///   </description></item>
///   <item><description>
///     RESUMEN amounts ("Cargos regulares (no a meses) + $6,479.43") are in the LEFT
///     column (X≈25) with a "+" sign separating label from value.
///   </description></item>
///   <item><description>
///     "Pago mínimo:" sits in the centre column with its value ($980.00) at far right
///     (X≈528), beyond the original X≤300 constraint.
///   </description></item>
/// </list>
/// <para>
/// Tests in this class pin the concrete field values from the golden-master corpus.
/// A mismatch here means either the extractor regressed on this layout or the fixture
/// was replaced with a different document.
/// </para>
/// </remarks>
public sealed class PdfPigStatementFieldExtractorRealLayoutTests
{
    // -----------------------------------------------------------------------
    // Fixture path
    // -----------------------------------------------------------------------

    private static readonly string GoodPdfPath =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo", "good.pdf");

    // -----------------------------------------------------------------------
    // Factory helper
    // -----------------------------------------------------------------------

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider());

    // -----------------------------------------------------------------------
    // Lazy fixture loader — skip gracefully if file is absent
    // -----------------------------------------------------------------------

    private static async Task<(StatementModel model, PeriodSummary ps)> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(GoodPdfPath))
            Assert.Skip($"Demo corpus fixture not found at: {GoodPdfPath}");

        var extractor = CreateExtractor();
        var pdf = await File.ReadAllBytesAsync(GoodPdfPath, ct);
        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync must succeed. Error: {result.Error}");
        var model = result.Value!;
        model.PeriodSummary.ShouldNotBeNull("PeriodSummary must be populated");
        return (model, model.PeriodSummary!);
    }

    // -----------------------------------------------------------------------
    // Coverage floor — extracted ≥ 10 without any bypass
    // -----------------------------------------------------------------------

    /// <summary>
    /// The real Banamex Visa layout must yield ≥10 extracted fields (the LegalBaseline
    /// coverage floor) without any TenantProfile override.  Previously only 8 were
    /// extracted; the extractor was calibrated to also handle the left-column layout.
    /// </summary>
    [Fact]
    public async Task Extract_RealLayout_MeetsExtractionFloor()
    {
        var ct = TestContext.Current.CancellationToken;
        var (model, ps) = await LoadAsync(ct);

        // Count fields the same way VerificationPipeline.CountExtractedFields does.
        static bool IsExtracted(ExtractionStatus s) =>
            s is ExtractionStatus.Extracted or ExtractionStatus.ExtractedInvalidFormat;

        var count = 0;
        if (IsExtracted(model.ClientName.Status)) count++;
        if (IsExtracted(model.Address.Status)) count++;
        if (IsExtracted(model.BranchNumber.Status)) count++;
        if (IsExtracted(model.CardNumber.Status)) count++;
        if (IsExtracted(model.Clabe.Status)) count++;
        if (IsExtracted(model.ClientNumber.Status)) count++;
        if (IsExtracted(model.Rfc.Status)) count++;
        if (IsExtracted(ps.Product.Status)) count++;
        if (IsExtracted(ps.PeriodStart.Status)) count++;
        if (IsExtracted(ps.PeriodCutDate.Status)) count++;
        if (IsExtracted(ps.PaymentDueDate.Status)) count++;
        if (IsExtracted(ps.DayCountPrinted.Status)) count++;
        if (IsExtracted(ps.PagoParaNoGenerarIntereses.Status)) count++;
        if (IsExtracted(ps.PagoMinimo.Status)) count++;
        if (IsExtracted(ps.PagoMinimoMasMeses.Status)) count++;
        if (IsExtracted(ps.Tasa.Status)) count++;
        if (IsExtracted(ps.Cat.Status)) count++;
        if (IsExtracted(ps.SaldoDeudorTotal.Status)) count++;
        if (IsExtracted(ps.CreditoDisponible.Status)) count++;
        if (IsExtracted(ps.AdeudoPeriodoAnterior.Status)) count++;
        if (IsExtracted(ps.CargosRegularesNoMeses.Status)) count++;
        if (IsExtracted(ps.CargosComprasAMesesCapital.Status)) count++;
        if (IsExtracted(ps.MontoIntereses.Status)) count++;
        if (IsExtracted(ps.MontoComisiones.Status)) count++;
        if (IsExtracted(ps.IvaInteresesYComisiones.Status)) count++;
        if (IsExtracted(ps.PagosYAbonos.Status)) count++;
        if (IsExtracted(ps.SaldoCargosRegulares.Status)) count++;
        if (IsExtracted(ps.SaldoCargosAMeses.Status)) count++;
        if (IsExtracted(ps.TotalCargos.Status)) count++;
        if (IsExtracted(ps.TotalAbonos.Status)) count++;

        count.ShouldBeGreaterThanOrEqualTo(10,
            $"Real Banamex Visa layout must extract ≥10 fields (LegalBaseline floor). Got {count}.");
    }

    // -----------------------------------------------------------------------
    // Header fields — left-column layout (labels at X≈25, values at X≈102)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Extract_RealLayout_ClientNameExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (model, _) = await LoadAsync(ct);

        model.ClientName.Status.ShouldBe(ExtractionStatus.Extracted, "ClientName must be extracted");
        model.ClientName.Value!.FullName.ShouldBe("CARLOS MENDOZA VARGAS");
    }

    [Fact]
    public async Task Extract_RealLayout_CardNumberExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (model, _) = await LoadAsync(ct);

        model.CardNumber.Status.ShouldBe(ExtractionStatus.Extracted, "CardNumber must be extracted");
        model.CardNumber.Value.ShouldBe("4111000000070001");
    }

    [Fact]
    public async Task Extract_RealLayout_ClabeExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (model, _) = await LoadAsync(ct);

        model.Clabe.Status.ShouldBe(ExtractionStatus.Extracted, "CLABE must be extracted");
        model.Clabe.Value.ShouldBe("002180000000000025");
    }

    [Fact]
    public async Task Extract_RealLayout_RfcExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (model, _) = await LoadAsync(ct);

        model.Rfc.Status.ShouldBe(ExtractionStatus.Extracted, "RFC must be extracted");
        model.Rfc.Value.ShouldBe("MEVC000101XX5");
    }

    [Fact]
    public async Task Extract_RealLayout_BranchNumberExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (model, _) = await LoadAsync(ct);

        model.BranchNumber.Status.ShouldBe(ExtractionStatus.Extracted, "BranchNumber must be extracted");
        model.BranchNumber.Value.ShouldBe("0002");
    }

    [Fact]
    public async Task Extract_RealLayout_ClientNumberExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (model, _) = await LoadAsync(ct);

        model.ClientNumber.Status.ShouldBe(ExtractionStatus.Extracted, "ClientNumber must be extracted");
        model.ClientNumber.Value.ShouldBe("00000002");
    }

    // -----------------------------------------------------------------------
    // Period / summary header fields — centre column ("Periodo:", "corte:", etc.)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Extract_RealLayout_PeriodStartExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, ps) = await LoadAsync(ct);

        ps.PeriodStart.Status.ShouldBe(ExtractionStatus.Extracted, "PeriodStart must be extracted");
        ps.PeriodStart.Value.ShouldBe(new DateOnly(2026, 3, 4));
    }

    [Fact]
    public async Task Extract_RealLayout_PeriodCutDateExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, ps) = await LoadAsync(ct);

        ps.PeriodCutDate.Status.ShouldBe(ExtractionStatus.Extracted, "PeriodCutDate must be extracted");
        ps.PeriodCutDate.Value.ShouldBe(new DateOnly(2026, 4, 1));
    }

    [Fact]
    public async Task Extract_RealLayout_DayCountExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, ps) = await LoadAsync(ct);

        ps.DayCountPrinted.Status.ShouldBe(ExtractionStatus.Extracted, "DayCountPrinted must be extracted");
        ps.DayCountPrinted.Value.ShouldBe(29);
    }

    [Fact]
    public async Task Extract_RealLayout_PagoMinimoExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, ps) = await LoadAsync(ct);

        ps.PagoMinimo.Status.ShouldBe(ExtractionStatus.Extracted, "PagoMinimo must be extracted");
        ps.PagoMinimo.Value.ShouldBe(980.00m);
    }

    // -----------------------------------------------------------------------
    // RESUMEN fields — left-column layout (X≈25, "label + $amount")
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Extract_RealLayout_CargosRegularesNoMesesExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, ps) = await LoadAsync(ct);

        ps.CargosRegularesNoMeses.Status.ShouldBe(ExtractionStatus.Extracted,
            "CargosRegularesNoMeses must be extracted");
        ps.CargosRegularesNoMeses.Value.ShouldBe(6479.43m);
    }

    [Fact]
    public async Task Extract_RealLayout_CargosComprasAMesesCapitalExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, ps) = await LoadAsync(ct);

        ps.CargosComprasAMesesCapital.Status.ShouldBe(ExtractionStatus.Extracted,
            "CargosComprasAMesesCapital must be extracted");
        ps.CargosComprasAMesesCapital.Value.ShouldBe(5582.90m);
    }

    [Fact]
    public async Task Extract_RealLayout_MontoInteresesExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, ps) = await LoadAsync(ct);

        ps.MontoIntereses.Status.ShouldBe(ExtractionStatus.Extracted,
            "MontoIntereses must be extracted");
        ps.MontoIntereses.Value.ShouldBe(488.82m);
    }

    [Fact]
    public async Task Extract_RealLayout_MontoComisionesExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, ps) = await LoadAsync(ct);

        ps.MontoComisiones.Status.ShouldBe(ExtractionStatus.Extracted,
            "MontoComisiones must be extracted");
        ps.MontoComisiones.Value.ShouldBe(0.00m);
    }

    [Fact]
    public async Task Extract_RealLayout_IvaInteresesYComisionesExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, ps) = await LoadAsync(ct);

        ps.IvaInteresesYComisiones.Status.ShouldBe(ExtractionStatus.Extracted,
            "IvaInteresesYComisiones must be extracted");
        ps.IvaInteresesYComisiones.Value.ShouldBe(53.40m);
    }

    // -----------------------------------------------------------------------
    // Already-working fields — regression guard
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Extract_RealLayout_PagoParaNoGenerarInteresesExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, ps) = await LoadAsync(ct);

        ps.PagoParaNoGenerarIntereses.Status.ShouldBe(ExtractionStatus.Extracted,
            "PagoParaNoGenerarIntereses must be extracted");
        ps.PagoParaNoGenerarIntereses.Value.ShouldBe(12604.55m);
    }

    [Fact]
    public async Task Extract_RealLayout_SaldoCargosRegularesExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, ps) = await LoadAsync(ct);

        ps.SaldoCargosRegulares.Status.ShouldBe(ExtractionStatus.Extracted,
            "SaldoCargosRegulares must be extracted");
        ps.SaldoCargosRegulares.Value.ShouldBe(12604.55m);
    }

    [Fact]
    public async Task Extract_RealLayout_SaldoCargosAMesesExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, ps) = await LoadAsync(ct);

        ps.SaldoCargosAMeses.Status.ShouldBe(ExtractionStatus.Extracted,
            "SaldoCargosAMeses must be extracted");
        ps.SaldoCargosAMeses.Value.ShouldBe(38604.69m);
    }

    [Fact]
    public async Task Extract_RealLayout_CreditoDisponibleExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, ps) = await LoadAsync(ct);

        ps.CreditoDisponible.Status.ShouldBe(ExtractionStatus.Extracted,
            "CreditoDisponible must be extracted");
        ps.CreditoDisponible.Value.ShouldBe(26791.00m);
    }
}
