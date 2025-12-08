---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7]
inputDocuments:
  - 'docs/qa/prd.md'
  - 'docs/stories/epic-1-regulatory-compliance-automation-system.md'
  - 'docs/qa/xunit-v3-best-practices-research.md'
  - 'Fixtures/PRP2/PRP.txt'
  - 'Fixtures/PRP2/PRP.md'
workflowType: 'architecture'
lastStep: 7
project_name: 'ExxerCube.Prisma.Veriqan'
user_name: 'Abel Briones'
date: '2025-01-15'
expandedDate: '2025-01-24'
hasProjectContext: true
expansionNote: 'Architecture expanded with VEC Statement Processing details from PRP.md. Updated for compatibility with existing ExxerCube.Prisma codebase infrastructure.'
compatibilityNote: 'VEC components reuse existing Prisma infrastructure (OCR, extraction, imaging, database, events). Project namespaced as Prisma.Veriqan to indicate extension of existing system.'
externalDependencies:
  - 'IndFusion.Ember: F:\Dynamic\IndFusion\IndFusion.Ember\ - Transport hub abstraction (SignalR, TCP, MQTT, OPC)'
  - 'ExxerCube.Prisma: F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\ - Base infrastructure (OCR, extraction, imaging, database)'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**

The system encompasses two related but distinct functional domains:

1. **VEC Statement PDF Extraction & Validation (PRP.txt):**
   - Extract structured data from BSSB Bank PDF statements including client information, financial summaries, rates, transactions, and visual elements
   - Validate field presence, content correctness, formatting, and visual compliance
   - Generate structured outputs (CSV/TXT) and annotated PDF reports for non-conformities
   - Support batch processing of 100+ files per session

2. **Regulatory Compliance Automation (PRD):**
   - 20 Functional Requirements organized into 4 processing stages:
     - **Stage 1 (Ingestion):** Browser automation for document acquisition from UIF/CNBV websites
     - **Stage 2 (Extraction):** Enhanced metadata extraction, classification, and field matching across XML/DOCX/PDF
     - **Stage 3 (Decision Logic):** Identity resolution, legal directive classification, SLA tracking and escalation
     - **Stage 4 (Compliance Response):** SIRO-compliant XML/PDF export generation with digital signing

**Non-Functional Requirements:**

Critical NFRs that will drive architectural decisions:

- **Performance:** <5 seconds per PDF processing, <2s for XML/DOCX extraction, <500ms for classification, <5s for browser automation
- **Scalability:** Stateless design enabling horizontal scaling, microservices-ready architecture
- **Reliability:** 99.9% uptime for SLA tracking services, graceful error handling with Result<T> pattern
- **Security:** Encryption at rest and in transit (TLS 1.3), field-level PII encryption, role-based access control
- **Compliance:** 7-year audit log retention, SIRO schema compliance, non-notification enforcement
- **Compatibility:** Backward compatibility with existing OCR pipeline, Python-C# interop via CSnakes, Hexagonal Architecture boundaries

**Scale & Complexity:**

- **Primary domain:** Document processing / Financial compliance automation system
- **Complexity level:** Enterprise
- **Estimated architectural components:** 28+ interfaces (per PRD), 4 processing stages, real-time UI components, multiple integration points
- **Story structure:** 10 stories implementing the 4-stage workflow with clear dependencies and integration verification points

### Technical Constraints & Dependencies

