// <copyright file="PrismaWebUiHostController.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Net.Http;
using ExxerCube.Prisma.Web.UI.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.QaHarness.Hosting;

/// <summary>
/// Application host controller that wraps <c>PrismaWebApplicationFactory</c> (the dual-host
/// Andrew-Lock pattern: in-memory TestServer + live Kestrel on a dynamic port) so that Playwright
/// can reach the Blazor Web UI over a real HTTP port while DI interactions use the TestServer.
/// </summary>
/// <remarks>
/// <para>
/// Activates for <see cref="HostingMode.WebUiOnly"/> and <see cref="HostingMode.All"/>.
/// </para>
/// <para>
/// Call <see cref="StartAsync"/> before any workflow that requires the Web UI.  Always
/// <c>await using</c> (or call <see cref="DisposeAsync"/>) to tear down both hosts.
/// </para>
/// </remarks>
public sealed class PrismaWebUiHostController : IApplicationHostController
{
    private readonly ILogger<PrismaWebUiHostController> _logger;
    private HarnessWebApplicationFactory? _factory;
    private bool _disposed;

    /// <summary>
    /// Initialises a new instance of <see cref="PrismaWebUiHostController"/>.
    /// </summary>
    /// <param name="logger">Logger for startup diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public PrismaWebUiHostController(ILogger<PrismaWebUiHostController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initialises a new instance of <see cref="PrismaWebUiHostController"/> using a null logger.
    /// Suitable for tests that do not require structured output.
    /// </summary>
    public PrismaWebUiHostController()
        : this(NullLogger<PrismaWebUiHostController>.Instance)
    {
    }

    /// <inheritdoc/>
    public IServiceProvider? Services => _factory?.HostedServices ?? _factory?.Services;

    /// <inheritdoc/>
    public Uri? BaseAddress => _factory?.HostedBaseAddress;

    /// <inheritdoc/>
    public async Task<Result<ApplicationStartupResult>> StartAsync(
        HostingOptions options,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<ApplicationStartupResult>.WithFailure("StartAsync cancelled before it began.");
        }

        if (options is null)
        {
            return Result<ApplicationStartupResult>.WithFailure("HostingOptions must not be null.");
        }

