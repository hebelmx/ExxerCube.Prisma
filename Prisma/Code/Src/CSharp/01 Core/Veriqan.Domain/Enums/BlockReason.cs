namespace ExxerCube.Prisma.Veriqan.Domain.Enums;

/// <summary>
/// Machine-readable reason codes carried by non-verdict outcomes before any checklist
/// checks can run.  Each reason maps to a <see cref="VerdictSignal"/>:
/// <list type="bullet">
///   <item><b><see cref="VerdictSignal.ExtractionGap"/></b> — permanent system/engineering
///     capability gap.  The 4 currently-wired reasons and the future taxonomy members
///     <see cref="AmbiguousDocumentScope"/>, <see cref="MissingMandatoryAnchorFields"/>, and
///     <see cref="RefDataVersionMismatch"/> route here.</item>
///   <item><b><see cref="VerdictSignal.Blocked"/></b> — reserved for genuine document defects
///     requiring human callback (e.g. encrypted, corrupt, tampered documents).  The taxonomy
///     members <see cref="EncryptedDocument"/>, <see cref="CorruptDocument"/>,
///     <see cref="InvalidDocumentFormat"/>, <see cref="InvalidDocumentSize"/>,
///     <see cref="TamperedDocument"/>, and <see cref="UnsupportedLanguage"/> are defined here
///     but have NO wired detection yet (Story 4.2 deferred).
///   </item>
/// </list>
/// A job carrying one of these reasons allows callers to route, surface,
/// or suppress the condition without parsing free-text error messages.
/// </summary>
public enum BlockReason
{
    // ------------------------------------------------------------------
    // Currently-wired reasons — all map to VerdictSignal.ExtractionGap
    // ------------------------------------------------------------------

    /// <summary>
    /// The product token extracted from the statement could not be matched to any
    /// canonical product in the reference bundle (neither by <c>ProductId</c> nor any
    /// entry in <c>Aliases</c>).  No silent default is applied; the job is blocked until
    /// the correct product is identified.
    /// <para><b>Routes to:</b> <see cref="VerdictSignal.ExtractionGap"/> — the system cannot
    /// resolve the product; resubmitting unchanged bytes would produce the same result until
    /// the bundle/alias catalogue is updated.</para>
    /// </summary>
    UnknownProduct = 1,

    /// <summary>
    /// The reference-data bundle returned by the reference-data provider port
    /// was invalid, unavailable, or could not be decoded (schema error, I/O failure, etc.).
    /// <para><b>Routes to:</b> <see cref="VerdictSignal.ExtractionGap"/> — the system cannot
    /// load the required reference data; a bundle update or config fix is needed.</para>
    /// </summary>
    InvalidBundle = 2,

    /// <summary>
    /// Fewer fields were successfully extracted from the statement PDF than the configured
    /// <c>TenantProfile.MinExtractionCoverageCount</c> floor.  This indicates the PDF is
    /// encrypted, blank, or so layout-drifted that the extractor could not read it.
    /// The pipeline is blocked before section validation to prevent a spurious GREEN verdict
    /// caused by universal abstain (U2) when near-zero fields are available.
    /// <para><b>Routes to:</b> <see cref="VerdictSignal.ExtractionGap"/> — the extractor
    /// could not meet the coverage floor; an extractor improvement or bundle update is needed.</para>
    /// </summary>
    InsufficientExtractionCoverage = 3,

    /// <summary>
    /// The total word count across all PDF pages is below the configured
    /// <c>TenantProfile.MinTextLayerWordCount</c> floor.  This indicates a scanned /
    /// image-only PDF whose text layer is absent or near-zero, so mandatory-section
    /// rules would all fail (false RED) rather than abstain.
    /// The pipeline is blocked before bind/validate to honour the abstain-safety rule:
    /// a scanned-but-compliant statement must never receive a RED verdict.
    /// <para><b>Routes to:</b> <see cref="VerdictSignal.ExtractionGap"/> — the system cannot
    /// process image-only PDFs with the current OCR/text-layer capability.</para>
    /// </summary>
    InsufficientTextLayer = 4,

    // ------------------------------------------------------------------
    // Future taxonomy — document defects → VerdictSignal.Blocked (reserved)
    // NO wired detection exists for any of these after Story 4.2.
    // ------------------------------------------------------------------

