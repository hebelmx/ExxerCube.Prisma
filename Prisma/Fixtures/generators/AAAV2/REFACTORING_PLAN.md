# CNBV/SIARA Fixture Generator - Refactoring Plan
## Using Proven HTML+Chrome PDF Generation Approach

**Version:** 2.0
**Date:** 2025-01-21
**Target:** E2E Testing for Banking Authority Requirements Processing System

---

## 🎯 Executive Summary

Replace the complex ReportLab-based PDF generation with our proven HTML+Chrome headless approach while maintaining the sophisticated data generation and validation capabilities of the existing system.

**Key Innovation:** Leverage what we learned from AAAV2 - HTML templates are easier to maintain, Chrome provides perfect rendering, and the watermark/styling is simple CSS.

---

## 📋 Current System Analysis

### What We Keep (Good Architecture)
✅ **XML Schema Layer** (`cnbv_schema.py`) - Pure dataclasses
✅ **Validation Layer** (`validators.py`) - Schema validation
✅ **Chaos Simulator** (`chaos_simulator.py`) - Data perturbation
✅ **Context Sampler** - Entity and profile sampling
✅ **Ollama Integration** - LLM text generation

### What We Replace (Complex/Brittle)
❌ **ReportLab PDF Generator** (`cnbv_pdf_generator.py`) - Complex, hard to maintain
❌ **Manual layout calculation** - Error-prone positioning
❌ **Image handling complexity** - Logo repetition difficult

### What We Add (Missing Capabilities)
➕ **DOCX Export** - Required for full E2E testing
➕ **Spanish Faker** - Mexican-specific realistic data
➕ **Legal Article Catalog** - Structured legal references
➕ **HTML Template Engine** - Simplified styling

---

## 🏗️ New Architecture Design

```
┌─────────────────────────────────────────────────────────────┐
│         CNBV E2E Fixture Generator v2.0                     │
│         (HTML-First Architecture)                            │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌──────────────┐      ┌──────────────┐     ┌──────────────┐
│ Data Layer   │      │ Template     │     │ Export       │
│              │      │ Layer        │     │ Layer        │
└──────────────┘      └──────────────┘     └──────────────┘
        │                     │                     │
   ┌────┴────┐           ┌────┴────┐          ┌────┴────┐
   │         │           │         │          │         │
   ▼         ▼           ▼         ▼          ▼         ▼
┌─────┐  ┌─────┐   ┌─────┐  ┌─────┐   ┌─────┐  ┌─────┐
│Faker│  │ LLM │   │ HTML│  │ CSS │   │ PDF │  │DOCX │
│es_MX│  │Ollama│  │Jinja│  │Water│   │Chrome│ │python│
│     │  │     │   │2    │  │mark │   │Head │  │-docx │
└─────┘  └─────┘   └─────┘  └─────┘   └─────┘  └─────┘
```

---

## 📦 Module Breakdown

### 1. Data Generation Module (`data_generator.py`)

**Purpose:** Generate realistic Mexican banking data

**Components:**
```python
class MexicanDataGenerator:
    """Generate realistic Mexican banking data using Faker + custom catalogs"""

    def __init__(self, locale='es_MX'):
        self.faker = Faker(locale)
        self.legal_catalog = LegalArticleCatalog()

    def generate_person(self) -> dict:
        """Mexican person with RFC, CURP"""

    def generate_company(self) -> dict:
        """Mexican company with RFC"""

    def generate_authority(self) -> dict:
        """Government authority (SAT, FGR, UIF, Judge)"""

    def generate_requirement(self, type: str) -> dict:
        """Requirement with Mexican legal framework"""
```

**Data Sources:**
- **Faker(es_MX):** Names, addresses, phones
- **Custom Catalogs:**
  - Mexican municipalities & states
  - Banking institutions
  - Legal articles by type
  - Common typos/errors

---

### 2. Legal Catalog Module (`legal_catalog.py`)

**Purpose:** Structured legal article references

```python
class LegalArticleCatalog:
    """Catalog of Mexican banking/legal articles"""

    ARTICLES = {
        'fiscal': [
            'Art. 42 Código Fiscal de la Federación',
            'Art. 145, 151, 152, 153, 154 CFF',
        ],
        'judicial': [
            'Art. 142 Ley de Instituciones de Crédito',
            'Art. 16 Constitución Política',
        ],
        'pld': [
            'Art. 400 bis Código Penal Federal',
            'LFPIORPI artículos 17, 18',
        ],
        'aseguramiento': [
            'Art. 160 CFF',
            'Art. 40 Código Nacional de Procedimientos Penales',
        ]
    }

    def get_random_articles(self, req_type: str, count: int=3) -> list:
        """Get random legal articles for requirement type"""
```

---

