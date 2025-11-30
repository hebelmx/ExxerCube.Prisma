using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

/// <summary>
/// Implementation tests for TemplateFieldMapper using REAL implementation.
/// These tests have IDENTICAL names to ITemplateFieldMapperContractTests to prove Liskov Substitution Principle.
/// </summary>
/// <remarks>
/// ITDD Step 3.0: These tests start in RED phase (implementation not yet written).
/// Test names MUST match contract tests exactly - this proves LSP when both pass.
/// </remarks>
public sealed class TemplateFieldMapperTests
{
    private readonly TemplateFieldMapper _mapper;

    public TemplateFieldMapperTests()
    {
        // Use REAL implementation (not mocks)
        _mapper = new TemplateFieldMapper(NullLogger<TemplateFieldMapper>.Instance);
    }

    //
    // MapFieldAsync Tests - IDENTICAL names to contract tests
    //

    [Fact]
    public async Task MapFieldAsync_WhenValidField_ReturnsSuccess()
    {
        // Arrange: REAL source object
        var sourceObject = new TestSourceObject { Name = "John Doe" };
        var mapping = new FieldMapping("Name", "TargetName", isRequired: true);

        // Act: REAL mapper call
        var result = await _mapper.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("John Doe");
    }

    [Fact]
    public async Task MapFieldAsync_WhenRequiredFieldMissing_ReturnsFailure()
    {
        // Arrange: REAL source object missing required field
        var sourceObject = new TestSourceObject { Name = "John" };
        var mapping = new FieldMapping("Email", "TargetEmail", isRequired: true);

        // Act: REAL mapper call
        var result = await _mapper.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Email");
    }

    [Fact]
    public async Task MapFieldAsync_WhenOptionalFieldMissing_ReturnsDefaultValue()
    {
        // Arrange: REAL source object missing optional field
        var sourceObject = new TestSourceObject { Name = "John" };
        var mapping = new FieldMapping("Email", "TargetEmail", isRequired: false)
        {
            DefaultValue = "N/A"
        };

        // Act: REAL mapper call
        var result = await _mapper.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("N/A");
    }

    [Fact]
    public async Task MapFieldAsync_WithTransformation_ReturnsTransformedValue()
    {
        // Arrange: REAL source object with email
        var sourceObject = new TestSourceObject { Email = "user@example.com" };
        var mapping = new FieldMapping("Email", "TargetEmail", isRequired: true)
        {
            TransformExpression = "ToUpper()"
        };

        // Act: REAL mapper call
        var result = await _mapper.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("USER@EXAMPLE.COM");
    }

    [Fact]
    public async Task MapFieldAsync_WithFormatting_ReturnsFormattedValue()
    {
        // Arrange: REAL source object with date
        var sourceObject = new TestSourceObject { BirthDate = new DateTime(1990, 5, 15) };
        var mapping = new FieldMapping("BirthDate", "TargetBirthDate", isRequired: true)
        {
            DataType = "DateTime",
            Format = "yyyy-MM-dd"
        };

        // Act: REAL mapper call
        var result = await _mapper.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("1990-05-15");
    }

    [Fact]
    public async Task MapFieldAsync_WithNestedProperty_ReturnsNestedValue()
    {
        // Arrange: REAL source object with nested property
        var sourceObject = new TestSourceObject
        {
            Expediente = new TestExpediente { NumeroExpediente = "EXP-2024-001" }
        };
        var mapping = new FieldMapping("Expediente.NumeroExpediente", "NumExp", isRequired: true);

        // Act: REAL mapper call
        var result = await _mapper.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("EXP-2024-001");
    }

    //
    // MapAllFieldsAsync Tests - IDENTICAL names to contract tests
    //

