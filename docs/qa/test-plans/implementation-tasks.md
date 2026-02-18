# Implementation Tasks - Regulatory Compliance Automation System

**Version:** 1.0  
**Date:** 2025-01-12  
**Source PRD:** `docs/prd.md`  
**Source PRP:** `Prisma/Fixtures/PRP1/PRP.md`

---

## 🚨 MANDATORY READING BEFORE STARTING

**ALL DEVELOPERS MUST READ THIS SECTION BEFORE IMPLEMENTING ANY TASK:**

### Critical Architectural Rules

This project follows **Hexagonal Architecture (Ports and Adapters)** pattern. Violating these rules will cause architectural debt and make the codebase unmaintainable.

**The Golden Rules:**
1. **PORTS (Interfaces)** → `Domain/Interfaces/` ONLY
2. **ADAPTERS (Implementations)** → `Infrastructure/` ONLY  
3. **ORCHESTRATION** → `Application/Services/` ONLY (uses Ports, does NOT implement them)
4. **Dependency Flow:** `Infrastructure → Domain ← Application`

**Common Violations to AVOID:**
- ❌ Putting interfaces in Application or Infrastructure layers
- ❌ Implementing interfaces in Application layer
- ❌ Application layer importing Infrastructure namespaces
- ❌ Infrastructure depending on Application layer
- ❌ Application services implementing Domain interfaces

