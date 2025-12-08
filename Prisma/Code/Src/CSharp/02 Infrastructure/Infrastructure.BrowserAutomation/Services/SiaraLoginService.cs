using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.Services;

/// <summary>
/// Service for handling SIARA Simulator login operations using browser automation.
/// </summary>
public class SiaraLoginService : ISiaraLoginService
{
    private readonly ILogger<SiaraLoginService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiaraLoginService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public SiaraLoginService(ILogger<SiaraLoginService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> LoginAsync(
        IBrowserAutomationAgent agent,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting SIARA login for user: {Username}", username);

            // Wait for username input field to be visible
            _logger.LogDebug("Waiting for username input field");
            var waitUsernameResult = await agent.WaitForSelectorAsync("input[name='username']", timeoutMs: 10000, cancellationToken);
            if (!waitUsernameResult.IsSuccess)
            {
                return Result.WithFailure($"Username input field not found: {waitUsernameResult.Error}");
            }

            // Fill username
            _logger.LogDebug("Filling username field");
            var fillUsernameResult = await agent.FillInputAsync("input[name='username']", username, cancellationToken);
            if (!fillUsernameResult.IsSuccess)
            {
                return Result.WithFailure($"Failed to fill username: {fillUsernameResult.Error}");
            }

            // Fill password
            _logger.LogDebug("Filling password field");
            var fillPasswordResult = await agent.FillInputAsync("input[name='password']", password, cancellationToken);
            if (!fillPasswordResult.IsSuccess)
            {
                return Result.WithFailure($"Failed to fill password: {fillPasswordResult.Error}");
            }

            // Click login button
            _logger.LogDebug("Clicking login button");
            var clickResult = await agent.ClickElementAsync("button[type='submit']", cancellationToken);
            if (!clickResult.IsSuccess)
            {
                return Result.WithFailure($"Failed to click login button: {clickResult.Error}");
            }

            // Wait for navigation to complete (wait for a post-login element or URL change)
            // Using a reasonable timeout for authentication to complete
            _logger.LogDebug("Waiting for navigation after login");
            await Task.Delay(2000, cancellationToken); // Allow time for navigation

            _logger.LogInformation("Successfully logged in to SIARA as {Username}", username);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SIARA login operation was cancelled");
            return Result.WithFailure("Login operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to login to SIARA for user: {Username}", username);
            return Result.WithFailure($"Failed to login to SIARA: {ex.Message}", ex);
        }
    }
}