### 3. Template Module (`template_generator.py`)

**Purpose:** Simplified HTML template rendering (learned from AAAV2)

```python
class HTMLTemplateRenderer:
    """Render CNBV documents using Jinja2 + our proven CSS"""

    def __init__(self, template_dir: Path):
        self.env = jinja2.Environment(loader=FileSystemLoader(template_dir))

    def render_requirement(self, data: dict, watermark: str) -> str:
        """Render requirement HTML with embedded logo and watermark"""
        template = self.env.get_template('cnbv_requirement.html')

        # Add our proven AAAV2 CSS styling
        # Add watermark with hashed ID
        # Embed logo as base64 (5 repetitions)

        return template.render(
            data=data,
            watermark=watermark,
            logo_base64=self._encode_logo(),
            css=self._get_proven_css()
        )
```

**Template Structure:**
```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <style>
        /* Our proven AAAV2 CSS - copy directly */
        .watermark { /* red diagonal */ }
        .header-logo { /* 5 logos */ }
        .section-header { /* bordered sections */ }
        /* ... proven styles ... */
    </style>
</head>
<body>
    <div class="watermark">{{watermark}}</div>
    <div class="header">
        {% for i in range(5) %}
        <img src="data:image/jpeg;base64,{{logo_base64}}" class="header-logo"/>
        {% endfor %}
    </div>

    <!-- Content from data -->
    {{ content }}
</body>
</html>
```

---

### 4. Export Module (`exporters.py`)

**Purpose:** Generate all required formats from HTML

```python
class MultiFormatExporter:
    """Export to MD, XML, HTML, PDF, DOCX"""

    def export_all(self, data: dict, output_dir: Path):
        """Generate all formats for E2E testing"""

        # 1. Generate Markdown (source of truth)
        md_content = self._render_markdown(data)

        # 2. Generate HTML with our proven CSS
        html_content = self._render_html(data)

        # 3. Generate PDF using Chrome headless (proven)
        pdf_path = self._html_to_pdf_chrome(html_content)

        # 4. Generate DOCX (NEW - python-docx)
        docx_path = self._html_to_docx(html_content, data)

        # 5. Generate XML (existing schema)
        xml_path = self._data_to_xml(data)

        return {
            'md': md_path,
            'html': html_path,
            'pdf': pdf_path,
            'docx': docx_path,
            'xml': xml_path
        }
```

**DOCX Generation Strategy:**
```python
from docx import Document
from docx.shared import Inches, Pt, RGBColor

def _html_to_docx(self, html_content: str, data: dict) -> Path:
    """Convert HTML structure to DOCX format"""
    doc = Document()

    # Add logo header (5 repetitions)
    # Add watermark as background
    # Parse HTML sections and convert to DOCX paragraphs/tables
    # Maintain formatting (bold, sections, tables)

    doc.save(output_path)
    return output_path
```

---

### 5. Chaos Module (Enhanced)

**Purpose:** Introduce realistic imperfections

```python
class RealisticChaosSimulator:
    """Introduce authentic Mexican bureaucratic imperfections"""

    MEXICAN_TYPOS = {
        'ó': 'o',  # Missing accents
        'í': 'i',
        'México': 'Mexico',
        'número': 'numero',
    }

    COMMON_ERRORS = [
        'double_space',      # "para  proporcionar"
        'missing_accent',    # "numero" instead of "número"
        'date_format_mix',   # Mix DD/MM/YYYY with DD-MM-YYYY
        'inconsistent_caps', # "CNBV" vs "Cnbv"
    ]

    def apply_chaos(self, data: dict, level: str = 'medium') -> dict:
        """Apply controlled imperfections to data"""
```

---

## 🔄 Complete Pipeline Flow

```python
# Main Generator Script
class CNBVFixtureGenerator:
    """Main orchestrator for E2E fixture generation"""

    def generate_batch(self, count: int, requirement_types: list):
        """
        Generate batch of realistic CNBV fixtures

        Flow:
        1. Generate base data (Faker + LLM)
        2. Add legal framework (Legal Catalog)
        3. Apply chaos (Realistic errors)
        4. Render HTML template
        5. Export all formats (MD, XML, HTML, PDF, DOCX)
        6. Create organized output folder
        """

        for i in range(count):
            # 1. Generate structured data
            data = self.data_generator.generate_requirement(
                req_type=random.choice(requirement_types)
            )

            # 2. Generate legal narrative with LLM
            narrative = self.llm_client.generate_legal_text(
                context=data,
                persona="rushed_mexican_lawyer"
            )
            data['narrative'] = narrative

            # 3. Apply chaos
            chaotic_data = self.chaos.apply_chaos(data, level='medium')

            # 4. Render HTML with proven template
            html = self.template_renderer.render(chaotic_data)

            # 5. Export all formats
            output_folder = self._create_output_folder(i)
            files = self.exporter.export_all(
                data=chaotic_data,
                html=html,
                output_dir=output_folder
            )

            # 6. Validate outputs
            self.validator.validate_xml(files['xml'])

            print(f"✓ Generated fixture {i+1}/{count}")
```

