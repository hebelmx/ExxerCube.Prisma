using Microsoft.EntityFrameworkCore;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive.Data;
using ExxerCube.Prisma.Testing.Contracts;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

/// <summary>
/// Implementation instance of <see cref="AdaptiveExporterContract"/> for
/// <see cref="AdaptiveExporter"/> (ADR-005), using the <c>CreateSut()</c> fallback with a
/// per-test EF Core InMemory fixture, a real <see cref="TemplateRepository"/>, and a real
/// <see cref="TemplateFieldMapper"/>.
/// </summary>
/// <remarks>
/// Supersedes <c>AdaptiveExporterTests</c>: its 17 zero-drift bodies were lifted into the
/// contract base and run here through inheritance. Implementation-specific tests remain in
/// <c>AdaptiveExporterCachingTests</c>, <c>AdaptiveExporterErrorPathTests</c>, and
/// <c>AdaptiveExporterMutationTests</c> (never merged into a contract).
/// </remarks>
public sealed class AdaptiveExporterContractTests : AdaptiveExporterContract, IDisposable
{
    private readonly TemplateDbContext _dbContext;
    private readonly ITemplateRepository _templateRepository;
    private readonly IAdaptiveExporter _exporter;

    /// <summary>
    /// Initializes the implementation instance with real EF InMemory-backed dependencies.
    /// </summary>
    /// <param name="output">xUnit test output sink for the loggers.</param>
    public AdaptiveExporterContractTests(ITestOutputHelper output)
    {
        var options = new DbContextOptionsBuilder<TemplateDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new TemplateDbContext(options);
        _templateRepository = new TemplateRepository(_dbContext, XUnitLogger.CreateLogger<TemplateRepository>(output));
        var fieldMapper = new TemplateFieldMapper(XUnitLogger.CreateLogger<TemplateFieldMapper>(output));
        _exporter = new AdaptiveExporter(_templateRepository, fieldMapper, XUnitLogger.CreateLogger<AdaptiveExporter>(output));
    }

    /// <inheritdoc />
    protected override IAdaptiveExporter CreateSut() => _exporter;

    /// <inheritdoc />
    protected override Task SeedTemplateAsync(TemplateDefinition template, CancellationToken cancellationToken)
        => _templateRepository.SaveTemplateAsync(template, cancellationToken);

    /// <summary>Disposes the per-test database context.</summary>
    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
