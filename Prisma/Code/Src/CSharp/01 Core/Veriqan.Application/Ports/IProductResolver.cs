using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Resolves a free-text product token (extracted from a statement) to the canonical
/// <see cref="VecProduct"/> in a reference bundle.
/// </summary>
/// <remarks>
/// Matching is case-insensitive and whitespace-normalised.  A token may match either
/// the <see cref="VecProduct.ProductId"/> or any entry in <see cref="VecProduct.Aliases"/>.
/// If no product matches, the result is a failure whose error string encodes
/// <see cref="Domain.Enums.BlockReason.UnknownProduct"/> via <see cref="Binding.BlockedOutcome"/>.
/// </remarks>
public interface IProductResolver
{
    /// <summary>
    /// Attempts to resolve <paramref name="productToken"/> against the products in <paramref name="bundle"/>.
    /// </summary>
    /// <param name="productToken">
    /// Free-text product name or alias extracted from the statement (e.g. <c>"NL"</c>).
    /// </param>
    /// <param name="bundle">The reference bundle whose <c>Products</c> catalogue to search.</param>
    /// <returns>
    /// <see cref="Result{T}.IsSuccess"/> carrying the matched <see cref="VecProduct"/> on success,
    /// or a failure with a <see cref="Binding.BlockedOutcome"/> error string when the token
    /// cannot be resolved (reason: <see cref="Domain.Enums.BlockReason.UnknownProduct"/>).
    /// </returns>
    Result<VecProduct> Resolve(string productToken, VecReferenceBundle bundle);
}
