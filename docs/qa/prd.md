# ExxerCube.Prisma Brownfield Enhancement PRD

**Version:** 1.0  
**Date:** 2025-01-12  
**Status:** Draft  
**Project:** ExxerCube.Prisma - Regulatory Compliance Automation System

---

## Table of Contents

1. [Intro Project Analysis and Context](#intro-project-analysis-and-context)
2. [Requirements](#requirements)
3. [User Interface Enhancement Goals](#user-interface-enhancement-goals)
4. [Technical Constraints and Integration Requirements](#technical-constraints-and-integration-requirements)
5. [Epic and Story Structure](#epic-and-story-structure)
6. [Epic Details](#epic-details)

---

## Intro Project Analysis and Context

### Existing Project Overview

#### Analysis Source

**IDE-based fresh analysis** - Project structure analyzed from current codebase.

#### Current Project State

**ExxerCube.Prisma** is a C# implementation of an OCR document processing pipeline using Hexagonal Architecture to integrate with existing Python modules. The solution provides a clean, maintainable interface for processing Spanish legal documents with high accuracy.

**Current Capabilities:**
- OCR processing pipeline for Spanish legal documents
- Hexagonal Architecture with Domain, Application, and Infrastructure layers
- Railway-Oriented Programming using `Result<T>` pattern for error handling
- Python-C# integration via CSnakes library
- Async document processing with concurrent support
- Basic field extraction from OCR text
- Image preprocessing for OCR optimization

**Primary Purpose:**
The system currently focuses on OCR text extraction and basic field extraction from Spanish legal documents. It processes documents through a pipeline that includes image preprocessing, OCR execution, and structured field extraction.

**Technology Foundation:**
- .NET 10 (C#) with nullable reference types
- Python 3.9+ integration for OCR and NLP
- Tesseract OCR engine
- Hexagonal Architecture pattern
- Railway-Oriented Programming (Result<T> pattern)

### Available Documentation Analysis

#### Available Documentation

- ✅ **Tech Stack Documentation** - README.md documents .NET 10, Python 3.9+, Tesseract OCR
- ✅ **Source Tree/Architecture** - Hexagonal Architecture documented in `docs/architecture/hexagonal-architecture.md`
- ✅ **Coding Standards** - Documented in README.md (Railway-Oriented Programming, Result<T> pattern, XML documentation requirements)
- ✅ **API Documentation** - Basic API docs in `docs/api/`
- ⚠️ **External API Documentation** - Python modules documented but may need expansion
- ⚠️ **UX/UI Guidelines** - Blazor Server UI exists but guidelines may need documentation
- ⚠️ **Technical Debt Documentation** - Not explicitly documented
- ✅ **Other:** Development workflow, testing strategy, quality standards documented

**Documentation Status:**
The project has solid foundational documentation for architecture and development practices. The existing Hexagonal Architecture aligns well with the proposed Interface-Driven Development (ITDD) approach in the PRP. However, comprehensive technical debt documentation and detailed UI/UX guidelines may need to be created as part of this enhancement.

### Enhancement Scope Definition

#### Enhancement Type

- ✅ **New Feature Addition** - Adding comprehensive regulatory compliance automation system
- ✅ **Integration with New Systems** - Browser automation, database tracking, SLA management
- ✅ **Major Feature Modification** - Expanding existing OCR pipeline into full compliance workflow
- ⚠️ **Performance/Scalability Improvements** - May be required for high-volume regulatory processing
- ⚠️ **UI/UX Overhaul** - New UI components for manual review and compliance workflows

#### Enhancement Description

This enhancement transforms the existing OCR document processing pipeline into a comprehensive **Regulatory Compliance Automation System** that automates the end-to-end processing of regulatory directives (oficios) from Mexican financial regulatory bodies (UIF, CNBV).

**Note:** The complete feature-to-interface mapping (35 features mapped to 28 distinct interfaces) is defined in the Product Requirements Plan (PRP) document at `Prisma/Fixtures/PRP1/PRP.md`. This PRD focuses on the functional, non-functional, and compatibility requirements derived from those features.

The system implements capabilities organized into **four processing stages**:

1. **Stage 1: Ingestion & Acquisition** - Automated browser agent downloads regulatory documents
2. **Stage 2: Extraction & Classification** - Enhanced metadata extraction, classification, and field matching
3. **Stage 3: Decision Logic & SLA Management** - Identity resolution, legal directive classification, SLA tracking
4. **Stage 4: Final Compliance Response** - SIRO-compliant XML/PDF export generation

This enhancement builds upon the existing OCR infrastructure while adding significant new capabilities for regulatory compliance automation.

#### Impact Assessment

- ⚠️ **Significant Impact** - Substantial existing code changes required

**Rationale:**
- Existing interfaces (`IFieldExtractor`, `IOcrExecutor`, `IImagePreprocessor`) need to be extended or wrapped
- New domain entities and data models need to be added
- Application layer needs new orchestration services for multi-stage processing
- Infrastructure layer needs new adapters (browser automation, database, SLA tracking)
- UI layer needs new components for manual review and compliance workflows
- Database schema changes required for audit logging, SLA tracking, and metadata storage

**Areas of Impact:**
- **Domain Layer**: New interfaces (28 total), new entities (Expediente, Persona, Oficio, ComplianceAction, etc.)
- **Application Layer**: New orchestration services for 4-stage processing workflow
- **Infrastructure Layer**: New adapters for browser automation, database, file storage, SLA management
- **UI Layer**: New Blazor components for manual review, compliance validation, and reporting
- **Database**: New tables for audit logs, file metadata, SLA tracking, identity resolution

### Goals and Background Context

#### Goals

- Automate regulatory document acquisition from UIF/CNBV websites
- Extract and normalize metadata from multiple document formats (XML, DOCX, PDF)
- Classify regulatory directives using deterministic rules and decision trees
- Resolve person identities across RFC variants and alias names
- Track SLA deadlines and escalate impending breaches automatically
- Generate SIRO-compliant compliance packages (XML/PDF) for regulatory submission
- Maintain complete audit trail for compliance and traceability
- Support manual review workflows for ambiguous or low-confidence cases
- Ensure existing OCR functionality remains intact and enhanced

#### Background Context

Financial institutions in Mexico regularly receive official directives (oficios) from regulatory bodies (UIF, CNBV) that require urgent compliance within tight deadlines (typically 1 business day). These directives involve:

- Requests for blocking/unblocking individuals or entities
- Enforcement of legal instruments (e.g., Acuerdo 105/2021)
- Identification and suspension/reactivation of financial products
- Document requests and information gathering

**Current Pain Points:**
- Manual processing introduces operational risk and inconsistencies
- Document formats vary (PDF, DOC, XML) with ambiguous identity information
- Tight compliance timeframes create pressure and error potential
- Limited traceability for audits and compliance reporting
- Manual classification errors lead to compliance failures

**Solution Fit:**
The existing ExxerCube.Prisma OCR pipeline provides a solid foundation for document processing. This enhancement extends it into a complete compliance automation system that:
- Leverages existing OCR and field extraction capabilities
- Adds browser automation for document acquisition
- Introduces classification, identity resolution, and SLA management
- Provides compliance export capabilities
- Maintains the existing Hexagonal Architecture and Railway-Oriented Programming patterns

The enhancement follows Interface-Driven Development (ITDD) principles, defining all 28 interfaces first to enable parallel development and clear contracts between components.

### Change Log

| Change | Date | Version | Description | Author |
|--------|------|----------|-------------|--------|
| Initial PRD Draft | 2025-01-12 | 1.0 | Created brownfield PRD for Regulatory Compliance Automation System enhancement | PM Agent |

---

## Requirements

**Note:** These requirements are based on my understanding of your existing system and the PRP document. Please review carefully and confirm they align with your project's reality.

### Functional Requirements

**FR1:** The system shall automatically download regulatory documents (oficios) from UIF/CNBV websites using browser automation, detecting and downloading PDF, XML, and DOCX files.

**FR2:** The system shall track downloaded files using checksums to prevent redundant downloads and maintain a download history.

**FR3:** The system shall extract metadata from XML documents, including expediente number, oficio number, RFC, names, dates, and legal references.

**FR4:** The system shall extract metadata from DOCX documents using structured field extraction, handling both searchable and scanned documents.

**FR5:** The system shall extract metadata from PDF documents with OCR fallback for scanned/non-searchable PDFs, maintaining compatibility with existing OCR pipeline.

**FR6:** The system shall detect scanned PDFs and apply image preprocessing (deskew, binarization) before OCR processing.

**FR7:** The system shall classify documents into Level 1 categories (Aseguramiento, Desembargo, Documentacion, Informacion, Transferencia, OperacionesIlicitas) using deterministic rules.

**FR8:** The system shall classify documents into Level 2/3 subcategories (Especial, Judicial, Hacendario) based on extracted metadata.

**FR9:** The system shall match and consolidate field values across XML, DOCX, and PDF sources, generating unified metadata records with confidence scores.

**FR10:** The system shall resolve person identities by handling RFC variants and alias names, deduplicating person records across multiple documents.

**FR11:** The system shall classify legal directives from document text, mapping legal clauses to compliance actions (block, unblock, document, transfer, information, ignore).

**FR12:** The system shall calculate SLA deadlines based on intake date and days plazo (business days), tracking remaining time for each regulatory response.

**FR13:** The system shall escalate impending SLA breaches when remaining time falls below critical threshold (default: 4 hours), triggering alerts and notifications.

**FR14:** The system shall provide a manual review interface for ambiguous classifications, low-confidence extractions, or cases requiring human judgment.

**FR15:** The system shall generate SIRO-compliant XML exports from unified metadata records, validating against regulatory schema requirements.

**FR16:** The system shall generate digitally signed PDF exports from unified metadata records, supporting X.509 certificate-based signing (PAdES).

**FR17:** The system shall maintain an immutable audit log of all processing steps, including downloads, classifications, extractions, reviews, and exports.

**FR18:** The system shall generate Excel layouts from unified metadata for structured data delivery to SIRO registration systems.

**FR19:** The system shall summarize PDF content into requirement categories (bloqueo, desbloqueo, documentacion, transferencia, informacion) using semantic analysis.

**FR20:** The system shall validate field completeness and consistency before export, ensuring all required regulatory fields are present and valid.

### Non-Functional Requirements

**NFR1:** The system shall maintain existing OCR performance characteristics, not exceeding current memory usage by more than 20% for OCR operations.

**NFR2:** The system shall process documents asynchronously, supporting concurrent processing of multiple documents without blocking operations.

**NFR3:** The system shall complete browser automation operations (launch, navigate, detect files) within 5 seconds for typical regulatory websites.

**NFR4:** The system shall complete metadata extraction within 2 seconds for XML/DOCX files and within 30 seconds for PDF files requiring OCR.

**NFR5:** The system shall complete classification operations within 500ms per document, maintaining deterministic rule-based performance.

**NFR6:** The system shall support horizontal scaling through stateless design, enabling deployment as independent microservices for each processing stage.

**NFR7:** The system shall maintain 99.9% uptime for critical SLA tracking and escalation services.

**NFR8:** The system shall encrypt sensitive data at rest and in transit (TLS 1.3), ensuring compliance with financial regulatory data protection requirements.

**NFR9:** The system shall retain audit logs for a minimum of 7 years, supporting regulatory compliance and audit requirements.

**NFR10:** The system shall support digital signature operations for PDF/XML exports, integrating with X.509 certificate management systems.

**NFR11:** The system shall provide structured logging with correlation IDs, enabling distributed tracing across processing stages.

**NFR12:** The system shall maintain backward compatibility with existing OCR pipeline interfaces, ensuring existing integrations continue to function.

**NFR13:** The system shall support configuration-driven matching policies, allowing customization of field matching rules without code changes.

**NFR14:** The system shall handle file processing errors gracefully, logging errors to audit trail and queuing failed documents for manual review.

**NFR15:** The system shall support batch processing of multiple documents, optimizing throughput for high-volume regulatory periods.

### Compatibility Requirements

**CR1:** The system must maintain compatibility with existing `IFieldExtractor`, `IOcrExecutor`, and `IImagePreprocessor` interfaces, extending rather than replacing current OCR functionality.

**CR2:** The system must maintain compatibility with existing Python OCR modules (`prisma-ocr-pipeline`, `prisma-ai-extractors`), ensuring CSnakes integration continues to function.

**CR3:** The system must maintain UI/UX consistency with existing Blazor Server components, following established MudBlazor design patterns and component library usage.

**CR4:** The system must maintain compatibility with existing database schema, adding new tables without modifying existing table structures.

**CR5:** The system must maintain compatibility with existing Railway-Oriented Programming patterns, using `Result<T>` for all new interface methods.

**CR6:** The system must maintain compatibility with existing Hexagonal Architecture boundaries, placing new interfaces in Domain layer and implementations in Infrastructure layer.

**CR7:** The system must maintain compatibility with existing .NET 10 and Python 3.9+ runtime requirements, not introducing new runtime dependencies.

**CR8:** The system must maintain compatibility with existing file storage patterns, supporting both local filesystem and Azure Blob Storage adapters.

---

## Detailed Rationale

**Trade-offs and Choices Made:**

1. **Interface Extension vs. Replacement**: I chose to extend existing interfaces (`IFieldExtractor` → `IFieldExtractor<T>`) rather than replace them, preserving backward compatibility. This allows gradual migration but may require adapter patterns.

2. **Performance Targets**: Performance requirements are based on existing OCR pipeline benchmarks, with OCR operations capped at 30 seconds to maintain user experience. This may need adjustment based on actual document complexity.

3. **SLA Escalation Threshold**: Default 4-hour threshold for escalation balances urgency with operational flexibility. This should be configurable per regulatory body or directive type.

4. **Audit Retention**: 7-year retention aligns with Mexican financial regulatory requirements but may need adjustment based on specific institutional policies.

5. **Microservices Architecture**: Stateless design enables scalability but adds complexity for inter-stage communication. Message queue integration (Azure Service Bus/RabbitMQ) is assumed but not explicitly required.

**Key Assumptions:**

1. Regulatory websites (UIF/CNBV) have consistent structure and file download patterns that can be automated via browser automation.

2. Existing Python OCR modules can be extended or wrapped to support new metadata extraction requirements without major refactoring.

3. Database schema changes can be managed via Entity Framework Core migrations without impacting existing data.

4. Digital signature certificates are managed externally and provided via configuration or secure key management.

5. SIRO XML schema and regulatory requirements are well-defined and stable, allowing schema validation.

**Areas Requiring Validation:**

1. **Browser Automation Feasibility**: Need to confirm UIF/CNBV website structure supports reliable browser automation without CAPTCHA or complex authentication.

2. **Performance Benchmarks**: Actual document processing times may vary significantly based on document complexity, OCR quality, and server resources.

3. **SLA Calculation Rules**: Business day calculation may need to account for Mexican holidays and regulatory body-specific rules.

4. **Identity Resolution Accuracy**: RFC variant matching and alias resolution algorithms need validation against real-world data to ensure acceptable false positive/negative rates.

5. **Legal Directive Classification**: Legal text interpretation requires domain expertise validation to ensure accurate mapping to compliance actions.

---

---

## Tree of Thoughts Deep Dive Analysis

**Elicitation Method Applied:** Tree of Thoughts Deep Dive

This analysis breaks down the requirements into discrete reasoning paths, evaluates each path's feasibility, and identifies optimal solution approaches.

### Reasoning Path 1: Stage-by-Stage Implementation Strategy

**Path Description:** Implement the 4 stages sequentially, completing each stage before moving to the next.

**Intermediate Steps:**
1. Stage 1 (Ingestion) → Complete browser automation and download tracking
2. Stage 2 (Extraction) → Enhance existing OCR with classification and field matching
3. Stage 3 (Decision Logic) → Add identity resolution and SLA management
4. Stage 4 (Response) → Implement export and compliance generation

**Evaluation:** ✅ **SURE** - This path is optimal because:
- Each stage builds on the previous one
- Allows incremental value delivery
- Enables testing at each stage boundary
- Reduces risk by validating assumptions early

**Dependencies Identified:**
- Stage 2 depends on Stage 1 (needs downloaded files)
- Stage 3 depends on Stage 2 (needs extracted metadata)
- Stage 4 depends on Stage 3 (needs validated unified records)

---

### Reasoning Path 2: Interface-Driven Parallel Development

**Path Description:** Define all 28 interfaces first, then implement in parallel by different teams.

**Intermediate Steps:**
1. Define all interface contracts (ITDD approach from PRP)
2. Create mock implementations for testing
3. Implement Stage 1 interfaces (4 interfaces)
4. Implement Stage 2 interfaces (13 interfaces) - can parallelize
5. Implement Stage 3 interfaces (8 interfaces) - can parallelize
6. Implement Stage 4 interfaces (3 interfaces)

**Evaluation:** ✅ **LIKELY** - This path is feasible but requires:
- Strong interface contracts upfront
- Coordination between teams
- Integration testing strategy
- Mock implementations for dependent interfaces

**Advantages:**
- Enables parallel development
- Clear contracts reduce integration issues
- Follows ITDD methodology from PRP

**Risks:**
- Interface contracts may need refinement during implementation
- Integration complexity increases with parallel work
- Requires strong architectural oversight

---

### Reasoning Path 3: Feature-Centric Implementation

**Path Description:** Implement by feature clusters rather than stages, grouping related features.

**Feature Clusters:**
- **Cluster A:** Document Acquisition (Features 1-5 per PRP) → Browser automation, download tracking
- **Cluster B:** Document Processing (Features 6-19 per PRP) → Extraction, classification, OCR
- **Cluster C:** Identity & Compliance Logic (Features 20-24 per PRP) → Identity resolution, legal classification, SLA
- **Cluster D:** Export & Reporting (Features 25-35 per PRP) → Export, summarization, validation

**Note:** The complete feature enumeration (Features 1-35) and their mapping to 28 interfaces is documented in the PRP document. This PRD focuses on requirements (FRs, NFRs, CRs) derived from those features.

**Evaluation:** ⚠️ **LIKELY** - This path works but:
- May create dependencies between clusters
- Less clear stage boundaries
- Could lead to partial implementations

**Trade-off:** Faster feature delivery vs. cleaner stage boundaries

---

### Reasoning Path 4: Backward Compatibility Preservation

**Path Description:** Ensure every new interface extends or wraps existing functionality.

**Critical Compatibility Points:**
1. `IFieldExtractor` → `IFieldExtractor<T>` (generic extension)
2. `IOcrExecutor` → Wrapped by `IMetadataExtractor` for OCR fallback
3. `IImagePreprocessor` → Extended by `IScanCleaner` for PDF preprocessing
4. Existing Python modules → Wrapped via `IPythonInteropService`

**Evaluation:** ✅ **SURE** - This path is essential because:
- Prevents breaking existing integrations
- Allows gradual migration
- Maintains system stability during enhancement

**Implementation Strategy:**
- Create adapter patterns for existing interfaces
- Use composition over modification
- Maintain separate test suites for existing vs. new functionality

---

### Reasoning Path 5: Performance vs. Accuracy Trade-offs

**Path Description:** Evaluate performance requirements against accuracy needs.

**Performance Critical Paths:**
1. **Browser Automation (NFR3: <5s)** → ✅ Achievable with Playwright headless mode
2. **Metadata Extraction (NFR4: <2s XML/DOCX, <30s PDF)** → ⚠️ OCR may exceed 30s for complex documents
3. **Classification (NFR5: <500ms)** → ✅ Achievable with rule-based deterministic logic
4. **Identity Resolution (Not specified)** → ⚠️ May need 1-2 seconds for complex RFC matching

**Evaluation:** ⚠️ **LIKELY** - Some performance targets may need adjustment:
- OCR processing time depends on document complexity
- Identity resolution may require database lookups (slower)
- Field matching across formats may take longer than expected

**Recommendations:**
- Make OCR timeout configurable (default 30s, max 60s)
- Add performance monitoring to identify bottlenecks
- Consider caching for identity resolution results
- Allow async processing for non-critical paths

---

### Reasoning Path 6: Error Handling and Resilience

**Path Description:** Analyze error handling requirements across all stages.

**Error Scenarios by Stage:**
1. **Stage 1 Errors:** Browser failures, network timeouts, duplicate detection failures
2. **Stage 2 Errors:** File corruption, OCR failures, parsing errors, classification ambiguity
3. **Stage 3 Errors:** Identity resolution failures, SLA calculation errors, legal classification ambiguity
4. **Stage 4 Errors:** Schema validation failures, digital signature errors, export format errors

**Evaluation:** ✅ **SURE** - Error handling is critical:
- All interfaces use `Result<T>` pattern (Railway-Oriented Programming)
- NFR14 requires graceful error handling
- FR17 requires audit logging of all errors
- FR14 provides manual review for error recovery

**Missing Requirements Identified:**
- **FR21 (NEW):** The system shall retry transient failures (network, file locks) with exponential backoff
- **FR22 (NEW):** The system shall queue failed documents for retry after manual intervention
- **FR23 (NEW):** The system shall provide error recovery workflows for each processing stage

---

### Reasoning Path 7: Data Model Completeness

**Path Description:** Verify all data models from PRP are represented in requirements.

**Data Models from PRP:**
- ✅ Expediente → Covered by FR3 (XML extraction)
- ✅ Persona → Covered by FR10 (identity resolution)
- ✅ Oficio → Covered by FR3, FR11 (legal classification)
- ✅ ComplianceAction → Covered by FR11 (legal directive mapping)
- ✅ SLAStatus → Covered by FR12, FR13 (SLA tracking)
- ✅ AuditRecord → Covered by FR17 (audit logging)
- ✅ UnifiedMetadataRecord → Covered by FR9 (field matching)
- ✅ MatchedFields → Covered by FR9 (field matching)
- ✅ RequirementSummary → Covered by FR19 (PDF summarization)

**Evaluation:** ✅ **SURE** - All major data models are covered, but:
- May need explicit requirements for data model persistence
- Database schema requirements could be more explicit
- Data migration requirements not specified

**Missing Requirements Identified:**
- **FR24 (NEW):** The system shall persist all domain entities (Expediente, Persona, Oficio) to database with Entity Framework Core
- **FR25 (NEW):** The system shall support data migration scripts for schema evolution
- **FR26 (NEW):** The system shall maintain referential integrity between related entities

---

### Reasoning Path 8: Integration Points Analysis

**Path Description:** Analyze integration requirements between stages and external systems.

**Stage Integration Points:**
- ✅ Stage 1 → Stage 2: FileMetadata transfer (CR1, CR8)
- ✅ Stage 2 → Stage 3: UnifiedMetadataRecord transfer (FR9)
- ✅ Stage 3 → Stage 4: Validated UnifiedMetadataRecord (FR20)

**External System Integrations:**
- ✅ Browser Automation → UIF/CNBV websites (FR1)
- ✅ Python OCR Modules → CSnakes integration (CR2)
- ✅ Digital Signatures → X.509 certificate systems (FR16, NFR10)
- ✅ SIRO Export → Regulatory submission systems (FR15)
- ⚠️ **MISSING:** Notification/Alert system for SLA escalations (FR13 mentions alerts but no integration requirement)

**Evaluation:** ⚠️ **LIKELY** - Most integrations covered, but:
- SLA escalation notification mechanism not specified
- Manual review UI integration with backend not detailed
- Excel layout generation integration point unclear

**Missing Requirements Identified:**
- **FR27 (NEW):** The system shall integrate with notification systems (email, SMS, Slack) for SLA escalation alerts
- **FR28 (NEW):** The system shall provide REST API endpoints for manual review UI components
- **FR29 (NEW):** The system shall support webhook callbacks for export completion notifications

---

### Reasoning Path 9: Security and Compliance Deep Dive

**Path Description:** Analyze security and compliance requirements in detail.

**Security Requirements:**
- ✅ Encryption at rest and in transit (NFR8)
- ✅ Digital signatures (FR16, NFR10)
- ✅ Audit logging (FR17, NFR9)
- ⚠️ **MISSING:** Authentication/Authorization requirements
- ⚠️ **MISSING:** Role-based access control for manual review
- ⚠️ **MISSING:** Data retention and deletion policies

**Compliance Requirements:**
- ✅ SIRO schema compliance (FR15)
- ✅ Audit retention (NFR9: 7 years)
- ✅ Legal constraints (mentioned in PRP but not in requirements)
- ⚠️ **MISSING:** Non-notification enforcement requirement (mentioned in PRP)

**Evaluation:** ⚠️ **LIKELY** - Security requirements need expansion:
- Financial regulatory systems require strong access control
- Manual review workflows need role-based permissions
- Data privacy requirements (GDPR-like) may apply

**Missing Requirements Identified:**
- **FR30 (NEW):** The system shall enforce role-based access control (RBAC) for manual review operations
- **FR31 (NEW):** The system shall prevent client notification unless explicitly allowed by legal directive (non-notification enforcement)
- **FR32 (NEW):** The system shall support data retention policies with automated archival and deletion
- **NFR16 (NEW):** The system shall authenticate users via Azure AD or Identity Server
- **NFR17 (NEW):** The system shall encrypt sensitive PII data (RFC, names, addresses) at field level

---

### Optimal Solution Path Synthesis

**Recommended Approach:** Combine Paths 1, 4, and 6:

1. **Sequential Stage Implementation** (Path 1) - Reduces risk, enables incremental delivery
2. **Backward Compatibility Preservation** (Path 4) - Essential for brownfield enhancement
3. **Comprehensive Error Handling** (Path 6) - Critical for production reliability

**Additional Considerations:**
- Use Path 2 (Interface-Driven) for interface definition phase
- Apply Path 5 (Performance Analysis) to validate and adjust performance targets
- Incorporate Path 9 (Security) to expand security requirements

**Refined Requirements Summary:**
- **Requirements Defined:** 20 FRs, 15 NFRs, 8 CRs (43 total requirements)
- **Source Features:** 35 features defined in PRP document (`Prisma/Fixtures/PRP1/PRP.md`)
- **Interface Mapping:** 28 distinct interfaces mapped to the 35 features (per PRP document)
- **Decision:** Keep original requirements only - no additional requirements needed
- **Note:** The deep dive analysis identified potential gaps, but user confirmed core requirements are sufficient for MVP scope

**Clarification:** The PRP document defines 35 features mapped to 28 interfaces. This PRD translates those features into 43 specific requirements (20 functional, 15 non-functional, 8 compatibility). The requirements cover all 35 features from the PRP, with some features addressed by multiple requirements and some requirements addressing multiple features.

---

---

## Requirements Refinement Analysis

To help refine the requirements, here are potential areas for adjustment:

### Potential Refinement Areas

**1. Scope Clarity:**
- **FR1:** Browser automation - Is this required immediately, or can manual upload be supported first?
- **FR13:** SLA escalation - What type of alerts/notifications? (Email, dashboard, API?)
- **FR14:** Manual review interface - How detailed? Full UI or just API endpoints?

**2. Performance Targets:**
- **NFR3:** 5 seconds for browser automation - Is this realistic for all regulatory websites?
- **NFR4:** 30 seconds for PDF OCR - May be too optimistic for complex documents
- **NFR7:** 99.9% uptime - Is this required for all services or just SLA tracking?

**3. Feature Scope:**
- **FR16:** Digital PDF signing - Required for MVP or can be Phase 2?
- **FR18:** Excel layout generation - Is this separate from SIRO XML export (FR15)?
- **FR19:** PDF summarization - Is semantic analysis required, or can rule-based work?

**4. Compatibility:**
- **CR4:** Database schema - Are migrations acceptable, or must we avoid any schema changes?
- **CR7:** Runtime dependencies - Can we add new NuGet packages, or must we use only existing?

**5. Missing Clarifications:**
- Error handling: Are retries needed, or just log and queue for review?
- Authentication: Is user authentication required, or internal system only?
- Notifications: What mechanism for SLA alerts (FR13)?

---

**What would you like to refine?**

**A. Remove or defer features** - Identify which features can be Phase 2
**B. Adjust performance targets** - Make targets more realistic or remove specific ones
**C. Clarify ambiguous requirements** - Add more detail to vague requirements
**D. Simplify scope** - Reduce complexity in specific areas
**E. Specify what you want changed** - Tell me exactly what to adjust

**Select A-E or describe specific changes:**

---

## User Interface Enhancement Goals

This enhancement includes significant UI components for manual review, compliance validation, and reporting workflows.

### Integration with Existing UI

The new UI components will integrate seamlessly with the existing Blazor Server application, following established MudBlazor design patterns and component library usage (CR3). The existing UI structure provides a solid foundation for adding:

- **Manual Review Panels** - For ambiguous classifications and low-confidence extractions (FR14)
- **SLA Dashboard** - For tracking deadlines and viewing escalation alerts (FR12, FR13)
- **Compliance Validation Forms** - For reviewing and editing unified metadata records before export
- **Audit Trail Viewer** - For browsing audit logs and processing history (FR17)
- **Export Management** - For initiating and monitoring SIRO XML/PDF exports (FR15, FR16)

All new components will maintain visual consistency with existing UI, using MudBlazor's component library for forms, tables, dialogs, and navigation.

### Modified/New Screens and Views

**New Screens:**
1. **Manual Review Dashboard** - Lists cases requiring human review, filtered by confidence level, classification ambiguity, or error status
2. **SLA Monitoring Dashboard** - Displays active regulatory cases with deadline countdown, escalation status, and risk indicators
3. **Compliance Validation Screen** - Allows review and editing of unified metadata records before export, with field-level validation
4. **Audit Trail Viewer** - Browseable audit log with filtering by file ID, date range, action type, and user
5. **Export Management Screen** - Initiate exports, view export status, download generated files (SIRO XML, signed PDF, Excel layouts)

**Modified Screens:**
- **Document Processing Dashboard** - Enhanced to show classification results, field matching confidence scores, and processing stage status
- **File Upload/Management** - Extended to support browser automation download results and manual file uploads

### UI Consistency Requirements

- **Design System:** All new components must use MudBlazor components and follow existing color scheme, typography, and spacing
- **Navigation:** New screens integrated into existing navigation structure without disrupting current workflows
- **Responsive Design:** All new screens must be responsive and work on desktop and tablet devices
- **Accessibility:** Follow WCAG 2.1 AA standards for keyboard navigation and screen reader support
- **Loading States:** Consistent loading indicators and progress feedback for async operations
- **Error Display:** Use existing error message patterns and toast notifications for user feedback

---

## Technical Constraints and Integration Requirements

### Existing Technology Stack

**Languages:** C# (.NET 10), Python 3.9+

**Frameworks:** 
- ASP.NET Core (Blazor Server)
- Entity Framework Core
- MudBlazor (UI component library)
- CSnakes (Python-C# interop)

**Database:** SQL Server / PostgreSQL (to be determined based on existing infrastructure)

**Infrastructure:** 
- Local filesystem or Azure Blob Storage (CR8)
- Docker containers for deployment
- Kubernetes orchestration (optional, for microservices)

**External Dependencies:**
- Tesseract OCR engine
- Python modules: `prisma-ocr-pipeline`, `prisma-ai-extractors`, `prisma-document-generator`
- Playwright (for browser automation - new dependency)

### Integration Approach

**Database Integration Strategy:**
- Use Entity Framework Core migrations to add new tables without modifying existing schema (CR4)
- New tables: `FileMetadata`, `AuditRecords`, `SLAStatus`, `Expediente`, `Persona`, `Oficio`, `ComplianceActions`
- Maintain referential integrity between related entities
- Support both SQL Server and PostgreSQL via EF Core providers

**API Integration Strategy:**
- New interfaces defined in Domain layer following Hexagonal Architecture (CR6)
- Implementations in Infrastructure layer as adapters
- REST API endpoints for manual review UI components (Blazor Server can call directly, but API layer supports future separation)
- Maintain existing Python integration via CSnakes (CR2)

**Frontend Integration Strategy:**
- New Blazor Server components in existing UI project
- Use MudBlazor components for consistency (CR3)
- SignalR for real-time updates (SLA alerts, processing status)
- Maintain existing UI patterns and navigation structure

**Testing Integration Strategy:**
- Unit tests for all new interfaces using xUnit v3
- Integration tests for Python-C# interop via CSnakes
- End-to-end tests for complete processing pipeline
- UI tests using Playwright for Blazor components

### Code Organization and Standards

**File Structure Approach:**
- New interfaces in `Domain/Interfaces/` following existing naming conventions
- New entities in `Domain/Entities/` 
- New application services in `Application/Services/`
- New infrastructure adapters in `Infrastructure/` organized by concern (BrowserAutomation, Database, FileStorage, etc.)
- New UI components in `UI/ExxerCube.Prisma.Web.UI/` following existing component structure

**Naming Conventions:**
- Interfaces: `I{ServiceName}` (e.g., `IBrowserAutomationAgent`)
- Implementations: `{ServiceName}Adapter` or `{ServiceName}Service` (e.g., `PlaywrightBrowserAutomationAdapter`)
- Entities: PascalCase (e.g., `Expediente`, `Persona`)
- Methods: Async methods end with `Async`, use `Result<T>` return type (CR5)

**Coding Standards:**
- Railway-Oriented Programming: All interface methods return `Task<Result<T>>` (CR5)
- XML documentation required for all public APIs
- Treat warnings as errors (existing standard)
- Nullable reference types enabled
- Comprehensive logging with correlation IDs (NFR11)

**Documentation Standards:**
- XML documentation for all public interfaces and methods
- Architecture decisions documented in `docs/architecture/`
- API documentation updated in `docs/api/`
- User guides for manual review workflows

### Deployment and Operations

**Build Process Integration:**
- New projects follow existing `.csproj` structure and `Directory.Build.props` conventions
- Python modules remain separate, integrated via CSnakes
- Docker images built for each stage (if microservices architecture adopted)
- CI/CD pipeline extends existing workflow

**Deployment Strategy:**
- Can deploy as monolith initially (all stages in single application)
- Microservices architecture (NFR6) enables independent deployment per stage
- Database migrations run automatically via EF Core migrations
- Health checks for all new services

**Monitoring and Logging:**
- Structured logging with Serilog (existing pattern)
- Application Insights / OpenTelemetry for distributed tracing (NFR11)
- Correlation IDs for tracking requests across stages
- Metrics collection for SLA tracking, processing times, error rates

**Configuration Management:**
- Configuration via `appsettings.json` and environment variables
- Browser automation settings (timeouts, headless mode)
- SLA escalation thresholds configurable
- Matching policies configurable (NFR13)
- Digital signature certificate configuration

### Risk Assessment and Mitigation

**Technical Risks:**
- **Browser Automation Reliability:** Regulatory websites may change structure, breaking automation
  - *Mitigation:* Robust error handling, fallback to manual upload, monitoring and alerting
- **OCR Accuracy:** Complex documents may have low OCR accuracy, affecting downstream processing
  - *Mitigation:* Manual review workflow (FR14), confidence scoring, preprocessing improvements
- **Performance Bottlenecks:** OCR processing may exceed 30-second target for complex documents
  - *Mitigation:* Make timeout configurable, async processing, performance monitoring

**Integration Risks:**
- **Python-C# Interop:** CSnakes integration may have performance overhead or compatibility issues
  - *Mitigation:* Existing integration proven, maintain compatibility (CR2), performance testing
- **Database Schema Changes:** Migrations may impact existing data or require downtime
  - *Mitigation:* Additive-only schema changes (CR4), test migrations thoroughly, backup strategy

**Deployment Risks:**
- **Microservices Complexity:** Inter-stage communication adds complexity and potential failure points
  - *Mitigation:* Start with monolith, migrate to microservices incrementally, message queue for reliability
- **Digital Signature Dependencies:** External certificate management systems may be unavailable
  - *Mitigation:* Graceful degradation, queue exports for retry, manual signing workflow

**Mitigation Strategies:**
- Comprehensive error handling with `Result<T>` pattern (CR5)
- Manual review workflows for ambiguous cases (FR14)
- Audit logging for traceability (FR17)
- Health checks and monitoring for early problem detection
- Rollback strategy for database migrations
- Feature flags for gradual rollout of new capabilities

---

## Epic and Story Structure

### Epic Approach

**Epic Structure Decision:** Single comprehensive epic for this brownfield enhancement

**Rationale:**
This enhancement represents a cohesive transformation of the existing OCR pipeline into a complete regulatory compliance automation system. While it spans four processing stages, all stages work together to deliver a unified end-to-end capability. Breaking this into multiple epics would create artificial boundaries and complicate dependency management.

The single epic approach:
- Maintains clear focus on the overall goal: Regulatory Compliance Automation System
- Allows for incremental delivery by stage (Stage 1 → Stage 2 → Stage 3 → Stage 4)
- Enables value delivery at each stage completion
- Simplifies dependency tracking and story sequencing
- Aligns with the ITDD approach where all 28 interfaces form a cohesive system

**Epic Goal:** Transform ExxerCube.Prisma OCR pipeline into a comprehensive Regulatory Compliance Automation System that automates end-to-end processing of regulatory directives (oficios) from UIF/CNBV, from document acquisition through SIRO-compliant export generation.

**Integration Requirements:**
- Build upon existing OCR infrastructure without breaking current functionality
- Maintain Hexagonal Architecture and Railway-Oriented Programming patterns
- Integrate seamlessly with existing Python modules via CSnakes
- Preserve existing UI patterns while adding new compliance workflows

---

## Epic 1: Regulatory Compliance Automation System

**Epic Goal:** Transform ExxerCube.Prisma OCR pipeline into a comprehensive Regulatory Compliance Automation System that automates end-to-end processing of regulatory directives (oficios) from UIF/CNBV, from document acquisition through SIRO-compliant export generation.

**Integration Requirements:**
- All new interfaces extend or wrap existing functionality without breaking current OCR pipeline
- Maintain backward compatibility with existing `IFieldExtractor`, `IOcrExecutor`, `IImagePreprocessor` interfaces
- Preserve existing Python module integration via CSnakes
- Database schema changes are additive-only (new tables, no modifications to existing tables)

### Story 1.1: Browser Automation and Document Download (Stage 1 - Ingestion)

**As a** compliance officer,  
**I want** the system to automatically download regulatory documents from UIF/CNBV websites,  
**so that** I don't have to manually check and download oficios, reducing operational risk and ensuring timely processing.

**Acceptance Criteria:**
1. System launches browser session and navigates to configured regulatory website URL
2. System identifies downloadable files (PDF, XML, DOCX) matching configured patterns
3. System checks file checksum against download history to prevent duplicates
4. System downloads new files and saves them to configured storage directory with deterministic paths
5. System logs file metadata (name, URL, timestamp, checksum) to database
6. System closes browser session cleanly after download completion
7. System handles browser failures gracefully, logging errors and allowing retry

**Integration Verification:**
- **IV1:** Existing OCR pipeline continues to function when processing manually uploaded files (no regression)
- **IV2:** New `IBrowserAutomationAgent` interface integrates with existing `IDownloadStorage` and `IFileMetadataLogger` adapters
- **IV3:** Download performance does not impact existing OCR processing throughput

---

### Story 1.2: Enhanced Metadata Extraction and File Classification (Stage 2 - Extraction)

**As a** compliance analyst,  
**I want** the system to extract metadata from multiple document formats and classify them automatically,  
**so that** documents are properly categorized and ready for compliance processing.

**Acceptance Criteria:**
1. System identifies file type based on content (not just extension) for PDF, XML, DOCX files
2. System extracts metadata from XML documents (expediente number, oficio number, RFC, names, dates, legal references)
3. System extracts metadata from DOCX documents using structured field extraction
4. System detects scanned PDFs and applies image preprocessing before OCR
5. System extracts metadata from PDF documents with OCR fallback using existing OCR pipeline
6. System classifies documents into Level 1 categories (Aseguramiento, Desembargo, Documentacion, etc.) using deterministic rules
7. System classifies documents into Level 2/3 subcategories (Especial, Judicial, Hacendario) based on metadata
8. System generates safe, normalized file names and organizes files based on classification
9. System logs all classification decisions with confidence scores to audit trail

**Integration Verification:**
- **IV1:** Existing `IOcrExecutor` and `IImagePreprocessor` interfaces continue to work for OCR operations (no breaking changes)
- **IV2:** New `IMetadataExtractor` wraps existing OCR functionality, maintaining compatibility
- **IV3:** Classification performance (500ms target) does not degrade existing OCR processing speed

---

### Story 1.3: Field Matching and Unified Metadata Generation (Stage 2 - Extraction Continued)

**As a** compliance analyst,  
**I want** the system to match and consolidate field values across XML, DOCX, and PDF sources,  
**so that** I have a single, reliable metadata record with confidence scores for each field.

**Acceptance Criteria:**
1. System extracts structured fields from DOCX files using `IFieldExtractor<DocxSource>`
2. System extracts structured fields from PDF files (OCR'd) using `IFieldExtractor<PdfSource>`
3. System matches field values across XML, DOCX, and PDF sources, identifying conflicts and agreements
4. System generates unified metadata record consolidating best values from all sources
5. System calculates confidence scores for each field based on source agreement
6. System validates field completeness and consistency before proceeding to next stage
7. System logs field matching decisions and confidence scores to audit trail

**Integration Verification:**
- **IV1:** Existing `IFieldExtractor` interface extended to generic `IFieldExtractor<T>` without breaking existing implementations
- **IV2:** Field extraction performance maintains existing OCR pipeline throughput
- **IV3:** Unified metadata generation does not impact existing document processing workflows

---

### Story 1.4: Identity Resolution and Legal Directive Classification (Stage 3 - Decision Logic)

**As a** compliance officer,  
**I want** the system to resolve person identities and classify legal directives automatically,  
**so that** I can quickly understand what compliance actions are required for each regulatory case.

**Acceptance Criteria:**
1. System resolves person identities by handling RFC variants and alias names
2. System deduplicates person records across multiple documents for the same case
3. System classifies legal directives from document text, mapping clauses to compliance actions (block, unblock, document, transfer, information, ignore)
4. System detects references to legal instruments (e.g., Acuerdo 105/2021) in document text
5. System maps classified directives to specific compliance actions with confidence scores
6. System logs all identity resolution and classification decisions to audit trail

**Integration Verification:**
- **IV1:** Identity resolution does not modify existing person data structures or break existing OCR field extraction
- **IV2:** Legal classification uses extracted metadata from Stage 2 without requiring re-processing
- **IV3:** Classification performance (500ms target) maintains system responsiveness

---

### Story 1.5: SLA Tracking and Escalation Management (Stage 3 - Decision Logic Continued)

**As a** compliance manager,  
**I want** the system to track SLA deadlines and escalate impending breaches automatically,  
**so that** critical regulatory responses are never missed and I'm alerted in time to take action.

**Acceptance Criteria:**
1. System calculates SLA deadlines based on intake date and days plazo (business days)
2. System tracks remaining time for each regulatory response case
3. System identifies cases at risk when remaining time falls below critical threshold (default: 4 hours)
4. System escalates at-risk cases, triggering alerts and notifications
5. System provides SLA dashboard showing all active cases with deadline countdown and risk indicators
6. System logs all SLA calculations and escalations to audit trail
7. System supports configurable escalation thresholds per regulatory body or directive type

**Integration Verification:**
- **IV1:** SLA tracking does not impact existing document processing performance
- **IV2:** Escalation alerts integrate with existing notification mechanisms (if any) or use standard logging
- **IV3:** SLA calculations use business day logic that accounts for Mexican holidays (if applicable)

---

### Story 1.6: Manual Review Interface (Stage 3 - Decision Logic Continued)

**As a** compliance analyst,  
**I want** a manual review interface for ambiguous cases,  
**so that** I can review, correct, and approve low-confidence classifications or extractions before they proceed to export.

**Acceptance Criteria:**
1. System identifies cases requiring manual review (low confidence, ambiguous classification, extraction errors)
2. System provides manual review dashboard listing all review cases with filters (confidence level, classification ambiguity, error status)
3. System displays unified metadata record with field-level annotations showing source, confidence, and conflicts
4. System allows reviewer to override classifications, correct field values, and add notes
5. System submits review decisions and updates unified metadata record accordingly
6. System logs all manual review actions to audit trail with reviewer identity
7. System integrates seamlessly with existing Blazor Server UI using MudBlazor components

**Integration Verification:**
- **IV1:** Manual review interface does not disrupt existing document processing workflows
- **IV2:** Review decisions integrate with existing data models without breaking existing functionality
- **IV3:** UI components follow existing MudBlazor patterns and navigation structure

---

### Story 1.7: SIRO-Compliant Export Generation (Stage 4 - Final Compliance Response)

**As a** compliance officer,  
**I want** the system to generate SIRO-compliant XML exports from validated metadata,  
**so that** I can submit regulatory responses quickly and accurately without manual data entry.

**Acceptance Criteria:**
1. System maps unified metadata records to SIRO regulatory schema
2. System validates data against SIRO schema requirements before export
3. System generates SIRO-compliant XML files from validated metadata
4. System validates all required regulatory fields are present and valid
5. System generates Excel layouts from unified metadata for SIRO registration systems (FR18)
6. System logs all export operations to audit trail
7. System provides export management screen for initiating exports and downloading generated files

**Integration Verification:**
- **IV1:** Export generation does not modify source metadata or break existing data structures
- **IV2:** SIRO XML validation uses standard XML schema validation without impacting existing XML parsing
- **IV3:** Export performance does not block other document processing operations

---

### Story 1.8: PDF Summarization and Digital Signing (Stage 4 - Final Compliance Response Continued)

**As a** compliance officer,  
**I want** the system to summarize PDF content and generate digitally signed PDF exports,  
**so that** I can provide comprehensive compliance packages in regulatory-required formats.

**Acceptance Criteria:**
1. System summarizes PDF content into requirement categories (bloqueo, desbloqueo, documentacion, transferencia, informacion)
2. System uses semantic analysis or rule-based classification to categorize requirements
3. System generates digitally signed PDF exports from unified metadata records
4. System supports X.509 certificate-based signing (PAdES standard)
5. System integrates with external certificate management systems for signing certificates
6. System validates digital signatures before finalizing exports
7. System logs all PDF summarization and signing operations to audit trail

**Integration Verification:**
- **IV1:** PDF summarization uses existing OCR text extraction without breaking current OCR pipeline
- **IV2:** Digital signing operations do not impact existing PDF processing or export workflows
- **IV3:** Certificate integration handles certificate unavailability gracefully with error logging

---

### Story 1.9: Audit Trail and Reporting (Cross-Stage)

**As a** compliance manager,  
**I want** complete audit trail and reporting capabilities,  
**so that** I can track all processing steps, demonstrate compliance, and generate reports for regulatory audits.

**Acceptance Criteria:**
1. System maintains immutable audit log of all processing steps (downloads, classifications, extractions, reviews, exports)
2. System logs all actions with timestamp, user identity, file ID, and action details
3. System provides audit trail viewer with filtering (file ID, date range, action type, user)
4. System generates classification reports in CSV/JSON format for specified date ranges
5. System retains audit logs for minimum 7 years per regulatory requirements
6. System supports audit log export for compliance reporting
7. System provides correlation IDs for tracking requests across processing stages

**Integration Verification:**
- **IV1:** Audit logging does not impact processing performance (async logging, non-blocking)
- **IV2:** Audit trail viewer integrates with existing UI navigation and MudBlazor components
- **IV3:** Audit log retention policies do not conflict with existing data retention requirements

---

### Story Sequencing and Dependencies

**Critical Story Sequence:**
1. **Story 1.1** (Browser Automation) → Must complete first to provide source documents
2. **Story 1.2** (Metadata Extraction) → Depends on Story 1.1 for downloaded files
3. **Story 1.3** (Field Matching) → Depends on Story 1.2 for extracted metadata
4. **Story 1.4** (Identity Resolution) → Depends on Story 1.3 for unified metadata
5. **Story 1.5** (SLA Tracking) → Can run in parallel with Story 1.4, depends on Story 1.2 for intake dates
6. **Story 1.6** (Manual Review) → Depends on Stories 1.2-1.4 for review cases
7. **Story 1.7** (SIRO Export) → Depends on Stories 1.3-1.6 for validated metadata
8. **Story 1.8** (PDF Signing) → Depends on Story 1.7, can be implemented in parallel
9. **Story 1.9** (Audit Trail) → Cross-cutting, can be implemented incrementally alongside other stories

**Risk Mitigation:**
- Each story includes integration verification to ensure existing functionality remains intact
- Stories are sequenced to minimize risk to existing system
- Manual review workflow (Story 1.6) provides safety net for ambiguous cases
- Audit trail (Story 1.9) ensures traceability for troubleshooting

**Rollback Considerations:**
- Each story can be deployed independently with feature flags
- Database migrations are additive-only, enabling rollback without data loss
- New interfaces extend existing ones, allowing gradual migration
- Existing OCR pipeline remains functional if new features are disabled

---

