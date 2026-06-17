using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Application.Services;

/// <summary>
/// Default implementation of <see cref="IBundleBinder"/>.
/// Orchestrates bundle retrieval, product resolution, availability assessment,
/// and assembly of a <see cref="VerificationContext"/>.
/// </summary>
internal sealed class BundleBinder : IBundleBinder
{
    private readonly IVecReferenceDataProvider _provider;
    private readonly IProductResolver _productResolver;
    private readonly ILogger<BundleBinder> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="BundleBinder"/>.
    /// </summary>
    /// <param name="provider">Port that loads the VEC reference-data bundle.</param>
    /// <param name="productResolver">Resolver that maps a token to a canonical product.</param>
    /// <param name="logger">Structured logger.</param>
    public BundleBinder(
        IVecReferenceDataProvider provider,
        IProductResolver productResolver,
        ILogger<BundleBinder> logger)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _productResolver = productResolver ?? throw new ArgumentNullException(nameof(productResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<VerificationContext>> BindAsync(
        VerificationJob job,
        StatementContextKey key,
        string productToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(key);

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<VerificationContext>();

        // ----------------------------------------------------------------
        // Step 1 — Resolve the reference bundle
        // ----------------------------------------------------------------
        var bundleResult = await _provider.GetBundleAsync(key, cancellationToken)
            .ConfigureAwait(false);

        if (bundleResult.IsCancelled())
            return ResultExtensions.Cancelled<VerificationContext>();

        if (bundleResult.IsFailure)
        {
            _logger.LogWarning(
                "Bundle retrieval failed for job {JobId} / key {Institution}: {Error}",
                job.Id,
                key.Institution,
                bundleResult.Error);

            var blocked = new BlockedOutcome(
                BlockReason.InvalidBundle,
                bundleResult.Error ?? "Reference bundle could not be loaded.");
            return Result<VerificationContext>.WithFailure(blocked.ToErrorString());
        }

        var bundle = bundleResult.Value!;

        // ----------------------------------------------------------------
        // Step 2 — Resolve the product
        // ----------------------------------------------------------------
        var productResult = _productResolver.Resolve(productToken, bundle);

        if (productResult.IsFailure)
        {
            _logger.LogWarning(
                "Product resolution failed for job {JobId}, token '{Token}': {Error}",
                job.Id,
                productToken,
                productResult.Error);

            // The resolver already encodes a BlockedOutcome error string — propagate as-is.
            return Result<VerificationContext>.WithFailure(productResult.Error!);
        }

        var resolvedProduct = productResult.Value!;

        // ----------------------------------------------------------------
        // Step 3 — Compute reference-data availability
        // ----------------------------------------------------------------
        var availability = ReferenceDataAvailability.FromBundle(bundle);

        // ----------------------------------------------------------------
        // Step 4 — Project convenience fields from the bundle
        // ----------------------------------------------------------------

        // Find the prior statement for this account (by AccountRef if the key provides one;
        // otherwise return the first available, or null).
        var priorStatement = bundle.PriorStatements is { Count: > 0 }
            ? (key.AccountRef is not null
                ? bundle.PriorStatements.FirstOrDefault(
                    ps => string.Equals(ps.AccountRef, key.AccountRef, StringComparison.OrdinalIgnoreCase))
                : bundle.PriorStatements[0])
            : null;

        var toleranceConfig = bundle.ToleranceConfig;

        // ----------------------------------------------------------------
        // Step 5 — Assemble the context
        // ----------------------------------------------------------------
        var context = new VerificationContext(
            bundle: bundle,
            resolvedProduct: resolvedProduct,
            availability: availability,
            priorStatement: priorStatement,
            toleranceConfig: toleranceConfig,
            statementModel: null); // Epic 3: will be populated by the extraction pipeline.

        _logger.LogDebug(
            "Bound context for job {JobId}: product={ProductId}, " +
            "rate={RateStatus}, tolerances={ToleranceStatus}, priorStatement={PriorStatementStatus}.",
            job.Id,
            resolvedProduct.ProductId,
            availability.StatusOf(Domain.Enums.ReferenceCapability.Rate),
            availability.StatusOf(Domain.Enums.ReferenceCapability.Tolerances),
            availability.StatusOf(Domain.Enums.ReferenceCapability.PriorStatement));

        return Result<VerificationContext>.WithSuccess(context);
    }
}
