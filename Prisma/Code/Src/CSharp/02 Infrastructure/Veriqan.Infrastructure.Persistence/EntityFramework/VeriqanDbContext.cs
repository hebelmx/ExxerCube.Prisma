using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Crypto;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;

/// <summary>
/// Entity Framework Core database context for the Veriqan compliance-verification subsystem.
/// All tables are created in the <c>veriqan</c> SQL schema, fully isolated from
/// <c>PrismaDbContext</c> which owns the <c>dbo</c> schema.
/// The EF migrations history table is also placed in the <c>veriqan</c> schema
/// (<c>veriqan.__EFMigrationsHistory</c>) so the two contexts never collide.
/// </summary>
public sealed class VeriqanDbContext : DbContext
{
    private readonly AesEncryptedDecimalConverter? _decimalConverter;

    /// <summary>
    /// Initializes a new instance of <see cref="VeriqanDbContext"/> for DI / runtime use.
    /// </summary>
    /// <param name="options">Database context options supplied by DI or the design-time factory.</param>
    /// <param name="decimalConverter">
    /// AES value converter for encrypted decimal columns. Pass <see langword="null"/> for design-time
    /// migrations (no key available); non-null for runtime use.
    /// </param>
    public VeriqanDbContext(
        DbContextOptions<VeriqanDbContext> options,
        AesEncryptedDecimalConverter? decimalConverter = null)
        : base(options)
    {
        _decimalConverter = decimalConverter;
    }

    /// <summary>Gets or sets the verification jobs aggregate-root set.</summary>
    public DbSet<VerificationJob> VerificationJobs { get; set; } = null!;

    /// <summary>Gets or sets the compliance findings entity set.</summary>
    public DbSet<Finding> Findings { get; set; } = null!;

    /// <summary>Gets or sets the rollup job-verdict entity set.</summary>
    public DbSet<JobVerdict> JobVerdicts { get; set; } = null!;

    /// <summary>
    /// Gets or sets the append-only human-reviewer disposition audit set (AR-9, FR-18).
    /// Rows in this set are never updated or deleted; use <c>AddAsync</c> / <c>AddRangeAsync</c>
    /// only.
    /// </summary>
    public DbSet<Disposition> Dispositions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the read-only legal-baseline tolerance records.
    /// All numeric columns are AES-encrypted at rest.
    /// </summary>
    public DbSet<LegalBaselineToleranceRecord> LegalBaselineTolerances { get; set; } = null!;

    /// <summary>Gets or sets the JSON snapshots of completed <c>VerificationOutcome</c> objects.</summary>
    public DbSet<VerificationOutcomeSnapshotEntity> OutcomeSnapshots { get; set; } = null!;

    /// <summary>Gets or sets the append-only reprocess audit log.</summary>
    public DbSet<ReprocessAuditLogEntity> ReprocessAuditLog { get; set; } = null!;

    /// <summary>
    /// Gets or sets the durable batch-processing dead-letter log (VERIQAN-E3-S3).
    /// Unlike <see cref="ReprocessAuditLog"/>, rows here are mutable (RetryCount / LastRetryAt /
    /// ResolvedAt triage lifecycle) — see <see cref="Configurations.BatchExceptionLogConfiguration"/>.
    /// </summary>
    public DbSet<BatchExceptionLogEntity> BatchExceptionLog { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Place ALL tables in the veriqan schema — this is the primary isolation boundary.
        modelBuilder.HasDefaultSchema("veriqan");

        modelBuilder.ApplyConfiguration(new VerificationJobConfiguration());
        modelBuilder.ApplyConfiguration(new FindingConfiguration());
        modelBuilder.ApplyConfiguration(new JobVerdictConfiguration());
        modelBuilder.ApplyConfiguration(new DispositionConfiguration());

        // Legal-baseline table: converter may be null at design time (migrations have no key).
        // When null, a placeholder no-op converter is used so migrations can scaffold the schema;
        // actual encryption only occurs at runtime when a real key is provided.
        var converter = _decimalConverter ?? CreateNoOpConverter();
        modelBuilder.ApplyConfiguration(new LegalBaselineToleranceConfiguration(converter));

        modelBuilder.ApplyConfiguration(new VerificationOutcomeSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new ReprocessAuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new BatchExceptionLogConfiguration());
    }

    /// <summary>
    /// Creates a no-op converter used at design time when no AES key is available.
    /// It serialises the decimal as a plain string — migrations only need the CLR→column
    /// type mapping, not the actual encryption logic.
    /// </summary>
    private static AesEncryptedDecimalConverter CreateNoOpConverter()
    {
        // 32 zero bytes — a valid AES-256 key length; only used at design time for
        // migration scaffolding, never for actual data reads/writes.
        return new AesEncryptedDecimalConverter(new byte[32]);
    }
}
