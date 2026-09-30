// <copyright file="CorpusSeederTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using ExxerCube.Prisma.QaHarness.Provisioning;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.QaHarness.Tests;

/// <summary>
/// Fast unit tests for <see cref="CorpusSeeder"/>. No Docker required.
/// </summary>
public sealed class CorpusSeederTests
{
    /// <summary>
    /// When the static Fixtures directory exists and contains files, the seeder should
    /// report <see cref="CorpusStatus.RestoredFromFixtures"/> and return the fixtures path.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task CorpusSeeder_WhenStaticFixturesExist_ReturnsRestoredFromFixtures()
    {
        // Arrange — build a fake repo root with a non-empty Fixtures/ directory.
        var repoRoot = Path.Combine(Path.GetTempPath(), $"qa-harness-test-{Guid.NewGuid():N}");
        var fixturesDir = Path.Combine(repoRoot, "Prisma", "Code", "Fixtures");
        Directory.CreateDirectory(fixturesDir);
        await File.WriteAllTextAsync(Path.Combine(fixturesDir, "sample.txt"), "fixture content", TestContext.Current.CancellationToken);

        // Also create the Prisma\Fixtures marker so LocateRepoRoot-style logic works if needed.
        var prismaFixtures = Path.Combine(repoRoot, "Prisma", "Fixtures");
        Directory.CreateDirectory(prismaFixtures);

        var seeder = new CorpusSeeder(NullLogger<CorpusSeeder>.Instance);

        try
        {
            // Act — no SIARA corpus dir, no Python generator → should fall back to Fixtures.
            var (status, corpusPath) = await seeder.SeedAsync(
                repoRoot,
                outputPath: null,
                documentCount: 5,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            status.ShouldBe(CorpusStatus.RestoredFromFixtures);
            corpusPath.ShouldBe(fixturesDir);
            Directory.Exists(corpusPath).ShouldBeTrue();
        }
        finally
        {
            try { Directory.Delete(repoRoot, recursive: true); } catch { /* cleanup best-effort */ }
        }
    }

    /// <summary>
    /// When neither a corpus dir, generator, nor fixtures exist, the seeder should return
    /// <see cref="CorpusStatus.AbsentNoGenerator"/>.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task CorpusSeeder_WhenNothingExists_ReturnsAbsentNoGenerator()
    {
        // Arrange — completely empty temp directory (no Fixtures, no generator).
        var repoRoot = Path.Combine(Path.GetTempPath(), $"qa-harness-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoRoot);

        var seeder = new CorpusSeeder(NullLogger<CorpusSeeder>.Instance);

        try
        {
            var (status, _) = await seeder.SeedAsync(
                repoRoot,
                outputPath: null,
                documentCount: 5,
                cancellationToken: TestContext.Current.CancellationToken);

            status.ShouldBe(CorpusStatus.AbsentNoGenerator);
        }
        finally
        {
            try { Directory.Delete(repoRoot, recursive: true); } catch { /* cleanup */ }
        }
    }

    /// <summary>
    /// When the SIARA corpus dir already exists and is non-empty, the seeder should report
    /// <see cref="CorpusStatus.Seeded"/> without invoking the Python generator.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task CorpusSeeder_WhenSiaraCorpusDirNonEmpty_ReturnsSeeded()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"qa-harness-siara-{Guid.NewGuid():N}");
        var siaraDir = Path.Combine(
            repoRoot,
            "Prisma", "Deployments", "Siara.Simulator", "bulk_generated_documents_all_formats");
        Directory.CreateDirectory(siaraDir);
        await File.WriteAllTextAsync(Path.Combine(siaraDir, "doc1.pdf"), "dummy", TestContext.Current.CancellationToken);

        var seeder = new CorpusSeeder(NullLogger<CorpusSeeder>.Instance);

        try
        {
            var (status, corpusPath) = await seeder.SeedAsync(
                repoRoot,
                outputPath: null,
                cancellationToken: TestContext.Current.CancellationToken);

            status.ShouldBe(CorpusStatus.Seeded);
            corpusPath.ShouldBe(siaraDir);
        }
        finally
        {
            try { Directory.Delete(repoRoot, recursive: true); } catch { /* cleanup */ }
        }
    }
}
