using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Hand-written, stateful reference fake of <see cref="ISiaraCredentialSource"/> (ADR-005 §6, ADR-010 P5):
/// honest logic, no mocking framework, modelling a client secret store that yields transient credentials.
/// </summary>
/// <remarks>
/// <para>
/// It holds configured username/password characters and issues a fresh, self-clearing
/// <see cref="SiaraCredential"/> on each read, tracking a read count so the AutomatedLogin
/// provider/watch-loop tests can assert the credential path is exercised (ties to the ADR-010 audit
/// requirement) <strong>without a real vault</strong>. Construct one per test for isolation.
/// </para>
/// <para>
/// It never persists the credential it hands out and exposes the secret only through the credential's
/// scoped reveal — it is a test double, but it honors the same no-leak shape as production.
/// </para>
/// </remarks>
public sealed class FakeSiaraCredentialSource : ISiaraCredentialSource
{
    private readonly char[] _username;
    private readonly char[] _password;
    private readonly object _gate = new();
    private int _reads;

    /// <summary>Initializes the fake with the credentials it should hand out.</summary>
    /// <param name="username">The username to issue. Defaults to a non-empty placeholder.</param>
    /// <param name="password">The password to issue. Defaults to a non-empty placeholder.</param>
    public FakeSiaraCredentialSource(string username = "fake-siara-user", string password = "fake-siara-password")
    {
        _username = username.ToCharArray();
        _password = password.ToCharArray();
    }

    /// <summary>Gets the number of times credentials have been read from this fake.</summary>
    public int Reads
    {
        get { lock (_gate) { return _reads; } }
    }

    /// <inheritdoc />
    public Task<Result<SiaraCredential>> GetCredentialsAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<SiaraCredential>());
        }

        lock (_gate)
        {
            _reads++;
        }

        // Hand out a fresh copy each time; the caller owns and disposes it.
        var credential = new SiaraCredential(_username, _password);
        return Task.FromResult(Result<SiaraCredential>.Success(credential));
    }
}
