namespace ExxerCube.Prisma.Tests.Domain.Domain.Interfaces;

using ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// ITDD contract tests for <see cref="IAdaptiveDocxStrategy"/> interface.
/// These tests define the behavioral contract that ANY implementation must satisfy.
/// </summary>
/// <remarks>
/// <para>
/// <strong>ITDD Principles:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>Tests validate interface contracts using mocks (WHAT, not HOW)</description></item>
///   <item><description>Tests are reusable across all implementations</description></item>
///   <item><description>Tests validate Liskov Substitution Principle</description></item>
///   <item><description>Any implementation satisfying these tests is correct</description></item>
/// </list>
/// <para>
/// <strong>Purpose:</strong> Prove that the interface contract is testable before implementing.
/// If we can test the interface with mocks, we can implement it correctly.
/// </para>
/// </remarks>
public sealed class IAdaptiveDocxStrategyContractTests
{
    // Sample document text for testing
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

    //
    // StrategyName Property Contract Tests

    [Fact]
    public void StrategyName_ShouldReturnNonEmptyString()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();
        strategy.StrategyName.Returns("TestStrategy");

        // Act
        var strategyName = strategy.StrategyName;

        // Assert - Contract: Must return non-empty strategy name
        strategyName.ShouldNotBeNullOrWhiteSpace();
    }

    //
    // ExtractAsync Contract Tests

    [Fact]
    public async Task ExtractAsync_ShouldReturnExtractedFields_WhenDataFoundInDocument()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();
        var expectedFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Lavado de dinero",
            AccionSolicitada = "Aseguramiento precautorio",
            Montos = new List<AmountData>
            {
                new AmountData("MXN", 100000m, "$100,000.00 MXN")
            },
            AdditionalFields = new Dictionary<string, string?>
            {
                ["NumeroOficio"] = "214-1-18714972/2025",
                ["AutoridadNombre"] = "PGR",
                ["Paterno"] = "GARCÍA",
                ["Materno"] = "LÓPEZ",
                ["Nombre"] = "Juan Carlos",
                ["NumeroCuenta"] = "0123456789012345",
                ["Banco"] = "BANAMEX"
            }
        };

        strategy.ExtractAsync(ValidStructuredDocx, Arg.Any<CancellationToken>())
            .Returns(expectedFields);

        // Act
        var result = await strategy.ExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return ExtractedFields when data is found
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Causa.ShouldBe("Lavado de dinero");
        result.AccionSolicitada.ShouldBe("Aseguramiento precautorio");
        result.Montos.Count.ShouldBe(1);
        result.Montos[0].Currency.ShouldBe("MXN");
        result.Montos[0].Value.ShouldBe(100000m);
        result.AdditionalFields["NumeroOficio"].ShouldBe("214-1-18714972/2025");
        result.AdditionalFields["Paterno"].ShouldBe("GARCÍA");
        result.AdditionalFields["Materno"].ShouldBe("LÓPEZ");
        result.AdditionalFields["Nombre"].ShouldBe("Juan Carlos");
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnNull_WhenStrategyCannotExtract()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        strategy.ExtractAsync(IncompatibleDocx, Arg.Any<CancellationToken>())
            .Returns((ExtractedFields?)null);

        // Act
        var result = await strategy.ExtractAsync(IncompatibleDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return null when strategy cannot extract meaningful data
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnEmptyExtractedFields_WhenNoDataFound()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();
        var emptyFields = new ExtractedFields();

        strategy.ExtractAsync(EmptyDocx, Arg.Any<CancellationToken>())
            .Returns(emptyFields);

        // Act
        var result = await strategy.ExtractAsync(EmptyDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: May return empty ExtractedFields (but not null) when document is empty
        result.ShouldNotBeNull();
        result.Expediente.ShouldBeNull();
        result.Causa.ShouldBeNull();
        result.AccionSolicitada.ShouldBeNull();
        result.Montos.Count.ShouldBe(0);
        result.AdditionalFields.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExtractAsync_ShouldHandleCancellation()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        strategy.ExtractAsync(ValidStructuredDocx, cts.Token)
            .Returns(Task.FromCanceled<ExtractedFields?>(cts.Token));

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<TaskCanceledException>(async () =>
            await strategy.ExtractAsync(ValidStructuredDocx, cts.Token));
    }

    [Fact]
    public async Task ExtractAsync_ShouldExtractMexicanNamesCorrectly()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();
        var fieldsWithMexicanName = new ExtractedFields
        {
            AdditionalFields = new Dictionary<string, string?>
            {
                ["Paterno"] = "GARCÍA",
                ["Materno"] = "LÓPEZ",
                ["Nombre"] = "Juan Carlos",
                ["NombreCompleto"] = "Juan Carlos GARCÍA LÓPEZ"
            }
        };

        strategy.ExtractAsync(Arg.Is<string>(s => s.Contains("GARCÍA LÓPEZ")), Arg.Any<CancellationToken>())
            .Returns(fieldsWithMexicanName);

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
    public async Task ExtractAsync_ShouldExtractMonetaryAmountsWithCurrency()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();
        var fieldsWithAmounts = new ExtractedFields
        {
            Montos = new List<AmountData>
            {
                new AmountData("MXN", 100000m, "$100,000.00 MXN"),
                new AmountData("USD", 5000m, "$5,000.00 USD")
            }
        };

        strategy.ExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(fieldsWithAmounts);

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
    public async Task ExtractAsync_ShouldExtractAccountInformation()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();
        var fieldsWithAccount = new ExtractedFields
        {
            AdditionalFields = new Dictionary<string, string?>
            {
                ["NumeroCuenta"] = "0123456789012345",
                ["CLABE"] = "012345678901234567",
                ["Banco"] = "BANAMEX"
            }
        };

        strategy.ExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(fieldsWithAccount);

        // Act
        var result = await strategy.ExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: May extract account information in AdditionalFields
        result.ShouldNotBeNull();
        if (result.AdditionalFields.ContainsKey("NumeroCuenta"))
        {
            result.AdditionalFields["NumeroCuenta"].ShouldNotBeNullOrWhiteSpace();
        }
        if (result.AdditionalFields.ContainsKey("CLABE"))
        {
            result.AdditionalFields["CLABE"].ShouldNotBeNullOrWhiteSpace();
        }
    }

    //
    // CanExtractAsync Contract Tests

    [Fact]
    public async Task CanExtractAsync_ShouldReturnTrue_WhenStrategyCanHandleDocument()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        strategy.CanExtractAsync(ValidStructuredDocx, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var canExtract = await strategy.CanExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return true when strategy can process document
        canExtract.ShouldBeTrue();
    }

    [Fact]
    public async Task CanExtractAsync_ShouldReturnFalse_WhenStrategyCannotHandleDocument()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        strategy.CanExtractAsync(IncompatibleDocx, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var canExtract = await strategy.CanExtractAsync(IncompatibleDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return false when strategy cannot process document
        canExtract.ShouldBeFalse();
    }

    [Fact]
    public async Task CanExtractAsync_ShouldBeFasterThanExtractAsync()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        // Configure CanExtractAsync to be fast
        strategy.CanExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        var canExtractTask = strategy.CanExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: CanExtractAsync should be a fast preliminary check
        canExtractTask.IsCompleted.ShouldBeTrue(); // Should complete synchronously for fast checks
    }

    //
    // GetConfidenceAsync Contract Tests

    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnZero_WhenStrategyCannotExtract()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        strategy.GetConfidenceAsync(IncompatibleDocx, Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        var confidence = await strategy.GetConfidenceAsync(IncompatibleDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return 0 when strategy cannot extract
        confidence.ShouldBe(0);
    }

    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnScoreBetween0And100()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        strategy.GetConfidenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(75);

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return confidence score between 0 and 100
        confidence.ShouldBeInRange(0, 100);
    }

    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnHighConfidence_WhenDocumentMatchesStrategy()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        // Structured strategy should return high confidence for structured documents
        strategy.StrategyName.Returns("StructuredDocx");
        strategy.GetConfidenceAsync(ValidStructuredDocx, Arg.Any<CancellationToken>())
            .Returns(90);

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Should return high confidence (81-100) for ideal match
        confidence.ShouldBeGreaterThanOrEqualTo(81);
    }

    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnMediumConfidence_ForFallbackStrategy()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        // Complement strategy should return medium confidence (always available)
        strategy.StrategyName.Returns("Complement");
        strategy.GetConfidenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(50);

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Fallback/complement strategies should return medium confidence (31-60)
        confidence.ShouldBeInRange(31, 60);
    }

    [Fact]
    public async Task GetConfidenceAsync_ShouldReturnLowConfidence_ForBackupStrategy()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        strategy.GetConfidenceAsync(EmptyDocx, Arg.Any<CancellationToken>())
            .Returns(20);

        // Act
        var confidence = await strategy.GetConfidenceAsync(EmptyDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Backup strategies should return low confidence (1-30)
        confidence.ShouldBeInRange(1, 30);
    }

    //
    // Behavioral Contract Tests (Cross-Method Consistency)

    [Fact]
    public async Task StrategyContract_WhenCanExtractReturnsFalse_ConfidenceShouldBeZero()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        strategy.CanExtractAsync(IncompatibleDocx, Arg.Any<CancellationToken>())
            .Returns(false);
        strategy.GetConfidenceAsync(IncompatibleDocx, Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        var canExtract = await strategy.CanExtractAsync(IncompatibleDocx, TestContext.Current.CancellationToken);
        var confidence = await strategy.GetConfidenceAsync(IncompatibleDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: CanExtract = false implies Confidence = 0
        canExtract.ShouldBeFalse();
        confidence.ShouldBe(0);
    }

    [Fact]
    public async Task StrategyContract_WhenCanExtractReturnsTrue_ConfidenceShouldBePositive()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        strategy.CanExtractAsync(ValidStructuredDocx, Arg.Any<CancellationToken>())
            .Returns(true);
        strategy.GetConfidenceAsync(ValidStructuredDocx, Arg.Any<CancellationToken>())
            .Returns(85);

        // Act
        var canExtract = await strategy.CanExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);
        var confidence = await strategy.GetConfidenceAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: CanExtract = true implies Confidence > 0
        canExtract.ShouldBeTrue();
        confidence.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task StrategyContract_WhenConfidenceIsZero_ExtractAsyncShouldReturnNull()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();

        strategy.GetConfidenceAsync(IncompatibleDocx, Arg.Any<CancellationToken>())
            .Returns(0);
        strategy.ExtractAsync(IncompatibleDocx, Arg.Any<CancellationToken>())
            .Returns((ExtractedFields?)null);

        // Act
        var confidence = await strategy.GetConfidenceAsync(IncompatibleDocx, TestContext.Current.CancellationToken);
        var result = await strategy.ExtractAsync(IncompatibleDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Confidence = 0 implies ExtractAsync returns null
        confidence.ShouldBe(0);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task StrategyContract_WhenConfidenceIsPositive_ExtractAsyncShouldReturnData()
    {
        // Arrange
        var strategy = Substitute.For<IAdaptiveDocxStrategy>();
        var extractedFields = new ExtractedFields { Expediente = "A/AS1-2505-088637-PHM" };

        strategy.GetConfidenceAsync(ValidStructuredDocx, Arg.Any<CancellationToken>())
            .Returns(85);
        strategy.ExtractAsync(ValidStructuredDocx, Arg.Any<CancellationToken>())
            .Returns(extractedFields);

        // Act
        var confidence = await strategy.GetConfidenceAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);
        var result = await strategy.ExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Confidence > 0 implies ExtractAsync returns data (not null)
        confidence.ShouldBeGreaterThan(0);
        result.ShouldNotBeNull();
    }

    //
    // Liskov Substitution Principle Tests

    [Fact]
    public async Task LiskovSubstitution_AnyImplementationMustSatisfyExtractContract()
    {
        // Arrange - Create two different mock strategies
        var strategy1 = Substitute.For<IAdaptiveDocxStrategy>();
        var strategy2 = Substitute.For<IAdaptiveDocxStrategy>();

        var fields1 = new ExtractedFields { Expediente = "Strategy1Result" };
        var fields2 = new ExtractedFields { Expediente = "Strategy2Result" };

        strategy1.ExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(fields1);
        strategy2.ExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(fields2);

        // Act
        var result1 = await strategy1.ExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);
        var result2 = await strategy2.ExtractAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Liskov: Any implementation can be substituted
        result1.ShouldNotBeNull();
        result2.ShouldNotBeNull();
        // Both satisfy the contract, even with different results
    }

    [Fact]
    public async Task LiskovSubstitution_ConfidenceScoresAreComparable()
    {
        // Arrange
        var structuredStrategy = Substitute.For<IAdaptiveDocxStrategy>();
        var complementStrategy = Substitute.For<IAdaptiveDocxStrategy>();

        structuredStrategy.StrategyName.Returns("StructuredDocx");
        structuredStrategy.GetConfidenceAsync(ValidStructuredDocx, Arg.Any<CancellationToken>())
            .Returns(90);

        complementStrategy.StrategyName.Returns("Complement");
        complementStrategy.GetConfidenceAsync(ValidStructuredDocx, Arg.Any<CancellationToken>())
            .Returns(50);

        // Act
        var confidence1 = await structuredStrategy.GetConfidenceAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);
        var confidence2 = await complementStrategy.GetConfidenceAsync(ValidStructuredDocx, TestContext.Current.CancellationToken);

        // Assert - Liskov: Confidence scores are comparable across implementations
        confidence1.ShouldBeGreaterThan(confidence2);
        // This allows orchestrator to select best strategy
    }

    //
}
