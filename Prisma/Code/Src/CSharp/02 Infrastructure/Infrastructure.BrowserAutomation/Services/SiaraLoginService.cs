using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;
using IndQuestResults;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.Services;

/// <summary>
/// Service for handling SIARA Simulator login operations using browser automation.
/// </summary>
/// <remarks>
/// The raw-credential form driver invoked by the <c>AutomatedLogin</c> provider with transient,
/// vault-sourced credentials. Per ADR-010 P6 it deliberately takes <strong>no</strong> configuration
/// access (only a logger and the secret-free <see cref="SiaraHostPolicy"/>), so it can never read persisted
/// credentials itself; and per ADR-010 P8 it consults the host policy <strong>before</strong> entering any
/// credentials, failing closed on the real SIARA production host unless a deployment has explicitly opted
/// in. This is what structurally prevents the fake-credential simulator + automation tooling from being
/// repointed at the regulator's portal.
/// </remarks>
public class SiaraLoginService : ISiaraLoginService
{
    private readonly SiaraHostPolicy _hostPolicy;
    private readonly ILogger<SiaraLoginService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiaraLoginService"/> class.
    /// </summary>
    /// <param name="hostPolicy">The P8 production-host guardrail consulted before entering credentials.</param>
    /// <param name="logger">The logger instance.</param>
    public SiaraLoginService(SiaraHostPolicy hostPolicy, ILogger<SiaraLoginService> logger)
    {
        ArgumentNullException.ThrowIfNull(hostPolicy);
        ArgumentNullException.ThrowIfNull(logger);

        _hostPolicy = hostPolicy;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> LoginAsync(
        IBrowserAutomationAgent agent,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.WithFailure("Login operation was cancelled");
        }

        try
        {
            _logger.LogInformation("Starting SIARA login");

            // ADR-010 P8: confirm the loaded page is an allowed host BEFORE entering any credentials. The
            // real SIARA production host fails closed unless the deployment explicitly opted in.
            var currentUrl = await agent.GetCurrentUrlAsync(cancellationToken);
            if (!currentUrl.IsSuccess)
            {
                return Result.WithFailure(
                    $"Cannot confirm the SIARA login host before entering credentials: {currentUrl.Error}");
            }

            var hostCheck = _hostPolicy.Validate(currentUrl.Value);
            if (!hostCheck.IsSuccess)
            {
                return Result.WithFailure(hostCheck.Error ?? "SIARA login host is not allowed.");
            }

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

            _logger.LogInformation("Successfully logged in to SIARA");
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SIARA login operation was cancelled");
            return Result.WithFailure("Login operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to login to SIARA");
            return Result.WithFailure($"Failed to login to SIARA: {ex.Message}", ex);
        }
    }
}
