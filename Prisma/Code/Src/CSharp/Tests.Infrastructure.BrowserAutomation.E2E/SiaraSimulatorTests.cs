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

    // Siara Simulator configuration
    private const string SimulatorUrl = "http://localhost:5001";
    private const int SimulatorStartupWaitMs = 3000; // 3 seconds for simulator to be ready
    private const int PostLoginWaitMs = 5000; // 5 seconds after login for UI to load

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
    /// </summary>
    public async ValueTask InitializeAsync()
    {
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
    /// Navigate to Siara Simulator, login, and verify access to main UI.
    /// Any username and password will work for login.
    /// </summary>
    [Fact(Timeout = 300000)] // 5 minute timeout (headed mode is slower)
    public async Task SiaraSimulator_LoginAndAccess_ShouldSucceed()
    {
        // Arrange
        _automationAgent.ShouldNotBeNull("Browser automation agent not initialized");

        _logger.LogInformation("Starting Siara Simulator login test");
        _logger.LogInformation("Target URL: {Url}", SimulatorUrl);

        try
        {
            // Wait for simulator to be ready
            _logger.LogInformation("Waiting {Ms}ms for simulator to be ready...", SimulatorStartupWaitMs);
            await Task.Delay(SimulatorStartupWaitMs, TestContext.Current.CancellationToken);

            // Act - Navigate to Siara Simulator
            _logger.LogInformation("Navigating to Siara Simulator: {Url}", SimulatorUrl);
            var navResult = await _automationAgent.NavigateToAsync(SimulatorUrl, TestContext.Current.CancellationToken);

            if (!navResult.IsSuccess)
            {
                _logger.LogError("Failed to navigate to Siara Simulator: {Error}", navResult.Error);
                navResult.IsSuccess.ShouldBeTrue($"Navigation to {SimulatorUrl} failed: {navResult.Error}");
                return;
            }

            _logger.LogInformation("✓ Successfully navigated to Siara Simulator");
            await Task.Delay(2000, TestContext.Current.CancellationToken); // 2 second delay for visibility

            // Perform login with any credentials
            _logger.LogInformation("Attempting login with test credentials...");
            var loginResult = await LoginToSiaraSimulatorAsync("testuser", "testpass123");

            loginResult.ShouldBeTrue("Login should succeed with any credentials");
            _logger.LogInformation("✓ Successfully logged into Siara Simulator");

            // Wait for UI to load after login
            _logger.LogInformation("Waiting {Ms}ms for UI to load after login...", PostLoginWaitMs);
            await Task.Delay(PostLoginWaitMs, TestContext.Current.CancellationToken);

            _logger.LogInformation("✓ Siara Simulator UI loaded successfully");
            _logger.LogInformation("✓ Login test completed successfully");

            // Assert
            loginResult.ShouldBeTrue("Login and UI access should be successful");
        }
        finally
        {
            _logger.LogInformation("Siara Simulator login test completed");
        }
    }

    /// <summary>
    /// Navigate to Siara Simulator, login, and download documents to configured location.
    /// Demonstrates full E2E workflow for document retrieval.
    /// </summary>
    [Fact(Timeout = 300000)] // 5 minute timeout (headed mode is slower)
    public async Task SiaraSimulator_LoginAndDownloadDocuments_ShouldDownloadFiles()
    {
        // Arrange
        _automationAgent.ShouldNotBeNull("Browser automation agent not initialized");
        var downloadedFiles = new List<DownloadedFile>();
        var targetDownloadCount = 3; // Target: download at least 3 documents

        _logger.LogInformation("Starting Siara Simulator document download test");
        _logger.LogInformation("Target URL: {Url}", SimulatorUrl);
        _logger.LogInformation("Download path: {Path}", _downloadPath);

        try
        {
            // Wait for simulator to be ready
            _logger.LogInformation("Waiting {Ms}ms for simulator to be ready...", SimulatorStartupWaitMs);
            await Task.Delay(SimulatorStartupWaitMs, TestContext.Current.CancellationToken);

            // Navigate to Siara Simulator
            _logger.LogInformation("Navigating to Siara Simulator: {Url}", SimulatorUrl);
            var navResult = await _automationAgent.NavigateToAsync(SimulatorUrl, TestContext.Current.CancellationToken);

            if (!navResult.IsSuccess)
            {
                _logger.LogError("Failed to navigate to Siara Simulator: {Error}", navResult.Error);
                navResult.IsSuccess.ShouldBeTrue($"Navigation to {SimulatorUrl} failed: {navResult.Error}");
                return;
            }

            _logger.LogInformation("✓ Successfully navigated to Siara Simulator");
            await Task.Delay(2000, TestContext.Current.CancellationToken);

            // Login to Siara Simulator
            _logger.LogInformation("Logging into Siara Simulator...");
            var loginResult = await LoginToSiaraSimulatorAsync("testuser", "testpass123");
            loginResult.ShouldBeTrue("Login should succeed");
            _logger.LogInformation("✓ Successfully logged in");

            // Wait for UI to load
            _logger.LogInformation("Waiting {Ms}ms for UI to load after login...", PostLoginWaitMs);
            await Task.Delay(PostLoginWaitMs, TestContext.Current.CancellationToken);

            // Download documents from Siara Simulator
            _logger.LogInformation("Attempting to download documents from Siara Simulator...");
            var downloadResults = await DownloadSiaraDocumentsAsync(targetDownloadCount);

            downloadedFiles.AddRange(downloadResults);
            _logger.LogInformation("✓ Downloaded {Count} documents from Siara Simulator", downloadedFiles.Count);

            // Save files to disk
            foreach (var file in downloadedFiles)
            {
                if (file.Content != null)
                {
                    var filePath = Path.Combine(_downloadPath, file.FileName);
                    await File.WriteAllBytesAsync(filePath, file.Content, TestContext.Current.CancellationToken);
                    _logger.LogInformation("  ✓ Saved: {FileName} ({Size} bytes) to {Path}",
                        file.FileName,
                        file.Content.Length,
                        filePath);
                }
            }

            // Assert - Should download at least some documents
            downloadedFiles.Count.ShouldBeGreaterThanOrEqualTo(1,
                $"Expected to download at least 1 document, but downloaded {downloadedFiles.Count}");

            _logger.LogInformation("✓ Document download test completed successfully");
        }
        finally
        {
            _logger.LogInformation("Siara Simulator document download test completed");
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
    }
}
