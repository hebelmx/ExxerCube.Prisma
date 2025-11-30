using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Domain.Domain.Interfaces;

/// <summary>
/// ITDD contract tests for ISchemaEvolutionDetector.
/// These tests define the behavioral contract that ALL implementations must satisfy.
///
/// ITDD Step 1: Write contract tests with mocks (RED phase).
/// ITDD Step 2: These tests should PASS with a mock implementation.
/// ITDD Step 3: Real implementation tests will mirror these (Liskov Substitution).
/// ITDD Step 4: Verify real implementation passes same contract.
/// </summary>
public sealed class ISchemaEvolutionDetectorContractTests
{
    private readonly ISchemaEvolutionDetector _detector;
    private readonly ITemplateRepository _mockRepository;

    public ISchemaEvolutionDetectorContractTests()
    {
        _detector = Substitute.For<ISchemaEvolutionDetector>();
        _mockRepository = Substitute.For<ITemplateRepository>();
    }

    #region DetectDriftAsync Contract Tests

    [Fact]
    public async Task DetectDriftAsync_WithNoDrift_ReturnsSuccessWithNoDrift()
    {
        // Arrange: Source has exactly the fields template expects
        var sourceObject = new
        {
            Name = "John Doe",
            Age = 30
        };

        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));

        var expectedReport = new SchemaDriftReport
        {
            TemplateId = template.TemplateId,
            TemplateType = template.TemplateType,
            TemplateVersion = template.Version,
            Severity = DriftSeverity.None
        };

        _detector.DetectDriftAsync(sourceObject, template, Arg.Any<CancellationToken>())
            .Returns(Result<SchemaDriftReport>.Success(expectedReport));

        // Act
        var result = await _detector.DetectDriftAsync(sourceObject, template, CancellationToken.None);

        // Assert: Contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.HasDrift.ShouldBeFalse();
        result.Value.Severity.ShouldBe(DriftSeverity.None);
        result.Value.NewFields.ShouldBeEmpty();
        result.Value.MissingFields.ShouldBeEmpty();
        result.Value.RenamedFields.ShouldBeEmpty();
    }

    [Fact]
    public async Task DetectDriftAsync_WithNewFields_ReturnsSuccessWithNewFieldsDrift()
    {
        // Arrange: Source has extra fields not in template
        var sourceObject = new
        {
            Name = "John Doe",
            Age = 30,
            Email = "john@example.com",  // NEW field
            Phone = "555-1234"             // NEW field
        };

        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));

        var expectedReport = new SchemaDriftReport
        {
            TemplateId = template.TemplateId,
            TemplateType = template.TemplateType,
            TemplateVersion = template.Version,
            Severity = DriftSeverity.Low,
            NewFields =
            {
                new NewFieldInfo { FieldPath = "Email", DetectedType = "string", SampleValue = "john@example.com" },
                new NewFieldInfo { FieldPath = "Phone", DetectedType = "string", SampleValue = "555-1234" }
            }
        };

        _detector.DetectDriftAsync(sourceObject, template, Arg.Any<CancellationToken>())
            .Returns(Result<SchemaDriftReport>.Success(expectedReport));

        // Act
        var result = await _detector.DetectDriftAsync(sourceObject, template, CancellationToken.None);

        // Assert: Contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.HasDrift.ShouldBeTrue();
        result.Value.Severity.ShouldBe(DriftSeverity.Low);
        result.Value.NewFields.Count.ShouldBe(2);
        result.Value.MissingFields.ShouldBeEmpty();
        result.Value.RenamedFields.ShouldBeEmpty();
    }

    [Fact]
    public async Task DetectDriftAsync_WithMissingRequiredFields_ReturnsSuccessWithHighSeverity()
    {
        // Arrange: Source is missing required template fields
        var sourceObject = new
        {
            Name = "John Doe"
            // Age is MISSING but required
        };

        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));  // REQUIRED

        var expectedReport = new SchemaDriftReport
        {
            TemplateId = template.TemplateId,
            TemplateType = template.TemplateType,
            TemplateVersion = template.Version,
            Severity = DriftSeverity.High,
            MissingFields =
            {
                new MissingFieldInfo
                {
                    FieldPath = "Age",
                    TargetField = "TargetAge",
                    IsRequired = true,
                    ExpectedType = "string"
                }
            }
        };

        _detector.DetectDriftAsync(sourceObject, template, Arg.Any<CancellationToken>())
            .Returns(Result<SchemaDriftReport>.Success(expectedReport));

        // Act
        var result = await _detector.DetectDriftAsync(sourceObject, template, CancellationToken.None);

        // Assert: Contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.HasDrift.ShouldBeTrue();
        result.Value.Severity.ShouldBe(DriftSeverity.High);
        result.Value.NewFields.ShouldBeEmpty();
        result.Value.MissingFields.Count.ShouldBe(1);
        result.Value.MissingFields[0].IsRequired.ShouldBeTrue();
        result.Value.RenamedFields.ShouldBeEmpty();
    }

    [Fact]
    public async Task DetectDriftAsync_WithRenamedFields_ReturnsSuccessWithRenamedFieldsDrift()
    {
        // Arrange: Source has fields that look like renamed versions of template fields
        var sourceObject = new
        {
            FullName = "John Doe",      // Looks like "Name" was renamed to "FullName"
            PersonAge = 30               // Looks like "Age" was renamed to "PersonAge"
        };

        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));

        var expectedReport = new SchemaDriftReport
        {
            TemplateId = template.TemplateId,
            TemplateType = template.TemplateType,
            TemplateVersion = template.Version,
            Severity = DriftSeverity.Medium,
            RenamedFields =
            {
                new RenamedFieldInfo
                {
                    OldFieldPath = "Name",
                    SuggestedNewFieldPath = "FullName",
                    SimilarityScore = 0.75,
                    TargetField = "TargetName"
                },
                new RenamedFieldInfo
                {
                    OldFieldPath = "Age",
                    SuggestedNewFieldPath = "PersonAge",
                    SimilarityScore = 0.70,
                    TargetField = "TargetAge"
                }
            }
        };

        _detector.DetectDriftAsync(sourceObject, template, Arg.Any<CancellationToken>())
            .Returns(Result<SchemaDriftReport>.Success(expectedReport));

        // Act
        var result = await _detector.DetectDriftAsync(sourceObject, template, CancellationToken.None);

        // Assert: Contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.HasDrift.ShouldBeTrue();
        result.Value.Severity.ShouldBe(DriftSeverity.Medium);
        result.Value.RenamedFields.Count.ShouldBe(2);
        result.Value.RenamedFields[0].SimilarityScore.ShouldBeGreaterThan(0.7);
    }

    [Fact]
    public async Task DetectDriftAsync_WithNullSourceObject_ReturnsFailure()
    {
        // Arrange
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0"
        };

        _detector.DetectDriftAsync(null!, template, Arg.Any<CancellationToken>())
            .Returns(Result<SchemaDriftReport>.Failure("Source object cannot be null"));

        // Act
        var result = await _detector.DetectDriftAsync(null!, template, CancellationToken.None);

        // Assert: Contract expectations
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Source object cannot be null");
    }

    [Fact]
    public async Task DetectDriftAsync_WithNullTemplate_ReturnsFailure()
    {
        // Arrange
        var sourceObject = new { Name = "Test" };

        _detector.DetectDriftAsync(sourceObject, null!, Arg.Any<CancellationToken>())
            .Returns(Result<SchemaDriftReport>.Failure("Template cannot be null"));

        // Act
        var result = await _detector.DetectDriftAsync(sourceObject, null!, CancellationToken.None);

        // Assert: Contract expectations
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Template cannot be null");
    }

    #endregion

    #region DetectDriftForActiveTemplateAsync Contract Tests

    [Fact]
    public async Task DetectDriftForActiveTemplateAsync_WithActiveTemplate_ReturnsSuccess()
    {
        // Arrange
        var sourceObject = new { Name = "John Doe", Age = 30 };
        var templateType = "Excel";

        var expectedReport = new SchemaDriftReport
        {
            TemplateId = "template-123",
            TemplateType = templateType,
            TemplateVersion = "1.0.0",
            Severity = DriftSeverity.None
        };

        _detector.DetectDriftForActiveTemplateAsync(sourceObject, templateType, Arg.Any<CancellationToken>())
            .Returns(Result<SchemaDriftReport>.Success(expectedReport));

        // Act
        var result = await _detector.DetectDriftForActiveTemplateAsync(sourceObject, templateType, CancellationToken.None);

        // Assert: Contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.TemplateType.ShouldBe(templateType);
    }

    [Fact]
    public async Task DetectDriftForActiveTemplateAsync_WithNoActiveTemplate_ReturnsFailure()
    {
        // Arrange
        var sourceObject = new { Name = "Test" };
        var templateType = "InvalidType";

        _detector.DetectDriftForActiveTemplateAsync(sourceObject, templateType, Arg.Any<CancellationToken>())
            .Returns(Result<SchemaDriftReport>.Failure("No active template found for type 'InvalidType'"));

        // Act
        var result = await _detector.DetectDriftForActiveTemplateAsync(sourceObject, templateType, CancellationToken.None);

        // Assert: Contract expectations
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("No active template found");
    }

    #endregion

    #region SuggestFieldMappingsAsync Contract Tests

    [Fact]
    public async Task SuggestFieldMappingsAsync_WithValidSourceObject_ReturnsSuggestedMappings()
    {
        // Arrange
        var sourceObject = new
        {
            Name = "John Doe",
            Age = 30,
            Email = "john@example.com"
        };

        var templateType = "Excel";

        var expectedMappings = new[]
        {
            new FieldMapping("Name", "Name", isRequired: true, dataType: "string"),
            new FieldMapping("Age", "Age", isRequired: true, dataType: "int"),
            new FieldMapping("Email", "Email", isRequired: false, dataType: "string")
        };

        _detector.SuggestFieldMappingsAsync(sourceObject, templateType, Arg.Any<CancellationToken>())
            .Returns(Result<FieldMapping[]>.Success(expectedMappings));

        // Act
        var result = await _detector.SuggestFieldMappingsAsync(sourceObject, templateType, CancellationToken.None);

        // Assert: Contract expectations
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Length.ShouldBe(3);
        result.Value[0].SourceFieldPath.ShouldBe("Name");
        result.Value[0].DataType.ShouldBe("string");
    }

    [Fact]
    public async Task SuggestFieldMappingsAsync_WithNullSourceObject_ReturnsFailure()
    {
        // Arrange
        var templateType = "Excel";

        _detector.SuggestFieldMappingsAsync(null!, templateType, Arg.Any<CancellationToken>())
            .Returns(Result<FieldMapping[]>.Failure("Source object cannot be null"));

        // Act
        var result = await _detector.SuggestFieldMappingsAsync(null!, templateType, CancellationToken.None);

        // Assert: Contract expectations
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Source object cannot be null");
    }

    #endregion

    #region CalculateSimilarity Contract Tests

    [Fact]
    public void CalculateSimilarity_WithIdenticalStrings_Returns1()
    {
        // Arrange & Act
        _detector.CalculateSimilarity("Name", "Name").Returns(1.0);
        var result = _detector.CalculateSimilarity("Name", "Name");

        // Assert: Contract expectations
        result.ShouldBe(1.0);
    }

    [Fact]
    public void CalculateSimilarity_WithSimilarStrings_ReturnsHighScore()
    {
        // Arrange & Act
        _detector.CalculateSimilarity("Name", "FullName").Returns(0.75);
        var result = _detector.CalculateSimilarity("Name", "FullName");

        // Assert: Contract expectations
        result.ShouldBeGreaterThanOrEqualTo(0.7);
        result.ShouldBeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void CalculateSimilarity_WithCompletelyDifferentStrings_ReturnsLowScore()
    {
        // Arrange & Act
        _detector.CalculateSimilarity("Name", "XYZ").Returns(0.2);
        var result = _detector.CalculateSimilarity("Name", "XYZ");

        // Assert: Contract expectations
        result.ShouldBeLessThan(0.5);
    }

    #endregion

    #region ValidateTemplateCompatibilityAsync Contract Tests

    [Fact]
    public async Task ValidateTemplateCompatibilityAsync_WithCompatibleTemplate_ReturnsSuccess()
    {
        // Arrange
        var sourceObject = new { Name = "John Doe", Age = 30 };

        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));

        _detector.ValidateTemplateCompatibilityAsync(sourceObject, template, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _detector.ValidateTemplateCompatibilityAsync(sourceObject, template, CancellationToken.None);

        // Assert: Contract expectations
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateTemplateCompatibilityAsync_WithIncompatibleTemplate_ReturnsFailure()
    {
        // Arrange
        var sourceObject = new { Name = "John Doe" };  // Missing Age

        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));  // REQUIRED but missing

        _detector.ValidateTemplateCompatibilityAsync(sourceObject, template, Arg.Any<CancellationToken>())
            .Returns(Result.Failure("Template incompatible: Required field 'Age' not found in source"));

        // Act
        var result = await _detector.ValidateTemplateCompatibilityAsync(sourceObject, template, CancellationToken.None);

        // Assert: Contract expectations
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("incompatible");
    }

    #endregion
}
