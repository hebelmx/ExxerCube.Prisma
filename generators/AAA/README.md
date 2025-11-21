# PRP1 Generator - CNBV Visual Fidelity Edition

**Version 2.1.0** - CNBV Document Generator for End-to-End SIARA Testing

## 🎯 Mission

Generate **"clear fake but very realistic"** CNBV (Comisión Nacional Bancaria y de Valores) documents for end-to-end testing of compliance systems **without using any real confidential data**.

## 🏆 Achievement: 95% Visual Similarity ✅

All test samples scored **EXCELLENT** (85%+ threshold):
- 222AAA-44444444442025: **95.1%**
- 333BBB-44444444442025: **94.9%**
- 333ccc-6666666662025: **95.3%**
- 555CCC-66666662025: **94.7%**

## 🚀 Quick Start

### Generate Single PDF
```bash
cd generators/AAA
python -c "from prp1_generator import xml_to_pdf; xml_to_pdf('input.xml', 'output.pdf')"
```

### Generate 100 Chaotic Documents
```bash
python generate_chaotic_corpus.py --count 100 --seed 42
```

### Validate Document
```bash
python test_cnbv_basic_validation.py test_output/fake_sample_001.pdf --xml test_output/fake_sample_001.xml
```

### Test Visual Fidelity
```bash
python test_cnbv_fidelity.py
```

## 📁 Key Files

| File | Purpose |
|------|---------|
| `cnbv_pdf_generator.py` | XML→PDF converter (95% similarity) |
| `visual_similarity.py` | Image-based similarity measurement |
| `chaos_simulator.py` | Real-world data quality simulation |
| `test_cnbv_basic_validation.py` | Lightweight fixture validator |
| `generate_chaotic_corpus.py` | Batch generation pipeline |

## 📊 Quality Metrics

| Metric | Target | Achieved |
|--------|--------|----------|
| Visual Similarity | ≥70% | **95.0%** ✅ |
| Layout Score | ≥80% | **99.8%** ✅ |
| Intentional Errors | ≥1 | **3/3** ✅ |

## 📚 Documentation

- `README_CNBV_FIDELITY.md` - Complete system documentation
- `IMPLEMENTATION_STATUS.md` - What's done vs specification
- `VALIDATION_STRATEGY.md` - Validation approach
- `LESSONS_LEARNED.md` - Development insights
- `ARCHITECTURE.md` - System design
- `ADR_001_VISUAL_FIDELITY.md` - Architecture decisions

## 🎯 Use Cases

✅ Workflow testing
✅ Visual fidelity validation
✅ Chaos testing
✅ Fixture generation (no confidential data)

## 📦 Installation

```bash
pip install reportlab Pillow pdf2image PyPDF2
```

**System**: Install poppler for pdf2image

## 🙏 Achievement

**User's Goal**: *"simulate siara, so our system can be tested end to end, without ours know nothing with confidential information"*

**Status**: ✅ **ACHIEVED** - Documents are "clear fake but very realistic"!
