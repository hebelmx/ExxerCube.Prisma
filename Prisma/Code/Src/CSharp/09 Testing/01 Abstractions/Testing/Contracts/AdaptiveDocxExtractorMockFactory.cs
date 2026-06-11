using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance
/// of <see cref="AdaptiveDocxExtractorContract"/>.
/// </summary>
/// <remarks>
/// <para>
/// The contract asserts real orchestration outcomes (best-strategy selection, merge,
/// gap-filling complement, descending-ordered confidences), so canned stubs cannot
/// satisfy it. The configuration is a small <em>reference fake</em> of the documented
/// orchestrator semantics, mirroring <c>AdaptiveDocxExtractor</c>: an extractable
/// document yields a populated result and a descending confidence list; an empty or
/// incompatible document yields a null result and one zero-valued entry per strategy
/// (never an empty list — the validated reality the restored
/// <c>ExtractorContract_WhenNoConfidences_ExtractShouldReturnNull</c> test reflects).
/// </para>
/// </remarks>
public static class AdaptiveDocxExtractorMockFactory
{
    private static readonly string[] StrategyNames =
    {
        "StructuredDocx", "ComplementExtraction", "TableBased", "ContextualDocx", "SearchExtraction",
    };

    private static readonly int[] DescendingScores = { 90, 75, 50, 40, 25 };

    /// <summary>
    /// Creates an <see cref="IAdaptiveDocxExtractor"/> mock that satisfies every test in
    /// <see cref="AdaptiveDocxExtractorContract"/>.
    /// </summary>
    /// <returns>The configured mock.</returns>
    public static IAdaptiveDocxExtractor CreateContractConformingMock()
    {
        var mock = Substitute.For<IAdaptiveDocxExtractor>();

        mock.ExtractAsync(Arg.Any<string>(), Arg.Any<ExtractionMode>(), Arg.Any<ExtractedFields?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var text = call.ArgAt<string>(0);
                var mode = call.ArgAt<ExtractionMode>(1);
                var existing = call.ArgAt<ExtractedFields?>(2);
                var ct = call.ArgAt<CancellationToken>(3);
                return Task.FromResult(Extract(text, mode, existing, ct));
            });

        mock.GetStrategyConfidencesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var text = call.ArgAt<string>(0);
                var ct = call.ArgAt<CancellationToken>(1);
                return Task.FromResult(Confidences(text, ct));
            });

        return mock;
    }

    private static ExtractedFields? Extract(string docxText, ExtractionMode mode, ExtractedFields? existingFields, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(docxText))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!CanExtract(docxText))
        {
            return null;
        }

        var extracted = BuildExtractedFields();

        if (mode == ExtractionMode.Complement && existingFields != null)
        {
            return Complement(existingFields, extracted);
        }

        return extracted;
    }

    private static IReadOnlyList<StrategyConfidence> Confidences(string docxText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(docxText))
        {
            return AllZero();
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!CanExtract(docxText))
        {
            return AllZero();
        }

        return StrategyNames
            .Select((name, i) => new StrategyConfidence(name, DescendingScores[i]))
            .ToList();
    }

    private static IReadOnlyList<StrategyConfidence> AllZero()
        => StrategyNames.Select(name => new StrategyConfidence(name, 0)).ToList();

    /// <summary>
    /// Design spec: a document is extractable iff it is non-blank and mentions an
    /// <c>Expediente</c> (the marker every contract sample document shares).
    /// </summary>
    private static bool CanExtract(string docxText)
        => docxText.Contains("Expediente", StringComparison.OrdinalIgnoreCase);

    private static ExtractedFields BuildExtractedFields()
        => new()
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Lavado de dinero",
            AccionSolicitada = "Aseguramiento precautorio",
            Montos = { new AmountData("MXN", 100000m, "$100,000.00 MXN") },
            Fechas = { "15/11/2025" },
            AdditionalFields =
            {
                ["NumeroOficio"] = "214-1-18714972/2025",
                ["AutoridadNombre"] = "PGR",
            },
        };

    // Mirrors AdaptiveDocxExtractor.ComplementFields: preserve existing scalars, fill gaps.
    private static ExtractedFields Complement(ExtractedFields existing, ExtractedFields newExtraction)
    {
        var complemented = new ExtractedFields
        {
            Expediente = existing.Expediente ?? newExtraction.Expediente,
            Causa = existing.Causa ?? newExtraction.Causa,
            AccionSolicitada = existing.AccionSolicitada ?? newExtraction.AccionSolicitada,
        };

        foreach (var kvp in existing.AdditionalFields)
        {
            complemented.AdditionalFields[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in newExtraction.AdditionalFields)
        {
            if (!complemented.AdditionalFields.ContainsKey(kvp.Key))
            {
                complemented.AdditionalFields[kvp.Key] = kvp.Value;
            }
        }

        return complemented;
    }
}
