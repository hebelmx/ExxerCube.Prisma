namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

using Microsoft.EntityFrameworkCore;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive.Data;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Implementation tests for <see cref="TemplateRepository"/> using REAL database.
/// </summary>
/// <remarks>
/// <para>
/// These tests verify that <see cref="TemplateRepository"/> satisfies the
/// behavioral contract defined by <see cref="ITemplateRepository"/>.
/// </para>
/// <para>
/// <strong>LISKOV VERIFICATION:</strong> Test names are IDENTICAL to ITemplateRepositoryContractTests.
/// If all tests pass, Liskov Substitution Principle is satisfied.
/// </para>
/// <para>
/// <strong>Key Differences from Contract Tests:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>Uses REAL TemplateRepository (not mocks)</description></item>
///   <item><description>Uses REAL EF Core InMemory database (not mocks)</description></item>
///   <item><description>Tests actual implementation behavior</description></item>
/// </list>
/// </remarks>
public sealed class TemplateRepositoryTests : IDisposable
{
    private readonly TemplateDbContext _dbContext;
    private readonly TemplateRepository _repository;

    public TemplateRepositoryTests()
    {
        // Create in-memory database for each test (isolated)
        var options = new DbContextOptionsBuilder<TemplateDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TemplateDbContext(options);
        _repository = new TemplateRepository(_dbContext, NullLogger<TemplateRepository>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    //
    // GetTemplateAsync Tests - IDENTICAL names to contract tests
    //

    [Fact]
    public async Task GetTemplateAsync_WhenTemplateExists_ReturnsTemplateDefinition()
    {
        // Arrange: Create REAL template in database
        var template = new TemplateDefinition
        {
            TemplateId = "excel-1.0.0",
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Excel Template v1.0",
            IsActive = true,
            FieldMappings = new List<FieldMapping>
            {
                new FieldMapping("Expediente.NumeroExpediente", "A1", true)
            },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "TestUser"
        };

        await _dbContext.Templates.AddAsync(template, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act: Call REAL repository
        var result = await _repository.GetTemplateAsync(
            "Excel",
            "1.0.0",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations as contract test (Liskov!)
        result.ShouldNotBeNull();
        result.TemplateType.ShouldBe("Excel");
        result.Version.ShouldBe("1.0.0");
        result.FieldMappings.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetTemplateAsync_WhenTemplateNotFound_ReturnsNull()
    {
        // Arrange: Empty database

        // Act: Query non-existent template
        var result = await _repository.GetTemplateAsync(
            "Invalid",
            "9.9.9",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetTemplateAsync_WhenInactiveTemplateExists_ReturnsTemplate()
    {
        // Arrange: Create inactive template
        var template = new TemplateDefinition
        {
            TemplateId = "xml-2.0.0",
            TemplateType = "XML",
            Version = "2.0.0",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "TestUser",
            FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
        };

        await _dbContext.Templates.AddAsync(template, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetTemplateAsync(
            "XML",
            "2.0.0",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.ShouldNotBeNull();
        result.IsActive.ShouldBeFalse();
    }

    //
    // GetLatestTemplateAsync Tests - IDENTICAL names to contract tests
    //

    [Fact]
    public async Task GetLatestTemplateAsync_WhenActiveTemplateExists_ReturnsLatestVersion()
    {
        // Arrange: Create active template
        var template = new TemplateDefinition
        {
            TemplateId = "excel-2.5.0",
            TemplateType = "Excel",
            Version = "2.5.0",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-30),
            ExpirationDate = null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "TestUser",
            FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
        };

        await _dbContext.Templates.AddAsync(template, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetLatestTemplateAsync(
            "Excel",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.ShouldNotBeNull();
        result.TemplateType.ShouldBe("Excel");
        result.Version.ShouldBe("2.5.0");
        result.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetLatestTemplateAsync_WhenNoActiveTemplates_ReturnsNull()
    {
        // Arrange: Create only inactive template
        var template = new TemplateDefinition
        {
            TemplateId = "pdf-1.0.0",
            TemplateType = "PDF",
            Version = "1.0.0",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "TestUser",
            FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
        };

        await _dbContext.Templates.AddAsync(template, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetLatestTemplateAsync(
            "PDF",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetLatestTemplateAsync_WhenMultipleActiveVersions_ReturnsHighestVersion()
    {
        // Arrange: Create multiple active versions
        var templates = new[]
        {
            new TemplateDefinition
            {
                TemplateId = "xml-1.0.0",
                TemplateType = "XML",
                Version = "1.0.0",
                IsActive = true,
                EffectiveDate = DateTime.UtcNow.AddDays(-100),
                CreatedAt = DateTime.UtcNow.AddDays(-100),
                CreatedBy = "TestUser",
                FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
            },
            new TemplateDefinition
            {
                TemplateId = "xml-10.0.1",
                TemplateType = "XML",
                Version = "10.0.1", // Highest version
                IsActive = true,
                EffectiveDate = DateTime.UtcNow.AddDays(-50),
                CreatedAt = DateTime.UtcNow.AddDays(-50),
                CreatedBy = "TestUser",
                FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
            }
        };

        await _dbContext.Templates.AddRangeAsync(templates, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetLatestTemplateAsync(
            "XML",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.ShouldNotBeNull();
        result.Version.ShouldBe("10.0.1");
    }

    //
    // GetAllTemplateVersionsAsync Tests - IDENTICAL names to contract tests
    //

    [Fact]
    public async Task GetAllTemplateVersionsAsync_WhenTemplatesExist_ReturnsAllVersionsDescending()
    {
        // Arrange: Create multiple versions
        var templates = new[]
        {
            new TemplateDefinition
            {
                TemplateId = "excel-1.0.0",
                TemplateType = "Excel",
                Version = "1.0.0",
                IsActive = false,
                CreatedAt = DateTime.UtcNow.AddDays(-100),
                CreatedBy = "TestUser",
                FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
            },
            new TemplateDefinition
            {
                TemplateId = "excel-1.5.0",
                TemplateType = "Excel",
                Version = "1.5.0",
                IsActive = false,
                CreatedAt = DateTime.UtcNow.AddDays(-50),
                CreatedBy = "TestUser",
                FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
            },
            new TemplateDefinition
            {
                TemplateId = "excel-2.0.0",
                TemplateType = "Excel",
                Version = "2.0.0",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "TestUser",
                FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
            }
        };

        await _dbContext.Templates.AddRangeAsync(templates, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetAllTemplateVersionsAsync(
            "Excel",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(3);
        result[0].Version.ShouldBe("2.0.0");
        result[1].Version.ShouldBe("1.5.0");
        result[2].Version.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task GetAllTemplateVersionsAsync_WhenNoTemplates_ReturnsEmptyList()
    {
        // Arrange: Empty database

        // Act
        var result = await _repository.GetAllTemplateVersionsAsync(
            "Unknown",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllTemplateVersionsAsync_ReturnsActiveAndInactiveTemplates()
    {
        // Arrange: Mix of active and inactive
        var templates = new[]
        {
            new TemplateDefinition
            {
                TemplateId = "xml-1.0.0",
                TemplateType = "XML",
                Version = "1.0.0",
                IsActive = false,
                CreatedAt = DateTime.UtcNow.AddDays(-100),
                CreatedBy = "TestUser",
                FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
            },
            new TemplateDefinition
            {
                TemplateId = "xml-2.0.0",
                TemplateType = "XML",
                Version = "2.0.0",
                IsActive = false,
                CreatedAt = DateTime.UtcNow.AddDays(-50),
                CreatedBy = "TestUser",
                FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
            },
            new TemplateDefinition
            {
                TemplateId = "xml-3.0.0",
                TemplateType = "XML",
                Version = "3.0.0",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "TestUser",
                FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
            }
        };

        await _dbContext.Templates.AddRangeAsync(templates, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetAllTemplateVersionsAsync(
            "XML",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.Count.ShouldBe(3);
        result.Count(t => t.IsActive).ShouldBe(1);
        result.Count(t => !t.IsActive).ShouldBe(2);
    }

    //
    // SaveTemplateAsync Tests - IDENTICAL names to contract tests
    //

    [Fact]
    public async Task SaveTemplateAsync_WhenValidTemplate_ReturnsSuccess()
    {
        // Arrange: Valid template
        var validTemplate = new TemplateDefinition
        {
            TemplateId = "excel-1.0.0",
            TemplateType = "Excel",
            Version = "1.0.0",
            FieldMappings = new List<FieldMapping>
            {
                new FieldMapping("Expediente.NumeroExpediente", "A1", true)
            },
            EffectiveDate = DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "TestUser"
        };

        // Act
        var result = await _repository.SaveTemplateAsync(
            validTemplate,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsSuccess.ShouldBeTrue();

        // Verify it's actually in database
        var saved = await _dbContext.Templates.FirstOrDefaultAsync(t => t.TemplateId == "excel-1.0.0", TestContext.Current.CancellationToken);
        saved.ShouldNotBeNull();
    }

    [Fact]
    public async Task SaveTemplateAsync_WhenInvalidTemplate_ReturnsFailure()
    {
        // Arrange: Invalid template (no field mappings)
        var invalidTemplate = new TemplateDefinition
        {
            TemplateId = "excel-1.0.0",
            TemplateType = "Excel",
            Version = "1.0.0",
            FieldMappings = new List<FieldMapping>(), // Empty - invalid
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "TestUser"
        };

        // Act
        var result = await _repository.SaveTemplateAsync(
            invalidTemplate,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("FieldMappings");
    }

    [Fact]
    public async Task SaveTemplateAsync_WhenDatabaseError_ReturnsFailure()
    {
        // Arrange: This test simulates database constraint violation
        // First, create a valid template
        var template = new TemplateDefinition
        {
            TemplateId = "xml-1.0.0",
            TemplateType = "XML",
            Version = "1.0.0",
            FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "TestUser"
        };

        await _dbContext.Templates.AddAsync(template, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Try to save duplicate with same ID (should fail)
        var duplicate = new TemplateDefinition
        {
            TemplateId = "xml-1.0.0", // Same ID
            TemplateType = "XML",
            Version = "1.0.0",
            FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "TestUser"
        };

        // Act
        var result = await _repository.SaveTemplateAsync(
            duplicate,
            TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsFailure.ShouldBeTrue();
    }

    //
    // DeleteTemplateAsync Tests - IDENTICAL names to contract tests
    //

    [Fact]
    public async Task DeleteTemplateAsync_WhenInactiveTemplateExists_ReturnsSuccess()
    {
        // Arrange: Create inactive template
        var template = new TemplateDefinition
        {
            TemplateId = "excel-1.0.0",
            TemplateType = "Excel",
            Version = "1.0.0",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "TestUser",
            FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
        };

        await _dbContext.Templates.AddAsync(template, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.DeleteTemplateAsync(
            "Excel",
            "1.0.0",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteTemplateAsync_WhenActiveTemplate_ReturnsFailure()
    {
        // Arrange: Create active template
        var template = new TemplateDefinition
        {
            TemplateId = "xml-2.0.0",
            TemplateType = "XML",
            Version = "2.0.0",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "TestUser",
            FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
        };

        await _dbContext.Templates.AddAsync(template, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.DeleteTemplateAsync(
            "XML",
            "2.0.0",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("active");
    }

    [Fact]
    public async Task DeleteTemplateAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange: Empty database

        // Act
        var result = await _repository.DeleteTemplateAsync(
            "Invalid",
            "9.9.9",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("not found");
    }

    //
    // ActivateTemplateAsync Tests - IDENTICAL names to contract tests
    //

    [Fact]
    public async Task ActivateTemplateAsync_WhenTemplateExists_ReturnsSuccess()
    {
        // Arrange: Create template
        var template = new TemplateDefinition
        {
            TemplateId = "excel-2.0.0",
            TemplateType = "Excel",
            Version = "2.0.0",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "TestUser",
            FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
        };

        await _dbContext.Templates.AddAsync(template, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.ActivateTemplateAsync(
            "Excel",
            "2.0.0",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectation (Liskov!)
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ActivateTemplateAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange: Empty database

        // Act
        var result = await _repository.ActivateTemplateAsync(
            "PDF",
            "9.9.9",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("not found");
    }

    [Fact]
    public async Task ActivateTemplateAsync_DeactivatesOtherVersions()
    {
        // Arrange: Create multiple templates of same type
        var templates = new[]
        {
            new TemplateDefinition
            {
                TemplateId = "xml-1.0.0",
                TemplateType = "XML",
                Version = "1.0.0",
                IsActive = true, // Currently active
                CreatedAt = DateTime.UtcNow.AddDays(-100),
                CreatedBy = "TestUser",
                FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
            },
            new TemplateDefinition
            {
                TemplateId = "xml-3.0.0",
                TemplateType = "XML",
                Version = "3.0.0",
                IsActive = false, // Will be activated
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "TestUser",
                FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
            }
        };

        await _dbContext.Templates.AddRangeAsync(templates, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act: Activate version 3.0.0
        var activateResult = await _repository.ActivateTemplateAsync(
            "XML",
            "3.0.0",
            TestContext.Current.CancellationToken);

        // Get latest template
        var latestTemplate = await _repository.GetLatestTemplateAsync(
            "XML",
            TestContext.Current.CancellationToken);

        // Assert: SAME expectations (Liskov!)
        activateResult.IsSuccess.ShouldBeTrue();
        latestTemplate.ShouldNotBeNull();
        latestTemplate.Version.ShouldBe("3.0.0");
        latestTemplate.IsActive.ShouldBeTrue();

        // Verify old version is deactivated
        var oldTemplate = await _dbContext.Templates.FirstOrDefaultAsync(t => t.Version == "1.0.0", TestContext.Current.CancellationToken);
        oldTemplate.ShouldNotBeNull();
        oldTemplate!.IsActive.ShouldBeFalse();
    }
}
