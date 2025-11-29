namespace ExxerCube.Prisma.Tests.System;

/// <summary>
/// Collection fixture for Tesseract aggressive enhanced image tests.
/// Reuses TesseractFixture from original tests.
/// </summary>
[CollectionDefinition(nameof(TesseractEnhancedAggressiveCollection))]
public class TesseractEnhancedAggressiveCollection : ICollectionFixture<TesseractFixture>
{
    // Marker for collection definition - reuses TesseractFixture
}