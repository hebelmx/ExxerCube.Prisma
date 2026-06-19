using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Story 4.2 extraction tests for the RESUMEN DE CARGOS Y ABONOS DEL PERIODO
/// and NIVEL DE USO DE TU TARJETA subtotal fields added to
/// <see cref="PeriodSummary"/> to support arithmetic validation rules
/// CL-21, CL-22, CL-24, CL-25, and CL-26.
/// </summary>
/// <remarks>
/// All expected values are calibrated from fixture
/// <c>01+Dummie+VEC+jul_ago+20252.pdf</c> (jul-ago 2025).
/// The fixture arithmetic cross-checks:
/// <list type="bullet">
///   <item><description>
///     CL-21: 67,796.35 + 31,461.30 + 985.39 + 0.00 + 0.00 + 0.00 − 67,796.35 = 32,446.69
///     (matches PagoParaNoGenerarIntereses).
///   </description></item>
///   <item><description>
///     CL-22: SaldoCargosRegulares (32,446.69) = PagoParaNoGenerarIntereses (32,446.69).
///   </description></item>
///   <item><description>
///     CL-24: SaldoCargosRegulares (32,446.69) + SaldoCargosAMeses (19,941.16)
///            = SaldoDeudorTotal (52,387.85).
///   </description></item>
///   <item><description>
///     CL-25: 100,000 (creditLine) − 52,387.85 = 47,612.15 (CreditoDisponible).
///   </description></item>
/// </list>
/// </remarks>
public sealed class PdfPigStatementFieldExtractorResumenTests
{
    // -----------------------------------------------------------------------
    // Fixture paths
    // -----------------------------------------------------------------------

    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FixturePath(string fileName) =>
        Path.Combine(FixturesDir, fileName);

    private static readonly string JulAgoFixture =
        FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");

