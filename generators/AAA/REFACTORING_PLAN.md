# PRP1 Generator Refactoring Plan
## Alignment with ScopeOfTasks.md Requirements

### Current Status
✅ **Completed**:
- Basic generator infrastructure
- Docker orchestration
- Test framework
- Documentation

⚠️ **Needs Refactoring**:
- Dual-document generation (Authority + CNBV)
- CNBV XML schema compliance
- Controlled imperfection injection
- Markdown base format
- Multi-format export pipeline

---

## Phase 1: CNBV Schema Integration ✅ COMPLETE

**Status**: COMPLETE
**File**: `prp1_generator/cnbv_schema.py`

**What was done**:
- Extracted exact schema from real PRP1 samples
- Created `CNBVExpediente` dataclass matching real structure
- Implemented CNBV-compliant XML generation
- Added XML parsing capabilities
- Defined area codes, character types, person types

**Key Features**:
- Matches `http://www.cnbv.gob.mx` namespace
- Supports all CNBV metadata fields
- Handles nil values properly
- Preserves trailing spaces where needed
- Supports nested structures (SolicitudPartes, SolicitudEspecifica)

---

## Phase 2: Dual-Document Generation System

**Status**: PENDING
**Files to create/modify**:
- `prp1_generator/dual_document.py` (NEW)
- `prp1_generator/authority_simulator.py` (NEW)
- `prp1_generator/cnbv_normalizer.py` (NEW)

**Requirements from ScopeOfTasks.md**:

### Document 1: Authority Originating Request
- Simulates document from SAT, FGR, Poder Judicial, UIF, etc.
- Bureaucratic tone with imperfections
- Typos, formatting defects, inconsistencies
- Government-style jargon
- Text representations of stamps/seals

### Document 2: CNBV "Vetted" Request
- Standardized CNBV format
- Cleaner but still realistic imperfections
- CNBV-specific metadata and identifiers
- Preserves essential elements from Document 1
- May reorganize/standardize content

**Implementation Approach**:
1. Create authority-specific templates for different types:
   - SAT (Tax authority)
   - FGR (Attorney General)
   - Poder Judicial (Judicial power)
   - UIF (Financial Intelligence Unit)
   - IMSS (Social Security)

2. Generate Document 1 with:
   - LLM-generated narrative (rushed junior lawyer persona)
   - Intentional imperfections
   - Authority-specific formatting

3. Transform Document 1 → Document 2:
   - Extract core information
   - Apply CNBV normalization
   - Add CNBV metadata
   - Clean up SOME but not all errors

---

## Phase 3: Controlled Imperfection System

**Status**: PENDING
**File to create**: `prp1_generator/imperfections.py`

**Requirements from ScopeOfTasks.md**:
- Typographical mistakes
- Slight inconsistencies
- Formatting flaws
- Redundant phrasing
- OCR-like artifacts
- Misaligned text blocks
- Inconsistent spacing

**Implementation**:
```python
class ImperfectionInjector:
    def __init__(self, error_profile: str, randomness: float = 0.3):
        # error_profile: "rushed_lawyer", "scan_artifact", "ocr_noise"
        pass

    def inject_typos(self, text: str) -> str:
        # s → z, missing accents, extra commas
        pass

    def inject_formatting_errors(self, text: str) -> str:
        # Extra spaces, missing line breaks
        pass

    def inject_inconsistencies(self, doc1: str, doc2: str) -> tuple[str, str]:
        # Slight differences between documents
        pass
```

---

## Phase 4: Markdown-Based Pipeline

**Status**: PENDING
**Files to create/modify**:
- `prp1_generator/markdown_generator.py` (NEW)
- `prp1_generator/markdown_exporter.py` (NEW)

**Requirements from ScopeOfTasks.md**:
> "Every test case is generated into a Markdown (.md) file. Each file contains two sequential documents..."

**Structure**:
```markdown
# Expediente: [Número de Oficio]

## Document 1: Originating Authority Request

**Autoridad**: [Authority Name]
**Fecha**: [Date]
**Expediente**: [Case Number]

[Full authority document with imperfections]

---

## Document 2: CNBV Vetted Request

**Número de Oficio CNBV**: [CNBV Number]
**SIARA**: [SIARA ID]
**Área**: [Area Description]

[Normalized CNBV version]

---

## Metadata

[JSON or YAML metadata block]
```

---

## Phase 5: Multi-Format Export Pipeline

**Status**: PARTIALLY COMPLETE
**Files to enhance**:
- `prp1_generator/fixtures.py` (ENHANCE)
- `prp1_generator/exporters.py` (ENHANCE)

**Current**: Generates PDF, DOCX, PNG, XML
**Needed**:
1. **Markdown** as PRIMARY format
2. **XML** matching CNBV schema exactly (use `cnbv_schema.py`)
3. **DOCX** preserving imperfections
4. **PDF** preserving imperfections
5. **PNG** (optional) with scan artifacts

**Changes needed**:
1. Generate Markdown first
2. Parse Markdown to extract both documents
3. Export each format from Markdown
4. Ensure imperfections are preserved in all formats

---

## Phase 6: Enhanced Context Sampling

**Status**: NEEDS ENHANCEMENT
**File to modify**: `prp1_generator/context.py`

