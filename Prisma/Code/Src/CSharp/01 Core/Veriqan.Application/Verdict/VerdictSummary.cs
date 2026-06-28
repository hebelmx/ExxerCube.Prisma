using System.Collections.Generic;
using System.Linq;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;

namespace ExxerCube.Prisma.Veriqan.Application.Verdict;

/// <summary>
/// Immutable value object that summarises a complete set of <see cref="Domain.Verification.RuleFinding"/>
/// items into a single traffic-light signal together with per-category counts and diagnostic lists.
/// </summary>
/// <remarks>
/// <para>
/// <b>Precedence (FR-15):</b>
/// <list type="number">
///   <item><b>BLOCKED</b> — a <see cref="BlockedOutcome"/> was supplied; binding could not
///     complete before rules ran.  The signal is <see cref="VerdictSignal.Blocked"/>
///     regardless of findings.</item>
///   <item><b>RED</b> — at least one finding has <see cref="FindingVerdict.Fail"/>.
///     The signal is <see cref="VerdictSignal.Red"/>.</item>
///   <item><b>GREEN</b> — all findings are Pass or InsufficientData (or the list is empty).
///     <see cref="FindingVerdict.InsufficientData"/> findings do <b>not</b> escalate to Red;
///     they are surfaced separately via <see cref="InsufficientDataCount"/> and
///     <see cref="InsufficientDataCheckIds"/>.</item>
/// </list>
/// </para>
/// <para>
/// <b>Mapping to <see cref="Domain.Entities.JobVerdict"/>:</b>
/// Use <see cref="ToJobVerdict"/> when the persistence story (Epic 7, Story 7.4) needs to
/// materialise a <see cref="Domain.Entities.JobVerdict"/> entity from this summary.
/// </para>
/// </remarks>
public sealed record VerdictSummary
{
    // -----------------------------------------------------------------------
    // Construction (via static factories only — keeps ctor args hidden)
    // -----------------------------------------------------------------------

    private VerdictSummary(
        VerdictSignal signal,
        int failCount,
        int passCount,
        int insufficientDataCount,
        int total,
        IReadOnlyList<string> failCheckIds,
        IReadOnlyList<string> insufficientDataCheckIds,
        BlockedOutcome? blockedOutcome,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null,
        IReadOnlyList<string>? legalBreachCheckIds = null,
        IReadOnlyList<string>? tenantOnlyFailCheckIds = null,
        VerdictSignal bankTierVerdict = VerdictSignal.Green,
        VerdictSignal condusefTierVerdict = VerdictSignal.Green,
        IReadOnlyList<string>? bankFailCheckIds = null,
        IReadOnlyList<string>? condusefFailCheckIds = null,
        double confidence = 1.0,
        string? transientDetail = null)
    {
        Signal = signal;
        FailCount = failCount;
        PassCount = passCount;
        InsufficientDataCount = insufficientDataCount;
        Total = total;
        FailCheckIds = failCheckIds;
        InsufficientDataCheckIds = insufficientDataCheckIds;
        BlockedOutcome = blockedOutcome;
        TenantDeviations = tenantDeviations ?? [];
        LegalBreachCheckIds = legalBreachCheckIds ?? [];
        TenantOnlyFailCheckIds = tenantOnlyFailCheckIds ?? [];
        BankTierVerdict = bankTierVerdict;
        CondusefTierVerdict = condusefTierVerdict;
        BankFailCheckIds = bankFailCheckIds ?? [];
        CondusefFailCheckIds = condusefFailCheckIds ?? [];
        Confidence = confidence;
        TransientDetail = transientDetail;
    }

    // -----------------------------------------------------------------------
    // Properties
    // -----------------------------------------------------------------------

    /// <summary>Gets the traffic-light signal that summarises all findings.</summary>
    /// <remarks>
    /// <b>Two-tier combination rule (Story 1.1 — FR-15 extension):</b>
    /// <list type="number">
    ///   <item><b>BLOCKED</b> — takes absolute precedence over tier verdicts.</item>
    ///   <item><b>RED</b> — <see cref="CondusefTierVerdict"/> is <see cref="VerdictSignal.Red"/>.</item>
    ///   <item><b>YELLOW</b> — <see cref="BankTierVerdict"/> is <see cref="VerdictSignal.Yellow"/>
    ///     and <see cref="CondusefTierVerdict"/> is not <see cref="VerdictSignal.Red"/>.</item>
    ///   <item><b>GREEN</b> — both <see cref="BankTierVerdict"/> and <see cref="CondusefTierVerdict"/>
    ///     are <see cref="VerdictSignal.Green"/>.</item>
    /// </list>
    /// Use <see cref="CombineOverallSignal"/> to compute this from tier verdicts directly.
    /// </remarks>
    public VerdictSignal Signal { get; }

