# Document Processing Pipeline - System Test Plan

## Overview
End-to-end system tests for the complete document processing pipeline in **Tests.System.Storage**.
Tests business logic with **real SQL Server container** and **real SIARA XML/PDF fixtures**.

## Test Philosophy
- ✅ Test business logic (data flow, events, persistence, reconciliation)
- ✅ Use real infrastructure (SQL Server, file system, actual PDFs/XMLs)
- ✅ Test critical failure paths (degraded PDFs, malformed XML, conflicts)
- ✅ Test defensive intelligence (partial data, low confidence, flagging)
- ❌ Do NOT test UI (no CSS, fonts, component loading, SignalR rendering)
- ❌ Do NOT test mocked components (use real services where possible)

## Existing Assets

### Test Infrastructure (Already Working)
- **SQL Server Container**: SqlServerContainerFixture with EnsureCreatedAsync strategy
- **Event Publisher**: Real EventPublisher + EventPersistenceWorker
- **Database**: PrismaDbContext with FK constraints enforced
- **Test Data**: Fixtures/PRP1/ with real SIARA XML/PDF pairs

### Test Data Files (Fixtures/PRP1/)
| File | Type | Description |
|------|------|-------------|
| 222AAA-44444444442025.xml/.pdf | Aseguramiento | Clean document pair |
| 333BBB-44444444442025.xml/.pdf | Hacendario Documentacion | Clean document pair |
| 333ccc-6666666662025.xml/.pdf | Judicial with CURP+RFC | Person identification |
| 555CCC-66666662025.xml/.pdf | Operaciones Ilícitas | Account blocking case |
| missing_expediente.xml | Malformed XML | Missing expediente field |
| missing_subdivision.xml | Malformed XML | Missing subdivision field |
| missing_identity.xml | Malformed XML | Missing RFC/CURP fields |
| missing_accounts.xml | Malformed XML | Missing account data |

## Test Suite: DocumentProcessingPipelineIntegrationTests

**Location**: `Tests.System.Storage/DocumentProcessingPipelineIntegrationTests.cs`

### Test 1: Happy Path - Complete Document Processing
```csharp
[Fact]
public async Task ProcessDocument_CleanXmlAndPdf_CompletePipelineWithEvents()
```

**Business Logic Verified**:
- ✅ XML parsed successfully from 222AAA-44444444442025.xml
- ✅ PDF quality analyzed (blur, noise, contrast metrics calculated)
- ✅ OCR extracts text from PDF
- ✅ XML and OCR data reconciled (field-by-field comparison)
- ✅ No conflicts detected (data matches)
- ✅ Classification determines: Area=Aseguramiento, Type=Bloqueo
- ✅ **Events Published**:
  - `DocumentDownloadedEvent`
  - `QualityAnalysisCompletedEvent`
  - `OcrCompletedEvent`
  - `ClassificationCompletedEvent`
  - `DocumentProcessingCompletedEvent`
- ✅ **All events persisted to SQL Server AuditRecords**
- ✅ **FileMetadata created** (with FK satisfaction)
- ✅ **CorrelationId** links all events
- ✅ **Audit trail complete** with timestamps, stages, success flags

**What we DON'T test**:
- ❌ SignalR hub broadcasts
- ❌ UI rendering
- ❌ Real-time component updates

### Test 2: Critical Failure Path - Degraded PDF Quality
```csharp
[Fact]
public async Task ProcessDocument_LowQualityPdf_AdaptiveFilteringAndFlagging()
```

**Business Logic Verified**:
- ✅ Quality analysis detects low quality metrics (high blur, low contrast)
- ✅ Adaptive filter selection chooses polynomial enhancement
- ✅ OCR runs with enhancement applied
- ✅ Low confidence detected (< 70%)
- ✅ **`DocumentFlaggedForReviewEvent` published**
- ✅ Event persisted with `Success=true` but flagged for manual review
- ✅ Defensive Intelligence: Processing continues, doesn't crash
- ✅ Partial data still extracted and saved

### Test 3: Defensive Intelligence - Malformed XML
```csharp
[Fact]
public async Task ProcessDocument_MissingXmlFields_TolerantParsingContinues()
```

**Business Logic Verified**:
- ✅ XML parser handles missing_expediente.xml gracefully
- ✅ Nullable fields populated (Expediente=null, but other fields present)
- ✅ No XML parsing exception thrown
- ✅ OCR provides fallback data for missing fields
- ✅ **`ConflictDetectedEvent` published** (missing data flagged)
- ✅ Processing completes with partial data
- ✅ Defensive Intelligence: System continues

