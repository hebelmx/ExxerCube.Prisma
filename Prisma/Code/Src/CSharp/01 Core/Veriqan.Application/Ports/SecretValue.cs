using System;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Carries a resolved secret value together with its provenance metadata.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Value"/> holds the plaintext secret (AES key material, JWT signing key, SMTP
/// password, etc.). <b>Never log, serialize, or expose this property.</b>
/// </para>
/// <para>
/// <see cref="KeyId"/> and <see cref="Version"/> form the <em>rotation surface</em>: a caller
/// that caches the result can compare <see cref="Version"/> on the next resolution to detect
/// that the secret has been rotated and that its cached copy must be refreshed.
/// </para>
/// <para>
/// For the default <c>ConfigurationSecretProvider</c>:
/// <list type="bullet">
///   <item><see cref="KeyId"/> is <c>"config:&lt;logicalName&gt;"</c> — stable across rotations.</item>
///   <item><see cref="Version"/> is the first 8 hex characters of <c>SHA-256(value)</c> — changes
///         when the secret value changes (i.e. when the operator rotates the key).</item>
/// </list>
/// A future Key Vault / KMS implementation returns the provider's native version identifier in
/// <see cref="Version"/> (e.g. Azure Key Vault's <c>keyVersion</c> GUID).
/// </para>
/// </remarks>
/// <param name="Value">Plaintext secret value. <b>Never log or expose.</b></param>
/// <param name="KeyId">Stable logical identifier for the key (does not change on rotation).</param>
/// <param name="Version">Version / rotation marker (changes when the secret is rotated).</param>
/// <param name="RetrievedAtUtc">UTC timestamp at which the secret was fetched from its backing store.</param>
public sealed record SecretValue(
    string Value,
    string KeyId,
    string Version,
    DateTimeOffset RetrievedAtUtc);