    // -----------------------------------------------------------------------
    // Two-tier verdict surface (Story 1.1)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets the verdict from the bank's own ruleset tier.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="VerdictSignal.Green"/> — no bank-tier failures.
    /// <see cref="VerdictSignal.Yellow"/> — bank-tier passes with improvement opportunities
    /// (non-blocking gaps relative to the bank's full ruleset).
    /// </para>
    /// <para>
    /// Until bank-tier rule classification is wired (Story 1.2 / 1.3), this property
    /// defaults to <see cref="VerdictSignal.Green"/> for all non-blocked summaries and
    /// <see cref="VerdictSignal.Blocked"/> for blocked summaries.
    /// </para>
    /// </remarks>
    public VerdictSignal BankTierVerdict { get; }

    /// <summary>
    /// Gets the verdict from the CONDUSEF regulatory tier (the legal compliance floor).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="VerdictSignal.Green"/> — no CONDUSEF-mandated failures.
    /// <see cref="VerdictSignal.Red"/> — at least one CONDUSEF-mandated rule failed.
    /// </para>
    /// <para>
    /// Until tier classification is wired (Story 1.2 / 1.3), every rule is treated as
    /// CONDUSEF-mandated, so this property mirrors the overall <see cref="Signal"/>
    /// for Green/Red outcomes.  Blocked summaries carry
    /// <see cref="VerdictSignal.Blocked"/> here as well.
    /// </para>
    /// </remarks>
    public VerdictSignal CondusefTierVerdict { get; }

    // -----------------------------------------------------------------------
    // Verdict-level extraction confidence (Story 4.1 — Epic 4 confidence degree)
    // -----------------------------------------------------------------------

    /// <summary>
    /// The minimum extraction confidence across all <see cref="Domain.Verification.RuleFinding"/>
    /// items that contributed to this verdict.  Ranges from <c>0.0</c> (fully uncertain) to
    /// <c>1.0</c> (no low-confidence guarded field consumed by any rule).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Computed by <see cref="VerdictAggregator"/> as
    /// <c>findings.Min(f =&gt; f.Confidence)</c>.  An empty findings list yields <c>1.0</c>
    /// (no evidence of low confidence).  A <see cref="VerdictSignal.Blocked"/> or
    /// <see cref="VerdictSignal.ExtractionGap"/> summary also carries <c>1.0</c> because
    /// no rules were evaluated.
    /// </para>
    /// <para>
    /// <b>Semantics of 1.0 (important):</b> a value of <c>1.0</c> means no rule consumed a
    /// confidence-guarded field with low fidelity — it is <em>not</em> a verdict-level
    /// certainty attestation.  Rules that evaluate presence, structural layout, document
    /// movement, section structure, or legend elements always produce per-finding
    /// <c>1.0</c> regardless of extraction fidelity (see
    /// <see cref="Domain.Verification.RuleFinding.Confidence"/>).
    /// </para>
    /// <para>
    /// Consumer note: a low <c>Confidence</c> alongside <see cref="VerdictSignal.Green"/> means
    /// all rules passed but some fields were extracted with low fidelity — the verdict is correct
    /// under the data available, but should be treated with appropriate caution.
    /// </para>
    /// </remarks>
    public double Confidence { get; }

    // -----------------------------------------------------------------------
    // Two-tier fail partitions (Story 1.2)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets the <see cref="Domain.Verification.RuleFinding.CheckId"/> values of fail findings
    /// that belong to the bank tier (<c>ChecklistTier.Bank</c> or <c>ChecklistTier.Both</c>).
    /// </summary>
    /// <remarks>
    /// Empty when no tier map was supplied to the aggregator (null-map / legacy path),
    /// or when no bank-tier checks failed.
    /// Populated only by <c>VerdictAggregator.Aggregate</c> when a non-null
    /// <c>checklistTiers</c> map is provided (Story 1.2 / 1.3).
    /// </remarks>
    public IReadOnlyList<string> BankFailCheckIds { get; }

