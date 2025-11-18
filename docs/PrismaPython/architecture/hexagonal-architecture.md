# Hexagonal Architecture Implementation

## Overview

This document describes the Hexagonal Architecture implementation for the ExxerCube OCR Pipeline, ensuring clean separation of concerns and maintainable code.

## Architecture Layers

### 1. Domain Layer (Core)

**Purpose**: Contains business logic, entities, and domain interfaces (ports)

**Key Components**:
- `ImageData` - Document image entity
- `OCRResult` - OCR processing result
- `ExtractedFields` - Extracted document fields
- `ProcessingConfig` - Processing configuration
- Domain interfaces (ports) for external dependencies

**Location**: `src/ExxerCube.Ocr.Domain/`

### 2. Application Layer

**Purpose**: Orchestrates use cases and business workflows

**Key Components**:
- `ProcessDocumentCommand` - Command for document processing
- `ProcessDocumentHandler` - Command handler
- `ProcessingOrchestrator` - Main processing workflow
- Application services

**Location**: `src/ExxerCube.Ocr.Application/`

### 3. Infrastructure Layer (Adapters)

**Purpose**: Implements domain interfaces for external systems

**Key Components**:
- `PythonOcrProcessingAdapter` - Python OCR integration
- `FileSystemLoader` - File system operations
- `FileSystemOutputWriter` - Output file operations
- Configuration adapters

**Location**: `src/ExxerCube.Ocr.Infrastructure/`

## Dependency Flow

```
Console/API → Application → Domain ← Infrastructure
```

- **Inward Dependencies**: Infrastructure depends on Domain
- **Outward Dependencies**: Application depends on Domain
- **No Cross-Layer Dependencies**: Infrastructure never depends on Application

## Key Principles

1. **Dependency Inversion**: High-level modules don't depend on low-level modules
2. **Single Responsibility**: Each class has one reason to change
3. **Interface Segregation**: Clients depend only on interfaces they use
4. **Open/Closed**: Open for extension, closed for modification

## Benefits

- **Testability**: Easy to mock dependencies
- **Maintainability**: Clear separation of concerns
- **Flexibility**: Easy to swap implementations
- **Scalability**: Independent scaling of layers
