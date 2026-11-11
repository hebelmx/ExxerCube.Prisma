// <copyright file="IDomainValidator.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Validators;

/// <summary>
/// Validates a domain artefact of type <typeparamref name="TSubject"/> against a set of
/// product-specific rules, returning raw findings without rendering a PASS/FAIL verdict.
/// </summary>
/// <typeparam name="TSubject">
/// The type of object being validated (e.g. a file path <see cref="string"/>, an XML document,
/// a byte array, or a domain DTO).
/// </typeparam>
/// <remarks>
/// Validators never throw for business-logic failures and never assign PASS/FAIL — that
/// responsibility belongs to the QA agent that consumes the <see cref="ValidationResult"/>.
/// Implementations are registered with
/// <c>AddQaHarness().AddValidator&lt;TSubject, TValidator&gt;()</c> and resolved
/// via <c>IServiceProvider.GetServices&lt;IDomainValidator&lt;TSubject&gt;&gt;()</c>.
/// </remarks>
public interface IDomainValidator<TSubject>
{
    /// <summary>
    /// Gets the stable identifier for this validator used in reports and traceability entries
    /// (e.g. <c>"SIRO-STRUCT"</c>).
    /// </summary>
    string ValidatorId { get; }

    /// <summary>
    /// Gets a human-readable description of what this validator checks.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Validates <paramref name="subject"/> against all applicable rules and returns a
    /// <see cref="ValidationResult"/> containing every finding observed.
    /// </summary>
    /// <param name="subject">The artefact to validate.  Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel long-running validation steps.</param>
    /// <returns>
    /// A <see cref="ValidationResult"/> with <see cref="ValidationResult.IsConformant"/> set
    /// to <see langword="true"/> when no Critical or Major findings were observed.
    /// This method does not return <c>Result&lt;T&gt;</c> by design — validation completion
    /// is always meaningful regardless of conformance.
    /// </returns>
    Task<ValidationResult> ValidateAsync(
        TSubject subject,
        CancellationToken cancellationToken = default);
}