    /// <summary>
    /// Gets the <see cref="Domain.Verification.RuleFinding.CheckId"/> values of fail findings
    /// that belong to the CONDUSEF tier (<c>ChecklistTier.Condusef</c> or <c>ChecklistTier.Both</c>).
    /// </summary>
    /// <remarks>
    /// Empty when no tier map was supplied to the aggregator (null-map / legacy path),
    /// or when no CONDUSEF-tier checks failed.
    /// Populated only by <c>VerdictAggregator.Aggregate</c> when a non-null
    /// <c>checklistTiers</c> map is provided (Story 1.2 / 1.3).
    /// </remarks>
    public IReadOnlyList<string> CondusefFailCheckIds { get; }

    /// <summary>Gets the count of <see cref="FindingVerdict.Fail"/> findings.</summary>
    public int FailCount { get; }

    /// <summary>Gets the count of <see cref="FindingVerdict.Pass"/> findings.</summary>
    public int PassCount { get; }

    /// <summary>
    /// Gets the count of <see cref="FindingVerdict.InsufficientData"/> findings.
    /// These are reported here but do <b>not</b> by themselves make the signal Red.
    /// </summary>
    public int InsufficientDataCount { get; }

    /// <summary>Gets the total number of findings evaluated (Pass + Fail + InsufficientData).</summary>
    public int Total { get; }

    /// <summary>Gets the <see cref="Domain.Verification.RuleFinding.CheckId"/> values of all failed checks.</summary>
    public IReadOnlyList<string> FailCheckIds { get; }

    /// <summary>
    /// Gets the <see cref="Domain.Verification.RuleFinding.CheckId"/> values of all InsufficientData checks.
    /// </summary>
    public IReadOnlyList<string> InsufficientDataCheckIds { get; }

    /// <summary>
    /// Gets the <see cref="Binding.BlockedOutcome"/> when <see cref="Signal"/> is
    /// <see cref="VerdictSignal.Blocked"/> or <see cref="VerdictSignal.ExtractionGap"/>;
    /// otherwise <see langword="null"/>.
    /// </summary>
    public BlockedOutcome? BlockedOutcome { get; }

    /// <summary>
    /// Gets a human-readable detail string when <see cref="Signal"/> is
    /// <see cref="VerdictSignal.TransientFailure"/>; <see langword="null"/> for all other signals.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the carrier for transient-failure context (Story 4.2). Unlike
    /// <see cref="BlockedOutcome"/> (which carries a typed <see cref="Domain.Enums.BlockReason"/>
    /// for routing), a transient failure does not require machine-readable reason routing —
    /// callers should simply retry.  The detail string is surfaced for logging and diagnostics.
    /// </para>
    /// </remarks>
    public string? TransientDetail { get; }

    /// <summary>
    /// Gets the list of rejected tenant override deviations produced during tenant-profile
    /// resolution (Story 9.3b). Empty when no tenant profile was applied or when all
    /// overrides were accepted.
    /// </summary>
    /// <remarks>
    /// Surfaced here so that compliance reviewers examining the verdict summary can see
    /// which tenant tolerance overrides were silently reverted to the CONDUSEF legal baseline.
    /// </remarks>
    public IReadOnlyList<TenantDeviation> TenantDeviations { get; }

    // -----------------------------------------------------------------------
    // Legal-baseline separable signal (Fix D — Story 9 remediation)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets the <see cref="Domain.Verification.RuleFinding.CheckId"/> values of findings where
    /// <see cref="Domain.Verification.RuleFinding.LegalBaselineVerdict"/> is
    /// <see cref="FindingVerdict.Fail"/> — i.e. the statement breaches the CONDUSEF legal floor,
    /// not merely the tenant's stricter bar.
    /// </summary>
    /// <remarks>
    /// Empty when <see cref="Signal"/> is <see cref="VerdictSignal.Blocked"/> or when no
    /// findings breach the legal floor.
    /// </remarks>
    public IReadOnlyList<string> LegalBreachCheckIds { get; }

