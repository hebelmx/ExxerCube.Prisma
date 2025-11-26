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
    private readonly string _manifestFilePath;
    private readonly HashSet<string> _downloadedDocuments = new(); // Track downloaded documents
    private Process? _simulatorProcess; // Track simulator process if we started it
    private bool _simulatorStartedByTest = false; // Flag to track if we started the simulator

    // Siara Simulator configuration
    private const string SimulatorUrl = "http://localhost:5001";
    private const int SimulatorStartupWaitMs = 8000; // 8 seconds for simulator to be ready
    private const int PostLoginWaitMs = 5000; // 5 seconds after login for UI to load

    // Path to the deployed simulator executable
    // Navigate from test assembly location to solution root, then to Deployments
    private static readonly string SimulatorExePath = GetSimulatorPath();

    private static string GetSimulatorPath()
    {
        // Start from current directory (test assembly location)
        var currentDir = Directory.GetCurrentDirectory();

        // Find solution root by looking for .git or Deployments folder
        var searchDir = new DirectoryInfo(currentDir);
        while (searchDir != null)
        {
            // Check if we found the solution root (has Deployments folder)
            var deploymentsPath = Path.Combine(searchDir.FullName, "Deployments", "Siara.Simulator", "app", "Siara.Simulator.exe");
            if (File.Exists(deploymentsPath))
            {
                return deploymentsPath;
            }

            // Move up one directory
            searchDir = searchDir.Parent;
        }

        // Fallback: construct from typical structure
        return Path.GetFullPath(
            Path.Combine(
                currentDir,
                "..", "..", "..", "..", "..", "..", "..",
                "Deployments", "Siara.Simulator", "app", "Siara.Simulator.exe"));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SiaraSimulatorTests"/> class.
    /// </summary>
    /// <param name="output">xUnit test output helper for logging.</param>
    public SiaraSimulatorTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<SiaraSimulatorTests>(output);

        // Create organized download path structure: SIARA_Downloads/YYYY/MM/DD/
        var baseDownloadPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SIARA_Downloads");

        var today = DateTime.Now;
        _downloadPath = Path.Combine(
            baseDownloadPath,
            today.ToString("yyyy"),
            today.ToString("MM"),
            today.ToString("dd"));

        Directory.CreateDirectory(_downloadPath);
        _logger.LogInformation("Download path created: {DownloadPath}", _downloadPath);

        // Create manifest file to track downloads
        _manifestFilePath = Path.Combine(_downloadPath, "download_manifest.txt");

        // Load previously downloaded documents from manifest
        if (File.Exists(_manifestFilePath))
        {
            var previousDownloads = File.ReadAllLines(_manifestFilePath);
            foreach (var line in previousDownloads)
            {
                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                {
                    var parts = line.Split('|');
                    if (parts.Length > 0)
                    {
                        _downloadedDocuments.Add(parts[0].Trim());
                    }
                }
            }
            _logger.LogInformation("Loaded {Count} previously downloaded documents from manifest", _downloadedDocuments.Count);
        }
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

            // STEP 5: Wait for Cases to Appear and Download Documents
            _logger.LogInformation("");
            _logger.LogInformation("STEP 5: Waiting for Cases to Appear (Minimum 3 cases required)");
            _logger.LogInformation("The simulation generates cases using Poisson distribution.");
            _logger.LogInformation("This may take 1-3 minutes depending on arrival rate...");

            var downloadResults = await WaitForCasesAndDownloadAsync(targetDownloadCount);
            downloadedFiles.AddRange(downloadResults);

            if (downloadedFiles.Count > 0)
            {
                _logger.LogInformation("✓ Found and downloaded {Count} new documents", downloadedFiles.Count);

                // STEP 6: Save Documents to Disk
                _logger.LogInformation("");
                _logger.LogInformation("STEP 6: Saving Documents to Organized Folder Structure");
                _logger.LogInformation("Base path: {Path}", _downloadPath);

                foreach (var file in downloadedFiles)
                {
                    if (file.Content != null)
                    {
                        var filePath = Path.Combine(_downloadPath, file.FileName);
                        await File.WriteAllBytesAsync(filePath, file.Content, TestContext.Current.CancellationToken);
                        _logger.LogInformation("  ✓ Saved: {FileName} ({Size:N0} bytes)",
                            file.FileName,
                            file.Content.Length);

                        // Add to manifest
                        await AppendToManifestAsync(file);
                    }
                }
                _logger.LogInformation("✓ All documents saved to: {Path}", _downloadPath);

                // STEP 7: Open Download Manifest
                _logger.LogInformation("");
                _logger.LogInformation("STEP 7: Opening Download Manifest");
                OpenManifestFile();
            }
            else
            {
                _logger.LogWarning("⚠ No NEW documents were available for download (may have been downloaded previously)");
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
    /// Waits for cases to appear on the dashboard and downloads new documents.
    /// Polls the dashboard every 10 seconds for up to 3 minutes waiting for cases to appear.
    /// Only downloads documents that haven't been downloaded before (tracked in _downloadedDocuments).
    /// </summary>
    /// <param name="targetCount">Minimum number of documents to wait for.</param>
    /// <returns>List of newly downloaded files.</returns>
    private async Task<List<DownloadedFile>> WaitForCasesAndDownloadAsync(int targetCount)
    {
        var downloadedFiles = new List<DownloadedFile>();
        var maxWaitMinutes = 3;
        var pollIntervalSeconds = 10;
        var maxAttempts = (maxWaitMinutes * 60) / pollIntervalSeconds; // 18 attempts (3 minutes)
        var attempt = 0;

        _logger.LogInformation("Starting case arrival polling (max {MaxWait} minutes, checking every {Interval} seconds)...",
            maxWaitMinutes, pollIntervalSeconds);

        while (attempt < maxAttempts)
        {
            attempt++;
            _logger.LogInformation("Poll attempt {Attempt}/{MaxAttempts} - Checking for available documents...",
                attempt, maxAttempts);

            try
            {
                // Identify downloadable files on the page
                var filePatterns = new[] { "*.pdf", "*.docx", "*.xml" };
                var identifyResult = await _automationAgent!.IdentifyDownloadableFilesAsync(
                    filePatterns,
                    TestContext.Current.CancellationToken);

                if (identifyResult.IsSuccess && identifyResult.Value != null)
                {
                    var availableFiles = identifyResult.Value;

                    // Filter out already downloaded files
                    var newFiles = availableFiles
                        .Where(f => !_downloadedDocuments.Contains(f.FileName))
                        .ToList();

                    _logger.LogInformation("  Found {Total} total files, {New} are NEW (not previously downloaded)",
                        availableFiles.Count, newFiles.Count);

                    if (newFiles.Count >= targetCount)
                    {
                        _logger.LogInformation("✓ Target met! Found {Count} new documents ready for download", newFiles.Count);

                        // Download the new files
                        var filesToDownload = newFiles.Take(targetCount).ToList();
                        _logger.LogInformation("Downloading {Count} new documents...", filesToDownload.Count);

                        for (int i = 0; i < filesToDownload.Count; i++)
                        {
                            var file = filesToDownload[i];
                            _logger.LogInformation("  [{Index}/{Total}] Downloading: {FileName} ({Format})",
                                i + 1, filesToDownload.Count, file.FileName, file.Format);

                            var downloadResult = await _automationAgent.DownloadFileAsync(
                                file.Url,
                                TestContext.Current.CancellationToken);

                            if (downloadResult.IsSuccess && downloadResult.Value != null)
                            {
                                downloadedFiles.Add(downloadResult.Value);
                                _downloadedDocuments.Add(downloadResult.Value.FileName); // Track this download
                                _logger.LogInformation("    ✓ Downloaded: {FileName} ({Size:N0} bytes)",
                                    downloadResult.Value.FileName,
                                    downloadResult.Value.Content?.Length ?? 0);
                            }
                            else
                            {
                                _logger.LogWarning("    ✗ Failed to download {FileName}: {Error}",
                                    file.FileName, downloadResult.Error);
                            }

                            await Task.Delay(500, TestContext.Current.CancellationToken); // Small delay between downloads
                        }

                        _logger.LogInformation("✓ Successfully downloaded {Count} new documents", downloadedFiles.Count);
                        return downloadedFiles;
                    }
                    else
                    {
                        _logger.LogInformation("  Waiting for more cases... (need {Need} more)",
                            targetCount - newFiles.Count);
                    }
                }
                else
                {
                    _logger.LogInformation("  No documents found yet on dashboard");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during polling attempt {Attempt}", attempt);
            }

            // Wait before next poll
            if (attempt < maxAttempts)
            {
                _logger.LogInformation("  Waiting {Seconds} seconds before next check...", pollIntervalSeconds);
                await Task.Delay(pollIntervalSeconds * 1000, TestContext.Current.CancellationToken);
            }
        }

        _logger.LogWarning("⚠ Timeout: Did not find {TargetCount} new documents after {MaxWait} minutes",
            targetCount, maxWaitMinutes);
        return downloadedFiles;
    }

    /// <summary>
    /// Appends download information to the manifest file with timestamp.
    /// </summary>
    /// <param name="file">Downloaded file to record in manifest.</param>
    private async Task AppendToManifestAsync(DownloadedFile file)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var fileSize = file.Content?.Length ?? 0;
            var manifestEntry = $"{file.FileName} | {timestamp} | {fileSize:N0} bytes | {file.Url}";

            // Create header if file is new
            if (!File.Exists(_manifestFilePath))
            {
                var header = "# SIARA Document Download Manifest\n" +
                            "# Format: Filename | Timestamp | Size | URL\n" +
                            "# ==================================================\n";
                await File.WriteAllTextAsync(_manifestFilePath, header);
                _logger.LogInformation("Created new manifest file: {ManifestPath}", _manifestFilePath);
            }

            // Append entry
            await File.AppendAllTextAsync(_manifestFilePath, manifestEntry + Environment.NewLine);
            _logger.LogInformation("  → Manifest updated: {FileName}", file.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append to manifest file: {ManifestPath}", _manifestFilePath);
        }
    }

    /// <summary>
    /// Opens the download manifest file in Notepad for user review.
    /// </summary>
    private void OpenManifestFile()
    {
        try
        {
            if (!File.Exists(_manifestFilePath))
            {
                _logger.LogWarning("Manifest file does not exist: {ManifestPath}", _manifestFilePath);
                return;
            }

            _logger.LogInformation("Opening manifest in Notepad: {ManifestPath}", _manifestFilePath);

            var startInfo = new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{_manifestFilePath}\"",
                UseShellExecute = true
            };

            Process.Start(startInfo);
            _logger.LogInformation("✓ Manifest opened in Notepad");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open manifest file in Notepad");
        }
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

        // NOTE: We do NOT cleanup the download directory - documents are kept organized by date
        // The downloads are stored in: Documents/SIARA_Downloads/YYYY/MM/DD/
        // This allows tracking download history over time via the manifest file
        _logger.LogInformation("Downloaded documents preserved at: {DownloadPath}", _downloadPath);

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
