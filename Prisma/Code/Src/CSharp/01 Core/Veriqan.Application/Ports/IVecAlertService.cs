using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Application port for dispatching VEC alert notifications.
/// </summary>
/// <remarks>
/// <para>
/// <b>Policy (FR-17, CL-54):</b>
/// <list type="bullet">
///   <item><description>
///     <see cref="Domain.Enums.VerdictSignal.Red"/> — exactly one alert email is composed and
///     dispatched (with retry on transient failure); if dispatch ultimately fails the result is a
///     typed failure and an error is logged — the failure is <b>never silently dropped</b>.
///   </description></item>
///   <item><description>
///     <see cref="Domain.Enums.VerdictSignal.Green"/> or
///     <see cref="Domain.Enums.VerdictSignal.Blocked"/> — no email is sent.
///     A success result is returned with a diagnostic note in the error field.
///   </description></item>
/// </list>
/// </para>
/// </remarks>
public interface IVecAlertService
{
    /// <summary>
    /// Evaluates <paramref name="verdict"/> and, when the signal is
    /// <see cref="Domain.Enums.VerdictSignal.Red"/>, dispatches a single alert email to the
    /// recipients carried in <paramref name="context"/>.
    /// </summary>
    /// <param name="verdict">The verdict summary produced for the statement.</param>
    /// <param name="context">Statement identifier and alert recipients.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><description>Success — either an alert was sent (RED) or no alert was needed (GREEN/BLOCKED).</description></item>
    ///   <item><description>Failure — the alert could not be delivered after all retry attempts.</description></item>
    ///   <item><description>Cancelled — when <paramref name="cancellationToken"/> is signalled.</description></item>
    /// </list>
    /// </returns>
    Task<Result> SendRedAlertAsync(
        VerdictSummary verdict,
        AlertContext context,
        CancellationToken cancellationToken = default);
}
