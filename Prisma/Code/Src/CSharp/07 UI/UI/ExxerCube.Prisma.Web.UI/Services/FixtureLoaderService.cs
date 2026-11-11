namespace ExxerCube.Prisma.Web.UI.Services;

using ExxerCube.Prisma.Testing.Infrastructure;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for loading fixture files from the PRP1 fixtures directory.
/// Abstracts file I/O operations to enable testing and centralize error handling.
/// </summary>
public class FixtureLoaderService
{
    private readonly ILogger<FixtureLoaderService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixtureLoaderService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for fixture loading operations</param>
    public FixtureLoaderService(ILogger<FixtureLoaderService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads a fixture file as a byte array.
    /// </summary>
    /// <param name="fixtureName">The fixture file name (e.g., "222AAA-44444444442025.xml")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Byte array containing file contents</returns>
    /// <exception cref="FileNotFoundException">Thrown when fixture file is not found</exception>
    /// <exception cref="IOException">Thrown when file cannot be read</exception>
    public virtual async Task<byte[]> LoadFixtureBytesAsync(
        string fixtureName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fixturesPath = GetFixturesPath();
            var filePath = Path.Combine(fixturesPath, fixtureName);

            _logger.LogWarning("🔍 FIXTURE DEBUG: Requested fixture name: {RequestedFixtureName}", fixtureName);
            _logger.LogWarning("🔍 FIXTURE DEBUG: Fixtures base path: {FixturesPath}", fixturesPath);
            _logger.LogWarning("🔍 FIXTURE DEBUG: Combined file path: {FilePath}", filePath);

            if (!File.Exists(filePath))
            {
                _logger.LogError("❌ FIXTURE DEBUG: File does NOT exist at path: {FilePath}", filePath);
                throw new FileNotFoundException($"Fixture file not found: {fixtureName}", filePath);
            }

            // Get actual file info to verify the name matches
            var fileInfo = new FileInfo(filePath);
            var actualFileName = fileInfo.Name;

            _logger.LogWarning("🔍 FIXTURE DEBUG: Actual file name on disk: {ActualFileName}", actualFileName);

            // CRITICAL: Verify the actual file name matches the requested fixture name
            if (!string.Equals(actualFileName, fixtureName, StringComparison.OrdinalIgnoreCase))
            {
                var errorMessage = $"FIXTURE MISMATCH! Requested: '{fixtureName}' but found: '{actualFileName}' at path: {filePath}";
                _logger.LogError("❌❌❌ {ErrorMessage}", errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);

            _logger.LogWarning("✅ FIXTURE DEBUG: Successfully loaded {Size} bytes from {ActualFileName}",
                bytes.Length, actualFileName);

            return bytes;
        }
        catch (Exception ex) when (ex is not FileNotFoundException and not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to load fixture: {FixtureName}", fixtureName);
            throw new IOException($"Failed to load fixture: {fixtureName}", ex);
        }
    }

    /// <summary>
    /// Gets the path to the PRP1 fixtures directory using FixtureFinder.
    /// </summary>
    /// <returns>Full path to fixtures directory</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when fixtures directory cannot be found</exception>
    public string GetFixturesPath()
    {
        try
        {
            var path = FixtureFinder.FindFixturesPath("PRP1");
            _logger.LogDebug("Fixtures path resolved: {Path}", path);
            return path;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Failed to locate PRP1 fixtures directory");
            throw;
        }
    }

    /// <summary>
    /// Checks if a fixture file exists.
    /// </summary>
    /// <param name="fixtureName">The fixture file name</param>
    /// <returns>True if file exists, false otherwise</returns>
    public bool FixtureExists(string fixtureName)
    {
        try
        {
            var fixturesPath = GetFixturesPath();
            var filePath = Path.Combine(fixturesPath, fixtureName);
            var exists = File.Exists(filePath);

            _logger.LogDebug("Fixture {FixtureName} exists: {Exists}", fixtureName, exists);

            return exists;
        }
        catch (DirectoryNotFoundException)
        {
            _logger.LogWarning("Cannot check fixture existence - fixtures directory not found");
            return false;
        }
    }

    /// <summary>
    /// Gets information about a fixture file.
    /// </summary>
    /// <param name="fixtureName">The fixture file name</param>
    /// <returns>FileInfo if file exists, null otherwise</returns>
    public FileInfo? GetFixtureInfo(string fixtureName)
    {
        try
        {
            var fixturesPath = GetFixturesPath();
            var filePath = Path.Combine(fixturesPath, fixtureName);

            if (!File.Exists(filePath))
            {
                return null;
            }

            return new FileInfo(filePath);
        }
        catch (DirectoryNotFoundException)
        {
            _logger.LogWarning("Cannot get fixture info - fixtures directory not found");
            return null;
        }
    }
}
