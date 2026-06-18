# Visual Fidelity Plan - Realistic CNBV/SIARA Document Generator
## Ultra-Realistic Fake Documents for Testing

**Goal**: Generate documents that are **clearly fake but visually indistinguishable** from real CNBV documents for robust testing

---

## 1. Analysis of Real Documents ✅

### A. CNBV Document Structure (From Real Samples)

#### Document Headers (DOCX Layout):
```
VICEPRESIDENCIA DE SUPERVISIÓN DE PROCESOS PREVENTIVOS
Dirección General de Atención a Autoridades
COORDINACIÓN DE ATENCIÓN A AUTORIDADES "A"/"C"
CNBV.4S.1,214-1,"04/06/2025",<2>
```

#### Address Block:
```
MXNALBCO
01 ENERO PISO 1000 PONIENTE
06000 CIUDAD DE MÉXICO
```

#### Metadata Section:
```
Ciudad de México, a 04 de Junio de 2025.
Oficio Núm.: 222/AAA/-4444444444/2025
Folio   Núm.: A/AS1-1111-222222-AAA
Registro: 6789 Año : 2025
```

#### Subject Line:
```
ASUNTO: Para su atención, se remite oficio girado por
[AUTHORITY NAME]
```

#### Attention Section:
```
At'n:
[RECIPIENT NAME]
[TITLE]
```

#### Legal Foundation Paragraph:
```
Con fundamento en lo dispuesto por los artículos 142, segundo y tercer
párrafos, de la Ley de Instituciones de Crédito, 19 de la Ley de la
Comisión Nacional Bancaria y de Valores, y 49, fracciones I y III,
del Reglamento Interior de la Comisión Nacional Bancaria y de Valores...
```

#### Deadline Paragraph:
```
Se le concede a esa Entidad Financiera, un plazo de [X] DIA(S) HABIL(ES),
contado(s) a partir del día hábil siguiente al que surta sus efectos
la notificación del presente, para proporcionar la respuesta...
```

#### Signature Block:
```
Atentamente.
[OFFICIAL NAME]
COORDINADOR DE ATENCIÓN
A AUTORIDADES "A"
[DATE]
```

#### Footer:
```
NOTA: En su contestación, sírvase citar número de oficio y expediente.
[INITIALS]
```

### B. Observed Realistic Imperfections:

**Found in Real Samples**:
1. ✅ Extra spaces: `párrafo s` (should be `párrafos`)
2. ✅ Typos: `indi car` (should be `indicar`)
3. ✅ Inconsistent spacing in addresses
4. ✅ Trailing spaces in XML fields
5. ✅ Mixed capitalization: `At'n:` vs `Atentamente.`
6. ✅ Punctuation errors: `De inmediato` vs `de inmediato`
7. ✅ Name inconsistencies between XML and DOCX
   - XML: `AEROLINEAS PAYASO ORGULLO NACIONAL`
   - DOCX: `EAEROLÍNEAS PAYASO ORGULLO NACIONALIVE, S.A. DE C.V.`
8. ✅ RFC padding with spaces: `"             "` (13 spaces)
9. ✅ Reference padding: `"                         "` (25 spaces)

### C. Existing Artifact Injection (From fixtures.py):

**Already Implemented** ✅:
- Gaussian noise (5% pixel coverage)
- Blur (0.3-1.0 radius)
- Contrast variation (0.8-1.2)
- Brightness variation (0.9-1.1)
- Rotation (-0.8° to +0.8°)
- PDF to PNG conversion at 200 DPI

---

## 2. Document Layout Specifications

### A. DOCX Template Structure

```python
CNBV_DOCX_LAYOUT = {
    "page_size": "Letter (8.5 x 11 inches)",
    "margins": {
        "top": "0.7 inches",
        "bottom": "0.7 inches",
        "left": "0.8 inches",
        "right": "0.8 inches"
    },
    "fonts": {
        "header": "Arial 12pt Bold",
        "body": "Arial 10pt",
        "footer": "Arial 9pt"
    },
    "sections": [
        "header_block",      # VICEPRESIDENCIA...
        "address_block",     # MXNALBCO...
        "metadata_block",    # Ciudad de México...
        "subject_block",     # ASUNTO:...
        "attention_block",   # At'n:...
        "legal_foundation",  # Con fundamento...
        "request_details",   # Body of request
        "deadline",          # Se le concede...
        "closing",           # Sin otro particular...
        "signature_block",   # Atentamente...
        "footer_note"        # NOTA:...
    ]
}
```