**Existing Technology Stack:**
- .NET 10 (C#) with nullable reference types
- Python 3.9+ integration via CSnakes library
- Tesseract OCR engine
- Entity Framework Core (SQL Server/PostgreSQL)
- Blazor Server UI with MudBlazor components
- Playwright for browser automation (new dependency)

**Architectural Constraints:**
- Must maintain Hexagonal Architecture boundaries (Domain/Application/Infrastructure layers)
- Railway-Oriented Programming: All interfaces return `Result<T>` (no exceptions)
- Backward compatibility: Existing `IFieldExtractor`, `IOcrExecutor`, `IImagePreprocessor` interfaces must continue functioning
- Database schema changes must be additive-only (new tables, no modifications to existing)

**Processing Constraints:**
- PDF processing with OCR fallback for scanned documents
- Visual compliance validation (logo presence/quality, font matching, layout alignment)
- Mathematical validation (balance calculations, inter-statement continuity)
- Multi-format extraction (XML structured, DOCX field extraction, PDF OCR)

**Integration Dependencies:**
- Python modules: `prisma-ocr-pipeline`, `prisma-ai-extractors`, `prisma-document-generator`
- Browser automation: Playwright for UIF/CNBV website interaction
- Digital signatures: X.509 certificate management systems
- SIRO regulatory submission systems
- Notification systems for SLA escalations (email/SMS/Slack)

### Cross-Cutting Concerns Identified

1. **Document Processing Pipeline:** OCR, extraction, validation, classification across multiple formats
2. **Error Handling & Resilience:** Result<T> pattern throughout, graceful degradation, retry logic with exponential backoff
3. **Audit & Compliance:** Immutable audit logs with correlation IDs, 7-year retention, regulatory compliance tracking
4. **Security:** Encryption (at rest/in transit), role-based access control, sensitive data protection (RFC, account numbers)
5. **Performance:** Sub-5-second processing targets, batch optimization, async/await patterns, horizontal scaling capability
6. **Real-Time UI:** SignalR infrastructure for live updates, SLA dashboards, processing status notifications
7. **Multi-Format Support:** XML parsing, DOCX field extraction, PDF with OCR fallback, unified metadata generation
8. **Visual Validation:** Image quality checking, layout compliance, font matching (Aptos), overlap detection
9. **Identity Resolution:** RFC variant handling, person deduplication across documents, alias name matching
10. **Export Generation:** SIRO-compliant XML schema validation, digitally signed PDF (PAdES), Excel layout generation

## Starter Template Evaluation

### Primary Technology Domain

**Backend/Full-Stack .NET Application** - This is a brownfield enhancement to an existing .NET solution, not a greenfield project requiring a starter template.

### Starter Options Considered

**Not Applicable - Brownfield Project**

This project is an enhancement to an existing ExxerCube.Prisma system. We are not starting from a template, but rather extending the existing architecture with new components following established patterns.

**Existing Foundation Analysis:**

The project already has a solid foundation with:
- Established Hexagonal Architecture boundaries
- Railway-Oriented Programming patterns (Result<T>)
- Blazor Server UI with MudBlazor components
- Python-C# interop via CSnakes
- Entity Framework Core persistence layer
- xUnit v3 testing infrastructure

### Selected Approach: Extend Existing Architecture

**Rationale for Selection:**

Rather than using a starter template, we will:
1. Follow existing architectural patterns (Hexagonal Architecture, Result<T>)
2. Extend existing interfaces where possible (backward compatibility)
3. Add new components following established conventions
4. Maintain existing project structure and organization
5. Integrate new features within the current technology stack

**Architectural Decisions Already Established:**

**Language & Runtime:**
- .NET 10 with C# (nullable reference types enabled)
- Python 3.9+ for OCR/NLP modules
- CSnakes for Python-C# interop

**Architecture Pattern:**
- Hexagonal Architecture with clear layer boundaries
- Domain layer: Interfaces and entities
- Application layer: Orchestration services
- Infrastructure layer: Adapters and implementations

**Error Handling:**
- Railway-Oriented Programming: All interfaces return `Result<T>`
- No exceptions for business logic errors
- Fluent error handling patterns

**UI Framework:**
- Blazor Server with Interactive Server Render Mode
- MudBlazor component library
- SignalR for real-time updates

**Persistence:**
- Entity Framework Core
- Additive-only database schema changes
- Support for SQL Server and PostgreSQL

**Testing:**
- xUnit v3 framework
- Shouldly for assertions
- NSubstitute for mocking
- Library-based test infrastructure (no test project dependencies)

**Code Organization:**
- Interfaces in `Domain/Interfaces/`
- Entities in `Domain/Entities/`
- Application services in `Application/Services/`
- Infrastructure adapters in `Infrastructure/` organized by concern
- UI components in `UI/ExxerCube.Prisma.Web.UI/`

**Development Experience:**
- TreatWarningsAsErrors
- XML documentation required
- Structured logging with Serilog
- Async/await patterns throughout

**Note:** New components and features will be added following these established patterns. The architecture document will guide how new interfaces, services, and components integrate with the existing system.

## Existing Infrastructure Compatibility & Reuse

### Overview

The VEC Statement Processing system extends the existing ExxerCube.Prisma codebase rather than replacing it. This section documents which existing components are reused directly, which are extended, and which are net-new for VEC functionality.

### Existing Infrastructure Ready for Reuse

**Domain Layer Interfaces (Direct Reuse):**

| Interface | Location | VEC Use Case | Status |
|-----------|----------|--------------|--------|
| `IOcrExecutor` | `ExxerCube.Prisma.Domain/Interfaces/` | PDF text extraction via OCR | ✓ **Reuse existing** |
| `IImagePreprocessor` | `ExxerCube.Prisma.Domain/Interfaces/` | Image enhancement before OCR | ✓ **Reuse existing** |
| `IFieldExtractor<PdfSource>` | `ExxerCube.Prisma.Domain/Interfaces/` | Field extraction from PDFs | ✓ **Extend with VEC field definitions** |
| `IImageQualityAnalyzer` | `ExxerCube.Prisma.Domain/Interfaces/` | Image quality verification (REQ-029) | ✓ **Reuse existing** |
| `IOcrProcessingService` | `ExxerCube.Prisma.Domain/Interfaces/` | Full pipeline orchestration pattern | ✓ **Pattern reuse** |
| `IEventPublisher` | `ExxerCube.Prisma.Domain/Interfaces/` | Real-time event publishing | ✓ **Reuse existing** |

**Infrastructure Implementations (Direct Reuse):**

| Component | Location | VEC Use Case | Status |
|-----------|----------|--------------|--------|
| **GotOcr2OcrExecutor** | `Infrastructure.Extraction/GotOcr2/` | Extract text from VEC PDFs (REQ-004, REQ-005) | ✓ **Reuse existing** |
| **PdfOcrFieldExtractor** | `Infrastructure.Extraction/FieldExtractors/` | Extract header fields, transaction tables | ✓ **Extend for VEC** |
| **EmguCvImageQualityAnalyzer** | `Infrastructure.Imaging/` | Image quality verification (REQ-029) | ✓ **Reuse existing** |
| **Image Enhancement Filters** | `Infrastructure.Imaging/Filters/` | Enhance low-quality scanned PDFs | ✓ **Reuse existing** |
| **AuditLoggerService** | `Infrastructure.Database/` | 7-year audit trail (REQ-021) | ✓ **Reuse existing** |
| **FileMetadataLoggerService** | `Infrastructure.Database/` | Track processed statements | ✓ **Reuse existing** |
| **InMemoryEventBus** | `Infrastructure.Events/` | Real-time processing notifications | ✓ **Reuse existing** |
| **PrismaDbContext (EF Core)** | `Infrastructure.Database/` | Data persistence with additive schema | ✓ **Extend with VEC entities** |
| **IndFusion.Ember** | Existing package | Transport hub abstraction (SignalR, TCP, MQTT, OPC) | ✓ **Expand scope for VEC dashboards** |

**Python Integration (CSnakes Pattern - Reuse):**

| Component | Location | VEC Use Case | Status |
|-----------|----------|--------------|--------|
| **CSnakes.Runtime** | NuGet package | C# ↔ Python interop | ✓ **Reuse existing pattern** |
| **Python Environment Setup** | `Infrastructure.Python.GotOcr2/` | Virtual environment management | ✓ **Extend for VEC models** |
| **GotOcr2 Integration Pattern** | `Infrastructure.Extraction/GotOcr2/` | Python module invocation pattern | ✓ **Follow same pattern** |

**Testing Infrastructure (Direct Reuse):**

| Component | Location | VEC Use Case | Status |
|-----------|----------|--------------|--------|
| **xUnit v3 Framework** | All test projects | Test framework | ✓ **Reuse existing** |
| **Testcontainers** | `Testing/03-Infrastructure/` | SQL Server, Docker integration | ✓ **Reuse existing** |
| **Base Fixtures** | `Testing/01-Abstractions/` | TestFixtureBase, ContainerFixtureBase | ✓ **Reuse existing** |
| **Test Utilities** | `Testing/` | TestImageDataGenerator, DocumentOpener | ✓ **Reuse existing** |

### New VEC-Specific Components

**New Domain Interfaces (VEC-Specific):**

| Interface | Purpose | Rationale |
|-----------|---------|-----------|
| `IVecStatementExtractor` | VEC-specific PDF extraction orchestration | Coordinates LayoutLMv3, Table Transformer, OCR models |
| `IVecValidationService` | 115+ validation rules execution | VEC-specific financial validation logic |
| `IVisualComplianceValidator` | Logo, font, layout verification | VEC document quality requirements |
| `IFiscalComplianceValidator` | Post-timbrado verification | Barcode, cadena original, digital timbre validation |
| `IMarkedPdfGenerator` | Generate annotated PDFs | Visual markers for failing validation elements |
| `IErrorConfigurationService` | Database-driven error configuration | Runtime-configurable error messages |
| `IWorkloadCalculator` | Auto-calculate processing capacity | Monthly batch workload planning |
| `IResourceAllocator` | Infrastructure resource allocation | On-premises vs cloud resource decisions |
| `ICapacityPlanner` | Monthly capacity planning | Predict and allocate resources in advance |

**New Infrastructure Projects:**

| Project | Purpose | Dependencies |
|---------|---------|--------------|
| `Infrastructure.Validation.VecStatement` | VEC validation engine, marked PDF generation | PdfSharp, Python models |
| `Infrastructure.ErrorConfiguration` | Database-driven error storage and loading | EF Core |
| `Infrastructure.WorkloadManagement` | Batch workload calculation and planning | Azure Service Bus (future) |
| `Infrastructure.Caching.Memory` | In-memory cache adapter (Phase 1) | IMemoryCache |

**New Python Modules:**

| Module | Purpose | Models Used |
|--------|---------|-------------|
| `vec_extraction.py` | VEC statement extraction | LayoutLMv3, Table Transformer, GotOcr2/Donut/TrOCR, CLIP |
| `vec_validation.py` | Visual compliance validation | CLIP (logo detection), font analysis |
| `fiscal_verification.py` | Fiscal compliance verification | Barcode validation, digital timbre verification |

**New Database Entities:**

| Entity | Purpose | Retention |
|--------|---------|-----------|
| `VecStatement` | VEC statement data | 7 years (CNBV compliance) |
| `ValidationResult` | Validation rule execution results | 7 years |
| `IssueRecord` | Issue codes, severity, positions | 7 years |
| `ErrorConfiguration` | Runtime-configurable error messages | Permanent (configuration) |

### Integration Strategy

**Dependency Flow:**
```
New VEC Components → Existing Prisma Infrastructure
                  ↓
            Domain Interfaces (shared)
                  ↓
         Hexagonal Architecture Boundaries
```

**Namespace Strategy:**
- VEC-specific: `ExxerCube.Prisma.Veriqan.*`
- Shared infrastructure: `ExxerCube.Prisma.*`
- Cross-references allowed: Veriqan → Prisma ✓, Prisma → Veriqan ✗

**Database Strategy:**
- **Additive-only:** New VEC tables, no modifications to existing Prisma tables
- **Shared DbContext:** Extend existing `PrismaDbContext` with VEC entities
- **Backward compatibility:** Existing Prisma functionality unaffected

**Reusability Estimate:**
- ~60% of infrastructure exists and can be reused directly
- ~40% new VEC-specific components needed
- Zero breaking changes to existing Prisma functionality

## Core Architectural Decisions

### Decision Priority Analysis

**Critical Decisions (Block Implementation):**
1. Database choice with provider abstraction
2. Caching strategy with adapters
3. Migration approach (hybrid)
4. Authentication/authorization approach
5. Error handling with database-driven configuration
6. Workload management and resource allocation

**Important Decisions (Shape Architecture):**
7. Transport abstraction (IndFusion.Ember)
8. Component standardization
9. Monitoring and logging infrastructure
10. Deployment strategy with abstraction

**Deferred Decisions (Post-MVP):**
- API layer (can be added later if external consumers needed)
- Field-level encryption implementation (interfaces defined, implementation deferred)

### Data Architecture

**Database Choice:**
- **Decision:** SQL Server with EF Core provider abstraction
- **Rationale:** Primary database is SQL Server, but architecture uses EF Core adapters to maintain Hexagonal Architecture boundaries, enabling future PostgreSQL support if needed
- **Affects:** All data access interfaces, repository implementations, EF Core configurations
- **Version:** .NET 10 with EF Core (latest stable)

**Caching Strategy:**
- **Decision:** Phased caching approach with adapters
  - **Phase 1:** In-memory caching via `IMemoryCache` (existing .NET component)
  - **Phase 2:** CacheFusion for local caching (when needed)
  - **Phase 3:** Redis for distributed caching (when horizontal scaling required)
- **Rationale:**
  - Start simple with proven in-memory caching
  - Hexagonal Architecture allows swapping implementations without changing domain interfaces
  - Add distributed caching when scaling requirements materialize
  - Monthly batch processing pattern (3-5 day window) makes startup caching efficient
- **Affects:** Caching interfaces in Domain layer, implementations in Infrastructure layer
- **Implementation Status:**
  - Phase 1: `ICacheService` → In-memory implementation (immediate)
  - Phase 2: CacheFusion adapter (deferred)
  - Phase 3: Redis adapter (deferred)
- **Version:** IMemoryCache (.NET 10 built-in), CacheFusion (future), Redis (future)

**Migration Approach:**
- **Decision:** Hybrid approach
  - New deployments: EF Core Migrations (automated, versioned)
  - Existing deployments: SQL scripts (manual review, controlled rollout)
- **Rationale:** EF Core migrations for greenfield/new environments, SQL scripts for brownfield/existing production databases requiring careful change management
- **Affects:** Migration strategy documentation, deployment procedures

### Authentication & Security

**Authentication Method:**
- **Decision:** Extend existing ASP.NET Core Identity (for now)
- **Rationale:** Already in place, maintain compatibility, can migrate to Azure AD/Identity Server later if needed
- **Affects:** Authentication interfaces, user management, login flows
- **Version:** ASP.NET Core Identity (latest with .NET 10)

**Authorization Patterns:**
- **Decision:** Hybrid approach (Roles + Policies) with expanded abstraction
- **Rationale:** Simple roles for basic access, policies for complex rules; expand existing minimal abstraction for more robust, extensible authorization system
- **Future consideration:** Will likely need more users and roles as legal requirements evolve
- **Affects:** Authorization interfaces in Domain layer, policy definitions, role management

**Data Encryption Approach:**
- **Decision:** Database-level encryption (TDE) as primary, with mock interfaces for field-level encryption
- **Rationale:** Data stays on-premises (possibly backup to another database), TDE provides sufficient protection; define interfaces for field-level encryption to enable defense in depth later without breaking changes
- **Implementation:** TDE for production, field-level encryption interfaces defined but not implemented initially
- **Affects:** Encryption interfaces in Domain layer (mock/placeholder), TDE configuration in database

### API & Communication Patterns

**API Design Pattern:**
- **Decision:** No API layer initially - direct service calls from Blazor Server
- **Rationale:** No external consumers expected; maintain clean service separation; can add API layer later if needed
- **Affects:** Service interfaces, Blazor component-to-service communication patterns

**Error Handling Standards:**
- **Decision:** Standardized error format with database-driven configuration
- **Components:**
  - Error codes (taxonomy/system)
  - Configurable error messages stored in database
  - Images associated with errors (stored in database)
  - Position/location information
  - Artifact type classification
  - No recompilation needed for error message changes
- **Examples:**
  - "Image 'foo' was not found on the 'far' location"
  - "Image 'foo' was found with an artifact type 'kind 1' on page n"
- **Rationale:** Many verifications, changing application requirements, need runtime configurability
- **Affects:** Error configuration interfaces, error message repository, image storage for error annotations, Result<T> error structure

**Rate Limiting & Workload Management:**
- **Decision:** Auto-calculated, infrastructure-aware workload management
- **Components:**
  - Background worker with self-service capability
  - Auto-calculate rate and time needed to fulfill current load
  - Predict and allocate resources in advance
  - Support monthly task patterns (predict demand, allocate resources)
  - Mixed infrastructure support (on-premises + as-a-service)
- **Infrastructure Strategy:**
  - On-premises for early development
  - As-a-service for first scaling tests and optimization
  - Business decision based on KPIs, workload, and business case
  - System must adapt to infrastructure architecture and budget
- **Rationale:** Monthly processing tasks require advance resource planning; system must be self-aware of capacity and workload
- **Affects:** Workload calculation interfaces, resource allocation services, infrastructure abstraction layer, capacity planning services

### Frontend Architecture

**State Management & Real-Time Communication:**
- **Decision:** IndFusion.Ember transport hub abstraction with scope expansion
- **Rationale:**
  - Existing owned package with transport hub pattern
  - Currently supports SignalR, designed for TCP, MQTT, OPC, event bus
  - Born from need to make Hub<T> testable
  - Pattern repeated across projects
  - Supports non-blocking dashboard for SignalR
- **Extension Required:**
  - Research and add support for the two most popular service transports above SignalR
  - Expand scope for VEC real-time dashboards (processing status, validation results, batch progress)
  - Ensure compatibility with existing Prisma real-time communication patterns
- **VEC-Specific Usage:**
  - Real-time processing status updates (extraction, validation, marked PDF generation)
  - Batch processing progress tracking (130K-200K statements/month)
  - SLA dashboard updates (if applicable for regulatory compliance)
  - Validation results streaming
- **Affects:** Real-time communication interfaces, transport abstraction layer, dashboard components
- **Version:** IndFusion.Ember (existing package, scope expansion required)

**Component Architecture:**
- **Decision:** MudBlazor with team standardization
- **Rationale:**
  - MudBlazor has breaking changes, waiting for API stabilization
  - Likely FOSS to survive Blazor first/second wave
  - Found implementation variations between developers
  - Need team standards (like backend standards) - standard components
  - SRP is good, but reuse across projects is limited
- **Action Required:** Define team component standards and patterns
- **Affects:** Component library standards, UI component patterns, developer guidelines
- **Version:** MudBlazor (waiting for API stabilization)

### Infrastructure & Deployment

**Monitoring and Logging:**
- **Decision:** Extend Serilog with SEQ
- **Rationale:**
  - Already using Serilog
  - SEQ provides SQL search capabilities
  - Provides significant value for log analysis
- **Affects:** Logging configuration, correlation ID implementation, SEQ integration
- **Version:** Serilog (latest), SEQ (latest)

**Deployment Strategy:**
- **Decision:** Docker with abstraction for flexibility
- **Rationale:**
  - Docker is increasingly better each day
  - System likely needs horizontal scaling
  - Will need deployment expert for infrastructure
  - Keep door open for alternative deployment strategies
- **Affects:** Deployment abstraction interfaces, containerization strategy, orchestration support
- **Version:** Docker (latest stable)

### Decision Impact Analysis

**Implementation Sequence:**
1. Database and caching adapters (foundation) - **Reuse existing PrismaDbContext, add in-memory cache**
2. Error configuration system (early, needed for validations) - **New VEC-specific**
3. IndFusion.Ember scope expansion for VEC dashboards - **Extend existing package**
4. Workload management interfaces (needed for background processing) - **New VEC-specific**
5. Authorization abstraction expansion - **Extend existing Auth infrastructure**
6. Component standards definition - **Extend existing MudBlazor patterns**
7. Monitoring and logging extension - **Reuse existing Serilog + SEQ**
8. Deployment abstraction - **Reuse existing Docker patterns**

**Note:** Items marked "Reuse existing" leverage current Prisma infrastructure. Items marked "Extend existing" build upon proven patterns. Only items marked "New VEC-specific" are net-new components.

**Cross-Component Dependencies:**
- Error configuration system depends on database adapters
- Workload management depends on infrastructure abstraction
- IndFusion.Ember extension affects all real-time UI components
- Component standards affect all UI development
- Authorization abstraction affects all protected endpoints
- Deployment abstraction affects all infrastructure decisions

**Research Required:**
- Two most popular service transports above SignalR (for IndFusion.Ember extension)

## Implementation Patterns & Consistency Rules

### Pattern Categories Defined

**Critical Conflict Points Identified:**
12 areas where AI agents could make different choices, all now standardized to prevent implementation conflicts.

### Naming Patterns

**Database Naming Conventions:**
- **Standard:** SQL Server conventions (PascalCase tables/columns, `IX_` prefix for indexes)
- **Configuration:** Explicitly configured in EF Core, tested via architecture rules
- **Examples:**
  - Tables: `Users`, `Documents`, `AuditRecords`
  - Columns: `UserId`, `DocumentId`, `CreatedAt`
  - Indexes: `IX_Users_Email`, `IX_Documents_Status`
- **Enforcement:** Architecture test project validates naming conventions

**Service/Repository Naming Conventions:**
- **Standard:** Use `Service` naming (not `Repository`)
- **Pattern:** `I{Entity}Service` with `{Action}{Entity}Async` methods
- **Examples:**
  - `IUserService.GetUserAsync()`
  - `IDocumentService.ProcessDocumentAsync()`
  - `IValidationService.ValidateDocumentAsync()`
- **Rationale:** Keep Repository isolated from service layer - repositories are injected but not attached even in naming
- **Enforcement:** Architecture rules in test project

**Code Naming Conventions:**
- **Standard:** PascalCase for all naming (classes, interfaces, methods, properties, events)
- **File Naming:** Match class name exactly (`UserService.cs` for `UserService` class)
- **Namespace:** `Company.Project.Layer.Feature` pattern
- **Async Methods:** Always suffix with `Async`
- **Examples:**
  - Classes: `UserService`, `DocumentProcessor`
  - Interfaces: `IUserService`, `IDocumentProcessor`
  - Methods: `GetUserAsync()`, `ProcessDocumentAsync()`
  - Events: `UserCreated`, `DocumentProcessed`
- **Enforcement:** Documented explicitly (many dev agents don't follow standards without explicit documentation)

### Structure Patterns

**Project Organization:**

**Infrastructure Layer:**
- **Standard:** Hybrid organization - by feature and technology
- **Pattern:** Many SRP as possible, each implementation in its own project
- **Structure:**
  - Technology-based projects: `Infrastructure.Database`, `Infrastructure.Caching`, `Infrastructure.BrowserAutomation`
  - Feature-based subfolders within technology projects
  - Each implementation in separate project for cleaner implementation and testing
- **Examples:**
  - `Infrastructure.Database.SqlServer` (SQL Server adapter)
  - `Infrastructure.Caching.CacheFusion` (CacheFusion adapter)
  - `Infrastructure.Caching.Redis` (Redis adapter)
  - `Infrastructure.BrowserAutomation.Playwright` (Playwright adapter)

**Test Organization:**
- **Standard:** Separate test projects mirroring structure
- **Pattern:** By project and abstraction level (like existing project)
- **Structure:**
  - `Tests.Domain` - Domain layer tests
  - `Tests.Application` - Application layer tests
  - `Tests.Infrastructure.Database` - Database adapter tests
  - `Tests.Infrastructure.Caching` - Caching adapter tests
- **Enforcement:** Follow existing xUnit v3 patterns

**File Structure Patterns:**
- Configuration files: Within each infrastructure project
- Error configuration: Separate project or within Domain/Application (to be determined)
- Test files: Separate test projects, not co-located

### Format Patterns

**Result<T> Error Format:**
- **Error Code Format:** Hierarchical codes (`VALIDATION.IMAGE.NOT_FOUND`, `VALIDATION.IMAGE.ARTIFACT_TYPE`)
- **Error Structure:** Database-driven error configuration
- **Error Loading:** Load error config on startup, cache in memory
- **Error Message Resolution:**
  - Lazy loading of messages
  - Always logged
  - Displayed on demand (not automatically)
- **Examples:**
  - "Image 'foo' was not found on the 'far' location"
  - "Image 'foo' was found with an artifact type 'kind 1' on page n"
- **Rationale:** App goes live for 3-4 days then dormant until next batch (monthly), so startup caching is efficient

**Date/Time Formats:**
- **Standard:** `DateTimeOffset` everywhere (timezone-aware)
- **Abstraction:** `DateTimeMachine` for testability
  - Hides testing features (changing time, advancing time) when compiled for production
  - Designed for testing scenarios
  - May not exist yet in database but designed for testing
- **JSON Serialization:** ISO 8601 strings
- **Database:** `DateTimeOffset` type

**Data Exchange Formats:**
- **JSON Field Naming:** camelCase for JSON (standard .NET serialization)
- **Boolean:** true/false (not 1/0)
- **Null Handling:** Explicit null checks, nullable reference types enabled

### Communication Patterns

**IndFusion.Ember Event Patterns:**
- **Event Naming:** PascalCase (`UserCreated`, `DocumentProcessed`, `DocumentValidationFailed`)
- **Event Payload:** Standard envelope format (to be defined in IndFusion.Ember extension)
- **Event Versioning:** Approach to be defined during IndFusion.Ember extension research

**State Management Patterns:**
- **Standard:** Component-local state with SignalR updates
- **Rationale:** Simpler approach, avoids centralized state complexity
- **Pattern:**
  - Component manages its own state
  - SignalR updates trigger component state updates
  - No centralized state service for UI state
- **Loading States:**
  - Naming: `IsLoading` pattern
  - UI: Spinner (MudBlazor patterns)
  - Scope: Per operation
  - Features: Cache and preloading, async stream patterns
  - Follow MudBlazor loading patterns

**Blazor Server Communication:**
- **Direct Service Calls:** No API layer initially
- **SignalR Updates:** Real-time updates via IndFusion.Ember abstraction
- **State Updates:** Component-local with SignalR synchronization

### Process Patterns

**Error Handling Patterns:**

**Error Recovery - Document Errors:**
- **Pattern:** Short-circuit pipeline or three-pipeline abstraction (depends on error type)
- **Flow:**
  1. Retry on queue based on severity
  2. Process 200,000 documents in batch
  3. If corrupted/damaged → send to queue for special inspection
  4. Different recovery paths based on error severity
  5. If nothing succeeds → flag for manual review
- **Queue Management:** Severity-based routing to different recovery pipelines

**Error Recovery - Infrastructure Errors:**
- **Hot Backups:** Critical tasks require hot backups (no failure allowed)
- **Availability:** Double availability with hot backup + cold backup when fully deployed
- **Backup Rotation:** Both backups on monthly rotation
- **Testing:** Early testing - week before operation scheduled
- **Rationale:** Critical tasks when fully implemented require this level of redundancy

**Loading State Patterns:**
- **Naming:** `IsLoading` pattern (e.g., `IsSavingLoading`, `IsDeletingLoading`)
- **UI:** Spinner (MudBlazor patterns)
- **Scope:** Per operation
- **Features:** 
  - Cache and preloading
  - Async stream patterns
- **Library:** Follow MudBlazor loading patterns

**UI Usage Patterns:**
- **Primary Purpose:** Acceptance and validation (not interaction during processing)
- **Processing:** Background processing
- **Display:**
  - Documents with errors shown
  - Random samples of validated documents (for quality assurance)
- **Interaction:** Minimal during processing, focused on review and validation

### Enforcement Guidelines

**All AI Agents MUST:**

1. **Follow PascalCase naming** for all code elements (classes, interfaces, methods, properties, events)
2. **Use `Service` naming** (not `Repository`) for service layer interfaces
3. **Use `DateTimeOffset`** with `DateTimeMachine` abstraction for all date/time operations
4. **Load error configuration on startup** and cache in memory
5. **Use component-local state** with SignalR updates for Blazor Server
6. **Organize infrastructure by feature and technology** with each implementation in separate project
7. **Use separate test projects** mirroring structure by project and abstraction level
8. **Follow SQL Server naming conventions** (PascalCase, `IX_` prefix) explicitly configured in EF Core
9. **Use hierarchical error codes** (`VALIDATION.IMAGE.NOT_FOUND`) with database-driven messages
10. **Implement error recovery pipelines** based on severity (document errors vs infrastructure errors)
11. **Follow MudBlazor patterns** for loading states and UI components
12. **Use IndFusion.Ember** for all real-time communication with PascalCase event names

**Pattern Enforcement:**

- **Architecture Test Project:** Validates naming conventions, structure patterns, and architectural rules
- **Code Reviews:** Verify patterns are followed
- **Documentation:** All patterns documented in architecture document
- **Process:** Update patterns through architecture document updates

### Pattern Examples

**Good Examples:**

```csharp
// Service Interface (PascalCase, Service naming)
public interface IUserService
{
    Task<Result<User>> GetUserAsync(Guid userId, CancellationToken cancellationToken);
}

// Event (PascalCase)
public class UserCreated
{
    public Guid UserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

// DateTimeOffset with DateTimeMachine
public class DocumentProcessor
{
    private readonly IDateTimeMachine _dateTimeMachine;
    
    public async Task<Result<Document>> ProcessAsync(Document document)
    {
        var processedAt = _dateTimeMachine.UtcNow; // Testable
        // ...
    }
}

// Error Code (Hierarchical)
public static class ErrorCodes
{
    public const string ValidationImageNotFound = "VALIDATION.IMAGE.NOT_FOUND";
    public const string ValidationImageArtifactType = "VALIDATION.IMAGE.ARTIFACT_TYPE";
}
```

**Anti-Patterns:**

```csharp
// ❌ Wrong: Repository naming in service layer
public interface IUserRepository { } // Should be IUserService

// ❌ Wrong: DateTime instead of DateTimeOffset
public DateTime CreatedAt { get; set; } // Should be DateTimeOffset

// ❌ Wrong: camelCase event name
public class userCreated { } // Should be PascalCase: UserCreated

// ❌ Wrong: Flat error codes
public const string Error001 = "ERR_001"; // Should be hierarchical: VALIDATION.IMAGE.NOT_FOUND
```

## Project Structure & Boundaries

### Complete Project Directory Structure

Based on the existing ExxerCube.Prisma structure and our architectural decisions, here's the complete project structure that extends the current organization:

**Note:** VEC Statement Processing components are namespaced as `ExxerCube.Prisma.Veriqan.*` to indicate they are extensions of the existing ExxerCube.Prisma system. This allows reuse of existing Prisma infrastructure (OCR, extraction, imaging, database, events) while maintaining clear separation of VEC-specific functionality.

```
ExxerCube.Prisma.Veriqan/
├── .editorconfig
├── Directory.Build.props
├── Directory.Packages.props
├── .gitignore
├── README.md
│
├── 📁 00 Solution Items
│   ├── .editorconfig
│   ├── Directory.Build.props
│   └── Directory.Packages.props
│
├── 📁 01 Core
│   ├── 📦 ExxerCube.Prisma.Veriqan.Domain
│   │   ├── Interfaces/
│   │   │   ├── Ingestion/                    # Stage 1: Document Acquisition
│   │   │   │   ├── IBrowserAutomationAgent.cs
│   │   │   │   ├── IDownloadStorage.cs
│   │   │   │   └── IFileMetadataLogger.cs
│   │   │   ├── Extraction/                   # Stage 2: Metadata Extraction
│   │   │   │   ├── IMetadataExtractor.cs
│   │   │   │   ├── IFieldExtractor.cs        # ✓ REUSE from ExxerCube.Prisma.Domain
│   │   │   │   ├── IClassificationService.cs
│   │   │   │   └── IFieldMatchingService.cs
│   │   │   ├── DecisionLogic/                # Stage 3: Decision & SLA
│   │   │   │   ├── IPersonIdentityResolver.cs
│   │   │   │   ├── ILegalDirectiveClassifier.cs
│   │   │   │   ├── ISlaTrackingService.cs
│   │   │   │   └── IManualReviewService.cs
│   │   │   ├── Compliance/                   # Stage 4: Export Generation
│   │   │   │   ├── ISiroExportService.cs
│   │   │   │   ├── IPdfSigningService.cs
│   │   │   │   └── IExportValidationService.cs
│   │   │   ├── Validation/                   # VEC Statement PDF Validation
│   │   │   │   ├── IVecStatementExtractor.cs
│   │   │   │   ├── IVecValidationService.cs
│   │   │   │   ├── IVisualComplianceValidator.cs
│   │   │   │   └── IImageQualityValidator.cs
│   │   │   ├── ErrorConfiguration/           # Database-driven Error Config
│   │   │   │   ├── IErrorConfigurationService.cs
│   │   │   │   └── IErrorMessageRepository.cs
│   │   │   ├── WorkloadManagement/           # Auto-calculated Workload
│   │   │   │   ├── IWorkloadCalculator.cs
│   │   │   │   ├── IResourceAllocator.cs
│   │   │   │   └── ICapacityPlanner.cs
│   │   │   ├── Caching/                      # Caching Abstractions
│   │   │   │   └── ICacheService.cs
│   │   │   ├── Authorization/                # Expanded Authorization
│   │   │   │   └── IAuthorizationService.cs
│   │   │   └── Common/
│   │   │       ├── IDateTimeMachine.cs
│   │   │       └── IAuditLogger.cs
│   │   ├── Entities/
│   │   │   ├── Expediente.cs                 # Regulatory case
│   │   │   ├── Persona.cs                    # Person identity
│   │   │   ├── Oficio.cs                     # Regulatory directive
│   │   │   ├── ComplianceAction.cs           # Compliance action mapping
│   │   │   ├── SlaStatus.cs                  # SLA tracking
│   │   │   ├── UnifiedMetadataRecord.cs      # Consolidated metadata
│   │   │   ├── VecStatement.cs               # VEC statement entity
│   │   │   ├── ErrorConfiguration.cs         # Error config entity
│   │   │   └── AuditRecord.cs                # Audit trail
│   │   └── ValueObjects/
│   │       ├── Rfc.cs                        # RFC value object
│   │       ├── Clabe.cs                      # CLABE value object
│   │       └── ErrorCode.cs                  # Error code value object
│   │
│   └── 📦 ExxerCube.Prisma.Veriqan.Application
│       ├── Services/
│       │   ├── Ingestion/                    # Stage 1 Orchestration
│       │   │   └── DocumentIngestionService.cs
│       │   ├── Extraction/                   # Stage 2 Orchestration
│       │   │   ├── MetadataExtractionService.cs
│       │   │   └── FieldMatchingService.cs
│       │   ├── DecisionLogic/                # Stage 3 Orchestration
│       │   │   ├── IdentityResolutionService.cs
│       │   │   ├── LegalClassificationService.cs
│       │   │   └── SlaTrackingService.cs
│       │   ├── Compliance/                   # Stage 4 Orchestration
│       │   │   └── ComplianceExportService.cs
│       │   ├── Validation/                   # VEC Statement Validation
│       │   │   └── VecValidationOrchestrationService.cs
│       │   └── WorkloadManagement/           # Workload Orchestration
│       │       └── WorkloadOrchestrationService.cs
│       └── Handlers/                         # CQRS handlers (if used)
│
├── 📁 02 Infrastructure
│   ├── 📦 ExxerCube.Prisma.Veriqan.Infrastructure.Database.SqlServer
│   │   ├── EntityFramework/
│   │   │   ├── Configurations/                # EF Core configurations
│   │   │   │   ├── ExpedienteConfiguration.cs
│   │   │   │   ├── PersonaConfiguration.cs
│   │   │   │   ├── OficioConfiguration.cs
│   │   │   │   ├── ErrorConfigurationConfiguration.cs
│   │   │   │   └── VecStatementConfiguration.cs
│   │   │   └── VeriqanDbContext.cs
│   │   └── Repositories/                     # Repository implementations
│   │       └── (Repository implementations following existing patterns)
│   │
│   ├── 📦 ExxerCube.Prisma.Veriqan.Infrastructure.Caching.CacheFusion
│   │   └── CacheFusionCacheAdapter.cs        # Local caching adapter
│   │
│   ├── 📦 ExxerCube.Prisma.Veriqan.Infrastructure.Caching.Redis
│   │   └── RedisCacheAdapter.cs              # Distributed caching adapter
│   │
│   ├── 📦 ExxerCube.Prisma.Veriqan.Infrastructure.BrowserAutomation.Playwright
│   │   └── PlaywrightBrowserAutomationAdapter.cs
│   │
│   ├── 📦 ExxerCube.Prisma.Veriqan.Infrastructure.Extraction (existing, extended)
│   │   └── (Existing extraction adapters - XML, DOCX, PDF)
│   │
│   ├── 📦 ExxerCube.Prisma.Veriqan.Infrastructure.Classification (existing, extended)
│   │   └── (Existing classification adapters)
│   │
│   ├── 📦 ExxerCube.Prisma.Veriqan.Infrastructure.Export
│   │   ├── SiroXmlExportAdapter.cs
│   │   └── PdfSigningAdapter.cs
│   │
│   ├── 📦 ExxerCube.Prisma.Veriqan.Infrastructure.ErrorConfiguration
│   │   ├── ErrorConfigurationRepository.cs
│   │   └── ErrorMessageRepository.cs
│   │
│   ├── 📦 ExxerCube.Prisma.Veriqan.Infrastructure.WorkloadManagement
│   │   ├── WorkloadCalculator.cs
│   │   ├── ResourceAllocator.cs
│   │   └── CapacityPlanner.cs
│   │
│   ├── 📦 ExxerCube.Prisma.Veriqan.Infrastructure.Validation.VecStatement
│   │   ├── VecStatementExtractor.cs
│   │   ├── VisualComplianceValidator.cs
│   │   └── ImageQualityValidator.cs
│   │
│   ├── 📦 ExxerCube.Prisma.Veriqan.Infrastructure.Authorization
│   │   └── AuthorizationService.cs           # Expanded authorization
│   │
│   ├── 📦 ExxerCube.Prisma.Veriqan.Infrastructure.Transport.IndFusionEmber
│   │   └── (IndFusion.Ember integration and extensions)
│   │
│   └── 📦 ExxerCube.Prisma.Veriqan.Infrastructure.Common
│       ├── DateTimeMachine.cs                # Testable date/time
│       └── AuditLogger.cs                    # Audit logging
│
├── 📁 03 UI
│   └── 📦 ExxerCube.Prisma.Veriqan.Web.UI
│       ├── Components/
│       │   ├── Pages/
│       │   │   ├── Ingestion/
│       │   │   │   └── DocumentIngestionDashboard.razor
│       │   │   ├── Extraction/
│       │   │   │   └── MetadataExtractionDashboard.razor
│       │   │   ├── DecisionLogic/
│       │   │   │   ├── SlaDashboard.razor
│       │   │   │   └── ManualReviewDashboard.razor
│       │   │   ├── Compliance/
│       │   │   │   └── ExportManagement.razor
│       │   │   └── Validation/
│       │   │       └── VecValidationDashboard.razor
│       │   ├── Shared/
│       │   │   └── (MudBlazor standard components)
│       │   └── Layout/
│       │       └── MainLayout.razor
│       └── Services/
│           └── (Blazor service registrations)
│
├── 📁 04 Tests
│   ├── 📁 01 Core
│   │   ├── 📦 ExxerCube.Prisma.Veriqan.Tests.Domain
│   │   ├── 📦 ExxerCube.Prisma.Veriqan.Tests.Application
│   │   └── 📦 ExxerCube.Prisma.Veriqan.Tests.Domain.Interfaces
│   │
│   ├── 📁 02 Infrastructure
│   │   ├── 📦 ExxerCube.Prisma.Veriqan.Tests.Infrastructure.Database
│   │   ├── 📦 ExxerCube.Prisma.Veriqan.Tests.Infrastructure.Caching.CacheFusion
│   │   ├── 📦 ExxerCube.Prisma.Veriqan.Tests.Infrastructure.Caching.Redis
│   │   ├── 📦 ExxerCube.Prisma.Veriqan.Tests.Infrastructure.BrowserAutomation
│   │   ├── 📦 ExxerCube.Prisma.Veriqan.Tests.Infrastructure.ErrorConfiguration
│   │   ├── 📦 ExxerCube.Prisma.Veriqan.Tests.Infrastructure.WorkloadManagement
│   │   └── 📦 ExxerCube.Prisma.Veriqan.Tests.Infrastructure.Validation.VecStatement
│   │
│   ├── 📁 06 Architecture
│   │   └── 📦 ExxerCube.Prisma.Veriqan.Tests.Architecture
│   │       └── (Architecture rule tests - naming, structure, patterns)
│   │
│   └── 📁 03 System
│       └── (System/E2E tests)
│
└── 📁 Fixtures
    ├── PRP1/                                 # Existing fixtures
    ├── PRP2/                                 # VEC Statement fixtures
    └── (Other test fixtures)
```

### Architectural Boundaries

**API Boundaries:**
- **No REST API Layer Initially:** Direct service calls from Blazor Server components
- **Service Interfaces:** All services exposed through Domain layer interfaces
- **Future API Layer:** Can be added as facade layer without changing service implementations

**Component Boundaries:**
- **Domain Layer:** Pure interfaces and entities, no dependencies on infrastructure
- **Application Layer:** Orchestrates domain services, depends only on Domain interfaces
- **Infrastructure Layer:** Implements Domain interfaces, organized by technology and feature
- **UI Layer:** Depends on Application services, uses IndFusion.Ember for real-time updates

**Service Boundaries:**
- **Stage 1 (Ingestion):** Browser automation → File storage → Metadata logging
- **Stage 2 (Extraction):** File processing → Metadata extraction → Classification → Field matching
- **Stage 3 (Decision Logic):** Identity resolution → Legal classification → SLA tracking → Manual review
- **Stage 4 (Compliance):** Export validation → SIRO XML generation → PDF signing
- **VEC Validation:** PDF extraction → Visual validation → Image quality → Error reporting

**Data Boundaries:**
- **Database:** SQL Server with EF Core, additive-only schema changes
- **Caching:** Abstracted through `ICacheService`, implementations in separate projects
- **File Storage:** Abstracted through existing `IFileStorage` interfaces
- **Error Configuration:** Stored in database, loaded on startup, cached in memory

### Requirements to Structure Mapping

**Epic 1: Regulatory Compliance Automation System**

**Story 1.1 (Browser Automation and Document Download):**
- **Domain Interface:** `Domain/Interfaces/Ingestion/IBrowserAutomationAgent.cs`
- **Infrastructure:** `Infrastructure.BrowserAutomation.Playwright/PlaywrightBrowserAutomationAdapter.cs`
- **Application:** `Application/Services/Ingestion/DocumentIngestionService.cs`
- **UI:** `Web.UI/Components/Pages/Ingestion/DocumentIngestionDashboard.razor`
- **Database:** New tables for `FileMetadata`, `DownloadHistory`

**Story 1.2 (Enhanced Metadata Extraction and File Classification):**
- **Domain Interfaces:** `Domain/Interfaces/Extraction/IMetadataExtractor.cs`, `IClassificationService.cs`
- **Infrastructure:** `Infrastructure.Extraction/` (existing, extended), `Infrastructure.Classification/` (existing, extended)
- **Application:** `Application/Services/Extraction/MetadataExtractionService.cs`
- **UI:** `Web.UI/Components/Pages/Extraction/MetadataExtractionDashboard.razor`
- **Database:** New tables for classification results, metadata records

**Story 1.3 (Field Matching and Unified Metadata Generation):**
- **Domain Interface:** `Domain/Interfaces/Extraction/IFieldMatchingService.cs`
- **Infrastructure:** Extends existing extraction infrastructure
- **Application:** `Application/Services/Extraction/FieldMatchingService.cs`
- **UI:** Extended extraction dashboard
- **Database:** `UnifiedMetadataRecord` entity

**Story 1.4 (Identity Resolution and Legal Directive Classification):**
- **Domain Interfaces:** `Domain/Interfaces/DecisionLogic/IPersonIdentityResolver.cs`, `ILegalDirectiveClassifier.cs`
- **Infrastructure:** New adapters for identity resolution and legal classification
- **Application:** `Application/Services/DecisionLogic/IdentityResolutionService.cs`, `LegalClassificationService.cs`
- **UI:** Decision logic dashboard components
- **Database:** `Persona`, `ComplianceAction` entities

**Story 1.5 (SLA Tracking and Escalation Management):**
- **Domain Interface:** `Domain/Interfaces/DecisionLogic/ISlaTrackingService.cs`
- **Infrastructure:** SLA tracking adapter
- **Application:** `Application/Services/DecisionLogic/SlaTrackingService.cs`
- **UI:** `Web.UI/Components/Pages/DecisionLogic/SlaDashboard.razor`
- **Database:** `SlaStatus` entity
- **Real-time:** IndFusion.Ember for SLA alerts

**Story 1.6 (Manual Review Interface):**
- **Domain Interface:** `Domain/Interfaces/DecisionLogic/IManualReviewService.cs`
- **Infrastructure:** Manual review adapter
- **Application:** `Application/Services/DecisionLogic/ManualReviewService.cs`
- **UI:** `Web.UI/Components/Pages/DecisionLogic/ManualReviewDashboard.razor`
- **Database:** Review records, reviewer actions

**Story 1.7 (SIRO-Compliant Export Generation):**
- **Domain Interface:** `Domain/Interfaces/Compliance/ISiroExportService.cs`
- **Infrastructure:** `Infrastructure.Export/SiroXmlExportAdapter.cs`
- **Application:** `Application/Services/Compliance/ComplianceExportService.cs`
- **UI:** `Web.UI/Components/Pages/Compliance/ExportManagement.razor`
- **Database:** Export history, validation results

**Story 1.8 (PDF Summarization and Digital Signing):**
- **Domain Interface:** `Domain/Interfaces/Compliance/IPdfSigningService.cs`
- **Infrastructure:** `Infrastructure.Export/PdfSigningAdapter.cs`
- **Application:** Extended compliance export service
- **UI:** Extended export management
- **Database:** Signing certificates, signature records

**Story 1.9 (Audit Trail and Reporting):**
- **Domain Interface:** `Domain/Interfaces/Common/IAuditLogger.cs`
- **Infrastructure:** `Infrastructure.Common/AuditLogger.cs`
- **Application:** Cross-cutting, used by all services
- **UI:** Audit trail viewer component
- **Database:** `AuditRecord` entity (7-year retention)

**Story 1.10 (SignalR Unified Hub Abstraction):**
- **Infrastructure:** `Infrastructure.Transport.IndFusionEmber/`
- **UI:** All real-time components use IndFusion.Ember abstraction
- **Cross-cutting:** Foundation for all real-time UI features

**VEC Statement PDF Extraction & Validation (PRP.txt):**
- **Domain Interfaces:** `Domain/Interfaces/Validation/IVecStatementExtractor.cs`, `IVecValidationService.cs`, `IVisualComplianceValidator.cs`, `IImageQualityValidator.cs`
- **Infrastructure:** `Infrastructure.Validation.VecStatement/`
- **Application:** `Application/Services/Validation/VecValidationOrchestrationService.cs`
- **UI:** `Web.UI/Components/Pages/Validation/VecValidationDashboard.razor`
- **Database:** `VecStatement` entity, validation results, error annotations

**Cross-Cutting Concerns:**

**Error Configuration System:**
- **Domain Interfaces:** `Domain/Interfaces/ErrorConfiguration/IErrorConfigurationService.cs`, `IErrorMessageRepository.cs`
- **Infrastructure:** `Infrastructure.ErrorConfiguration/`
- **Application:** Used by all validation and processing services
- **Database:** `ErrorConfiguration` entity (messages, images, positions, artifact types)
- **Loading:** Startup cache, lazy message loading, on-demand display

**Workload Management:**
- **Domain Interfaces:** `Domain/Interfaces/WorkloadManagement/IWorkloadCalculator.cs`, `IResourceAllocator.cs`, `ICapacityPlanner.cs`
- **Infrastructure:** `Infrastructure.WorkloadManagement/`
- **Application:** `Application/Services/WorkloadManagement/WorkloadOrchestrationService.cs`
- **Background:** Self-service background worker
- **Infrastructure Abstraction:** Adapts to on-premises vs as-a-service

**Caching:**
- **Domain Interface:** `Domain/Interfaces/Caching/ICacheService.cs`
- **Infrastructure:** `Infrastructure.Caching.CacheFusion/`, `Infrastructure.Caching.Redis/`
- **Usage:** Cross-cutting, used by all services requiring caching

**Authorization:**
- **Domain Interface:** `Domain/Interfaces/Authorization/IAuthorizationService.cs`
- **Infrastructure:** `Infrastructure.Authorization/`
- **Application:** Used by all protected operations
- **Pattern:** Hybrid (Roles + Policies) with expanded abstraction

### Integration Points

**Internal Communication:**
- **Service-to-Service:** Direct method calls through interfaces (no API layer)
- **Real-time Updates:** IndFusion.Ember transport hub abstraction
- **Event-Driven:** PascalCase events (`DocumentProcessed`, `SlaBreachImminent`)
- **State Management:** Component-local state with SignalR updates

**External Integrations:**
- **Python Modules:** CSnakes integration (existing pattern)
- **Browser Automation:** Playwright for UIF/CNBV websites
- **Digital Signatures:** X.509 certificate management systems
- **SIRO Systems:** Regulatory submission endpoints
- **Notification Systems:** Email/SMS/Slack for SLA escalations
- **SEQ:** Log aggregation and SQL search

**Data Flow:**
1. **Ingestion:** Browser → Download Storage → File Metadata Logger
2. **Extraction:** File → Metadata Extractor → Classifier → Field Matcher → Unified Metadata
3. **Decision Logic:** Unified Metadata → Identity Resolver → Legal Classifier → SLA Tracker
4. **Compliance:** Validated Metadata → Export Validator → SIRO XML/PDF Generator → Signed Export
5. **VEC Validation:** PDF → Extractor → Visual Validator → Image Quality Checker → Error Reporter
6. **Error Handling:** Error → Error Configuration Service → Database Message → User Display
7. **Workload:** Current Load → Workload Calculator → Resource Allocator → Capacity Planner → Infrastructure

### File Organization Patterns

**Configuration Files:**
- **Solution Level:** `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`
- **Project Level:** `appsettings.json`, `appsettings.Development.json`
- **Infrastructure:** Each infrastructure project contains its own configuration
- **Database:** EF Core configurations in `Infrastructure.Database.SqlServer/EntityFramework/Configurations/`

**Source Organization:**
- **Domain:** Interfaces organized by feature/stage, Entities and ValueObjects at root
- **Application:** Services organized by feature/stage, Handlers if using CQRS
- **Infrastructure:** Separate projects per technology/feature, adapters implement Domain interfaces
- **UI:** Pages organized by feature, Shared components, Layout components

**Test Organization:**
- **Mirror Structure:** Test projects mirror production structure
- **By Abstraction Level:** Core tests, Infrastructure tests, System tests, Architecture tests
- **Fixtures:** Local to each test project (no fragile relative paths)
- **Architecture Tests:** Validate naming conventions, structure patterns, architectural rules

**Asset Organization:**
- **Fixtures:** Organized by test scenario (PRP1, PRP2, etc.)
- **Error Images:** Stored in database (ErrorConfiguration entity)
- **Export Files:** Managed through file storage abstraction
- **Static Assets:** In UI project `wwwroot/` folder

### Development Workflow Integration

**Development Server Structure:**
- **Blazor Server:** Runs UI project, connects to Application services
- **Background Workers:** Separate processes for workload management, batch processing
- **Database:** Local SQL Server instance for development
- **Caching:** CacheFusion for local development, Redis for distributed testing

**Build Process Structure:**
- **Solution Build:** All projects build together, warnings as errors
- **Test Execution:** Separate test projects, can run independently
- **Architecture Validation:** Architecture test project validates patterns
- **Python Integration:** CSnakes handles Python module integration

**Deployment Structure:**
- **Docker Containers:** Each infrastructure component can be containerized
- **On-Premises:** Traditional deployment for early development
- **As-a-Service:** Cloud deployment for scaling tests
- **Mixed Infrastructure:** System adapts to infrastructure architecture and budget
- **Hot/Cold Backups:** Monthly rotation, tested week before operation

## VEC Statement Processing Architecture

### Overview

The VEC Statement PDF Extraction & Validation System is a specialized component within ExxerCube.Prisma.Veriqan that processes Vector Casa de Bolsa (VEC) financial statements. The system handles monthly batch processing of 130,000-200,000 statements (1% quality control sample) with a target processing time of <30 seconds per statement.

### Processing Pipeline Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    VEC PDF Statement Upload                   │
│              (130K-200K/month, batch processing)              │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              C# Orchestration Layer (Application)            │
│  ExxerCube.Prisma.Veriqan.Application.Services.Validation          │
│  - VecValidationOrchestrationService                         │
│  - Batch processing coordination                             │
│  - Queue management (Azure Service Bus)                       │
└──────────────────────┬──────────────────────────────────────┘
                       │ CSnakes.Runtime (C# ↔ Python)
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Python Processing Pipeline                       │
│  Infrastructure.Extraction.Python (via CSnakes)              │
│  - LayoutLMv3: Header extraction, field detection           │
│  - Table Transformer: Transaction table detection            │
│  - GotOcr2/Donut/TrOCR: Text extraction                     │
│  - CLIP: Document quality verification                       │
│  → Structured JSON Output                                   │
└──────────────────────┬──────────────────────────────────────┘
                       │ Return to C#
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Data Normalization Layer (C#)                   │
│  Application.Services.Validation                            │
│  - Currency Formatting (Mexican Peso: $1,234,567.89)         │
│  - Date Parsing (DD/MMM/YYYY: 15/ENE/2025)                  │
│  - Decimal Precision Handling (2-4 decimal places)          │
│  - Product Type Detection (8 product types)                 │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Validation Engine (C#)                           │
│  Application.Services.Validation                            │
│  - Field-Level Validation (115+ Rules)                      │
│  - Mathematical Reconciliation (15 Formulas)                 │
│  - Cross-Field Dependencies                                 │
│  - Product-Specific Logic (8 Products)                      │
│  - Document Quality Verification (20+ Rules)                 │
│  - Fiscal Compliance Verification (Post-Timbrado)            │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Issue Detection & Reporting (C#)                │
│  Application.Services.Validation                            │
│  - Issue Code Assignment (Taxonomy: HDR, INT, CALC, etc.)   │
│  - Severity Classification (Critical, High, Medium, Low)    │
│  - Human Review Flagging                                    │
│  - Marked PDF Generation (PdfSharp)                        │
│  - Error Configuration Service Integration                   │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Data Persistence Layer (C#)                     │
│  Infrastructure.Database.SqlServer                          │
│  - VecStatement entity (validated statement data)           │
│  - ValidationResult entity (115+ rule results)               │
│  - IssueRecord entity (issue codes, severity, positions)      │
│  - AuditRecord entity (7-year retention, CNBV compliance)    │
│  - ErrorConfiguration entity (database-driven error config) │
└─────────────────────────────────────────────────────────────┘
```

### Component Architecture Details

#### 1. Python Integration Layer (CSnakes)

**Purpose:** Bridge between C# orchestration and Python ML models

**Compatibility Note:** ✓ **Reuses existing CSnakes integration pattern from ExxerCube.Prisma.Infrastructure.Python.GotOcr2**
- Existing `GotOcr2OcrExecutor` demonstrates proven pattern for Python interop
- VEC extraction follows same pattern with new Python modules
- Virtual environment management already established

**Technology Stack:**
- **CSnakes.Runtime:** C# ↔ Python interop library (✓ **already in use**)
- **Python 3.9+:** ML model execution environment (✓ **already configured**)
- **HuggingFace Transformers:** Pre-trained and fine-tuned models (⚠️ **new models to add**)

**Key Models:**
1. **LayoutLMv3** (`microsoft/layoutlmv3-base`)
   - Fine-tuned on VEC statements
   - Header extraction, field detection, document classification
   - Product type auto-detection (8 product types)

2. **Table Transformer** (`microsoft/table-transformer-detection`)
   - Transaction table detection and extraction
   - Handles multi-column layouts, page breaks, split rows

3. **CLIP** (`openai/clip-vit-large-patch14`)
   - Document quality verification
   - Logo presence/position verification
   - Image quality assessment

4. **Donut** (Optional, `naver-clova-ix/donut-base-finetuned-cord-v2`)
   - OCR-free document understanding
   - Backup for low-quality scans

5. **TrOCR** (Backup, `microsoft/trocr-large-printed`)
   - Transformer-based OCR fallback
   - Spanish text support

**Architecture Pattern:**
```csharp
// Domain Interface
public interface IVecStatementExtractor
{
    Task<Result<VecStatementData>> ExtractAsync(
        byte[] pdfBytes,
        CancellationToken cancellationToken);
}

// Infrastructure Implementation
public class VecStatementExtractor : IVecStatementExtractor
{
    private readonly IPythonEnvironment _pythonEnv;
    
    public async Task<Result<VecStatementData>> ExtractAsync(
        byte[] pdfBytes,
        CancellationToken cancellationToken)
    {
        using var scope = _pythonEnv.CreateScope();
        dynamic vecExtractor = scope.Import("vec_extraction");
        var result = await Task.Run(() =>
            vecExtractor.extract_vec_statement(pdfBytes),
            cancellationToken);
        return MapToVecStatementData(result);
    }
}
```

**Performance Targets:**
- Python extraction pipeline: <10 seconds (target), <15 seconds (maximum)
- Model loading: Cached on startup, reused across requests
- Batch processing: Parallel execution for multiple statements

#### 2. Validation Engine Architecture

**Purpose:** Apply 115+ validation rules across 12 categories

**Compatibility Note:** ⚠️ **New VEC-specific component** - Follows existing validation patterns
- Extends existing `Result<T>` error handling pattern (IndQuestResults package)
- Follows Railway Oriented Programming like `OcrProcessingService`
- Integrates with existing `AuditLoggerService` for validation audit trail
- Uses existing `IEventPublisher` for real-time validation progress updates

**Validation Categories:**
1. **Header Validation (HDR):** Account numbers, dates, client information
2. **Interest Validation (INT):** Interest calculations, rate conversions
3. **Calculation Validation (CALC):** Mathematical reconciliation (15 formulas)
4. **Transaction Validation (TXN):** Transaction table integrity
5. **Product Validation (PROD):** Product-specific rules (8 product types)
6. **Rate Validation (RATE):** Interest rate tiers, variable rates
7. **Data Quality (DQ):** Formatting, completeness, consistency
8. **Extraction Issues (EXT):** OCR confidence, table recognition
9. **Image Quality (IMG):** Logo presence, image clarity
10. **Font Compliance (FONT):** Approved fonts (Aptos), no overlaps
11. **Layout Compliance (LAYOUT):** Document structure, alignment
12. **Marketing Compliance (MKT):** Brand guidelines
13. **Fiscal Compliance (FISCAL):** Post-timbrado verification

**Architecture Pattern:**
```csharp
// Validation Rule Interface
public interface IVecValidationRule
{
    string RuleCode { get; }
    ValidationSeverity Severity { get; }
    Task<Result<ValidationResult>> ValidateAsync(
        VecStatementData statement,
        CancellationToken cancellationToken);
}

// Validation Engine
public class VecValidationEngine
{
    private readonly IEnumerable<IVecValidationRule> _rules;
    
    public async Task<Result<ValidationReport>> ValidateAsync(
        VecStatementData statement,
        CancellationToken cancellationToken)
    {
        var results = new List<ValidationResult>();
        foreach (var rule in _rules)
        {
            var result = await rule.ValidateAsync(statement, cancellationToken);
            if (result.IsFailure)
                results.Add(new ValidationResult(rule.RuleCode, result.Error!));
        }
        return new ValidationReport(results);
    }
}
```

**Mathematical Reconciliation (15 Formulas):**
1. **FORMULA-001:** Daily Average Balance Interest
2. **FORMULA-002:** Annual to Daily Rate Conversion
3. **FORMULA-003:** Daily Rate to Annual Rate Conversion
4. **FORMULA-004:** ISR Tax Calculation
5. **FORMULA-005:** Net Interest Calculation
6. **FORMULA-006:** Compound Interest (CEDE/Pagaré)
7. **FORMULA-007:** Accrued Interest Calculation
8. **FORMULA-008:** Transaction Balance Update
9. **FORMULA-009:** Period Balance Reconciliation
10. **FORMULA-010:** Average Daily Balance
11. **FORMULA-011:** Market Value Calculation
12. **FORMULA-012:** UDI to Peso Conversion
13. **FORMULA-013:** Rate Reasonableness Check
14. **FORMULA-014:** Rate Consistency Check
15. **FORMULA-015:** CAT (Total Annual Cost) Calculation

**Product-Specific Validation:**
- **Vista (Checking Account):** Balance calculations, transaction validation
- **Recompra (Repurchase Agreement):** Rate validation, maturity calculations
- **Reporto (Repo Transaction):** Collateral validation, rate calculations
- **CEDE (Certificate of Deposit):** Compound interest, maturity validation
- **Pagaré (Promissory Note):** Fixed interest, maturity calculations
- **UDIBONO (UDI-denominated Bond):** UDI conversion, inflation adjustments
- **Fondos (Investment Funds):** NAV calculations, position valuation
- **Acciones (Stocks):** Market value, position calculations

**Performance Targets:**
- Validation execution: <8 seconds (target), <15 seconds (maximum)
- Rule execution: Parallel where possible, sequential for dependencies
- Caching: Validation results cached for repeated validations

#### 3. Document Quality Verification Architecture

**Purpose:** Verify document quality, visual compliance, and fiscal compliance

**Quality Verification Categories:**

1. **Image Verification (REQ-029):**
   - Logo presence and position verification (CLIP model)
   - Image quality assessment (blur, clarity, resolution)
   - Marketing image validation

2. **Font Verification (REQ-030):**
   - Approved font validation (Aptos font family)
   - Font size compliance
   - Text overlap detection

3. **Layout Verification (REQ-031):**
   - Document structure validation
   - Column alignment
   - Page break handling

4. **Marketing Compliance (REQ-032):**
   - Brand guidelines compliance
   - Marketing image placement
   - Color scheme validation

5. **Fiscal Compliance (REQ-033, REQ-035):**
   - Pre-timbrado: RFC validation, fiscal period verification
   - Post-timbrado: Barcode verification, cadena original validation, digital timbre validation

**Architecture Pattern:**
```csharp
public interface IVisualComplianceValidator
{
    Task<Result<VisualComplianceReport>> ValidateAsync(
        VecStatementData statement,
        byte[] pdfBytes,
        CancellationToken cancellationToken);
}

public interface IImageQualityValidator
{
    Task<Result<ImageQualityReport>> ValidateAsync(
        byte[] pdfBytes,
        CancellationToken cancellationToken);
}

public interface IFiscalComplianceValidator
{
    Task<Result<FiscalComplianceReport>> ValidatePostTimbradoAsync(
        VecStatementData statement,
        byte[] stampedPdfBytes,
        CancellationToken cancellationToken);
}
```

**Performance Targets:**
- Document quality check: <5 seconds (target), <10 seconds (maximum)
- Post-timbrado verification: <10 seconds (target), <20 seconds (maximum)

#### 4. Error Configuration System Architecture

**Purpose:** Database-driven error configuration for runtime flexibility

**Architecture:**
- **Error Configuration Entity:** Stores error codes, messages, images, positions, artifact types
- **Startup Caching:** Error configuration loaded on startup, cached in memory
- **Lazy Loading:** Error messages loaded on-demand when errors occur
- **Image Storage:** Error images stored in database (ErrorConfiguration entity)

**Error Code Taxonomy:**
- **HDR-001 to HDR-XXX:** Header issues
- **INT-001 to INT-XXX:** Interest issues
- **CALC-001 to CALC-XXX:** Calculation issues
- **TXN-001 to TXN-XXX:** Transaction issues
- **PROD-001 to PROD-XXX:** Product issues
- **RATE-001 to RATE-XXX:** Rate issues
- **DQ-001 to DQ-XXX:** Data quality issues
- **EXT-001 to EXT-XXX:** Extraction issues
- **IMG-001 to IMG-XXX:** Image issues
- **FONT-001 to FONT-XXX:** Font issues
- **LAYOUT-001 to LAYOUT-XXX:** Layout issues
- **MKT-001 to MKT-XXX:** Marketing issues
- **FISCAL-001 to FISCAL-XXX:** Fiscal issues

**Error Structure:**
```csharp
public class ValidationError
{
    public string ErrorCode { get; init; } // e.g., "VALIDATION.IMAGE.NOT_FOUND"
    public string Message { get; init; } // Database-driven, configurable
    public ValidationSeverity Severity { get; init; }
    public string? ImageUrl { get; init; } // Error annotation image
    public Position? Position { get; init; } // Page, X, Y coordinates
    public string? ArtifactType { get; init; } // Type of issue
}
```

#### 5. Marked PDF Generation Architecture

**Purpose:** Generate PDFs with visual markers highlighting all failing elements

**Technology:** PdfSharp (C# PDF manipulation library)

**Architecture Pattern:**
```csharp
public interface IMarkedPdfGenerator
{
    Task<Result<byte[]>> GenerateMarkedPdfAsync(
        byte[] originalPdfBytes,
        ValidationReport validationReport,
        CancellationToken cancellationToken);
}
```

**Marking Features:**
- Visual markers (red boxes, highlights) for failing fields
- Issue code annotations
- Severity indicators (color-coded)
- Position markers (page, coordinates)
- Error message overlays

**Performance Targets:**
- Marked PDF generation: <5 seconds per statement
- Storage: Additional 1.3-2TB/month for marked PDFs (if 10% require marking)

### Batch Processing Architecture

**Monthly Processing Requirements:**
- **Volume:** 130,000-200,000 statements per month (1% quality control sample)
- **Review Window:** 1-2 business days (target), 3-5 days (current baseline)
- **Peak Daily Volume:** 65,000-100,000 statements per day (during review window)
- **Peak Hourly Load:** 5,000-8,000 statements per hour (during peak periods)

**Architecture Components:**

1. **Queue Management:**
   - Azure Service Bus or equivalent message queue
   - Batch job submission
   - Priority queuing for urgent statements

2. **Auto-Scaling:**
   - Horizontal scaling based on queue depth
   - Worker node auto-provisioning
   - Load balancing across workers

3. **Progress Tracking:**
   - Real-time progress updates via IndFusion.Ember
   - Batch status dashboard
   - Completion notifications

4. **Error Recovery:**
   - Failed statement retry logic
   - Dead-letter queue for unrecoverable failures
   - Manual intervention workflow

**Architecture Pattern:**
```csharp
public interface IBatchProcessingService
{
    Task<Result<BatchJob>> SubmitBatchAsync(
        IEnumerable<byte[]> pdfStatements,
        CancellationToken cancellationToken);
    
    Task<Result<BatchStatus>> GetBatchStatusAsync(
        Guid batchJobId,
        CancellationToken cancellationToken);
}

public class BatchProcessingService : IBatchProcessingService
{
    private readonly IMessageQueue _queue;
    private readonly IWorkloadCalculator _workloadCalculator;
    
    public async Task<Result<BatchJob>> SubmitBatchAsync(
        IEnumerable<byte[]> pdfStatements,
        CancellationToken cancellationToken)
    {
        var statements = pdfStatements.ToList();
        var workload = await _workloadCalculator.CalculateAsync(
            statements.Count,
            cancellationToken);
        
        var batchJob = new BatchJob(Guid.NewGuid(), statements.Count);
        await _queue.EnqueueBatchAsync(batchJob, statements, cancellationToken);
        
        return Result.Success(batchJob);
    }
}
```

### Storage Architecture

**Compatibility Note:** ✓ **Extends existing Prisma storage patterns**
- Reuses existing `PrismaDbContext` (EF Core) for structured data
- Follows existing file storage abstraction (`Infrastructure.FileStorage`)
- Extends existing audit logging (`AuditLoggerService`) for 7-year retention
- **Additive-only schema:** New VEC tables, no modifications to existing Prisma tables

**Storage Requirements:**
- **PDF Storage:** 1.3-2TB per month (130K-200K statements × ~10MB average)
- **7-Year Retention:** ~109-168TB total (CNBV compliance requirement)
- **Structured Data:** 650GB-1TB per month (130K-200K statements × ~5MB structured data)
- **Marked PDFs:** Additional 1.3-2TB per month (if 10% require marking)
- **Post-Timbrado PDFs:** Additional 1.3-2TB per month (stamped statements)

**Storage Strategy:**
- **Hot Storage:** Recent statements (last 3 months) in fast storage
- **Warm Storage:** 3-12 months in standard storage
- **Cold Storage:** 1-7 years in archive storage (cost-optimized)
- **Backup:** Full backup with 7-year retention

**Database Tables (New - Additive Only):**
- `VecStatements` - VEC statement data (new table)
- `ValidationResults` - Validation rule results (new table)
- `IssueRecords` - Issue codes, severity, positions (new table)
- `ErrorConfiguration` - Runtime error messages (new table)
- **No modifications to existing Prisma tables** - backward compatibility guaranteed

### Integration Architecture

**Downstream System Integration:**

1. **Core Banking System:**
   - Standardized data export (JSON/XML)
   - Real-time or batch (configurable)
   - Data mapping to core banking schema

2. **Reporting & Analytics Systems:**
   - Structured data export or API access
   - Aggregated reporting support
   - Real-time or scheduled batch

3. **Compliance & Audit Systems:**
   - Complete audit log export
   - 7-year retention (CNBV requirement)
   - On-demand or scheduled export

**Integration Patterns:**
- **API Integration:** RESTful APIs for real-time integration
- **File Export:** Scheduled file exports (CSV, JSON, XML)
- **Database Integration:** Read-only database access for reporting
- **Event-Driven:** Publish events when statements are processed/approved

### Security & Compliance Architecture

**Security Requirements:**
- **Encryption at Rest:** AES-256 for PDFs and database
- **Encryption in Transit:** TLS 1.3 for all API calls
- **Access Control:** Role-based access (Admin, Analyst, Viewer)
- **PII Protection:** Account holder names encrypted, account numbers masked in logs
- **Audit Logs:** Complete traceability for all data access

**Compliance Requirements:**
- **CNBV Compliance:** Mexican banking regulations
- **Data Retention:** 7 years for financial statements
- **Audit Trail:** Complete traceability of all processing steps
- **Data Privacy:** GDPR/equivalent for personal data

### Performance Architecture

**Processing Time SLAs:**

| Processing Stage | Target Time | Maximum Time | Current Manual Equivalent |
|------------------|-------------|--------------|---------------------------|
| PDF Upload | <5 seconds | 10 seconds | N/A (manual file handling) |
| Python Extraction Pipeline | <10 seconds | 15 seconds | 30-60 minutes (manual data entry) |
| Data Normalization | <2 seconds | 5 seconds | 15-30 minutes (manual formatting) |
| Validation (all rules) | <8 seconds | 15 seconds | 60-90 minutes (manual validation) |
| Document Quality Check | <5 seconds | 10 seconds | N/A (not performed manually) |
| Post-Timbrado Fiscal Verification | <10 seconds | 20 seconds | 30-60 minutes (manual verification) |
| **Total End-to-End** | **<30 seconds** | **60 seconds** | **120-240 minutes (2-4 hours)** |

**Efficiency Improvement:**
- **Time Reduction:** 99.8% reduction (from 2-4 hours to <30 seconds per statement)
- **Review Window:** 50-60% reduction (from 3-5 days to 1-2 days)
- **Peak Daily Capacity:** 2.5x increase (from 26K-40K to 65K-100K statements per day)
- **Throughput Increase:** 100x improvement per analyst (from 5-10 to 500-1,000 statements per day)
- **Labor Reduction:** 97% reduction (from 260K-800K to 6.5K-20K analyst hours per month)

**Scalability Targets:**
- **Concurrent Processing:** 500 statements simultaneously
- **Monthly Volume:** 130,000-200,000 statements per month
- **Peak Daily Volume:** 65,000-100,000 statements per day
- **Peak Hourly Load:** 5,000-8,000 statements per hour
- **Horizontal Scaling:** Auto-scaling based on queue depth
- **Load Testing:** 2x peak load (10,000-16,000 statements/hour)

### Error Handling Architecture

**Error Recovery Strategies:**

1. **Extraction Errors:**
   - **PDF Parsing Failure (EXT-001):** Log error, notify user, request re-upload
   - **Table Structure Not Recognized (EXT-002):** Flag for manual review, attempt alternate parsing
   - **OCR Confidence Below Threshold (EXT-004):** Flag affected fields, request re-scan if critical

2. **Validation Errors:**
   - **Auto-Fix Eligible Issues:**
     - `INT-003`: Calculate daily rate from annual rate
     - `DQ-001` to `DQ-005`: Apply formatting corrections
     - `EXT-005`: Merge split content
   - **Human Review Required:**
     - All Critical severity issues
     - High severity calculation mismatches
     - Suspicious patterns (e.g., rate anomalies)
   - **Override Mechanism:**
     - Authorized users can override Medium/Low issues
     - Override reason required and logged
     - Critical issues cannot be overridden

3. **Infrastructure Errors:**
   - **Retry Logic:** Exponential backoff for transient failures
   - **Dead-Letter Queue:** Unrecoverable failures queued for manual review
   - **Graceful Degradation:** Partial results returned when possible

### Testing Architecture

**Test Strategy:**
- **Unit Tests:** Individual validation rules, formulas, normalization logic
- **Integration Tests:** Python pipeline integration, database operations
- **System Tests:** End-to-end processing with real PDFs
- **Performance Tests:** Load testing at 2x peak load (10,000-16,000 statements/hour)
- **Accuracy Tests:** 1000-statement sample validation (target: ≥99.9% accuracy)

**Test Data Requirements:**
- **Anonymized VEC Statements:** 100-200 statements (all product types)
- **Edge Cases:** Poor quality scans, non-standard layouts, missing fields
- **Validation Scenarios:** All 115+ validation rules covered
- **Product Coverage:** All 8 product types represented

### Monitoring & Observability

**Key Metrics:**
- **Processing Time:** 50th, 75th, 90th, 95th, 99th percentiles
- **Accuracy:** Field-level extraction accuracy (target: ≥99.9%)
- **False Positive Rate:** Validation errors that are actually correct (target: <1%)
- **Automation Rate:** Statements requiring manual intervention (target: <0.1%)
- **Throughput:** Statements processed per hour/day
- **Error Rates:** By error code, severity, product type

**Monitoring Tools:**
- **Serilog + SEQ:** Structured logging with SQL search capabilities (✓ **Reuse existing**)
- **Real-Time Dashboard:** IndFusion.Ember for live updates (✓ **Expand existing**)
- **Performance Metrics:** Application Insights or equivalent (✓ **Reuse existing**)
- **Alerting:** SLA violation alerts, error threshold alerts (⚠️ **New VEC-specific alerts**)

---

## Implementation Compatibility Summary

### Overview

This architecture extends the existing ExxerCube.Prisma system with VEC Statement Processing capabilities. **Approximately 60% of required infrastructure already exists and can be reused directly**, with 40% new VEC-specific components needed. **Zero breaking changes** will be introduced to existing Prisma functionality.

### Compatibility Assessment: EXCELLENT (9/10)

**Strengths:**
1. ✓ Architecture perfectly aligns with existing hexagonal architecture
2. ✓ Result<T> pattern consistently applied throughout
3. ✓ Major infrastructure components (OCR, extraction, imaging) ready for reuse
4. ✓ Python integration pattern (CSnakes) well-established
5. ✓ Testing infrastructure comprehensive and ready
6. ✓ All PRP requirements covered in architecture
7. ✓ Database strategy (additive-only) is safe
8. ✓ Async/await patterns correctly specified

**Risk Level: LOW**
- Existing patterns proven in production
- Clear separation of VEC-specific vs shared components
- Incremental implementation path available
- No breaking changes to existing Prisma functionality

### Component Reuse Matrix

| Category | Reuse | Extend | New | Total |
|----------|-------|--------|-----|-------|
| **Domain Interfaces** | 6 | 2 | 9 | 17 |
| **Infrastructure** | 9 | 3 | 4 | 16 |
| **Database** | 1 | 1 | 4 | 6 |
| **Python Integration** | 3 | 1 | 3 | 7 |
| **Testing** | 4 | 0 | 0 | 4 |
| **UI/Real-time** | 1 | 1 | 0 | 2 |
| **Total** | **24 (46%)** | **8 (15%)** | **20 (39%)** | **52** |

**Reusability Score: 61% direct reuse + extension**

### Critical Dependencies - Existing & Verified

| Dependency | Location | Status | VEC Integration |
|------------|----------|--------|-----------------|
| **CSnakes.Runtime** | NuGet v1.2.1 | ✓ Working | Reuse for new Python models |
| **IOcrExecutor** | Prisma.Domain | ✓ Working | Reuse GotOcr2OcrExecutor |
| **IFieldExtractor<PdfSource>** | Prisma.Domain | ✓ Working | Extend with VEC field defs |
| **PrismaDbContext** | Prisma.Infrastructure.Database | ✓ Working | Add VEC entities |
| **AuditLoggerService** | Prisma.Infrastructure.Database | ✓ Working | Reuse for 7-year audit trail |
| **IndFusion.Ember** | F:\Dynamic\IndFusion\IndFusion.Ember\ | ✓ Existing | Expand for VEC dashboards |
| **Result<T> Pattern** | IndQuestResults NuGet | ✓ Working | Reuse throughout |

### Implementation Roadmap

**Phase 1: Foundation (Weeks 1-2) - Reuse Maximum Existing Code**
```
1. ✓ Reuse IOcrExecutor (GotOcr2OcrExecutor)
2. ✓ Reuse IImagePreprocessor
3. ✓ Reuse IFieldExtractor<PdfSource>
4. ✓ Reuse AuditLoggerService
5. ✓ Extend PrismaDbContext with VEC entities
6. ⚠️ Add Python/vec_extraction.py (follow CSnakes pattern)
7. ⚠️ Add Infrastructure.Validation.VecStatement project
```

**Phase 2: VEC-Specific Components (Weeks 3-4)**
```
8. ⚠️ Add IVecValidationService interface
9. ⚠️ Add VecValidationEngine (115+ rules)
10. ⚠️ Add IVisualComplianceValidator (CLIP integration)
11. ⚠️ Add IMarkedPdfGenerator (PdfSharp)
12. ⚠️ Add Database migrations (VEC tables - additive only)
13. ✓ Expand IndFusion.Ember for VEC dashboards
```

**Phase 3: Advanced Features (Weeks 5-6)**
```
14. ⚠️ Add IErrorConfigurationService (database-driven errors)
15. ⚠️ Add IBatchProcessingService (Azure Service Bus)
16. ⚠️ Add IWorkloadManagement interfaces
17. ⚠️ Performance testing and optimization
```

**Legend:**
- ✓ = Reuse existing component
- ⚠️ = New VEC-specific component

### Integration Checklist

**Before Starting Development:**
- [ ] Verify CSnakes.Runtime installation and Python 3.9+ environment
- [ ] Confirm GotOcr2OcrExecutor is working (test with sample PDF)
- [ ] Verify PrismaDbContext migrations are current
- [ ] Confirm IndFusion.Ember location: F:\Dynamic\IndFusion\IndFusion.Ember\
- [ ] Review existing Result<T> error handling patterns in OcrProcessingService
- [ ] Set up test data: 100-200 anonymized VEC statements (all 8 product types)

**During Development:**
- [ ] Follow existing naming conventions (PascalCase, Service suffix, Async methods)
- [ ] All new interfaces return Result<T>
- [ ] All async methods accept CancellationToken
- [ ] Use existing TestFixtureBase for unit tests
- [ ] Use existing Testcontainers for integration tests
- [ ] Add VEC entities to PrismaDbContext (additive only, no modifications)
- [ ] Follow GotOcr2OcrExecutor pattern for Python integration
- [ ] Reuse AuditLoggerService for all validation audit trails
- [ ] Expand IndFusion.Ember for VEC real-time dashboards

**Quality Gates:**
- [ ] All existing Prisma tests still pass (zero regressions)
- [ ] Architecture tests validate naming and structure conventions
- [ ] VEC extraction accuracy ≥99.9% on 1000-statement sample
- [ ] Processing time <30 seconds (95th percentile)
- [ ] False positive rate <1% on validation rules
- [ ] Database migrations apply cleanly (additive only)
- [ ] IndFusion.Ember dashboards show real-time updates
- [ ] Load testing passes at 2x peak (10,000-16,000 statements/hour)

### Namespace Organization

```
ExxerCube.Prisma.*                    # Existing infrastructure (shared)
├── Domain/                            # ✓ Reuse existing interfaces
├── Infrastructure.Extraction/         # ✓ Reuse GotOcr2OcrExecutor
├── Infrastructure.Imaging/            # ✓ Reuse image quality analysis
├── Infrastructure.Database/           # ✓ Extend PrismaDbContext
├── Infrastructure.Events/             # ✓ Reuse event bus
└── Infrastructure.Python.GotOcr2/     # ✓ Follow CSnakes pattern

ExxerCube.Prisma.Veriqan.*            # New VEC-specific components
├── Domain/                            # ⚠️ New VEC interfaces
│   ├── Interfaces/Validation/
│   ├── Interfaces/ErrorConfiguration/
│   └── Entities/ (VecStatement, etc.)
├── Application/                       # ⚠️ New VEC orchestration
│   └── Services/Validation/
└── Infrastructure/                    # ⚠️ New VEC implementations
    ├── Validation.VecStatement/
    ├── ErrorConfiguration/
    └── WorkloadManagement/

IndFusion.Ember                        # External dependency (expand)
└── F:\Dynamic\IndFusion\IndFusion.Ember\
```

### Success Criteria

**Technical Compatibility:**
- ✓ Zero breaking changes to existing Prisma functionality
- ✓ All existing Prisma tests pass without modification
- ✓ VEC components can be deployed independently
- ✓ Database migrations are additive-only (rollback safe)

**Functional Compatibility:**
- ✓ VEC extraction reuses proven OCR pipeline
- ✓ VEC validation follows Railway Oriented Programming
- ✓ VEC audit trail integrates with existing AuditLoggerService
- ✓ VEC dashboards integrate with existing IndFusion.Ember

**Operational Compatibility:**
- ✓ VEC components use same logging (Serilog + SEQ)
- ✓ VEC components use same monitoring (Application Insights)
- ✓ VEC components use same deployment (Docker)
- ✓ VEC components use same testing infrastructure (xUnit v3)

### Conclusion

The VEC Statement Processing architecture is **production-ready** and **highly compatible** with the existing ExxerCube.Prisma codebase. Implementation teams can proceed confidently, leveraging the robust foundation already in place while adding VEC-specific functionality incrementally. The phased implementation approach ensures risk mitigation and allows for validation at each stage.

**Recommended Next Steps:**
1. Set up development environment with VEC test data
2. Implement Phase 1 foundation components (reuse existing)
3. Validate Python integration with vec_extraction.py prototype
4. Implement Phase 2 VEC-specific validation engine
5. Integrate with IndFusion.Ember for real-time dashboards
6. Complete Phase 3 advanced features (batch processing, workload management)

**Estimated Timeline:** 6 weeks for full implementation (3 phases × 2 weeks)
**Confidence Level:** HIGH (based on proven patterns and existing infrastructure)
