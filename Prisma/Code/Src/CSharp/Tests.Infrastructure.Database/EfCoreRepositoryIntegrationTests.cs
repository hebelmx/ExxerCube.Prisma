namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Comprehensive integration tests for <see cref="EfCoreRepository{T, TId}"/> demonstrating
/// that the generic repository works with multiple domain entities using in-memory database.
///
/// These tests verify:
/// - Repository works with different entity types (FileMetadata, Persona, ReviewCase, ReviewDecision)
/// - Repository works with different ID types (string, int)
/// - All CRUD operations work correctly
/// - Specifications work correctly
/// - Projections work correctly
/// - Real domain entities are used (not mocks)
/// </summary>
public sealed class EfCoreRepositoryIntegrationTests : IDisposable
{
    private readonly PrismaDbContext _dbContext;

    public EfCoreRepositoryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    // FileMetadata Tests (String ID)

    [Fact]
    public async Task FileMetadataRepository_GetByIdAsync_ShouldReturnEntity_WhenExists()
    {
        // Arrange
        var repository = new EfCoreRepository<FileMetadata, string>(_dbContext);
        var metadata = CreateFileMetadata("file-001", "test.pdf", FileFormat.Pdf);
        _dbContext.FileMetadata.Add(metadata);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.GetByIdAsync("file-001", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.FileId.ShouldBe("file-001");
        result.Value.FileName.ShouldBe("test.pdf");
        result.Value.Format.ShouldBe(FileFormat.Pdf);
    }

    [Fact]
    public async Task FileMetadataRepository_GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        var repository = new EfCoreRepository<FileMetadata, string>(_dbContext);

        // Act
        var result = await repository.GetByIdAsync("non-existent", TestContext.Current.CancellationToken);

        // Assert
        // For nullable Result<T?>, use IsSuccessMayBeNull to check success (null is valid)
        result.IsSuccessMayBeNull.ShouldBeTrue($"Expected success (may be null) but got failure. Error: {result.Error}");
        result.Value.ShouldBeNull();
    }

    [Fact]
    public async Task FileMetadataRepository_AddAsync_ShouldPersistEntity()
    {
        // Arrange
        var repository = new EfCoreRepository<FileMetadata, string>(_dbContext);
        var metadata = CreateFileMetadata("file-002", "document.pdf", FileFormat.Pdf);

        // Act
        var addResult = await repository.AddAsync(metadata, TestContext.Current.CancellationToken);
        addResult.IsSuccess.ShouldBeTrue();

        var saveResult = await repository.SaveChangesAsync(TestContext.Current.CancellationToken);
        saveResult.IsSuccess.ShouldBeTrue();
        saveResult.Value.ShouldBe(1);

        // Assert - Verify entity was persisted
        var retrieved = await repository.GetByIdAsync("file-002", TestContext.Current.CancellationToken);
        retrieved.IsSuccess.ShouldBeTrue();
        retrieved.Value.ShouldNotBeNull();
        retrieved.Value!.FileName.ShouldBe("document.pdf");
    }

