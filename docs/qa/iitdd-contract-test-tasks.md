# IITDD Contract Test Tasks - Stories 1.3-1.8

**Created:** 2025-01-15  
**Purpose:** Add missing IITDD contract test tasks to maintain consistency with Stories 1.1, 1.2, and 1.9  
**Status:** Ready for Integration into implementation-tasks.md

---

## Story 1.3: Field Matching and Unified Metadata Generation

### Task 1.3.1A: Create IITDD Contract Tests for Field Matching Interfaces
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**⚠️ IITDD REQUIREMENT:** Contract tests MUST be written BEFORE any adapter implementations. These tests validate interface contracts using mocks and must pass for ANY valid implementation.

**Description:** Create IITDD contract tests for all Stage 3 field matching interfaces. These tests validate the interface contracts (WHAT) using mocks, not implementation details (HOW). Tests must be in `Interfaces/` folder and use naming pattern `II{InterfaceName}Tests.cs`.

**Tasks:**
- [ ] Create test project structure: `ExxerCube.Prisma.Tests/Interfaces/` folder (if not exists)
- [ ] Create `IIFieldExtractorTests.cs` in `Interfaces/` folder
  - Test contract: `ExtractFieldsAsync` returns `Result<ExtractedFields>` on success
  - Test contract: `ExtractFieldsAsync` returns failure `Result` on extraction errors
  - Test contract: `ExtractFieldAsync` returns `Result<FieldValue>` for valid field name
  - Test contract: `ExtractFieldAsync` returns failure `Result` on invalid field name
  - Test contract: Generic `IFieldExtractor<T>` supports multiple source types
  - Use NSubstitute mocks to test interface contracts, NOT implementation details
- [ ] Create `IIFieldMatcherTests.cs` in `Interfaces/` folder
  - Test contract: `MatchFieldsAsync` returns `Result<MatchedFields>` with field matches
  - Test contract: `MatchFieldsAsync` returns failure `Result` on matching errors
  - Test contract: `GenerateUnifiedRecordAsync` returns `Result<UnifiedMetadataRecord>` on success
  - Test contract: `GenerateUnifiedRecordAsync` returns failure `Result` on invalid matched fields
  - Test contract: `ValidateMatchResultAsync` returns `Result<ValidationResult>` with validation status
  - Test contract: `ValidateMatchResultAsync` returns failure `Result` on validation errors
  - Use NSubstitute mocks to test interface contracts

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** All contract tests are in `Tests/Interfaces/` folder (IITDD structure)
- ⚠️ **CRITICAL:** Tests use mocks (NSubstitute) to validate interface contracts, NOT implementation details
- ⚠️ **CRITICAL:** Tests validate WHAT the interface promises, not HOW it's implemented
- Tests follow naming pattern: `II{InterfaceName}Tests.cs`
- Tests validate Liskov Substitution Principle (contracts must work for ANY valid implementation)
- All interface methods have contract tests covering success and failure scenarios
- Tests use Shouldly for assertions
- Tests follow xUnit v3 patterns

**Dependencies:** Task 1.3.1, Task 1.3.2 (Domain Interfaces must exist before contract tests)

---

## Story 1.4: Identity Resolution and Legal Directive Classification

### Task 1.4.1A: Create IITDD Contract Tests for Identity Resolution Interfaces
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**⚠️ IITDD REQUIREMENT:** Contract tests MUST be written BEFORE any adapter implementations. These tests validate interface contracts using mocks and must pass for ANY valid implementation.

**Description:** Create IITDD contract tests for all Stage 3 identity resolution and legal classification interfaces. These tests validate the interface contracts (WHAT) using mocks, not implementation details (HOW). Tests must be in `Interfaces/` folder and use naming pattern `II{InterfaceName}Tests.cs`.

**Tasks:**
- [ ] Create `IIPersonIdentityResolverTests.cs` in `Interfaces/` folder
  - Test contract: `ResolveIdentityAsync` returns `Result<ResolvedIdentity>` on success
  - Test contract: `ResolveIdentityAsync` returns failure `Result` on resolution errors
  - Test contract: `DeduplicateRecordsAsync` returns `Result<List<ConsolidatedPersonRecord>>` with deduplicated records
  - Test contract: `DeduplicateRecordsAsync` returns failure `Result` on deduplication errors
  - Test contract: `FindIdentityVariantsAsync` returns `Result<List<PersonIdentityVariant>>` for valid RFC
  - Test contract: `FindIdentityVariantsAsync` returns failure `Result` on invalid RFC or query errors
  - Use NSubstitute mocks to test interface contracts
