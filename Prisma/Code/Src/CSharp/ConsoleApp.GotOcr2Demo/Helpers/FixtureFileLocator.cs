using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.ConsoleApp.GotOcr2Demo.Helpers;

/// <summary>
/// Helper to locate fixture files with detailed logging.
/// </summary>
public static class FixtureFileLocator
{
    /// <summary>
    /// Locates fixture files, checking multiple possible locations.
    /// </summary>
    /// <param name="logger">Logger for detailed diagnostics.</param>
    /// <param name="args">Command line arguments (may contain explicit path).</param>
    /// <returns>Full path to image file, or null if not found.</returns>
    public static string? LocateFixtureImage(Microsoft.Extensions.Logging.ILogger logger, string[] args)
    {
        logger.LogInformation("=== Fixture File Locator ===");
        logger.LogInformation("Current directory: {CurrentDir}", Environment.CurrentDirectory);
        logger.LogInformation("Base directory: {BaseDir}", AppDomain.CurrentDomain.BaseDirectory);
        logger.LogInformation("Command line args: {Args}", string.Join(", ", args.Select(a => $"'{a}'")));

        // Option 1: Explicit path from command line
        if (args.Length > 0)
        {
            var explicitPath = args[0];
            logger.LogInformation("Checking explicit path from args[0]: {Path}", explicitPath);

            if (File.Exists(explicitPath))
            {
                var fullPath = Path.GetFullPath(explicitPath);
                logger.LogInformation("✓ Found file at explicit path: {FullPath}", fullPath);
                logger.LogInformation("  File size: {Size:N0} bytes", new FileInfo(fullPath).Length);
                return fullPath;
            }
            else
            {
                logger.LogWarning("✗ Explicit path does not exist: {Path}", explicitPath);
            }
        }
        else
        {
            logger.LogInformation("No command line arguments provided, searching for fixtures...");
        }

        // Option 2: Search multiple possible fixture locations
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var possiblePaths = new[]
        {
            // From bin/Debug/net10.0 -> Prisma/Fixtures/PRP1
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "..", "..", "Fixtures", "PRP1"),
            // From project root -> Fixtures/PRP1
            Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "..", "..", "..", "Fixtures", "PRP1"),
            // Absolute path (repo root)
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "..", "..", "..", "..", "Fixtures", "PRP1"),
            // Sample location
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "..", "..", "samples", "GotOcr2Sample", "PythonOcrLib", "PRP1"),

            Path.Combine("F:\\Dynamic\\ExxerCubeBanamex\\ExxerCube.Prisma\\Prisma\\Samples\\GotOcr2Sample\\PythonOcrLib\\PRP1"),
        };

        logger.LogInformation("Searching {Count} possible fixture locations...", possiblePaths.Length);

        for (int i = 0; i < possiblePaths.Length; i++)
        {
            var checkPath = Path.GetFullPath(possiblePaths[i]);
            logger.LogInformation("[{Index}] Checking: {Path}", i + 1, checkPath);

            if (Directory.Exists(checkPath))
            {
                logger.LogInformation("  ✓ Directory exists");

                // Search for multiple image and document extensions
                var allImageFiles = new List<string>();
                var patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.pdf", "*.JPG", "*.JPEG", "*.PNG", "*.PDF" };

                foreach (var pattern in patterns)
                {
                    var files = Directory.GetFiles(checkPath, pattern, SearchOption.TopDirectoryOnly);
                    allImageFiles.AddRange(files);
                }

                // Remove duplicates (in case of case-insensitive file system)
                var uniqueFiles = allImageFiles.Distinct().ToArray();

                logger.LogInformation("  Found {Count} image files ({Patterns})",
                    uniqueFiles.Length,
                    string.Join(", ", patterns));

                if (uniqueFiles.Length > 0)
                {
                    var selectedFile = uniqueFiles[0];
                    logger.LogInformation("  ✓ Selected: {FileName}", Path.GetFileName(selectedFile));
                    logger.LogInformation("  Full path: {FullPath}", selectedFile);
                    logger.LogInformation("  File size: {Size:N0} bytes", new FileInfo(selectedFile).Length);
                    return selectedFile;
                }
                else
                {
                    // Log directory contents for debugging
                    var allFiles = Directory.GetFiles(checkPath, "*.*", SearchOption.TopDirectoryOnly);
                    logger.LogWarning("  ✗ No image files in directory");
                    logger.LogDebug("  Directory contains {Count} total files", allFiles.Length);
                    if (allFiles.Length > 0 && allFiles.Length <= 10)
                    {
                        logger.LogDebug("  Files: {Files}", string.Join(", ", allFiles.Select(Path.GetFileName)));
                    }
                }
            }
            else
            {
                logger.LogDebug("  ✗ Directory does not exist");
            }
        }

        logger.LogError("✗ Could not locate any fixture images in any searched location");
        logger.LogInformation("Searched locations:");
        for (int i = 0; i < possiblePaths.Length; i++)
        {
            logger.LogInformation("  [{Index}] {Path}", i + 1, Path.GetFullPath(possiblePaths[i]));
        }

        return null;
    }
}