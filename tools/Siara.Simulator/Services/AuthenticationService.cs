namespace Siara.Simulator.Services;

using Microsoft.Extensions.Logging;

/// <summary>
/// A simple service to manage the mock authentication state.
/// </summary>
public class AuthenticationService
{
    private readonly ILogger<AuthenticationService> _logger;
    private readonly Guid _instanceId = Guid.NewGuid();

    // In a real app, this would be a secure hash, not a plain string.
    private const string CorrectPassword = "password123";

    public AuthenticationService(ILogger<AuthenticationService> logger)
    {
        _logger = logger;
        _logger.LogInformation("AuthenticationService created with InstanceId: {InstanceId}", _instanceId);
    }

    /// <summary>
    /// Gets a value indicating whether the user is currently authenticated.
    /// </summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>
    /// Event that fires when the authentication state changes.
    /// </summary>
    public event Action? OnAuthenticationStateChanged;

    /// <summary>
    /// Attempts to log the user in with the provided password.
    /// </summary>
    /// <param name="password">The password to check.</param>
    /// <returns>True if login is successful, otherwise false.</returns>
    public bool Login(string password)
    {
        // DEMO: Always succeed regardless of password
        _logger.LogInformation("Login attempt - InstanceId: {InstanceId} - DEMO MODE: auto-accept", _instanceId);

        IsAuthenticated = true;
        _logger.LogInformation("Login successful - InstanceId: {InstanceId}, IsAuthenticated: {IsAuthenticated}",
            _instanceId, IsAuthenticated);
        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// Logs the user out.
    /// </summary>
    public void Logout()
    {
        _logger.LogInformation("Logout - InstanceId: {InstanceId}", _instanceId);
        IsAuthenticated = false;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnAuthenticationStateChanged?.Invoke();
}