- [ ] Create `IILegalDirectiveClassifierTests.cs` in `Interfaces/` folder
  - Test contract: `ClassifyDirectiveAsync` returns `Result<LegalDirective>` on success
  - Test contract: `ClassifyDirectiveAsync` returns failure `Result` on classification errors
  - Test contract: `DetectLegalInstrumentsAsync` returns `Result<List<LegalInstrument>>` with detected instruments
  - Test contract: `DetectLegalInstrumentsAsync` returns failure `Result` on detection errors
  - Test contract: `MapToComplianceActionAsync` returns `Result<ComplianceAction>` based on directive
  - Test contract: `MapToComplianceActionAsync` returns failure `Result` on mapping errors
  - Use NSubstitute mocks to test interface contracts

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** All contract tests are in `Tests/Interfaces/` folder (IITDD structure)
- ⚠️ **CRITICAL:** Tests use mocks (NSubstitute) to validate interface contracts, NOT implementation details
- ⚠️ **CRITICAL:** Tests validate WHAT the interface promises, not HOW it's implemented
- Tests follow naming pattern: `II{InterfaceName}Tests.cs`
- Tests validate Liskov Substitution Principle (contracts must work for ANY valid implementation)
- All interface methods have contract tests covering success and failure scenarios
- Tests use Shouldly for assertions
- Tests follow xUnit v3 patterns

**Dependencies:** Task 1.4.1 (Domain Interfaces must exist before contract tests)

---

## Story 1.5: SLA Tracking and Escalation Management

### Task 1.5.1A: Create IITDD Contract Tests for SLA Interfaces
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**⚠️ IITDD REQUIREMENT:** Contract tests MUST be written BEFORE any adapter implementations. These tests validate interface contracts using mocks and must pass for ANY valid implementation.

**Description:** Create IITDD contract tests for SLA tracking interface. These tests validate the interface contract (WHAT) using mocks, not implementation details (HOW). Tests must be in `Interfaces/` folder and use naming pattern `II{InterfaceName}Tests.cs`.

**Tasks:**
- [ ] Create `IISLAEnforcerTests.cs` in `Interfaces/` folder
  - Test contract: `CalculateDeadlineAsync` returns `Result<DateTime>` with calculated deadline
  - Test contract: `CalculateDeadlineAsync` returns failure `Result` on invalid input (negative days, invalid date)
  - Test contract: `CheckAndEscalateAsync` returns `Result<EscalationStatus>` with escalation status
  - Test contract: `CheckAndEscalateAsync` returns failure `Result` on escalation errors
  - Test contract: `GetSlaStatusAsync` returns `Result<SLAStatus>` for valid file ID
  - Test contract: `GetSlaStatusAsync` returns failure `Result` on invalid file ID or query errors
  - Test contract: `GetFilesAtRiskAsync` returns `Result<List<FileSlaStatus>>` filtered by threshold
  - Test contract: `GetFilesAtRiskAsync` returns failure `Result` on query errors
  - Use NSubstitute mocks to test interface contracts

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** All contract tests are in `Tests/Interfaces/` folder (IITDD structure)
- ⚠️ **CRITICAL:** Tests use mocks (NSubstitute) to validate interface contracts, NOT implementation details
- ⚠️ **CRITICAL:** Tests validate WHAT the interface promises, not HOW it's implemented
- Tests follow naming pattern: `IISLAEnforcerTests.cs`
- Tests validate Liskov Substitution Principle (contracts must work for ANY valid implementation)
- All interface methods have contract tests covering success and failure scenarios
- Tests use Shouldly for assertions
- Tests follow xUnit v3 patterns

**Dependencies:** Task 1.5.1 (Domain Interface must exist before contract tests)

---

## Story 1.6: Manual Review Interface

### Task 1.6.1A: Create IITDD Contract Tests for Manual Review Interfaces
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**⚠️ IITDD REQUIREMENT:** Contract tests MUST be written BEFORE any adapter implementations. These tests validate interface contracts using mocks and must pass for ANY valid implementation.

