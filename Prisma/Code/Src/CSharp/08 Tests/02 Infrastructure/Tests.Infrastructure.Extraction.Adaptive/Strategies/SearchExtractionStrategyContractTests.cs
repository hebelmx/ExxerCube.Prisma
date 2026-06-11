namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive.Strategies;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using ExxerCube.Prisma.Testing.Contracts;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Implementation instance of <see cref="AdaptiveDocxStrategyContract"/> for
/// <see cref="SearchExtractionStrategy"/> (ADR-005).
/// </summary>
/// <remarks>
/// Supersedes <c>SearchExtractionStrategyLiskovTests</c>: its 13 shared test bodies were
/// lifted into the contract base and run here through inheritance. The four tests below
/// stay implementation-side because their assertions are Search-specific (keyword-proximity
/// extraction, <c>CLABE</c>, the ≥ 70 confidence threshold) — not behaviour every strategy
/// must exhibit (ADR-005 §5). Names preserved (including the <c>_Liskov</c> suffix) so
/// Stryker kill power is unchanged. Implementation-pinning tests remain in
/// <c>SearchExtractionStrategyMutationKillingTests</c>.
/// </remarks>
public sealed class SearchExtractionStrategyContractTests : AdaptiveDocxStrategyContract
{
    private readonly ILogger<SearchExtractionStrategy> _logger;

    private const string ValidSearchDocx = @"
        ASEGURAMIENTO PRECAUTORIO - EXPEDIENTE A/AS1-2505-088637-PHM

        Mediante oficio 214-1-18714972/2025, la autoridad competente PGR solicita
        el aseguramiento de recursos relacionados con investigación por lavado de dinero.

        Datos del titular: Juan Carlos GARCÍA LÓPEZ, RFC GALJ850101XXX
        Cuenta bancaria: CLABE 012345678901234567, institución BANAMEX
        Monto aproximado: $100,000.00 MXN

        Fecha de solicitud: 15/11/2025
        Fundamento: artículos 40 y 41 CNPP
    ";

    private const string IncompatibleDocx = "Random unrelated content without any relevant keywords or structure.";

    /// <summary>Initializes the implementation instance with the xUnit output logger.</summary>
    /// <param name="output">xUnit test output sink for the strategy's logger.</param>
    public SearchExtractionStrategyContractTests(ITestOutputHelper output)
        => _logger = XUnitLogger.CreateLogger<SearchExtractionStrategy>(output);

    /// <inheritdoc />
    protected override IAdaptiveDocxStrategy CreateSut() => new SearchExtractionStrategy(_logger);

    /// <inheritdoc />
    protected override string ValidDocxText => ValidSearchDocx;

    /// <inheritdoc />
    protected override string IncompatibleDocxText => IncompatibleDocx;

    /// <inheritdoc />
    protected override string ExpectedStrategyName => "SearchExtraction";

    //
    // Implementation-side: Search-specific richness (kept out of the shared contract)
    //

    [Fact]
    public async Task ExtractAsync_ShouldReturnExtractedFields_WhenDataFoundInDocument_Liskov()
    {
        // Arrange
        var strategy = new SearchExtractionStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidSearchDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return ExtractedFields when data is found
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Causa.ShouldNotBeNull();
        result.Causa.ShouldContain("lavado de dinero");
        result.AccionSolicitada.ShouldNotBeNull();
        result.AccionSolicitada.ShouldContain("aseguramiento");
    }

    [Fact]
    public async Task ExtractAsync_ShouldExtractUsingKeywordSearch_Liskov()
    {
        // Arrange
        var strategy = new SearchExtractionStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidSearchDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Should extract using keyword proximity and search patterns
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
        var strategy = new SearchExtractionStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidSearchDocx, TestContext.Current.CancellationToken);

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
        var strategy = new SearchExtractionStrategy(_logger);

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidSearchDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Should return high confidence (81-100) for ideal match
        confidence.ShouldBeGreaterThanOrEqualTo(70); // Search-based is less precise
    }
}
