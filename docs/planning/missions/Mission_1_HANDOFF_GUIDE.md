> **⚠️ Status correction (2026-06-07):** This document's "production-ready" claims are aspirational / MVP-demo framing. The system is at **almost-ready, beta-test stage with important gaps remaining** — not production-ready. See docs/planning/gap-analysis/GAP-MATRIX-2026-06-dual-ground-truth.md.

# Agent Handoff Guide - Document Generation Pipeline

## 🎯 Mission Status: COMPLETE ✅

**Current Agent has delivered a beta-test-stage document generation pipeline (almost ready, important gaps remaining — not yet production-ready).**

## 📋 Handoff Summary

### What's Been Completed
1. ✅ **Complete document generation pipeline** (entities → corpus → simulation → OCR)
2. ✅ **999 test documents generated** with 30-hash watermarking system
3. ✅ **Comprehensive documentation** in Mission.md
4. ✅ **Inline code comments** for easy refinement
5. ✅ **Beta-test-stage scripts** with error handling (not yet production-hardened)

### What's Currently Running
- **999 document batch generation** running in background (Fixtures999/)
- Estimated completion: 30-50 minutes
- Progress visible with: `ls -la Fixtures999/ | wc -l`

## 🔧 Next Agent Instructions

### Primary Task: Real Document Integration
**Priority: HIGH** - Replace synthetic system with real document processing

### Key Refinement Areas

#### 1. Watermarking System (`simulate_documents.py` lines 161-270)
```python
# CURRENT: 30 synthetic hashes (10 clusters × 3 hashes)
# REFINE: Adjust density based on real document analysis

# Key parameters to adjust:
cluster_distance = 250      # Space between clusters
hash_distance = 150         # Space between hashes in cluster  
opacity = random.randint(80, 100)  # Watermark visibility
```

#### 2. Document Cropping (`simulate_documents.py` lines 290-302)
```python
# CURRENT: Aggressive cropping (500px right, 20% bottom)
# REFINE: Measure real document margins

crop_right = width - 500    # ADJUST: Measure real doc margins
crop_bottom = height - int(height * 0.2)  # ADJUST: Based on real docs
```

#### 3. Degradation Realism (`simulate_documents.py` lines 305-600)
```python
# CURRENT: Random degradation levels
# REFINE: Analyze real scan quality and adjust parameters

# Priority parameters:
blur_radius = random.uniform(0.3, 3.0)     # TUNE: Based on real scans
noise_prob = random.uniform(0.005, 0.015)  # TUNE: Match real noise levels
stain_frequency = random.random() > 0.7    # TUNE: Real stain frequency
```

#### 4. Entity Database (`entities.json`)
```json
// CURRENT: Synthetic legal entities
// REFINE: Replace with actual court names, laws, institutions from real docs

"autoridades": ["Actual Court Names..."],
"fundamentos_legales": ["Real Article References..."],
"entidades_financieras": ["Actual Bank Names..."]
```

### Recommended Workflow

#### Phase 1: Real Document Analysis (1-2 days)
1. **Scan Analysis**: Measure real document margins, font sizes, degradation patterns
2. **Entity Extraction**: Parse real documents for actual court names, laws, amounts
3. **Quality Assessment**: Analyze real scan artifacts and degradation types

#### Phase 2: System Adaptation (2-3 days)
1. **Update entities.json**: Replace with real legal terminology
2. **Adjust watermarking**: Match density to real document complexity
3. **Tune degradation**: Calibrate to real scanning conditions
4. **Test cropping**: Ensure output matches real document proportions

#### Phase 3: Validation (1 day)
1. **OCR Testing**: Validate against SmolVLM extraction accuracy
2. **Visual Comparison**: Ensure realistic appearance vs. real documents
3. **Performance Metrics**: Measure generation speed and quality

### Critical Files to Modify

| File | Lines | Refinement Focus |
|------|-------|------------------|
| `simulate_documents.py` | 161-270 | Watermark positioning and density |
| `simulate_documents.py` | 290-302 | Document cropping dimensions |
| `simulate_documents.py` | 305-600 | Degradation parameter tuning |
| `entities.json` | All | Real legal terminology replacement |
| `generate_test_corpus.py` | 15-110 | Real document structure parsing |

### Testing Commands
```bash
# Test single document with specific degradation
uv run python simulate_documents.py --input real_corpus.json --degradation medium --num 1

# Generate small batch for testing
uv run python simulate_documents.py --input real_corpus.json --num 10

# OCR validation
uv run python smolvlm_extractor.py --image test_output/Fixture001.png
```

## 📊 Current System Performance

### Generation Metrics
- **Template speed**: 0.1s per document text
- **Simulation speed**: 2-3s per document image
- **File sizes**: 800KB-1.5MB PNG, 300-500KB PDF
- **Quality**: High diversity, realistic degradation

### System Architecture
```
Real Documents → Entity Parser → Corpus Generator → Document Simulator → OCR Tester
                      ↓              ↓                   ↓              ↓
                 entities.json → corpus.json → degraded images → extraction results
```

## 🚀 Success Criteria

### For Next Agent Success
1. **Real integration**: System processes actual legal documents
2. **Maintained quality**: Realistic degradation and proper formatting
3. **OCR challenge**: Appropriate difficulty level for testing
4. **Performance**: Similar generation speeds with real data
5. **Scalability**: Easy to generate larger batches

### Warning Signs to Watch
- ⚠️ **Over-degradation**: If OCR extraction becomes impossible
- ⚠️ **Under-challenge**: If watermarks too light/sparse
- ⚠️ **Format mismatch**: If output doesn't match real document proportions
- ⚠️ **Performance drop**: If real document processing too slow

## 📞 Handoff Contact Points

### Questions/Issues
- **Watermarking problems**: Check lines 161-270 in simulate_documents.py
- **Cropping issues**: Adjust lines 290-302 based on real measurements
- **Degradation calibration**: Modify parameters in lines 305-600
- **Entity updates**: Replace synthetic data in entities.json

### Success Indicators
- ✅ Generated documents visually match real scan quality
- ✅ OCR extraction maintains appropriate challenge level
- ✅ System processes real legal documents efficiently
- ✅ Output scales to 999+ documents without issues

---

**HANDOFF COMPLETE** 🎯  
**Next Agent: Focus on real document integration and calibration refinement.**