namespace Siara.Simulator.Services;

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Siara.Simulator.Configuration;

/// <summary>
/// Validates simulator login credentials and enforces max-attempt lockout, modelling the real SIARA
/// cookie-session login (username/password, lockout, Spanish error messages). Singleton.
/// </summary>
public sealed class SiaraCredentialValidator
{
    private readonly SiaraAuthOptions _options;
    private readonly ILogger<SiaraCredentialValidator> _logger;
    private readonly ConcurrentDictionary<string, AttemptState> _attempts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes the validator.</summary>
    /// <param name="options">The simulator auth options.</param>
    /// <param name="logger">The logger.</param>
    public SiaraCredentialValidator(IOptions<SiaraAuthOptions> options, ILogger<SiaraCredentialValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Validates the supplied credentials, applying lockout. Returns a Spanish error message on failure.
    /// </summary>
    /// <param name="username">The username entered.</param>
    /// <param name="password">The password entered.</param>
    /// <returns>The validation result.</returns>
    public CredentialValidationResult Validate(string? username, string? password)
    {
        username = username?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return CredentialValidationResult.Fail("Debe ingresar usuario y contraseña.");
        }

        var state = _attempts.GetOrAdd(username, _ => new AttemptState());
        lock (state)
        {
            if (state.LockedUntil is { } until && until > DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Login blocked for locked account {User}", username);
                return CredentialValidationResult.Fail(
                    "Cuenta bloqueada por exceder el número máximo de intentos permitidos. Intente nuevamente más tarde.");
            }

            if (!string.Equals(username, _options.ValidUsername, StringComparison.OrdinalIgnoreCase))
            {
                RegisterFailure(state, username);
                return Failure(state, "El usuario especificado no existe.");
            }

            if (!string.Equals(password, _options.ValidPassword, StringComparison.Ordinal))
            {
                RegisterFailure(state, username);
                return Failure(state, "La contraseña es incorrecta.");
            }

            // Success — clear any accumulated failures.
            state.Failures = 0;
            state.LockedUntil = null;
            _logger.LogInformation("Login succeeded for {User}", username);
            return CredentialValidationResult.Ok();
        }
    }

    private CredentialValidationResult Failure(AttemptState state, string message)
    {
        // If that failure just triggered the lockout, report the lockout instead.
        if (state.LockedUntil is { } until && until > DateTimeOffset.UtcNow)
        {
            return CredentialValidationResult.Fail(
                "Cuenta bloqueada por exceder el número máximo de intentos permitidos. Intente nuevamente más tarde.");
        }

        return CredentialValidationResult.Fail(message);
    }

    private void RegisterFailure(AttemptState state, string username)
    {
        state.Failures++;
        if (state.Failures >= _options.MaxFailedAttempts)
        {
            state.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(_options.LockoutMinutes);
            _logger.LogWarning(
                "Account {User} locked after {Failures} failed attempts", username, state.Failures);
        }
    }

    private sealed class AttemptState
    {
        public int Failures { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
    }
}

/// <summary>The outcome of a credential validation.</summary>
/// <param name="Success">Whether the credentials are valid and the account is not locked.</param>
/// <param name="ErrorMessage">A Spanish error message when <paramref name="Success"/> is false; otherwise null.</param>
public readonly record struct CredentialValidationResult(bool Success, string? ErrorMessage)
{
    /// <summary>Creates a successful result.</summary>
    /// <returns>A success result.</returns>
    public static CredentialValidationResult Ok() => new(true, null);

    /// <summary>Creates a failed result with a Spanish message.</summary>
    /// <param name="message">The error message.</param>
    /// <returns>A failure result.</returns>
    public static CredentialValidationResult Fail(string message) => new(false, message);
}
