# ADR-002: Test Project Split - Clean Architecture Violations and Remediation

**Status:** ✅ **Implemented** (with violations identified and remediation in progress)  
**Date:** 2025-01-15  
**Deciders:** Development Team, Architecture Team  
**Tags:** testing, clean-architecture, hexagonal-architecture, dependency-inversion

**Related ADRs:**
- [ADR-003: Test Suite Split Decision](./adr-003-test-suite-split-decision.md) - Documents the decision to split the test suite
- [ADR-002 Remediation Guide](./adr-002-remediation-guide.md) - **Detailed step-by-step remediation instructions for .NET dev agents**

---

## Context

Following ADR-003, the monolithic `ExxerCube.Prisma.Tests` project was successfully split into 10 separate test projects following clean architecture principles:

1. `Tests.Domain` - Domain layer tests
2. `Tests.Domain.Interfaces` - Interface contract tests (IITDD)
3. `Tests.Application` - Application layer tests
4. `Tests.Infrastructure.Classification` - Classification infrastructure tests
5. `Tests.Infrastructure.Database` - Database infrastructure tests
6. `Tests.Infrastructure.Export` - Export infrastructure tests
7. `Tests.Infrastructure.Extraction` - Extraction infrastructure tests
8. `Tests.Infrastructure.FileStorage` - File storage infrastructure tests
9. `Tests.Infrastructure.FileSystem` - File system infrastructure tests
10. `Tests.EndToEnd` - End-to-end workflow tests

**Goal:** Achieve clean architecture compliance where:
- Domain tests depend only on Domain
- Application tests depend only on Domain + Application (using mocks for Infrastructure)
- Infrastructure tests depend only on their Infrastructure project + Domain
- **No dependencies between Infrastructure projects**

---

## Decision

### Architecture Rules Established

1. **Infrastructure Independence:** Infrastructure projects cannot depend on each other
2. **Interface-Based Construction:** All constructors must accept Domain interfaces, not concrete Infrastructure implementations
3. **Test Isolation:** Application tests must mock Domain interfaces, never instantiate Infrastructure types
4. **Layer Separation:** Tests must only reference types from their corresponding layer and Domain

### Violations Identified

During the test migration, **9 test classes** were identified as violating clean architecture:

#### Tests.Application Violations (7 classes):
1. `DecisionLogicIntegrationTests` - Instantiates `PersonIdentityResolverService`, `LegalDirectiveClassifierService` (Infrastructure.Classification)
2. `MetadataExtractionIntegrationTests` - Instantiates Infrastructure.Extraction types directly
3. `MetadataExtractionPerformanceTests` - Instantiates Infrastructure.Extraction types directly
4. `DocumentIngestionIntegrationTests` - Instantiates `PrismaDbContext`, `FileSystemDownloadStorageAdapter` (Infrastructure.Database, Infrastructure.FileStorage)
5. `FieldMatchingServiceTests` - Instantiates `MatchingPolicyService` (Infrastructure.Classification)
6. `FieldMatchingPerformanceTests` - Instantiates `MatchingPolicyService` (Infrastructure.Classification)
7. `FieldMatchingIntegrationTests` - Instantiates `MatchingPolicyService` (Infrastructure.Classification)

#### Tests.Infrastructure.Database Violations (1 class):
8. `AuditLoggerIntegrationTests` - Instantiates Application services directly

#### Tests.Infrastructure.Export Violations (1 class):
9. `ExportIntegrationTests` - Instantiates Infrastructure.Extraction types directly

---

## Consequences

### Positive Consequences

✅ **Test Discovery:** All 446 tests successfully discovered across 10 projects  
✅ **Architecture Visibility:** Violations are now visible and documented  
✅ **Hard Failures:** Violating tests fail immediately with clear error messages  
✅ **Refactoring Guidance:** Each violation includes specific remediation instructions

### Negative Consequences

⚠️ **9 Test Classes Disabled:** Tests fail hard until refactored (intentional)  
⚠️ **Refactoring Required:** Tests need to be updated to use mocks instead of concrete types  
⚠️ **Migration Incomplete:** Cannot delete monolithic `Tests` project until violations resolved

---

## Remediation Plan

### Phase 1: Application Test Refactoring (Priority: High)

**Goal:** Refactor Application tests to use Domain interface mocks

#### 1.1 DecisionLogicIntegrationTests
- **Current:** `new PersonIdentityResolverService(logger)`, `new LegalDirectiveClassifierService(logger)`
- **Required:** `Substitute.For<IPersonIdentityResolver>()`, `Substitute.For<ILegalDirectiveClassifier>()`
- **Effort:** 2 hours
- **Dependencies:** None

