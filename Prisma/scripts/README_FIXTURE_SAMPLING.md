# Test Fixture Sampling Scripts

**Purpose**: Intelligent document sampling from KpiExxerpro/Fixture for integration test suites.

**Philosophy**: Use statistical distributions to create realistic, representative test samples that balance coverage, performance, and repository size constraints.

---

## Available Scripts

### 1. `sample_kpi_fixtures.py` - 1-Sigma Normal Distribution Sampler

**Best for**: Helix OCR tests, balanced document size distribution

**Strategy**: Selects documents within ±1 standard deviation of mean size (68% of normal distribution)

**Defaults**:
- Target: 40 files
- Target size: ~20MB total
- Extensions: `.pdf`, `.png`, `.jpg`, `.jpeg`, `.tiff`, `.tif`
- Max individual file size: 50MB
- Output naming: `helix_001.pdf`, `helix_002.png`, etc.

**Usage**:
```bash
# Default (40 files, 20MB)
python scripts/sample_kpi_fixtures.py

# Custom target count and size (50 files, max 4MB constraint)
python scripts/sample_kpi_fixtures.py --target-count 50 --target-size-mb 4

# Dry run (preview without copying)
python scripts/sample_kpi_fixtures.py --dry-run

# Custom source/target directories
python scripts/sample_kpi_fixtures.py \
  --source-dir "KpiExxerpro/Documents" \
  --target-dir "code/src/tests/04IntegrationTests/ExxerAI.Datastream.Integration.Tests/Fixtures"

# Reproducible sampling with seed
python scripts/sample_kpi_fixtures.py --seed 42
```

**How it works**:
1. Scans source directory recursively for valid documents
2. Calculates mean and standard deviation of file sizes
3. Filters documents within ±1σ of mean (removes outliers)
4. Randomly samples target count from filtered set
5. Sorts by extension then size for consistent numbering
6. Copies with sequential naming

**Output example**:
```
📊 Size Distribution:
   Total documents: 1247
   Mean size: 2.34 MB
   Std deviation: 1.12 MB
   Min size: 12.45 KB
   Max size: 47.89 MB

🎯 1-Sigma Selection (±1.12 MB):
   Documents in range: 823
   Size range: 1.22 MB - 3.46 MB

✅ Selected 40 documents:
   Total size: 19.87 MB
   Target size: 20.00 MB
   Difference: -0.13 MB

📋 Breakdown by extension:
   .pdf: 28 files
   .png: 8 files
   .tiff: 4 files
```

---

### 2. `select_pdf_fixtures_sigmoid.py` - Sigmoid Distribution Sampler

**Best for**: PdfPig tests, sophisticated size categorization, PDF-only

**Strategy**: Uses inverse sigmoid function for natural mid-range bias with long tails

**Defaults**:
- Target: 100 files
- Extensions: `.pdf` only
- Max individual file size: 100MB
- Output naming: `test_001_medium_524288.pdf` (includes category and size)

**Size Categories**:
- **Tiny**: < 10 KB (10% of sample)
- **Small**: 10 KB - 100 KB (25% of sample)
- **Medium**: 100 KB - 1 MB (35% of sample)
- **Large**: 1 MB - 10 MB (20% of sample)
- **XLarge**: > 10 MB (10% of sample)

**Usage**:
```bash
# Default (100 PDFs)
python scripts/select_pdf_fixtures_sigmoid.py

# Note: Script has hardcoded paths, modify for different targets
# Source: F:/Dynamic/ExxerAi/ExxerAI/KpiExxerpro/Fixture
# Target: code/src/tests/03AdapterTests/ExxerAI.Axis.Adapter.PdfPig.Tests/Fixtures
```

**How it works**:
1. Recursively finds all PDF documents
2. Categorizes by size (tiny/small/medium/large/xlarge)
3. Uses sigmoid-weighted random selection per category
4. Ensures bell curve distribution favoring medium sizes
5. Copies with metadata-rich filenames (category + size)
6. Generates `fixtures_metadata.json` with complete stats

