using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Classification dictionary mapping Spanish legal phrases to compliance action kinds.
/// Uses fuzzy phrase matching to handle variations in legal language.
/// </summary>
/// <remarks>
/// Design Philosophy:
/// - Hardcoded for Phase 1 (dictionary-driven approach)
/// - Extensible for Phase 3 (AI-generated dictionaries)
/// - Phrase variations handled by ITextComparer fuzzy matching (≥85% similarity threshold)
///
/// Dictionary Sources:
/// - Historical oficios from SAT, FGR, UIF
/// - Legal terminology from Mexican financial law
/// - Audit findings (e.g., "aseguramiento de fondos" vs "aseguramiento de los fondos")
///
/// Future Enhancement (Phase 3): Generate dictionaries dynamically using AI from historical documents
/// </remarks>
internal static class ClassificationDictionary
{
    /// <summary>
    /// Similarity threshold for fuzzy phrase matching (85% = tolerates minor typos and variations).
    /// Based on audit findings: "aseguramiento de fondos" should match "aseguramiento de los fondos".
    /// </summary>
    public const double DefaultThreshold = 0.85;

    /// <summary>
    /// Dictionary mapping legal directive phrases to Block (asset freeze) actions.
    /// </summary>
    /// <remarks>
    /// Spanish Terms:
    /// - Aseguramiento: Preventive asset freeze (SAT, FGR)
    /// - Bloqueo: Account block (FGR, CNBV)
    /// - Congelamiento: Asset freeze (UIF)
    /// - Embargo: Judicial seizure (general)
    /// - Inmovilización: Immobilization of assets
    /// </remarks>
    public static readonly Dictionary<string, ComplianceActionKind> BlockPhrases = new()
    {
        // Primary terms (exact matches highly likely)
        ["aseguramiento de fondos"] = ComplianceActionKind.Block,
        ["aseguramiento de la cuenta"] = ComplianceActionKind.Block,
        ["aseguramiento de cuentas"] = ComplianceActionKind.Block,
        ["aseguramiento de recursos"] = ComplianceActionKind.Block,
        ["aseguramiento precautorio"] = ComplianceActionKind.Block,

        ["bloqueo de cuenta"] = ComplianceActionKind.Block,
        ["bloqueo de cuentas"] = ComplianceActionKind.Block,
        ["bloqueo de fondos"] = ComplianceActionKind.Block,
        ["bloqueo temporal"] = ComplianceActionKind.Block,
        ["bloqueo preventivo"] = ComplianceActionKind.Block,

        ["congelamiento de recursos"] = ComplianceActionKind.Block,
        ["congelamiento de fondos"] = ComplianceActionKind.Block,
        ["congelamiento de activos"] = ComplianceActionKind.Block,

        ["embargo de cuenta"] = ComplianceActionKind.Block,
        ["embargo de cuentas"] = ComplianceActionKind.Block,
        ["embargo precautorio"] = ComplianceActionKind.Block,

        ["inmovilización de recursos"] = ComplianceActionKind.Block,
        ["inmovilización de fondos"] = ComplianceActionKind.Block,

        ["retención de fondos"] = ComplianceActionKind.Block,
        ["retención de recursos"] = ComplianceActionKind.Block,

        // Verb forms (matches imperative legal language)
        ["asegurar fondos"] = ComplianceActionKind.Block,
        ["asegurar recursos"] = ComplianceActionKind.Block,
        ["bloquear cuenta"] = ComplianceActionKind.Block,
        ["bloquear cuentas"] = ComplianceActionKind.Block,
        ["congelar recursos"] = ComplianceActionKind.Block,
        ["embargar cuenta"] = ComplianceActionKind.Block,
        ["retener fondos"] = ComplianceActionKind.Block,
    };

