# CNBV Document Validation Strategy

Integration with existing `prisma-ai-extractors` OCR infrastructure for comprehensive document validation.

## 🎯 Mission

Validate that generated CNBV documents:
1. Meet technical specification requirements
2. Can be reliably parsed back using OCR
3. Contain realistic imperfections
4. Match source XML data

## 🏗 Architecture

### Existing Infrastructure (Reused)

From `Prisma/Code/Src/Python/prisma-ai-extractors/src/`:

#### 1. **GOT-OCR2** (`got_ocr2_extractor.py`)
- **Model**: stepfun-ai/GOT-OCR-2.0-hf
- **Type**: Multimodal transformer (General OCR Theory 2.0)
- **Strength**: State-of-the-art for Spanish legal documents
- **Usage**: Primary text extraction engine

#### 2. **ComprehensiveDocumentValidator** (`comprehensive_document_validator.py`)
- **Validates**:
  - Logo detection and quality
  - Font analysis (sizes, consistency, spacing)
  - Layout analysis (margins, columns, alignment)
  - Print quality (resolution, sharpness, contrast)
  - Content completeness
- **Output**: Comprehensive quality scores

#### 3. **GroundTruthValidator** (`ground_truth_validator.py`)
- **Validates**: OCR results against known ground truth
- **Methods**: Field extraction accuracy, text similarity
- **Usage**: XML-PDF consistency verification

### New Integration (`test_cnbv_with_ocr.py`)

Orchestrates existing tools to validate CNBV documents against technical specification.

## 📋 Technical Specification Requirements

Based on `222AAA-44444444442025.pdf` (4-page document):

### 1. Required Sections (in order)

| Section | Purpose |
|---------|---------|
| Header with authority | Identifies issuing authority |
| No. De Identificación | Unique request ID |
| Addressee block | CNBV official recipient |
| Datos generales del solicitante | Request origin details |
| Facultades de la Autoridad | Legal authority citations |
| Fundamento del Requerimiento | Legal foundation |
| Motivación del Requerimiento | Justification |
| Origen del requerimiento | Source information |
| Antecedentes | Background/subjects |
| Personas de quien se requiere información | Individuals/entities |
| Cuentas por conocer | Account types |
| Instrucciones | Specific instructions |
| Cierre y firma | Signature block |

**Validation**: At least 75% of sections must be present

### 2. Data Presence Requirements

| Element | Pattern | Example |
|---------|---------|---------|
| RFC | `[A-Z]{3,4}\d{6}[A-Z0-9]{3}` | `APON33333444` |
| Date | `\d{1,2}[/-]\d{1,2}[/-]\d{4}` | `05/06/2025` |
| Legal Citation | `(artículo\|art\.)\s*\d+` | `artículo 142` |
| Oficio Number | `\d{3}/[A-Z]{3}/-?\d+/\d{4}` | `222/AAA/-4444444444/2025` |

**Validation**: At least 3 of 4 element types must be present

### 3. Layout Integrity

- Headers on every page
- Consistent margins (top, bottom, left, right)
- Proper table structure
- Section title formatting
- Column alignment

**Validation**: Overall layout score ≥ 70%

### 4. Intentional Imperfections (MUST be present)

These simulate real SIARA documents:

| Imperfection | Example | Purpose |
|--------------|---------|---------|
| Spacing errors | "párrafo s" instead of "párrafos" | Template typos |
| Mixed case | Inconsistent UPPERCASE/Title Case | Real-world variation |
| Legal variations | "artículo", "art.", "articulo" | Formatting inconsistency |
| Whitespace noise | Extra spaces, line breaks | OCR artifacts |

**Validation**: At least 1 imperfection type present

### 5. XML-PDF Consistency

If source XML available, verify:
- Oficio number appears in PDF
- Expediente number appears in PDF
- Authority name appears in PDF (partial match OK)
- RFC appears in PDF (if not null)

**Validation**: At least 66% of checks pass

## 🚀 Usage

### Validate Single Document

```bash
cd generators/AAA
python test_cnbv_with_ocr.py \
    test_output/fake_sample_001.pdf \
    --xml test_output/fake_sample_001.xml \
    --output validation_report.json
```

### Validate Test Suite

```bash
# Validate all generated samples
for pdf in test_output/*.pdf; do
    xml="${pdf%.pdf}.xml"
    if [ -f "$xml" ]; then
        python test_cnbv_with_ocr.py "$pdf" --xml "$xml"
    else
        python test_cnbv_with_ocr.py "$pdf"
    fi
done
```

### Expected Output

