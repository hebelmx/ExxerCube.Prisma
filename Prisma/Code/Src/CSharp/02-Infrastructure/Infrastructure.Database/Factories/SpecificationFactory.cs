using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces.Factories;
using ExxerCube.Prisma.Infrastructure.Database.Specifications;

namespace ExxerCube.Prisma.Infrastructure.Database.Factories;

/// <summary>
/// Factory for creating query specifications.
/// </summary>
public sealed class SpecificationFactory : ISpecificationFactory
{
    /// <inheritdoc />
    public ISpecification<FileMetadata> CreateFileMetadataFilters(
        DateTime? startDate,
        DateTime? endDate,
        FileFormat? format,
        int? skip = null,
        int? take = null)
    {
        return new FileMetadataFiltersSpecification(startDate, endDate, format, skip, take);
    }
}
