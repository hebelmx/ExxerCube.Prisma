// <copyright file="EvidenceItem.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Evidence;

/// <summary>
/// Represents a single captured artefact (screenshot, log snapshot, file, network trace, etc.)
/// collected by an <see cref="IEvidenceCollector"/> during a workflow run.
/// </summary>
/// <param name="Kind">The type of artefact (e.g. <see cref="EvidenceKind.Screenshot"/>).</param>
/// <param name="Label">A short human-readable name for the artefact used in reports.</param>
/// <param name="AbsolutePath">The fully-qualified file system path where the artefact was written.</param>
/// <param name="CapturedAt">The UTC timestamp at which the artefact was captured.</param>
/// <param name="SizeBytes">The size of the artefact in bytes as observed at capture time.</param>
public sealed record EvidenceItem(
    EvidenceKind Kind,
    string Label,
    string AbsolutePath,
    DateTimeOffset CapturedAt,
    long SizeBytes);
