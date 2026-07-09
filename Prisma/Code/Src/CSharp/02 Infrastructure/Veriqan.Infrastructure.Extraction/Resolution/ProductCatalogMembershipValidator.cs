using System;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Domain validator for a recovered <see cref="ExxerCube.Prisma.Veriqan.Domain.Extraction.FieldKind.Product"/>
/// value: accepts only a product token that resolves in the tenant's catalog.
/// </summary>
/// <remarks>
/// <para>
/// This is the extraction-side mirror of the catalog gate the downstream BundleBinder applies —
/// both sides resolve through the exact same <see cref="IProductResolver"/> instance/implementation
/// (case-insensitive/whitespace-normalised exact match, then a bounded FuzzySharp fallback that
/// abstains rather than nearest-guesses), so extraction never accepts a Product value binding would
/// later reject, and the two paths can never disagree about which token is a "known" product.
/// </para>
/// <para>
/// Used as a per-call <c>validatorOverride</c> on <see cref="FieldResolutionOrchestrator.ResolveAsync{TValue}"/>
/// (Story 3.3a) — supplied by <see cref="EscalatingStatementFieldExtractor"/> only when a caller
/// passes a non-empty <see cref="VecReferenceBundle.Products"/> catalog; when no bundle is supplied,
/// Product resolution is unaffected (this validator is simply never constructed).
/// </para>
/// </remarks>
public sealed class ProductCatalogMembershipValidator : IFieldValidator
{
    private readonly IProductResolver _resolver;
    private readonly VecReferenceBundle _bundle;

    /// <summary>
    /// Initializes a <see cref="ProductCatalogMembershipValidator"/>.
    /// </summary>
    /// <param name="resolver">The production <see cref="IProductResolver"/> — the same matcher the downstream binder uses.</param>
    /// <param name="bundle">The tenant reference bundle whose <see cref="VecReferenceBundle.Products"/> catalog to match against.</param>
    public ProductCatalogMembershipValidator(IProductResolver resolver, VecReferenceBundle bundle)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <see langword="true"/> only when <paramref name="value"/> is a non-null, non-whitespace
    /// string that <see cref="IProductResolver.Resolve"/> successfully matches against the
    /// catalog — i.e. exactly the condition under which the downstream binder would also accept
    /// this token.
    /// </remarks>
    public bool IsValid(object? value) =>
        value is string productToken
        && !string.IsNullOrWhiteSpace(productToken)
        && _resolver.Resolve(productToken, _bundle).IsSuccess;
}
