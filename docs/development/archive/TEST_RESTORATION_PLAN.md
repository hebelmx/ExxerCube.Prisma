# Test Restoration Plan - IngestionOrchestratorTests

**Date**: 2025-12-04
**Status**: READY FOR EXECUTION
**Estimated Effort**: ~30-45 minutes

---

## Executive Summary

One valuable test file (`IngestionOrchestratorTests.cs`) was disabled during architecture fixes due to `IIngestionJournal` API signature changes after the `Prisma.Shared.Contracts` deletion. This document provides a complete analysis and step-by-step restoration plan.

---

## 1. Disabled Test Analysis

### 1.1 What Was Disabled

**File**: `Prisma/Code/Src/CSharp/04-Tests/06-Orion/Prisma.Orion.Ingestion.Tests/IngestionOrchestratorTests.cs`
**Commit**: `7750c1d` - "fix: Disable IngestionOrchestratorTests due to API signature changes"
**Date**: 2025-12-04 11:40:32
**Test Count**: 8 comprehensive unit tests

### 1.2 Test Coverage (What We're Losing)

The disabled test file provides critical coverage for:

1. **Happy Path Testing**
   - `IngestDocument_NewDocument_ReturnsSuccessAndBroadcastsEvent` ✅
   - Basic ingestion workflow validation

2. **Idempotency Testing**
   - `IngestDocument_DuplicateHash_ReturnsSuccessWithoutBroadcast` ✅
   - Validates hash-based duplicate detection
   - Ensures no duplicate storage or event broadcast

3. **SHA-256 Hash Validation**
   - `IngestDocument_ComputesCorrectSHA256Hash` ✅
   - Verifies cryptographic integrity
   - Test data: "Hello" → `185f8db32271fe25f561a6fc938b2e264306ec304eda518007d1764826381969`

4. **Storage Partitioning**
   - `IngestDocument_CreatesPartitionedStoragePath` ✅
   - Validates YYYY/MM/DD partitioning structure
   - Ensures proper file naming

5. **Correlation ID Preservation**
   - `IngestDocument_CorrelationId_PreservedInResult` ✅
   - End-to-end tracing validation

6. **Railway-Oriented Programming (ROP) Patterns**
   - `IngestDocument_WhenCancelled_ReturnsCancelledResult` ✅
   - `IngestDocument_WhenDownloadFails_ReturnsFailureWithoutBroadcast` ✅
   - Validates error handling without exceptions

7. **Transport-Agnostic Event Broadcasting**
   - `IngestDocument_BroadcastsViaIExxerHub_NotIEventPublisher` ✅
   - Validates use of `IExxerHub<T>.SendToAllAsync()`

---

## 2. Root Cause Analysis

### 2.1 API Signature Changes

#### OLD API (What Tests Expect)
```csharp
public interface IIngestionJournal
{
    // Check for duplicates using only hash
    Task<bool> IsDuplicateAsync(
        string hash,
        CancellationToken cancellationToken);

    // Record hash and path as individual parameters
    Task RecordAsync(
        string hash,
        string path,
        CancellationToken cancellationToken);
}
```

#### NEW API (Current Implementation)
```csharp
public interface IIngestionJournal
{
    // ✅ IMPROVED: Now checks hash + sourceUrl for better idempotency
    Task<bool> ExistsAsync(
        string contentHash,
        string sourceUrl,
        CancellationToken cancellationToken = default);

    // ✅ IMPROVED: Now takes structured entry object
    Task RecordAsync(
        IngestionManifestEntry entry,
        CancellationToken cancellationToken = default);

    // ✅ NEW: Query by FileId capability
    Task<IngestionManifestEntry?> GetByFileIdAsync(
        Guid fileId,
        CancellationToken cancellationToken = default);
}
```

#### New Data Structure
```csharp
public sealed record IngestionManifestEntry(
    Guid FileId,
    string FileName,
    string SourceUrl,
    string ContentHash,
    long FileSizeBytes,
    string StoredPath,
    Guid CorrelationId,
    DateTimeOffset DownloadedAt);
```

### 2.2 Why the API Changed

**Trigger**: `Prisma.Shared.Contracts` deletion during architecture cleanup (commit `c0c8b0d`)

**Improvements**:
1. **Better Idempotency**: Now uses `(contentHash, sourceUrl)` pair instead of just hash
   - Prevents false positives when same content comes from different sources
2. **Richer Manifest Data**: Structured `IngestionManifestEntry` instead of primitives
   - Supports full audit trail with timestamps, correlation IDs, file metadata
