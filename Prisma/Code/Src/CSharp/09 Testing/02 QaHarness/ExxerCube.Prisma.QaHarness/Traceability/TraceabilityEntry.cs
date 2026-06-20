// <copyright file="TraceabilityEntry.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Traceability;

/// <summary>
/// Links the result of a workflow run or validation pass to the PRD requirements,
/// product features, and system invariants it exercises.
/// </summary>
/// <param name="SourceId">
/// The unique identifier of the artefact that produced this entry:
/// the workflow name or the validator identifier.
/// </param>
/// <param name="SourceKind">
/// Whether this entry was produced by a workflow, a domain validator, or a manual annotation.
/// </param>
/// <param name="Requirements">PRD requirements exercised by this artefact.</param>
/// <param name="Features">Product features exercised by this artefact.</param>
/// <param name="Invariants">System invariants verified by this artefact.</param>
/// <param name="RecordedAt">UTC timestamp at which the entry was added to the <see cref="ITraceabilityMap"/>.</param>
public sealed record TraceabilityEntry(
    string SourceId,
    TraceabilitySourceKind SourceKind,
    IReadOnlyList<RequirementRef> Requirements,
    IReadOnlyList<FeatureRef> Features,
    IReadOnlyList<InvariantRef> Invariants,
    DateTimeOffset RecordedAt);
