using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace ExxerCube.Prisma.Tests;

/// <summary>
/// Configuration for integration tests.
/// </summary>
public static class TestConfiguration
{
    /// <summary>
    /// Gets the Python modules path for testing.
    /// </summary>
    public static string PythonModulesPath => Path.Combine(
        Directory.GetCurrentDirectory(), 
        "..", "..", "..", "Python", "ocr_modules");

    /// <summary>
    /// Gets the test data path.
    /// </summary>
    public static string TestDataPath => Path.Combine(
        Directory.GetCurrentDirectory(), "TestData");

    /// <summary>
    /// Checks if Python environment is available for testing.
    /// </summary>
    /// <returns>True if Python environment is available.</returns>
    public static bool IsPythonEnvironmentAvailable()
    {
        try
        {
            var pythonPath = PythonModulesPath;
            return Directory.Exists(pythonPath) && 
                   File.Exists(Path.Combine(pythonPath, "expediente_extractor.py"));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets test documents for integration testing.
    /// </summary>
    /// <returns>List of test document paths.</returns>
    public static List<string> GetTestDocuments()
    {
        var testDataPath = TestDataPath;
        if (!Directory.Exists(testDataPath))
        {
            return new List<string>();
        }

        return Directory.GetFiles(testDataPath, "*.png")
            .Concat(Directory.GetFiles(testDataPath, "*.jpg"))
            .Concat(Directory.GetFiles(testDataPath, "*.pdf"))
            .ToList();
    }
}
