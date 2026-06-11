namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive.Strategies;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using ExxerCube.Prisma.Testing.Contracts;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Implementation instance of <see cref="AdaptiveDocxStrategyContract"/> for
/// <see cref="StructuredDocxStrategy"/> (ADR-005).
/// </summary>
/// <remarks>
/// Supersedes <c>StructuredDocxStrategyLiskovTests</c>: its 13 shared test bodies were
/// lifted into the contract base and run here through inheritance. The four tests below
/// stay implementation-side because their assertions are Structured-specific richness
/// (exact field detail, Mexican-name parts, <c>NumeroCuenta</c>, the exact confidence
/// score 90) — not behaviour every strategy must exhibit (ADR-005 §5). Names preserved
/// (including the <c>_Liskov</c> suffix) so Stryker kill power is unchanged.
/// Implementation-pinning tests remain in <c>StructuredDocxStrategyMutationKillingTests</c>.
/// </remarks>
public sealed class StructuredDocxStrategyContractTests : AdaptiveDocxStrategyContract
{
    private readonly ILogger<StructuredDocxStrategy> _logger;

    private const string ValidStructuredDocx = @"
        Expediente No.: A/AS1-2505-088637-PHM
        Oficio: 214-1-18714972/2025
        Autoridad: PGR
        Causa: Lavado de dinero
        Acción Solicitada: Aseguramiento precautorio
        Nombre: Juan Carlos GARCÍA LÓPEZ
        RFC: GALJ850101XXX
        Cuenta: 0123456789012345
        Banco: BANAMEX
        Monto: $100,000.00 MXN
        Fecha: 15/11/2025
    ";

    private const string IncompatibleDocx = "This is just random text with no structure.";

    /// <summary>Initializes the implementation instance with the xUnit output logger.</summary>
    /// <param name="output">xUnit test output sink for the strategy's logger.</param>
    public StructuredDocxStrategyContractTests(ITestOutputHelper output)
        => _logger = XUnitLogger.CreateLogger<StructuredDocxStrategy>(output);

    /// <inheritdoc />
    protected override IAdaptiveDocxStrategy CreateSut() => new StructuredDocxStrategy(_logger);

    /// <inheritdoc />
    protected override string ValidDocxText => ValidStructuredDocx;

    /// <inheritdoc />
    protected override string IncompatibleDocxText => IncompatibleDocx;

    /// <inheritdoc />
    protected override string ExpectedStrategyName => "StructuredDocx";

    //
    // Implementation-side: Structured-specific richness (kept out of the shared contract)
    //

    [Fact]
    public async Task ExtractAsync_ShouldReturnExtractedFields_WhenDataFoundInDocument_Liskov()
    {
        // Arrange
        var strategy = new StructuredDocxStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return ExtractedFields when data is found
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Causa.ShouldBe("Lavado de dinero");
        result.AccionSolicitada.ShouldBe("Aseguramiento precautorio");

        // Verify monetary amounts
        result.Montos.ShouldNotBeEmpty();
        result.Montos.ShouldContain(m => m.Currency == "MXN" && m.Value == 100000m);

        // Verify extended fields
        result.AdditionalFields.ShouldContainKey("NumeroOficio");
        result.AdditionalFields["NumeroOficio"].ShouldBe("214-1-18714972/2025");
        result.AdditionalFields.ShouldContainKey("AutoridadNombre");
        result.AdditionalFields["AutoridadNombre"].ShouldBe("PGR");
    }

    [Fact]
    public async Task ExtractAsync_ShouldExtractMexicanNamesCorrectly_Liskov()
    {
        // Arrange
        var strategy = new StructuredDocxStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must extract Mexican names with Paterno, Materno, Nombre
        result.ShouldNotBeNull();
        result.AdditionalFields.ShouldContainKey("Paterno");
        result.AdditionalFields.ShouldContainKey("Materno");
        result.AdditionalFields.ShouldContainKey("Nombre");
        result.AdditionalFields["Paterno"].ShouldBe("GARCÍA");
        result.AdditionalFields["Materno"].ShouldBe("LÓPEZ");
        result.AdditionalFields["Nombre"].ShouldBe("Juan Carlos");
    }

    [Fact]
    public async Task ExtractAsync_ShouldExtractAccountInformation_Liskov()
    {
        // Arrange
        var strategy = new StructuredDocxStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: May extract account information in AdditionalFields
        result.ShouldNotBeNull();
        if (result.AdditionalFields.ContainsKey("NumeroCuenta"))
        {
            result.AdditionalFields["NumeroCuenta"].ShouldNotBeNullOrWhiteSpace();
            result.AdditionalFields["NumeroCuenta"].ShouldBe("0123456789012345");
        }
        if (result.AdditionalFields.ContainsKey("Banco"))
        {
            result.AdditionalFields["Banco"].ShouldBe("BANAMEX");
        }
    }

    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnHighConfidence_WhenDocumentMatchesStrategy_Liskov()
    {
        // Arrange
        var strategy = new StructuredDocxStrategy(_logger);

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Should return high confidence (81-100) for ideal match
        confidence.ShouldBeGreaterThanOrEqualTo(81);
        confidence.ShouldBe(90); // StructuredDocx returns 90 for 3+ standard labels
    }
}
