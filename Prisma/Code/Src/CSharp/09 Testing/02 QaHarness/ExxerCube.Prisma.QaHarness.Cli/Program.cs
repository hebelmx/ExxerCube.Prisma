// <copyright file="Program.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

// QA Harness CLI — Chunk 5 implementation.
// Top-level statements are used per ARCHITECTURE §8; internal helpers do not need XML docs
// because the CLI project is OutputType=Exe (not a library; GenerateDocumentationFile is false).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.QaHarness.DependencyInjection;
using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Hosting;
using ExxerCube.Prisma.QaHarness.Provisioning;
using ExxerCube.Prisma.QaHarness.Reporting;
using ExxerCube.Prisma.QaHarness.Traceability;
using ExxerCube.Prisma.QaHarness.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ── Ctrl-C wire-up ──────────────────────────────────────────────────────────
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;   // prevent immediate process termination; let the harness clean up
    cts.Cancel();
    Console.Error.WriteLine("[QaHarness CLI] Cancellation requested (Ctrl-C).");
};

var ct = cts.Token;

// ── Argument parsing ─────────────────────────────────────────────────────────
CliOptions opts;
try
{
    opts = CliOptions.Parse(args);
}
catch (CliArgumentException ex)
{
    Console.Error.WriteLine($"[QaHarness CLI] ERROR: {ex.Message}");
    Console.Error.WriteLine("Run with --help for usage.");
    return 2;
}

if (opts.ShowHelp)
{
    PrintHelp();
    return 0;
}

// ── DI container ─────────────────────────────────────────────────────────────
var services = new ServiceCollection();
services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});
services.AddQaHarness();

await using var rootProvider = services.BuildServiceProvider();
await using var scope = rootProvider.CreateAsyncScope();
var sp = scope.ServiceProvider;

var logger = sp.GetRequiredService<ILogger<object>>();

// ── Run ID + output directory ─────────────────────────────────────────────────
var runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
var outputBase = string.IsNullOrWhiteSpace(opts.OutputDir)
    ? Path.Combine("docs", "qa", "harness", "runs", runId)
    : opts.OutputDir;

Directory.CreateDirectory(outputBase);

var runStartedAt = DateTimeOffset.UtcNow;

// ── Provisioning ─────────────────────────────────────────────────────────────
logger.LogInformation("[QaHarness CLI] Provisioning environment (run={RunId})", runId);

var provisioner = sp.GetRequiredService<IEnvironmentProvisioner>();
var provisionOpts = new ProvisioningOptions(
    StartSqlContainer: !opts.SkipDocker,
    StartOllamaContainer: false,
    SeedCorpus: true);

var provisionResult = await provisioner.ProvisionAsync(provisionOpts, ct);
if (!provisionResult.IsSuccess)
{
    var err = $"Provisioning failed: {string.Join("; ", provisionResult.Errors ?? [])}";
    logger.LogError("[QaHarness CLI] {Error}", err);
    EmitJsonSummary(new RunSummaryDto(runId, runStartedAt, DateTimeOffset.UtcNow,
        "error", err, [], [], []));
    return 1;
}

var env = provisionResult.Value!;

// Log capability status
foreach (var cap in env.CapabilityStatuses)
{
    var level = cap.Available ? LogLevel.Information : LogLevel.Warning;
    logger.Log(level, "[QaHarness CLI] Capability {Capability}: {Status}{Detail}",
        cap.Capability,
        cap.Available ? "available" : "UNAVAILABLE",
        cap.Detail is not null ? $" — {cap.Detail}" : string.Empty);
}

// ── Provision-only path ───────────────────────────────────────────────────────
if (opts.ProvisionOnly)
{
    var capDtos = env.CapabilityStatuses
        .Select(c => new CapabilityDto(c.Capability, c.Available, c.Detail))
        .ToList();

    EmitJsonSummary(new RunSummaryDto(runId, runStartedAt, DateTimeOffset.UtcNow,
        "provision-only", null, capDtos, [], []));

    await provisioner.TeardownAsync(env, CancellationToken.None);
    return 0;
}