---

## 📁 Project Structure

```
generators/AAAV2_refactored/
├── core/
│   ├── __init__.py
│   ├── data_generator.py       # Faker + Mexican data
│   ├── legal_catalog.py        # Legal articles catalog
│   ├── llm_client.py           # Ollama integration (existing)
│   └── chaos_simulator.py      # Enhanced chaos (existing + new)
│
├── templates/
│   ├── cnbv_requirement.html   # Main template (AAAV2 style)
│   ├── styles.css              # Our proven CSS
│   └── sections/               # Reusable HTML sections
│
├── exporters/
│   ├── __init__.py
│   ├── markdown_exporter.py
│   ├── html_exporter.py
│   ├── pdf_exporter.py         # Chrome headless (AAAV2)
│   ├── docx_exporter.py        # NEW
│   └── xml_exporter.py         # Existing schema
│
├── catalogs/
│   ├── mexican_municipalities.json
│   ├── banking_institutions.json
│   ├── legal_articles.json
│   └── common_typos.json
│
├── schemas/
│   └── cnbv_schema.py          # Keep existing
│
├── config/
│   ├── faker_config.yaml
│   ├── llm_config.yaml
│   └── export_config.yaml
│
├── main_generator.py           # Main orchestrator
├── requirements.txt
└── README.md
```

---

## 🚀 Implementation Phases

### Phase 1: Foundation (Week 1)
- [ ] Setup new project structure
- [ ] Migrate proven HTML template from AAAV2
- [ ] Create Mexican data generator with Faker(es_MX)
- [ ] Build legal article catalog
- [ ] Test HTML rendering with sample data

### Phase 2: Export Pipeline (Week 2)
- [ ] Implement Chrome headless PDF export (copy AAAV2)
- [ ] Build DOCX exporter using python-docx
- [ ] Maintain existing XML exporter
- [ ] Test all format outputs
- [ ] Validate XML against schema

### Phase 3: Chaos & LLM (Week 3)
- [ ] Enhanced chaos simulator with Mexican typos
- [ ] LLM integration for narrative generation
- [ ] Legal text templates and prompts
- [ ] Test realistic imperfections

### Phase 4: Integration & Testing (Week 4)
- [ ] Complete end-to-end pipeline
- [ ] Batch generation scripts
- [ ] Validation suite
- [ ] Documentation
- [ ] Performance optimization

---

## 🔧 Key Dependencies

```txt
# Core
faker>=20.0.0
jinja2>=3.1.0
python-docx>=1.1.0
markdown>=3.5.0

# Export
# (Chrome/Edge already installed - no additional deps needed)

# LLM (existing)
requests>=2.31.0
tqdm>=4.66.0

# Validation (existing)
jsonschema>=4.20.0
lxml>=5.0.0
```

---

## 📊 Success Metrics

✅ **Generate 1000+ unique fixtures in < 30 minutes**
✅ **All 5 formats (MD, XML, HTML, PDF, DOCX) match perfectly**
✅ **XML validates 100% against CNBV schema**
✅ **PDF renders identically to real samples**
✅ **Chaos introduces realistic but controlled errors**
✅ **E2E test suite passes with generated fixtures**

---

## 🎓 Lessons Applied from AAAV2

1. ✅ **HTML+Chrome is superior to ReportLab**
   - Easier to maintain
   - Better rendering
   - Simpler watermarks

2. ✅ **CSS solves layout problems elegantly**
   - No manual positioning
   - Responsive design
   - Professional output

3. ✅ **Base64 logo embedding works perfectly**
   - No file path issues
   - Portable HTML

4. ✅ **Watermarks are just CSS**
   - `position: fixed + rotate(-45deg)`
   - Red color with opacity
   - z-index above content

5. ✅ **Chrome `--no-pdf-header-footer` flag**
   - Clean output
   - No browser artifacts

---

## 🎯 Next Steps

1. **Immediate:** Copy AAAV2 successful implementation to new structure
2. **Add:** Spanish Faker + Mexican catalogs
3. **Build:** DOCX exporter
4. **Integrate:** LLM for legal text
5. **Test:** Full E2E pipeline with C# system

---

## 📝 Notes

- Keep existing schema validation (it works)
- Reuse Ollama integration (it works)
- Replace only PDF generation layer
- Add missing DOCX capability
- Focus on maintainability over complexity

**The goal is not perfection - it's authentic imperfection for robust E2E testing.**
