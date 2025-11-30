# Entity Extraction Methodology for ExxerCube Prisma

**Author:** ExxerCube Development Team
**Date:** November 2025
**Version:** 1.0

---

## Executive Summary

This document describes the methodology developed to extract structured entity catalogs from unstructured PDF documents using a combination of:

1. **Regex Pattern Matching** - Fast, deterministic extraction of known entity patterns
2. **LLM Processing** - Intelligent extraction using Ollama with chunked prompts
3. **Polynomial OCR Enhancement** - 18.4% OCR improvement using trained filters
4. **Fuzzy Deduplication** - RapidFuzz-based similarity matching to clean results

**Results:** From an initial extraction of 3 entities, we achieved **195 unique, clean entities** (65x improvement).

---

## 1. The Problem

The Prisma system requires validated catalogs of Mexican legal entities to:
- Classify incoming requirements by authority type
- Validate authority competence per legal articles
- Route requirements to appropriate departments
- Generate R29 reports with correct authority codes

These catalogs are scattered across official PDF documents that are:
- Sometimes text-based, sometimes scanned images
- Often contain tabular data that OCR struggles with
- May have degraded quality (blur, noise, low contrast)

---

## 2. Methodology Overview

```
┌─────────────────────┐
│  PDF Documents      │
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐     ┌─────────────────────┐
│  Text Extraction    │────▶│  PyPDF2 (native)    │
│  (Primary)          │     │  or                 │
└─────────────────────┘     │  Tesseract + Poly   │
                            │  (OCR with 18.4%    │
                            │   enhancement)      │
                            └─────────┬───────────┘
                                      │
          ┌───────────────────────────┼───────────────────────────┐
          │                           │                           │
          ▼                           ▼                           ▼
┌─────────────────────┐ ┌─────────────────────┐ ┌─────────────────────┐
│  Regex Extraction   │ │  LLM Extraction     │ │  LLM Extraction     │
│  (Fast, Pattern)    │ │  (Chunk 1)          │ │  (Chunk N)          │
└─────────┬───────────┘ └─────────┬───────────┘ └─────────┬───────────┘
          │                       │                       │
          └───────────────────────┴───────────────────────┘
                                  │
                                  ▼
                    ┌─────────────────────────┐
                    │  Combine & Deduplicate  │
                    │  (Simple uniqueness)    │
                    └─────────┬───────────────┘
                              │
                              ▼
                    ┌─────────────────────────┐
                    │  Fuzzy Cleanup          │
                    │  (RapidFuzz 80%+ match) │
                    └─────────┬───────────────┘
                              │
                              ▼
                    ┌─────────────────────────┐
                    │  Clean Entity Catalog   │
                    │  (JSON Output)          │
                    └─────────────────────────┘
```

---

## 3. Component Details

### 3.1 Polynomial OCR Enhancement

When PDFs are scanned images, we apply the polynomial filter model trained in the OCR optimization research (see `OCR_FILTER_OPTIMIZATION_JOURNAL.md`).

**Features extracted from image:**
- `blur_score` - Laplacian variance (higher = sharper)
- `contrast` - Grayscale std deviation
- `noise_estimate` - High-frequency energy
- `edge_density` - Canny edge pixel ratio

**Filters applied:**
- Brightness adjustment
- Contrast enhancement
- Sharpness enhancement
- Unsharp mask

**Result:** 18.4% reduction in OCR edit distance on degraded documents.

### 3.2 Regex Pattern Extraction

Fast, deterministic extraction using compiled patterns:

```python
ENTITY_PATTERNS = [
    # Juzgados
    (r'JUZGADO\s+(?:DE\s+)?(?:CONTROL|CIVIL|PENAL|FAMILIAR|...)...', 'Juzgado'),
    # Tribunales
    (r'TRIBUNAL\s+(?:COLEGIADO|UNITARIO|ELECTORAL|...)...', 'Tribunal'),
    # Secretarías, Consejos, Direcciones, etc.
    ...
]
```

**Advantages:**
- Very fast (milliseconds per document)
- Predictable, deterministic results
- No API calls or external dependencies

**Limitations:**
- Only finds patterns we've defined
- Misses novel entity types
- Can capture noise (addresses, partial names)

### 3.3 LLM Extraction (Chunked)

Ollama with llama3:8b for intelligent extraction:

**Key insights:**
1. **Chunk the text** - 3,000 chars per chunk to avoid timeouts
2. **Use English prompts** - Better model compliance than Spanish
3. **Simple JSON format** - Minimal schema for reliable parsing
4. **Request JSON mode** - `format: "json"` in Ollama API

