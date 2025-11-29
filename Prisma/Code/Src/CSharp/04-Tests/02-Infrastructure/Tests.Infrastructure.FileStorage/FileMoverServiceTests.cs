using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Tests.Infrastructure.FileStorage;

/// <summary>
/// Unit tests for <see cref="FileMoverService"/>.
/// </summary>
public class FileMoverServiceTests : IDisposable
{
    private readonly ILogger<FileMoverService> _logger;
    private readonly string _testStoragePath;
    private readonly FileMoverService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileMoverServiceTests"/> class.
    /// </summary>
    public FileMoverServiceTests()
    {
        _logger = Substitute.For<ILogger<FileMoverService>>();
        _testStoragePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = Microsoft.Extensions.Options.Options.Create(new FileStorageOptions
        {
            BaseStoragePath = _testStoragePath
        });
        _service = new FileMoverService(_logger, options);
    }

    /// <summary>
    /// Tests that file is moved to classification-based directory.
    /// </summary>
    [Fact]
    public async Task MoveFileAsync_ValidFile_MovesToClassificationDirectory()
    {
        // Arrange
        var sourcePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.pdf");
        var testContent = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(sourcePath, testContent, TestContext.Current.CancellationToken);

        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento,
            Level2 = ClassificationLevel2.Especial
        };
        var safeFileName = "ASEGURAMIENTO_ESPECIAL_test.pdf";

        Result<string>? result = null;
        try
        {
            // Act
            result = await _service.MoveFileAsync(sourcePath, classification, safeFileName, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNullOrEmpty();
            File.Exists(result.Value).ShouldBeTrue();
            File.Exists(sourcePath).ShouldBeFalse();
            result.Value.ShouldContain("Aseguramiento");
            result.Value.ShouldContain("Especial");
            result.Value.ShouldContain(DateTime.Now.Year.ToString());
        }
        finally
        {
            // Cleanup
            if (File.Exists(sourcePath))
                File.Delete(sourcePath);
            if (result != null && result.IsSuccess && result.Value != null && File.Exists(result.Value))
                File.Delete(result.Value);
        }
    }

    /// <summary>
    /// Tests that non-existent source file returns failure.
    /// </summary>
    [Fact]
    public async Task MoveFileAsync_NonExistentFile_ReturnsFailure()
    {
        // Arrange
        var sourcePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.pdf");
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Documentacion
        };
        var safeFileName = "test.pdf";

        // Act
        var result = await _service.MoveFileAsync(sourcePath, classification, safeFileName, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("does not exist");
    }

    /// <summary>
    /// Tests that file name conflicts are handled by appending counter.
    /// </summary>
    [Fact]
    public async Task MoveFileAsync_FileNameConflict_AppendsCounter()
    {
        // Arrange
        var sourcePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.pdf");
        var testContent = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(sourcePath, testContent, TestContext.Current.CancellationToken);

        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento
        };
        var safeFileName = "test.pdf";

        // Create destination directory and file to cause conflict
        var destDir = Path.Combine(_testStoragePath, "Aseguramiento", DateTime.Now.Year.ToString());
        Directory.CreateDirectory(destDir);
        var existingFile = Path.Combine(destDir, safeFileName);
        await File.WriteAllBytesAsync(existingFile, new byte[] { 9, 9, 9 }, TestContext.Current.CancellationToken);

        try
        {
            // Act
            var result = await _service.MoveFileAsync(sourcePath, classification, safeFileName, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNullOrEmpty();
            result.Value.ShouldContain("_1.pdf"); // Should append counter
            File.Exists(result.Value).ShouldBeTrue();
            
            var resultValue = result.Value;
            
            // Cleanup
            if (File.Exists(sourcePath))
                File.Delete(sourcePath);
            if (resultValue != null && File.Exists(resultValue))
                File.Delete(resultValue);
            if (File.Exists(existingFile))
                File.Delete(existingFile);
        }
        finally
        {
            // Cleanup on exception or success
            if (File.Exists(sourcePath))
                File.Delete(sourcePath);
            if (File.Exists(existingFile))
                File.Delete(existingFile);
        }
    }

    /// <summary>
    /// Disposes test resources.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_testStoragePath))
        {
            Directory.Delete(_testStoragePath, true);
        }
    }
}

