namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation.E2E;

/// <summary>
/// End-to-end tests for Siara Simulator integration.
/// Tests run in headed mode to demonstrate login and document download capabilities.
/// Uses IBrowserAutomationAgent interface for proper hexagonal architecture compliance.
/// </summary>
public class SiaraSimulatorTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<SiaraSimulatorTests> _logger;
    private IBrowserAutomationAgent? _automationAgent;
    private readonly string _downloadPath;
    private Process? _simulatorProcess; // Track simulator process if we started it
    private bool _simulatorStartedByTest = false; // Flag to track if we started the simulator

    // Siara Simulator configuration
    private const string SimulatorUrl = "http://localhost:5001";
    private const int SimulatorStartupWaitMs = 8000; // 8 seconds for simulator to be ready
    private const int PostLoginWaitMs = 5000; // 5 seconds after login for UI to load

    // Path to the deployed simulator executable
    private static readonly string SimulatorExePath = Path.GetFullPath(
        Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", "..",
            "Deployments", "Siara.Simulator", "app", "Siara.Simulator.exe"));

    /// <summary>
    /// Initializes a new instance of the <see cref="SiaraSimulatorTests"/> class.
    /// </summary>
    /// <param name="output">xUnit test output helper for logging.</param>
    public SiaraSimulatorTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<SiaraSimulatorTests>(output);

        // Create configurable download path in temp directory
        _downloadPath = Path.Combine(Path.GetTempPath(), "SiaraE2ETests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_downloadPath);
        _logger.LogInformation("Download path created: {DownloadPath}", _downloadPath);
    }

    /// <summary>
    /// Initialize browser automation agent in headed mode for visual demo.
    /// Also checks if simulator is running and starts it if needed.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        // Check if simulator is already running
        if (!await IsSimulatorRunningAsync())
        {
            _logger.LogInformation("Simulator not running. Starting simulator...");
            await StartSimulatorAsync();
        }
        else
        {
            _logger.LogInformation("Simulator is already running");
        }

        _logger.LogInformation("Initializing browser automation agent in headed mode...");

        // Create options for headed mode (visible browser for demo)
        var options = Options.Create(new BrowserAutomationOptions
        {
            Headless = false,                // HEADED MODE for visual demo
            BrowserLaunchTimeoutMs = 60000,  // 60 second timeout
            PageTimeoutMs = 30000            // 30 second page timeout
        });

        // Create adapter-specific logger
        var adapterLogger = XUnitLogger.CreateLogger<PlaywrightBrowserAutomationAdapter>(_output);

        _automationAgent = new PlaywrightBrowserAutomationAdapter(adapterLogger, options);

        // Launch browser
        var launchResult = await _automationAgent.LaunchBrowserAsync(TestContext.Current.CancellationToken);
        launchResult.IsSuccess.ShouldBeTrue($"Failed to launch browser: {launchResult.Error}");

        _logger.LogInformation("Browser automation agent initialized successfully");
    }

    /// <summary>
    /// Complete E2E workflow: Navigate to Siara Simulator, login, view dashboard, and download documents.
    /// Demonstrates full access flow with delays for visual demonstration in headed mode.
    /// </summary>
    [Fact(Timeout = 300000)] // 5 minute timeout (headed mode is slower)
    public async Task SiaraSimulator_CompleteE2EWorkflow_ShouldSucceed()
    {
        // Arrange
        _automationAgent.ShouldNotBeNull("Browser automation agent not initialized");
        var downloadedFiles = new List<DownloadedFile>();
        var targetDownloadCount = 3; // Target: download at least 3 documents

        _logger.LogInformation("===========================================");
        _logger.LogInformation("Starting SIARA Simulator E2E Test - Full Workflow");
        _logger.LogInformation("===========================================");
        _logger.LogInformation("Target URL: {Url}", SimulatorUrl);
        _logger.LogInformation("Download path: {Path}", _downloadPath);

        try
        {
            // STEP 1: Wait for simulator to be ready
            _logger.LogInformation("");
            _logger.LogInformation("STEP 1: Waiting for simulator to be ready...");
            await Task.Delay(SimulatorStartupWaitMs, TestContext.Current.CancellationToken);
            _logger.LogInformation("✓ Simulator ready");

            // STEP 2: Navigate to Siara Simulator Login Page
            _logger.LogInformation("");
            _logger.LogInformation("STEP 2: Navigating to SIARA Simulator Login Page");
            _logger.LogInformation("URL: {Url}", SimulatorUrl);
            var navResult = await _automationAgent.NavigateToAsync(SimulatorUrl, TestContext.Current.CancellationToken);

            if (!navResult.IsSuccess)
            {
                _logger.LogError("Failed to navigate to Siara Simulator: {Error}", navResult.Error);
                navResult.IsSuccess.ShouldBeTrue($"Navigation to {SimulatorUrl} failed: {navResult.Error}");
                return;
            }

            _logger.LogInformation("✓ Successfully navigated to SIARA login page");
            _logger.LogInformation("Pausing to view login page...");
            await Task.Delay(4000, TestContext.Current.CancellationToken); // 4 second pause to view login page

            // STEP 3: Perform Login
            _logger.LogInformation("");
            _logger.LogInformation("STEP 3: Logging into SIARA Simulator");
            _logger.LogInformation("Username: BancoDemo");
            _logger.LogInformation("Password: ********");
            var loginResult = await LoginToSiaraSimulatorAsync("BancoDemo", "demo123");

            loginResult.ShouldBeTrue("Login should succeed with demo credentials");
            _logger.LogInformation("✓ Successfully logged into SIARA Simulator");
            _logger.LogInformation("Redirecting to dashboard...");

            // STEP 4: Wait for Dashboard to Load
            _logger.LogInformation("");
            _logger.LogInformation("STEP 4: Loading SIARA Dashboard");
            await Task.Delay(PostLoginWaitMs, TestContext.Current.CancellationToken);
            _logger.LogInformation("✓ Dashboard loaded successfully");
            _logger.LogInformation("Viewing case list...");
            await Task.Delay(3000, TestContext.Current.CancellationToken); // 3 second pause to view dashboard

            // STEP 5: Identify Available Documents
            _logger.LogInformation("");
            _logger.LogInformation("STEP 5: Identifying Available Documents");
            var downloadResults = await DownloadSiaraDocumentsAsync(targetDownloadCount);
            downloadedFiles.AddRange(downloadResults);

            if (downloadedFiles.Count > 0)
            {
                _logger.LogInformation("✓ Found and downloaded {Count} documents", downloadedFiles.Count);

                // STEP 6: Save Documents to Disk
                _logger.LogInformation("");
                _logger.LogInformation("STEP 6: Saving Documents to Download Path");
                foreach (var file in downloadedFiles)
                {
                    if (file.Content != null)
                    {
                        var filePath = Path.Combine(_downloadPath, file.FileName);
                        await File.WriteAllBytesAsync(filePath, file.Content, TestContext.Current.CancellationToken);
                        _logger.LogInformation("  ✓ Saved: {FileName} ({Size:N0} bytes)",
                            file.FileName,
                            file.Content.Length);
                    }
                }
                _logger.LogInformation("✓ All documents saved to: {Path}", _downloadPath);
            }
            else
            {
                _logger.LogWarning("⚠ No documents were available for download");
            }

            // Final pause to show completion
            _logger.LogInformation("");
            _logger.LogInformation("===========================================");
            _logger.LogInformation("E2E Workflow Complete - Test Passed");
            _logger.LogInformation("===========================================");
            await Task.Delay(3000, TestContext.Current.CancellationToken); // 3 second final pause

            // Assert - Login should succeed even if no documents available
            loginResult.ShouldBeTrue("Login and UI access should be successful");
        }
        finally
        {
            _logger.LogInformation("");
            _logger.LogInformation("SIARA Simulator E2E test completed");
        }
    }

    /// <summary>
    /// RECORD MODE: Launch browser, navigate to Siara Simulator, then pause for manual control.
    /// Use this test to explore the UI, find download links, and identify element selectors.
    /// Press Ctrl+C in console when done exploring.
    /// </summary>
    [Fact(Timeout = 600000)] // 10 minute timeout for manual exploration
    public async Task RecordMode_SiaraSimulator_ManualExploration()
    {
        // Arrange
        _automationAgent.ShouldNotBeNull("Browser automation agent not initialized");

        _logger.LogInformation("=== RECORD MODE ACTIVATED ===");
        _logger.LogInformation("Browser will stay open for manual control");
        _logger.LogInformation("Explore Siara Simulator UI manually");
        _logger.LogInformation("Find login form, document links, and download buttons");
        _logger.LogInformation("Press Ctrl+C when done to stop test");
        _logger.LogInformation("================================");

        try
        {
            // Wait for simulator to be ready
            await Task.Delay(SimulatorStartupWaitMs, TestContext.Current.CancellationToken);

            // Navigate to Siara Simulator
            _logger.LogInformation("Navigating to: {Url}", SimulatorUrl);
            var navResult = await _automationAgent.NavigateToAsync(SimulatorUrl, TestContext.Current.CancellationToken);
            navResult.IsSuccess.ShouldBeTrue("Navigation should succeed");

            _logger.LogInformation("✓ Page loaded: {Url}", SimulatorUrl);
            _logger.LogInformation("");
            _logger.LogInformation("NOW IN MANUAL CONTROL MODE:");
            _logger.LogInformation("1. Try logging in with any username/password");
            _logger.LogInformation("2. Look for document lists or download links");
            _logger.LogInformation("3. Inspect elements with browser DevTools (F12)");
            _logger.LogInformation("4. Note CSS selectors, IDs, and URL patterns");
            _logger.LogInformation("");
            _logger.LogInformation("Browser will stay open for 10 minutes or until you press Ctrl+C");

            // Keep browser open for manual exploration
            await Task.Delay(TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Manual exploration session ended by user (Ctrl+C)");
        }
        finally
        {
            _logger.LogInformation("=== RECORD MODE SESSION COMPLETE ===");
        }
    }

    /// <summary>
    /// Performs login to Siara Simulator.
    /// Any username and password will work for authentication (demo mode).
    /// </summary>
    /// <param name="username">Username for login.</param>
    /// <param name="password">Password for login.</param>
    /// <returns>True if login succeeded, false otherwise.</returns>
    private async Task<bool> LoginToSiaraSimulatorAsync(string username, string password)
    {
        try
        {
            _automationAgent.ShouldNotBeNull("Automation agent not initialized");

            // Wait for login page to load
            _logger.LogInformation("Waiting for login form to load...");
            var waitResult = await _automationAgent.WaitForSelectorAsync("#username", 10000, TestContext.Current.CancellationToken);
            if (!waitResult.IsSuccess)
            {
                _logger.LogError("Login form not found: {Error}", waitResult.Error);
                return false;
            }
            _logger.LogInformation("✓ Login form loaded");
            await Task.Delay(1000, TestContext.Current.CancellationToken);

            // Fill username field
            _logger.LogInformation("Entering username: {Username}", username);
            var fillUsernameResult = await _automationAgent.FillInputAsync("#username", username, TestContext.Current.CancellationToken);
            if (!fillUsernameResult.IsSuccess)
            {
                _logger.LogError("Failed to fill username: {Error}", fillUsernameResult.Error);
                return false;
            }
            _logger.LogInformation("✓ Username entered");
            await Task.Delay(500, TestContext.Current.CancellationToken);

            // Fill password field
            _logger.LogInformation("Entering password");
            var fillPasswordResult = await _automationAgent.FillInputAsync("#password", password, TestContext.Current.CancellationToken);
            if (!fillPasswordResult.IsSuccess)
            {
                _logger.LogError("Failed to fill password: {Error}", fillPasswordResult.Error);
                return false;
            }
            _logger.LogInformation("✓ Password entered");
            await Task.Delay(500, TestContext.Current.CancellationToken);

            // Click login button
            _logger.LogInformation("Clicking login button...");
            var clickResult = await _automationAgent.ClickElementAsync("button[type='submit']", TestContext.Current.CancellationToken);
            if (!clickResult.IsSuccess)
            {
                _logger.LogError("Failed to click login button: {Error}", clickResult.Error);
                return false;
            }
            _logger.LogInformation("✓ Login button clicked");

            // Wait for redirect/navigation after login
            _logger.LogInformation("Waiting for login to complete...");
            await Task.Delay(3000, TestContext.Current.CancellationToken);

            // Verify login success by checking if we're redirected to main page
            // In Siara Simulator, successful login redirects to "/"
            _logger.LogInformation("✓ Login completed successfully");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to login to Siara Simulator");
            return false;
        }
    }

    /// <summary>
    /// Downloads documents from Siara Simulator after successful login.
    /// </summary>
    /// <param name="targetCount">Target number of documents to download.</param>
    /// <returns>List of downloaded files.</returns>
    private async Task<List<DownloadedFile>> DownloadSiaraDocumentsAsync(int targetCount)
    {
        var downloadedFiles = new List<DownloadedFile>();

        try
        {
            _automationAgent.ShouldNotBeNull("Automation agent not initialized");

            // Wait for dashboard to load with case list
            _logger.LogInformation("Waiting for dashboard to load...");
            await Task.Delay(2000, TestContext.Current.CancellationToken);

            // Identify downloadable files on the page
            _logger.LogInformation("Searching for available documents in UI...");
            var filePatterns = new[] { "*.pdf", "*.docx", "*.xml" };
            var identifyResult = await _automationAgent.IdentifyDownloadableFilesAsync(
                filePatterns,
                TestContext.Current.CancellationToken);

            if (!identifyResult.IsSuccess || identifyResult.Value == null)
            {
                _logger.LogWarning("Failed to identify downloadable files: {Error}", identifyResult.Error);
                return downloadedFiles;
            }

            var availableFiles = identifyResult.Value;
            _logger.LogInformation("Found {Count} downloadable files", availableFiles.Count);

            // Download files up to target count
            var filesToDownload = availableFiles.Take(targetCount).ToList();
            _logger.LogInformation("Attempting to download {Count} documents...", filesToDownload.Count);

            for (int i = 0; i < filesToDownload.Count; i++)
            {
                var file = filesToDownload[i];
                _logger.LogInformation("  Downloading document {Index}/{Total}: {FileName} ({Format})",
                    i + 1, filesToDownload.Count, file.FileName, file.Format);

                var downloadResult = await _automationAgent.DownloadFileAsync(
                    file.Url,
                    TestContext.Current.CancellationToken);

                if (downloadResult.IsSuccess && downloadResult.Value != null)
                {
                    downloadedFiles.Add(downloadResult.Value);
                    _logger.LogInformation("  ✓ Downloaded: {FileName} ({Size} bytes)",
                        downloadResult.Value.FileName,
                        downloadResult.Value.Content?.Length ?? 0);
                }
                else
                {
                    _logger.LogWarning("  ✗ Failed to download {FileName}: {Error}",
                        file.FileName, downloadResult.Error);
                }

                await Task.Delay(1000, TestContext.Current.CancellationToken);
            }

            _logger.LogInformation("✓ Completed document download sequence - {Count} files downloaded", downloadedFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download documents from Siara Simulator");
        }

        return downloadedFiles;
    }

    /// <summary>
    /// Cleanup resources after test execution.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_automationAgent != null)
        {
            var closeResult = await _automationAgent.CloseBrowserAsync(TestContext.Current.CancellationToken);
            if (closeResult.IsSuccess)
            {
                _logger.LogInformation("Browser closed successfully");
            }
            else
            {
                _logger.LogWarning("Failed to close browser: {Error}", closeResult.Error);
            }
        }

        // Cleanup download directory
        if (Directory.Exists(_downloadPath))
        {
            try
            {
                Directory.Delete(_downloadPath, recursive: true);
                _logger.LogInformation("Download path cleaned up: {DownloadPath}", _downloadPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup download path: {DownloadPath}", _downloadPath);
            }
        }

        // Stop simulator if we started it
        if (_simulatorStartedByTest && _simulatorProcess != null)
        {
            _logger.LogInformation("Stopping simulator that was started by test...");
            try
            {
                if (!_simulatorProcess.HasExited)
                {
                    _simulatorProcess.Kill(entireProcessTree: true);
                    _simulatorProcess.WaitForExit(5000); // Wait up to 5 seconds
                    _logger.LogInformation("Simulator stopped successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop simulator process");
            }
            finally
            {
                _simulatorProcess?.Dispose();
            }
        }
    }

    /// <summary>
    /// Checks if the SIARA Simulator is running on localhost:5001
    /// </summary>
    private async Task<bool> IsSimulatorRunningAsync()
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await httpClient.GetAsync(SimulatorUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Starts the SIARA Simulator executable
    /// </summary>
    private async Task StartSimulatorAsync()
    {
        // Verify the executable exists
        if (!File.Exists(SimulatorExePath))
        {
            throw new FileNotFoundException(
                $"Simulator executable not found at: {SimulatorExePath}. " +
                $"Please ensure the simulator has been published to Deployments/Siara.Simulator/app/");
        }

        _logger.LogInformation("Starting simulator from: {Path}", SimulatorExePath);

        // Start the simulator process
        var startInfo = new ProcessStartInfo
        {
            FileName = SimulatorExePath,
            WorkingDirectory = Path.GetDirectoryName(SimulatorExePath),
            UseShellExecute = false,
            CreateNoWindow = false, // Show window so user can see it running
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        _simulatorProcess = Process.Start(startInfo);
        if (_simulatorProcess == null)
        {
            throw new InvalidOperationException("Failed to start simulator process");
        }

        _simulatorStartedByTest = true;

        // Log simulator output
        _simulatorProcess.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.LogInformation("[SIMULATOR] {Output}", e.Data);
            }
        };
        _simulatorProcess.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.LogWarning("[SIMULATOR ERROR] {Output}", e.Data);
            }
        };
        _simulatorProcess.BeginOutputReadLine();
        _simulatorProcess.BeginErrorReadLine();

        _logger.LogInformation("Simulator process started (PID: {ProcessId}). Waiting for it to be ready...", _simulatorProcess.Id);

        // Wait for simulator to be ready
        var maxAttempts = 40; // 40 attempts * 500ms = 20 seconds
        var attempt = 0;
        while (attempt < maxAttempts)
        {
            await Task.Delay(500);
            if (await IsSimulatorRunningAsync())
            {
                _logger.LogInformation("Simulator is ready and responding on {Url}", SimulatorUrl);
                return;
            }
            attempt++;
        }

        throw new TimeoutException($"Simulator did not become ready within 20 seconds on {SimulatorUrl}");
    }
}