// ── Host startup ──────────────────────────────────────────────────────────────
logger.LogInformation("[QaHarness CLI] Starting application host (WebUiOnly).");
var hostController = sp.GetRequiredService<IApplicationHostController>();
var hostingOpts = new HostingOptions(
    Mode: HostingMode.WebUiOnly,
    SqlConnectionString: env.SqlConnectionString,
    SharedStoragePath: null,
    DisableAutonomousWatchLoop: true);

var hostResult = await hostController.StartAsync(hostingOpts, ct);
if (!hostResult.IsSuccess)
{
    logger.LogWarning("[QaHarness CLI] Host start failed — workflows will run without live host: {Reason}",
        string.Join("; ", hostResult.Errors ?? []));
}
else
{
    logger.LogInformation("[QaHarness CLI] Host started: healthy={Healthy}, address={Address}",
        hostResult.Value?.IsHealthy, hostResult.Value?.BaseAddress?.ToString() ?? "(none)");
}

// ── Workflow execution ────────────────────────────────────────────────────────
var runner = sp.GetRequiredService<IWorkflowRunner>();
var traceability = sp.GetRequiredService<ITraceabilityMap>();
var evidence = sp.GetRequiredService<IEvidenceCollector>();

var workflowContext = new WorkflowContext(
    Services: hostController.Services ?? sp,
    Evidence: evidence,
    Traceability: traceability,
    PlaywrightPage: null,
    SharedStoragePath: null);

var workflowsToRun = SelectWorkflows(runner.AvailableWorkflows, opts);
var workflowResults = new List<WorkflowResult>();     // typed for the report writer
var workflowDtos = new List<WorkflowResultDto>();     // lightweight for stdout JSON

foreach (var workflow in workflowsToRun)
{
    if (ct.IsCancellationRequested)
        break;

    logger.LogInformation("[QaHarness CLI] Running workflow: {Name}", workflow.Name);
    var result = await runner.RunAsync(workflow, workflowContext, ct);

    workflowResults.Add(result);
    workflowDtos.Add(new WorkflowResultDto(
        result.WorkflowName,
        result.Status.ToString(),
        result.AbortReason,
        (long)(result.FinishedAt - result.StartedAt).TotalMilliseconds,
        result.Evidence.Items.Count));

    logger.LogInformation("[QaHarness CLI] Workflow {Name} → {Status}{Reason}",
        result.WorkflowName, result.Status,
        result.AbortReason is not null ? $" ({result.AbortReason})" : string.Empty);
}

// ── Report writing ────────────────────────────────────────────────────────────
var reportWriters = sp.GetServices<IReportWriter>().ToList();
var requestedFormats = new HashSet<string>(
    opts.ReportFormats.Count > 0 ? opts.ReportFormats : ["md"],
    StringComparer.OrdinalIgnoreCase);

// Normalize "md" → "markdown" since MarkdownReportWriter.Format = "markdown"
bool wantsMarkdown = requestedFormats.Contains("md") || requestedFormats.Contains("markdown");

var harnessRunSummary = new HarnessRunSummary(
    RunId: runId,
    ProductVersion: GetProductVersion(),
    StartedAt: runStartedAt,
    FinishedAt: DateTimeOffset.UtcNow,
    ProvisioningResult: env,
    WorkflowResults: workflowResults.AsReadOnly(),
    ValidationResults: [],
    Evidence: evidence.CurrentPackage,
    TraceabilityMap: traceability);

var writtenReports = new List<string>();
foreach (var writer in reportWriters)
{
    if (ct.IsCancellationRequested)
        break;

    bool wantsThisFormat = writer.Format switch
    {
        "markdown" => wantsMarkdown,
        _ => requestedFormats.Contains(writer.Format),
    };

    if (!wantsThisFormat)
        continue;

    var ext = writer.Format switch
    {
        "markdown" => "md",
        "html" => "html",
        "json" => "json",
        _ => writer.Format,
    };

    var reportPath = Path.Combine(outputBase, $"report-{runId}.{ext}");
    var writeResult = await writer.WriteAsync(harnessRunSummary, reportPath, ct);

    if (writeResult.IsSuccess)
    {
        writtenReports.Add(reportPath);
        logger.LogInformation("[QaHarness CLI] Report written: {Path}", reportPath);
    }
    else
    {
        logger.LogWarning("[QaHarness CLI] Report write failed ({Format}): {Error}",
            writer.Format, string.Join("; ", writeResult.Errors ?? []));
    }
}

