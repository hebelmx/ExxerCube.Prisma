// <copyright file="ExpedienteClassificationResult.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.ValueObjects;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents the result of classifying an Expediente into one of 5 CNBV requirement types (100-104)
/// and validating it against legal requirements (Articles 4, 17, 142 LIC, 34 LACP).
/// </summary>
/// <remarks>
/// This is distinct from file-level ClassificationResult (which determines document type).
/// ExpedienteClassificationResult operates on extracted Expediente data to determine:
/// 1. Requirement type (Información, Aseguramiento, Desbloqueo, Transferencia, SituaciónFondos)
/// 2. Authority type (CNBV, UIF, Juzgado, Hacienda, etc.)
/// 3. Required fields per operation type (Article 4)
/// 4. Legal rejection grounds (Article 17)
/// 5. Semantic analysis ("The 5 Situations")
/// </remarks>
public class ExpedienteClassificationResult
{
    /// <summary>
    /// Gets or sets the classified requirement type (100-104 or 999 for Unknown).
    /// </summary>
    public RequirementType RequirementType { get; set; } = RequirementType.Unknown;

    /// <summary>
    /// Gets or sets the confidence score for the classification (0.0-1.0).
    /// Based on keyword matching, pattern analysis, and field presence.
    /// </summary>
    public double ClassificationConfidence { get; set; }

    /// <summary>
    /// Gets or sets the classified authority type (CNBV, UIF, Juzgado, Hacienda, Other).
    /// Determines legal basis and response time requirements.
    /// </summary>
    public AuthorityKind AuthorityType { get; set; } = AuthorityKind.Unknown;

    /// <summary>
    /// Gets or sets the list of required field names for this requirement type per Article 4.
    /// Used to validate completeness before auto-processing.
    /// </summary>
    public List<string> RequiredFields { get; set; } = new();

    /// <summary>
    /// Gets or sets the Article 4 and Article 17 validation results.
    /// Includes missing required fields and legal rejection grounds.
    /// </summary>
    public ArticleValidationResult ArticleValidation { get; set; } = new();

    /// <summary>
    /// Gets or sets the semantic analysis result ("The 5 Situations").
    /// Determines which of the 5 requirement categories apply:
    /// - RequiereBloqueo (Aseguramiento)
    /// - RequiereDesbloqueo
    /// - RequiereDocumentacion (Información)
    /// - RequiereTransferencia
    /// - RequiereInformacionGeneral
    /// </summary>
    public SemanticAnalysis SemanticAnalysis { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of Article 17 rejection reasons (if any).
    /// Empty list indicates the requirement is legally valid and enforceable.
    /// Non-empty list indicates grounds for rejection per CNBV regulations.
    /// </summary>
    public List<RejectionReason> RejectionReasons { get; set; } = new();

    /// <summary>
    /// Gets the validation state for this classification result.
    /// </summary>
    public ValidationState Validation { get; } = new();
}
