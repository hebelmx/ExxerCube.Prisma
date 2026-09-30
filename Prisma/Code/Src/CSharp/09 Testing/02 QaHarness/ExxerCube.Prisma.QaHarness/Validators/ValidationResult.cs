// <copyright file="ValidationResult.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Validators;

/// <summary>
/// The outcome of a single <c>IDomainValidator</c> run against a subject.
/// Carries all observations collected during validation; the QA agent
/// interprets <see cref="IsConformant"/> and <see cref="Findings"/> to render a verdict.
/// </summary>
/// <param name="ValidatorId">The identifier of the validator that produced this result (matches <c>IDomainValidator.ValidatorId</c>).</param>
/// <param name="IsConformant">
/// <see langword="true"/> when all rules passed with no <see cref="FindingSeverity.Critical"/>
/// or <see cref="FindingSeverity.Major"/> findings; <see langword="false"/> otherwise.
/// The validator sets this field — the QA agent may override its interpretation.
/// </param>
/// <param name="Findings">
/// All observations recorded during the validation run, including
/// <see cref="FindingSeverity.Info"/> items.  May be empty when the subject fully conforms.
/// </param>
public sealed record ValidationResult(
    string ValidatorId,
    bool IsConformant,
    IReadOnlyList<ValidationFinding> Findings);
