using System;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Services;

/// <summary>
/// Default implementation of <see cref="IProductResolver"/>.
/// Matches a free-text product token against <see cref="VecProduct.ProductId"/>
/// and each entry in <see cref="VecProduct.Aliases"/> using case-insensitive,
/// whitespace-normalised comparison.
/// No silent default is applied: an unresolvable token always produces a
/// BLOCKED <see cref="BlockReason.UnknownProduct"/> failure.
/// </summary>
internal sealed class ProductResolver : IProductResolver
{
    /// <inheritdoc />
    public Result<VecProduct> Resolve(string productToken, VecReferenceBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        if (string.IsNullOrWhiteSpace(productToken))
        {
            var empty = new BlockedOutcome(
                BlockReason.UnknownProduct,
                "Product token must not be null or empty.");
            return Result<VecProduct>.WithFailure(empty.ToErrorString());
        }

        var normalised = Normalise(productToken);

        if (bundle.Products is null || bundle.Products.Count == 0)
        {
            var noProducts = new BlockedOutcome(
                BlockReason.UnknownProduct,
                $"Bundle contains no products; cannot resolve token '{productToken}'.");
            return Result<VecProduct>.WithFailure(noProducts.ToErrorString());
        }

        foreach (var product in bundle.Products)
        {
            // Match by canonical ProductId
            if (Normalise(product.ProductId) == normalised)
                return Result<VecProduct>.WithSuccess(product);

            // Match by any alias
            if (product.Aliases is not null)
            {
                foreach (var alias in product.Aliases)
                {
                    if (Normalise(alias) == normalised)
                        return Result<VecProduct>.WithSuccess(product);
                }
            }
        }

        var blocked = new BlockedOutcome(
            BlockReason.UnknownProduct,
            $"No product matched token '{productToken}' in the reference bundle.");
        return Result<VecProduct>.WithFailure(blocked.ToErrorString());
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns a lowercase, whitespace-collapsed string suitable for token comparison.
    /// </summary>
    private static string Normalise(string value) =>
        value.Trim().ToUpperInvariant();
}
