# Incremental Test Restoration Plan - IngestionOrchestratorTests

**Date**: 2025-12-04
**Approach**: INCREMENTAL BATCHES (User's Choice)
**Estimated Effort**: 2-3 hours in 3 manageable batches

---

## Strategy: Batch Processing

Instead of fixing all 8 tests at once, we'll proceed in **3 incremental batches**:
- **Batch 1**: Re-enable file, fix 2 critical tests (45 min)
- **Batch 2**: Fix remaining 6 tests (60 min)
- **Batch 3**: Integration validation (30 min)

**Benefits**:
- ✅ Incremental validation (catch issues early)
- ✅ Commit after each batch (safe rollback points)
- ✅ Easier to debug (smaller scope)
- ✅ Can pause/resume between batches

---

## Phase 0: Architecture Baseline (CURRENT STATE)

### Known Architecture Violations (Not Blocking)

We acknowledge these exist but won't block test restoration:

**1. IEventHandler<T> No Implementation** (LIKELY FALSE POSITIVE)
```
Violation: IEventHandler<T> has no implementation
Reason: Generic interface, implementations exist via closed types
Action: Document as false positive in architecture tests
Risk: LOW - Does not affect IngestionOrchestratorTests
```

**2. Worker Projects Don't Depend on Domain** (FALSE POSITIVE)
```
Violation: Prisma.Athena.Worker, Prisma.Orion.Worker don't depend on Domain
Reality: They DO have Domain references (checked in commit 2e2a8ca)
Reason: NetArchTest might not detect transitive dependencies
Action: Review architecture test logic
Risk: LOW - Workers compile and run fine
```

**3. Duplicate Class Names** (MIXED - Some Real, Some False Positive)
```
REAL ISSUE:
- ProcessingResult: Domain vs Prisma.Athena.Processing (NEEDS FIX)

FALSE POSITIVE:
- Program class in Workers (NORMAL - every Worker has Program.cs)

Action: Fix ProcessingResult duplication, whitelist Program class
Risk: MEDIUM for ProcessingResult, LOW for Program
```

**4. Stub Implementations** (INTENTIONAL DESIGN)
```
Violations (5 methods):
- ComplementExtractionStrategy.CanHandle (returns false - intentional)
- SearchExtractionStrategy.CanHandle (returns false - intentional)
- InMemoryEventBus methods (placeholder for future impl)

Reason: Some strategies intentionally return false (not applicable)
Action: Document as intentional, consider adding comments
Risk: LOW - These are design decisions, not bugs
```

### Decision: Proceed Despite Violations

**Rationale**:
- Architecture violations are **not related** to IngestionOrchestratorTests
- Tests are about ingestion pipeline, not event handling or workers
- We can fix architecture issues in parallel/after test restoration
- User confirmed: "due diligence required" but proceed

---

## Batch 1: Re-enable + Fix 2 Critical Tests (45 min)

### Goals
- [ ] Re-enable IngestionOrchestratorTests.cs compilation
- [ ] Fix project references
- [ ] Fix 2 highest-value tests to validate approach
- [ ] Commit progress

### Step 1.1: Re-enable Test File (5 min)

```bash
cd Prisma/Code/Src/CSharp/04-Tests/06-Orion/Prisma.Orion.Ingestion.Tests
```

**Edit**: `Prisma.Orion.Ingestion.Tests.csproj`

```xml
<!-- DELETE THIS ENTIRE BLOCK -->
<ItemGroup Label="Excluded Test Files">
  <Compile Remove="IngestionOrchestratorTests.cs" />
</ItemGroup>
```

### Step 1.2: Update Project References (5 min)

Ensure correct references exist:

```xml
<ItemGroup>
  <!-- Ensure these exist -->
  <ProjectReference Include="..\..\..\01-Core\Domain\ExxerCube.Prisma.Domain.csproj" />
  <ProjectReference Include="..\..\..\06-Orion\Prisma.Orion.Ingestion\Prisma.Orion.Ingestion.csproj" />

  <!-- DELETE if exists -->
  <!-- <ProjectReference Include="..\..\..\..\Prisma.Shared.Contracts\..." /> -->
</ItemGroup>
```

### Step 1.3: Attempt Build - Capture Errors (5 min)

```bash
dotnet build 2>&1 | tee build_errors.txt
```

Expected: ~12-15 compilation errors (API signature mismatches)

### Step 1.4: Fix Using Statements (5 min)

**Edit**: `IngestionOrchestratorTests.cs` (lines 1-5)

```csharp
// DELETE (if exists)
// using Prisma.Shared.Contracts;

// ADD (if missing)
using ExxerCube.Prisma.Domain.Events;         // DocumentDownloadedEvent
using ExxerCube.Prisma.Domain.Interfaces;     // IIngestionJournal, IngestionManifestEntry
```

### Step 1.5: Fix Test #1 - NewDocument (15 min)

**Target**: `IngestDocument_NewDocument_ReturnsSuccessAndBroadcastsEvent` (lines 24-65)

**Change 1**: Update duplicate check mock (line 32-34)
```csharp
// BEFORE
journal.IsDuplicateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(false);

// AFTER
journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(false);
```

**Change 2**: Update record mock verification (lines 55-58)
```csharp
// BEFORE
await journal.Received(1).RecordAsync(
    Arg.Any<string>(),
    Arg.Any<string>(),
    Arg.Any<CancellationToken>());

// AFTER
await journal.Received(1).RecordAsync(
    Arg.Is<IngestionManifestEntry>(e =>
        !string.IsNullOrEmpty(e.ContentHash) &&
        !string.IsNullOrEmpty(e.SourceUrl) &&
        e.FileId != Guid.Empty &&
        e.CorrelationId == correlationId),
    Arg.Any<CancellationToken>());
```

### Step 1.6: Fix Test #2 - DuplicateHash (10 min)

**Target**: `IngestDocument_DuplicateHash_ReturnsSuccessWithoutBroadcast` (lines 69-100)

**Change 1**: Update duplicate check (line 81-82)
```csharp
// BEFORE
journal.IsDuplicateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(true);

// AFTER
journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(true);
```

**Change 2**: Update negative verification (line 98)
```csharp
// BEFORE
await journal.DidNotReceive().RecordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

// AFTER
await journal.DidNotReceive().RecordAsync(Arg.Any<IngestionManifestEntry>(), Arg.Any<CancellationToken>());
```

### Step 1.7: Build and Test (5 min)

```bash
dotnet build
dotnet test --filter "FullyQualifiedName~IngestDocument_NewDocument_ReturnsSuccessAndBroadcastsEvent"
dotnet test --filter "FullyQualifiedName~IngestDocument_DuplicateHash_ReturnsSuccessWithoutBroadcast"
```

**Expected**: 2/8 tests passing

### Step 1.8: Commit Batch 1 (5 min)

```bash
git add .
git commit -m "feat: Restore IngestionOrchestratorTests (Batch 1/3) - 2 critical tests passing

- Re-enabled IngestionOrchestratorTests.cs compilation
- Updated project references (removed Prisma.Shared.Contracts)
- Fixed using statements for Domain.Events and Domain.Interfaces
- Refactored 2 tests to use new IIngestionJournal API:
  * IngestDocument_NewDocument_ReturnsSuccessAndBroadcastsEvent
  * IngestDocument_DuplicateHash_ReturnsSuccessWithoutBroadcast

API Changes Applied:
- IsDuplicateAsync() → ExistsAsync(hash, sourceUrl)
- RecordAsync(hash, path) → RecordAsync(IngestionManifestEntry)

Progress: 2/8 tests passing (25%)

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

**Checkpoint**: You can stop here and resume later!

---

## Batch 2: Fix Remaining 6 Tests (60 min)

### Test #3: ComputesCorrectSHA256Hash (10 min)

**Changes**:
```csharp
// Line 115
journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(false);

// Lines 133-136
await journal.Received(1).RecordAsync(
    Arg.Is<IngestionManifestEntry>(e => e.ContentHash == expectedHash),
    Arg.Any<CancellationToken>());
```

### Test #4: CreatesPartitionedStoragePath (10 min)

**Changes**:
```csharp
// Line 149
journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(false);

// Lines 172-179
await journal.Received(1).RecordAsync(
    Arg.Is<IngestionManifestEntry>(e =>
        e.StoredPath.Contains($"{now.Year:D4}") &&
        e.StoredPath.Contains($"{now.Month:D2}") &&
        e.StoredPath.Contains($"{now.Day:D2}") &&
        e.FileName.EndsWith($"{documentId}.pdf")),
    Arg.Any<CancellationToken>());
```

### Test #5: CorrelationId_PreservedInResult (10 min)

**Changes**:
```csharp
// Line 192
journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(false);

// Lines 211-213
await journal.Received(1).RecordAsync(
    Arg.Is<IngestionManifestEntry>(e => e.CorrelationId == correlationId),
    Arg.Any<CancellationToken>());
```

### Test #6: WhenCancelled_ReturnsCancelledResult (5 min)

**No journal mock changes needed** - just verify test still passes

### Test #7: WhenDownloadFails_ReturnsFailureWithoutBroadcast (5 min)

**Change**:
```csharp
// Lines 266-267
await journal.DidNotReceive().RecordAsync(Arg.Any<IngestionManifestEntry>(), Arg.Any<CancellationToken>());
```

### Test #8: BroadcastsViaIExxerHub_NotIEventPublisher (10 min)

**Changes**:
```csharp
// Line 280
journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(false);

// No RecordAsync verification in this test (focuses on event broadcasting)
```

### Build and Test All (10 min)

```bash
dotnet build
dotnet test --filter "FullyQualifiedName~IngestionOrchestratorTests"
```

**Expected**: 8/8 tests passing ✅

### Commit Batch 2 (5 min)

```bash
git add .
git commit -m "feat: Restore IngestionOrchestratorTests (Batch 2/3) - All 8 tests passing

- Refactored remaining 6 tests to use new IIngestionJournal API
- All tests now validate IngestionManifestEntry structure
- Updated all mock setups to use ExistsAsync(hash, sourceUrl)
- Updated all verifications to use Arg.Is<IngestionManifestEntry>()

Tests Fixed:
- ComputesCorrectSHA256Hash
- CreatesPartitionedStoragePath
- CorrelationId_PreservedInResult
- WhenCancelled_ReturnsCancelledResult
- WhenDownloadFails_ReturnsFailureWithoutBroadcast
- BroadcastsViaIExxerHub_NotIEventPublisher

Progress: 8/8 tests passing (100%)

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

**Checkpoint**: Major milestone - all tests passing!

---

## Batch 3: Integration Validation (30 min)

### Step 3.1: Test with Real FileIngestionJournal (10 min)

Create integration test to verify new API works:

```bash
cd ../Prisma.Orion.Ingestion.Tests
```

**Add** new test file: `FileIngestionJournalIntegrationTests.cs`

```csharp
using ExxerCube.Prisma.Domain.Interfaces;
using Prisma.Orion.Ingestion;

namespace Prisma.Orion.Ingestion.Tests;

public sealed class FileIngestionJournalIntegrationTests : IDisposable
{
    private readonly string _tempJournalPath;
    private readonly FileIngestionJournal _journal;

    public FileIngestionJournalIntegrationTests()
    {
        _tempJournalPath = Path.Combine(Path.GetTempPath(), $"test-journal-{Guid.NewGuid()}.jsonl");
        _journal = new FileIngestionJournal(_tempJournalPath, NullLogger<FileIngestionJournal>.Instance);
    }

    [Fact]
    public async Task RecordAndRetrieve_ValidEntry_Success()
    {
        // Arrange
        var entry = new IngestionManifestEntry(
            FileId: Guid.NewGuid(),
            FileName: "test.pdf",
            SourceUrl: "http://test.com/doc",
            ContentHash: "abc123",
            FileSizeBytes: 1024,
            StoredPath: "/storage/test.pdf",
            CorrelationId: Guid.NewGuid(),
            DownloadedAt: DateTimeOffset.UtcNow);

        // Act
        await _journal.RecordAsync(entry);
        var exists = await _journal.ExistsAsync(entry.ContentHash, entry.SourceUrl);
        var retrieved = await _journal.GetByFileIdAsync(entry.FileId);

        // Assert
        exists.ShouldBeTrue();
        retrieved.ShouldNotBeNull();
        retrieved.FileId.ShouldBe(entry.FileId);
        retrieved.ContentHash.ShouldBe(entry.ContentHash);
    }

    public void Dispose()
    {
        if (File.Exists(_tempJournalPath))
            File.Delete(_tempJournalPath);
    }
}
```

Run integration test:
```bash
dotnet test --filter "FullyQualifiedName~FileIngestionJournalIntegrationTests"
```

### Step 3.2: Run Full Test Suite (10 min)

```bash
cd ../../..
dotnet test --no-build 2>&1 | tee full_test_results.txt
```

Verify no regressions in other test projects.

### Step 3.3: Architecture Test Review (10 min)

Document false positives for architecture team:

```bash
cd 04-Tests/06-Architecture/Tests.Architecture
dotnet test --filter "FullyQualifiedName~HexagonalArchitectureTests" 2>&1 | tee arch_test_review.txt
```

**Create**: `ARCHITECTURE_TEST_FALSE_POSITIVES.md`

```markdown
# Architecture Test False Positives Review

## 1. IEventHandler<T> No Implementation
**Status**: FALSE POSITIVE
**Reason**: Generic interface with closed-type implementations
**Recommendation**: Update test to check for closed generic types

## 2. Worker Projects Don't Depend on Domain
**Status**: FALSE POSITIVE
**Reason**: Dependencies exist but not detected by NetArchTest
**Recommendation**: Check transitive dependencies or use direct assembly scanning

## 3. Program Class Duplicates
**Status**: FALSE POSITIVE (for Program class)
**Reason**: Every Worker project has Program.cs - this is normal
**Recommendation**: Whitelist "Program" class in architecture test

## 4. ProcessingResult Duplicate
**Status**: REAL ISSUE
**Action Required**: Consolidate or rename one implementation
**Location**: Domain vs Prisma.Athena.Processing

## 5. Stub Implementations
**Status**: INTENTIONAL DESIGN
**Reason**: Some strategies return false (not applicable scenarios)
**Recommendation**: Add XML doc comments explaining intentional design
```

### Step 3.4: Final Commit (5 min)

```bash
git add .
git commit -m "feat: Restore IngestionOrchestratorTests (Batch 3/3) - Integration validated

- Added FileIngestionJournalIntegrationTests
- Validated real FileIngestionJournal works with new API
- Documented architecture test false positives
- Full test suite passing (no regressions)

Final Status:
- IngestionOrchestratorTests: 8/8 passing ✅
- Integration tests: Passing ✅
- Full test suite: No regressions ✅

Remaining Work (Future):
- Review architecture test false positives
- Fix ProcessingResult duplication
- Add XML doc comments to stub implementations

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Summary Timeline

| Batch | Tasks | Duration | Checkpoint |
|-------|-------|----------|------------|
| **1** | Re-enable + 2 tests | 45 min | Can pause here ✅ |
| **2** | Remaining 6 tests | 60 min | Can pause here ✅ |
| **3** | Integration validation | 30 min | COMPLETE ✅ |
| **Total** | | **2.25 hours** | 3 safe checkpoints |

---

## Rollback Strategy

**If Batch 1 fails**:
```bash
git reset --hard HEAD~1  # Undo commit
# Re-add <Compile Remove="IngestionOrchestratorTests.cs" /> to .csproj
dotnet build  # Should succeed
```

**If Batch 2 fails**:
```bash
git reset --hard HEAD~1  # Keep Batch 1 progress, undo Batch 2
# Fix issues, re-attempt
```

**If Batch 3 fails**:
```bash
# Batches 1 & 2 are still successful!
# Review integration test issues separately
```

---

## Next Action

Ready to start? I can:
1. **Execute Batch 1 now** (45 min, 2 tests)
2. **Execute all batches** (2.25 hours, full restoration)
3. **Just provide the plan** (you execute incrementally)

Which would you prefer?
