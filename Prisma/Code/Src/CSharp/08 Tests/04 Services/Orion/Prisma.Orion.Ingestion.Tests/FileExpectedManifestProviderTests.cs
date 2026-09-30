using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Services.Manifest;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Orion.Ingestion;

namespace ExxerCube.Prisma.Orion.Ingestion.Tests;

/// <summary>
/// Unit tests for <see cref="FileExpectedManifestProvider"/> (Item B #8 — fault-tolerant JSON loader).
/// Covers: valid JSON parse; missing file; blank path; garbage/unparseable JSON.
/// </summary>
public sealed class FileExpectedManifestProviderTests
{
    private static FileExpectedManifestProvider CreateSut(string? manifestPath)
    {
        var opts = new ExpectedManifestOptions { Enabled = true, ManifestPath = manifestPath };
        return new FileExpectedManifestProvider(
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<FileExpectedManifestProvider>.Instance);
    }

    private static async Task<string> WriteManifestAsync(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"manifest-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, json, System.Text.Encoding.UTF8);
        return path;
    }

    // ------------------------------------------------------------------------------------------------
    // Valid JSON parse
    // ------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_ValidJson_ReturnsParsedManifest()
    {
        const string json = """
            {
              "oficios": [
                { "caseId": "222AAA-2025", "expectedFormats": ["Pdf", "Xml", "Docx"] },
                { "caseId": "333BBB-2025", "expectedFormats": ["Pdf"] }
              ]
            }
            """;
        var path = await WriteManifestAsync(json);
        try
        {
            var sut = CreateSut(path);
            var result = await sut.LoadAsync(TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            var manifest = result.Value!;
            manifest.Oficios.Count.ShouldBe(2);

            var first = manifest.Oficios[0];
            first.CaseId.ShouldBe("222AAA-2025");
            first.ExpectedFormats.Count.ShouldBe(3);
            first.ExpectedFormats.ShouldContain(FileFormat.Pdf);
            first.ExpectedFormats.ShouldContain(FileFormat.Xml);
            first.ExpectedFormats.ShouldContain(FileFormat.Docx);

            var second = manifest.Oficios[1];
            second.CaseId.ShouldBe("333BBB-2025");
            second.ExpectedFormats.Count.ShouldBe(1);
            second.ExpectedFormats.ShouldContain(FileFormat.Pdf);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_ValidJson_EmptyOficiosList_ReturnsEmptyManifest()
    {
        const string json = """{ "oficios": [] }""";
        var path = await WriteManifestAsync(json);
        try
        {
            var sut = CreateSut(path);
            var result = await sut.LoadAsync(TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            result.Value!.Oficios.ShouldBeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Missing file → graceful failure
    // ------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_FileDoesNotExist_ReturnsFailure()
    {
        var sut = CreateSut(Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.json"));
        var result = await sut.LoadAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
    }

    // ------------------------------------------------------------------------------------------------
    // Blank / null path → graceful failure
    // ------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_BlankPath_ReturnsFailure()
    {
        var sut = CreateSut(string.Empty);
        var result = await sut.LoadAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_NullPath_ReturnsFailure()
    {
        var sut = CreateSut(null);
        var result = await sut.LoadAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
    }

    // ------------------------------------------------------------------------------------------------
    // Garbage / unparseable JSON → graceful failure
    // ------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_GarbageJson_ReturnsFailure()
    {
        var path = await WriteManifestAsync("NOT_VALID_JSON{{{");
        try
        {
            var sut = CreateSut(path);
            var result = await sut.LoadAsync(TestContext.Current.CancellationToken);

            result.IsFailure.ShouldBeTrue();
            result.Errors.ShouldNotBeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_EmptyFile_ReturnsFailure()
    {
        var path = await WriteManifestAsync(string.Empty);
        try
        {
            var sut = CreateSut(path);
            var result = await sut.LoadAsync(TestContext.Current.CancellationToken);

            result.IsFailure.ShouldBeTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Cancellation
    // ------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_WhenCancelled_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var sut = CreateSut("/any/path.json");
        var result = await sut.LoadAsync(cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }
}
