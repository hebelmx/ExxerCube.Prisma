# Stage 8: End-to-End Validation Implementation Plan

**Status**: 🔄 IN PROGRESS
**Goal**: Validate complete SIARA → Orion → Athena → DB/Export → UI notification flow
**Created**: 2025-12-03
**Last Updated**: 2025-12-03

---

## 📋 Overview

Stage 8 validates the entire Prisma system working end-to-end:
- Synthetic SIARA document submission
- Orion ingestion and manifest creation
- Athena processing pipeline (Quality → OCR → Classification → Export)
- Database audit trail persistence
- Event broadcasting to HMI
- Health endpoint operational status

**Exit Criteria**:
- ✅ E2E test project created and integrated into solution
- ✅ Full pipeline test passes (SIARA → UI notification)
- ✅ Correlation ID preserved across all stages
- ✅ Audit trail verified in database
- ✅ Export artifacts generated and validated
- ✅ Health endpoints return correct status
- ✅ HMI UI receives and displays events

---

## 🏗️ Architecture

### Test Project Structure
```
Prisma.Tests.System.E2E/
├── Prisma.Tests.System.E2E.csproj
├── GlobalUsings.cs
├── Fixtures/
│   ├── SiaraDocumentFixture.cs         # Synthetic SIARA document generator
│   ├── DatabaseFixture.cs              # In-memory/test DB setup
│   └── WorkerHostFixture.cs            # Orion/Athena worker hosting
├── Tests/
│   ├── FullPipelineE2ETests.cs         # Main E2E flow validation
│   ├── CorrelationIdTracking Tests.cs   # Correlation ID end-to-end tracking
│   ├── AuditTrailValidationTests.cs    # Database audit completeness
│   └── HealthEndpointE2ETests.cs       # Health/dashboard endpoint validation
└── Utilities/
    ├── TestCorrelationIdTracker.cs     # Helper to track correlation across stages
    └── TestEventCollector.cs           # Collects all emitted events for assertion
```

### Test Flow Diagram
```
[Test Setup]
    ↓
[Synthetic SIARA Document] → [Orion Worker]
    ↓                               ↓
[File Fixture]              [DocumentDownloadedEvent]
    ↓                               ↓
[Stored: /yyyy/MM/dd/]      [Athena Worker]
    ↓                               ↓
[DB Manifest Entry]         [Processing Pipeline]
                                    ↓
                    [Quality → OCR → Classification → Export]
                                    ↓
                         [Events: Quality/Ocr/Classification/ProcessingCompleted]
                                    ↓
                           [HMI Event Broadcaster]
                                    ↓
                            [Test Event Collector]
                                    ↓
                          [Assertions: Full Validation]
```

---

## 📦 Test Project Configuration

### 1. Create Test Project

**Location**: `Prisma/Code/Src/CSharp/04-Tests/07-E2E/Prisma.Tests.System.E2E/`

**Project File** (`Prisma.Tests.System.E2E.csproj`):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup Label="Core Properties">
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <OutputType>Exe</OutputType>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
  </PropertyGroup>

  <PropertyGroup Label="Testing Platform">
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
    <TestingPlatformServer>true</TestingPlatformServer>
  </PropertyGroup>

  <!-- Testing Utilities -->
  <ItemGroup Label="Testing Utilities">
    <PackageReference Include="IndFusion.Ember" />
    <PackageReference Include="IndQuestResults" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
    <PackageReference Include="Testcontainers" />
    <PackageReference Include="Testcontainers.MsSql" />
  </ItemGroup>

  <!-- Logging & Diagnostics -->
  <ItemGroup Label="Logging">
    <PackageReference Include="Microsoft.Extensions.Logging" />
    <PackageReference Include="Meziantou.Extensions.Logging.Xunit.v3" />
  </ItemGroup>

  <!-- xUnit v3 -->
  <ItemGroup Label="xUnit v3">
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="xunit.v3.runner.inproc.console" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
  </ItemGroup>

  <!-- Project References -->
  <ItemGroup>
    <ProjectReference Include="..\..\..\06-Orion\Prisma.Orion.Worker\Prisma.Orion.Worker.csproj" />
    <ProjectReference Include="..\..\..\06-Athena\Prisma.Athena.Worker\Prisma.Athena.Worker.csproj" />
    <ProjectReference Include="..\..\..\06-Orion\Prisma.Orion.Ingestion\Prisma.Orion.Ingestion.csproj" />
    <ProjectReference Include="..\..\..\06-Athena\Prisma.Athena.Processing\Prisma.Athena.Processing.csproj" />
    <ProjectReference Include="..\..\..\06-Shared\Prisma.Shared.Contracts\Prisma.Shared.Contracts.csproj" />
    <ProjectReference Include="..\..\..\02-Infrastructure\Infrastructure.Database\ExxerCube.Prisma.Infrastructure.Database.csproj" />
  </ItemGroup>

  <!-- Global Usings -->
  <ItemGroup Label="Global Usings">
    <Using Include="Xunit" />
    <Using Include="NSubstitute" />
    <Using Include="Shouldly" />
    <Using Include="IndFusion.Ember.Abstractions.Hubs" />
    <Using Include="IndQuestResults" />
    <Using Include="IndQuestResults.Operations" />
    <Using Include="Microsoft.Extensions.Logging" />
    <Using Include="Microsoft.Extensions.DependencyInjection" />
    <Using Include="Microsoft.Extensions.Hosting" />
    <Using Include="System" />
    <Using Include="System.Collections.Generic" />
    <Using Include="System.Linq" />
    <Using Include="System.Threading" />
    <Using Include="System.Threading.Tasks" />
    <Using Include="System.Text.Json" />
    <Using Include="System.IO" />
  </ItemGroup>