### B. PDF Visual Elements

**Must Include**:
1. ✅ LogoMexico.jpg in header (5 copies side-by-side, 1.1" x 0.45" each)
2. ✅ Tables with borders for metadata
3. ✅ Watermarks: "CNBV CONFIDENCIAL" + hash (48pt, 30° rotation, 12% opacity)
4. ✅ CNBV branding colors: #f2f2f2 (header bg), #e6f0ff (table bg)
5. ✅ Professional table styling with gridlines

### C. PNG Scan Simulation

**Pipeline**:
```
PDF (clean) → Convert to PNG (200 DPI) → Apply artifacts → Save PNG
```

**Artifacts** (from existing code):
- Additive noise (40% probability)
- Gaussian blur (50% probability, radius 0.3-1.0)
- Contrast shift (50% probability, 0.8-1.2x)
- Brightness shift (50% probability, 0.9-1.1x)
- Rotation (40% probability, ±0.8°)

---

## 3. Dual-Document Generation Strategy

### Document 1: Authority Originating Request

**Characteristics**:
- Simulates original authority (SAT, FGR, IMSS, UIF, Judicial)
- **More** imperfections than CNBV version
- Authority-specific letterhead
- Less standardized format
- Bureaucratic tone
- Handwritten-style "seals" as text

**Layout**:
```
[AUTHORITY LETTERHEAD]
[Authority Logo/Seal - Text representation]

Oficio No. [AUTHORITY_REF]
Expediente: [AUTHORITY_EXP]
Fecha: [DATE]

[RECIPIENT]
Presente.

[LEGAL FOUNDATION SPECIFIC TO AUTHORITY]

[DETAILED REQUEST WITH TYPOS AND ERRORS]

[SPECIFIC INSTRUCTIONS]

Atentamente,
[AUTHORITY OFFICIAL]
[TITLE]
[SEAL/STAMP - Text]
```

**Imperfection Profile for Authority Doc**:
- Typo rate: 1-2 per paragraph
- Spacing errors: Frequent
- Formatting inconsistencies: High
- Missing accents: Occasional
- Run-on sentences: Common

### Document 2: CNBV Vetted Request

**Characteristics**:
- CNBV standardized format (as analyzed above)
- **Fewer** imperfections (some cleaned up)
- CNBV metadata added
- Professional structure
- Some errors preserved (realistic)

**Transformation from Doc1 → Doc2**:
```python
def transform_authority_to_cnbv(authority_doc):
    """
    Convert authority request to CNBV standard format.
    """
    # 1. Extract core information
    core_info = extract_core_data(authority_doc)

    # 2. Add CNBV metadata
    cnbv_metadata = generate_cnbv_metadata(core_info)

    # 3. Clean up SOME errors (not all)
    cleaned_text = partial_cleanup(authority_doc.text)
    # Fix: 60% of spelling errors
    # Keep: formatting issues, some typos

    # 4. Apply CNBV template
    cnbv_doc = apply_cnbv_template(cnbv_metadata, cleaned_text)

    # 5. Add CNBV-specific sections
    cnbv_doc.add_legal_foundation()
    cnbv_doc.add_deadline_clause()
    cnbv_doc.add_signature_block()

    return cnbv_doc
```

---

## 4. Authority-Specific Templates

### A. IMSS (Social Security - Aseguramiento)

**Profile**:
```python
IMSS_PROFILE = {
    "authority_type": "SUBDELEGACION [N] [LOCATION]",
    "requirement_type": "ASEGURAMIENTO",
    "area_clave": "3",
    "typical_sla": [5, 7],
    "letterhead": "INSTITUTO MEXICANO DEL SEGURO SOCIAL\nCOORDINACION DE COBRANZA",
    "legal_basis": "Artículos 160 CFF, 142 LIC, 157 fracción X",
    "imperfection_bias": [
        "long run-on sentences",
        "bureaucratic repetition",
        "legal jargon overuse"
    ]
}
```

