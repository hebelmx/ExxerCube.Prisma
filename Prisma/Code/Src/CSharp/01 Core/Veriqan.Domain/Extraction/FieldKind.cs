namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Enumerates every extractable statement field, so that a per-field escalation ladder
/// (see <c>docs/planning-artifacts/veriqan-fallback-chain-design-2026-07-05.md</c>) can be
/// keyed on a stable identifier instead of a property-reflection lookup.
/// </summary>
/// <remarks>
/// Mirrors, one-to-one, the header fields declared on <see cref="StatementModel"/>'s
/// constructor and the period/summary fields declared on <see cref="PeriodSummary"/>'s
/// constructor, plus the <see cref="StatementModel.Movements"/> table. This enum is
/// purely additive vocabulary as of E1 — no stage or orchestrator consumes it yet.
/// </remarks>
public enum FieldKind
{
    // -----------------------------------------------------------------------
    // Header — identity fields (StatementModel)
    // -----------------------------------------------------------------------

    /// <summary>Corresponds to <see cref="StatementModel.ClientName"/>.</summary>
    ClientName,

    /// <summary>Corresponds to <see cref="StatementModel.Address"/>.</summary>
    Address,

    /// <summary>Corresponds to <see cref="StatementModel.BranchNumber"/>.</summary>
    BranchNumber,

    /// <summary>Corresponds to <see cref="StatementModel.CardNumber"/>.</summary>
    CardNumber,

    /// <summary>Corresponds to <see cref="StatementModel.Clabe"/>.</summary>
    Clabe,

    /// <summary>Corresponds to <see cref="StatementModel.ClientNumber"/>.</summary>
    ClientNumber,

    /// <summary>Corresponds to <see cref="StatementModel.Rfc"/>.</summary>
    Rfc,

    // -----------------------------------------------------------------------
    // Period / summary fields (PeriodSummary)
    // -----------------------------------------------------------------------

    /// <summary>Corresponds to <see cref="PeriodSummary.Product"/>.</summary>
    Product,

    /// <summary>Corresponds to <see cref="PeriodSummary.PeriodStart"/>.</summary>
    PeriodStart,

    /// <summary>Corresponds to <see cref="PeriodSummary.PeriodCutDate"/>.</summary>
    PeriodCutDate,

    /// <summary>Corresponds to <see cref="PeriodSummary.PaymentDueDate"/>.</summary>
    PaymentDueDate,

    /// <summary>Corresponds to <see cref="PeriodSummary.DayCountPrinted"/>.</summary>
    DayCountPrinted,

    /// <summary>Corresponds to <see cref="PeriodSummary.PagoParaNoGenerarIntereses"/>.</summary>
    PagoParaNoGenerarIntereses,

    /// <summary>Corresponds to <see cref="PeriodSummary.PagoMinimo"/>.</summary>
    PagoMinimo,

    /// <summary>Corresponds to <see cref="PeriodSummary.PagoMinimoMasMeses"/>.</summary>
    PagoMinimoMasMeses,

    /// <summary>Corresponds to <see cref="PeriodSummary.Tasa"/>.</summary>
    Tasa,

    /// <summary>Corresponds to <see cref="PeriodSummary.Cat"/>.</summary>
    Cat,

    /// <summary>Corresponds to <see cref="PeriodSummary.SaldoDeudorTotal"/>.</summary>
    SaldoDeudorTotal,

    /// <summary>Corresponds to <see cref="PeriodSummary.CreditoDisponible"/>.</summary>
    CreditoDisponible,

    /// <summary>Corresponds to <see cref="PeriodSummary.AdeudoPeriodoAnterior"/>.</summary>
    AdeudoPeriodoAnterior,

    /// <summary>Corresponds to <see cref="PeriodSummary.CargosRegularesNoMeses"/>.</summary>
    CargosRegularesNoMeses,

    /// <summary>Corresponds to <see cref="PeriodSummary.CargosComprasAMesesCapital"/>.</summary>
    CargosComprasAMesesCapital,

    /// <summary>Corresponds to <see cref="PeriodSummary.MontoIntereses"/>.</summary>
    MontoIntereses,

    /// <summary>Corresponds to <see cref="PeriodSummary.MontoComisiones"/>.</summary>
    MontoComisiones,

    /// <summary>Corresponds to <see cref="PeriodSummary.IvaInteresesYComisiones"/>.</summary>
    IvaInteresesYComisiones,

    /// <summary>Corresponds to <see cref="PeriodSummary.PagosYAbonos"/>.</summary>
    PagosYAbonos,

    /// <summary>Corresponds to <see cref="PeriodSummary.SaldoCargosRegulares"/>.</summary>
    SaldoCargosRegulares,

    /// <summary>Corresponds to <see cref="PeriodSummary.SaldoCargosAMeses"/>.</summary>
    SaldoCargosAMeses,

    /// <summary>Corresponds to <see cref="PeriodSummary.TotalCargos"/>.</summary>
    TotalCargos,

    /// <summary>Corresponds to <see cref="PeriodSummary.TotalAbonos"/>.</summary>
    TotalAbonos,

    // -----------------------------------------------------------------------
    // DESGLOSE DE MOVIMIENTOS DEL PERIODO — transaction table (StatementModel)
    // -----------------------------------------------------------------------

    /// <summary>Corresponds to the <see cref="StatementModel.Movements"/> table as a whole.</summary>
    Movements,
}