```
============================================================
Validating: fake_sample_001.pdf
============================================================

1. Extracting text with GOT-OCR2...
2. Validating layout and print quality...
3. Checking required sections...
4. Checking data presence...
5. Validating XML-PDF consistency...
6. Checking realistic imperfections...

============================================================
VALIDATION REPORT
============================================================

REQUIRED SECTIONS: 5/6
  ✓ VICEPRESIDENCIA
  ✓ DIRECCIÓN GENERAL
  ✓ COORDINACIÓN
  ✓ DATOS GENERALES
  ✓ FACULTADES
  ✗ FUNDAMENTO

DATA PRESENCE:
  ✓ has_rfc
  ✓ has_date
  ✓ has_legal_citation
  ✓ has_oficio
    RFCs: EPF999888ZZZ
    Dates: 20/11/2025, 05/06/2025

QUALITY SCORE: 92.5%
  Layout: 95.2%
  Print: 89.8%

XML-PDF CONSISTENCY:
  ✓ oficio_present
  ✓ expediente_present
  ✓ autoridad_present

REALISTIC IMPERFECTIONS:
  ✓ has_spacing_errors
  ✓ has_mixed_case
  ○ has_legal_variations

OVERALL SCORE: 87.3/100 ✅ EXCELLENT
============================================================
```

## 📊 Scoring System

### Overall Score Calculation

| Component | Weight | Criteria |
|-----------|--------|----------|
| Sections | 30% | Required sections present |
| Data Presence | 25% | RFCs, dates, legal citations |
| Quality | 25% | Layout + print quality |
| Consistency | 15% | XML-PDF matching |
| Imperfections | 5% | Realistic errors |

### Rating Scale

| Score | Rating | Meaning |
|-------|--------|---------|
| 75-100 | ✅ EXCELLENT | Production-ready |
| 60-74 | ✓ GOOD | Acceptable for testing |
| 50-59 | ⚠ FAIR | Needs improvement |
| 0-49 | ✗ POOR | Significant issues |

## 🔧 Dependencies

### Required

```bash
cd Prisma/Code/Src/Python/prisma-ai-extractors
pip install -r requirements.txt
```

**Key packages**:
- `transformers` - For GOT-OCR2
- `torch` - PyTorch backend
- `Pillow` - Image processing
- `pdf2image` - PDF conversion
- `doctr` - Alternative OCR engine
- `paddlepaddle`, `paddleocr` - PaddleOCR support
- `opencv-python` - Computer vision
- `scikit-learn` - Layout analysis

### System Requirements

**GPU (Recommended)**:
- CUDA-capable GPU for GOT-OCR2
- Significantly faster processing
- Better accuracy

**CPU (Fallback)**:
- Works but slower
- Uses `torch.float32` instead of `bfloat16`

## 🎯 Test Strategy

### Phase 1: Visual Fidelity (✅ Complete)
- Generate PDFs from XMLs
- Measure visual similarity vs real samples
- **Result**: 95% average similarity

### Phase 2: OCR Validation (Current)
- Extract text with GOT-OCR2
- Validate structure and content
- Verify XML-PDF consistency
- Check realistic imperfections

### Phase 3: Unit Tests (Next)
Based on extracted data, create unit tests:

```python
class TestCNBVStructure(unittest.TestCase):
    def test_required_sections_present(self):
        """All required sections must be present."""
        # Use OCR results to verify

    def test_rfc_format_valid(self):
        """RFC must match pattern."""
        # Validate extracted RFCs

    def test_xml_pdf_consistency(self):
        """Key XML data must appear in PDF."""
        # Compare ground truth vs OCR

    def test_intentional_imperfections(self):
        """Document must have realistic errors."""
        # Verify imperfection patterns
```

## 🔄 Integration with Generator

### Add validation to generation pipeline:

```python
from prp1_generator import xml_to_pdf
from test_cnbv_with_ocr import CNBVDocumentValidator

# Generate PDF
xml_path = "sample.xml"
pdf_path = "sample.pdf"
xml_to_pdf(xml_path, pdf_path)

# Validate
validator = CNBVDocumentValidator()
report = validator.validate_document(pdf_path, xml_path)

if report["overall_score"] >= 60:
    print("✅ Document passed validation")
else:
    print("❌ Document failed validation")
    validator.print_report(report)
```

## 📈 Success Metrics

### Visual Fidelity (Image-based)
- **Target**: ≥70% similarity
- **Achieved**: 95% average
- **Status**: ✅ EXCELLENT

### OCR Validation (Text-based)
- **Target**: ≥60% overall score
- **Status**: 🚧 In Progress
- **Components**:
  - Sections: ≥75%
  - Data presence: ≥3/4
  - Quality: ≥70%
  - Consistency: ≥66%
  - Imperfections: ≥1 type

## 🚧 Known Limitations

1. **OCR Accuracy**: Even GOT-OCR2 may miss some text
   - **Mitigation**: Use lower thresholds (75%, 66%)
   - Accept partial matches for names

2. **Intentional Errors**: OCR may "correct" typos
   - **Mitigation**: Check multiple imperfection types
   - Only require 1 type to be detected

3. **GPU Dependency**: Best results require CUDA
   - **Mitigation**: Graceful CPU fallback
   - Document performance differences

## 📚 References

- GOT-OCR2: https://github.com/stepfun-ai/GOT-OCR-2.0
- Technical Spec: Based on `222AAA-44444444442025.pdf`
- Existing OCR: `Prisma/Code/Src/Python/prisma-ai-extractors/`
- Visual Fidelity: `README_CNBV_FIDELITY.md`
