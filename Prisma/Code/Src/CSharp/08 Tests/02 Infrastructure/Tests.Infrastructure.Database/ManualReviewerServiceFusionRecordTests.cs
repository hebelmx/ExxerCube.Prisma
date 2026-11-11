using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Services;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// The Extractor saves <see cref="FusionMetadataRecordBuilder"/>'s record; the review page reads it back through
/// <see cref="ManualReviewerService.GetFieldAnnotationsAsync"/>. These tests run that whole path through the real
/// JSON store, so a field that does not survive serialization (or is not hydrated) fails here.
/// </summary>
public sealed class ManualReviewerServiceFusionRecordTests : IDisposable
{
    private const string FileId = "4c1d7a52-0b8e-4f53-9a51-6f1c2f0e9a11";
    private const string CaseId = "CASE-FUSION-001";

    private readonly PrismaDbContext _dbContext;
    private readonly EfCoreUnifiedMetadataStore _store;
    private readonly ManualReviewerService _service;

    /// <summary>Initializes with a unique in-memory database and a seeded review case.</summary>
    public ManualReviewerServiceFusionRecordTests(ITestOutputHelper output)
    {
        var options = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PrismaDbContext(options);
        _dbContext.Database.EnsureCreated();
        _store = new EfCoreUnifiedMetadataStore(_dbContext, XUnitLogger.CreateLogger<EfCoreUnifiedMetadataStore>(output));
        _service = new ManualReviewerService(_dbContext, XUnitLogger.CreateLogger<ManualReviewerService>(output), _store);

        _dbContext.FileMetadata.Add(new FileMetadata
        {
            FileId = FileId,
            FileName = "333ccc-6666666662025.pdf",
            FilePath = "2026/10/04/333ccc/333ccc-6666666662025.pdf",
            DownloadTimestamp = DateTime.UtcNow,
            Checksum = "abc",
            FileSize = 1,
            Format = FileFormat.Pdf,
        });
        _dbContext.ReviewCases.Add(new ReviewCase
        {
            CaseId = CaseId,
            FileId = FileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 62,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        });
        _dbContext.SaveChanges();
    }

    /// <inheritdoc />
    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task GetFieldAnnotationsAsync_FusionRecordSaved_ShowsEachSourcesValueForTheConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        (await _store.SaveAsync(FileId, FusionMetadataRecordBuilder.From(HeroCaseFusion()), ct)).IsSuccess.ShouldBeTrue();

        var result = await _service.GetFieldAnnotationsAsync(CaseId, ct);

        result.IsSuccess.ShouldBeTrue(result.Error);
        var oficio = result.Value!.FieldAnnotationsDict["NumeroOficio"];
        oficio.HasConflict.ShouldBeTrue();
        oficio.Value.ShouldBe("214-1-55555555/2025");
        oficio.Confidence.ShouldBe(55);
        oficio.AgreementLevel.ShouldBe(0.55f, 0.0001f);

