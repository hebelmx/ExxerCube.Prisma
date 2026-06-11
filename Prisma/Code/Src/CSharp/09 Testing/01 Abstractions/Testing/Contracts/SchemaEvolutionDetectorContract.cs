using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="ISchemaEvolutionDetector"/> — every implementation
/// (and the mock blueprint) must pass these tests unchanged (ADR-005).
/// </summary>
/// <remarks>
/// <para>
/// 16 tests: the 15 shared (zero-named-drift) bodies lifted from <c>SchemaEvolutionDetectorTests</c>
/// plus the promoted boundary case <see cref="CalculateSimilarity_WithEmptyStrings_Returns0"/>
/// (master plan §2 "promote the ~3 contract-grade null/boundary tests" — recounted
/// per-method). Where the shared bodies diverged, the twin's looser assertion is the
/// executable truth (e.g. <see cref="CalculateSimilarity_WithSimilarStrings_ReturnsHighScore"/>
/// asserts ≥ 0.5, not the mock's ≥ 0.7).
/// </para>
/// <para>
/// The two "Additional Real-World Scenarios" twin tests (recursive nested-field detection;
/// exact <c>dataType</c> strings for the complex-object suggestion) stay implementation-side
/// in the deriving class — their specifics are implementation richness, not behavior every
/// correct implementation must exhibit (ADR-005 §5).
/// </para>
/// <para>
/// Uses the <c>CreateSut()</c> fallback plus a <see cref="SeedActiveTemplateAsync"/> hook:
/// <see cref="ISchemaEvolutionDetector.DetectDriftForActiveTemplateAsync"/> reads the
/// active template from a backing repository the implementation owns, which the contract
/// seeds implementation-agnostically.
/// </para>
/// </remarks>
public abstract class SchemaEvolutionDetectorContract
{
    /// <summary>Creates the implementation under test. Called once per test.</summary>
    /// <returns>The <see cref="ISchemaEvolutionDetector"/> implementation to verify.</returns>
    protected abstract ISchemaEvolutionDetector CreateSut();

    /// <summary>
    /// Seeds an active template into the repository backing the SUT, so
    /// <see cref="ISchemaEvolutionDetector.DetectDriftForActiveTemplateAsync"/> can find it.
    /// </summary>
    /// <param name="template">The active template to seed.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    protected abstract Task SeedActiveTemplateAsync(TemplateDefinition template, CancellationToken cancellationToken);

    //
    // DetectDriftAsync Tests
    //

