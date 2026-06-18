using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Verification;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

/// <summary>
/// Immutable outcome record produced by <see cref="IVerificationPipeline"/> upon successful
/// completion of all pipeline stages.
/// </summary>
/// <param name="Job">The verification job created or retrieved during ingestion.</param>
/// <param name="Summary">Aggregated verdict summary (Green / Red / Blocked).</param>
/// <param name="Findings">Ordered list of rule findings from the validation engine.</param>
/// <param name="ProcessingDuration">
/// Wall-clock time from pipeline entry to outcome production.
/// <see cref="TimeSpan.Zero"/> when the pipeline did not instrument the run (e.g. in
/// legacy unit tests that construct the record directly).
/// Used by NFR-1 monitoring (p95 ≤ 10 s per statement).
/// </param>
public sealed record VerificationOutcome(
    VerificationJob Job,
    VerdictSummary Summary,
    IReadOnlyList<RuleFinding> Findings,
    TimeSpan ProcessingDuration = default);
