namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// Collection fixture for Tesseract enhanced image tests.
/// Reuses TesseractFixture from original tests.
/// </summary>
[CollectionDefinition(nameof(TesseractEnhancedCollection))]
public class TesseractEnhancedCollection : ICollectionFixture<TesseractFixture>
{
    // Marker for collection definition - reuses TesseractFixture
}