#### 1.2 MetadataExtractionIntegrationTests & MetadataExtractionPerformanceTests
- **Current:** Instantiates `FileTypeIdentifierService`, `XmlMetadataExtractor`, `DocxMetadataExtractor`, `PdfMetadataExtractor`, `CompositeMetadataExtractor`, `FileClassifierService`, `SafeFileNamerService`, `FileMoverService`
- **Required:** Mock `IFileTypeIdentifier`, `IMetadataExtractor`, `IFileClassifier`, `ISafeFileNamer`, `IFileMover`
- **Effort:** 4 hours
- **Dependencies:** None

#### 1.3 DocumentIngestionIntegrationTests
- **Current:** Instantiates `PrismaDbContext`, `DownloadTrackerService`, `FileMetadataLoggerService`, `FileSystemDownloadStorageAdapter`
- **Required:** Mock `IDownloadTracker`, `IFileMetadataLogger`, `IDownloadStorage`
- **Effort:** 3 hours
- **Dependencies:** None

#### 1.4 FieldMatchingServiceTests, FieldMatchingPerformanceTests, FieldMatchingIntegrationTests
- **Current:** `new MatchingPolicyService(options, logger)`
- **Required:** `Substitute.For<IMatchingPolicy>()`
- **Effort:** 2 hours
- **Dependencies:** None

**Total Phase 1 Effort:** ~11 hours

### Phase 2: Infrastructure Test Refactoring (Priority: Medium)

#### 2.1 AuditLoggerIntegrationTests (Tests.Infrastructure.Database)
- **Current:** Instantiates `DocumentIngestionService`, `MetadataExtractionService`, `DecisionLogicService`, `ExportService`
- **Options:**
  - **Option A:** Move to `Tests.Application` (recommended)
  - **Option B:** Mock Application service interfaces
- **Effort:** 2 hours
- **Dependencies:** None

#### 2.2 ExportIntegrationTests (Tests.Infrastructure.Export)
- **Current:** Instantiates `CompositeMetadataExtractor`, `XmlMetadataExtractor`, `DocxMetadataExtractor`, `PdfMetadataExtractor`
- **Options:**
  - **Option A:** Move to `Tests.Infrastructure.Extraction` (recommended)
  - **Option B:** Mock `IMetadataExtractor` interface
- **Effort:** 2 hours
- **Dependencies:** None

**Total Phase 2 Effort:** ~4 hours

### Phase 3: Cleanup (Priority: Low)

#### 3.1 Delete Monolithic Tests Project
- **Prerequisite:** All violations resolved, all tests migrated
- **Effort:** 1 hour
- **Dependencies:** Phase 1 & 2 complete

**Total Remediation Effort:** ~16 hours

---

## Implementation Status

### ✅ Completed

- [x] Test project split (10 projects created)
- [x] Dependency cleanup (removed cross-layer dependencies)
- [x] Violation identification (9 classes documented)
- [x] Hard-fail mechanism (tests fail with clear error messages)
- [x] Deprecated test removal (PrismaOcrWrapperAdapter tests deleted)

### 🔄 In Progress

- [ ] Application test refactoring (Phase 1)
- [ ] Infrastructure test refactoring (Phase 2)

### ⏳ Pending

- [ ] Delete monolithic `Tests` project (Phase 3)

---

## References

- **[📋 ADR-002 Remediation Guide](./adr-002-remediation-guide.md)** - **Detailed step-by-step instructions for fixing all 9 violations**
- [ADR-003: Test Suite Split Decision](./adr-003-test-suite-split-decision.md)
- [Clean Architecture Patterns](../.cursor/rules/1008_CleanArchitecturePatterns.mdc)
- [Domain-Driven Design Patterns](../.cursor/rules/1007_DomainDrivenDesignPatterns.mdc)
- [C# Coding Standards](../.cursor/rules/1001_CSharpCodingStandards.mdc)
- [Test Project Separation Plan](../../docs/qa/test-project-separation-plan.md)

---

## Notes

- All violating tests include `⚠️ REFACTORING REQUIRED ⚠️` banners in XML documentation
- Constructors throw `InvalidOperationException` with specific remediation instructions
- Test count: 446 tests discovered (483 original - 37 deprecated PrismaOcrWrapperAdapter tests)
- Failing tests: 8 tests (intentional failures until refactored)

