namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// A transient, self-clearing carrier for SIARA login credentials used by
/// <see cref="Enum.SiaraAuthMode.AutomatedLogin"/> only (ADR-010 P5).
/// </summary>
/// <remarks>
/// <para>
/// Credentials are held as <see cref="char"/> arrays (never <see cref="string"/>) so they can be
/// zeroed deterministically on <see cref="Dispose"/>, and are revealed only inside the scope of
/// <see cref="UseAsync{TResult}"/> — the caller never holds them. The type overrides
/// <see cref="ToString"/> to a redacted constant and exposes no string credential members, so it cannot
/// be logged or serialized to leak the secret. Prisma <strong>never persists</strong> this value
/// (the no-storage guarantee): obtain it, use it for one login, and dispose it.
/// </para>
/// <para>
/// This is <em>not</em> part of the credential-free <see cref="SiaraSession"/> artifact — it lives only
/// on the <see cref="Enum.SiaraAuthMode.AutomatedLogin"/> login path, between the secret store and the
/// form driver.
/// </para>
/// </remarks>
public sealed class SiaraCredential : IDisposable
{
    private readonly char[] _username;
    private readonly char[] _password;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="SiaraCredential"/> class.</summary>
    /// <param name="username">The username characters. Copied; the caller may clear its own buffer.</param>
    /// <param name="password">The password characters. Copied; the caller may clear its own buffer.</param>
    public SiaraCredential(char[] username, char[] password)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        _username = (char[])username.Clone();
        _password = (char[])password.Clone();
    }

    /// <summary>Gets a value indicating whether either credential part is empty.</summary>
    public bool IsEmpty => _username.Length == 0 || _password.Length == 0;

    /// <summary>
    /// Reveals the credentials to the supplied delegate for the minimum time needed (for example, to
    /// drive the login form), and returns its result. The credentials exist as strings only within the
    /// delegate's scope.
    /// </summary>
    /// <typeparam name="TResult">The delegate's result type.</typeparam>
    /// <param name="use">A delegate invoked with the username and password.</param>
    /// <returns>The delegate's result.</returns>
    public async Task<TResult> UseAsync<TResult>(Func<string, string, Task<TResult>> use)
    {
        ArgumentNullException.ThrowIfNull(use);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await use(new string(_username), new string(_password)).ConfigureAwait(false);
    }

    /// <summary>Returns a redacted placeholder; the credentials are never rendered (ADR-010 P5).</summary>
    /// <returns>A constant redaction marker.</returns>
    public override string ToString() => "SiaraCredential { [REDACTED] }";

    /// <summary>Zeroes the credential buffers. Idempotent.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Array.Clear(_username);
        Array.Clear(_password);
        _disposed = true;
    }
}
