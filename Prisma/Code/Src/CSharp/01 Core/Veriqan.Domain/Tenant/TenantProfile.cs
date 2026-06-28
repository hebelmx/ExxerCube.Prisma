using System;
using System.Collections.Generic;

namespace ExxerCube.Prisma.Veriqan.Domain.Tenant;

/// <summary>
/// Immutable value object representing a tenant's configuration profile, including any
/// tolerance overrides the tenant wishes to apply on top of the CONDUSEF legal baseline.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="ToleranceOverrides"/> is empty the profile is functionally equivalent to
/// <see cref="LegalBaseline"/>: all rules apply the legally-mandated defaults.
/// </para>
/// <para>
/// Overrides are validated and resolved by <c>TenantProfileResolver</c> (Story 9.3b).
/// Invalid or out-of-range overrides are rejected and surfaced as <see cref="TenantDeviation"/>
/// entries without aborting the resolution. The effective values live in the resulting
/// <see cref="ResolvedTenantProfile"/>.
/// </para>
/// </remarks>
public sealed record TenantProfile
{
    /// <summary>
    /// Gets the unique identifier for this tenant, e.g. <c>"BANCO-NORTE-001"</c>.
    /// Never null or white-space.
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// Gets the human-readable display name for this tenant.
    /// Never null or white-space.
    /// </summary>
    public string TenantName { get; }

    /// <summary>
    /// Gets the per-rule tolerance override map where the key is the <c>CheckId</c>
    /// (e.g. <c>"CL-10"</c>) and the value is the requested tolerance value.
    /// An empty map means no overrides — the legal baseline applies for all rules.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> ToleranceOverrides { get; }

    /// <summary>
    /// Gets the minimum extraction-confidence score a field must have before a rule
    /// uses it for arithmetic comparison. Fields below this floor cause the rule to
    /// abstain (<see cref="Domain.Enums.FindingVerdict.InsufficientData"/>) instead of
    /// producing a potentially incorrect Pass or Fail verdict.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Legal default: 0.8.</b> Clean <c>Found</c> fields have confidence 1.0 and always
    /// pass the guard. <c>InvalidFormat</c> fields have confidence 0.7 and abstain under the
    /// default floor, which is the correct preventive behaviour (a misread digit must yield
    /// "cannot verify", not "bank non-compliant"). A tenant may raise this value above 0.8
    /// to require even higher extraction quality before verdict, but may never lower it below
    /// zero or above one.
    /// </para>
    /// <para>
    /// Design choice (Story 9.5): kept as a single profile-level scalar rather than a per-rule
    /// map because confidence thresholds are an extraction-quality concern that applies uniformly
    /// across all field-reading rules. Per-rule overrides are reserved for tolerance bands
    /// (see <see cref="ToleranceOverrides"/>).
    /// </para>
    /// </remarks>
    public double MinFieldConfidence { get; }

    /// <summary>
    /// The legal minimum value for <see cref="MinFieldConfidence"/>.
    /// A tenant-supplied value below this is rejected.
    /// </summary>
    public const double MinFieldConfidenceLowerBound = 0.0;

    /// <summary>
    /// The legal maximum value for <see cref="MinFieldConfidence"/>.
    /// A tenant-supplied value above this is rejected.
    /// </summary>
    public const double MinFieldConfidenceUpperBound = 1.0;

    /// <summary>
    /// The CONDUSEF legal-floor default for <see cref="MinFieldConfidence"/> (Story 9.5).
    /// Clean <c>Found</c> fields (confidence 1.0) always pass; <c>InvalidFormat</c> fields
    /// (confidence 0.7) abstain, which is the intended preventive behaviour.
    /// </summary>
    public const double LegalMinFieldConfidenceDefault = 0.8;

