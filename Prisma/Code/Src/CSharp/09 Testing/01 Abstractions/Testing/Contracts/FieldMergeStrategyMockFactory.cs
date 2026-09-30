using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance
/// of <see cref="FieldMergeStrategyContract"/>.
/// </summary>
/// <remarks>
/// <para>
/// This factory centralizes what the original standalone blueprint
/// (<c>IFieldMergeStrategyContractTests</c>) configured inline per test. Because the
/// contract asserts real merge outcomes (combination, conflict detection,
/// deduplication), simple canned stubs cannot satisfy it — the configuration has
/// evolved into a small <em>reference fake</em> of the documented merge semantics
/// (first-wins scalars, conflict reporting, deduplicated collections), exactly the
/// evolution ADR-005 §6 expects.
/// </para>
/// </remarks>
public static class FieldMergeStrategyMockFactory
{
    /// <summary>
    /// Creates an <see cref="IFieldMergeStrategy"/> mock that satisfies every test in
    /// <see cref="FieldMergeStrategyContract"/>.
    /// </summary>
    /// <returns>The configured mock.</returns>
    public static IFieldMergeStrategy CreateContractConformingMock()
    {
        var mock = Substitute.For<IFieldMergeStrategy>();

        mock.MergeAsync(Arg.Any<IReadOnlyList<ExtractedFields?>>(), Arg.Any<CancellationToken>())
            .Returns(call => Merge(
                call.ArgAt<IReadOnlyList<ExtractedFields?>>(0),
                call.ArgAt<CancellationToken>(1)));

        mock.MergeAsync(Arg.Any<ExtractedFields?>(), Arg.Any<ExtractedFields?>(), Arg.Any<CancellationToken>())
            .Returns(call => Merge(
                new List<ExtractedFields?> { call.ArgAt<ExtractedFields?>(0), call.ArgAt<ExtractedFields?>(1) },
                call.ArgAt<CancellationToken>(2)));

        return mock;
    }

    private static MergeResult Merge(IReadOnlyList<ExtractedFields?> fieldSets, CancellationToken cancellationToken)
    {
        // Contract: a pre-cancelled token surfaces as OperationCanceledException
        // (MergeResult has no cancellation channel).
        cancellationToken.ThrowIfCancellationRequested();

        var result = new MergeResult();
        var nonNullSets = fieldSets.Where(f => f != null).Cast<ExtractedFields>().ToList();
        result.SourceCount = nonNullSets.Count;

        if (nonNullSets.Count == 0)
        {
            return result;
        }

        // Contract: first non-null value wins per scalar field; >1 distinct value = conflict.
        MergeScalar(nonNullSets, f => f.Expediente, "Expediente", v => result.MergedFields.Expediente = v, result);
        MergeScalar(nonNullSets, f => f.Causa, "Causa", v => result.MergedFields.Causa = v, result);
        MergeScalar(nonNullSets, f => f.AccionSolicitada, "AccionSolicitada", v => result.MergedFields.AccionSolicitada = v, result);

        // Contract: collections are combined and deduplicated; AdditionalFields first-wins per key.
        foreach (var fieldSet in nonNullSets)
        {
            foreach (var kvp in fieldSet.AdditionalFields)
            {
                if (!result.MergedFields.AdditionalFields.ContainsKey(kvp.Key))
                {
                    result.MergedFields.AdditionalFields[kvp.Key] = kvp.Value;
                    result.MergedFieldNames.Add($"AdditionalFields.{kvp.Key}");
                }
            }

            foreach (var monto in fieldSet.Montos)
            {
                if (!result.MergedFields.Montos.Any(m => m.Currency == monto.Currency && m.Value == monto.Value))
                {
                    result.MergedFields.Montos.Add(monto);
                }
            }

            foreach (var fecha in fieldSet.Fechas)
            {
                if (!result.MergedFields.Fechas.Contains(fecha))
                {
                    result.MergedFields.Fechas.Add(fecha);
                }
            }
        }

        return result;
    }

    private static void MergeScalar(
        List<ExtractedFields> fieldSets,
        Func<ExtractedFields, string?> selector,
        string fieldName,
        Action<string> assign,
        MergeResult result)
    {
        var values = fieldSets
            .Select(selector)
            .Where(v => !string.IsNullOrEmpty(v))
            .Cast<string>()
            .Distinct()
            .ToList();

        if (values.Count == 0)
        {
            return;
        }

        assign(values[0]);
        result.MergedFieldNames.Add(fieldName);

        if (values.Count > 1)
        {
            result.Conflicts.Add(new FieldConflict
            {
                FieldName = fieldName,
                ConflictingValues = values,
                ResolvedValue = values[0],
                ResolutionStrategy = "First non-null value (design spec)",
            });
        }
    }
}
