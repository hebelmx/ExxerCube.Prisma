using Microsoft.EntityFrameworkCore;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive.Data;
using ExxerCube.Prisma.Testing.Contracts;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

/// <summary>
/// Implementation instance of <see cref="TemplateRepositoryContract"/> for
/// <see cref="TemplateRepository"/> (ADR-005), using the <c>CreateSut()</c> fallback with
/// a per-test EF Core InMemory fixture (ADR-005 §3 Example C).
/// </summary>
/// <remarks>
/// Supersedes <c>TemplateRepositoryTests</c>: its 18 zero-drift test bodies were lifted
/// into the contract base (seeding/verification re-expressed through the interface so the
/// contract is implementation-agnostic). This repository is excluded from mutation testing
/// (EF/DB I/O), so there is no kill-power regression to guard here.
/// </remarks>
public sealed class TemplateRepositoryContractTests : TemplateRepositoryContract, IDisposable
{
    private readonly TemplateDbContext _dbContext;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes the implementation instance with a fresh in-memory database.
    /// </summary>
    /// <param name="output">xUnit test output sink for the repository's logger.</param>
    public TemplateRepositoryContractTests(ITestOutputHelper output)
    {
        var options = new DbContextOptionsBuilder<TemplateDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TemplateDbContext(options);
        _output = output;
    }

    /// <inheritdoc />
    protected override ITemplateRepository CreateSut()
        => new TemplateRepository(_dbContext, XUnitLogger.CreateLogger<TemplateRepository>(_output));

    /// <summary>Disposes the per-test database context.</summary>
    public void Dispose() => _dbContext.Dispose();
}
