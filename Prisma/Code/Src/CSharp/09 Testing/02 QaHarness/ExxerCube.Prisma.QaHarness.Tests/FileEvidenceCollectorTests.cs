// <copyright file="FileEvidenceCollectorTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using ExxerCube.Prisma.QaHarness.Evidence;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.QaHarness.Tests;

/// <summary>
/// Fast unit tests for <see cref="FileEvidenceCollector"/>. No Docker required.
/// </summary>
public sealed class FileEvidenceCollectorTests
{
    /// <summary>
    /// Planting files in a temp directory and harvesting them should yield
    /// <see cref="EvidenceItem"/> records matching the pattern.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task FileEvidenceCollector_HarvestFiles_FindsMatchingFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"file-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        await File.WriteAllTextAsync(Path.Combine(tempDir, "output.siro.xml"), "<siro />", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "output.siro.xml.bak"), "<siro />", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "report.txt"), "text", TestContext.Current.CancellationToken);

        var collector = new FileEvidenceCollector(
            "run-001",
            NullLogger<FileEvidenceCollector>.Instance);

        try
        {
            // Act — harvest only .siro.xml files.
            var result = await collector.HarvestFilesAsync(
                tempDir,
                "*.siro.xml",
                "siro-output",
                TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue(result.Error ?? "HarvestFilesAsync failed");
            result.Value.ShouldNotBeNull();
            result.Value.Count.ShouldBe(1);
            result.Value[0].Kind.ShouldBe(EvidenceKind.GeneratedFile);
            result.Value[0].Label.ShouldContain("output.siro.xml");
            result.Value[0].SizeBytes.ShouldBeGreaterThan(0);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* cleanup */ }
        }
    }

    /// <summary>
    /// Harvesting from a non-existent directory should return a failure result, not throw.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task FileEvidenceCollector_HarvestFiles_NonExistentPath_ReturnsFailure()
    {
        var collector = new FileEvidenceCollector(
            "run-002",
            NullLogger<FileEvidenceCollector>.Instance);

        var result = await collector.HarvestFilesAsync(
            @"C:\does-not-exist-qa-harness-test",
            "*.xml",
            "test",
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
    }

    /// <summary>
    /// Harvested items should accumulate in <see cref="IEvidenceCollector.CurrentPackage"/>.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task FileEvidenceCollector_HarvestFiles_AccumulatesInCurrentPackage()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"file-evidence-pkg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "a.fusion.json"), "{}", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "b.fusion.json"), "{}", TestContext.Current.CancellationToken);

        var runId = "run-pkg-test";
        var collector = new FileEvidenceCollector(runId, NullLogger<FileEvidenceCollector>.Instance);

        try
        {
            await collector.HarvestFilesAsync(
                tempDir, "*.fusion.json", "fusion", TestContext.Current.CancellationToken);

            var pkg = collector.CurrentPackage;
            pkg.RunId.ShouldBe(runId);
            pkg.Items.Count.ShouldBe(2);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* cleanup */ }
        }
    }

    /// <summary>
    /// Calling <see cref="IEvidenceCollector.Reset"/> should clear accumulated items.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task FileEvidenceCollector_Reset_ClearsPackage()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"file-evidence-reset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "file.xml"), "<x/>", TestContext.Current.CancellationToken);

        var collector = new FileEvidenceCollector("run-reset", NullLogger<FileEvidenceCollector>.Instance);

        try
        {
            await collector.HarvestFilesAsync(
                tempDir, "*.xml", "xml", TestContext.Current.CancellationToken);

            collector.CurrentPackage.Items.Count.ShouldBe(1);

            collector.Reset();

            collector.CurrentPackage.Items.Count.ShouldBe(0);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* cleanup */ }
        }
    }
}
