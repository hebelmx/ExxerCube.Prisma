using System;

namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Structured period and summary fields extracted from a VEC statement PDF (Story 3.2).
/// Produced by the extraction pipeline and carried in <see cref="StatementModel.PeriodSummary"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every field is wrapped in <see cref="ExtractedField{T}"/> so callers can inspect
/// <c>Confidence</c>, <c>Locator</c>, and <c>Status</c> independently of the value.
/// Fields that are not found are represented as <see cref="ExtractionStatus.NotExtracted"/>
/// with a best-effort page-level locator hint — never silently absent.
/// </para>
/// <para>
/// <b>Day-count convention:</b> the <see cref="DayCountVerification"/> record uses the
/// <em>exclusive-end</em> convention: <c>ComputedSpanDays = PeriodCutDate − PeriodStart</c>
/// (i.e. count whole days from start up to but not including the cut date).
/// Example: 5-jul-2025 to 04-ago-2025 → <c>(2025-08-04 − 2025-07-05).TotalDays = 30</c>.
/// The fixture prints "31 días", which uses the <em>inclusive-end</em> convention
/// (<c>TotalDays + 1</c>).  The extractor stores the <em>printed</em> value in
/// <see cref="DayCountPrinted"/> and stores the computed span + the reconciliation flag
/// in <see cref="DayCount"/>.
/// </para>
/// <para>
/// <b>RESUMEN / NIVEL-DE-USO subtotals (Story 4.2):</b> the right-side block labelled
/// "RESUMEN DE CARGOS Y ABONOS DEL PERIODO" and the "NIVEL DE USO DE TU TARJETA" band
/// contribute nine additional subtotal fields used by the arithmetic validation rules
/// (CL-21, CL-22, CL-24, CL-25, CL-26).  These fields are extracted on page 1 at
/// Y ≈ 182–357; see <see cref="AdeudoPeriodoAnterior"/> through <see cref="SaldoCargosAMeses"/>.
/// </para>
/// </remarks>
public sealed class PeriodSummary
{
    /// <summary>
    /// Initializes a <see cref="PeriodSummary"/> with all period, summary, and RESUMEN subtotal fields.
    /// </summary>
    /// <param name="product">Product name / alias text as it appears on the statement.</param>
    /// <param name="periodStart">Start date of the billing period ("Periodo …").</param>
    /// <param name="periodCutDate">Cut date ("Fecha de Corte …").</param>
    /// <param name="paymentDueDate">Payment due date ("Fecha límite de pago …").</param>
    /// <param name="dayCountPrinted">Day-count as printed ("Número de días en el periodo: N").</param>
    /// <param name="dayCount">Day-count verification: computed span + printed vs. computed comparison.</param>
    /// <param name="pagoParaNoGenerarIntereses">Payment to avoid interest charges.</param>
    /// <param name="pagoMinimo">Minimum payment.</param>
    /// <param name="pagoMinimoMasMeses">Minimum payment plus MSI charges and deferred amounts.</param>
    /// <param name="tasa">Annual ordinary interest rate (TASA) as printed on the statement.</param>
    /// <param name="cat">CAT (Costo Anual Total) as printed on the statement.</param>
    /// <param name="saldoDeudorTotal">Total outstanding balance ("Saldo Deudor Total").</param>
    /// <param name="creditoDisponible">Available credit line ("Crédito Disponible").</param>
    /// <param name="adeudoPeriodoAnterior">
    /// Opening balance from the RESUMEN block ("Adeudo del periodo anterior =").
    /// Used by CL-21 arithmetic check.
    /// </param>
    /// <param name="cargosRegularesNoMeses">
    /// Regular charges excluding MSI ("Cargos regulares (no a meses) +").
    /// Used by CL-21 arithmetic check.
    /// </param>
    /// <param name="cargosComprasAMesesCapital">
    /// MSI installment capital portion ("Cargos compras a meses (capital) +").
    /// Used by CL-21 arithmetic check.
    /// </param>
    /// <param name="montoIntereses">
    /// Interest amount ("Monto de Intereses +").
    /// Used by CL-21 arithmetic check.
    /// </param>
    /// <param name="montoComisiones">
    /// Commissions/fees amount ("Monto de comisiones +").
    /// Used by CL-21 arithmetic check.
    /// </param>
    /// <param name="ivaInteresesYComisiones">
    /// VAT on interest and fees ("IVA de Intereses y comisiones +").
    /// Used by CL-21 arithmetic check.
    /// </param>
    /// <param name="pagosYAbonos">
    /// Total payments and credits ("Pagos y abonos -").
    /// Used by CL-21 arithmetic check.
    /// </param>
    /// <param name="saldoCargosRegulares">
    /// Regular-charges balance from the NIVEL-DE-USO block ("Saldo cargos regulares:").
    /// Used by CL-22 check.
    /// </param>
    /// <param name="saldoCargosAMeses">
    /// MSI-charges balance from the NIVEL-DE-USO block ("Saldo cargos a meses:").
    /// Used by CL-24 arithmetic check.
    /// </param>
    public PeriodSummary(
        ExtractedField<string> product,
        ExtractedField<DateOnly> periodStart,
        ExtractedField<DateOnly> periodCutDate,
        ExtractedField<DateOnly> paymentDueDate,
        ExtractedField<int> dayCountPrinted,
        DayCountVerification dayCount,
        ExtractedField<decimal> pagoParaNoGenerarIntereses,
        ExtractedField<decimal> pagoMinimo,
        ExtractedField<decimal> pagoMinimoMasMeses,
        ExtractedField<decimal> tasa,
        ExtractedField<decimal> cat,
        ExtractedField<decimal> saldoDeudorTotal,
        ExtractedField<decimal> creditoDisponible,
        ExtractedField<decimal>? adeudoPeriodoAnterior = null,
        ExtractedField<decimal>? cargosRegularesNoMeses = null,
        ExtractedField<decimal>? cargosComprasAMesesCapital = null,
        ExtractedField<decimal>? montoIntereses = null,
        ExtractedField<decimal>? montoComisiones = null,
        ExtractedField<decimal>? ivaInteresesYComisiones = null,
        ExtractedField<decimal>? pagosYAbonos = null,
        ExtractedField<decimal>? saldoCargosRegulares = null,
        ExtractedField<decimal>? saldoCargosAMeses = null)
    {
        Product = product ?? throw new ArgumentNullException(nameof(product));
        PeriodStart = periodStart ?? throw new ArgumentNullException(nameof(periodStart));
        PeriodCutDate = periodCutDate ?? throw new ArgumentNullException(nameof(periodCutDate));
        PaymentDueDate = paymentDueDate ?? throw new ArgumentNullException(nameof(paymentDueDate));
        DayCountPrinted = dayCountPrinted ?? throw new ArgumentNullException(nameof(dayCountPrinted));
        DayCount = dayCount ?? throw new ArgumentNullException(nameof(dayCount));
        PagoParaNoGenerarIntereses = pagoParaNoGenerarIntereses ?? throw new ArgumentNullException(nameof(pagoParaNoGenerarIntereses));
        PagoMinimo = pagoMinimo ?? throw new ArgumentNullException(nameof(pagoMinimo));
        PagoMinimoMasMeses = pagoMinimoMasMeses ?? throw new ArgumentNullException(nameof(pagoMinimoMasMeses));
        Tasa = tasa ?? throw new ArgumentNullException(nameof(tasa));
        Cat = cat ?? throw new ArgumentNullException(nameof(cat));
        SaldoDeudorTotal = saldoDeudorTotal ?? throw new ArgumentNullException(nameof(saldoDeudorTotal));
        CreditoDisponible = creditoDisponible ?? throw new ArgumentNullException(nameof(creditoDisponible));
        AdeudoPeriodoAnterior = adeudoPeriodoAnterior ?? ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
        CargosRegularesNoMeses = cargosRegularesNoMeses ?? ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
        CargosComprasAMesesCapital = cargosComprasAMesesCapital ?? ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
        MontoIntereses = montoIntereses ?? ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
        MontoComisiones = montoComisiones ?? ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
        IvaInteresesYComisiones = ivaInteresesYComisiones ?? ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
        PagosYAbonos = pagosYAbonos ?? ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
        SaldoCargosRegulares = saldoCargosRegulares ?? ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
        SaldoCargosAMeses = saldoCargosAMeses ?? ExtractedField<decimal>.Missing(FieldLocator.PageHint(1));
    }

