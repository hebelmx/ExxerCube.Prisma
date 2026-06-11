namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Test helpers for creating strategy instances.
/// </summary>
/// <remarks>
/// Relocated here from <c>AdaptiveDocxExtractorLiskovTests</c> when that twin was superseded
/// by <c>AdaptiveDocxExtractorContractTests</c> (ITDD Phase 3). Still used by both
/// <c>AdaptiveDocxExtractorContractTests</c> and <c>AdaptiveDocxExtractionIntegrationTests</c>.
/// </remarks>
internal static class TestHelpers
{
    public static IReadOnlyList<IAdaptiveDocxStrategy> CreateAllStrategies(ITestOutputHelper output)
    {
        return new List<IAdaptiveDocxStrategy>
        {
            new StructuredDocxStrategy(XUnitLogger.CreateLogger<StructuredDocxStrategy>(output)),
            new ContextualDocxStrategy(XUnitLogger.CreateLogger<ContextualDocxStrategy>(output)),
            new TableBasedDocxStrategy(XUnitLogger.CreateLogger<TableBasedDocxStrategy>(output)),
            new ComplementExtractionStrategy(XUnitLogger.CreateLogger<ComplementExtractionStrategy>(output)),
            new SearchExtractionStrategy(XUnitLogger.CreateLogger<SearchExtractionStrategy>(output))
        };
    }
}