    /// <summary>
    /// Dictionary mapping legal directive phrases to Unblock (asset unfreeze) actions.
    /// </summary>
    /// <remarks>
    /// Spanish Terms:
    /// - Desbloqueo: Account unblock
    /// - Desaseguramiento: Asset unfreeze (reverse of aseguramiento)
    /// - Liberación: Release/liberation of assets
    /// - Desembargo: Judicial release
    /// </remarks>
    public static readonly Dictionary<string, ComplianceActionKind> UnblockPhrases = new()
    {
        // Primary terms
        ["desbloqueo de cuenta"] = ComplianceActionKind.Unblock,
        ["desbloqueo de cuentas"] = ComplianceActionKind.Unblock,
        ["desbloqueo de fondos"] = ComplianceActionKind.Unblock,
        ["desbloqueo de recursos"] = ComplianceActionKind.Unblock,

        ["desaseguramiento de fondos"] = ComplianceActionKind.Unblock,
        ["desaseguramiento de cuenta"] = ComplianceActionKind.Unblock,
        ["desaseguramiento de recursos"] = ComplianceActionKind.Unblock,

        ["liberación de fondos"] = ComplianceActionKind.Unblock,
        ["liberación de recursos"] = ComplianceActionKind.Unblock,
        ["liberación de cuenta"] = ComplianceActionKind.Unblock,

        ["descongelamiento de recursos"] = ComplianceActionKind.Unblock,
        ["descongelamiento de fondos"] = ComplianceActionKind.Unblock,

        ["levantamiento de aseguramiento"] = ComplianceActionKind.Unblock,
        ["levantamiento del aseguramiento"] = ComplianceActionKind.Unblock,
        ["levantamiento de bloqueo"] = ComplianceActionKind.Unblock,
        ["levantamiento del bloqueo"] = ComplianceActionKind.Unblock,

        ["desembargo de cuenta"] = ComplianceActionKind.Unblock,
        ["desembargo de cuentas"] = ComplianceActionKind.Unblock,

        // Verb forms
        ["desbloquear cuenta"] = ComplianceActionKind.Unblock,
        ["desbloquear cuentas"] = ComplianceActionKind.Unblock,
        ["liberar fondos"] = ComplianceActionKind.Unblock,
        ["liberar recursos"] = ComplianceActionKind.Unblock,
        ["descongelar recursos"] = ComplianceActionKind.Unblock,
    };

    /// <summary>
    /// Dictionary mapping legal directive phrases to Document request actions.
    /// </summary>
    /// <remarks>
    /// Spanish Terms:
    /// - Documentación: General document requests
    /// - Expedición: Issuance of documents
    /// - Constancia: Certificate/proof documents
    /// - Certificación: Official certification
    /// </remarks>
    public static readonly Dictionary<string, ComplianceActionKind> DocumentPhrases = new()
    {
        // Primary terms
        ["solicitud de documentación"] = ComplianceActionKind.Document,
        ["solicitud de documentos"] = ComplianceActionKind.Document,
        ["solicito estados de cuenta"] = ComplianceActionKind.Document,
        ["estados de cuenta"] = ComplianceActionKind.Document,
        ["solicito información"] = ComplianceActionKind.Document,
        ["entrega de documentación"] = ComplianceActionKind.Document,
        ["entrega de documentos"] = ComplianceActionKind.Document,
        ["presentación de documentación"] = ComplianceActionKind.Document,
        ["presentación de documentos"] = ComplianceActionKind.Document,

        ["expedición de constancia"] = ComplianceActionKind.Document,
        ["expedición de certificado"] = ComplianceActionKind.Document,
        ["expedición de comprobante"] = ComplianceActionKind.Document,

        ["certificación de saldos"] = ComplianceActionKind.Document,
        ["certificación de movimientos"] = ComplianceActionKind.Document,
        ["constancia de saldos"] = ComplianceActionKind.Document,

        ["documentación correspondiente"] = ComplianceActionKind.Document,
        ["documentación requerida"] = ComplianceActionKind.Document,
        ["documentación solicitada"] = ComplianceActionKind.Document,

        // Verb forms
        ["expedir constancia"] = ComplianceActionKind.Document,
        ["expedir certificado"] = ComplianceActionKind.Document,
        ["entregar documentos"] = ComplianceActionKind.Document,
        ["presentar documentación"] = ComplianceActionKind.Document,
        ["proporcionar documentación"] = ComplianceActionKind.Document,
    };

