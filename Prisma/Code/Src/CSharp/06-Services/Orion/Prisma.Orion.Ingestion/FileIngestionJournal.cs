using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// File-based ingestion journal for manifest-based idempotency tracking.
/// </summary>
/// <remarks>
/// Stores ingestion manifest entries in JSON format (one entry per line).
/// In-memory cache for performance. Thread-safe for concurrent access.
/// Production: Consider SQLite or database for better scalability and querying.
/// </remarks>
public sealed class FileIngestionJournal : IIngestionJournal
{
    private readonly string _journalFilePath;
    private readonly ILogger<FileIngestionJournal> _logger;
    private readonly ConcurrentDictionary<string, IngestionManifestEntry> _manifestEntries;
    private readonly ConcurrentDictionary<Guid, IngestionManifestEntry> _entriesByFileId;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileIngestionJournal"/> class.
    /// </summary>
    /// <param name="journalFilePath">Path to the journal file (defaults to ./journal.txt).</param>
    /// <param name="logger">The logger.</param>
    public FileIngestionJournal(string? journalFilePath, ILogger<FileIngestionJournal> logger)
    {
        _journalFilePath = journalFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), "ingestion-journal.jsonl");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _manifestEntries = new ConcurrentDictionary<string, IngestionManifestEntry>();
        _entriesByFileId = new ConcurrentDictionary<Guid, IngestionManifestEntry>();

        LoadJournalAsync().GetAwaiter().GetResult(); // Load existing entries on startup
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(
        string contentHash,
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentHash) || string.IsNullOrWhiteSpace(sourceUrl))
        {
            _logger.LogWarning("Cannot check existence for null/empty hash or URL");
            return Task.FromResult(false);
        }

        var key = GetKey(contentHash, sourceUrl);
        var exists = _manifestEntries.ContainsKey(key);
        return Task.FromResult(exists);
    }

    /// <inheritdoc />
    public async Task RecordAsync(IngestionManifestEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry == null)
        {
            _logger.LogWarning("Cannot record null entry");
            return;
        }

        var key = GetKey(entry.ContentHash, entry.SourceUrl);

        // Add to in-memory caches (idempotent - no-op if already exists)
        if (_manifestEntries.TryAdd(key, entry))
        {
            _entriesByFileId.TryAdd(entry.FileId, entry);
            _logger.LogDebug("Manifest entry added to journal cache: {FileId}, Hash: {Hash}",
                entry.FileId, entry.ContentHash);

            // Persist to file
            await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Append entry to journal file (JSONL format - one JSON object per line)
                var json = JsonSerializer.Serialize(entry);
                var journalLine = $"{json}{Environment.NewLine}";
                await File.AppendAllTextAsync(_journalFilePath, journalLine, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Manifest entry persisted to journal file: {FileId}", entry.FileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist entry to journal file: {FileId}", entry.FileId);
                // Remove from caches since persistence failed
                _manifestEntries.TryRemove(key, out _);
                _entriesByFileId.TryRemove(entry.FileId, out _);
                throw;
            }
            finally
            {
                _fileLock.Release();
            }
        }
        else
        {
            _logger.LogDebug("Entry already exists in journal: {FileId}, Hash: {Hash}",
                entry.FileId, entry.ContentHash);
        }
    }

    /// <inheritdoc />
    public Task<IngestionManifestEntry?> GetByFileIdAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        _entriesByFileId.TryGetValue(fileId, out var entry);
        return Task.FromResult(entry);
    }

    private static string GetKey(string contentHash, string sourceUrl) =>
        $"{contentHash}|{sourceUrl}";

    private async Task LoadJournalAsync()
    {
        if (!File.Exists(_journalFilePath))
        {
            _logger.LogInformation("Journal file does not exist, will be created on first write: {JournalPath}", _journalFilePath);
            return;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(_journalFilePath).ConfigureAwait(false);
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<IngestionManifestEntry>(line);
                    if (entry != null)
                    {
                        var key = GetKey(entry.ContentHash, entry.SourceUrl);
                        _manifestEntries.TryAdd(key, entry);
                        _entriesByFileId.TryAdd(entry.FileId, entry);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize journal entry, skipping: {Line}", line);
                }
            }

            _logger.LogInformation("Loaded {Count} manifest entries from journal file", _manifestEntries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load journal file: {JournalPath}", _journalFilePath);
            throw;
        }
    }
}