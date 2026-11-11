namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

using ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Mutation-killing tests for <see cref="NameMatchingPolicy"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="NameMatchingPolicy"/> was previously never exercised directly (only constructed inside the
/// FieldMatcher tests, which use non-name fields), so its baseline mutation score was ~3%. These tests pin
/// its observable contract: the input guards, the produced <see cref="FieldMatchResult"/> fields, and the
/// score for identical names.
/// </para>
/// <para>
/// <strong>Important — these tests deliberately only assert behavior that is correct regardless of the
/// self-pairing defect documented in
/// <c>docs/qa/findings/2026-06-08-namematchingpolicy-self-pairing-defeats-fuzzy-matching.md</c>.</strong>
/// Because both <c>SelectBestValueAsync</c> and <c>CalculateAgreementLevelAsync</c> score every value against
/// <em>itself</em> (and <c>ScorePair(x, x) == 1.0</c>), the result is always driven by the diagonal: the
/// winner is always the first value, the score is always 1.0, and a conflict is never reported. That makes
/// the fuzzy / alias / accent-normalization logic (~70% of the file) unobservable, so those mutants cannot
/// be killed through the public API until the defect is fixed. We therefore assert only identical-name and
/// guard behavior (where 1.0 / no-conflict is the *correct* answer) — these tests will still pass after the
/// defect is fixed, and will then be joined by real cross-name tests.
/// </para>
/// </remarks>
public sealed class NameMatchingPolicyMutationKillingTests
{
    private static NameMatchingPolicy Policy(NameMatchingOptions? options = null)
        => new(new StaticOptionsMonitor<NameMatchingOptions>(options ?? new NameMatchingOptions()),
               Substitute.For<ILogger<NameMatchingPolicy>>());

    private static FieldValue FV(string? value, string sourceType = "DOCX",
        FieldOrigin origin = FieldOrigin.Docx, string? raw = null)
        => new("NombreSolicitante", value, 1.0f, sourceType, origin, raw);

    // ---------------------------------------------------------------------
    // SelectBestValueAsync — input guards (L44-46, L50-56).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SelectBestValueAsync_Null_FailsWithNoValuesMessage()
    {
        var result = await Policy().SelectBestValueAsync("NombreSolicitante", null!);

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("No values to match");
    }

    [Fact]
    public async Task SelectBestValueAsync_Empty_FailsWithNoValuesMessage()
    {
        var result = await Policy().SelectBestValueAsync("NombreSolicitante", new List<FieldValue>());

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("No values to match");
    }

    [Fact]
    public async Task SelectBestValueAsync_AllValuesHaveNullText_FailsWithNoNonEmptyMessage()
    {
        var values = new List<FieldValue> { FV(null), FV(null) };

        var result = await Policy().SelectBestValueAsync("NombreSolicitante", values);

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("No non-empty values to match");
    }

    [Fact]
    public async Task SelectBestValueAsync_SkipsNullTextValues_AndWinsFromTheRealValue()
    {
        // First entry has null text and must be filtered out (L50); the real value wins.
        var values = new List<FieldValue> { FV(null), FV("Juan Pérez García", "PDF", FieldOrigin.PdfOcr) };

        var result = await Policy().SelectBestValueAsync("NombreSolicitante", values);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.MatchedValue.ShouldBe("Juan Pérez García");
        result.Value.SourceType.ShouldBe("PDF");
    }

    // ---------------------------------------------------------------------
    // SelectBestValueAsync — result construction for an exact match (L66, L69-73).
    // Identical names => score 1.0 and no conflict, which is correct regardless of the
    // self-pairing defect.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SelectBestValueAsync_IdenticalNames_BuildsFullResultWithPerfectScore()
    {
        var values = new List<FieldValue>
        {
            FV("Juan Pérez", "XML", FieldOrigin.Xml, raw: "RAW-XML"),
            FV("Juan Pérez", "PDF", FieldOrigin.PdfOcr)
        };

        var result = await Policy().SelectBestValueAsync("NombreSolicitante", values);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.MatchedValue.ShouldBe("Juan Pérez");
        result.Value.Confidence.ShouldBe(1.0f);
        result.Value.AgreementLevel.ShouldBe(1.0f);
        result.Value.HasConflict.ShouldBeFalse();      // 1.0 is not below ConflictThreshold (0.80)
        result.Value.SourceType.ShouldBe("XML");        // winner is the first value
        result.Value.Origin.ShouldBe(FieldOrigin.Xml);
        result.Value.RawValue.ShouldBe("RAW-XML");
        result.Value.AllValues.Count.ShouldBe(2);
    }

    // ---------------------------------------------------------------------
    // CalculateAgreementLevelAsync — guards (L89-101) + the Max aggregation (L108).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CalculateAgreementLevelAsync_Null_Fails()
    {
        var result = await Policy().CalculateAgreementLevelAsync(null!);

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("Not enough values to compare");
    }

    [Fact]
    public async Task CalculateAgreementLevelAsync_SingleValue_Fails()
    {
        var result = await Policy().CalculateAgreementLevelAsync(new List<FieldValue> { FV("Juan Pérez") });

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("Not enough values to compare");
    }

    [Fact]
    public async Task CalculateAgreementLevelAsync_TwoButOneHasNullText_FailsAsNotComparable()
    {
        // Two entries, but one has null text -> only one comparable value remains (L95/L99/L101).
        var values = new List<FieldValue> { FV("Juan Pérez"), FV(null) };

        var result = await Policy().CalculateAgreementLevelAsync(values);

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("Not enough comparable values");
    }

    [Fact]
    public async Task CalculateAgreementLevelAsync_TwoIdenticalNames_ReturnsPerfectScore()
    {
        // Kills the Math.Max -> Math.Min mutant (Min would leave the seed 0.0).
        var values = new List<FieldValue> { FV("Juan Pérez"), FV("Juan Pérez") };

        var result = await Policy().CalculateAgreementLevelAsync(values);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1.0f);
    }

    // ---------------------------------------------------------------------
    // HasConflictAsync — failure propagation (L120/L122) + threshold comparison (L124).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HasConflictAsync_NotEnoughValues_PropagatesFailure()
    {
        var result = await Policy().HasConflictAsync(new List<FieldValue> { FV("Juan Pérez") });

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("Not enough values");
    }

    [Fact]
    public async Task HasConflictAsync_IdenticalNames_DefaultThreshold_NoConflict()
    {
        var values = new List<FieldValue> { FV("Juan Pérez"), FV("Juan Pérez") };

        var result = await Policy().HasConflictAsync(values);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse(); // agreement 1.0 is not below the 0.5 default threshold
    }

    [Fact]
    public async Task HasConflictAsync_IdenticalNames_ThresholdAboveScore_ReportsConflict()
    {
        // A threshold above the (perfect) score must flip the comparison (pins agreement == 1.0
        // and kills the agreement.Value > threshold variant of L124).
        var values = new List<FieldValue> { FV("Juan Pérez"), FV("Juan Pérez") };

        var result = await Policy().HasConflictAsync(values, threshold: 2.0f);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }
}
