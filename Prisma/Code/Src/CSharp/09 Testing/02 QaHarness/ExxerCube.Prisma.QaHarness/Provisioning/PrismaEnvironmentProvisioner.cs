// <copyright file="PrismaEnvironmentProvisioner.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.QaHarness.Provisioning;

/// <summary>
/// Orchestrates the full environment provisioning sequence:
/// Docker health gate → SQL Server container → corpus seeding.
/// </summary>
/// <remarks>
/// Never throws for business-logic failures; returns <see cref="Result{T}"/> failures instead.
/// Partial success (e.g. Docker available but corpus absent) is represented as a successful
/// result whose <see cref="EnvironmentProvisioningResult"/> carries the degraded-capability
/// detail — it is the caller's responsibility to decide whether to abort.
/// </remarks>
public sealed class PrismaEnvironmentProvisioner : IEnvironmentProvisioner
{
    private readonly DockerHealthGate _dockerGate;
    private readonly CorpusSeeder _corpusSeeder;
    private readonly ILogger<PrismaEnvironmentProvisioner> _logger;

    // Container lifecycle — owned here, disposed in TeardownAsync.
    private SqlServerContainerFixture? _sqlFixture;
    private string? _repoRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrismaEnvironmentProvisioner"/> class.
    /// </summary>
    /// <param name="dockerGate">Gate that probes Docker daemon liveness.</param>
    /// <param name="corpusSeeder">Seeder that provisions the document corpus.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public PrismaEnvironmentProvisioner(
        DockerHealthGate dockerGate,
        CorpusSeeder corpusSeeder,
        ILogger<PrismaEnvironmentProvisioner> logger)
    {
        ArgumentNullException.ThrowIfNull(dockerGate);
        ArgumentNullException.ThrowIfNull(corpusSeeder);
        ArgumentNullException.ThrowIfNull(logger);
        _dockerGate = dockerGate;
        _corpusSeeder = corpusSeeder;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<EnvironmentProvisioningResult>> ProvisionAsync(
        ProvisioningOptions options,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Result<EnvironmentProvisioningResult>.WithFailure("ProvisionAsync was cancelled before it started.");

        _repoRoot = ResolveRepoRoot(options.RepoRoot);

        var capabilities = new List<CapabilityStatus>();
        string? sqlConnectionString = null;
        string? ollamaEndpoint = null;
        bool dockerAvailable = false;

        try
        {
            // 1 — Docker health gate.
            var dockerStatus = await _dockerGate.CheckAsync(cancellationToken).ConfigureAwait(false);
            capabilities.Add(dockerStatus);
            dockerAvailable = dockerStatus.Available;

            // 2 — SQL Server container (only if Docker is up and requested).
            if (options.StartSqlContainer)
            {
                if (!dockerAvailable)
                {
                    _logger.LogWarning("SQL container requested but Docker is unavailable; skipping SQL provisioning.");
                    capabilities.Add(new CapabilityStatus("SqlServer", false, "Docker unavailable; SQL container not started."));
                }
                else
                {
                    var sqlResult = await StartSqlContainerAsync(cancellationToken).ConfigureAwait(false);
                    capabilities.Add(sqlResult.Status);
                    sqlConnectionString = sqlResult.ConnectionString;
                }
            }

            // 3 — Ollama container (optional, deferred — mark capability accordingly).
            if (options.StartOllamaContainer)
            {
                if (!dockerAvailable)
                {
                    capabilities.Add(new CapabilityStatus("Ollama", false, "Docker unavailable; Ollama container not started."));
                }
                else
                {
                    // Ollama start is handled in Chunk 2B (hosting). For now record as skipped.
                    capabilities.Add(new CapabilityStatus("Ollama", false, "Ollama provisioning deferred to hosting layer (Chunk 2B)."));
                }
            }

            // 4 — Corpus seeding.
            string corpusPath;
            CorpusStatus corpusStatus;

            if (options.SeedCorpus && _repoRoot is not null)
            {
                (corpusStatus, corpusPath) = await _corpusSeeder.SeedAsync(
                    _repoRoot,
                    options.CorpusOutputPath,
                    options.CorpusDocumentCount,
                    cancellationToken).ConfigureAwait(false);
            }
            else if (_repoRoot is not null)
            {
                // Seeding skipped; point at fixtures as a minimal corpus.
                corpusPath = Path.Combine(_repoRoot, @"Prisma\Code\Fixtures");
                corpusStatus = CorpusStatus.RestoredFromFixtures;
            }
            else
            {
                corpusPath = string.Empty;
                corpusStatus = CorpusStatus.AbsentNoGenerator;
            }

            var corpusCapability = corpusStatus switch
            {
                CorpusStatus.Seeded => new CapabilityStatus("Corpus", true, null),
                CorpusStatus.RestoredFromFixtures => new CapabilityStatus("Corpus", true, "Restored from static Fixtures/."),
                CorpusStatus.AbsentGeneratorFailed => new CapabilityStatus("Corpus", false, "Python generator failed; corpus absent."),
                _ => new CapabilityStatus("Corpus", false, "No corpus source available."),
            };
            capabilities.Add(corpusCapability);

            var result = new EnvironmentProvisioningResult(
                SqlConnectionString: sqlConnectionString,
                OllamaEndpoint: ollamaEndpoint,
                CorpusPath: corpusPath,
                CorpusStatus: corpusStatus,
                DockerAvailable: dockerAvailable,
                CapabilityStatuses: capabilities.AsReadOnly());

            _logger.LogInformation(
                "Provisioning complete. Docker={DockerAvailable}, SQL={SqlAvailable}, Corpus={CorpusStatus}.",
                dockerAvailable,
                sqlConnectionString is not null,
                corpusStatus);

            return Result<EnvironmentProvisioningResult>.WithSuccess(result);
        }
        catch (OperationCanceledException)
        {
            return Result<EnvironmentProvisioningResult>.WithFailure("Provisioning was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during provisioning.");
            return Result<EnvironmentProvisioningResult>.WithFailure($"Provisioning failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result> TeardownAsync(
        EnvironmentProvisioningResult provisioningResult,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Result.WithFailure("TeardownAsync was cancelled before it started.");

        try
        {
            if (_sqlFixture is not null)
            {
                await _sqlFixture.DisposeAsync().ConfigureAwait(false);
                _sqlFixture = null;
                _logger.LogInformation("SQL Server container disposed.");
            }

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return Result.WithFailure("Teardown was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during teardown.");
            return Result.WithFailure($"Teardown failed: {ex.Message}");
        }
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private async Task<(CapabilityStatus Status, string? ConnectionString)> StartSqlContainerAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            _sqlFixture = new SqlServerContainerFixture();
            await _sqlFixture.InitializeAsync().ConfigureAwait(false);

            var connectionString = await _sqlFixture.CreateIsolatedDatabaseAsync(
                "QaHarnessRun", cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("SQL Server container started; isolated DB connection string obtained.");
            return (
                new CapabilityStatus("SqlServer", true, null),
                connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SQL Server container.");
            return (
                new CapabilityStatus("SqlServer", false, $"Container startup failed: {ex.Message}"),
                null);
        }
    }

    /// <summary>
    /// Resolves the repository root using a three-priority chain:
    /// <list type="number">
    ///   <item><description>(1) Explicit value from <paramref name="explicitRepoRoot"/> (e.g. <c>--repo-root</c> CLI flag or <see cref="ProvisioningOptions.RepoRoot"/>).</description></item>
    ///   <item><description>(2) <c>PRISMA_REPO_ROOT</c> environment variable.</description></item>
    ///   <item><description>(3) Walk-up locator from <c>AppContext.BaseDirectory</c> — looks for the <c>Prisma\Fixtures</c> directory marker.</description></item>
    /// </list>
    /// Returns <see langword="null"/> when none of the three sources can locate the root.
    /// Never throws.
    /// </summary>
    /// <param name="explicitRepoRoot">Optional caller-supplied absolute path; takes highest precedence.</param>
    private static string? ResolveRepoRoot(string? explicitRepoRoot)
    {
        // (1) Explicit value — caller knows better than any heuristic.
        if (!string.IsNullOrWhiteSpace(explicitRepoRoot) && Directory.Exists(explicitRepoRoot))
            return explicitRepoRoot;

        // (2) Environment variable override — useful for CI pipelines and Docker environments
        //     where the build artifacts live outside the repo tree.
        var envRoot = Environment.GetEnvironmentVariable("PRISMA_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot) && Directory.Exists(envRoot))
            return envRoot;

        // (3) Walk-up locator — same repo-root marker used by CalibrationReportRenderer.
        try
        {
            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                var marker = Path.Combine(dir, "Prisma", "Fixtures");
                if (Directory.Exists(marker))
                    return dir;

                var parent = Directory.GetParent(dir)?.FullName;
                if (parent == dir || parent is null)
                    break;
                dir = parent;
            }
        }
        catch
        {
            // Never throw.
        }

        return null;
    }
}
