using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

/// <summary>
/// Mutation-killing tests for <see cref="TemplateFieldMapper"/>.
/// Targets survivors and uncovered mutants from the Stryker baseline: transformation
/// functions (ToLower/Replace/PadLeft/PadRight), validation rules
/// (MaxLength/EmailAddress/Required/Range/MinLength boundaries), FormatValue branches,
/// exact error messages, and the IsRequired/DefaultValue branch logic.
/// </summary>
public sealed class TemplateFieldMapperMutationTests
{
    private readonly TemplateFieldMapper _mapper = new(NullLogger<TemplateFieldMapper>.Instance);

    private sealed class TypedSource
    {
        public string Name { get; set; } = string.Empty;
        public string? Maybe { get; set; }
        public decimal Amount { get; set; }
        public int Count { get; set; }
        public DateTime When { get; set; }
        public Inner? Nested { get; set; }
    }

    private sealed class Inner
    {
        public string Leaf { get; set; } = string.Empty;
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WhenLoggerNull_ThrowsArgumentNullException()
    {
        var ex = Record.Exception(() => new TemplateFieldMapper(null!));

        ex.ShouldBeOfType<ArgumentNullException>();
    }

    // ── MapFieldAsync: guard messages ─────────────────────────────────────────

    [Fact]
    public async Task MapFieldAsync_WhenSourceNull_ReturnsExactError()
    {
        var result = await _mapper.MapFieldAsync(
            null!,
            new FieldMapping("Name", "T", true),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Source object cannot be null");
    }

    [Fact]
    public async Task MapFieldAsync_WhenMappingNull_ReturnsExactError()
    {
        var result = await _mapper.MapFieldAsync(
            new TypedSource(),
            null!,
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Mapping cannot be null");
    }

    // ── MapFieldAsync: extraction-failure branch (field truly absent) ──────────

    [Fact]
    public async Task MapFieldAsync_WhenOptionalFieldAbsent_WithDefault_ReturnsDefault()
    {
        // Anonymous object truly lacks "Ghost" → ExtractFieldValue fails.
        var source = new { Name = "x" };
        var mapping = new FieldMapping("Ghost", "T", isRequired: false) { DefaultValue = "DEF" };

        var result = await _mapper.MapFieldAsync(source, mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("DEF");
    }

    [Fact]
    public async Task MapFieldAsync_WhenOptionalFieldAbsent_NoDefault_ReturnsEmpty()
    {
        var source = new { Name = "x" };
        var mapping = new FieldMapping("Ghost", "T", isRequired: false);

        var result = await _mapper.MapFieldAsync(source, mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task MapFieldAsync_WhenRequiredFieldAbsent_ReturnsNotFoundError()
    {
        var source = new { Name = "x" };
        var mapping = new FieldMapping("Ghost", "T", isRequired: true);

        var result = await _mapper.MapFieldAsync(source, mapping, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Required field 'Ghost' not found");
    }

    // ── MapFieldAsync: null-value branch (property exists, value null) ─────────

    [Fact]
    public async Task MapFieldAsync_WhenValueNull_RequiredWithDefault_ReturnsDefault()
    {
        // Property exists but is null; required + non-empty default → returns default (not failure).
        var source = new TypedSource { Maybe = null };
        var mapping = new FieldMapping("Maybe", "T", isRequired: true) { DefaultValue = "FALLBACK" };

        var result = await _mapper.MapFieldAsync(source, mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("FALLBACK");
    }

    [Fact]
    public async Task MapFieldAsync_WhenValueNull_RequiredNoDefault_ReturnsIsNullError()
    {
        var source = new TypedSource { Maybe = null };
        var mapping = new FieldMapping("Maybe", "T", isRequired: true);

        var result = await _mapper.MapFieldAsync(source, mapping, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Required field 'Maybe' is null");
    }

    [Fact]
    public async Task MapFieldAsync_WhenValueNull_Optional_ReturnsDefault()
    {
        var source = new TypedSource { Maybe = null };
        var mapping = new FieldMapping("Maybe", "T", isRequired: false) { DefaultValue = "OPT" };

        var result = await _mapper.MapFieldAsync(source, mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("OPT");
    }

    // ── MapFieldAsync: transformation failure + validation paths ──────────────

    [Fact]
    public async Task MapFieldAsync_WhenTransformInvalid_ReturnsFailure()
    {
        var source = new TypedSource { Name = "hello" };
        var mapping = new FieldMapping("Name", "T", isRequired: true)
        {
            TransformExpression = "Bogus()"
        };

        var result = await _mapper.MapFieldAsync(source, mapping, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Transformation error");
    }

    [Fact]
    public async Task MapFieldAsync_WhenValidationRulesPass_ReturnsSuccess()
    {
        var source = new TypedSource { Name = "HelloWorld" };
        var mapping = new FieldMapping("Name", "T", isRequired: true)
        {
            ValidationRules = new List<string> { "MinLength:3" }
        };

        var result = await _mapper.MapFieldAsync(source, mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("HelloWorld");
    }

    [Fact]
    public async Task MapFieldAsync_WhenValidationRulesFail_ReturnsFailure()
    {
        var source = new TypedSource { Name = "Hi" };
        var mapping = new FieldMapping("Name", "T", isRequired: true)
        {
            ValidationRules = new List<string> { "MinLength:5" }
        };

        var result = await _mapper.MapFieldAsync(source, mapping, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("minimum length");
    }

    [Fact]
    public async Task MapFieldAsync_WhenFormatInvalid_ReturnsMapFieldError()
    {
        // decimal.ToString("Q", ...) throws FormatException → reaches the outer catch.
        var source = new TypedSource { Amount = 12.5m };
        var mapping = new FieldMapping("Amount", "T", isRequired: true)
        {
            Format = "Q"
        };

        var result = await _mapper.MapFieldAsync(source, mapping, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Error mapping field 'Amount'");
    }

    // ── FormatValue branches ──────────────────────────────────────────────────

    [Fact]
    public async Task MapFieldAsync_DateTimeWithCustomFormat_UsesThatFormat()
    {
        // Format "yyyy/MM/dd" differs from the default datetime format "yyyy-MM-dd",
        // so removing the format branch is observable.
        var source = new TypedSource { When = new DateTime(1990, 5, 15) };
        var mapping = new FieldMapping("When", "T", isRequired: true)
        {
            DataType = "datetime",
            Format = "yyyy/MM/dd"
        };

        var result = await _mapper.MapFieldAsync(source, mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("1990/05/15");
    }

    [Fact]
    public async Task MapFieldAsync_DateTimeNoFormat_UsesDefaultIsoDate()
    {
        var source = new TypedSource { When = new DateTime(1990, 5, 15) };
        var mapping = new FieldMapping("When", "T", isRequired: true)
        {
            DataType = "datetime"
        };

        var result = await _mapper.MapFieldAsync(source, mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("1990-05-15");
    }

    [Fact]
    public async Task MapFieldAsync_DecimalWithFormat_UsesFormattable()
    {
        var source = new TypedSource { Amount = 1234.5m };
        var mapping = new FieldMapping("Amount", "T", isRequired: true)
        {
            DataType = "decimal",
            Format = "F2"
        };

        var result = await _mapper.MapFieldAsync(source, mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("1234.50");
    }

    // ── MapAllFieldsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task MapAllFieldsAsync_WhenSourceNull_ReturnsExactError()
    {
        var result = await _mapper.MapAllFieldsAsync(
            null!,
            new TemplateDefinition(),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Source object cannot be null");
    }

    [Fact]
    public async Task MapAllFieldsAsync_WhenTemplateNull_ReturnsExactError()
    {
        var result = await _mapper.MapAllFieldsAsync(
            new TypedSource(),
            null!,
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Template cannot be null");
    }

    [Fact]
    public async Task MapAllFieldsAsync_ProcessesInDisplayOrder_LastWriteWins()
    {
        // Two mappings target the same field; ascending DisplayOrder means the
        // higher-order one is written last and wins. OrderByDescending would flip this.
        var source = new { A = "first", B = "second" };
        var template = new TemplateDefinition
        {
            FieldMappings = new List<FieldMapping>
            {
                new FieldMapping("A", "Shared", isRequired: false) { DisplayOrder = 1 },
                new FieldMapping("B", "Shared", isRequired: false) { DisplayOrder = 2 }
            }
        };

        var result = await _mapper.MapAllFieldsAsync(source, template, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        result.Value["Shared"].ShouldBe("second");
    }

    // ── ValidateMappingAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task ValidateMappingAsync_WhenSourceTypeNull_ReturnsExactError()
    {
        var result = await _mapper.ValidateMappingAsync(
            null!,
            new FieldMapping("Name", "T", true),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Source type cannot be null");
    }

    [Fact]
    public async Task ValidateMappingAsync_WhenMappingNull_ReturnsExactError()
    {
        var result = await _mapper.ValidateMappingAsync(
            typeof(TypedSource),
            null!,
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Mapping cannot be null");
    }

    [Fact]
    public async Task ValidateMappingAsync_WithValidNoArgTransform_ReturnsSuccess()
    {
        var mapping = new FieldMapping("Name", "T", true) { TransformExpression = "ToUpper()" };

        var result = await _mapper.ValidateMappingAsync(
            typeof(TypedSource),
            mapping,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateMappingAsync_WithValidParameterizedTransform_ReturnsSuccess()
    {
        // "Substring(0,3)" matches only via StartsWith (not exact Equals), exercising the || branch.
        var mapping = new FieldMapping("Name", "T", true) { TransformExpression = "Substring(0,3)" };

        var result = await _mapper.ValidateMappingAsync(
            typeof(TypedSource),
            mapping,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    // ── ApplyTransformationAsync: short-circuit + each function ────────────────

    [Fact]
    public async Task ApplyTransformationAsync_WhenValueEmpty_ReturnsEmpty()
    {
        var result = await _mapper.ApplyTransformationAsync(
            string.Empty, "ToUpper()", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ApplyTransformationAsync_WhenExpressionWhitespace_ReturnsOriginalValue()
    {
        var result = await _mapper.ApplyTransformationAsync(
            "Hello", "   ", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Hello");
    }

    [Fact]
    public async Task ApplyTransformationAsync_ToLower_ReturnsLowercase()
    {
        var result = await _mapper.ApplyTransformationAsync(
            "HELLO", "ToLower()", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("hello");
    }

    [Fact]
    public async Task ApplyTransformationAsync_Replace_ReplacesSubstring()
    {
        var result = await _mapper.ApplyTransformationAsync(
            "a-b-c", "Replace(-,_)", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("a_b_c");
    }

    [Fact]
    public async Task ApplyTransformationAsync_PadLeft_PadsOnLeft()
    {
        var result = await _mapper.ApplyTransformationAsync(
            "5", "PadLeft(3,0)", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("005");
    }

    [Fact]
    public async Task ApplyTransformationAsync_PadRight_PadsOnRight()
    {
        var result = await _mapper.ApplyTransformationAsync(
            "5", "PadRight(3,0)", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("500");
    }

    [Fact]
    public async Task ApplyTransformationAsync_WhenValueNull_ReturnsEmpty()
    {
        // value == null trips the IsNullOrEmpty guard → returns (value ?? string.Empty) == "".
        var result = await _mapper.ApplyTransformationAsync(
            null!, "ToUpper()", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ApplyTransformationAsync_ReplaceWrongArgCount_ReturnsFailure()
    {
        var result = await _mapper.ApplyTransformationAsync(
            "abc", "Replace(x)", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        // Assert the inner ArgumentException message so the throw's literal/statement is pinned.
        (result.Error ?? string.Empty).ShouldContain("Invalid Replace arguments");
    }

    [Fact]
    public async Task ApplyTransformationAsync_MalformedExpression_ReturnsFailure()
    {
        // No parentheses → fails the "^(\w+)\((.*)\)$" match → ArgumentException → caught.
        var result = await _mapper.ApplyTransformationAsync(
            "abc", "NotAFunction", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Invalid transformation format");
    }

    [Fact]
    public async Task ApplyTransformationAsync_UnsupportedFunction_ReturnsFailure()
    {
        var result = await _mapper.ApplyTransformationAsync(
            "abc", "Reverse()", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("is not supported");
    }

    // ── ValidateFieldValueAsync: rule branches + boundaries ───────────────────

    [Fact]
    public async Task ValidateFieldValueAsync_WhenRulesNull_ReturnsSuccess()
    {
        var mapping = new FieldMapping("F", "T", true) { ValidationRules = null! };

        var result = await _mapper.ValidateFieldValueAsync(
            "anything", mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateFieldValueAsync_MaxLength_AtBoundary_ReturnsSuccess()
    {
        var mapping = new FieldMapping("F", "T", true) { ValidationRules = new List<string> { "MaxLength:5" } };

        var result = await _mapper.ValidateFieldValueAsync(
            "Hello", mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateFieldValueAsync_MaxLength_Exceeded_ReturnsFailure()
    {
        var mapping = new FieldMapping("F", "T", true) { ValidationRules = new List<string> { "MaxLength:5" } };

        var result = await _mapper.ValidateFieldValueAsync(
            "HelloX", mapping, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("exceeds maximum length");
    }

    [Fact]
    public async Task ValidateFieldValueAsync_EmailAddress_Valid_ReturnsSuccess()
    {
        var mapping = new FieldMapping("F", "T", true) { ValidationRules = new List<string> { "EmailAddress" } };

        var result = await _mapper.ValidateFieldValueAsync(
            "user@example.com", mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateFieldValueAsync_EmailAddress_Invalid_ReturnsFailure()
    {
        var mapping = new FieldMapping("F", "T", true) { ValidationRules = new List<string> { "EmailAddress" } };

        var result = await _mapper.ValidateFieldValueAsync(
            "not-an-email", mapping, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("not a valid email address");
    }

    [Fact]
    public async Task ValidateFieldValueAsync_Required_Empty_ReturnsFailure()
    {
        var mapping = new FieldMapping("F", "T", true) { ValidationRules = new List<string> { "Required" } };

        var result = await _mapper.ValidateFieldValueAsync(
            "   ", mapping, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("required and cannot be empty");
    }

    [Fact]
    public async Task ValidateFieldValueAsync_Required_NonEmpty_ReturnsSuccess()
    {
        var mapping = new FieldMapping("F", "T", true) { ValidationRules = new List<string> { "Required" } };

        var result = await _mapper.ValidateFieldValueAsync(
            "present", mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateFieldValueAsync_Range_AtMin_ReturnsSuccess()
    {
        var mapping = new FieldMapping("F", "T", true) { ValidationRules = new List<string> { "Range:1,100" } };

        var result = await _mapper.ValidateFieldValueAsync(
            "1", mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateFieldValueAsync_Range_AtMax_ReturnsSuccess()
    {
        var mapping = new FieldMapping("F", "T", true) { ValidationRules = new List<string> { "Range:1,100" } };

        var result = await _mapper.ValidateFieldValueAsync(
            "100", mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateFieldValueAsync_MinLength_AtBoundary_ReturnsSuccess()
    {
        var mapping = new FieldMapping("F", "T", true) { ValidationRules = new List<string> { "MinLength:5" } };

        var result = await _mapper.ValidateFieldValueAsync(
            "Hello", mapping, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateFieldValueAsync_WhenRegexPatternInvalid_ReturnsRuleError()
    {
        // "Regex:[" is an invalid pattern → Regex.IsMatch throws → caught by ValidateRule.
        var mapping = new FieldMapping("F", "T", true) { ValidationRules = new List<string> { "Regex:[" } };

        var result = await _mapper.ValidateFieldValueAsync(
            "abc", mapping, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Validation rule");
    }

    // ── MapAllFieldsAsync: optional field failing validation is skipped ────────

    [Fact]
    public async Task MapAllFieldsAsync_WhenOptionalFieldFailsValidation_SkipsField()
    {
        // The optional "Code" field fails MinLength validation → mapper logs and `continue`s,
        // so its target key is never added. Removing the continue would add an empty entry.
        var source = new { Name = "Hi", Code = "bad" };
        var template = new TemplateDefinition
        {
            FieldMappings = new List<FieldMapping>
            {
                new FieldMapping("Name", "TargetName", isRequired: true) { DisplayOrder = 1 },
                new FieldMapping("Code", "TargetCode", isRequired: false)
                {
                    DisplayOrder = 2,
                    ValidationRules = new List<string> { "MinLength:10" }
                }
            }
        };

        var result = await _mapper.MapAllFieldsAsync(source, template, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.ContainsKey("TargetName").ShouldBeTrue();
        result.Value.ContainsKey("TargetCode").ShouldBeFalse();
    }
}
