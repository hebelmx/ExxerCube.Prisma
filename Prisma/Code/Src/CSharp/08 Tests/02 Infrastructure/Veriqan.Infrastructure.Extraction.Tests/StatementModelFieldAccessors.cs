using ExxerCube.Prisma.Veriqan.Domain.Extraction;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Explicit, reflection-free accessor map from a synthetic gold manifest field name
/// (the exact C# property name on <see cref="StatementModel"/> / <see cref="PeriodSummary"/>)
/// to a function that reads that field's (value, status) pair off a real
/// <see cref="StatementModel"/> produced by <see cref="PdfPigStatementFieldExtractor"/>.
/// </summary>
/// <remarks>
/// One entry per field covered by a synthetic manifest schema — see the design doc
/// <c>docs/planning-artifacts/E6-S6.2-synthetic-generator-design.md</c> §6.3. Adding coverage
/// for a new field is a one-line addition here plus a manifest entry, not a new test method.
/// </remarks>
internal static class StatementModelFieldAccessors
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