    /// <summary>
    /// Gets the <see cref="Domain.Verification.RuleFinding.CheckId"/> values of findings where
    /// <see cref="Domain.Verification.RuleFinding.Verdict"/> is <see cref="FindingVerdict.Fail"/>
    /// <b>but</b> <see cref="Domain.Verification.RuleFinding.LegalBaselineVerdict"/> is
    /// <see cref="FindingVerdict.Pass"/> — i.e. the statement satisfies the legal floor but
    /// fails the tenant's stricter threshold.
    /// </summary>
    /// <remarks>
    /// A non-empty list here indicates the pipeline gate (on effective <c>Verdict</c>) fired
    /// for a purely tenant-driven reason, not a regulatory breach.
    /// </remarks>
    public IReadOnlyList<string> TenantOnlyFailCheckIds { get; }

    /// <summary>
    /// Gets a separable traffic-light signal derived exclusively from
    /// <see cref="LegalBreachCheckIds"/>: <see cref="VerdictSignal.Red"/> when at least one
    /// finding breaches the CONDUSEF legal floor; <see cref="VerdictSignal.Green"/> otherwise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This signal is purely informational — the pipeline gates on <see cref="Signal"/>
    /// (the effective, possibly-stricter tenant verdict) and this value must NOT change that.
    /// </para>
    /// <para>
    /// When <see cref="Signal"/> is a non-verdict signal (<see cref="VerdictSignal.Blocked"/>,
    /// <see cref="VerdictSignal.ExtractionGap"/>, or <see cref="VerdictSignal.TransientFailure"/>)
    /// this property mirrors <see cref="Signal"/> — no legal-floor ruling was made.
    /// </para>
    /// </remarks>
    public VerdictSignal LegalBaselineSignal =>
        Signal is VerdictSignal.Blocked or VerdictSignal.ExtractionGap or VerdictSignal.TransientFailure
            ? Signal
            : LegalBreachCheckIds.Count > 0
                ? VerdictSignal.Red
                : VerdictSignal.Green;

    // -----------------------------------------------------------------------
    // Static factories
    // -----------------------------------------------------------------------

    /// <summary>Creates a <see cref="VerdictSignal.Green"/> summary from aggregated counts.</summary>
    /// <param name="passCount">Count of passing findings.</param>
    /// <param name="insufficientDataCount">Count of InsufficientData findings.</param>
    /// <param name="insufficientDataCheckIds">Check IDs with InsufficientData verdicts.</param>
    /// <param name="tenantDeviations">Rejected tenant override deviations, if any.</param>
    /// <param name="legalBreachCheckIds">
    /// Check IDs whose <see cref="Domain.Verification.RuleFinding.LegalBaselineVerdict"/>
    /// is <see cref="FindingVerdict.Fail"/>. Pass <see langword="null"/> or omit when there
    /// are none (no legal-floor breaches in a Green summary).
    /// </param>
    /// <param name="tenantOnlyFailCheckIds">
    /// Check IDs where <see cref="Domain.Verification.RuleFinding.Verdict"/> is Fail but
    /// <see cref="Domain.Verification.RuleFinding.LegalBaselineVerdict"/> is Pass.
    /// Pass <see langword="null"/> or omit when there are none.
    /// </param>
    /// <param name="bankFailCheckIds">
    /// Check IDs of fail findings in the bank tier (Story 1.2). Empty when no tier map was supplied
    /// or when no bank-tier checks failed. Always empty for Green summaries.
    /// </param>
    /// <param name="condusefFailCheckIds">
    /// Check IDs of fail findings in the CONDUSEF tier (Story 1.2). Empty when no tier map was
    /// supplied or when no CONDUSEF-tier checks failed. Always empty for Green summaries.
    /// </param>
    /// <param name="confidence">
    /// Minimum extraction confidence across all findings (Story 4.1). Defaults to <c>1.0</c>.
    /// </param>
    internal static VerdictSummary Green(
        int passCount,
        int insufficientDataCount,
        IReadOnlyList<string> insufficientDataCheckIds,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null,
        IReadOnlyList<string>? legalBreachCheckIds = null,
        IReadOnlyList<string>? tenantOnlyFailCheckIds = null,
        IReadOnlyList<string>? bankFailCheckIds = null,
        IReadOnlyList<string>? condusefFailCheckIds = null,
        double confidence = 1.0) =>
        new(
            signal: VerdictSignal.Green,
            failCount: 0,
            passCount: passCount,
            insufficientDataCount: insufficientDataCount,
            total: passCount + insufficientDataCount,
            failCheckIds: [],
            insufficientDataCheckIds: insufficientDataCheckIds,
            blockedOutcome: null,
            tenantDeviations: tenantDeviations,
            legalBreachCheckIds: legalBreachCheckIds,
            tenantOnlyFailCheckIds: tenantOnlyFailCheckIds,
            // Story 1.1: no tier data yet — both tiers default Green (combination rule: Green ∧ Green = Green)
            bankTierVerdict: VerdictSignal.Green,
            condusefTierVerdict: VerdictSignal.Green,
            // Story 1.2: partition lists (always empty for Green summaries; carried for API completeness)
            bankFailCheckIds: bankFailCheckIds,
            condusefFailCheckIds: condusefFailCheckIds,
            // Story 4.1: min confidence across all findings
            confidence: confidence);

