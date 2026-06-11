using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance
/// of <see cref="AdaptiveDocxStrategyContract"/>.
/// </summary>
/// <remarks>
/// <para>
/// This factory centralises what the original standalone blueprint
/// (<c>IAdaptiveDocxStrategyContractTests</c>) configured inline per test. Because the
/// contract asserts real outcomes (a non-null result with monetary amounts for a valid
/// document, null/zero for an incompatible one, the cross-method consistency invariants),
/// canned stubs cannot satisfy it — the configuration is a small <em>reference fake</em>
/// of the documented strategy semantics, exactly the evolution ADR-005 §6 expects.
/// </para>
/// <para>
/// Semantics: a document is "extractable" iff it is non-blank and mentions an
/// <c>Expediente</c> (the marker every contract sample document shares). Extractable
/// documents yield confidence 90, <c>CanExtract = true</c> and a populated
/// <see cref="ExtractedFields"/>; everything else yields confidence 0,
/// <c>CanExtract = false</c> and a null result. The blueprint instance's
/// <c>ExpectedStrategyName</c> hook is wired to <see cref="StrategyName"/> below.
/// </para>
/// </remarks>
public static class AdaptiveDocxStrategyMockFactory
{
    /// <summary>The strategy name the contract-conforming mock reports.</summary>
    public const string StrategyName = "MockAdaptiveDocxStrategy";

    /// <summary>
    /// Creates an <see cref="IAdaptiveDocxStrategy"/> mock that satisfies every test in
    /// <see cref="AdaptiveDocxStrategyContract"/>.
    /// </summary>
    /// <returns>The configured mock.</returns>
    public static IAdaptiveDocxStrategy CreateContractConformingMock()
    {
        var mock = Substitute.For<IAdaptiveDocxStrategy>();

        mock.StrategyName.Returns(StrategyName);

        mock.ExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var text = call.ArgAt<string>(0);
                var ct = call.ArgAt<CancellationToken>(1);
                ct.ThrowIfCancellationRequested();
                return Task.FromResult<ExtractedFields?>(CanExtract(text) ? BuildExtractedFields() : null);
            });

        mock.CanExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var ct = call.ArgAt<CancellationToken>(1);
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(CanExtract(call.ArgAt<string>(0)));
            });

        mock.GetConfidenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var ct = call.ArgAt<CancellationToken>(1);
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(CanExtract(call.ArgAt<string>(0)) ? 90 : 0);
            });

        return mock;
    }

    /// <summary>
    /// Design spec: a document is extractable iff it is non-blank and contains the
    /// <c>Expediente</c> marker shared by every contract sample document.
    /// </summary>
    private static bool CanExtract(string? docxText)
        => !string.IsNullOrWhiteSpace(docxText)
           && docxText.Contains("Expediente", StringComparison.OrdinalIgnoreCase);

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
}
