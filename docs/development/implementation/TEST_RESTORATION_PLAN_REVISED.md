# Test Restoration Plan - IngestionOrchestratorTests (REVISED)

**Date**: 2025-12-04
**Status**: ⚠️ **ARCHITECTURE-AWARE - NOT JUST API CHANGES**
**Estimated Effort**: ~2-3 hours (not 45 minutes!)

---

## Executive Summary - The Real Story

The `IngestionOrchestratorTests.cs` file wasn't disabled due to simple API signature changes. It was collateral damage from **massive architectural refactoring** to fix fundamental Clean Architecture violations:

1. **Duplicate Interface Definitions** - Same interfaces existed in multiple layers with different implementations
2. **Circular Dependencies** - Adapter pattern misuse creating Infrastructure → Application → Infrastructure cycles
3. **Layer Violations** - Infrastructure projects contained interfaces (should be in Domain)
4. **Shared Contracts Anti-Pattern** - `Prisma.Shared.Contracts` project deleted (83 files affected)

**Quick-and-dirty workaround**: Tests were disabled to unblock compilation during architecture cleanup.

**Proper restoration**: Requires understanding and validating the architectural fixes are complete.

---

## 1. The Architecture Violation Saga

### 1.1 Timeline of Architecture Fixes

**Commit 2e2a8ca** - "Consolidate domain interfaces to fix architecture violations"
- **Files Changed**: 83 files (+1685/-1437 lines)
- **Problem**: Duplicate interfaces across layers
  - `IHealthCheckService` existed in:
    - `Orion.HealthChecks/IHealthCheckService.cs`
    - `Athena.HealthChecks/IHealthCheckService.cs`
    - Both had **similar but different** implementations
  - Same pattern for `IDashboardService`, `IDocumentDownloader`
- **Type Collisions**: `HealthCheckResult` vs ASP.NET Core's `HealthCheckResult`
- **Fix**: Consolidated all to `Domain.Interfaces`, deleted duplicates
- **Impact**: 42 project files updated, entire event system reorganized

**Commit 54741eb** - "Resolve DI container architecture violations and circular dependencies"
- **Problem**: `OcrProcessingServiceAdapter.cs` created circular dependency
  - `IOcrProcessingService` (Domain) → `OcrProcessingServiceAdapter` (Infrastructure) → back to Domain
  - Violated: Infrastructure MUST NOT reference Application
- **Fix**: Deleted adapter, made `OcrProcessingService` implement interface directly
- **Impact**: DI container could finally build successfully