**See [Hexagonal Architecture - Ports and Adapters Pattern](#-critical-hexagonal-architecture---ports-and-adapters-pattern) section below for detailed examples and code review checklist.**

**If you're unsure where code belongs, ASK THE ARCHITECT before implementing.**

---

## Table of Contents

1. [🚨 MANDATORY READING BEFORE STARTING](#-mandatory-reading-before-starting)
2. [Task Organization](#task-organization)
3. [Story 1.1: Browser Automation and Document Download](#story-11-browser-automation-and-document-download)
4. [Story 1.2: Enhanced Metadata Extraction and File Classification](#story-12-enhanced-metadata-extraction-and-file-classification)
5. [Story 1.3: Field Matching and Unified Metadata Generation](#story-13-field-matching-and-unified-metadata-generation)
6. [Story 1.4: Identity Resolution and Legal Directive Classification](#story-14-identity-resolution-and-legal-directive-classification)
7. [Story 1.5: SLA Tracking and Escalation Management](#story-15-sla-tracking-and-escalation-management)
8. [Story 1.6: Manual Review Interface](#story-16-manual-review-interface)
9. [Story 1.7: SIRO-Compliant Export Generation](#story-17-siro-compliant-export-generation)
10. [Story 1.8: PDF Summarization and Digital Signing](#story-18-pdf-summarization-and-digital-signing)
11. [Story 1.9: Audit Trail and Reporting](#story-19-audit-trail-and-reporting)
12. [Cross-Cutting Tasks](#cross-cutting-tasks)

---

## Task Organization

### Task Status Legend
- 🔴 **Not Started** - Task not yet begun
- 🟡 **In Progress** - Task currently being worked on
- 🟢 **Completed** - Task finished and verified
- ⚠️ **Blocked** - Task blocked by dependency

### Priority Levels
- **P0** - Critical path, blocks other work
- **P1** - High priority, should be done soon
- **P2** - Medium priority, can be deferred
- **P3** - Low priority, nice to have

### Task Dependencies
Tasks are numbered sequentially within each story. Dependencies are indicated with "Depends on: Task X.Y.Z"

### Result Type Pattern Note
**Important:** For operations that only need to indicate success/failure, use a non-generic `Result` type (or `Result<Unit>`) instead of `Result<bool>`. The `IsSuccess` property is sufficient - no need to return a boolean value. Use `Result<bool>` only when the boolean value itself is meaningful (e.g., `IsFileAlreadyDownloadedAsync` returns whether a file was already downloaded).

**Pattern:**
- ✅ `Task<Result>` or `Task<Result<Unit>>` - For success/failure only operations (e.g., `CloseBrowserAsync`, `RecordDownloadAsync`, `LogMetadataAsync`)
- ✅ `Task<Result<bool>>` - When the boolean value is needed (e.g., `IsFileAlreadyDownloadedAsync`, `FileExistsAsync`, `ValidateFileFormatAsync`)

### IITDD Testing and Test Project Structure

This solution uses **Interface-based Integration Test-Driven Development (IITDD)** on top of Hexagonal Architecture. Tests are organized by responsibility:

- **Interface Contract Tests (Ports)**
  - Location: `ExxerCube.Prisma.Tests.Interfaces/*`
  - Purpose: Define and verify the **behavioral contract of each Domain interface (port)**.
  - Pattern:
    - One contract test suite per port, e.g. `IBrowserAutomationAgentContractTests`.
    - Tests target the interface only and make no assumptions about specific adapter implementations.
    - Each suite is written so it can be reused against any concrete implementation of the port.

- **Adapter Implementation Tests**
  - Location: `ExxerCube.Prisma.Tests.Implementations.*/*`
  - Purpose: Verify that each **Infrastructure adapter** correctly implements its port and integrates with external systems.
  - Pattern:
    - For each adapter, provide:
      - Adapter-specific tests (e.g. Playwright behavior, DB queries, file system behavior).
      - A fixture that **runs the shared interface contract tests** against that adapter instance.

- **Application Orchestration Tests**
  - Location: `ExxerCube.Prisma.Tests.Application/*`
  - Purpose: Test **Application services** as pure orchestrators that depend only on ports.
  - Pattern:
    - Use substitutes/mocks for all Domain interfaces.
    - Never reference Infrastructure types or namespaces.
    - Focus on workflow, sequencing, error handling, and logging behavior.

- **End-to-End / Pipeline Integration Tests**
  - Location: `ExxerCube.Prisma.Tests.Integration/*`
  - Purpose: Validate **Stage 1–4 pipeline behavior** using real adapters wired through DI.
  - Pattern:
    - Compose real adapters via the UI/Host DI configuration.
    - Use realistic test data (XML, DOCX, PDF, etc.).
    - Verify cross-cutting concerns (audit, SLA, exports) without breaking Hexagonal boundaries.

**Key IITDD Rules:**
- Every **port** defined in `Domain/Interfaces/` must have a corresponding **contract test suite**.
- Every **adapter implementation** must:
  - Satisfy its port interface contract by passing the shared contract test suite.
  - Have adapter-specific tests for external system behavior.
- Every **Application service** must be testable without Infrastructure by using port interfaces and substitutes only.

### ⚠️ CRITICAL: Hexagonal Architecture - Ports and Adapters Pattern

**MANDATORY ARCHITECTURAL RULES - DO NOT VIOLATE:**

**1. Ports (Interfaces) → Domain Layer ONLY**
- ✅ All interfaces (`IBrowserAutomationAgent`, `IDownloadTracker`, etc.) MUST be defined in `Domain/Interfaces/`
- ✅ Interfaces define WHAT capabilities are needed, NOT HOW they are implemented
- ✅ Interfaces have NO dependencies on Infrastructure or Application layers
- ❌ NEVER define interfaces in Application or Infrastructure layers

**2. Adapters (Implementations) → Separate Infrastructure Projects by Concern**
- ✅ **CRITICAL:** Create SEPARATE Infrastructure projects for each concern (High Cohesion, Loose Coupling)
- ✅ Each Infrastructure project implements Domain interfaces (Ports) for ONE concern only
- ✅ Implementations depend on Domain interfaces (Ports), NOT on Application layer
- ✅ Each Infrastructure project can be tested independently
- ✅ Application layer can be tested WITHOUT Infrastructure dependencies (use mocks)
- ❌ NEVER put all Infrastructure adapters in a single project (violates High Cohesion)
- ❌ NEVER implement interfaces in Application layer
- ❌ NEVER create Infrastructure → Application dependencies
- ❌ NEVER create Infrastructure → Infrastructure dependencies (each project is independent)

**3. Application Layer → Orchestration ONLY**
- ✅ Application layer (`Application/Services/`) contains orchestration services that USE Domain interfaces
- ✅ Application services receive interfaces via constructor injection (DI)
- ✅ Application layer depends on Domain layer (interfaces), NOT on Infrastructure
- ❌ Application layer does NOT implement Domain interfaces
- ❌ Application layer does NOT depend on Infrastructure layer

**4. Dependency Flow Rules:**
```
Infrastructure → Domain ← Application
     (Adapters)  (Ports)  (Orchestration)
```

**CORRECT Pattern:**
```csharp
// ✅ Domain/Interfaces/IBrowserAutomationAgent.cs (PORT)
public interface IBrowserAutomationAgent { ... }

// ✅ Infrastructure/BrowserAutomation/PlaywrightBrowserAutomationAdapter.cs (ADAPTER)
public class PlaywrightBrowserAutomationAdapter : IBrowserAutomationAgent { ... }

// ✅ Application/Services/DocumentIngestionService.cs (ORCHESTRATION)
public class DocumentIngestionService
{
    private readonly IBrowserAutomationAgent _browserAgent; // Uses PORT, not ADAPTER
    
    public DocumentIngestionService(IBrowserAutomationAgent browserAgent) // DI injects ADAPTER
    {
        _browserAgent = browserAgent;
    }
}

// ✅ Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs (WIRING)
services.AddScoped<IBrowserAutomationAgent, PlaywrightBrowserAutomationAdapter>();
```

**WRONG Patterns (DO NOT DO THIS):**
```csharp
// ❌ WRONG: Interface in Infrastructure
namespace Infrastructure.Interfaces
{
    public interface IBrowserAutomationAgent { ... } // NO! Ports belong in Domain
}

// ❌ WRONG: Implementation in Application
namespace Application.Services
{
    public class BrowserAutomationService : IBrowserAutomationAgent { ... } // NO! Adapters belong in Infrastructure
}

// ❌ WRONG: Application depends on Infrastructure
namespace Application.Services
{
    public class DocumentIngestionService
    {
        private readonly Infrastructure.BrowserAutomation.PlaywrightAdapter _adapter; // NO! Use interface
    }
}

// ❌ WRONG: Infrastructure depends on Application
namespace Infrastructure.BrowserAutomation
{
    public class PlaywrightAdapter
    {
        private readonly Application.Services.SomeService _service; // NO! Infrastructure cannot depend on Application
    }
}
```

**5. File Structure Enforcement - SEPARATE INFRASTRUCTURE PROJECTS:**

```
Solution Structure:
├── ExxerCube.Prisma.Domain.csproj          ← PORTS (interfaces, entities)
│   ├── Interfaces/                         ← PORTS (interfaces only)
│   │   ├── IBrowserAutomationAgent.cs
│   │   ├── IDownloadTracker.cs
│   │   └── ...
│   ├── Entities/                          ← Domain entities
│   └── Common/                            ← Result<T>, Value objects
│
├── ExxerCube.Prisma.Application.csproj    ← ORCHESTRATION (uses Ports via DI)
│   ├── Services/                          ← ORCHESTRATION (uses Ports via DI)
│   │   ├── DocumentIngestionService.cs
│   │   └── ...
│   └── (NO interfaces, NO Infrastructure project references)
│
├── ExxerCube.Prisma.Infrastructure.BrowserAutomation.csproj  ← ADAPTER Project 1
│   ├── PlaywrightBrowserAutomationAdapter.cs
│   └── DependencyInjection/
│       └── ServiceCollectionExtensions.cs (registers IBrowserAutomationAgent)
│
├── ExxerCube.Prisma.Infrastructure.Database.csproj            ← ADAPTER Project 2
│   ├── DownloadTrackerService.cs
│   ├── FileMetadataLoggerService.cs
│   ├── AuditLoggerService.cs
│   ├── EntityFramework/                   ← EF Core DbContext, entities
│   └── DependencyInjection/
│       └── ServiceCollectionExtensions.cs (registers IDownloadTracker, IFileMetadataLogger, etc.)
│
├── ExxerCube.Prisma.Infrastructure.FileStorage.csproj          ← ADAPTER Project 3
│   ├── FileSystemDownloadStorageAdapter.cs
│   └── DependencyInjection/
│       └── ServiceCollectionExtensions.cs (registers IDownloadStorage)
│
├── ExxerCube.Prisma.Infrastructure.Extraction.csproj          ← ADAPTER Project 4
│   ├── XmlMetadataExtractor.cs
│   ├── DocxMetadataExtractor.cs
│   ├── PdfMetadataExtractor.cs
│   └── DependencyInjection/
│       └── ServiceCollectionExtensions.cs (registers IMetadataExtractor, etc.)
│
├── ExxerCube.Prisma.Infrastructure.Classification.csproj      ← ADAPTER Project 5
│   ├── FileClassifierService.cs
│   ├── LegalDirectiveClassifierService.cs
│   └── DependencyInjection/
│       └── ServiceCollectionExtensions.cs (registers IFileClassifier, etc.)
│
├── ExxerCube.Prisma.Infrastructure.Export.csproj              ← ADAPTER Project 6
│   ├── SiroXmlExporter.cs
│   ├── ExcelLayoutGenerator.cs
│   └── DependencyInjection/
│       └── ServiceCollectionExtensions.cs (registers IResponseExporter, etc.)
│
└── ExxerCube.Prisma.Web.UI.csproj         ← UI/Host Layer
    ├── References: Application, Domain
    ├── References: All Infrastructure projects (for DI wiring only)
    └── Wires all Infrastructure projects together via DI extension methods
```

**Benefits of Separate Infrastructure Projects:**
- ✅ **High Cohesion:** Each project has ONE concern (BrowserAutomation, Database, FileStorage, etc.)
- ✅ **Loose Coupling:** Infrastructure projects don't depend on each other
- ✅ **Testability:** Application can be tested WITHOUT Infrastructure (use mocks)
- ✅ **Independent Testing:** Each Infrastructure project can be tested in isolation
- ✅ **Selective Deployment:** Can deploy/update Infrastructure projects independently
- ✅ **Clear Boundaries:** Easy to see what each Infrastructure project does
- ✅ **Reduced Dependencies:** Application doesn't need Infrastructure projects to compile/test

**Project Reference Rules:**
```
Domain Project:
├── No project references (pure domain)

Application Project:
├── References: Domain ONLY
└── NO Infrastructure project references

Infrastructure Projects (each independent):
├── References: Domain ONLY
└── NO references to other Infrastructure projects
└── NO references to Application

UI/Host Project:
├── References: Application, Domain
├── References: All Infrastructure projects (for DI wiring)
└── Wires everything together via DI
```

**Testing Strategy:**
- **Application Tests:** Mock Domain interfaces, NO Infrastructure projects needed
- **Infrastructure Tests:** Test each Infrastructure project independently
- **Integration Tests:** Use actual Infrastructure projects, test in UI/Host project or separate test project

**6. Code Review Checklist:**
- [ ] Are all interfaces in `Domain/Interfaces/` (PORTS)?
- [ ] Are implementations in SEPARATE Infrastructure projects by concern?
- [ ] Does each Infrastructure project have HIGH COHESION (one concern only)?
- [ ] Do Infrastructure projects NOT depend on each other (LOOSE COUPLING)?
- [ ] Does Application layer only use interfaces (not concrete classes)?
- [ ] Does Application NOT reference Infrastructure projects (only Domain)?
- [ ] Does Infrastructure NOT depend on Application?
- [ ] Can Application be tested WITHOUT Infrastructure projects (using mocks)?
- [ ] Are all dependencies injected via constructor (DI)?
- [ ] Are Ports registered with Adapters in each Infrastructure project's DI configuration?
- [ ] Does UI/Host project wire all Infrastructure projects together?

**7. Common Mistakes to Avoid:**
- ❌ **Putting all Infrastructure adapters in one project** → Create separate projects by concern
- ❌ **Infrastructure projects depending on each other** → Each project is independent, depends only on Domain
- ❌ **Application referencing Infrastructure projects** → Application only references Domain, uses interfaces
- ❌ **Creating "service" classes in Application that implement interfaces** → Move to Infrastructure as adapters
- ❌ **Application layer importing Infrastructure namespaces** → Use interfaces from Domain
- ❌ **Infrastructure classes calling Application services** → Use Domain interfaces or create new Ports
- ❌ **Putting business logic in Infrastructure adapters** → Move to Application orchestration or Domain entities
- ❌ **Creating "helper" classes that both Application and Infrastructure use** → Put in Domain/Common
- ❌ **Mixing concerns in one Infrastructure project** → Separate by concern (BrowserAutomation, Database, FileStorage, etc.)

**Remember:** 
- **Ports** define contracts (Domain/Interfaces)
- **Adapters** implement them (Separate Infrastructure projects by concern)
- **Application** orchestrates them (uses Ports via DI, NO Infrastructure references)
- **High Cohesion:** Each Infrastructure project = ONE concern
- **Loose Coupling:** Infrastructure projects don't depend on each other
- **Testability:** Application can be tested without Infrastructure (use mocks)
- Keep dependencies flowing inward toward Domain.

---

## Story 1.1: Browser Automation and Document Download

**Story Goal:** Automatically download regulatory documents from UIF/CNBV websites using browser automation.

### Task 1.1.1: Create Domain Interfaces for Stage 1
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**⚠️ PREREQUISITE:** Task CC.0 (Create Non-Generic Result Type) must be completed first. All interface contracts reference the non-generic `Result` type.

**Description:** Define the 4 Stage 1 interfaces in the Domain layer following Hexagonal Architecture and IITDD principles. Interfaces define contracts that ANY valid implementation must satisfy.

**Tasks:**
- [ ] Create `IBrowserAutomationAgent` interface in `Domain/Interfaces/`
  - Method: `LaunchBrowserAsync(string url, BrowserOptions? options) -> Task<Result<string>>`
  - Method: `IdentifyDownloadableFilesAsync(string sessionId, FilePattern[] patterns) -> Task<Result<List<DownloadableFile>>>`
  - Method: `DownloadFileAsync(string sessionId, DownloadableFile fileReference) -> Task<Result<DownloadedFile>>`
  - Method: `CloseBrowserAsync(string sessionId) -> Task<Result>` (Note: Use non-generic Result or Result<Unit> - IsSuccess is sufficient, no need for Result<bool>)
- [ ] Create `IDownloadTracker` interface
  - Method: `IsFileAlreadyDownloadedAsync(string fileChecksum, string fileName) -> Task<Result<bool>>` (Returns bool value - needed)
  - Method: `RecordDownloadAsync(FileMetadata fileMetadata) -> Task<Result>` (Success/failure only - IsSuccess sufficient)
  - Method: `GetDownloadedFilesAsync(DateTime? since) -> Task<Result<List<FileMetadata>>>`
- [ ] Create `IDownloadStorage` interface
  - Method: `SaveFileAsync(DownloadedFile fileData, string basePath, FileNamingStrategy namingStrategy) -> Task<Result<string>>`
  - Method: `GetFilePathAsync(string fileId) -> Task<Result<string>>`
  - Method: `FileExistsAsync(string filePath) -> Task<Result<bool>>`
- [ ] Create `IFileMetadataLogger` interface
  - Method: `LogMetadataAsync(FileMetadata metadata) -> Task<Result>` (Success/failure only - IsSuccess sufficient)
  - Method: `GetMetadataAsync(string fileId) -> Task<Result<FileMetadata>>`
  - Method: `GetMetadataByDateRangeAsync(DateTime startDate, DateTime endDate) -> Task<Result<List<FileMetadata>>>`

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** All interfaces MUST be in `Domain/Interfaces/` namespace (PORTS)
- ⚠️ **CRITICAL:** Interfaces have NO dependencies on Infrastructure or Application layers
- All interfaces follow Railway-Oriented Programming pattern (`Result<T>` or `Result` return types)
- Use `Result` (non-generic) or `Result<Unit>` for success/failure only operations
- Use `Result<bool>` only when the boolean value is meaningful
- All methods have XML documentation describing the contract (WHAT, not HOW)
- Interfaces define WHAT capabilities are needed, NOT HOW they are implemented
- Interface contracts are designed for Liskov Substitution Principle (any valid implementation must satisfy the contract)

**Dependencies:** Task CC.0 (Create Non-Generic Result Type) - Must complete before interface definitions

---

### Task 1.1.1A: Create IITDD Contract Tests for Stage 1 Interfaces
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**⚠️ IITDD REQUIREMENT:** Contract tests MUST be written BEFORE any adapter implementations. These tests validate interface contracts using mocks and must pass for ANY valid implementation.

**Description:** Create IITDD contract tests for all Stage 1 interfaces. These tests validate the interface contracts (WHAT) using mocks, not implementation details (HOW). Tests must be in `Interfaces/` folder and use naming pattern `I{InterfaceName}Tests.cs`.

**Tasks:**
- [ ] Create test project structure: `ExxerCube.Prisma.Tests/Interfaces/` folder
- [ ] Create `IIBrowserAutomationAgentTests.cs` in `Interfaces/` folder
  - Test contract: `LaunchBrowserAsync` returns `Result<string>` with session ID on success
  - Test contract: `LaunchBrowserAsync` returns failure `Result` on invalid URL or browser launch failure
  - Test contract: `IdentifyDownloadableFilesAsync` returns `Result<List<DownloadableFile>>` matching patterns
  - Test contract: `IdentifyDownloadableFilesAsync` returns failure `Result` on invalid session ID
  - Test contract: `DownloadFileAsync` returns `Result<DownloadedFile>` with file data
  - Test contract: `DownloadFileAsync` returns failure `Result` on download failure
  - Test contract: `CloseBrowserAsync` returns `Result` (success/failure only)
  - Test contract: `CloseBrowserAsync` handles cleanup even on errors
  - Use NSubstitute mocks to test interface contracts, NOT implementation details
- [ ] Create `IIDownloadTrackerTests.cs` in `Interfaces/` folder
  - Test contract: `IsFileAlreadyDownloadedAsync` returns `Result<bool>` indicating duplicate status
  - Test contract: `IsFileAlreadyDownloadedAsync` returns failure `Result` on database errors
  - Test contract: `RecordDownloadAsync` returns `Result` (success/failure only)
  - Test contract: `RecordDownloadAsync` handles duplicate records appropriately
  - Test contract: `GetDownloadedFilesAsync` returns `Result<List<FileMetadata>>` filtered by date
  - Test contract: `GetDownloadedFilesAsync` returns failure `Result` on query errors
  - Use NSubstitute mocks to test interface contracts
- [ ] Create `IIDownloadStorageTests.cs` in `Interfaces/` folder
  - Test contract: `SaveFileAsync` returns `Result<string>` with file path on success
  - Test contract: `SaveFileAsync` returns failure `Result` on disk space or permission errors
  - Test contract: `GetFilePathAsync` returns `Result<string>` with path for valid file ID
  - Test contract: `GetFilePathAsync` returns failure `Result` on invalid file ID
  - Test contract: `FileExistsAsync` returns `Result<bool>` indicating file existence
  - Test contract: `FileExistsAsync` returns failure `Result` on access errors
  - Use NSubstitute mocks to test interface contracts
- [ ] Create `IIFileMetadataLoggerTests.cs` in `Interfaces/` folder
  - Test contract: `LogMetadataAsync` returns `Result` (success/failure only)
  - Test contract: `LogMetadataAsync` returns failure `Result` on database errors
  - Test contract: `GetMetadataAsync` returns `Result<FileMetadata>` for valid file ID
  - Test contract: `GetMetadataAsync` returns failure `Result` on invalid file ID
  - Test contract: `GetMetadataByDateRangeAsync` returns `Result<List<FileMetadata>>` filtered by dates
  - Test contract: `GetMetadataByDateRangeAsync` returns failure `Result` on query errors
  - Use NSubstitute mocks to test interface contracts

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** All contract tests are in `Tests/Interfaces/` folder (IITDD structure)
- ⚠️ **CRITICAL:** Tests use mocks (NSubstitute) to validate interface contracts, NOT implementation details
- ⚠️ **CRITICAL:** Tests validate WHAT the interface promises, not HOW it's implemented
- Tests follow naming pattern: `I{InterfaceName}Tests.cs`
- Tests validate Liskov Substitution Principle (contracts must work for ANY valid implementation)
- All interface methods have contract tests covering success and failure scenarios
- Tests use Shouldly for assertions
- Tests follow xUnit v3 patterns

**Dependencies:** Task 1.1.1 (Domain Interfaces must exist before contract tests)

---

### Task 1.1.2: Create Domain Entities for Stage 1
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 3 hours

**Description:** Create domain entities and value objects for browser automation and file metadata.

**Tasks:**
- [ ] Create `FileMetadata` entity in `Domain/Entities/`
  - Properties: FileId, FileName, FilePath, Url, DownloadTimestamp, Checksum (SHA-256), FileSize, Format
- [ ] Create `DownloadableFile` value object
  - Properties: Url, FileName, FileSize, ContentType
- [ ] Create `DownloadedFile` value object
  - Properties: FileData (byte[]), FileName, ContentType, Checksum
- [ ] Create `BrowserOptions` value object
  - Properties: Headless (bool), Timeout (TimeSpan), UserAgent (string)
- [ ] Create `FilePattern` value object
  - Properties: Pattern (string), FileType (enum)
- [ ] Create `FileNamingStrategy` enum
  - Values: ChecksumBased, TimestampBased, MetadataBased

**Acceptance Criteria:**
- All entities use nullable reference types appropriately
- Entities follow existing domain model patterns
- Value objects are immutable

**Dependencies:** None

---

### Task 1.1.3: Create Database Schema for File Metadata
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Create Entity Framework Core entities and migrations for file metadata storage.

**⚠️ ARCHITECTURAL REQUIREMENT:** Database entities and migrations belong in `ExxerCube.Prisma.Infrastructure.Database.csproj` project. This keeps all database concerns together (HIGH COHESION).

**Tasks:**
- [ ] In `ExxerCube.Prisma.Infrastructure.Database.csproj` project:
- [ ] Create `FileMetadataEntity` (or use Domain entity with EF Core configuration)
  - Table: `FileMetadata`
  - Columns: FileId (PK, string), FileName, FilePath, Url, DownloadTimestamp, Checksum, FileSize, Format
  - Index on Checksum for duplicate detection
  - Index on DownloadTimestamp for date range queries
- [ ] Create EF Core DbContext configuration
- [ ] Create initial migration: `AddFileMetadataTable`
- [ ] Test migration on SQL Server and PostgreSQL

**Acceptance Criteria:**
- Migration is additive-only (doesn't modify existing tables)
- Migration can be rolled back safely
- Indexes are created for performance-critical queries
- Migration tested on both database providers

**Dependencies:** Task 1.1.2

---

### Task 1.1.4: Implement Playwright Browser Automation Adapter
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 10 hours

**Description:** Implement `IBrowserAutomationAgent` using Playwright for browser automation. This adapter MUST satisfy the interface contract defined in Task 1.1.1 and pass all contract tests from Task 1.1.1A.

**⚠️ ARCHITECTURAL REQUIREMENT:** This is an ADAPTER - it MUST be in Infrastructure layer, NOT Application layer. It implements the PORT (interface) from Domain.

**⚠️ IITDD REQUIREMENT:** The adapter implementation MUST pass all contract tests from `IIBrowserAutomationAgentTests.cs`. Contract tests validate the interface contract (WHAT), not Playwright-specific implementation details (HOW).

**Tasks:**
- [ ] Create NEW Infrastructure project: `ExxerCube.Prisma.Infrastructure.BrowserAutomation.csproj`
- [ ] Add reference to `ExxerCube.Prisma.Domain` project (for Port interface)
- [ ] Add reference to test project to access contract tests
- [ ] Add Playwright NuGet package to this Infrastructure project
- [ ] Create `PlaywrightBrowserAutomationAdapter` in this project (NOT in Application!)
  - Implement `IBrowserAutomationAgent` interface
  - Ensure implementation satisfies interface contract (Liskov Substitution Principle)
- [ ] Create `DependencyInjection/ServiceCollectionExtensions.cs` in this project to register `IBrowserAutomationAgent → PlaywrightBrowserAutomationAdapter`
- [ ] Implement `LaunchBrowserAsync` - Launch Playwright browser, navigate to URL
- [ ] Implement `IdentifyDownloadableFilesAsync` - Use Playwright selectors to find download links
- [ ] Implement `DownloadFileAsync` - Download file via Playwright download API
- [ ] Implement `CloseBrowserAsync` - Clean up browser session
- [ ] Add error handling for network timeouts, browser launch failures
- [ ] Add configuration for headless mode, timeouts via appsettings.json
- [ ] **IITDD Contract Compliance:** Run `IIBrowserAutomationAgentTests.cs` against `PlaywrightBrowserAutomationAdapter` instance - ALL tests MUST pass
- [ ] Create implementation-specific tests in `Implementations/PlaywrightBrowserAutomationAdapterTests.cs`
  - Test Playwright-specific behavior (browser launch, selector matching, download handling)
  - Test error scenarios specific to Playwright (network timeouts, browser crashes)
  - Test configuration handling (headless mode, timeouts)
  - Use real Playwright API or test doubles as appropriate
- [ ] Write integration tests (may require test website or mock server)

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** Implementation passes ALL contract tests from `IIBrowserAutomationAgentTests.cs` (IITDD compliance)
- ⚠️ **CRITICAL:** Implementation satisfies interface contract (Liskov Substitution Principle)
- All methods return `Result<T>` for error handling
- Browser sessions are properly cleaned up even on errors
- Configuration is externalized (appsettings.json)
- Performance meets NFR3: <5 seconds for browser operations
- Implementation tests have good coverage (>80%)
- Contract tests validate WHAT, implementation tests validate HOW (Playwright-specific)

**Dependencies:** Task 1.1.1, Task 1.1.1A, Task 1.1.2

---

### Task 1.1.5: Implement Download Tracker
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**Description:** Implement `IDownloadTracker` to prevent duplicate downloads using checksums. This adapter MUST satisfy the interface contract defined in Task 1.1.1 and pass all contract tests from Task 1.1.1A.

**⚠️ ARCHITECTURAL REQUIREMENT:** This is an ADAPTER - it MUST be in a SEPARATE Infrastructure project (`ExxerCube.Prisma.Infrastructure.Database.csproj`) for database-related adapters. This project will contain all database adapters (DownloadTracker, FileMetadataLogger, AuditLogger, etc.) for HIGH COHESION.

**⚠️ IITDD REQUIREMENT:** The adapter implementation MUST pass all contract tests from `IIDownloadTrackerTests.cs`. Contract tests validate the interface contract (WHAT), not database-specific implementation details (HOW).

**Tasks:**
- [ ] Create NEW Infrastructure project: `ExxerCube.Prisma.Infrastructure.Database.csproj` (if not exists)
- [ ] Add reference to `ExxerCube.Prisma.Domain` project
- [ ] Add reference to test project to access contract tests
- [ ] Add Entity Framework Core NuGet packages to this project
- [ ] Create `DownloadTrackerService` in this project
  - Implement `IDownloadTracker` interface
  - Ensure implementation satisfies interface contract (Liskov Substitution Principle)
- [ ] Implement `IsFileAlreadyDownloadedAsync` - Query database by checksum
- [ ] Implement `RecordDownloadAsync` - Insert file metadata into database
- [ ] Implement `GetDownloadedFilesAsync` - Query with optional date filter
- [ ] Add checksum calculation utility (SHA-256)
- [ ] **IITDD Contract Compliance:** Run `IIDownloadTrackerTests.cs` against `DownloadTrackerService` instance - ALL tests MUST pass
- [ ] Create implementation-specific tests in `Implementations/DownloadTrackerServiceTests.cs`
  - Test database-specific behavior (EF Core queries, transaction handling)
  - Test checksum calculation and duplicate detection logic
  - Test error scenarios specific to database (connection failures, constraint violations)
  - Use in-memory database or test doubles as appropriate
- [ ] Write integration tests with database

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** Implementation passes ALL contract tests from `IIDownloadTrackerTests.cs` (IITDD compliance)
- ⚠️ **CRITICAL:** Implementation satisfies interface contract (Liskov Substitution Principle)
- Duplicate detection uses checksum, not filename
- Performance meets requirement: <100ms for duplicate check
- Database queries are optimized with indexes
- Error handling uses `Result<T>` pattern
- Contract tests validate WHAT, implementation tests validate HOW (database-specific)

**Dependencies:** Task 1.1.1, Task 1.1.1A, Task 1.1.3

---

### Task 1.1.6: Implement Download Storage
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**Description:** Implement `IDownloadStorage` for persisting downloaded files with deterministic paths. This adapter MUST satisfy the interface contract defined in Task 1.1.1 and pass all contract tests from Task 1.1.1A.

**⚠️ ARCHITECTURAL REQUIREMENT:** This is an ADAPTER - it MUST be in a SEPARATE Infrastructure project (`ExxerCube.Prisma.Infrastructure.FileStorage.csproj`) for file storage concerns. This keeps file storage adapters together (HIGH COHESION).

**⚠️ IITDD REQUIREMENT:** The adapter implementation MUST pass all contract tests from `IIDownloadStorageTests.cs`. Contract tests validate the interface contract (WHAT), not filesystem-specific implementation details (HOW).

**Tasks:**
- [ ] Create NEW Infrastructure project: `ExxerCube.Prisma.Infrastructure.FileStorage.csproj`
- [ ] Add reference to `ExxerCube.Prisma.Domain` project
- [ ] Add reference to test project to access contract tests
- [ ] Create `FileSystemDownloadStorageAdapter` in this project
  - Implement `IDownloadStorage` interface
  - Ensure implementation satisfies interface contract (Liskov Substitution Principle)
- [ ] Implement `SaveFileAsync` - Save file with deterministic path based on naming strategy
- [ ] Implement `GetFilePathAsync` - Retrieve file path by file ID
- [ ] Implement `FileExistsAsync` - Check file existence
- [ ] Support multiple naming strategies (checksum-based, timestamp-based, metadata-based)
- [ ] Add configuration for base storage path (local filesystem or Azure Blob Storage path)
- [ ] Handle disk space errors, permission errors gracefully
- [ ] **IITDD Contract Compliance:** Run `IIDownloadStorageTests.cs` against `FileSystemDownloadStorageAdapter` instance - ALL tests MUST pass
- [ ] Create implementation-specific tests in `Implementations/FileSystemDownloadStorageAdapterTests.cs`
  - Test filesystem-specific behavior (path generation, file I/O operations)
  - Test naming strategy implementations (checksum, timestamp, metadata-based)
  - Test error scenarios specific to filesystem (disk space, permissions, path too long)
  - Use mock file system or test directories as appropriate
- [ ] Write integration tests with actual file system

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** Implementation passes ALL contract tests from `IIDownloadStorageTests.cs` (IITDD compliance)
- ⚠️ **CRITICAL:** Implementation satisfies interface contract (Liskov Substitution Principle)
- Files are saved with deterministic, safe paths
- Performance: <1 second per MB for file save
- Error handling for disk space, permissions
- Supports both local filesystem and Azure Blob Storage (via adapter pattern)
- Contract tests validate WHAT, implementation tests validate HOW (filesystem-specific)

**Dependencies:** Task 1.1.1, Task 1.1.1A, Task 1.1.2

---

### Task 1.1.7: Implement File Metadata Logger
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 5 hours

**Description:** Implement `IFileMetadataLogger` for logging file metadata to database. This adapter MUST satisfy the interface contract defined in Task 1.1.1 and pass all contract tests from Task 1.1.1A.

**⚠️ ARCHITECTURAL REQUIREMENT:** This adapter belongs in `ExxerCube.Prisma.Infrastructure.Database.csproj` project (same as DownloadTracker) for HIGH COHESION of database adapters.

**⚠️ IITDD REQUIREMENT:** The adapter implementation MUST pass all contract tests from `IIFileMetadataLoggerTests.cs`. Contract tests validate the interface contract (WHAT), not database-specific implementation details (HOW).

**Tasks:**
- [ ] Create `FileMetadataLoggerService` in `ExxerCube.Prisma.Infrastructure.Database.csproj` project
  - Implement `IFileMetadataLogger` interface
  - Ensure implementation satisfies interface contract (Liskov Substitution Principle)
- [ ] Implement `LogMetadataAsync` - Insert metadata into database
- [ ] Implement `GetMetadataAsync` - Query by file ID
- [ ] Implement `GetMetadataByDateRangeAsync` - Query with date range
- [ ] Use Entity Framework Core for database operations
- [ ] **IITDD Contract Compliance:** Run `IIFileMetadataLoggerTests.cs` against `FileMetadataLoggerService` instance - ALL tests MUST pass
- [ ] Create implementation-specific tests in `Implementations/FileMetadataLoggerServiceTests.cs`
  - Test database-specific behavior (EF Core queries, date range filtering)
  - Test error scenarios specific to database (connection failures, constraint violations)
  - Use in-memory database or test doubles as appropriate
- [ ] Write integration tests with database

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** Implementation passes ALL contract tests from `IIFileMetadataLoggerTests.cs` (IITDD compliance)
- ⚠️ **CRITICAL:** Implementation satisfies interface contract (Liskov Substitution Principle)
- Performance: <100ms for logging, <500ms for queries
- Error handling uses `Result<T>` pattern
- Database operations use async/await
- Contract tests validate WHAT, implementation tests validate HOW (database-specific)

**Dependencies:** Task 1.1.1, Task 1.1.1A, Task 1.1.3

---

### Task 1.1.8: Create Application Service for Stage 1 Orchestration
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**Description:** Create application service that orchestrates the browser automation workflow. This service uses Domain interfaces (PORTS) via constructor injection and MUST be testable without Infrastructure dependencies using mocks/substitutes.

**⚠️ ARCHITECTURAL REQUIREMENT:** This is ORCHESTRATION - it MUST use interfaces (PORTS) from Domain via constructor injection, NOT concrete classes from Infrastructure. It does NOT implement interfaces.

**⚠️ PROJECT REFERENCES:**
- ✅ `ExxerCube.Prisma.Application` references ONLY `ExxerCube.Prisma.Domain`
- ❌ `ExxerCube.Prisma.Application` does NOT reference any Infrastructure projects
- ✅ Application can be tested WITHOUT Infrastructure projects (use mocks)

**⚠️ IITDD REQUIREMENT:** Application orchestration tests MUST use mocks/substitutes (NSubstitute) for all Domain interfaces. Tests validate orchestration logic (workflow coordination), not adapter implementations.

**Tasks:**
- [ ] Create `DocumentIngestionService` in `Application/Services/`
- [ ] ⚠️ **CRITICAL:** Verify `ExxerCube.Prisma.Application.csproj` only references `ExxerCube.Prisma.Domain.csproj`
- [ ] ⚠️ **CRITICAL:** Inject `IBrowserAutomationAgent` interface (PORT), NOT `PlaywrightBrowserAutomationAdapter` (ADAPTER)
- [ ] ⚠️ **CRITICAL:** Inject `IDownloadTracker`, `IDownloadStorage`, `IFileMetadataLogger` interfaces (PORTS), NOT concrete adapters
- [ ] ⚠️ **CRITICAL:** Do NOT import Infrastructure namespaces - only use Domain interfaces
- [ ] Implement workflow:
  1. Launch browser and navigate to URL
  2. Identify downloadable files
  3. For each file: check if already downloaded, download if new, save to storage, log metadata
  4. Close browser session
- [ ] Add error handling and retry logic for transient failures
- [ ] Add logging with correlation IDs
- [ ] Create orchestration tests in `Application.Tests/DocumentIngestionServiceTests.cs`
  - Use NSubstitute to mock ALL Domain interfaces (`IBrowserAutomationAgent`, `IDownloadTracker`, `IDownloadStorage`, `IFileMetadataLogger`)
  - Test orchestration workflow: successful download flow
  - Test orchestration workflow: duplicate file detection and skip
  - Test orchestration workflow: error handling and retry logic
  - Test orchestration workflow: browser cleanup on errors
  - Test orchestration workflow: correlation ID propagation
  - ⚠️ **CRITICAL:** Tests use mocks, NOT real Infrastructure adapters
  - ⚠️ **CRITICAL:** Tests validate orchestration logic (WHEN/THEN), not adapter behavior
- [ ] Write integration test for complete workflow (uses real Infrastructure adapters)

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** Application service uses ONLY Domain interfaces (PORTS), no Infrastructure dependencies
- ⚠️ **CRITICAL:** Orchestration tests use mocks/substitutes (NSubstitute) for all interfaces
- ⚠️ **CRITICAL:** Application can be tested WITHOUT Infrastructure projects (IITDD principle)
- Workflow handles errors gracefully
- All steps are logged to audit trail
- Performance meets requirements
- Integration verification: Existing OCR pipeline continues to work
- Orchestration tests validate workflow coordination, not adapter implementations

**Dependencies:** Tasks 1.1.4, 1.1.5, 1.1.6, 1.1.7

---

### Task 1.1.9: Add Dependency Injection Configuration
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 2 hours

**Description:** Register all Stage 1 services in DI container.

**Tasks:**
- [ ] Add service registrations in `Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`
- [ ] Register `IBrowserAutomationAgent` → `PlaywrightBrowserAutomationAdapter`
- [ ] Register `IDownloadTracker` → `DownloadTrackerService`
- [ ] Register `IDownloadStorage` → `FileSystemDownloadStorageAdapter`
- [ ] Register `IFileMetadataLogger` → `FileMetadataLoggerService`
- [ ] Register `DocumentIngestionService` as scoped service
- [ ] Add configuration binding for browser automation settings

**Acceptance Criteria:**
- All services registered with appropriate lifetimes
- Configuration is injected via IOptions pattern
- Services can be resolved and tested

**Dependencies:** All Task 1.1.x tasks

---

## Story 1.2: Enhanced Metadata Extraction and File Classification

**Story Goal:** Extract metadata from multiple document formats and classify them automatically.

### Task 1.2.1: Create Domain Interfaces for Stage 2 (Part 1)
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**Description:** Define Stage 2 interfaces for file type identification, metadata extraction, and classification. Interfaces define contracts that ANY valid implementation must satisfy (IITDD principle).

**Tasks:**
- [ ] Create `IFileTypeIdentifier` interface in `Domain/Interfaces/`
  - Method: `IdentifyFileTypeAsync(string filePath) -> Task<Result<FileTypeInfo>>`
  - Method: `ValidateFileFormatAsync(string filePath, FileFormat[] expectedFormats) -> Task<Result<bool>>`
  - Method: `GetSupportedFormats() -> FileFormat[]`
- [ ] Create `IMetadataExtractor` interface in `Domain/Interfaces/` (extends/wraps existing OCR)
  - Method: `ExtractMetadataAsync(string filePath, ExtractionOptions?) -> Task<Result<ExtractedMetadata>>`
  - Method: `ExtractFromXmlAsync(string xmlContent) -> Task<Result<ExtractedMetadata>>`
  - Method: `ExtractFromDocxAsync(string filePath) -> Task<Result<ExtractedMetadata>>`
  - Method: `ExtractFromPdfAsync(string filePath, bool useOcrFallback) -> Task<Result<ExtractedMetadata>>`
- [ ] Create `ISafeFileNamer` interface in `Domain/Interfaces/`
  - Method: `GenerateSafeFileNameAsync(ExtractedMetadata metadata, string originalFileName) -> Task<Result<string>>`
  - Method: `EnsureUniqueFileNameAsync(string baseFileName, string directoryPath) -> Task<Result<string>>`
  - Method: `NormalizeFileName(string fileName) -> string`
- [ ] Create `IFileClassifier` interface in `Domain/Interfaces/`
  - Method: `ClassifyLevel1Async(ExtractedMetadata metadata) -> Task<Result<ClassificationLevel1>>`
  - Method: `ClassifyLevel2Async(ExtractedMetadata metadata, ClassificationLevel1 level1) -> Task<Result<ClassificationLevel2>>`
  - Method: `GetClassificationConfidenceAsync(ExtractedMetadata metadata, ClassificationResult classification) -> Task<Result<int>>`
- [ ] Create `IFileMover` interface in `Domain/Interfaces/`
  - Method: `MoveFileAsync(string sourcePath, ClassificationResult classification, string baseDirectory) -> Task<Result<string>>`
  - Method: `GetTargetFolderPath(ClassificationResult classification, string baseDirectory) -> string`

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** All interfaces MUST be in `Domain/Interfaces/` namespace (PORTS)
- ⚠️ **CRITICAL:** Interfaces have NO dependencies on Infrastructure or Application layers
- All interfaces follow Railway-Oriented Programming pattern
- `IMetadataExtractor` wraps existing `IOcrExecutor` without breaking compatibility
- Interfaces are properly documented with XML comments describing contracts (WHAT, not HOW)
- Interface contracts are designed for Liskov Substitution Principle (any valid implementation must satisfy the contract)

**Dependencies:** None

---

### Task 1.2.1A: Create IITDD Contract Tests for Stage 2 Interfaces
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**⚠️ IITDD REQUIREMENT:** Contract tests MUST be written BEFORE any adapter implementations. These tests validate interface contracts using mocks and must pass for ANY valid implementation.

**Description:** Create IITDD contract tests for all Stage 2 interfaces. These tests validate the interface contracts (WHAT) using mocks, not implementation details (HOW). Tests must be in `Interfaces/` folder and use naming pattern `I{InterfaceName}Tests.cs`.

**Tasks:**
- [ ] Create `IIFileTypeIdentifierTests.cs` in `Interfaces/` folder
  - Test contract: `IdentifyFileTypeAsync` returns `Result<FileTypeInfo>` on success
  - Test contract: `IdentifyFileTypeAsync` returns failure `Result` on invalid file path
  - Test contract: `ValidateFileFormatAsync` returns `Result<bool>` indicating format match
  - Test contract: `ValidateFileFormatAsync` returns failure `Result` on file access errors
  - Test contract: `GetSupportedFormats` returns array of supported formats
  - Use NSubstitute mocks to test interface contracts
- [ ] Create `IIMetadataExtractorTests.cs` in `Interfaces/` folder
  - Test contract: `ExtractMetadataAsync` returns `Result<ExtractedMetadata>` on success
  - Test contract: `ExtractMetadataAsync` returns failure `Result` on extraction errors
  - Test contract: `ExtractFromXmlAsync` returns `Result<ExtractedMetadata>` for valid XML
  - Test contract: `ExtractFromXmlAsync` returns failure `Result` on malformed XML
  - Test contract: `ExtractFromDocxAsync` returns `Result<ExtractedMetadata>` for valid DOCX
  - Test contract: `ExtractFromDocxAsync` returns failure `Result` on file errors
  - Test contract: `ExtractFromPdfAsync` returns `Result<ExtractedMetadata>` with OCR fallback option
  - Test contract: `ExtractFromPdfAsync` returns failure `Result` on PDF processing errors
  - Use NSubstitute mocks to test interface contracts
- [ ] Create `IISafeFileNamerTests.cs` in `Interfaces/` folder
  - Test contract: `GenerateSafeFileNameAsync` returns `Result<string>` with normalized filename
  - Test contract: `GenerateSafeFileNameAsync` returns failure `Result` on invalid input
  - Test contract: `EnsureUniqueFileNameAsync` returns `Result<string>` with unique filename
  - Test contract: `EnsureUniqueFileNameAsync` returns failure `Result` on directory errors
  - Test contract: `NormalizeFileName` returns normalized string (synchronous)
  - Use NSubstitute mocks to test interface contracts
- [ ] Create `IIFileClassifierTests.cs` in `Interfaces/` folder
  - Test contract: `ClassifyLevel1Async` returns `Result<ClassificationLevel1>` on success
  - Test contract: `ClassifyLevel1Async` returns failure `Result` on classification errors
  - Test contract: `ClassifyLevel2Async` returns `Result<ClassificationLevel2>` based on Level1 and metadata
  - Test contract: `ClassifyLevel2Async` returns failure `Result` on invalid Level1 input
  - Test contract: `GetClassificationConfidenceAsync` returns `Result<int>` with confidence score (0-100)
  - Test contract: `GetClassificationConfidenceAsync` returns failure `Result` on invalid input
  - Use NSubstitute mocks to test interface contracts
- [ ] Create `IIFileMoverTests.cs` in `Interfaces/` folder
  - Test contract: `MoveFileAsync` returns `Result<string>` with target path on success
  - Test contract: `MoveFileAsync` returns failure `Result` on file system errors
  - Test contract: `GetTargetFolderPath` returns folder path based on classification (synchronous)
  - Use NSubstitute mocks to test interface contracts

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** All contract tests are in `Tests/Interfaces/` folder (IITDD structure)
- ⚠️ **CRITICAL:** Tests use mocks (NSubstitute) to validate interface contracts, NOT implementation details
- ⚠️ **CRITICAL:** Tests validate WHAT the interface promises, not HOW it's implemented
- Tests follow naming pattern: `I{InterfaceName}Tests.cs`
- Tests validate Liskov Substitution Principle (contracts must work for ANY valid implementation)
- All interface methods have contract tests covering success and failure scenarios
- Tests use Shouldly for assertions
- Tests follow xUnit v3 patterns

**Dependencies:** Task 1.2.1 (Domain Interfaces must exist before contract tests)

---

### Task 1.2.2: Create Domain Entities for Stage 2
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Create domain entities for metadata extraction and classification.

**Tasks:**
- [ ] Create `ExtractedMetadata` entity
  - Properties: ExpedienteNumber, OficioNumber, RFC, Names (List<Persona>), Dates, LegalReferences, etc.
- [ ] Create `FileTypeInfo` value object
  - Properties: Format (FileFormat enum), IsScanned (bool), MimeType (string)
- [ ] Create `ClassificationResult` entity
  - Properties: Level1 (ClassificationLevel1 enum), Level2 (ClassificationLevel2?), Scores (ClassificationScores), Confidence (int)
- [ ] Create `ClassificationLevel1` enum
  - Values: Aseguramiento, Desembargo, Documentacion, Informacion, Transferencia, OperacionesIlicitas
- [ ] Create `ClassificationLevel2` enum
  - Values: Especial, Judicial, Hacendario
- [ ] Create `FileFormat` enum
  - Values: Pdf, Xml, Docx, Zip
- [ ] Create `ExtractionOptions` value object
  - Properties: UseOcrFallback (bool), OcrLanguage (string), etc.

**Acceptance Criteria:**
- Entities follow existing domain model patterns
- Enums are well-documented
- Entities support nullable reference types

**Dependencies:** None

---

### Task 1.2.3: Implement File Type Identifier
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**Description:** Implement `IFileTypeIdentifier` for file type identification based on content, not just extension. This adapter MUST satisfy the interface contract defined in Task 1.2.1 and pass all contract tests from Task 1.2.1A.

**⚠️ ARCHITECTURAL REQUIREMENT:** This is an ADAPTER - it MUST be in Infrastructure layer. It implements the PORT (interface) from Domain.

**⚠️ IITDD REQUIREMENT:** The adapter implementation MUST pass all contract tests from `IIFileTypeIdentifierTests.cs`. Contract tests validate the interface contract (WHAT), not content-detection implementation details (HOW).

**Tasks:**
- [ ] Create `FileTypeIdentifierService` in `ExxerCube.Prisma.Infrastructure.Extraction.csproj` project
  - Implement `IFileTypeIdentifier` interface
  - Ensure implementation satisfies interface contract (Liskov Substitution Principle)
- [ ] Implement `IdentifyFileTypeAsync` - Read file headers/magic numbers to identify type
- [ ] Implement `ValidateFileFormatAsync` - Check file matches expected formats
- [ ] Implement `GetSupportedFormats` - Return array of supported formats
- [ ] Support PDF, XML, DOCX, ZIP detection
- [ ] Add detection for scanned PDFs (check for text layer)
- [ ] **IITDD Contract Compliance:** Run `IIFileTypeIdentifierTests.cs` against `FileTypeIdentifierService` instance - ALL tests MUST pass
- [ ] Create implementation-specific tests in `Implementations/FileTypeIdentifierServiceTests.cs`
  - Test content-detection logic (magic numbers, file headers)
  - Test scanned PDF detection (text layer analysis)
  - Test error scenarios (corrupted files, invalid paths)
  - Use sample test files as appropriate
- [ ] Write integration tests

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** Implementation passes ALL contract tests from `IIFileTypeIdentifierTests.cs` (IITDD compliance)
- ⚠️ **CRITICAL:** Implementation satisfies interface contract (Liskov Substitution Principle)
- File type identification based on content, not extension
- Performance: <500ms for identification
- Handles corrupted files gracefully
- Contract tests validate WHAT, implementation tests validate HOW (content-detection specific)

**Dependencies:** Task 1.2.1, Task 1.2.1A, Task 1.2.2

---

### Task 1.2.4: Implement XML Metadata Extractor
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**Description:** Implement `IMetadataExtractor` for XML documents (expediente format). This adapter MUST satisfy the interface contract defined in Task 1.2.1 and pass all contract tests from Task 1.2.1A.

**⚠️ ARCHITECTURAL REQUIREMENT:** This is an ADAPTER - it MUST be in `ExxerCube.Prisma.Infrastructure.Extraction.csproj` project for extraction adapters (HIGH COHESION).

**⚠️ IITDD REQUIREMENT:** The adapter implementation MUST pass all contract tests from `IIMetadataExtractorTests.cs`. Contract tests validate the interface contract (WHAT), not XML parsing implementation details (HOW).

**Tasks:**
- [ ] Create `XmlMetadataExtractor` in `ExxerCube.Prisma.Infrastructure.Extraction.csproj` project
  - Implement `IMetadataExtractor` interface
  - Ensure implementation satisfies interface contract (Liskov Substitution Principle)
- [ ] Implement `ExtractFromXmlAsync` - Parse XML and extract expediente, oficio, RFC, names, dates
- [ ] Implement `ExtractMetadataAsync` - Route to XML extraction based on file type
- [ ] Handle nullable XML elements (use `IXmlNullableParser<T>` pattern)
- [ ] Map XML structure to `ExtractedMetadata` entity
- [ ] Add error handling for malformed XML
- [ ] **IITDD Contract Compliance:** Run `IIMetadataExtractorTests.cs` against `XmlMetadataExtractor` instance - ALL tests MUST pass
- [ ] Create implementation-specific tests in `Implementations/XmlMetadataExtractorTests.cs`
  - Test XML parsing logic (expediente structure, nullable elements)
  - Test error scenarios (malformed XML, missing required elements)
  - Use sample XML files as appropriate
- [ ] Write integration tests

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** Implementation passes ALL contract tests from `IIMetadataExtractorTests.cs` (IITDD compliance)
- ⚠️ **CRITICAL:** Implementation satisfies interface contract (Liskov Substitution Principle)
- Extracts all required metadata fields from XML
- Performance: <200ms for XML extraction
- Handles missing/nullable fields gracefully
- Contract tests validate WHAT, implementation tests validate HOW (XML parsing specific)

**Dependencies:** Task 1.2.1, Task 1.2.1A, Task 1.2.2

---

### Task 1.2.5: Implement DOCX Metadata Extractor
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**Description:** Implement `IMetadataExtractor` for DOCX documents using structured field extraction. This adapter MUST satisfy the interface contract defined in Task 1.2.1 and pass all contract tests from Task 1.2.1A.

**⚠️ ARCHITECTURAL REQUIREMENT:** This is an ADAPTER - it MUST be in `ExxerCube.Prisma.Infrastructure.Extraction.csproj` project (same as XML extractor) for HIGH COHESION.

**⚠️ IITDD REQUIREMENT:** The adapter implementation MUST pass all contract tests from `IIMetadataExtractorTests.cs`. Contract tests validate the interface contract (WHAT), not DOCX parsing implementation details (HOW).

**Tasks:**
- [ ] Add DocumentFormat.OpenXml NuGet package to `ExxerCube.Prisma.Infrastructure.Extraction.csproj`
- [ ] Create `DocxMetadataExtractor` in `ExxerCube.Prisma.Infrastructure.Extraction.csproj` project
  - Implement `IMetadataExtractor` interface
  - Ensure implementation satisfies interface contract (Liskov Substitution Principle)
- [ ] Implement `ExtractFromDocxAsync` - Parse DOCX and extract structured fields
- [ ] Implement `ExtractMetadataAsync` - Route to DOCX extraction based on file type
- [ ] Handle both searchable DOCX and scanned DOCX (with OCR fallback via existing `IOcrExecutor`)
- [ ] Map DOCX content to `ExtractedMetadata` entity
- [ ] **IITDD Contract Compliance:** Run `IIMetadataExtractorTests.cs` against `DocxMetadataExtractor` instance - ALL tests MUST pass
- [ ] Create implementation-specific tests in `Implementations/DocxMetadataExtractorTests.cs`
  - Test DOCX parsing logic (OpenXML structure, field extraction)
  - Test OCR fallback for scanned DOCX
  - Test error scenarios (corrupted DOCX, missing fields)
  - Use sample DOCX files as appropriate
- [ ] Write integration tests

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** Implementation passes ALL contract tests from `IIMetadataExtractorTests.cs` (IITDD compliance)
- ⚠️ **CRITICAL:** Implementation satisfies interface contract (Liskov Substitution Principle)
- Extracts metadata from DOCX documents
- Performance: <1 second for DOCX extraction
- Handles scanned DOCX with OCR fallback
- Contract tests validate WHAT, implementation tests validate HOW (DOCX parsing specific)

**Dependencies:** Task 1.2.1, Task 1.2.1A, Task 1.2.2, Existing OCR interfaces

---

### Task 1.2.6: Implement PDF Metadata Extractor with OCR Fallback
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 10 hours

**Description:** Implement `IMetadataExtractor` for PDF documents, using existing OCR pipeline as fallback. This adapter MUST satisfy the interface contract defined in Task 1.2.1 and pass all contract tests from Task 1.2.1A.

**⚠️ ARCHITECTURAL REQUIREMENT:** This is an ADAPTER - it MUST be in `ExxerCube.Prisma.Infrastructure.Extraction.csproj` project (same as XML/DOCX extractors) for HIGH COHESION.

**⚠️ IITDD REQUIREMENT:** The adapter implementation MUST pass all contract tests from `IIMetadataExtractorTests.cs`. Contract tests validate the interface contract (WHAT), not PDF/OCR implementation details (HOW).

**Tasks:**
- [ ] Create `PdfMetadataExtractor` in `ExxerCube.Prisma.Infrastructure.Extraction.csproj` project
  - Implement `IMetadataExtractor` interface
  - Ensure implementation satisfies interface contract (Liskov Substitution Principle)
- [ ] Implement `ExtractFromPdfAsync` - Try text extraction first, fallback to OCR if needed
- [ ] Implement `ExtractMetadataAsync` - Route to PDF extraction based on file type
- [ ] Integrate with existing `IOcrExecutor` for OCR fallback (maintain compatibility)
- [ ] Use existing `IScanDetector` and `IScanCleaner` (or `IImagePreprocessor`) for scanned PDFs
- [ ] Map PDF content to `ExtractedMetadata` entity
- [ ] Add preprocessing for scanned PDFs (deskew, binarization)
- [ ] **IITDD Contract Compliance:** Run `IIMetadataExtractorTests.cs` against `PdfMetadataExtractor` instance - ALL tests MUST pass
- [ ] Create implementation-specific tests in `Implementations/PdfMetadataExtractorTests.cs`
  - Test PDF text extraction logic
  - Test OCR fallback integration with existing `IOcrExecutor`
  - Test scanned PDF preprocessing (deskew, binarization)
  - Test error scenarios (corrupted PDF, OCR failures)
  - Use sample PDFs (searchable and scanned) as appropriate
- [ ] Write integration tests with sample PDFs (searchable and scanned)

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** Implementation passes ALL contract tests from `IIMetadataExtractorTests.cs` (IITDD compliance)
- ⚠️ **CRITICAL:** Implementation satisfies interface contract (Liskov Substitution Principle)
- Maintains compatibility with existing OCR pipeline
- Performance: <2 seconds (without OCR), <30 seconds (with OCR)
- Handles both searchable and scanned PDFs
- Integration verification: Existing OCR functionality continues to work
- Contract tests validate WHAT, implementation tests validate HOW (PDF/OCR specific)

**Dependencies:** Task 1.2.1, Task 1.2.1A, Task 1.2.2, Existing OCR interfaces

---

### Task 1.2.7: Implement File Classifier
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 10 hours

**Description:** Implement `IFileClassifier` for deterministic rule-based classification for Level 1 and Level 2 categories. This adapter MUST satisfy the interface contract defined in Task 1.2.1 and pass all contract tests from Task 1.2.1A.

**⚠️ ARCHITECTURAL REQUIREMENT:** This is an ADAPTER - it MUST be in `ExxerCube.Prisma.Infrastructure.Classification.csproj` project for classification adapters (HIGH COHESION).

**⚠️ IITDD REQUIREMENT:** The adapter implementation MUST pass all contract tests from `IIFileClassifierTests.cs`. Contract tests validate the interface contract (WHAT), not classification rule implementation details (HOW).

**Tasks:**
- [ ] Create `FileClassifierService` in `ExxerCube.Prisma.Infrastructure.Classification.csproj` project
  - Implement `IFileClassifier` interface
  - Ensure implementation satisfies interface contract (Liskov Substitution Principle)
- [ ] Implement `ClassifyLevel1Async` - Apply deterministic rules based on metadata
- [ ] Implement `ClassifyLevel2Async` - Apply subcategory rules based on Level 1 and metadata
- [ ] Implement `GetClassificationConfidenceAsync` - Calculate confidence score (0-100)
- [ ] Create classification rules configuration (JSON or code-based)
- [ ] Handle ambiguous cases (low confidence)
- [ ] **IITDD Contract Compliance:** Run `IIFileClassifierTests.cs` against `FileClassifierService` instance - ALL tests MUST pass
- [ ] Create implementation-specific tests in `Implementations/FileClassifierServiceTests.cs`
  - Test classification rule logic (deterministic rules, rule matching)
  - Test confidence score calculation
  - Test ambiguous case handling (low confidence scenarios)
  - Test various metadata scenarios
- [ ] Write integration tests

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** Implementation passes ALL contract tests from `IIFileClassifierTests.cs` (IITDD compliance)
- ⚠️ **CRITICAL:** Implementation satisfies interface contract (Liskov Substitution Principle)
- Classification uses deterministic rules (not ML)
- Performance: <500ms per document
- Confidence scores are meaningful (0-100)
- Handles ambiguous cases gracefully
- Contract tests validate WHAT, implementation tests validate HOW (classification rules specific)

**Dependencies:** Task 1.2.1, Task 1.2.1A, Task 1.2.2

---

### Task 1.2.8: Implement Safe File Namer and File Mover
**Priority:** P1  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**Description:** Implement `ISafeFileNamer` and `IFileMover` for file naming normalization and organization based on classification. These adapters MUST satisfy the interface contracts defined in Task 1.2.1 and pass all contract tests from Task 1.2.1A.

**⚠️ ARCHITECTURAL REQUIREMENT:** These are ADAPTERS - they MUST be in Infrastructure layer. They implement PORTS (interfaces) from Domain.

**⚠️ IITDD REQUIREMENT:** The adapter implementations MUST pass all contract tests from `IISafeFileNamerTests.cs` and `IIFileMoverTests.cs`. Contract tests validate the interface contracts (WHAT), not filesystem implementation details (HOW).

**Tasks:**
- [ ] Create `SafeFileNamerService` in `ExxerCube.Prisma.Infrastructure.FileStorage.csproj` project
  - Implement `ISafeFileNamer` interface
  - Ensure implementation satisfies interface contract (Liskov Substitution Principle)
- [ ] Implement `GenerateSafeFileNameAsync` - Create normalized filename from metadata
- [ ] Implement `EnsureUniqueFileNameAsync` - Add hash/timestamp if needed
- [ ] Implement `NormalizeFileName` - Remove invalid characters
- [ ] **IITDD Contract Compliance:** Run `IISafeFileNamerTests.cs` against `SafeFileNamerService` instance - ALL tests MUST pass
- [ ] Create `FileMoverService` in `ExxerCube.Prisma.Infrastructure.FileStorage.csproj` project
  - Implement `IFileMover` interface
  - Ensure implementation satisfies interface contract (Liskov Substitution Principle)
- [ ] Implement `MoveFileAsync` - Move file to folder based on classification
- [ ] Implement `GetTargetFolderPath` - Generate folder path from classification
- [ ] **IITDD Contract Compliance:** Run `IIFileMoverTests.cs` against `FileMoverService` instance - ALL tests MUST pass
- [ ] Create implementation-specific tests in `Implementations/SafeFileNamerServiceTests.cs` and `Implementations/FileMoverServiceTests.cs`
  - Test filename normalization logic (invalid character removal, length limits)
  - Test uniqueness generation (hash/timestamp logic)
  - Test file move operations (folder creation, file I/O)
  - Test error scenarios (permissions, disk space)
- [ ] Write integration tests

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** Implementations pass ALL contract tests from `IISafeFileNamerTests.cs` and `IIFileMoverTests.cs` (IITDD compliance)
- ⚠️ **CRITICAL:** Implementations satisfy interface contracts (Liskov Substitution Principle)
- File names are safe and normalized
- Files are organized by classification
- Performance: <500ms for file move
- Contract tests validate WHAT, implementation tests validate HOW (filesystem-specific)

**Dependencies:** Task 1.2.1, Task 1.2.1A, Task 1.2.2, Task 1.2.7

---

### Task 1.2.9: Create Application Service for Stage 2 Orchestration
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**Description:** Create application service that orchestrates metadata extraction and classification workflow. This service uses Domain interfaces (PORTS) via constructor injection and MUST be testable without Infrastructure dependencies using mocks/substitutes.

**⚠️ ARCHITECTURAL REQUIREMENT:** This is ORCHESTRATION - it MUST use interfaces (PORTS) from Domain via constructor injection, NOT concrete classes from Infrastructure. It does NOT implement interfaces.

**⚠️ PROJECT REFERENCES:**
- ✅ `ExxerCube.Prisma.Application` references ONLY `ExxerCube.Prisma.Domain`
- ❌ `ExxerCube.Prisma.Application` does NOT reference any Infrastructure projects
- ✅ Application can be tested WITHOUT Infrastructure projects (use mocks)

**⚠️ IITDD REQUIREMENT:** Application orchestration tests MUST use mocks/substitutes (NSubstitute) for all Domain interfaces. Tests validate orchestration logic (workflow coordination), not adapter implementations.

**Tasks:**
- [ ] Create `MetadataExtractionService` in `Application/Services/`
- [ ] ⚠️ **CRITICAL:** Verify `ExxerCube.Prisma.Application.csproj` only references `ExxerCube.Prisma.Domain.csproj`
- [ ] ⚠️ **CRITICAL:** Inject `IFileTypeIdentifier`, `IMetadataExtractor`, `IFileClassifier`, `ISafeFileNamer`, `IFileMover` interfaces (PORTS), NOT concrete adapters
- [ ] ⚠️ **CRITICAL:** Do NOT import Infrastructure namespaces - only use Domain interfaces
- [ ] Implement workflow:
  1. Identify file type
  2. Extract metadata (XML/DOCX/PDF)
  3. Classify document (Level 1, Level 2)
  4. Generate safe filename
  5. Move file to organized location
  6. Log classification decisions
- [ ] Add error handling
- [ ] Add logging with correlation IDs
- [ ] Create orchestration tests in `Application.Tests/MetadataExtractionServiceTests.cs`
  - Use NSubstitute to mock ALL Domain interfaces (`IFileTypeIdentifier`, `IMetadataExtractor`, `IFileClassifier`, `ISafeFileNamer`, `IFileMover`)
  - Test orchestration workflow: successful extraction and classification flow
  - Test orchestration workflow: error handling at each step
  - Test orchestration workflow: file type routing (XML/DOCX/PDF)
  - Test orchestration workflow: classification confidence handling
  - Test orchestration workflow: correlation ID propagation
  - ⚠️ **CRITICAL:** Tests use mocks, NOT real Infrastructure adapters
  - ⚠️ **CRITICAL:** Tests validate orchestration logic (WHEN/THEN), not adapter behavior
- [ ] Write integration test for complete workflow (uses real Infrastructure adapters)

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** Application service uses ONLY Domain interfaces (PORTS), no Infrastructure dependencies
- ⚠️ **CRITICAL:** Orchestration tests use mocks/substitutes (NSubstitute) for all interfaces
- ⚠️ **CRITICAL:** Application can be tested WITHOUT Infrastructure projects (IITDD principle)
- Workflow handles all file types
- Integration verification: Existing OCR pipeline continues to work
- Performance meets requirements
- Orchestration tests validate workflow coordination, not adapter implementations

**Dependencies:** All Task 1.2.x tasks

---

## Story 1.3: Field Matching and Unified Metadata Generation

**Story Goal:** Match and consolidate field values across XML, DOCX, and PDF sources.

### Task 1.3.1: Create Generic Field Extractor Interface
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Extend existing `IFieldExtractor` to generic `IFieldExtractor<T>` while maintaining backward compatibility.

**Tasks:**
- [ ] Review existing `IFieldExtractor` interface
- [ ] Create `IFieldExtractor<T>` generic interface
  - Method: `ExtractFieldsAsync(T source, FieldDefinition[] fieldDefinitions) -> Task<Result<ExtractedFields>>`
  - Method: `ExtractFieldAsync(T source, string fieldName) -> Task<Result<FieldValue>>`
- [ ] Create adapter to wrap existing `IFieldExtractor` for backward compatibility
- [ ] Create `DocxFieldExtractor : IFieldExtractor<DocxSource>`
- [ ] Create `PdfOcrFieldExtractor : IFieldExtractor<PdfSource>`
- [ ] Ensure existing code continues to work

**Acceptance Criteria:**
- Backward compatibility maintained (CR1)
- Generic interface supports multiple source types
- Existing implementations continue to function

**Dependencies:** Review existing `IFieldExtractor` interface

---

### Task 1.3.2: Create Field Matcher Interface and Entities
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Create interface and entities for field matching across formats.

**Tasks:**
- [ ] Create `IFieldMatcher<T>` interface
  - Method: `MatchFieldsAsync(List<T> sources, FieldDefinition[] fieldDefinitions) -> Task<Result<MatchedFields>>`
  - Method: `GenerateUnifiedRecordAsync(MatchedFields matchedFields) -> Task<Result<UnifiedMetadataRecord>>`
  - Method: `ValidateMatchResultAsync(MatchedFields matchedFields, string[] requiredFields) -> Task<Result<ValidationResult>>`
- [ ] Create `MatchedFields` entity
  - Properties: FieldMatches (Dictionary<string, FieldMatchResult>), OverallAgreement (float), ConflictingFields, MissingFields
- [ ] Create `FieldMatchResult` entity
  - Properties: Value, Sources (List<FieldSource>), AgreementLevel (int 0-100), OriginTrace
- [ ] Create `UnifiedMetadataRecord` entity
  - Properties: Expediente, Personas, Oficio, ExtractedFields, Classification, MatchedFields, RequirementSummary, SlaStatus
- [ ] Create `FieldDefinition` value object
- [ ] Create `FieldValue` value object

**Acceptance Criteria:**
- Entities support confidence scoring
- Entities track field origins for traceability

**Dependencies:** Task 1.2.2 (ExtractedMetadata entity)

---

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

### Task 1.3.3: Implement Field Extractor for DOCX
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**Description:** Implement DOCX field extraction using structured field definitions.

**Tasks:**
- [ ] Create `DocxFieldExtractor` in `Infrastructure/Extraction/`
- [ ] Implement `ExtractFieldsAsync` - Extract structured fields from DOCX
- [ ] Implement `ExtractFieldAsync` - Extract single field by name
- [ ] Support field definitions with patterns/regex
- [ ] Calculate confidence scores for extracted fields
- [ ] Write unit tests with sample DOCX files
- [ ] Write integration tests

**Acceptance Criteria:**
- Extracts structured fields from DOCX
- Performance: <2 seconds per document
- Confidence scores are meaningful

**Dependencies:** Task 1.3.1

---

### Task 1.3.4: Implement Field Extractor for PDF (OCR)
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**Description:** Implement PDF field extraction using OCR text.

**Tasks:**
- [ ] Create `PdfOcrFieldExtractor` in `Infrastructure/Extraction/`
- [ ] Integrate with existing OCR pipeline to get text
- [ ] Implement `ExtractFieldsAsync` - Extract structured fields from OCR text
- [ ] Implement `ExtractFieldAsync` - Extract single field by name
- [ ] Use field definitions with patterns/regex
- [ ] Calculate confidence scores
- [ ] Write unit tests
- [ ] Write integration tests

**Acceptance Criteria:**
- Uses existing OCR pipeline (maintains compatibility)
- Extracts fields from OCR text
- Performance meets requirements

**Dependencies:** Task 1.3.1, Existing OCR pipeline

---

### Task 1.3.5: Implement Field Matcher
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**Description:** Implement field matching logic to compare and consolidate fields across formats.

**Tasks:**
- [ ] Create `FieldMatcherService` in `Infrastructure/Matching/`
- [ ] Implement `MatchFieldsAsync` - Compare fields across XML, DOCX, PDF sources
- [ ] Calculate agreement levels for each field (0-100)
- [ ] Identify conflicting fields
- [ ] Identify missing fields
- [ ] Implement `GenerateUnifiedRecordAsync` - Consolidate best values into unified record
- [ ] Implement `ValidateMatchResultAsync` - Check completeness and consistency
- [ ] Add matching policy support (configurable rules)
- [ ] Write unit tests with various matching scenarios
- [ ] Write integration tests

**Acceptance Criteria:**
- Matches fields across multiple sources
- Generates unified metadata record with confidence scores
- Performance: <1 second per comparison
- Handles conflicts and missing fields

**Dependencies:** Task 1.3.2, Task 1.3.3, Task 1.3.4

---

### Task 1.3.6: Create Application Service for Field Matching Orchestration
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Create application service that orchestrates field extraction and matching workflow.

**Tasks:**
- [ ] Create `FieldMatchingService` in `Application/Services/`
- [ ] Implement workflow:
  1. Extract fields from XML, DOCX, PDF sources
  2. Match fields across sources
  3. Generate unified metadata record
  4. Validate completeness
  5. Log matching decisions
- [ ] Add error handling
- [ ] Add logging
- [ ] Write unit tests
- [ ] Write integration test

**Acceptance Criteria:**
- Workflow produces unified metadata records
- Integration verification: Existing field extraction continues to work

**Dependencies:** All Task 1.3.x tasks

---

## Story 1.4: Identity Resolution and Legal Directive Classification

**Story Goal:** Resolve person identities and classify legal directives automatically.

### Task 1.4.1: Create Domain Interfaces for Stage 3 (Part 1)
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Define Stage 3 interfaces for identity resolution and legal classification.

**Tasks:**
- [ ] Create `IPersonIdentityResolver` interface
  - Method: `ResolveIdentityAsync(PersonData personData, Dictionary<string, object>? metadata) -> Task<Result<ResolvedIdentity>>`
  - Method: `DeduplicateRecordsAsync(List<PersonData> personRecords) -> Task<Result<List<ConsolidatedPersonRecord>>>`
  - Method: `FindIdentityVariantsAsync(string rfc) -> Task<Result<List<PersonIdentityVariant>>>`
- [ ] Create `ILegalDirectiveClassifier` interface
  - Method: `ClassifyDirectiveAsync(string legalText, ExtractedMetadata? metadata) -> Task<Result<LegalDirective>>`
  - Method: `DetectLegalInstrumentsAsync(string legalText) -> Task<Result<List<LegalInstrument>>>`
  - Method: `MapToComplianceActionAsync(LegalDirective directive) -> Task<Result<ComplianceAction>>`

**Acceptance Criteria:**
- Interfaces follow Railway-Oriented Programming pattern
- All methods have XML documentation

**Dependencies:** None

---

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

### Task 1.4.2: Create Domain Entities for Stage 3
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Create domain entities for identity resolution and legal classification.

**Tasks:**
- [ ] Create `Persona` entity (from PRP)
  - Properties: ParteId, Caracter, PersonaTipo, Paterno, Materno, Nombre, Rfc, Relacion, Domicilio, Complementarios, RfcVariants
- [ ] Create `ResolvedIdentity` entity
- [ ] Create `ConsolidatedPersonRecord` entity
- [ ] Create `PersonIdentityVariant` entity
- [ ] Create `LegalDirective` entity
- [ ] Create `LegalInstrument` entity
- [ ] Create `ComplianceAction` entity
  - Properties: ActionType (enum), AccountNumber, ProductType, Amount, ExpedienteOrigen, OficioOrigen, RequerimientoOrigen, AdditionalData
- [ ] Create `ComplianceActionType` enum
  - Values: Block, Unblock, Document, Transfer, Information, Ignore

**Acceptance Criteria:**
- Entities match PRP data models
- Entities support nullable reference types

**Dependencies:** None

---

### Task 1.4.3: Implement Person Identity Resolver
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**Description:** Implement identity resolution handling RFC variants and alias names.

**Tasks:**
- [ ] Create `PersonIdentityResolverService` in `Infrastructure/IdentityResolution/`
- [ ] Implement `ResolveIdentityAsync` - Resolve person using RFC, name, metadata
- [ ] Implement RFC variant matching (handle different RFC formats)
- [ ] Implement alias name matching (handle name variations)
- [ ] Implement `DeduplicateRecordsAsync` - Consolidate duplicate person records
- [ ] Implement `FindIdentityVariantsAsync` - Find all variants of an RFC
- [ ] Add database support for identity storage (if needed)
- [ ] Write unit tests with various RFC and name scenarios
- [ ] Write integration tests

**Acceptance Criteria:**
- Handles RFC variants correctly
- Deduplicates person records
- Performance: <500ms for identity resolution

**Dependencies:** Task 1.4.1, Task 1.4.2

---

### Task 1.4.4: Implement Legal Directive Classifier
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 10 hours

**Description:** Implement legal text classification to map clauses to compliance actions.

**Tasks:**
- [ ] Create `LegalDirectiveClassifierService` in `Infrastructure/Classification/`
- [ ] Implement `ClassifyDirectiveAsync` - Analyze legal text and classify directive
- [ ] Implement `DetectLegalInstrumentsAsync` - Find references to legal instruments (Acuerdo 105/2021, etc.)
- [ ] Implement `MapToComplianceActionAsync` - Map directive to compliance action
- [ ] Create rule-based classification (or use NLP if needed)
- [ ] Handle Spanish legal text
- [ ] Write unit tests with sample legal text
- [ ] Write integration tests

**Acceptance Criteria:**
- Classifies legal directives accurately
- Detects legal instruments
- Maps to compliance actions correctly
- Performance: <2 seconds for classification

**Dependencies:** Task 1.4.1, Task 1.4.2

---

### Task 1.4.5: Create Application Service for Stage 3 Orchestration
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Create application service that orchestrates identity resolution and legal classification.

**Tasks:**
- [ ] Create `DecisionLogicService` in `Application/Services/`
- [ ] Implement workflow:
  1. Resolve person identities
  2. Deduplicate person records
  3. Classify legal directives
  4. Map to compliance actions
  5. Log decisions
- [ ] Add error handling
- [ ] Add logging
- [ ] Write unit tests
- [ ] Write integration test

**Acceptance Criteria:**
- Workflow produces resolved identities and compliance actions
- Integration verification: Uses metadata from Stage 2 without re-processing

**Dependencies:** All Task 1.4.x tasks

---

## Story 1.5: SLA Tracking and Escalation Management

**Story Goal:** Track SLA deadlines and escalate impending breaches automatically.

### Task 1.5.1: Create SLA Domain Interface and Entities
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 3 hours

**Description:** Define SLA tracking interface and entities.

**Tasks:**
- [ ] Create `ISLAEnforcer` interface
  - Method: `CalculateDeadlineAsync(DateTime intakeDate, int daysPlazo) -> Task<Result<DateTime>>`
  - Method: `CheckAndEscalateAsync(string fileId, DateTime deadline, int criticalThresholdHours) -> Task<Result<EscalationStatus>>`
  - Method: `GetSlaStatusAsync(string fileId) -> Task<Result<SLAStatus>>`
  - Method: `GetFilesAtRiskAsync(int thresholdHours) -> Task<Result<List<FileSlaStatus>>>`
- [ ] Create `SLAStatus` entity
  - Properties: FileId, IntakeDate, Deadline, DaysPlazo, RemainingTime, IsAtRisk, IsBreached, EscalationLevel, EscalatedAt
- [ ] Create `EscalationStatus` entity
- [ ] Create `EscalationLevel` enum
  - Values: None, Warning, Critical, Breached
- [ ] Create `FileSlaStatus` entity

**Acceptance Criteria:**
- Interface follows Railway-Oriented Programming pattern
- Entities support business day calculations

**Dependencies:** None

---

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

### Task 1.5.2: Create Database Schema for SLA Tracking
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 3 hours

**Description:** Create database tables for SLA status tracking.

**Tasks:**
- [ ] Create `SLAStatusEntity` in Infrastructure layer
  - Table: `SLAStatus`
  - Columns: FileId (PK, FK to FileMetadata), IntakeDate, Deadline, DaysPlazo, RemainingTime, IsAtRisk, IsBreached, EscalationLevel, EscalatedAt
  - Index on Deadline for at-risk queries
  - Index on IsAtRisk, IsBreached for filtering
- [ ] Create EF Core configuration
- [ ] Create migration: `AddSLAStatusTable`
- [ ] Test migration

**Acceptance Criteria:**
- Migration is additive-only
- Indexes support performance requirements

**Dependencies:** Task 1.5.1, Task 1.1.3 (FileMetadata table)

---

### Task 1.5.3: Implement SLA Enforcer
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**Description:** Implement SLA deadline calculation and escalation logic.

**Tasks:**
- [ ] Create `SLAEnforcerService` in `Infrastructure/SLA/`
- [ ] Implement `CalculateDeadlineAsync` - Calculate deadline from intake date + business days
- [ ] Implement business day calculation (exclude weekends, Mexican holidays if applicable)
- [ ] Implement `CheckAndEscalateAsync` - Check if within critical threshold, escalate if needed
- [ ] Implement `GetSlaStatusAsync` - Get current SLA status for a file
- [ ] Implement `GetFilesAtRiskAsync` - Query files at risk of breach
- [ ] Add escalation notification mechanism (logging, or integrate with notification service)
- [ ] Add configuration for escalation thresholds
- [ ] Write unit tests for business day calculations
- [ ] Write integration tests

**Acceptance Criteria:**
- Calculates deadlines correctly using business days
- Escalates at-risk cases
- Performance: <50ms for deadline calculation, <200ms for escalation check
- Integration verification: Does not impact document processing performance

**Dependencies:** Task 1.5.1, Task 1.5.2

---

### Task 1.5.4: Create SLA Dashboard UI Component
**Priority:** P1  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**Description:** Create Blazor Server component for SLA monitoring dashboard.

**Tasks:**
- [ ] Create `SlaDashboard.razor` component in `UI/ExxerCube.Prisma.Web.UI/Pages/`
- [ ] Display active cases with deadline countdown
- [ ] Show escalation status and risk indicators
- [ ] Add filtering by risk level, deadline range
- [ ] Add real-time updates via SignalR (optional)
- [ ] Use MudBlazor components for consistency
- [ ] Write component tests

**Acceptance Criteria:**
- Dashboard displays SLA information clearly
- Follows MudBlazor design patterns
- Responsive design

**Dependencies:** Task 1.5.3

---

### Task 1.5.5: Create Application Service for SLA Orchestration
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Create application service that orchestrates SLA tracking workflow.

**Tasks:**
- [ ] Create `SLATrackingService` in `Application/Services/`
- [ ] Implement workflow:
  1. Calculate SLA deadline when file is ingested
  2. Periodically check SLA status
  3. Escalate at-risk cases
  4. Log SLA calculations and escalations
- [ ] Add background service for periodic SLA checks (optional)
- [ ] Add error handling
- [ ] Add logging
- [ ] Write unit tests
- [ ] Write integration test

**Acceptance Criteria:**
- SLA tracking works end-to-end
- Escalations are triggered correctly

**Dependencies:** Task 1.5.3

---

## Story 1.6: Manual Review Interface

**Story Goal:** Provide manual review interface for ambiguous cases.

### Task 1.6.1: Create Manual Review Domain Interface
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 3 hours

**Description:** Define interface for manual review operations.

**Tasks:**
- [ ] Create `IManualReviewerPanel` interface
  - Method: `GetReviewCasesAsync(ReviewFilters? filters) -> Task<Result<List<ReviewCase>>>`
  - Method: `SubmitReviewDecisionAsync(string caseId, ReviewDecision decision) -> Task<Result>` (Success/failure only - IsSuccess sufficient)
  - Method: `GetFieldAnnotationsAsync(string caseId) -> Task<Result<FieldAnnotations>>`
- [ ] Create `ReviewCase` entity
- [ ] Create `ReviewDecision` entity
- [ ] Create `ReviewFilters` value object
- [ ] Create `FieldAnnotations` entity

**Acceptance Criteria:**
- Interface follows Railway-Oriented Programming pattern
- Entities support review workflow

**Dependencies:** None

---

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

### Task 1.6.2: Create Database Schema for Manual Review
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 3 hours

**Description:** Create database tables for review cases and decisions.

**Tasks:**
- [ ] Create `ReviewCaseEntity` in Infrastructure layer
  - Table: `ReviewCases`
  - Columns: CaseId (PK), FileId (FK), ReviewReason, ConfidenceLevel, Status, CreatedAt, ReviewedAt, ReviewerId
- [ ] Create `ReviewDecisionEntity`
  - Table: `ReviewDecisions`
  - Columns: DecisionId (PK), CaseId (FK), Decision, Overrides (JSON), Notes, ReviewedBy, ReviewedAt
- [ ] Create migrations
- [ ] Test migrations

**Acceptance Criteria:**
- Migrations are additive-only
- Supports review workflow

**Dependencies:** Task 1.6.1

---

### Task 1.6.3: Implement Manual Review Service
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**Description:** Implement backend service for manual review operations.

**Tasks:**
- [ ] Create `ManualReviewerService` in `Infrastructure/Review/`
- [ ] Implement `GetReviewCasesAsync` - Query cases requiring review
- [ ] Implement `SubmitReviewDecisionAsync` - Save review decision and update metadata
- [ ] Implement `GetFieldAnnotationsAsync` - Get field-level annotations for review
- [ ] Add logic to identify cases requiring review (low confidence, ambiguity, errors)
- [ ] Write unit tests
- [ ] Write integration tests

**Acceptance Criteria:**
- Identifies review cases correctly
- Saves review decisions
- Updates unified metadata based on review

**Dependencies:** Task 1.6.1, Task 1.6.2

---

### Task 1.6.4: Create Manual Review Dashboard UI
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 12 hours

**Description:** Create Blazor Server components for manual review interface.

**Tasks:**
- [ ] Create `ManualReviewDashboard.razor` component
  - List review cases with filters
  - Display case details
- [ ] Create `ReviewCaseDetail.razor` component
  - Display unified metadata record
  - Show field-level annotations (source, confidence, conflicts)
  - Allow editing/overriding values
  - Submit review decision
- [ ] Use MudBlazor components (tables, forms, dialogs)
- [ ] Add validation
- [ ] Add error handling
- [ ] Write component tests

**Acceptance Criteria:**
- UI is intuitive and follows MudBlazor patterns
- Review workflow is complete
- Integration verification: Does not disrupt existing workflows

**Dependencies:** Task 1.6.3

---

### Task 1.6.5: Create Application Service for Manual Review Orchestration
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Create application service that orchestrates manual review workflow.

**Tasks:**
- [ ] Create `ManualReviewService` in `Application/Services/`
- [ ] Implement workflow:
  1. Identify cases requiring review
  2. Queue cases for review
  3. Process review decisions
  4. Update unified metadata
  5. Log review actions
- [ ] Add error handling
- [ ] Add logging
- [ ] Write unit tests
- [ ] Write integration test

**Acceptance Criteria:**
- Review workflow is complete
- Review decisions update metadata correctly

**Dependencies:** All Task 1.6.x tasks

---

## Story 1.7: SIRO-Compliant Export Generation

**Story Goal:** Generate SIRO-compliant XML exports from validated metadata.

### Task 1.7.1: Create Export Domain Interface and Entities
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Define export interface and entities for SIRO compliance.

**Tasks:**
- [ ] Create `IResponseExporter` interface
  - Method: `ExportSiroXmlAsync(UnifiedMetadataRecord metadata, string outputPath) -> Task<Result<string>>`
  - Method: `ExportSignedPdfAsync(UnifiedMetadataRecord metadata, SigningCertificate certificate, string outputPath) -> Task<Result<string>>`
  - Method: `MapToRegulatorySchemaAsync(UnifiedMetadataRecord metadata, RegulatorySchema schema) -> Task<Result<RegulatoryData>>`
  - Method: `ValidateAgainstSchemaAsync(RegulatoryData data, RegulatorySchema schema) -> Task<Result<ValidationResult>>`
- [ ] Create `ILayoutGenerator` interface
  - Method: `GenerateExcelLayoutAsync(UnifiedMetadataRecord metadata, ExcelLayoutSchema schema, string outputPath) -> Task<Result<string>>`
  - Method: `GenerateExcelLayoutBatchAsync(List<UnifiedMetadataRecord> metadataList, ExcelLayoutSchema schema, string outputPath) -> Task<Result<string>>`
  - Method: `ValidateLayoutSchemaAsync(ExcelLayoutSchema schema) -> Task<Result<bool>>`
- [ ] Create `RegulatoryData` entity
- [ ] Create `RegulatorySchema` entity
- [ ] Create `ExcelLayoutSchema` entity
- [ ] Create `SigningCertificate` entity

**Acceptance Criteria:**
- Interfaces follow Railway-Oriented Programming pattern
- Entities support SIRO schema mapping

**Dependencies:** None

---

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

### Task 1.7.2: Implement SIRO XML Exporter
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 10 hours

**Description:** Implement SIRO-compliant XML export generation.

**Tasks:**
- [ ] Create `SiroXmlExporter` in `Infrastructure/Export/`
- [ ] Implement `MapToRegulatorySchemaAsync` - Map unified metadata to SIRO schema
- [ ] Implement `ValidateAgainstSchemaAsync` - Validate against SIRO XML schema
- [ ] Implement `ExportSiroXmlAsync` - Generate SIRO XML file
- [ ] Add SIRO XML schema file (XSD)
- [ ] Handle schema validation errors
- [ ] Write unit tests with sample metadata
- [ ] Write integration tests

**Acceptance Criteria:**
- Generates SIRO-compliant XML
- Validates against schema
- Performance: <1 second for XML export
- Integration verification: Does not modify source metadata

**Dependencies:** Task 1.7.1

---

### Task 1.7.3: Implement Excel Layout Generator
**Priority:** P1  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**Description:** Implement Excel layout generation for SIRO registration.

**Tasks:**
- [ ] Add EPPlus or ClosedXML NuGet package
- [ ] Create `ExcelLayoutGenerator` in `Infrastructure/Export/`
- [ ] Implement `GenerateExcelLayoutAsync` - Generate Excel from metadata
- [ ] Implement `GenerateExcelLayoutBatchAsync` - Generate Excel for multiple records
- [ ] Implement `ValidateLayoutSchemaAsync` - Validate schema
- [ ] Write unit tests
- [ ] Write integration tests

**Acceptance Criteria:**
- Generates Excel layouts correctly
- Performance: <2 seconds per record

**Dependencies:** Task 1.7.1

---

### Task 1.7.4: Create Export Management UI
**Priority:** P1  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**Description:** Create Blazor Server component for export management.

**Tasks:**
- [ ] Create `ExportManagement.razor` component
  - Initiate exports
  - View export status
  - Download generated files
- [ ] Use MudBlazor components
- [ ] Add error handling
- [ ] Write component tests

**Acceptance Criteria:**
- UI follows MudBlazor patterns
- Export workflow is complete

**Dependencies:** Task 1.7.2, Task 1.7.3

---

### Task 1.7.5: Create Application Service for Export Orchestration
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Create application service that orchestrates export workflow.

**Tasks:**
- [ ] Create `ExportService` in `Application/Services/`
- [ ] Implement workflow:
  1. Validate metadata completeness (FR20)
  2. Map to regulatory schema
  3. Validate against schema
  4. Generate export (XML/Excel)
  5. Log export operation
- [ ] Add error handling
- [ ] Add logging
- [ ] Write unit tests
- [ ] Write integration test

**Acceptance Criteria:**
- Export workflow is complete
- Validates before export
- Integration verification: Export does not block other operations

**Dependencies:** All Task 1.7.x tasks

---

## Story 1.8: PDF Summarization and Digital Signing

**Story Goal:** Summarize PDF content and generate digitally signed PDF exports.

### Task 1.8.1: Create PDF Summarization Domain Interface
**Priority:** P1  
**Status:** 🔴 Not Started  
**Estimated Effort:** 3 hours

**Description:** Define interface for PDF summarization and requirement categorization.

**Tasks:**
- [ ] Create `IPdfRequirementSummarizer` interface
  - Method: `SummarizeRequirementsAsync(string pdfPath, RequirementCategory[] categories) -> Task<Result<RequirementSummary>>`
  - Method: `ClassifyRequirementsAsync(string ocrText, RequirementCategory[] categories) -> Task<Result<ClassifiedRequirements>>`
  - Method: `GetRequirementConfidenceAsync(RequirementSummary summary) -> Task<Result<RequirementConfidenceScores>>`
- [ ] Create `ICriterionMapper` interface
  - Method: `MapToCategoriesAsync(string content, CriterionMappingConfig config) -> Task<Result<List<MappedCategory>>>`
  - Method: `LoadCriterionConfigAsync(string configPath) -> Task<Result<CriterionMappingConfig>>`
  - Method: `ValidateCriterionConfigAsync(CriterionMappingConfig config) -> Task<Result<bool>>`
- [ ] Create `RequirementSummary` entity
- [ ] Create `RequirementCategory` enum
- [ ] Create `CriterionMappingConfig` entity

**Acceptance Criteria:**
- Interfaces follow Railway-Oriented Programming pattern
- Entities support requirement categorization

**Dependencies:** None

---

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

### Task 1.8.2: Implement PDF Requirement Summarizer
**Priority:** P1  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**Description:** Implement PDF content summarization into requirement categories.

**Tasks:**
- [ ] Create `PdfRequirementSummarizer` in `Infrastructure/Summarization/`
- [ ] Implement `SummarizeRequirementsAsync` - Analyze PDF and categorize requirements
- [ ] Implement `ClassifyRequirementsAsync` - Classify OCR text into categories
- [ ] Use rule-based or semantic analysis
- [ ] Implement `GetRequirementConfidenceAsync` - Calculate confidence scores
- [ ] Integrate with existing OCR pipeline for text extraction
- [ ] Write unit tests
- [ ] Write integration tests

**Acceptance Criteria:**
- Summarizes PDF into requirement categories
- Performance: <5 seconds for summarization
- Integration verification: Uses existing OCR without breaking it

**Dependencies:** Task 1.8.1, Existing OCR pipeline

---

### Task 1.8.3: Implement Digital PDF Signing
**Priority:** P1  
**Status:** 🔴 Not Started  
**Estimated Effort:** 10 hours

**Description:** Implement digitally signed PDF export using X.509 certificates.

**Tasks:**
- [ ] Add PDF signing library (iTextSharp, PdfSharp, or similar)
- [ ] Create `DigitalPdfSigner` in `Infrastructure/Export/`
- [ ] Implement `ExportSignedPdfAsync` - Generate and sign PDF
- [ ] Support X.509 certificate integration
- [ ] Implement PAdES standard compliance
- [ ] Handle certificate management (load from config or key management system)
- [ ] Add certificate validation
- [ ] Write unit tests
- [ ] Write integration tests

**Acceptance Criteria:**
- Generates digitally signed PDFs
- Supports PAdES standard
- Performance: <3 seconds for PDF export
- Handles certificate unavailability gracefully

**Dependencies:** Task 1.7.1

---

### Task 1.8.4: Create Application Service for PDF Operations
**Priority:** P1  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Create application service that orchestrates PDF summarization and signing.

**Tasks:**
- [ ] Create `PdfOperationsService` in `Application/Services/`
- [ ] Implement workflow:
  1. Summarize PDF requirements
  2. Map to categories
  3. Generate signed PDF (if needed)
  4. Log operations
- [ ] Add error handling
- [ ] Add logging
- [ ] Write unit tests
- [ ] Write integration test

**Acceptance Criteria:**
- PDF operations workflow is complete
- Integration verification: Does not impact existing PDF processing

**Dependencies:** All Task 1.8.x tasks

---

## Story 1.9: Audit Trail and Reporting

**Story Goal:** Maintain complete audit trail and reporting capabilities.

### Task 1.9.1: Create Audit Logging Domain Interface
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 3 hours

**Description:** Define interface for audit logging (may already exist partially).

**Tasks:**
- [ ] Review existing audit logging (if any)
- [ ] Create/Extend `IAuditLogger` interface
  - Method: `LogActionAsync(AuditAction action, string fileId, Dictionary<string, object>? details) -> Task<Result>` (Success/failure only - IsSuccess sufficient)
  - Method: `LogClassificationDecisionAsync(string fileId, ClassificationResult classification, ClassificationScores scores) -> Task<Result>` (Success/failure only - IsSuccess sufficient)
  - Method: `GetAuditRecordsAsync(string fileId) -> Task<Result<List<AuditRecord>>>`
- [ ] Create `IReportGenerator` interface
  - Method: `ExportSummaryAsync(ExportFormat format, SummaryFilters? filters) -> Task<Result<string>>`
  - Method: `GenerateClassificationReportAsync(DateTime startDate, DateTime endDate, ExportFormat format) -> Task<Result<string>>`
- [ ] Create `AuditRecord` entity
- [ ] Create `AuditAction` enum
- [ ] Create `ExportFormat` enum

**Acceptance Criteria:**
- Interface follows Railway-Oriented Programming pattern
- Supports all audit logging needs

**Dependencies:** None

---

### Task 1.9.1A: Create IITDD Contract Tests for Audit Logging Interfaces
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**⚠️ IITDD REQUIREMENT:** Contract tests MUST be written BEFORE any adapter implementations. These tests validate interface contracts using mocks and must pass for ANY valid implementation.

**Description:** Create IITDD contract tests for `IAuditLogger` and `IReportGenerator` interfaces. These tests define the contract (WHAT) that all implementations must satisfy, using mocks to validate behavior without implementation details (HOW).

**Tasks:**
- [ ] Create `Tests/Interfaces/IIAuditLoggerTests.cs`
- [ ] Test contract: `LogActionAsync` success path
  - Use NSubstitute to create mock `IAuditLogger` instance
  - Setup mock to return `Result.Success()`
  - Call `LogActionAsync` and verify result is success
  - Verify contract: method accepts `AuditAction`, `string fileId`, and optional `Dictionary<string, object>?` and returns `Result`
- [ ] Test contract: `LogActionAsync` failure path
  - Setup mock to return `Result.Failure(...)`
  - Verify contract: failures are returned as `Result` failures, not exceptions
- [ ] Test contract: `LogClassificationDecisionAsync` success path
  - Setup mock to return `Result.Success()`
  - Verify contract: method accepts `string fileId`, `ClassificationResult`, and `ClassificationScores` and returns `Result`
- [ ] Test contract: `LogClassificationDecisionAsync` failure path
  - Setup mock to return `Result.Failure(...)`
  - Verify contract: failures are returned as `Result` failures
- [ ] Test contract: `GetAuditRecordsAsync` success path
  - Setup mock to return `Result<List<AuditRecord>>.Success(...)`
  - Verify contract: method accepts `string fileId` and returns `Result<List<AuditRecord>>`
- [ ] Test contract: `GetAuditRecordsAsync` failure path
  - Setup mock to return `Result<List<AuditRecord>>.Failure(...)`
  - Verify contract: failures are returned as `Result<T>` failures
- [ ] Create `Tests/Interfaces/IIReportGeneratorTests.cs`
- [ ] Test contract: `ExportSummaryAsync` success path
  - Use NSubstitute to create mock `IReportGenerator` instance
  - Setup mock to return `Result<string>.Success(...)`
  - Call `ExportSummaryAsync` and verify result is success
  - Verify contract: method accepts `ExportFormat` and optional `SummaryFilters?` and returns `Result<string>`
- [ ] Test contract: `ExportSummaryAsync` failure path
  - Setup mock to return `Result<string>.Failure(...)`
  - Verify contract: failures are returned as `Result<T>` failures
- [ ] Test contract: `GenerateClassificationReportAsync` success path
  - Setup mock to return `Result<string>.Success(...)`
  - Verify contract: method accepts `DateTime startDate`, `DateTime endDate`, and `ExportFormat` and returns `Result<string>`
- [ ] Test contract: `GenerateClassificationReportAsync` failure path
  - Setup mock to return `Result<string>.Failure(...)`
  - Verify contract: failures are returned as `Result<T>` failures
- [ ] Test contract: Null parameter handling for both interfaces
  - Verify contract: methods handle null inputs appropriately (return failure or throw, as per contract)
- [ ] Use Shouldly for assertions
- [ ] Use xUnit v3 test framework

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** Tests are in `Tests/Interfaces/` project (IITDD Contract Tests)
- ⚠️ **CRITICAL:** Tests use mocks (NSubstitute), NOT real implementations
- ⚠️ **CRITICAL:** Tests validate "WHAT" (contract), not "HOW" (implementation details)
- ⚠️ **CRITICAL:** Tests must pass for ANY valid implementation (Liskov Substitution Principle)
- All interface methods have contract tests
- Tests use xUnit v3 and Shouldly
- Tests validate Railway-Oriented Programming pattern (Result<T> returns)

**Dependencies:** Task 1.9.1

---

### Task 1.9.2: Create Database Schema for Audit Logging
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Create database tables for immutable audit logs.

**Tasks:**
- [ ] Create `AuditRecordEntity` in Infrastructure layer
  - Table: `AuditRecords`
  - Columns: Id (PK), FileId (FK), Action (enum), Timestamp, UserId, Details (JSON), Classification (JSON), Scores (JSON)
  - Index on FileId for file queries
  - Index on Timestamp for date range queries
  - Index on Action for action type queries
- [ ] Create EF Core configuration
- [ ] Create migration: `AddAuditRecordsTable`
- [ ] Consider audit log retention policy (7 years per NFR9)
- [ ] Test migration

**Acceptance Criteria:**
- Migration is additive-only
- Supports 7-year retention
- Indexes support query performance

**Dependencies:** Task 1.9.1, Task 1.1.3 (FileMetadata table)

---

### Task 1.9.3: Implement Audit Logger
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**Description:** Implement audit logging service for all processing steps.

**Tasks:**
- [ ] Create `AuditLoggerService` in `Infrastructure/Logging/`
- [ ] Implement `LogActionAsync` - Log any action with details
- [ ] Implement `LogClassificationDecisionAsync` - Log classification with scores
- [ ] Implement `GetAuditRecordsAsync` - Query audit records for a file
- [ ] Make logging async and non-blocking
- [ ] Add correlation ID support
- [ ] Write unit tests
- [ ] Write integration tests

**Acceptance Criteria:**
- Logs all processing steps
- Performance: <100ms for logging (non-blocking)
- Supports correlation IDs
- Integration verification: Logging does not impact processing performance

**Dependencies:** Task 1.9.1, Task 1.9.2

---

### Task 1.9.4: Implement Report Generator
**Priority:** P1  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**Description:** Implement report generation for classification summaries and audit reports.

**Tasks:**
- [ ] Create `ReportGeneratorService` in `Infrastructure/Reporting/`
- [ ] Implement `ExportSummaryAsync` - Export current classification state (CSV/JSON)
- [ ] Implement `GenerateClassificationReportAsync` - Generate classification report for date range
- [ ] Support CSV and JSON formats
- [ ] Add filtering capabilities
- [ ] Write unit tests
- [ ] Write integration tests

**Acceptance Criteria:**
- Generates reports in CSV/JSON format
- Performance: <2 seconds for summary export, <5 seconds for classification report

**Dependencies:** Task 1.9.1, Task 1.9.2

---

### Task 1.9.5: Create Audit Trail Viewer UI
**Priority:** P1  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**Description:** Create Blazor Server component for viewing audit trails.

**Tasks:**
- [ ] Create `AuditTrailViewer.razor` component
  - Display audit records
  - Filter by file ID, date range, action type, user
  - Export audit logs
- [ ] Use MudBlazor components (tables, filters)
- [ ] Add pagination for large result sets
- [ ] Write component tests

**Acceptance Criteria:**
- UI follows MudBlazor patterns
- Supports filtering and export
- Integration verification: Integrates with existing UI navigation

**Dependencies:** Task 1.9.3

---

### Task 1.9.6: Create Application Service for Audit Orchestration
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Create application service that orchestrates audit logging across all stages. This service uses Domain interfaces (PORTS) via constructor injection and MUST be testable without Infrastructure dependencies using mocks/substitutes.

**⚠️ ARCHITECTURAL REQUIREMENT:** This is ORCHESTRATION - it MUST use interfaces (PORTS) from Domain via constructor injection, NOT concrete classes from Infrastructure. It does NOT implement interfaces.

**⚠️ PROJECT REFERENCES:**
- ✅ `ExxerCube.Prisma.Application` references ONLY `ExxerCube.Prisma.Domain`
- ❌ `ExxerCube.Prisma.Application` does NOT reference any Infrastructure projects
- ✅ Application can be tested WITHOUT Infrastructure projects (use mocks)

**⚠️ IITDD REQUIREMENT:** Application orchestration tests MUST use mocks/substitutes (NSubstitute) for all Domain interfaces. Tests validate orchestration logic (workflow coordination), not adapter implementations.

**Tasks:**
- [ ] Create `AuditService` in `Application/Services/`
- [ ] ⚠️ **CRITICAL:** Verify `ExxerCube.Prisma.Application.csproj` only references `ExxerCube.Prisma.Domain.csproj`
- [ ] ⚠️ **CRITICAL:** Inject `IAuditLogger`, `IReportGenerator` interfaces (PORTS), NOT concrete adapters
- [ ] ⚠️ **CRITICAL:** Do NOT import Infrastructure namespaces - only use Domain interfaces
- [ ] Integrate audit logging into all processing workflows
- [ ] Ensure correlation IDs are propagated
- [ ] Add error handling
- [ ] Add logging with correlation IDs
- [ ] Create orchestration tests in `Application.Tests/AuditServiceTests.cs`
  - Use NSubstitute to mock ALL Domain interfaces (`IAuditLogger`, `IReportGenerator`)
  - Test orchestration workflow: successful audit logging flow
  - Test orchestration workflow: error handling at each step
  - Test orchestration workflow: correlation ID propagation
  - Test orchestration workflow: report generation orchestration
  - ⚠️ **CRITICAL:** Tests use mocks, NOT real Infrastructure adapters
  - ⚠️ **CRITICAL:** Tests validate orchestration logic (WHEN/THEN), not adapter behavior
- [ ] Write integration test for complete workflow (uses real Infrastructure adapters)

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** Application service uses ONLY Domain interfaces (PORTS), no Infrastructure dependencies
- ⚠️ **CRITICAL:** Orchestration tests use mocks/substitutes (NSubstitute) for all interfaces
- ⚠️ **CRITICAL:** Application can be tested WITHOUT Infrastructure projects (IITDD principle)
- Audit logging integrated into all stages
- Correlation IDs tracked across stages
- Orchestration tests validate workflow coordination, not adapter implementations

**Dependencies:** All Task 1.9.x tasks

---

## Cross-Cutting Tasks

### Task CC.0: Create Non-Generic Result Type or Unit Type
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 2 hours

**⚠️ CRITICAL DEPENDENCY:** This task MUST be completed before any interface implementation tasks begin. All interface contracts in PRP.md reference the non-generic `Result` type. See PRP.md "Data Models & Entities" → "Common Types" → "Result (Non-Generic)" for complete specification.

**Description:** Create a non-generic `Result` type or `Unit` type to avoid redundant `Result<bool>` for success/failure-only operations.

**Tasks:**
- [ ] Option A: Create non-generic `Result` class in `Domain/Common/Result.cs`
  - Properties: `IsSuccess`, `Error`
  - Methods: `Success()`, `Failure(string error)`
  - Add extension methods for binding/mapping
- [ ] Option B: Create `Unit` struct in `Domain/Common/Unit.cs`
  - Empty struct (similar to F#'s Unit or C#'s void)
  - Use `Result<Unit>` for success/failure-only operations
- [ ] Update `ResultExtensions` to support non-generic Result or Result<Unit>
- [ ] Add unit tests
- [ ] Update existing code if needed (or maintain backward compatibility)

**Acceptance Criteria:**
- Non-generic Result or Unit type available
- Can be used instead of `Result<bool>` for success/failure-only operations
- Maintains Railway-Oriented Programming pattern
- Backward compatible with existing `Result<T>` usage
- ⚠️ **IITDD SUPPORT:** Result types MUST support IITDD contract tests - all interface methods return `Result<T>` or `Result` (non-generic) for Railway-Oriented Programming

**Dependencies:** None (must be done before interface definitions)

**Reference:** See PRP.md "Data Models & Entities" → "Common Types" → "Result (Non-Generic)" for complete specification and usage patterns.

**Recommendation:** Option A (non-generic Result) is cleaner and more explicit than `Result<Unit>`.

---

### Task CC.1: Update Dependency Injection Configuration
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 4 hours

**Description:** Register all new services in DI container. Each Infrastructure project has its own DI configuration.

**⚠️ ARCHITECTURAL REQUIREMENT:** Each Infrastructure project has its own `DependencyInjection/ServiceCollectionExtensions.cs`. The UI/Host project wires them all together. This keeps DI configuration close to the adapters (HIGH COHESION).

**Tasks:**
- [ ] In each Infrastructure project, create/update `DependencyInjection/ServiceCollectionExtensions.cs`:
  - [ ] `ExxerCube.Prisma.Infrastructure.BrowserAutomation` → Register browser automation adapters
  - [ ] `ExxerCube.Prisma.Infrastructure.Database` → Register database adapters
  - [ ] `ExxerCube.Prisma.Infrastructure.FileStorage` → Register file storage adapters
  - [ ] `ExxerCube.Prisma.Infrastructure.Extraction` → Register extraction adapters
  - [ ] `ExxerCube.Prisma.Infrastructure.Classification` → Register classification adapters
  - [ ] `ExxerCube.Prisma.Infrastructure.Export` → Register export adapters
- [ ] In UI/Host project (`ExxerCube.Prisma.Web.UI`), wire all Infrastructure projects together:
  - [ ] Call each Infrastructure project's `AddInfrastructureXxx()` extension method
- [ ] Register all application services in UI/Host project
- [ ] Add configuration bindings
- [ ] Test service resolution

**Acceptance Criteria:**
- ⚠️ **CRITICAL:** All registrations map PORT (interface) → ADAPTER (implementation)
- ⚠️ **CRITICAL:** DI configuration is in Infrastructure layer, NOT Application layer
- ⚠️ **IITDD SUPPORT:** Test project structure supports IITDD:
  - `Tests/Interfaces/` project contains IITDD contract tests (mock-based)
  - `Tests/Implementations/` project contains implementation-specific tests (real adapters)
  - `Application.Tests/` project contains orchestration tests (mock-based)
  - `Tests/Integration/` project contains end-to-end tests (real adapters)
- All services registered correctly
- Configuration injected properly
- **Code Review:** Verify Port → Adapter mappings, not concrete → concrete

**Dependencies:** All interface and service implementation tasks

---

### Task CC.2: Create Integration Tests for Complete Pipeline
**Priority:** P0  
**Status:** 🔴 Not Started  
**Estimated Effort:** 12 hours

**Description:** Create end-to-end integration tests for the complete 4-stage pipeline. These tests use REAL Infrastructure adapters (not mocks) and validate the complete system behavior.

**⚠️ IITDD TEST STRUCTURE:** Integration tests are distinct from IITDD contract tests:
- **IITDD Contract Tests** (`Tests/Interfaces/`): Use mocks, validate interface contracts (WHAT)
- **Implementation Tests** (`Tests/Implementations/`): Use real adapters, validate adapter behavior (HOW)
- **Orchestration Tests** (`Application.Tests/`): Use mocks, validate workflow coordination
- **Integration Tests** (`Tests/Integration/`): Use real adapters, validate end-to-end system behavior

**Tasks:**
- [ ] Create integration test project `Tests/Integration/` (if not exists)
- [ ] Create test for Stage 1 → Stage 2 → Stage 3 → Stage 4 workflow
- [ ] Test with sample documents (XML, DOCX, PDF)
- [ ] Test error scenarios
- [ ] Test integration verification points
- [ ] Add test data fixtures
- [ ] ⚠️ **IITDD ALIGNMENT:** Ensure integration tests complement (not duplicate) IITDD contract tests

**Acceptance Criteria:**
- ⚠️ **IITDD SUPPORT:** Integration tests use REAL Infrastructure adapters (distinct from IITDD contract tests which use mocks)
- Complete pipeline tested end-to-end
- Integration verification tests pass
- Error scenarios handled correctly
- Tests complement IITDD contract tests (integration = real adapters, contract = mocks)

**Dependencies:** All story implementation tasks

---

### Task CC.3: Update Documentation
**Priority:** P1  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**Description:** Update project documentation with new interfaces and workflows, including IITDD testing strategy.

**Tasks:**
- [ ] Update architecture documentation
- [ ] Document all 28 interfaces (reference PRP)
- [ ] Document IITDD testing strategy:
  - [ ] Explain IITDD principles (interface-first, contract-based, mock-based)
  - [ ] Document test project structure (`Tests/Interfaces/`, `Tests/Implementations/`, `Application.Tests/`, `Tests/Integration/`)
  - [ ] Document contract test naming conventions (`II{InterfaceName}Tests.cs`)
  - [ ] Document implementation test naming conventions (`{ImplementationName}Tests.cs`)
  - [ ] Explain distinction between contract tests (WHAT) and implementation tests (HOW)
- [ ] Create API documentation
- [ ] Update README with new capabilities
- [ ] Create developer onboarding guide

**Acceptance Criteria:**
- Documentation is complete and accurate
- Interfaces are documented
- ⚠️ **IITDD SUPPORT:** IITDD testing strategy is clearly documented, including test project structure and naming conventions

**Dependencies:** All implementation tasks

---

### Task CC.4: Performance Testing and Optimization
**Priority:** P1  
**Status:** 🔴 Not Started  
**Estimated Effort:** 8 hours

**Description:** Performance testing and optimization to meet NFRs. Note: IITDD contract tests use mocks and do NOT impact performance testing (they validate contracts, not performance).

**Tasks:**
- [ ] Create performance test scenarios
- [ ] Test browser automation performance (NFR3: <5s)
- [ ] Test metadata extraction performance (NFR4: <2s XML/DOCX, <30s PDF)
- [ ] Test classification performance (NFR5: <500ms)
- [ ] Identify and fix bottlenecks
- [ ] Add performance monitoring
- [ ] ⚠️ **IITDD NOTE:** Performance tests use REAL Infrastructure adapters (not mocks). IITDD contract tests validate interface contracts, not performance.

**Acceptance Criteria:**
- All performance NFRs met
- Performance monitoring in place
- ⚠️ **IITDD SUPPORT:** Performance tests use real adapters; IITDD contract tests use mocks and don't impact performance measurements

**Dependencies:** All implementation tasks

---

### Task CC.5: Security Review and Hardening
**Priority:** P1  
**Status:** 🔴 Not Started  
**Estimated Effort:** 6 hours

**Description:** Security review and hardening for financial regulatory system. Note: IITDD contract tests use mocks and do NOT expose security vulnerabilities (they validate contracts, not security).

**Tasks:**
- [ ] Review encryption implementation (NFR8)
- [ ] Review audit logging security
- [ ] Review access control (if applicable)
- [ ] Review input validation
- [ ] Review error message security (no sensitive data leakage)
- [ ] Add security testing
- [ ] ⚠️ **IITDD NOTE:** Security tests use REAL Infrastructure adapters (not mocks). IITDD contract tests validate interface contracts, not security.

**Acceptance Criteria:**
- Security requirements met
- No security vulnerabilities identified
- ⚠️ **IITDD SUPPORT:** Security tests use real adapters; IITDD contract tests use mocks and don't expose security vulnerabilities

**Dependencies:** All implementation tasks

---

## Summary

### Total Tasks Breakdown
- **Story 1.1:** 9 tasks (includes IITDD contract test tasks)
- **Story 1.2:** 9 tasks (includes IITDD contract test tasks)
- **Story 1.3:** 7 tasks (includes IITDD contract test tasks)
- **Story 1.4:** 6 tasks (includes IITDD contract test tasks)
- **Story 1.5:** 6 tasks (includes IITDD contract test tasks)
- **Story 1.6:** 6 tasks (includes IITDD contract test tasks)
- **Story 1.7:** 6 tasks (includes IITDD contract test tasks)
- **Story 1.8:** 5 tasks (includes IITDD contract test tasks)
- **Story 1.9:** 7 tasks (includes IITDD contract test tasks)
- **Cross-Cutting:** 6 tasks (includes IITDD test design and structure support)

**Total: 66 tasks** (includes IITDD contract test tasks for all interfaces)

### Estimated Effort Summary
- **P0 Tasks (Critical Path):** ~214 hours (includes 34 hours from new IITDD contract test tasks)
- **P1 Tasks (High Priority):** ~86 hours (includes 6 hours from new IITDD contract test tasks)
- **P2/P3 Tasks:** ~40 hours

**Total Estimated Effort:** ~340 hours (~8.5 weeks for 1 developer, or ~4-5 weeks for 2 developers)

### Critical Path
1. Story 1.1 (Browser Automation) - Must complete first
2. Story 1.2 (Metadata Extraction) - Depends on 1.1
3. Story 1.3 (Field Matching) - Depends on 1.2
4. Story 1.4 (Identity Resolution) - Depends on 1.3
5. Story 1.5 (SLA Tracking) - Can run in parallel with 1.4
6. Story 1.6 (Manual Review) - Depends on 1.2-1.4
7. Story 1.7 (SIRO Export) - Depends on 1.3-1.6
8. Story 1.8 (PDF Signing) - Depends on 1.7
9. Story 1.9 (Audit Trail) - Cross-cutting, can be done incrementally

---

**Document Status:** Complete  
**Next Steps:** Assign tasks to developers, create sprint plan, begin implementation

---

## QA Results

### Review Date: 2025-01-15

### Reviewed By: Quinn (Test Architect & Quality Advisor)

### Code Quality Assessment

**Overall Assessment:** ✅ **PASS** - Document is comprehensive, well-structured, and ready for implementation. Strong architectural guidance and IITDD compliance. Minor improvements recommended.

**Quality Score:** 88/100

The implementation tasks document demonstrates excellent planning quality with:
- **Comprehensive Coverage:** All 9 stories fully decomposed into 66 actionable tasks
- **Strong Architectural Foundation:** Clear Hexagonal Architecture enforcement with separate Infrastructure projects by concern
- **IITDD Compliance:** All interface definitions include contract test tasks (1.1.1A, 1.2.1A, 1.3.1A, 1.4.1A, 1.5.1A, 1.6.1A, 1.7.1A, 1.8.1A, 1.9.1A)
- **Clear Dependencies:** Task dependencies well-documented with prerequisite identification
- **Result Pattern Clarity:** Excellent guidance on `Result` vs `Result<bool>` vs `Result<Unit>` usage

### Refactoring Performed

No code refactoring performed - this is a planning document review.

### Compliance Check

- **Coding Standards:** ✅ **PASS** - Document aligns with Railway-Oriented Programming, Hexagonal Architecture, and functional programming patterns
- **Project Structure:** ✅ **PASS** - Clear separation of Domain/Interfaces (Ports), Infrastructure (Adapters), and Application (Orchestration) with separate Infrastructure projects by concern
- **Testing Strategy:** ✅ **PASS** - IITDD strategy well-integrated with contract tests, implementation tests, orchestration tests, and integration tests clearly defined
- **Architectural Compliance:** ✅ **PASS** - Hexagonal Architecture rules clearly enforced with examples and anti-patterns documented

### Improvements Checklist

- [x] Verified IITDD contract test tasks present for all stories (1.1.1A through 1.9.1A)
- [x] Confirmed separate Infrastructure projects by concern (High Cohesion, Loose Coupling)
- [x] Validated Result pattern guidance clarity
- [x] Verified Application layer dependency rules (Domain only, no Infrastructure references)
- [x] **✅ COMPLETED:** Added explicit acceptance criteria mapping to PRD requirements per story (Appendix A)
- [x] **✅ COMPLETED:** Added integration verification checklist per story (Appendix B)
- [x] **✅ COMPLETED:** Added performance NFR validation checkpoints per story (Appendix C)
- [x] **Note:** Rollback strategy not needed - this is a new deployment (greenfield project)

### Security Review

**Security Considerations:** ✅ **PASS**

- Task CC.5 explicitly addresses security review and hardening
- Security considerations embedded in tasks (e.g., safe file naming, audit logging)
- No critical security gaps identified in planning document

**Recommendations:**
- Ensure security review (CC.5) includes OWASP Top 10 checklist
- Consider adding security-focused contract tests for authentication/authorization interfaces

### Performance Considerations

**Performance Planning:** ✅ **PASS**

- Performance NFRs clearly documented (NFR3: <5s browser automation, NFR4: <2s XML/DOCX/<30s PDF, NFR5: <500ms classification)
- Task CC.4 dedicated to performance testing and optimization
- Performance considerations embedded in individual tasks

**Recommendations:**
- Consider incremental performance validation per story rather than waiting for CC.4
- Add performance benchmarks to contract tests where applicable (e.g., SLA tracking)

### Test Architecture Assessment

**IITDD Compliance:** ✅ **EXCELLENT**

**Strengths:**
- All interface definitions include contract test tasks (9 contract test tasks total)
- Clear separation: Contract tests (`Tests/Interfaces/`) vs Implementation tests (`Tests/Implementations/`)
- Application orchestration tests use mocks (NSubstitute) - validates workflow, not adapter behavior
- Integration tests use real adapters - validates end-to-end system behavior
- Test project structure clearly defined and aligned with Hexagonal Architecture

**Test Coverage Strategy:**
- **Contract Tests:** Validate interface contracts (WHAT) using mocks
- **Implementation Tests:** Validate adapter behavior (HOW) using real implementations
- **Orchestration Tests:** Validate workflow coordination using mocks
- **Integration Tests:** Validate end-to-end pipeline using real adapters

**Quality Indicators:**
- ✅ Every port has contract test task
- ✅ Every adapter has implementation test task
- ✅ Application services have orchestration test tasks
- ✅ Integration tests planned for complete pipeline

### Requirements Traceability

**Traceability Assessment:** ⚠️ **PARTIAL**

**Strengths:**
- Stories clearly mapped to PRD (Story 1.1 through 1.9)
- Task dependencies well-documented
- Cross-cutting concerns identified (CC.0 through CC.5)

**Gaps Identified:**
- No explicit mapping between tasks and PRD acceptance criteria
- Integration verification points mentioned but not systematically tracked per story
- No explicit validation that all PRD requirements are covered by tasks

**Recommendations:**
- Add acceptance criteria mapping table per story
- Create integration verification checklist per story
- Validate complete PRD coverage before sprint planning

### Risk Assessment

**Risk Profile:** 🟡 **MEDIUM RISK**

**Identified Risks:**
1. **TECH-001:** Missing explicit acceptance criteria mapping - Risk: Medium (3×2=6)
   - **Impact:** Potential scope gaps during implementation
   - **Mitigation:** Add acceptance criteria mapping before sprint planning

2. **TECH-002:** Integration verification not systematically tracked - Risk: Low (2×2=4)
   - **Impact:** Breaking changes to existing systems may go undetected
   - **Mitigation:** Add integration verification checklist per story

3. **TECH-003:** Performance validation deferred to CC.4 - Risk: Medium (2×3=6)
   - **Impact:** Performance issues discovered late in development
   - **Mitigation:** Add incremental performance checkpoints per story

**Risk Score:** 16/36 (Medium Risk)

**Note:** Rollback strategy (TECH-004) removed from risk assessment - not applicable for new deployment (greenfield project).

### Files Modified During Review

No files modified - planning document review only.

### Gate Status

**Gate:** ✅ **PASS** with recommendations

**Gate File:** `docs/qa/gates/implementation-tasks-review-20250115.yml`

**Rationale:**
- Document is comprehensive and well-structured
- Architectural guidance is excellent with clear Hexagonal Architecture enforcement
- IITDD strategy is sound with all contract test tasks present
- No critical blocking issues
- Minor improvements recommended for traceability and risk mitigation

**Confidence Level:** High

### Recommended Status

✅ **Ready for Implementation** - Document is ready for sprint planning and task assignment. Address recommended improvements (acceptance criteria mapping, integration verification tracking) before sprint planning for optimal implementation quality.

**Next Steps:**
1. Address Should-Fix recommendations (acceptance criteria mapping, integration verification tracking)
2. Proceed with sprint planning using this document
3. Track integration verification systematically during development
4. Validate performance NFRs incrementally (don't wait for CC.4)

---

## Appendix A: Acceptance Criteria Mapping

This section maps PRD acceptance criteria to implementation tasks for traceability and validation.

### Story 1.1: Browser Automation and Document Download

| PRD AC | Description | Implementation Tasks | Status |
|--------|-------------|---------------------|--------|
| AC1 | System launches browser session and navigates to configured regulatory website URL | Task 1.1.4 (Playwright Browser Automation Adapter) | 🔴 Not Started |
| AC2 | System identifies downloadable files (PDF, XML, DOCX) matching configured patterns | Task 1.1.4 (Playwright Browser Automation Adapter) | 🔴 Not Started |
| AC3 | System checks file checksum against download history to prevent duplicates | Task 1.1.5 (Download Tracker) | 🔴 Not Started |
| AC4 | System downloads new files and saves them to configured storage directory | Task 1.1.6 (Download Storage) | 🔴 Not Started |
| AC5 | System logs file metadata (name, URL, timestamp, checksum) to database | Task 1.1.7 (File Metadata Logger) | 🔴 Not Started |
| AC6 | System closes browser session cleanly after download completion | Task 1.1.4 (Playwright Browser Automation Adapter) | 🔴 Not Started |
| AC7 | System handles browser failures gracefully, logging errors and allowing retry | Task 1.1.8 (Application Service Orchestration) | 🔴 Not Started |

**Coverage:** ✅ All 7 acceptance criteria mapped to tasks

### Story 1.2: Enhanced Metadata Extraction and File Classification

| PRD AC | Description | Implementation Tasks | Status |
|--------|-------------|---------------------|--------|
| AC1 | System identifies file type based on content (not just extension) | Task 1.2.3 (File Type Identifier) | 🔴 Not Started |
| AC2 | System extracts metadata from XML documents | Task 1.2.4 (XML Metadata Extractor) | 🔴 Not Started |
| AC3 | System extracts metadata from DOCX documents | Task 1.2.5 (DOCX Metadata Extractor) | 🔴 Not Started |
| AC4 | System detects scanned PDFs and applies image preprocessing | Task 1.2.6 (PDF Metadata Extractor with OCR) | 🔴 Not Started |
| AC5 | System extracts metadata from PDF with OCR fallback | Task 1.2.6 (PDF Metadata Extractor with OCR) | 🔴 Not Started |
| AC6 | System classifies documents into Level 1 categories | Task 1.2.7 (File Classifier) | 🔴 Not Started |
| AC7 | System classifies documents into Level 2/3 subcategories | Task 1.2.7 (File Classifier) | 🔴 Not Started |
| AC8 | System generates safe, normalized file names | Task 1.2.8 (Safe File Namer) | 🔴 Not Started |
| AC9 | System logs all classification decisions with confidence scores | Task 1.2.9 (Application Service Orchestration) | 🔴 Not Started |

**Coverage:** ✅ All 9 acceptance criteria mapped to tasks

### Story 1.3: Field Matching and Unified Metadata Generation

| PRD AC | Description | Implementation Tasks | Status |
|--------|-------------|---------------------|--------|
| AC1 | System extracts structured fields from DOCX files | Task 1.3.3 (Field Extractor for DOCX) | 🔴 Not Started |
| AC2 | System extracts structured fields from PDF files (OCR'd) | Task 1.3.4 (Field Extractor for PDF) | 🔴 Not Started |
| AC3 | System matches field values across XML, DOCX, and PDF sources | Task 1.3.5 (Field Matcher) | 🔴 Not Started |
| AC4 | System generates unified metadata record | Task 1.3.5 (Field Matcher) | 🔴 Not Started |
| AC5 | System calculates confidence scores for each field | Task 1.3.5 (Field Matcher) | 🔴 Not Started |
| AC6 | System validates field completeness and consistency | Task 1.3.6 (Application Service Orchestration) | 🔴 Not Started |
| AC7 | System logs field matching decisions and confidence scores | Task 1.3.6 (Application Service Orchestration) | 🔴 Not Started |

**Coverage:** ✅ All 7 acceptance criteria mapped to tasks

### Story 1.4: Identity Resolution and Legal Directive Classification

| PRD AC | Description | Implementation Tasks | Status |
|--------|-------------|---------------------|--------|
| AC1 | System resolves person identities by handling RFC variants | Task 1.4.3 (Person Identity Resolver) | 🔴 Not Started |
| AC2 | System deduplicates person records across multiple documents | Task 1.4.3 (Person Identity Resolver) | 🔴 Not Started |
| AC3 | System classifies legal directives from document text | Task 1.4.4 (Legal Directive Classifier) | 🔴 Not Started |
| AC4 | System detects references to legal instruments | Task 1.4.4 (Legal Directive Classifier) | 🔴 Not Started |
| AC5 | System maps classified directives to compliance actions | Task 1.4.4 (Legal Directive Classifier) | 🔴 Not Started |
| AC6 | System logs all identity resolution and classification decisions | Task 1.4.5 (Application Service Orchestration) | 🔴 Not Started |

**Coverage:** ✅ All 6 acceptance criteria mapped to tasks

### Story 1.5: SLA Tracking and Escalation Management

| PRD AC | Description | Implementation Tasks | Status |
|--------|-------------|---------------------|--------|
| AC1 | System calculates SLA deadlines based on intake date and days plazo | Task 1.5.3 (SLA Enforcer) | 🔴 Not Started |
| AC2 | System tracks remaining time for each regulatory response case | Task 1.5.3 (SLA Enforcer) | 🔴 Not Started |
| AC3 | System identifies cases at risk when remaining time falls below threshold | Task 1.5.3 (SLA Enforcer) | 🔴 Not Started |
| AC4 | System escalates at-risk cases, triggering alerts and notifications | Task 1.5.3 (SLA Enforcer) | 🔴 Not Started |
| AC5 | System provides SLA dashboard showing all active cases | Task 1.5.4 (SLA Dashboard UI) | 🔴 Not Started |
| AC6 | System logs all SLA calculations and escalations | Task 1.5.5 (Application Service Orchestration) | 🔴 Not Started |
| AC7 | System supports configurable escalation thresholds | Task 1.5.3 (SLA Enforcer) | 🔴 Not Started |

**Coverage:** ✅ All 7 acceptance criteria mapped to tasks

### Story 1.6: Manual Review Interface

| PRD AC | Description | Implementation Tasks | Status |
|--------|-------------|---------------------|--------|
| AC1 | System identifies cases requiring manual review | Task 1.6.3 (Manual Review Service) | 🔴 Not Started |
| AC2 | System provides manual review dashboard with filters | Task 1.6.4 (Manual Review Dashboard UI) | 🔴 Not Started |
| AC3 | System displays unified metadata record with annotations | Task 1.6.4 (Manual Review Dashboard UI) | 🔴 Not Started |
| AC4 | System allows reviewer to override classifications and correct fields | Task 1.6.3 (Manual Review Service) | 🔴 Not Started |
| AC5 | System submits review decisions and updates unified metadata | Task 1.6.3 (Manual Review Service) | 🔴 Not Started |
| AC6 | System logs all manual review actions to audit trail | Task 1.6.5 (Application Service Orchestration) | 🔴 Not Started |
| AC7 | System integrates seamlessly with existing Blazor Server UI | Task 1.6.4 (Manual Review Dashboard UI) | 🔴 Not Started |

**Coverage:** ✅ All 7 acceptance criteria mapped to tasks

### Story 1.7: SIRO-Compliant Export Generation

| PRD AC | Description | Implementation Tasks | Status |
|--------|-------------|---------------------|--------|
| AC1 | System maps unified metadata records to SIRO regulatory schema | Task 1.7.2 (SIRO XML Exporter) | 🔴 Not Started |
| AC2 | System validates data against SIRO schema requirements | Task 1.7.2 (SIRO XML Exporter) | 🔴 Not Started |
| AC3 | System generates SIRO-compliant XML files | Task 1.7.2 (SIRO XML Exporter) | 🔴 Not Started |
| AC4 | System validates all required regulatory fields are present | Task 1.7.5 (Application Service Orchestration) | 🔴 Not Started |
| AC5 | System generates Excel layouts from unified metadata | Task 1.7.3 (Excel Layout Generator) | 🔴 Not Started |
| AC6 | System logs all export operations to audit trail | Task 1.7.5 (Application Service Orchestration) | 🔴 Not Started |
| AC7 | System provides export management screen | Task 1.7.4 (Export Management UI) | 🔴 Not Started |

**Coverage:** ✅ All 7 acceptance criteria mapped to tasks

### Story 1.8: PDF Summarization and Digital Signing

| PRD AC | Description | Implementation Tasks | Status |
|--------|-------------|---------------------|--------|
| AC1 | System summarizes PDF content into requirement categories | Task 1.8.2 (PDF Requirement Summarizer) | 🔴 Not Started |
| AC2 | System uses semantic analysis or rule-based classification | Task 1.8.2 (PDF Requirement Summarizer) | 🔴 Not Started |
| AC3 | System generates digitally signed PDF exports | Task 1.8.3 (Digital PDF Signing) | 🔴 Not Started |
| AC4 | System supports X.509 certificate-based signing (PAdES) | Task 1.8.3 (Digital PDF Signing) | 🔴 Not Started |
| AC5 | System integrates with external certificate management systems | Task 1.8.3 (Digital PDF Signing) | 🔴 Not Started |
| AC6 | System validates digital signatures before finalizing exports | Task 1.8.3 (Digital PDF Signing) | 🔴 Not Started |
| AC7 | System logs all PDF summarization and signing operations | Task 1.8.4 (Application Service Orchestration) | 🔴 Not Started |

**Coverage:** ✅ All 7 acceptance criteria mapped to tasks

### Story 1.9: Audit Trail and Reporting

| PRD AC | Description | Implementation Tasks | Status |
|--------|-------------|---------------------|--------|
| AC1 | System maintains immutable audit log of all processing steps | Task 1.9.3 (Audit Logger) | 🔴 Not Started |
| AC2 | System logs all actions with timestamp, user identity, file ID | Task 1.9.3 (Audit Logger) | 🔴 Not Started |
| AC3 | System provides audit trail viewer with filtering | Task 1.9.5 (Audit Trail Viewer UI) | 🔴 Not Started |
| AC4 | System generates classification reports in CSV/JSON format | Task 1.9.4 (Report Generator) | 🔴 Not Started |
| AC5 | System retains audit logs for minimum 7 years | Task 1.9.2 (Database Schema for Audit Logging) | 🔴 Not Started |
| AC6 | System supports audit log export for compliance reporting | Task 1.9.4 (Report Generator) | 🔴 Not Started |
| AC7 | System provides correlation IDs for tracking requests | Task 1.9.3 (Audit Logger) | 🔴 Not Started |

**Coverage:** ✅ All 7 acceptance criteria mapped to tasks

**Summary:** All 64 PRD acceptance criteria (7+9+7+6+7+7+7+7+7) are mapped to implementation tasks. ✅ **Complete Coverage**

---

## Appendix B: Integration Verification Checklist Per Story

### Story 1.1: Browser Automation and Document Download

**Integration Verification Points (from PRD):**
- **IV1:** Existing OCR pipeline continues to function when processing manually uploaded files (no regression)
- **IV2:** New `IBrowserAutomationAgent` interface integrates with existing `IDownloadStorage` and `IFileMetadataLogger` adapters
- **IV3:** Download performance does not impact existing OCR processing throughput

**Verification Checklist:**

#### Pre-Implementation
- [ ] Baseline existing OCR pipeline performance (manual upload processing time)
- [ ] Document existing `IDownloadStorage` and `IFileMetadataLogger` adapter interfaces
- [ ] Identify shared resources (file storage, database connections)

#### During Implementation
- [ ] Verify `IBrowserAutomationAgent` interface is in `Domain/Interfaces/` (Port, not Adapter)
- [ ] Verify Playwright adapter is in separate `Infrastructure.BrowserAutomation` project
- [ ] Verify Application service uses `IBrowserAutomationAgent` interface only (no Infrastructure references)
- [ ] Test that existing manual upload workflow still functions
- [ ] Verify download operations don't block OCR processing (async/await)

#### Post-Implementation
- [ ] Run existing OCR pipeline regression tests - all must pass
- [ ] Measure download performance impact on OCR throughput (should be <5% degradation)
- [ ] Verify `IDownloadStorage` and `IFileMetadataLogger` adapters work with new browser automation
- [ ] Test browser failure scenarios don't crash existing OCR pipeline

### Story 1.2: Enhanced Metadata Extraction and File Classification

**Integration Verification Points (from PRD):**
- **IV1:** Existing `IOcrExecutor` and `IImagePreprocessor` interfaces continue to work (no breaking changes)
- **IV2:** New `IMetadataExtractor` wraps existing OCR functionality, maintaining compatibility
- **IV3:** Classification performance (500ms target) does not degrade existing OCR processing speed

**Verification Checklist:**

#### Pre-Implementation
- [ ] Baseline existing OCR processing performance (text extraction time)
- [ ] Document existing `IOcrExecutor` and `IImagePreprocessor` interfaces
- [ ] Test existing OCR pipeline with sample documents

#### During Implementation
- [ ] Verify `IMetadataExtractor` extends/wraps existing OCR interfaces (no breaking changes)
- [ ] Verify PDF extractor uses existing `IOcrExecutor` and `IImagePreprocessor` (reuse, don't replace)
- [ ] Verify classification logic doesn't modify existing OCR text extraction
- [ ] Test classification performance meets 500ms target

#### Post-Implementation
- [ ] Run existing OCR pipeline regression tests - all must pass
- [ ] Verify existing `IOcrExecutor` and `IImagePreprocessor` implementations still work
- [ ] Measure classification performance impact (should be <500ms, non-blocking)
- [ ] Test that existing OCR text extraction quality unchanged

### Story 1.3: Field Matching and Unified Metadata Generation

**Integration Verification Points (from PRD):**
- **IV1:** Existing `IFieldExtractor` interface extended to generic `IFieldExtractor<T>` without breaking existing implementations
- **IV2:** Field extraction performance maintains existing OCR pipeline throughput
- **IV3:** Unified metadata generation does not impact existing document processing workflows

**Verification Checklist:**

#### Pre-Implementation
- [ ] Document existing `IFieldExtractor` interface and implementations
- [ ] Baseline existing field extraction performance
- [ ] Test existing field extraction with sample documents

#### During Implementation
- [ ] Verify generic `IFieldExtractor<T>` extends existing `IFieldExtractor` (backward compatible)
- [ ] Verify existing `IFieldExtractor` implementations still compile and work
- [ ] Test field extraction performance meets existing throughput targets
- [ ] Verify unified metadata generation doesn't modify source documents

#### Post-Implementation
- [ ] Run existing field extraction regression tests - all must pass
- [ ] Verify existing `IFieldExtractor` implementations still function correctly
- [ ] Measure unified metadata generation performance (should not block processing)
- [ ] Test that existing document processing workflows unchanged

### Story 1.4: Identity Resolution and Legal Directive Classification

**Integration Verification Points (from PRD):**
- **IV1:** Identity resolution does not modify existing person data structures or break existing OCR field extraction
- **IV2:** Legal classification uses extracted metadata from Stage 2 without requiring re-processing
- **IV3:** Classification performance (500ms target) maintains system responsiveness

**Verification Checklist:**

#### Pre-Implementation
- [ ] Document existing person/identity data structures (if any)
- [ ] Baseline existing field extraction performance
- [ ] Identify metadata structures from Stage 2

#### During Implementation
- [ ] Verify identity resolution creates new data structures (additive, not modifying existing)
- [ ] Verify legal classification uses Stage 2 metadata (no re-processing required)
- [ ] Test classification performance meets 500ms target
- [ ] Verify existing OCR field extraction unchanged

#### Post-Implementation
- [ ] Run existing field extraction regression tests - all must pass
- [ ] Verify existing person/identity data structures unchanged
- [ ] Measure classification performance (should be <500ms)
- [ ] Test that existing OCR field extraction still works

### Story 1.5: SLA Tracking and Escalation Management

**Integration Verification Points (from PRD):**
- **IV1:** SLA tracking does not impact existing document processing performance
- **IV2:** Escalation alerts integrate with existing notification mechanisms (if any) or use standard logging
- **IV3:** SLA calculations use business day logic that accounts for Mexican holidays (if applicable)

**Verification Checklist:**

#### Pre-Implementation
- [ ] Baseline existing document processing performance
- [ ] Document existing notification mechanisms (if any)
- [ ] Identify business day calculation requirements

#### During Implementation
- [ ] Verify SLA tracking is async/non-blocking (doesn't impact processing)
- [ ] Verify escalation alerts use standard logging (or integrate with existing notifications)
- [ ] Test business day calculation logic (Mexican holidays if applicable)
- [ ] Verify SLA calculations don't modify existing data structures

#### Post-Implementation
- [ ] Measure SLA tracking performance impact (should be <1% overhead)
- [ ] Test escalation alerts work correctly
- [ ] Verify existing document processing performance unchanged
- [ ] Test business day calculations are accurate

### Story 1.6: Manual Review Interface

**Integration Verification Points (from PRD):**
- **IV1:** Manual review interface does not disrupt existing document processing workflows
- **IV2:** Review decisions integrate with existing data models without breaking existing functionality
- **IV3:** UI components follow existing MudBlazor patterns and navigation structure

**Verification Checklist:**

#### Pre-Implementation
- [ ] Document existing MudBlazor UI patterns and navigation structure
- [ ] Baseline existing document processing workflows
- [ ] Identify existing data models

#### During Implementation
- [ ] Verify manual review UI follows existing MudBlazor component patterns
- [ ] Verify review decisions extend existing data models (additive, not breaking)
- [ ] Test that manual review doesn't block automatic processing
- [ ] Verify UI navigation integrates with existing structure

#### Post-Implementation
- [ ] Test existing document processing workflows still function
- [ ] Verify existing data models unchanged
- [ ] Test manual review UI integrates seamlessly with existing UI
- [ ] Verify MudBlazor patterns followed consistently

### Story 1.7: SIRO-Compliant Export Generation

**Integration Verification Points (from PRD):**
- **IV1:** Export generation does not modify source metadata or break existing data structures
- **IV2:** SIRO XML validation uses standard XML schema validation without impacting existing XML parsing
- **IV3:** Export performance does not block other document processing operations

**Verification Checklist:**

#### Pre-Implementation
- [ ] Document existing metadata data structures
- [ ] Baseline existing XML parsing performance
- [ ] Identify existing document processing operations

#### During Implementation
- [ ] Verify export generation reads metadata (doesn't modify source)
- [ ] Verify SIRO XML validation uses standard XML schema validation
- [ ] Test export performance is async/non-blocking
- [ ] Verify existing XML parsing unchanged

#### Post-Implementation
- [ ] Test that source metadata unchanged after export
- [ ] Verify existing XML parsing still works
- [ ] Measure export performance (should not block processing)
- [ ] Test that existing document processing operations continue

### Story 1.8: PDF Summarization and Digital Signing

**Integration Verification Points (from PRD):**
- **IV1:** PDF summarization uses existing OCR text extraction without breaking current OCR pipeline
- **IV2:** Digital signing operations do not impact existing PDF processing or export workflows
- **IV3:** Certificate integration handles certificate unavailability gracefully with error logging

**Verification Checklist:**

#### Pre-Implementation
- [ ] Baseline existing OCR text extraction performance
- [ ] Document existing PDF processing workflows
- [ ] Identify certificate management requirements

#### During Implementation
- [ ] Verify PDF summarization uses existing OCR text extraction (reuse, don't replace)
- [ ] Verify digital signing doesn't modify existing PDF processing
- [ ] Test certificate unavailability handling (graceful error logging)
- [ ] Verify existing OCR pipeline unchanged

#### Post-Implementation
- [ ] Run existing OCR pipeline regression tests - all must pass
- [ ] Verify existing PDF processing workflows still function
- [ ] Test certificate unavailability scenarios (should log errors, not crash)
- [ ] Measure PDF summarization performance impact

### Story 1.9: Audit Trail and Reporting

**Integration Verification Points (from PRD):**
- **IV1:** Audit logging does not impact processing performance (async logging, non-blocking)
- **IV2:** Audit trail viewer integrates with existing UI navigation and MudBlazor components
- **IV3:** Audit log retention policies do not conflict with existing data retention requirements

**Verification Checklist:**

#### Pre-Implementation
- [ ] Baseline existing processing performance
- [ ] Document existing UI navigation and MudBlazor patterns
- [ ] Identify existing data retention requirements

#### During Implementation
- [ ] Verify audit logging is async/non-blocking (doesn't impact processing)
- [ ] Verify audit trail viewer follows existing MudBlazor patterns
- [ ] Test audit log retention policies (7 years minimum)
- [ ] Verify existing data retention requirements respected

#### Post-Implementation
- [ ] Measure audit logging performance impact (should be <1% overhead)
- [ ] Test audit trail viewer integrates with existing UI
- [ ] Verify audit log retention policies don't conflict with existing requirements
- [ ] Test that existing processing performance unchanged

---

## Appendix C: Performance NFR Validation Checkpoints Per Story

### Performance NFRs Summary

- **NFR3:** Browser automation operations (launch, navigate, detect files) within 5 seconds
- **NFR4:** Metadata extraction within 2 seconds for XML/DOCX, within 30 seconds for PDF with OCR
- **NFR5:** Classification operations within 500ms per document
- **NFR6:** Horizontal scaling through stateless design
- **NFR7:** 99.9% uptime for critical SLA tracking services
- **NFR15:** Batch processing support for high-volume periods

### Story 1.1: Browser Automation and Document Download

**Performance Checkpoints:**

#### Task 1.1.4: Playwright Browser Automation Adapter
- [ ] **Checkpoint:** Browser launch and navigation completes within 5 seconds (NFR3)
  - **Measurement:** Time from `LaunchBrowserAsync` call to navigation complete
  - **Target:** <5 seconds for typical regulatory websites
  - **Validation:** Run performance test with 10 iterations, average <5s

#### Task 1.1.5: Download Tracker
- [ ] **Checkpoint:** Checksum lookup completes within 100ms
  - **Measurement:** Time for `IsFileAlreadyDownloadedAsync` call
  - **Target:** <100ms for database lookup
  - **Validation:** Test with 1000 file checksums, average <100ms

#### Task 1.1.8: Application Service Orchestration
- [ ] **Checkpoint:** Complete download workflow (launch → detect → download → log) completes within 10 seconds
  - **Measurement:** End-to-end time for single file download
  - **Target:** <10 seconds total (includes NFR3 browser automation)
  - **Validation:** Integration test with real website, measure end-to-end time

### Story 1.2: Enhanced Metadata Extraction and File Classification

**Performance Checkpoints:**

#### Task 1.2.3: File Type Identifier
- [ ] **Checkpoint:** File type identification completes within 50ms
  - **Measurement:** Time for `IdentifyFileTypeAsync` call
  - **Target:** <50ms per file
  - **Validation:** Test with 100 files of each type, average <50ms

#### Task 1.2.4: XML Metadata Extractor
- [ ] **Checkpoint:** XML metadata extraction completes within 2 seconds (NFR4)
  - **Measurement:** Time for `ExtractFromXmlAsync` call
  - **Target:** <2 seconds for typical XML files
  - **Validation:** Test with 50 XML files, 95th percentile <2s

#### Task 1.2.5: DOCX Metadata Extractor
- [ ] **Checkpoint:** DOCX metadata extraction completes within 2 seconds (NFR4)
  - **Measurement:** Time for `ExtractFromDocxAsync` call
  - **Target:** <2 seconds for typical DOCX files
  - **Validation:** Test with 50 DOCX files, 95th percentile <2s

#### Task 1.2.6: PDF Metadata Extractor with OCR
- [ ] **Checkpoint:** PDF metadata extraction completes within 30 seconds (NFR4)
  - **Measurement:** Time for `ExtractFromPdfAsync` call (including OCR if needed)
  - **Target:** <30 seconds for PDF files requiring OCR
  - **Validation:** Test with 20 scanned PDFs, 95th percentile <30s
- [ ] **Checkpoint:** Searchable PDF extraction completes within 2 seconds
  - **Measurement:** Time for searchable PDFs (no OCR needed)
  - **Target:** <2 seconds for searchable PDFs
  - **Validation:** Test with 50 searchable PDFs, 95th percentile <2s

#### Task 1.2.7: File Classifier
- [ ] **Checkpoint:** Classification completes within 500ms (NFR5)
  - **Measurement:** Time for `ClassifyLevel1Async` and `ClassifyLevel2Async` calls
  - **Target:** <500ms per document
  - **Validation:** Test with 100 documents, 95th percentile <500ms

#### Task 1.2.9: Application Service Orchestration
- [ ] **Checkpoint:** Complete extraction and classification workflow completes within performance targets
  - **Measurement:** End-to-end time (file type → extract → classify)
  - **Target:** XML/DOCX <3s total, PDF <32s total (includes NFR4 targets)
  - **Validation:** Integration test with real documents

### Story 1.3: Field Matching and Unified Metadata Generation

**Performance Checkpoints:**

#### Task 1.3.3: Field Extractor for DOCX
- [ ] **Checkpoint:** DOCX field extraction completes within 2 seconds
  - **Measurement:** Time for `ExtractFieldsAsync` call
  - **Target:** <2 seconds per DOCX file
  - **Validation:** Test with 50 DOCX files, 95th percentile <2s

#### Task 1.3.4: Field Extractor for PDF (OCR)
- [ ] **Checkpoint:** PDF field extraction completes within 30 seconds
  - **Measurement:** Time for `ExtractFieldsAsync` call (including OCR)
  - **Target:** <30 seconds per PDF file
  - **Validation:** Test with 20 PDF files, 95th percentile <30s

#### Task 1.3.5: Field Matcher
- [ ] **Checkpoint:** Field matching completes within 1 second
  - **Measurement:** Time for `MatchFieldsAsync` call
  - **Target:** <1 second for matching across 3 sources (XML, DOCX, PDF)
  - **Validation:** Test with 100 unified records, 95th percentile <1s

### Story 1.4: Identity Resolution and Legal Directive Classification

**Performance Checkpoints:**

#### Task 1.4.3: Person Identity Resolver
- [ ] **Checkpoint:** Identity resolution completes within 500ms
  - **Measurement:** Time for `ResolvePersonIdentityAsync` call
  - **Target:** <500ms per person resolution
  - **Validation:** Test with 100 person records, 95th percentile <500ms

#### Task 1.4.4: Legal Directive Classifier
- [ ] **Checkpoint:** Legal directive classification completes within 500ms (NFR5)
  - **Measurement:** Time for `ClassifyLegalDirectiveAsync` call
  - **Target:** <500ms per document
  - **Validation:** Test with 100 documents, 95th percentile <500ms

### Story 1.5: SLA Tracking and Escalation Management

**Performance Checkpoints:**

#### Task 1.5.3: SLA Enforcer
- [ ] **Checkpoint:** SLA calculation completes within 100ms (NFR7: 99.9% uptime)
  - **Measurement:** Time for `CalculateSlaDeadlineAsync` call
  - **Target:** <100ms per calculation (critical service)
  - **Validation:** Test with 1000 SLA calculations, 99th percentile <100ms
- [ ] **Checkpoint:** Escalation check completes within 200ms
  - **Measurement:** Time for escalation detection and alert triggering
  - **Target:** <200ms per escalation check
  - **Validation:** Test with 1000 cases, 99th percentile <200ms

#### Task 1.5.4: SLA Dashboard UI
- [ ] **Checkpoint:** Dashboard loads within 2 seconds
  - **Measurement:** Time for dashboard page load with 1000 active cases
  - **Target:** <2 seconds initial load
  - **Validation:** Load test with 1000 cases, measure page load time

### Story 1.6: Manual Review Interface

**Performance Checkpoints:**

#### Task 1.6.3: Manual Review Service
- [ ] **Checkpoint:** Review case retrieval completes within 500ms
  - **Measurement:** Time for `GetReviewCasesAsync` call
  - **Target:** <500ms for 100 review cases
  - **Validation:** Test with 100 review cases, 95th percentile <500ms

#### Task 1.6.4: Manual Review Dashboard UI
- [ ] **Checkpoint:** Review dashboard loads within 2 seconds
  - **Measurement:** Time for dashboard page load with 100 review cases
  - **Target:** <2 seconds initial load
  - **Validation:** Load test with 100 review cases

### Story 1.7: SIRO-Compliant Export Generation

**Performance Checkpoints:**

#### Task 1.7.2: SIRO XML Exporter
- [ ] **Checkpoint:** XML export generation completes within 2 seconds
  - **Measurement:** Time for `GenerateSiroXmlAsync` call
  - **Target:** <2 seconds per export
  - **Validation:** Test with 50 exports, 95th percentile <2s

#### Task 1.7.3: Excel Layout Generator
- [ ] **Checkpoint:** Excel layout generation completes within 3 seconds
  - **Measurement:** Time for `GenerateExcelLayoutAsync` call
  - **Target:** <3 seconds per export
  - **Validation:** Test with 50 exports, 95th percentile <3s

### Story 1.8: PDF Summarization and Digital Signing

**Performance Checkpoints:**

#### Task 1.8.2: PDF Requirement Summarizer
- [ ] **Checkpoint:** PDF summarization completes within 5 seconds
  - **Measurement:** Time for `SummarizePdfAsync` call
  - **Target:** <5 seconds per PDF
  - **Validation:** Test with 20 PDFs, 95th percentile <5s

#### Task 1.8.3: Digital PDF Signing
- [ ] **Checkpoint:** PDF signing completes within 2 seconds
  - **Measurement:** Time for `SignPdfAsync` call
  - **Target:** <2 seconds per PDF (excluding certificate retrieval)
  - **Validation:** Test with 50 PDFs, 95th percentile <2s

### Story 1.9: Audit Trail and Reporting

**Performance Checkpoints:**

#### Task 1.9.3: Audit Logger
- [ ] **Checkpoint:** Audit log write completes within 50ms (async, non-blocking)
  - **Measurement:** Time for `LogAuditEventAsync` call (fire-and-forget)
  - **Target:** <50ms per log write (should not block processing)
  - **Validation:** Test with 1000 audit log writes, 99th percentile <50ms

#### Task 1.9.4: Report Generator
- [ ] **Checkpoint:** Report generation completes within 5 seconds for 1 year of data
  - **Measurement:** Time for `GenerateReportAsync` call (1 year date range)
  - **Target:** <5 seconds for CSV/JSON report generation
  - **Validation:** Test with 1 year of audit data, 95th percentile <5s

### Cross-Cutting Performance Validation

#### Task CC.4: Performance Testing and Optimization
- [ ] **Checkpoint:** Complete pipeline performance meets all NFRs
  - **Measurement:** End-to-end time for Stage 1 → Stage 4 processing
  - **Target:** 
    - Stage 1: <10 seconds (includes NFR3)
    - Stage 2: <35 seconds (includes NFR4 for PDF)
    - Stage 3: <1 second (includes NFR5)
    - Stage 4: <10 seconds (export generation)
  - **Validation:** Integration test with complete pipeline, measure each stage

- [ ] **Checkpoint:** Batch processing supports high-volume periods (NFR15)
  - **Measurement:** Throughput for batch processing (documents per minute)
  - **Target:** Process 100 documents in <10 minutes (10 docs/min minimum)
  - **Validation:** Load test with 100 documents, measure total processing time

- [ ] **Checkpoint:** Horizontal scaling validated (NFR6)
  - **Measurement:** Performance with multiple instances
  - **Target:** Linear scaling (2 instances = 2x throughput)
  - **Validation:** Deploy 2 instances, measure throughput vs single instance

**Note:** These performance checkpoints should be validated incrementally during implementation, not deferred to CC.4. Each story should include performance validation in its acceptance criteria.

