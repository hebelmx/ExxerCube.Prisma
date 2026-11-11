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

    // ── Mutation-killing exact-value tests (Unit 35) ──────────────────────────

    /// <summary>
    /// Constructing with an empty base path must throw (kills the IsNullOrWhiteSpace guard / negation).
    /// </summary>
    [Fact]
    public void Constructor_EmptyBaseStoragePath_ThrowsArgumentException()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new FileStorageOptions { BaseStoragePath = string.Empty });

        var ex = Should.Throw<ArgumentException>(() => new FileMoverService(_logger, options));
        ex.Message.ShouldContain("BaseStoragePath");
    }

    /// <summary>
    /// Constructing with a whitespace-only base path must also throw
    /// (distinguishes IsNullOrWhiteSpace from IsNullOrEmpty).
    /// </summary>
    [Fact]
    public void Constructor_WhitespaceBaseStoragePath_ThrowsArgumentException()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new FileStorageOptions { BaseStoragePath = "   " });

        Should.Throw<ArgumentException>(() => new FileMoverService(_logger, options));
    }

    /// <summary>
    /// A valid base path constructs without throwing (kills mutations that always throw / invert the guard).
    /// </summary>
    [Fact]
    public void Constructor_ValidBaseStoragePath_DoesNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var options = Microsoft.Extensions.Options.Options.Create(new FileStorageOptions { BaseStoragePath = path });
            var service = new FileMoverService(_logger, options);
            service.ShouldNotBeNull();
            Directory.Exists(path).ShouldBeTrue(); // constructor ensures the base directory exists
        }
        finally
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }

    /// <summary>
    /// With both classification levels the destination path is exactly
    /// base / Level1 / Level2 / Year / safeFileName (no extra components, correct order).
    /// </summary>
    [Fact]
    public async Task MoveFileAsync_WithLevel2_BuildsExactClassificationPath()
    {
        var sourcePath = CreateTempSource();
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento,
            Level2 = ClassificationLevel2.Especial,
        };
        var expected = Path.Combine(_testStoragePath, "Aseguramiento", "Especial", DateTime.Now.Year.ToString(), "doc.pdf");

        try
        {
            var result = await _service.MoveFileAsync(sourcePath, classification, "doc.pdf", TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(expected);
            File.Exists(expected).ShouldBeTrue();
            File.Exists(sourcePath).ShouldBeFalse(); // moved, not copied
        }
        finally
        {
            SafeDelete(sourcePath);
        }
    }

    /// <summary>
    /// When Level2 is null the subcategory directory is omitted — destination is
    /// base / Level1 / Year / safeFileName (kills the <c>Level2 is not null</c> branch).
    /// </summary>
    [Fact]
    public async Task MoveFileAsync_NullLevel2_OmitsSubcategoryDirectory()
    {
        var sourcePath = CreateTempSource();
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Documentacion,
            Level2 = null,
        };
        // Level1.ToString() returns the DisplayName; Documentacion's is accented "Documentación".
        var expected = Path.Combine(_testStoragePath, "Documentación", DateTime.Now.Year.ToString(), "doc.pdf");

        try
        {
            var result = await _service.MoveFileAsync(sourcePath, classification, "doc.pdf", TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(expected);
        }
        finally
        {
            SafeDelete(sourcePath);
        }
    }

    /// <summary>
    /// With no naming conflict the file keeps its exact name — no counter suffix is appended
    /// (kills mutations that always enter the uniqueness loop).
    /// </summary>
    [Fact]
    public async Task MoveFileAsync_NoConflict_KeepsExactName()
    {
        var sourcePath = CreateTempSource();
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Aseguramiento };

        try
        {
            var result = await _service.MoveFileAsync(sourcePath, classification, "unique.pdf", TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            Path.GetFileName(result.Value).ShouldBe("unique.pdf");
        }
        finally
        {
            SafeDelete(sourcePath);
        }
    }

    /// <summary>
    /// With two existing conflicts the counter advances to 2 (kills the counter-increment mutation —
    /// a stuck counter would loop to the 1000 cap and throw).
    /// </summary>
    [Fact]
    public async Task MoveFileAsync_TwoConflicts_AppendsCounter2()
    {
        var sourcePath = CreateTempSource();
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Aseguramiento };

        var destDir = Path.Combine(_testStoragePath, "Aseguramiento", DateTime.Now.Year.ToString());
        Directory.CreateDirectory(destDir);
        await File.WriteAllBytesAsync(Path.Combine(destDir, "doc.pdf"), new byte[] { 1 }, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(destDir, "doc_1.pdf"), new byte[] { 1 }, TestContext.Current.CancellationToken);

        try
        {
            var result = await _service.MoveFileAsync(sourcePath, classification, "doc.pdf", TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            Path.GetFileName(result.Value).ShouldBe("doc_2.pdf");
        }
        finally
        {
            SafeDelete(sourcePath);
        }
    }

    /// <summary>
    /// When the source file exists but cannot be moved (held with an exclusive lock), File.Move
    /// throws and the catch converts it to a failure (covers and kills the catch-block mutants).
    /// </summary>
    [Fact]
    public async Task MoveFileAsync_SourceLocked_ReturnsFailureFromCatch()
    {
        var sourcePath = CreateTempSource();
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Aseguramiento };

        try
        {
            using var exclusiveLock = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None);

            var result = await _service.MoveFileAsync(sourcePath, classification, "doc.pdf", TestContext.Current.CancellationToken);

            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldContain("Error moving file");
        }
        finally
        {
            SafeDelete(sourcePath);
        }
    }

    private string CreateTempSource()
    {
        var path = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid()}.pdf");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        return path;
    }

    private static void SafeDelete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
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

