namespace ExxerCube.Prisma.Tests.System.Ocr.Pipeline;

/// <summary>
/// Collection fixture for Tesseract tests.
/// Uses simple initialization - no Python environment needed.
/// </summary>
[CollectionDefinition(nameof(TesseractCollection))]
public class TesseractCollection : ICollectionFixture<TesseractFixture>
{
    // This class has no code, and is never instantiated.
    // Its purpose is to be the marker for the collection definition.
}