    /// <summary>
    /// Creates a fail summary (default: <see cref="VerdictSignal.Red"/>) from aggregated counts.
    /// </summary>
    /// <param name="failCount">Count of failing findings.</param>
    /// <param name="passCount">Count of passing findings.</param>
    /// <param name="insufficientDataCount">Count of InsufficientData findings.</param>
    /// <param name="failCheckIds">Check IDs with Fail verdicts.</param>
    /// <param name="insufficientDataCheckIds">Check IDs with InsufficientData verdicts.</param>
    /// <param name="tenantDeviations">Rejected tenant override deviations, if any.</param>
    /// <param name="legalBreachCheckIds">
    /// Check IDs whose <see cref="Domain.Verification.RuleFinding.LegalBaselineVerdict"/>
    /// is <see cref="FindingVerdict.Fail"/>. Pass <see langword="null"/> or omit when there
    /// are none.
    /// </param>
    /// <param name="tenantOnlyFailCheckIds">
    /// Check IDs where effective <see cref="Domain.Verification.RuleFinding.Verdict"/> is Fail
    /// but <see cref="Domain.Verification.RuleFinding.LegalBaselineVerdict"/> is Pass.
    /// Pass <see langword="null"/> or omit when there are none.
    /// </param>
    /// <param name="bankFailCheckIds">
    /// Check IDs of fail findings in the bank tier (Story 1.2). Empty when no tier map was supplied.
    /// </param>
    /// <param name="condusefFailCheckIds">
    /// Check IDs of fail findings in the CONDUSEF tier (Story 1.2). Empty when no tier map was supplied.
    /// </param>
    /// <param name="bankTierVerdict">
    /// Bank-tier verdict to store on the summary (Story 1.2).
    /// Defaults to <see cref="VerdictSignal.Green"/> (legacy: no bank-tier data).
    /// Pass <see cref="VerdictSignal.Yellow"/> when bank-tier fails are present.
    /// </param>
    /// <param name="condusefTierVerdict">
    /// CONDUSEF-tier verdict to store on the summary (Story 1.2).
    /// Defaults to <see cref="VerdictSignal.Red"/> (legacy: every fail is a regulatory breach).
    /// Pass <see cref="VerdictSignal.Green"/> when only bank-tier fails are present.
    /// </param>
    /// <param name="signal">
    /// Overall traffic-light signal.  Defaults to <see cref="VerdictSignal.Red"/> (legacy path).
    /// Pass <see cref="VerdictSignal.Yellow"/> when the two-tier combination rule yields Yellow
    /// (i.e. bank-only fails, CONDUSEF tier is Green).
    /// </param>
    /// <param name="confidence">
    /// Minimum extraction confidence across all findings (Story 4.1). Defaults to <c>1.0</c>.
    /// </param>
    internal static VerdictSummary Red(
        int failCount,
        int passCount,
        int insufficientDataCount,
        IReadOnlyList<string> failCheckIds,
        IReadOnlyList<string> insufficientDataCheckIds,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null,
        IReadOnlyList<string>? legalBreachCheckIds = null,
        IReadOnlyList<string>? tenantOnlyFailCheckIds = null,
        IReadOnlyList<string>? bankFailCheckIds = null,
        IReadOnlyList<string>? condusefFailCheckIds = null,
        VerdictSignal bankTierVerdict = VerdictSignal.Green,
        VerdictSignal condusefTierVerdict = VerdictSignal.Red,
        VerdictSignal signal = VerdictSignal.Red,
        double confidence = 1.0) =>
        new(
            signal: signal,
            failCount: failCount,
            passCount: passCount,
            insufficientDataCount: insufficientDataCount,
            total: failCount + passCount + insufficientDataCount,
            failCheckIds: failCheckIds,
            insufficientDataCheckIds: insufficientDataCheckIds,
            blockedOutcome: null,
            tenantDeviations: tenantDeviations,
            legalBreachCheckIds: legalBreachCheckIds,
            tenantOnlyFailCheckIds: tenantOnlyFailCheckIds,
            // Story 1.1 defaults (Green/Red): override via bankTierVerdict / condusefTierVerdict params.
            // Story 1.2: tier map path supplies partitioned values via those params.
            bankTierVerdict: bankTierVerdict,
            condusefTierVerdict: condusefTierVerdict,
            // Story 1.2: tier partition lists
            bankFailCheckIds: bankFailCheckIds,
            condusefFailCheckIds: condusefFailCheckIds,
            // Story 4.1: min confidence across all findings
            confidence: confidence);

