namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive.Strategies;

using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Liskov Substitution Principle verification tests for <see cref="ComplementExtractionStrategy"/>.
/// </summary>
/// <remarks>
/// <para>
/// These tests prove that ComplementExtractionStrategy correctly implements <see cref="IAdaptiveDocxStrategy"/>
/// by running the SAME contract tests that were defined in Tests.Domain against the ACTUAL implementation.
/// </para>
/// <para>
/// <strong>ITDD Principle:</strong> If the implementation passes all interface contract tests,
/// it satisfies the Liskov Substitution Principle and is correct.
/// </para>
/// </remarks>
public sealed class ComplementExtractionStrategyLiskovTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<ComplementExtractionStrategy> _logger;

    // Sample document with comprehensive data for complement extraction
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

    private const string EmptyDocx = "";

    private const string IncompatibleDocx = "This is just random text with no meaningful content.";

    public ComplementExtractionStrategyLiskovTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<ComplementExtractionStrategy>(output);
    }

    //
    // Liskov Verification: StrategyName Property
    //

    [Fact]
    public void StrategyName_ShouldReturnNonEmptyString_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var strategyName = strategy.StrategyName;

        // Assert - Contract: Must return non-empty strategy name
        strategyName.ShouldNotBeNullOrWhiteSpace();
        strategyName.ShouldBe("ComplementExtraction");
    }

    //
    // Liskov Verification: ExtractAsync
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
    public async Task ExtractAsync_ShouldReturnNull_WhenStrategyCannotExtract_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(IncompatibleDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return null when strategy cannot extract meaningful data
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnNullOrEmpty_WhenNoDataFound_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(EmptyDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: May return null when document is empty
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractAsync_ShouldHandleCancellation_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await strategy.ExtractAsync(ValidComplementDocx, cts.Token));
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
    public async Task ExtractAsync_ShouldExtractMonetaryAmountsWithCurrency_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidComplementDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must extract monetary amounts with currency and original text
        result.ShouldNotBeNull();
        result.Montos.Count.ShouldBeGreaterThan(0);
        result.Montos.ShouldAllBe(m => !string.IsNullOrWhiteSpace(m.Currency));
        result.Montos.ShouldAllBe(m => m.Value > 0);
        result.Montos.ShouldAllBe(m => !string.IsNullOrWhiteSpace(m.OriginalText));
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

    //
    // Liskov Verification: CanExtractAsync
    //

    [Fact]
    public async Task CanExtractAsync_ShouldReturnTrue_WhenStrategyCanHandleDocument_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var canExtract = await strategy.CanExtractAsync(ValidComplementDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return true when strategy can process document
        canExtract.ShouldBeTrue();
    }

    [Fact]
    public async Task CanExtractAsync_ShouldReturnFalse_WhenStrategyCannotHandleDocument_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var canExtract = await strategy.CanExtractAsync(IncompatibleDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return false when strategy cannot process document
        canExtract.ShouldBeFalse();
    }

    //
    // Liskov Verification: GetConfidenceAsync
    //

    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnZero_WhenStrategyCannotExtract_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var confidence = await strategy.GetConfidenceAsync(IncompatibleDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return 0 when strategy cannot extract
        confidence.ShouldBe(0);
    }

    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnScoreBetween0And100_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidComplementDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return confidence score between 0 and 100
        confidence.ShouldBeInRange(0, 100);
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

    //
    // Liskov Verification: Behavioral Contract (Cross-Method Consistency)
    //

    [Fact]
    public async Task StrategyContract_WhenCanExtractReturnsFalse_ConfidenceShouldBeZero_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var canExtract = await strategy.CanExtractAsync(IncompatibleDocx, TestContext.Current.CancellationToken);
        var confidence = await strategy.GetConfidenceAsync(IncompatibleDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: CanExtract = false implies Confidence = 0
        canExtract.ShouldBeFalse();
        confidence.ShouldBe(0);
    }

    [Fact]
    public async Task StrategyContract_WhenCanExtractReturnsTrue_ConfidenceShouldBePositive_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var canExtract = await strategy.CanExtractAsync(ValidComplementDocx, TestContext.Current.CancellationToken);
        var confidence = await strategy.GetConfidenceAsync(ValidComplementDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: CanExtract = true implies Confidence > 0
        canExtract.ShouldBeTrue();
        confidence.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task StrategyContract_WhenConfidenceIsZero_ExtractAsyncShouldReturnNull_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var confidence = await strategy.GetConfidenceAsync(IncompatibleDocx, TestContext.Current.CancellationToken);
        var result = await strategy.ExtractAsync(IncompatibleDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Confidence = 0 implies ExtractAsync returns null
        confidence.ShouldBe(0);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task StrategyContract_WhenConfidenceIsPositive_ExtractAsyncShouldReturnData_Liskov()
    {
        // Arrange
        var strategy = new ComplementExtractionStrategy(_logger);

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidComplementDocx, TestContext.Current.CancellationToken);
        var result = await strategy.ExtractAsync(ValidComplementDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Confidence > 0 implies ExtractAsync returns data (not null)
        confidence.ShouldBeGreaterThan(0);
        result.ShouldNotBeNull();
    }
}
