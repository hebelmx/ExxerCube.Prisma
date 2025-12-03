namespace Prisma.Tests.System.E2E.Fixtures;

/// <summary>
/// Provides access to PRP1 client-provided test fixtures.
/// These are the "happy path" documents with typical errors and edge cases from the client.
/// </summary>
/// <remarks>
/// Fixtures are sourced from: Prisma/Code/Src/CSharp/04-Tests/03-System/Tests.System/Fixtures/PRP1/
/// These are real SIARA documents provided by the client with known extraction challenges.
/// </remarks>
public static class PRP1FixtureProvider
{
    private static readonly string BasePath = GetFixturesBasePath();

    /// <summary>
    /// Gets the base path for PRP1 fixtures.
    /// Searches from current assembly location up to find the source Fixtures directory.
    /// </summary>
    private static string GetFixturesBasePath()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(PRP1FixtureProvider).Assembly.Location)
            ?? throw new InvalidOperationException("Could not determine assembly location");

        // Navigate up to find the repository root
        // From BuildArtifacts: BuildArtifacts/Prisma/bin/Prisma.Tests.System.E2E/net10.0
        // OR from source: ExxerCube.Prisma/Prisma/Code/Src/CSharp/04-Tests/07-E2E/.../bin/...
        var current = new DirectoryInfo(assemblyDir);

        // Navigate up until we find either "ExxerCube.Prisma" or "BuildArtifacts"
        while (current != null &&
               current.Name != "ExxerCube.Prisma" &&
               current.Name != "BuildArtifacts")
        {
            current = current.Parent;
        }

        if (current == null)
            throw new InvalidOperationException($"Could not find repository root from: {assemblyDir}");

        // If we're in BuildArtifacts, go up one more level to find ExxerCube.Prisma
        DirectoryInfo repoRoot;
        if (current.Name == "BuildArtifacts")
        {
            // Go up to parent, then look for ExxerCube.Prisma
            var parentDir = current.Parent;
            if (parentDir == null)
                throw new InvalidOperationException($"Could not find repository root from BuildArtifacts: {assemblyDir}");

            // Look for ExxerCube.Prisma in the parent directory
            var possibleRoot = new DirectoryInfo(Path.Combine(parentDir.FullName, "ExxerCube.Prisma"));
            if (!possibleRoot.Exists)
                throw new InvalidOperationException($"Could not find ExxerCube.Prisma from: {assemblyDir}");

            repoRoot = possibleRoot;
        }
        else
        {
            repoRoot = current;
        }

        // Use the System test fixtures as the source of truth
        var fixturesPath = Path.Combine(
            repoRoot.FullName,
            "Prisma",
            "Code",
            "Src",
            "CSharp",
            "04-Tests",
            "03-System",
            "Tests.System",
            "Fixtures",
            "PRP1"
        );

        if (!Directory.Exists(fixturesPath))
            throw new DirectoryNotFoundException($"PRP1 fixtures directory not found: {fixturesPath}");

        return fixturesPath;
    }

    /// <summary>
    /// 222AAA-44444444442025: Standard case with typical data extraction requirements
    /// </summary>
    public static TestFixture AAA_222_Standard => new(
        Name: "222AAA-44444444442025",
        PdfPath: Path.Combine(BasePath, "222AAA-44444444442025.pdf"),
        XmlPath: Path.Combine(BasePath, "222AAA-44444444442025.xml"),
        Description: "Standard SIARA document with typical aseguramiento (asset freezing) request. " +
                    "Tests basic OCR, field extraction, and data reconciliation.",
        ExpectedErrors: new[]
        {
            "Company name typo: 'AEROLINEAS PAYASO' may have OCR artifacts",
            "Reference field whitespace padding may cause trimming issues"
        }
    );

    /// <summary>
    /// 333BBB-44444444442025: Case with more complex text extraction challenges
    /// </summary>
    public static TestFixture BBB_333_Complex => new(
        Name: "333BBB-44444444442025",
        PdfPath: Path.Combine(BasePath, "333BBB-44444444442025.pdf"),
        XmlPath: Path.Combine(BasePath, "333BBB-44444444442025.xml"),
        Description: "More complex document with additional extraction challenges. " +
                    "Tests OCR resilience and fuzzy matching capabilities.",
        ExpectedErrors: new[]
        {
            "Potential OCR quality issues in handwritten sections",
            "Date format variations requiring normalization"
        }
    );

    /// <summary>
    /// 333ccc-6666666662025: Lowercase variant with specific edge cases
    /// </summary>
    public static TestFixture CCC_333_EdgeCase => new(
        Name: "333ccc-6666666662025",
        PdfPath: Path.Combine(BasePath, "333ccc-6666666662025.pdf"),
        XmlPath: Path.Combine(BasePath, "333ccc-6666666662025.xml"),
        Description: "Edge case document with non-standard formatting. " +
                    "Tests system robustness with lowercase expediente numbers.",
        ExpectedErrors: new[]
        {
            "Lowercase 'ccc' in expediente number requires case-insensitive matching",
            "Extended deadline (plazo) value may trigger validation warnings"
        }
    );

    /// <summary>
    /// 555CCC-66666662025: Minimal document for baseline validation
    /// </summary>
    public static TestFixture CCC_555_Minimal => new(
        Name: "555CCC-66666662025",
        PdfPath: Path.Combine(BasePath, "555CCC-66666662025.pdf"),
        XmlPath: Path.Combine(BasePath, "555CCC-66666662025.xml"),
        Description: "Minimal SIARA document with essential fields only. " +
                    "Tests baseline extraction with sparse data.",
        ExpectedErrors: new[]
        {
            "Small file size (56KB) may indicate reduced complexity",
            "Minimal field set requires handling of missing optional fields"
        }
    );

    /// <summary>
    /// Gets all PRP1 fixtures for comprehensive E2E testing.
    /// </summary>
    public static IReadOnlyList<TestFixture> AllFixtures => new List<TestFixture>
    {
        AAA_222_Standard,
        BBB_333_Complex,
        CCC_333_EdgeCase,
        CCC_555_Minimal
    };

    /// <summary>
    /// Validates that all fixture files exist on disk.
    /// Call this during test initialization to fail fast if fixtures are missing.
    /// </summary>
    public static void ValidateAllFixtures()
    {
        foreach (var fixture in AllFixtures)
        {
            fixture.ValidateFilesExist();
        }
    }

    /// <summary>
    /// Gets a fixture by name (case-insensitive).
    /// </summary>
    public static TestFixture? GetFixtureByName(string name)
    {
        return AllFixtures.FirstOrDefault(f =>
            f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