    /// <summary>
    /// Creates a <see cref="VerdictSignal.Blocked"/> summary from a binding failure.
    /// </summary>
    /// <remarks>
    /// <b>RESERVED — no production emitter after Story 4.2.</b>
    /// The pipeline routes all currently-wired reasons to <see cref="ExtractionGap"/> instead.
    /// This factory is kept for future use when genuine document-defect detection
    /// (EncryptedDocument, CorruptDocument, TamperedDocument, etc.) is wired.
    /// </remarks>
    /// <param name="blockedOutcome">The blocking outcome from the binder.</param>
    /// <param name="tenantDeviations">Rejected tenant override deviations, if any.</param>
    internal static VerdictSummary Blocked(
        BlockedOutcome blockedOutcome,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null) =>
        new(
            signal: VerdictSignal.Blocked,
            failCount: 0,
            passCount: 0,
            insufficientDataCount: 0,
            total: 0,
            failCheckIds: [],
            insufficientDataCheckIds: [],
            blockedOutcome: blockedOutcome,
            tenantDeviations: tenantDeviations,
            legalBreachCheckIds: null,
            tenantOnlyFailCheckIds: null,
            // Story 1.1: Blocked takes absolute precedence — both tier verdicts mirror Blocked.
            bankTierVerdict: VerdictSignal.Blocked,
            condusefTierVerdict: VerdictSignal.Blocked);

    /// <summary>
    /// Creates a <see cref="VerdictSignal.ExtractionGap"/> summary from a permanent
    /// system/engineering capability gap (Story 4.2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Abstain-safety:</b> ExtractionGap is a non-verdict and takes absolute precedence —
    /// it must never be treated as a compliance pass.
    /// </para>
    /// <para>
    /// All four currently-wired <see cref="Domain.Enums.BlockReason"/> values
    /// (<see cref="Domain.Enums.BlockReason.UnknownProduct"/>,
    /// <see cref="Domain.Enums.BlockReason.InvalidBundle"/>,
    /// <see cref="Domain.Enums.BlockReason.InsufficientExtractionCoverage"/>,
    /// <see cref="Domain.Enums.BlockReason.InsufficientTextLayer"/>) route here via
    /// <c>VerdictAggregator</c>.
    /// </para>
    /// </remarks>
    /// <param name="extractionGapOutcome">The blocking outcome carrying the gap reason and detail.</param>
    /// <param name="tenantDeviations">Rejected tenant override deviations, if any.</param>
    internal static VerdictSummary ExtractionGap(
        BlockedOutcome extractionGapOutcome,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null) =>
        new(
            signal: VerdictSignal.ExtractionGap,
            failCount: 0,
            passCount: 0,
            insufficientDataCount: 0,
            total: 0,
            failCheckIds: [],
            insufficientDataCheckIds: [],
            blockedOutcome: extractionGapOutcome,
            tenantDeviations: tenantDeviations,
            legalBreachCheckIds: null,
            tenantOnlyFailCheckIds: null,
            // Story 4.2: ExtractionGap takes absolute precedence — both tier verdicts mirror ExtractionGap.
            bankTierVerdict: VerdictSignal.ExtractionGap,
            condusefTierVerdict: VerdictSignal.ExtractionGap);

