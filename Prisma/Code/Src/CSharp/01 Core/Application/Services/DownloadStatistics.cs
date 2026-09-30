namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Represents download statistics for a time period.
/// </summary>
public class DownloadStatistics
{
    /// <summary>
    /// Gets or sets the total number of files downloaded.
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// Gets or sets the total size of downloaded files in bytes.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the count of files by format.
    /// </summary>
    public Dictionary<FileFormat, int> FilesByFormat { get; set; } = new();

    /// <summary>
    /// Gets or sets the start date of the statistics period.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date of the statistics period.
    /// </summary>
    public DateTime EndDate { get; set; }
}