using System.Linq.Expressions;
using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Infrastructure.Database.Specifications;

/// <summary>
/// Encapsulates the filtering, ordering and paging rules for retrieving file metadata records.
/// </summary>
public sealed class FileMetadataFiltersSpecification : ISpecification<FileMetadata>
{
    private static readonly Expression<Func<FileMetadata, object>> OrderByDownloadTimestamp
        = file => file.DownloadTimestamp;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileMetadataFiltersSpecification"/> class.
    /// </summary>
    /// <param name="startDate">Optional lower bound for the download timestamp.</param>
    /// <param name="endDate">Optional upper bound for the download timestamp.</param>
    /// <param name="format">Optional file format filter.</param>
    /// <param name="skip">Optional number of rows to skip.</param>
    /// <param name="take">Optional number of rows to take.</param>
    public FileMetadataFiltersSpecification(
        DateTime? startDate,
        DateTime? endDate,
        FileFormat? format,
        int? skip = null,
        int? take = null)
    {
        StartDate = startDate;
        EndDate = endDate;
        Format = format;
        Skip = skip;
        Take = take;

        var hasFilter = startDate.HasValue || endDate.HasValue || format != null;
        if (hasFilter)
        {
            Criteria = file =>
                (!startDate.HasValue || file.DownloadTimestamp >= startDate.Value) &&
                (!endDate.HasValue || file.DownloadTimestamp <= endDate.Value) &&
                (format == null || file.Format == format);
        }
    }

    /// <summary>Gets the optional start date filter.</summary>
    public DateTime? StartDate { get; }

    /// <summary>Gets the optional end date filter.</summary>
    public DateTime? EndDate { get; }

    /// <summary>Gets the optional file format filter.</summary>
    public FileFormat? Format { get; }

    /// <inheritdoc />
    public Expression<Func<FileMetadata, bool>>? Criteria { get; }

    /// <inheritdoc />
    public Expression<Func<FileMetadata, object>>? OrderBy => null;

    /// <inheritdoc />
    public Expression<Func<FileMetadata, object>>? OrderByDescending => OrderByDownloadTimestamp;

    /// <inheritdoc />
    public IReadOnlyList<Expression<Func<FileMetadata, object>>> Includes { get; }
        = Array.Empty<Expression<Func<FileMetadata, object>>>();

    /// <inheritdoc />
    public int? Skip { get; }

    /// <inheritdoc />
    public int? Take { get; }
}
