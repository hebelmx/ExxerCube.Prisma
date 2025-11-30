namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive.Strategies;

using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using Meziantou.Extensions.Logging.Xunit.v3;
using Xunit.Sdk;

/// <summary>
/// Liskov Substitution Principle verification tests for <see cref="StructuredDocxStrategy"/>.
/// </summary>
/// <remarks>
/// <para>
/// These tests prove that StructuredDocxStrategy correctly implements <see cref="IAdaptiveDocxStrategy"/>
/// by running the SAME contract tests that were defined in Tests.Domain against the ACTUAL implementation.
/// </para>
/// <para>
/// <strong>ITDD Principle:</strong> If the implementation passes all interface contract tests,
/// it satisfies the Liskov Substitution Principle and is correct.
/// </para>
/// </remarks>
public sealed class StructuredDocxStrategyLiskovTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<StructuredDocxStrategy> _logger;

    // Sample documents for testing (same as contract tests)
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

    private const string EmptyDocx = "";

    private const string IncompatibleDocx = "This is just random text with no structure.";

    public StructuredDocxStrategyLiskovTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<StructuredDocxStrategy>(output);
    }

    //
    // Liskov Verification: StrategyName Property
    //

    [Fact]
    public void StrategyName_ShouldReturnNonEmptyString_Liskov()
    {
        // Arrange
        var strategy = new StructuredDocxStrategy(_logger);

        // Act
        var strategyName = strategy.StrategyName;

        // Assert - Contract: Must return non-empty strategy name
        strategyName.ShouldNotBeNullOrWhiteSpace();
        strategyName.ShouldBe("StructuredDocx");
    }

    //
    // Liskov Verification: ExtractAsync
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
    public async Task ExtractAsync_ShouldReturnNull_WhenStrategyCannotExtract_Liskov()
    {
        // Arrange
        var strategy = new StructuredDocxStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(IncompatibleDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return null when strategy cannot extract meaningful data
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnNullOrEmpty_WhenNoDataFound_Liskov()
    {
        // Arrange
        var strategy = new StructuredDocxStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(EmptyDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: May return null when document is empty
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractAsync_ShouldHandleCancellation_Liskov()
    {
        // Arrange
        var strategy = new StructuredDocxStrategy(_logger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await strategy.ExtractAsync(ValidStructuredDocx, cts.Token));
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
    public async Task ExtractAsync_ShouldExtractMonetaryAmountsWithCurrency_Liskov()
    {
        // Arrange
        var strategy = new StructuredDocxStrategy(_logger);

        // Act
        var result = await strategy.ExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

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

    //
    // Liskov Verification: CanExtractAsync
    //

    [Fact]
    public async Task CanExtractAsync_ShouldReturnTrue_WhenStrategyCanHandleDocument_Liskov()
    {
        // Arrange
        var strategy = new StructuredDocxStrategy(_logger);

        // Act
        var canExtract = await strategy.CanExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return true when strategy can process document
        canExtract.ShouldBeTrue();
    }

    [Fact]
    public async Task CanExtractAsync_ShouldReturnFalse_WhenStrategyCannotHandleDocument_Liskov()
    {
        // Arrange
        var strategy = new StructuredDocxStrategy(_logger);

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
        var strategy = new StructuredDocxStrategy(_logger);

        // Act
        var confidence = await strategy.GetConfidenceAsync(IncompatibleDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return 0 when strategy cannot extract
        confidence.ShouldBe(0);
    }

    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnScoreBetween0And100_Liskov()
    {
        // Arrange
        var strategy = new StructuredDocxStrategy(_logger);

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return confidence score between 0 and 100
        confidence.ShouldBeInRange(0, 100);
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

    //
    // Liskov Verification: Behavioral Contract (Cross-Method Consistency)
    //

    [Fact]
    public async Task StrategyContract_WhenCanExtractReturnsFalse_ConfidenceShouldBeZero_Liskov()
    {
        // Arrange
        var strategy = new StructuredDocxStrategy(_logger);

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
        var strategy = new StructuredDocxStrategy(_logger);

        // Act
        var canExtract = await strategy.CanExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);
        var confidence = await strategy.GetConfidenceAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: CanExtract = true implies Confidence > 0
        canExtract.ShouldBeTrue();
        confidence.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task StrategyContract_WhenConfidenceIsZero_ExtractAsyncShouldReturnNull_Liskov()
    {
        // Arrange
        var strategy = new StructuredDocxStrategy(_logger);

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
        var strategy = new StructuredDocxStrategy(_logger);

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);
        var result = await strategy.ExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Confidence > 0 implies ExtractAsync returns data (not null)
        confidence.ShouldBeGreaterThan(0);
        result.ShouldNotBeNull();
    }
}