    // -----------------------------------------------------------------------
    // Factory helpers
    // -----------------------------------------------------------------------

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(), Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()), new NullPasswordProvider());

    private static async Task<PeriodSummary> GetJulAgoPeriodSummaryAsync(CancellationToken ct)
    {
        var extractor = CreateExtractor();
        File.Exists(JulAgoFixture).ShouldBeTrue($"Fixture not found: {JulAgoFixture}");
        var pdf = File.ReadAllBytes(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);
        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync must succeed. Error: {result.Error}");

        var model = result.Value!;
        model.PeriodSummary.ShouldNotBeNull("PeriodSummary must be populated");
        return model.PeriodSummary!;
    }

    // -----------------------------------------------------------------------
    // Test S4.2-Ext-1: RESUMEN subtotals are extracted with correct values
    // -----------------------------------------------------------------------

    /// <summary>
    /// All seven RESUMEN subtotal fields must be extracted from fixture #1 with
    /// the expected values identified during PDF layout calibration.
    /// </summary>
    [Fact]
    public async Task Extract_DummieVec_ResumenSubtotalsExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var ps = await GetJulAgoPeriodSummaryAsync(ct);

        // AdeudoPeriodoAnterior = $67,796.35
        ps.AdeudoPeriodoAnterior.Status.ShouldBe(
            ExtractionStatus.Extracted, "AdeudoPeriodoAnterior must be extracted");
        ps.AdeudoPeriodoAnterior.Value.ShouldBe(
            67796.35m, "AdeudoPeriodoAnterior fixture value must be $67,796.35");

        // CargosRegularesNoMeses = $31,461.30
        ps.CargosRegularesNoMeses.Status.ShouldBe(
            ExtractionStatus.Extracted, "CargosRegularesNoMeses must be extracted");
        ps.CargosRegularesNoMeses.Value.ShouldBe(
            31461.30m, "CargosRegularesNoMeses fixture value must be $31,461.30");

        // CargosComprasAMesesCapital = $985.39
        ps.CargosComprasAMesesCapital.Status.ShouldBe(
            ExtractionStatus.Extracted, "CargosComprasAMesesCapital must be extracted");
        ps.CargosComprasAMesesCapital.Value.ShouldBe(
            985.39m, "CargosComprasAMesesCapital fixture value must be $985.39");

        // MontoIntereses = $0.00
        ps.MontoIntereses.Status.ShouldBe(
            ExtractionStatus.Extracted, "MontoIntereses must be extracted");
        ps.MontoIntereses.Value.ShouldBe(
            0.00m, "MontoIntereses fixture value must be $0.00");

        // MontoComisiones = $0.00
        ps.MontoComisiones.Status.ShouldBe(
            ExtractionStatus.Extracted, "MontoComisiones must be extracted");
        ps.MontoComisiones.Value.ShouldBe(
            0.00m, "MontoComisiones fixture value must be $0.00");

        // IvaInteresesYComisiones = $0.00
        ps.IvaInteresesYComisiones.Status.ShouldBe(
            ExtractionStatus.Extracted, "IvaInteresesYComisiones must be extracted");
        ps.IvaInteresesYComisiones.Value.ShouldBe(
            0.00m, "IvaInteresesYComisiones fixture value must be $0.00");

        // PagosYAbonos = $67,796.35
        ps.PagosYAbonos.Status.ShouldBe(
            ExtractionStatus.Extracted, "PagosYAbonos must be extracted");
        ps.PagosYAbonos.Value.ShouldBe(
            67796.35m, "PagosYAbonos fixture value must be $67,796.35");
    }

    // -----------------------------------------------------------------------
    // Test S4.2-Ext-2: NIVEL-DE-USO subtotals are extracted with correct values
    // -----------------------------------------------------------------------

    /// <summary>
    /// The two NIVEL-DE-USO subtotal fields must be extracted from fixture #1
    /// with the expected values: SaldoCargosRegulares = $32,446.69 and
    /// SaldoCargosAMeses = $19,941.16.
    /// </summary>
    [Fact]
    public async Task Extract_DummieVec_NivelDeUsoSubtotalsExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var ps = await GetJulAgoPeriodSummaryAsync(ct);

        // SaldoCargosRegulares = $32,446.69
        ps.SaldoCargosRegulares.Status.ShouldBe(
            ExtractionStatus.Extracted, "SaldoCargosRegulares must be extracted");
        ps.SaldoCargosRegulares.Value.ShouldBe(
            32446.69m, "SaldoCargosRegulares fixture value must be $32,446.69");

        // SaldoCargosAMeses = $19,941.16
        ps.SaldoCargosAMeses.Status.ShouldBe(
            ExtractionStatus.Extracted, "SaldoCargosAMeses must be extracted");
        ps.SaldoCargosAMeses.Value.ShouldBe(
            19941.16m, "SaldoCargosAMeses fixture value must be $19,941.16");
    }

    // -----------------------------------------------------------------------
    // Test S4.2-Ext-3: All new fields have non-null locators (no silent blanks)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Every new Story-4.2 field must carry a non-null Locator and a
    /// non-negative Confidence, whether extracted or not.
    /// </summary>
    [Fact]
    public async Task Extract_DummieVec_AllNewFieldsHaveLocatorAndConfidence()
    {
        var ct = TestContext.Current.CancellationToken;
        var ps = await GetJulAgoPeriodSummaryAsync(ct);

        AssertField(ps.AdeudoPeriodoAnterior, nameof(ps.AdeudoPeriodoAnterior));
        AssertField(ps.CargosRegularesNoMeses, nameof(ps.CargosRegularesNoMeses));
        AssertField(ps.CargosComprasAMesesCapital, nameof(ps.CargosComprasAMesesCapital));
        AssertField(ps.MontoIntereses, nameof(ps.MontoIntereses));
        AssertField(ps.MontoComisiones, nameof(ps.MontoComisiones));
        AssertField(ps.IvaInteresesYComisiones, nameof(ps.IvaInteresesYComisiones));
        AssertField(ps.PagosYAbonos, nameof(ps.PagosYAbonos));
        AssertField(ps.SaldoCargosRegulares, nameof(ps.SaldoCargosRegulares));
        AssertField(ps.SaldoCargosAMeses, nameof(ps.SaldoCargosAMeses));
    }

    // -----------------------------------------------------------------------
    // Test S4.2-Ext-4: CL-21 arithmetic cross-check on extracted values
    // -----------------------------------------------------------------------

    /// <summary>
    /// The CL-21 formula applied to the extracted subtotals must produce a value
    /// that equals the extracted PagoParaNoGenerarIntereses within $0.50.
    /// Formula: AdeudoPeriodoAnterior + CargosRegularesNoMeses + CargosComprasAMesesCapital
    ///          + MontoIntereses + MontoComisiones + IvaInteresesYComisiones − PagosYAbonos.
    /// </summary>
    [Fact]
    public async Task Extract_DummieVec_Cl21FormulaMatchesExtractedPago()
    {
        var ct = TestContext.Current.CancellationToken;
        var ps = await GetJulAgoPeriodSummaryAsync(ct);

        // All components must be extracted to run this cross-check.
        ps.AdeudoPeriodoAnterior.Status.ShouldBe(ExtractionStatus.Extracted);
        ps.CargosRegularesNoMeses.Status.ShouldBe(ExtractionStatus.Extracted);
        ps.CargosComprasAMesesCapital.Status.ShouldBe(ExtractionStatus.Extracted);
        ps.MontoIntereses.Status.ShouldBe(ExtractionStatus.Extracted);
        ps.MontoComisiones.Status.ShouldBe(ExtractionStatus.Extracted);
        ps.IvaInteresesYComisiones.Status.ShouldBe(ExtractionStatus.Extracted);
        ps.PagosYAbonos.Status.ShouldBe(ExtractionStatus.Extracted);
        ps.PagoParaNoGenerarIntereses.Status.ShouldBe(ExtractionStatus.Extracted);

        var computed =
            ps.AdeudoPeriodoAnterior.Value
            + ps.CargosRegularesNoMeses.Value
            + ps.CargosComprasAMesesCapital.Value
            + ps.MontoIntereses.Value
            + ps.MontoComisiones.Value
            + ps.IvaInteresesYComisiones.Value
            - ps.PagosYAbonos.Value;

        var pagoExtracted = ps.PagoParaNoGenerarIntereses.Value;
        var difference = Math.Abs(computed - pagoExtracted);

        difference.ShouldBeLessThanOrEqualTo(0.50m,
            $"CL-21 computed {computed:F2} vs extracted PagoParaNoGenerarIntereses " +
            $"{pagoExtracted:F2}, diff={difference:F2} exceeds $0.50");
    }

    // -----------------------------------------------------------------------
    // Test S4.2-Ext-5: Regression — original 33 fields still work after new additions
    // -----------------------------------------------------------------------

    /// <summary>
    /// After Story-4.2 additions the original extraction fields must still yield
    /// their expected values from fixture #1 (regression guard).
    /// </summary>
    [Fact]
    public async Task Extract_DummieVec_OriginalFieldsUnchangedAfterStory42()
    {
        var ct = TestContext.Current.CancellationToken;
        var ps = await GetJulAgoPeriodSummaryAsync(ct);

        // Core summary amounts unchanged
        ps.PagoParaNoGenerarIntereses.Value.ShouldBe(32446.69m, "PagoParaNoGenerarIntereses regression");
        ps.PagoMinimo.Value.ShouldBe(2160.00m, "PagoMinimo regression");
        ps.SaldoDeudorTotal.Value.ShouldBe(52387.85m, "SaldoDeudorTotal regression");
        ps.CreditoDisponible.Value.ShouldBe(47612.15m, "CreditoDisponible regression");
    }

    // -----------------------------------------------------------------------
    // Assertion helper
    // -----------------------------------------------------------------------

    private static void AssertField<T>(ExtractedField<T> field, string name)
    {
        field.ShouldNotBeNull($"{name} must not be null");
        field.Locator.ShouldNotBeNull($"{name}.Locator must not be null");
        field.Locator.PageNumber.ShouldBeGreaterThan(0, $"{name}.Locator.PageNumber must be >= 1");
        field.Confidence.ShouldBeGreaterThanOrEqualTo(0.0, $"{name}.Confidence must be >= 0");
        field.Confidence.ShouldBeLessThanOrEqualTo(1.0, $"{name}.Confidence must be <= 1");
    }
}
