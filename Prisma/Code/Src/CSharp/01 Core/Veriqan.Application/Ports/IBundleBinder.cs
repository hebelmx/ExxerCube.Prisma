using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Binds a <see cref="VerificationJob"/> to its reference-data bundle and resolved product,
/// producing a fully-populated <see cref="VerificationContext"/> ready for checklist execution.
/// </summary>
/// <remarks>
/// <para>
/// The bind operation:
/// <list type="number">
///   <item>Resolves the bundle via <see cref="IVecReferenceDataProvider"/> (failure → BLOCKED <see cref="Domain.Enums.BlockReason.InvalidBundle"/>).</item>
///   <item>Resolves the product via <see cref="IProductResolver"/> (failure → BLOCKED <see cref="Domain.Enums.BlockReason.UnknownProduct"/>).</item>
///   <item>Computes <see cref="Domain.Binding.ReferenceDataAvailability"/> from the bundle sections present.</item>
///   <item>Assembles and returns a <see cref="VerificationContext"/>.</item>
/// </list>
/// </para>
/// <para>
/// The <see cref="VerificationContext.StatementModel"/> slot is always <see langword="null"/>
/// until the Epic 3 extraction pipeline is wired in.
/// </para>
/// </remarks>
public interface IBundleBinder
{
    /// <summary>
    /// Loads the reference bundle, resolves the product, and builds a <see cref="VerificationContext"/>.
    /// </summary>
    /// <param name="job">The verification job being processed.</param>
    /// <param name="key">Context key used to look up the correct reference bundle.</param>
    /// <param name="productToken">
    /// Free-text product token extracted from the statement (e.g. <c>"NL"</c>).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the <see cref="VerificationContext"/> on success;
    /// a failure result encoding a <see cref="Binding.BlockedOutcome"/> on any blocking condition;
    /// or a cancelled result when <paramref name="cancellationToken"/> is signalled.
    /// </returns>
    Task<Result<VerificationContext>> BindAsync(
        VerificationJob job,
        StatementContextKey key,
        string productToken,
        CancellationToken cancellationToken = default);
}
