using System;
using System.Linq;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="ITemplateRepository"/> — every implementation
/// (and the mock blueprint) must pass these tests unchanged (ADR-005).
/// </summary>
/// <remarks>
/// <para>
/// Uses the sanctioned ADR-005 §3 <c>CreateSut()</c> fallback: a real implementation owns
/// a per-test persistence fixture (EF InMemory context), while the blueprint owns an
/// in-memory reference store. Method names are preserved from the zero-drift
/// <c>TemplateRepositoryTests</c> twin (master plan §2).
/// </para>
/// <para>
/// <strong>Seeding/verification go through the interface</strong> (<see cref="ITemplateRepository.SaveTemplateAsync"/>,
/// <see cref="ITemplateRepository.GetTemplateAsync"/>) rather than the twin's
/// EF-<c>DbContext</c>-direct calls, because the contract must be implementation-agnostic —
/// a mock blueprint has no DbContext. The behavior asserted is identical. (This repository
/// is intentionally excluded from mutation testing — EF/DB I/O — so verbatim body
/// preservation is not a kill-power concern here; see mutation-testing.md §2.2.)
/// </para>
/// </remarks>
public abstract class TemplateRepositoryContract
{
    /// <summary>
    /// Creates the implementation under test. Called once per test; the implementation
    /// owns its fixture lifetime.
    /// </summary>
    /// <returns>The <see cref="ITemplateRepository"/> implementation to verify.</returns>
    protected abstract ITemplateRepository CreateSut();

    //
    // GetTemplateAsync Tests
    //

