using System;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;

namespace ExxerCube.Prisma.Veriqan.Application.Binding;

/// <summary>
/// All reference-data and job context that checklist checks need to run a single verification.
/// Produced by <see cref="Ports.IBundleBinder.BindAsync"/> after the bundle has been loaded,
/// the product resolved, and the availability assessment computed.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="StatementModel"/> slot carries the extracted statement model produced by
/// <see cref="Ports.IStatementFieldExtractor"/> (Story 3.1+).
/// At bind time it is always <see langword="null"/>; it is populated by a separate extraction
/// stage before checks run.  Checks that need the statement model must guard on
/// <c>StatementModel is null</c>.
/// </para>
/// </remarks>
public sealed class VerificationContext
{
    /// <summary>
    /// Initializes a <see cref="VerificationContext"/> with all required components.
    /// </summary>
    /// <param name="bundle">The full reference-data bundle for this run.</param>
    /// <param name="resolvedProduct">The canonical product resolved from the statement token.</param>
    /// <param name="availability">Per-capability availability derived from the bundle.</param>
    /// <param name="priorStatement">
    /// The prior-month statement for the account, if present in the bundle; otherwise <see langword="null"/>.
    /// </param>
    /// <param name="toleranceConfig">
    /// Tolerance bands from the bundle, if present; otherwise <see langword="null"/>.
    /// </param>
    /// <param name="statementModel">
    /// The extracted statement model populated after the extraction stage (Story 3.1+);
    /// <see langword="null"/> until extraction completes or when not yet run.
    /// </param>
    /// <param name="tenantProfile">
    /// The resolved tenant profile that contains effective per-rule tolerance overrides
    /// (Story 9.3b); <see langword="null"/> when no tenant context is available, in which
    /// case all rules apply the CONDUSEF legal baseline tolerances.
    /// </param>
    public VerificationContext(
        VecReferenceBundle bundle,
        VecProduct resolvedProduct,
        ReferenceDataAvailability availability,
        PriorStatement? priorStatement,
        ToleranceConfig? toleranceConfig,
        StatementModel? statementModel,
        ResolvedTenantProfile? tenantProfile = null)
    {
        Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        ResolvedProduct = resolvedProduct ?? throw new ArgumentNullException(nameof(resolvedProduct));
        Availability = availability ?? throw new ArgumentNullException(nameof(availability));
        PriorStatement = priorStatement;
        ToleranceConfig = toleranceConfig;
        StatementModel = statementModel;
        TenantProfile = tenantProfile;
    }

    // -----------------------------------------------------------------------
    // Reference data
    // -----------------------------------------------------------------------

    /// <summary>
    /// The full reference-data bundle loaded for this verification run.
    /// Contains all sections that were present in the source (some may be null — see <see cref="Availability"/>).
    /// </summary>
    public VecReferenceBundle Bundle { get; }

    /// <summary>
    /// The canonical product resolved from the statement's product token.
    /// Guaranteed non-null; the binder blocks with <see cref="Domain.Enums.BlockReason.UnknownProduct"/>
    /// if no match is found.
    /// </summary>
    public VecProduct ResolvedProduct { get; }

    /// <summary>
    /// Per-capability availability assessment derived from <see cref="Bundle"/>.
    /// Use <see cref="ReferenceDataAvailability.IsInsufficientData"/> to gate checks
    /// that depend on optional sections (e.g. TASA, PriorStatement).
    /// </summary>
    public ReferenceDataAvailability Availability { get; }

    // -----------------------------------------------------------------------
    // Convenient projections from the bundle (may be null)
    // -----------------------------------------------------------------------

    /// <summary>
    /// The prior-month statement for the account being verified, looked up from
    /// <see cref="VecReferenceBundle.PriorStatements"/> by <c>AccountRef</c>
    /// (if available); otherwise <see langword="null"/>.
    /// </summary>
    public PriorStatement? PriorStatement { get; }

    /// <summary>
    /// Tolerance configuration from the bundle; <see langword="null"/> when the
    /// bundle's <c>toleranceConfig</c> section was absent.
    /// Checks should consult <see cref="Availability"/> before using this value.
    /// </summary>
    public ToleranceConfig? ToleranceConfig { get; }

    // -----------------------------------------------------------------------
    // Statement model (Epic 3)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Structured model extracted from the statement PDF by the extraction pipeline (Story 3.1+).
    /// <see langword="null"/> until the extraction stage runs (or when not applicable).
    /// Checks that depend on extracted fields must guard on <c>StatementModel is null</c>
    /// and emit an appropriate result when absent.
    /// </summary>
    public StatementModel? StatementModel { get; }

    // -----------------------------------------------------------------------
    // Tenant profile (Story 9.3b)
    // -----------------------------------------------------------------------

    /// <summary>
    /// The resolved tenant profile containing accepted per-rule tolerance overrides
    /// (Story 9.3b). <see langword="null"/> when no tenant context is available;
    /// rules must fall back to the CONDUSEF legal baseline in that case.
    /// </summary>
    /// <remarks>
    /// Use <see cref="ResolvedTenantProfile.GetEffectiveTolerance"/> to obtain the
    /// effective tolerance for a given <c>CheckId</c>, passing the legal default as the fallback.
    /// Rules that do not use tolerance bands need not consult this property.
    /// </remarks>
    public ResolvedTenantProfile? TenantProfile { get; }
}