### Test 4: Reconciliation - XML vs OCR Conflict Detection
```csharp
[Fact]
public async Task ProcessDocument_XmlOcrMismatch_DetectsAndFlagsConflict()
```

**Business Logic Verified**:
- ✅ XML extracts Subdivision="Aseguramiento"
- ✅ OCR extracts Subdivision="Judicial" (mismatch)
- ✅ Reconciliation compares field-by-field
- ✅ Levenshtein distance calculated
- ✅ Conflict detected for Subdivision field
- ✅ **`ConflictDetectedEvent` published** with field details
- ✅ XML value prioritized (wins over OCR)
- ✅ Conflict logged in AuditRecords with details
- ✅ Document flagged for manual review

### Test 5: Classification - Multi-Field Detection
```csharp
[Fact]
public async Task ProcessDocument_OperacionesIlicitas_ClassifiesCorrectly()
```

**Business Logic Verified**:
- ✅ Processes 555CCC-66666662025.xml/pdf (Operaciones Ilícitas + Desbloqueo)
- ✅ Classification detects: Area=OperacionesIlicitas, Type=Desbloqueo, Priority=High
- ✅ Account numbers extracted: ["00466773850", "00195019117"]
- ✅ RFC list extracted: ["LUMH111111111", "LUMH222222222"]
- ✅ **`ClassificationCompletedEvent` published** with classification data
- ✅ Event persisted with correct ActionType and Stage
- ✅ Account and identity data included in ActionDetails JSON

### Test 6: Full Pipeline Ordering - Event Sequence Verification
```csharp
[Fact]
public async Task ProcessDocument_FullPipeline_EventsInCorrectOrder()
```

**Business Logic Verified**:
- ✅ Process complete document through all stages
- ✅ Query AuditRecords by CorrelationId
- ✅ Verify events ordered by timestamp:
  1. DocumentDownloadedEvent (Stage=Ingestion)
  2. QualityAnalysisCompletedEvent (Stage=Extraction)
  3. OcrCompletedEvent (Stage=Extraction)
  4. ClassificationCompletedEvent (Stage=DecisionLogic)
  5. DocumentProcessingCompletedEvent (Stage=Export)
- ✅ All events share same FileId and CorrelationId
- ✅ Timestamps in ascending order
- ✅ No gaps in processing stages

### Test 7: FK Constraint Enforcement - Real Database Validation
```csharp
[Fact]
public async Task ProcessDocument_WithoutFileMetadata_EnforcesFK()
```

**Business Logic Verified**:
- ✅ Attempt to persist event without creating FileMetadata first
- ✅ SQL Server FK constraint violation detected
- ✅ **Defensive Intelligence**: EventPersistenceWorker logs error but continues
- ✅ System doesn't crash
- ✅ Error logged in Serilog with FileId details
- ✅ Subsequent events with proper FK setup succeed

### Test 8: Multi-Document Processing - Independent Workflows
```csharp
[Fact]
public async Task ProcessDocuments_MultipleConcurrent_IndependentCorrelationIds()
```

**Business Logic Verified**:
- ✅ Process 222AAA and 333BBB documents concurrently
- ✅ Each workflow has unique CorrelationId
- ✅ Events kept separate by CorrelationId
- ✅ No cross-contamination between workflows
- ✅ Both audit trails complete independently
- ✅ FileMetadata records distinct

## Test Implementation Pattern

### Setup (Per Test Class)
```csharp
[Collection("DatabaseInfrastructure")]
public class DocumentProcessingPipelineIntegrationTests : IDisposable
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly DbContextOptions<PrismaDbContext> _dbOptions;
    private readonly IEventPublisher _eventPublisher;
    private readonly EventPersistenceWorker _worker;
    private readonly string _fixturesPath;

    public DocumentProcessingPipelineIntegrationTests(
        SqlServerContainerFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _fixture.EnsureAvailable();

        _dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseSqlServer(_fixture.ConnectionString)
            .Options;

        // Apply EF Core EnsureCreatedAsync strategy
        using (var context = new PrismaDbContext(_dbOptions))
        {
            context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken)
                .GetAwaiter()
                .GetResult();
        }

        _fixture.CleanDatabaseAsync().GetAwaiter().GetResult();

        // Wire up real services (not mocks!)
        _eventPublisher = new EventPublisher(logger);
        _worker = new EventPersistenceWorker(_eventPublisher, logger, scopeFactory);
        _fixturesPath = Path.Combine("Fixtures", "PRP1");
    }
}
```

