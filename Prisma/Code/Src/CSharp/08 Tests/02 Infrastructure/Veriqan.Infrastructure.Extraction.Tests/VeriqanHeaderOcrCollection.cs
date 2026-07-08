namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// xUnit collection that serialises every test class constructing a real
/// <see cref="ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr.TesseractHeaderProductOcrEngine"/>.
/// </summary>
/// <remarks>
/// The native <c>TesseractEngine</c> must be created at most once per process and deadlocks on a
/// second concurrent instantiation (see <c>TesseractHeaderProductOcrEngine</c>'s remarks). Any
/// test class that constructs the real engine — rather than a fake
/// <see cref="ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr.IHeaderProductOcrEngine"/> —
/// must declare <c>[Collection(VeriqanHeaderOcrCollection.Name)]</c> so xUnit v3 runs those
/// classes serially instead of in parallel (mirrors the
/// <c>Veriqan.Orchestration.Tests.MetricsIsolationCollection</c> pattern, design doc
/// <c>veriqan-e7-s72-header-ocr-design-2026-07-08.md</c> §6).
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class VeriqanHeaderOcrCollection
{
    /// <summary>Collection name used in <c>[Collection]</c> attributes.</summary>
    public const string Name = "VeriqanHeaderOcr";
}
