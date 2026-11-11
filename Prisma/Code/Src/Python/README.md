# Prisma Python Applications Suite

## Overview

This directory contains three independent Python applications for the Prisma document processing system. Each application follows Single Responsibility Principle (SRP) and can be developed, tested, and deployed independently.

## 📦 Applications

### 1. **Prisma OCR Pipeline** (`prisma-ocr-pipeline/`)

Traditional OCR processing system using Tesseract engine with advanced image preprocessing.

**Key Features:**
- Red watermark removal
- Document deskewing
- Spanish/English OCR
- Modular architecture (15+ components)
- Full unit test coverage

**Use Case:** Processing scanned documents with traditional OCR techniques

```bash
cd prisma-ocr-pipeline
python src/ocr_pipeline.py --input document.pdf --outdir ./output
```

---

### 2. **Prisma AI Extractors** (`prisma-ai-extractors/`)

Modern AI-powered document extraction using vision-language models.

**Key Features:**
- Multiple AI models (SmolVLM2, GOT-OCR2, PaddleOCR, DocTR)
- Unified CLI interface
- GPU acceleration with automatic fallback
- Pydantic-validated outputs
- Batch processing support

**Use Case:** Extracting structured information from complex documents using AI

```bash
cd prisma-ai-extractors
python src/main.py --image document.png --extractor smolvlm
```

---

### 3. **Prisma Document Generator** (`prisma-document-generator/`)

Test data generation system for creating synthetic documents.

**Key Features:**
- Generate synthetic Spanish legal documents
- Realistic degradation simulation
- Watermark application
- Corpus generation (template-based and AI-powered)
- 999+ test fixtures

**Use Case:** Creating test datasets for OCR/AI model validation

```bash
cd prisma-document-generator
python src/simulate_documents.py --input corpus.json --output fixtures/
```

---

## 🏗️ Architecture

Each application is structured as:

```
prisma-{app-name}/
├── src/           # Source code
├── tests/         # Unit tests
├── docs/          # Documentation
├── config/        # Configuration files
└── requirements.txt
```

## 🚀 Quick Start

### Installation

```bash
# For OCR Pipeline
cd prisma-ocr-pipeline
pip install -r requirements.txt

# For AI Extractors
cd prisma-ai-extractors
pip install -r requirements.txt

# For Document Generator
cd prisma-document-generator
pip install -r requirements.txt
```

### Running Tests

```bash
# Test any application
cd prisma-{app-name}
pytest tests/
```

## 📊 Comparison

| Feature | OCR Pipeline | AI Extractors | Document Generator |
|---------|-------------|---------------|-------------------|
| **Technology** | Tesseract | Vision-Language Models | PIL/Synthetic |
| **Speed** | Fast | Medium | Fast |
| **Accuracy** | Good | Excellent | N/A |
| **GPU Required** | No | Recommended | No |
| **Use Case** | Traditional OCR | Complex extraction | Test data |

## 🔄 Migration from Old Structure

### Previous Structure
```
/CSharp/Python/
├── smolvlm_extractor.py
├── ocr_pipeline.py
├── simulate_documents.py
└── ... (all files mixed)
```

### New Structure
```
/Python/
├── prisma-ocr-pipeline/     # OCR components
├── prisma-ai-extractors/    # AI models
└── prisma-document-generator/ # Test data tools
```

## 📚 Documentation

Each application has its own documentation:

- [OCR Pipeline Documentation](prisma-ocr-pipeline/docs/USER_MANUAL.md)
- [AI Extractors Documentation](prisma-ai-extractors/docs/README.md)
- [Document Generator Documentation](prisma-document-generator/docs/README.md)

## 🤝 Contributing

1. Choose the application you want to contribute to
2. Follow the specific application's development guide
3. Write tests for new features
4. Submit PR with clear description

## 📝 License

Proprietary - ExxerCube

## 🆘 Support

For issues specific to an application, check its documentation. For general questions about the Prisma system, contact the development team.

---

**Note:** The original monolithic implementation is preserved in `/Prisma/Code/Src/CSharp/Python/` for backward compatibility.