using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IAdaptiveExporter"/> — every implementation
/// (and the mock blueprint) must pass these tests unchanged (ADR-005).
/// </summary>
/// <remarks>
/// <para>
/// The 17 test bodies are lifted from the zero-drift <c>AdaptiveExporterTests</c> twin —
/// names preserved. Seeding is re-expressed through the contract's
/// <see cref="SeedTemplateAsync"/> hook (the twin seeded through its real
/// <c>ITemplateRepository</c>); the asserted behavior is identical.
/// </para>
/// <para>
/// Uses the <c>CreateSut()</c> fallback plus a seed hook: the exporter reads its templates
/// from a backing repository the implementation owns, which the contract seeds
/// implementation-agnostically.
/// </para>
/// </remarks>
public abstract class AdaptiveExporterContract
{
    /// <summary>Creates the implementation under test. Called once per test.</summary>
    /// <returns>The <see cref="IAdaptiveExporter"/> implementation to verify.</returns>
    protected abstract IAdaptiveExporter CreateSut();

    /// <summary>
    /// Seeds a template into the repository backing the SUT.
    /// </summary>
    /// <param name="template">The template to seed.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    protected abstract Task SeedTemplateAsync(TemplateDefinition template, CancellationToken cancellationToken);

    /// <summary>Contract: a valid source and active template export to non-empty bytes.</summary>
    [Fact]
    public async Task ExportAsync_WhenValidSourceAndTemplate_ReturnsSuccessWithBytes()
    {
        // Arrange: Create and seed a real template
        var exporter = CreateSut();
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Test Excel Template",
            Description = "Test Template",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));

        await SeedTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceObject = new { Name = "Test" };

        // Act
        var result = await exporter.ExportAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectations as contract test (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Length.ShouldBeGreaterThan(0);
    }

    /// <summary>Contract: an unknown template type fails.</summary>
    [Fact]
    public async Task ExportAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange: No template in repository
        var exporter = CreateSut();
        var sourceObject = new { Name = "Test" };

        // Act
        var result = await exporter.ExportAsync(sourceObject, "InvalidType", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Template");
    }

    /// <summary>Contract: a null source fails.</summary>
    [Fact]
    public async Task ExportAsync_WhenSourceIsNull_ReturnsFailure()
    {
        // Arrange: No source object
        var exporter = CreateSut();

        // Act
        var result = await exporter.ExportAsync(null!, "Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Source object cannot be null");
    }

    /// <summary>Contract: a required field absent from the source fails the export.</summary>
    [Fact]
    public async Task ExportAsync_WhenMappingFails_ReturnsFailure()
    {
        // Arrange: Create template with required field that doesn't exist in source
        var exporter = CreateSut();
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Test Excel Template",
            Description = "Test Template",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));

        await SeedTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceObject = new { Name = "Test" }; // Missing Age field

        // Act
        var result = await exporter.ExportAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("field");
    }

    /// <summary>Contract: a specific valid version exports to non-empty bytes.</summary>
    [Fact]
    public async Task ExportWithVersionAsync_WhenValidVersion_ReturnsSuccessWithBytes()
    {
        // Arrange
        var exporter = CreateSut();
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Test Excel Template v1",
            Description = "Test Template",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));

        await SeedTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceObject = new { Name = "Test" };

        // Act
        var result = await exporter.ExportWithVersionAsync(sourceObject, "Excel", "1.0.0", TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Length.ShouldBeGreaterThan(0);
    }

    /// <summary>Contract: an unknown version fails.</summary>
    [Fact]
    public async Task ExportWithVersionAsync_WhenInvalidVersion_ReturnsFailure()
    {
        // Arrange
        var exporter = CreateSut();
        var sourceObject = new { Name = "Test" };

        // Act
        var result = await exporter.ExportWithVersionAsync(sourceObject, "Excel", "99.99.99", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("version");
    }

    /// <summary>Contract: the active template for a type is returned.</summary>
    [Fact]
    public async Task GetActiveTemplateAsync_WhenTemplateExists_ReturnsTemplate()
    {
        // Arrange: Create and seed active template
        var exporter = CreateSut();
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Test Excel Template",
            Description = "Test Template",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));

        await SeedTemplateAsync(template, TestContext.Current.CancellationToken);

        // Act
        var result = await exporter.GetActiveTemplateAsync("Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.TemplateType.ShouldBe("Excel");
        result.Value.Version.ShouldBe("1.0.0");
    }

    /// <summary>Contract: no active template for the type fails.</summary>
    [Fact]
    public async Task GetActiveTemplateAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange: No active template
        var exporter = CreateSut();

        // Act
        var result = await exporter.GetActiveTemplateAsync("InvalidType", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("No active template found");
    }

    /// <summary>Contract: a source providing all required fields validates successfully.</summary>
    [Fact]
    public async Task ValidateExportAsync_WhenValidSource_ReturnsSuccess()
    {
        // Arrange
        var exporter = CreateSut();
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Test Excel Template",
            Description = "Test Template",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));

        await SeedTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceObject = new { Name = "Test", Age = 30 };

        // Act
        var result = await exporter.ValidateExportAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>Contract: a missing required field fails validation.</summary>
    [Fact]
    public async Task ValidateExportAsync_WhenRequiredFieldMissing_ReturnsFailure()
    {
        // Arrange
        var exporter = CreateSut();
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Test Excel Template",
            Description = "Test Template",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));

        await SeedTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceObject = new { Name = "Test" }; // Missing Age

        // Act
        var result = await exporter.ValidateExportAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Required field 'Age' not found");
    }

    /// <summary>Contract: an unknown template type fails validation.</summary>
    [Fact]
    public async Task ValidateExportAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange
        var exporter = CreateSut();
        var sourceObject = new { Name = "Test" };

        // Act
        var result = await exporter.ValidateExportAsync(sourceObject, "InvalidType", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Template 'InvalidType' not found");
    }

    /// <summary>Contract: a preview returns the mapped target fields.</summary>
    [Fact]
    public async Task PreviewMappingAsync_WhenValidSource_ReturnsMappedFields()
    {
        // Arrange
        var exporter = CreateSut();
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Test Excel Template",
            Description = "Test Template",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));
        template.FieldMappings.Add(new FieldMapping("Age", "TargetAge", isRequired: true));

        await SeedTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceObject = new { Name = "John Doe", Age = 30 };

        // Act
        var result = await exporter.PreviewMappingAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        result.Value["TargetName"].ShouldBe("John Doe");
        result.Value["TargetAge"].ShouldBe("30");
    }

    /// <summary>Contract: a null source fails preview.</summary>
    [Fact]
    public async Task PreviewMappingAsync_WhenSourceIsNull_ReturnsFailure()
    {
        // Arrange & Act
        var exporter = CreateSut();
        var result = await exporter.PreviewMappingAsync(null!, "Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Source object cannot be null");
    }

    /// <summary>Contract: an unknown template type fails preview.</summary>
    [Fact]
    public async Task PreviewMappingAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange
        var exporter = CreateSut();
        var sourceObject = new { Name = "Test" };

        // Act
        var result = await exporter.PreviewMappingAsync(sourceObject, "InvalidType", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Template 'InvalidType' not found");
    }

    /// <summary>Contract: availability is true when an active template exists.</summary>
    [Fact]
    public async Task IsTemplateAvailableAsync_WhenTemplateExists_ReturnsTrue()
    {
        // Arrange
        var exporter = CreateSut();
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Test Excel Template",
            Description = "Test Template",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };

        template.FieldMappings.Add(new FieldMapping("Name", "TargetName", isRequired: true));

        await SeedTemplateAsync(template, TestContext.Current.CancellationToken);

        // Act
        var result = await exporter.IsTemplateAvailableAsync("Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    /// <summary>Contract: availability is false when no active template exists.</summary>
    [Fact]
    public async Task IsTemplateAvailableAsync_WhenTemplateNotFound_ReturnsFalse()
    {
        // Arrange & Act
        var exporter = CreateSut();
        var result = await exporter.IsTemplateAvailableAsync("InvalidType", TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    /// <summary>Contract: clearing the cache does not throw.</summary>
    [Fact]
    public void ClearTemplateCache_DoesNotThrow()
    {
        // Arrange & Act
        var exporter = CreateSut();
        var exception = Record.Exception(() => exporter.ClearTemplateCache());

        // Assert: SAME expectation (Liskov!)
        exception.ShouldBeNull();
    }
}
