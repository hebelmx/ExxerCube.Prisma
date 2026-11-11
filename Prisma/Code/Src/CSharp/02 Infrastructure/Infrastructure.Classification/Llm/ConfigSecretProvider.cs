using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ExxerCube.Prisma.Infrastructure.Classification.Llm;

/// <summary>
/// Resolves secrets from <see cref="IConfiguration"/> (user-secrets / environment variables).
/// The retrieved value is NEVER logged.
/// </summary>
public sealed class ConfigSecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of <see cref="ConfigSecretProvider"/>.
    /// </summary>
    /// <param name="configuration">The application configuration graph.</param>
    public ConfigSecretProvider(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc />
    public Task<Result<string>> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(Result<string>.WithFailure("Secret key cannot be null or empty."));
        }

        var value = _configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            return Task.FromResult(
                Result<string>.WithFailure($"Secret '{key}' was not found in configuration or is empty."));
        }

        return Task.FromResult(Result<string>.WithSuccess(value));
    }
}
