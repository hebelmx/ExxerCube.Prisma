using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;

namespace ExxerCube.Prisma.Tests.Domain.Domain.Interfaces;

/// <summary>
/// Contract tests for ITemplateFieldMapper using mocked interfaces.
/// These tests validate the interface design and expected behaviors WITHOUT implementation.
/// </summary>
/// <remarks>
/// ITDD Step 2.5: These tests use NSubstitute mocks to prove the abstraction is sound.
/// All tests should be GREEN before implementing TemplateFieldMapper.
/// </remarks>
public sealed class ITemplateFieldMapperContractTests
{
    //
    // MapFieldAsync Tests - Contract validation with mocks
    //

    [Fact]
    public async Task MapFieldAsync_WhenValidField_ReturnsSuccess()
    {
        // Arrange: Mock the interface
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var sourceObject = new { Name = "John Doe" };
        var mapping = new FieldMapping("Name", "TargetName", isRequired: true);

        mockMapper.MapFieldAsync(sourceObject, mapping, Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("John Doe"));

        // Act
        var result = await mockMapper.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("John Doe");
    }

    [Fact]
    public async Task MapFieldAsync_WhenRequiredFieldMissing_ReturnsFailure()
    {
        // Arrange: Mock failure for missing required field
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var sourceObject = new { Name = "John" };
        var mapping = new FieldMapping("Email", "TargetEmail", isRequired: true);

        mockMapper.MapFieldAsync(sourceObject, mapping, Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure("Required field 'Email' not found"));

        // Act
        var result = await mockMapper.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Email");
    }

    [Fact]
    public async Task MapFieldAsync_WhenOptionalFieldMissing_ReturnsDefaultValue()
    {
        // Arrange: Mock default value for optional field
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var sourceObject = new { Name = "John" };
        var mapping = new FieldMapping("MiddleName", "TargetMiddleName", isRequired: false)
        {
            DefaultValue = "N/A"
        };

        mockMapper.MapFieldAsync(sourceObject, mapping, Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("N/A"));

        // Act
        var result = await mockMapper.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("N/A");
    }

    [Fact]
    public async Task MapFieldAsync_WithTransformation_ReturnsTransformedValue()
    {
        // Arrange: Mock transformation applied
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var sourceObject = new { Email = "user@example.com" };
        var mapping = new FieldMapping("Email", "TargetEmail", isRequired: true)
        {
            TransformExpression = "ToUpper()"
        };

        mockMapper.MapFieldAsync(sourceObject, mapping, Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("USER@EXAMPLE.COM"));

        // Act
        var result = await mockMapper.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("USER@EXAMPLE.COM");
    }

    [Fact]
    public async Task MapFieldAsync_WithFormatting_ReturnsFormattedValue()
    {
        // Arrange: Mock date formatting
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var sourceObject = new { BirthDate = new DateTime(1990, 5, 15) };
        var mapping = new FieldMapping("BirthDate", "TargetBirthDate", isRequired: true)
        {
            DataType = "DateTime",
            Format = "yyyy-MM-dd"
        };

        mockMapper.MapFieldAsync(sourceObject, mapping, Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("1990-05-15"));

        // Act
        var result = await mockMapper.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("1990-05-15");
    }

    [Fact]
    public async Task MapFieldAsync_WithNestedProperty_ReturnsNestedValue()
    {
        // Arrange: Mock nested property access
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var sourceObject = new
        {
            Expediente = new { NumeroExpediente = "EXP-2024-001" }
        };
        var mapping = new FieldMapping("Expediente.NumeroExpediente", "NumExp", isRequired: true);

        mockMapper.MapFieldAsync(sourceObject, mapping, Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("EXP-2024-001"));

        // Act
        var result = await mockMapper.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("EXP-2024-001");
    }

    //
    // MapAllFieldsAsync Tests - Contract validation with mocks
    //