    /// <summary>
    /// Dictionary mapping legal directive phrases to Transfer actions.
    /// </summary>
    /// <remarks>
    /// Spanish Terms:
    /// - Transferencia: Bank transfer
    /// - Giro: Money order/transfer
    /// - Traspaso: Transfer between accounts
    /// - Movimiento: Account movement
    /// </remarks>
    public static readonly Dictionary<string, ComplianceActionKind> TransferPhrases = new()
    {
        // Primary terms
        ["transferencia de fondos"] = ComplianceActionKind.Transfer,
        ["transferencia de recursos"] = ComplianceActionKind.Transfer,
        ["transferencia bancaria"] = ComplianceActionKind.Transfer,
        ["transferencia electrónica"] = ComplianceActionKind.Transfer,

        ["giro de fondos"] = ComplianceActionKind.Transfer,
        ["giro bancario"] = ComplianceActionKind.Transfer,

        ["traspaso de fondos"] = ComplianceActionKind.Transfer,
        ["traspaso de recursos"] = ComplianceActionKind.Transfer,

        ["envío de fondos"] = ComplianceActionKind.Transfer,
        ["remisión de fondos"] = ComplianceActionKind.Transfer,

        // Verb forms
        ["transferir fondos"] = ComplianceActionKind.Transfer,
        ["transferir recursos"] = ComplianceActionKind.Transfer,
        ["enviar fondos"] = ComplianceActionKind.Transfer,
        ["girar fondos"] = ComplianceActionKind.Transfer,
    };

    /// <summary>
    /// Dictionary mapping legal directive phrases to Information request actions.
    /// </summary>
    /// <remarks>
    /// Spanish Terms:
    /// - Información: General information requests
    /// - Reporte: Report submission
    /// - Notificación: Notification/notice
    /// - Comunicación: Communication/correspondence
    /// </remarks>
    public static readonly Dictionary<string, ComplianceActionKind> InformationPhrases = new()
    {
        // Primary terms
        ["solicitud de información"] = ComplianceActionKind.Information,
        ["petición de información"] = ComplianceActionKind.Information,
        ["requerimiento de información"] = ComplianceActionKind.Information,
        ["información solicitada"] = ComplianceActionKind.Information,
        ["información requerida"] = ComplianceActionKind.Information,

        ["reporte de movimientos"] = ComplianceActionKind.Information,
        ["reporte de operaciones"] = ComplianceActionKind.Information,
        ["reporte de saldos"] = ComplianceActionKind.Information,

        ["notificación de operaciones"] = ComplianceActionKind.Information,
        ["notificación de movimientos"] = ComplianceActionKind.Information,

        ["comunicación de datos"] = ComplianceActionKind.Information,
        ["comunicación de información"] = ComplianceActionKind.Information,

        // Verb forms
        ["informar sobre"] = ComplianceActionKind.Information,
        ["reportar movimientos"] = ComplianceActionKind.Information,
        ["reportar operaciones"] = ComplianceActionKind.Information,
        ["comunicar información"] = ComplianceActionKind.Information,
        ["proporcionar información"] = ComplianceActionKind.Information,
    };

    /// <summary>
    /// Gets all phrase dictionaries for iteration.
    /// Ordered by precedence: Unblock > Block/Transfer/Document > Information.
    /// </summary>
    /// <remarks>
    /// Precedence rationale:
    /// 1. Unblock has highest priority (e.g., "desbloquear el aseguramiento" is Unblock, not Block)
    /// 2. Block, Transfer, Document are specific operations (equal precedence)
    /// 3. Information is catch-all for general requests (lowest precedence)
    /// </remarks>
    public static IEnumerable<(ComplianceActionKind ActionKind, Dictionary<string, ComplianceActionKind> Phrases)> GetAllDictionaries()
    {
        // PRIORITY 1: Unblock (highest - prevents "desbloquear el aseguramiento" being classified as Block)
        yield return (ComplianceActionKind.Unblock, UnblockPhrases);

        // PRIORITY 2: Specific operations (Block, Transfer, Document - no order preference)
        yield return (ComplianceActionKind.Block, BlockPhrases);
        yield return (ComplianceActionKind.Transfer, TransferPhrases);
        yield return (ComplianceActionKind.Document, DocumentPhrases);

        // PRIORITY 3: Information (catch-all for general requests)
        yield return (ComplianceActionKind.Information, InformationPhrases);
    }
}