**Template**:
```
INSTITUTO MEXICANO DEL SEGURO SOCIAL
COORDINACION DE COBRANZA
[SUBDELEGACION]

Oficio No.: IMSSCOB/[XX]/[YY]/[NNNNNN]/[YEAR]
Expediente: [EXP_NUM]
Fecha: [DATE]

C. COMISIONADO NACIONAL BANCARIA Y DE VALORES
PRESENTE.

Por medio del presente y con fundamento en el artículo 160 del Código
Fiscal de la Federación, solicito a esa H. Comisión se sirva girar sus
oficios a las instituciones de crédito bajo su vigilancia...

[DETAILED ASEGURAMIENTO REQUEST WITH TYPOS]

Atentamente,
[OFFICIAL NAME]
[TITLE]
```

### B. SAT (Tax Authority - Información)

**Profile**:
```python
SAT_PROFILE = {
    "authority_type": "ADMINISTRACION DESCONCENTRADA DE AUDITORIA FISCAL",
    "requirement_type": "HACENDARIO",
    "area_clave": "1",
    "typical_sla": [10, 15],
    "letterhead": "ADMINISTRACIÓN DESCONCENTRADA DE AUDITORIA FISCAL DE [STATE]",
    "legal_basis": "Artículos 42, 63 CFF",
    "imperfection_bias": [
        "copy-paste errors",
        "numbered lists formatting issues",
        "sector-specific terminology"
    ]
}
```

**Template**:
```
SERVICIO DE ADMINISTRACIÓN TRIBUTARIA
ADMINISTRACIÓN DESCONCENTRADA DE AUDITORIA FISCAL DE [STATE]

Oficio No.: [ADMIN_CODE]/[YEAR]/[NNNNNN]
Expediente: [EXP_NUM]
Asunto: Solicitud de Información

[RECIPIENT]

Por medio del presente y con fundamento en los artículos 42 y 63 del
Código Fiscal de la Federación, solicito información de:

Sector: [SECTOR]
1. [ITEM 1 WITH TYPOS]
2. [ITEM 2 WITH FORMATTING ERRORS]
...

[SPECIFIC INSTRUCTIONS]

Atentamente,
[OFFICIAL NAME]
[TITLE]
```

### C. UIF (Financial Intelligence - Bloqueo/Desbloqueo)

**Profile**:
```python
UIF_PROFILE = {
    "authority_type": "UNIDAD DE INTELIGENCIA FINANCIERA",
    "requirement_type": "Operaciones Ilícitas",
    "area_clave": "5",
    "typical_sla": [1, 3],
    "letterhead": "SECRETARÍA DE HACIENDA Y CRÉDITO PÚBLICO\nUNIDAD DE INTELIGENCIA FINANCIERA",
    "legal_basis": "Ley Federal para la Prevención e Identificación de Operaciones con Recursos de Procedencia Ilícita",
    "imperfection_bias": [
        "urgent tone",
        "ALL CAPS sections",
        "reference to acuerdos/listas"
    ]
}
```

### D. Judicial (Courts - Embargo/Información)

**Profile**:
```python
JUDICIAL_PROFILE = {
    "authority_type": "JUZGADO [TYPE] DE [JURISDICTION]",
    "requirement_type": "JUDICIAL",
    "area_clave": "6",
    "typical_sla": [3, 5],
    "letterhead": "PODER JUDICIAL\n[JURISDICTION]\n[COURT NAME]",
    "legal_basis": "Código de Procedimientos Civiles/Penales",
    "imperfection_bias": [
        "legalese overuse",
        "case law references",
        "procedural formality"
    ]
}
```

---

## 5. Imperfection Injection System

### A. Controlled Error Types

```python
class ImperfectionEngine:
    """Inject realistic, controlled errors into documents."""

    ERROR_TYPES = {
        # Typographical
        "typo_s_to_z": {"buscar": "buzcar", "pesos": "pezos"},
        "typo_missing_accent": {"información": "informacion", "número": "numero"},
        "typo_extra_space": {"párrafos": "párrafo s", "indicar": "indi car"},
        "typo_double_letter": {"atención": "attención", "solicitud": "sollicitud"},

        # Grammatical
        "grammar_agreement": {"los oficios": "el oficios"},
        "grammar_tense": {"solicito": "solicitó"},

        # Formatting
        "format_spacing": Add random double spaces,
        "format_linebreak": Remove/add unexpected linebreaks,
        "format_capitalization": Random caps: "IMPORTANTE" vs "importante",

        # Semantic (minor)
        "semantic_repetition": Repeat phrases slightly differently,
        "semantic_redundancy": "por favor sírvase" + "le solicito",

        # OCR-like
        "ocr_0_to_O": "2025" → "2O25",
        "ocr_1_to_l": "1111" → "ll11",
        "ocr_rn_to_m": "firma" → "fimna"
    }

    def inject_errors(self, text: str, profile: str, rate: float = 0.15):
        """
        Inject errors based on profile.

        Args:
            text: Clean text
            profile: "authority" (more errors) or "cnbv" (fewer errors)
            rate: Error probability per eligible token

        Returns:
            Text with realistic imperfections
        """
        if profile == "authority":
            rate *= 1.5  # 50% more errors in authority docs
        elif profile == "cnbv":
            rate *= 0.6  # 40% fewer errors in CNBV docs

        # Apply errors...
```

