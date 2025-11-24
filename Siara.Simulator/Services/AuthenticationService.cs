namespace Siara.Simulator.Services;

/// <summary>
/// A simple service to manage the mock authentication state.
/// </summary>
public class AuthenticationService
{
    // In a real app, this would be a secure hash, not a plain string.
    private const string CorrectPassword = "password123";

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
        if (password == CorrectPassword)
        {
            IsAuthenticated = true;
            NotifyStateChanged();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Logs the user out.
    /// </summary>
    public void Logout()
    {
        IsAuthenticated = false;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnAuthenticationStateChanged?.Invoke();
}
