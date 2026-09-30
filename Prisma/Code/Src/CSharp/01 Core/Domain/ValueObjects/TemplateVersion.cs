namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents a semantic version for a template definition.
/// </summary>
/// <remarks>
/// <para>
/// Follows semantic versioning (SemVer) specification: MAJOR.MINOR.PATCH.
/// </para>
/// <list type="bullet">
///   <item><description>MAJOR: Incompatible API changes (breaking changes)</description></item>
///   <item><description>MINOR: Added functionality in backward-compatible manner</description></item>
///   <item><description>PATCH: Backward-compatible bug fixes</description></item>
/// </list>
/// <para>
/// Examples: "1.0.0", "2.5.3", "10.0.1"
/// </para>
/// </remarks>
public class TemplateVersion
{
    /// <summary>
    /// Gets or sets the major version number (breaking changes).
    /// </summary>
    public int Major { get; set; }

    /// <summary>
    /// Gets or sets the minor version number (backward-compatible features).
    /// </summary>
    public int Minor { get; set; }

    /// <summary>
    /// Gets or sets the patch version number (backward-compatible fixes).
    /// </summary>
    public int Patch { get; set; }

    /// <summary>
    /// Gets the full version string (e.g., "1.2.3").
    /// </summary>
    public string VersionString => $"{Major}.{Minor}.{Patch}";

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateVersion"/> class.
    /// </summary>
    public TemplateVersion()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateVersion"/> class with specified version components.
    /// </summary>
    /// <param name="major">The major version number.</param>
    /// <param name="minor">The minor version number.</param>
    /// <param name="patch">The patch version number.</param>
    public TemplateVersion(int major, int minor, int patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>
    /// Parses a version string (e.g., "1.2.3") into a TemplateVersion.
    /// </summary>
    /// <param name="versionString">The version string to parse.</param>
    /// <returns>A TemplateVersion instance.</returns>
    /// <exception cref="ArgumentException">Thrown when version string format is invalid.</exception>
    public static TemplateVersion Parse(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
        {
            throw new ArgumentException("Version string cannot be null or empty.", nameof(versionString));
        }

        var parts = versionString.Split('.');
        if (parts.Length != 3)
        {
            throw new ArgumentException($"Invalid version string format: '{versionString}'. Expected format: MAJOR.MINOR.PATCH", nameof(versionString));
        }

        if (!int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch))
        {
            throw new ArgumentException($"Invalid version string format: '{versionString}'. All components must be integers.", nameof(versionString));
        }

        return new TemplateVersion(major, minor, patch);
    }

    /// <summary>
    /// Tries to parse a version string into a TemplateVersion.
    /// </summary>
    /// <param name="versionString">The version string to parse.</param>
    /// <param name="version">The parsed TemplateVersion if successful; otherwise, null.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string versionString, out TemplateVersion? version)
    {
        try
        {
            version = Parse(versionString);
            return true;
        }
        catch
        {
            version = null;
            return false;
        }
    }

    /// <summary>
    /// Compares two template versions to determine if this version is greater than another.
    /// </summary>
    /// <param name="other">The version to compare against.</param>
    /// <returns>True if this version is greater than the other; otherwise, false.</returns>
    public bool IsGreaterThan(TemplateVersion other)
    {
        if (Major != other.Major)
            return Major > other.Major;
        if (Minor != other.Minor)
            return Minor > other.Minor;
        return Patch > other.Patch;
    }

    /// <summary>
    /// Compares two template versions to determine if this version is compatible with another.
    /// </summary>
    /// <param name="other">The version to compare against.</param>
    /// <returns>True if versions are compatible (same major version); otherwise, false.</returns>
    /// <remarks>
    /// Compatible versions share the same major version number, indicating no breaking changes.
    /// </remarks>
    public bool IsCompatibleWith(TemplateVersion other)
    {
        return Major == other.Major;
    }

    /// <summary>
    /// Returns the version string representation.
    /// </summary>
    /// <returns>The version string (e.g., "1.2.3").</returns>
    public override string ToString() => VersionString;
}
