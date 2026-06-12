namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Yields the SIARA credentials at the moment of an <see cref="Enum.SiaraAuthMode.AutomatedLogin"/>
/// login, sourced from a client-owned secret store (Key Vault / env / secrets manager). See ADR-010.
/// </summary>
/// <remarks>
/// <para>
/// This port exists so that <see cref="Enum.SiaraAuthMode.AutomatedLogin"/> can run unattended without
/// Prisma ever <strong>persisting</strong> a username/password: the provider asks for the credentials
/// transiently, drives one login, and disposes them. The returned <see cref="ValueObjects.SiaraCredential"/>
/// is self-clearing and exposes no string credential members (ADR-010 P5).
/// </para>
/// <para>
/// <strong>Contract:</strong> returns <see cref="Result{T}"/> and never throws for business outcomes; a
/// pre-cancelled token yields a cancelled result; a missing/misconfigured secret fails closed. Each read
/// should be audited by the implementation (ADR-010 Consequences).
/// </para>
/// </remarks>
public interface ISiaraCredentialSource
{
    /// <summary>
    /// Obtains the SIARA credentials from the secret store for a single, transient use.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A success result wrapping a disposable <see cref="ValueObjects.SiaraCredential"/>, or a failure
    /// when no credentials are configured/available.
    /// </returns>
    Task<Result<ValueObjects.SiaraCredential>> GetCredentialsAsync(CancellationToken cancellationToken = default);
}
