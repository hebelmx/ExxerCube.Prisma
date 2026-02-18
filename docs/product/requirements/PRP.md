# Product Requirements Plan (PRP)
## Regulatory Compliance Automation System
### Interface-Driven Development (ITDD) Design Document

**Version:** 1.0  
**Date:** 2025-01-XX  
**Status:** Draft  
**Approach:** Interface-Driven Development (ITDD)

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Interface Inventory by Stage](#interface-inventory-by-stage)
3. [Interface Contracts (ITDD Core)](#interface-contracts-itdd-core)
4. [Data Models & Entities](#data-models--entities)
5. [Sequential Processing Flow](#sequential-processing-flow)
6. [Cross-Stage Integration Points](#cross-stage-integration-points)
7. [Implementation Requirements](#implementation-requirements)
8. [Appendix: Existing Interfaces Analysis](#appendix-existing-interfaces-analysis)

---

## Executive Summary

### Problem Statement

Financial institutions are regularly served with official directives (referred to as *oficios*) from national regulatory bodies such as the Unidad de Inteligencia Financiera (UIF) and Comisión Nacional Bancaria y de Valores (CNBV). These directives often involve:

- Requests for blocking or unblocking specific individuals or entities
- Enforcement of legal instruments (e.g., Acuerdo 105/2021)
- Identification and suspension or reactivation of financial products, accounts, or services
- Urgent deadlines, typically requiring compliance within 1 business day
- Legal constraints prohibiting the notification of involved clients unless expressly allowed

Manually processing such directives introduces significant operational risk due to:
- Inconsistencies in document formats (PDF, DOC, XML)
- Ambiguous or missing identity information (e.g., RFC variants)
- Manual classification errors
- Tight compliance timeframes
- Limited traceability for audits

### Solution Overview

The system implements **35 features** mapped to **28 distinct interfaces**, organized into **four processing stages**.

**Stage 1: Ingestion & Acquisition (Etapa.1)**
- Automated browser agent downloads oficio files
- Duplicate tracking prevents reprocessing
- Files are persistently stored and logged

**Stage 2: Extraction & Classification (Etapa.2)**
- File types are validated (including OCR fallback for scanned PDFs)
- Metadata (e.g., RFC, name, oficio number) is extracted and normalized
- Files are classified using deterministic rules and ambiguity scoring
- Audit logging ensures traceability of each transformation

**Stage 3: Decision Logic & SLA Management (Etapa.3)**
- Identity resolution module merges aliases and variants
- Legal clause classifier interprets directives from legal text
- SLA tracker escalates impending deadline breaches
- Manual reviewer UI supports exception handling

**Stage 4: Final Compliance Response (Etapa.4)**
- A dedicated export module generates SIRO-compliant XML or digitally signed PDFs
- The solution maintains legal constraints (e.g., non-notification enforcement)
- A UI bundle supports validation, oversight, and reporting

### ITDD Approach

**Interface-Driven Development (ITDD)** is a design methodology where interfaces are defined first, establishing clear contracts between system components before implementation details are considered. This approach:

1. **Establishes Clear Contracts**: Each interface defines what capabilities are needed, not how they are implemented
2. **Enables Parallel Development**: Teams can work on different implementations simultaneously
3. **Facilitates Testing**: Mock implementations can be created immediately for integration testing
4. **Promotes Loose Coupling**: Components depend on abstractions, not concrete implementations
5. **Supports Hexagonal Architecture**: Interfaces act as ports, implementations as adapters

This PRP follows ITDD principles by:
- Defining all 28 interfaces with complete method signatures
- Organizing interfaces by processing stage
- Establishing data models that interfaces will operate on
- Documenting dependencies between interfaces
- Deriving implementation requirements from interface contracts

---

## Interface Inventory by Stage

### Feature-to-Interface Mapping

The system implements **35 features** mapped to **28 distinct interfaces**. Some interfaces handle multiple features.

**Complete Feature Mapping**:
- **Features 1-2**: `IBrowserAutomationAgent` (2 features, 1 interface)
- **Feature 3**: `IDownloadTracker` (1 feature, 1 interface)
- **Feature 4**: `IDownloadStorage` (1 feature, 1 interface)
- **Feature 5**: `IFileMetadataLogger` (1 feature, 1 interface)
- **Feature 6**: `IFileTypeIdentifier` (1 feature, 1 interface)
- **Features 7, 15**: `IMetadataExtractor` (2 features, 1 interface - metadata extraction and OCR fallback)
- **Feature 8**: `ISafeFileNamer` (1 feature, 1 interface)
- **Features 9-10**: `IFileClassifier` (2 features, 1 interface - Level 1 and Level 2/3)
- **Feature 11**: `IFileMover` (1 feature, 1 interface)
- **Feature 12**: `IRuleScorer` (1 feature, 1 interface)
- **Feature 13**: `IScanDetector` (1 feature, 1 interface)
- **Feature 14**: `IScanCleaner` (1 feature, 1 interface)
- **Features 16-17**: `IAuditLogger` (2 features, 1 interface - action logging and classification decisions)
- **Feature 18**: `IReportGenerator` (1 feature, 1 interface)
- **Feature 19**: `IXmlNullableParser<T>` (1 feature, 1 interface)
- **Feature 20**: `ISLAEnforcer` (1 feature, 1 interface)
- **Feature 21**: `IPersonIdentityResolver` (1 feature, 1 interface)
- **Feature 22**: `ILegalDirectiveClassifier` (1 feature, 1 interface)
- **Feature 23**: `IManualReviewerPanel` (1 feature, 1 interface)
- **Feature 24**: `IUIBundle` (1 feature, 1 interface)
- **Feature 25**: `IResponseExporter` (1 feature, 1 interface)
- **Features 26-27**: `IFieldExtractor<T>` (2 features, 1 interface - DocxFieldExtractor and PdfOcrFieldExtractor)
- **Features 28, 29, 33**: `IFieldMatcher<T>` (3 features, 1 interface - matching, unified record, validation)
- **Feature 30**: `ILayoutGenerator` (1 feature, 1 interface)
- **Feature 31**: `IFieldAgreement` (1 feature, 1 interface)
- **Feature 32**: `IMatchingPolicy` (1 feature, 1 interface)
- **Feature 34**: `IPdfRequirementSummarizer` (1 feature, 1 interface)
- **Feature 35**: `ICriterionMapper` (1 feature, 1 interface)

**Summary**: 35 features → 28 distinct interfaces

### Stage 1: Ingestion & Acquisition (Etapa.1)

**Purpose**: Automate the acquisition of regulatory documents from external sources.

| Interface | Purpose | Features | Feature IDs |
|-----------|---------|----------|-------------|
| `IBrowserAutomationAgent` | Browser session management and file detection | Launch browser, navigate, detect downloadable files | 1, 2 |
| `IDownloadTracker` | Duplicate detection and tracking | Track downloaded files, prevent redundant downloads | 3 |
| `IDownloadStorage` | File persistence | Store files with deterministic paths and names | 4 |
| `IFileMetadataLogger` | Metadata recording | Log file attributes (name, URL, timestamp, checksum) | 5 |

**Key Capabilities**:
- Feature 1: Launch browser agent to access specified page (`IBrowserAutomationAgent`)
- Feature 2: Identify and download new files from target source (`IBrowserAutomationAgent`)
- Feature 3: Detect and ignore previously downloaded files (`IDownloadTracker`)
- Feature 4: Save downloaded files to a configured directory (`IDownloadStorage`)
- Feature 5: Record file metadata (name, URL, timestamp, checksum) (`IFileMetadataLogger`)

### Stage 2: Extraction & Classification (Etapa.2)

**Purpose**: Extract metadata from documents, classify them, and organize for processing.

| Interface | Purpose | Features | Feature IDs |
|-----------|---------|----------|-------------|
| `IFileTypeIdentifier` | File type validation | Identify file type based on content (PDF, ZIP, DOCX) | 6 |
| `IMetadataExtractor` | Metadata extraction | Extract from XML, DOCX, PDF (with OCR fallback) | 7, 15 |
| `ISafeFileNamer` | File naming normalization | Generate clean, unique file names | 8 |
| `IFileClassifier` | Classification logic | Assign main category (Level 1) and subcategories (Level 2/3) | 9, 10 |
| `IFileMover` | File organization | Relocate files based on classification | 11 |
| `IRuleScorer` | Duplicate/ambiguity resolution | Resolve naming conflicts using rule-based decisions | 12 |
| `IScanDetector` | Scanned document detection | Identify non-searchable PDFs | 13 |
| `IScanCleaner` | Image preprocessing | Clean visual artifacts for OCR | 14 |
| `IAuditLogger` | Audit trail generation | Track all processing steps | 16, 17 |
| `IReportGenerator` | Summary export | Generate CSV, JSON summaries | 18 |
| `IXmlNullableParser<T>` | XML parsing | Convert XML to structured objects | 19 |
| `IFieldExtractor<T>` | Generic field extraction | Extract from DOCX, PDF (OCR'd) | 26, 27 |
| `IFieldMatcher<T>` | Cross-format field matching | Compare and match fields across formats | 28 |

**Key Capabilities**:
- Features 6-19: File type identification, metadata extraction, classification, OCR processing, audit logging
- Features 26-28: Structured field extraction from DOCX/PDF, cross-format matching

### Stage 3: Decision Logic & SLA Management (Etapa.3)

**Purpose**: Resolve identities, interpret legal directives, manage SLAs, and support human review.

| Interface | Purpose | Features | Feature IDs |
|-----------|---------|----------|-------------|
| `ISLAEnforcer` | Deadline tracking and escalation | Calculate deadlines, trigger escalation alerts | 20 |
| `IPersonIdentityResolver` | Identity deduplication | Resolve RFC variants and alias names | 21 |
| `ILegalDirectiveClassifier` | Legal clause interpretation | Map legal text to actions (block, unblock, ignore) | 22 |
| `IManualReviewerPanel` | Human review UI | Visual interface for ambiguous cases | 23 |
| `IUIBundle` | Frontend component library | Technology-agnostic UI components | 24 |
| `ILayoutGenerator` | Excel layout generation | Generate structured Excel files | 30 |
| `IFieldMatcher<T>` | Unified metadata generation | Consolidate best values into single record | 29 |
| `IFieldAgreement` | Match confidence reporting | Report field-level match confidence | 31 |
| `IMatchingPolicy` | Configurable matching rules | Define customizable matching policies | 32 |

**Key Capabilities**:
- Features 20-24: SLA tracking, identity resolution, legal classification, manual review, UI components
- Feature 29: Unified metadata generation (`IFieldMatcher<T>`)
- Feature 30: Excel layout creation (`ILayoutGenerator`)

### Stage 4: Final Compliance Response (Etapa.4)

**Purpose**: Generate final compliance packages in regulatory formats.

| Interface | Purpose | Features | Feature IDs |
|-----------|---------|----------|-------------|
| `IResponseExporter` | Regulatory format export | Generate SIRO XML or signed PDF | 25 |
| `IFieldMatcher<T>` | Field validation | Validate completeness and consistency | 33 |
| `IPdfRequirementSummarizer` | PDF content summarization | Classify into requirement categories | 34 |
| `ICriterionMapper` | Semantic label mapping | Map content to labeled categories | 35 |

**Key Capabilities**:
- Feature 25: Export final compliance package (`IResponseExporter`)
- Feature 33: Field validation (`IFieldMatcher<T>`)
- Feature 34: PDF summarization (`IPdfRequirementSummarizer`)
- Feature 35: Semantic mapping (`ICriterionMapper`)

---

## Interface Contracts (ITDD Core)

This section defines complete interface contracts with method signatures for all **28 distinct interfaces**, following Railway-Oriented Programming patterns using `Result<T>` for error handling. Each interface may handle multiple features (as noted in the Feature-to-Interface Mapping above).

### Stage 1 Interfaces

#### IBrowserAutomationAgent

**Purpose**: Manages browser automation for accessing regulatory websites and detecting downloadable files.

**Namespace**: `ExxerCube.Prisma.Domain.Interfaces`

**Dependencies**: None

**Method Signatures**:

```csharp
/// <summary>
/// Launches a browser session and navigates to the specified URL.
/// </summary>
/// <param name="url">The URL to navigate to.</param>
/// <param name="options">Browser automation options (headless mode, timeout, etc.).</param>
/// <returns>A result containing the browser session ID or an error.</returns>
Task<Result<string>> LaunchBrowserAsync(string url, BrowserOptions? options = null);

/// <summary>
/// Identifies downloadable files from the current page.
/// </summary>
/// <param name="sessionId">The browser session ID.</param>
/// <param name="patterns">File name patterns or selectors to match.</param>
/// <returns>A result containing a list of downloadable file references or an error.</returns>
Task<Result<List<DownloadableFile>>> IdentifyDownloadableFilesAsync(string sessionId, FilePattern[] patterns);

/// <summary>
/// Downloads a file from the browser session.
/// </summary>
/// <param name="sessionId">The browser session ID.</param>
/// <param name="fileReference">Reference to the file to download.</param>
/// <returns>A result containing the downloaded file data or an error.</returns>
Task<Result<DownloadedFile>> DownloadFileAsync(string sessionId, DownloadableFile fileReference);

/// <summary>
/// Closes the browser session.
/// </summary>
/// <param name="sessionId">The browser session ID.</param>
/// <returns>A result indicating success or failure (non-generic Result - IsSuccess property sufficient).</returns>
Task<Result> CloseBrowserAsync(string sessionId);
```

**Error Handling**:
- Network timeouts
- Invalid URLs
- Browser launch failures
- File detection failures

**Performance Requirements**:
- Browser launch: < 5 seconds
- File detection: < 2 seconds per page
- Download: Depends on file size, timeout after 5 minutes

---

#### IDownloadTracker

**Purpose**: Tracks downloaded files to prevent redundant downloads.

**Dependencies**: `IFileMetadataLogger`

**Method Signatures**:

```csharp
/// <summary>
/// Checks if a file has already been downloaded based on content identity.
/// </summary>
/// <param name="fileChecksum">The file checksum (SHA-256).</param>
/// <param name="fileName">The file name.</param>
/// <returns>A result indicating whether the file was previously downloaded.</returns>
Task<Result<bool>> IsFileAlreadyDownloadedAsync(string fileChecksum, string fileName);

/// <summary>
/// Records a file as downloaded.
/// </summary>
/// <param name="fileMetadata">The file metadata to record.</param>
/// <returns>A result indicating success or failure (non-generic Result - IsSuccess property sufficient).</returns>
Task<Result> RecordDownloadAsync(FileMetadata fileMetadata);

/// <summary>
/// Gets the list of previously downloaded files.
/// </summary>
/// <param name="since">Optional date filter to get files downloaded since this date.</param>
/// <returns>A result containing the list of downloaded file metadata or an error.</returns>
Task<Result<List<FileMetadata>>> GetDownloadedFilesAsync(DateTime? since = null);
```

**Error Handling**:
- Database connection failures
- Checksum calculation errors
- Duplicate detection failures

**Performance Requirements**:
- Duplicate check: < 100ms
- Record download: < 200ms

---

#### IDownloadStorage

**Purpose**: Persists downloaded files to storage with deterministic paths and names.

**Dependencies**: None

**Method Signatures**:

```csharp
/// <summary>
/// Saves a downloaded file to the configured storage directory.
/// </summary>
/// <param name="fileData">The file data to save.</param>
/// <param name="basePath">The base storage path.</param>
/// <param name="namingStrategy">The file naming strategy to use.</param>
/// <returns>A result containing the saved file path or an error.</returns>
Task<Result<string>> SaveFileAsync(DownloadedFile fileData, string basePath, FileNamingStrategy namingStrategy);

/// <summary>
/// Gets the file path for a given file identifier.
/// </summary>
/// <param name="fileId">The file identifier.</param>
/// <returns>A result containing the file path or an error.</returns>
Task<Result<string>> GetFilePathAsync(string fileId);

/// <summary>
/// Validates that a file exists in storage.
/// </summary>
/// <param name="filePath">The file path to validate.</param>
/// <returns>A result indicating whether the file exists.</returns>
Task<Result<bool>> FileExistsAsync(string filePath);
```

**Error Handling**:
- Disk space errors
- Permission errors
- Invalid file paths
- File system errors

**Performance Requirements**:
- Save file: Depends on file size, < 1 second per MB
- File existence check: < 50ms

---

#### IFileMetadataLogger

**Purpose**: Logs file metadata for audit and tracking purposes.

**Dependencies**: None

**Method Signatures**:

```csharp
/// <summary>
/// Logs file metadata including name, URL, timestamp, and checksum.
/// </summary>
/// <param name="metadata">The file metadata to log.</param>
/// <returns>A result indicating success or failure (non-generic Result - IsSuccess property sufficient).</returns>
Task<Result> LogMetadataAsync(FileMetadata metadata);

/// <summary>
/// Gets file metadata by file identifier.
/// </summary>
/// <param name="fileId">The file identifier.</param>
/// <returns>A result containing the file metadata or an error.</returns>
Task<Result<FileMetadata>> GetMetadataAsync(string fileId);

/// <summary>
/// Gets all metadata for files processed within a time range.
/// </summary>
/// <param name="startDate">The start date.</param>
/// <param name="endDate">The end date.</param>
/// <returns>A result containing the list of file metadata or an error.</returns>
Task<Result<List<FileMetadata>>> GetMetadataByDateRangeAsync(DateTime startDate, DateTime endDate);
```

**Error Handling**:
- Logging service failures
- Invalid metadata
- Query failures

**Performance Requirements**:
- Log metadata: < 100ms
- Query metadata: < 500ms

---

### Stage 2 Interfaces

#### IFileTypeIdentifier

**Purpose**: Identifies file types based on content, not just file extensions.

**Dependencies**: `IFileLoader`

**Method Signatures**:

```csharp
/// <summary>
/// Identifies the file type based on content analysis.
/// </summary>
/// <param name="filePath">The path to the file.</param>
/// <returns>A result containing the file type information or an error.</returns>
Task<Result<FileTypeInfo>> IdentifyFileTypeAsync(string filePath);

/// <summary>
/// Validates that a file matches the expected format.
/// </summary>
/// <param name="filePath">The path to the file.</param>
/// <param name="expectedFormats">The expected file formats (PDF, ZIP, DOCX).</param>
/// <returns>A result indicating whether the file matches expected formats.</returns>
Task<Result<bool>> ValidateFileFormatAsync(string filePath, FileFormat[] expectedFormats);

/// <summary>
/// Gets supported file formats.
/// </summary>
/// <returns>An array of supported file formats.</returns>
FileFormat[] GetSupportedFormats();
```

**Error Handling**:
- File read errors
- Corrupted files
- Unsupported formats

**Performance Requirements**:
- File type identification: < 500ms
- Format validation: < 300ms

---

#### IMetadataExtractor

**Purpose**: Extracts metadata from various document formats (XML, DOCX, PDF with OCR fallback).

**Dependencies**: `IFileTypeIdentifier`, `IScanDetector`, `IOcrExecutor` (for OCR fallback)

**Method Signatures**:

```csharp
/// <summary>
/// Extracts metadata from a document file.
/// </summary>
/// <param name="filePath">The path to the document file.</param>
/// <param name="extractionOptions">Options for extraction (OCR fallback, etc.).</param>
/// <returns>A result containing the extracted metadata or an error.</returns>
Task<Result<ExtractedMetadata>> ExtractMetadataAsync(string filePath, ExtractionOptions? extractionOptions = null);

/// <summary>
/// Extracts metadata from XML content.
/// </summary>
/// <param name="xmlContent">The XML content as string.</param>
/// <returns>A result containing the extracted metadata or an error.</returns>
Task<Result<ExtractedMetadata>> ExtractFromXmlAsync(string xmlContent);

/// <summary>
/// Extracts metadata from DOCX file.
/// </summary>
/// <param name="filePath">The path to the DOCX file.</param>
/// <returns>A result containing the extracted metadata or an error.</returns>
Task<Result<ExtractedMetadata>> ExtractFromDocxAsync(string filePath);

/// <summary>
/// Extracts metadata from PDF file (with OCR fallback if needed).
/// </summary>
/// <param name="filePath">The path to the PDF file.</param>
/// <param name="useOcrFallback">Whether to use OCR if text extraction fails.</param>
/// <returns>A result containing the extracted metadata or an error.</returns>
Task<Result<ExtractedMetadata>> ExtractFromPdfAsync(string filePath, bool useOcrFallback = true);
```

**Error Handling**:
- File format errors
- OCR failures
- Parsing errors
- Missing required fields

**Performance Requirements**:
- XML extraction: < 200ms
- DOCX extraction: < 1 second
- PDF extraction: < 2 seconds (without OCR), < 30 seconds (with OCR)

---

#### ISafeFileNamer

**Purpose**: Generates clean, consistent, and unique file names based on extracted data.

**Dependencies**: None

**Method Signatures**:

```csharp
/// <summary>
/// Generates a safe file name based on extracted metadata.
/// </summary>
/// <param name="metadata">The extracted metadata.</param>
/// <param name="originalFileName">The original file name.</param>
/// <returns>A result containing the generated safe file name or an error.</returns>
Task<Result<string>> GenerateSafeFileNameAsync(ExtractedMetadata metadata, string originalFileName);

/// <summary>
/// Ensures file name uniqueness by appending hash and timestamp if needed.
/// </summary>
/// <param name="baseFileName">The base file name.</param>
/// <param name="directoryPath">The target directory path.</param>
/// <returns>A result containing a unique file name or an error.</returns>
Task<Result<string>> EnsureUniqueFileNameAsync(string baseFileName, string directoryPath);

/// <summary>
/// Normalizes a file name to remove invalid characters.
/// </summary>
/// <param name="fileName">The file name to normalize.</param>
/// <returns>The normalized file name.</returns>
string NormalizeFileName(string fileName);
```

**Error Handling**:
- Invalid metadata
- File system errors
- Name generation failures

**Performance Requirements**:
- Name generation: < 50ms
- Uniqueness check: < 100ms

---

#### IFileClassifier

**Purpose**: Classifies files using deterministic rules and decision trees.

**Dependencies**: `IMetadataExtractor`

**Method Signatures**:

```csharp
/// <summary>
/// Classifies a file and assigns main category (Level 1).
/// </summary>
/// <param name="metadata">The extracted metadata.</param>
/// <returns>A result containing the Level 1 classification or an error.</returns>
Task<Result<ClassificationLevel1>> ClassifyLevel1Async(ExtractedMetadata metadata);

/// <summary>
/// Classifies a file and assigns subcategory (Level 2 or 3).
/// </summary>
/// <param name="metadata">The extracted metadata.</param>
/// <param name="level1Classification">The Level 1 classification.</param>
/// <returns>A result containing the Level 2/3 classification or an error.</returns>
Task<Result<ClassificationLevel2>> ClassifyLevel2Async(ExtractedMetadata metadata, ClassificationLevel1 level1Classification);

/// <summary>
/// Gets the classification confidence score.
/// </summary>
/// <param name="metadata">The extracted metadata.</param>
/// <param name="classification">The classification result.</param>
/// <returns>A result containing the confidence score (0-100) or an error.</returns>
Task<Result<int>> GetClassificationConfidenceAsync(ExtractedMetadata metadata, ClassificationResult classification);
```

**Error Handling**:
- Ambiguous classifications
- Missing required fields
- Classification rule errors

**Performance Requirements**:
- Level 1 classification: < 200ms
- Level 2 classification: < 300ms
- Confidence calculation: < 100ms

---

#### IFileMover

**Purpose**: Relocates files based on classification results.

**Dependencies**: `IFileClassifier`

**Method Signatures**:

```csharp
/// <summary>
/// Moves a file to a folder path based on classification.
/// </summary>
/// <param name="sourcePath">The source file path.</param>
/// <param name="classification">The classification result.</param>
/// <param name="baseDirectory">The base directory for organized storage.</param>
/// <returns>A result containing the destination path or an error.</returns>
Task<Result<string>> MoveFileAsync(string sourcePath, ClassificationResult classification, string baseDirectory);

/// <summary>
/// Gets the target folder path for a given classification.
/// </summary>
/// <param name="classification">The classification result.</param>
/// <param name="baseDirectory">The base directory.</param>
/// <returns>The target folder path.</returns>
string GetTargetFolderPath(ClassificationResult classification, string baseDirectory);
```

**Error Handling**:
- File move errors
- Permission errors
- Disk space errors

**Performance Requirements**:
- File move: < 500ms (depends on file size)

---

#### IRuleScorer

**Purpose**: Resolves duplicate or ambiguous files using rule-based decisions.

**Dependencies**: `IFileMetadataLogger`, `IFileClassifier`

**Method Signatures**:

```csharp
/// <summary>
/// Scores files based on rules to resolve naming conflicts.
/// </summary>
/// <param name="files">The list of files to score.</param>
/// <param name="rules">The scoring rules to apply.</param>
/// <returns>A result containing scored files or an error.</returns>
Task<Result<List<ScoredFile>>> ScoreFilesAsync(List<FileMetadata> files, ScoringRule[] rules);

/// <summary>
/// Resolves duplicate files by selecting the best candidate.
/// </summary>
/// <param name="duplicateFiles">The list of duplicate files.</param>
/// <returns>A result containing the selected file or an error.</returns>
Task<Result<FileMetadata>> ResolveDuplicatesAsync(List<FileMetadata> duplicateFiles);
```

**Error Handling**:
- Ambiguous resolutions
- Rule evaluation errors
- Scoring failures

**Performance Requirements**:
- File scoring: < 500ms per file
- Duplicate resolution: < 1 second

---

#### IScanDetector

**Purpose**: Identifies scanned (non-searchable) PDF documents.

**Dependencies**: `IFileTypeIdentifier`

**Method Signatures**:

```csharp
/// <summary>
/// Detects if a PDF is scanned (image-based) by checking for textual data layer.
/// </summary>
/// <param name="filePath">The path to the PDF file.</param>
/// <returns>A result indicating whether the PDF is scanned or an error.</returns>
Task<Result<bool>> IsScannedPdfAsync(string filePath);

/// <summary>
/// Gets the scan quality score (0-100).
/// </summary>
/// <param name="filePath">The path to the PDF file.</param>
/// <returns>A result containing the scan quality score or an error.</returns>
Task<Result<int>> GetScanQualityScoreAsync(string filePath);
```

**Error Handling**:
- File read errors
- PDF parsing errors

**Performance Requirements**:
- Scan detection: < 1 second

---

#### IScanCleaner

**Purpose**: Cleans visual artifacts from scanned images to improve OCR accuracy.

**Dependencies**: `IImagePreprocessor`

**Method Signatures**:

```csharp
/// <summary>
/// Cleans visual artifacts from a scanned image.
/// </summary>
/// <param name="imageData">The image data to clean.</param>
/// <param name="cleaningOptions">Options for cleaning (deskew, binarize, etc.).</param>
/// <returns>A result containing the cleaned image or an error.</returns>
Task<Result<ImageData>> CleanScanAsync(ImageData imageData, CleaningOptions? cleaningOptions = null);
```

**Error Handling**:
- Image processing errors
- Invalid image format

**Performance Requirements**:
- Image cleaning: < 5 seconds per page

---

#### IAuditLogger

**Purpose**: Tracks all processing steps in a secure and structured log.

**Dependencies**: None

**Method Signatures**:

```csharp
/// <summary>
/// Logs an action taken on a file.
/// </summary>
/// <param name="action">The action performed (download, classification, move, etc.).</param>
/// <param name="fileId">The file identifier.</param>
/// <param name="details">Additional action details.</param>
/// <returns>A result indicating success or failure (non-generic Result - IsSuccess property sufficient).</returns>
Task<Result> LogActionAsync(AuditAction action, string fileId, Dictionary<string, object>? details = null);

/// <summary>
/// Generates an audit record with classification decisions and scores.
/// </summary>
/// <param name="fileId">The file identifier.</param>
/// <param name="classification">The classification result.</param>
/// <param name="scores">The classification scores.</param>
/// <returns>A result indicating success or failure (non-generic Result - IsSuccess property sufficient).</returns>
Task<Result> LogClassificationDecisionAsync(string fileId, ClassificationResult classification, ClassificationScores scores);

/// <summary>
/// Gets audit records for a file.
/// </summary>
/// <param name="fileId">The file identifier.</param>
/// <returns>A result containing the audit records or an error.</returns>
Task<Result<List<AuditRecord>>> GetAuditRecordsAsync(string fileId);
```

**Error Handling**:
- Logging service failures
- Invalid audit data

**Performance Requirements**:
- Log action: < 100ms
- Query audit records: < 500ms

---

#### IReportGenerator

**Purpose**: Produces output summaries suitable for review and reporting.

**Dependencies**: `IAuditLogger`, `IFileClassifier`

**Method Signatures**:

```csharp
/// <summary>
/// Exports a summary of the current classification state.
/// </summary>
/// <param name="format">The export format (CSV, JSON).</param>
/// <param name="filters">Optional filters for the summary.</param>
/// <returns>A result containing the export file path or an error.</returns>
Task<Result<string>> ExportSummaryAsync(ExportFormat format, SummaryFilters? filters = null);

/// <summary>
/// Generates a classification report.
/// </summary>
/// <param name="startDate">The start date for the report.</param>
/// <param name="endDate">The end date for the report.</param>
/// <param name="format">The export format.</param>
/// <returns>A result containing the report file path or an error.</returns>
Task<Result<string>> GenerateClassificationReportAsync(DateTime startDate, DateTime endDate, ExportFormat format);
```

**Error Handling**:
- Export format errors
- Data retrieval errors

**Performance Requirements**:
- Summary export: < 2 seconds
- Report generation: < 5 seconds

---

#### IXmlNullableParser<T>

**Purpose**: Converts semi-structured XML into flexible data objects.

**Dependencies**: None

**Method Signatures**:

```csharp
/// <summary>
/// Parses XML content into a structured object of type T.
/// </summary>
/// <param name="xmlContent">The XML content as string.</param>
/// <param name="options">Parsing options (namespace handling, etc.).</param>
/// <returns>A result containing the parsed object or an error.</returns>
Task<Result<T>> ParseAsync(string xmlContent, XmlParseOptions? options = null);

/// <summary>
/// Parses XML from a file.
/// </summary>
/// <param name="filePath">The path to the XML file.</param>
/// <param name="options">Parsing options.</param>
/// <returns>A result containing the parsed object or an error.</returns>
Task<Result<T>> ParseFromFileAsync(string filePath, XmlParseOptions? options = null);

/// <summary>
/// Validates XML against a schema (optional).
/// </summary>
/// <param name="xmlContent">The XML content.</param>
/// <param name="schemaPath">The path to the XML schema.</param>
/// <returns>A result indicating validation success or failure.</returns>
Task<Result<bool>> ValidateSchemaAsync(string xmlContent, string? schemaPath = null);
```

**Error Handling**:
- XML parsing errors
- Schema validation errors
- Missing required elements

**Performance Requirements**:
- XML parsing: < 500ms
- Schema validation: < 1 second

---

#### IFieldExtractor<T>

**Purpose**: Generic interface for extracting structured fields from documents.

**Dependencies**: `IMetadataExtractor`, `IOcrExecutor` (for PDF OCR)

**Method Signatures**:

```csharp
/// <summary>
/// Extracts structured fields from a document.
/// </summary>
/// <param name="source">The document source (file path or content).</param>
/// <param name="fieldDefinitions">The field definitions to extract.</param>
/// <returns>A result containing the extracted fields or an error.</returns>
Task<Result<ExtractedFields>> ExtractFieldsAsync(T source, FieldDefinition[] fieldDefinitions);

/// <summary>
/// Extracts a specific field by name.
/// </summary>
/// <param name="source">The document source.</param>
/// <param name="fieldName">The field name to extract.</param>
/// <returns>A result containing the field value or an error.</returns>
Task<Result<FieldValue>> ExtractFieldAsync(T source, string fieldName);
```

**Implementations**:
- `DocxFieldExtractor` : Extracts from DOCX files
- `PdfOcrFieldExtractor` : Extracts from OCR'd PDF documents

**Error Handling**:
- Field extraction errors
- Missing fields
- Format errors

**Performance Requirements**:
- Field extraction: < 2 seconds per document

---

#### IFieldMatcher<T>

**Purpose**: Compares and matches field values across different document formats.

**Dependencies**: `IFieldExtractor<T>`, `IMatchingPolicy`

**Method Signatures**:

```csharp
/// <summary>
/// Matches field values across XML, DOCX, and PDF sources.
/// </summary>
/// <param name="sources">The list of document sources to match.</param>
/// <param name="fieldDefinitions">The field definitions to match.</param>
/// <returns>A result containing matched fields with confidence scores or an error.</returns>
Task<Result<MatchedFields>> MatchFieldsAsync(List<T> sources, FieldDefinition[] fieldDefinitions);

/// <summary>
/// Generates a unified metadata record from matched fields.
/// </summary>
/// <param name="matchedFields">The matched fields result.</param>
/// <returns>A result containing the unified metadata record or an error.</returns>
Task<Result<UnifiedMetadataRecord>> GenerateUnifiedRecordAsync(MatchedFields matchedFields);

/// <summary>
/// Validates completeness and consistency of final match result.
/// </summary>
/// <param name="matchedFields">The matched fields to validate.</param>
/// <param name="requiredFields">The list of required field names.</param>
/// <returns>A result containing validation result or an error.</returns>
Task<Result<ValidationResult>> ValidateMatchResultAsync(MatchedFields matchedFields, string[] requiredFields);
```

**Error Handling**:
- Field matching errors
- Inconsistent matches
- Missing required fields

**Performance Requirements**:
- Field matching: < 1 second per comparison
- Unified record generation: < 500ms

---

### Stage 3 Interfaces

#### ISLAEnforcer

**Purpose**: Tracks remaining time for regulatory responses and escalates on breach risk.

**Dependencies**: `IFileMetadataLogger`

**Method Signatures**:

```csharp
/// <summary>
/// Calculates the deadline based on date of intake and current timestamp.
/// </summary>
/// <param name="intakeDate">The date the oficio was received.</param>
/// <param name="daysPlazo">The number of business days granted.</param>
/// <returns>A result containing the calculated deadline or an error.</returns>
Task<Result<DateTime>> CalculateDeadlineAsync(DateTime intakeDate, int daysPlazo);

/// <summary>
/// Checks if a deadline is within critical threshold and triggers escalation if needed.
/// </summary>
/// <param name="fileId">The file identifier.</param>
/// <param name="deadline">The deadline to check.</param>
/// <param name="criticalThresholdHours">The critical threshold in hours (default: 4).</param>
/// <returns>A result containing escalation status or an error.</returns>
Task<Result<EscalationStatus>> CheckAndEscalateAsync(string fileId, DateTime deadline, int criticalThresholdHours = 4);

/// <summary>
/// Gets the SLA status for a file.
/// </summary>
/// <param name="fileId">The file identifier.</param>
/// <returns>A result containing the SLA status or an error.</returns>
Task<Result<SLAStatus>> GetSlaStatusAsync(string fileId);

/// <summary>
/// Gets all files with impending deadline breaches.
/// </summary>
/// <param name="thresholdHours">The threshold in hours.</param>
/// <returns>A result containing the list of files at risk or an error.</returns>
Task<Result<List<FileSlaStatus>>> GetFilesAtRiskAsync(int thresholdHours);
```

**Error Handling**:
- Date calculation errors
- Escalation service failures

**Performance Requirements**:
- Deadline calculation: < 50ms
- Escalation check: < 200ms

---

#### IPersonIdentityResolver

**Purpose**: Resolves person identity across RFC variants and alias names.

**Dependencies**: `IFieldMatcher<T>`

**Method Signatures**:

```csharp
/// <summary>
/// Resolves person identity using RFC, full name, and optional metadata.
/// </summary>
/// <param name="personData">The person data to resolve.</param>
/// <param name="metadata">Optional additional metadata.</param>
/// <returns>A result containing the resolved identity or an error.</returns>
Task<Result<ResolvedIdentity>> ResolveIdentityAsync(PersonData personData, Dictionary<string, object>? metadata = null);

/// <summary>
/// Deduplicates and consolidates entity records.
/// </summary>
/// <param name="personRecords">The list of person records to deduplicate.</param>
/// <returns>A result containing the consolidated records or an error.</returns>
Task<Result<List<ConsolidatedPersonRecord>>> DeduplicateRecordsAsync(List<PersonData> personRecords);

/// <summary>
/// Finds all variants of a person identity.
/// </summary>
/// <param name="rfc">The RFC to search for.</param>
/// <returns>A result containing all identity variants or an error.</returns>
Task<Result<List<PersonIdentityVariant>>> FindIdentityVariantsAsync(string rfc);
```

**Error Handling**:
- Identity resolution failures
- Ambiguous matches
- Database errors

**Performance Requirements**:
- Identity resolution: < 500ms
- Deduplication: < 2 seconds per 100 records

---

#### ILegalDirectiveClassifier

**Purpose**: Interprets legal clauses and maps them to actionable directives.

**Dependencies**: `IMetadataExtractor`

**Method Signatures**:

```csharp
/// <summary>
/// Classifies legal directives from legal text.
/// </summary>
/// <param name="legalText">The legal text to analyze.</param>
/// <param name="metadata">Optional document metadata.</param>
/// <returns>A result containing the directive classification or an error.</returns>
Task<Result<LegalDirective>> ClassifyDirectiveAsync(string legalText, ExtractedMetadata? metadata = null);

/// <summary>
/// Detects references to legal instruments (e.g., Acuerdo 105/2021).
/// </summary>
/// <param name="legalText">The legal text to analyze.</param>
/// <returns>A result containing detected legal instruments or an error.</returns>
Task<Result<List<LegalInstrument>>> DetectLegalInstrumentsAsync(string legalText);

/// <summary>
/// Maps a directive to a compliance action (block, unblock, ignore).
/// </summary>
/// <param name="directive">The legal directive.</param>
/// <returns>A result containing the compliance action or an error.</returns>
Task<Result<ComplianceAction>> MapToComplianceActionAsync(LegalDirective directive);
```

**Error Handling**:
- Text parsing errors
- Ambiguous directives
- Unknown legal instruments

**Performance Requirements**:
- Directive classification: < 2 seconds
- Legal instrument detection: < 1 second

---

#### IManualReviewerPanel

**Purpose**: Provides visual interface for human review of ambiguous classifications or extractions.

**Dependencies**: `IUIBundle`, `IFieldMatcher<T>`

**Method Signatures**:

```csharp
/// <summary>
/// Gets cases requiring manual review.
/// </summary>
/// <param name="filters">Optional filters for review cases.</param>
/// <returns>A result containing the list of review cases or an error.</returns>
Task<Result<List<ReviewCase>>> GetReviewCasesAsync(ReviewFilters? filters = null);

/// <summary>
/// Submits a manual review decision.
/// </summary>
/// <param name="caseId">The review case identifier.</param>
/// <param name="decision">The review decision with overrides.</param>
/// <returns>A result indicating success or failure (non-generic Result - IsSuccess property sufficient).</returns>
Task<Result> SubmitReviewDecisionAsync(string caseId, ReviewDecision decision);

/// <summary>
/// Gets field-level annotations for a review case.
/// </summary>
/// <param name="caseId">The review case identifier.</param>
/// <returns>A result containing field annotations or an error.</returns>
Task<Result<FieldAnnotations>> GetFieldAnnotationsAsync(string caseId);
```

**Error Handling**:
- Review case retrieval errors
- Invalid review decisions
- Submission failures

**Performance Requirements**:
- Get review cases: < 500ms
- Submit decision: < 200ms

---

#### IUIBundle

**Purpose**: Technology-agnostic frontend UI library for validation, editing, and submission.

**Dependencies**: None (UI layer)

**Method Signatures**:

```csharp
/// <summary>
/// Renders a validation form component.
/// </summary>
/// <param name="data">The data to validate.</param>
/// <param name="validationRules">The validation rules to apply.</param>
/// <returns>A result containing the rendered component or an error.</returns>
Task<Result<UIComponent>> RenderValidationFormAsync(UnifiedMetadataRecord data, ValidationRule[] validationRules);

/// <summary>
/// Renders an editing form component.
/// </summary>
/// <param name="data">The data to edit.</param>
/// <param name="editableFields">The list of editable field names.</param>
/// <returns>A result containing the rendered component or an error.</returns>
Task<Result<UIComponent>> RenderEditingFormAsync(UnifiedMetadataRecord data, string[] editableFields);

/// <summary>
/// Renders a submission confirmation workflow.
/// </summary>
/// <param name="data">The data to submit.</param>
/// <returns>A result containing the rendered workflow or an error.</returns>
Task<Result<UIComponent>> RenderSubmissionWorkflowAsync(UnifiedMetadataRecord data);
```

**Note**: This interface is technology-agnostic and may have different implementations for Blazor, React, Vue, etc.

**Error Handling**:
- Component rendering errors
- Validation errors

**Performance Requirements**:
- Component rendering: < 500ms

---

#### ILayoutGenerator

**Purpose**: Generates Excel layouts from unified metadata for structured data delivery.

**Dependencies**: `IFieldMatcher<T>`

**Method Signatures**:

```csharp
/// <summary>
/// Generates an Excel layout from unified metadata.
/// </summary>
/// <param name="metadata">The unified metadata record.</param>
/// <param name="layoutSchema">The Excel layout schema definition.</param>
/// <param name="outputPath">The output file path.</param>
/// <returns>A result containing the generated file path or an error.</returns>
Task<Result<string>> GenerateExcelLayoutAsync(UnifiedMetadataRecord metadata, ExcelLayoutSchema layoutSchema, string outputPath);

/// <summary>
/// Generates Excel layout for multiple records.
/// </summary>
/// <param name="metadataList">The list of unified metadata records.</param>
/// <param name="layoutSchema">The Excel layout schema definition.</param>
/// <param name="outputPath">The output file path.</param>
/// <returns>A result containing the generated file path or an error.</returns>
Task<Result<string>> GenerateExcelLayoutBatchAsync(List<UnifiedMetadataRecord> metadataList, ExcelLayoutSchema layoutSchema, string outputPath);

/// <summary>
/// Validates the layout schema against SIRO requirements.
/// </summary>
/// <param name="layoutSchema">The layout schema to validate.</param>
/// <returns>A result indicating validation success or failure.</returns>
Task<Result<bool>> ValidateLayoutSchemaAsync(ExcelLayoutSchema layoutSchema);
```

**Error Handling**:
- Excel generation errors
- Schema validation errors
- File write errors

**Performance Requirements**:
- Excel generation: < 2 seconds per record
- Batch generation: < 5 seconds per 100 records

---

#### IFieldAgreement

**Purpose**: Reports field-level match confidence and origin trace.

**Dependencies**: `IFieldMatcher<T>`

**Method Signatures**:

```csharp
/// <summary>
/// Annotates field origins and agreement levels.
/// </summary>
/// <param name="matchedFields">The matched fields result.</param>
/// <returns>A result containing field agreement annotations or an error.</returns>
Task<Result<FieldAgreementAnnotations>> AnnotateFieldAgreementAsync(MatchedFields matchedFields);

/// <summary>
/// Gets the confidence score for a specific field.
/// </summary>
/// <param name="fieldName">The field name.</param>
/// <param name="matchedFields">The matched fields result.</param>
/// <returns>A result containing the confidence score (0-100) or an error.</returns>
Task<Result<int>> GetFieldConfidenceAsync(string fieldName, MatchedFields matchedFields);

/// <summary>
/// Gets the origin trace for a field value.
/// </summary>
/// <param name="fieldName">The field name.</param>
/// <param name="matchedFields">The matched fields result.</param>
/// <returns>A result containing the origin trace or an error.</returns>
Task<Result<FieldOriginTrace>> GetFieldOriginTraceAsync(string fieldName, MatchedFields matchedFields);
```

**Error Handling**:
- Field annotation errors
- Missing field data

**Performance Requirements**:
- Field annotation: < 200ms
- Confidence calculation: < 100ms

---

#### IMatchingPolicy

**Purpose**: Defines configurable decision rules for field matching.

**Dependencies**: None

**Method Signatures**:

```csharp
/// <summary>
/// Applies matching policy rules to control matching behavior.
/// </summary>
/// <param name="fieldValues">The field values to match.</param>
/// <param name="policy">The matching policy to apply.</param>
/// <returns>A result containing the matching decision or an error.</returns>
Task<Result<MatchingDecision>> ApplyMatchingPolicyAsync(Dictionary<string, object> fieldValues, MatchingPolicy policy);

/// <summary>
/// Loads a matching policy from configuration.
/// </summary>
/// <param name="policyName">The policy name.</param>
/// <returns>A result containing the matching policy or an error.</returns>
Task<Result<MatchingPolicy>> LoadPolicyAsync(string policyName);

/// <summary>
/// Validates a matching policy configuration.
/// </summary>
/// <param name="policy">The policy to validate.</param>
/// <returns>A result indicating validation success or failure.</returns>
Task<Result<bool>> ValidatePolicyAsync(MatchingPolicy policy);
```

**Error Handling**:
- Policy loading errors
- Invalid policy configuration
- Policy application errors

**Performance Requirements**:
- Policy application: < 100ms
- Policy loading: < 200ms

---

### Stage 4 Interfaces

#### IResponseExporter

**Purpose**: Exports final compliance packages in regulator-specific formats (SIRO XML, signed PDF).

**Dependencies**: `ILayoutGenerator`, `IFieldMatcher<T>`

**Method Signatures**:

```csharp
/// <summary>
/// Exports a compliance package in SIRO-compliant XML format.
/// </summary>
/// <param name="metadata">The unified metadata record.</param>
/// <param name="outputPath">The output file path.</param>
/// <returns>A result containing the exported file path or an error.</returns>
Task<Result<string>> ExportSiroXmlAsync(UnifiedMetadataRecord metadata, string outputPath);

/// <summary>
/// Exports a compliance package as a digitally signed PDF.
/// </summary>
/// <param name="metadata">The unified metadata record.</param>
/// <param name="signingCertificate">The digital signing certificate.</param>
/// <param name="outputPath">The output file path.</param>
/// <returns>A result containing the exported file path or an error.</returns>
Task<Result<string>> ExportSignedPdfAsync(UnifiedMetadataRecord metadata, SigningCertificate signingCertificate, string outputPath);

/// <summary>
/// Maps validated data to regulatory schema.
/// </summary>
/// <param name="metadata">The unified metadata record.</param>
/// <param name="schema">The regulatory schema to map to.</param>
/// <returns>A result containing the mapped data or an error.</returns>
Task<Result<RegulatoryData>> MapToRegulatorySchemaAsync(UnifiedMetadataRecord metadata, RegulatorySchema schema);

/// <summary>
/// Validates data against regulatory schema requirements.
/// </summary>
/// <param name="data">The data to validate.</param>
/// <param name="schema">The regulatory schema.</param>
/// <returns>A result containing validation result or an error.</returns>
Task<Result<ValidationResult>> ValidateAgainstSchemaAsync(RegulatoryData data, RegulatorySchema schema);
```

**Error Handling**:
- Export format errors
- Digital signature errors
- Schema validation errors

**Performance Requirements**:
- XML export: < 1 second
- PDF export: < 3 seconds
- Schema mapping: < 500ms

---

#### IPdfRequirementSummarizer

**Purpose**: Summarizes PDF content into predefined requirement categories.

**Dependencies**: `IMetadataExtractor`, `IOcrExecutor`

**Method Signatures**:

```csharp
/// <summary>
/// Summarizes PDF content into predefined requirement categories.
/// </summary>
/// <param name="pdfPath">The path to the PDF file.</param>
/// <param name="categories">The predefined categories (bloqueo, documentación, etc.).</param>
/// <returns>A result containing the requirement summary or an error.</returns>
Task<Result<RequirementSummary>> SummarizeRequirementsAsync(string pdfPath, RequirementCategory[] categories);

/// <summary>
/// Extracts semantic cues from OCR text and classifies into buckets.
/// </summary>
/// <param name="ocrText">The OCR text to analyze.</param>
/// <param name="categories">The requirement categories.</param>
/// <returns>A result containing the classified requirements or an error.</returns>
Task<Result<ClassifiedRequirements>> ClassifyRequirementsAsync(string ocrText, RequirementCategory[] categories);

/// <summary>
/// Gets the confidence score for requirement classification.
/// </summary>
/// <param name="summary">The requirement summary.</param>
/// <returns>A result containing confidence scores or an error.</returns>
Task<Result<RequirementConfidenceScores>> GetRequirementConfidenceAsync(RequirementSummary summary);
```

**Error Handling**:
- PDF parsing errors
- OCR errors
- Classification errors

**Performance Requirements**:
- Requirement summarization: < 5 seconds
- Classification: < 2 seconds

---

#### ICriterionMapper

**Purpose**: Maps semantic labels to category references from configuration.

**Dependencies**: `IPdfRequirementSummarizer`

**Method Signatures**:

```csharp
/// <summary>
/// Maps content excerpts to labeled categories using keyword or rule-based lookup.
/// </summary>
/// <param name="content">The content excerpt to map.</param>
/// <param name="criterionConfig">The criterion mapping configuration.</param>
/// <returns>A result containing the mapped categories or an error.</returns>
Task<Result<List<MappedCategory>>> MapToCategoriesAsync(string content, CriterionMappingConfig criterionConfig);

/// <summary>
/// Loads criterion mapping configuration.
/// </summary>
/// <param name="configPath">The path to the configuration file.</param>
/// <returns>A result containing the criterion configuration or an error.</returns>
Task<Result<CriterionMappingConfig>> LoadCriterionConfigAsync(string configPath);

/// <summary>
/// Validates criterion mapping configuration.
/// </summary>
/// <param name="config">The configuration to validate.</param>
/// <returns>A result indicating validation success or failure.</returns>
Task<Result<bool>> ValidateCriterionConfigAsync(CriterionMappingConfig config);
```

**Error Handling**:
- Configuration loading errors
- Mapping errors
- Invalid configuration

**Performance Requirements**:
- Category mapping: < 500ms
- Config loading: < 200ms

---

## Data Models & Entities

This section defines the domain entities and data models referenced by the interfaces.

### Common Types

#### Result (Non-Generic)

Represents a result that indicates only success or failure, without a value. Used for operations that don't need to return data, only indicate success/failure status.

**Namespace:** `ExxerCube.Prisma.Domain.Common`

**Properties:**
- `IsSuccess` (bool): Indicates whether the operation succeeded
- `Error` (string?): Error message if operation failed

**Static Methods:**
- `Result Success()`: Creates a successful result
- `Result Failure(string error)`: Creates a failure result with error message

**Usage Pattern:**
```csharp
// For success/failure-only operations
Task<Result> CloseBrowserAsync(string sessionId);

// Implementation
public async Task<Result> CloseBrowserAsync(string sessionId)
{
    try
    {
        // ... operation ...
        return Result.Success();
    }
    catch (Exception ex)
    {
        return Result.Failure($"Failed to close browser: {ex.Message}");
    }
}
```

**Relationship to Result<T>:**
- `Result` is the non-generic version for success/failure-only operations
- `Result<T>` is the generic version for operations that return a value
- Both follow Railway-Oriented Programming pattern
- Use `Result` when `IsSuccess` property is sufficient
- Use `Result<bool>` only when the boolean value itself is meaningful

**Note:** This type must be implemented before interface implementations begin (see Task CC.0 in implementation-tasks.md).

### Core Entities

#### Expediente

Represents a regulatory case file (expediente) from CNBV/UIF.

```csharp
public class Expediente
{
    public string NumeroExpediente { get; set; }  // e.g., "A/AS1-2505-088637-PHM"
    public string NumeroOficio { get; set; }     // e.g., "214-1-18714972/2025"
    public string SolicitudSiara { get; set; }    // e.g., "AGAFADAFSON2/2025/000083"
    public int Folio { get; set; }
    public int OficioYear { get; set; }
    public int AreaClave { get; set; }
    public string AreaDescripcion { get; set; }   // e.g., "ASEGURAMIENTO", "HACENDARIO"
    public DateTime FechaPublicacion { get; set; }
    public int DiasPlazo { get; set; }
    public string AutoridadNombre { get; set; }
    public string? AutoridadEspecificaNombre { get; set; }
    public string? NombreSolicitante { get; set; }
    public string Referencia { get; set; }
    public string Referencia1 { get; set; }
    public string Referencia2 { get; set; }
    public bool TieneAseguramiento { get; set; }
    public List<SolicitudParte> SolicitudPartes { get; set; }
    public List<SolicitudEspecifica> SolicitudEspecificas { get; set; }
}
```

#### Persona

Represents a person (physical or legal entity) involved in a regulatory case.

```csharp
public class Persona
{
    public int ParteId { get; set; }
    public string Caracter { get; set; }          // e.g., "Patrón Determinado", "Contribuyente Auditado"
    public string PersonaTipo { get; set; }       // "Fisica" or "Moral"
    public string? Paterno { get; set; }
    public string? Materno { get; set; }
    public string Nombre { get; set; }
    public string? Rfc { get; set; }
    public string? Relacion { get; set; }
    public string? Domicilio { get; set; }
    public string? Complementarios { get; set; }  // Additional info (CURP, birth date, etc.)
    public List<string> RfcVariants { get; set; } // RFC variants for identity resolution
}
```

#### Oficio

Represents a regulatory directive (oficio).

```csharp
public class Oficio
{
    public string NumeroOficio { get; set; }
    public string NumeroExpediente { get; set; }
    public DateTime FechaRecepcion { get; set; }
    public DateTime FechaRegistro { get; set; }
    public DateTime FechaEstimadaConclusion { get; set; }
    public int DiasPlazo { get; set; }
    public string TipoAsunto { get; set; }        // "EMBARGO", "DESEMBARGO", "DOCUMENTACIÓN", etc.
    public string Subdivision { get; set; }       // e.g., "A/AS Especial Aseguramiento"
    public string Descripcion { get; set; }       // Full name (Paterno + Materno + Nombre)
    public string NombreRemitente { get; set; }
    public List<ComplianceRequirement> Requisitos { get; set; }
}
```

#### ComplianceAction

Represents a compliance action to be taken.

```csharp
public enum ComplianceActionType
{
    Block,
    Unblock,
    Document,
    Transfer,
    Information,
    Ignore
}

public class ComplianceAction
{
    public ComplianceActionType ActionType { get; set; }
    public string? AccountNumber { get; set; }
    public string? ProductType { get; set; }
    public decimal? Amount { get; set; }
    public string? ExpedienteOrigen { get; set; }  // Original expediente that required block
    public string? OficioOrigen { get; set; }      // Original oficio that required block
    public string? RequerimientoOrigen { get; set; } // Original requerimiento ID
    public Dictionary<string, object> AdditionalData { get; set; }
}
```

#### SLAStatus

Tracks SLA status for regulatory responses.

```csharp
public class SLAStatus
{
    public string FileId { get; set; }
    public DateTime IntakeDate { get; set; }
    public DateTime Deadline { get; set; }
    public int DaysPlazo { get; set; }
    public TimeSpan RemainingTime { get; set; }
    public bool IsAtRisk { get; set; }
    public bool IsBreached { get; set; }
    public EscalationLevel EscalationLevel { get; set; }
    public DateTime? EscalatedAt { get; set; }
}

public enum EscalationLevel
{
    None,
    Warning,
    Critical,
    Breached
}
```

#### AuditRecord

Represents an audit log entry.

```csharp
public class AuditRecord
{
    public string Id { get; set; }
    public string FileId { get; set; }
    public AuditAction Action { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; }
    public Dictionary<string, object> Details { get; set; }
    public ClassificationResult? Classification { get; set; }
    public ClassificationScores? Scores { get; set; }
}

public enum AuditAction
{
    Download,
    Classification,
    Move,
    Extraction,
    Review,
    Export,
    Escalation
}
```

#### FieldExtractionResult

Represents the result of field extraction.

```csharp
public class FieldExtractionResult
{
    public string FieldName { get; set; }
    public object? Value { get; set; }
    public float Confidence { get; set; }
    public string Source { get; set; }            // "XML", "DOCX", "PDF", "OCR"
    public FieldOriginTrace OriginTrace { get; set; }
}

public class ExtractedFields
{
    public Dictionary<string, FieldExtractionResult> Fields { get; set; }
    public float OverallConfidence { get; set; }
    public List<string> MissingFields { get; set; }
}
```

#### ClassificationResult

Represents file classification results.

```csharp
public class ClassificationResult
{
    public ClassificationLevel1 Level1 { get; set; }
    public ClassificationLevel2? Level2 { get; set; }
    public ClassificationScores Scores { get; set; }
    public int Confidence { get; set; }          // 0-100
}

public enum ClassificationLevel1
{
    Aseguramiento,
    Desembargo,
    Documentacion,
    Informacion,
    Transferencia,
    OperacionesIlicitas
}

public enum ClassificationLevel2
{
    Especial,
    Judicial,
    Hacendario
}
```

### Supporting Models

#### FileMetadata

```csharp
public class FileMetadata
{
    public string FileId { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public string? Url { get; set; }
    public DateTime DownloadTimestamp { get; set; }
    public string Checksum { get; set; }         // SHA-256
    public long FileSize { get; set; }
    public FileFormat Format { get; set; }
}
```

#### UnifiedMetadataRecord

```csharp
public class UnifiedMetadataRecord
{
    public Expediente Expediente { get; set; }
    public List<Persona> Personas { get; set; }
    public Oficio Oficio { get; set; }
    public ExtractedFields ExtractedFields { get; set; }
    public ClassificationResult Classification { get; set; }
    public MatchedFields MatchedFields { get; set; }
    public RequirementSummary? RequirementSummary { get; set; }
    public SLAStatus? SlaStatus { get; set; }
}
```

#### MatchedFields

```csharp
public class MatchedFields
{
    public Dictionary<string, FieldMatchResult> FieldMatches { get; set; }
    public float OverallAgreement { get; set; }
    public List<string> ConflictingFields { get; set; }
    public List<string> MissingFields { get; set; }
}

public class FieldMatchResult
{
    public object? Value { get; set; }
    public List<FieldSource> Sources { get; set; }
    public int AgreementLevel { get; set; }      // 0-100
    public FieldOriginTrace OriginTrace { get; set; }
}
```

#### RequirementSummary

```csharp
public class RequirementSummary
{
    public bool RequiereBloqueo { get; set; }
    public BloqueoDetails? BloqueoDetails { get; set; }
    public bool RequiereDesbloqueo { get; set; }
    public DesbloqueoDetails? DesbloqueoDetails { get; set; }
    public bool RequiereDocumentacion { get; set; }
    public DocumentacionDetails? DocumentacionDetails { get; set; }
    public bool RequiereTransferencia { get; set; }
    public TransferenciaDetails? TransferenciaDetails { get; set; }
    public bool RequiereInformacion { get; set; }
    public InformacionDetails? InformacionDetails { get; set; }
}
```

---

## Sequential Processing Flow

This section documents the end-to-end processing flows for all four stages.

### Stage 1: Ingestion Flow

```
1. IBrowserAutomationAgent.LaunchBrowserAsync()
   └─> Navigate to regulatory website
   
2. IBrowserAutomationAgent.IdentifyDownloadableFilesAsync()
   └─> Detect PDF, XML, DOCX files
   
3. For each downloadable file:
   a. IDownloadTracker.IsFileAlreadyDownloadedAsync()
      └─> Check checksum against previous downloads
   
   b. If not downloaded:
      - IBrowserAutomationAgent.DownloadFileAsync()
        └─> Download file data
      
      - IDownloadStorage.SaveFileAsync()
        └─> Persist to storage with deterministic path
      
      - IFileMetadataLogger.LogMetadataAsync()
        └─> Record file metadata (name, URL, timestamp, checksum)
      
      - IDownloadTracker.RecordDownloadAsync()
        └─> Mark as downloaded

4. IBrowserAutomationAgent.CloseBrowserAsync()
   └─> Clean up browser session
```

**Output**: List of downloaded files with metadata, ready for Stage 2.

### Stage 2: Extraction Flow

```
1. For each downloaded file:
   a. IFileTypeIdentifier.IdentifyFileTypeAsync()
      └─> Determine file type (PDF, XML, DOCX)
   
   b. IFileTypeIdentifier.ValidateFileFormatAsync()
      └─> Ensure expected format
   
   c. IScanDetector.IsScannedPdfAsync() [if PDF]
      └─> Check if PDF is scanned (non-searchable)
   
   d. If scanned:
      - IScanCleaner.CleanScanAsync()
        └─> Preprocess image for OCR
      
      - IOcrExecutor.ExecuteOcrAsync()
        └─> Extract text from images
   
   e. IMetadataExtractor.ExtractMetadataAsync()
      └─> Extract metadata based on file type
         - ExtractFromXmlAsync() [if XML]
         - ExtractFromDocxAsync() [if DOCX]
         - ExtractFromPdfAsync() [if PDF, with OCR fallback]
   
   f. IXmlNullableParser<Expediente>.ParseAsync() [if XML]
      └─> Parse XML to Expediente object
   
   g. IFieldExtractor<T>.ExtractFieldsAsync()
      └─> Extract structured fields
         - DocxFieldExtractor for DOCX
         - PdfOcrFieldExtractor for PDF
   
   h. IFieldMatcher<T>.MatchFieldsAsync()
      └─> Compare fields across XML, DOCX, PDF
      └─> Generate unified metadata record
   
   i. IFileClassifier.ClassifyLevel1Async()
      └─> Assign main category
   
   j. IFileClassifier.ClassifyLevel2Async()
      └─> Assign subcategory
   
   k. ISafeFileNamer.GenerateSafeFileNameAsync()
      └─> Generate normalized file name
   
   l. IFileMover.MoveFileAsync()
      └─> Organize file based on classification
   
   m. IAuditLogger.LogActionAsync()
      └─> Log download action
   
   n. IAuditLogger.LogClassificationDecisionAsync()
      └─> Log classification with scores

2. IReportGenerator.ExportSummaryAsync()
   └─> Generate CSV/JSON summary of classification state
```

**Output**: Classified files with extracted metadata, unified records, ready for Stage 3.

### Stage 3: Decision Flow

```
1. For each unified metadata record:
   a. ISLAEnforcer.CalculateDeadlineAsync()
      └─> Calculate deadline from intake date + days plazo
   
   b. ISLAEnforcer.CheckAndEscalateAsync()
      └─> Check if within critical threshold, escalate if needed
   
   c. IPersonIdentityResolver.ResolveIdentityAsync()
      └─> Resolve person identity, handle RFC variants
   
   d. IPersonIdentityResolver.DeduplicateRecordsAsync()
      └─> Consolidate duplicate person records
   
   e. ILegalDirectiveClassifier.ClassifyDirectiveAsync()
      └─> Interpret legal clauses from text
   
   f. ILegalDirectiveClassifier.DetectLegalInstrumentsAsync()
      └─> Detect references to legal instruments (Acuerdo 105/2021, etc.)
   
   g. ILegalDirectiveClassifier.MapToComplianceActionAsync()
      └─> Map directive to action (block, unblock, ignore)
   
   h. IFieldMatcher<T>.ValidateMatchResultAsync()
      └─> Validate completeness and consistency
   
   i. If validation fails or confidence low:
      - IManualReviewerPanel.GetReviewCasesAsync()
        └─> Queue for manual review
      
      - [Human Review Process]
        - IManualReviewerPanel.GetFieldAnnotationsAsync()
        - IManualReviewerPanel.SubmitReviewDecisionAsync()
   
   j. IFieldMatcher<T>.GenerateUnifiedRecordAsync()
      └─> Generate final unified metadata record
   
   k. ILayoutGenerator.GenerateExcelLayoutAsync()
      └─> Generate Excel layout for SIRO registration
   
   l. IFieldAgreement.AnnotateFieldAgreementAsync()
      └─> Report field-level match confidence

2. ISLAEnforcer.GetFilesAtRiskAsync()
   └─> Monitor and alert on impending breaches
```

**Output**: Validated unified records with compliance actions, Excel layouts, ready for Stage 4.

### Stage 4: Response Flow

```
1. For each validated unified record:
   a. IPdfRequirementSummarizer.SummarizeRequirementsAsync()
      └─> Classify PDF into requirement categories
         - RequiereBloqueo
         - RequiereDesbloqueo
         - RequiereDocumentacion
         - RequiereTransferencia
         - RequiereInformacion
   
   b. ICriterionMapper.MapToCategoriesAsync()
      └─> Map semantic labels to categories
   
   c. IResponseExporter.MapToRegulatorySchemaAsync()
      └─> Map data to SIRO/regulatory schema
   
   d. IResponseExporter.ValidateAgainstSchemaAsync()
      └─> Validate against regulatory requirements
   
   e. If validation passes:
      - IResponseExporter.ExportSiroXmlAsync()
        └─> Generate SIRO-compliant XML
      
      - OR IResponseExporter.ExportSignedPdfAsync()
        └─> Generate digitally signed PDF
   
   f. IAuditLogger.LogActionAsync()
      └─> Log export action

2. Final audit and reporting:
   - IReportGenerator.GenerateClassificationReportAsync()
     └─> Generate compliance report
```

**Output**: SIRO XML or signed PDF files, ready for submission to regulatory authorities.

---

## Cross-Stage Integration Points

### Stage 1 → Stage 2

**Data Flow**:
- `FileMetadata` from Stage 1 feeds into `IFileTypeIdentifier` and `IMetadataExtractor`
- File paths from `IDownloadStorage` are used by Stage 2 processors

**Integration Points**:
- `IDownloadStorage.GetFilePathAsync()` → `IFileTypeIdentifier.IdentifyFileTypeAsync()`
- `IFileMetadataLogger.GetMetadataAsync()` → `IMetadataExtractor.ExtractMetadataAsync()`

**Error Handling**:
- If file is missing, Stage 2 returns error and logs to `IAuditLogger`
- If metadata is incomplete, Stage 2 attempts extraction with reduced confidence

### Stage 2 → Stage 3

**Data Flow**:
- `UnifiedMetadataRecord` from Stage 2 feeds into Stage 3 decision logic
- `ClassificationResult` determines processing priority
- `ExtractedFields` provides data for identity resolution

**Integration Points**:
- `IFieldMatcher<T>.GenerateUnifiedRecordAsync()` → `IPersonIdentityResolver.ResolveIdentityAsync()`
- `ClassificationResult` → `ISLAEnforcer.CalculateDeadlineAsync()` (for priority)
- `ExtractedMetadata` → `ILegalDirectiveClassifier.ClassifyDirectiveAsync()`

**Error Handling**:
- If classification confidence is low, record is queued for manual review
- If field matching fails, `IManualReviewerPanel` is invoked
- If SLA is at risk, escalation is triggered immediately

### Stage 3 → Stage 4

**Data Flow**:
- Validated `UnifiedMetadataRecord` feeds into export generation
- `ComplianceAction` determines export format requirements
- `RequirementSummary` guides PDF summarization

**Integration Points**:
- `ILayoutGenerator.GenerateExcelLayoutAsync()` → `IResponseExporter.MapToRegulatorySchemaAsync()`
- `IPdfRequirementSummarizer.SummarizeRequirementsAsync()` → `ICriterionMapper.MapToCategoriesAsync()`
- `UnifiedMetadataRecord` → `IResponseExporter.ExportSiroXmlAsync()` or `ExportSignedPdfAsync()`

**Error Handling**:
- If schema validation fails, record is returned to Stage 3 for correction
- If digital signature fails, error is logged and manual intervention required
- If export format is invalid, fallback to alternative format

### Error Propagation Strategy

1. **Immediate Retry**: Transient errors (network, file locks) are retried with exponential backoff
2. **Manual Review**: Ambiguous cases are queued for `IManualReviewerPanel`
3. **Escalation**: SLA breaches trigger immediate escalation via `ISLAEnforcer`
4. **Audit Trail**: All errors are logged via `IAuditLogger` for traceability
5. **Recovery**: Failed stages can be restarted from last successful checkpoint

---

## Implementation Requirements

This section derives implementation requirements from the interface contracts defined above.

### Technology Stack

**Core Platform**: .NET 10 (C#)
- **Rationale**: Strong typing, async/await support, Railway-Oriented Programming patterns
- **Domain Layer**: Pure C# with `Result<T>` pattern
- **Application Layer**: C# orchestration services
- **Infrastructure Layer**: C# adapters for external systems

**Python Integration**: Python 3.11+ for OCR and NLP
- **Rationale**: Rich ecosystem for OCR (Tesseract, EasyOCR) and NLP (spaCy, transformers)
- **Integration**: CSnakes for Python-C# interop
- **Modules**: 
  - OCR pipeline (`prisma-ocr-pipeline`)
  - AI extractors (`prisma-ai-extractors`)
  - Document generators (`prisma-document-generator`)

**UI Framework**: Blazor Server (or technology-agnostic via `IUIBundle`)
- **Rationale**: C#-based, real-time updates via SignalR
- **Components**: MudBlazor for modern UI components

**Database**: SQL Server / PostgreSQL
- **Rationale**: Relational data for audit logs, metadata, SLA tracking
- **ORM**: Entity Framework Core

**File Storage**: Local filesystem or Azure Blob Storage
- **Rationale**: Deterministic paths, scalable storage
- **Implementation**: `IDownloadStorage` adapter pattern

**Browser Automation**: Playwright
- **Rationale**: Modern, reliable browser automation
- **Implementation**: `IBrowserAutomationAgent` adapter

### Infrastructure Requirements

**Microservices Architecture**:
- Each stage can be deployed as independent microservice
- Stateless design for horizontal scaling
- Message queue for inter-stage communication (Azure Service Bus / RabbitMQ)

**Deployment**:
- Containerized (Docker)
- Kubernetes orchestration for production
- Health checks and metrics endpoints

**Monitoring & Observability**:
- Application Insights / OpenTelemetry
- Structured logging (Serilog)
- Metrics collection (Prometheus)
- Distributed tracing

**Security**:
- Authentication/Authorization (Azure AD / Identity Server)
- Encryption at rest for sensitive data
- Encryption in transit (TLS 1.3)
- Digital signature support for PDF/XML export
- Audit logging for compliance

**Performance**:
- Async/await throughout for non-blocking I/O
- Parallel processing where possible (Stage 2 extraction)
- Caching for frequently accessed data (Redis)
- CDN for static assets

### Compliance Requirements

**SIRO Format Compliance**:
- XML schema validation
- Required field completeness
- Data format compliance (dates, amounts, identifiers)

**Digital Signatures**:
- X.509 certificate support
- PDF signing (PAdES)
- XML signing (XMLDSig)

**Audit Requirements**:
- Immutable audit logs
- Complete traceability (file → processing → export)
- Retention policies (7+ years)

**Legal Constraints**:
- Non-notification enforcement (no client alerts unless allowed)
- Confidentiality of sensitive data
- Data retention and deletion policies

### Testing Requirements

**Unit Tests**:
- All interface implementations
- Domain logic (classification, identity resolution)
- Error handling scenarios

**Integration Tests**:
- End-to-end pipeline (Stage 1 → Stage 4)
- Python-C# interop
- Database operations
- File system operations

**Performance Tests**:
- Load testing (100+ concurrent files)
- SLA deadline calculations
- OCR processing time
- Export generation time

**Compliance Tests**:
- SIRO schema validation
- Digital signature verification
- Audit log completeness

---

## Appendix: Existing Interfaces Analysis

### Currently Implemented Interfaces

The following interfaces are already implemented in `Prisma/Code/Src/CSharp/Domain/Interfaces/`:

1. **IFieldExtractor** - Extracts structured fields from OCR text
   - Maps to: Feature 26-27 (partial - needs generic `IFieldExtractor<T>`)

2. **IFileLoader** - Loads images from file system
   - Maps to: Supporting infrastructure (not in feature list)

3. **IOcrProcessingService** - Orchestrates OCR processing pipeline
   - Maps to: Supporting infrastructure (orchestration layer)

4. **IPythonInteropService** - Python interoperability abstraction
   - Maps to: Supporting infrastructure (Python integration)

5. **IImagePreprocessor** - Image preprocessing for OCR
   - Maps to: Feature 14 (IScanCleaner - partial)

6. **IOcrExecutor** - Executes OCR on images
   - Maps to: Feature 15 (IMetadataExtractor - OCR fallback)

7. **IOutputWriter** - Writes processing results
   - Maps to: Supporting infrastructure (not in feature list)

### Gap Analysis

**Missing Interfaces (to be implemented)**:

**Stage 1** (4 interfaces):
- `IBrowserAutomationAgent`
- `IDownloadTracker`
- `IDownloadStorage`
- `IFileMetadataLogger`

**Stage 2** (13 interfaces):
- `IFileTypeIdentifier`
- `IMetadataExtractor` (needs expansion beyond OCR)
- `ISafeFileNamer`
- `IFileClassifier`
- `IFileMover`
- `IRuleScorer`
- `IScanDetector`
- `IScanCleaner` (partial - `IImagePreprocessor` exists)
- `IAuditLogger`
- `IReportGenerator`
- `IXmlNullableParser<T>`
- `IFieldExtractor<T>` (generic version)
- `IFieldMatcher<T>`

**Stage 3** (8 interfaces):
- `ISLAEnforcer`
- `IPersonIdentityResolver`
- `ILegalDirectiveClassifier`
- `IManualReviewerPanel`
- `IUIBundle`
- `ILayoutGenerator`
- `IFieldAgreement`
- `IMatchingPolicy`

**Stage 4** (3 interfaces):
- `IResponseExporter`
- `IPdfRequirementSummarizer`
- `ICriterionMapper`

**Total Missing**: 28 interfaces to be implemented

**Note**: Some interfaces handle multiple features (e.g., `IBrowserAutomationAgent` handles Features 1-2, `IFileClassifier` handles Features 9-10, `IFieldMatcher<T>` handles Features 28, 29, and 33).

---

## Conclusion

This PRP document establishes a complete interface-driven design for the Regulatory Compliance Automation System. The system implements **35 features** mapped to **28 distinct interfaces**. By defining all interfaces first with complete method signatures, we enable:

1. **Parallel Development**: Teams can implement different interfaces simultaneously
2. **Clear Contracts**: Each interface defines exactly what capabilities are needed
3. **Testability**: Mock implementations can be created immediately
4. **Flexibility**: Implementations can be swapped without changing dependent code
5. **Documentation**: Interface contracts serve as living documentation

The next phase involves implementing these interfaces following Hexagonal Architecture principles, with domain logic in the core and adapters in the infrastructure layer.

---

**Document Status**: Complete  
**Next Steps**: Begin implementation of Stage 1 interfaces, starting with `IBrowserAutomationAgent` and `IDownloadTracker`.