// ── Teardown ──────────────────────────────────────────────────────────────────
await hostController.StopAsync(CancellationToken.None);
await provisioner.TeardownAsync(env, CancellationToken.None);

// ── Final JSON summary to stdout ──────────────────────────────────────────────
var capabilityDtos = env.CapabilityStatuses
    .Select(c => new CapabilityDto(c.Capability, c.Available, c.Detail))
    .ToList();

EmitJsonSummary(new RunSummaryDto(
    RunId: runId,
    StartedAt: runStartedAt,
    FinishedAt: DateTimeOffset.UtcNow,
    Status: ct.IsCancellationRequested ? "cancelled" : "completed",
    ErrorMessage: null,
    Capabilities: capabilityDtos,
    WorkflowResults: workflowDtos,
    Reports: writtenReports));

// Exit 0 on a successful run (regardless of QA outcome — the report holds verdicts).
// Non-zero only on harness/usage errors.
return ct.IsCancellationRequested ? 1 : 0;

// ── Local helpers ─────────────────────────────────────────────────────────────

static void EmitJsonSummary(RunSummaryDto summary)
{
    var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    });
    Console.WriteLine(json);
}

static void PrintHelp()
{
    // Build a temporary DI container just to enumerate available workflows for --help output.
    var hServices = new ServiceCollection();
    hServices.AddLogging(l => l.SetMinimumLevel(LogLevel.None));
    hServices.AddQaHarness();
    using var hProvider = hServices.BuildServiceProvider();
    var runner = hProvider.GetRequiredService<IWorkflowRunner>();

    var sb = new StringBuilder();
    sb.AppendLine();
    sb.AppendLine("ExxerCube.Prisma QA Harness CLI");
    sb.AppendLine("================================");
    sb.AppendLine();
    sb.AppendLine("USAGE:");
    sb.AppendLine("  dotnet run --project ExxerCube.Prisma.QaHarness.Cli -- [OPTIONS]");
    sb.AppendLine();
    sb.AppendLine("OPTIONS:");
    sb.AppendLine("  --help, -h                   Show this help message and exit (exit 0).");
    sb.AppendLine("  --workflow <name>            Run a single named workflow.");
    sb.AppendLine("  --all-workflows              Run all registered workflows.");
    sb.AppendLine("  --provision-only             Provision environment only; skip host and workflows.");
    sb.AppendLine("  --report-format <fmt>        Report format: md (default), html, json.");
    sb.AppendLine("                               Repeat or comma-separate for multiple.");
    sb.AppendLine("  --output-dir <path>          Directory for reports.");
    sb.AppendLine("                               Default: docs/qa/harness/runs/<runId>/");
    sb.AppendLine("  --skip-docker                Skip Docker provisioning.");
    sb.AppendLine();
    sb.AppendLine("EXIT CODES:");
    sb.AppendLine("  0  Successful run (read the report for QA verdicts — harness makes none).");
    sb.AppendLine("  1  Harness error (provisioning failure, cancellation).");
    sb.AppendLine("  2  Usage error (bad flag, unknown argument).");
    sb.AppendLine();
    sb.AppendLine("AVAILABLE WORKFLOWS:");
    sb.AppendLine();

    foreach (var wf in runner.AvailableWorkflows)
    {
        sb.AppendLine($"  {wf.Name}");
        sb.AppendLine($"    Description : {wf.Description}");
        if (wf.RequiredCapabilities.Count > 0)
            sb.AppendLine($"    Requires    : {string.Join(", ", wf.RequiredCapabilities)}");
        if (wf.Tags.Count > 0)
            sb.AppendLine($"    Tags        : {string.Join(", ", wf.Tags)}");
        sb.AppendLine();
    }

    Console.Write(sb);
}

