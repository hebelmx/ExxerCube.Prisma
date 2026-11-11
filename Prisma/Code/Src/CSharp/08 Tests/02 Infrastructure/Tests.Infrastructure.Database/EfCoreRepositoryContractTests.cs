using System.Linq.Expressions;

namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Implementation instance of <see cref="RepositoryContract{T, TId}"/> for
/// <see cref="EfCoreRepository{T, TId}"/> (ADR-005), using the <c>CreateSut()</c> fallback with a
/// per-test EF Core InMemory fixture (ADR-005 §3 Example C).
/// </summary>
/// <remarks>
/// <strong>Phase 5 (severe drift): this class is net-new real-SUT coverage.</strong> Before it, only
/// the impl-specific error/validation paths in <c>EfCoreRepositoryTests</c> exercised the production
/// repository (~28% of the documented contract — master plan §2). The 21 inherited query/command
/// behaviours below run against the real <c>EfCoreRepository</c> for the first time. The entity used is
/// <see cref="FileMetadata"/> (string key) — the same entity the pre-existing twin uses. That twin
/// (<c>EfCoreRepositoryTests</c>) is <strong>not</strong> superseded; it keeps its implementation-specific
/// tests (null-guard messages, disposed-context failure) alongside this contract instance.
/// </remarks>
public sealed class EfCoreRepositoryContractTests : RepositoryContract<FileMetadata, string>, IDisposable
{
    private readonly PrismaDbContext _dbContext;

    /// <summary>Initializes the implementation instance with a fresh in-memory database.</summary>
    public EfCoreRepositoryContractTests()
    {
        var options = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    /// <inheritdoc />
    protected override IRepository<FileMetadata, string> CreateSut()
        => new EfCoreRepository<FileMetadata, string>(_dbContext);

    /// <inheritdoc />
    protected override FileMetadata CreateMatchingEntity() => NewMetadata(fileSize: 1024);

    /// <inheritdoc />
    protected override FileMetadata CreateNonMatchingEntity() => NewMetadata(fileSize: 0);

    /// <inheritdoc />
    protected override string IdOf(FileMetadata entity) => entity.FileId;

    /// <inheritdoc />
    protected override Expression<Func<FileMetadata, bool>> MatchingPredicate() => f => f.FileSize > 0;

    /// <inheritdoc />
    protected override Expression<Func<FileMetadata, bool>> NeverMatchingPredicate() => f => f.FileSize < 0;

    /// <inheritdoc />
    protected override Expression<Func<FileMetadata, string>> NameSelector() => f => f.FileName;

    private static FileMetadata NewMetadata(long fileSize)
    {
        var id = Guid.NewGuid().ToString("N");
        return new FileMetadata
        {
            FileId = id,
            FileName = $"{id}.pdf",
            FilePath = $"/files/{id}.pdf",
            DownloadTimestamp = DateTime.UtcNow,
            Checksum = Guid.NewGuid().ToString("N"),
            FileSize = fileSize,
            Format = FileFormat.Pdf
        };
    }

    /// <summary>Disposes the per-test database context.</summary>
    public void Dispose() => _dbContext.Dispose();
}
