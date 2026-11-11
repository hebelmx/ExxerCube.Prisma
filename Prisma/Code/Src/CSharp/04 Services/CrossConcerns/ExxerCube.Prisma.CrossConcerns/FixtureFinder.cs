namespace ExxerCube.Prisma.Testing.Infrastructure;

/// <summary>
/// Helper class for locating test fixtures in the solution directory.
/// Provides robust path resolution that works consistently across different test runners.
/// </summary>
public static class FixtureFinder
{
    /// <summary>
    /// Finds the fixtures directory by searching up from the current directory.
    /// </summary>
    /// <param name="fixtureSubPath">The relative path within the Fixtures directory (e.g., "PRP1").</param>
    /// <returns>The full path to the fixture directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the fixtures directory cannot be found.</exception>
    public static string FindFixturesPath(string fixtureSubPath = "")
    {
        var searchPaths = new[]
        {
            // Strategy 1: Start from current directory (test runner working directory)
            Directory.GetCurrentDirectory(),

            // Strategy 2: Start from test assembly location
            AppDomain.CurrentDomain.BaseDirectory,

            // Strategy 3: Start from entry assembly location (if different)
            Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "")
        };

        foreach (var startPath in searchPaths.Where(p => !string.IsNullOrEmpty(p)))
        {
            var foundPath = SearchForFixtures(startPath!, fixtureSubPath);
            if (foundPath != null)
            {
                return foundPath;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find Fixtures directory. Searched from:\n" +
            string.Join("\n", searchPaths.Where(p => !string.IsNullOrEmpty(p))));
    }

    /// <summary>
    /// Searches for the Fixtures directory by walking up the directory tree.
    /// </summary>
    /// <param name="startPath">The starting directory path.</param>
    /// <param name="fixtureSubPath">The relative path within the Fixtures directory.</param>
    /// <returns>The full path to the fixture directory, or null if not found.</returns>
    private static string? SearchForFixtures(string startPath, string fixtureSubPath)
    {
        var currentDir = new DirectoryInfo(startPath);

        // Walk up the directory tree (max 15 levels to prevent infinite loops)
        for (int i = 0; i < 15 && currentDir != null; i++)
        {
            // Look for "Fixtures" directory in current directory
            var fixturesDir = Path.Combine(currentDir.FullName, "Fixtures");
            if (Directory.Exists(fixturesDir))
            {
                var targetPath = string.IsNullOrEmpty(fixtureSubPath)
                    ? fixturesDir
                    : Path.Combine(fixturesDir, fixtureSubPath);

                if (Directory.Exists(targetPath))
                {
                    return targetPath;
                }
            }

            // Look for "ExxerCube.Prisma" directory (solution root marker)
            if (currentDir.Name == "ExxerCube.Prisma")
            {
                var fixturesInRoot = Path.Combine(currentDir.FullName, "Fixtures");
                if (Directory.Exists(fixturesInRoot))
                {
                    var targetPath = string.IsNullOrEmpty(fixtureSubPath)
                        ? fixturesInRoot
                        : Path.Combine(fixturesInRoot, fixtureSubPath);

                    if (Directory.Exists(targetPath))
                    {
                        return targetPath;
                    }
                }
            }

            currentDir = currentDir.Parent;
        }

        return null;
    }

    /// <summary>
    /// Gets the full path to a specific fixture file.
    /// </summary>
    /// <param name="fixtureSubPath">The relative path within the Fixtures directory (e.g., "PRP1").</param>
    /// <param name="fileName">The fixture file name.</param>
    /// <returns>The full path to the fixture file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the fixture file cannot be found.</exception>
    public static string GetFixturePath(string fixtureSubPath, string fileName)
    {
        var fixturesPath = FindFixturesPath(fixtureSubPath);
        var filePath = Path.Combine(fixturesPath, fileName);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Fixture file not found: {fileName}\nSearched in: {fixturesPath}\n" +
                $"Available files:\n{GetAvailableFiles(fixturesPath)}",
                filePath);
        }

        return filePath;
    }

    /// <summary>
    /// Lists all available fixture files in a directory for debugging.
    /// </summary>
    /// <param name="directoryPath">The directory path to list.</param>
    /// <returns>A formatted string listing all files in the directory.</returns>
    private static string GetAvailableFiles(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
                return "(directory does not exist)";

            var files = Directory.GetFiles(directoryPath)
                .Select(Path.GetFileName)
                .OrderBy(f => f);

            return string.Join("\n", files.Select(f => $"  - {f}"));
        }
        catch (Exception ex)
        {
            return $"(error listing files: {ex.Message})";
        }
    }

    /// <summary>
    /// Validates that required fixture files exist in the specified directory.
    /// </summary>
    /// <param name="fixtureSubPath">The relative path within the Fixtures directory.</param>
    /// <param name="requiredFiles">The list of required file names.</param>
    /// <exception cref="FileNotFoundException">Thrown when any required file is missing.</exception>
    public static void ValidateFixtures(string fixtureSubPath, params string[] requiredFiles)
    {
        var fixturesPath = FindFixturesPath(fixtureSubPath);
        var missingFiles = requiredFiles.Where(file => !File.Exists(Path.Combine(fixturesPath, file))).ToList();

        if (missingFiles.Any())
        {
            throw new FileNotFoundException(
                $"Missing {missingFiles.Count} required fixture file(s) in {fixturesPath}:\n" +
                string.Join("\n", missingFiles.Select(f => $"  - {f}")));
        }
    }

    /// <summary>
    /// Finds the bulk_generated_documents_all_formats directory by searching up from the current directory.
    /// </summary>
    /// <returns>The full path to the bulk documents directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the bulk documents directory cannot be found.</exception>
    public static string FindBulkDocumentsPath()
    {
        var searchPaths = new[]
        {
            // Strategy 1: Start from current directory (test runner working directory)
            Directory.GetCurrentDirectory(),

            // Strategy 2: Start from test assembly location
            AppDomain.CurrentDomain.BaseDirectory,

            // Strategy 3: Start from entry assembly location (if different)
            Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "")
        };

        foreach (var startPath in searchPaths.Where(p => !string.IsNullOrEmpty(p)))
        {
            var foundPath = SearchForBulkDocuments(startPath!);
            if (foundPath != null)
            {
                return foundPath;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find bulk_generated_documents_all_formats directory. Searched from:\n" +
            string.Join("\n", searchPaths.Where(p => !string.IsNullOrEmpty(p))));
    }

    /// <summary>
    /// Searches for the bulk_generated_documents_all_formats directory by walking up the directory tree.
    /// </summary>
    /// <param name="startPath">The starting directory path.</param>
    /// <returns>The full path to the bulk documents directory, or null if not found.</returns>
    private static string? SearchForBulkDocuments(string startPath)
    {
        var currentDir = new DirectoryInfo(startPath);

        // Walk up the directory tree (max 15 levels to prevent infinite loops)
        for (int i = 0; i < 15 && currentDir != null; i++)
        {
            // Look for "bulk_generated_documents_all_formats" directory in current directory
            var bulkDir = Path.Combine(currentDir.FullName, "bulk_generated_documents_all_formats");
            if (Directory.Exists(bulkDir))
            {
                return bulkDir;
            }

            // Look for "ExxerCube.Prisma" directory (solution root marker)
            if (currentDir.Name == "ExxerCube.Prisma")
            {
                var bulkInRoot = Path.Combine(currentDir.FullName, "bulk_generated_documents_all_formats");
                if (Directory.Exists(bulkInRoot))
                {
                    return bulkInRoot;
                }
            }

            currentDir = currentDir.Parent;
        }

        return null;
    }
}
