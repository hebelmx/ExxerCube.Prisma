// <copyright file="ReportWriterTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using System.Text.Json;
using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Provisioning;
using ExxerCube.Prisma.QaHarness.Reporting;
using ExxerCube.Prisma.QaHarness.Traceability;
using ExxerCube.Prisma.QaHarness.Validators;
using ExxerCube.Prisma.QaHarness.Workflows;

namespace ExxerCube.Prisma.QaHarness.Tests;

/// <summary>
/// Fast unit tests for <see cref="MarkdownReportWriter"/>, <see cref="HtmlReportWriter"/>,
/// and <see cref="JsonReportWriter"/>. No Docker required.
/// </summary>
public sealed class ReportWriterTests
{
    // -----------------------------------------------------------------------
    // Shared fixture builder
    // -----------------------------------------------------------------------

    private static HarnessRunSummary BuildMinimalSummary() =>
        new(
            RunId: "test-run-001",
            ProductVersion: "1.0.0-test",
            StartedAt: DateTimeOffset.UtcNow.AddSeconds(-30),
            FinishedAt: DateTimeOffset.UtcNow,
            ProvisioningResult: new EnvironmentProvisioningResult(
                SqlConnectionString: null,
                OllamaEndpoint: null,
                CorpusPath: @"Prisma\Code\Fixtures",
                CorpusStatus: CorpusStatus.RestoredFromFixtures,
                DockerAvailable: false,
                CapabilityStatuses: [
                    new CapabilityStatus("Docker", false, "Docker not available in fast test"),
                    new CapabilityStatus("Corpus", true, "Restored from static Fixtures"),
                ]),
            WorkflowResults: [
                new WorkflowResult(
                    WorkflowName: "HealthCheckWorkflow",
                    StartedAt: DateTimeOffset.UtcNow.AddSeconds(-20),
                    FinishedAt: DateTimeOffset.UtcNow.AddSeconds(-10),
                    Status: WorkflowStatus.Skipped,
                    AbortReason: "Docker unavailable",
                    Evidence: new EvidencePackage("test-run-001", []),
                    Outputs: new Dictionary<string, object?>()),
            ],
            ValidationResults: [
                new ValidationResult(
                    ValidatorId: "SiroXmlStructureValidator",
                    IsConformant: true,
                    Findings: []),
            ],
            Evidence: new EvidencePackage("test-run-001", []),
            TraceabilityMap: BuildTraceabilityMap());

    private static ITraceabilityMap BuildTraceabilityMap()
    {
        var map = new TraceabilityMap();
        map.AddEntry(new TraceabilityEntry(
            "HealthCheckWorkflow",
            TraceabilitySourceKind.Workflow,
            [new RequirementRef("REQ-H1", "Health endpoint", "§Health")],
            [new FeatureRef("F-HEALTH", "Health Checks")],
            [new InvariantRef("INV-001", "/health/live must return 200")],
            DateTimeOffset.UtcNow));
        return map;
    }

    // -----------------------------------------------------------------------
    // MarkdownReportWriter
    // -----------------------------------------------------------------------

    /// <summary>
    /// The Markdown report must contain all required section headers defined in §3.7 of the
    /// architecture document.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task MarkdownReportWriter_Write_ProducesRequiredSections()
    {
        var writer = new MarkdownReportWriter();
        var summary = BuildMinimalSummary();
        var outputPath = Path.Combine(Path.GetTempPath(), $"report-{Guid.NewGuid():N}.md");

        try
        {
            var result = await writer.WriteAsync(
                summary, outputPath, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue(result.Error ?? "WriteAsync failed");
            result.Value.ShouldBe(outputPath);
            File.Exists(outputPath).ShouldBeTrue();

            var content = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);

            // Required section headers per architecture §3.7
            content.ShouldContain("## §1 Executive Summary");
            content.ShouldContain("## §2 Environment & Capability Status");
            content.ShouldContain("## §3 Requirement Traceability");
            content.ShouldContain("## §4 Workflow Results");
            content.ShouldContain("## §5 Findings");
            content.ShouldContain("## §6 Human-Review Queue");
            content.ShouldContain("## §7 Evidence Inventory");
            content.ShouldContain("## §8 Coverage Assessment");
            content.ShouldContain("## §9 Risk Register");
            content.ShouldContain("## §10 Deployment Recommendation");
        }
        finally
        {
            try { File.Delete(outputPath); } catch { /* cleanup */ }
        }
    }

