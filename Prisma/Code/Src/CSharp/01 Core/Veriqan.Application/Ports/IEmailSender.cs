using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Infrastructure port for dispatching outbound email messages.
/// Implementations live in the Infrastructure layer and are resolved via dependency injection.
/// </summary>
/// <remarks>
/// <para>
/// Implementations must never throw; all error conditions are surfaced as
/// <see cref="Result"/> failure returns.
/// </para>
/// <para>
/// Callers that need retry-on-transient-failure (e.g. <c>VecAlertService</c>) are responsible
/// for driving retry logic — the sender itself is a simple, single-attempt transport.
/// </para>
/// </remarks>
public interface IEmailSender
{
    /// <summary>
    /// Sends <paramref name="message"/> via the underlying mail transport.
    /// </summary>
    /// <param name="message">The fully-populated email to dispatch.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><description>Success — the message was accepted by the mail transport.</description></item>
    ///   <item><description>Failure — a typed error when the transport rejected or could not deliver the message.</description></item>
    ///   <item><description>Cancelled — when <paramref name="cancellationToken"/> is signalled before or during the send.</description></item>
    /// </list>
    /// </returns>
    Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
