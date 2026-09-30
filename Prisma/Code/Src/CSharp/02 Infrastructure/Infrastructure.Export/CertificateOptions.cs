namespace ExxerCube.Prisma.Infrastructure.Export;

/// <summary>
/// Configuration options for digital certificate management for PDF signing.
/// </summary>
public class CertificateOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Certificates";

    /// <summary>
    /// Gets or sets the certificate source type (AzureKeyVault, WindowsStore, File).
    /// </summary>
    public string Source { get; set; } = "AzureKeyVault";

    /// <summary>
    /// Gets or sets the Azure Key Vault URL (required if Source is AzureKeyVault).
    /// </summary>
    public string? KeyVaultUrl { get; set; }

    /// <summary>
    /// Gets or sets the certificate name in Azure Key Vault or Windows Certificate Store.
    /// </summary>
    public string? CertificateName { get; set; }

    /// <summary>
    /// Gets or sets the certificate thumbprint (required if Source is WindowsStore).
    /// </summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to fallback to file-based certificate if Key Vault is unavailable.
    /// </summary>
    public bool FallbackToFile { get; set; } = true;

    /// <summary>
    /// Gets or sets the file path to the certificate file (PFX/P12 format) if using file-based certificate.
    /// </summary>
    public string? FileCertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the certificate password if the certificate file is password-protected.
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Gets or sets the file path to the certificate file (PFX/P12 format) - alias for FileCertificatePath for test compatibility.
    /// </summary>
    public string? FilePath
    {
        get => FileCertificatePath;
        set => FileCertificatePath = value;
    }

    /// <summary>
    /// Gets or sets the certificate password - alias for CertificatePassword for test compatibility.
    /// </summary>
    public string? Password
    {
        get => CertificatePassword;
        set => CertificatePassword = value;
    }
}

