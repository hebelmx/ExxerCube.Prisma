using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Startup;

/// <summary>
/// <see cref="IHostedService"/> that runs once at host startup to audit critical
/// Veriqan configuration keys and emit structured Serilog-compatible warnings for any
/// that are absent or empty.
/// </summary>
/// <remarks>
/// <para>
/// <b>Design intent:</b> this validator does NOT crash the process.  Missing keys are
/// surfaced as loud structured warnings so operators can observe them via log aggregation
/// tooling (Seq, ELK, Application Insights, etc.) without taking the worker offline.
/// A later story will make <c>POST /verify</c> return a non-2xx while unconfigured.
/// </para>
/// <para>
/// <b>When <c>ConnectionStrings:VeriqanDb</c> is absent</b> the worker silently falls
/// back to in-memory persistence (see <c>VeriqanOrchestrationExtensions.AddVeriqan</c>).
/// That silent fallback is intentional for developer convenience but must be flagged
/// loudly in any environment where durable storage is expected.
/// </para>
/// <para>
/// <b>Keys inspected:</b>
/// <list type="bullet">
///   <item><c>ConnectionStrings:VeriqanDb</c> — SQL persistence (absence = in-memory fallback)</item>
///   <item><c>Veriqan:CsvReferenceData:RootDirectory</c> — reference-data CSV root</item>
///   <item><c>Veriqan:Smtp:Host</c> — SMTP relay for RED-verdict alerts</item>
///   <item><c>Veriqan:LegalBaseline:EncryptionKey</c> — AES-256 key for at-rest encryption</item>
/// </list>
/// </para>
/// </remarks>
public sealed class VeriqanConfigurationValidator : IHostedService
{
    /// <summary>
    /// Key/description pairs for every configuration value that triggers a warning when absent.
    /// </summary>
    private static readonly IReadOnlyList<(string Key, string Purpose)> CriticalKeys =
    [
        ("ConnectionStrings:VeriqanDb",
            "SQL Server connection string — absence causes in-memory fallback (no durability)"),
        ("Veriqan:CsvReferenceData:RootDirectory",
            "CSV reference-data root directory — reference lookups will fail without it"),
        ("Veriqan:Smtp:Host",
            "SMTP host — RED-verdict alert emails cannot be dispatched without it"),
        ("Veriqan:LegalBaseline:EncryptionKey",
            "AES-256 encryption key — required for the encrypted SQL legal-baseline store"),
    ];

    private readonly IConfiguration _configuration;
    private readonly ILogger<VeriqanConfigurationValidator> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="VeriqanConfigurationValidator"/>.
    /// </summary>
    /// <param name="configuration">Application configuration to inspect.</param>
    /// <param name="logger">Structured logger used to emit warnings.</param>
    public VeriqanConfigurationValidator(
        IConfiguration configuration,
        ILogger<VeriqanConfigurationValidator> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Scans <see cref="CriticalKeys"/> and emits a structured warning for every absent
    /// or whitespace-only entry.  Always returns <see cref="Task.CompletedTask"/> —
    /// missing keys do NOT abort startup.
    /// </summary>
    /// <param name="cancellationToken">Host cancellation token (unused; no I/O performed).</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.CompletedTask;

        var missingKeys = new List<string>();

        foreach (var (key, purpose) in CriticalKeys)
        {
            var value = _configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                missingKeys.Add(key);

                // Emit a dedicated warning per key so each can be queried independently
                // in structured log stores without parsing a combined message.
                if (key == "ConnectionStrings:VeriqanDb")
                {
                    _logger.LogWarning(
                        "VeriqanDb connection string absent — persistence will be IN-MEMORY. " +
                        "Purpose: {KeyPurpose}. Set {ConfigKey} to enable SQL durability.",
                        purpose, key);
                }
                else
                {
                    _logger.LogWarning(
                        "Critical Veriqan configuration key {ConfigKey} is absent or empty. " +
                        "Purpose: {KeyPurpose}. Supply the value in appsettings.json or " +
                        "the corresponding environment variable before serving traffic.",
                        key, purpose);
                }
            }
        }

        if (missingKeys.Count > 0)
        {
            _logger.LogWarning(
                "Veriqan startup validation detected {MissingKeyCount} absent configuration " +
                "key(s): {MissingKeys}. Review appsettings.Production.json.template for " +
                "required values.",
                missingKeys.Count,
                string.Join(", ", missingKeys));
        }
        else
        {
            _logger.LogInformation(
                "Veriqan configuration validation passed — all critical keys are present.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Returns the set of configuration keys that are currently absent or empty.
    /// Exposed for unit-testing without running the full hosted-service lifecycle.
    /// </summary>
    /// <returns>
    /// A read-only list of configuration key strings that have no value in the current
    /// <see cref="IConfiguration"/>; empty if all critical keys are present.
    /// </returns>
    internal IReadOnlyList<string> FindMissingKeys()
    {
        var missing = new List<string>();
        foreach (var (key, _) in CriticalKeys)
        {
            if (string.IsNullOrWhiteSpace(_configuration[key]))
                missing.Add(key);
        }

        return missing;
    }
}