    [Fact]
    public async Task MapAllFieldsAsync_WhenAllFieldsValid_ReturnsAllMappedFields()
    {
        // Arrange: Mock successful mapping of all fields
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var sourceObject = new { Name = "John", Age = 30 };
        var template = new TemplateDefinition
        {
            TemplateId = "test-1.0.0",
            TemplateType = "Test",
            Version = "1.0.0",
            FieldMappings = new List<FieldMapping>
            {
                new FieldMapping("Name", "TargetName", true),
                new FieldMapping("Age", "TargetAge", true)
            },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        var expectedResult = new Dictionary<string, string>
        {
            { "TargetName", "John" },
            { "TargetAge", "30" }
        };

        mockMapper.MapAllFieldsAsync(sourceObject, template, Arg.Any<CancellationToken>())
            .Returns(Result<Dictionary<string, string>>.Success(expectedResult));

        // Act
        var result = await mockMapper.MapAllFieldsAsync(
            sourceObject,
            template,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        result.Value["TargetName"].ShouldBe("John");
        result.Value["TargetAge"].ShouldBe("30");
    }

    [Fact]
    public async Task MapAllFieldsAsync_WhenRequiredFieldMissing_ReturnsFailure()
    {
        // Arrange: Mock failure when required field missing
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var sourceObject = new { Name = "John" }; // Missing Age
        var template = new TemplateDefinition
        {
            TemplateId = "test-1.0.0",
            TemplateType = "Test",
            Version = "1.0.0",
            FieldMappings = new List<FieldMapping>
            {
                new FieldMapping("Name", "TargetName", true),
                new FieldMapping("Age", "TargetAge", true) // Required but missing
            },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        mockMapper.MapAllFieldsAsync(sourceObject, template, Arg.Any<CancellationToken>())
            .Returns(Result<Dictionary<string, string>>.Failure("Required field 'Age' not found"));

        // Act
        var result = await mockMapper.MapAllFieldsAsync(
            sourceObject,
            template,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Age");
    }

    [Fact]
    public async Task MapAllFieldsAsync_WhenOptionalFieldMissing_ContinuesMapping()
    {
        // Arrange: Mock success even with optional field missing
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var sourceObject = new { Name = "John" }; // Missing optional MiddleName
        var template = new TemplateDefinition
        {
            TemplateId = "test-1.0.0",
            TemplateType = "Test",
            Version = "1.0.0",
            FieldMappings = new List<FieldMapping>
            {
                new FieldMapping("Name", "TargetName", true),
                new FieldMapping("MiddleName", "TargetMiddleName", false) // Optional
            },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        var expectedResult = new Dictionary<string, string>
        {
            { "TargetName", "John" }
            // TargetMiddleName omitted or has default value
        };

        mockMapper.MapAllFieldsAsync(sourceObject, template, Arg.Any<CancellationToken>())
            .Returns(Result<Dictionary<string, string>>.Success(expectedResult));

        // Act
        var result = await mockMapper.MapAllFieldsAsync(
            sourceObject,
            template,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ContainsKey("TargetName").ShouldBeTrue();
    }

    //
    // ValidateMappingAsync Tests - Contract validation with mocks
    //

    [Fact]
    public async Task ValidateMappingAsync_WhenMappingValid_ReturnsSuccess()
    {
        // Arrange: Mock valid mapping
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var sourceType = typeof(TestSourceObject);
        var mapping = new FieldMapping("Name", "TargetName", true);

        mockMapper.ValidateMappingAsync(sourceType, mapping, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await mockMapper.ValidateMappingAsync(
            sourceType,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateMappingAsync_WhenFieldPathInvalid_ReturnsFailure()
    {
        // Arrange: Mock invalid field path
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var sourceType = typeof(TestSourceObject);
        var mapping = new FieldMapping("NonExistentField", "Target", true);

        mockMapper.ValidateMappingAsync(sourceType, mapping, Arg.Any<CancellationToken>())
            .Returns(Result.Failure("Field path 'NonExistentField' not found on type 'TestSourceObject'"));

        // Act
        var result = await mockMapper.ValidateMappingAsync(
            sourceType,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("NonExistentField");
    }

    [Fact]
    public async Task ValidateMappingAsync_WhenTransformationInvalid_ReturnsFailure()
    {
        // Arrange: Mock invalid transformation expression
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var sourceType = typeof(TestSourceObject);
        var mapping = new FieldMapping("Name", "TargetName", true)
        {
            TransformExpression = "InvalidFunction()"
        };

        mockMapper.ValidateMappingAsync(sourceType, mapping, Arg.Any<CancellationToken>())
            .Returns(Result.Failure("Invalid transformation expression: 'InvalidFunction()' is not supported"));

        // Act
        var result = await mockMapper.ValidateMappingAsync(
            sourceType,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("InvalidFunction");
    }

    //
    // ApplyTransformationAsync Tests - Contract validation with mocks
    //

    [Fact]
    public async Task ApplyTransformationAsync_ToUpper_ReturnsUppercase()
    {
        // Arrange: Mock ToUpper transformation
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var value = "hello world";
        var transformExpression = "ToUpper()";

        mockMapper.ApplyTransformationAsync(value, transformExpression, Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("HELLO WORLD"));

        // Act
        var result = await mockMapper.ApplyTransformationAsync(
            value,
            transformExpression,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("HELLO WORLD");
    }

    [Fact]
    public async Task ApplyTransformationAsync_Trim_RemovesWhitespace()
    {
        // Arrange: Mock Trim transformation
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var value = "  hello world  ";
        var transformExpression = "Trim()";

        mockMapper.ApplyTransformationAsync(value, transformExpression, Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("hello world"));

        // Act
        var result = await mockMapper.ApplyTransformationAsync(
            value,
            transformExpression,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("hello world");
    }

    [Fact]
    public async Task ApplyTransformationAsync_ChainedTransformations_AppliesInOrder()
    {
        // Arrange: Mock chained transformations
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var value = "  hello world  ";
        var transformExpression = "Trim() | ToUpper()";

        mockMapper.ApplyTransformationAsync(value, transformExpression, Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("HELLO WORLD"));

        // Act
        var result = await mockMapper.ApplyTransformationAsync(
            value,
            transformExpression,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("HELLO WORLD");
    }

    [Fact]
    public async Task ApplyTransformationAsync_Substring_ExtractsSubstring()
    {
        // Arrange: Mock Substring transformation
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var value = "Hello World";
        var transformExpression = "Substring(0, 5)";

        mockMapper.ApplyTransformationAsync(value, transformExpression, Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Hello"));

        // Act
        var result = await mockMapper.ApplyTransformationAsync(
            value,
            transformExpression,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Hello");
    }

    //
    // ValidateFieldValueAsync Tests - Contract validation with mocks
    //

    [Fact]
    public async Task ValidateFieldValueAsync_WhenValuePassesRegex_ReturnsSuccess()
    {
        // Arrange: Mock regex validation success
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var value = "ABC-123";
        var mapping = new FieldMapping("Code", "TargetCode", true)
        {
            ValidationRules = new List<string> { "Regex:^[A-Z0-9-]+$" }
        };

        mockMapper.ValidateFieldValueAsync(value, mapping, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await mockMapper.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateFieldValueAsync_WhenValueFailsRegex_ReturnsFailure()
    {
        // Arrange: Mock regex validation failure
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var value = "abc-123"; // lowercase fails pattern
        var mapping = new FieldMapping("Code", "TargetCode", true)
        {
            ValidationRules = new List<string> { "Regex:^[A-Z0-9-]+$" }
        };

        mockMapper.ValidateFieldValueAsync(value, mapping, Arg.Any<CancellationToken>())
            .Returns(Result.Failure("Value 'abc-123' does not match regex pattern '^[A-Z0-9-]+$'"));

        // Act
        var result = await mockMapper.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("regex");
    }

    [Fact]
    public async Task ValidateFieldValueAsync_WhenValueInRange_ReturnsSuccess()
    {
        // Arrange: Mock range validation success
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var value = "50";
        var mapping = new FieldMapping("Age", "TargetAge", true)
        {
            ValidationRules = new List<string> { "Range:1,100" }
        };

        mockMapper.ValidateFieldValueAsync(value, mapping, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await mockMapper.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateFieldValueAsync_WhenValueOutOfRange_ReturnsFailure()
    {
        // Arrange: Mock range validation failure
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var value = "150"; // Out of range
        var mapping = new FieldMapping("Age", "TargetAge", true)
        {
            ValidationRules = new List<string> { "Range:1,100" }
        };

        mockMapper.ValidateFieldValueAsync(value, mapping, Arg.Any<CancellationToken>())
            .Returns(Result.Failure("Value '150' is outside the range 1-100"));

        // Act
        var result = await mockMapper.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("range");
    }

    [Fact]
    public async Task ValidateFieldValueAsync_WhenValueMeetsMinLength_ReturnsSuccess()
    {
        // Arrange: Mock MinLength validation success
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var value = "HelloWorld";
        var mapping = new FieldMapping("Name", "TargetName", true)
        {
            ValidationRules = new List<string> { "MinLength:5" }
        };

        mockMapper.ValidateFieldValueAsync(value, mapping, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await mockMapper.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateFieldValueAsync_WhenValueBelowMinLength_ReturnsFailure()
    {
        // Arrange: Mock MinLength validation failure
        var mockMapper = Substitute.For<ITemplateFieldMapper>();
        var value = "Hi";
        var mapping = new FieldMapping("Name", "TargetName", true)
        {
            ValidationRules = new List<string> { "MinLength:5" }
        };

        mockMapper.ValidateFieldValueAsync(value, mapping, Arg.Any<CancellationToken>())
            .Returns(Result.Failure("Value length 2 is below minimum length 5"));

        // Act
        var result = await mockMapper.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: ITDD contract expectations
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("minimum length");
    }

    //
    // Helper classes for testing
    //

    private class TestSourceObject
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string? Email { get; set; }
    }
}
