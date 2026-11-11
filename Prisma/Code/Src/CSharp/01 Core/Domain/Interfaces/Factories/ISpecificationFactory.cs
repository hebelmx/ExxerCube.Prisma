namespace ExxerCube.Prisma.Domain.Interfaces.Factories;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Factory for creating query specifications.
/// This abstraction allows Application layer to request specifications without depending on concrete implementations.
/// </summary>
public interface ISpecificationFactory
{
    /// <summary>
    /// Creates a specification for filtering file metadata.
    /// </summary>
    /// <param name="startDate">Optional lower bound for the download timestamp.</param>
    /// <param name="endDate">Optional upper bound for the download timestamp.</param>
    /// <param name="format">Optional file format filter.</param>
    /// <param name="skip">Optional number of rows to skip.</param>
    /// <param name="take">Optional number of rows to take.</param>
    /// <returns>A specification for file metadata filtering.</returns>
    ISpecification<FileMetadata> CreateFileMetadataFilters(
        DateTime? startDate,
        DateTime? endDate,
        FileFormat? format,
        int? skip = null,
        int? take = null);
}
