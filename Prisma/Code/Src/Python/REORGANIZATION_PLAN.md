# Project Reorganization Plan

## Overview
Separating the monolithic Python implementation into three distinct applications following Single Responsibility Principle (SRP).

## Three Applications

### 1. Prisma OCR Pipeline (Tesseract-based)
**Location**: `/Prisma/Code/Src/Python/prisma-ocr-pipeline/`
**Purpose**: Traditional OCR processing with image preprocessing and text extraction

**Files to Move**:
- `ocr_pipeline.py` → Main pipeline
- `ocr_modules/*` → Modular components (already well-structured)
- `modular_ocr_cli.py` → CLI interface
- `*_cli.py` → Individual field extractors
- OCR documentation and configurations

### 2. Prisma AI Extractors (ML Models)
**Location**: `/Prisma/Code/Src/Python/prisma-ai-extractors/`
**Purpose**: AI-powered document extraction using vision-language models

**Files to Move**:
- `smolvlm_extractor.py` → SmolVLM2 model
- `got_ocr2_extractor.py` → GOT-OCR2 model
- `paddleocr_extractor.py` → PaddleOCR model
- `doctr_extractor.py` → DocTR model
- Benchmark and validation scripts
- `client_demo.py` → Demo application

### 3. Prisma Document Generator
**Location**: `/Prisma/Code/Src/Python/prisma-document-generator/`
**Purpose**: Generate synthetic documents for testing

**Files to Move**:
- `simulate_documents.py` → Document simulation
- `generate_corpus.py` → AI-powered corpus generation
- `generate_test_corpus.py` → Template-based generation
- `entities.json` → Legal entities database
- `requerimientos_schema.json` → Document schema
- Test fixtures and corpus files

## Folder Structure for Each App

```
app-name/
├── src/                    # Source code
│   ├── __init__.py
│   ├── main.py            # Entry point
│   ├── models/            # Data models
│   ├── services/          # Business logic
│   ├── utils/             # Utilities
│   └── config/            # Configuration
├── tests/                 # Unit tests
│   ├── __init__.py
│   ├── test_models.py
│   ├── test_services.py
│   └── test_utils.py
├── docs/                  # Documentation
│   ├── README.md
│   ├── USER_MANUAL.md
│   └── DEVELOPER_GUIDE.md
├── config/                # Configuration files
│   ├── default.yaml
│   └── requirements.txt
├── examples/              # Usage examples
└── pyproject.toml         # Project configuration
```

## Migration Steps

1. **Phase 1**: Create folder structure ✅
2. **Phase 2**: Move and reorganize OCR pipeline
3. **Phase 3**: Move and reorganize AI extractors
4. **Phase 4**: Move test data generator
5. **Phase 5**: Create documentation for each app
6. **Phase 6**: Create unit tests for AI extractors
7. **Phase 7**: Create PR documentation

## Benefits

- **Clear Separation of Concerns**: Each app has a single responsibility
- **Independent Development**: Teams can work on different apps without conflicts
- **Better Testing**: Focused unit tests for each component
- **Easier Maintenance**: Clear boundaries and dependencies
- **Scalability**: Apps can be deployed independently