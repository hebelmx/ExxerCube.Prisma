namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

using ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Mutation-killing tests for <see cref="MatchingPolicyService"/>.
/// </summary>
/// <remarks>
/// Written against the Stryker.NET survivor map (baseline 41.05%). The existing
/// <c>MatchingPolicyServiceTests</c> cover the common single/agree/conflict/priority paths but never hit the
/// all-empty "NONE" result, the exact <c>finalConfidence = agreement * avgConfidence</c> arithmetic, the
/// null/single edges of <c>CalculateAgreementLevelAsync</c>/<c>HasConflictAsync</c>, the custom-threshold
/// ternary, or the source-priority resolution paths (options-level fallback + per-field rule override).
/// These tests pin those.
///
/// Several residual mutants are equivalent and intentionally left: <c>First()</c> -> <c>FirstOrDefault()</c>
/// on groups that are always non-empty, and the <c>ApplySourcePriority</c> reordering (it cannot change the
/// selected *value*, only tie-break order, which is not otherwise observable).
/// </remarks>
public sealed class MatchingPolicyServiceMutationKillingTests
{
    private static MatchingPolicyService Svc(MatchingPolicyOptions? options = null)
        => new(Options.Create(options ?? new MatchingPolicyOptions()),
               Substitute.For<ILogger<MatchingPolicyService>>());

    private static FieldValue FV(string? value, float confidence, string sourceType,
        FieldOrigin origin = FieldOrigin.Unknown)
        => new("F", value, confidence, sourceType, origin);

    // ---------------------------------------------------------------------
    // SelectBestValueAsync — failure message + the all-empty "NONE" result (L41, L48).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SelectBestValueAsync_EmptyList_FailsWithNoValuesMessage()
    {
        var result = await Svc().SelectBestValueAsync("F", new List<FieldValue>());

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("No values");
    }

    [Fact]
    public async Task SelectBestValueAsync_OnlyBlankValues_ReturnsNoneSentinel()
    {
        var values = new List<FieldValue> { FV("   ", 1.0f, "DOCX"), FV("", 0.9f, "PDF") };

        var result = await Svc().SelectBestValueAsync("F", values);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.MatchedValue.ShouldBeNull();
        result.Value.Confidence.ShouldBe(0.0f);
        result.Value.SourceType.ShouldBe("NONE");
    }

    [Fact]
    public async Task SelectBestValueAsync_SingleValue_PreservesSourceTypeAndAllValues()
    {
        var values = new List<FieldValue> { FV("X", 0.7f, "PDF") };

        var result = await Svc().SelectBestValueAsync("F", values);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.MatchedValue.ShouldBe("X");
        result.Value.SourceType.ShouldBe("PDF");
        result.Value.Confidence.ShouldBe(0.7f);
        result.Value.AgreementLevel.ShouldBe(1.0f);
        result.Value.HasConflict.ShouldBeFalse();
        result.Value.AllValues.Count.ShouldBe(1);
    }

    // ---------------------------------------------------------------------
    // finalConfidence = agreementLevel * average(confidences)  (L85 Average->Min, L86 *->/).
    // Two agreeing values with different confidences: agreement 1.0, avg 0.8 -> 0.8.
    // (Min would give 0.6; division would give 1.25.)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SelectBestValueAsync_AgreeingValues_ConfidenceIsAgreementTimesAverage()
    {
        var values = new List<FieldValue> { FV("X", 1.0f, "DOCX"), FV("X", 0.6f, "PDF") };

        var result = await Svc().SelectBestValueAsync("F", values);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AgreementLevel.ShouldBe(1.0f);
        result.Value.Confidence.ShouldBe(0.8f, 0.0001); // 1.0 * average(1.0, 0.6)
    }

