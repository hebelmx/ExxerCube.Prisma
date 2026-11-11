namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Implementation instance of <see cref="ManualReviewerPanelContract"/> for
/// <see cref="ManualReviewerService"/> (ADR-005), using the <c>CreateSut()</c> fallback with a per-test
/// EF Core InMemory fixture (ADR-005 §3 Example C).
/// </summary>
/// <remarks>
/// Runs the shared contract behaviours against the real DB-backed service. Implementation-specific
/// richness that needs direct fixture seeding — arbitrary status/confidence filtering, pagination
/// counts, status-mapping verification, duplicate-decision prevention, the notes-required-on-override
/// rule, and field-annotation retrieval — remains in <see cref="ManualReviewerServiceTests"/> alongside
/// this instance. <c>ManualReviewerService</c> is excluded from mutation testing (EF/DB I/O), so there
/// is no kill-power regression to guard here.
/// </remarks>
public sealed class ManualReviewerServiceContractTests : ManualReviewerPanelContract, IDisposable
{
    private readonly PrismaDbContext _dbContext;
    private readonly ITestOutputHelper _output;

    /// <summary>Initializes the implementation instance with a fresh in-memory database.</summary>
    /// <param name="output">xUnit test output sink for the service's logger.</param>
    public ManualReviewerServiceContractTests(ITestOutputHelper output)
    {
        var options = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(options);
        _dbContext.Database.EnsureCreated();
        _output = output;
    }

    /// <inheritdoc />
    protected override IManualReviewerPanel CreateSut()
        => new ManualReviewerService(_dbContext, XUnitLogger.CreateLogger<ManualReviewerService>(_output));

    /// <summary>Disposes the per-test database context.</summary>
    public void Dispose() => _dbContext.Dispose();
}