    // -----------------------------------------------------------------------
    // Product identification
    // -----------------------------------------------------------------------

    /// <summary>
    /// Product name / alias as it appears on the statement (e.g. "Tarjeta de Crédito BSSB").
    /// Used to confirm the product identity against the reference bundle.
    /// </summary>
    public ExtractedField<string> Product { get; }

    // -----------------------------------------------------------------------
    // Period dates
    // -----------------------------------------------------------------------

    /// <summary>
    /// Start date of the billing period ("Periodo &lt;start&gt; al &lt;end&gt;").
    /// Parsed from Spanish format, e.g. "5-jul-2025" → <c>new DateOnly(2025, 7, 5)</c>.
    /// </summary>
    public ExtractedField<DateOnly> PeriodStart { get; }

    /// <summary>
    /// Cut date ("Fecha de Corte …").
    /// Parsed from Spanish format, e.g. "04 de ago 2025" → <c>new DateOnly(2025, 8, 4)</c>.
    /// </summary>
    public ExtractedField<DateOnly> PeriodCutDate { get; }

    /// <summary>
    /// Payment due date ("Fecha límite de pago …").
    /// Parsed from Spanish format that may include a day-name prefix,
    /// e.g. "lunes, 25-ago-2025" → <c>new DateOnly(2025, 8, 25)</c>.
    /// </summary>
    public ExtractedField<DateOnly> PaymentDueDate { get; }