### Helper Methods
```csharp
private async Task<Guid> CreateFileMetadataAsync(string fileName)
{
    // Creates FileMetadata to satisfy FK constraints
    var fileId = Guid.NewGuid();
    using var context = new PrismaDbContext(_dbOptions);
    context.FileMetadata.Add(new FileMetadata
    {
        FileId = fileId.ToString(),
        FileName = fileName,
        DownloadDateTime = DateTime.UtcNow,
        Format = FileFormat.Pdf
    });
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    return fileId;
}

private async Task<List<AuditRecord>> GetAuditTrailAsync(Guid correlationId)
{
    // Retrieves complete audit trail for correlation ID
    using var context = new PrismaDbContext(_dbOptions);
    return await context.AuditRecords
        .Where(r => r.CorrelationId == correlationId.ToString())
        .OrderBy(r => r.Timestamp)
        .ToListAsync(TestContext.Current.CancellationToken);
}

private string GetFixturePath(string filename)
{
    return Path.Combine(_fixturesPath, filename);
}
```

## Success Criteria

### Phase 1: Infrastructure (Completed ✅)
- [x] SQL Server container working
- [x] EventPersistenceWorker 100% passing
- [x] EnsureCreatedAsync strategy implemented
- [x] FK constraint enforcement verified

### Phase 2: Basic Pipeline Tests
- [ ] Test 1: Happy path implemented and passing
- [ ] Test 2: Degraded PDF implemented and passing
- [ ] Test 3: Malformed XML implemented and passing
- [ ] FK helper methods working

### Phase 3: Advanced Business Logic
- [ ] Test 4: Reconciliation conflicts implemented and passing
- [ ] Test 5: Classification implemented and passing
- [ ] Test 6: Event ordering implemented and passing
- [ ] Test 7: FK enforcement implemented and passing
- [ ] Test 8: Multi-document implemented and passing

### Phase 4: Stakeholder Demo Preparation
- [ ] Identify 3 most impressive test scenarios
- [ ] Document visual flow for each scenario
- [ ] Prepare realistic test data for demo

### Phase 5: WebUI Implementation (Later)
- [ ] Create Real-time Processing Dashboard
- [ ] Wire SignalR hub to show live events
- [ ] Visualize quality metrics
- [ ] Display audit trail timeline

## Key Architectural Decisions

### Why Tests.System.Storage?
- Tests storage-level integration (database + events + persistence)
- Already has SQL Server container infrastructure
- Already has real SIARA test data
- Builds on EventPersistenceWorker success

### Why SQL Server Container (Not InMemory)?
- FK constraints enforced (caught 9 violations!)
- Real transaction behavior
- Real concurrency/timing
- Real migration/schema behavior
- **Production-equivalent testing**

### Why Real Services (Not Mocks)?
- Test actual integration points
- Test actual data flow
- Catch real FK violations, timing issues, serialization bugs
- **Test the joints, not the parts**

## Dependencies

### Infrastructure Services Required
- `PrismaDbContext` with SQL Server
- `IEventPublisher` + `EventPersistenceWorker`
- `XmlFieldExtractor`
- `QualityAnalysisService` (EmguCV)
- `OcrProcessingService` (Tesseract)
- `FieldMatchingService` (reconciliation)
- `DocumentClassificationService`

### Test Data Required
- PRP1 XML/PDF fixtures (already exist!)
- Malformed XML test cases (already exist!)
- Degraded PDF samples (may need to create)

## Notes

### Testing Philosophy
This follows the user's explicit guidance:
> "we are testing business logic, not that it has a style, it loads the reactive component"
> "someone told me once if you are not testing on a production database you are not really testing"

### TDD Approach
> "lets make that plan on written first system test later refactor to webui"
> "using the most impressive ones to make visual impression for the stakeholder but still drive development"

Tests drive implementation → Most impressive scenarios become stakeholder demos.

### What Makes a Test "Impressive" for Stakeholders?
1. **Defensive Intelligence in Action**: System handles bad data gracefully, continues processing
2. **Real SIARA Documents**: Using actual government XML/PDFs, not toy data
3. **Complete Audit Trail**: Full traceability from download → processing → storage
4. **Conflict Detection**: Shows intelligent reconciliation when XML ≠ OCR
5. **Real-time Event Flow**: Complete correlation tracking across pipeline stages

These scenarios will translate directly to compelling WebUI visualizations later.
