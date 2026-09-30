namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;
using ExxerCube.Prisma.Testing.Abstractions;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Mutation-killing tests for <see cref="EnhancedFieldMergeStrategy"/>.
/// </summary>
/// <remarks>
/// Written against the Stryker.NET survivor map (baseline 66.22%: Survived 10, NoCoverage 40). The
/// existing <c>*LiskovTests</c> verify the interface contract on happy paths but only ever exercise an
/// <em>Expediente</em> conflict, never assert conflict details (<see cref="FieldConflict.ResolvedValue"/>,
/// <see cref="FieldConflict.ResolutionStrategy"/>, <see cref="FieldConflict.ConflictingValues"/>), and never
/// merge <c>AdditionalFields</c> or feed duplicate <c>Montos</c>/<c>Fechas</c>. These tests pin those exact
/// behaviors plus the <c>MergedFieldNames</c> bookkeeping so the corresponding string/equality/object-init/
/// collection mutants become observable.
/// </remarks>
public sealed class EnhancedFieldMergeStrategyMutationKillingTests
{
    private readonly EnhancedFieldMergeStrategy _strategy;

    public EnhancedFieldMergeStrategyMutationKillingTests(ITestOutputHelper output)
    {
        var logger = XUnitLogger.CreateLogger<EnhancedFieldMergeStrategy>(output);
        _strategy = new EnhancedFieldMergeStrategy(logger);
    }

    private static ExtractedFields Fields(string? exp = null, string? causa = null, string? accion = null)
        => new() { Expediente = exp, Causa = causa, AccionSolicitada = accion };

