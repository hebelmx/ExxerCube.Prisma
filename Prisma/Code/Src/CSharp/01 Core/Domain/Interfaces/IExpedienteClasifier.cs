// <copyright file="IExpedienteClasifier.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Interfaces;

using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Service interface for classifying Expedientes into CNBV requirement types (100-104)
/// and validating against legal requirements (Articles 4, 17, 142 LIC, 34 LACP).
/// </summary>
/// <remarks>
/// <para><strong>Purpose:</strong></para>
/// <para>
/// After data fusion, the classifier determines:
/// 1. Requirement type (100-104): Información, Aseguramiento, Desbloqueo, Transferencia, SituaciónFondos
/// 2. Authority type: CNBV, UIF, Juzgado, Hacienda, Other
/// 3. Required fields per operation type (Article 4 validation)
/// 4. Legal rejection grounds (Article 17 validation)
/// 5. Semantic analysis ("The 5 Situations")
/// </para>
/// <para><strong>Legal Basis:</strong></para>
/// <list type="bullet">
/// <item><term>Article 4</term><description>Specifies required fields per operation type (100-104)</description></item>
/// <item><term>Article 17</term><description>Specifies 6 grounds for legal rejection</description></item>
/// <item><term>Article 142 LIC</term><description>Information requests from authorities</description></item>
/// <item><term>Article 34 LACP</term><description>Asset freezing and seizure procedures</description></item>
/// <item><term>R29 A-2911</term><description>Monthly reporting with 42 mandatory fields</description></item>
/// </list>
/// <para><strong>Classification Methodology:</strong></para>
/// <list type="number">
/// <item>Keyword analysis: Search for "asegurar", "bloquear", "desbloquear", "transferir", "información"</item>
/// <item>Field presence: Check for operation-specific fields (e.g., MontoSolicitado for Bloqueo)</item>
/// <item>Pattern matching: CLABE for Transferencia, AntecedentesDocumentales for Desbloqueo</item>
/// <item>Confidence scoring: Based on keyword matches + field presence + pattern validation</item>
/// <item>Authority classification: Extract from AutoridadNombre field, match against catalogs</item>
/// </list>
/// <para><strong>The 5 Situations (Semantic Analysis):</strong></para>
/// <list type="bullet">
/// <item>RequiereBloqueo - Account freezing with amount and account details</item>
/// <item>RequiereDesbloqueo - Release of frozen funds with reference to original blocking</item>
/// <item>RequiereDocumentacion - Information request with document types and date ranges</item>
/// <item>RequiereTransferencia - Electronic transfer to government account (SPEI/CLABE)</item>
/// <item>RequiereInformacionGeneral - General information request without specific document types</item>
/// </list>
/// </remarks>
public interface IExpedienteClasifier
{
    /// <summary>
    /// Classifies an Expediente into one of 5 requirement types (100-104) and validates
    /// against CNBV legal requirements (Articles 4, 17, 142 LIC, 34 LACP).
    /// </summary>
    /// <param name="expediente">The Expediente to classify (typically after data fusion).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// ExpedienteClassificationResult containing:
    /// - RequirementType (100-104 or 999 for Unknown)
    /// - ClassificationConfidence (0.0-1.0)
    /// - AuthorityType (CNBV, UIF, Juzgado, Hacienda, Other)
    /// - RequiredFields list per Article 4
    /// - ArticleValidationResult (Article 4 compliance, Article 17 rejection grounds)
    /// - SemanticAnalysis ("The 5 Situations")
    /// - RejectionReasons list (empty if requirement is valid)
    /// </returns>
    /// <remarks>
    /// Classification confidence thresholds:
    /// - >0.90: High confidence (clear keyword matches + all required fields present)
    /// - 0.70-0.90: Moderate confidence (partial keyword matches or some fields missing)
    /// - &lt;0.70: Low confidence (ambiguous or Unknown type, requires manual classification)
    ///
    /// If classification confidence &lt; 0.70 OR RequirementType = Unknown, the Expediente
    /// should be flagged for manual review.
    /// </remarks>
    Task<Result<ExpedienteClassificationResult>> ClassifyAsync(
        Expediente expediente,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates an Expediente against Article 4 required fields for a specific requirement type.
    /// </summary>
    /// <param name="expediente">The Expediente to validate.</param>
    /// <param name="requirementType">The requirement type (100-104) to validate against.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// ArticleValidationResult containing:
    /// - PassesArticle4 (true if all required fields present)
    /// - MissingRequiredFields list (field names that are null or invalid)
    /// - RejectionReasons list (from Article 17 validation)
    /// - IsRejectable (true if any Article 17 grounds apply)
    /// - ValidationNotes (detailed explanation of failures)
    /// </returns>
    /// <remarks>
    /// <para><strong>Article 4 Required Fields by Operation Type:</strong></para>
    /// <list type="table">
    /// <item>
    /// <term>100 - Información</term>
    /// <description>NumeroExpediente, NumeroOficio, FechaSolicitud, AutoridadNombre, RFC, TipoDocumentoSolicitado</description>
    /// </item>
    /// <item>
    /// <term>101 - Aseguramiento</term>
    /// <description>+ MontoSolicitado, NumeroCuenta, Instrucciones (forma de disposición)</description>
    /// </item>
    /// <item>
    /// <term>102 - Desbloqueo</term>
    /// <description>+ AntecedentesDocumentales, FolioSiaraOriginal, NumeroOficioOriginal</description>
    /// </item>
    /// <item>
    /// <term>103 - Transferencia</term>
    /// <description>+ NumeroCuentaOrigen, NumeroCuentaDestino (CLABE), EntidadDestino, MontoTransferencia</description>
    /// </item>
    /// <item>
    /// <term>104 - SituaciónFondos</term>
    /// <description>+ NumeroCuenta, MontoSolicitado, EntidadDestino</description>
    /// </item>
    /// </list>
    /// </remarks>
    Task<Result<ArticleValidationResult>> ValidateArticle4Async(
        Expediente expediente,
        RequirementType requirementType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks an Expediente against Article 17 grounds for legal rejection.
    /// Banks may reject requirements that fail legal formalities.
    /// </summary>
    /// <param name="expediente">The Expediente to check for rejection grounds.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// List of RejectionReason enums indicating which Article 17 grounds apply.
    /// Empty list indicates the requirement is legally valid and enforceable.
    /// Non-empty list indicates grounds for rejection (bank must notify CNBV within 24 hours via SIARA).
    /// </returns>
    /// <remarks>
    /// <para><strong>Article 17 Rejection Grounds:</strong></para>
    /// <list type="bullet">
    /// <item>I. No legal authority citation (missing FundamentoLegal)</item>
    /// <item>II. Missing or incomplete authority signature</item>
    /// <item>III. Lack of specificity (e.g., "all accounts" without date range)</item>
    /// <item>IV. Request exceeds authority's jurisdiction</item>
    /// <item>V. Missing required data per Article 4</item>
    /// <item>VI. Technical impossibility (e.g., account doesn't exist, request predates records)</item>
    /// </list>
    /// </remarks>
    Task<Result<List<RejectionReason>>> CheckArticle17RejectionAsync(
        Expediente expediente,
        CancellationToken cancellationToken);

    /// <summary>
    /// Performs semantic analysis to determine "The 5 Situations" from the Expediente's Instrucciones
    /// and Antecedentes fields.
    /// </summary>
    /// <param name="expediente">The Expediente to analyze.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// SemanticAnalysis value object with one or more of the 5 requirement categories populated:
    /// - RequiereBloqueo: Account freezing requirements (amount, accounts, products)
    /// - RequiereDesbloqueo: Unblocking requirements (reference to original blocking)
    /// - RequiereDocumentacion: Documentation requirements (types, date ranges)
    /// - RequiereTransferencia: Transfer requirements (destination account, amount)
    /// - RequiereInformacionGeneral: General information request
    /// </returns>
    /// <remarks>
    /// <para>
    /// Semantic analysis uses NLP techniques to extract structured requirements from unstructured text:
    /// - Keyword extraction: "bloquear", "desbloquear", "transferir", "estados de cuenta"
    /// - Amount parsing: "50,000 MXN", "$100,000 pesos"
    /// - Date parsing: "del 01/01/2024 al 31/12/2024"
    /// - Account number extraction: "cuenta 1234567890"
    /// - CLABE validation: 18-digit bank account format
    /// </para>
    /// <para>
    /// Multiple situations may apply simultaneously (e.g., Bloqueo + Documentación).
    /// Each situation has an EsRequerido flag indicating whether it applies to this Expediente.
    /// </para>
    /// </remarks>
    Task<Result<SemanticAnalysis>> AnalyzeSemanticRequirementsAsync(
        Expediente expediente,
        CancellationToken cancellationToken);
}
