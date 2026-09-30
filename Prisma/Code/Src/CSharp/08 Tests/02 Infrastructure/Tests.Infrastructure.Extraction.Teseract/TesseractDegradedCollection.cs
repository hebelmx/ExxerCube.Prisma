namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// Collection fixture for Tesseract degraded image tests.
/// Reuses same TesseractFixture as original tests.
/// </summary>
[CollectionDefinition(nameof(TesseractDegradedCollection))]
public class TesseractDegradedCollection : ICollectionFixture<TesseractFixture>
{
    // Marker for collection definition - reuses TesseractFixture
}