namespace ExxerCube.Prisma.Veriqan.Domain.Enums;

/// <summary>
/// Traffic-light signal that summarises all findings for a <see cref="Entities.VerificationJob"/>
/// into a single consumer-facing rollup.
/// </summary>
/// <remarks>
/// <para>
/// <b>Verdict vs. non-verdict signals:</b>
/// <see cref="Green"/>, <see cref="Red"/>, and <see cref="Yellow"/> are compliance verdicts.
/// <see cref="ExtractionGap"/>, <see cref="TransientFailure"/>, and <see cref="Blocked"/> are
/// <em>non-verdict</em> signals — they must NEVER be treated as a compliance pass and take
/// absolute precedence over any compliance finding.
/// </para>
/// <para>
/// <b>Routing decision rule (Story 4.2):</b> "Could the same document bytes, resubmitted
/// tomorrow with NO human action, produce a verdict?"
/// <list type="bullet">
///   <item><see cref="ExtractionGap"/> — No, but engineering could fix the system (permanent
///     system/capability gap).</item>
///   <item><see cref="TransientFailure"/> — Yes-maybe: transient infra/DB failure, retry may
///     succeed.</item>
///   <item><see cref="Blocked"/> — No, a genuine document defect requires human intervention.</item>
/// </list>
/// </para>
/// </remarks>
public enum VerdictSignal
{
    /// <summary>All checks passed — content is compliant.</summary>
    Green = 0,

    /// <summary>One or more checks failed — content is non-compliant.</summary>
    Red = 1,

    /// <summary>
    /// <b>RESERVED — no production emitter exists after Story 4.2.</b>
    /// Intended for genuine document defects requiring human callback (e.g. encrypted,
    /// corrupt, tampered, or out-of-scope documents) where resubmitting the same bytes
    /// would never produce a verdict without human intervention.
    /// <para>
    /// The pipeline currently emits <see cref="ExtractionGap"/> for all known non-verdict
    /// conditions.  <see cref="Blocked"/> will be wired when document-defect detection
    /// (EncryptedDocument, CorruptDocument, TamperedDocument, etc.) is implemented.
    /// </para>
    /// <para>
    /// Note: an aggregation of <see cref="FindingVerdict.InsufficientData"/>
    /// alone (with no explicit fail) correctly yields <see cref="Green"/>, not Blocked.
    /// </para>
    /// </summary>
    Blocked = 2,

    /// <summary>
    /// The statement is acceptable at the bank's own bar but has non-blocking improvement
    /// opportunities relative to the bank's full ruleset (bank-tier passes with gaps).
    /// <para>
    /// Two-tier combination rule (Story 1.1):
    /// <list type="bullet">
    ///   <item><see cref="ExtractionGap"/> / <see cref="Blocked"/> — take absolute precedence
    ///     over both tier verdicts.</item>
    ///   <item><see cref="Red"/> overall — when the CONDUSEF tier verdict is <see cref="Red"/>.</item>
    ///   <item><see cref="Yellow"/> overall — when the bank tier verdict is <see cref="Yellow"/>
    ///     and the CONDUSEF tier verdict is not <see cref="Red"/>.</item>
    ///   <item><see cref="Green"/> overall — both tiers are <see cref="Green"/>.</item>
    /// </list>
    /// </para>
    /// <para>
    /// See <c>VerdictSummary.BankTierVerdict</c>, <c>VerdictSummary.CondusefTierVerdict</c>, and
    /// <c>VerdictSummary.CombineOverallSignal</c> in <c>Veriqan.Application</c>.
    /// </para>
    /// </summary>
    Yellow = 3,

    /// <summary>
    /// A <b>permanent</b> system/engineering capability gap prevented a ruling (Story 4.2).
    /// Resubmitting the same document bytes tomorrow would yield the same outcome unless the
    /// system is updated — no human action on the document side can resolve this.
    /// <para>
    /// <b>Current emitters:</b> all four <see cref="BlockReason"/> values that are currently
    /// wired for detection (<see cref="BlockReason.UnknownProduct"/>,
    /// <see cref="BlockReason.InvalidBundle"/>,
    /// <see cref="BlockReason.InsufficientExtractionCoverage"/>,
    /// <see cref="BlockReason.InsufficientTextLayer"/>) route here.  Future taxonomy members
    /// that represent capability/config gaps (<see cref="BlockReason.AmbiguousDocumentScope"/>,
    /// <see cref="BlockReason.MissingMandatoryAnchorFields"/>,
    /// <see cref="BlockReason.RefDataVersionMismatch"/>) will also route here when wired.
    /// </para>
    /// <para>
    /// <b>Abstain-safety:</b> <see cref="ExtractionGap"/> is a non-verdict — it must never be
    /// treated as a compliance pass and takes absolute precedence over any compliance finding.
    /// </para>
    /// </summary>
    ExtractionGap = 4,

    /// <summary>
    /// A <b>retryable</b> operational failure prevented a ruling (Story 4.2).
    /// Resubmitting the same document bytes after the transient condition clears (e.g. DB
    /// connection restored, extractor crash resolved) may succeed.
    /// <para>
    /// <b>Current emitters:</b> none — this signal is reserved for future wiring when the
    /// pipeline detects transient infra/DB failures, extractor crashes, or timeouts that are
    /// distinguishable from permanent gaps.  Currently, such conditions produce a
    /// <see cref="IndQuestResults.Result{T}"/> failure (exception queue in batch) rather than
    /// a persisted verdict.
    /// </para>
    /// <para>
    /// <b>Abstain-safety:</b> <see cref="TransientFailure"/> is a non-verdict — it must never
    /// be treated as a compliance pass and takes absolute precedence over any compliance finding.
    /// Unlike <see cref="ExtractionGap"/> it is NOT a final tally outcome in batch reports
    /// (retryable means it should not count as a closed case).
    /// </para>
    /// </summary>
    TransientFailure = 5,
}
