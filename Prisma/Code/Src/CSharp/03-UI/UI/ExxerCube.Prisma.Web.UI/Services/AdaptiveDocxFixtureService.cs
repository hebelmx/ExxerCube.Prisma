using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Web.UI.Services;

/// <summary>
/// Loads existing DOCX fixtures and converts them to plain text for adaptive extraction demos.
/// </summary>
public sealed class AdaptiveDocxFixtureService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AdaptiveDocxFixtureService> _logger;

    private static readonly IReadOnlyList<AdaptiveDocxFixture> Fixtures = new[]
    {
        new AdaptiveDocxFixture(
            Key: "222aaa",
            DisplayName: "222AAA — IMSS oficio (structured)",
            FileName: "222AAA-44444444442025.docx",
            Description: "Highly structured oficio with expediente + folio + plazo; good for structured confidence.",
            Persona: "IMSS remit / structured labels"),
        new AdaptiveDocxFixture(
            Key: "333bbb",
            DisplayName: "333BBB — Sonora SAT (narrative)",
            FileName: "333BBB-44444444442025.docx",
            Description: "Narrative request with embedded expediente and plazo; closer to contextual extraction.",
            Persona: "SAT narrative / mixed"),
        new AdaptiveDocxFixture(
            Key: "333ccc",
            DisplayName: "333ccc — UIF urgente (short, urgent)",
            FileName: "333ccc-6666666662025.docx",
            Description: "Shorter urgent oficio with minimal structure; good for complement mode.",
            Persona: "UIF urgent / compact"),
        new AdaptiveDocxFixture(
            Key: "555ccc",
            DisplayName: "555CCC — Degradado (stress test)",
            FileName: "555CCC-66666662025.docx",
            Description: "Degraded spacing/labels to stress regex tolerance and merging.",
            Persona: "Degraded / tolerance test")
    };

    public AdaptiveDocxFixtureService(
        IWebHostEnvironment environment,
        ILogger<AdaptiveDocxFixtureService> logger)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IReadOnlyList<AdaptiveDocxFixture>> GetFixturesAsync() =>
        Task.FromResult(Fixtures);

    public async Task<DocxFixtureContent?> LoadAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fixture = Fixtures.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
        if (fixture is null)
        {
            return null;
        }

        var fullPath = GetFixturePath(fixture.FileName);
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("AdaptiveDocxFixture: Fixture not found at {Path}", fullPath);
            return null;
        }

        try
        {
            var text = await ExtractPlainTextAsync(fullPath, cancellationToken);
            return new DocxFixtureContent(fixture, text, fullPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdaptiveDocxFixture: Failed to load fixture {Key}", key);
            return null;
        }
    }

    private string GetFixturePath(string fileName)
    {
        // ContentRootPath is .../Prisma/Code/Src/CSharp/03-UI/UI/ExxerCube.Prisma.Web.UI
        // Fixtures live under Prisma/Fixtures/PRP1/<file>
        return Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            "..", "..", "..", "..", "..", "..",
            "Fixtures",
            "PRP1",
            fileName));
    }

    private static async Task<string> ExtractPlainTextAsync(string filePath, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(filePath);
        var entry = archive.GetEntry("word/document.xml");
        if (entry is null)
        {
            return string.Empty;
        }

        await using var stream = entry.Open();
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        var w = (XNamespace)"http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        var paragraphs = document
            .Descendants(w + "p")
            .Select(p =>
            {
                var parts = p.Descendants(w + "t")
                    .Select(t => t.Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList();

                return parts.Count == 0
                    ? null
                    : NormalizeSpacing(string.Join(' ', parts));
            })
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        return string.Join(Environment.NewLine, paragraphs);
    }

    private static string NormalizeSpacing(string value)
    {
        var normalized = Regex.Replace(value, @"\s+", " ").Trim();
        return normalized;
    }
}

public sealed record AdaptiveDocxFixture(
    string Key,
    string DisplayName,
    string FileName,
    string Description,
    string Persona);

public sealed record DocxFixtureContent(
    AdaptiveDocxFixture Fixture,
    string Text,
    string FullPath);
