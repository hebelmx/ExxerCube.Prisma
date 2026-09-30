// <copyright file="MarkdownReportWriter.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using System.Text;
using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Provisioning;
using ExxerCube.Prisma.QaHarness.Validators;
using ExxerCube.Prisma.QaHarness.Workflows;

namespace ExxerCube.Prisma.QaHarness.Reporting;

/// <summary>
/// Produces a structured Markdown report from a <see cref="HarnessRunSummary"/>.
/// </summary>
/// <remarks>
/// <para>
/// The report scaffolds the Phase-2 report sections required by the architecture:
/// executive summary, requirement/feature/invariant tables, findings, human-review queue,
/// coverage, risk, and a deployment recommendation placeholder.
/// </para>
/// <para>
/// <b>Critical constraint:</b> the report emits NO pass/fail verdicts. It records observed
/// data and structures it for an independent QA agent to interpret. All judgements are
/// explicitly deferred with "QA agent to assess" annotations.
/// </para>
/// <para>
/// Follows the repo-root locator + never-throw write pattern of
/// <c>CalibrationReportRenderer</c>.
/// </para>
/// </remarks>
public sealed class MarkdownReportWriter : IReportWriter
{
    /// <inheritdoc/>
    public string Format => "markdown";

    /// <inheritdoc/>
    public async Task<Result<string>> WriteAsync(
        HarnessRunSummary summary,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Result<string>.WithFailure("WriteAsync was cancelled.");

        ArgumentNullException.ThrowIfNull(summary);
        if (string.IsNullOrWhiteSpace(outputPath))
            return Result<string>.WithFailure("outputPath must not be null or empty.");

        try
        {
            var markdown = Render(summary);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(outputPath, markdown, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);

            return Result<string>.WithSuccess(outputPath);
        }
        catch (OperationCanceledException)
        {
            return Result<string>.WithFailure("Report write was cancelled.");
        }
        catch (Exception ex)
        {
            return Result<string>.WithFailure($"Failed to write Markdown report: {ex.Message}");
        }
    }

    // -----------------------------------------------------------------------
    // Rendering
    // -----------------------------------------------------------------------

