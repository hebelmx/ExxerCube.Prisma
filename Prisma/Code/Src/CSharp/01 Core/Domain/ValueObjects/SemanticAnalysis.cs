namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Semantic analysis of legal directive - the "5 Situations".
/// Describes WHAT THE CASE REQUIRES (domain concern), not HOW it was classified (infrastructure).
/// </summary>
/// <remarks>
/// Source: DATA_MODEL.md Section 2.5
///
/// The 5 Situations:
/// 1. Requiere Bloqueo (Asset Freeze)
/// 2. Requiere Desbloqueo (Asset Unfreeze)
/// 3. Requiere Documentación (Document Request)
/// 4. Requiere Transferencia (Transfer Order)
/// 5. Requiere Información General (General Information Request)
///
/// This is OUTPUT of classification/extraction algorithms, but once computed,
/// it becomes a FACT about what the case requires. Therefore: DOMAIN.
///
/// All fields nullable until semantic analysis is performed.
/// </remarks>
public class SemanticAnalysis
{
    /// <summary>
    /// Indicates if case requires asset freeze (bloqueo/aseguramiento).
    /// </summary>
    public BloqueoRequirement? RequiereBloqueo { get; set; }

    /// <summary>
    /// Indicates if case requires asset unfreeze (desbloqueo).
    /// </summary>
    public DesbloqueoRequirement? RequiereDesbloqueo { get; set; }

    /// <summary>
    /// Indicates if case requires documentation submission.
    /// </summary>
    public DocumentacionRequirement? RequiereDocumentacion { get; set; }

    /// <summary>
    /// Indicates if case requires fund transfer.
    /// </summary>
    public TransferenciaRequirement? RequiereTransferencia { get; set; }

    /// <summary>
    /// Indicates if case requires general information.
    /// </summary>
    public InformacionGeneralRequirement? RequiereInformacionGeneral { get; set; }

    /// <summary>
    /// Validation state for semantic analysis.
    /// </summary>
    public ValidationState Validation { get; } = new();
}