**Prompt template:**
```
Extract Mexican legal entities from this Spanish text. Return JSON only.

Types: Juzgado, Tribunal, Secretaría, Dirección, Consejo, Comisión, Fiscalía, Sala, Pleno, Centro

Format: {"entidades": [{"nombre": "NAME", "tipo": "TYPE"}]}

Text:
{chunk}
```

### 3.4 Fuzzy Deduplication

RapidFuzz for similarity matching:

```python
from rapidfuzz import fuzz

# Use token_sort_ratio for word-order independence
similarity = fuzz.token_sort_ratio(name1, name2)
if similarity >= 80:
    merge_entities(name1, name2)
```

**Cleaning steps:**
1. Normalize whitespace
2. Remove trailing addresses/garbage
3. Truncate at concatenation points (multiple entities per line)
4. Select best canonical name from similar group

---

## 4. Scripts

### `extract_authorities.py`

Main extraction script with multi-method approach:

```bash
# Run with single model
python extract_authorities.py --model llama3:8b

# Run with multiple models in parallel
python extract_authorities.py --model "llama3:8b,qwen3:8b,phi4"

# Force OCR for all PDFs
python extract_authorities.py --model llama3:8b --ocr

# Skip LLM (regex only)
python extract_authorities.py --skip-llm
```

**Output:** `extracted_authorities.json`

### `cleanup_entities.py`

Fuzzy deduplication and cleaning:

```bash
# Default threshold (85%)
python cleanup_entities.py

# Lower threshold for more aggressive deduplication
python cleanup_entities.py --threshold 80

# Custom input/output files
python cleanup_entities.py --input raw.json --output clean.json
```

**Output:** `extracted_authorities_clean.json`

---

## 5. Entity Catalogs Required for Prisma

Based on the system documentation, the following entity catalogs are needed:

### 5.1 Autoridades Requirentes (Requesting Authorities)

**Priority:** HIGH - Required for requirement classification and routing

| Category | Examples | Source Documents |
|----------|----------|------------------|
| **Hacendarias (Tax)** | SAT, SHCP, Procuraduría Fiscal | SAT catalogs, SHCP directories |
| **Judiciales (Courts)** | Juzgados Federales, Tribunales Colegiados, Suprema Corte | PJF directories ✅ (done) |
| **Ministeriales (Prosecutors)** | FGR, Fiscalías Estatales | FGR structure docs |
| **Seguridad Social** | IMSS, ISSSTE, INFONAVIT | IMSS directories |
| **Reguladores** | CNBV, CONDUSEF, CONSAR, CNSF | CNBV catalogs |
| **Inteligencia** | UIF (Financial Intelligence Unit) | UIF published lists |
| **Electorales** | INE, TEPJF | INE directories |
| **Otras** | PROFECO, SFP, ASF | Various government sites |

### 5.2 Tipos de Requerimiento (Requirement Types)

**Priority:** HIGH - Required for R29 report field "Tipo de Requerimiento"

| Code | Type | Description |
|------|------|-------------|
| 100 | Información | Information request |
| 101 | Aseguramiento | Account freezing/seizure |
| 102 | Desbloqueo | Account unblocking |
| 103 | Transferencia | Fund transfer order |
| 104 | Situación de Fondos | Account status inquiry |

### 5.3 Instituciones Financieras (Financial Institutions)

**Priority:** MEDIUM - Required for routing and validation

| Type | Examples |
|------|----------|
| Banks | BBVA, Santander, Citibanamex, Banorte |
| SOFOMES | Various regulated finance companies |
| Fintech | CoDi participants, digital banks |
| Credit Unions | Cooperativas de ahorro |

### 5.4 Catálogos Geográficos (Geographic Catalogs)

**Priority:** MEDIUM - Required for jurisdiction determination

| Catalog | Source |
|---------|--------|
| Estados (States) | INEGI Catálogo de Entidades Federativas |
| Municipios | INEGI Catálogo de Municipios |
| Circuitos Judiciales | PJF Circuit definitions |

### 5.5 Catálogos Legales (Legal Catalogs)

**Priority:** LOW - Reference data for validation

| Catalog | Purpose |
|---------|---------|
| Artículos Legales | LIC Art. 142, LACP Art. 34, etc. |
| Fundamentos | Legal basis codes |
| Delitos | Crime classifications |

---

## 6. Extraction Plan by Priority

### Phase 1: Judicial Authorities (DONE)
- [x] Tribunales (PJF structure)
- [x] Juzgados Federales
- [x] Salas, Plenos, Centros de Justicia

### Phase 2: Tax & Financial Authorities
- [ ] SAT structure (Administraciones Generales, Locales)
- [ ] SHCP structure (Procuraduría Fiscal, Tesorería)
- [ ] UIF structure
- [ ] CNBV structure