        if (options.Mode is not (HostingMode.WebUiOnly or HostingMode.All))
        {
            return Result<ApplicationStartupResult>.WithFailure(
                $"PrismaWebUiHostController does not handle HostingMode.{options.Mode}. " +
                "Use ThreeProcessHostController for ThreeProcessPipeline.");
        }

        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "PrismaWebUiHostController: starting Web UI (Mode={Mode}, SQL={HasSql}, Storage={HasStorage})",
                options.Mode,
                options.SqlConnectionString is not null,
                options.SharedStoragePath is not null);

            _factory = new HarnessWebApplicationFactory(options);

            // EnsureStarted triggers CreateHost → builds TestServer + Kestrel on a dynamic port.
            await Task.Run(() => _factory.EnsureStarted(), cancellationToken).ConfigureAwait(false);

            var baseAddress = _factory.HostedBaseAddress;
            sw.Stop();

            // Perform a lightweight health probe to confirm the server is accepting requests.
            var (healthy, failureReason) = await ProbeHealthAsync(baseAddress, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "PrismaWebUiHostController: startup complete in {ElapsedMs} ms, healthy={Healthy}, address={Address}",
                sw.ElapsedMilliseconds,
                healthy,
                baseAddress?.ToString() ?? "<no-kestrel-address>");

            var result = new ApplicationStartupResult(
                IsHealthy: healthy,
                BaseAddress: baseAddress,
                StartupDurationMs: sw.ElapsedMilliseconds,
                FailureReason: failureReason);

            return Result<ApplicationStartupResult>.WithSuccess(result);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return Result<ApplicationStartupResult>.WithFailure("StartAsync was cancelled during host boot.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "PrismaWebUiHostController: startup failed after {ElapsedMs} ms.", sw.ElapsedMilliseconds);
            return Result<ApplicationStartupResult>.WithFailure(
                $"Web UI startup threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<Result> StopAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result.WithFailure("StopAsync cancelled before it began."));
        }

        try
        {
            if (_factory is not null)
            {
                _factory.Dispose();
                _factory = null;
            }

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PrismaWebUiHostController: error during StopAsync.");
            return Task.FromResult(Result.WithFailure($"StopAsync threw {ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_factory is not null)
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    // ── Health probe ──────────────────────────────────────────────────────────────

    private async Task<(bool Healthy, string? FailureReason)> ProbeHealthAsync(
        Uri? baseAddress,
        CancellationToken cancellationToken)
    {
        if (baseAddress is null)
        {
            // No Kestrel address means the dual-host boot did not bind a port;
            // but the TestServer may still work. This is a degraded-but-functional state.
            _logger.LogWarning(
                "PrismaWebUiHostController: no Kestrel address was captured; health probe skipped. " +
                "TestServer DI is available but browser automation will not work.");
            return (false, "No Kestrel base address — Kestrel host may not have started.");
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var healthUrl = new Uri(baseAddress, "/health/live");

        try
        {
            _logger.LogDebug("PrismaWebUiHostController: probing {HealthUrl}", healthUrl);
            var response = await http.GetAsync(healthUrl, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return (false, $"Health probe returned {(int)response.StatusCode}: {body.Truncate(200)}");
        }
        catch (OperationCanceledException)
        {
            return (false, "Health probe was cancelled.");
        }
        catch (Exception ex)
        {
            // /health/live not mounted is not necessarily a fatal harness failure; log and continue.
            _logger.LogWarning(ex, "PrismaWebUiHostController: health probe failed (endpoint may not be mounted).");
            return (false, $"Health probe threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ── Inner WebApplicationFactory ───────────────────────────────────────────────

    /// <summary>
    /// Harness-owned <c>WebApplicationFactory</c> that replicates the dual-host pattern from
    /// <c>PrismaWebApplicationFactory</c> (Andrew-Lock technique): builds an in-memory
    /// TestServer host first (WAF contract) then a real Kestrel host so Playwright can reach it.
    /// </summary>
    private sealed class HarnessWebApplicationFactory
        : WebApplicationFactory<ExxerCube.Prisma.Web.UI.Program>
    {
        private readonly HostingOptions _options;
        private IHost? _kestrelHost;

        internal HarnessWebApplicationFactory(HostingOptions options)
        {
            _options = options;
        }

        /// <summary>Gets the live Kestrel base address (captured after host start), or <see langword="null"/>.</summary>
        internal Uri? HostedBaseAddress { get; private set; }

        /// <summary>Gets the DI service provider of the Kestrel host (distinct from <see cref="WebApplicationFactory{T}.Services"/>).</summary>
        internal IServiceProvider? HostedServices => _kestrelHost?.Services;

        /// <summary>
        /// Triggers WAF host bootstrap (which calls <see cref="CreateHost"/>) if not already started.
        /// </summary>
        internal void EnsureStarted()
        {
            // CreateDefaultClient is the documented trigger for minimal-hosting (WebApplication.CreateBuilder) apps.
            using var _ = CreateDefaultClient();
        }

        /// <inheritdoc/>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Inject SQL connection string and shared storage path when provided.
            var overrides = new Dictionary<string, string?>();

            if (_options.SqlConnectionString is not null)
            {
                overrides["ConnectionStrings:DefaultConnection"] = _options.SqlConnectionString;
            }

            if (_options.SharedStoragePath is not null)
            {
                overrides["Storage:BasePath"] = _options.SharedStoragePath;
            }

            if (overrides.Count > 0)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(overrides));
            }

            builder.ConfigureServices(services =>
            {
                // Remove Python environment registration (not required for harness runs; avoids CSnakes init).
                // Use type-name comparison to avoid a compile-time dependency on CSnakes.Runtime — the type
                // lives in ExxerCube.Prisma.Infrastructure which references CSnakes transitively, but that
                // transitive reference does not flow to this library's compilation unit.
                var pythonEnvDescriptors = services
                    .Where(d => d.ServiceType.FullName?.Contains("IPythonEnvironment") == true)
                    .ToList();
                foreach (var d in pythonEnvDescriptors)
                {
                    services.Remove(d);
                }

                // Remove SignalR broadcaster (avoids Rx subscription leak during harness teardown).
                // The /processingHub negotiate endpoint remains available regardless.
                var broadcasters = services
                    .Where(d =>
                        d.ServiceType == typeof(IHostedService) &&
                        d.ImplementationType == typeof(SignalREventBroadcaster))
                    .ToList();
                foreach (var d in broadcasters)
                {
                    services.Remove(d);
                }

                // Disable the autonomous watch loop when requested (prevents SIARA polling interfering).
                if (_options.DisableAutonomousWatchLoop)
                {
                    var watchLoop = services
                        .Where(d => d.ServiceType == typeof(IHostedService) &&
                                    d.ImplementationType?.Name == "OrionWorkerService")
                        .ToList();
                    foreach (var d in watchLoop)
                    {
                        services.Remove(d);
                    }
                }
            });

            builder.UseEnvironment("Development");
        }

        /// <inheritdoc/>
        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Build the in-memory TestServer host FIRST (WAF requires this host to be returned).
            var testHost = builder.Build();

            // Reconfigure the SAME builder to use real Kestrel on a dynamic loopback port.
            builder.ConfigureWebHost(webHost =>
            {
                webHost.UseKestrel(opts =>
                    opts.Listen(System.Net.IPAddress.Loopback, 0));
            });

            try
            {
                _kestrelHost = builder.Build();
                _kestrelHost.Start();

                var addresses = _kestrelHost.Services
                    .GetRequiredService<IServer>()
                    .Features.Get<IServerAddressesFeature>()
                    ?.Addresses;

                var first = addresses?.FirstOrDefault();
                if (first is not null)
                {
                    HostedBaseAddress = new Uri(first);
                }

                testHost.Start();
                return testHost;
            }
            catch
            {
                testHost.Dispose();
                throw;
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _kestrelHost?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

// ── Local extension helper ────────────────────────────────────────────────────

/// <summary>
/// String helper used internally by the QA Harness to truncate long error payloads in log messages.
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    /// Returns the first <paramref name="maxLength"/> characters of <paramref name="value"/>
    /// followed by <c>…</c> when truncation occurs.
    /// </summary>
    /// <param name="value">The string to truncate.</param>
    /// <param name="maxLength">Maximum allowed length before truncation.</param>
    /// <returns>The truncated string, or the original string when it fits within <paramref name="maxLength"/>.</returns>
    internal static string Truncate(this string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "…");
}