    /// <summary>
    /// The CONDUSEF legal floor for <see cref="MinFieldConfidence"/> (Story 9.5 remediation).
    /// A tenant may RAISE this threshold (stricter extraction quality required before verdict),
    /// but may NEVER set it below this value — doing so would bypass the abstain guard on
    /// <c>InvalidFormat</c> fields (confidence 0.7) and allow a mis-read digit to produce a
    /// potentially incorrect Pass or Fail verdict.
    /// </summary>
    /// <remarks>
    /// When a tenant supplies a value below this floor, <c>TenantProfileResolver</c> rejects
    /// the override, records a <see cref="TenantDeviation"/> (CheckId = "MIN-FIELD-CONFIDENCE"),
    /// and resolves <see cref="MinFieldConfidence"/> to this floor (0.8).
    /// </remarks>
    public const double MinFieldConfidenceLegalFloor = 0.8;

    // -----------------------------------------------------------------------
    // Extraction-coverage floor (Story E1-S10 — U2 guard)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets the minimum number of fields that must be successfully extracted
    /// (<see cref="Domain.Extraction.ExtractionStatus.Extracted"/> or
    /// <see cref="Domain.Extraction.ExtractionStatus.ExtractedInvalidFormat"/>)
    /// from the statement PDF before the pipeline may proceed to bind and validate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Default: 10.</b>  A normal VEC statement contains well over 10 readable fields;
    /// the default catches PDFs that are encrypted, blank, or so heavily layout-drifted
    /// that the extractor yields near-zero output.
    /// </para>
    /// <para>
    /// When the extracted count falls below this floor the pipeline emits a
    /// <c>VerdictSignal.Blocked</c> outcome with reason
    /// <c>BlockReason.InsufficientExtractionCoverage</c> BEFORE binding or running any
    /// section rules.  This prevents the universal-abstain (U2) false-GREEN: a near-zero
    /// extraction causes every rule to abstain (<c>InsufficientData</c>), and the verdict
    /// aggregator then counts only abstains and emits GREEN — which is a spurious PASS on a
    /// genuinely defective statement.
    /// </para>
    /// <para>
    /// The field count includes header fields (<see cref="Domain.Extraction.ExtractionStatus.Extracted"/>
    /// and <see cref="Domain.Extraction.ExtractionStatus.ExtractedInvalidFormat"/> — both count)
    /// plus any successfully extracted period-summary / paragraph fields exposed via
    /// <see cref="Domain.Extraction.StatementModel.PeriodSummary"/> and similar collections.
    /// </para>
    /// </remarks>
    public int MinExtractionCoverageCount { get; }

    /// <summary>
    /// The default value for <see cref="MinExtractionCoverageCount"/>.
    /// A healthy VEC PDF yields tens of extracted fields; 10 is a conservative floor that
    /// flags encrypted/blank documents without risking false-blocks on intentionally minimal PDFs.
    /// </summary>
    public const int DefaultMinExtractionCoverageCount = 10;

    // -----------------------------------------------------------------------
    // Text-layer density floor (Story E2-S1 — abstain-safety for scanned PDFs)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets the minimum total word count across all PDF pages that must be present before
    /// the pipeline may proceed to bind and validate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Default: 20.</b>  A scanned / image-only PDF has a near-zero text layer (0–3 words
    /// from the PDF metadata shell); the default catches these before any section rule runs.
    /// </para>
    /// <para>
    /// Without this guard a scanned-but-compliant statement causes all 28 mandatory-section
    /// rules to fail (because the detected-section list is empty and the rule falls through to
    /// Fail rather than InsufficientData) → false RED verdict on a genuinely OK document.
    /// </para>
    /// <para>
    /// When the word count falls below this floor the pipeline emits a
    /// <c>VerdictSignal.Blocked</c> outcome with reason
    /// <c>BlockReason.InsufficientTextLayer</c> BEFORE binding or running any rules,
    /// honouring the abstain-safety invariant (scanned PDFs must never receive RED).
    /// </para>
    /// <para>
    /// The word count is derived from <see cref="Domain.Extraction.StatementModel.NormalizedFullText"/>
    /// by splitting on spaces (the normalized form collapses all whitespace to a single space),
    /// so an empty string and a string of only spaces both yield a count of zero.
    /// </para>
    /// </remarks>
    public int MinTextLayerWordCount { get; }

