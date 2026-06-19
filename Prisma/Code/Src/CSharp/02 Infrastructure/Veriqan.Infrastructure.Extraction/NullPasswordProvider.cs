using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;

/// <summary>
/// No-op <see cref="IPasswordProvider"/> used when no per-institution PDF passwords are
/// configured.  Always returns a failure result so the extractor can produce a
/// <c>PasswordProtected</c> failure instead of rethrowing the open exception.
/// </summary>
internal sealed class NullPasswordProvider : IPasswordProvider
{
    /// <inheritdoc />
    public Task<Result<string>> GetPasswordAsync(
        string institutionKey,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Result<string>.WithFailure($"No password configured for institution '{institutionKey}'."));
    }
}
