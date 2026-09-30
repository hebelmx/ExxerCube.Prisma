namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2;

/// <summary>
/// Collection fixture for GOT-OCR2 degraded image tests.
/// Reuses same GotOcr2Fixture as original tests.
/// </summary>
[CollectionDefinition(nameof(GotOcr2DegradedCollection))]
public class GotOcr2DegradedCollection : ICollectionFixture<GotOcr2Fixture>
{
    // Marker for collection definition - reuses GotOcr2Fixture
}