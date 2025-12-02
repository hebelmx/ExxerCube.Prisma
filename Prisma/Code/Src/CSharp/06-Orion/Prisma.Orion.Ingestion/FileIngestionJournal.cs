using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// File-based ingestion journal for hash-based idempotency tracking.
/// </summary>
/// <remarks>
/// Stores processed document hashes in a simple text file (one hash per line).
/// In-memory cache for performance. Thread-safe for concurrent access.
/// Production: Consider SQLite or database for better scalability and querying.
/// </remarks>
public sealed class FileIngestionJournal : IIngestionJournal
{
    private readonly string _journalFilePath;
    private readonly ILogger<FileIngestionJournal> _logger;
    private readonly ConcurrentDictionary<string, string> _processedHashes;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileIngestionJournal"/> class.
    /// </summary>
    /// <param name="journalFilePath">Path to the journal file (defaults to ./journal.txt).</param>
    /// <param name="logger">The logger.</param>
    public FileIngestionJournal(string? journalFilePath, ILogger<FileIngestionJournal> logger)
    {
        _journalFilePath = journalFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), "journal.txt");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processedHashes = new ConcurrentDictionary<string, string>();

        LoadJournalAsync().GetAwaiter().GetResult(); // Load existing hashes on startup
    }

    /// <inheritdoc />
    public Task<bool> IsDuplicateAsync(string hash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            _logger.LogWarning("Cannot check duplicate for null/empty hash");
            return Task.FromResult(false);
        }

        var isDuplicate = _processedHashes.ContainsKey(hash);
        return Task.FromResult(isDuplicate);
    }

    /// <inheritdoc />
    public async Task RecordAsync(string hash, string storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            _logger.LogWarning("Cannot record null/empty hash");
            return;
        }

        // Add to in-memory cache
        if (_processedHashes.TryAdd(hash, storagePath))
        {
            _logger.LogDebug("Hash added to journal cache: {Hash}", hash);

            // Persist to file
            await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Append hash to journal file (format: hash|storagePath)
                var journalEntry = $"{hash}|{storagePath}{Environment.NewLine}";
                await File.AppendAllTextAsync(_journalFilePath, journalEntry, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Hash persisted to journal file: {Hash}", hash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist hash to journal file: {Hash}", hash);
                // Remove from cache since persistence failed
                _processedHashes.TryRemove(hash, out _);
                throw;
            }
            finally
            {
                _fileLock.Release();
            }
        }
        else
        {
            _logger.LogDebug("Hash already exists in journal: {Hash}", hash);
        }
    }

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
                var parts = line.Split('|', 2);
                if (parts.Length >= 1)
                {
                    var hash = parts[0];
                    var storagePath = parts.Length > 1 ? parts[1] : string.Empty;
                    _processedHashes.TryAdd(hash, storagePath);
                }
            }

            _logger.LogInformation("Loaded {Count} hashes from journal file", _processedHashes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load journal file: {JournalPath}", _journalFilePath);
            throw;
        }
    }
}
