namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// A single row in a <see cref="FinancialTable"/>, composed of a label cell and
/// an ordered list of value cells.
/// </summary>
/// <param name="Label">The row label (leftmost column text, e.g. "Ordinarios").</param>
/// <param name="Values">
/// Value cells in column order. Count and column semantics are defined by the
/// table that contains this row (see <see cref="FinancialTable.SectionNumber"/>).
/// </param>
public sealed record TableRow(TableCell Label, IReadOnlyList<TableCell> Values);
