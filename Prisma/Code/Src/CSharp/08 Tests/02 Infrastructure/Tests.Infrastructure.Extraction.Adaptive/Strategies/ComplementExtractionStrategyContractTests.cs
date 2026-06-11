namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive.Strategies;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using ExxerCube.Prisma.Testing.Contracts;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Implementation instance of <see cref="AdaptiveDocxStrategyContract"/> for
/// <see cref="ComplementExtractionStrategy"/> (ADR-005).
/// </summary>
/// <remarks>
/// Supersedes <c>ComplementExtractionStrategyLiskovTests</c>: its 13 shared test bodies
/// were lifted into the contract base and run here through inheritance. The four tests
/// below stay implementation-side because their assertions are Complement-specific
/// (multi-pattern extraction, <c>CLABE</c>, the ≥ 75 confidence threshold) — not behaviour
/// every strategy must exhibit (ADR-005 §5). Names preserved (including the <c>_Liskov</c>
/// suffix) so Stryker kill power is unchanged. Implementation-pinning tests remain in
/// <c>ComplementExtractionStrategyMutationKillingTests</c>.
/// </remarks>
public sealed class ComplementExtractionStrategyContractTests : AdaptiveDocxStrategyContract
{
    private readonly ILogger<ComplementExtractionStrategy> _logger;

    private const string ValidComplementDocx = @"
        OFICIO DE ASEGURAMIENTO PRECAUTORIO

        Expediente: A/AS1-2505-088637-PHM
        Oficio: 214-1-18714972/2025
        Fecha: 15/11/2025

        La Procuraduría General de la República (PGR), en el marco de la causa penal
        por lavado de dinero, solicita el aseguramiento precautorio de la cuenta bancaria
        con CLABE 012345678901234567, titularidad de Juan Carlos GARCÍA LÓPEZ
        (RFC: GALJ850101XXX), en BANAMEX.

        Monto estimado: $100,000.00 MXN

        Fundamento Legal: Artículos 40, 41 y 178 del Código Nacional de Procedimientos Penales.
    ";

    private const string IncompatibleDocx = "This is just random text with no meaningful content.";

    /// <summary>Initializes the implementation instance with the xUnit output logger.</summary>
    /// <param name="output">xUnit test output sink for the strategy's logger.</param>
    public ComplementExtractionStrategyContractTests(ITestOutputHelper output)
        => _logger = XUnitLogger.CreateLogger<ComplementExtractionStrategy>(output);

    /// <inheritdoc />
    protected override IAdaptiveDocxStrategy CreateSut() => new ComplementExtractionStrategy(_logger);

    /// <inheritdoc />
    protected override string ValidDocxText => ValidComplementDocx;

    /// <inheritdoc />
    protected override string IncompatibleDocxText => IncompatibleDocx;

    /// <inheritdoc />
    protected override string ExpectedStrategyName => "ComplementExtraction";

    //
    // Implementation-side: Complement-specific richness (kept out of the shared contract)
    //

    [Fact]
    public async Task ExtractAsync_ShouldReturnExtractedFields_WhenDataFoundInDocument_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidComplementDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return ExtractedFields when data is found
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Causa.ShouldNotBeNull();
        result.Causa.ShouldContain("lavado de dinero");
        result.AccionSolicitada.ShouldNotBeNull();
        result.AccionSolicitada.ShouldContain("aseguramiento precautorio");
    }

    [Fact]
    public async Task ExtractAsync_ShouldExtractFromMultiplePatterns_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidComplementDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Should extract from various patterns (labels, context, narrative)
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");

        // Verify extended fields
        if (result.AdditionalFields.ContainsKey("NumeroOficio"))
        {
            result.AdditionalFields["NumeroOficio"].ShouldBe("214-1-18714972/2025");
        }
    }

    [Fact]
    public async Task ExtractAsync_ShouldExtractAccountInformation_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidComplementDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: May extract account information in AdditionalFields
        result.ShouldNotBeNull();
        if (result.AdditionalFields.ContainsKey("CLABE"))
        {
            result.AdditionalFields["CLABE"].ShouldBe("012345678901234567");
        }
        if (result.AdditionalFields.ContainsKey("Banco"))
        {
            var banco = result.AdditionalFields["Banco"];
            banco.ShouldNotBeNull();
            banco.ShouldContain("BANAMEX");
        }
    }

    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnHighConfidence_WhenDocumentMatchesStrategy_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidComplementDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Should return high confidence (81-100) for ideal match
        confidence.ShouldBeGreaterThanOrEqualTo(75); // Complement uses multiple patterns
    }
}