    /// <summary>
    /// The default value for <see cref="MinTextLayerWordCount"/>.
    /// A minimal but text-layer-bearing VEC PDF contains many more than 20 words; this floor
    /// is deliberately conservative to avoid false-blocks on intentionally sparse documents
    /// while still catching scanned / image-only PDFs (which have 0–3 text-layer words).
    /// </summary>
    public const int DefaultMinTextLayerWordCount = 20;

    // -----------------------------------------------------------------------
    // Ambiguous-document-scope guard (Story 4.2-B — U4 guard)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets the maximum PDF page count a statement PDF may have before the pipeline treats
    /// the document as a multi-statement bundle and routes to
    /// <c>VerdictSignal.ExtractionGap</c> / <c>BlockReason.AmbiguousDocumentScope</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Default: 20.</b>  A typical CONDUSEF single credit-card statement is 8–15 pages.
    /// An archive PDF bundling multiple monthly statements tends to exceed 20 pages.
    /// When <see cref="Domain.Extraction.StatementModel.PageCount"/> exceeds this limit the
    /// pipeline aborts BEFORE binding or running any rules, preventing a confident-but-wrong
    /// verdict caused by fields being read from the wrong statement scope.
    /// </para>
    /// <para>
    /// <b>STUB limitation (Story 4.2-B):</b> page count is a cheap proxy only.  A legitimate
    /// single-statement PDF with more than the configured threshold of pages would
    /// false-positive; equally, a bundle of two very short statements might not be caught.
    /// Full detection (page-level structural analysis, distinct account-number anchors,
    /// period boundary comparison) is deferred to a future story.
    /// </para>
    /// <para>
    /// Set to <see cref="int.MaxValue"/> to disable the guard entirely in test scenarios
    /// that need to exercise later pipeline stages without triggering this check.
    /// Must be ≥ 1.
    /// </para>
    /// </remarks>
    public int MaxStatementBoundarySignalCount { get; }

    /// <summary>
    /// The default value for <see cref="MaxStatementBoundarySignalCount"/>.
    /// A typical CONDUSEF statement is 8–15 pages; 20 is a conservative ceiling that
    /// allows for longer-than-average single statements while still flagging most
    /// multi-statement archive bundles (which tend to exceed 20 pages).
    /// </summary>
    public const int DefaultMaxStatementBoundarySignalCount = 20;

