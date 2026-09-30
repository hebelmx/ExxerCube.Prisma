// <copyright file="EvidencePackage.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Evidence;

/// <summary>
/// An immutable collection of <see cref="EvidenceItem"/> records accumulated during a single
/// workflow run or an entire harness session.  Embedded in <c>WorkflowResult</c> and in
/// <c>HarnessRunSummary</c>.
/// </summary>
/// <param name="RunId">
/// The identifier of the harness run or workflow execution that produced this package.
/// Correlates evidence items across the run summary and individual workflow results.
/// </param>
/// <param name="Items">The ordered list of evidence items captured during the run.</param>
public sealed record EvidencePackage(
    string RunId,
    IReadOnlyList<EvidenceItem> Items);