</Project>
```

---

## 🧪 Test Implementation

### Test 1: Full Pipeline E2E Flow

**File**: `FullPipelineE2ETests.cs`

**Purpose**: Validate complete flow from synthetic SIARA document to HMI notification

**Test Cases**:
1. `E2E_SyntheticDocument_CompletesFullPipeline` ✅
   - Create synthetic SIARA document fixture
   - Submit to Orion worker
   - Wait for DocumentDownloadedEvent
   - Verify Athena processing starts
   - Collect all pipeline events (Quality, OCR, Classification, ProcessingCompleted)
   - Assert all events emitted with correct correlation ID
   - Verify export artifact created
   - Validate DB audit trail entries

2. `E2E_CorrelationId_PreservedAcrossAllStages` ✅
   - Track single correlation ID through entire pipeline
   - Assert same ID in: manifest DB entry, all events, audit log, export metadata

3. `E2E_HealthEndpoints_ReflectPipelineStatus` ✅
   - Start workers
   - Check `/health` and `/dashboard` endpoints return 200
   - Submit document
   - Verify dashboard metrics updated (processed count, last event time)

### Test 2: Audit Trail Validation

**File**: `AuditTrailValidationTests.cs`

**Purpose**: Ensure complete audit trail in database

**Test Cases**:
1. `AuditTrail_AllStages_RecordedInDatabase` ✅
   - Process document E2E
   - Query audit table
   - Assert entries for: Download, Quality, OCR, Classification, Export
   - Verify timestamps sequential
   - Confirm correlation ID consistent

2. `AuditTrail_ErrorScenario_RecordsFailure` ✅
   - Inject OCR failure
   - Verify audit trail records error state
   - Confirm pipeline stops at failure point
   - Assert no downstream events emitted

### Test 3: Event Broadcasting Validation

**File**: `EventBroadcastingValidationTests.cs`

**Purpose**: Validate events reach HMI broadcasting layer

**Test Cases**:
1. `EventBroadcasting_AllEvents_ReachHMI` ✅
   - Mock IExxerHub<T> collectors for each event type
   - Process document E2E
   - Assert all 4 event types broadcast:
     - QualityCompletedEvent
     - OcrCompletedEvent
     - ClassificationCompletedEvent
     - ProcessingCompletedEvent

2. `EventBroadcasting_CorrelationId_PreservedInAllEvents` ✅
   - Collect all broadcast events
   - Assert correlation ID identical across all events

---

## 🛠️ Implementation Steps

### Phase 1: Project Setup ✅
1. Create `Prisma.Tests.System.E2E` project in `04-Tests/07-E2E/`
2. Add project to `ExxerCube.Prisma.sln`
3. Configure project references and package dependencies
4. Create `GlobalUsings.cs`

### Phase 2: Test Fixtures (Infrastructure) 🔄
1. `SiaraDocumentFixture.cs` - Generate synthetic PDF documents
2. `DatabaseFixture.cs` - In-memory SQL Server (Testcontainers)
3. `WorkerHostFixture.cs` - Host Orion/Athena workers in test context
4. `TestEventCollector.cs` - Capture all IExxerHub<T> broadcasts

### Phase 3: Core E2E Tests 🔄
1. Implement `FullPipelineE2ETests.cs`
2. Implement `AuditTrailValidationTests.cs`
3. Implement `EventBroadcastingValidationTests.cs`
4. Implement `HealthEndpointE2ETests.cs`

### Phase 4: Integration & Validation 🔄
1. Run all E2E tests
2. Fix any discovered integration issues
3. Verify 100% test pass rate
4. Update ITDD_Implementation_Plan.md with results

### Phase 5: Documentation 🔄
1. Document E2E test execution instructions
2. Add CI/CD pipeline configuration for E2E tests
3. Update README with Stage 8 completion

---

## 📊 Success Metrics

- ✅ E2E test project builds successfully
- ✅ All E2E tests pass (target: 10-15 tests)
- ✅ Correlation ID tracking validated end-to-end
- ✅ Audit trail completeness verified
- ✅ Health endpoints operational
- ✅ Event broadcasting to HMI confirmed
- ✅ Export artifacts generated correctly
- ✅ No exceptions or crashes during full pipeline

---

## 🚧 Known Challenges

1. **Testcontainers SQL Server**: May require Docker Desktop running
   - **Mitigation**: Use in-memory EF Core database for faster tests

2. **Worker Lifecycle Management**: Starting/stopping workers in tests
   - **Mitigation**: Use `WebApplicationFactory<TEntryPoint>` for hosting

3. **Event Timing**: Async events may require polling/waiting
   - **Mitigation**: Use `TaskCompletionSource` to await specific events

4. **File System Isolation**: Tests must not interfere with each other
   - **Mitigation**: Use unique temp directories per test (`Path.GetTempPath()` + Guid)

---

## 📝 Next Actions

1. ✅ Document updated with current state
2. 🔄 Create test project structure
3. ⏳ Implement test fixtures
4. ⏳ Write core E2E tests
5. ⏳ Run and validate
6. ⏳ Update main ITDD plan with results

---

## 🎯 Definition of Done

Stage 8 is complete when:
- ✅ `Prisma.Tests.System.E2E` project exists in solution
- ✅ All E2E tests pass (10-15 tests minimum)
- ✅ Full pipeline validated: SIARA → Orion → Athena → DB → HMI
- ✅ Correlation ID tracking proven end-to-end
- ✅ Audit trail completeness verified
- ✅ Health endpoints operational
- ✅ Documentation updated
- ✅ CI/CD pipeline includes E2E tests

**Final Deliverable**: Working end-to-end Prisma system with proven reliability through comprehensive E2E validation! 🎉
