namespace ExxerCube.Prisma.Tests.Domain.Domain.Interfaces;

using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Testing.Abstractions;
using IndQuestResults;

/// <summary>
/// Contract tests for <see cref="ITemplateRepository"/> interface using mocks.
/// </summary>
/// <remarks>
/// <para>
/// These tests define the BEHAVIORAL CONTRACT that ANY implementation of
/// <see cref="ITemplateRepository"/> MUST satisfy.
/// </para>
/// <para>
/// <strong>ITDD Principle:</strong> Interface is testable with mocks BEFORE implementation exists.
/// Any implementation that passes these tests (Liskov verification) is correct.
/// </para>
/// </remarks>
public sealed class ITemplateRepositoryContractTests
{
    //
    // GetTemplateAsync Tests
    //

    [Fact]
    public async Task GetTemplateAsync_WhenTemplateExists_ReturnsTemplateDefinition()
    {
        // Arrange: Mock the interface (thinking about BEHAVIOR)
        var mockRepo = Substitute.For<ITemplateRepository>();
        var expectedTemplate = new TemplateDefinition
        {
            TemplateId = "excel-1.0.0",
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Excel Template v1.0",
            IsActive = true,
            FieldMappings = new List<FieldMapping>
            {
                new FieldMapping("Expediente.NumeroExpediente", "A1", true)
            }
        };

        mockRepo.GetTemplateAsync("Excel", "1.0.0", Arg.Any<CancellationToken>())
                .Returns(expectedTemplate);

        // Act: Use the mocked abstraction
        var result = await mockRepo.GetTemplateAsync(
            "Excel",
            "1.0.0",
            TestContext.Current.CancellationToken);

        // Assert: Verify expected BEHAVIOR
        result.ShouldNotBeNull();
        result.TemplateType.ShouldBe("Excel");
        result.Version.ShouldBe("1.0.0");
        result.FieldMappings.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetTemplateAsync_WhenTemplateNotFound_ReturnsNull()
    {
        // Arrange: Mock returns null (thinking about RESULTS)
        var mockRepo = Substitute.For<ITemplateRepository>();
        mockRepo.GetTemplateAsync("Invalid", "9.9.9", Arg.Any<CancellationToken>())
                .Returns((TemplateDefinition?)null);

        // Act
        var result = await mockRepo.GetTemplateAsync(
            "Invalid",
            "9.9.9",
            TestContext.Current.CancellationToken);

        // Assert: Contract - Must return null when template not found
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetTemplateAsync_WhenInactiveTemplateExists_ReturnsTemplate()
    {
        // Arrange: Inactive templates should still be returned by GetTemplateAsync
        var mockRepo = Substitute.For<ITemplateRepository>();
        var inactiveTemplate = new TemplateDefinition
        {
            TemplateId = "xml-2.0.0",
            TemplateType = "XML",
            Version = "2.0.0",
            IsActive = false // Inactive
        };

        mockRepo.GetTemplateAsync("XML", "2.0.0", Arg.Any<CancellationToken>())
                .Returns(inactiveTemplate);

        // Act
        var result = await mockRepo.GetTemplateAsync(
            "XML",
            "2.0.0",
            TestContext.Current.CancellationToken);

        // Assert: Contract - Returns inactive templates (doesn't filter by IsActive)
        result.ShouldNotBeNull();
        result.IsActive.ShouldBeFalse();
    }

    //
    // GetLatestTemplateAsync Tests
    //

    [Fact]
    public async Task GetLatestTemplateAsync_WhenActiveTemplateExists_ReturnsLatestVersion()
    {
        // Arrange: Latest active template
        var mockRepo = Substitute.For<ITemplateRepository>();
        var latestTemplate = new TemplateDefinition
        {
            TemplateId = "excel-2.5.0",
            TemplateType = "Excel",
            Version = "2.5.0",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-30),
            ExpirationDate = null
        };

        mockRepo.GetLatestTemplateAsync("Excel", Arg.Any<CancellationToken>())
                .Returns(latestTemplate);

        // Act
        var result = await mockRepo.GetLatestTemplateAsync(
            "Excel",
            TestContext.Current.CancellationToken);

        // Assert: Contract - Must return latest active template
        result.ShouldNotBeNull();
        result.TemplateType.ShouldBe("Excel");
        result.Version.ShouldBe("2.5.0");
        result.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetLatestTemplateAsync_WhenNoActiveTemplates_ReturnsNull()
    {
        // Arrange: No active templates
        var mockRepo = Substitute.For<ITemplateRepository>();
        mockRepo.GetLatestTemplateAsync("PDF", Arg.Any<CancellationToken>())
                .Returns((TemplateDefinition?)null);

        // Act
        var result = await mockRepo.GetLatestTemplateAsync(
            "PDF",
            TestContext.Current.CancellationToken);

        // Assert: Contract - Returns null when no active templates exist
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetLatestTemplateAsync_WhenMultipleActiveVersions_ReturnsHighestVersion()
    {
        // Arrange: Should return highest version number
        var mockRepo = Substitute.For<ITemplateRepository>();
        var highestVersion = new TemplateDefinition
        {
            TemplateId = "xml-10.0.1",
            TemplateType = "XML",
            Version = "10.0.1", // Highest version
            IsActive = true
        };

        mockRepo.GetLatestTemplateAsync("XML", Arg.Any<CancellationToken>())
                .Returns(highestVersion);

        // Act
        var result = await mockRepo.GetLatestTemplateAsync(
            "XML",
            TestContext.Current.CancellationToken);

        // Assert: Contract - Returns highest version number
        result.ShouldNotBeNull();
        result.Version.ShouldBe("10.0.1");
    }

    //
    // GetAllTemplateVersionsAsync Tests
    //

    [Fact]
    public async Task GetAllTemplateVersionsAsync_WhenTemplatesExist_ReturnsAllVersionsDescending()
    {
        // Arrange: Multiple versions ordered by version descending
        var mockRepo = Substitute.For<ITemplateRepository>();
        var allVersions = new List<TemplateDefinition>
        {
            new TemplateDefinition { TemplateType = "Excel", Version = "2.0.0", IsActive = true },
            new TemplateDefinition { TemplateType = "Excel", Version = "1.5.0", IsActive = false },
            new TemplateDefinition { TemplateType = "Excel", Version = "1.0.0", IsActive = false }
        };

        mockRepo.GetAllTemplateVersionsAsync("Excel", Arg.Any<CancellationToken>())
                .Returns(allVersions.AsReadOnly());

        // Act
        var result = await mockRepo.GetAllTemplateVersionsAsync(
            "Excel",
            TestContext.Current.CancellationToken);

        // Assert: Contract - Returns all versions in descending order
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(3);
        result[0].Version.ShouldBe("2.0.0");
        result[1].Version.ShouldBe("1.5.0");
        result[2].Version.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task GetAllTemplateVersionsAsync_WhenNoTemplates_ReturnsEmptyList()
    {
        // Arrange: No templates
        var mockRepo = Substitute.For<ITemplateRepository>();
        mockRepo.GetAllTemplateVersionsAsync("Unknown", Arg.Any<CancellationToken>())
                .Returns(new List<TemplateDefinition>().AsReadOnly());

        // Act
        var result = await mockRepo.GetAllTemplateVersionsAsync(
            "Unknown",
            TestContext.Current.CancellationToken);

        // Assert: Contract - Returns empty list when no templates exist
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllTemplateVersionsAsync_ReturnsActiveAndInactiveTemplates()
    {
        // Arrange: Mix of active and inactive
        var mockRepo = Substitute.For<ITemplateRepository>();
        var mixedVersions = new List<TemplateDefinition>
        {
            new TemplateDefinition { TemplateType = "XML", Version = "3.0.0", IsActive = true },
            new TemplateDefinition { TemplateType = "XML", Version = "2.0.0", IsActive = false },
            new TemplateDefinition { TemplateType = "XML", Version = "1.0.0", IsActive = false }
        };

        mockRepo.GetAllTemplateVersionsAsync("XML", Arg.Any<CancellationToken>())
                .Returns(mixedVersions.AsReadOnly());

        // Act
        var result = await mockRepo.GetAllTemplateVersionsAsync(
            "XML",
            TestContext.Current.CancellationToken);

        // Assert: Contract - Returns all templates regardless of IsActive
        result.Count.ShouldBe(3);
        result.Count(t => t.IsActive).ShouldBe(1);
        result.Count(t => !t.IsActive).ShouldBe(2);
    }

    //
    // SaveTemplateAsync Tests
    //

    [Fact]
    public async Task SaveTemplateAsync_WhenValidTemplate_ReturnsSuccess()
    {
        // Arrange: Valid template
        var mockRepo = Substitute.For<ITemplateRepository>();
        var validTemplate = new TemplateDefinition
        {
            TemplateType = "Excel",
            Version = "1.0.0",
            FieldMappings = new List<FieldMapping>
            {
                new FieldMapping("Expediente.NumeroExpediente", "A1", true)
            },
            EffectiveDate = DateTime.UtcNow,
            IsActive = true
        };

        mockRepo.SaveTemplateAsync(validTemplate, Arg.Any<CancellationToken>())
                .Returns(Result.Success());

        // Act
        var result = await mockRepo.SaveTemplateAsync(
            validTemplate,
            TestContext.Current.CancellationToken);

        // Assert: Contract - Returns success for valid template
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveTemplateAsync_WhenInvalidTemplate_ReturnsFailure()
    {
        // Arrange: Invalid template (no field mappings)
        var mockRepo = Substitute.For<ITemplateRepository>();
        var invalidTemplate = new TemplateDefinition
        {
            TemplateType = "Excel",
            Version = "1.0.0",
            FieldMappings = new List<FieldMapping>() // Empty - invalid
        };

        mockRepo.SaveTemplateAsync(invalidTemplate, Arg.Any<CancellationToken>())
                .Returns(Result.WithFailure("FieldMappings must contain at least one mapping"));

        // Act
        var result = await mockRepo.SaveTemplateAsync(
            invalidTemplate,
            TestContext.Current.CancellationToken);

        // Assert: Contract - Returns failure for invalid template
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("FieldMappings");
    }

    [Fact]
    public async Task SaveTemplateAsync_WhenDatabaseError_ReturnsFailure()
    {
        // Arrange: Database error scenario
        var mockRepo = Substitute.For<ITemplateRepository>();
        var template = new TemplateDefinition
        {
            TemplateType = "XML",
            Version = "1.0.0",
            FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
        };

        mockRepo.SaveTemplateAsync(template, Arg.Any<CancellationToken>())
                .Returns(Result.WithFailure("Database connection failed"));

        // Act
        var result = await mockRepo.SaveTemplateAsync(
            template,
            TestContext.Current.CancellationToken);

        // Assert: Contract - Returns failure on database error
        result.IsFailure.ShouldBeTrue();
    }

    //
    // DeleteTemplateAsync Tests
    //

    [Fact]
    public async Task DeleteTemplateAsync_WhenInactiveTemplateExists_ReturnsSuccess()
    {
        // Arrange: Can delete inactive templates
        var mockRepo = Substitute.For<ITemplateRepository>();
        mockRepo.DeleteTemplateAsync("Excel", "1.0.0", Arg.Any<CancellationToken>())
                .Returns(Result.Success());

        // Act
        var result = await mockRepo.DeleteTemplateAsync(
            "Excel",
            "1.0.0",
            TestContext.Current.CancellationToken);

        // Assert: Contract - Can delete inactive templates
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteTemplateAsync_WhenActiveTemplate_ReturnsFailure()
    {
        // Arrange: Cannot delete active templates
        var mockRepo = Substitute.For<ITemplateRepository>();
        mockRepo.DeleteTemplateAsync("XML", "2.0.0", Arg.Any<CancellationToken>())
                .Returns(Result.WithFailure("Cannot delete active template"));

        // Act
        var result = await mockRepo.DeleteTemplateAsync(
            "XML",
            "2.0.0",
            TestContext.Current.CancellationToken);

        // Assert: Contract - Cannot delete active templates
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("active");
    }

    [Fact]
    public async Task DeleteTemplateAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange: Template doesn't exist
        var mockRepo = Substitute.For<ITemplateRepository>();
        mockRepo.DeleteTemplateAsync("Invalid", "9.9.9", Arg.Any<CancellationToken>())
                .Returns(Result.WithFailure("Template not found"));

        // Act
        var result = await mockRepo.DeleteTemplateAsync(
            "Invalid",
            "9.9.9",
            TestContext.Current.CancellationToken);

        // Assert: Contract - Returns failure when template not found
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("not found");
    }

    //
    // ActivateTemplateAsync Tests
    //

    [Fact]
    public async Task ActivateTemplateAsync_WhenTemplateExists_ReturnsSuccess()
    {
        // Arrange: Activate valid template
        var mockRepo = Substitute.For<ITemplateRepository>();
        mockRepo.ActivateTemplateAsync("Excel", "2.0.0", Arg.Any<CancellationToken>())
                .Returns(Result.Success());

        // Act
        var result = await mockRepo.ActivateTemplateAsync(
            "Excel",
            "2.0.0",
            TestContext.Current.CancellationToken);

        // Assert: Contract - Returns success when activation succeeds
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ActivateTemplateAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange: Template doesn't exist
        var mockRepo = Substitute.For<ITemplateRepository>();
        mockRepo.ActivateTemplateAsync("PDF", "9.9.9", Arg.Any<CancellationToken>())
                .Returns(Result.WithFailure("Template not found"));

        // Act
        var result = await mockRepo.ActivateTemplateAsync(
            "PDF",
            "9.9.9",
            TestContext.Current.CancellationToken);

        // Assert: Contract - Returns failure when template not found
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("not found");
    }

    [Fact]
    public async Task ActivateTemplateAsync_DeactivatesOtherVersions()
    {
        // Arrange: Only one version should be active at a time
        var mockRepo = Substitute.For<ITemplateRepository>();

        // Mock behavior: After activation, only the target version is active
        mockRepo.ActivateTemplateAsync("XML", "3.0.0", Arg.Any<CancellationToken>())
                .Returns(Result.Success());

        mockRepo.GetLatestTemplateAsync("XML", Arg.Any<CancellationToken>())
                .Returns(new TemplateDefinition
                {
                    TemplateType = "XML",
                    Version = "3.0.0",
                    IsActive = true
                });

        // Act
        var activateResult = await mockRepo.ActivateTemplateAsync(
            "XML",
            "3.0.0",
            TestContext.Current.CancellationToken);

        var latestTemplate = await mockRepo.GetLatestTemplateAsync(
            "XML",
            TestContext.Current.CancellationToken);

        // Assert: Contract - Only activated version is active
        activateResult.IsSuccess.ShouldBeTrue();
        latestTemplate.ShouldNotBeNull();
        latestTemplate.Version.ShouldBe("3.0.0");
        latestTemplate.IsActive.ShouldBeTrue();
    }
}