**Output example**:
```
📊 Found 2,341 PDF documents

📈 Size Distribution:
  TINY    :  234 files (min:        128 bytes, max:      9,876 bytes)
  SMALL   :  612 files (min:     10,240 bytes, max:     98,304 bytes)
  MEDIUM  :  891 files (min:    102,400 bytes, max:    987,654 bytes)
  LARGE   :  456 files (min:  1,048,576 bytes, max:  9,437,184 bytes)
  XLARGE  :  148 files (min: 10,485,760 bytes, max: 52,428,800 bytes)

🎯 Selected 100 documents using sigmoid distribution

📊 Selection Distribution:
  TINY    :  10 files ( 10.0%)
  SMALL   :  25 files ( 25.0%)
  MEDIUM  :  35 files ( 35.0%)
  LARGE   :  20 files ( 20.0%)
  XLARGE  :  10 files ( 10.0%)

📊 Final Statistics:
  Total Documents: 100
  Total Size: 287,453,184 bytes (274.17 MB)
  Average Size: 2,874,531 bytes (2.74 MB)
  Min Size: 2,048 bytes
  Max Size: 45,678,901 bytes (43.56 MB)
```

**Metadata file** (`fixtures_metadata.json`):
```json
{
  "total_documents": 100,
  "documents": [
    {
      "index": 1,
      "filename": "test_001_tiny_2048.pdf",
      "original_path": "KpiExxerpro/Fixture/...",
      "size_bytes": 2048,
      "size_category": "tiny",
      "extension": ".pdf"
    }
  ],
  "statistics": {
    "tiny": 10,
    "small": 25,
    "medium": 35,
    "large": 20,
    "xlarge": 10,
    "total_bytes": 287453184,
    "min_size": 2048,
    "max_size": 45678901,
    "avg_size": 2874531.84,
    "extensions": { ".pdf": 100 }
  }
}
```

---

## Comparison Matrix

| Feature | 1-Sigma Sampler | Sigmoid Sampler |
|---------|----------------|-----------------|
| **Distribution** | Normal (68% within 1σ) | Sigmoid (bell curve, long tails) |
| **File types** | Multi-format (PDF, PNG, JPG, TIFF) | PDF only |
| **Target count** | 40 (configurable) | 100 (hardcoded) |
| **Size categorization** | Statistical (mean ± stddev) | Fixed ranges (tiny/small/medium/large/xlarge) |
| **Naming** | Sequential (`helix_001.pdf`) | Metadata-rich (`test_001_medium_524288.pdf`) |
| **Metadata output** | Console only | JSON file with complete stats |
| **Flexibility** | High (CLI args) | Low (hardcoded paths) |
| **Best use case** | Helix OCR, general testing | PdfPig PDF-specific testing |

---

## Example: Sampling 50 Files (Max 4MB) for Datastream Tests

**Requirement**: 50 PDF fixtures, max 4MB total, for Datastream integration tests

```bash
# Step 1: Use 1-sigma sampler with size constraint
python scripts/sample_kpi_fixtures.py \
  --source-dir "KpiExxerpro/Fixture" \
  --target-dir "code/src/tests/04IntegrationTests/ExxerAI.Datastream.Integration.Tests/Fixtures" \
  --target-count 50 \
  --target-size-mb 4 \
  --seed 42

# Step 2: Verify output
ls -lh code/src/tests/04IntegrationTests/ExxerAI.Datastream.Integration.Tests/Fixtures/
du -sh code/src/tests/04IntegrationTests/ExxerAI.Datastream.Integration.Tests/Fixtures/

# Expected output:
# Total: 50 files
# Size: ~3.8-4.2 MB (algorithm aims close to target)
# Naming: helix_001.pdf, helix_002.pdf, ..., helix_050.pdf
```

**Why 1-sigma sampler for this case?**
- ✅ Flexible CLI args (can set target count and size)
- ✅ Multi-format support (if we need PNG/JPG later)
- ✅ Size constraint enforcement (4MB limit)
- ✅ Simple sequential naming (easier test debugging)

---

## Integration with Test Projects