    // -----------------------------------------------------------------------
    // Day count
    // -----------------------------------------------------------------------

    /// <summary>
    /// Day count as literally printed in the statement ("Número de días en el periodo: N").
    /// The printed count uses the <em>inclusive-end</em> convention: the fixture prints 31
    /// for the period 5-jul to 04-ago-2025.
    /// </summary>
    public ExtractedField<int> DayCountPrinted { get; }

    /// <summary>
    /// Day-count verification result: computed span (exclusive-end), printed value,
    /// and whether the two reconcile under the inclusive-end convention (printed = span + 1).
    /// </summary>
    public DayCountVerification DayCount { get; }

    // -----------------------------------------------------------------------
    // Summary amounts
    // -----------------------------------------------------------------------

    /// <summary>
    /// Payment required to avoid interest charges ("Pago para no generar intereses").
    /// e.g. $32,446.69 → <c>32446.69m</c>.
    /// </summary>
    public ExtractedField<decimal> PagoParaNoGenerarIntereses { get; }

    /// <summary>
    /// Minimum payment due ("Pago mínimo").
    /// e.g. $2,160.00 → <c>2160.00m</c>.
    /// </summary>
    public ExtractedField<decimal> PagoMinimo { get; }

    /// <summary>
    /// Minimum payment plus MSI (meses sin intereses) charges and deferred amounts
    /// ("Pago mínimo + compras y cargos diferidos a meses").
    /// e.g. $3,145.39 → <c>3145.39m</c>.
    /// </summary>
    public ExtractedField<decimal> PagoMinimoMasMeses { get; }

    // -----------------------------------------------------------------------
    // Rate / financial metadata
    // -----------------------------------------------------------------------

    /// <summary>
    /// Annual ordinary interest rate (TASA) as printed on the statement, as a decimal fraction.
    /// e.g. "19.75%" → <c>0.1975m</c>.
    /// <see cref="ExtractionStatus.NotExtracted"/> if the TASA label is not found.
    /// </summary>
    public ExtractedField<decimal> Tasa { get; }

    /// <summary>
    /// CAT (Costo Anual Total) as printed on the statement, as a decimal fraction.
    /// e.g. "26.00%" → <c>0.26m</c>.
    /// <see cref="ExtractionStatus.NotExtracted"/> if the CAT label is not found.
    /// </summary>
    public ExtractedField<decimal> Cat { get; }

