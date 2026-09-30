using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IFieldMergeStrategy"/> — every implementation
/// (and the mock blueprint) must pass these tests unchanged (ADR-005).
/// </summary>
/// <remarks>
/// <para>
/// The 16 <c>*_Liskov</c>-suffixed tests are the bodies lifted VERBATIM from
/// <c>EnhancedFieldMergeStrategyLiskovTests</c> (Tests.Infrastructure.Extraction.Adaptive,
/// the executable truth before this refactor) — names preserved, including the suffix,
/// because the suite is Stryker-hardened and kill power is keyed to them (plan §6.1).
/// Only the SUT-construction line changed (<c>new EnhancedFieldMergeStrategy(_logger)</c>
/// → <c>Sut</c>).
/// </para>
/// <para>
/// The 5 unsuffixed tests restore design-checklist behaviors from the original mock
/// blueprint (<c>IFieldMergeStrategyContractTests</c>) that had no real-SUT twin:
/// empty-list handling, 3-source merge incl. AdditionalFields, conflict ResolvedValue
/// detail, collection deduplication, and empty-Conflicts-when-no-conflict.
/// </para>
/// <para>
/// Cancellation contract: <c>MergeAsync</c> returns <see cref="MergeResult"/>
/// (not <c>Result&lt;T&gt;</c>), so a pre-cancelled token
/// surfaces as <see cref="OperationCanceledException"/> — the established contract both
/// twins already pinned.
/// </para>
/// </remarks>
public abstract class FieldMergeStrategyContract
{
    /// <summary>
    /// Initializes the contract with the implementation under test.
    /// </summary>
    /// <param name="sut">The <see cref="IFieldMergeStrategy"/> implementation to verify.</param>
    protected FieldMergeStrategyContract(IFieldMergeStrategy sut)
    {
        ArgumentNullException.ThrowIfNull(sut);
        Sut = sut;
    }

    /// <summary>Gets the implementation under test.</summary>
    protected IFieldMergeStrategy Sut { get; }

    //
    // Contract: MergeAsync (List Overload) — lifted Liskov bodies
    //

