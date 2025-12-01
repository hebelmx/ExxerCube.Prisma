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

/// <summary>
/// Asset freeze (bloqueo/aseguramiento) requirement details.
/// </summary>
public class BloqueoRequirement
{
    /// <summary>
    /// Whether asset freeze is required.
    /// </summary>
    public bool EsRequerido { get; set; }

    /// <summary>
    /// Whether freeze is partial (specific amount) or total.
    /// </summary>
    public bool EsParcial { get; set; }

    /// <summary>
    /// Amount to freeze (if partial).
    /// </summary>
    public decimal? Monto { get; set; }

    /// <summary>
    /// Currency of amount.
    /// </summary>
    public string? Moneda { get; set; }

    /// <summary>
    /// Specific accounts to freeze (if specified).
    /// </summary>
    public List<string> CuentasEspecificas { get; set; } = new();

    /// <summary>
    /// Specific products to freeze (if specified).
    /// </summary>
    public List<string> ProductosEspecificos { get; set; } = new();
}

/// <summary>
/// Asset unfreeze (desbloqueo) requirement details.
/// </summary>
public class DesbloqueoRequirement
{
    /// <summary>
    /// Whether asset unfreeze is required.
    /// </summary>
    public bool EsRequerido { get; set; }

    /// <summary>
    /// Reference to original freeze case (expediente).
    /// </summary>
    public string? ExpedienteBloqueoOriginal { get; set; }
}

/// <summary>
/// Documentation submission requirement details.
/// </summary>
public class DocumentacionRequirement
{
    /// <summary>
    /// Whether documentation is required.
    /// </summary>
    public bool EsRequerido { get; set; }

    /// <summary>
    /// Types of documents requested.
    /// </summary>
    public List<DocumentoRequerido> TiposDocumento { get; set; } = new();
}

/// <summary>
/// Specific document requirement.
/// </summary>
public class DocumentoRequerido
{
    /// <summary>
    /// Type of document (e.g., "Estado de cuenta", "ID del cliente").
    /// </summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>
    /// Period start date (for statements, transactions, etc.).
    /// </summary>
    public DateTime? PeriodoInicio { get; set; }

    /// <summary>
    /// Period end date (for statements, transactions, etc.).
    /// </summary>
    public DateTime? PeriodoFin { get; set; }
}

/// <summary>
/// Fund transfer requirement details.
/// </summary>
public class TransferenciaRequirement
{
    /// <summary>
    /// Whether transfer is required.
    /// </summary>
    public bool EsRequerido { get; set; }

    /// <summary>
    /// Destination account for transfer (if specified).
    /// </summary>
    public string? CuentaDestino { get; set; }

    /// <summary>
    /// Amount to transfer (if specified).
    /// </summary>
    public decimal? Monto { get; set; }
}

/// <summary>
/// General information request requirement details.
/// </summary>
public class InformacionGeneralRequirement
{
    /// <summary>
    /// Whether general information is required.
    /// </summary>
    public bool EsRequerido { get; set; }

    /// <summary>
    /// Description of information requested.
    /// </summary>
    public string? InformacionSolicitada { get; set; }
}
