namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// Collection fixture for analytical filter E2E tests.
/// Reuses TesseractFixture for OCR capabilities.
/// </summary>
[CollectionDefinition(nameof(AnalyticalFilterE2ECollection))]
public class AnalyticalFilterE2ECollection : ICollectionFixture<TesseractFixture>
{
    // Marker for collection definition
}