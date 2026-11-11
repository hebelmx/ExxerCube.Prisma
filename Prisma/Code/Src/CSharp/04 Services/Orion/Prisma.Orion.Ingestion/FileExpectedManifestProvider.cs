using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Services.Manifest;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// File-backed implementation of <see cref="IExpectedManifestProvider"/> that reads and parses the
/// operator-supplied expected-case manifest JSON file (Item B #8).
/// </summary>
/// <remarks>
/// <para>
/// Fault-tolerant: every failure path (blank path, file missing, parse error, I/O error) returns a
/// <see cref="Result{T}"/> failure with a descriptive error message so the caller can log a warning
/// and continue — the watch loop is never crashed by a manifest configuration error.
/// </para>
/// <para>
/// Expected JSON format:
/// <code>
/// {
///   "oficios": [
///     { "caseId": "222AAA-2025", "expectedFormats": ["Pdf", "Xml", "Docx"] }
///   ]
/// }
/// </code>
/// <c>expectedFormats</c> values are matched by name (case-insensitive) against <see cref="FileFormat"/>;
/// unrecognized names are silently mapped to <see cref="FileFormat.Unknown"/>.
/// </para>
/// </remarks>
public sealed class FileExpectedManifestProvider : IExpectedManifestProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ExpectedManifestOptions _options;
    private readonly ILogger<FileExpectedManifestProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="FileExpectedManifestProvider"/> class.</summary>
    /// <param name="options">The expected manifest options (path + enabled flag).</param>
    /// <param name="logger">The logger.</param>
    public FileExpectedManifestProvider(
        IOptions<ExpectedManifestOptions> options,
        ILogger<FileExpectedManifestProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ExpectedManifest>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<ExpectedManifest>();
        }

        var path = _options.ManifestPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result<ExpectedManifest>.WithFailure(
                "ExpectedManifest:ManifestPath is blank or null. Cannot load manifest.");
        }

        if (!File.Exists(path))
        {
            return Result<ExpectedManifest>.WithFailure(
                $"Expected manifest file not found at '{path}'.");
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Result<ExpectedManifest>.WithFailure(
                    $"Expected manifest file is empty: '{path}'.");
            }

            var dto = JsonSerializer.Deserialize<ManifestDto>(json, JsonOptions);
            if (dto is null)
            {
                return Result<ExpectedManifest>.WithFailure(
                    $"Expected manifest file deserialised to null at '{path}'.");
            }

            var oficios = (dto.Oficios ?? [])
                .Select(o => new ExpectedOficio(
                    CaseId: o.CaseId ?? string.Empty,
                    ExpectedFormats: ParseFormats(o.ExpectedFormats ?? [])))
                .ToList();

            _logger.LogDebug(
                "Loaded expected manifest from '{Path}': {Count} oficio(s)",
                path, oficios.Count);

            return Result<ExpectedManifest>.Success(new ExpectedManifest { Oficios = oficios });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Expected manifest JSON parse error at '{Path}'", path);
            return Result<ExpectedManifest>.WithFailure(
                $"Expected manifest JSON parse error at '{path}': {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Expected manifest I/O error reading '{Path}'", path);
            return Result<ExpectedManifest>.WithFailure(
                $"Expected manifest I/O error reading '{path}': {ex.Message}");
        }
    }

    private static IReadOnlyList<FileFormat> ParseFormats(IReadOnlyList<string> names)
    {
        var result = new List<FileFormat>(names.Count);
        foreach (var name in names)
        {
            try
            {
                result.Add(FileFormat.FromName(name));
            }
            catch
            {
                result.Add(FileFormat.Unknown);
            }
        }

        return result;
    }

    // ─── Private DTO types (deserialization only, never leave this class) ─────────────────────────

    private sealed class ManifestDto
    {
        [JsonPropertyName("oficios")]
        public IReadOnlyList<OficioDto>? Oficios { get; set; }
    }

    private sealed class OficioDto
    {
        [JsonPropertyName("caseId")]
        public string? CaseId { get; set; }

        [JsonPropertyName("expectedFormats")]
        public IReadOnlyList<string>? ExpectedFormats { get; set; }
    }
}