**Description:** Create IITDD contract tests for manual review interface. These tests validate the interface contract (WHAT) using mocks, not implementation details (HOW). Tests must be in `Interfaces/` folder and use naming pattern `II{InterfaceName}Tests.cs`.

**Tasks:**
- [ ] Create `IIManualReviewerPanelTests.cs` in `Interfaces/` folder
  - Test contract: `GetReviewCasesAsync` returns `Result<List<ReviewCase>>` filtered by filters
  - Test contract: `GetReviewCasesAsync` returns failure `Result` on query errors
  - Test contract: `SubmitReviewDecisionAsync` returns `Result` (success/failure only - IsSuccess sufficient)
  - Test contract: `SubmitReviewDecisionAsync` returns failure `Result` on invalid case ID or save errors
  - Test contract: `GetFieldAnnotationsAsync` returns `Result<FieldAnnotations>` for valid case ID
  - Test contract: `GetFieldAnnotationsAsync` returns failure `Result` on invalid case ID or query errors
  - Use NSubstitute mocks to test interface contracts

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** All contract tests are in `Tests/Interfaces/` folder (IITDD structure)
- ⚠️ **CRITICAL:** Tests use mocks (NSubstitute) to validate interface contracts, NOT implementation details
- ⚠️ **CRITICAL:** Tests validate WHAT the interface promises, not HOW it's implemented
- Tests follow naming pattern: `IIManualReviewerPanelTests.cs`
- Tests validate Liskov Substitution Principle (contracts must work for ANY valid implementation)
- All interface methods have contract tests covering success and failure scenarios
- Tests use Shouldly for assertions
- Tests follow xUnit v3 patterns

**Dependencies:** Task 1.6.1 (Domain Interface must exist before contract tests)

---

## Story 1.7: SIRO-Compliant Export Generation

### Task 1.7.1A: Create IITDD Contract Tests for Export Interfaces
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**⚠️ IITDD REQUIREMENT:** Contract tests MUST be written BEFORE any adapter implementations. These tests validate interface contracts using mocks and must pass for ANY valid implementation.

**Description:** Create IITDD contract tests for all export interfaces. These tests validate the interface contracts (WHAT) using mocks, not implementation details (HOW). Tests must be in `Interfaces/` folder and use naming pattern `II{InterfaceName}Tests.cs`.

**Tasks:**
- [ ] Create `IIResponseExporterTests.cs` in `Interfaces/` folder
  - Test contract: `ExportSiroXmlAsync` returns `Result<string>` with output path on success
  - Test contract: `ExportSiroXmlAsync` returns failure `Result` on export errors (invalid metadata, file system errors)
  - Test contract: `ExportSignedPdfAsync` returns `Result<string>` with output path on success
  - Test contract: `ExportSignedPdfAsync` returns failure `Result` on signing errors (certificate issues, PDF generation errors)
  - Test contract: `MapToRegulatorySchemaAsync` returns `Result<RegulatoryData>` with mapped data
  - Test contract: `MapToRegulatorySchemaAsync` returns failure `Result` on mapping errors
  - Test contract: `ValidateAgainstSchemaAsync` returns `Result<ValidationResult>` with validation status
  - Test contract: `ValidateAgainstSchemaAsync` returns failure `Result` on validation errors
  - Use NSubstitute mocks to test interface contracts
- [ ] Create `IILayoutGeneratorTests.cs` in `Interfaces/` folder
  - Test contract: `GenerateExcelLayoutAsync` returns `Result<string>` with output path on success
  - Test contract: `GenerateExcelLayoutAsync` returns failure `Result` on generation errors
  - Test contract: `GenerateExcelLayoutBatchAsync` returns `Result<string>` with output path for batch
  - Test contract: `GenerateExcelLayoutBatchAsync` returns failure `Result` on batch generation errors
  - Test contract: `ValidateLayoutSchemaAsync` returns `Result<bool>` indicating schema validity
  - Test contract: `ValidateLayoutSchemaAsync` returns failure `Result` on validation errors
  - Use NSubstitute mocks to test interface contracts

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** All contract tests are in `Tests/Interfaces/` folder (IITDD structure)
- ⚠️ **CRITICAL:** Tests use mocks (NSubstitute) to validate interface contracts, NOT implementation details
- ⚠️ **CRITICAL:** Tests validate WHAT the interface promises, not HOW it's implemented
- Tests follow naming pattern: `II{InterfaceName}Tests.cs`
- Tests validate Liskov Substitution Principle (contracts must work for ANY valid implementation)
- All interface methods have contract tests covering success and failure scenarios
- Tests use Shouldly for assertions
- Tests follow xUnit v3 patterns