    /// <summary>
    /// Total outstanding balance ("Saldo Deudor Total") in MXN.
    /// <see cref="ExtractionStatus.NotExtracted"/> if the label is not found.
    /// </summary>
    public ExtractedField<decimal> SaldoDeudorTotal { get; }

    /// <summary>
    /// Available credit line ("Crédito Disponible") in MXN.
    /// <see cref="ExtractionStatus.NotExtracted"/> if the label is not found.
    /// </summary>
    public ExtractedField<decimal> CreditoDisponible { get; }

    // -----------------------------------------------------------------------
    // RESUMEN DE CARGOS Y ABONOS DEL PERIODO subtotals (Story 4.2)
    // Right-side block at Y ≈ 291–357 on page 1.
    // Used by CL-21, CL-22, CL-24, CL-25, CL-26 arithmetic validation rules.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Opening balance carried forward from the prior period
    /// ("Adeudo del periodo anterior ="), right-side RESUMEN block, Y ≈ 357.
    /// <see cref="ExtractionStatus.NotExtracted"/> if not found.
    /// Used by CL-21.
    /// </summary>
    public ExtractedField<decimal> AdeudoPeriodoAnterior { get; }

    /// <summary>
    /// Charges excluding MSI purchases ("Cargos regulares (no a meses) +"),
    /// right-side RESUMEN block, Y ≈ 346.
    /// <see cref="ExtractionStatus.NotExtracted"/> if not found.
    /// Used by CL-21.
    /// </summary>
    public ExtractedField<decimal> CargosRegularesNoMeses { get; }

    /// <summary>
    /// MSI installment capital portion for this period
    /// ("Cargos compras a meses (capital) +"),
    /// right-side RESUMEN block, Y ≈ 335.
    /// <see cref="ExtractionStatus.NotExtracted"/> if not found.
    /// Used by CL-21.
    /// </summary>
    public ExtractedField<decimal> CargosComprasAMesesCapital { get; }

    /// <summary>
    /// Interest amount charged this period ("Monto de Intereses +"),
    /// right-side RESUMEN block, Y ≈ 324.
    /// <see cref="ExtractionStatus.NotExtracted"/> if not found.
    /// Used by CL-21.
    /// </summary>
    public ExtractedField<decimal> MontoIntereses { get; }

    /// <summary>
    /// Commissions / fees amount ("Monto de comisiones +"),
    /// right-side RESUMEN block, Y ≈ 313.
    /// <see cref="ExtractionStatus.NotExtracted"/> if not found.
    /// Used by CL-21.
    /// </summary>
    public ExtractedField<decimal> MontoComisiones { get; }

    /// <summary>
    /// VAT on interest and commissions ("IVA de Intereses y comisiones +"),
    /// right-side RESUMEN block, Y ≈ 303.
    /// <see cref="ExtractionStatus.NotExtracted"/> if not found.
    /// Used by CL-21.
    /// </summary>
    public ExtractedField<decimal> IvaInteresesYComisiones { get; }

    /// <summary>
    /// Total payments and credits applied this period ("Pagos y abonos -"),
    /// right-side RESUMEN block, Y ≈ 292.
    /// <see cref="ExtractionStatus.NotExtracted"/> if not found.
    /// Used by CL-21.
    /// </summary>
    public ExtractedField<decimal> PagosYAbonos { get; }

    // -----------------------------------------------------------------------
    // NIVEL DE USO DE TU TARJETA subtotals (Story 4.2)
    // Right-side block at Y ≈ 171–183 on page 1.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Running balance of regular charges for this period
    /// ("Saldo cargos regulares:"), NIVEL-DE-USO block, Y ≈ 183.
    /// <see cref="ExtractionStatus.NotExtracted"/> if not found.
    /// Used by CL-22.
    /// </summary>
    public ExtractedField<decimal> SaldoCargosRegulares { get; }

    /// <summary>
    /// Running balance of MSI-charges for this period
    /// ("Saldo cargos a meses:"), NIVEL-DE-USO block, Y ≈ 172.
    /// <see cref="ExtractionStatus.NotExtracted"/> if not found.
    /// Used by CL-24.
    /// </summary>
    public ExtractedField<decimal> SaldoCargosAMeses { get; }
}
