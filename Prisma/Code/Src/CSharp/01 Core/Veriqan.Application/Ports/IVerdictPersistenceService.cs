using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Application service port for durably persisting the <see cref="JobVerdict"/> and all
/// <see cref="Finding"/> rows produced by a successful verification pipeline run.
/// </summary>
/// <remarks>
/// <para>
/// Called by the pipeline's persist stage (stage 8) immediately after verdict aggregation.
/// Persistence is <b>non-optional</b>: a failure returned by this service causes the
/// pipeline to return <c>Result.WithFailure</c> rather than silently discarding the verdict.
/// </para>
/// <para>
/// The implementation maps each <see cref="RuleFinding"/> (the rich in-process value object)
/// to a <see cref="Finding"/> entity (the lean persistence entity) and saves both the verdict
/// and the findings in a single unit of work.
/// </para>
/// </remarks>
public interface IVerdictPersistenceService
{
    /// <summary>
    /// Persists a <see cref="JobVerdict"/> and the associated <see cref="Finding"/> rows for the
    /// given verification job.
    /// </summary>
    /// <param name="jobId">Identifier of the parent <see cref="VerificationJob"/>.</param>
    /// <param name="signal">Traffic-light signal to store in the <see cref="JobVerdict"/> row.</param>
    /// <param name="findings">
    /// The ordered list of <see cref="RuleFinding"/> items produced by the validation engine.
    /// Each item is mapped to a <see cref="Finding"/> entity before being saved.
    /// </param>
    /// <param name="engineVersion">
    /// Semantic version of the verification engine that produced these findings, stored on every
    /// <see cref="Finding"/> row for auditability (NFR-7).
    /// </param>
    /// <param name="bankTierVerdict">
    /// Bank-tier verdict (Story 1.4). Defaults to <c>Green</c> so existing early-exit
    /// blocked callers that supply only <paramref name="signal"/> compile unchanged.
    /// The main pipeline path supplies <c>VerdictSummary.BankTierVerdict</c>.
    /// </param>
    /// <param name="condusefTierVerdict">
    /// CONDUSEF-tier verdict (Story 1.4). Defaults to <c>Green</c> — same rationale as
    /// <paramref name="bankTierVerdict"/>. The main pipeline path supplies
    /// <c>VerdictSummary.CondusefTierVerdict</c>.
    /// </param>
    /// <param name="checklistTiers">
    /// Per-tenant checklist-tier map used to stamp each <see cref="Finding"/> with its
    /// <see cref="Domain.Entities.Finding.Tier"/> (Story 1.4). Pass <see langword="null"/>
    /// when unavailable; unmapped check IDs default to <c>ChecklistTier.Condusef</c>.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="referenceBundleVersion">
    /// Version of the reference-data bundle (<c>BundleMetadata.SchemaVersion</c>) active when
    /// this verdict was computed (VERIQAN-E3-S4), stored on the <see cref="JobVerdict"/> row for
    /// provenance/audit linkage. Pass <see langword="null"/> when no bundle was resolved for the
    /// run (graceful-degradation path). Deliberately placed after <paramref name="cancellationToken"/>
    /// (trailing optional parameter added post-hoc) so existing callers compile unchanged — the
    /// same pattern already used by <c>IStatementFieldExtractor.ExtractFullAsync</c>'s
    /// <c>referenceBundle</c> parameter.
    /// </param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the persisted <see cref="JobVerdict"/> on success.
    /// Returns a failure result on any infrastructure error, and a cancelled result when the
    /// cancellation token is already signalled before or during the operation.
    /// </returns>
    Task<Result<JobVerdict>> PersistAsync(
        Guid jobId,
        Domain.Enums.VerdictSignal signal,
        IReadOnlyList<RuleFinding> findings,
        string engineVersion,
        Domain.Enums.VerdictSignal bankTierVerdict = Domain.Enums.VerdictSignal.Green,
        Domain.Enums.VerdictSignal condusefTierVerdict = Domain.Enums.VerdictSignal.Green,
        IReadOnlyDictionary<string, Domain.Enums.ChecklistTier>? checklistTiers = null,
        CancellationToken cancellationToken = default,
        string? referenceBundleVersion = null);
}