    /// <summary>
    /// The Markdown report must NOT contain hard PASS/FAIL verdicts — it must remain
    /// data-only.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void MarkdownReportWriter_Render_DoesNotEmitVerdicts()
    {
        var summary = BuildMinimalSummary();
        var content = MarkdownReportWriter.Render(summary);

        // These verdict words must not appear as definitive statements.
        // The report may mention "QA agent to assess" but not render PASS/FAIL.
        content.ShouldNotContain("**PASS**");
        content.ShouldNotContain("**FAIL**");
    }

    /// <summary>
    /// The report must include the Run ID and Product Version in the output.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void MarkdownReportWriter_Render_IncludesRunMetadata()
    {
        var summary = BuildMinimalSummary();
        var content = MarkdownReportWriter.Render(summary);

        content.ShouldContain("test-run-001");
        content.ShouldContain("1.0.0-test");
    }

    // -----------------------------------------------------------------------
    // HtmlReportWriter
    // -----------------------------------------------------------------------

    /// <summary>
    /// The HTML report must be valid HTML (contains expected structural tags) and
    /// embed the Run ID.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task HtmlReportWriter_Write_ProducesValidHtmlWithRunId()
    {
        var writer = new HtmlReportWriter();
        var summary = BuildMinimalSummary();
        var outputPath = Path.Combine(Path.GetTempPath(), $"report-{Guid.NewGuid():N}.html");

        try
        {
            var result = await writer.WriteAsync(
                summary, outputPath, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue(result.Error ?? "WriteAsync failed");

            var content = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
            content.ShouldContain("<!DOCTYPE html>");
            content.ShouldContain("<html");
            content.ShouldContain("</html>");
            content.ShouldContain("test-run-001");
        }
        finally
        {
            try { File.Delete(outputPath); } catch { /* cleanup */ }
        }
    }

    // -----------------------------------------------------------------------
    // JsonReportWriter
    // -----------------------------------------------------------------------

    /// <summary>
    /// The JSON report must be parseable by <see cref="JsonDocument.Parse"/> and contain
    /// the expected top-level properties.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task JsonReportWriter_Write_IsValidJson()
    {
        var writer = new JsonReportWriter();
        var summary = BuildMinimalSummary();
        var outputPath = Path.Combine(Path.GetTempPath(), $"report-{Guid.NewGuid():N}.json");

        try
        {
            var result = await writer.WriteAsync(
                summary, outputPath, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue(result.Error ?? "WriteAsync failed");

            var json = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
            json.ShouldNotBeNullOrWhiteSpace();

            using var doc = JsonDocument.Parse(json);
            doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);

            // Top-level properties expected
            doc.RootElement.TryGetProperty("RunId", out var runId).ShouldBeTrue();
            runId.GetString().ShouldBe("test-run-001");

            doc.RootElement.TryGetProperty("ProductVersion", out _).ShouldBeTrue();
            doc.RootElement.TryGetProperty("WorkflowResults", out var workflows).ShouldBeTrue();
            workflows.ValueKind.ShouldBe(JsonValueKind.Array);
            workflows.GetArrayLength().ShouldBe(1);
        }
        finally
        {
            try { File.Delete(outputPath); } catch { /* cleanup */ }
        }
    }

    /// <summary>
    /// Writing to an invalid path should return a failure result, not throw.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task MarkdownReportWriter_Write_InvalidPath_ReturnsFailure()
    {
        var writer = new MarkdownReportWriter();
        var summary = BuildMinimalSummary();

        // Use a path that can't be created (null chars not allowed in Windows paths).
        var invalidPath = Path.Combine(Path.GetTempPath(), "\0invalid\0.md");

        var result = await writer.WriteAsync(
            summary, invalidPath, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
    }
}
