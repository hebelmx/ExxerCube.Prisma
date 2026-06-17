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
}
