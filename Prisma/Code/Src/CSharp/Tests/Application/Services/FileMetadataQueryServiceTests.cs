namespace Tests.Application.Services;

public class FileMetadataQueryServiceTests
{
    private readonly IRepository<FileMetadata, string> _repository;
    private readonly ILogger<FileMetadataQueryService> _logger;
    private readonly FileMetadataQueryService _service;

    public FileMetadataQueryServiceTests()
    {
        _repository = Substitute.For<IRepository<FileMetadata, string>>();
        _logger = Substitute.For<ILogger<FileMetadataQueryService>>();
        _service = new FileMetadataQueryService(_repository, _logger);
    }

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

        var result = await _service.GetFileMetadataAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(1);
        result.Value![0].FileId.ShouldBe("FILE-001");
    }

    [Fact]
    public async Task GetFileMetadataAsync_ShouldPropagateFailure_WhenRepositoryFails()
    {
        _repository.ListAsync(Arg.Any<ISpecification<FileMetadata>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<IReadOnlyList<FileMetadata>>.WithFailure("db-error")));

        var result = await _service.GetFileMetadataAsync();

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("db-error");
    }

    [Fact]
    public async Task GetFileMetadataByIdAsync_ShouldReturnNull_WhenEntityNotFound()
    {
        _repository.GetByIdAsync("missing", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<FileMetadata?>.Success(null)));

        var result = await _service.GetFileMetadataByIdAsync("missing");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

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

        var result = await _service.GetDownloadStatisticsAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.TotalFiles.ShouldBe(2);
        result.Value!.TotalSizeBytes.ShouldBe(300);
        result.Value!.FilesByFormat[FileFormat.Pdf].ShouldBe(1);
        result.Value!.FilesByFormat[FileFormat.Xml].ShouldBe(1);
    }

    [Fact]
    public async Task GetFileMetadataAsync_ShouldReturnCancelled_WhenTokenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _service.GetFileMetadataAsync(cancellationToken: cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }
}