### B. Error Distribution Strategy

**Authority Document** (Document 1):
- Typos: 1.5-2.0 per 100 words
- Spacing errors: 1.0 per 100 words
- Formatting issues: 0.5 per 100 words
- Missing accents: 0.3 per 100 words

**CNBV Document** (Document 2):
- Typos: 0.5-0.8 per 100 words (reduced by 60%)
- Spacing errors: 0.3 per 100 words
- Formatting issues: 0.2 per 100 words
- Missing accents: 0.1 per 100 words (most fixed)

### C. Preservation of Critical Fields

**Never inject errors in**:
- RFC numbers
- CURP numbers
- Amounts (montos)
- Dates (keep valid)
- Case numbers (expediente)
- Legal article citations (keep valid)

**Always keep realistic in**:
- Company names (can have typos)
- Addresses (can have formatting issues)
- Person names (can have capitalization issues)
- Narrative text (most errors here)

---

## 6. Visual Rendering Pipeline

### A. Multi-Format Generation Flow

```
┌─────────────────────────────────────────────────┐
│ 1. GENERATE METADATA (CNBV Schema)              │
│    - Sample from entities.json                  │
│    - Use Faker (es_MX)                          │
│    - Apply RequirementProfile                   │
└────────────────┬────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────┐
│ 2. GENERATE AUTHORITY DOCUMENT (Doc 1)          │
│    - Select authority template                  │
│    - Generate narrative with LLM                │
│    - Inject errors (high rate)                  │
│    - Create as Markdown + DOCX                  │
└────────────────┬────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────┐
│ 3. TRANSFORM TO CNBV DOCUMENT (Doc 2)           │
│    - Extract core information                   │
│    - Apply CNBV template                        │
│    - Clean SOME errors (60%)                    │
│    - Add CNBV metadata                          │
│    - Create as Markdown + DOCX                  │
└────────────────┬────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────┐
│ 4. GENERATE CNBV XML                            │
│    - Use cnbv_schema.py                         │
│    - Populate from Doc 2 metadata               │
│    - Apply realistic spacing/padding            │
└────────────────┬────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────┐
│ 5. RENDER PROFESSIONAL PDF                      │
│    - Apply CNBV layout                          │
│    - Add LogoMexico.jpg                         │
│    - Add watermarks                             │
│    - Professional tables                        │
└────────────────┬────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────┐
│ 6. CONVERT TO REALISTIC PNG                     │
│    - PDF → PNG (200 DPI)                        │
│    - Apply scan artifacts                       │
│    - Noise, blur, rotation                      │
└─────────────────────────────────────────────────┘
```

### B. Output File Structure

```
output/
├── REQ0001/
│   ├── 01_authority_original.md      # Authority doc (markdown)
│   ├── 01_authority_original.docx    # Authority doc (word)
│   ├── 02_cnbv_vetted.md             # CNBV doc (markdown)
│   ├── 02_cnbv_vetted.docx           # CNBV doc (word)
│   ├── 02_cnbv_vetted.xml            # CNBV XML (SIARA format)
│   ├── 02_cnbv_vetted.pdf            # Professional PDF
│   └── 02_cnbv_vetted.png            # Scanned simulation
└── corpus.json                        # All metadata
```

---

## 7. Implementation Checklist

### Phase 1: Core Infrastructure ✅
- [x] CNBV XML schema (`cnbv_schema.py`)
- [x] Existing artifact injection (`fixtures.py`)
- [x] Parse real samples (`parse_prp1_documents.py`)