    /// <summary>Contract: a source matching the template exactly reports no drift.</summary>
    [Fact]
    public async Task DetectDriftAsync_WithNoDrift_ReturnsSuccessWithNoDrift()
    {
        // Arrange: Source has exactly the fields template expects
        var detector = CreateSut();
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
        var result = await detector.DetectDriftAsync(sourceObject, template, CancellationToken.None);

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

    /// <summary>Contract: source fields absent from the template are reported as new (Low severity).</summary>
    [Fact]
    public async Task DetectDriftAsync_WithNewFields_ReturnsSuccessWithNewFieldsDrift()
    {
        // Arrange: Source has extra fields not in template
        var detector = CreateSut();
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
        var result = await detector.DetectDriftAsync(sourceObject, template, CancellationToken.None);

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

    /// <summary>Contract: a missing required template field yields High severity.</summary>
    [Fact]
    public async Task DetectDriftAsync_WithMissingRequiredFields_ReturnsSuccessWithHighSeverity()
    {
        // Arrange: Source is missing required template fields
        var detector = CreateSut();
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
        var result = await detector.DetectDriftAsync(sourceObject, template, CancellationToken.None);

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

    /// <summary>Contract: fields resembling renamed template fields are detected via fuzzy match (Medium).</summary>
    [Fact]
    public async Task DetectDriftAsync_WithRenamedFields_ReturnsSuccessWithRenamedFieldsDrift()
    {
        // Arrange: Source has fields that look like renamed versions of template fields
        var detector = CreateSut();
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
        var result = await detector.DetectDriftAsync(sourceObject, template, CancellationToken.None);

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

    /// <summary>Contract: a null source object yields a failure.</summary>
    [Fact]
    public async Task DetectDriftAsync_WithNullSourceObject_ReturnsFailure()
    {
        // Arrange
        var detector = CreateSut();
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0"
        };

        // Act
        var result = await detector.DetectDriftAsync(null!, template, CancellationToken.None);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Source object cannot be null");
    }

    /// <summary>Contract: a null template yields a failure.</summary>
    [Fact]
    public async Task DetectDriftAsync_WithNullTemplate_ReturnsFailure()
    {
        // Arrange
        var detector = CreateSut();
        var sourceObject = new { Name = "Test" };

        // Act
        var result = await detector.DetectDriftAsync(sourceObject, null!, CancellationToken.None);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Template cannot be null");
    }

    //
    // DetectDriftForActiveTemplateAsync Tests
    //

    /// <summary>Contract: drift is detected against the active template for a type.</summary>
    [Fact]
    public async Task DetectDriftForActiveTemplateAsync_WithActiveTemplate_ReturnsSuccess()
    {
        // Arrange: Create and seed active template
        var detector = CreateSut();
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

        await SeedActiveTemplateAsync(template, CancellationToken.None);

        var sourceObject = new { Name = "John Doe", Age = 30 };

        // Act
        var result = await detector.DetectDriftForActiveTemplateAsync(sourceObject, "Excel", CancellationToken.None);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.TemplateType.ShouldBe("Excel");
        result.Value.HasDrift.ShouldBeFalse();
    }

    /// <summary>Contract: no active template for the type yields a failure.</summary>
    [Fact]
    public async Task DetectDriftForActiveTemplateAsync_WithNoActiveTemplate_ReturnsFailure()
    {
        // Arrange: No active template in repository
        var detector = CreateSut();
        var sourceObject = new { Name = "Test" };
        var templateType = "InvalidType";

        // Act
        var result = await detector.DetectDriftForActiveTemplateAsync(sourceObject, templateType, CancellationToken.None);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("No active template found");
    }

    //
    // SuggestFieldMappingsAsync Tests
    //

    /// <summary>Contract: a source object yields one suggested mapping per field, with detected types.</summary>
    [Fact]
    public async Task SuggestFieldMappingsAsync_WithValidSourceObject_ReturnsSuggestedMappings()
    {
        // Arrange
        var detector = CreateSut();
        var sourceObject = new
        {
            Name = "John Doe",
            Age = 30,
            Email = "john@example.com"
        };

        var templateType = "Excel";

        // Act
        var result = await detector.SuggestFieldMappingsAsync(sourceObject, templateType, CancellationToken.None);

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

    /// <summary>Contract: a null source object yields a failure.</summary>
    [Fact]
    public async Task SuggestFieldMappingsAsync_WithNullSourceObject_ReturnsFailure()
    {
        // Arrange
        var detector = CreateSut();
        var templateType = "Excel";

        // Act
        var result = await detector.SuggestFieldMappingsAsync(null!, templateType, CancellationToken.None);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Source object cannot be null");
    }

    //
    // CalculateSimilarity Tests
    //

    /// <summary>Contract: identical strings score 1.0.</summary>
    [Fact]
    public void CalculateSimilarity_WithIdenticalStrings_Returns1()
    {
        // Arrange & Act
        var detector = CreateSut();
        var result = detector.CalculateSimilarity("Name", "Name");

        // Assert: SAME expectation (Liskov!)
        result.ShouldBe(1.0);
    }

    /// <summary>Contract: similar strings score in the upper range (≥ 0.5).</summary>
    [Fact]
    public void CalculateSimilarity_WithSimilarStrings_ReturnsHighScore()
    {
        // Arrange & Act
        var detector = CreateSut();
        var result = detector.CalculateSimilarity("Name", "FullName");

        // Assert: SAME expectations (Liskov!)
        result.ShouldBeGreaterThanOrEqualTo(0.5);
        result.ShouldBeLessThanOrEqualTo(1.0);
    }

    /// <summary>Contract: dissimilar strings score low (&lt; 0.5).</summary>
    [Fact]
    public void CalculateSimilarity_WithCompletelyDifferentStrings_ReturnsLowScore()
    {
        // Arrange & Act
        var detector = CreateSut();
        var result = detector.CalculateSimilarity("Name", "XYZ");

        // Assert: SAME expectation (Liskov!)
        result.ShouldBeLessThan(0.5);
    }

    /// <summary>Contract (boundary, promoted): an empty input scores 0.0.</summary>
    [Fact]
    public void CalculateSimilarity_WithEmptyStrings_Returns0()
    {
        // Arrange & Act
        var detector = CreateSut();
        var result = detector.CalculateSimilarity("", "Name");

        // Assert
        result.ShouldBe(0.0);
    }

    //
    // ValidateTemplateCompatibilityAsync Tests
    //

    /// <summary>Contract: a compatible template validates successfully.</summary>
    [Fact]
    public async Task ValidateTemplateCompatibilityAsync_WithCompatibleTemplate_ReturnsSuccess()
    {
        // Arrange
        var detector = CreateSut();
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
        var result = await detector.ValidateTemplateCompatibilityAsync(sourceObject, template, CancellationToken.None);

        // Assert: SAME expectation (Liskov!)
        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>Contract: a template requiring a missing field is incompatible.</summary>
    [Fact]
    public async Task ValidateTemplateCompatibilityAsync_WithIncompatibleTemplate_ReturnsFailure()
    {
        // Arrange
        var detector = CreateSut();
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
        var result = await detector.ValidateTemplateCompatibilityAsync(sourceObject, template, CancellationToken.None);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("incompatible");
        (result.Error ?? string.Empty).ShouldContain("Age");
    }

    //
    // Cancellation Tests (Phase 6 — repository-wide CancellationToken mandate, ADR-005 §5)
    //
    // CalculateSimilarity is a synchronous pure function with no CancellationToken, so it has no cancel test.
    //

    private static TemplateDefinition CompatibleTemplate()
    {
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
        return template;
    }

    /// <summary>Contract: a pre-cancelled token short-circuits DetectDriftAsync to Cancelled (never a throw).</summary>
    [Fact]
    public async Task DetectDriftAsync_WhenCancellationRequested_ReturnsCancelled()
    {
        var detector = CreateSut();

        var result = await detector.DetectDriftAsync(new { Name = "John" }, CompatibleTemplate(), new CancellationToken(canceled: true));

        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>Contract: a pre-cancelled token short-circuits DetectDriftForActiveTemplateAsync to Cancelled.</summary>
    [Fact]
    public async Task DetectDriftForActiveTemplateAsync_WhenCancellationRequested_ReturnsCancelled()
    {
        var detector = CreateSut();

        var result = await detector.DetectDriftForActiveTemplateAsync(new { Name = "John" }, "Excel", new CancellationToken(canceled: true));

        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>Contract: a pre-cancelled token short-circuits SuggestFieldMappingsAsync to Cancelled.</summary>
    [Fact]
    public async Task SuggestFieldMappingsAsync_WhenCancellationRequested_ReturnsCancelled()
    {
        var detector = CreateSut();

        var result = await detector.SuggestFieldMappingsAsync(new { Name = "John" }, "Excel", new CancellationToken(canceled: true));

        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>Contract: a pre-cancelled token short-circuits ValidateTemplateCompatibilityAsync to Cancelled.</summary>
    [Fact]
    public async Task ValidateTemplateCompatibilityAsync_WhenCancellationRequested_ReturnsCancelled()
    {
        var detector = CreateSut();

        var result = await detector.ValidateTemplateCompatibilityAsync(new { Name = "John" }, CompatibleTemplate(), new CancellationToken(canceled: true));

        result.IsCancelled().ShouldBeTrue();
    }
}
