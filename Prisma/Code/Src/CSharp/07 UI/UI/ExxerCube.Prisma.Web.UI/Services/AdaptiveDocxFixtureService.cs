using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Web.UI.Services;

/// <summary>
/// Service that loads existing DOCX fixtures from the file system and converts them to plain text
/// for use in adaptive extraction demos. Provides access to predefined test fixtures that represent
/// different document structures and personas for testing various extraction strategies.
/// </summary>
public sealed class AdaptiveDocxFixtureService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AdaptiveDocxFixtureService> _logger;

    /// <summary>
    /// Static collection of predefined DOCX fixtures available for adaptive extraction testing.
    /// Each fixture represents a different document type, structure, and persona scenario.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveDocxFixtureService"/> class.
    /// </summary>
    /// <param name="environment">The web host environment used to resolve fixture file paths.</param>
    /// <param name="logger">The logger instance for recording diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="environment"/> or <paramref name="logger"/> is null.</exception>
    public AdaptiveDocxFixtureService(
        IWebHostEnvironment environment,
        ILogger<AdaptiveDocxFixtureService> logger)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves the list of available DOCX fixtures for adaptive extraction testing.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains
    /// a read-only list of available fixtures, each representing a different document type and persona.</returns>
    public Task<IReadOnlyList<AdaptiveDocxFixture>> GetFixturesAsync() =>
        Task.FromResult(Fixtures);

    /// <summary>
    /// Loads a DOCX fixture by its key, extracts the plain text content, and returns the fixture data.
    /// </summary>
    /// <param name="key">The unique key identifying the fixture to load. Case-insensitive matching is used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains
    /// the loaded fixture content with extracted text, or null if the fixture was not found or could not be loaded.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
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

    /// <summary>
    /// Resolves the full file system path to a DOCX fixture file.
    /// </summary>
    /// <param name="fileName">The name of the fixture file to locate.</param>
    /// <returns>The absolute path to the fixture file in the PRP1 fixtures directory.</returns>
    /// <remarks>
    /// The fixture path is resolved relative to the ContentRootPath, navigating up to the Prisma root
    /// directory and then into the Fixtures/PRP1 subdirectory where DOCX fixtures are stored.
    /// </remarks>
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

    /// <summary>
    /// Extracts plain text content from a DOCX file by reading the Word document XML structure.
    /// </summary>
    /// <param name="filePath">The full path to the DOCX file to extract text from.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains
    /// the extracted plain text with normalized spacing, or an empty string if the document.xml entry is not found.</returns>
    /// <remarks>
    /// This method opens the DOCX file as a ZIP archive, locates the word/document.xml entry,
    /// parses the XML to extract text from paragraph elements, and normalizes whitespace.
    /// Each paragraph is joined with newlines to preserve document structure.
    /// </remarks>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
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

    /// <summary>
    /// Normalizes whitespace in a text string by collapsing multiple consecutive whitespace characters
    /// into a single space and trimming leading/trailing whitespace.
    /// </summary>
    /// <param name="value">The text string to normalize.</param>
    /// <returns>A normalized string with collapsed whitespace and trimmed edges.</returns>
    /// <remarks>
    /// This method uses a regular expression to replace any sequence of whitespace characters
    /// (spaces, tabs, newlines, etc.) with a single space, then trims the result.
    /// </remarks>
    private static string NormalizeSpacing(string value)
    {
        var normalized = Regex.Replace(value, @"\s+", " ").Trim();
        return normalized;
    }
}

/// <summary>
/// Represents a predefined DOCX fixture available for adaptive extraction testing.
/// Each fixture represents a specific document type, structure, and persona scenario.
/// </summary>
/// <param name="Key">The unique identifier key for the fixture, used for lookup operations. Case-insensitive.</param>
/// <param name="DisplayName">The human-readable display name shown in UI components.</param>
/// <param name="FileName">The name of the DOCX file in the fixtures directory.</param>
/// <param name="Description">A detailed description of the fixture's characteristics and use case.</param>
/// <param name="Persona">The persona or document type classification (e.g., "IMSS remit / structured labels").</param>
public sealed record AdaptiveDocxFixture(
    string Key,
    string DisplayName,
    string FileName,
    string Description,
    string Persona);

/// <summary>
/// Represents the loaded content of a DOCX fixture, including the fixture metadata,
/// extracted plain text, and the full file system path.
/// </summary>
/// <param name="Fixture">The fixture metadata describing the document type and characteristics.</param>
/// <param name="Text">The plain text content extracted from the DOCX file, with normalized spacing.</param>
/// <param name="FullPath">The absolute file system path to the source DOCX file.</param>
public sealed record DocxFixtureContent(
    AdaptiveDocxFixture Fixture,
    string Text,
    string FullPath);
