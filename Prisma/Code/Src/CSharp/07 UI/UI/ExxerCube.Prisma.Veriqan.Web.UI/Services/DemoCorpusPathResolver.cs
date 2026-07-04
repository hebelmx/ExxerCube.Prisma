namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>
/// Resolves repository-root-relative paths (e.g. the demo CSV reference-data bundle) to an
/// absolute path at process startup, independent of the host's current working directory.
/// </summary>
/// <remarks>
/// Ports the "walk up from the assembly output directory until a directory containing
/// <c>CLAUDE.md</c> is found" logic already proven in
/// <c>VecChecklistDemoE2ETests.ComputeDemoCorpusDir()</c>
/// (<c>Prisma/Code/Src/CSharp/08 Tests/03 Orchestration/Veriqan.Orchestration.Tests/VecChecklistDemoE2ETests.cs</c>),
/// so this host resolves the same repository root whether started via <c>dotnet run</c>
/// (assembly under <c>BuildArtifacts</c>, a sibling of the repository root) or from inside a
/// published container image — a hardcoded relative path would break in at least one of those
/// environments.
/// </remarks>
public static class DemoCorpusPathResolver
{
    /// <summary>Marker file used to identify the repository root.</summary>
    private const string RepoRootMarkerFile = "CLAUDE.md";

    /// <summary>
    /// Name of the repository directory when the search directory is a sibling of the
    /// repository root (the standard layout: <c>IndFusion/BuildArtifacts/</c> next to
    /// <c>IndFusion/ExxerCube.Prisma/</c>).
    /// </summary>
    private const string RepoDirectoryName = "ExxerCube.Prisma";

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> looking for the repository root — the
    /// first ancestor directory that either directly contains <c>CLAUDE.md</c>, or has a child
    /// directory named <c>ExxerCube.Prisma</c> that itself contains <c>CLAUDE.md</c>.
    /// </summary>
    /// <param name="startDirectory">
    /// Directory to start the upward search from (typically <see cref="AppContext.BaseDirectory"/>).
    /// </param>
    /// <returns>The absolute repository root path, or <see langword="null"/> when it cannot be located.</returns>
    public static string? FindRepoRoot(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            // Case 1: the search directory is inside the repository tree.
            if (File.Exists(Path.Combine(dir.FullName, RepoRootMarkerFile)))
                return dir.FullName;

            // Case 2: the repository root is a sibling of the search directory at this level.
            var siblingRepo = Path.Combine(dir.FullName, RepoDirectoryName);
            if (Directory.Exists(siblingRepo) && File.Exists(Path.Combine(siblingRepo, RepoRootMarkerFile)))
                return siblingRepo;

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    /// Resolves a repository-root-relative path (e.g. <c>"Prisma/Fixtures/PRP2/demo/reference-bundle"</c>)
    /// to an absolute path by locating the repository root from <paramref name="startDirectory"/>.
    /// </summary>
    /// <param name="startDirectory">
    /// Directory to start the upward search from (typically <see cref="AppContext.BaseDirectory"/>).
    /// </param>
    /// <param name="repoRelativePath">Path relative to the repository root.</param>
    /// <returns>
    /// The resolved absolute path, or <see langword="null"/> when the repository root cannot be
    /// located (e.g. running outside the normal build tree — the caller should fall back to
    /// leaving the configured value unresolved rather than throwing).
    /// </returns>
    public static string? ResolveAbsolutePath(string startDirectory, string repoRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRelativePath);

        var repoRoot = FindRepoRoot(startDirectory);
        return repoRoot is null ? null : Path.Combine(repoRoot, repoRelativePath);
    }
}
