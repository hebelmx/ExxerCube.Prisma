using System;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Verification;

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

    // -----------------------------------------------------------------------
    // Per-rule confidence accumulator (Story 4.1 — Epic 4 confidence degree)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Minimum confidence seen across all <see cref="ExtractedField{T}"/> values
    /// recorded via <see cref="ConfidenceBelowThreshold{T}"/> since the last
    /// <see cref="ResetConsumedConfidence"/> call; <see langword="null"/> when no
    /// field has been recorded in the current rule evaluation.
    /// </summary>
    private double? _minConsumedConfidence;

    /// <summary>
    /// Resets the per-rule confidence accumulator.
    /// Called by <c>VecValidationEngine</c> immediately before each rule is evaluated
    /// so that the accumulated min is scoped to exactly one rule's execution.
    /// </summary>
    public void ResetConsumedConfidence() => _minConsumedConfidence = null;

    /// <summary>
    /// Records the extraction confidence of <paramref name="field"/> into the per-rule
    /// minimum accumulator and returns whether the field is below the confidence threshold,
    /// delegating the comparison to <see cref="ConfidenceGuard.BelowThreshold{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the extracted field value.</typeparam>
    /// <param name="field">The extracted field whose confidence should be recorded.</param>
    /// <param name="threshold">
    /// The minimum confidence required for the field to be used in verification logic.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the field's confidence is strictly below
    /// <paramref name="threshold"/> (the rule should abstain for this field);
    /// <see langword="false"/> when the field passes the guard.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Recording semantics:</b> the field's confidence is always recorded into the
    /// accumulator regardless of whether it is below the threshold. This ensures that
    /// <see cref="ConsumedConfidenceOrFull"/> accurately reflects the lowest confidence
    /// seen across all fields the rule examined, even for fields that passed the guard.
    /// </para>
    /// <para>
    /// <see cref="ConfidenceGuard.BelowThreshold{T}"/> remains the single authority for
    /// the threshold comparison — this method delegates to it and does not duplicate
    /// the <c>field.Confidence &lt; threshold</c> logic.
    /// </para>
    /// </remarks>
    public bool ConfidenceBelowThreshold<T>(ExtractedField<T> field, double threshold)
    {
        ArgumentNullException.ThrowIfNull(field);

        // Always record the field's confidence into the accumulator (regardless of pass/fail).
        _minConsumedConfidence = _minConsumedConfidence.HasValue
            ? Math.Min(_minConsumedConfidence.Value, field.Confidence)
            : field.Confidence;

        // Delegate the comparison to ConfidenceGuard — single authority.
        return ConfidenceGuard.BelowThreshold(field, threshold);
    }

    /// <summary>
    /// Returns the minimum extraction confidence recorded across all fields consumed by
    /// the current rule evaluation (since the last <see cref="ResetConsumedConfidence"/>
    /// call), or <c>1.0</c> when no field has been recorded.
    /// </summary>
    /// <returns>
    /// The accumulated minimum confidence in <c>[0.0, 1.0]</c>, or <c>1.0</c> when
    /// the rule consumed no guarded fields (presence-only checks, non-field checks, etc.).
    /// </returns>
    public double ConsumedConfidenceOrFull() => _minConsumedConfidence ?? 1.0;
}
