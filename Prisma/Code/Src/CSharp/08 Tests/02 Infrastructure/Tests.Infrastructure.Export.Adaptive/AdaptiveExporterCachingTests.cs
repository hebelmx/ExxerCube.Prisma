using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

/// <summary>
/// Mutation-killing tests for <see cref="AdaptiveExporter"/>'s template cache, using a
/// substituted repository so cache hits/misses are observable as repository call counts.
/// Pins the per-type cache key, the cache population (TryAdd), and ClearTemplateCache.
/// </summary>
public sealed class AdaptiveExporterCachingTests
{
    private readonly ITemplateRepository _repository = Substitute.For<ITemplateRepository>();
    private readonly ITemplateFieldMapper _mapper = Substitute.For<ITemplateFieldMapper>();
    private readonly AdaptiveExporter _exporter;

    public AdaptiveExporterCachingTests()
    {
        _exporter = new AdaptiveExporter(_repository, _mapper, NullLogger<AdaptiveExporter>.Instance);
    }

    private static TemplateDefinition TemplateOfType(string type) => new()
    {
        TemplateId = type + "-id",
        TemplateType = type,
        Version = "1.0.0"
    };

    [Fact]
    public async Task GetActiveTemplateAsync_SecondCall_ServedFromCache_RepositoryHitOnce()
    {
        _repository.GetLatestTemplateAsync("Excel", Arg.Any<CancellationToken>())
            .Returns(TemplateOfType("Excel"));

        await _exporter.GetActiveTemplateAsync("Excel", TestContext.Current.CancellationToken);
        await _exporter.GetActiveTemplateAsync("Excel", TestContext.Current.CancellationToken);

        // TryAdd populated the cache, so the repository is queried only once.
        await _repository.Received(1).GetLatestTemplateAsync("Excel", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearTemplateCache_ForcesRepositoryReload()
    {
        _repository.GetLatestTemplateAsync("Excel", Arg.Any<CancellationToken>())
            .Returns(TemplateOfType("Excel"));

        await _exporter.GetActiveTemplateAsync("Excel", TestContext.Current.CancellationToken);
        _exporter.ClearTemplateCache();
        await _exporter.GetActiveTemplateAsync("Excel", TestContext.Current.CancellationToken);

        // Clearing the cache means the second lookup misses and hits the repository again.
        await _repository.Received(2).GetLatestTemplateAsync("Excel", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetActiveTemplateAsync_CachesPerTemplateType_NoCrossTypeCollision()
    {
        _repository.GetLatestTemplateAsync("Excel", Arg.Any<CancellationToken>())
            .Returns(TemplateOfType("Excel"));
        _repository.GetLatestTemplateAsync("Xml", Arg.Any<CancellationToken>())
            .Returns(TemplateOfType("Xml"));

        await _exporter.GetActiveTemplateAsync("Excel", TestContext.Current.CancellationToken);
        var xml = await _exporter.GetActiveTemplateAsync("Xml", TestContext.Current.CancellationToken);

        // A constant (non per-type) cache key would return the cached Excel template here.
        xml.IsSuccess.ShouldBeTrue();
        xml.Value!.TemplateType.ShouldBe("Xml");
    }
}
