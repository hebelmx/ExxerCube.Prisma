using System;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="ITemplateFieldMapper"/> — every implementation
/// (and the mock blueprint) must pass these tests unchanged (ADR-005).
/// </summary>
/// <remarks>
/// <para>
/// The 22 test bodies are lifted VERBATIM from <c>TemplateFieldMapperTests</c>
/// (Tests.Infrastructure.Export.Adaptive, the executable truth before this refactor) —
/// names preserved; the only edit is the SUT-construction line
/// (<c>_mapper</c> → <see cref="Sut"/>). The mock blueprint and
/// <c>TemplateFieldMapper</c> are zero-drift twins (master plan §2), so all 22 are
/// contract-grade.
/// </para>
/// <para>
/// Uses the default ADR-005 SUT mechanism: a constructor-injected, interface-typed
/// <see cref="Sut"/> (the mapper has only a logger dependency).
/// </para>
/// </remarks>
public abstract class TemplateFieldMapperContract
{
    /// <summary>
    /// Initializes the contract with the implementation under test.
    /// </summary>
    /// <param name="sut">The <see cref="ITemplateFieldMapper"/> implementation to verify.</param>
    protected TemplateFieldMapperContract(ITemplateFieldMapper sut)
    {
        ArgumentNullException.ThrowIfNull(sut);
        Sut = sut;
    }

    /// <summary>Gets the implementation under test.</summary>
    protected ITemplateFieldMapper Sut { get; }

    //
    // MapFieldAsync Tests
    //

    /// <summary>Contract: a present field maps to its value.</summary>
    [Fact]
    public async Task MapFieldAsync_WhenValidField_ReturnsSuccess()
    {
        // Arrange: REAL source object
        var sourceObject = new TestSourceObject { Name = "John Doe" };
        var mapping = new FieldMapping("Name", "TargetName", isRequired: true);

        // Act: REAL mapper call
        var result = await Sut.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("John Doe");
    }

    /// <summary>Contract: a missing required field fails.</summary>
    [Fact]
    public async Task MapFieldAsync_WhenRequiredFieldMissing_ReturnsFailure()
    {
        // Arrange: REAL source object missing required field
        var sourceObject = new TestSourceObject { Name = "John" };
        var mapping = new FieldMapping("Email", "TargetEmail", isRequired: true);

        // Act: REAL mapper call
        var result = await Sut.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Email");
    }

    /// <summary>Contract: a missing optional field with a default returns the default.</summary>
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
        var result = await Sut.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("N/A");
    }

    /// <summary>Contract: a transformation expression is applied.</summary>
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
        var result = await Sut.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("USER@EXAMPLE.COM");
    }

    /// <summary>Contract: a format string is applied (DateTime → yyyy-MM-dd).</summary>
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
        var result = await Sut.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("1990-05-15");
    }

    /// <summary>Contract: a dotted path resolves a nested property.</summary>
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
        var result = await Sut.MapFieldAsync(
            sourceObject,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("EXP-2024-001");
    }

    //
    // MapAllFieldsAsync Tests
    //

    /// <summary>Contract: all valid fields map into the target dictionary.</summary>
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
        var result = await Sut.MapAllFieldsAsync(
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

    /// <summary>Contract: a missing required field aborts the whole mapping with failure.</summary>
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
        var result = await Sut.MapAllFieldsAsync(
            sourceObject,
            template,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Age");
    }

    /// <summary>Contract: a missing optional field does not abort mapping.</summary>
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
        var result = await Sut.MapAllFieldsAsync(
            sourceObject,
            template,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ContainsKey("TargetName").ShouldBeTrue();
    }

    //
    // ValidateMappingAsync Tests
    //

    /// <summary>Contract: a valid mapping against a known type succeeds.</summary>
    [Fact]
    public async Task ValidateMappingAsync_WhenMappingValid_ReturnsSuccess()
    {
        // Arrange: REAL validation
        var sourceType = typeof(TestSourceObject);
        var mapping = new FieldMapping("Name", "TargetName", true);

        // Act: REAL mapper call
        var result = await Sut.ValidateMappingAsync(
            sourceType,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>Contract: a field path absent on the source type fails validation.</summary>
    [Fact]
    public async Task ValidateMappingAsync_WhenFieldPathInvalid_ReturnsFailure()
    {
        // Arrange: REAL validation with invalid field path
        var sourceType = typeof(TestSourceObject);
        var mapping = new FieldMapping("NonExistentField", "Target", true);

        // Act: REAL mapper call
        var result = await Sut.ValidateMappingAsync(
            sourceType,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("NonExistentField");
    }

    /// <summary>Contract: an unsupported transformation expression fails validation.</summary>
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
        var result = await Sut.ValidateMappingAsync(
            sourceType,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("InvalidFunction");
    }

    //
    // ApplyTransformationAsync Tests
    //

    /// <summary>Contract: ToUpper() uppercases the value.</summary>
    [Fact]
    public async Task ApplyTransformationAsync_ToUpper_ReturnsUppercase()
    {
        // Arrange: REAL transformation
        var value = "hello world";
        var transformExpression = "ToUpper()";

        // Act: REAL mapper call
        var result = await Sut.ApplyTransformationAsync(
            value,
            transformExpression,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("HELLO WORLD");
    }

    /// <summary>Contract: Trim() removes surrounding whitespace.</summary>
    [Fact]
    public async Task ApplyTransformationAsync_Trim_RemovesWhitespace()
    {
        // Arrange: REAL transformation
        var value = "  hello world  ";
        var transformExpression = "Trim()";

        // Act: REAL mapper call
        var result = await Sut.ApplyTransformationAsync(
            value,
            transformExpression,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("hello world");
    }

    /// <summary>Contract: chained transformations apply left to right.</summary>
    [Fact]
    public async Task ApplyTransformationAsync_ChainedTransformations_AppliesInOrder()
    {
        // Arrange: REAL chained transformations
        var value = "  hello world  ";
        var transformExpression = "Trim() | ToUpper()";

        // Act: REAL mapper call
        var result = await Sut.ApplyTransformationAsync(
            value,
            transformExpression,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("HELLO WORLD");
    }

    /// <summary>Contract: Substring(start, length) extracts the substring.</summary>
    [Fact]
    public async Task ApplyTransformationAsync_Substring_ExtractsSubstring()
    {
        // Arrange: REAL transformation
        var value = "Hello World";
        var transformExpression = "Substring(0, 5)";

        // Act: REAL mapper call
        var result = await Sut.ApplyTransformationAsync(
            value,
            transformExpression,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Hello");
    }

    //
    // ValidateFieldValueAsync Tests
    //

    /// <summary>Contract: a value matching a Regex rule passes.</summary>
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
        var result = await Sut.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>Contract: a value failing a Regex rule fails.</summary>
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
        var result = await Sut.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("regex");
    }

    /// <summary>Contract: a value within a Range rule passes.</summary>
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
        var result = await Sut.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>Contract: a value outside a Range rule fails.</summary>
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
        var result = await Sut.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("range");
    }

    /// <summary>Contract: a value meeting a MinLength rule passes.</summary>
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
        var result = await Sut.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>Contract: a value below a MinLength rule fails.</summary>
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
        var result = await Sut.ValidateFieldValueAsync(
            value,
            mapping,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("minimum length");
    }

    //
    // Helper classes for testing (lifted with the bodies)
    //

    private sealed class TestSourceObject
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string? Email { get; set; }
        public DateTime BirthDate { get; set; }
        public TestExpediente? Expediente { get; set; }
    }

    private sealed class TestExpediente
    {
        public string NumeroExpediente { get; set; } = string.Empty;
    }
}
