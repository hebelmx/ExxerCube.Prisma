using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Orchestration.Batch;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Worker;
using IndQuestResults;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Endpoint-level integration tests for the Veriqan Worker <c>GET /exceptions?batchId=</c>
/// dead-letter lookup (VERIQAN-E3-S3, commit <c>368ec43c</c>).  Closes an adversarial-review
/// finding that the endpoint — added alongside <see cref="IBatchExceptionLogRepository"/> —
/// had zero test coverage of its own: prior tests exercised <see cref="BatchProcessor"/> and
/// <see cref="Orchestration.InMemory.InMemoryBatchExceptionLogRepository"/> directly
/// (<c>BatchExceptionLogPersistenceTests</c>), never the HTTP route, query-param parsing, or
/// auth gate.
/// </summary>
/// <remarks>
/// The Worker is hosted in-process via <see cref="WebApplicationFactory{TEntryPoint}"/>, mirroring
/// <see cref="VerifyEndpointTests"/>: <see cref="IVerificationPipeline"/> is replaced with an
/// NSubstitute double so no real OCR/extraction runs, while <see cref="IBatchProcessor"/> and
/// <see cref="IBatchExceptionLogRepository"/> stay wired to their real DI registrations
/// (<c>AddVeriqan</c>'s in-memory persistence branch — no SQL connection string is configured in
/// the test host, so <c>InMemoryBatchExceptionLogRepository</c> backs the dead-letter log).
/// The primary test below drives the literal spec flow — <c>POST /batch</c> with one
/// valid-shaped and one malformed statement, then <c>GET /exceptions?batchId=</c> — entirely
/// over HTTP, with "malformed" simulated by the pipeline double returning a failure
/// <c>Result</c> for that item (the same failure shape a real malformed-PDF parse produces per
/// <c>IVerificationPipeline.ProcessAsync</c>'s documented contract).  Real <see cref="BatchProcessor"/>
/// logic performs the dead-letter write; this test never touches the repository directly.
/// </remarks>
[Collection(MetricsIsolationCollection.Name)]
public sealed class ExceptionsEndpointTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static StatementSubmission MakeSubmission(string fileName, byte[] pdf) =>
        new(Pdf: pdf, FileName: fileName, ContextKey: new StatementContextKey("Test Institution"));

    private static VerificationOutcome BuildSuccessOutcome(Guid jobId)
    {
        var job = new VerificationJob(
            id: jobId,
            contentHash: "abc123",
            receivedAtUtc: DateTimeOffset.UtcNow,
            status: VerificationJobStatus.Completed);

        var aggregator = new VerdictAggregator();
        var findings = new List<RuleFinding>
        {
            RuleFinding.Pass(checkId: "CL-TEST-1", technique: TechniqueClass.Deterministic, engineVersion: "1.0"),
        };
        var summary = aggregator.Aggregate(findings).Value!;

        return new VerificationOutcome(Job: job, Summary: summary, Findings: findings);
    }

    /// <summary>
    /// Mirrors <c>VerifyEndpointTests.ComputeSha256Hex</c> so the test can independently predict
    /// the <see cref="BatchExceptionLogEntry.StatementHash"/> that <see cref="BatchProcessor"/>
    /// will persist for a given submission, without depending on its private implementation.
    /// </summary>
    private static string ComputeSha256Hex(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>
    /// Boots the Veriqan Worker with JWT auth disabled and the supplied <paramref name="pipeline"/>
    /// double registered as the <see cref="IVerificationPipeline"/> implementation. All other
    /// services — including <see cref="IBatchProcessor"/> and
    /// <see cref="IBatchExceptionLogRepository"/> — resolve through <c>AddVeriqan</c>'s real
    /// (in-memory, no connection string configured) registrations.
    /// </summary>
    private static WebApplicationFactory<Program> CreateHost(IVerificationPipeline pipeline) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Veriqan:Auth:Enabled", "false");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IVerificationPipeline>();
                services.AddScoped<IVerificationPipeline>(_ => pipeline);
            });
        });

    /// <summary>Response shape of <c>POST /batch</c> (anonymous object in Program.cs).</summary>
    private sealed record BatchSubmitResponse(Guid BatchId, int TotalSubmitted, int CompletedCount, int FailedCount);

    private static readonly JsonSerializerOptions CaseInsensitiveJson = new() { PropertyNameCaseInsensitive = true };

    // -----------------------------------------------------------------------
    // Literal spec AC — POST /batch (one valid + one malformed) → GET /exceptions
    // -----------------------------------------------------------------------

    /// <summary>
    /// Processes a batch containing one valid-shaped and one malformed statement via
    /// <c>POST /batch</c>, then queries <c>GET /exceptions?batchId=</c> for the returned
    /// <c>batchId</c> and asserts the malformed statement — and only the malformed
    /// statement — was dead-lettered and is retrievable over HTTP.
    /// </summary>
    [Fact]
    public async Task PostBatch_OneValidOneMalformed_MalformedIsDeadLetteredAndRetrievableViaExceptionsEndpoint()
    {
        // Arrange
        var validPdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34 }; // "%PDF-1.4" header, well-formed-shaped
        var malformedPdf = new byte[] { 0x00, 0x01, 0x02, 0x03 }; // garbage bytes — not a parseable PDF

        var validSubmission = MakeSubmission("valid-statement.pdf", validPdf);
        var malformedSubmission = MakeSubmission("malformed-statement.pdf", malformedPdf);

        var pipeline = Substitute.For<IVerificationPipeline>();
        pipeline
            .ProcessAsync(Arg.Is<StatementSubmission>(s => s.FileName == "valid-statement.pdf"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<VerificationOutcome>.WithSuccess(BuildSuccessOutcome(Guid.NewGuid()))));
        pipeline
            .ProcessAsync(Arg.Is<StatementSubmission>(s => s.FileName == "malformed-statement.pdf"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<VerificationOutcome>.WithFailure("Malformed PDF: could not parse document structure")));

        await using var factory = CreateHost(pipeline);
        using var client = factory.CreateClient();

        var batchRequest = new BatchSubmissionRequest(Items: [validSubmission, malformedSubmission]);

        // Act — submit the batch
        var postResponse = await client.PostAsJsonAsync("/batch", batchRequest, TestContext.Current.CancellationToken);
        postResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var submitBody = await postResponse.Content.ReadFromJsonAsync<BatchSubmitResponse>(
            CaseInsensitiveJson, TestContext.Current.CancellationToken);
        submitBody.ShouldNotBeNull();
        submitBody!.BatchId.ShouldNotBe(Guid.Empty);
        submitBody.TotalSubmitted.ShouldBe(2);
        submitBody.CompletedCount.ShouldBe(1, "only the valid-shaped item completes the pipeline.");
        submitBody.FailedCount.ShouldBe(1, "the malformed item must be counted as failed.");

        // Act — query the dead-letter log for this batch over HTTP
        var getResponse = await client.GetAsync($"/exceptions?batchId={submitBody.BatchId}", TestContext.Current.CancellationToken);

        // Assert — round trip succeeded and exactly the malformed item is dead-lettered
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var entries = await getResponse.Content.ReadFromJsonAsync<List<BatchExceptionLogEntry>>(
            CaseInsensitiveJson, TestContext.Current.CancellationToken);
        entries.ShouldNotBeNull();
        entries!.Count.ShouldBe(1, "exactly one item (the malformed statement) must be dead-lettered.");

        var deadLetter = entries[0];
        deadLetter.BatchId.ShouldBe(submitBody.BatchId);
        deadLetter.StatementHash.ShouldBe(ComputeSha256Hex(malformedPdf));
        deadLetter.InstitutionId.ShouldBe("Test Institution");
        deadLetter.FailureReason.ShouldBe("Malformed PDF: could not parse document structure");

        // Assert — the valid-shaped item's hash never appears in the dead-letter log
        entries.ShouldNotContain(e => e.StatementHash == ComputeSha256Hex(validPdf));

        // Assert — the pipeline double was invoked exactly once per item
        await pipeline.Received(1).ProcessAsync(
            Arg.Is<StatementSubmission>(s => s.FileName == "valid-statement.pdf"), Arg.Any<CancellationToken>());
        await pipeline.Received(1).ProcessAsync(
            Arg.Is<StatementSubmission>(s => s.FileName == "malformed-statement.pdf"), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // GET /exceptions — validation
    // -----------------------------------------------------------------------

    /// <summary>A request with no <c>batchId</c> query parameter must return HTTP 400.</summary>
    [Fact]
    public async Task GetExceptions_MissingBatchId_Returns400()
    {
        await using var factory = CreateHost(Substitute.For<IVerificationPipeline>());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/exceptions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>A <c>batchId</c> that is not a parseable GUID must return HTTP 400.</summary>
    [Fact]
    public async Task GetExceptions_GarbageBatchId_Returns400()
    {
        await using var factory = CreateHost(Substitute.For<IVerificationPipeline>());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/exceptions?batchId=not-a-guid", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A well-formed <c>batchId</c> for which no batch has ever run must return HTTP 200 with an
    /// empty list — never a failure — matching
    /// <see cref="IBatchExceptionLogRepository.GetByBatchIdAsync"/>'s documented contract.
    /// </summary>
    [Fact]
    public async Task GetExceptions_UnknownBatchId_Returns200EmptyList()
    {
        await using var factory = CreateHost(Substitute.For<IVerificationPipeline>());
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/exceptions?batchId={Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var entries = await response.Content.ReadFromJsonAsync<List<BatchExceptionLogEntry>>(
            CaseInsensitiveJson, TestContext.Current.CancellationToken);
        entries.ShouldNotBeNull();
        entries!.ShouldBeEmpty();
    }

    // -----------------------------------------------------------------------
    // GET /exceptions — auth enforcement
    // -----------------------------------------------------------------------

    /// <summary>
    /// With JWT auth ENABLED (the secure-by-default state), a request to <c>/exceptions</c>
    /// carrying no bearer token must be rejected with HTTP 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task GetExceptions_AuthEnabledNoToken_Returns401()
    {
        var pipeline = Substitute.For<IVerificationPipeline>();

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Veriqan:Auth:Enabled", "true");
            builder.UseSetting("Veriqan:Auth:Jwt:SigningKey", "test-only-signing-key-0123456789-abcdef");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IVerificationPipeline>();
                services.AddScoped<IVerificationPipeline>(_ => pipeline);
            });
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/exceptions?batchId={Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
