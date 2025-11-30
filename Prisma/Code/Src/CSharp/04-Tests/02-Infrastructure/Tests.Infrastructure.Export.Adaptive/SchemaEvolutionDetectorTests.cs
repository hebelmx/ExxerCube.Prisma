using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive.Data;
using IndQuestResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

/// <summary>
/// Implementation tests for SchemaEvolutionDetector using REAL dependencies.
/// These tests have IDENTICAL names to ISchemaEvolutionDetectorContractTests to verify Liskov Substitution Principle.
/// ITDD Step 4: Test real implementation with same contract (Verify Liskov).
/// </summary>
public sealed class SchemaEvolutionDetectorTests : IDisposable
{
    private readonly TemplateDbContext _dbContext;
    private readonly ITemplateRepository _templateRepository;
    private readonly ISchemaEvolutionDetector _detector;

    public SchemaEvolutionDetectorTests()
    {
        // Setup REAL database (InMemory for tests)
        var options = new DbContextOptionsBuilder<TemplateDbContext>()
            .UseInMemoryDatabase(databaseName: $"SchemaDetectorTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new TemplateDbContext(options);

        // Create REAL dependencies (NO MOCKS)
        _templateRepository = new TemplateRepository(_dbContext, NullLogger<TemplateRepository>.Instance);
        _detector = new SchemaEvolutionDetector(_templateRepository, NullLogger<SchemaEvolutionDetector>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region DetectDriftAsync Tests (Mirror Contract Tests)

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
            Version = "1.0.0",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));

        // Act
        var result = await _detector.DetectDriftAsync(sourceObject, template, CancellationToken.None);

        // Assert: SAME expectations as contract test (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.HasDrift.ShouldBeFalse();
        result.Value.Severity.ShouldBe(DriftSeverity.None);
        result.Value.NewFields.ShouldBeEmpty();
        result.Value.MissingFields.ShouldBeEmpty();
        result.Value.RenamedFields.ShouldBeEmpty();
        result.Value.TemplateId.ShouldBe(template.TemplateId);
        result.Value.TemplateType.ShouldBe(template.TemplateType);
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
            Version = "1.0.0",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));

        // Act
        var result = await _detector.DetectDriftAsync(sourceObject, template, CancellationToken.None);

        // Assert: SAME expectations as contract test (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.HasDrift.ShouldBeTrue();
        result.Value.Severity.ShouldBe(DriftSeverity.Low);
        result.Value.NewFields.Count.ShouldBe(2);
        result.Value.MissingFields.ShouldBeEmpty();
        result.Value.RenamedFields.ShouldBeEmpty();

        // Verify new field details
        result.Value.NewFields.ShouldContain(nf => nf.FieldPath == "Email");
        result.Value.NewFields.ShouldContain(nf => nf.FieldPath == "Phone");
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
            Version = "1.0.0",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));  // REQUIRED

        // Act
        var result = await _detector.DetectDriftAsync(sourceObject, template, CancellationToken.None);

        // Assert: SAME expectations as contract test (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.HasDrift.ShouldBeTrue();
        result.Value.Severity.ShouldBe(DriftSeverity.High);
        result.Value.NewFields.ShouldBeEmpty();
        result.Value.MissingFields.Count.ShouldBe(1);
        result.Value.MissingFields[0].FieldPath.ShouldBe("Age");
        result.Value.MissingFields[0].IsRequired.ShouldBeTrue();
        result.Value.RenamedFields.ShouldBeEmpty();
    }

    [Fact]
    public async Task DetectDriftAsync_WithRenamedFields_ReturnsSuccessWithRenamedFieldsDrift()
    {
        // Arrange: Source has fields that look like renamed versions of template fields
        var sourceObject = new
        {
            FullName = "John Doe",      // Similar to "Name"
            PersonAge = 30               // Similar to "Age"
        };

        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));

        // Act
        var result = await _detector.DetectDriftAsync(sourceObject, template, CancellationToken.None);

        // Assert: SAME expectations as contract test (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.HasDrift.ShouldBeTrue();
        result.Value.Severity.ShouldBe(DriftSeverity.Medium);

        // Should detect renamed fields (fuzzy match)
        result.Value.RenamedFields.Count.ShouldBeGreaterThan(0);

        // Verify similarity scores are meaningful
        foreach (var renamed in result.Value.RenamedFields)
        {
            renamed.SimilarityScore.ShouldBeGreaterThanOrEqualTo(0.7);
            renamed.SimilarityScore.ShouldBeLessThanOrEqualTo(1.0);
        }
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

        // Act
        var result = await _detector.DetectDriftAsync(null!, template, CancellationToken.None);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Source object cannot be null");
    }

    [Fact]
    public async Task DetectDriftAsync_WithNullTemplate_ReturnsFailure()
    {
        // Arrange
        var sourceObject = new { Name = "Test" };

        // Act
        var result = await _detector.DetectDriftAsync(sourceObject, null!, CancellationToken.None);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Template cannot be null");
    }

    #endregion

    #region DetectDriftForActiveTemplateAsync Tests

    [Fact]
    public async Task DetectDriftForActiveTemplateAsync_WithActiveTemplate_ReturnsSuccess()
    {
        // Arrange: Create and save active template
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));

        await _templateRepository.SaveTemplateAsync(template, CancellationToken.None);

        var sourceObject = new { Name = "John Doe", Age = 30 };

        // Act
        var result = await _detector.DetectDriftForActiveTemplateAsync(sourceObject, "Excel", CancellationToken.None);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.TemplateType.ShouldBe("Excel");
        result.Value.HasDrift.ShouldBeFalse();
    }

    [Fact]
    public async Task DetectDriftForActiveTemplateAsync_WithNoActiveTemplate_ReturnsFailure()
    {
        // Arrange: No active template in database
        var sourceObject = new { Name = "Test" };
        var templateType = "InvalidType";

        // Act
        var result = await _detector.DetectDriftForActiveTemplateAsync(sourceObject, templateType, CancellationToken.None);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("No active template found");
    }

    #endregion

    #region SuggestFieldMappingsAsync Tests

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

        // Act
        var result = await _detector.SuggestFieldMappingsAsync(sourceObject, templateType, CancellationToken.None);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Length.ShouldBe(3);

        // Verify field mappings were created
        result.Value.ShouldContain(m => m.SourceFieldPath == "Name");
        result.Value.ShouldContain(m => m.SourceFieldPath == "Age");
        result.Value.ShouldContain(m => m.SourceFieldPath == "Email");

        // Verify data types detected
        var nameMapping = result.Value.First(m => m.SourceFieldPath == "Name");
        nameMapping.DataType.ShouldBe("string");
    }

    [Fact]
    public async Task SuggestFieldMappingsAsync_WithNullSourceObject_ReturnsFailure()
    {
        // Arrange
        var templateType = "Excel";

        // Act
        var result = await _detector.SuggestFieldMappingsAsync(null!, templateType, CancellationToken.None);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Source object cannot be null");
    }

    #endregion

    #region CalculateSimilarity Tests

    [Fact]
    public void CalculateSimilarity_WithIdenticalStrings_Returns1()
    {
        // Arrange & Act
        var result = _detector.CalculateSimilarity("Name", "Name");

        // Assert: SAME expectation (Liskov!)
        result.ShouldBe(1.0);
    }

    [Fact]
    public void CalculateSimilarity_WithSimilarStrings_ReturnsHighScore()
    {
        // Arrange & Act
        var result = _detector.CalculateSimilarity("Name", "FullName");

        // Assert: SAME expectations (Liskov!)
        result.ShouldBeGreaterThanOrEqualTo(0.5);
        result.ShouldBeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void CalculateSimilarity_WithCompletelyDifferentStrings_ReturnsLowScore()
    {
        // Arrange & Act
        var result = _detector.CalculateSimilarity("Name", "XYZ");

        // Assert: SAME expectation (Liskov!)
        result.ShouldBeLessThan(0.5);
    }

    [Fact]
    public void CalculateSimilarity_WithEmptyStrings_Returns0()
    {
        // Arrange & Act
        var result = _detector.CalculateSimilarity("", "Name");

        // Assert
        result.ShouldBe(0.0);
    }

    #endregion

    #region ValidateTemplateCompatibilityAsync Tests

    [Fact]
    public async Task ValidateTemplateCompatibilityAsync_WithCompatibleTemplate_ReturnsSuccess()
    {
        // Arrange
        var sourceObject = new { Name = "John Doe", Age = 30 };

        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));

        // Act
        var result = await _detector.ValidateTemplateCompatibilityAsync(sourceObject, template, CancellationToken.None);

        // Assert: SAME expectation (Liskov!)
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
            Version = "1.0.0",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));  // REQUIRED but missing

        // Act
        var result = await _detector.ValidateTemplateCompatibilityAsync(sourceObject, template, CancellationToken.None);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("incompatible");
        (result.Error ?? string.Empty).ShouldContain("Age");
    }

    #endregion

    #region Additional Real-World Scenarios

    [Fact]
    public async Task DetectDriftAsync_WithNestedObjects_DetectsNestedFields()
    {
        // Arrange: Source with nested object
        var sourceObject = new
        {
            Name = "John Doe",
            Address = new
            {
                Street = "123 Main St",
                City = "Springfield"
            }
        };

        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        // Missing nested Address fields

        // Act
        var result = await _detector.DetectDriftAsync(sourceObject, template, CancellationToken.None);

        // Assert: Should detect nested fields as new
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.HasDrift.ShouldBeTrue();
        result.Value.NewFields.Count.ShouldBeGreaterThan(0);

        // Should find Address.Street and Address.City
        result.Value.NewFields.ShouldContain(nf => nf.FieldPath.Contains("Address"));
    }

    [Fact]
    public async Task SuggestFieldMappingsAsync_WithComplexObject_SuggestsAllFields()
    {
        // Arrange
        var sourceObject = new
        {
            Id = 123,
            Name = "Test",
            IsActive = true,
            CreatedDate = DateTime.Now,
            Amount = 99.99m
        };

        // Act
        var result = await _detector.SuggestFieldMappingsAsync(sourceObject, "Excel", CancellationToken.None);

        // Assert: Should suggest mappings for all primitive fields
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Length.ShouldBe(5);

        // Verify different data types detected
        result.Value.ShouldContain(m => m.DataType == "int");
        result.Value.ShouldContain(m => m.DataType == "string");
        result.Value.ShouldContain(m => m.DataType == "bool");
        result.Value.ShouldContain(m => m.DataType == "datetime");
        result.Value.ShouldContain(m => m.DataType == "decimal");
    }

    #endregion
}
