using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
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
/// Integration tests for the Veriqan Worker <c>POST /verify</c> and <c>POST /batch</c> endpoints.
/// The Worker is hosted in-process via <see cref="WebApplicationFactory{TEntryPoint}"/>
/// with <see cref="IVerificationPipeline"/> replaced by an NSubstitute double —
/// no database or external I/O is required.
/// </summary>
public sealed class VerifyEndpointTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal <see cref="StatementSubmission"/> whose PDF bytes are a
    /// stand-in (single zero byte) — the pipeline substitute ignores the content.
    /// </summary>
    private static StatementSubmission BuildSubmission() =>
        new StatementSubmission(
            Pdf: [0x00],
            FileName: "test.pdf",
            ContextKey: new StatementContextKey("Test Institution"));

    /// <summary>
    /// Creates a <see cref="VerificationOutcome"/> that carries a known <paramref name="jobId"/>
    /// and a Green verdict, suitable for the pipeline double to return.
    /// </summary>
    private static VerificationOutcome BuildSuccessOutcome(Guid jobId)
    {
        var job = new VerificationJob(
            id: jobId,
            contentHash: "abc123",
            receivedAtUtc: DateTimeOffset.UtcNow,
            status: VerificationJobStatus.Completed);

        // Build a green VerdictSummary via the public VerdictAggregator (VerdictSummary ctor is internal).
        var aggregator = new VerdictAggregator();
        var findings = new List<RuleFinding>
        {
            RuleFinding.Pass(checkId: "CL-TEST-1", technique: TechniqueClass.Deterministic, engineVersion: "1.0"),
        };
        var summaryResult = aggregator.Aggregate(findings);
        var summary = summaryResult.Value!;

        return new VerificationOutcome(
            Job: job,
            Summary: summary,
            Findings: findings);
    }

    // -----------------------------------------------------------------------
    // Test host factory
    // -----------------------------------------------------------------------

    /// <summary>
    /// Boots the Veriqan Worker with the supplied <paramref name="pipeline"/> double registered
    /// as the <see cref="IVerificationPipeline"/> implementation.
    /// </summary>
    private static WebApplicationFactory<Program> CreateHost(IVerificationPipeline pipeline) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // JWT auth is enabled by default (secure-by-default). These endpoint-behavior
            // tests exercise validation/routing, not the auth gate, so disable it in the
            // test host via the documented escape hatch. The enforced-auth contract
            // (401 without a token) is covered separately by PostVerify_AuthEnabledNoToken_Returns401.
            builder.UseSetting("Veriqan:Auth:Enabled", "false");
            builder.ConfigureServices(services =>
            {
                // Remove the real scoped pipeline registered by AddVeriqan() and replace
                // with the NSubstitute double so no real infrastructure is exercised.
                services.RemoveAll<IVerificationPipeline>();
                services.AddScoped<IVerificationPipeline>(_ => pipeline);
            });
        });

    // -----------------------------------------------------------------------
    // POST /verify — happy path
    // -----------------------------------------------------------------------

    /// <summary>
    /// A valid <see cref="StatementSubmission"/> posted to <c>/verify</c> must return
    /// HTTP 202 Accepted and the pipeline substitute must have been called exactly once.
    /// </summary>
    [Fact]
    public async Task PostVerify_ValidSubmission_Returns202AndInvokesPipeline()
    {
        // Arrange
        var knownJobId = Guid.NewGuid();
        var successOutcome = BuildSuccessOutcome(knownJobId);

        var pipeline = Substitute.For<IVerificationPipeline>();
        pipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<VerificationOutcome>.WithSuccess(successOutcome)));

        await using var factory = CreateHost(pipeline);
        using var client = factory.CreateClient();

        var submission = BuildSubmission();

        // Act
        var response = await client.PostAsJsonAsync("/verify", submission, TestContext.Current.CancellationToken);

        // Assert — HTTP status
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Assert — body contains the job GUID
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain(knownJobId.ToString());

        // Assert — pipeline was called exactly once
        await pipeline.Received(1).ProcessAsync(
            Arg.Any<StatementSubmission>(),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // POST /verify — failure path
    // -----------------------------------------------------------------------

    /// <summary>
    /// When <see cref="IVerificationPipeline.ProcessAsync"/> returns a failure <c>Result</c>,
    /// <c>POST /verify</c> must return HTTP 422 Unprocessable Entity.
    /// </summary>
    [Fact]
    public async Task PostVerify_PipelineFailure_Returns422()
    {
        // Arrange
        var pipeline = Substitute.For<IVerificationPipeline>();
        pipeline
            .ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<VerificationOutcome>.WithFailure("Simulated pipeline error")));

        await using var factory = CreateHost(pipeline);
        using var client = factory.CreateClient();

        var submission = BuildSubmission();

        // Act
        var response = await client.PostAsJsonAsync("/verify", submission, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // -----------------------------------------------------------------------
    // POST /verify — auth enforcement (security contract, #17)
    // -----------------------------------------------------------------------

    /// <summary>
    /// With JWT auth ENABLED (the secure-by-default state), a request to <c>/verify</c>
    /// carrying no bearer token must be rejected with HTTP 401 Unauthorized, and the
    /// pipeline must never be invoked.
    /// </summary>
    [Fact]
    public async Task PostVerify_AuthEnabledNoToken_Returns401()
    {
        // Arrange — auth enabled with a placeholder signing key so the bearer handler
        // is fully configured; the request deliberately carries no token.
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

        // Act
        var response = await client.PostAsJsonAsync("/verify", BuildSubmission(), TestContext.Current.CancellationToken);

        // Assert — rejected at the auth gate, before the pipeline.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await pipeline.DidNotReceive().ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>());
    }
}