**Dependencies:** Task 1.7.1 (Domain Interfaces must exist before contract tests)

---

## Story 1.8: PDF Summarization and Digital Signing

### Task 1.8.1A: Create IITDD Contract Tests for PDF Summarization Interfaces
**Priority:** P1  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**⚠️ IITDD REQUIREMENT:** Contract tests MUST be written BEFORE any adapter implementations. These tests validate interface contracts using mocks and must pass for ANY valid implementation.

**Description:** Create IITDD contract tests for PDF summarization interfaces. These tests validate the interface contracts (WHAT) using mocks, not implementation details (HOW). Tests must be in `Interfaces/` folder and use naming pattern `II{InterfaceName}Tests.cs`.

**Tasks:**
- [ ] Create `IIPdfRequirementSummarizerTests.cs` in `Interfaces/` folder
  - Test contract: `SummarizeRequirementsAsync` returns `Result<RequirementSummary>` on success
  - Test contract: `SummarizeRequirementsAsync` returns failure `Result` on summarization errors (invalid PDF, processing errors)
  - Test contract: `ClassifyRequirementsAsync` returns `Result<ClassifiedRequirements>` with classified requirements
  - Test contract: `ClassifyRequirementsAsync` returns failure `Result` on classification errors
  - Test contract: `GetRequirementConfidenceAsync` returns `Result<RequirementConfidenceScores>` with confidence scores
  - Test contract: `GetRequirementConfidenceAsync` returns failure `Result` on confidence calculation errors
  - Use NSubstitute mocks to test interface contracts
- [ ] Create `IICriterionMapperTests.cs` in `Interfaces/` folder
  - Test contract: `MapToCategoriesAsync` returns `Result<List<MappedCategory>>` with mapped categories
  - Test contract: `MapToCategoriesAsync` returns failure `Result` on mapping errors
  - Test contract: `LoadCriterionConfigAsync` returns `Result<CriterionMappingConfig>` with loaded config
  - Test contract: `LoadCriterionConfigAsync` returns failure `Result` on config load errors (file not found, invalid format)
  - Test contract: `ValidateCriterionConfigAsync` returns `Result<bool>` indicating config validity
  - Test contract: `ValidateCriterionConfigAsync` returns failure `Result` on validation errors
  - Use NSubstitute mocks to test interface contracts

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** All contract tests are in `Tests/Interfaces/` folder (IITDD structure)
- ⚠️ **CRITICAL:** Tests use mocks (NSubstitute) to validate interface contracts, NOT implementation details
- ⚠️ **CRITICAL:** Tests validate WHAT the interface promises, not HOW it's implemented
- Tests follow naming pattern: `II{InterfaceName}Tests.cs`
- Tests validate Liskov Substitution Principle (contracts must work for ANY valid implementation)
- All interface methods have contract tests covering success and failure scenarios
- Tests use Shouldly for assertions
- Tests follow xUnit v3 patterns

**Dependencies:** Task 1.8.1 (Domain Interfaces must exist before contract tests)

---

## Integration Instructions

These tasks should be inserted into `implementation-tasks.md` immediately after their corresponding interface definition tasks:

1. **Task 1.3.1A** → Insert after Task 1.3.2 (after field matcher interface is defined)
2. **Task 1.4.1A** → Insert after Task 1.4.1 (after identity resolution interfaces are defined)
3. **Task 1.5.1A** → Insert after Task 1.5.1 (after SLA interface is defined)
4. **Task 1.6.1A** → Insert after Task 1.6.1 (after manual review interface is defined)
5. **Task 1.7.1A** → Insert after Task 1.7.1 (after export interfaces are defined)
6. **Task 1.8.1A** → Insert after Task 1.8.1 (after PDF summarization interfaces are defined)

**Note:** These contract test tasks MUST be completed BEFORE their corresponding adapter implementation tasks to maintain IITDD principles (interface-first, contract-based development).