**Add CNBV-specific fields**:
```python
@dataclass
class CNBVContext:
    # All CNBV metadata fields
    Cnbv_NumeroOficio: str
    Cnbv_NumeroExpediente: str
    Cnbv_SolicitudSiara: str
    Cnbv_Folio: str
    Cnbv_OficioYear: str
    Cnbv_AreaClave: str
    Cnbv_AreaDescripcion: str
    Cnbv_FechaPublicacion: str
    Cnbv_DiasPlazo: str

    # Authority info
    AutoridadNombre: str
    AutoridadTipo: str  # SAT, FGR, etc.

    # Parties
    SolicitudPartes: ...
    PersonasSolicitud: ...

    # Instructions
    InstruccionesCuentasPorConocer: str

    # Flags
    TieneAseguramiento: bool
```

**Sampling Strategy**:
1. Select authority type (SAT, FGR, UIF, etc.)
2. Generate CNBV metadata
3. Sample realistic Mexican data (Faker es_MX)
4. Generate narrative with LLM
5. Apply imperfections based on profile

---

## Phase 7: Integration & Testing

**Files to create**:
- `tests/test_cnbv_schema.py`
- `tests/test_dual_document.py`
- `tests/test_imperfections.py`
- `tests/test_markdown_pipeline.py`

**Test Strategy**:
1. **Schema Validation**: Parse real PRP1 samples, regenerate, compare
2. **Round-trip Tests**: Generate XML → parse → regenerate → verify identical
3. **Imperfection Tests**: Verify errors are present but controlled
4. **Format Tests**: Ensure all formats (MD, XML, DOCX, PDF) are generated
5. **Dual-document Tests**: Verify both documents are created correctly

---

## Implementation Order

### Week 1: Core Schema & Dual-Document
1. ✅ CNBV schema module (`cnbv_schema.py`)
2. ⏳ Authority simulator (`authority_simulator.py`)
3. ⏳ CNBV normalizer (`cnbv_normalizer.py`)
4. ⏳ Dual-document orchestrator (`dual_document.py`)

### Week 2: Imperfections & Markdown
5. ⏳ Imperfection injection system (`imperfections.py`)
6. ⏳ Markdown generation (`markdown_generator.py`)
7. ⏳ Markdown export (`markdown_exporter.py`)

### Week 3: Integration & Testing
8. ⏳ Enhance context sampler
9. ⏳ Update main generator
10. ⏳ Update fixtures renderer
11. ⏳ Comprehensive testing
12. ⏳ Documentation updates

---

## Success Criteria

### Must Have
- [x] CNBV XML schema compliance
- [ ] Dual-document generation (Authority + CNBV)
- [ ] Controlled imperfections
- [ ] Markdown primary format
- [ ] Multi-format export (MD, XML, DOCX, PDF)
- [ ] Realistic Mexican data (Faker es_MX)
- [ ] LLM-generated narrative
- [ ] Batch generation (100+ cases)
- [ ] Deterministic/reproducible

### Should Have
- [ ] Authority-specific templates (SAT, FGR, UIF, etc.)
- [ ] Profile-based sampling
- [ ] PNG with scan artifacts
- [ ] Audit logging
- [ ] Comprehensive tests (90%+ coverage)

### Nice to Have
- [ ] Web UI for generation
- [ ] Interactive imperfection tuning
- [ ] Real-time validation against samples
- [ ] Diff view (generated vs. real)

---

## Next Steps

**Immediate Actions**:
1. Create `authority_simulator.py` with templates for:
   - SAT (Tax authority)
   - FGR (Attorney General's Office)
   - UIF (Financial Intelligence Unit)
   - IMSS (Social Security)
   - Judicial authorities

2. Create `cnbv_normalizer.py` to transform Authority → CNBV format

3. Create `dual_document.py` to orchestrate both generations

4. Update `context.py` to use CNBV schema

5. Create tests for each component

**Questions to Resolve**:
- Should we keep existing PDF/DOCX generators or rebuild from Markdown?
- What level of imperfection is acceptable (typo rate, error types)?
- Should we support bidirectional conversion (XML → MD → XML)?

---

## File Structure After Refactoring

```
generators/AAA/
├── prp1_generator/
│   ├── __init__.py
│   ├── config.py
│   ├── context.py              # ENHANCE: Add CNBV fields
│   ├── cnbv_schema.py           # NEW: CNBV XML schema ✅
│   ├── authority_simulator.py   # NEW: Authority doc generator
│   ├── cnbv_normalizer.py       # NEW: Authority → CNBV transformer
│   ├── dual_document.py         # NEW: Orchestrates both docs
│   ├── imperfections.py         # NEW: Error injection
│   ├── markdown_generator.py    # NEW: MD generation
│   ├── markdown_exporter.py     # NEW: MD → other formats
│   ├── ollama_client.py
│   ├── ollama_orchestrator.py
│   ├── fixtures.py              # ENHANCE: Use MD pipeline
│   ├── validators.py
│   ├── exporters.py             # ENHANCE: MD support
│   ├── fallback.py
│   └── authority_templates.py   # ENHANCE: More authorities
│
├── tests/
│   ├── test_cnbv_schema.py      # NEW
│   ├── test_dual_document.py    # NEW
│   ├── test_imperfections.py    # NEW
│   ├── test_markdown_pipeline.py # NEW
│   ├── test_ollama_client.py
│   ├── test_orchestrator.py
│   └── test_integration.py      # ENHANCE
│
├── generate_documents.py        # ENHANCE: Use new pipeline
├── README.md                    # UPDATE
└── REFACTORING_PLAN.md          # This file
```

---

## Risk & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Breaking existing functionality | High | Comprehensive regression tests |
| CNBV schema mismatch | High | Validation against real samples |
| Imperfections too realistic | Medium | Configurable error rates |
| Performance degradation | Medium | Benchmark before/after |
| LLM availability | Low | Fallback templates already exist |

---

*Last Updated: 2025-11-20*
*Status: Phase 1 Complete, Phase 2-7 Pending*
