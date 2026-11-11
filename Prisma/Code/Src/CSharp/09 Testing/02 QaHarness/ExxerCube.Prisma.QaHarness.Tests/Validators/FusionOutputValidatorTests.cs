// <copyright file="FusionOutputValidatorTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using ExxerCube.Prisma.QaHarness.Validators;
using ExxerCube.Prisma.QaHarness.Validators.Pipeline;

namespace ExxerCube.Prisma.QaHarness.Tests.Validators;

/// <summary>
/// Fast unit tests for <see cref="FusionOutputValidator"/>.
/// Uses planted <c>.fusion.json</c> files in a temp directory — no Docker or network required.
/// </summary>
public sealed class FusionOutputValidatorTests
{
    private static readonly FusionOutputValidator Sut = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fusion-validator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task<string> PlantFusionFileAsync(
        string directory, string content, string? fileName = null)
    {
        fileName ??= $"{Guid.NewGuid():N}.fusion.json";
        var path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
        return path;
    }

    // ── Conformant: valid fusion file with required fields ───────────────────

    /// <summary>A directory with a valid .fusion.json containing FileId and CorrelationId should be conformant.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_ValidFusionJson_IsConformant()
    {
        var dir = CreateTempDir();
        try
        {
            await PlantFusionFileAsync(dir, """
                {
                  "FileId": "file-abc-123",
                  "CorrelationId": "corr-xyz-456",
                  "NumeroExpediente": "A/AS1-2505-088637-PHM",
                  "Stage": "Extraction"
                }
                """);

            var result = await Sut.ValidateAsync(dir, TestContext.Current.CancellationToken);

            result.ValidatorId.ShouldBe("FUSION-OUTPUT");
            result.IsConformant.ShouldBeTrue();
            result.Findings.ShouldNotContain(f =>
                f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Non-conformant: directory absent ────────────────────────────────────

    /// <summary>A non-existent directory path should produce a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_MissingDirectory_IsNotConformant_WithCriticalFinding()
    {
        var path = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}");

        var result = await Sut.ValidateAsync(path, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f =>
            f.Severity == FindingSeverity.Critical && f.RuleId == "FUSION-02");
    }

    // ── Non-conformant: no fusion files in directory ────────────────────────

    /// <summary>An existing but empty directory (no .fusion.json) should produce a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_EmptyDirectory_NoFusionFiles_IsNotConformant_Critical()
    {
        var dir = CreateTempDir();
        try
        {
            // Add a non-fusion file to ensure the directory exists with content
            await File.WriteAllTextAsync(
                Path.Combine(dir, "something.txt"), "hello",
                TestContext.Current.CancellationToken);

            var result = await Sut.ValidateAsync(dir, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeFalse();
            result.Findings.ShouldContain(f =>
                f.Severity == FindingSeverity.Critical && f.RuleId == "FUSION-03");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Non-conformant: fusion file missing FileId ───────────────────────────

    /// <summary>A .fusion.json missing the FileId field should produce a Major finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_FusionMissingFileId_IsNotConformant_WithMajorFinding()
    {
        var dir = CreateTempDir();
        try
        {
            await PlantFusionFileAsync(dir, """
                {
                  "CorrelationId": "corr-xyz-456"
                }
                """);

            var result = await Sut.ValidateAsync(dir, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeFalse();
            result.Findings.ShouldContain(f =>
                f.Severity == FindingSeverity.Major && f.RuleId == "FUSION-04");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Non-conformant: fusion file missing CorrelationId ───────────────────

    /// <summary>A .fusion.json missing the CorrelationId field should produce a Major finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_FusionMissingCorrelationId_IsNotConformant_WithMajorFinding()
    {
        var dir = CreateTempDir();
        try
        {
            await PlantFusionFileAsync(dir, """
                {
                  "FileId": "file-abc-123"
                }
                """);

            var result = await Sut.ValidateAsync(dir, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeFalse();
            result.Findings.ShouldContain(f =>
                f.Severity == FindingSeverity.Major && f.RuleId == "FUSION-05");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Non-conformant: fusion file with empty FileId ───────────────────────

    /// <summary>A .fusion.json with an empty FileId string should produce a Major finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_FusionEmptyFileId_IsNotConformant_WithMajorFinding()
    {
        var dir = CreateTempDir();
        try
        {
            await PlantFusionFileAsync(dir, """
                {
                  "FileId": "",
                  "CorrelationId": "corr-xyz-456"
                }
                """);

            var result = await Sut.ValidateAsync(dir, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeFalse();
            result.Findings.ShouldContain(f =>
                f.Severity == FindingSeverity.Major && f.RuleId == "FUSION-04");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Non-conformant: malformed JSON ───────────────────────────────────────

    /// <summary>A malformed .fusion.json file should produce a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_MalformedJson_IsNotConformant_WithCriticalFinding()
    {
        var dir = CreateTempDir();
        try
        {
            await PlantFusionFileAsync(dir, "{ not valid json <<<");

            var result = await Sut.ValidateAsync(dir, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeFalse();
            result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Informational finding: file count ────────────────────────────────────

    /// <summary>When fusion files are found, an Info finding records the count.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_ValidFusion_HasInfoFindingWithFileCount()
    {
        var dir = CreateTempDir();
        try
        {
            await PlantFusionFileAsync(dir, """{"FileId":"f1","CorrelationId":"c1"}""");
            await PlantFusionFileAsync(dir, """{"FileId":"f2","CorrelationId":"c2"}""");

            var result = await Sut.ValidateAsync(dir, TestContext.Current.CancellationToken);

            var info = result.Findings.FirstOrDefault(f => f.RuleId == "FUSION-INFO");
            info.ShouldNotBeNull();
            info!.Observed!.ShouldContain("2 files");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    /// <summary>A pre-cancelled token yields IsConformant=false with a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_CancelledToken_IsNotConformant()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await Sut.ValidateAsync("/any/dir", cts.Token);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }

    // ── Null / empty path ────────────────────────────────────────────────────

    /// <summary>A null/empty path should produce a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_EmptyPath_IsNotConformant_WithCriticalFinding()
    {
        var result = await Sut.ValidateAsync(string.Empty, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }
}
