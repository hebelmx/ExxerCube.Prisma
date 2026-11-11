using Microsoft.EntityFrameworkCore;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive.Data;
using ExxerCube.Prisma.Testing.Contracts;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

/// <summary>
/// Implementation instance of <see cref="SchemaEvolutionDetectorContract"/> for
/// <see cref="SchemaEvolutionDetector"/> (ADR-005), using the <c>CreateSut()</c> fallback
/// with a per-test EF Core InMemory fixture and a real <see cref="TemplateRepository"/>.
/// </summary>
/// <remarks>
/// Supersedes <c>SchemaEvolutionDetectorTests</c>: its 15 shared bodies + the promoted
/// boundary case live in the contract base and run here through inheritance. The two
/// "Additional Real-World Scenarios" tests (implementation-specific richness — recursive
/// nested detection and exact <c>dataType</c> strings) remain here, alongside the contract.
/// Implementation-pinning tests remain in <c>SchemaEvolutionDetectorMutationTests</c>.
/// </remarks>
public sealed class SchemaEvolutionDetectorContractTests : SchemaEvolutionDetectorContract, IDisposable
{
    private readonly TemplateDbContext _dbContext;
    private readonly ITemplateRepository _templateRepository;
    private readonly ISchemaEvolutionDetector _detector;

    /// <summary>
    /// Initializes the implementation instance with real EF InMemory-backed dependencies.
    /// </summary>
    /// <param name="output">xUnit test output sink for the loggers.</param>
    public SchemaEvolutionDetectorContractTests(ITestOutputHelper output)
    {
        var options = new DbContextOptionsBuilder<TemplateDbContext>()
            .UseInMemoryDatabase(databaseName: $"SchemaDetectorTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new TemplateDbContext(options);
        _templateRepository = new TemplateRepository(_dbContext, XUnitLogger.CreateLogger<TemplateRepository>(output));
        _detector = new SchemaEvolutionDetector(_templateRepository, XUnitLogger.CreateLogger<SchemaEvolutionDetector>(output));
    }

    /// <inheritdoc />
    protected override ISchemaEvolutionDetector CreateSut() => _detector;

    /// <inheritdoc />
    protected override Task SeedActiveTemplateAsync(TemplateDefinition template, CancellationToken cancellationToken)
        => _templateRepository.SaveTemplateAsync(template, cancellationToken);

    /// <summary>Disposes the per-test database context.</summary>
    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    //
    // Additional Real-World Scenarios — implementation-specific (kept alongside the contract)
    //

    /// <summary>Implementation: recursive nested-object field detection (richness beyond the bare contract).</summary>
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

    /// <summary>Implementation: exact data-type strings for a complex object's suggested mappings.</summary>
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
}
