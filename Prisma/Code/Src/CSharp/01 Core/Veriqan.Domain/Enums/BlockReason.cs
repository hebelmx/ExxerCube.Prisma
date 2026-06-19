namespace ExxerCube.Prisma.Veriqan.Domain.Enums;

/// <summary>
/// Machine-readable reason codes that cause a verification job to be <c>Blocked</c>
/// before any checklist checks can run.
/// A blocked job carries one of these reasons so callers can route, surface,
/// or suppress the condition without parsing free-text error messages.
/// </summary>
public enum BlockReason
{
    /// <summary>
    /// The product token extracted from the statement could not be matched to any
    /// canonical product in the reference bundle (neither by <c>ProductId</c> nor any
    /// entry in <c>Aliases</c>).  No silent default is applied; the job is blocked until
    /// the correct product is identified.
    /// </summary>
    UnknownProduct = 1,

    /// <summary>
    /// The reference-data bundle returned by the reference-data provider port
    /// was invalid, unavailable, or could not be decoded (schema error, I/O failure, etc.).
    /// </summary>
    InvalidBundle = 2,

    /// <summary>
    /// Fewer fields were successfully extracted from the statement PDF than the configured
    /// <c>TenantProfile.MinExtractionCoverageCount</c> floor.  This indicates the PDF is
    /// encrypted, blank, or so layout-drifted that the extractor could not read it.
    /// The pipeline is blocked before section validation to prevent a spurious GREEN verdict
    /// caused by universal abstain (U2) when near-zero fields are available.
    /// </summary>
    InsufficientExtractionCoverage = 3,

    /// <summary>
    /// The total word count across all PDF pages is below the configured
    /// <c>TenantProfile.MinTextLayerWordCount</c> floor.  This indicates a scanned /
    /// image-only PDF whose text layer is absent or near-zero, so mandatory-section
    /// rules would all fail (false RED) rather than abstain.
    /// The pipeline is blocked before bind/validate to honour the abstain-safety rule:
    /// a scanned-but-compliant statement must never receive a RED verdict.
    /// </summary>
    InsufficientTextLayer = 4,
}
