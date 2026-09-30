using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

/// <summary>
/// Mutation-killing tests for <see cref="SchemaEvolutionDetector"/>.
/// Drives the deterministic surface that the contract/LSP tests leave thin:
/// the public <c>CalculateSimilarity</c> (substring-containment ratio, Levenshtein
/// distance, prefix/suffix normalization), <c>SuggestFieldMappingsAsync</c>
/// (data-type mapping, nullability → IsRequired, field-name humanization), nested
/// field-path recursion, severity calculation, rename-threshold gating, and exact
/// guard/error messages.
/// </summary>
public sealed class SchemaEvolutionDetectorMutationTests
{
    private readonly SchemaEvolutionDetector _detector =
        new(Substitute.For<ITemplateRepository>(), NullLogger<SchemaEvolutionDetector>.Instance);

    private sealed class Inner
    {
        public string Leaf { get; set; } = string.Empty;
    }

    private sealed class SuggestSource
    {
        public int Count { get; set; }
        public string Name { get; set; } = string.Empty;
        public long Big { get; set; }
        public int? Maybe { get; set; }
        public string FullName { get; set; } = string.Empty;
        public Inner? Nested { get; set; }
    }

    private static TemplateDefinition Template(params FieldMapping[] mappings) => new()
    {
        TemplateId = "t",
        TemplateType = "Excel",
        Version = "1.0.0",
        FieldMappings = mappings.ToList()
    };

    // ── CalculateSimilarity: substring-containment ratio (shorter/longer) ─────

    [Fact]
    public void CalculateSimilarity_SubstringContainment_UsesShorterOverLongerRatio()
    {
        // "names" contains "name": shorter=4, longer=5 → 4/5 = 0.80.
        // Swapping Min/Max would yield 1.0.
        var score = _detector.CalculateSimilarity("name", "names");

        score.ShouldBe(0.8);
    }

    // ── CalculateSimilarity: Levenshtein distance path (no containment) ────────

    [Fact]
    public void CalculateSimilarity_LevenshteinSingleEdit_ReturnsExactScore()
    {
        // "cat" vs "car": distance 1, maxLength 3 → 1 - 1/3 = 0.67.
        var score = _detector.CalculateSimilarity("cat", "car");

        score.ShouldBe(0.67);
    }

    [Fact]
    public void CalculateSimilarity_LevenshteinNoOverlap_ReturnsZero()
    {
        // "abc" vs "wxyz": distance 4, maxLength 4 → 1 - 4/4 = 0.
        var score = _detector.CalculateSimilarity("abc", "wxyz");

        score.ShouldBe(0.0);
    }

    [Fact]
    public void CalculateSimilarity_LevenshteinAsymmetricLength_ReturnsExactScore()
    {
        // "cats" vs "car": substitute t→r + delete s = distance 2, maxLength 4 → 1 - 2/4 = 0.5.
        // The asymmetric lengths exercise the last DP row/column initialization and the
        // deletion/insertion inner Min.
        var score = _detector.CalculateSimilarity("cats", "car");

        score.ShouldBe(0.5);
    }

    // ── CalculateSimilarity: prefix stripping in NormalizeFieldName ────────────

    [Theory]
    [InlineData("getName")]
    [InlineData("setName")]
    [InlineData("isName")]
    [InlineData("hasName")]
    [InlineData("theName")]
    public void CalculateSimilarity_StripsKnownPrefix_NormalizesToName(string prefixed)
    {
        // After stripping the known prefix both normalize to "name" → identical → 1.0.
        // Nulling a prefix array element leaves it unstripped → ratio < 1.0.
        var score = _detector.CalculateSimilarity(prefixed, "name");

        score.ShouldBe(1.0);
    }

    // ── CalculateSimilarity: suffix stripping in NormalizeFieldName ────────────

    [Theory]
    [InlineData("nameField")]
    [InlineData("nameProperty")]
    [InlineData("nameValue")]
    public void CalculateSimilarity_StripsKnownSuffix_NormalizesToName(string suffixed)
    {
        var score = _detector.CalculateSimilarity(suffixed, "name");

        score.ShouldBe(1.0);
    }

    // ── SuggestFieldMappingsAsync: data types, nullability, humanization ───────

