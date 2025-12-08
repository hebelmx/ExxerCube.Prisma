using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ExxerCube.Prisma.Testing.Infrastructure.Docker;

/// <summary>
/// Provides Docker daemon health checks and auto-start capabilities for test infrastructure.
/// Supports both Windows (Docker Desktop) and Linux (systemd docker service).
/// </summary>
public static class DockerHealthCheck
{
    /// <summary>
    /// Checks if Docker or Podman is running and attempts to start it if available but not running.
    /// Supports both Docker and Podman (Podman is a daemonless Docker-compatible container engine).
    /// Logs diagnostic messages using TestContext.Current.SendDiagnosticMessage().
    /// </summary>
    /// <returns>A task containing the container runtime status result.</returns>
    public static async Task<DockerStatus> CheckAndStartDockerAsync()
    {
        try
        {
            LogMessage("🔍 Checking container runtime (Docker/Podman) status...");

            // Check both Docker and Podman
            var dockerRunning = await IsContainerRuntimeRunningAsync("docker");
            if (dockerRunning)
            {
                LogMessage("✅ Docker is running");
                return new DockerStatus(true, true, "Docker is running", "docker");
            }

            var podmanRunning = await IsContainerRuntimeRunningAsync("podman");
            if (podmanRunning)
            {
                LogMessage("✅ Podman is running");
                return new DockerStatus(true, true, "Podman is running", "podman");
            }

            LogMessage("⚠️ Neither Docker nor Podman is running. Attempting to start...");

            // Try to start Docker first, then Podman
            var dockerStartResult = await TryStartDockerAsync();
            if (dockerStartResult.Success)
            {
                LogMessage("✅ Docker started successfully");

                // Wait 3 seconds initially, then poll every second for up to 7 more seconds (10 seconds total)
                LogMessage("⏳ Waiting for Docker to initialize (3s + polling up to 7s)...");
                await Task.Delay(TimeSpan.FromSeconds(3));

                // Poll every second for up to 7 additional seconds
                for (int i = 0; i < 7; i++)
                {
                    if (await IsContainerRuntimeRunningAsync("docker"))
                    {
                        LogMessage($"✅ Docker is ready after {3 + i} seconds");
                        return new DockerStatus(true, true, "Docker started successfully", "docker");
                    }
                    LogMessage($"⏳ Docker not yet ready, waiting... ({3 + i + 1}s elapsed)");
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                LogMessage("⚠️ Docker started but not yet ready after 10 seconds. May need more time to initialize.");
                return new DockerStatus(true, false, "Docker started but not yet fully initialized", "docker");
            }

            // If Docker failed, check if Podman is available
            if (await IsContainerRuntimeAvailableAsync("podman"))
            {
                LogMessage("💡 Podman is available (daemonless - no start needed)");
                // Podman doesn't have a daemon, so if the command exists, it's "running"
                return new DockerStatus(true, true, "Podman is available", "podman");
            }

            return dockerStartResult;
        }
        catch (Exception ex)
        {
            LogMessage(ex, "❌ Error checking container runtime status");
            return new DockerStatus(false, false, $"Error checking container runtime: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Checks if the specified container runtime (docker or podman) is currently running.
    /// </summary>
    /// <param name="runtime">The container runtime to check ("docker" or "podman").</param>
    private static async Task<bool> IsContainerRuntimeRunningAsync(string runtime)
    {
        try
        {
            // Try to run 'docker ps' or 'podman ps' - simplest check that works on all platforms
            var processInfo = new ProcessStartInfo
            {
                FileName = runtime,
                Arguments = "ps",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return false;
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the specified container runtime command is available on the system.
    /// </summary>
    /// <param name="runtime">The container runtime to check ("docker" or "podman").</param>
    private static async Task<bool> IsContainerRuntimeAvailableAsync(string runtime)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = runtime,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return false;
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to start the Docker daemon.
    /// Supports Windows (Docker Desktop) and Linux (systemd service).
    /// </summary>
    private static async Task<DockerStatus> TryStartDockerAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await TryStartDockerWindowsAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await TryStartDockerLinuxAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await TryStartDockerMacAsync();
        }

        return new DockerStatus(true, false, "Unsupported operating system for auto-start", null);
    }

    /// <summary>
    /// Attempts to start Docker Desktop on Windows.
    /// </summary>
    private static async Task<DockerStatus> TryStartDockerWindowsAsync()
    {
        try
        {
            LogMessage("🪟 Attempting to start Docker Desktop on Windows...");

            // Try to start Docker Desktop executable
            var dockerDesktopPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Docker", "Docker", "Docker Desktop.exe");

            if (!File.Exists(dockerDesktopPath))
            {
                LogMessage($"⚠️ Docker Desktop not found at: {dockerDesktopPath}");
                return new DockerStatus(false, false,
                    "Docker Desktop not installed. Please install Docker Desktop from https://www.docker.com/products/docker-desktop", "docker");
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = dockerDesktopPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return new DockerStatus(true, false, "Failed to start Docker Desktop process", "docker");
            }

            LogMessage("⏳ Docker Desktop starting... This may take 30-60 seconds...");

            // Wait for Docker to start (with timeout)
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(1000);
                if (await IsContainerRuntimeRunningAsync("docker"))
                {
                    LogMessage($"✅ Docker Desktop started successfully after {i + 1} seconds");
                    return new DockerStatus(true, true, "Docker Desktop started successfully", "docker");
                }
            }

            return new DockerStatus(true, false,
                "Docker Desktop started but not ready within 60 seconds. " +
                "It may still be initializing. Please wait and retry tests.", "docker");
        }
        catch (Exception ex)
        {
            LogMessage(ex, "❌ Error starting Docker Desktop on Windows");
            return new DockerStatus(true, false, $"Error starting Docker Desktop: {ex.Message}", "docker");
        }
    }

    /// <summary>
    /// Attempts to start Docker service on Linux using systemd.
    /// </summary>
    private static async Task<DockerStatus> TryStartDockerLinuxAsync()
    {
        try
        {
            LogMessage("🐧 Attempting to start Docker service on Linux...");

            // Try to start docker service using systemctl
            var processInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = "start docker",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return new DockerStatus(true, false, "Failed to start systemctl process", "docker");
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                LogMessage($"⚠️ systemctl start docker failed: {error}");

                // If permission denied, suggest sudo
                if (error.Contains("Permission denied") || error.Contains("authentication"))
                {
                    LogMessage("💡 Hint: You may need to run tests with sudo or add your user to the docker group:");
                    LogMessage("   sudo usermod -aG docker $USER");
                    LogMessage("   (requires logout/login to take effect)");

                    return new DockerStatus(true, false,
                        "Docker service exists but requires elevated permissions. " +
                        "Add your user to the docker group or run tests with sudo.", "docker");
                }

                return new DockerStatus(true, false, $"Failed to start Docker service: {error}", "docker");
            }

            LogMessage("✅ Docker service start command executed successfully");

            // Wait a moment for service to start
            await Task.Delay(3000);

            // Verify it's running
            if (await IsContainerRuntimeRunningAsync("docker"))
            {
                return new DockerStatus(true, true, "Docker service started successfully", "docker");
            }

            return new DockerStatus(true, false,
                "Docker service start command succeeded but daemon not yet responsive. " +
                "Check service status with: systemctl status docker", "docker");
        }
        catch (Exception ex)
        {
            LogMessage(ex, "❌ Error starting Docker service on Linux");
            return new DockerStatus(true, false, $"Error starting Docker service: {ex.Message}", "docker");
        }
    }

    /// <summary>
    /// Attempts to start Docker Desktop on macOS.
    /// </summary>
    private static async Task<DockerStatus> TryStartDockerMacAsync()
    {
        try
        {
            LogMessage("🍎 Attempting to start Docker Desktop on macOS...");

            var processInfo = new ProcessStartInfo
            {
                FileName = "open",
                Arguments = "-a Docker",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return new DockerStatus(true, false, "Failed to start Docker Desktop", "docker");
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                LogMessage($"⚠️ Failed to open Docker Desktop: {error}");
                return new DockerStatus(false, false,
                    "Docker Desktop not installed. Please install Docker Desktop from https://www.docker.com/products/docker-desktop", "docker");
            }

            LogMessage("⏳ Docker Desktop starting... This may take 30-60 seconds...");

            // Wait for Docker to start (with timeout)
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(1000);
                if (await IsContainerRuntimeRunningAsync("docker"))
                {
                    LogMessage($"✅ Docker Desktop started successfully after {i + 1} seconds");
                    return new DockerStatus(true, true, "Docker Desktop started successfully", "docker");
                }
            }

            return new DockerStatus(true, false,
                "Docker Desktop started but not ready within 60 seconds. " +
                "It may still be initializing. Please wait and retry tests.", "docker");
        }
        catch (Exception ex)
        {
            LogMessage(ex, "❌ Error starting Docker Desktop on macOS");
            return new DockerStatus(true, false, $"Error starting Docker Desktop: {ex.Message}", "docker");
        }
    }

    /// <summary>
    /// Logs a message using TestContext.Current.SendDiagnosticMessage().
    /// </summary>
    private static void LogMessage(string message)
    {
        try
        {
            TestContext.Current?.SendDiagnosticMessage(message);
        }
        catch
        {
            // Ignore if TestContext is not available
        }
    }

    /// <summary>
    /// Logs an error message with exception details.
    /// </summary>
    private static void LogMessage(Exception ex, string message)
    {
        try
        {
            var fullMessage = $"{message}\nException: {ex.GetType().Name}: {ex.Message}\nStackTrace: {ex.StackTrace}";
            TestContext.Current?.SendDiagnosticMessage(fullMessage);
        }
        catch
        {
            // Ignore if TestContext is not available
        }
    }
}

/// <summary>
/// Represents the status of container runtime (Docker or Podman).
/// </summary>
/// <param name="IsInstalled">Whether a container runtime is installed on the system.</param>
/// <param name="IsRunning">Whether the container runtime is currently running.</param>
/// <param name="Message">Detailed status message.</param>
/// <param name="Runtime">The runtime type ("docker", "podman", or null if not available).</param>
public record DockerStatus(bool IsInstalled, bool IsRunning, string Message, string? Runtime)
{
    /// <summary>
    /// Gets whether the status indicates success (runtime is available and running).
    /// </summary>
    public bool Success => IsInstalled && IsRunning;
};
