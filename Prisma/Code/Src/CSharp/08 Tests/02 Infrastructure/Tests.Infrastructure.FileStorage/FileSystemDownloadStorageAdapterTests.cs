using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.FileStorage;

namespace ExxerCube.Prisma.Tests.Infrastructure.FileStorage;

/// <summary>
/// Unit tests for <see cref="FileSystemDownloadStorageAdapter"/>.
/// </summary>
public class FileSystemDownloadStorageAdapterTests : IDisposable
{
    // A valid 32-byte AES-256 test key (never use in production).
    private static readonly string TestEncryptionKey = Convert.ToBase64String(
        Enumerable.Range(10, 32).Select(i => (byte)i).ToArray());

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

        // Wire up the real AES-256-GCM encryptor so these tests cover the full wiring.
        var config = Substitute.For<IConfiguration>();
        config[AesGcmStorageEncryptor.ConfigurationKey].Returns(TestEncryptionKey);
        var encryptor = new AesGcmStorageEncryptor(config, NullLogger<AesGcmStorageEncryptor>.Instance);

        _logger = XUnitLogger.CreateLogger<FileSystemDownloadStorageAdapter>(output);
        _service = new FileSystemDownloadStorageAdapter(_logger, options, encryptor);
    }

    /// <summary>
    /// Tests that <see cref="FileSystemDownloadStorageAdapter.SaveFileAsync"/> successfully saves
    /// a file and that <see cref="FileSystemDownloadStorageAdapter.ReadFileAsync"/> recovers the
    /// original plaintext (round-trip via the storage adapter — not raw disk bytes).
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
        result.Value.ShouldNotBeNullOrEmpty();

        var savedPath = result.Value!;
        File.Exists(savedPath).ShouldBeTrue();

        // The raw on-disk bytes are ciphertext — use ReadFileAsync to decrypt.
        var readResult = await _service.ReadFileAsync(savedPath, TestContext.Current.CancellationToken);
        readResult.IsSuccess.ShouldBeTrue();
        readResult.Value.ShouldBe(fileContent);
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