    [Fact]
    public async Task SuggestFieldMappingsAsync_MapsDataTypesNullabilityAndHumanizesNames()
    {
        var source = new SuggestSource
        {
            Count = 1,
            Name = "x",
            Big = 5L,
            Maybe = null,
            FullName = "y",
            Nested = new Inner { Leaf = "z" }
        };

        var result = await _detector.SuggestFieldMappingsAsync(
            source, "Excel", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var maps = result.Value!;

        // long maps to "long" (explicit arm differs from the lowercased default "int64").
        maps.First(m => m.SourceFieldPath == "Big").DataType.ShouldBe("long");

        // Nullable<int> unwraps to underlying "int" (?? type would yield "nullable`1").
        maps.First(m => m.SourceFieldPath == "Maybe").DataType.ShouldBe("int");

        // Non-nullable value type → required; reference/nullable types → not required.
        maps.First(m => m.SourceFieldPath == "Count").IsRequired.ShouldBeTrue();
        maps.First(m => m.SourceFieldPath == "Name").IsRequired.ShouldBeFalse();
        maps.First(m => m.SourceFieldPath == "Maybe").IsRequired.ShouldBeFalse();

        // Humanizer inserts spaces before interior capitals on the last path segment.
        maps.First(m => m.SourceFieldPath == "FullName").TargetField.ShouldBe("Full Name");

        // Nested path → target uses the LAST segment, not the first.
        maps.First(m => m.SourceFieldPath == "Nested.Leaf").TargetField.ShouldBe("Leaf");
    }

    // ── DetectDriftAsync: nested-path recursion ───────────────────────────────

    [Fact]
    public async Task DetectDriftAsync_WithNestedObject_EmitsDottedNestedPath()
    {
        var source = new { Name = "x", Block = new Inner { Leaf = "y" } };
        var template = Template(new FieldMapping("Name", "TargetName", isRequired: true));

        var result = await _detector.DetectDriftAsync(
            source, template, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var paths = result.Value!.NewFields.Select(f => f.FieldPath).ToList();
        paths.ShouldContain("Block");
        paths.ShouldContain("Block.Leaf");
    }

    [Fact]
    public async Task DetectDriftAsync_WithCollectionProperty_DoesNotRecurseIntoIt()
    {
        // A collection is a complex type but must NOT be recursed into — so its internal
        // members (Count/Capacity) never appear as nested field paths.
        var source = new { Name = "x", Items = new List<int> { 1, 2 } };
        var template = Template(new FieldMapping("Name", "TargetName", isRequired: true));

        var result = await _detector.DetectDriftAsync(
            source, template, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var paths = result.Value!.NewFields.Select(f => f.FieldPath).ToList();
        paths.ShouldContain("Items");
        paths.ShouldNotContain(p => p.StartsWith("Items."));
    }

    [Fact]
    public async Task DetectDriftAsync_EqualScoringRenameCandidates_KeepsFirstEncountered()
    {
        // Both "Names" and "Named" score 0.8 against the missing "Name". The strict `>`
        // comparison keeps the first candidate; `>=` would let the later one overwrite it.
        var source = new { Names = "a", Named = "b" };
        var template = Template(new FieldMapping("Name", "TargetName", isRequired: false));

        var result = await _detector.DetectDriftAsync(
            source, template, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.RenamedFields.Count.ShouldBe(1);
        result.Value.RenamedFields[0].SuggestedNewFieldPath.ShouldBe("Names");
    }

    // ── DetectDriftAsync: new-field sample value + detected type ───────────────

    [Fact]
    public async Task DetectDriftAsync_NewField_CapturesSampleValueAndType()
    {
        var source = new { Name = "x", Email = "john@example.com" };
        var template = Template(new FieldMapping("Name", "TargetName", isRequired: true));

        var result = await _detector.DetectDriftAsync(
            source, template, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var email = result.Value!.NewFields.First(f => f.FieldPath == "Email");
        email.SampleValue.ShouldBe("john@example.com");
        email.DetectedType.ShouldBe("String");
    }

    // ── Severity: missing optional field (no rename, no new) → Medium ─────────

    [Fact]
    public async Task DetectDriftAsync_MissingOptionalFieldOnly_IsMediumSeverity()
    {
        // Source has exactly the required field; an optional template field is missing.
        // No new fields, no rename candidate → Medium hinges on (renamed || missing).
        var source = new { Name = "x" };
        var template = Template(
            new FieldMapping("Name", "TargetName", isRequired: true),
            new FieldMapping("Color", "TargetColor", isRequired: false));

        var result = await _detector.DetectDriftAsync(
            source, template, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Severity.ShouldBe(DriftSeverity.Medium);
        result.Value.HasDrift.ShouldBeTrue();
    }

    // ── Rename threshold: a weak (<0.7) match must NOT be reported as a rename ─

    [Fact]
    public async Task DetectDriftAsync_WeakSimilarityCandidate_IsNotReportedAsRename()
    {
        // "Cold" vs missing "Color" scores 0.6 (< 0.7 threshold) → no rename.
        // The && gate must hold; an || gate would record it because 0.6 > bestScore (0).
        var source = new { Name = "x", Cold = "y" };
        var template = Template(
            new FieldMapping("Name", "TargetName", isRequired: true),
            new FieldMapping("Color", "TargetColor", isRequired: false));

        var result = await _detector.DetectDriftAsync(
            source, template, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.RenamedFields.ShouldBeEmpty();
    }

    // ── ValidateTemplateCompatibilityAsync: lists every missing required field ─

    [Fact]
    public async Task ValidateTemplateCompatibilityAsync_ListsAllMissingRequiredFields()
    {
        var source = new { Other = "z" };
        var template = Template(
            new FieldMapping("Age", "TargetAge", isRequired: true),
            new FieldMapping("Email", "TargetEmail", isRequired: true));

        var result = await _detector.ValidateTemplateCompatibilityAsync(
            source, template, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("'Age', 'Email'");
    }

    [Fact]
    public async Task ValidateTemplateCompatibilityAsync_WhenSourceNull_PropagatesDetectError()
    {
        var template = Template(new FieldMapping("Name", "TargetName", isRequired: true));

        var result = await _detector.ValidateTemplateCompatibilityAsync(
            null!, template, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Source object cannot be null");
    }

    // ── DetectDriftForActiveTemplateAsync: null-source guard message ───────────

    [Fact]
    public async Task DetectDriftForActiveTemplateAsync_WhenSourceNull_ReturnsExactError()
    {
        var result = await _detector.DetectDriftForActiveTemplateAsync(
            null!, "Excel", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Source object cannot be null");
    }
}
