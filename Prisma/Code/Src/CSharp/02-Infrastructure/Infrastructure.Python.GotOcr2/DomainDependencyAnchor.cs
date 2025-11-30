using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Infrastructure.Python.GotOcr2;

/// <summary>
/// Ensures this infrastructure assembly references the Domain layer (architecture rule).
/// </summary>
internal static class DomainDependencyAnchor
{
    // Reference a domain type to enforce dependency on Domain.
    private static readonly FileFormat Anchor = FileFormat.Unknown;
}
