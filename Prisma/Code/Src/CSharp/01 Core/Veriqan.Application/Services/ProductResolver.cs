using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using FuzzySharp;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Services;

/// <summary>
/// Default implementation of <see cref="IProductResolver"/>.
/// Matches a free-text product token against <see cref="VecProduct.ProductId"/>
/// and each entry in <see cref="VecProduct.Aliases"/> using case-insensitive,
/// whitespace-normalised comparison, with a fuzzy-match fallback for OCR-recovered tokens.
/// No silent default is applied: an unresolvable token always produces a
/// BLOCKED <see cref="BlockReason.UnknownProduct"/> failure.
/// </summary>
internal sealed class ProductResolver : IProductResolver
{
    /// <summary>
    /// Minimum FuzzySharp <see cref="Fuzz.Ratio(string, string)"/> score (0-100) a catalog <c>ProductName</c>,
    /// <c>ProductId</c>, or alias must clear — after the exact-match pass misses every product —
    /// before a fuzzy match is accepted (E7.S7.2/S7.3 owner ruling 2, 2026-07-08: "fuzzy from day
    /// one"). Exists to absorb small OCR noise (stray whitespace, an accent drop, a misread
    /// character) on a header-image-OCR-recovered product token, NOT to guess between genuinely
    /// different products. Below this threshold the token is treated as unresolved — this
    /// resolver NEVER picks the nearest match by fiat; abstention (BLOCKED UnknownProduct) is
    /// always preferred over a fabricated match. 85 mirrors the conservative 80-point convention
    /// already used by <c>FuzzyLabelStage.DefaultScoreThreshold</c> for label matching, nudged up
    /// because a wrong PRODUCT match (vs. a wrong label window) directly changes which legal
    /// checklist runs.
    /// </summary>
    public const int FuzzyScoreThreshold = 85;

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

        // Pass 1 — exact match (canonical ProductId or any alias).
        foreach (var product in bundle.Products)
        {
            if (Normalise(product.ProductId) == normalised)
                return Result<VecProduct>.WithSuccess(product);

            if (product.Aliases is not null)
            {
                foreach (var alias in product.Aliases)
                {
                    if (Normalise(alias) == normalised)
                        return Result<VecProduct>.WithSuccess(product);
                }
            }
        }

        // Pass 2 — fuzzy fallback (owner ruling 2). Only reached when exact match missed every
        // product/alias. Scores every candidate name and only accepts the best match when it
        // clears FuzzyScoreThreshold — never picks a "least-bad" match below the bar.
        var fuzzyMatch = TryFuzzyResolve(normalised, bundle.Products);
        if (fuzzyMatch is not null)
            return Result<VecProduct>.WithSuccess(fuzzyMatch);

        var blocked = new BlockedOutcome(
            BlockReason.UnknownProduct,
            $"No product matched token '{productToken}' in the reference bundle.");
        return Result<VecProduct>.WithFailure(blocked.ToErrorString());
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static VecProduct? TryFuzzyResolve(string normalisedToken, IReadOnlyList<VecProduct> products)
    {
        VecProduct? bestMatch = null;
        var bestScore = -1;

        foreach (var product in products)
        {
            foreach (var candidateName in CandidateNames(product))
            {
                if (string.IsNullOrWhiteSpace(candidateName))
                    continue;

                var score = Fuzz.Ratio(normalisedToken, Normalise(candidateName));
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = product;
                }
            }
        }

        return bestMatch is not null && bestScore >= FuzzyScoreThreshold ? bestMatch : null;
    }

    private static IEnumerable<string> CandidateNames(VecProduct product)
    {
        yield return product.ProductId;
        yield return product.ProductName;

        if (product.Aliases is null)
            yield break;

        foreach (var alias in product.Aliases)
            yield return alias;
    }

    /// <summary>
    /// Returns an upper-case, whitespace-collapsed string suitable for token comparison.
    /// Leading/trailing whitespace is stripped and any run of internal whitespace is
    /// collapsed to a single space before uppercasing.
    /// </summary>
    private static string Normalise(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
              .ToUpperInvariant();
}
