using ExxerCube.Prisma.Infrastructure.Database;

namespace ExxerCube.Prisma.Tests.System.Storage;

/// <summary>
/// Real-SQL regression for the <c>DropReviewCaseFileMetadataFk</c> fix: the 3-process worker pipeline
/// persists <see cref="ReviewCase"/> rows keyed by <c>FileId</c> BEFORE any <see cref="FileMetadata"/>
/// row exists. The previous <c>FK_ReviewCases_FileMetadata_FileId</c> made every such INSERT fail on a
/// real SQL Server; because review-case persistence is fail-open, the failure was swallowed and the
/// manual-review dashboard silently showed nothing from the live pipeline.
/// </summary>
/// <remarks>
/// This MUST run against Testcontainers SQL — the EF Core in-memory provider used by
/// <c>ManualReviewerServiceIsCompleteTests</c> does not enforce foreign keys, so it could never have
/// caught this. Mirrors the audit-side proof in <see cref="ProcessAuditIntegrationTests"/>
/// ("No FileMetadata seed — FK is dropped").
/// </remarks>
public sealed class ReviewCaseFkDropIntegrationTests : IDisposable
{
    private readonly DbContextOptions<PrismaDbContext> _dbOptions;
    private readonly ITestOutputHelper _output;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ReviewCaseFkDropIntegrationTests(SqlServerContainerFixture fixture, ITestOutputHelper output)
    {
        _output = output;
        fixture.EnsureAvailable();

        var connectionString = fixture
            .CreateIsolatedDatabaseAsync(nameof(ReviewCaseFkDropIntegrationTests), Ct)
            .GetAwaiter()
            .GetResult();

        _dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        // EnsureCreated applies the CURRENT EF model — after the ReviewCaseConfiguration change there is
        // no FK from ReviewCases to FileMetadata, so a worker INSERT keyed by an unknown FileId succeeds.
        using var ctx = new PrismaDbContext(_dbOptions);
        ctx.Database.EnsureCreatedAsync(Ct).GetAwaiter().GetResult();
    }

    /// <summary>
    /// A worker flags a degraded/partial case (<c>isComplete=false</c>) for a brand-new FileId that has no
    /// FileMetadata parent. The IncompleteCase row must persist to real SQL and be queryable — exactly what
    /// the manual-review dashboard reads. Before the FK drop this INSERT raised SqlException 547 and was
    /// silently swallowed.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCases_ForFileIdWithoutFileMetadata_PersistsToRealSql()
    {
        // Arrange — a FileId with NO FileMetadata row (the real worker scenario).
        var fileId = Guid.NewGuid().ToString();

        var classification = new ClassificationResult { Confidence = 90 };
        var metadata = new UnifiedMetadataRecord
        {
            Classification = classification,
            MatchedFields = new MatchedFields
            {
                ConflictingFields = new List<string>(),
                MissingFields = new List<string>(),
            },
        };

        await using var dbContext = new PrismaDbContext(_dbOptions);
        var service = new ManualReviewerService(
            dbContext, XUnitLogger.CreateLogger<ManualReviewerService>(_output));

        // Act — partial case (isComplete=false) ⇒ an IncompleteCase row, no FileMetadata parent seeded.
        var result = await service.IdentifyReviewCasesAsync(
            fileId, metadata, classification, isComplete: false, cancellationToken: Ct);

        // Assert — persistence succeeds (no FK violation) and the row is queryable from real SQL.
        result.IsSuccess.ShouldBeTrue(
            $"persisting a review case for a FileId with no FileMetadata row must succeed after the FK drop: " +
            $"{string.Join(", ", result.Errors)}");

        await using var queryContext = new PrismaDbContext(_dbOptions);
        var persisted = await queryContext.ReviewCases
            .Where(c => c.FileId == fileId)
            .ToListAsync(Ct);

        persisted.ShouldNotBeEmpty(
            "the review case must be physically persisted to SQL (it is what the manual-review dashboard reads)");
        persisted.ShouldContain(c => c.RequiresReviewReason == ReviewReason.IncompleteCase,
            "a partial case (isComplete=false) must persist an IncompleteCase review row");
    }

    /// <inheritdoc />
    public void Dispose() { }
}
