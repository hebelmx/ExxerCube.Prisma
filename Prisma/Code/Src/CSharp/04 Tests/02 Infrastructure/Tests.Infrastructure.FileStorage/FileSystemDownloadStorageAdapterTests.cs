using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.FileStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
namespace ExxerCube.Prisma.Tests.Infrastructure.FileStorage;

/// <summary>
/// Unit tests for <see cref="FileSystemDownloadStorageAdapter"/>.
/// </summary>
public class FileSystemDownloadStorageAdapterTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ILogger<FileSystemDownloadStorageAdapter> _logger;
    private readonly FileSystemDownloadStorageAdapter _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemDownloadStorageAdapterTests"/> class.
    /// </summary>
    public FileSystemDownloadStorageAdapterTests(ITestOutputHelper output)
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        var options = Options.Create(new FileStorageOptions
        {
            StorageBasePath = _tempDirectory
        });

        _logger = XUnitLogger.CreateLogger<FileSystemDownloadStorageAdapter>(output);
        _service = new FileSystemDownloadStorageAdapter(_logger, options);
    }

    /// <summary>
    /// Tests that <see cref="FileSystemDownloadStorageAdapter.SaveFileAsync"/> successfully saves a file.
    /// </summary>
    [Fact]
    public async Task SaveFileAsync_ValidFile_SavesSuccessfully()
    {
        // Arrange
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var fileName = "test-document.pdf";
        var format = FileFormat.Pdf;

        // Act
        var result = await _service.SaveFileAsync(fileContent, fileName, format, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        if (result.IsSuccess)
        {
            result.Value.ShouldNotBeNullOrEmpty();
            var savedPath = result.Value;
            File.Exists(savedPath).ShouldBeTrue();
            var savedContent = await File.ReadAllBytesAsync(savedPath, TestContext.Current.CancellationToken);
            savedContent.ShouldBe(fileContent);
        }
    }

    /// <summary>
    /// Tests that <see cref="FileSystemDownloadStorageAdapter.SaveFileAsync"/> creates the directory structure.
    /// </summary>
    [Fact]
    public async Task SaveFileAsync_ValidFile_CreatesDirectoryStructure()
    {
        // Arrange
        var fileContent = new byte[] { 1, 2, 3 };
        var fileName = "test.xml";
        var format = FileFormat.Xml;

        // Act
        var result = await _service.SaveFileAsync(fileContent, fileName, format, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        if (result.IsSuccess)
        {
            var savedPath = result.Value;
            var directory = Path.GetDirectoryName(savedPath);
            directory.ShouldNotBeNull();
            Directory.Exists(directory).ShouldBeTrue();
        }
    }

    /// <summary>
    /// Tests that <see cref="FileSystemDownloadStorageAdapter.GenerateStoragePath"/> generates deterministic paths.
    /// </summary>
    [Fact]
    public void GenerateStoragePath_WithChecksum_GeneratesDeterministicPath()
    {
        // Arrange
        var fileName = "test.docx";
        var format = FileFormat.Docx;
        var checksum = "test-checksum-123";

        // Act
        var path1 = _service.GenerateStoragePath(fileName, format, checksum);
        var path2 = _service.GenerateStoragePath(fileName, format, checksum);

        // Assert
        path1.ShouldBe(path2);
        path1.ShouldContain(checksum);
        path1.ShouldContain("docx");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}

