namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive.Strategies;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using ExxerCube.Prisma.Testing.Contracts;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Implementation instance of <see cref="AdaptiveDocxStrategyContract"/> for
/// <see cref="TableBasedDocxStrategy"/> (ADR-005).
/// </summary>
/// <remarks>
/// Supersedes <c>TableBasedDocxStrategyLiskovTests</c>: its 13 shared test bodies were
/// lifted into the contract base and run here through inheritance. The four tests below
/// stay implementation-side because their assertions are TableBased-specific (table-row
/// extraction, exact field values, <c>CLABE</c>, the ≥ 85 confidence threshold) — not
/// behaviour every strategy must exhibit (ADR-005 §5). Names preserved (including the
/// <c>_Liskov</c> suffix) so Stryker kill power is unchanged. Implementation-pinning tests
/// remain in <c>TableBasedDocxStrategyMutationKillingTests</c>.
/// </remarks>
public sealed class TableBasedDocxStrategyContractTests : AdaptiveDocxStrategyContract
{
    private readonly ILogger<TableBasedDocxStrategy> _logger;

    private const string ValidTableBasedDocx = @"
        | Campo              | Valor                        |
        |--------------------|------------------------------|
        | Expediente         | A/AS1-2505-088637-PHM       |
        | Oficio             | 214-1-18714972/2025         |
        | Autoridad          | PGR                         |
        | Causa              | Lavado de dinero            |
        | Acción Solicitada  | Aseguramiento precautorio   |
        | Nombre             | Juan Carlos GARCÍA LÓPEZ    |
        | RFC                | GALJ850101XXX               |
        | CLABE              | 012345678901234567          |
        | Banco              | BANAMEX                     |
        | Monto              | $100,000.00 MXN             |
        | Fecha              | 15/11/2025                  |
    ";

    private const string IncompatibleDocx = "This is just random text with no table structure.";

    /// <summary>Initializes the implementation instance with the xUnit output logger.</summary>
    /// <param name="output">xUnit test output sink for the strategy's logger.</param>
    public TableBasedDocxStrategyContractTests(ITestOutputHelper output)
        => _logger = XUnitLogger.CreateLogger<TableBasedDocxStrategy>(output);

    /// <inheritdoc />
    protected override IAdaptiveDocxStrategy CreateSut() => new TableBasedDocxStrategy(_logger);

    /// <inheritdoc />
    protected override string ValidDocxText => ValidTableBasedDocx;

    /// <inheritdoc />
    protected override string IncompatibleDocxText => IncompatibleDocx;

    /// <inheritdoc />
    protected override string ExpectedStrategyName => "TableBased";

    //
    // Implementation-side: TableBased-specific richness (kept out of the shared contract)
    //

    [Fact]
    public async Task ExtractAsync_ShouldReturnExtractedFields_WhenDataFoundInDocument_Liskov()
    {
        // Arrange
        var strategy = new TableBasedDocxStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidTableBasedDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return ExtractedFields when data is found
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Causa.ShouldNotBeNull();
        result.Causa.ShouldBe("Lavado de dinero");
        result.AccionSolicitada.ShouldNotBeNull();
        result.AccionSolicitada.ShouldBe("Aseguramiento precautorio");
    }

    [Fact]
    public async Task ExtractAsync_ShouldExtractFromTableRows_Liskov()
    {
        // Arrange
        var strategy = new TableBasedDocxStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidTableBasedDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Should extract from table structure
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");

        // Verify extended fields from table
        if (result.AdditionalFields.ContainsKey("NumeroOficio"))
        {
            result.AdditionalFields["NumeroOficio"].ShouldBe("214-1-18714972/2025");
        }
        if (result.AdditionalFields.ContainsKey("RFC"))
        {
            result.AdditionalFields["RFC"].ShouldBe("GALJ850101XXX");
        }
    }

    [Fact]
    public async Task ExtractAsync_ShouldExtractAccountInformation_Liskov()
    {
        // Arrange
        var strategy = new TableBasedDocxStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidTableBasedDocx, TestContext.Current.CancellationToken);

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
            banco.ShouldBe("BANAMEX");
        }
    }

    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnHighConfidence_WhenDocumentMatchesStrategy_Liskov()
    {
        // Arrange
        var strategy = new TableBasedDocxStrategy(_logger);

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidTableBasedDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Should return high confidence (81-100) for ideal match
        confidence.ShouldBeGreaterThanOrEqualTo(85); // Table structure is very reliable
    }
}