        var sourceValues = oficio.AllSourceValues.OfType<FieldValue>().ToList();
        sourceValues.Select(v => (v.SourceType, v.Value)).ShouldBe(new[]
        {
            ("XML_HandFilled", (string?)"214-1-55555555/2025"),
            ("PDF_OCR_CNBV", (string?)"214-1-55555558/2025"),
        });
    }

    [Fact]
    public async Task GetFieldAnnotationsAsync_FusionRecordSaved_ShowsAgreedFieldsWithFullAgreementAndTheirSources()
    {
        var ct = TestContext.Current.CancellationToken;
        await _store.SaveAsync(FileId, FusionMetadataRecordBuilder.From(HeroCaseFusion()), ct);

        var result = await _service.GetFieldAnnotationsAsync(CaseId, ct);

        var expediente = result.Value!.FieldAnnotationsDict["NumeroExpediente"];
        expediente.HasConflict.ShouldBeFalse();
        expediente.AgreementLevel.ShouldBe(1f);
        expediente.Value.ShouldBe("A/AS1-4444-5555555-HHHH");
        expediente.AllSourceValues.OfType<FieldValue>().Select(v => v.SourceType)
            .ShouldBe(new[] { "XML_HandFilled", "PDF_OCR_CNBV", "DOCX_OCR_Authority" });
    }

    [Fact]
    public async Task GetFieldAnnotationsAsync_FusionRecordSaved_NamesTheRealSourcesNotAPlaceholder()
    {
        var ct = TestContext.Current.CancellationToken;
        await _store.SaveAsync(FileId, FusionMetadataRecordBuilder.From(HeroCaseFusion()), ct);

        var annotations = (await _service.GetFieldAnnotationsAsync(CaseId, ct)).Value!.FieldAnnotationsDict;

        annotations["NumeroExpediente"].Source.ShouldBe("XML_HandFilled, PDF_OCR_CNBV, DOCX_OCR_Authority");
        annotations["NumeroOficio"].Source.ShouldBe("XML_HandFilled, PDF_OCR_CNBV");
    }

    [Fact]
    public async Task GetFieldAnnotationsAsync_FieldNoSourceProvided_IsNotListedAsAgreed()
    {
        // A field every source left empty is not an agreement; listing it as "Agreed" at 0% misleads a reviewer.
        var ct = TestContext.Current.CancellationToken;
        await _store.SaveAsync(FileId, FusionMetadataRecordBuilder.From(HeroCaseFusion()), ct);

        var annotations = (await _service.GetFieldAnnotationsAsync(CaseId, ct)).Value!.FieldAnnotationsDict;

        annotations.ShouldNotContainKey("FundamentoLegal");
    }

    [Fact]
    public async Task SaveAsync_FusionRecord_ConflictAlertsAndExpedienteSurviveTheStoreRoundTrip()
    {
        var ct = TestContext.Current.CancellationToken;
        await _store.SaveAsync(FileId, FusionMetadataRecordBuilder.From(HeroCaseFusion()), ct);

        var loaded = await _store.GetByFileIdAsync(FileId, ct);

        loaded.IsSuccess.ShouldBeTrue(loaded.Error);
        var record = loaded.Value!;
        record.Expediente!.NumeroExpediente.ShouldBe("A/AS1-4444-5555555-HHHH");
        record.MatchedFields!.ConflictingFields.ShouldBe(new[] { "NumeroOficio" });
        var alert = record.FieldConflictAlerts.ShouldHaveSingleItem();
        alert.FieldName.ShouldBe("NumeroOficio");
        alert.ConflictingValues.Select(v => v.Source).ShouldBe(new[] { SourceType.XML_HandFilled, SourceType.PDF_OCR_CNBV });
    }

    /// <summary>The hero case's shape: the oficio number disagrees between XML and OCR; the expediente agrees.</summary>
    private static FusionResult HeroCaseFusion() => new()
    {
        FusedExpediente = new Expediente { NumeroExpediente = "A/AS1-4444-5555555-HHHH", NumeroOficio = "214-1-55555555/2025" },
        Confidence = Confidence.FromFusion(0.7),
        ConflictingFields = new List<string> { "NumeroOficio" },
        FieldResults = new Dictionary<string, FieldFusionResult>
        {
            ["NumeroExpediente"] = new()
            {
                Value = "A/AS1-4444-5555555-HHHH",
                Confidence = 0.95,
                Decision = FusionDecision.AllAgree,
                ContributingSources = new List<SourceType> { SourceType.XML_HandFilled, SourceType.PDF_OCR_CNBV, SourceType.DOCX_OCR_Authority },
            },
            ["FundamentoLegal"] = new()
            {
                Value = null,
                Confidence = 0,
                Decision = FusionDecision.AllSourcesNull,
            },
            ["NumeroOficio"] = new()
            {
                Value = "214-1-55555555/2025",
                Confidence = 0.55,
                Decision = FusionDecision.Conflict,
                ContributingSources = new List<SourceType> { SourceType.XML_HandFilled, SourceType.PDF_OCR_CNBV },
                ConflictingValues = new List<(SourceType Source, string? Value)>
                {
                    (SourceType.XML_HandFilled, "214-1-55555555/2025"),
                    (SourceType.PDF_OCR_CNBV, "214-1-55555558/2025"),
                },
            },
        },
    };
}
