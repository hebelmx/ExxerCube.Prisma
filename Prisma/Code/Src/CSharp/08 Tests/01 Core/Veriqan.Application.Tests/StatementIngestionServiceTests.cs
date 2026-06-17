using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Services;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Repositories;
using IndQuestResults;
using IndQuestResults.Operations;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Application.Tests;

/// <summary>
/// Unit and integration tests for <see cref="StatementIngestionService"/> covering
/// the idempotent ingestion contract (FR-1, Story 2.1).
/// </summary>
public sealed class StatementIngestionServiceTests
{
    // -----------------------------------------------------------------------
    // Test fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// Minimal valid single-page PDF bytes. The document starts with the mandatory
    /// <c>%PDF-</c> magic header, which is the validation gate used by the service.
    /// </summary>
    private static readonly byte[] ValidPdfBytes = BuildMinimalPdf();

    /// <summary>Bytes that are clearly not a PDF (UTF-8 plain text).</summary>
    private static readonly byte[] NonPdfBytes = Encoding.UTF8.GetBytes("This is not a PDF document.");

    /// <summary>Builds a trivial but structurally minimal PDF byte array.</summary>
    private static byte[] BuildMinimalPdf()
    {
        // A minimal valid PDF-1.4 document that passes the %PDF- header check.
        const string pdfContent =
            "%PDF-1.4\n" +
            "1 0 obj<</Type /Catalog /Pages 2 0 R>>endobj\n" +
            "2 0 obj<</Type /Pages /Kids [3 0 R] /Count 1>>endobj\n" +
            "3 0 obj<</Type /Page /MediaBox [0 0 3 3]>>endobj\n" +
            "xref\n" +
            "0 4\n" +
            "0000000000 65535 f \n" +
            "0000000009 00000 n \n" +
            "0000000058 00000 n \n" +
            "0000000115 00000 n \n" +
            "trailer<</Size 4 /Root 1 0 R>>\n" +
            "startxref\n" +
            "190\n" +
            "%%EOF\n";
        return Encoding.ASCII.GetBytes(pdfContent);
    }

    /// <summary>
    /// Builds a second valid PDF byte array with different content so it produces
    /// a distinct SHA-256 hash from <see cref="ValidPdfBytes"/>.
    /// </summary>
    private static byte[] BuildSecondMinimalPdf()
    {
        const string pdfContent =
            "%PDF-1.4\n" +
            "% Different content to produce a distinct hash\n" +
            "1 0 obj<</Type /Catalog /Pages 2 0 R>>endobj\n" +
            "2 0 obj<</Type /Pages /Kids [3 0 R] /Count 1>>endobj\n" +
            "3 0 obj<</Type /Page /MediaBox [0 0 6 6]>>endobj\n" +
            "xref\n" +
            "0 4\n" +
            "0000000000 65535 f \n" +
            "0000000009 00000 n \n" +
            "0000000107 00000 n \n" +
            "0000000166 00000 n \n" +
            "trailer<</Size 4 /Root 1 0 R>>\n" +
            "startxref\n" +
            "243\n" +
            "%%EOF\n";
        return Encoding.ASCII.GetBytes(pdfContent);
    }