static IReadOnlyList<IWorkflow> SelectWorkflows(
    IReadOnlyList<IWorkflow> available, CliOptions opts)
{
    if (opts.AllWorkflows)
        return available;

    if (!string.IsNullOrWhiteSpace(opts.WorkflowName))
    {
        var match = available.FirstOrDefault(w =>
            string.Equals(w.Name, opts.WorkflowName, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            Console.Error.WriteLine(
                $"[QaHarness CLI] WARNING: Workflow '{opts.WorkflowName}' not found. " +
                "No workflows will run. Use --help to list available workflows.");
            return [];
        }

        return [match];
    }

    // No workflow flag and not --all-workflows: run all by default so a bare invocation
    // produces a useful full report rather than doing nothing.
    return available;
}

static string GetProductVersion()
{
    var asm = typeof(ExxerCube.Prisma.QaHarness.DependencyInjection.QaHarnessServiceCollectionExtensions)
        .Assembly;
    return asm.GetName().Version?.ToString(3) ?? "0.0.0";
}

// ── CLI option record ─────────────────────────────────────────────────────────

internal sealed record CliOptions(
    bool ShowHelp,
    string? WorkflowName,
    bool AllWorkflows,
    bool ProvisionOnly,
    bool SkipDocker,
    List<string> ReportFormats,
    string? OutputDir)
{
    internal static CliOptions Parse(string[] args)
    {
        bool showHelp = false;
        string? workflowName = null;
        bool allWorkflows = false;
        bool provisionOnly = false;
        bool skipDocker = false;
        var reportFormats = new List<string>();
        string? outputDir = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;

                case "--workflow":
                    if (i + 1 >= args.Length)
                        throw new CliArgumentException("--workflow requires a workflow name.");
                    workflowName = args[++i];
                    break;

                case "--all-workflows":
                    allWorkflows = true;
                    break;

                case "--provision-only":
                    provisionOnly = true;
                    break;

                case "--skip-docker":
                    skipDocker = true;
                    break;

                case "--report-format":
                    if (i + 1 >= args.Length)
                        throw new CliArgumentException("--report-format requires md, html, or json.");
                    var fmts = args[++i].Split(',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var fmt in fmts)
                    {
                        if (!fmt.Equals("md", StringComparison.OrdinalIgnoreCase) &&
                            !fmt.Equals("html", StringComparison.OrdinalIgnoreCase) &&
                            !fmt.Equals("json", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new CliArgumentException(
                                $"Unrecognised report format '{fmt}'. Valid values: md, html, json.");
                        }

                        reportFormats.Add(fmt.ToLowerInvariant());
                    }

                    break;

                case "--output-dir":
                    if (i + 1 >= args.Length)
                        throw new CliArgumentException("--output-dir requires a path.");
                    outputDir = args[++i];
                    break;

                default:
                    throw new CliArgumentException(
                        $"Unrecognised flag '{args[i]}'. Run with --help for usage.");
            }
        }

        return new CliOptions(showHelp, workflowName, allWorkflows, provisionOnly,
            skipDocker, reportFormats, outputDir);
    }
}

// Thrown by CliOptions.Parse for unrecognised or malformed arguments.
internal sealed class CliArgumentException : Exception
{
    internal CliArgumentException(string message) : base(message) { }
}

// ── JSON stdout DTO types ─────────────────────────────────────────────────────

internal sealed record RunSummaryDto(
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string Status,
    string? ErrorMessage,
    IReadOnlyList<CapabilityDto> Capabilities,
    IReadOnlyList<WorkflowResultDto> WorkflowResults,
    IReadOnlyList<string> Reports);

internal sealed record CapabilityDto(
    string Capability,
    bool Available,
    string? Detail);

internal sealed record WorkflowResultDto(
    string Name,
    string Status,
    string? AbortReason,
    long DurationMs,
    int EvidenceItems);