    // ---------------------------------------------------------------------
    // List overload — null guard (L42).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MergeAsync_List_NullFieldSets_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _strategy.MergeAsync((IReadOnlyList<ExtractedFields?>)null!, TestContext.Current.CancellationToken));
    }

    // ---------------------------------------------------------------------
    // List overload — conflict DETAILS for every core field + "first non-null wins"
    // resolution string (L169-175 Expediente, L193-199 Causa, L217-223 Accion).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MergeAsync_List_ExpedienteConflict_RecordsFirstNonNullDetails()
    {
        var fieldSets = new List<ExtractedFields?> { Fields(exp: "EXP-A"), Fields(exp: "EXP-B") };

        var result = await _strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        result.MergedFields.Expediente.ShouldBe("EXP-A"); // first non-null wins
        var conflict = result.Conflicts.ShouldHaveSingleItem();
        conflict.FieldName.ShouldBe("Expediente");
        conflict.ResolvedValue.ShouldBe("EXP-A");
        conflict.ResolutionStrategy.ShouldBe("First non-null value");
        conflict.ConflictingValues.ShouldBe(new List<string> { "EXP-A", "EXP-B" });
    }

    [Fact]
    public async Task MergeAsync_List_CausaConflict_RecordsFirstNonNullDetails()
    {
        var fieldSets = new List<ExtractedFields?> { Fields(causa: "Causa-A"), Fields(causa: "Causa-B") };

        var result = await _strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        result.MergedFields.Causa.ShouldBe("Causa-A");
        var conflict = result.Conflicts.ShouldHaveSingleItem();
        conflict.FieldName.ShouldBe("Causa");
        conflict.ResolvedValue.ShouldBe("Causa-A");
        conflict.ResolutionStrategy.ShouldBe("First non-null value");
        conflict.ConflictingValues.ShouldBe(new List<string> { "Causa-A", "Causa-B" });
    }

    [Fact]
    public async Task MergeAsync_List_AccionConflict_RecordsFirstNonNullDetails()
    {
        var fieldSets = new List<ExtractedFields?> { Fields(accion: "Accion-A"), Fields(accion: "Accion-B") };

        var result = await _strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        result.MergedFields.AccionSolicitada.ShouldBe("Accion-A");
        var conflict = result.Conflicts.ShouldHaveSingleItem();
        conflict.FieldName.ShouldBe("AccionSolicitada");
        conflict.ResolvedValue.ShouldBe("Accion-A");
        conflict.ResolutionStrategy.ShouldBe("First non-null value");
        conflict.ConflictingValues.ShouldBe(new List<string> { "Accion-A", "Accion-B" });
    }

    // ---------------------------------------------------------------------
    // List overload — identical values must NOT raise a conflict (Distinct + Count>1 guard,
    // L162/L167/L186/L191/L210/L215).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MergeAsync_List_IdenticalValues_MergesWithoutConflict()
    {
        var fieldSets = new List<ExtractedFields?> { Fields(exp: "SAME"), Fields(exp: "SAME") };

        var result = await _strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        result.MergedFields.Expediente.ShouldBe("SAME");
        result.MergedFieldNames.ShouldContain("Expediente");
        result.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public async Task MergeAsync_List_SingleSource_NoConflictButTracksFieldNames()
    {
        var fieldSets = new List<ExtractedFields?> { Fields(exp: "EXP-1", causa: "C1", accion: "A1") };

        var result = await _strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        result.Conflicts.ShouldBeEmpty();
        result.MergedFieldNames.ShouldContain("Expediente");
        result.MergedFieldNames.ShouldContain("Causa");
        result.MergedFieldNames.ShouldContain("AccionSolicitada");
    }

    // ---------------------------------------------------------------------
    // Two-param overload — conflict DETAILS with "Primary source preference"
    // (L241-247 Expediente, L262/L264-270 Causa, L285/L287-293 Accion).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MergeAsync_TwoParams_ExpedienteConflict_RecordsPrimaryPreferenceDetails()
    {
        var result = await _strategy.MergeAsync(
            Fields(exp: "PRIMARY"), Fields(exp: "SECONDARY"), TestContext.Current.CancellationToken);

        result.MergedFields.Expediente.ShouldBe("PRIMARY");
        var conflict = result.Conflicts.ShouldHaveSingleItem();
        conflict.FieldName.ShouldBe("Expediente");
        conflict.ResolvedValue.ShouldBe("PRIMARY");
        conflict.ResolutionStrategy.ShouldBe("Primary source preference");
        conflict.ConflictingValues.ShouldBe(new List<string> { "PRIMARY", "SECONDARY" });
    }

    [Fact]
    public async Task MergeAsync_TwoParams_CausaConflict_RecordsPrimaryPreferenceDetails()
    {
        var result = await _strategy.MergeAsync(
            Fields(causa: "Causa-P"), Fields(causa: "Causa-S"), TestContext.Current.CancellationToken);

        result.MergedFields.Causa.ShouldBe("Causa-P");
        var conflict = result.Conflicts.ShouldHaveSingleItem();
        conflict.FieldName.ShouldBe("Causa");
        conflict.ResolvedValue.ShouldBe("Causa-P");
        conflict.ResolutionStrategy.ShouldBe("Primary source preference");
        conflict.ConflictingValues.ShouldBe(new List<string> { "Causa-P", "Causa-S" });
    }

    [Fact]
    public async Task MergeAsync_TwoParams_AccionConflict_RecordsPrimaryPreferenceDetails()
    {
        var result = await _strategy.MergeAsync(
            Fields(accion: "Accion-P"), Fields(accion: "Accion-S"), TestContext.Current.CancellationToken);

        result.MergedFields.AccionSolicitada.ShouldBe("Accion-P");
        var conflict = result.Conflicts.ShouldHaveSingleItem();
        conflict.FieldName.ShouldBe("AccionSolicitada");
        conflict.ResolvedValue.ShouldBe("Accion-P");
        conflict.ResolutionStrategy.ShouldBe("Primary source preference");
        conflict.ConflictingValues.ShouldBe(new List<string> { "Accion-P", "Accion-S" });
    }

    [Fact]
    public async Task MergeAsync_TwoParams_IdenticalValues_NoConflict()
    {
        var result = await _strategy.MergeAsync(
            Fields(exp: "SAME", causa: "SAME-C"), Fields(exp: "SAME", causa: "SAME-C"),
            TestContext.Current.CancellationToken);

        result.MergedFields.Expediente.ShouldBe("SAME");
        result.MergedFields.Causa.ShouldBe("SAME-C");
        result.Conflicts.ShouldBeEmpty();
    }

    // ---------------------------------------------------------------------
    // Two-param overload — primary present: every core field name is tracked
    // (kills the MergedFieldNames.Add("...") string mutants L237/L260/L283).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MergeAsync_TwoParams_PrimaryOnly_TracksAllFieldNames()
    {
        var result = await _strategy.MergeAsync(
            Fields(exp: "E", causa: "C", accion: "A"), null, TestContext.Current.CancellationToken);

        result.MergedFields.Expediente.ShouldBe("E");
        result.MergedFields.Causa.ShouldBe("C");
        result.MergedFields.AccionSolicitada.ShouldBe("A");
        result.MergedFieldNames.ShouldContain("Expediente");
        result.MergedFieldNames.ShouldContain("Causa");
        result.MergedFieldNames.ShouldContain("AccionSolicitada");
        result.Conflicts.ShouldBeEmpty();
    }

    // ---------------------------------------------------------------------
    // Two-param overload — secondary fills gaps when primary is empty
    // (L250-253 Expediente, L273-276 Causa, L296-299 Accion else-if branches).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MergeAsync_TwoParams_PrimaryEmpty_FillsAllFieldsFromSecondaryAndTracksNames()
    {
        var result = await _strategy.MergeAsync(
            primary: new ExtractedFields(),
            secondary: Fields(exp: "S-E", causa: "S-C", accion: "S-A"),
            TestContext.Current.CancellationToken);

        result.MergedFields.Expediente.ShouldBe("S-E");
        result.MergedFields.Causa.ShouldBe("S-C");
        result.MergedFields.AccionSolicitada.ShouldBe("S-A");
        result.MergedFieldNames.ShouldContain("Expediente");
        result.MergedFieldNames.ShouldContain("Causa");
        result.MergedFieldNames.ShouldContain("AccionSolicitada");
        result.Conflicts.ShouldBeEmpty(); // secondary-fill is not a conflict
    }

    [Fact]
    public async Task MergeAsync_TwoParams_TracksSourceCount()
    {
        var both = await _strategy.MergeAsync(Fields(exp: "E"), Fields(causa: "C"), TestContext.Current.CancellationToken);
        both.SourceCount.ShouldBe(2);

        var onlyPrimary = await _strategy.MergeAsync(Fields(exp: "E"), null, TestContext.Current.CancellationToken);
        onlyPrimary.SourceCount.ShouldBe(1);

        var onlySecondary = await _strategy.MergeAsync(null, Fields(exp: "E"), TestContext.Current.CancellationToken);
        onlySecondary.SourceCount.ShouldBe(1);
    }

    // ---------------------------------------------------------------------
    // Collection merge — AdditionalFields first-key-wins + "AdditionalFields.{key}" naming
    // (L314 ContainsKey guard, L316 assignment, L317 name).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MergeAsync_List_AdditionalFields_FirstKeyWinsAndNamesAreTracked()
    {
        var source1 = new ExtractedFields { AdditionalFields = { ["Email"] = "first@x.mx" } };
        var source2 = new ExtractedFields { AdditionalFields = { ["Email"] = "second@x.mx", ["Telefono"] = "555" } };
        var fieldSets = new List<ExtractedFields?> { source1, source2 };

        var result = await _strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        result.MergedFields.AdditionalFields["Email"].ShouldBe("first@x.mx"); // first source wins
        result.MergedFields.AdditionalFields["Telefono"].ShouldBe("555");
        result.MergedFieldNames.ShouldContain("AdditionalFields.Email");
        result.MergedFieldNames.ShouldContain("AdditionalFields.Telefono");
    }

    // ---------------------------------------------------------------------
    // Collection merge — Montos deduped by (Currency, Value) (L322-327, esp. the
    // m.Currency == ... && m.Value == ... equality at L324).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MergeAsync_List_Montos_ExactDuplicate_IsDeduped()
    {
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Montos = { new AmountData("MXN", 100m, "$100") } },
            new ExtractedFields { Montos = { new AmountData("MXN", 100m, "different text, same amount") } }
        };

        var result = await _strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        result.MergedFields.Montos.Count.ShouldBe(1);
    }

    [Fact]
    public async Task MergeAsync_List_Montos_SameCurrencyDifferentValue_BothKept()
    {
        // Kills the m.Value == monto.Value mutant: flipping it would dedupe these wrongly.
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Montos = { new AmountData("MXN", 100m, "$100") } },
            new ExtractedFields { Montos = { new AmountData("MXN", 200m, "$200") } }
        };

        var result = await _strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        result.MergedFields.Montos.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MergeAsync_List_Montos_SameValueDifferentCurrency_BothKept()
    {
        // Kills the m.Currency == monto.Currency mutant.
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Montos = { new AmountData("MXN", 100m, "$100") } },
            new ExtractedFields { Montos = { new AmountData("USD", 100m, "$100") } }
        };

        var result = await _strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        result.MergedFields.Montos.Count.ShouldBe(2);
    }

    // ---------------------------------------------------------------------
    // Collection merge — Fechas deduped by exact match (L331-336).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MergeAsync_List_Fechas_ExactDuplicate_IsDeduped()
    {
        var fieldSets = new List<ExtractedFields?>
        {
            new ExtractedFields { Fechas = { "01/01/2025" } },
            new ExtractedFields { Fechas = { "01/01/2025" } }
        };

        var result = await _strategy.MergeAsync(fieldSets, TestContext.Current.CancellationToken);

        result.MergedFields.Fechas.Count.ShouldBe(1);
    }
}
