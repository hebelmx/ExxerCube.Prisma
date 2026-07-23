namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Adapters;

/// <summary>
/// Configuration options for <see cref="CsvReferenceDataAdapter"/>.
/// </summary>
public sealed class CsvReferenceDataOptions
{
    /// <summary>
    /// Root directory under which institution-specific sub-directories contain the CSV data files.
    /// Each sub-directory name should match the institution name with spaces replaced by underscores.
    /// </summary>
    /// <example><c>C:\ReferenceData\csv\</c></example>
    public string RootDirectory { get; set; } = string.Empty;

    /// <summary>
    /// HMAC key used to verify each institution bundle's <c>bundle-manifest.sha256</c> /
    /// <c>bundle-manifest.hmac</c> signature at load time (see
    /// <see cref="ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Integrity.BundleIntegrityVerifier"/>).
    /// Default is empty, which
    /// <strong>disables</strong> integrity verification for backwards compatibility with
    /// existing unsigned bundles (a warning is logged once per adapter instance in that case).
    /// A whitespace-only value is treated identically to empty — also disabled, also warned.
    /// Configure via <c>Veriqan:CsvReferenceData:BundleHmacKey</c> (or the corresponding
    /// <c>Veriqan__CsvReferenceData__BundleHmacKey</c> environment variable) in production —
    /// never commit a real key to source control.
    /// </summary>
    /// <remarks>
    /// This value is key material: never log it, include it in exception messages, or expose
    /// it via <see cref="object.ToString"/> — this type intentionally does not override
    /// <c>ToString</c>, so the default reflection-free member name is printed instead.
    /// </remarks>
    public string BundleHmacKey { get; set; } = string.Empty;
}
