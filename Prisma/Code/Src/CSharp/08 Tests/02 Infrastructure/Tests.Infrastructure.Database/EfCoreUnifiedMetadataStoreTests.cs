namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Round-trip and not-found tests for <see cref="EfCoreUnifiedMetadataStore"/>.
/// </summary>
public class EfCoreUnifiedMetadataStoreTests : IDisposable
{
    private readonly PrismaDbContext _dbContext;
    private readonly EfCoreUnifiedMetadataStore _store;
    private readonly ILogger<EfCoreUnifiedMetadataStore> _logger;

    /// <summary>
    /// Initializes a new instance using an in-memory database (unique per test).
    /// </summary>
    public EfCoreUnifiedMetadataStoreTests(ITestOutputHelper output)
    {
        var dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(dbOptions);
        _dbContext.Database.EnsureCreated();
        _logger = XUnitLogger.CreateLogger<EfCoreUnifiedMetadataStore>(output);
        _store = new EfCoreUnifiedMetadataStore(_dbContext, _logger);
    }

    /// <inheritdoc />
    public void Dispose() => _dbContext.Dispose();

    /// <summary>
    /// Saving a new record and loading it back returns an equal record.
    /// AdditionalFields must be preserved exactly.
    /// </summary>
    [Fact]
    public async Task SaveAsync_ThenGetByFileIdAsync_ReturnsEqualRecord()
    {
        // Arrange
        var fileId = "FILE-ROUND-TRIP-001";
        var record = new UnifiedMetadataRecord
        {
            AdditionalFields = new Dictionary<string, string?>
            {
                ["AreaDescripcion"] = "Dirección General",
                ["NumeroExpediente"] = "EXP-2026-001",
            },
            Classification = new ClassificationResult
            {
                Level1 = ClassificationLevel1.Aseguramiento,
                Confidence = Confidence.FromInt(85),
            },
        };

        // Act
        var saveResult = await _store.SaveAsync(fileId, record, TestContext.Current.CancellationToken);
        var loadResult = await _store.GetByFileIdAsync(fileId, TestContext.Current.CancellationToken);

        // Assert
        saveResult.IsSuccess.ShouldBeTrue();
        loadResult.IsSuccess.ShouldBeTrue();
        loadResult.Value.ShouldNotBeNull();

        // AdditionalFields preserved
        loadResult.Value!.AdditionalFields["AreaDescripcion"].ShouldBe("Dirección General");
        loadResult.Value.AdditionalFields["NumeroExpediente"].ShouldBe("EXP-2026-001");

        // SmartEnum (ClassificationLevel1) survives round-trip
        loadResult.Value.Classification.ShouldNotBeNull();
        loadResult.Value.Classification!.Level1.ShouldNotBeNull();
        loadResult.Value.Classification.Level1!.Name.ShouldBe(ClassificationLevel1.Aseguramiento.Name);
        loadResult.Value.Classification.Confidence.Value.ShouldBe(0.85);
    }

    /// <summary>
    /// A second Save for the same fileId should upsert (not insert a duplicate row).
    /// </summary>
    [Fact]
    public async Task SaveAsync_CalledTwiceForSameFileId_UpsertsBothTimes()
    {
        // Arrange
        var fileId = "FILE-UPSERT-001";
        var first = new UnifiedMetadataRecord
        {
            AdditionalFields = { ["Field1"] = "value1" },
        };
        var second = new UnifiedMetadataRecord
        {
            AdditionalFields = { ["Field1"] = "updated" },
        };

        // Act
        await _store.SaveAsync(fileId, first, TestContext.Current.CancellationToken);
        var saveResult2 = await _store.SaveAsync(fileId, second, TestContext.Current.CancellationToken);
        var loadResult = await _store.GetByFileIdAsync(fileId, TestContext.Current.CancellationToken);

        // Assert
        saveResult2.IsSuccess.ShouldBeTrue();
        loadResult.IsSuccess.ShouldBeTrue();
        loadResult.Value!.AdditionalFields["Field1"].ShouldBe("updated");

        // Only one row in the table
        var count = await _dbContext.UnifiedMetadataRecords.CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(1);
    }

    /// <summary>
    /// GetByFileIdAsync for an absent fileId returns IsFailure (not success-with-null).
    /// </summary>
    [Fact]
    public async Task GetByFileIdAsync_ForAbsentFileId_ReturnsFailure()
    {
        // Act
        var result = await _store.GetByFileIdAsync("NOT-EXIST", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("NOT-EXIST");
    }

    /// <summary>
    /// SaveAsync with a null record returns a failure immediately (validation guard).
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithNullRecord_ReturnsFailure()
    {
        var result = await _store.SaveAsync("FILE-NULL", null!, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// SaveAsync with an empty fileId returns a failure immediately (validation guard).
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithEmptyFileId_ReturnsFailure()
    {
        var result = await _store.SaveAsync(string.Empty, new UnifiedMetadataRecord(), TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// GetByFileIdAsync with an empty fileId returns a failure immediately (validation guard).
    /// </summary>
    [Fact]
    public async Task GetByFileIdAsync_WithEmptyFileId_ReturnsFailure()
    {
        var result = await _store.GetByFileIdAsync(string.Empty, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
    }
}
