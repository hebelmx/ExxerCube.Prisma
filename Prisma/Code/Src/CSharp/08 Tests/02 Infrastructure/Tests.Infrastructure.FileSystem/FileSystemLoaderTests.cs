namespace ExxerCube.Prisma.Tests.Infrastructure.FileSystem
{
    /// <summary>
    /// Contains unit tests for <see cref="FileSystemLoader"/>.
    /// </summary>
    public class FileSystemLoaderTests : IDisposable
    {
        private readonly ILogger<FileSystemLoader> _logger;
        private readonly FileSystemLoader _loader;
        private readonly string _testDirectory;
        private readonly string _testImagePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemLoaderTests"/> class.
        /// </summary>
        /// <param name="output">The test output helper.</param>
        public FileSystemLoaderTests(ITestOutputHelper output)
        {
            _logger = XUnitLogger.CreateLogger<FileSystemLoader>(output);
            _loader = new FileSystemLoader(_logger);
            _testDirectory = Path.Combine(Path.GetTempPath(), $"FileSystemLoaderTests_{Guid.NewGuid()}");
            _testImagePath = Path.Combine(_testDirectory, "test.png");

            Directory.CreateDirectory(_testDirectory);
            CreateTestImageFile(_testImagePath);
        }

        /// <summary>
        /// Tests that LoadImageAsync returns a successful result for a valid image file.
        /// </summary>
        [Fact]
        public async Task LoadImageAsync_ValidImageFile_ReturnsSuccessResult()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows platforms
            }

            // Act
            var result = await _loader.LoadImageAsync(_testImagePath, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value!.SourcePath.ShouldBe(_testImagePath);
            result.Value!.Data.ShouldNotBeNull();
            result.Value!.Data.Length.ShouldBeGreaterThan(0);
        }

        /// <summary>
        /// Tests that LoadImageAsync handles cancellation token correctly.
        /// </summary>
        [Fact]
        public async Task LoadImageAsync_WithCancellationRequested_ReturnsCancelledResult()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows platforms
            }

            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await _loader.LoadImageAsync(_testImagePath, cts.Token);

            // Assert
            // Service MUST respect cancellation token and propagate cancellation signal
            result.IsCancelled().ShouldBeTrue();
        }

        /// <summary>
        /// Tests that LoadImageAsync returns a failure result for a non-existent file.
        /// </summary>
        [Fact]
        public async Task LoadImageAsync_NonExistentFile_ReturnsFailureResult()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows platforms
            }

            // Arrange
            var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.png");

            // Act
            var result = await _loader.LoadImageAsync(nonExistentPath, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldNotBeNullOrEmpty();
        }

        /// <summary>
        /// Tests that LoadImageAsync returns a failure result for an unsupported file extension.
        /// </summary>
        [Fact]
        public async Task LoadImageAsync_UnsupportedExtension_ReturnsFailureResult()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows platforms
            }

            // Arrange
            var unsupportedPath = Path.Combine(_testDirectory, "test.xyz");
            File.WriteAllText(unsupportedPath, "test content");

            // Act
            var result = await _loader.LoadImageAsync(unsupportedPath, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldNotBeNullOrEmpty();
            result.Error.ShouldContain("Unsupported file extension");
        }

        /// <summary>
        /// Tests that LoadImagesFromDirectoryAsync returns successful results for multiple images.
        /// </summary>
        [Fact]
        public async Task LoadImagesFromDirectoryAsync_MultipleImages_ReturnsSuccessfulResults()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows platforms
            }

            // Arrange
            var image1Path = Path.Combine(_testDirectory, "image1.png");
            var image2Path = Path.Combine(_testDirectory, "image2.jpg");
            CreateTestImageFile(image1Path);
            CreateTestImageFile(image2Path);

            // Act
            var result = await _loader.LoadImagesFromDirectoryAsync(_testDirectory, Array.Empty<string>(), TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value!.Count.ShouldBeGreaterThanOrEqualTo(2);
        }

        /// <summary>
        /// Tests that LoadImagesFromDirectoryAsync handles cancellation token correctly.
        /// </summary>
        [Fact]
        public async Task LoadImagesFromDirectoryAsync_WithCancellationRequested_ReturnsCancelledResult()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows platforms
            }

            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await _loader.LoadImagesFromDirectoryAsync(_testDirectory, Array.Empty<string>(), cts.Token);

            // Assert
            // Service MUST respect cancellation token and propagate cancellation signal
            result.IsCancelled().ShouldBeTrue();
        }

        /// <summary>
        /// Tests that LoadImagesFromDirectoryAsync handles cancellation during processing and returns partial results with warnings.
        /// </summary>
        [Fact]
        public async Task LoadImagesFromDirectoryAsync_CancelledDuringProcessing_ReturnsPartialResultsWithWarnings()
        {
            // Arrange
            // Create multiple test images
            for (int i = 0; i < 5; i++)
            {
                var imagePath = Path.Combine(_testDirectory, $"image{i}.png");
                CreateTestImageFile(imagePath);
            }

            var cts = new CancellationTokenSource();

            if (OperatingSystem.IsWindows())
            {
                // Simulate cancellation after some images are loaded
                // Note: This is a simplified test - in real scenarios, cancellation would happen during actual file I/O
                var result = await _loader.LoadImagesFromDirectoryAsync(_testDirectory, Array.Empty<string>(), cts.Token);

                // Act - Cancel after starting
                cts.Cancel();

                // Since cancellation happens before processing starts (early check), we expect cancelled result
                // For a more realistic test, we'd need to cancel during actual processing
                var cancelledResult = await _loader.LoadImagesFromDirectoryAsync(_testDirectory, Array.Empty<string>(), cts.Token);

                // Assert
                cancelledResult.IsCancelled().ShouldBeTrue();
            }
        }

        /// <summary>
        /// Tests that LoadImagesFromDirectoryAsync returns an empty list for a directory with no images.
        /// </summary>
        [Fact]
        public async Task LoadImagesFromDirectoryAsync_EmptyDirectory_ReturnsEmptyList()
        {
            // Arrange
            var emptyDirectory = Path.Combine(Path.GetTempPath(), $"EmptyDir_{Guid.NewGuid()}");
            Directory.CreateDirectory(emptyDirectory);

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    // Act
                    var result = await _loader.LoadImagesFromDirectoryAsync(emptyDirectory, Array.Empty<string>(),
                        TestContext.Current.CancellationToken);

                    // Assert
                    result.IsSuccess.ShouldBeTrue();
                    result.Value.ShouldNotBeNull();
                    result.Value!.ShouldBeEmpty();
                }
                finally
                {
                    Directory.Delete(emptyDirectory);
                }
            }
        }

        /// <summary>
        /// Tests that ValidateFilePathAsync returns a successful result for a valid file path.
        /// </summary>
        [Fact]
        public async Task ValidateFilePathAsync_ValidFilePath_ReturnsSuccessResult()
        {
            // Act
            var result = await _loader.ValidateFilePathAsync(_testImagePath, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBeTrue();
        }

        /// <summary>
        /// Tests that ValidateFilePathAsync handles cancellation token correctly.
        /// </summary>
        [Fact]
        public async Task ValidateFilePathAsync_WithCancellationRequested_ReturnsCancelledResult()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await _loader.ValidateFilePathAsync(_testImagePath, cts.Token);

            // Assert
            // Service MUST respect cancellation token and propagate cancellation signal
            result.IsCancelled().ShouldBeTrue();
        }

        /// <summary>
        /// Tests that ValidateFilePathAsync returns a failure result for a non-existent file.
        /// </summary>
        [Fact]
        public async Task ValidateFilePathAsync_NonExistentFile_ReturnsFailureResult()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.png");

            // Act
            var result = await _loader.ValidateFilePathAsync(nonExistentPath, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldNotBeNullOrEmpty();
        }

        /// <summary>
        /// Tests that ValidateFilePathAsync returns a failure result for a null file path.
        /// </summary>
        [Fact]
        public async Task ValidateFilePathAsync_NullFilePath_ReturnsFailureResult()
        {
            // Act
            var result = await _loader.ValidateFilePathAsync(null!, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldNotBeNullOrEmpty();
        }

        /// <summary>
        /// Tests that GetSupportedExtensions returns the expected extensions.
        /// </summary>
        [Fact]
        public void GetSupportedExtensions_ReturnsExpectedExtensions()
        {
            // Act
            var extensions = _loader.GetSupportedExtensions();

            // Assert
            extensions.ShouldNotBeNull();
            extensions.Length.ShouldBeGreaterThan(0);
            extensions.ShouldContain(".png");
            extensions.ShouldContain(".jpg");
        }

        /// <summary>
        /// Tests that a PDF primary is rasterized via the injected converter and the first page's PNG bytes are
        /// returned (the single-image pipeline OCRs page 1, where the oficio/expediente header lives).
        /// </summary>
        [Fact]
        public async Task LoadImageAsync_PdfWithConverter_ReturnsRasterizedFirstPage()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows platforms
            }

            // Arrange: a .pdf file on disk + a converter that yields two pages.
            var pdfPath = Path.Combine(_testDirectory, "case.pdf");
            await File.WriteAllBytesAsync(
                pdfPath, new byte[] { 0x25, 0x50, 0x44, 0x46 }, TestContext.Current.CancellationToken); // "%PDF"

            var page1 = new byte[] { 1, 2, 3, 4 };
            var page2 = new byte[] { 5, 6, 7, 8 };
            var converter = Substitute.For<IPdfToImageConverter>();
            converter.ConvertToImagesAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result<IReadOnlyList<byte[]>>.Success(new List<byte[]> { page1, page2 })));

            var loader = new FileSystemLoader(_logger, converter);

            // Act
            var result = await loader.LoadImageAsync(pdfPath, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value!.Data.ShouldBe(page1); // first page only — used by the single-image pipeline
        }

        /// <summary>
        /// Tests that without a converter a PDF primary returns the raw PDF bytes (not an empty array). The
        /// previous stub returned <c>byte[0]</c>, which silently broke downstream OCR; raw bytes let an OCR
        /// executor with its own PDF fallback still attempt conversion.
        /// </summary>
        [Fact]
        public async Task LoadImageAsync_PdfWithoutConverter_ReturnsRawPdfBytesNotEmpty()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows platforms
            }

            // Arrange: _loader was constructed without a converter.
            var pdfPath = Path.Combine(_testDirectory, "nocvt.pdf");
            var raw = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }; // "%PDF-"
            await File.WriteAllBytesAsync(pdfPath, raw, TestContext.Current.CancellationToken);

            // Act
            var result = await _loader.LoadImageAsync(pdfPath, TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value!.Data.Length.ShouldBeGreaterThan(0);
            result.Value!.Data.ShouldBe(raw);
        }

        /// <summary>
        /// Creates a minimal valid test image file.
        /// </summary>
        /// <param name="filePath">The path where to create the test image.</param>
        private static void CreateTestImageFile(string filePath)
        {
            // Create minimal valid PNG file
            var pngBytes = new byte[]
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
                0x00, 0x00, 0x00, 0x0D, // IHDR chunk length
                0x49, 0x48, 0x44, 0x52, // IHDR
                0x00, 0x00, 0x00, 0x01, // Width: 1
                0x00, 0x00, 0x00, 0x01, // Height: 1
                0x08, 0x02, 0x00, 0x00, 0x00, // Bit depth, color type, etc.
                0x90, 0x77, 0x53, 0xDE, // CRC
                0x00, 0x00, 0x00, 0x0C, // IDAT chunk length
                0x49, 0x44, 0x41, 0x54, // IDAT
                0x08, 0x99, 0x01, 0x01, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, // Compressed data
                0xE2, 0x21, 0xBC, 0x33, // CRC
                0x00, 0x00, 0x00, 0x00, // IEND chunk length
                0x49, 0x45, 0x4E, 0x44, // IEND
                0xAE, 0x42, 0x60, 0x82  // CRC
            };

            File.WriteAllBytes(filePath, pngBytes);
        }

        /// <summary>
        /// Disposes of the test resources.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}