    /// <summary>Contract: an existing template is returned with its field mappings.</summary>
    [Fact]
    public async Task GetTemplateAsync_WhenTemplateExists_ReturnsTemplateDefinition()
    {
        // Arrange: seed a template through the contract
        var repository = CreateSut();
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

        (await repository.SaveTemplateAsync(template, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        // Act
        var result = await repository.GetTemplateAsync(
            "Excel",
            "1.0.0",
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.TemplateType.ShouldBe("Excel");
        result.Version.ShouldBe("1.0.0");
        result.FieldMappings.ShouldNotBeEmpty();
    }

    /// <summary>Contract: a missing template returns null (not a throw).</summary>
    [Fact]
    public async Task GetTemplateAsync_WhenTemplateNotFound_ReturnsNull()
    {
        // Arrange: empty repository
        var repository = CreateSut();

        // Act
        var result = await repository.GetTemplateAsync(
            "Invalid",
            "9.9.9",
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>Contract: GetTemplateAsync returns inactive templates too (no IsActive filter).</summary>
    [Fact]
    public async Task GetTemplateAsync_WhenInactiveTemplateExists_ReturnsTemplate()
    {
        // Arrange: seed an inactive template
        var repository = CreateSut();
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

        (await repository.SaveTemplateAsync(template, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        // Act
        var result = await repository.GetTemplateAsync(
            "XML",
            "2.0.0",
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.IsActive.ShouldBeFalse();
    }

    //
    // GetLatestTemplateAsync Tests
    //

    /// <summary>Contract: the latest active template for a type is returned.</summary>
    [Fact]
    public async Task GetLatestTemplateAsync_WhenActiveTemplateExists_ReturnsLatestVersion()
    {
        // Arrange: seed an active template
        var repository = CreateSut();
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

        (await repository.SaveTemplateAsync(template, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        // Act
        var result = await repository.GetLatestTemplateAsync(
            "Excel",
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.TemplateType.ShouldBe("Excel");
        result.Version.ShouldBe("2.5.0");
        result.IsActive.ShouldBeTrue();
    }

    /// <summary>Contract: when no active template exists, null is returned.</summary>
    [Fact]
    public async Task GetLatestTemplateAsync_WhenNoActiveTemplates_ReturnsNull()
    {
        // Arrange: seed only an inactive template
        var repository = CreateSut();
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

        (await repository.SaveTemplateAsync(template, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        // Act
        var result = await repository.GetLatestTemplateAsync(
            "PDF",
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>Contract: among multiple active versions, the most recently effective is returned.</summary>
    [Fact]
    public async Task GetLatestTemplateAsync_WhenMultipleActiveVersions_ReturnsHighestVersion()
    {
        // Arrange: seed multiple active versions
        var repository = CreateSut();
        var older = new TemplateDefinition
        {
            TemplateId = "xml-1.0.0",
            TemplateType = "XML",
            Version = "1.0.0",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-100),
            CreatedAt = DateTime.UtcNow.AddDays(-100),
            CreatedBy = "TestUser",
            FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
        };
        var newer = new TemplateDefinition
        {
            TemplateId = "xml-10.0.1",
            TemplateType = "XML",
            Version = "10.0.1", // Highest version
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-50),
            CreatedAt = DateTime.UtcNow.AddDays(-50),
            CreatedBy = "TestUser",
            FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
        };

        (await repository.SaveTemplateAsync(older, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await repository.SaveTemplateAsync(newer, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        // Act
        var result = await repository.GetLatestTemplateAsync(
            "XML",
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Version.ShouldBe("10.0.1");
    }

    //
    // GetAllTemplateVersionsAsync Tests
    //

    /// <summary>Contract: all versions are returned ordered by version descending.</summary>
    [Fact]
    public async Task GetAllTemplateVersionsAsync_WhenTemplatesExist_ReturnsAllVersionsDescending()
    {
        // Arrange: seed multiple versions
        var repository = CreateSut();
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

        foreach (var t in templates)
        {
            (await repository.SaveTemplateAsync(t, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        }

        // Act
        var result = await repository.GetAllTemplateVersionsAsync(
            "Excel",
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(3);
        result[0].Version.ShouldBe("2.0.0");
        result[1].Version.ShouldBe("1.5.0");
        result[2].Version.ShouldBe("1.0.0");
    }

    /// <summary>Contract: an unknown type returns an empty (never null) list.</summary>
    [Fact]
    public async Task GetAllTemplateVersionsAsync_WhenNoTemplates_ReturnsEmptyList()
    {
        // Arrange: empty repository
        var repository = CreateSut();

        // Act
        var result = await repository.GetAllTemplateVersionsAsync(
            "Unknown",
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    /// <summary>Contract: GetAll returns both active and inactive templates.</summary>
    [Fact]
    public async Task GetAllTemplateVersionsAsync_ReturnsActiveAndInactiveTemplates()
    {
        // Arrange: seed a mix of active and inactive
        var repository = CreateSut();
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

        foreach (var t in templates)
        {
            (await repository.SaveTemplateAsync(t, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        }

        // Act
        var result = await repository.GetAllTemplateVersionsAsync(
            "XML",
            TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(3);
        result.Count(t => t.IsActive).ShouldBe(1);
        result.Count(t => !t.IsActive).ShouldBe(2);
    }

    //
    // SaveTemplateAsync Tests
    //

    /// <summary>Contract: a valid template saves and is retrievable.</summary>
    [Fact]
    public async Task SaveTemplateAsync_WhenValidTemplate_ReturnsSuccess()
    {
        // Arrange: valid template
        var repository = CreateSut();
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
        var result = await repository.SaveTemplateAsync(
            validTemplate,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Verify it's actually retrievable
        var saved = await repository.GetTemplateAsync("Excel", "1.0.0", TestContext.Current.CancellationToken);
        saved.ShouldNotBeNull();
    }

    /// <summary>Contract: a template with no field mappings is rejected.</summary>
    [Fact]
    public async Task SaveTemplateAsync_WhenInvalidTemplate_ReturnsFailure()
    {
        // Arrange: invalid template (no field mappings)
        var repository = CreateSut();
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
        var result = await repository.SaveTemplateAsync(
            invalidTemplate,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("FieldMappings");
    }

    /// <summary>Contract: saving a duplicate (same id / type+version) fails.</summary>
    [Fact]
    public async Task SaveTemplateAsync_WhenDatabaseError_ReturnsFailure()
    {
        // Arrange: first, save a valid template
        var repository = CreateSut();
        var template = new TemplateDefinition
        {
            TemplateId = "xml-1.0.0",
            TemplateType = "XML",
            Version = "1.0.0",
            FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "TestUser"
        };

        (await repository.SaveTemplateAsync(template, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

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
        var result = await repository.SaveTemplateAsync(
            duplicate,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    //
    // DeleteTemplateAsync Tests
    //

    /// <summary>Contract: an inactive template can be deleted.</summary>
    [Fact]
    public async Task DeleteTemplateAsync_WhenInactiveTemplateExists_ReturnsSuccess()
    {
        // Arrange: seed an inactive template
        var repository = CreateSut();
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

        (await repository.SaveTemplateAsync(template, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        // Act
        var result = await repository.DeleteTemplateAsync(
            "Excel",
            "1.0.0",
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>Contract: an active template cannot be deleted.</summary>
    [Fact]
    public async Task DeleteTemplateAsync_WhenActiveTemplate_ReturnsFailure()
    {
        // Arrange: seed an active template
        var repository = CreateSut();
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

        (await repository.SaveTemplateAsync(template, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        // Act
        var result = await repository.DeleteTemplateAsync(
            "XML",
            "2.0.0",
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("active");
    }

    /// <summary>Contract: deleting a missing template fails.</summary>
    [Fact]
    public async Task DeleteTemplateAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange: empty repository
        var repository = CreateSut();

        // Act
        var result = await repository.DeleteTemplateAsync(
            "Invalid",
            "9.9.9",
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("not found");
    }

    //
    // ActivateTemplateAsync Tests
    //

    /// <summary>Contract: activating an existing template succeeds.</summary>
    [Fact]
    public async Task ActivateTemplateAsync_WhenTemplateExists_ReturnsSuccess()
    {
        // Arrange: seed a template
        var repository = CreateSut();
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

        (await repository.SaveTemplateAsync(template, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        // Act
        var result = await repository.ActivateTemplateAsync(
            "Excel",
            "2.0.0",
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>Contract: activating a missing template fails.</summary>
    [Fact]
    public async Task ActivateTemplateAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange: empty repository
        var repository = CreateSut();

        // Act
        var result = await repository.ActivateTemplateAsync(
            "PDF",
            "9.9.9",
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("not found");
    }

    /// <summary>Contract: activating one version deactivates the others of the same type.</summary>
    [Fact]
    public async Task ActivateTemplateAsync_DeactivatesOtherVersions()
    {
        // Arrange: seed multiple templates of the same type
        var repository = CreateSut();
        var currentlyActive = new TemplateDefinition
        {
            TemplateId = "xml-1.0.0",
            TemplateType = "XML",
            Version = "1.0.0",
            IsActive = true, // Currently active
            CreatedAt = DateTime.UtcNow.AddDays(-100),
            CreatedBy = "TestUser",
            FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
        };
        var toActivate = new TemplateDefinition
        {
            TemplateId = "xml-3.0.0",
            TemplateType = "XML",
            Version = "3.0.0",
            IsActive = false, // Will be activated
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "TestUser",
            FieldMappings = new List<FieldMapping> { new FieldMapping("Test", "Test", true) }
        };

        (await repository.SaveTemplateAsync(currentlyActive, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await repository.SaveTemplateAsync(toActivate, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        // Act: Activate version 3.0.0
        var activateResult = await repository.ActivateTemplateAsync(
            "XML",
            "3.0.0",
            TestContext.Current.CancellationToken);

        // Get latest template
        var latestTemplate = await repository.GetLatestTemplateAsync(
            "XML",
            TestContext.Current.CancellationToken);

        // Assert
        activateResult.IsSuccess.ShouldBeTrue();
        latestTemplate.ShouldNotBeNull();
        latestTemplate.Version.ShouldBe("3.0.0");
        latestTemplate.IsActive.ShouldBeTrue();

        // Verify old version is deactivated
        var oldTemplate = await repository.GetTemplateAsync("XML", "1.0.0", TestContext.Current.CancellationToken);
        oldTemplate.ShouldNotBeNull();
        oldTemplate.IsActive.ShouldBeFalse();
    }
}
