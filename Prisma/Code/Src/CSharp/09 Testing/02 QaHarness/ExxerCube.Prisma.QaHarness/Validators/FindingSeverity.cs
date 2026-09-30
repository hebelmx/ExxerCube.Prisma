// <copyright file="FindingSeverity.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Validators;

/// <summary>
/// Indicates how serious a <see cref="ValidationFinding"/> is.
/// The QA agent uses severity to decide whether to escalate or defer a finding.
/// </summary>
public enum FindingSeverity
{
    /// <summary>The finding represents a fundamental conformance failure that must be resolved before release.</summary>
    Critical,

    /// <summary>The finding indicates a significant deviation from specification that should be resolved.</summary>
    Major,

    /// <summary>The finding indicates a minor deviation or suboptimal behaviour that can be deferred.</summary>
    Minor,

    /// <summary>The finding is informational only and does not indicate non-conformance.</summary>
    Info,
}
