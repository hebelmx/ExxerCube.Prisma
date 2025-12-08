using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive.Data;
using IndQuestResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

/// <summary>
/// Implementation tests for AdaptiveExporter using real dependencies.
/// These tests have IDENTICAL names to IAdaptiveExporterContractTests to verify Liskov Substitution Principle.
/// ITDD Step 3: Test real implementation with same contract (RED → GREEN).
/// </summary>
public sealed class AdaptiveExporterTests : IDisposable
{
    private readonly TemplateDbContext _dbContext;
    private readonly ITemplateRepository _templateRepository;
    private readonly ITemplateFieldMapper _fieldMapper;
    private readonly IAdaptiveExporter _exporter;
    private readonly ILogger<AdaptiveExporter> _logger;

    public AdaptiveExporterTests()
    {
        // Setup in-memory database for testing
        var options = new DbContextOptionsBuilder<TemplateDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new TemplateDbContext(options);

        // Create real dependencies
        _templateRepository = new TemplateRepository(_dbContext, Substitute.For<ILogger<TemplateRepository>>());
        _fieldMapper = new TemplateFieldMapper(Substitute.For<ILogger<TemplateFieldMapper>>());
        _logger = Substitute.For<ILogger<AdaptiveExporter>>();

        // Create the real exporter (will implement next)
        _exporter = new AdaptiveExporter(_templateRepository, _fieldMapper, _logger);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ExportAsync_WhenValidSourceAndTemplate_ReturnsSuccessWithBytes()
    {
        // Arrange: Create and save a real template
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

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceObject = new { Name = "Test" };

        // Act
        var result = await _exporter.ExportAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectations as contract test (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ExportAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange: No template in database
        var sourceObject = new { Name = "Test" };

        // Act
        var result = await _exporter.ExportAsync(sourceObject, "InvalidType", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Template");
    }

    [Fact]
    public async Task ExportAsync_WhenSourceIsNull_ReturnsFailure()
    {
        // Arrange: No source object
        // Act
        var result = await _exporter.ExportAsync(null!, "Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Source object cannot be null");
    }

    [Fact]
    public async Task ExportAsync_WhenMappingFails_ReturnsFailure()
    {
        // Arrange: Create template with required field that doesn't exist in source
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

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceObject = new { Name = "Test" }; // Missing Age field

        // Act
        var result = await _exporter.ExportAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("field");
    }

    [Fact]
    public async Task ExportWithVersionAsync_WhenValidVersion_ReturnsSuccessWithBytes()
    {
        // Arrange
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

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceObject = new { Name = "Test" };

        // Act
        var result = await _exporter.ExportWithVersionAsync(sourceObject, "Excel", "1.0.0", TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ExportWithVersionAsync_WhenInvalidVersion_ReturnsFailure()
    {
        // Arrange
        var sourceObject = new { Name = "Test" };

        // Act
        var result = await _exporter.ExportWithVersionAsync(sourceObject, "Excel", "99.99.99", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("version");
    }

    [Fact]
    public async Task GetActiveTemplateAsync_WhenTemplateExists_ReturnsTemplate()
    {
        // Arrange: Create and save active template
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

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        // Act
        var result = await _exporter.GetActiveTemplateAsync("Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.TemplateType.ShouldBe("Excel");
        result.Value.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task GetActiveTemplateAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange: No active template
        // Act
        var result = await _exporter.GetActiveTemplateAsync("InvalidType", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("No active template found");
    }

    [Fact]
    public async Task ValidateExportAsync_WhenValidSource_ReturnsSuccess()
    {
        // Arrange
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

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceObject = new { Name = "Test", Age = 30 };

        // Act
        var result = await _exporter.ValidateExportAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateExportAsync_WhenRequiredFieldMissing_ReturnsFailure()
    {
        // Arrange
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

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceObject = new { Name = "Test" }; // Missing Age

        // Act
        var result = await _exporter.ValidateExportAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Required field 'Age' not found");
    }

    [Fact]
    public async Task ValidateExportAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange
        var sourceObject = new { Name = "Test" };

        // Act
        var result = await _exporter.ValidateExportAsync(sourceObject, "InvalidType", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Template 'InvalidType' not found");
    }

    [Fact]
    public async Task PreviewMappingAsync_WhenValidSource_ReturnsMappedFields()
    {
        // Arrange
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

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceObject = new { Name = "John Doe", Age = 30 };

        // Act
        var result = await _exporter.PreviewMappingAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        result.Value["TargetName"].ShouldBe("John Doe");
        result.Value["TargetAge"].ShouldBe("30");
    }

    [Fact]
    public async Task PreviewMappingAsync_WhenSourceIsNull_ReturnsFailure()
    {
        // Arrange & Act
        var result = await _exporter.PreviewMappingAsync(null!, "Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Source object cannot be null");
    }

    [Fact]
    public async Task PreviewMappingAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange
        var sourceObject = new { Name = "Test" };

        // Act
        var result = await _exporter.PreviewMappingAsync(sourceObject, "InvalidType", TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Template 'InvalidType' not found");
    }

    [Fact]
    public async Task IsTemplateAvailableAsync_WhenTemplateExists_ReturnsTrue()
    {
        // Arrange
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

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        // Act
        var result = await _exporter.IsTemplateAvailableAsync("Excel", TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task IsTemplateAvailableAsync_WhenTemplateNotFound_ReturnsFalse()
    {
        // Arrange & Act
        var result = await _exporter.IsTemplateAvailableAsync("InvalidType", TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public void ClearTemplateCache_DoesNotThrow()
    {
        // Arrange & Act
        var exception = Record.Exception(() => _exporter.ClearTemplateCache());

        // Assert: SAME expectation (Liskov!)
        exception.ShouldBeNull();
    }
}
