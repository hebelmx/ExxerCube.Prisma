// <copyright file="DockerHealthGate.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.QaHarness.Provisioning;

/// <summary>
/// Checks whether the Docker daemon is reachable before any container work is attempted.
/// Never throws — returns a <see cref="CapabilityStatus"/> or a boolean result.
/// </summary>
public sealed class DockerHealthGate
{
    private readonly ILogger<DockerHealthGate> _logger;

    /// <summary>
    /// Timeout in milliseconds for the <c>docker info</c> probe process.
    /// </summary>
    private const int DockerProbeTimeoutMs = 8_000;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockerHealthGate"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public DockerHealthGate(ILogger<DockerHealthGate> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Runs <c>docker info</c> with a short timeout and returns whether the daemon is reachable.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the probe.</param>
    /// <returns>
    /// A <see cref="CapabilityStatus"/> with <c>Available = true</c> when Docker is reachable;
    /// <c>Available = false</c> with a diagnostic detail otherwise.
    /// Never throws.
    /// </returns>
    public async Task<CapabilityStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new CapabilityStatus("Docker", false, "Check was cancelled before it started.");
        }

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(DockerProbeTimeoutMs);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return new CapabilityStatus("Docker", false, "Docker probe timed out or was cancelled.");
            }

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Docker daemon is reachable (docker info exit code 0).");
                return new CapabilityStatus("Docker", true, null);
            }

            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Docker probe returned exit code {ExitCode}: {Stderr}", process.ExitCode, stderr);
            return new CapabilityStatus("Docker", false, $"docker info exited {process.ExitCode}: {stderr.Trim()}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Docker probe failed (Docker may not be installed or not in PATH).");
            return new CapabilityStatus("Docker", false, $"Docker probe threw: {ex.Message}");
        }
    }
}
