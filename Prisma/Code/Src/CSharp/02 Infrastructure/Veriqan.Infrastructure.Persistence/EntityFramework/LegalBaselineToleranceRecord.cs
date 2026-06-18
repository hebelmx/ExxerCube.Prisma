namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;

/// <summary>
/// Persistence entity for a single legal-baseline tolerance specification.
/// All numeric columns are stored AES-256-CBC encrypted at rest via
/// <see cref="Crypto.AesEncryptedDecimalConverter"/>.
/// </summary>
/// <remarks>
/// <para>
/// This table is <b>read-only at runtime</b>. The production SQL login for the Veriqan
/// service account should be granted <c>db_datareader</c> only on this table.
/// SQL Server TDE should also be enabled on the database as an additional OPS-level
/// encryption layer (documented here; not enforced by code).
/// </para>
/// <para>
/// Values are seeded once by <c>LegalBaselineSeeder</c> and must match the specs in
/// <c>DefaultLegalToleranceProvider</c> exactly so behaviour is identical whether the
/// in-code or SQL-backed provider is active.
/// </para>
/// </remarks>
public sealed class LegalBaselineToleranceRecord
{
    /// <summary>
    /// Gets or sets the check identifier primary key (e.g. <c>"CL-10"</c>).
    /// </summary>
    public string CheckId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the legally-mandated default tolerance value (encrypted at rest).
    /// </summary>
    public decimal LegalDefault { get; set; }

    /// <summary>
    /// Gets or sets the minimum permitted override value (encrypted at rest).
    /// </summary>
    public decimal Min { get; set; }

    /// <summary>
    /// Gets or sets the maximum permitted override value (encrypted at rest).
    /// </summary>
    public decimal Max { get; set; }
}