3. **Enhanced Queryability**: Added `GetByFileIdAsync()` for manifest lookups
4. **Consistency**: Aligns with broader manifest-based architecture

---

## 3. Restoration Strategy

### 3.1 Approach

**Option 1: Refactor Tests (RECOMMENDED)**
- Update test mocks to use new API signature
- Add `sourceUrl` parameter to mocks
- Construct `IngestionManifestEntry` objects in mock setups
- **Pros**: Tests validate actual production API, future-proof
- **Cons**: Requires test code changes
- **Effort**: 30-45 minutes

**Option 2: Create Adapter (NOT RECOMMENDED)**
- Create legacy adapter to wrap new API
- **Pros**: Tests run unchanged
- **Cons**: Introduces technical debt, defeats purpose of API improvement
- **Effort**: Similar to Option 1 but adds maintenance burden

**Decision**: **Option 1 - Refactor Tests**

### 3.2 Test Refactoring Mapping

Each test needs these changes:

| Old Mock Setup | New Mock Setup | Notes |
|----------------|----------------|-------|
| `journal.IsDuplicateAsync(Arg.Any<string>(), ...)` | `journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), ...)` | Add sourceUrl parameter |
| `journal.RecordAsync(Arg.Any<string>(), Arg.Any<string>(), ...)` | `journal.RecordAsync(Arg.Is<IngestionManifestEntry>(e => ...), ...)` | Match entry properties |
| N/A | Optional: Add `GetByFileIdAsync()` tests | New capability |

---

## 4. Step-by-Step Restoration Plan

### Phase 1: Re-enable Test File
1. **Remove Exclusion** from `Prisma.Orion.Ingestion.Tests.csproj`
   ```xml
   <!-- DELETE THIS -->
   <ItemGroup Label="Excluded Test Files">
     <Compile Remove="IngestionOrchestratorTests.cs" />
   </ItemGroup>
   ```

2. **Attempt Build** to identify all compilation errors
   ```bash
   cd Prisma/Code/Src/CSharp/04-Tests/06-Orion/Prisma.Orion.Ingestion.Tests
   dotnet build
   ```

### Phase 2: Refactor Test 1 - `IngestDocument_NewDocument_ReturnsSuccessAndBroadcastsEvent`

**OLD CODE (Lines 32-34)**:
```csharp
journal.IsDuplicateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(false);
```

**NEW CODE**:
```csharp
journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(false);
```

**OLD CODE (Lines 55-58)**:
```csharp
await journal.Received(1).RecordAsync(
    Arg.Any<string>(),
    Arg.Any<string>(),
    Arg.Any<CancellationToken>());
```

**NEW CODE**:
```csharp
await journal.Received(1).RecordAsync(
    Arg.Is<IngestionManifestEntry>(e =>
        e.ContentHash != null &&
        e.SourceUrl != null &&
        e.FileId != Guid.Empty &&
        e.CorrelationId == correlationId),
    Arg.Any<CancellationToken>());
```

### Phase 3: Refactor Test 2 - `IngestDocument_DuplicateHash_ReturnsSuccessWithoutBroadcast`

**OLD CODE (Line 81)**:
```csharp
journal.IsDuplicateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(true); // Duplicate detected after hashing
```

**NEW CODE**:
```csharp
journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(true); // Duplicate detected after hashing
```

**OLD CODE (Line 98)**:
```csharp
await journal.DidNotReceive().RecordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
```

**NEW CODE**:
```csharp
await journal.DidNotReceive().RecordAsync(Arg.Any<IngestionManifestEntry>(), Arg.Any<CancellationToken>());
```

### Phase 4: Refactor Test 3 - `IngestDocument_ComputesCorrectSHA256Hash`

**OLD CODE (Line 115)**:
```csharp
journal.IsDuplicateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(false);
```

**NEW CODE**:
```csharp
journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(false);
```

**OLD CODE (Lines 133-136)**:
```csharp
await journal.Received(1).RecordAsync(
    expectedHash,
    Arg.Any<string>(),
    Arg.Any<CancellationToken>());
```

**NEW CODE**:
```csharp
await journal.Received(1).RecordAsync(
    Arg.Is<IngestionManifestEntry>(e => e.ContentHash == expectedHash),
    Arg.Any<CancellationToken>());
```

### Phase 5: Refactor Tests 4-8 (Similar Patterns)