**Commit c0c8b0d** - "Resolve compilation errors from Prisma.Shared.Contracts deletion"
- **Problem**: `Prisma.Shared.Contracts` project deleted (shared contracts anti-pattern)
- **Files Changed**: 80 files
- **Impact**: All event references broken, namespaces changed
- **Result**: `IngestionOrchestratorTests.cs` had 12 API signature errors
- **Quick Fix**: Test file disabled (what we're dealing with now)

**Commit 7750c1d** - "Disable IngestionOrchestratorTests due to API signature changes"
- **The Bandaid**: Excluded `IngestionOrchestratorTests.cs` from compilation
- **Reason Stated**: "IIngestionJournal interface changes after Prisma.Shared.Contracts deletion"
- **Real Reason**: Avoiding weeks of test refactoring during critical architecture fixes

### 1.2 What Was Really Duplicated

#### Duplicate Interfaces (BEFORE cleanup)

**Orion.HealthChecks/IHealthCheckService.cs**:
```csharp
// Infrastructure layer - WRONG LAYER!
public interface IHealthCheckService
{
    Task<HealthCheckResult> GetHealthAsync(); // Returns custom type
}
```

**Athena.HealthChecks/IHealthCheckService.cs**:
```csharp
// DUPLICATE - Different implementation contract!
public interface IHealthCheckService
{
    Task<HealthCheckResult> CheckHealthAsync(); // DIFFERENT METHOD NAME!
}
```

**ASP.NET Core**:
```csharp
// Type name collision!
public class HealthCheckResult { /* ASP.NET type */ }
```

#### After Consolidation

**Domain/Interfaces/IHealthCheckService.cs**:
```csharp
// Correct layer, single source of truth
public interface IHealthCheckService
{
    Task<OrchestratorHealthStatus> GetHealthAsync(); // Renamed to avoid collision
}
```

**Domain/Interfaces/OrchestratorHealthStatus.cs**:
```csharp
// New name to avoid ASP.NET Core collision
public record OrchestratorHealthStatus(
    OrchestratorHealthState State, // Was: HealthStatus enum
    string Message,
    DateTimeOffset Timestamp);
```

### 1.3 The Circular Dependency Problem

**BEFORE** (commit 54741eb):
```
┌─────────────────────────────────────────────────┐
│ Infrastructure.Extraction                       │
│ ├─ OcrProcessingServiceAdapter.cs               │ ← ADAPTER HERE
│ │  └─ Implements: IOcrProcessingService         │
│ │  └─ Depends on: Application.OcrProcessingService │ ← CIRCULAR!
│ └─ References: Application layer                │ ← VIOLATION!
└─────────────────────────────────────────────────┘
          ↓
┌─────────────────────────────────────────────────┐
│ Application.Services                            │
│ ├─ OcrProcessingService.cs                      │
│ │  └─ Implements: (nothing - just class)        │ ← NOT IMPLEMENTING INTERFACE!
│ └─ References: Domain layer                     │
└─────────────────────────────────────────────────┘
          ↓
┌─────────────────────────────────────────────────┐
│ Domain.Interfaces                               │
│ └─ IOcrProcessingService                        │
└─────────────────────────────────────────────────┘
```

**AFTER**:
```
┌─────────────────────────────────────────────────┐
│ Application.Services                            │
│ ├─ OcrProcessingService.cs                      │
│ │  └─ Implements: IOcrProcessingService         │ ← CORRECT!
│ └─ References: Domain layer ONLY                │
└─────────────────────────────────────────────────┘
          ↓
┌─────────────────────────────────────────────────┐
│ Domain.Interfaces                               │
│ └─ IOcrProcessingService                        │
└─────────────────────────────────────────────────┘

Infrastructure.Extraction → Deleted OcrProcessingServiceAdapter.cs ✅
```

---

## 2. Why Tests Were REALLY Disabled

### 2.1 The Compilation Cascade

When `Prisma.Shared.Contracts` was deleted:

1. **Event Types Moved**: `DocumentDownloadedEvent` moved from `Shared.Contracts` → `Domain.Events`
2. **Namespaces Changed**: All `using Prisma.Shared.Contracts;` broke
3. **Interface Signatures Changed**: `IIngestionJournal` API evolved
4. **Type Consolidation**: Multiple event types merged/renamed
5. **Project References**: 80 files needed reference updates

`IngestionOrchestratorTests.cs` hit ALL of these:
- ❌ Used old `IIngestionJournal.IsDuplicateAsync()` (renamed to `ExistsAsync()`)
- ❌ Referenced `Prisma.Shared.Contracts.DocumentDownloadedEvent` (moved)
- ❌ Used old `RecordAsync(hash, path)` (now takes `IngestionManifestEntry`)
- ❌ Mock setups incompatible with new interface shape
- ❌ Project reference to deleted `Prisma.Shared.Contracts` project

### 2.2 Why NOT Fixed Immediately?

During architecture cleanup, the priority was:
1. ✅ Fix circular dependencies (blocking DI container)
2. ✅ Fix layer violations (blocking build)
3. ✅ Consolidate duplicate interfaces (fixing architecture tests)
4. ✅ Delete `Prisma.Shared.Contracts` (removing anti-pattern)
5. ⏸️ **Refactor all affected tests** ← Too time-consuming, tests disabled instead

**Decision**: Disable 1 test file (8 tests) vs spending hours refactoring during critical fixes.

---

## 3. Current State Analysis

### 3.1 What's Fixed (Architecture-wise)

✅ **No More Duplicate Interfaces**: All in `Domain.Interfaces`
✅ **No Circular Dependencies**: Adapter deleted, proper LSP implementation
✅ **Correct Layer Dependencies**: Infrastructure → Domain ← Application
✅ **No `Prisma.Shared.Contracts`**: Anti-pattern removed
✅ **Type Name Collisions Resolved**: `OrchestratorHealthStatus` vs ASP.NET `HealthCheckResult`
✅ **Event System Consolidated**: All events in `Domain.Events` namespace

### 3.2 What's Still Broken

❌ **IngestionOrchestratorTests.cs**: Disabled, 8 tests not running
❌ **Test Coverage Gap**: Ingestion pipeline not validated
❌ **Mock Setups Outdated**: Tests reference old API signatures
❌ **Project References**: May still reference deleted `Prisma.Shared.Contracts`

### 3.3 Risk Assessment for Restoration

**LOW RISK** ✅:
- Architecture is fixed and stable
- No risk of re-introducing violations
- Tests just need API signature updates

**MEDIUM RISK** ⚠️:
- Tests may reveal bugs in new implementation
- Mock setups need careful validation
- Integration with `FileIngestionJournal` needs testing

**HIGH RISK** ❌:
- None - architecture refactoring is complete

---

## 4. Proper Restoration Strategy (REVISED)

### 4.1 Phase 0: Validate Architecture is Sound (NEW)

**CRITICAL: Verify no regression before enabling tests**

```bash
# 1. Verify no duplicate interfaces exist
cd Prisma/Code/Src/CSharp
grep -r "interface IHealthCheckService" --include="*.cs" | wc -l
# Expected: 1 (only in Domain/Interfaces)

# 2. Verify no circular dependencies
dotnet list package --include-transitive | grep -i "circular"
# Expected: No output

# 3. Verify Prisma.Shared.Contracts is gone
find . -name "Prisma.Shared.Contracts.csproj"
# Expected: No results

# 4. Verify architecture tests pass
cd 04-Tests/06-Architecture/Tests.Architecture
dotnet test --filter "FullyQualifiedName~HexagonalArchitectureTests"
# Expected: All pass
```

### 4.2 Phase 1: Understand New API Contract

**Don't just update tests blindly - understand WHY API changed**

**Old API** (what tests expect):
```csharp
// Primitive-based, hash-only idempotency
public interface IIngestionJournal
{
    Task<bool> IsDuplicateAsync(string hash, CancellationToken ct);
    Task RecordAsync(string hash, string path, CancellationToken ct);
}
```

**New API** (current, after consolidation):
```csharp
// Manifest-based, hash+URL idempotency, richer data model
public interface IIngestionJournal
{
    // ✅ IMPROVED: hash + sourceUrl prevents false positives
    Task<bool> ExistsAsync(string contentHash, string sourceUrl, CancellationToken ct = default);

    // ✅ IMPROVED: Structured entry with full audit trail
    Task RecordAsync(IngestionManifestEntry entry, CancellationToken ct = default);

    // ✅ NEW: Query capability
    Task<IngestionManifestEntry?> GetByFileIdAsync(Guid fileId, CancellationToken ct = default);
}

// ✅ NEW: Rich manifest entry (was primitives)
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

**WHY it changed**:
1. **Better idempotency**: `(hash, sourceUrl)` pair > hash alone
2. **Audit trail**: Full manifest entry > primitives
3. **Queryability**: Can retrieve entries by FileId
4. **Type safety**: Structured record > multiple parameters
5. **Future-proof**: Easy to add fields without breaking API

### 4.3 Phase 2: Update Test Project References

**Before any code changes, fix project references**

```xml
<!-- Prisma.Orion.Ingestion.Tests.csproj -->
<ItemGroup>
  <!-- DELETE THIS if it exists -->
  <ProjectReference Include="..\..\..\..\Prisma.Shared.Contracts\Prisma.Shared.Contracts.csproj" />

  <!-- ENSURE THESE EXIST -->
  <ProjectReference Include="..\..\..\01-Core\Domain\ExxerCube.Prisma.Domain.csproj" />
  <ProjectReference Include="..\..\..\06-Orion\Prisma.Orion.Ingestion\Prisma.Orion.Ingestion.csproj" />
</ItemGroup>
```

### 4.4 Phase 3: Update Using Statements

```csharp
// DELETE (if exists)
using Prisma.Shared.Contracts;

// ADD
using ExxerCube.Prisma.Domain.Events;         // For DocumentDownloadedEvent
using ExxerCube.Prisma.Domain.Interfaces;     // For IIngestionJournal, IngestionManifestEntry
```

### 4.5 Phase 4: Refactor Test Mocks (The Hard Part)

**Example: Test 1 - IngestDocument_NewDocument_ReturnsSuccessAndBroadcastsEvent**

**BEFORE** (broken):
```csharp
[Fact]
public async Task IngestDocument_NewDocument_ReturnsSuccessAndBroadcastsEvent()
{
    // Arrange
    var journal = Substitute.For<IIngestionJournal>();

    // ❌ BROKEN: Method renamed, missing sourceUrl parameter
    journal.IsDuplicateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns(false);

    // ... test execution ...

    // ❌ BROKEN: Different signature, expects IngestionManifestEntry
    await journal.Received(1).RecordAsync(
        Arg.Any<string>(),  // Was: hash
        Arg.Any<string>(),  // Was: path
        Arg.Any<CancellationToken>());
}
```

**AFTER** (fixed, understanding WHY):
```csharp
[Fact]
public async Task IngestDocument_NewDocument_ReturnsSuccessAndBroadcastsEvent()
{
    // Arrange
    var journal = Substitute.For<IIngestionJournal>();

    // ✅ FIXED: New method name + sourceUrl parameter
    // Validates both hash AND URL for better idempotency
    journal.ExistsAsync(
        Arg.Any<string>(),                    // contentHash
        Arg.Any<string>(),                    // sourceUrl (NEW!)
        Arg.Any<CancellationToken>())
        .Returns(false);

    // ... test execution ...

    // ✅ FIXED: Now validates structured manifest entry
    // Ensures rich audit trail is recorded
    await journal.Received(1).RecordAsync(
        Arg.Is<IngestionManifestEntry>(e =>
            e.ContentHash != null &&              // Hash recorded
            e.SourceUrl != null &&                // URL recorded (NEW!)
            e.FileId != Guid.Empty &&             // Valid file ID
            e.CorrelationId == correlationId &&   // Correlation preserved
            e.FileName.EndsWith(".pdf") &&        // Correct file type
            e.StoredPath.Contains(now.Year.ToString())), // Partitioned storage
        Arg.Any<CancellationToken>());
}
```

**Key Changes**:
1. `IsDuplicateAsync()` → `ExistsAsync()` (name clarity)
2. Added `sourceUrl` parameter (better idempotency)
3. `RecordAsync(hash, path)` → `RecordAsync(IngestionManifestEntry)` (structure)
4. Mock verification uses `Arg.Is<T>()` with property validation

### 4.6 Phase 5: Add Tests for New Capabilities (Optional but Recommended)

```csharp
[Fact]
public async Task GetByFileIdAsync_ExistingFile_ReturnsManifestEntry()
{
    // Arrange
    var journal = Substitute.For<IIngestionJournal>();
    var expectedEntry = new IngestionManifestEntry(
        FileId: Guid.NewGuid(),
        FileName: "test.pdf",
        SourceUrl: "http://siara.test/doc123",
        ContentHash: "abc123",
        FileSizeBytes: 1024,
        StoredPath: "/storage/2025/12/04/test.pdf",
        CorrelationId: Guid.NewGuid(),
        DownloadedAt: DateTimeOffset.UtcNow);

    journal.GetByFileIdAsync(expectedEntry.FileId, Arg.Any<CancellationToken>())
        .Returns(expectedEntry);

    // Act
    var result = await journal.GetByFileIdAsync(expectedEntry.FileId, CancellationToken.None);

    // Assert - NEW CAPABILITY: Query by FileId
    result.ShouldNotBeNull();
    result.FileId.ShouldBe(expectedEntry.FileId);
    result.ContentHash.ShouldBe("abc123");
}
```

---

## 5. Acceptance Criteria (REVISED)

### Must Have ✅

**Architecture Validation**:
- [ ] No duplicate interface definitions across projects
- [ ] No circular dependencies (`dotnet list package --include-transitive`)
- [ ] Architecture tests all passing
- [ ] `Prisma.Shared.Contracts` completely deleted

**Test Restoration**:
- [ ] All 8 tests compile successfully
- [ ] All 8 tests pass with green status
- [ ] Tests use `ExistsAsync(hash, sourceUrl)` correctly
- [ ] Tests use `RecordAsync(IngestionManifestEntry)` correctly
- [ ] Mock verifications validate manifest entry properties
- [ ] No references to `Prisma.Shared.Contracts` in test code

**Integration Validation**:
- [ ] `FileIngestionJournal` implementation works with tests
- [ ] Event serialization works with new `IngestionManifestEntry`
- [ ] Correlation IDs preserved end-to-end

### Should Have ✅

- [ ] Tests document WHY API changed (comments explaining manifest-based approach)
- [ ] Test names still accurate (may need renaming if behavior changed)
- [ ] Performance: Tests run in < 2 seconds
- [ ] Coverage: New `GetByFileIdAsync()` method tested

### Nice to Have

- [ ] Integration tests for full ingestion pipeline
- [ ] Performance tests for hash+URL duplicate detection vs hash-only
- [ ] Documentation on migration from primitive to manifest-based journal

---

## 6. Timeline (REALISTIC)

| Phase | Task | Duration | Notes |
|-------|------|----------|-------|
| 0 | Validate architecture is sound | 15 min | Critical - verify no regressions |
| 1 | Understand new API contract | 30 min | Read docs, review implementation |
| 2 | Update project references | 10 min | Remove Shared.Contracts refs |
| 3 | Update using statements | 10 min | Namespace changes |
| 4 | Refactor 8 test mocks | 60-90 min | **Hardest part** - careful validation |
| 5 | Add new capability tests | 30 min | Optional but recommended |
| 6 | Verification & debugging | 30-45 min | Run tests, fix issues |
| **Total** | | **3-4 hours** | NOT 45 minutes! |

---

## 7. Lessons Learned (For Future Reference)

### What Went Wrong

❌ **Duplicate Interfaces**: Same interface name in multiple projects with different contracts
❌ **Shared Contracts Project**: Anti-pattern that led to tight coupling
❌ **Adapter Misuse**: Created circular dependencies instead of solving problems
❌ **Infrastructure Contains Interfaces**: Violated hexagonal architecture
❌ **Type Name Collisions**: Didn't consider ASP.NET Core naming

### What Went Right

✅ **Consolidated to Domain**: Single source of truth for all interfaces
✅ **Deleted Adapter**: Removed circular dependency, proper LSP implementation
✅ **Rich Data Models**: `IngestionManifestEntry` > primitive parameters
✅ **Better Idempotency**: `(hash, sourceUrl)` pair > hash alone
✅ **Architecture Tests**: Caught violations, enforced rules

### Future Prevention

1. **Architecture Tests First**: Run before any major refactoring
2. **No Duplicate Interfaces**: Grep for interface names before creating new ones
3. **Domain-Driven Interfaces**: All abstractions in Domain layer
4. **Avoid Shared Contracts**: Use Domain interfaces instead
5. **Adapter Pattern**: Only when truly needed, never creates circular deps

---

## 8. Risk Mitigation

### If Tests Fail After Restoration

**Scenario 1**: Mock setup errors
- **Symptom**: NSubstitute configuration exceptions
- **Fix**: Verify `Arg.Is<IngestionManifestEntry>()` matchers are correct
- **Rollback**: Re-disable file, investigate interface contract

**Scenario 2**: Integration test failures
- **Symptom**: Tests pass in isolation, fail with real `FileIngestionJournal`
- **Fix**: Verify `FileIngestionJournal` implements new contract correctly
- **Investigate**: Check journal file format, serialization

**Scenario 3**: Event serialization failures
- **Symptom**: `DocumentDownloadedEvent` not deserializing
- **Fix**: Verify event moved to `Domain.Events`, JSON attributes correct
- **Investigate**: Check event persistence in `EventPersistenceWorker`

---

## 9. Next Steps

### Immediate Actions

1. **Run Phase 0 validation** - Ensure architecture is stable
2. **Review this plan with team** - Get buy-in for 3-4 hour effort
3. **Create feature branch** - `feature/restore-ingestion-orchestrator-tests`
4. **Execute phases sequentially** - Don't skip validation steps

### After Restoration

1. **Document migration** - Add to architecture docs
2. **Update HANDOFF_NEXT_SESSION.md** - Mark tests as restored
3. **Run full test suite** - Ensure no regressions
4. **Commit with detailed message** - Explain architectural context

---

## 10. Conclusion

This is **NOT** a simple API signature fix. This is restoring tests after a **major architectural refactoring** that:
- Deleted 1 entire project (`Prisma.Shared.Contracts`)
- Consolidated duplicate interfaces across 3+ projects
- Fixed circular dependencies through adapter deletion
- Moved 83 files and changed 3,000+ lines of code

**Estimated Effort**: 3-4 hours of careful work
**Risk Level**: Medium (architecture fixed, but tests may reveal integration issues)
**Value**: Restores 8 critical tests validating ingestion pipeline integrity

**Recommendation**: Allocate proper time for this. Rushing will create technical debt.

---

**Prepared by**: Claude Code Agent (with architectural context from user)
**Review Status**: READY FOR ARCHITECTURAL REVIEW
**Next Action**: Execute Phase 0 (Architecture Validation)