    // ---------------------------------------------------------------------
    // Source-priority resolution for the WINNING source type.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SelectBestValueAsync_NoSourcePriority_FallsBackToHighestConfidenceSource()
    {
        // Empty SourcePriority -> SelectBestSourceType uses the highest-confidence source (L239).
        var svc = Svc(new MatchingPolicyOptions { SourcePriority = new List<string>() });
        var values = new List<FieldValue> { FV("X", 0.7f, "DOCX"), FV("X", 1.0f, "PDF") };

        var result = await svc.SelectBestValueAsync("F", values);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.SourceType.ShouldBe("PDF"); // highest confidence wins, not DOCX
    }

    [Fact]
    public async Task SelectBestValueAsync_FieldSpecificPriority_OverridesOptionsLevelPriority()
    {
        // Options prefer XML; the per-field rule prefers PDF. The field rule must win (L201/L221).
        var options = new MatchingPolicyOptions
        {
            SourcePriority = new List<string> { "XML", "DOCX", "PDF" },
            FieldRules =
            {
                ["Expediente"] = new FieldMatchingRule { SourcePriority = new List<string> { "PDF", "DOCX", "XML" } }
            }
        };
        var values = new List<FieldValue>
        {
            FV("X", 0.8f, "XML"), FV("X", 0.9f, "DOCX"), FV("X", 1.0f, "PDF")
        };

        var result = await Svc(options).SelectBestValueAsync("Expediente", values);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.SourceType.ShouldBe("PDF"); // field rule (PDF-first) beats options (XML-first)
    }

    // ---------------------------------------------------------------------
    // CalculateAgreementLevelAsync — null / all-blank / single edges (L118-131).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CalculateAgreementLevelAsync_Null_ReturnsZeroSuccessfully()
    {
        var result = await Svc().CalculateAgreementLevelAsync(null!);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(0.0f);
    }

    [Fact]
    public async Task CalculateAgreementLevelAsync_OnlyBlankValues_ReturnsZero()
    {
        var result = await Svc().CalculateAgreementLevelAsync(new List<FieldValue> { FV("  ", 1.0f, "DOCX") });

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(0.0f);
    }

    [Fact]
    public async Task CalculateAgreementLevelAsync_SingleValue_ReturnsOne()
    {
        var result = await Svc().CalculateAgreementLevelAsync(new List<FieldValue> { FV("X", 1.0f, "DOCX") });

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1.0f);
    }

    // ---------------------------------------------------------------------
    // HasConflictAsync — null / single edges (L165-174) and the threshold ternary (L157).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task HasConflictAsync_Null_ReturnsFalseSuccessfully()
    {
        var result = await Svc().HasConflictAsync(null!);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task HasConflictAsync_SingleValue_ReturnsFalse()
    {
        var result = await Svc().HasConflictAsync(new List<FieldValue> { FV("X", 1.0f, "DOCX") });

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task HasConflictAsync_ExplicitThreshold_UsesPassedThresholdNotOptions()
    {
        // 2-of-3 agreement = 0.667. Default options threshold is 0.5 (=> no conflict);
        // an explicit 0.8 threshold means 0.667 <= 0.8 => conflict. Proves the ternary picks
        // the passed threshold when it differs from the 0.5 default.
        var values = new List<FieldValue> { FV("X", 1.0f, "DOCX"), FV("X", 1.0f, "PDF"), FV("Y", 1.0f, "XML") };

        var defaulted = await Svc().HasConflictAsync(values);
        defaulted.Value.ShouldBeFalse();

        var explicitHigh = await Svc().HasConflictAsync(values, 0.8f);
        explicitHigh.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task HasConflictAsync_DefaultThreshold_ReadsConflictThresholdFromOptions()
    {
        // With the default 0.5 sentinel, the ternary must read _options.ConflictThreshold.
        // Here options set it to 0.9, so 0.667 <= 0.9 => conflict (a mutant that hard-codes the
        // 0.5 sentinel would return false).
        var svc = Svc(new MatchingPolicyOptions { ConflictThreshold = 0.9f });
        var values = new List<FieldValue> { FV("X", 1.0f, "DOCX"), FV("X", 1.0f, "PDF"), FV("Y", 1.0f, "XML") };

        var result = await svc.HasConflictAsync(values);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }
}