    /// <summary>
    /// Creates a <see cref="VerdictSignal.TransientFailure"/> summary for a retryable
    /// operational failure (Story 4.2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Abstain-safety:</b> TransientFailure is a non-verdict and takes absolute precedence —
    /// it must never be treated as a compliance pass.
    /// </para>
    /// <para>
    /// Unlike <see cref="ExtractionGap"/> (which carries a typed
    /// <see cref="Domain.Enums.BlockReason"/>), a transient failure does not require
    /// machine-readable reason routing.  The <paramref name="detail"/> string is used for
    /// logging and diagnostics only; callers should simply retry.
    /// </para>
    /// <para>
    /// <b>Current emitters:</b> none.  This factory is reserved for future wiring when the
    /// pipeline can distinguish transient infra/DB failures from permanent gaps.
    /// </para>
    /// </remarks>
    /// <param name="detail">Human-readable description of the transient failure.</param>
    /// <param name="tenantDeviations">Rejected tenant override deviations, if any.</param>
    internal static VerdictSummary TransientFailure(
        string detail,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null) =>
        new(
            signal: VerdictSignal.TransientFailure,
            failCount: 0,
            passCount: 0,
            insufficientDataCount: 0,
            total: 0,
            failCheckIds: [],
            insufficientDataCheckIds: [],
            blockedOutcome: null,
            tenantDeviations: tenantDeviations,
            legalBreachCheckIds: null,
            tenantOnlyFailCheckIds: null,
            // Story 4.2: TransientFailure takes absolute precedence — both tier verdicts mirror TransientFailure.
            bankTierVerdict: VerdictSignal.TransientFailure,
            condusefTierVerdict: VerdictSignal.TransientFailure,
            transientDetail: detail);

    // -----------------------------------------------------------------------
    // Two-tier signal combination rule (Story 1.1)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Computes the overall <see cref="VerdictSignal"/> from the two per-tier verdicts,
    /// applying the FR-15 two-tier combination rule:
    /// <list type="number">
    ///   <item><b>BLOCKED</b> — callers must use the <see cref="Blocked"/> factory directly;
    ///     this method does not accept <see cref="VerdictSignal.Blocked"/> as a tier input.</item>
    ///   <item><b>RED</b> — <paramref name="condusefTier"/> is <see cref="VerdictSignal.Red"/>.</item>
    ///   <item><b>YELLOW</b> — <paramref name="bankTier"/> is <see cref="VerdictSignal.Yellow"/>
    ///     and <paramref name="condusefTier"/> is not <see cref="VerdictSignal.Red"/>.</item>
    ///   <item><b>GREEN</b> — both tiers are <see cref="VerdictSignal.Green"/>.</item>
    /// </list>
    /// </summary>
    /// <param name="bankTier">The bank-tier verdict (<see cref="BankTierVerdict"/>).</param>
    /// <param name="condusefTier">The CONDUSEF-tier verdict (<see cref="CondusefTierVerdict"/>).</param>
    /// <returns>The combined overall <see cref="VerdictSignal"/>.</returns>
    internal static VerdictSignal CombineOverallSignal(VerdictSignal bankTier, VerdictSignal condusefTier)
    {
        if (condusefTier == VerdictSignal.Red)
            return VerdictSignal.Red;

        if (bankTier == VerdictSignal.Yellow)
            return VerdictSignal.Yellow;

        return VerdictSignal.Green;
    }

    // -----------------------------------------------------------------------
    // Projection
    // -----------------------------------------------------------------------

    /// <summary>
    /// Projects this summary onto a new <see cref="Domain.Entities.JobVerdict"/> entity.
    /// </summary>
    /// <remarks>
    /// Wires <see cref="Signal"/>, <see cref="BankTierVerdict"/>, and
    /// <see cref="CondusefTierVerdict"/> into the persisted entity (Story 1.4).
    /// </remarks>
    /// <param name="id">Unique identifier for the new <see cref="Domain.Entities.JobVerdict"/>.</param>
    /// <param name="verificationJobId">Parent job identifier.</param>
    /// <returns>
    /// A new <see cref="Domain.Entities.JobVerdict"/> with the overall signal and both tier
    /// verdicts mapped from this summary.
    /// </returns>
    public Domain.Entities.JobVerdict ToJobVerdict(System.Guid id, System.Guid verificationJobId) =>
        new(id, verificationJobId, Signal, BankTierVerdict, CondusefTierVerdict);
}