### Helix Adapter Tests (Already implemented)
```bash
python scripts/sample_kpi_fixtures.py
# Output: code/src/tests/04AdapterTests/ExxerAI.Helix.Adapter.Tests/Fixtures/
```

### PdfPig Adapter Tests (Already implemented)
```bash
python scripts/select_pdf_fixtures_sigmoid.py
# Output: code/src/tests/03AdapterTests/ExxerAI.Axis.Adapter.PdfPig.Tests/Fixtures/
```

### Datastream Integration Tests (Proposed)
```bash
python scripts/sample_kpi_fixtures.py \
  --target-dir "code/src/tests/04IntegrationTests/ExxerAI.Datastream.Integration.Tests/Fixtures" \
  --target-count 50 \
  --target-size-mb 4
```

### OpenXml Adapter Tests (Needs implementation)
```bash
# Will need similar script for .docx, .xlsx files
# TODO: Create select_openxml_fixtures.py
```

---

## Statistical Background

### 1-Sigma Distribution (Normal/Gaussian)
- **68.27%** of data falls within ±1σ
- **95.45%** of data falls within ±2σ
- **99.73%** of data falls within ±3σ

**Why 1σ?**
- Excludes extreme outliers (very small/large files)
- Provides good coverage of typical file sizes
- Computationally simple
- Statistically sound

### Sigmoid Distribution
```
f(x) = 1 / (1 + e^(-x))
```

**Properties**:
- **S-shaped curve**: Smooth transition from 0 to 1
- **Mid-range bias**: Values near mean are more likely
- **Long tails**: Extreme values still possible (unlike hard cutoffs)
- **Inverse mapping**: Used to generate random values with desired distribution

**Why sigmoid?**
- More sophisticated than uniform random
- Natural distribution (appears in biology, physics)
- Balances coverage with realism
- Better for size-categorized sampling

---

## Best Practices

### 1. Always use `--dry-run` first
```bash
python scripts/sample_kpi_fixtures.py --dry-run
```
Preview selections before copying.

### 2. Use seeds for reproducibility
```bash
python scripts/sample_kpi_fixtures.py --seed 42
```
Same seed = same sample (critical for debugging).

### 3. Validate output
```bash
# Check file count
ls code/src/tests/.../Fixtures/ | wc -l

# Check total size
du -sh code/src/tests/.../Fixtures/

# Inspect metadata (if available)
cat code/src/tests/.../Fixtures/fixtures_metadata.json | jq '.statistics'
```

### 4. Document fixture strategy in test README
```markdown
## Test Fixtures

**Source**: `KpiExxerpro/Fixture` (2,341 documents)
**Sampling**: 1-sigma distribution, 50 files, ~4MB
**Seed**: 42 (reproducible)
**Command**: `python scripts/sample_kpi_fixtures.py --target-count 50 --target-size-mb 4 --seed 42`
```

---

## Troubleshooting

### "No documents found"
```bash
# Check source directory exists
ls -la KpiExxerpro/Fixture

# Check path is correct (absolute vs relative)
pwd
python scripts/sample_kpi_fixtures.py --source-dir "$(pwd)/KpiExxerpro/Fixture"
```

### "Selected size exceeds target"
- Algorithm doesn't guarantee exact size match
- Uses ±10% tolerance typically
- If critical, filter by individual file size first

### "Not enough documents in 1-sigma range"
- Source has high variance (many outliers)
- Algorithm falls back to using all documents
- Consider using sigmoid sampler instead

---

## Future Enhancements

### Potential improvements
- [ ] Add support for `.docx`, `.xlsx` in sigmoid sampler
- [ ] Implement 2-sigma and 3-sigma options
- [ ] Add histogram visualization (matplotlib)
- [ ] Support stratified sampling (by document type/category)
- [ ] Add fixture validation (check for corruption)
- [ ] Implement fixture rotation (prevent overfitting to same samples)

---

**Last Updated**: 2025-11-10
**Maintainer**: ExxerAI Development Team
**Related**: `FIXTURE_PROPAGATION_QUICK_START.md`, `baseline_test_suite_status_2025_11_10` (Serena memory)
