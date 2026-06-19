using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Port for resolving a per-institution PDF password, used to open password-protected
/// VEC statement files.
/// </summary>
/// <remarks>
/// <para>
/// Implementations look up a stored password keyed by institution name.
/// When no password is configured for the given key the method returns a failure result;
/// the extractor converts that into a <c>PasswordProtected</c> failure result rather than
/// propagating the exception thrown by <c>PdfDocument.Open</c>.
/// </para>
/// <para>
/// The port lives in the Application layer so the extraction infrastructure can depend on it
/// without taking a direct dependency on configuration or secret stores.
/// </para>
/// </remarks>
public interface IPasswordProvider
{
    /// <summary>
    /// Attempts to retrieve the PDF open-password configured for the given institution key.
    /// </summary>
    /// <param name="institutionKey">
    /// An institution identifier (e.g. <c>"BSSB"</c>, <c>"HSBC"</c>) used to look up the password.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}.IsSuccess"/> with the plaintext password string when one is configured;
    /// a failure result when no password is available for <paramref name="institutionKey"/>.
    /// </returns>
    Task<Result<string>> GetPasswordAsync(
        string institutionKey,
        CancellationToken cancellationToken = default);
}
