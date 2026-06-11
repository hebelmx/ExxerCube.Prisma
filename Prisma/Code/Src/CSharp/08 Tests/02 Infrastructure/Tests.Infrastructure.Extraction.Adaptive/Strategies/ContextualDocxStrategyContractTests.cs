namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive.Strategies;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using ExxerCube.Prisma.Testing.Contracts;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Implementation instance of <see cref="AdaptiveDocxStrategyContract"/> for
/// <see cref="ContextualDocxStrategy"/> (ADR-005).
/// </summary>
/// <remarks>
/// Supersedes <c>ContextualDocxStrategyLiskovTests</c>: its 13 shared test bodies were
/// lifted into the contract base and run here through inheritance. The four tests below
/// stay implementation-side because their assertions are Contextual-specific (paragraph
/// context extraction, <c>CLABE</c>, the looser ≥ 70 confidence threshold) — not behaviour
/// every strategy must exhibit (ADR-005 §5). Names preserved (including the <c>_Liskov</c>
/// suffix) so Stryker kill power is unchanged. Implementation-pinning tests remain in
/// <c>ContextualDocxStrategyMutationKillingTests</c>.
/// </remarks>
public sealed class ContextualDocxStrategyContractTests : AdaptiveDocxStrategyContract
{
    private readonly ILogger<ContextualDocxStrategy> _logger;

    private const string ValidContextualDocx = @"
        DOCUMENTO OFICIAL

        En el expediente A/AS1-2505-088637-PHM, relacionado con el oficio 214-1-18714972/2025,
        se solicita el aseguramiento precautorio de la cuenta bancaria identificada con CLABE
        012345678901234567, titularidad de Juan Carlos GARCÍA LÓPEZ (RFC: GALJ850101XXX),
        en BANAMEX.

        La autoridad emisora PGR fundamenta esta solicitud en la causa de lavado de dinero,
        conforme a los artículos aplicables del código penal federal.

        El monto estimado del aseguramiento asciende a $100,000.00 MXN (cien mil pesos 00/100 M.N.).

        Fecha de solicitud: 15/11/2025
    ";

    private const string IncompatibleDocx = "This is just random text with no structure or context.";

    /// <summary>Initializes the implementation instance with the xUnit output logger.</summary>
    /// <param name="output">xUnit test output sink for the strategy's logger.</param>
    public ContextualDocxStrategyContractTests(ITestOutputHelper output)
        => _logger = XUnitLogger.CreateLogger<ContextualDocxStrategy>(output);

    /// <inheritdoc />
    protected override IAdaptiveDocxStrategy CreateSut() => new ContextualDocxStrategy(_logger);

    /// <inheritdoc />
    protected override string ValidDocxText => ValidContextualDocx;

    /// <inheritdoc />
    protected override string IncompatibleDocxText => IncompatibleDocx;

    /// <inheritdoc />
    protected override string ExpectedStrategyName => "ContextualDocx";

    //
    // Implementation-side: Contextual-specific richness (kept out of the shared contract)
    //

    [Fact]
    public async Task ExtractAsync_ShouldReturnExtractedFields_WhenDataFoundInDocument_Liskov()
    {
        // Arrange
        var strategy = new ContextualDocxStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidContextualDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return ExtractedFields when data is found
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Causa.ShouldNotBeNull();
        result.Causa.ShouldContain("lavado de dinero"); // Case-insensitive contextual match
        result.AccionSolicitada.ShouldNotBeNull();
        result.AccionSolicitada.ShouldContain("aseguramiento precautorio");
    }

    [Fact]
    public async Task ExtractAsync_ShouldExtractFromParagraphContext_Liskov()
    {
        // Arrange
        var strategy = new ContextualDocxStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidContextualDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Should extract from contextual paragraphs
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");

        // Verify extended fields extracted from context
        if (result.AdditionalFields.ContainsKey("NumeroOficio"))
        {
            result.AdditionalFields["NumeroOficio"].ShouldBe("214-1-18714972/2025");
        }
    }

    [Fact]
    public async Task ExtractAsync_ShouldExtractAccountInformation_Liskov()
    {
        // Arrange
        var strategy = new ContextualDocxStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidContextualDocx, TestContext.Current.CancellationToken);

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
        var strategy = new ContextualDocxStrategy(_logger);

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidContextualDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Should return high confidence (81-100) for ideal match
        confidence.ShouldBeGreaterThanOrEqualTo(70); // Contextual is less precise than structured
    }
}