    [Fact]
    public async Task MapAllFieldsAsync_WhenAllFieldsValid_ReturnsAllMappedFields()
    {
        // Arrange: REAL source object
        var sourceObject = new TestSourceObject { Name = "John", Age = 30 };
        var template = new TemplateDefinition
        {
            TemplateId = "test-1.0.0",
            TemplateType = "Test",
            Version = "1.0.0",
            FieldMappings = new List<FieldMapping>
            {
                new FieldMapping("Name", "TargetName", true) { DisplayOrder = 1 },
                new FieldMapping("Age", "TargetAge", true) { DisplayOrder = 2 }
            },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        // Act: REAL mapper call
        var result = await _mapper.MapAllFieldsAsync(
            sourceObject,
            template,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        result.Value["TargetName"].ShouldBe("John");
        result.Value["TargetAge"].ShouldBe("30");
    }

    [Fact]
    public async Task MapAllFieldsAsync_WhenRequiredFieldMissing_ReturnsFailure()
    {
        // Arrange: REAL source object with genuinely missing required field (anonymous object)
        var sourceObject = new { Name = "John" }; // Truly missing Age property
        var template = new TemplateDefinition
        {
            TemplateId = "test-1.0.0",
            TemplateType = "Test",
            Version = "1.0.0",
            FieldMappings = new List<FieldMapping>
            {
                new FieldMapping("Name", "TargetName", true) { DisplayOrder = 1 },
                new FieldMapping("Age", "TargetAge", true) { DisplayOrder = 2 } // Required but missing
            },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        // Act: REAL mapper call
        var result = await _mapper.MapAllFieldsAsync(
            sourceObject,
            template,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Age");
    }

    [Fact]
    public async Task MapAllFieldsAsync_WhenOptionalFieldMissing_ContinuesMapping()
    {
        // Arrange: REAL source object missing optional field
        var sourceObject = new TestSourceObject { Name = "John" }; // Missing optional Email
        var template = new TemplateDefinition
        {
            TemplateId = "test-1.0.0",
            TemplateType = "Test",
            Version = "1.0.0",
            FieldMappings = new List<FieldMapping>
            {
                new FieldMapping("Name", "TargetName", true) { DisplayOrder = 1 },
                new FieldMapping("Email", "TargetEmail", false) { DisplayOrder = 2, DefaultValue = "" } // Optional
            },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        // Act: REAL mapper call
        var result = await _mapper.MapAllFieldsAsync(
            sourceObject,
            template,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ContainsKey("TargetName").ShouldBeTrue();
    }

    //
    // ValidateMappingAsync Tests - IDENTICAL names to contract tests
    //

    [Fact]
    public async Task ValidateMappingAsync_WhenMappingValid_ReturnsSuccess()
    {
        // Arrange: REAL validation
        var sourceType = typeof(TestSourceObject);
        var mapping = new FieldMapping("Name", "TargetName", true);

        // Act: REAL mapper call
        var result = await _mapper.ValidateMappingAsync(
            sourceType,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateMappingAsync_WhenFieldPathInvalid_ReturnsFailure()
    {
        // Arrange: REAL validation with invalid field path
        var sourceType = typeof(TestSourceObject);
        var mapping = new FieldMapping("NonExistentField", "Target", true);

        // Act: REAL mapper call
        var result = await _mapper.ValidateMappingAsync(
            sourceType,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("NonExistentField");
    }

    [Fact]
    public async Task ValidateMappingAsync_WhenTransformationInvalid_ReturnsFailure()
    {
        // Arrange: REAL validation with invalid transformation
        var sourceType = typeof(TestSourceObject);
        var mapping = new FieldMapping("Name", "TargetName", true)
        {
            TransformExpression = "InvalidFunction()"
        };

        // Act: REAL mapper call
        var result = await _mapper.ValidateMappingAsync(
            sourceType,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("InvalidFunction");
    }

    //
    // ApplyTransformationAsync Tests - IDENTICAL names to contract tests
    //

    [Fact]
    public async Task ApplyTransformationAsync_ToUpper_ReturnsUppercase()
    {
        // Arrange: REAL transformation
        var value = "hello world";
        var transformExpression = "ToUpper()";

        // Act: REAL mapper call
        var result = await _mapper.ApplyTransformationAsync(
            value,
            transformExpression,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("HELLO WORLD");
    }

    [Fact]
    public async Task ApplyTransformationAsync_Trim_RemovesWhitespace()
    {
        // Arrange: REAL transformation
        var value = "  hello world  ";
        var transformExpression = "Trim()";

        // Act: REAL mapper call
        var result = await _mapper.ApplyTransformationAsync(
            value,
            transformExpression,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("hello world");
    }

    [Fact]
    public async Task ApplyTransformationAsync_ChainedTransformations_AppliesInOrder()
    {
        // Arrange: REAL chained transformations
        var value = "  hello world  ";
        var transformExpression = "Trim() | ToUpper()";

        // Act: REAL mapper call
        var result = await _mapper.ApplyTransformationAsync(
            value,
            transformExpression,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("HELLO WORLD");
    }

    [Fact]
    public async Task ApplyTransformationAsync_Substring_ExtractsSubstring()
    {
        // Arrange: REAL transformation
        var value = "Hello World";
        var transformExpression = "Substring(0, 5)";

        // Act: REAL mapper call
        var result = await _mapper.ApplyTransformationAsync(
            value,
            transformExpression,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Hello");
    }

    //
    // ValidateFieldValueAsync Tests - IDENTICAL names to contract tests
    //

    [Fact]
    public async Task ValidateFieldValueAsync_WhenValuePassesRegex_ReturnsSuccess()
    {
        // Arrange: REAL validation
        var value = "ABC-123";
        var mapping = new FieldMapping("Code", "TargetCode", true)
        {
            ValidationRules = new List<string> { "Regex:^[A-Z0-9-]+$" }
        };

        // Act: REAL mapper call
        var result = await _mapper.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateFieldValueAsync_WhenValueFailsRegex_ReturnsFailure()
    {
        // Arrange: REAL validation
        var value = "abc-123"; // lowercase fails pattern
        var mapping = new FieldMapping("Code", "TargetCode", true)
        {
            ValidationRules = new List<string> { "Regex:^[A-Z0-9-]+$" }
        };

        // Act: REAL mapper call
        var result = await _mapper.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("regex");
    }

    [Fact]
    public async Task ValidateFieldValueAsync_WhenValueInRange_ReturnsSuccess()
    {
        // Arrange: REAL validation
        var value = "50";
        var mapping = new FieldMapping("Age", "TargetAge", true)
        {
            ValidationRules = new List<string> { "Range:1,100" }
        };

        // Act: REAL mapper call
        var result = await _mapper.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateFieldValueAsync_WhenValueOutOfRange_ReturnsFailure()
    {
        // Arrange: REAL validation
        var value = "150"; // Out of range
        var mapping = new FieldMapping("Age", "TargetAge", true)
        {
            ValidationRules = new List<string> { "Range:1,100" }
        };

        // Act: REAL mapper call
        var result = await _mapper.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("range");
    }

    [Fact]
    public async Task ValidateFieldValueAsync_WhenValueMeetsMinLength_ReturnsSuccess()
    {
        // Arrange: REAL validation
        var value = "HelloWorld";
        var mapping = new FieldMapping("Name", "TargetName", true)
        {
            ValidationRules = new List<string> { "MinLength:5" }
        };

        // Act: REAL mapper call
        var result = await _mapper.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateFieldValueAsync_WhenValueBelowMinLength_ReturnsFailure()
    {
        // Arrange: REAL validation
        var value = "Hi";
        var mapping = new FieldMapping("Name", "TargetName", true)
        {
            ValidationRules = new List<string> { "MinLength:5" }
        };

        // Act: REAL mapper call
        var result = await _mapper.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("minimum length");
    }

    //
    // Helper classes for testing (REAL test objects)
    //

    private class TestSourceObject
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string? Email { get; set; }
        public DateTime BirthDate { get; set; }
        public TestExpediente? Expediente { get; set; }
    }

    private class TestExpediente
    {
        public string NumeroExpediente { get; set; } = string.Empty;
    }
}