    /// <summary>Contract: non-null fields from multiple sources are combined.</summary>
    [Fact]
    public async Task MergeAsync_List_ShouldCombineNonNullFields_Liskov()
    {
        // Arrange
        var strategy = Sut;
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

    /// <summary>Contract: null entries in the list are skipped gracefully.</summary>
    [Fact]
    public async Task MergeAsync_List_ShouldHandleNullEntries_Liskov()
    {
        // Arrange
        var strategy = Sut;
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

    /// <summary>Contract: an all-null list yields an empty (never null) result.</summary>
    [Fact]
    public async Task MergeAsync_List_ShouldReturnEmptyResult_WhenAllNull_Liskov()
    {
        // Arrange
        var strategy = Sut;
        var fieldSets = new List<ExtractedFields?> { null, null };

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Should return empty result when all null
        result.ShouldNotBeNull();
        result.MergedFields.ShouldNotBeNull();
        result.SourceCount.ShouldBe(0);
    }

    /// <summary>Contract: differing values for the same field are reported as conflicts.</summary>
    [Fact]
    public async Task MergeAsync_List_ShouldDetectConflicts_WhenSameFieldHasDifferentValues_Liskov()
    {
        // Arrange
        var strategy = Sut;
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

    /// <summary>Contract: collection fields (Montos, Fechas) are combined across sources.</summary>
    [Fact]
    public async Task MergeAsync_List_ShouldCombineCollections_Liskov()
    {
        // Arrange
        var strategy = Sut;
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

    /// <summary>Contract: a pre-cancelled token surfaces as <see cref="OperationCanceledException"/>.</summary>
    [Fact]
    public async Task MergeAsync_List_ShouldHandleCancellation_Liskov()
    {
        // Arrange
        var strategy = Sut;
        var fieldSets = new List<ExtractedFields?> { new ExtractedFields() };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await strategy.MergeAsync(fieldSets, cts.Token));
    }

    //
    // Contract: MergeAsync (Two-Parameter Overload) — lifted Liskov bodies
    //

    /// <summary>Contract: primary values win when both sources have a value.</summary>
    [Fact]
    public async Task MergeAsync_TwoParams_ShouldPreferPrimary_WhenBothHaveValues_Liskov()
    {
        // Arrange
        var strategy = Sut;
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

    /// <summary>Contract: secondary fills gaps the primary leaves empty.</summary>
    [Fact]
    public async Task MergeAsync_TwoParams_ShouldFillFromSecondary_WhenPrimaryEmpty_Liskov()
    {
        // Arrange
        var strategy = Sut;
        var primary = new ExtractedFields { Expediente = "EXP-001" };
        var secondary = new ExtractedFields { Causa = "Causa-Secondary" };

        // Act
        var result = await strategy.MergeAsync(primary, secondary, TestContext.Current.CancellationToken);

        // Assert - Contract: Should fill gaps from secondary
        result.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("EXP-001");
        result.MergedFields.Causa.ShouldBe("Causa-Secondary");
    }

    /// <summary>Contract: a null primary is handled; secondary is used.</summary>
    [Fact]
    public async Task MergeAsync_TwoParams_ShouldHandleNullPrimary_Liskov()
    {
        // Arrange
        var strategy = Sut;
        var secondary = new ExtractedFields { Expediente = "EXP-001" };

        // Act
        var result = await strategy.MergeAsync(null, secondary, TestContext.Current.CancellationToken);

        // Assert - Contract: Should handle null primary
        result.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("EXP-001");
    }

    /// <summary>Contract: a null secondary is handled; primary is used.</summary>
    [Fact]
    public async Task MergeAsync_TwoParams_ShouldHandleNullSecondary_Liskov()
    {
        // Arrange
        var strategy = Sut;
        var primary = new ExtractedFields { Expediente = "EXP-001" };

        // Act
        var result = await strategy.MergeAsync(primary, null, TestContext.Current.CancellationToken);

        // Assert - Contract: Should handle null secondary
        result.ShouldNotBeNull();
        result.MergedFields.Expediente.ShouldBe("EXP-001");
    }

    /// <summary>Contract: both inputs null yields an empty (never null) result.</summary>
    [Fact]
    public async Task MergeAsync_TwoParams_ShouldReturnEmpty_WhenBothNull_Liskov()
    {
        // Arrange
        var strategy = Sut;

        // Act
        var result = await strategy.MergeAsync(null, null, TestContext.Current.CancellationToken);

        // Assert - Contract: Should return empty result when both null
        result.ShouldNotBeNull();
        result.MergedFields.ShouldNotBeNull();
        result.SourceCount.ShouldBe(0);
    }

    /// <summary>Contract: collections from primary and secondary are combined.</summary>
    [Fact]
    public async Task MergeAsync_TwoParams_ShouldCombineCollections_Liskov()
    {
        // Arrange
        var strategy = Sut;
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

    /// <summary>Contract: a pre-cancelled token surfaces as <see cref="OperationCanceledException"/>.</summary>
    [Fact]
    public async Task MergeAsync_TwoParams_ShouldHandleCancellation_Liskov()
    {
        // Arrange
        var strategy = Sut;
        var primary = new ExtractedFields();
        var secondary = new ExtractedFields();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await strategy.MergeAsync(primary, secondary, cts.Token));
    }

    //
    // Contract: MergeResult invariants — lifted Liskov bodies
    //

    /// <summary>Contract: SourceCount reflects only non-null sources.</summary>
    [Fact]
    public async Task MergeResult_ShouldTrackSourceCount_Liskov()
    {
        // Arrange
        var strategy = Sut;
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

    /// <summary>Contract: merged field names are tracked for diagnostics.</summary>
    [Fact]
    public async Task MergeResult_ShouldTrackMergedFieldNames_Liskov()
    {
        // Arrange
        var strategy = Sut;
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

    /// <summary>Contract: reported conflicts carry complete detail.</summary>
    [Fact]
    public async Task MergeResult_ShouldProvideConflictDetails_WhenConflictsDetected_Liskov()
    {
        // Arrange
        var strategy = Sut;
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

    //
    // Contract: design-checklist behaviors restored from the mock blueprint
    // (no real-SUT twin existed before this refactor)
    //

    /// <summary>Contract: an empty list yields a non-null, empty result.</summary>
    [Fact]
    public async Task MergeAsync_ShouldReturnNonNull_WhenGivenEmptyList()
    {
        // Arrange
        var strategy = Sut;
        var emptyList = new List<ExtractedFields?>();

        // Act
        var result = await strategy.MergeAsync(emptyList, TestContext.Current.CancellationToken);

        // Assert - Contract: Never returns null, even with empty input
        result.ShouldNotBeNull();
        result.MergedFields.ShouldNotBeNull();
        result.SourceCount.ShouldBe(0);
    }

    /// <summary>Contract: all non-conflicting data from three sources is preserved, including AdditionalFields.</summary>
    [Fact]
    public async Task MergeAsync_ShouldMergeMultipleFieldSets()
    {
        // Arrange
        var strategy = Sut;
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Expediente = "EXP-001", Causa = "Causa1" },
            new ExtractedFields { Expediente = "EXP-001", AccionSolicitada = "Accion1" },
            new ExtractedFields { AdditionalFields = new() { ["RFC"] = "RFC001" } }
        };

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

    /// <summary>Contract: conflicting fields report values and the resolved value.</summary>
    [Fact]
    public async Task MergeAsync_ShouldReportConflicts_WhenFieldsDiffer()
    {
        // Arrange
        var strategy = Sut;
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Expediente = "EXP-001", Causa = "Causa1" },
            new ExtractedFields { Expediente = "EXP-002", Causa = "Causa2" } // Conflicts
        };

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

    /// <summary>Contract: combined collections are deduplicated.</summary>
    [Fact]
    public async Task MergeAsync_ShouldDeduplicateCollections()
    {
        // Arrange
        var strategy = Sut;
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

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Collections must be deduplicated
        result.ShouldNotBeNull();
        result.MergedFields.Fechas.Count.ShouldBe(3);
        result.MergedFields.Montos.Count.ShouldBe(1);
    }

    /// <summary>Contract: Conflicts is an empty list (never null) when nothing conflicts.</summary>
    [Fact]
    public async Task MergeResult_Conflicts_ShouldBeEmptyList_WhenNoConflicts()
    {
        // Arrange
        var strategy = Sut;
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Expediente = "TEST" }
        };

        // Act
        var result = await strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        // Assert - Contract: Conflicts list should be empty (not null) when no conflicts
        result.Conflicts.ShouldNotBeNull();
        result.Conflicts.ShouldBeEmpty();
    }
}