### Phase 2: Authority Templates
- [ ] Create `authority_templates.py` with:
  - [ ] IMSS template
  - [ ] SAT template
  - [ ] UIF template
  - [ ] Judicial template
  - [ ] FGR template

### Phase 3: Imperfection System
- [ ] Create `imperfections.py` with:
  - [ ] Typo injection
  - [ ] Spacing errors
  - [ ] Formatting issues
  - [ ] OCR-like artifacts
  - [ ] Controlled error rates

### Phase 4: Dual-Document Generator
- [ ] Create `dual_document_generator.py` with:
  - [ ] Authority document generation
  - [ ] CNBV transformation
  - [ ] Partial error cleanup
  - [ ] Metadata preservation

### Phase 5: Visual Rendering
- [ ] Enhance `fixtures.py` with:
  - [ ] CNBV DOCX layout (exact match to samples)
  - [ ] LogoMexico.jpg integration
  - [ ] Professional PDF with watermarks
  - [ ] Tables and formatting

### Phase 6: Integration
- [ ] Update `generate_documents.py` to use dual-document pipeline
- [ ] Update `context.py` to use CNBV schema
- [ ] Add Markdown export
- [ ] Test against real samples

### Phase 7: Validation
- [ ] Visual comparison tool
- [ ] Layout diff checker
- [ ] Error rate validator
- [ ] Format integrity tests

---

## 8. Quality Metrics

### Visual Fidelity Score

```python
def calculate_fidelity_score(generated_doc, real_sample):
    """
    Score from 0-100 on visual similarity.

    Metrics:
    - Layout match: 30 points
    - Font/spacing match: 20 points
    - Content structure: 20 points
    - Artifact realism: 15 points
    - Error realism: 15 points
    """
    score = 0
    score += layout_match(generated_doc, real_sample) * 30
    score += font_spacing_match(generated_doc, real_sample) * 20
    score += structure_match(generated_doc, real_sample) * 20
    score += artifact_realism(generated_doc.png) * 15
    score += error_realism(generated_doc.text) * 15
    return score

# Target: 85-95 (clearly fake but visually realistic)
# Avoid: >95 (too perfect, suspicious)
# Avoid: <75 (obviously fake, poor testing value)
```

---

## 9. Testing Strategy

### A. Validation Against Real Samples

```python
def validate_against_real_samples():
    """
    Compare generated docs to real PRP1 samples.
    """
    real_samples = load_prp1_samples("Prisma/Fixtures/PRP1/")

    for sample in real_samples:
        generated = generate_similar_document(sample.metadata)

        # Visual checks
        assert layout_matches(generated.docx, sample.docx)
        assert has_realistic_errors(generated.text)
        assert xml_schema_valid(generated.xml)
        assert pdf_has_logo(generated.pdf)
        assert png_has_artifacts(generated.png)

        # Fidelity score
        score = calculate_fidelity_score(generated, sample)
        assert 85 <= score <= 95, f"Fidelity score {score} out of range"
```

### B. C# System Integration Test

```python
def test_csharp_system_compatibility():
    """
    Verify generated docs work with C# processing system.
    """
    generated_docs = generate_batch(count=10)

    for doc in generated_docs:
        # Test XML parsing
        csharp_result = csharp_xml_parser.parse(doc.xml)
        assert csharp_result.success

        # Test OCR processing
        ocr_result = csharp_ocr_service.extract(doc.png)
        assert ocr_result.has_errors  # Should handle errors gracefully

        # Test metadata extraction
        metadata = csharp_extractor.extract(doc.docx)
        assert metadata.expediente == doc.metadata.expediente
```

---

## 10. Success Criteria

✅ **Documents must be**:
1. Clearly fake (synthetic data, no real people/companies)
2. Visually indistinguishable from real CNBV documents
3. Contain realistic imperfections
4. Compatible with C# processing system
5. Suitable for robust testing

✅ **System must**:
1. Generate both authority and CNBV versions
2. Apply controlled, realistic errors
3. Use LogoMexico.jpg in PDFs
4. Create scan artifacts in PNGs
5. Export to all required formats (MD, XML, DOCX, PDF, PNG)
6. Be deterministic (same seed → same output)
7. Scale to 100+ documents
8. Pass visual fidelity validation

---

*Next Step: Implement Phase 2 (Authority Templates) based on this plan*