    // -----------------------------------------------------------------------
    // Helpers: build SUT wired to EF InMemory (integration-style)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a <see cref="VeriqanDbContext"/> backed by an EF Core InMemory database
    /// with a unique name per call to prevent test cross-contamination.
    /// </summary>
    private static VeriqanDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new VeriqanDbContext(options);
    }

    /// <summary>
    /// Builds the SUT (<see cref="StatementIngestionService"/>) with an EF InMemory
    /// repository and a controllable <see cref="FakeTimeProvider"/>.
    /// </summary>
    private static (StatementIngestionService Sut, FakeTimeProvider Clock, VeriqanDbContext Context)
        BuildSut(string? dbName = null)
    {
        var name = dbName ?? Guid.NewGuid().ToString("N");
        var context = CreateInMemoryContext(name);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));

        var repoLogger = XUnitLogger.CreateLogger<EfVerificationJobRepository>();
        var serviceLogger = XUnitLogger.CreateLogger<StatementIngestionService>();

        var repository = new EfVerificationJobRepository(context, repoLogger);
        var sut = new StatementIngestionService(repository, clock, serviceLogger);
        return (sut, clock, context);
    }

    // -----------------------------------------------------------------------
    // Test 1: New valid PDF → creates exactly one job with correct hash + status
    // -----------------------------------------------------------------------

    /// <summary>
    /// Ingesting a new valid PDF produces a single persisted VerificationJob
    /// with the correct hash, Pending status, and the clock's UTC timestamp.
    /// </summary>
    [Fact]
    public async Task Ingest_NewValidPdf_CreatesExactlyOneJob()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, clock, context) = BuildSut();
        var expectedTime = clock.GetUtcNow();

        // Act
        var result = await sut.IngestAsync(ValidPdfBytes, "statement.pdf", ct);

        // Assert: result is success
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();

        var job = result.Value!;
        job.Id.ShouldNotBe(Guid.Empty);
        job.ContentHash.ShouldNotBeNullOrWhiteSpace();
        job.ContentHash.Length.ShouldBe(64, "SHA-256 hex string should be 64 characters");
        job.Status.ShouldBe(VerificationJobStatus.Pending);
        job.ReceivedAtUtc.ShouldBe(expectedTime);

        // Assert: exactly one row was written to the store
        var rows = await context.VerificationJobs.CountAsync(ct);
        rows.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // Test 2: Same content twice → idempotent (no duplicate)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Ingesting the same bytes twice returns the same job both times.
    /// Exactly one row exists in the store after both calls.
    /// </summary>
    [Fact]
    public async Task Ingest_SameContentTwice_DoesNotDuplicate()
    {
        var ct = TestContext.Current.CancellationToken;
        var dbName = Guid.NewGuid().ToString("N");
        var (sut, _, context) = BuildSut(dbName);

        // Act — first ingest
        var first = await sut.IngestAsync(ValidPdfBytes, "statement.pdf", ct);
        first.IsSuccess.ShouldBeTrue();

        // Act — second ingest (same bytes, same hash)
        var second = await sut.IngestAsync(ValidPdfBytes, "statement_copy.pdf", ct);
        second.IsSuccess.ShouldBeTrue();

        // Both results must refer to the same job
        first.Value!.Id.ShouldBe(second.Value!.Id);
        first.Value!.ContentHash.ShouldBe(second.Value!.ContentHash);

        // Only one row in the store
        var rows = await context.VerificationJobs.CountAsync(ct);
        rows.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // Test 3: Distinct content → two distinct jobs
    // -----------------------------------------------------------------------

    /// <summary>
    /// Ingesting two PDF files with different content creates two distinct jobs.
    /// </summary>
    [Fact]
    public async Task Ingest_DistinctContent_CreatesTwoJobs()
    {
        var ct = TestContext.Current.CancellationToken;
        var dbName = Guid.NewGuid().ToString("N");
        var (sut, _, context) = BuildSut(dbName);
        var secondPdf = BuildSecondMinimalPdf();

        // Act
        var first = await sut.IngestAsync(ValidPdfBytes, "statement1.pdf", ct);
        var second = await sut.IngestAsync(secondPdf, "statement2.pdf", ct);

        // Assert: both succeed
        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();

        // Assert: distinct jobs (distinct IDs and hashes)
        first.Value!.Id.ShouldNotBe(second.Value!.Id);
        first.Value!.ContentHash.ShouldNotBe(second.Value!.ContentHash);

        // Assert: two rows persisted
        var rows = await context.VerificationJobs.CountAsync(ct);
        rows.ShouldBe(2);
    }

    // -----------------------------------------------------------------------
    // Test 4: Non-PDF / corrupt → failure result, no job created
    // -----------------------------------------------------------------------

    /// <summary>
    /// Non-PDF bytes are rejected with a failure result and no job is persisted.
    /// </summary>
    [Fact]
    public async Task Ingest_NonPdfBytes_ReturnsFailureAndNoJob()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, _, context) = BuildSut();

        // Act
        var result = await sut.IngestAsync(NonPdfBytes, "notapdf.txt", ct);

        // Assert: result is failure
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrWhiteSpace();
        result.Error!.ShouldContain("not a valid PDF", Case.Insensitive);

        // Assert: nothing persisted
        var rows = await context.VerificationJobs.CountAsync(ct);
        rows.ShouldBe(0);
    }

    /// <summary>
    /// Empty bytes are rejected with a failure result and no job is persisted.
    /// </summary>
    [Fact]
    public async Task Ingest_EmptyBytes_ReturnsFailureAndNoJob()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, _, context) = BuildSut();

        var result = await sut.IngestAsync([], "empty.pdf", ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrWhiteSpace();

        var rows = await context.VerificationJobs.CountAsync(ct);
        rows.ShouldBe(0);
    }

    // -----------------------------------------------------------------------
    // Test 5: Pre-cancelled token → cancelled result immediately
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the cancellation token is already signalled before the call,
    /// the service returns a cancelled result without performing any IO.
    /// </summary>
    [Fact]
    public async Task Ingest_Cancelled_ReturnsCancelledResult()
    {
        // Build a repo mock — we want to assert it was never called.
        var repoMock = Substitute.For<IVerificationJobRepository>();
        var clock = new FakeTimeProvider();
        var logger = XUnitLogger.CreateLogger<StatementIngestionService>();

        var sut = new StatementIngestionService(repoMock, clock, logger);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await sut.IngestAsync(ValidPdfBytes, "statement.pdf", cts.Token);

        // Assert: result is cancelled
        result.IsFailure.ShouldBeTrue();
        result.IsCancelled().ShouldBeTrue();

        // Assert: repository was never touched
        await repoMock.DidNotReceive().FindByContentHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await repoMock.DidNotReceive().AddAsync(Arg.Any<VerificationJob>(), Arg.Any<CancellationToken>());
    }
}