    /// <summary>
    /// The PDF is password-protected or encrypted so the text layer cannot be read.
    /// <para><b>Routes to (future):</b> <see cref="VerdictSignal.Blocked"/> — resubmitting
    /// the same bytes without decryption would always fail; human action is required to provide
    /// the unencrypted document.</para>
    /// <para><b>Detection:</b> not wired — reserved for a future implementation pass.</para>
    /// </summary>
    EncryptedDocument = 5,

    /// <summary>
    /// The PDF structure is corrupt or cannot be parsed by the extraction layer.
    /// <para><b>Routes to (future):</b> <see cref="VerdictSignal.Blocked"/> — the same bytes
    /// cannot be processed without human repair or replacement of the document.</para>
    /// <para><b>Detection:</b> not wired — reserved for a future implementation pass.</para>
    /// </summary>
    CorruptDocument = 6,

    /// <summary>
    /// The uploaded file is not a valid PDF (wrong MIME type, truncated, or non-PDF binary).
    /// <para><b>Routes to (future):</b> <see cref="VerdictSignal.Blocked"/> — the same bytes
    /// cannot be processed; the submitter must provide a valid PDF.</para>
    /// <para><b>Detection:</b> not wired — reserved for a future implementation pass.</para>
    /// </summary>
    InvalidDocumentFormat = 7,

    /// <summary>
    /// The document size (bytes or page count) is outside the permitted bounds configured
    /// for the tenant (too small to be a real statement, or too large for the processing tier).
    /// <para><b>Routes to (future):</b> <see cref="VerdictSignal.Blocked"/> — the same bytes
    /// will always violate the size constraint; human triage is required.</para>
    /// <para><b>Detection:</b> not wired — reserved for a future implementation pass.</para>
    /// </summary>
    InvalidDocumentSize = 8,

    /// <summary>
    /// The document contains statements for multiple accounts or periods in a single file,
    /// making unambiguous single-statement compliance evaluation impossible.
    /// <para><b>Routes to:</b> <see cref="VerdictSignal.ExtractionGap"/> — the system cannot
    /// isolate a single statement scope; a bundle/rule update (multi-statement guard, chunk B1b)
    /// is needed before resubmission would succeed.</para>
    /// <para><b>Detection:</b> not wired — reserved for chunk B1b (multi-statement guard).</para>
    /// </summary>
    AmbiguousDocumentScope = 9,

    /// <summary>
    /// The PDF has been digitally tampered with after issuance (e.g. signature validation
    /// failed, page-level hash mismatch).
    /// <para><b>Routes to (future):</b> <see cref="VerdictSignal.Blocked"/> — the tampered
    /// bytes cannot be trusted for compliance evaluation; human escalation is required.</para>
    /// <para><b>Detection:</b> not wired — reserved for a future implementation pass.</para>
    /// </summary>
    TamperedDocument = 10,

    /// <summary>
    /// The statement language is not supported by the current extraction and rule engine.
    /// <para><b>Routes to (future):</b> <see cref="VerdictSignal.Blocked"/> — the same bytes
    /// cannot be evaluated without adding language support to the system.</para>
    /// <para><b>Detection:</b> not wired — reserved for a future implementation pass.</para>
    /// </summary>
    UnsupportedLanguage = 11,

    /// <summary>
    /// One or more mandatory anchor fields (e.g. account number, period header) required
    /// for reliable rule evaluation are absent or unresolvable after extraction.
    /// <para><b>Routes to:</b> <see cref="VerdictSignal.ExtractionGap"/> — the extractor
    /// could not locate required structural anchors; an extractor or bundle update is needed.</para>
    /// <para><b>Detection:</b> not wired — reserved for a future implementation pass.</para>
    /// </summary>
    MissingMandatoryAnchorFields = 12,

    /// <summary>
    /// The reference-data bundle version does not match the schema version expected by the
    /// current rule engine, making evaluation unsafe.
    /// <para><b>Routes to:</b> <see cref="VerdictSignal.ExtractionGap"/> — the system cannot
    /// evaluate the statement until the reference data is updated to a compatible version.</para>
    /// <para><b>Detection:</b> not wired — reserved for a future implementation pass.</para>
    /// </summary>
    RefDataVersionMismatch = 13,
}
