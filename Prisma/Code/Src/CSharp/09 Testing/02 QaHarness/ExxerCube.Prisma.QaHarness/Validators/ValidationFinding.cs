// <copyright file="ValidationFinding.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Validators;

/// <summary>
/// Describes a single deviation or observation found by an <c>IDomainValidator</c>.
/// Findings are raw observations — the QA agent assigns PASS/FAIL based on them.
/// </summary>
/// <param name="RuleId">
/// A stable identifier for the validation rule that triggered this finding
/// (e.g. <c>"SIRO-STRUCT-01"</c>).  Used for deduplication and trend analysis.
/// </param>
/// <param name="Severity">How critical this finding is (Critical, Major, Minor, or Info).</param>
/// <param name="Description">A human-readable description of what was found and why it matters.</param>
/// <param name="Observed">
/// The actual value or structure observed in the subject, or <see langword="null"/> when not applicable.
/// </param>
/// <param name="Expected">
/// The value or structure that was expected per specification, or <see langword="null"/> when not applicable.
/// </param>
public sealed record ValidationFinding(
    string RuleId,
    FindingSeverity Severity,
    string Description,
    string? Observed,
    string? Expected);
