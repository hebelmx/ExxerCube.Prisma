namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Regression tests for <see cref="EfCoreRepository{T, TId}"/> ensuring ROP contracts wrap failures.
/// </summary>
public sealed class EfCoreRepositoryTests : IDisposable
{
    private readonly PrismaDbContext _dbContext;
    private readonly EfCoreRepository<FileMetadata, string> _repository;

    public EfCoreRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(options);
        _dbContext.Database.EnsureCreated();
        _repository = new EfCoreRepository<FileMetadata, string>(_dbContext);
    }

    [Fact]
    public async Task FindAsync_ShouldReturnFailure_WhenPredicateIsNull()
    {
        var result = await _repository.FindAsync(null!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Predicate cannot be null");
    }

    [Fact]
    public async Task ListAsync_ShouldReturnCancelled_WhenTokenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _repository.ListAsync(cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task AddAsync_ShouldReturnCancelled_WhenTokenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _repository.AddAsync(CreateMetadata("cancelled-add"), cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task AddRangeAsync_ShouldReturnFailure_WhenEntitiesAreNull()
    {
        var result = await _repository.AddRangeAsync(null!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error.Contains("Entities collection cannot be null"));
    }

    [Fact]
    public async Task SelectAsync_ShouldReturnFailure_WhenSelectorIsNull()
    {
        Expression<Func<FileMetadata, bool>> predicate = file => file.FileSize > 0;

        var result = await _repository.SelectAsync<FileMetadata>(predicate, null!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Selector cannot be null");
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldReturnFailure_WhenDbContextIsDisposed()
    {
        var metadata = CreateMetadata("disposed-save");
        var addResult = await _repository.AddAsync(metadata, TestContext.Current.CancellationToken);
        addResult.IsSuccess.ShouldBeTrue();

        _dbContext.Dispose();

        var saveResult = await _repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        saveResult.IsFailure.ShouldBeTrue();
        saveResult.Error.ShouldContain("Failed to persist");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnSuccess_WhenEntityExists()
    {
        var metadata = CreateMetadata("file-001");
        _dbContext.FileMetadata.Add(metadata);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _repository.GetByIdAsync("file-001", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.FileId.ShouldBe("file-001");
    }

    private static FileMetadata CreateMetadata(string id) => new()
    {
        FileId = id,
        FileName = $"{id}.pdf",
        FilePath = $"/files/{id}.pdf",
        DownloadTimestamp = DateTime.UtcNow,
        Checksum = Guid.NewGuid().ToString("N"),
        FileSize = 1024,
        Format = FileFormat.Pdf
    };

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}