Apply same transformations to remaining tests:
- Test 4: `IngestDocument_CreatesPartitionedStoragePath`
- Test 5: `IngestDocument_CorrelationId_PreservedInResult`
- Test 6: `IngestDocument_WhenCancelled_ReturnsCancelledResult`
- Test 7: `IngestDocument_WhenDownloadFails_ReturnsFailureWithoutBroadcast`
- Test 8: `IngestDocument_BroadcastsViaIExxerHub_NotIEventPublisher`

### Phase 6: Verification

1. **Build Tests**
   ```bash
   dotnet build
   ```

2. **Run Tests**
   ```bash
   dotnet test --filter "FullyQualifiedName~IngestionOrchestratorTests"
   ```

3. **Verify All 8 Tests Pass**
   - Expected: `Passed: 8, Failed: 0, Skipped: 0`

4. **Commit Results**
   ```bash
   git add .
   git commit -m "feat: Restore IngestionOrchestratorTests with updated IIngestionJournal API"
   ```

---

## 5. Acceptance Criteria

### Must Have ✅
- [ ] All 8 tests compile successfully
- [ ] All 8 tests pass with green status
- [ ] Tests validate new `ExistsAsync(hash, sourceUrl)` signature
- [ ] Tests validate new `RecordAsync(IngestionManifestEntry)` signature
- [ ] Mock verifications use `Arg.Is<IngestionManifestEntry>()` matchers
- [ ] No test logic changes (only API signature updates)

### Should Have ✅
- [ ] Test documentation updated to reflect new API
- [ ] Comments explain `IngestionManifestEntry` structure
- [ ] Integration test coverage maintained

### Nice to Have
- [ ] Additional tests for `GetByFileIdAsync()` new capability
- [ ] Performance benchmarks for hash+URL duplicate detection

---

## 6. Risk Assessment

### Low Risk ✅
- **Scope**: Only test mocking code changes, no production code
- **Impact**: Restores 8 valuable tests, improves coverage
- **Rollback**: Simple - re-disable file if tests fail

### Validation Strategy
1. Run tests in isolation first
2. Run full test suite to ensure no regressions
3. Verify FileIngestionJournal integration tests still pass

---

## 7. Post-Restoration Tasks

### Immediate
- [ ] Update `HANDOFF_NEXT_SESSION.md` to mark tests as restored
- [ ] Remove "IngestionOrchestratorTests disabled" from known issues

### Follow-Up (Future Sessions)
- [ ] Add integration tests for `GetByFileIdAsync()` query capability
- [ ] Consider adding E2E tests for full ingestion pipeline
- [ ] Performance test hash+URL duplicate detection vs hash-only

---

## 8. Code Examples for Reference

### Example: Complete Test Refactoring

**BEFORE**:
```csharp
[Fact]
public async Task IngestDocument_NewDocument_ReturnsSuccessAndBroadcastsEvent()
{
    var journal = Substitute.For<IIngestionJournal>();
    journal.IsDuplicateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns(false);

    // ... test execution ...

    await journal.Received(1).RecordAsync(
        Arg.Any<string>(),
        Arg.Any<string>(),
        Arg.Any<CancellationToken>());
}
```

**AFTER**:
```csharp
[Fact]
public async Task IngestDocument_NewDocument_ReturnsSuccessAndBroadcastsEvent()
{
    var journal = Substitute.For<IIngestionJournal>();
    journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns(false);

    // ... test execution ...

    await journal.Received(1).RecordAsync(
        Arg.Is<IngestionManifestEntry>(e =>
            e.ContentHash != null &&
            e.SourceUrl != null &&
            e.FileId != Guid.Empty),
        Arg.Any<CancellationToken>());
}
```

---

## 9. Timeline

| Phase | Task | Duration |
|-------|------|----------|
| 1 | Re-enable file & identify errors | 5 min |
| 2-5 | Refactor all 8 tests | 20-30 min |
| 6 | Verification & testing | 5-10 min |
| Total | | **30-45 min** |

---

## 10. Success Metrics

**Quantitative**:
- 8/8 tests passing (100% success rate)
- Test execution time < 2 seconds (maintained performance)
- Zero compilation warnings

**Qualitative**:
- Tests validate improved API design (hash+URL idempotency)
- Test readability maintained or improved
- Mock setups align with production API patterns

---

**Prepared by**: Claude Code Agent
**Review Status**: READY FOR EXECUTION
**Next Action**: Execute Phase 1 (Re-enable test file)
