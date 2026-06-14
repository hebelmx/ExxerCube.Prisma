using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Orion.Ingestion;

namespace ExxerCube.Prisma.Orion.Ingestion.Tests;

/// <summary>
/// Unit tests for <see cref="FileIngestionJournal.TryGetStoredPathAsync"/>: validates that the journal
/// returns the originally-journaled stored path for a known (hash, url) pair and null for unknown pairs
/// or invalid inputs — covering the fix for GH issue #3 (cross-day-partition duplicate resolution).
/// </summary>
public sealed class FileIngestionJournalTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task TryGetStoredPath_AfterRecord_ReturnsOriginalStoredPath()
    {
        // Arrange: record an entry whose bytes were stored on an earlier day's partition.
        var journalFile = Path.GetTempFileName();
        try
        {
            var journal = new FileIngestionJournal(journalFile, NullLogger<FileIngestionJournal>.Instance);

            var hash = "aabbccdd1122334455667788aabbccdd1122334455667788aabbccdd11223344";
            var url = "https://siara.local/cases/CASE5/doc.xml";
            var expectedPath = "2026/06/10/CASE5/doc.xml";

            var entry = new IngestionManifestEntry(
                FileId: Guid.NewGuid(),
                FileName: "doc.xml",
                SourceUrl: url,
                ContentHash: hash,
                FileSizeBytes: 1024,
                StoredPath: expectedPath,
                CorrelationId: Guid.NewGuid(),
                DownloadedAt: DateTimeOffset.UtcNow);

            await journal.RecordAsync(entry, TestContext.Current.CancellationToken);

            // Act
            var result = await journal.TryGetStoredPathAsync(hash, url, TestContext.Current.CancellationToken);

            // Assert: must return the exact stored path recorded by RecordAsync
            result.ShouldBe(expectedPath);
        }
        finally
        {
            File.Delete(journalFile);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TryGetStoredPath_UnknownPair_ReturnsNull()
    {
        // Arrange: journal with no entries.
        var journalFile = Path.GetTempFileName();
        try
        {
            var journal = new FileIngestionJournal(journalFile, NullLogger<FileIngestionJournal>.Instance);

            // Act
            var result = await journal.TryGetStoredPathAsync(
                "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef",
                "https://siara.local/cases/UNKNOWN/doc.xml",
                TestContext.Current.CancellationToken);

            // Assert: unknown pair → null, no throw
            result.ShouldBeNull();
        }
        finally
        {
            File.Delete(journalFile);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TryGetStoredPath_NullOrWhitespaceHash_ReturnsNull()
    {
        // Arrange
        var journalFile = Path.GetTempFileName();
        try
        {
            var journal = new FileIngestionJournal(journalFile, NullLogger<FileIngestionJournal>.Instance);

            // Act — null hash
            var resultNullHash = await journal.TryGetStoredPathAsync(
                null!,
                "https://siara.local/cases/CASE5/doc.xml",
                TestContext.Current.CancellationToken);

            // Act — whitespace hash
            var resultWhitespaceHash = await journal.TryGetStoredPathAsync(
                "   ",
                "https://siara.local/cases/CASE5/doc.xml",
                TestContext.Current.CancellationToken);

            // Assert: must return null, no throw
            resultNullHash.ShouldBeNull();
            resultWhitespaceHash.ShouldBeNull();
        }
        finally
        {
            File.Delete(journalFile);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TryGetStoredPath_NullOrWhitespaceUrl_ReturnsNull()
    {
        // Arrange
        var journalFile = Path.GetTempFileName();
        try
        {
            var journal = new FileIngestionJournal(journalFile, NullLogger<FileIngestionJournal>.Instance);

            var hash = "aabbccdd1122334455667788aabbccdd1122334455667788aabbccdd11223344";

            // Act — null url
            var resultNullUrl = await journal.TryGetStoredPathAsync(
                hash,
                null!,
                TestContext.Current.CancellationToken);

            // Act — whitespace url
            var resultWhitespaceUrl = await journal.TryGetStoredPathAsync(
                hash,
                "  ",
                TestContext.Current.CancellationToken);

            // Assert: must return null, no throw
            resultNullUrl.ShouldBeNull();
            resultWhitespaceUrl.ShouldBeNull();
        }
        finally
        {
            File.Delete(journalFile);
        }
    }
}
