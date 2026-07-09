using ExxerCube.Prisma.Veriqan.Domain.Extraction;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Honesty;

/// <summary>
/// Explicit, reflection-free accessor map from a synthetic gold manifest field name to a
/// function that reads that field's (value, status) pair off a real <see cref="StatementModel"/>.
/// </summary>
/// <remarks>
/// Local counterpart to <c>Veriqan.Infrastructure.Extraction.Tests/StatementModelFieldAccessors.cs</c>
/// (see the S3.1 structural note in <c>SyntheticGoldSupport.cs</c> for why this is a small local
/// copy rather than a shared type). Every entry here is a <b>verdict-gating field</b> per the
/// design doc (<c>docs/planning-artifacts/veriqan-fallback-chain-design-2026-07-05.md</c>
/// §"Eval, honesty &amp; determinism testing"): Tasa, Cat, and the RESUMEN/NIVEL-DE-USO monetary
/// totals feed CL-21/CL-22/CL-24 arithmetic checks directly, so a fabricated value here can flip
/// a verdict. <c>PaymentDueDate</c> is also nominally verdict-gating per the design doc, but no
/// synthetic specimen manifest currently tracks its expected status (only the RESUMEN-block and
/// rate fields are gold-tracked), so it is intentionally excluded from this data-driven map —
/// see the S3.1 return report for this gap.
/// </remarks>
internal static class VerdictGatingFieldAccessors
{
    public static readonly IReadOnlyDictionary<string, Func<StatementModel, (object? Value, ExtractionStatus Status)>> Map =
        new Dictionary<string, Func<StatementModel, (object?, ExtractionStatus)>>(StringComparer.Ordinal)
        {
            ["Tasa"] = m => (m.PeriodSummary?.Tasa.Value, m.PeriodSummary?.Tasa.Status ?? ExtractionStatus.NotExtracted),
            ["Cat"] = m => (m.PeriodSummary?.Cat.Value, m.PeriodSummary?.Cat.Status ?? ExtractionStatus.NotExtracted),
            ["PagoParaNoGenerarIntereses"] = m => (
                m.PeriodSummary?.PagoParaNoGenerarIntereses.Value,
                m.PeriodSummary?.PagoParaNoGenerarIntereses.Status ?? ExtractionStatus.NotExtracted),
            ["AdeudoPeriodoAnterior"] = m => (
                m.PeriodSummary?.AdeudoPeriodoAnterior.Value,
                m.PeriodSummary?.AdeudoPeriodoAnterior.Status ?? ExtractionStatus.NotExtracted),
            ["CargosRegularesNoMeses"] = m => (
                m.PeriodSummary?.CargosRegularesNoMeses.Value,
                m.PeriodSummary?.CargosRegularesNoMeses.Status ?? ExtractionStatus.NotExtracted),
            ["CargosComprasAMesesCapital"] = m => (
                m.PeriodSummary?.CargosComprasAMesesCapital.Value,
                m.PeriodSummary?.CargosComprasAMesesCapital.Status ?? ExtractionStatus.NotExtracted),
            ["MontoIntereses"] = m => (
                m.PeriodSummary?.MontoIntereses.Value,
                m.PeriodSummary?.MontoIntereses.Status ?? ExtractionStatus.NotExtracted),
            ["MontoComisiones"] = m => (
                m.PeriodSummary?.MontoComisiones.Value,
                m.PeriodSummary?.MontoComisiones.Status ?? ExtractionStatus.NotExtracted),
            ["IvaInteresesYComisiones"] = m => (
                m.PeriodSummary?.IvaInteresesYComisiones.Value,
                m.PeriodSummary?.IvaInteresesYComisiones.Status ?? ExtractionStatus.NotExtracted),
            ["PagosYAbonos"] = m => (
                m.PeriodSummary?.PagosYAbonos.Value,
                m.PeriodSummary?.PagosYAbonos.Status ?? ExtractionStatus.NotExtracted),
            ["SaldoCargosRegulares"] = m => (
                m.PeriodSummary?.SaldoCargosRegulares.Value,
                m.PeriodSummary?.SaldoCargosRegulares.Status ?? ExtractionStatus.NotExtracted),
            ["SaldoCargosAMeses"] = m => (
                m.PeriodSummary?.SaldoCargosAMeses.Value,
                m.PeriodSummary?.SaldoCargosAMeses.Status ?? ExtractionStatus.NotExtracted),
            ["SaldoDeudorTotal"] = m => (
                m.PeriodSummary?.SaldoDeudorTotal.Value,
                m.PeriodSummary?.SaldoDeudorTotal.Status ?? ExtractionStatus.NotExtracted),
            ["CreditoDisponible"] = m => (
                m.PeriodSummary?.CreditoDisponible.Value,
                m.PeriodSummary?.CreditoDisponible.Status ?? ExtractionStatus.NotExtracted),
            ["TotalCargos"] = m => (
                m.PeriodSummary?.TotalCargos.Value,
                m.PeriodSummary?.TotalCargos.Status ?? ExtractionStatus.NotExtracted),
            ["TotalAbonos"] = m => (
                m.PeriodSummary?.TotalAbonos.Value,
                m.PeriodSummary?.TotalAbonos.Status ?? ExtractionStatus.NotExtracted),
        };
}