    /// <summary>
    /// Initializes a <see cref="TenantProfile"/> with the specified identifiers, override map,
    /// and optional confidence threshold.
    /// </summary>
    /// <param name="tenantId">Unique tenant identifier. Must not be null or white-space.</param>
    /// <param name="tenantName">Human-readable tenant name. Must not be null or white-space.</param>
    /// <param name="toleranceOverrides">
    /// Per-rule override map. Pass an empty dictionary (or <see langword="null"/>
    /// to use an empty map automatically) when no overrides are desired.
    /// </param>
    /// <param name="minFieldConfidence">
    /// Minimum extraction-confidence threshold in [0.0, 1.0]. Defaults to
    /// <see cref="LegalMinFieldConfidenceDefault"/> (0.8) when omitted.
    /// A tenant may tighten (raise) this value; it is validated and clamped by the resolver.
    /// </param>
    /// <param name="minExtractionCoverageCount">
    /// Minimum number of extracted (or invalid-format) fields required before the pipeline
    /// proceeds to bind and validate. Defaults to <see cref="DefaultMinExtractionCoverageCount"/> (10).
    /// Must be ≥ 0.
    /// </param>
    /// <param name="minTextLayerWordCount">
    /// Minimum total word count across all PDF pages required before the pipeline proceeds to
    /// bind and validate. Defaults to <see cref="DefaultMinTextLayerWordCount"/> (20).
    /// Must be ≥ 0.
    /// </param>
    /// <param name="maxStatementBoundarySignalCount">
    /// Maximum PDF page count before the pipeline treats the document as a multi-statement
    /// bundle and routes to <c>BlockReason.AmbiguousDocumentScope</c>. Defaults to
    /// <see cref="DefaultMaxStatementBoundarySignalCount"/> (20). Must be ≥ 1.
    /// Pass <see cref="int.MaxValue"/> to disable the guard.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="tenantId"/> or <paramref name="tenantName"/> is null or white-space.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="minFieldConfidence"/> is outside [0.0, 1.0], or when
    /// <paramref name="minExtractionCoverageCount"/> or <paramref name="minTextLayerWordCount"/>
    /// is negative, or when <paramref name="maxStatementBoundarySignalCount"/> is less than 1.
    /// </exception>
    public TenantProfile(
        string tenantId,
        string tenantName,
        IReadOnlyDictionary<string, decimal>? toleranceOverrides = null,
        double minFieldConfidence = LegalMinFieldConfidenceDefault,
        int minExtractionCoverageCount = DefaultMinExtractionCoverageCount,
        int minTextLayerWordCount = DefaultMinTextLayerWordCount,
        int maxStatementBoundarySignalCount = DefaultMaxStatementBoundarySignalCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantName);
        if (minFieldConfidence is < MinFieldConfidenceLowerBound or > MinFieldConfidenceUpperBound)
            throw new ArgumentOutOfRangeException(
                nameof(minFieldConfidence),
                minFieldConfidence,
                $"MinFieldConfidence must be in [{MinFieldConfidenceLowerBound}, {MinFieldConfidenceUpperBound}].");
        if (minExtractionCoverageCount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(minExtractionCoverageCount),
                minExtractionCoverageCount,
                "MinExtractionCoverageCount must be >= 0.");
        if (minTextLayerWordCount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(minTextLayerWordCount),
                minTextLayerWordCount,
                "MinTextLayerWordCount must be >= 0.");
        if (maxStatementBoundarySignalCount < 1)
            throw new ArgumentOutOfRangeException(
                nameof(maxStatementBoundarySignalCount),
                maxStatementBoundarySignalCount,
                "MaxStatementBoundarySignalCount must be >= 1. Pass int.MaxValue to disable the guard.");

        TenantId = tenantId;
        TenantName = tenantName;
        ToleranceOverrides = toleranceOverrides
            ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        MinFieldConfidence = minFieldConfidence;
        MinExtractionCoverageCount = minExtractionCoverageCount;
        MinTextLayerWordCount = minTextLayerWordCount;
        MaxStatementBoundarySignalCount = maxStatementBoundarySignalCount;
    }

    /// <summary>
    /// Returns the CONDUSEF legal-baseline profile with no overrides and the default
    /// confidence threshold (<see cref="LegalMinFieldConfidenceDefault"/> = 0.8).
    /// Use this when a caller has no tenant context and must apply pure legal defaults.
    /// </summary>
    /// <returns>
    /// A <see cref="TenantProfile"/> with <see cref="TenantId"/> = <c>"LEGAL-BASELINE"</c>,
    /// <see cref="TenantName"/> = <c>"Legal Baseline (CONDUSEF)"</c>, an empty
    /// <see cref="ToleranceOverrides"/> map, and <see cref="MinFieldConfidence"/> = 0.8.
    /// </returns>
    public static TenantProfile LegalBaseline() =>
        new(
            tenantId: "LEGAL-BASELINE",
            tenantName: "Legal Baseline (CONDUSEF)",
            toleranceOverrides: new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
            minFieldConfidence: LegalMinFieldConfidenceDefault,
            minExtractionCoverageCount: DefaultMinExtractionCoverageCount,
            minTextLayerWordCount: DefaultMinTextLayerWordCount,
            maxStatementBoundarySignalCount: DefaultMaxStatementBoundarySignalCount);
}
