namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Integration-style unit tests that validate <see cref="FileMetadataQueryService"/> query behaviors and error handling.
/// </summary>
public class FileMetadataQueryServiceTests
{
    private readonly IRepository<FileMetadata, string> _repository;
    private readonly ISpecificationFactory _specificationFactory;
    private readonly ILogger<FileMetadataQueryService> _logger;
    private readonly FileMetadataQueryService _service;

    /// <summary>
    /// Initializes a new instance of the test suite with mocked repository and logger dependencies.
    /// </summary>
    public FileMetadataQueryServiceTests()
    {
        _repository = Substitute.For<IRepository<FileMetadata, string>>();
        _specificationFactory = Substitute.For<ISpecificationFactory>();
        _logger = Substitute.For<ILogger<FileMetadataQueryService>>();
        _service = new FileMetadataQueryService(_repository, _specificationFactory, _logger);
    }

    /// <summary>
    /// Verifies that all files are returned when the repository succeeds.
    /// </summary>
    /// <returns>A task that completes after the retrieval assertions are validated.</returns>
    [Fact]
    public async Task GetFileMetadataAsync_ShouldReturnFiles_WhenRepositorySucceeds()
    {
        var metadata = new List<FileMetadata>
        {
            new()
            {
                FileId = "FILE-001",
                DownloadTimestamp = DateTime.UtcNow,
                Format = FileFormat.Pdf,
                FileSize = 42
            }
        };

        _repository.ListAsync(Arg.Any<ISpecification<FileMetadata>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<IReadOnlyList<FileMetadata>>.Success(metadata)));

        var result = await _service.GetFileMetadataAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(1);
        result.Value![0].FileId.ShouldBe("FILE-001");
    }

    /// <summary>
    /// Verifies that repository failures are propagated as failed results.
    /// </summary>
    /// <returns>A task that completes after asserting the failure is surfaced.</returns>
    [Fact]
    public async Task GetFileMetadataAsync_ShouldPropagateFailure_WhenRepositoryFails()
    {
        _repository.ListAsync(Arg.Any<ISpecification<FileMetadata>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<IReadOnlyList<FileMetadata>>.WithFailure("db-error")));

        var result = await _service.GetFileMetadataAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("db-error");
    }

    /// <summary>
    /// Verifies that missing entities return a failed result with null value.
    /// </summary>
    /// <returns>A task that completes after null-result assertions are checked.</returns>
    [Fact]
    public async Task GetFileMetadataByIdAsync_ShouldReturnNull_WhenEntityNotFound()
    {
        _repository.GetByIdAsync("missing", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<FileMetadata?>.WithFailure("Entity not found")));

        var result = await _service.GetFileMetadataByIdAsync("missing", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Value.ShouldBeNull();
    }

    /// <summary>
    /// Verifies that download statistics aggregate counts and sizes correctly.
    /// </summary>
    /// <returns>A task that completes after aggregation assertions are validated.</returns>
    [Fact]
    public async Task GetDownloadStatisticsAsync_ShouldAggregateValues()
    {
        var files = new List<FileMetadata>
        {
            new()
            {
                FileId = "one",
                Format = FileFormat.Pdf,
                FileSize = 100,
                DownloadTimestamp = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                FileId = "two",
                Format = FileFormat.Xml,
                FileSize = 200,
                DownloadTimestamp = DateTime.UtcNow
            }
        };

        _repository.ListAsync(Arg.Any<ISpecification<FileMetadata>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<IReadOnlyList<FileMetadata>>.Success(files)));

        var result = await _service.GetDownloadStatisticsAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.TotalFiles.ShouldBe(2);
        result.Value!.TotalSizeBytes.ShouldBe(300);
        result.Value!.FilesByFormat[FileFormat.Pdf].ShouldBe(1);
        result.Value!.FilesByFormat[FileFormat.Xml].ShouldBe(1);
    }

    /// <summary>
    /// Verifies that cancellation tokens are honored by the query operation.
    /// </summary>
    /// <returns>A task that completes after cancellation is detected.</returns>
    [Fact]
    public async Task GetFileMetadataAsync_ShouldReturnCancelled_WhenTokenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _service.GetFileMetadataAsync(cancellationToken: cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }
}
