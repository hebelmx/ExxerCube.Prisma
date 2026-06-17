namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Adapters;

/// <summary>
/// Configuration options for <see cref="CsvReferenceDataAdapter"/>.
/// </summary>
public sealed class CsvReferenceDataOptions
{
    /// <summary>
    /// Root directory under which institution-specific sub-directories contain the CSV data files.
    /// Each sub-directory name should match the institution name with spaces replaced by underscores.
    /// </summary>
    /// <example><c>C:\ReferenceData\csv\</c></example>
    public string RootDirectory { get; set; } = string.Empty;
}