    /// <summary>
    /// Renders a <see cref="HarnessRunSummary"/> to a Markdown string without writing to disk.
    /// Public to allow direct use in tests and downstream report pipelines.
    /// </summary>
    /// <param name="summary">The harness run summary to render.</param>
    /// <returns>The rendered Markdown document as a string.</returns>
    public static string Render(HarnessRunSummary summary)
    {
        var sb = new StringBuilder();
        var durationMs = (summary.FinishedAt - summary.StartedAt).TotalMilliseconds;

        // -----------------------------------------------------------------------
        // Header + metadata
        // -----------------------------------------------------------------------
        sb.AppendLine("# QA Harness Run Report");
        sb.AppendLine();
        sb.AppendLine($"**Run ID:** `{summary.RunId}`  ");
        sb.AppendLine($"**Product Version:** {summary.ProductVersion}  ");
        sb.AppendLine($"**Started:** {summary.StartedAt:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Finished:** {summary.FinishedAt:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Duration:** {durationMs:N0} ms  ");
        sb.AppendLine();
        sb.AppendLine("> **Note:** This report records **observed data only**. No pass/fail verdicts are");
        sb.AppendLine("> rendered by the harness. A separate QA agent interprets the findings below.");
        sb.AppendLine();

        // -----------------------------------------------------------------------
        // §1 Executive Summary
        // -----------------------------------------------------------------------
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## §1 Executive Summary");
        sb.AppendLine();

        var workflowCount = summary.WorkflowResults.Count;
        var completedCount = summary.WorkflowResults.Count(w => w.Status == WorkflowStatus.Completed);
        var abortedCount = summary.WorkflowResults.Count(w => w.Status == WorkflowStatus.Aborted);
        var skippedCount = summary.WorkflowResults.Count(w => w.Status == WorkflowStatus.Skipped);
        var validatorCount = summary.ValidationResults.Count;
        var evidenceCount = summary.Evidence.Items.Count;

        sb.AppendLine($"| Metric | Value |");
        sb.AppendLine($"|--------|-------|");
        sb.AppendLine($"| Workflows executed | {workflowCount} |");
        sb.AppendLine($"| Completed | {completedCount} |");
        sb.AppendLine($"| Aborted | {abortedCount} |");
        sb.AppendLine($"| Skipped | {skippedCount} |");
        sb.AppendLine($"| Validator runs | {validatorCount} |");
        sb.AppendLine($"| Evidence items | {evidenceCount} |");
        sb.AppendLine($"| Docker available | {summary.ProvisioningResult.DockerAvailable} |");
        sb.AppendLine($"| Corpus status | {summary.ProvisioningResult.CorpusStatus} |");
        sb.AppendLine();

        // -----------------------------------------------------------------------
        // §2 Environment & Capability Status
        // -----------------------------------------------------------------------
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## §2 Environment & Capability Status");
        sb.AppendLine();
        sb.AppendLine("| Capability | Available | Detail |");
        sb.AppendLine("|------------|-----------|--------|");
        foreach (var cap in summary.ProvisioningResult.CapabilityStatuses)
        {
            var avail = cap.Available ? "YES" : "NO";
            var detail = cap.Detail ?? string.Empty;
            sb.AppendLine($"| {cap.Capability} | {avail} | {detail} |");
        }

        sb.AppendLine();
        if (summary.ProvisioningResult.SqlConnectionString is not null)
            sb.AppendLine($"- **SQL connection:** `{MaskConnectionString(summary.ProvisioningResult.SqlConnectionString)}`");
        sb.AppendLine($"- **Corpus path:** `{summary.ProvisioningResult.CorpusPath}`");
        sb.AppendLine();

        // -----------------------------------------------------------------------
        // §3 Requirement Traceability
        // -----------------------------------------------------------------------
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## §3 Requirement Traceability");
        sb.AppendLine();

        var allEntries = summary.TraceabilityMap.GetAllEntries();
        if (allEntries.Count == 0)
        {
            sb.AppendLine("_No traceability entries recorded during this run._");
        }
        else
        {
            sb.AppendLine("### Requirements Exercised");
            sb.AppendLine();
            sb.AppendLine("| Requirement ID | Title | Section | Source |");
            sb.AppendLine("|---------------|-------|---------|--------|");

            var reqs = allEntries
                .SelectMany(e => e.Requirements.Select(r => (Req: r, Source: e.SourceId)))
                .GroupBy(x => x.Req.Id)
                .OrderBy(g => g.Key);

            foreach (var g in reqs)
            {
                var first = g.First();
                var sources = string.Join(", ", g.Select(x => x.Source).Distinct());
                sb.AppendLine($"| {first.Req.Id} | {first.Req.Title} | {first.Req.Section ?? "—"} | {sources} |");
            }

            sb.AppendLine();
            sb.AppendLine("### Features Exercised");
            sb.AppendLine();
            sb.AppendLine("| Feature ID | Name | Source |");
            sb.AppendLine("|------------|------|--------|");

            var features = allEntries
                .SelectMany(e => e.Features.Select(f => (Feat: f, Source: e.SourceId)))
                .GroupBy(x => x.Feat.Id)
                .OrderBy(g => g.Key);

            foreach (var g in features)
            {
                var first = g.First();
                var sources = string.Join(", ", g.Select(x => x.Source).Distinct());
                sb.AppendLine($"| {first.Feat.Id} | {first.Feat.Name} | {sources} |");
            }

            sb.AppendLine();
            sb.AppendLine("### Invariants Verified");
            sb.AppendLine();
            sb.AppendLine("| Invariant ID | Description | Source |");
            sb.AppendLine("|-------------|-------------|--------|");

            var invariants = allEntries
                .SelectMany(e => e.Invariants.Select(i => (Inv: i, Source: e.SourceId)))
                .GroupBy(x => x.Inv.Id)
                .OrderBy(g => g.Key);

            foreach (var g in invariants)
            {
                var first = g.First();
                var sources = string.Join(", ", g.Select(x => x.Source).Distinct());
                sb.AppendLine($"| {first.Inv.Id} | {first.Inv.Description} | {sources} |");
            }
        }

        sb.AppendLine();

        // -----------------------------------------------------------------------
        // §4 Workflow Results
        // -----------------------------------------------------------------------
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## §4 Workflow Results");
        sb.AppendLine();

        if (summary.WorkflowResults.Count == 0)
        {
            sb.AppendLine("_No workflows were executed in this run._");
        }
        else
        {
            sb.AppendLine("| Workflow | Status | Duration (ms) | Abort Reason | Evidence Items |");
            sb.AppendLine("|----------|--------|--------------|--------------|----------------|");

            foreach (var wr in summary.WorkflowResults)
            {
                var durationWf = (wr.FinishedAt - wr.StartedAt).TotalMilliseconds;
                var abort = wr.AbortReason ?? "—";
                sb.AppendLine(
                    $"| {wr.WorkflowName} | {wr.Status} | {durationWf:N0} | {abort} | {wr.Evidence.Items.Count} |");
            }
        }

        sb.AppendLine();

        // -----------------------------------------------------------------------
        // §5 Findings (Validation Results)
        // -----------------------------------------------------------------------
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## §5 Findings");
        sb.AppendLine();
        sb.AppendLine("> Findings are raw observations. Severity does not imply a QA verdict.");
        sb.AppendLine();

        if (summary.ValidationResults.Count == 0)
        {
            sb.AppendLine("_No domain validator results were collected in this run._");
        }
        else
        {
            foreach (var vr in summary.ValidationResults)
            {
                sb.AppendLine($"### Validator: `{vr.ValidatorId}`");
                sb.AppendLine();
                sb.AppendLine($"- **IsConformant (validator-reported):** {vr.IsConformant}");
                sb.AppendLine($"- **Finding count:** {vr.Findings.Count}");
                sb.AppendLine();

                if (vr.Findings.Count > 0)
                {
                    sb.AppendLine("| Rule ID | Severity | Description | Observed | Expected |");
                    sb.AppendLine("|---------|----------|-------------|----------|----------|");

                    foreach (var f in vr.Findings)
                    {
                        sb.AppendLine(
                            $"| {f.RuleId} | {f.Severity} | {f.Description} | {f.Observed ?? "—"} | {f.Expected ?? "—"} |");
                    }

                    sb.AppendLine();
                }
            }
        }

        // -----------------------------------------------------------------------
        // §6 Human-Review Queue
        // -----------------------------------------------------------------------
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## §6 Human-Review Queue");
        sb.AppendLine();
        sb.AppendLine("> Items below require human review before a deployment recommendation can be issued.");
        sb.AppendLine("> QA agent to populate this section based on §5 findings and §4 workflow aborts.");
        sb.AppendLine();

        var abortedWorkflows = summary.WorkflowResults.Where(w => w.Status == WorkflowStatus.Aborted).ToList();
        if (abortedWorkflows.Count > 0)
        {
            sb.AppendLine("**Aborted workflows (investigation required):**");
            sb.AppendLine();
            foreach (var w in abortedWorkflows)
                sb.AppendLine($"- `{w.WorkflowName}`: {w.AbortReason ?? "(no reason recorded)"}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("_No aborted workflows to review._");
        }

        sb.AppendLine();

        // -----------------------------------------------------------------------
        // §7 Evidence Inventory
        // -----------------------------------------------------------------------
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## §7 Evidence Inventory");
        sb.AppendLine();

        if (summary.Evidence.Items.Count == 0)
        {
            sb.AppendLine("_No evidence items captured during this run._");
        }
        else
        {
            sb.AppendLine("| Kind | Label | Path | Size (bytes) | Captured At |");
            sb.AppendLine("|------|-------|------|-------------|-------------|");

            foreach (var item in summary.Evidence.Items)
            {
                sb.AppendLine(
                    $"| {item.Kind} | {item.Label} | `{item.AbsolutePath}` | {item.SizeBytes:N0} | {item.CapturedAt:yyyy-MM-dd HH:mm:ss} UTC |");
            }
        }

        sb.AppendLine();

        // -----------------------------------------------------------------------
        // §8 Coverage Assessment
        // -----------------------------------------------------------------------
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## §8 Coverage Assessment");
        sb.AppendLine();
        sb.AppendLine("> Coverage against the Prisma MVP requirement set. QA agent to assess each row.");
        sb.AppendLine();
        sb.AppendLine("| Area | Workflows Executed | Validators Run | Coverage (QA agent to assess) |");
        sb.AppendLine("|------|-------------------|---------------|-------------------------------|");
        sb.AppendLine("| Ingestion | — | — | QA agent to assess |");
        sb.AppendLine("| Processing Pipeline | — | — | QA agent to assess |");
        sb.AppendLine("| Export (SIRO XML / Excel) | — | — | QA agent to assess |");
        sb.AppendLine("| Health & Readiness | — | — | QA agent to assess |");
        sb.AppendLine("| Manual Review UI | — | — | QA agent to assess |");
        sb.AppendLine();

        // -----------------------------------------------------------------------
        // §9 Risk Register
        // -----------------------------------------------------------------------
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## §9 Risk Register");
        sb.AppendLine();
        sb.AppendLine("> Known harness limitations that affect confidence in this run's results.");
        sb.AppendLine("> See §13 of ARCHITECTURE.md for the full canonical list.");
        sb.AppendLine();

        if (!summary.ProvisioningResult.DockerAvailable)
        {
            sb.AppendLine("- Docker unavailable: container-dependent workflows were skipped. SQL and Ollama capabilities were absent for this run.");
        }

        if (summary.ProvisioningResult.CorpusStatus is CorpusStatus.AbsentNoGenerator or CorpusStatus.AbsentGeneratorFailed)
        {
            sb.AppendLine($"- Corpus absent (CorpusStatus={summary.ProvisioningResult.CorpusStatus}): ingestion workflows could not execute.");
        }

        sb.AppendLine("- Harness produces observations only; PASS/FAIL verdicts must come from an independent QA agent.");
        sb.AppendLine();

        // -----------------------------------------------------------------------
        // §10 Deployment Recommendation Placeholder
        // -----------------------------------------------------------------------
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## §10 Deployment Recommendation");
        sb.AppendLine();
        sb.AppendLine("> **PLACEHOLDER** — To be completed by the QA agent after reviewing §§ 1–9.");
        sb.AppendLine();
        sb.AppendLine("- [ ] All critical findings addressed");
        sb.AppendLine("- [ ] Human-review queue empty");
        sb.AppendLine("- [ ] Coverage assessment satisfactory for MVP deployment");
        sb.AppendLine("- [ ] Risk register reviewed and accepted");
        sb.AppendLine();
        sb.AppendLine("**QA Agent recommendation:** _(not yet rendered)_");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string MaskConnectionString(string connectionString)
    {
        // Mask passwords in connection strings for safe logging.
        var idx = connectionString.IndexOf("Password=", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return connectionString;

        var end = connectionString.IndexOf(';', idx);
        var masked = connectionString[..idx] + "Password=***" +
                     (end >= 0 ? connectionString[end..] : string.Empty);
        return masked;
    }
}