    [Fact]
    public async Task FileMetadataRepository_FindAsync_ShouldFilterCorrectly()
    {
        // Arrange
        var repository = new EfCoreRepository<FileMetadata, string>(_dbContext);
        var pdf1 = CreateFileMetadata("pdf-001", "file1.pdf", FileFormat.Pdf);
        var pdf2 = CreateFileMetadata("pdf-002", "file2.pdf", FileFormat.Pdf);
        var xml1 = CreateFileMetadata("xml-001", "file1.xml", FileFormat.Xml);

        await repository.AddRangeAsync(new[] { pdf1, pdf2, xml1 }, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.FindAsync(
            f => f.Format == FileFormat.Pdf,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        result.Value.All(f => f.Format == FileFormat.Pdf).ShouldBeTrue();
    }

    [Fact]
    public async Task FileMetadataRepository_UpdateAsync_ShouldModifyEntity()
    {
        // Arrange
        var repository = new EfCoreRepository<FileMetadata, string>(_dbContext);
        var metadata = CreateFileMetadata("file-003", "original.pdf", FileFormat.Pdf);
        await repository.AddAsync(metadata, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        metadata.FileName = "updated.pdf";
        var updateResult = await repository.UpdateAsync(metadata, TestContext.Current.CancellationToken);
        updateResult.IsSuccess.ShouldBeTrue();

        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var retrieved = await repository.GetByIdAsync("file-003", TestContext.Current.CancellationToken);
        retrieved.IsSuccess.ShouldBeTrue();
        retrieved.Value!.FileName.ShouldBe("updated.pdf");
    }

    [Fact]
    public async Task FileMetadataRepository_RemoveAsync_ShouldDeleteEntity()
    {
        // Arrange
        var repository = new EfCoreRepository<FileMetadata, string>(_dbContext);
        var metadata = CreateFileMetadata("file-004", "to-delete.pdf", FileFormat.Pdf);
        await repository.AddAsync(metadata, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var removeResult = await repository.RemoveAsync(metadata, TestContext.Current.CancellationToken);
        removeResult.IsSuccess.ShouldBeTrue();

        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var retrieved = await repository.GetByIdAsync("file-004", TestContext.Current.CancellationToken);
        // For nullable Result<T?>, use IsSuccessMayBeNull to check success (null is valid after deletion)
        retrieved.IsSuccessMayBeNull.ShouldBeTrue($"Expected success (may be null) but got failure. Error: {retrieved.Error}");
        retrieved.Value.ShouldBeNull();
    }

    [Fact]
    public async Task FileMetadataRepository_ListAsync_ShouldReturnAllEntities()
    {
        // Arrange
        var repository = new EfCoreRepository<FileMetadata, string>(_dbContext);
        var files = new[]
        {
            CreateFileMetadata("file-005", "file1.pdf", FileFormat.Pdf),
            CreateFileMetadata("file-006", "file2.xml", FileFormat.Xml),
            CreateFileMetadata("file-007", "file3.docx", FileFormat.Docx)
        };

        await repository.AddRangeAsync(files, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.ListAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task FileMetadataRepository_CountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var repository = new EfCoreRepository<FileMetadata, string>(_dbContext);
        var files = new[]
        {
            CreateFileMetadata("file-008", "file1.pdf", FileFormat.Pdf),
            CreateFileMetadata("file-009", "file2.pdf", FileFormat.Pdf),
            CreateFileMetadata("file-010", "file3.xml", FileFormat.Xml)
        };

        await repository.AddRangeAsync(files, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var totalCount = await repository.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        var pdfCount = await repository.CountAsync(f => f.Format == FileFormat.Pdf, TestContext.Current.CancellationToken);

        // Assert
        totalCount.IsSuccess.ShouldBeTrue();
        totalCount.Value.ShouldBeGreaterThanOrEqualTo(3);

        pdfCount.IsSuccess.ShouldBeTrue();
        pdfCount.Value.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task FileMetadataRepository_ExistsAsync_ShouldReturnTrue_WhenEntityExists()
    {
        // Arrange
        var repository = new EfCoreRepository<FileMetadata, string>(_dbContext);
        var metadata = CreateFileMetadata("file-011", "exists.pdf", FileFormat.Pdf);
        await repository.AddAsync(metadata, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var exists = await repository.ExistsAsync(f => f.FileId == "file-011", TestContext.Current.CancellationToken);
        var notExists = await repository.ExistsAsync(f => f.FileId == "non-existent", TestContext.Current.CancellationToken);

        // Assert
        exists.IsSuccess.ShouldBeTrue();
        exists.Value.ShouldBeTrue();

        notExists.IsSuccess.ShouldBeTrue();
        notExists.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task FileMetadataRepository_SelectAsync_ShouldProjectCorrectly()
    {
        // Arrange
        var repository = new EfCoreRepository<FileMetadata, string>(_dbContext);
        var files = new[]
        {
            CreateFileMetadata("file-012", "file1.pdf", FileFormat.Pdf),
            CreateFileMetadata("file-013", "file2.xml", FileFormat.Xml)
        };

        await repository.AddRangeAsync(files, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.SelectAsync(
            f => f.Format == FileFormat.Pdf,
            f => f.FileName,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBeGreaterThanOrEqualTo(1);
        result.Value.ShouldContain("file1.pdf");
    }

    //  FileMetadata Tests (String ID)

    // Persona Tests (Int ID)

    [Fact]
    public async Task PersonaRepository_GetByIdAsync_ShouldReturnEntity_WhenExists()
    {
        // Arrange
        var repository = new EfCoreRepository<Persona, int>(_dbContext);
        var persona = CreatePersona(1, "Juan", "Pérez", "García", "RFC123456789");
        _dbContext.Persona.Add(persona);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.GetByIdAsync(1, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ParteId.ShouldBe(1);
        result.Value.Nombre.ShouldBe("Juan");
        result.Value.Paterno.ShouldBe("Pérez");
    }

    [Fact]
    public async Task PersonaRepository_AddAsync_ShouldPersistEntity()
    {
        // Arrange
        var repository = new EfCoreRepository<Persona, int>(_dbContext);
        var persona = CreatePersona(2, "María", "González", "López", "RFC987654321");

        // Act
        var addResult = await repository.AddAsync(persona, TestContext.Current.CancellationToken);
        addResult.IsSuccess.ShouldBeTrue();

        var saveResult = await repository.SaveChangesAsync(TestContext.Current.CancellationToken);
        saveResult.IsSuccess.ShouldBeTrue();

        // Assert
        var retrieved = await repository.GetByIdAsync(2, TestContext.Current.CancellationToken);
        retrieved.IsSuccess.ShouldBeTrue();
        retrieved.Value.ShouldNotBeNull();
        retrieved.Value!.Nombre.ShouldBe("María");
    }

    [Fact]
    public async Task PersonaRepository_FindAsync_ShouldFilterByRfc()
    {
        // Arrange
        var repository = new EfCoreRepository<Persona, int>(_dbContext);
        var persona1 = CreatePersona(3, "Carlos", "Sánchez", "Martínez", "RFC111111111");
        var persona2 = CreatePersona(4, "Ana", "Rodríguez", "Fernández", "RFC222222222");

        await repository.AddRangeAsync(new[] { persona1, persona2 }, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.FindAsync(
            p => p.Rfc == "RFC111111111",
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(1);
        result.Value[0].Nombre.ShouldBe("Carlos");
    }

    [Fact]
    public async Task PersonaRepository_ListAsync_WithPredicate_ShouldFilterCorrectly()
    {
        // Arrange
        var repository = new EfCoreRepository<Persona, int>(_dbContext);
        var persona1 = CreatePersona(5, "Pedro", "Torres", null, "RFC333333333");
        var persona2 = CreatePersona(6, "Laura", "Vargas", "Hernández", "RFC444444444");

        await repository.AddRangeAsync(new[] { persona1, persona2 }, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.ListAsync(
            p => p.Materno != null,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBeGreaterThanOrEqualTo(1);
        result.Value.All(p => p.Materno != null).ShouldBeTrue();
    }

    //  Persona Tests (Int ID)

    // ReviewCase Tests (String ID)

    [Fact]
    public async Task ReviewCaseRepository_GetByIdAsync_ShouldReturnEntity_WhenExists()
    {
        // Arrange
        var repository = new EfCoreRepository<ReviewCase, string>(_dbContext);
        var reviewCase = CreateReviewCase("case-001", "file-001", ReviewReason.LowConfidence);
        _dbContext.ReviewCases.Add(reviewCase);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.GetByIdAsync("case-001", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.CaseId.ShouldBe("case-001");
        result.Value.FileId.ShouldBe("file-001");
        result.Value.RequiresReviewReason.ShouldBe(ReviewReason.LowConfidence);
    }

    [Fact]
    public async Task ReviewCaseRepository_AddAsync_ShouldPersistEntity()
    {
        // Arrange
        var repository = new EfCoreRepository<ReviewCase, string>(_dbContext);
        var reviewCase = CreateReviewCase("case-002", "file-002", ReviewReason.AmbiguousClassification);

        // Act
        var addResult = await repository.AddAsync(reviewCase, TestContext.Current.CancellationToken);
        addResult.IsSuccess.ShouldBeTrue();

        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var retrieved = await repository.GetByIdAsync("case-002", TestContext.Current.CancellationToken);
        retrieved.IsSuccess.ShouldBeTrue();
        retrieved.Value.ShouldNotBeNull();
        retrieved.Value!.RequiresReviewReason.ShouldBe(ReviewReason.AmbiguousClassification);
    }

    [Fact]
    public async Task ReviewCaseRepository_FindAsync_ShouldFilterByStatus()
    {
        // Arrange
        var repository = new EfCoreRepository<ReviewCase, string>(_dbContext);
        var case1 = CreateReviewCase("case-003", "file-003", ReviewReason.LowConfidence);
        case1.Status = ReviewStatus.Pending;
        var case2 = CreateReviewCase("case-004", "file-004", ReviewReason.AmbiguousClassification);
        case2.Status = ReviewStatus.InProgress;

        await repository.AddRangeAsync(new[] { case1, case2 }, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.FindAsync(
            c => c.Status == ReviewStatus.Pending,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBeGreaterThanOrEqualTo(1);
        result.Value.All(c => c.Status == ReviewStatus.Pending).ShouldBeTrue();
    }

    //  ReviewCase Tests (String ID)

    // ReviewDecision Tests (String ID)

    [Fact]
    public async Task ReviewDecisionRepository_GetByIdAsync_ShouldReturnEntity_WhenExists()
    {
        // Arrange
        var repository = new EfCoreRepository<ReviewDecision, string>(_dbContext);
        var reviewDecision = CreateReviewDecision("decision-001", "case-001", DecisionType.Approve, "reviewer-001");
        _dbContext.ReviewDecisions.Add(reviewDecision);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.GetByIdAsync("decision-001", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.DecisionId.ShouldBe("decision-001");
        result.Value.CaseId.ShouldBe("case-001");
        result.Value.DecisionType.ShouldBe(DecisionType.Approve);
        result.Value.ReviewerId.ShouldBe("reviewer-001");
    }

    [Fact]
    public async Task ReviewDecisionRepository_AddAsync_ShouldPersistEntity()
    {
        // Arrange
        var repository = new EfCoreRepository<ReviewDecision, string>(_dbContext);
        var reviewDecision = CreateReviewDecision("decision-002", "case-002", DecisionType.Reject, "reviewer-002");

        // Act
        var addResult = await repository.AddAsync(reviewDecision, TestContext.Current.CancellationToken);
        addResult.IsSuccess.ShouldBeTrue();

        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var retrieved = await repository.GetByIdAsync("decision-002", TestContext.Current.CancellationToken);
        retrieved.IsSuccess.ShouldBeTrue();
        retrieved.Value.ShouldNotBeNull();
        retrieved.Value!.DecisionType.ShouldBe(DecisionType.Reject);
        retrieved.Value.CaseId.ShouldBe("case-002");
    }

    [Fact]
    public async Task ReviewDecisionRepository_FindAsync_ShouldFilterByDecisionType()
    {
        // Arrange
        var repository = new EfCoreRepository<ReviewDecision, string>(_dbContext);
        var decision1 = CreateReviewDecision("decision-003", "case-003", DecisionType.Approve, "reviewer-001");
        var decision2 = CreateReviewDecision("decision-004", "case-004", DecisionType.Reject, "reviewer-002");

        await repository.AddRangeAsync(new[] { decision1, decision2 }, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.FindAsync(
            d => d.DecisionType == DecisionType.Approve,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBeGreaterThanOrEqualTo(1);
        result.Value.All(d => d.DecisionType == DecisionType.Approve).ShouldBeTrue();
    }

    [Fact]
    public async Task ReviewDecisionRepository_FindAsync_ShouldFilterByCaseId()
    {
        // Arrange
        var repository = new EfCoreRepository<ReviewDecision, string>(_dbContext);
        var decision1 = CreateReviewDecision("decision-005", "case-005", DecisionType.Approve, "reviewer-001");
        var decision2 = CreateReviewDecision("decision-006", "case-005", DecisionType.RequestMoreInfo, "reviewer-002");

        await repository.AddRangeAsync(new[] { decision1, decision2 }, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.FindAsync(
            d => d.CaseId == "case-005",
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        result.Value.All(d => d.CaseId == "case-005").ShouldBeTrue();
    }

    //  ReviewDecision Tests (String ID)

    // Specification Tests

    [Fact]
    public async Task FileMetadataRepository_ListAsync_WithSpecification_ShouldFilterAndOrder()
    {
        // Arrange
        var repository = new EfCoreRepository<FileMetadata, string>(_dbContext);
        var files = new[]
        {
            CreateFileMetadata("file-spec-001", "a.pdf", FileFormat.Pdf, fileSize: 1000),
            CreateFileMetadata("file-spec-002", "b.pdf", FileFormat.Pdf, fileSize: 2000),
            CreateFileMetadata("file-spec-003", "c.xml", FileFormat.Xml, fileSize: 500)
        };

        await repository.AddRangeAsync(files, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        var spec = new FileMetadataSpecification
        {
            Criteria = f => f.Format == FileFormat.Pdf,
            OrderBy = f => f.FileName,
            Take = 2
        };

        // Act
        var result = await repository.ListAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        result.Value.All(f => f.Format == FileFormat.Pdf).ShouldBeTrue();
        result.Value[0].FileName.ShouldBe("a.pdf");
        result.Value[1].FileName.ShouldBe("b.pdf");
    }

    [Fact]
    public async Task PersonaRepository_FirstOrDefaultAsync_WithSpecification_ShouldReturnSingleEntity()
    {
        // Arrange
        var repository = new EfCoreRepository<Persona, int>(_dbContext);
        var persona1 = CreatePersona(10, "Test", "Person1", null, "RFC555555555");
        var persona2 = CreatePersona(11, "Test", "Person2", "Last", "RFC666666666");

        await repository.AddRangeAsync(new[] { persona1, persona2 }, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        var spec = new PersonaSpecification
        {
            Criteria = p => p.Rfc == "RFC555555555",
            OrderBy = p => p.Nombre
        };

        // Act
        var result = await repository.FirstOrDefaultAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Rfc.ShouldBe("RFC555555555");
        result.Value.Nombre.ShouldBe("Test");
    }

    //  Specification Tests

    // Helper Methods

    private static FileMetadata CreateFileMetadata(string fileId, string fileName, FileFormat format, long fileSize = 1024)
    {
        return new FileMetadata
        {
            FileId = fileId,
            FileName = fileName,
            FilePath = $"/files/{fileName}",
            DownloadTimestamp = DateTime.UtcNow,
            Checksum = Guid.NewGuid().ToString("N"),
            FileSize = fileSize,
            Format = format
        };
    }

    private static Persona CreatePersona(int parteId, string nombre, string paterno, string? materno, string? rfc)
    {
        return new Persona
        {
            ParteId = parteId,
            Nombre = nombre,
            Paterno = paterno,
            Materno = materno,
            Rfc = rfc,
            Caracter = "Contribuyente",
            PersonaTipo = "Fisica"
        };
    }

    private static ReviewCase CreateReviewCase(string caseId, string fileId, ReviewReason reason)
    {
        return new ReviewCase
        {
            CaseId = caseId,
            FileId = fileId,
            RequiresReviewReason = reason,
            ConfidenceLevel = 50,
            ClassificationAmbiguity = false,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static ReviewDecision CreateReviewDecision(string decisionId, string caseId, DecisionType decisionType, string reviewerId)
    {
        return new ReviewDecision
        {
            DecisionId = decisionId,
            CaseId = caseId,
            DecisionType = decisionType,
            ReviewerId = reviewerId,
            ReviewedAt = DateTime.UtcNow,
            Notes = $"Test decision for {decisionType}",
            ReviewReason = ReviewReason.LowConfidence,
            OverriddenFields = new Dictionary<string, object>()
        };
    }

    //  Helper Methods

    // Specification Implementations

    private sealed class FileMetadataSpecification : ISpecification<FileMetadata>
    {
        public Expression<Func<FileMetadata, bool>>? Criteria { get; init; }
        public Expression<Func<FileMetadata, object>>? OrderBy { get; init; }
        public Expression<Func<FileMetadata, object>>? OrderByDescending { get; init; }
        public IReadOnlyList<Expression<Func<FileMetadata, object>>> Includes => Array.Empty<Expression<Func<FileMetadata, object>>>();
        public int? Skip { get; init; }
        public int? Take { get; init; }
        public bool IsPagingEnabled => Skip.HasValue || Take.HasValue;
    }

    private sealed class PersonaSpecification : ISpecification<Persona>
    {
        public Expression<Func<Persona, bool>>? Criteria { get; init; }
        public Expression<Func<Persona, object>>? OrderBy { get; init; }
        public Expression<Func<Persona, object>>? OrderByDescending { get; init; }
        public IReadOnlyList<Expression<Func<Persona, object>>> Includes => Array.Empty<Expression<Func<Persona, object>>>();
        public int? Skip { get; init; }
        public int? Take { get; init; }
        public bool IsPagingEnabled => Skip.HasValue || Take.HasValue;
    }

    //  Specification Implementations

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}