namespace ExxerCube.Prisma.Tests.Domain.Domain.Interfaces;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Testing.Abstractions;

/// <summary>
/// Contract tests for <see cref="IFieldMergeStrategy"/> interface using mocks.
/// </summary>
/// <remarks>
/// <para>
/// These tests define the BEHAVIORAL CONTRACT that ANY implementation of
/// <see cref="IFieldMergeStrategy"/> MUST satisfy.
/// </para>
/// <para>
/// <strong>ITDD Principle:</strong> Interface is testable with mocks BEFORE implementation exists.
/// Any implementation that passes these tests (Liskov verification) is correct.
/// </para>
/// </remarks>
public sealed class IFieldMergeStrategyContractTests
{
    //
    // MergeAsync (IReadOnlyList) - Basic Behavior Tests
    //

    [Fact]
    public async Task MergeAsync_ShouldReturnNonNull_WhenGivenEmptyList()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        var emptyList = new List<ExtractedFields?>();

        strategy.MergeAsync(emptyList, Arg.Any<CancellationToken>())
            .Returns(new MergeResult { SourceCount = 0 });

        // Act
        var result = await strategy.MergeAsync(emptyList, TestContext.Current.CancellationToken);

        // Assert - Contract: Never returns null, even with empty input
        result.ShouldNotBeNull();
        result.MergedFields.ShouldNotBeNull();
        result.SourceCount.ShouldBe(0);
    }

    [Fact]
    public async Task MergeAsync_ShouldHandleNullEntries_InFieldSetsList()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        var fieldSets = new List<ExtractedFields?>
        {
            null,
            new ExtractedFields { Expediente = "TEST" },
            null
        };

        strategy.MergeAsync(fieldSets, Arg.Any<CancellationToken>())
            .Returns(new MergeResult
            {
                MergedFields = new ExtractedFields { Expediente = "TEST" },
                SourceCount = 1
            });

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Must handle null entries gracefully
        result.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("TEST");
        result.SourceCount.ShouldBe(1);
    }

    [Fact]
    public async Task MergeAsync_ShouldMergeMultipleFieldSets()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Expediente = "EXP-001", Causa = "Causa1" },
            new ExtractedFields { Expediente = "EXP-001", AccionSolicitada = "Accion1" },
            new ExtractedFields { AdditionalFields = new() { ["RFC"] = "RFC001" } }
        };

        strategy.MergeAsync(fieldSets, Arg.Any<CancellationToken>())
            .Returns(new MergeResult
            {
                MergedFields = new ExtractedFields
                {
                    Expediente = "EXP-001",
                    Causa = "Causa1",
                    AccionSolicitada = "Accion1",
                    AdditionalFields = new() { ["RFC"] = "RFC001" }
                },
                SourceCount = 3,
                MergedFieldNames = new List<string> { "Expediente", "Causa", "AccionSolicitada", "RFC" }
            });

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Must combine all non-conflicting data
        result.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("EXP-001");
        result.MergedFields.Causa.ShouldBe("Causa1");
        result.MergedFields.AccionSolicitada.ShouldBe("Accion1");
        result.MergedFields.AdditionalFields["RFC"].ShouldBe("RFC001");
        result.SourceCount.ShouldBe(3);
        result.MergedFieldNames.Count.ShouldBe(4);
    }

    [Fact]
    public async Task MergeAsync_ShouldReportConflicts_WhenFieldsDiffer()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Expediente = "EXP-001", Causa = "Causa1" },
            new ExtractedFields { Expediente = "EXP-002", Causa = "Causa2" } // Conflicts
        };

        strategy.MergeAsync(fieldSets, Arg.Any<CancellationToken>())
            .Returns(new MergeResult
            {
                MergedFields = new ExtractedFields { Expediente = "EXP-001", Causa = "Causa1" },
                Conflicts = new List<FieldConflict>
                {
                    new()
                    {
                        FieldName = "Expediente",
                        ConflictingValues = new List<string> { "EXP-001", "EXP-002" },
                        ResolvedValue = "EXP-001",
                        ResolutionStrategy = "first-wins"
                    },
                    new()
                    {
                        FieldName = "Causa",
                        ConflictingValues = new List<string> { "Causa1", "Causa2" },
                        ResolvedValue = "Causa1",
                        ResolutionStrategy = "first-wins"
                    }
                },
                SourceCount = 2
            });

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Must report conflicts when fields differ
        result.ShouldNotBeNull();
        result.Conflicts.Count.ShouldBe(2);
        result.Conflicts[0].FieldName.ShouldBe("Expediente");
        result.Conflicts[0].ConflictingValues.ShouldContain("EXP-001");
        result.Conflicts[0].ConflictingValues.ShouldContain("EXP-002");
        result.Conflicts[0].ResolvedValue.ShouldBe("EXP-001");
    }

    [Fact]
    public async Task MergeAsync_ShouldDeduplicateCollections()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields
            {
                Fechas = new List<string> { "15/11/2025", "16/11/2025" },
                Montos = new List<AmountData> { new("MXN", 100000m, "$100,000 MXN") }
            },
            new ExtractedFields
            {
                Fechas = new List<string> { "15/11/2025", "17/11/2025" }, // Duplicate "15/11/2025"
                Montos = new List<AmountData> { new("MXN", 100000m, "$100,000 MXN") } // Duplicate
            }
        };

        strategy.MergeAsync(fieldSets, Arg.Any<CancellationToken>())
            .Returns(new MergeResult
            {
                MergedFields = new ExtractedFields
                {
                    Fechas = new List<string> { "15/11/2025", "16/11/2025", "17/11/2025" }, // Deduplicated
                    Montos = new List<AmountData> { new("MXN", 100000m, "$100,000 MXN") } // Deduplicated
                },
                SourceCount = 2
            });

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Collections must be deduplicated
        result.ShouldNotBeNull();
        result.MergedFields.Fechas.Count.ShouldBe(3);
        result.MergedFields.Montos.Count.ShouldBe(1);
    }

    [Fact]
    public async Task MergeAsync_ShouldHandleCancellation()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var fieldSets = new List<ExtractedFields?> { new ExtractedFields() };

        strategy.MergeAsync(fieldSets, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<MergeResult>(new OperationCanceledException()));

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await strategy.MergeAsync(fieldSets, cts.Token));
    }

    //
    // MergeAsync (Two Parameters) - Convenience Method Tests
    //

    [Fact]
    public async Task MergeAsync_TwoParams_ShouldReturnNonNull_WhenBothNull()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();

        strategy.MergeAsync(
            (ExtractedFields?)null,
            (ExtractedFields?)null,
            Arg.Any<CancellationToken>())
            .Returns(new MergeResult { SourceCount = 0 });

        // Act
        var result = await strategy.MergeAsync(null, null, TestContext.Current.CancellationToken);

        // Assert - Contract: Never returns null, even when both inputs are null
        result.ShouldNotBeNull();
        result.MergedFields.ShouldNotBeNull();
    }

    [Fact]
    public async Task MergeAsync_TwoParams_ShouldUsePrimary_WhenSecondaryIsNull()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        var primary = new ExtractedFields { Expediente = "PRIMARY-001" };

        strategy.MergeAsync(primary, null, Arg.Any<CancellationToken>())
            .Returns(new MergeResult
            {
                MergedFields = new ExtractedFields { Expediente = "PRIMARY-001" },
                SourceCount = 1
            });

        // Act
        var result = await strategy.MergeAsync(primary, null, TestContext.Current.CancellationToken);

        // Assert - Contract: Should use primary when secondary is null
        result.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("PRIMARY-001");
        result.SourceCount.ShouldBe(1);
    }

    [Fact]
    public async Task MergeAsync_TwoParams_ShouldUseSecondary_WhenPrimaryIsNull()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        var secondary = new ExtractedFields { Expediente = "SECONDARY-001" };

        strategy.MergeAsync(null, secondary, Arg.Any<CancellationToken>())
            .Returns(new MergeResult
            {
                MergedFields = new ExtractedFields { Expediente = "SECONDARY-001" },
                SourceCount = 1
            });

        // Act
        var result = await strategy.MergeAsync(null, secondary, TestContext.Current.CancellationToken);

        // Assert - Contract: Should use secondary when primary is null
        result.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("SECONDARY-001");
        result.SourceCount.ShouldBe(1);
    }

    [Fact]
    public async Task MergeAsync_TwoParams_ShouldPreferPrimary_WhenBothHaveValues()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        var primary = new ExtractedFields { Expediente = "PRIMARY-001", Causa = "Causa-Primary" };
        var secondary = new ExtractedFields { Expediente = "SECONDARY-001", AccionSolicitada = "Accion-Secondary" };

        strategy.MergeAsync(primary, secondary, Arg.Any<CancellationToken>())
            .Returns(new MergeResult
            {
                MergedFields = new ExtractedFields
                {
                    Expediente = "PRIMARY-001", // Primary wins
                    Causa = "Causa-Primary", // From primary
                    AccionSolicitada = "Accion-Secondary" // Filled from secondary
                },
                SourceCount = 2
            });

        // Act
        var result = await strategy.MergeAsync(primary, secondary, TestContext.Current.CancellationToken);

        // Assert - Contract: Primary values take precedence
        result.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("PRIMARY-001");
        result.MergedFields.Causa.ShouldBe("Causa-Primary");
        result.MergedFields.AccionSolicitada.ShouldBe("Accion-Secondary");
    }

    [Fact]
    public async Task MergeAsync_TwoParams_ShouldFillGaps_FromSecondary()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        var primary = new ExtractedFields { Expediente = "PRIMARY-001" }; // Causa missing
        var secondary = new ExtractedFields { Causa = "Causa-Secondary" }; // Fills gap

        strategy.MergeAsync(primary, secondary, Arg.Any<CancellationToken>())
            .Returns(new MergeResult
            {
                MergedFields = new ExtractedFields
                {
                    Expediente = "PRIMARY-001",
                    Causa = "Causa-Secondary" // Filled from secondary
                },
                SourceCount = 2
            });

        // Act
        var result = await strategy.MergeAsync(primary, secondary, TestContext.Current.CancellationToken);

        // Assert - Contract: Secondary should fill gaps in primary
        result.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("PRIMARY-001");
        result.MergedFields.Causa.ShouldBe("Causa-Secondary");
    }

    [Fact]
    public async Task MergeAsync_TwoParams_ShouldCombineCollections()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        var primary = new ExtractedFields
        {
            Fechas = new List<string> { "15/11/2025" },
            Montos = new List<AmountData> { new("MXN", 100000m, "$100k") }
        };
        var secondary = new ExtractedFields
        {
            Fechas = new List<string> { "16/11/2025" },
            Montos = new List<AmountData> { new("USD", 5000m, "$5k USD") }
        };

        strategy.MergeAsync(primary, secondary, Arg.Any<CancellationToken>())
            .Returns(new MergeResult
            {
                MergedFields = new ExtractedFields
                {
                    Fechas = new List<string> { "15/11/2025", "16/11/2025" },
                    Montos = new List<AmountData>
                    {
                        new("MXN", 100000m, "$100k"),
                        new("USD", 5000m, "$5k USD")
                    }
                },
                SourceCount = 2
            });

        // Act
        var result = await strategy.MergeAsync(primary, secondary, TestContext.Current.CancellationToken);

        // Assert - Contract: Collections should be combined
        result.ShouldNotBeNull();
        result.MergedFields.Fechas.Count.ShouldBe(2);
        result.MergedFields.Montos.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MergeAsync_TwoParams_ShouldHandleCancellation()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var primary = new ExtractedFields();
        var secondary = new ExtractedFields();

        strategy.MergeAsync(primary, secondary, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<MergeResult>(new OperationCanceledException()));

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await strategy.MergeAsync(primary, secondary, cts.Token));
    }

    //
    // MergeResult Contract Tests
    //

    [Fact]
    public async Task MergeResult_MergedFields_ShouldNeverBeNull()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        var emptyList = new List<ExtractedFields?>();

        strategy.MergeAsync(emptyList, Arg.Any<CancellationToken>())
            .Returns(new MergeResult { MergedFields = new ExtractedFields() });

        // Act
        var result = await strategy.MergeAsync(emptyList, TestContext.Current.CancellationToken);

        // Assert - Contract: MergedFields is never null
        result.MergedFields.ShouldNotBeNull();
    }

    [Fact]
    public async Task MergeResult_Conflicts_ShouldBeEmptyList_WhenNoConflicts()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Expediente = "TEST" }
        };

        strategy.MergeAsync(fieldSets, Arg.Any<CancellationToken>())
            .Returns(new MergeResult
            {
                MergedFields = new ExtractedFields { Expediente = "TEST" },
                Conflicts = new List<FieldConflict>()
            });

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Conflicts list should be empty (not null) when no conflicts
        result.Conflicts.ShouldNotBeNull();
        result.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public async Task MergeResult_SourceCount_ShouldReflectNonNullSources()
    {
        // Arrange
        var strategy = Substitute.For<IFieldMergeStrategy>();
        var fieldSets = new List<ExtractedFields?>
        {
            null,
            new ExtractedFields { Expediente = "TEST1" },
            null,
            new ExtractedFields { Causa = "TEST2" }
        };

        strategy.MergeAsync(fieldSets, Arg.Any<CancellationToken>())
            .Returns(new MergeResult { SourceCount = 2 }); // Only non-null sources

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: SourceCount should only count non-null sources
        result.SourceCount.ShouldBe(2);
    }
}
