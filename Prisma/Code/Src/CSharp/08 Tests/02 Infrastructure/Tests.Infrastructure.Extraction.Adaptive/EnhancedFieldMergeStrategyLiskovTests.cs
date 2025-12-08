namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;
using ExxerCube.Prisma.Testing.Abstractions;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Liskov Substitution Principle verification tests for <see cref="EnhancedFieldMergeStrategy"/>.
/// </summary>
/// <remarks>
/// <para>
/// These tests prove that EnhancedFieldMergeStrategy correctly implements <see cref="IFieldMergeStrategy"/>
/// by running the SAME contract tests that were defined in Tests.Domain against the ACTUAL implementation.
/// </para>
/// <para>
/// <strong>ITDD Principle:</strong> If the implementation passes all interface contract tests,
/// it satisfies the Liskov Substitution Principle and is correct.
/// </para>
/// </remarks>
public sealed class EnhancedFieldMergeStrategyLiskovTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<EnhancedFieldMergeStrategy> _logger;

    public EnhancedFieldMergeStrategyLiskovTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<EnhancedFieldMergeStrategy>(output);
    }

    //
    // Liskov Verification: MergeAsync (List Overload)
    //

    [Fact]
    public async Task MergeAsync_List_ShouldCombineNonNullFields_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Expediente = "EXP-001", Causa = "Causa1" },
            new ExtractedFields { AccionSolicitada = "Accion1" }
        };

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Must combine non-null fields
        result.ShouldNotBeNull();
        result.MergedFields.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("EXP-001");
        result.MergedFields.Causa.ShouldBe("Causa1");
        result.MergedFields.AccionSolicitada.ShouldBe("Accion1");
    }

    [Fact]
    public async Task MergeAsync_List_ShouldHandleNullEntries_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Expediente = "EXP-001" },
            null,
            new ExtractedFields { Causa = "Causa1" }
        };

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Should skip null entries
        result.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("EXP-001");
        result.MergedFields.Causa.ShouldBe("Causa1");
        result.SourceCount.ShouldBe(2); // Only 2 non-null sources
    }

    [Fact]
    public async Task MergeAsync_List_ShouldReturnEmptyResult_WhenAllNull_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var fieldSets = new List<ExtractedFields?> { null, null };

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Should return empty result when all null
        result.ShouldNotBeNull();
        result.MergedFields.ShouldNotBeNull();
        result.SourceCount.ShouldBe(0);
    }

    [Fact]
    public async Task MergeAsync_List_ShouldDetectConflicts_WhenSameFieldHasDifferentValues_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Expediente = "EXP-001" },
            new ExtractedFields { Expediente = "EXP-002" }
        };

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Should detect conflicts
        result.ShouldNotBeNull();
        result.Conflicts.Count.ShouldBeGreaterThan(0);
        result.Conflicts.ShouldContain(c => c.FieldName == "Expediente");
    }

    [Fact]
    public async Task MergeAsync_List_ShouldCombineCollections_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields
            {
                Montos = { new AmountData("MXN", 100m, "$100") },
                Fechas = { "01/01/2025" }
            },
            new ExtractedFields
            {
                Montos = { new AmountData("USD", 50m, "$50") },
                Fechas = { "02/01/2025" }
            }
        };

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Should combine collections
        result.ShouldNotBeNull();
        result.MergedFields.Montos.Count.ShouldBe(2);
        result.MergedFields.Fechas.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MergeAsync_List_ShouldHandleCancellation_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var fieldSets = new List<ExtractedFields?> { new ExtractedFields() };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await strategy.MergeAsync(fieldSets, cts.Token));
    }

    //
    // Liskov Verification: MergeAsync (Two-Parameter Overload)
    //

    [Fact]
    public async Task MergeAsync_TwoParams_ShouldPreferPrimary_WhenBothHaveValues_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var primary = new ExtractedFields { Expediente = "PRIMARY", Causa = "Causa-Primary" };
        var secondary = new ExtractedFields { Expediente = "SECONDARY", AccionSolicitada = "Accion-Secondary" };

        // Act
        var result = await strategy.MergeAsync(primary, secondary, TestContext.Current.CancellationToken);

        // Assert - Contract: Primary should win for conflicts
        result.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("PRIMARY");
        result.MergedFields.Causa.ShouldBe("Causa-Primary");
        result.MergedFields.AccionSolicitada.ShouldBe("Accion-Secondary");
    }

    [Fact]
    public async Task MergeAsync_TwoParams_ShouldFillFromSecondary_WhenPrimaryEmpty_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var primary = new ExtractedFields { Expediente = "EXP-001" };
        var secondary = new ExtractedFields { Causa = "Causa-Secondary" };

        // Act
        var result = await strategy.MergeAsync(primary, secondary, TestContext.Current.CancellationToken);

        // Assert - Contract: Should fill gaps from secondary
        result.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("EXP-001");
        result.MergedFields.Causa.ShouldBe("Causa-Secondary");
    }

    [Fact]
    public async Task MergeAsync_TwoParams_ShouldHandleNullPrimary_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var secondary = new ExtractedFields { Expediente = "EXP-001" };

        // Act
        var result = await strategy.MergeAsync(null, secondary, TestContext.Current.CancellationToken);

        // Assert - Contract: Should handle null primary
        result.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("EXP-001");
    }

    [Fact]
    public async Task MergeAsync_TwoParams_ShouldHandleNullSecondary_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var primary = new ExtractedFields { Expediente = "EXP-001" };

        // Act
        var result = await strategy.MergeAsync(primary, null, TestContext.Current.CancellationToken);

        // Assert - Contract: Should handle null secondary
        result.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("EXP-001");
    }

    [Fact]
    public async Task MergeAsync_TwoParams_ShouldReturnEmpty_WhenBothNull_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);

        // Act
        var result = await strategy.MergeAsync(null, null, TestContext.Current.CancellationToken);

        // Assert - Contract: Should return empty result when both null
        result.ShouldNotBeNull();
        result.MergedFields.ShouldNotBeNull();
        result.SourceCount.ShouldBe(0);
    }

    [Fact]
    public async Task MergeAsync_TwoParams_ShouldCombineCollections_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var primary = new ExtractedFields
        {
            Montos = { new AmountData("MXN", 100m, "$100") },
            Fechas = { "01/01/2025" }
        };
        var secondary = new ExtractedFields
        {
            Montos = { new AmountData("USD", 50m, "$50") },
            Fechas = { "02/01/2025" }
        };

        // Act
        var result = await strategy.MergeAsync(primary, secondary, TestContext.Current.CancellationToken);

        // Assert - Contract: Should combine collections
        result.ShouldNotBeNull();
        result.MergedFields.Montos.Count.ShouldBe(2);
        result.MergedFields.Fechas.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MergeAsync_TwoParams_ShouldHandleCancellation_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var primary = new ExtractedFields();
        var secondary = new ExtractedFields();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await strategy.MergeAsync(primary, secondary, cts.Token));
    }

    //
    // Liskov Verification: MergeResult Contracts
    //

    [Fact]
    public async Task MergeResult_ShouldTrackSourceCount_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Expediente = "EXP-001" },
            new ExtractedFields { Causa = "Causa1" },
            null
        };

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: SourceCount should reflect non-null sources
        result.ShouldNotBeNull();
        result.SourceCount.ShouldBe(2);
    }

    [Fact]
    public async Task MergeResult_ShouldTrackMergedFieldNames_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Expediente = "EXP-001", Causa = "Causa1" }
        };

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Should track which fields were merged
        result.ShouldNotBeNull();
        result.MergedFieldNames.ShouldNotBeNull();
        result.MergedFieldNames.ShouldContain("Expediente");
        result.MergedFieldNames.ShouldContain("Causa");
    }

    [Fact]
    public async Task MergeResult_ShouldProvideConflictDetails_WhenConflictsDetected_Liskov()
    {
        // Arrange
        var strategy = new EnhancedFieldMergeStrategy(_logger);
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Expediente = "EXP-001" },
            new ExtractedFields { Expediente = "EXP-002" }
        };

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Conflicts should have details
        result.ShouldNotBeNull();
        if (result.Conflicts.Count > 0)
        {
            var conflict = result.Conflicts[0];
            conflict.FieldName.ShouldNotBeNullOrWhiteSpace();
            conflict.ConflictingValues.ShouldNotBeNull();
            conflict.ConflictingValues.Count.ShouldBeGreaterThan(1);
            conflict.ResolutionStrategy.ShouldNotBeNullOrWhiteSpace();
        }
    }
}