### Phase 3: Prosecutors
- [ ] FGR structure (Fiscalías Especializadas)
- [ ] State prosecutor offices (32 states)
- [ ] FEPADE (electoral crimes)

### Phase 4: Social Security & Regulators
- [ ] IMSS structure (Delegaciones)
- [ ] ISSSTE structure
- [ ] INFONAVIT structure
- [ ] CONDUSEF structure

### Phase 5: Geographic & Reference Data
- [ ] INEGI state/municipality codes
- [ ] Judicial circuit mappings
- [ ] Legal article catalog

---

## 7. Data Model for Entity Catalogs

### Recommended JSON Schema

```json
{
  "catalog_name": "autoridades_judiciales",
  "version": "2025-11-30",
  "source_documents": ["Que-PJF.pdf", "EdoMexico2025.pdf"],
  "extraction_method": "regex+llm+polynomial_ocr",
  "entities": [
    {
      "id": "PJF-001",
      "nombre": "Tribunal Electoral del Poder Judicial de la Federación",
      "nombre_corto": "TEPJF",
      "tipo": "Tribunal",
      "jurisdiccion": "Federal",
      "competencia": ["electoral", "amparo electoral"],
      "nivel": "Federal",
      "estado": null,
      "activo": true,
      "fuente": "Que-PJF.pdf",
      "confianza": 0.95
    }
  ],
  "stats": {
    "total_entities": 195,
    "by_type": {
      "Tribunal": 84,
      "Juzgado": 78,
      "Dirección": 15,
      ...
    }
  }
}
```

### Database Table Structure

```sql
CREATE TABLE AuthorityCatalog (
    Id INT PRIMARY KEY,
    CatalogCode VARCHAR(50) NOT NULL,
    Nombre NVARCHAR(255) NOT NULL,
    NombreCorto NVARCHAR(50),
    Tipo VARCHAR(50) NOT NULL,
    Jurisdiccion VARCHAR(50),
    Estado VARCHAR(50),
    Activo BIT DEFAULT 1,
    FuenteDocumento VARCHAR(255),
    Confianza DECIMAL(3,2),
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME
);

CREATE INDEX IX_AuthorityCatalog_Tipo ON AuthorityCatalog(Tipo);
CREATE INDEX IX_AuthorityCatalog_Jurisdiccion ON AuthorityCatalog(Jurisdiccion);
```

---

## 8. Integration with Prisma System

### 8.1 Authority Validation Service

```csharp
public interface IAuthorityValidationService
{
    Task<AuthorityInfo?> ValidateAuthorityAsync(string authorityName);
    Task<IEnumerable<AuthorityInfo>> SearchAuthoritiesAsync(string searchTerm);
    Task<bool> IsCompetentForRequestTypeAsync(string authorityCode, RequestType type);
}
```

### 8.2 Requirement Classification Enhancement

The extracted authority catalog enables:
- Auto-classification of incoming requirements by authority type
- Validation that requesting authority is competent
- Routing to correct department based on authority category
- R29 report generation with validated authority codes

### 8.3 Adaptive Learning Integration

The entity extraction methodology integrates with Prisma's adaptive learning:
- Track which entities appear most frequently
- Learn new entity name variations
- Flag unknown authorities for manual review
- Periodically refresh catalogs from updated source documents

---

## 9. Maintenance & Updates

### Quarterly Review
- Download latest official directories from government sources
- Re-run extraction pipeline
- Compare with existing catalog (delta analysis)
- Manual review of new/changed entities
- Update database

### Monitoring
- Track "unknown authority" flags in production
- Analyze requirement rejection rates by authority type
- Measure classification confidence over time

---

## 10. Appendix: Command Reference

### Full Extraction Pipeline

```bash
# 1. Activate virtual environment
source .venv-extract/bin/activate

# 2. Run extraction with all methods
python extract_authorities.py --model "llama3:8b"

# 3. Clean and deduplicate
python cleanup_entities.py --threshold 80

# 4. Review results
cat extracted_authorities_clean.json | python -m json.tool | head -100
```

### Performance Metrics

| Metric | Value |
|--------|-------|
| Initial extraction (text only) | 3 entities |
| After regex patterns | 329 entities |
| After LLM chunked processing | 396 entities |
| After fuzzy cleanup | 195 unique entities |
| **Improvement** | **65x** |

### Dependencies

```bash
pip install PyPDF2 requests pytesseract pdf2image pillow \
            opencv-python scikit-learn rapidfuzz
```

---

**Document Status:** Complete
**Next Action:** Begin Phase 2 extraction (SAT, SHCP, UIF)
