# CNBV E2E Fixture Generator - Complete Implementation Plan
## Project Codename: PRISMA-FG (Fixture Generator)

**Version:** 2.0.0
**Created:** 2025-01-21
**Status:** 🟡 Planning Complete - Ready for Implementation
**Lead:** Claude + Abel
**Target Completion:** 4 weeks from start

---

## 📊 Executive Summary

### Problem Statement
Current banking compliance system requires high-fidelity E2E test fixtures that accurately simulate Mexican regulatory authority requests (CNBV/SIARA). Existing ReportLab-based generator is complex, hard to maintain, and lacks DOCX export capability.

### Solution
Build a modernized fixture generator leveraging:
- **Proven HTML+Chrome approach** (from AAAV2 success)
- **Spanish/Mexican Faker** for realistic data
- **LLM-generated legal text** for authentic narrative
- **Controlled chaos** for real-world imperfections
- **Multi-format export** (MD, XML, HTML, PDF, DOCX)

### Business Value
- **Faster E2E testing** - Generate 1000+ fixtures in < 30 minutes
- **Higher quality** - Realistic imperfections catch edge cases
- **Reduced maintenance** - HTML templates easier than ReportLab
- **Complete coverage** - All 5 formats for comprehensive testing
- **Regulatory confidence** - Fixtures match real CNBV structure

---

## 🎯 Project Objectives (SMART)

| ID | Objective | Measurable Target | Timeline |
|----|-----------|------------------|----------|
| **OBJ-1** | **Performance** | Generate 1000 unique fixtures in < 30 minutes | Week 4 |
| **OBJ-2** | **Quality** | 100% XML schema validation pass rate | Week 4 |
| **OBJ-3** | **Fidelity** | Visual similarity > 90% vs real samples | Week 4 |
| **OBJ-4** | **Coverage** | Support all 5 output formats (MD, XML, HTML, PDF, DOCX) | Week 3 |
| **OBJ-5** | **Realism** | Mexican data passes manual review by native speaker | Week 2 |
| **OBJ-6** | **Maintainability** | New requirement type added in < 4 hours | Week 4 |
| **OBJ-7** | **Chaos** | Controlled errors in 30% of fixtures (configurable) | Week 3 |

---

## 📋 Detailed Task Breakdown

### PHASE 1: Foundation & Data Generation (Week 1)
**Goal:** Establish project structure and Mexican data generation capability

#### Task 1.1: Project Setup & Structure
**Duration:** 4 hours
**Priority:** P0 (Critical Path)
**Dependencies:** None

**Subtasks:**
- [ ] Create new directory: `generators/AAAV2_refactored/`
- [ ] Setup Python virtual environment
- [ ] Install core dependencies (requirements.txt)
- [ ] Create folder structure per architecture doc
- [ ] Initialize Git repository (if not exists)
- [ ] Setup `.gitignore` for Python project

**Deliverables:**
```
✓ Folder structure created
✓ requirements.txt with all dependencies
✓ Virtual environment activated
✓ README.md with quick start guide
```

**Acceptance Criteria:**
- [ ] All folders exist per spec
- [ ] `pip install -r requirements.txt` runs without errors
- [ ] Can import all planned modules

**Technical Notes:**
```bash
# Create structure
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Fixtures\generators\AAAV2_refactored
python -m venv venv
.\venv\Scripts\activate
pip install -r requirements.txt
```

---

#### Task 1.2: Mexican Data Catalogs
**Duration:** 8 hours
**Priority:** P0
**Dependencies:** Task 1.1

**Subtasks:**
- [ ] Research and compile Mexican municipality list (top 50)
- [ ] Compile Mexican banking institutions (official CNBV list)
- [ ] Create legal articles catalog by requirement type
- [ ] Build common bureaucratic typos dictionary
- [ ] Create Mexican address patterns
- [ ] Compile realistic company names (Spanish)

**Deliverables:**
```json
✓ catalogs/mexican_municipalities.json (50 entries)
✓ catalogs/banking_institutions.json (30+ banks)
✓ catalogs/legal_articles.json (by req type)
✓ catalogs/common_typos.json (50+ patterns)
✓ catalogs/mexican_addresses.json (templates)
✓ catalogs/company_suffixes.json (S.A., S. de R.L., etc.)
```

**Sample Structure:**
```json
// legal_articles.json
{
  "fiscal": {
    "primary": [
      "Art. 42 Código Fiscal de la Federación",
      "Art. 145, 151, 152, 153, 154 CFF"
    ],
    "secondary": [
      "Art. 160 CFF",
      "Art. 287, 291 Ley del Seguro Social"
    ]
  },
  "judicial": {
    "primary": [
      "Art. 142 Ley de Instituciones de Crédito",
      "Art. 16 Constitución Política de los Estados Unidos Mexicanos"
    ]
  },
  "pld": {
    "primary": [
      "Art. 400 bis Código Penal Federal",
      "LFPIORPI artículos 17, 18"
    ]
  },
  "aseguramiento": {
    "primary": [
      "Art. 160 CFF",
      "Art. 40 Código Nacional de Procedimientos Penales"
    ]
  }
}

// common_typos.json
{
  "accent_removal": {
    "ó": "o",
    "í": "i",
    "é": "e",
    "ú": "u",
    "á": "a"
  },
  "common_words": {
    "México": ["Mexico", "Méxíco"],
    "número": ["numero", "núm.", "no."],
    "artículo": ["articulo", "art.", "Art"]
  }
}
```

**Acceptance Criteria:**
- [ ] All JSON files validate
- [ ] Legal articles match real CNBV requirements
- [ ] Municipality list covers major cities
- [ ] Banking institutions include top 10 Mexican banks

**Resources:**
- CNBV official website for bank list
- SAT website for legal articles
- Real PRP1 samples for reference

---

#### Task 1.3: Mexican Data Generator (Faker Integration)
**Duration:** 12 hours
**Priority:** P0
**Dependencies:** Task 1.2

**Subtasks:**
- [ ] Create `MexicanDataGenerator` class
- [ ] Implement person generator (with RFC/CURP format)
- [ ] Implement company generator (with RFC format)
- [ ] Implement authority generator (SAT, FGR, UIF, Judges)
- [ ] Implement address generator (Mexican format)
- [ ] Add date/timestamp generation (Mexican timezone)
- [ ] Create unit tests for each generator method

**Deliverables:**
```python
✓ core/data_generator.py (500+ lines)
✓ tests/test_data_generator.py (comprehensive tests)
✓ 100% test coverage for generators
```

**Technical Specification:**
```python
class MexicanDataGenerator:
    """Generate realistic Mexican banking data"""

    def __init__(self, locale='es_MX', seed: Optional[int] = None):
        self.faker = Faker(locale)
        if seed:
            Faker.seed(seed)
        self.municipalities = self._load_catalog('mexican_municipalities.json')
        self.banks = self._load_catalog('banking_institutions.json')

    def generate_person(self, include_rfc: bool = True) -> dict:
        """
        Generate Mexican person data

        Returns:
            {
                'nombre_completo': str,
                'nombre': str,
                'apellido_paterno': str,
                'apellido_materno': str,
                'rfc': str (if include_rfc),
                'curp': str (if include_rfc),
                'domicilio': dict,
                'telefono': str,
                'email': str
            }
        """
        nombre = self.faker.first_name()
        paterno = self.faker.last_name()
        materno = self.faker.last_name()

        person = {
            'nombre': nombre,
            'apellido_paterno': paterno,
            'apellido_materno': materno,
            'nombre_completo': f"{nombre} {paterno} {materno}",
            'domicilio': self.generate_address(),
            'telefono': self._generate_mexican_phone(),
            'email': self.faker.email()
        }

        if include_rfc:
            person['rfc'] = self._generate_rfc_person(paterno, materno, nombre)
            person['curp'] = self._generate_curp(paterno, materno, nombre)

        return person

    def generate_company(self) -> dict:
        """
        Generate Mexican company data

        Returns:
            {
                'razon_social': str,
                'nombre_comercial': str,
                'rfc': str,
                'domicilio_fiscal': dict,
                'telefono': str,
                'email': str,
                'tipo_sociedad': str (S.A., S. de R.L., etc.)
            }
        """
        pass  # Implementation

    def generate_authority(self, type: str = None) -> dict:
        """
        Generate government authority data

        Args:
            type: 'fiscal' | 'judicial' | 'ministerial' | 'pld' | None (random)

        Returns:
            {
                'tipo': str,
                'nombre_autoridad': str,
                'unidad': str,
                'direccion': str,
                'servidor_publico': dict (person),
                'numero_oficio': str,
                'fecha_oficio': date
            }
        """
        pass  # Implementation

    def generate_requirement(self, req_type: str, target_type: str = 'person') -> dict:
        """
        Generate complete requirement data

        Args:
            req_type: 'informacion' | 'aseguramiento' | 'desbloqueo' | 'pld'
            target_type: 'person' | 'company'

        Returns:
            Complete structured data for requirement
        """
        pass  # Implementation

    # Private helper methods
    def _generate_rfc_person(self, paterno: str, materno: str, nombre: str) -> str:
        """Generate realistic RFC for person (13 chars)"""
        # Format: PAPM850101XXX
        # First 4: First letter of paterno, first vowel of paterno,
        #          first letter of materno, first letter of nombre
        # Next 6: Birth date YYMMDD
        # Last 3: Homoclave
        pass

    def _generate_rfc_company(self, razon_social: str) -> str:
        """Generate realistic RFC for company (12 chars)"""
        pass

    def _generate_curp(self, paterno: str, materno: str, nombre: str) -> str:
        """Generate realistic CURP (18 chars)"""
        pass

    def _generate_mexican_phone(self) -> str:
        """Generate Mexican phone format: (55) 1234-5678"""
        pass

    def generate_address(self) -> dict:
        """
        Generate Mexican address

        Returns:
            {
                'calle': str,
                'numero_exterior': str,
                'numero_interior': str (optional),
                'colonia': str,
                'municipio': str,
                'estado': str,
                'codigo_postal': str,
                'pais': 'México'
            }
        """
        municipality = random.choice(self.municipalities)
        return {
            'calle': self.faker.street_name(),
            'numero_exterior': str(self.faker.building_number()),
            'numero_interior': random.choice([None, str(random.randint(1, 50))]),
            'colonia': self.faker.city_suffix(),
            'municipio': municipality['nombre'],
            'estado': municipality['estado'],
            'codigo_postal': self.faker.postcode(),
            'pais': 'México'
        }

    def _load_catalog(self, filename: str) -> dict:
        """Load JSON catalog file"""
        path = Path(__file__).parent.parent / 'catalogs' / filename
        with open(path, 'r', encoding='utf-8') as f:
            return json.load(f)
```

**Acceptance Criteria:**
- [ ] Generates valid-looking RFC (format correct, even if not real)
- [ ] Generates valid-looking CURP (format correct)
- [ ] Mexican phone numbers in correct format
- [ ] All person names are in Spanish
- [ ] Addresses include real Mexican states/municipalities
- [ ] Company names sound authentically Mexican
- [ ] Unit tests pass 100%

**Edge Cases to Handle:**
- Names with accents (José, María)
- Compound names (José María)
- Very long surnames
- RFC collisions (homoclave generation)
- Special characters in company names

---

#### Task 1.4: Legal Article Catalog System
**Duration:** 6 hours
**Priority:** P1
**Dependencies:** Task 1.2

**Subtasks:**
- [ ] Create `LegalArticleCatalog` class
- [ ] Implement article selection by requirement type
- [ ] Add article combination logic
- [ ] Create article formatting methods
- [ ] Build unit tests

**Deliverables:**
```python
✓ core/legal_catalog.py (300+ lines)
✓ tests/test_legal_catalog.py
```

**Technical Specification:**
```python
class LegalArticleCatalog:
    """Manage legal article references for Mexican banking requirements"""

    def __init__(self, catalog_path: Optional[Path] = None):
        self.articles = self._load_articles(catalog_path)
        self.combinations = self._build_common_combinations()

    def get_articles_for_requirement(
        self,
        req_type: str,
        count: int = 3,
        include_secondary: bool = True
    ) -> List[str]:
        """
        Get legal articles appropriate for requirement type

        Args:
            req_type: 'fiscal', 'judicial', 'pld', 'aseguramiento'
            count: Number of articles to return
            include_secondary: Include secondary/supporting articles

        Returns:
            List of formatted legal article references
        """
        primary = self.articles[req_type]['primary']
        articles = random.sample(primary, min(count, len(primary)))

        if include_secondary and count > len(primary):
            secondary = self.articles[req_type]['secondary']
            remaining = count - len(articles)
            articles.extend(random.sample(secondary, min(remaining, len(secondary))))

        return articles

    def format_article_list(self, articles: List[str], style: str = 'prose') -> str:
        """
        Format article list for document

        Args:
            style: 'prose' | 'bullets' | 'numbered'

        Returns:
            Formatted string ready for template
        """
        if style == 'prose':
            if len(articles) == 1:
                return articles[0]
            elif len(articles) == 2:
                return f"{articles[0]} y {articles[1]}"
            else:
                return f"{', '.join(articles[:-1])} y {articles[-1]}"
        elif style == 'bullets':
            return '\n'.join(f"• {art}" for art in articles)
        elif style == 'numbered':
            return '\n'.join(f"{i+1}. {art}" for i, art in enumerate(articles))

    def get_common_combination(self, req_type: str) -> List[str]:
        """Get a pre-defined common combination of articles"""
        return random.choice(self.combinations.get(req_type, []))

    def _build_common_combinations(self) -> dict:
        """Build commonly used article combinations"""
        return {
            'fiscal': [
                ['Art. 42 CFF', 'Art. 145 CFF', 'Art. 160 CFF'],
                ['Art. 287 LSS', 'Art. 291 LSS'],
            ],
            'aseguramiento': [
                ['Art. 40 CNPP', 'Art. 160 CFF', 'Art. 142 LIC'],
            ]
        }
```

**Acceptance Criteria:**
- [ ] Returns correct articles for each requirement type
- [ ] Formatting matches real documents
- [ ] Combinations make legal sense
- [ ] Random selection provides variety

---

### PHASE 2: Template & Export Engine (Week 2)
**Goal:** Build HTML template system and multi-format export

#### Task 2.1: HTML Template System (Copy AAAV2 Success)
**Duration:** 8 hours
**Priority:** P0
**Dependencies:** Phase 1 Complete

**Subtasks:**
- [ ] Copy proven Template.md from AAAV2
- [ ] Convert to Jinja2 template format
- [ ] Copy proven CSS from AAAV2 PdfGenerator.py
- [ ] Create separate CSS file for maintainability
- [ ] Setup Jinja2 environment
- [ ] Add base64 logo embedding
- [ ] Implement watermark generation
- [ ] Test rendering with sample data

**Deliverables:**
```
✓ templates/cnbv_requirement.html (Jinja2 template)
✓ templates/styles.css (extracted from AAAV2)
✓ core/template_renderer.py (Jinja2 wrapper)
✓ tests/test_template_rendering.py
```

**Technical Specification:**
```python
class HTMLTemplateRenderer:
    """Render CNBV documents using Jinja2 + proven AAAV2 CSS"""

    def __init__(self, template_dir: Path, logo_path: Path):
        self.env = jinja2.Environment(
            loader=jinja2.FileSystemLoader(template_dir),
            autoescape=jinja2.select_autoescape(['html', 'xml'])
        )
        self.logo_base64 = self._encode_logo(logo_path)

    def render(self, data: dict, output_type: str = 'full') -> str:
        """
        Render requirement HTML

        Args:
            data: Complete requirement data
            output_type: 'full' | 'preview' | 'print'

        Returns:
            Complete HTML string with embedded CSS and logo
        """
        # Generate watermark
        watermark = self._generate_watermark(data.get('folio_siara', ''))

        # Load template
        template = self.env.get_template('cnbv_requirement.html')

        # Render
        html = template.render(
            data=data,
            watermark=watermark,
            logo_base64=self.logo_base64,
            timestamp=datetime.now().isoformat()
        )

        return html

    def _generate_watermark(self, folio: str) -> str:
        """Generate scrambled watermark from folio"""
        hash_obj = hashlib.sha256(folio.encode())
        hash_hex = hash_obj.hexdigest()[:16].upper()

        parts = folio.split('/')
        if len(parts) >= 3:
            return f"{parts[0][:4]}-{hash_hex[:8]}-{parts[2]}"
        else:
            return f"{folio}-{hash_hex[:8]}"

    def _encode_logo(self, logo_path: Path) -> str:
        """Encode logo as base64 for embedding"""
        with open(logo_path, 'rb') as f:
            return base64.b64encode(f.read()).decode('utf-8')
```

**Template Structure (templates/cnbv_requirement.html):**
```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Requerimiento CNBV - {{ data.folio_siara }}</title>
    <style>
        /* Import proven CSS from AAAV2 */
        {% include 'styles.css' %}
    </style>
</head>
<body>
    <!-- Watermark -->
    <div class="watermark">{{ watermark }}</div>

    <!-- Header with 5 logos -->
    <div class="header">
        {% for i in range(5) %}
        <img src="data:image/jpeg;base64,{{ logo_base64 }}" class="header-logo"/>
        {% endfor %}
    </div>

    <!-- Title Section -->
    <h1>Administración General de Auditoría Fiscal Federal</h1>
    <h2>Administración Desconcentrada de Auditoría Fiscal de Sonora "2"</h2>

    <!-- ID Box -->
    <div class="id-box">
        No. De Identificación del Requerimiento<br>
        <strong>{{ data.folio_siara }}</strong>
    </div>

    <hr>

    <!-- Destinatario -->
    <p>
        <strong>{{ data.destinatario.nombre }}</strong><br>
        {{ data.destinatario.cargo }}<br>
        {{ data.destinatario.institucion }}<br>
        {{ data.destinatario.direccion }}<br>
        <strong>P r e s e n t e</strong>
    </p>

    <!-- Datos generales del solicitante -->
    <div class="section-header">Datos generales del solicitante</div>

    <table class="two-column-table">
        <tr>
            <td class="left-column">
                <strong>{{ data.autoridad.nombre }}</strong><br><br>
                Mesa, Turno y/o Unidad: <strong>{{ data.autoridad.unidad }}</strong><br>
                <strong>{{ data.autoridad.domicilio }}</strong>
            </td>
            <td class="right-column">
                <strong>Requerimiento Hacendario</strong><br><br>
                <strong>{{ data.servidor_publico.nombre }}</strong><br>
                <strong>{{ data.servidor_publico.cargo }}</strong><br>
                Tel. {{ data.servidor_publico.telefono }}<br>
                Correo: {{ data.servidor_publico.email }}
            </td>
        </tr>
    </table>

    <!-- Facultades de la Autoridad -->
    <div class="section-header">Facultades de la Autoridad</div>
    <p>{{ data.facultades_texto | safe }}</p>

    <!-- Fundamento del Requerimiento -->
    <div class="section-header">Fundamento del Requerimiento</div>
    <p>{{ data.fundamento_texto | safe }}</p>

    <!-- Motivación -->
    <div class="section-header">Motivación del requerimiento</div>
    <p>{{ data.motivacion_texto | safe }}</p>

    <!-- Origen del requerimiento -->
    <div class="section-header">Origen del requerimiento</div>
    <p><strong>¿Esta solicitud contiene requerimientos de aseguramiento?</strong> {{ data.tiene_aseguramiento }}</p>
    <p><strong>No de oficio:</strong> {{ data.numero_oficio }}</p>
    <p><strong>Monto a crédito:</strong> {{ data.monto_credito }}</p>
    <p><strong>Período de revisión:</strong> {{ data.periodo_revision }}</p>

    <!-- Antecedentes -->
    <p><strong>Antecedentes:</strong></p>
    <p><strong>Sujetos de la auditoría y revisión:</strong></p>
    <table>
        <tr>
            <th>Nombre</th>
            <th>Carácter</th>
        </tr>
        {% for parte in data.partes %}
        <tr>
            <td>{{ parte.nombre }}</td>
            <td>{{ parte.caracter }}</td>
        </tr>
        {% endfor %}
    </table>

    <!-- Solicitudes específicas -->
    <div class="section-header">Solicitudes específicas: 1</div>
    <div class="subsection-header">Personas de quien se requiere información</div>
    <table>
        <tr>
            <th>Nombre</th>
            <th>RFC</th>
            <th>Carácter</th>
            <th>Dirección</th>
            <th>Datos complementarios</th>
        </tr>
        {% for persona in data.personas_solicitud %}
        <tr>
            <td>{{ persona.nombre }}</td>
            <td>{{ persona.rfc }}</td>
            <td>{{ persona.caracter }}</td>
            <td>{{ persona.direccion }}</td>
            <td>{{ persona.datos_complementarios }}</td>
        </tr>
        {% endfor %}
    </table>

    <!-- Cuentas por conocer -->
    <div class="section-header">Cuentas por conocer</div>
    <ul>
        {% for sector in data.sectores_bancarios %}
        <li>{{ sector }}</li>
        {% endfor %}
    </ul>

    <!-- Instrucciones -->
    <div class="section-header">Instrucciones sobre las cuentas por conocer</div>
    <p>{{ data.instrucciones_texto | safe }}</p>

    <p>Derivado de lo anterior solicitó a la comisión nacional bancaria y de valores sea atendido el presente requerimiento y gestionado por medio del sistema de atención de requerimientos autoridad (SIARA) contando con el folio {{ data.folio_siara }}</p>

    <!-- Firma -->
    <div class="signature">
        __________________________________<br><br>
        <strong>{{ data.servidor_publico.nombre }}</strong><br>
        <strong>{{ data.servidor_publico.cargo }}</strong>
    </div>
</body>
</html>
```

**Acceptance Criteria:**
- [ ] Renders identical to AAAV2 output
- [ ] Watermark appears in red diagonal
- [ ] 5 logos display correctly
- [ ] All sections properly styled
- [ ] Tables have borders
- [ ] CSS is maintainable (separate file)

---

#### Task 2.2: PDF Exporter (Chrome Headless - Copy AAAV2)
**Duration:** 4 hours
**Priority:** P0
**Dependencies:** Task 2.1

**Subtasks:**
- [ ] Copy Chrome headless code from AAAV2
- [ ] Create `PDFExporter` class
- [ ] Add error handling for missing Chrome/Edge
- [ ] Test on sample HTML
- [ ] Verify no browser headers/footers

**Deliverables:**
```python
✓ exporters/pdf_exporter.py
✓ tests/test_pdf_export.py
```

**Technical Specification:**
```python
class PDFExporter:
    """Export HTML to PDF using Chrome/Edge headless"""

    def __init__(self):
        self.chrome_path = self._find_browser()
        if not self.chrome_path:
            raise RuntimeError("Chrome or Edge not found")

    def export(self, html_content: str, output_path: Path) -> Path:
        """
        Convert HTML to PDF

        Args:
            html_content: Complete HTML string
            output_path: Where to save PDF

        Returns:
            Path to generated PDF
        """
        # Save HTML to temp file
        temp_html = output_path.parent / f"{output_path.stem}_temp.html"
        temp_html.write_text(html_content, encoding='utf-8')

        try:
            # Run Chrome headless
            cmd = [
                self.chrome_path,
                "--headless",
                "--disable-gpu",
                "--no-pdf-header-footer",  # KEY: No browser headers
                f"--print-to-pdf={output_path}",
                str(temp_html.absolute())
            ]

            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=30
            )

            if not output_path.exists() or output_path.stat().st_size == 0:
                raise RuntimeError(f"PDF generation failed: {result.stderr}")

            return output_path

        finally:
            # Cleanup temp file
            if temp_html.exists():
                temp_html.unlink()

    def _find_browser(self) -> Optional[str]:
        """Find Chrome or Edge executable"""
        paths = [
            r"C:\Program Files\Google\Chrome\Application\chrome.exe",
            r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        ]
        for path in paths:
            if os.path.exists(path):
                return path
        return None
```

**Acceptance Criteria:**
- [ ] Generates valid PDF files
- [ ] No browser headers/footers
- [ ] Watermark visible in PDF
- [ ] All styling preserved
- [ ] File size reasonable (< 1MB per doc)

---

#### Task 2.3: DOCX Exporter (NEW)
**Duration:** 12 hours
**Priority:** P1
**Dependencies:** Task 2.1

**Subtasks:**
- [ ] Research python-docx capabilities
- [ ] Design DOCX structure matching HTML
- [ ] Implement `DOCXExporter` class
- [ ] Add table generation
- [ ] Add styled paragraphs
- [ ] Add logo header (image)
- [ ] Add watermark (background text)
- [ ] Test with sample data

**Deliverables:**
```python
✓ exporters/docx_exporter.py (400+ lines)
✓ tests/test_docx_export.py
✓ Sample DOCX file for manual verification
```

**Technical Specification:**
```python
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_LINE_SPACING
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

class DOCXExporter:
    """Export to Microsoft Word DOCX format"""

    def __init__(self, logo_path: Path):
        self.logo_path = logo_path

    def export(self, data: dict, output_path: Path) -> Path:
        """
        Create DOCX from structured data

        Args:
            data: Complete requirement data
            output_path: Where to save DOCX

        Returns:
            Path to generated DOCX
        """
        doc = Document()

        # Setup document properties
        self._setup_document(doc)

        # Add watermark
        self._add_watermark(doc, self._generate_watermark(data.get('folio_siara', '')))

        # Add logo header (5 repetitions)
        self._add_logo_header(doc)

        # Add title
        self._add_title(doc, "Administración General de Auditoría Fiscal Federal")
        self._add_subtitle(doc, "Administración Desconcentrada de Auditoría Fiscal de Sonora \"2\"")

        # Add ID box (right-aligned)
        self._add_id_box(doc, data.get('folio_siara', ''))

        # Add destinatario
        self._add_destinatario(doc, data.get('destinatario', {}))

        # Add section: Datos generales del solicitante
        self._add_section_header(doc, "Datos generales del solicitante")
        self._add_two_column_table(doc, data.get('autoridad', {}), data.get('servidor_publico', {}))

        # Add section: Facultades de la Autoridad
        self._add_section_header(doc, "Facultades de la Autoridad")
        self._add_paragraph(doc, data.get('facultades_texto', ''))

        # Add section: Fundamento del Requerimiento
        self._add_section_header(doc, "Fundamento del Requerimiento")
        self._add_paragraph(doc, data.get('fundamento_texto', ''))

        # Add section: Motivación
        self._add_section_header(doc, "Motivación del requerimiento")
        self._add_paragraph(doc, data.get('motivacion_texto', ''))

        # Add section: Origen del requerimiento
        self._add_section_header(doc, "Origen del requerimiento")
        self._add_origen_details(doc, data)

        # Add section: Antecedentes
        self._add_paragraph(doc, "Antecedentes:", bold=True)
        self._add_paragraph(doc, "Sujetos de la auditoría y revisión:", bold=True)
        self._add_partes_table(doc, data.get('partes', []))

        # Add section: Solicitudes específicas
        self._add_section_header(doc, "Solicitudes específicas: 1")
        self._add_subsection_header(doc, "Personas de quien se requiere información")
        self._add_personas_table(doc, data.get('personas_solicitud', []))

        # Add section: Cuentas por conocer
        self._add_section_header(doc, "Cuentas por conocer")
        self._add_bullet_list(doc, data.get('sectores_bancarios', []))

        # Add section: Instrucciones
        self._add_section_header(doc, "Instrucciones sobre las cuentas por conocer")
        self._add_paragraph(doc, data.get('instrucciones_texto', ''))

        # Add footer text
        footer_text = f"Derivado de lo anterior solicitó a la comisión nacional bancaria y de valores sea atendido el presente requerimiento y gestionado por medio del sistema de atención de requerimientos autoridad (SIARA) contando con el folio {data.get('folio_siara', '')}"
        self._add_paragraph(doc, footer_text)

        # Add signature
        self._add_signature(doc, data.get('servidor_publico', {}))

        # Save
        doc.save(str(output_path))
        return output_path

    def _setup_document(self, doc: Document):
        """Setup document margins and properties"""
        sections = doc.sections
        for section in sections:
            section.top_margin = Inches(1)
            section.bottom_margin = Inches(1)
            section.left_margin = Inches(1)
            section.right_margin = Inches(1)

    def _add_watermark(self, doc: Document, text: str):
        """Add diagonal watermark to document"""
        # This is complex in python-docx, requires XML manipulation
        # Add watermark to header
        section = doc.sections[0]
        header = section.header

        # Create watermark paragraph
        para = header.paragraphs[0] if header.paragraphs else header.add_paragraph()
        para.alignment = WD_ALIGN_PARAGRAPH.CENTER

        # Add text with specific formatting
        run = para.add_run(text)
        run.font.size = Pt(80)
        run.font.color.rgb = RGBColor(255, 0, 0)  # Red
        run.font.bold = True

        # Rotate text (requires XML manipulation)
        # This is simplified - full implementation requires more XML work

    def _add_logo_header(self, doc: Document):
        """Add 5 logos in a row at top"""
        para = doc.add_paragraph()
        para.alignment = WD_ALIGN_PARAGRAPH.CENTER

        for i in range(5):
            run = para.add_run()
            run.add_picture(str(self.logo_path), width=Inches(0.8))
            if i < 4:  # Add space between logos
                run.add_text("  ")

    def _add_title(self, doc: Document, text: str):
        """Add main title"""
        para = doc.add_paragraph(text)
        para.alignment = WD_ALIGN_PARAGRAPH.LEFT
        run = para.runs[0]
        run.font.size = Pt(11)
        run.font.name = 'Arial'

    def _add_subtitle(self, doc: Document, text: str):
        """Add subtitle"""
        para = doc.add_paragraph(text)
        para.alignment = WD_ALIGN_PARAGRAPH.LEFT
        run = para.runs[0]
        run.font.size = Pt(10)
        run.font.name = 'Arial'

    def _add_section_header(self, doc: Document, text: str):
        """Add bordered section header"""
        para = doc.add_paragraph(text)
        para.alignment = WD_ALIGN_PARAGRAPH.CENTER

        # Format as section header
        run = para.runs[0]
        run.font.bold = True
        run.font.size = Pt(11)

        # Add border (requires paragraph properties)
        pPr = para._element.get_or_add_pPr()
        pBdr = OxmlElement('w:pBdr')

        # Add borders on all sides
        for side in ['top', 'left', 'bottom', 'right']:
            border = OxmlElement(f'w:{side}')
            border.set(qn('w:val'), 'single')
            border.set(qn('w:sz'), '4')
            border.set(qn('w:space'), '1')
            border.set(qn('w:color'), '000000')
            pBdr.append(border)

        pPr.append(pBdr)

        # Add shading (gray background)
        shd = OxmlElement('w:shd')
        shd.set(qn('w:fill'), 'F2F2F2')
        pPr.append(shd)

    def _add_paragraph(self, doc: Document, text: str, bold: bool = False, justify: bool = True):
        """Add formatted paragraph"""
        para = doc.add_paragraph(text)
        if justify:
            para.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

        if bold:
            para.runs[0].font.bold = True

        para.runs[0].font.size = Pt(10)
        para.runs[0].font.name = 'Arial'

    def _add_two_column_table(self, doc: Document, left_data: dict, right_data: dict):
        """Add two-column table for solicitor info"""
        table = doc.add_table(rows=1, cols=2)
        table.style = 'Table Grid'

        # Left column
        left_cell = table.rows[0].cells[0]
        left_text = f"{left_data.get('nombre', '')}\n\nMesa, Turno y/o Unidad: {left_data.get('unidad', '')}\n{left_data.get('domicilio', '')}"
        left_cell.text = left_text

        # Right column
        right_cell = table.rows[0].cells[1]
        right_text = f"Requerimiento Hacendario\n\n{right_data.get('nombre', '')}\n{right_data.get('cargo', '')}\nTel. {right_data.get('telefono', '')}\nCorreo: {right_data.get('email', '')}"
        right_cell.text = right_text

    def _add_partes_table(self, doc: Document, partes: list):
        """Add table for partes"""
        table = doc.add_table(rows=len(partes) + 1, cols=2)
        table.style = 'Table Grid'

        # Header
        table.rows[0].cells[0].text = "Nombre"
        table.rows[0].cells[1].text = "Carácter"

        # Data rows
        for i, parte in enumerate(partes):
            table.rows[i+1].cells[0].text = parte.get('nombre', '')
            table.rows[i+1].cells[1].text = parte.get('caracter', '')

    def _add_personas_table(self, doc: Document, personas: list):
        """Add table for personas solicitud"""
        table = doc.add_table(rows=len(personas) + 1, cols=5)
        table.style = 'Table Grid'

        # Header
        headers = ["Nombre", "RFC", "Carácter", "Dirección", "Datos complementarios"]
        for i, header in enumerate(headers):
            table.rows[0].cells[i].text = header

        # Data rows
        for i, persona in enumerate(personas):
            table.rows[i+1].cells[0].text = persona.get('nombre', '')
            table.rows[i+1].cells[1].text = persona.get('rfc', '')
            table.rows[i+1].cells[2].text = persona.get('caracter', '')
            table.rows[i+1].cells[3].text = persona.get('direccion', '')
            table.rows[i+1].cells[4].text = persona.get('datos_complementarios', '')

    def _add_signature(self, doc: Document, servidor: dict):
        """Add centered signature block"""
        doc.add_paragraph()  # Space
        doc.add_paragraph()  # Space

        line_para = doc.add_paragraph("_" * 50)
        line_para.alignment = WD_ALIGN_PARAGRAPH.CENTER

        name_para = doc.add_paragraph(servidor.get('nombre', ''))
        name_para.alignment = WD_ALIGN_PARAGRAPH.CENTER
        name_para.runs[0].font.bold = True

        cargo_para = doc.add_paragraph(servidor.get('cargo', ''))
        cargo_para.alignment = WD_ALIGN_PARAGRAPH.CENTER
        cargo_para.runs[0].font.bold = True

    # ... more helper methods ...
```

**Acceptance Criteria:**
- [ ] DOCX opens correctly in Microsoft Word
- [ ] Tables render properly
- [ ] Logos appear in header
- [ ] Watermark visible (even if imperfect)
- [ ] Formatting matches PDF reasonably
- [ ] All text content present

**Known Limitations:**
- Watermark rotation in DOCX is complex (may be simplified)
- Some CSS effects may not translate perfectly
- Focus on content accuracy over pixel-perfect rendering

---

#### Task 2.4: XML & Markdown Exporters
**Duration:** 6 hours
**Priority:** P1
**Dependencies:** Task 1.3

**Subtasks:**
- [ ] Keep existing XML schema from AAA
- [ ] Create `XMLExporter` class using existing schema
- [ ] Create `MarkdownExporter` class
- [ ] Add validation against CNBV schema
- [ ] Test all exports

**Deliverables:**
```python
✓ exporters/xml_exporter.py
✓ exporters/markdown_exporter.py
✓ tests/test_exports.py
```

**Acceptance Criteria:**
- [ ] XML validates against schema
- [ ] Markdown is human-readable
- [ ] All data fields present

---

### PHASE 3: LLM Integration & Chaos (Week 3)
**Goal:** Generate realistic legal text and introduce controlled errors

#### Task 3.1: LLM Legal Text Generator
**Duration:** 10 hours
**Priority:** P1
**Dependencies:** Task 1.4

**Subtasks:**
- [ ] Reuse Ollama client from AAA
- [ ] Create legal text prompts
- [ ] Build prompt templates by requirement type
- [ ] Implement "rushed lawyer" persona
- [ ] Add fallback templates
- [ ] Test text quality

**Deliverables:**
```python
✓ core/llm_text_generator.py
✓ prompts/legal_templates.yaml
✓ tests/test_llm_generation.py
```

**Technical Specification:**
```python
class LegalTextGenerator:
    """Generate legal narrative using LLM"""

    PERSONAS = {
        'rushed_lawyer': """You are a competent but overworked junior lawyer
        working for a Mexican government authority. You write clear but occasionally
        have minor typos. You use formal legal Spanish.""",
    }

    PROMPT_TEMPLATES = {
        'fundamento': """Generate the legal foundation (fundamento jurídico) for a
        {req_type} requirement. Reference these articles: {articles}.
        Write 2-3 paragraphs in formal Spanish. Include minor imperfections.""",

        'motivacion': """Generate the motivation section explaining why this
        {req_type} requirement is being issued. Context: {context}.
        Write 2-3 paragraphs in formal Spanish.""",
    }

    def __init__(self, ollama_client: OllamaClient):
        self.client = ollama_client

    def generate_fundamento(self, req_type: str, articles: List[str]) -> str:
        """Generate fundamento text"""
        prompt = self._build_prompt('fundamento', req_type=req_type, articles=articles)
        return self.client.generate(prompt)

    def generate_motivacion(self, req_type: str, context: dict) -> str:
        """Generate motivacion text"""
        prompt = self._build_prompt('motivacion', req_type=req_type, context=context)
        return self.client.generate(prompt)

    def _build_prompt(self, template_key: str, **kwargs) -> str:
        """Build complete prompt with persona"""
        persona = self.PERSONAS['rushed_lawyer']
        template = self.PROMPT_TEMPLATES[template_key]
        user_prompt = template.format(**kwargs)

        return f"{persona}\n\n{user_prompt}"
```

**Acceptance Criteria:**
- [ ] Generated text is in Spanish
- [ ] Text uses formal legal tone
- [ ] References provided articles
- [ ] Contains minor natural imperfections
- [ ] Fallback works when LLM unavailable

---

#### Task 3.2: Enhanced Chaos Simulator
**Duration:** 8 hours
**Priority:** P1
**Dependencies:** Task 1.2

**Subtasks:**
- [ ] Enhance existing chaos simulator
- [ ] Add Mexican-specific typos
- [ ] Implement various chaos levels
- [ ] Add date format inconsistencies
- [ ] Add accent removal
- [ ] Test chaos application

**Deliverables:**
```python
✓ core/chaos_simulator.py (enhanced)
✓ tests/test_chaos.py
```

**Technical Specification:**
```python
class EnhancedChaosSimulator:
    """Apply realistic Mexican bureaucratic imperfections"""

    CHAOS_LEVELS = {
        'none': 0.0,
        'low': 0.1,      # 10% chance of any error
        'medium': 0.3,   # 30% chance
        'high': 0.5,     # 50% chance
    }

    def __init__(self, typos_catalog: dict, seed: Optional[int] = None):
        self.typos = typos_catalog
        if seed:
            random.seed(seed)

    def apply_chaos(self, data: dict, level: str = 'medium') -> dict:
        """
        Apply controlled chaos to data

        Args:
            data: Clean requirement data
            level: 'none', 'low', 'medium', 'high'

        Returns:
            Data with realistic imperfections
        """
        if level == 'none':
            return data

        chaos_prob = self.CHAOS_LEVELS[level]
        chaotic_data = data.copy()

        # Apply various chaos types
        if random.random() < chaos_prob:
            chaotic_data = self._remove_accents(chaotic_data)

        if random.random() < chaos_prob:
            chaotic_data = self._add_double_spaces(chaotic_data)

        if random.random() < chaos_prob:
            chaotic_data = self._mix_date_formats(chaotic_data)

        if random.random() < chaos_prob:
            chaotic_data = self._inconsistent_capitalization(chaotic_data)

        if random.random() < chaos_prob / 2:  # Rarer
            chaotic_data = self._typos_in_common_words(chaotic_data)

        return chaotic_data

    def _remove_accents(self, data: dict) -> dict:
        """Randomly remove accents from Spanish words"""
        def remove_accent_from_text(text: str) -> str:
            if not text or not isinstance(text, str):
                return text

            for accented, plain in self.typos['accent_removal'].items():
                if random.random() < 0.3:  # 30% chance per accent
                    text = text.replace(accented, plain)
            return text

        # Apply recursively to all string fields
        return self._apply_to_strings(data, remove_accent_from_text)

    def _add_double_spaces(self, data: dict) -> dict:
        """Add random double spaces"""
        def add_doubles(text: str) -> str:
            if not text or not isinstance(text, str):
                return text

            words = text.split(' ')
            result = []
            for word in words:
                result.append(word)
                if random.random() < 0.05:  # 5% chance
                    result.append('')  # Creates double space

            return ' '.join(result)

        return self._apply_to_strings(data, add_doubles)

    def _mix_date_formats(self, data: dict) -> dict:
        """Mix DD/MM/YYYY with DD-MM-YYYY"""
        # Find all date fields and randomly change separator
        pass

    def _inconsistent_capitalization(self, data: dict) -> dict:
        """Make capitalization inconsistent (CNBV vs Cnbv)"""
        pass

    def _typos_in_common_words(self, data: dict) -> dict:
        """Add typos to common words"""
        def add_typo(text: str) -> str:
            if not text or not isinstance(text, str):
                return text

            for correct, typos in self.typos['common_words'].items():
                if correct in text and random.random() < 0.2:
                    typo = random.choice(typos)
                    text = text.replace(correct, typo, 1)  # Only first occurrence

            return text

        return self._apply_to_strings(data, add_typo)

    def _apply_to_strings(self, data: dict, func) -> dict:
        """Recursively apply function to all string values"""
        if isinstance(data, dict):
            return {k: self._apply_to_strings(v, func) for k, v in data.items()}
        elif isinstance(data, list):
            return [self._apply_to_strings(item, func) for item in data]
        elif isinstance(data, str):
            return func(data)
        else:
            return data
```

**Acceptance Criteria:**
- [ ] Chaos is controlled and reproducible (with seed)
- [ ] Low/medium/high levels work as expected
- [ ] Errors are realistic (not gibberish)
- [ ] Original data preserved (no deep mutation)
- [ ] Can be disabled (level='none')

---

### PHASE 4: Integration & Pipeline (Week 4)
**Goal:** Complete end-to-end pipeline and testing

#### Task 4.1: Main Generator Orchestrator
**Duration:** 12 hours
**Priority:** P0
**Dependencies:** All previous tasks

**Subtasks:**
- [ ] Create main `CNBVFixtureGenerator` class
- [ ] Implement batch generation
- [ ] Add progress tracking
- [ ] Create organized output folders
- [ ] Add configuration system
- [ ] Implement CLI interface
- [ ] Add logging

**Deliverables:**
```python
✓ main_generator.py (main entry point)
✓ core/fixture_generator.py (orchestrator)
✓ config/default_config.yaml
✓ CLI with argparse
```

**Technical Specification:**
```python
class CNBVFixtureGenerator:
    """Main orchestrator for E2E fixture generation"""

    def __init__(self, config: dict):
        self.config = config
        self.data_gen = MexicanDataGenerator(locale='es_MX')
        self.legal_catalog = LegalArticleCatalog()
        self.llm_gen = LegalTextGenerator(ollama_client)
        self.chaos = EnhancedChaosSimulator(typos_catalog)
        self.template = HTMLTemplateRenderer(template_dir, logo_path)
        self.exporters = {
            'pdf': PDFExporter(),
            'docx': DOCXExporter(logo_path),
            'xml': XMLExporter(),
            'md': MarkdownExporter(),
        }

    def generate_batch(
        self,
        count: int,
        requirement_types: List[str],
        chaos_level: str = 'medium',
        output_base: Path = Path('output')
    ) -> List[Path]:
        """
        Generate batch of fixtures

        Args:
            count: Number of fixtures to generate
            requirement_types: List of types to randomly select from
            chaos_level: 'none', 'low', 'medium', 'high'
            output_base: Base output directory

        Returns:
            List of output folder paths
        """
        generated_folders = []

        for i in tqdm(range(count), desc="Generating fixtures"):
            try:
                # 1. Select requirement type
                req_type = random.choice(requirement_types)

                # 2. Generate base structured data
                data = self._generate_base_data(req_type)

                # 3. Generate LLM narrative
                data = self._add_llm_narrative(data, req_type)

                # 4. Apply chaos
                if chaos_level != 'none':
                    data = self.chaos.apply_chaos(data, level=chaos_level)

                # 5. Create output folder
                timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
                folio_safe = data['folio_siara'].replace('/', '-')
                output_folder = output_base / f"fixture_{folio_safe}_{timestamp}"
                output_folder.mkdir(parents=True, exist_ok=True)

                # 6. Render HTML
                html_content = self.template.render(data)

                # 7. Export all formats
                self._export_all_formats(data, html_content, output_folder)

                # 8. Save metadata
                self._save_metadata(data, output_folder)

                generated_folders.append(output_folder)

                logger.info(f"Generated fixture {i+1}/{count}: {output_folder.name}")

            except Exception as e:
                logger.error(f"Error generating fixture {i+1}: {e}")
                if not self.config.get('continue_on_error', False):
                    raise

        return generated_folders

    def _generate_base_data(self, req_type: str) -> dict:
        """Generate structured data"""
        # Person/company to investigate
        target = self.data_gen.generate_person() if random.random() < 0.7 else self.data_gen.generate_company()

        # Authority making request
        authority = self.data_gen.generate_authority(type=req_type)

        # Destinatario (CNBV)
        destinatario = {
            'nombre': 'Juan Juan Melón Sandía',
            'cargo': 'Vicepresidente de Supervisión de Procesos Preventivos',
            'institucion': 'Comisión Nacional Bancaria y de valores',
            'direccion': 'Insurgentes Sur 1971, Conjunto Plaza Inn, col. Guadalupe Inn,\nDel Alvaro obregón, C.P. 01020, Ciudad de México'
        }

        # Generate folio SIARA
        folio = self._generate_folio_siara(authority)

        # Get legal articles
        articles = self.legal_catalog.get_articles_for_requirement(req_type)

        # Banking sectors
        sectors = [
            'Sector Casas de Bolsa',
            'Sector Instituciones de Banca de Desarrollo',
            'Sector Instituciones de Banca Múltiple'
        ]

        return {
            'folio_siara': folio,
            'tipo_requerimiento': req_type,
            'destinatario': destinatario,
            'autoridad': authority,
            'servidor_publico': authority['servidor_publico'],
            'target': target,
            'partes': [{'nombre': target['nombre_completo'], 'caracter': 'Investigado'}],
            'personas_solicitud': [target],
            'articles': articles,
            'sectores_bancarios': sectors,
            'tiene_aseguramiento': 'Sí' if req_type == 'aseguramiento' else 'No',
            'numero_oficio': authority['numero_oficio'],
            'fecha_oficio': authority['fecha_oficio'].strftime('%d/%m/%Y'),
            # Placeholders for LLM-generated content
            'facultades_texto': '',
            'fundamento_texto': '',
            'motivacion_texto': '',
            'instrucciones_texto': '',
        }

    def _add_llm_narrative(self, data: dict, req_type: str) -> dict:
        """Generate legal narrative with LLM"""
        try:
            # Generate fundamento
            data['fundamento_texto'] = self.llm_gen.generate_fundamento(
                req_type=req_type,
                articles=data['articles']
            )

            # Generate facultades
            data['facultades_texto'] = self.llm_gen.generate_facultades(
                req_type=req_type,
                autoridad=data['autoridad']['nombre_autoridad']
            )

            # Generate motivacion
            data['motivacion_texto'] = self.llm_gen.generate_motivacion(
                req_type=req_type,
                context=data
            )

            # Generate instrucciones
            data['instrucciones_texto'] = self.llm_gen.generate_instrucciones(
                req_type=req_type,
                context=data
            )

        except Exception as e:
            logger.warning(f"LLM generation failed, using fallback: {e}")
            # Use fallback templates
            data = self._use_fallback_templates(data, req_type)

        return data

    def _export_all_formats(self, data: dict, html_content: str, output_folder: Path):
        """Export to all formats"""
        base_name = "requerimiento"

        # HTML
        html_path = output_folder / f"{base_name}.html"
        html_path.write_text(html_content, encoding='utf-8')

        # PDF
        pdf_path = output_folder / f"{base_name}.pdf"
        self.exporters['pdf'].export(html_content, pdf_path)

        # DOCX
        docx_path = output_folder / f"{base_name}.docx"
        self.exporters['docx'].export(data, docx_path)

        # XML
        xml_path = output_folder / f"{base_name}.xml"
        self.exporters['xml'].export(data, xml_path)

        # Markdown
        md_path = output_folder / f"{base_name}.md"
        self.exporters['md'].export(data, md_path)

        logger.info(f"Exported 5 formats to {output_folder}")

    def _save_metadata(self, data: dict, output_folder: Path):
        """Save generation metadata"""
        metadata = {
            'generated_at': datetime.now().isoformat(),
            'fixture_type': data['tipo_requerimiento'],
            'folio': data['folio_siara'],
            'chaos_applied': self.config.get('chaos_level', 'medium'),
            'llm_model': self.config.get('ollama_model', 'llama3.2'),
        }

        metadata_path = output_folder / "metadata.json"
        metadata_path.write_text(json.dumps(metadata, indent=2, ensure_ascii=False), encoding='utf-8')

    def _generate_folio_siara(self, authority: dict) -> str:
        """Generate realistic SIARA folio"""
        year = datetime.now().year
        seq = random.randint(1, 99999)

        if 'SAT' in authority['nombre_autoridad']:
            prefix = 'AGAFADAFSON'
        elif 'Judicial' in authority['tipo']:
            prefix = 'TSJDF'
        elif 'FGR' in authority['nombre_autoridad']:
            prefix = 'FGRCDMX'
        else:
            prefix = 'CNBV'

        return f"{prefix}{random.randint(1,9)}/{year}/{seq:06d}"
```

**CLI Interface (main_generator.py):**
```python
def main():
    parser = argparse.ArgumentParser(
        description='Generate CNBV E2E test fixtures',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Generate 10 fixtures with medium chaos
  python main_generator.py --count 10 --chaos medium

  # Generate 5 aseguramiento fixtures only
  python main_generator.py --count 5 --types aseguramiento

  # Generate without LLM (use templates)
  python main_generator.py --count 10 --no-llm

  # Generate with specific seed for reproducibility
  python main_generator.py --count 10 --seed 42
        """
    )

    # Core options
    parser.add_argument('--count', type=int, default=10,
                       help='Number of fixtures to generate')
    parser.add_argument('--output', type=Path, default=Path('output'),
                       help='Output directory')
    parser.add_argument('--types', nargs='+',
                       choices=['informacion', 'aseguramiento', 'desbloqueo', 'pld'],
                       default=['informacion', 'aseguramiento'],
                       help='Requirement types to generate')

    # Chaos control
    parser.add_argument('--chaos', choices=['none', 'low', 'medium', 'high'],
                       default='medium',
                       help='Chaos level for realistic errors')

    # LLM control
    parser.add_argument('--no-llm', action='store_true',
                       help='Use templates instead of LLM')
    parser.add_argument('--ollama-model', default='llama3.2',
                       help='Ollama model for text generation')
    parser.add_argument('--ollama-url', default='http://localhost:11434',
                       help='Ollama API URL')

    # Other
    parser.add_argument('--seed', type=int,
                       help='Random seed for reproducibility')
    parser.add_argument('--continue-on-error', action='store_true',
                       help='Continue generation if individual fixture fails')
    parser.add_argument('--verbose', '-v', action='store_true',
                       help='Verbose logging')

    args = parser.parse_args()

    # Setup logging
    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
    )

    # Load config
    config = load_config(args)

    # Create generator
    generator = CNBVFixtureGenerator(config)

    # Generate
    print(f"\n🚀 Starting fixture generation:")
    print(f"   Count: {args.count}")
    print(f"   Types: {', '.join(args.types)}")
    print(f"   Chaos: {args.chaos}")
    print(f"   Output: {args.output}\n")

    try:
        folders = generator.generate_batch(
            count=args.count,
            requirement_types=args.types,
            chaos_level=args.chaos,
            output_base=args.output
        )

        print(f"\n✅ Successfully generated {len(folders)} fixtures")
        print(f"   Location: {args.output}")

    except KeyboardInterrupt:
        print("\n⚠️  Generation interrupted by user")
        sys.exit(130)
    except Exception as e:
        print(f"\n❌ Generation failed: {e}")
        if args.verbose:
            import traceback
            traceback.print_exc()
        sys.exit(1)
```

**Acceptance Criteria:**
- [ ] CLI works with all options
- [ ] Batch generation completes successfully
- [ ] Progress bar shows accurately
- [ ] Errors handled gracefully
- [ ] Output folders organized
- [ ] All 5 formats generated per fixture
- [ ] Metadata saved correctly

---

#### Task 4.2: Validation Suite
**Duration:** 8 hours
**Priority:** P1
**Dependencies:** Task 4.1

**Subtasks:**
- [ ] Create validation module
- [ ] XML schema validation
- [ ] PDF readability check
- [ ] DOCX format validation
- [ ] Content completeness check
- [ ] Automated test suite

**Deliverables:**
```python
✓ tests/validate_fixtures.py
✓ tests/test_end_to_end.py
✓ Validation report generator
```

**Acceptance Criteria:**
- [ ] All generated XMLs validate
- [ ] PDFs are readable
- [ ] DOCXs open in Word
- [ ] No missing required fields

---

#### Task 4.3: Documentation & Examples
**Duration:** 6 hours
**Priority:** P2
**Dependencies:** Task 4.1

**Subtasks:**
- [ ] Write comprehensive README
- [ ] Create usage examples
- [ ] Document configuration options
- [ ] Add troubleshooting guide
- [ ] Create sample output

**Deliverables:**
```
✓ README.md (comprehensive)
✓ docs/USAGE.md
✓ docs/CONFIGURATION.md
✓ docs/TROUBLESHOOTING.md
✓ examples/ directory with samples
```

**Acceptance Criteria:**
- [ ] README covers all features
- [ ] Examples work as documented
- [ ] Configuration fully documented

---

## 📊 Success Metrics & Testing

### Performance Metrics
| Metric | Target | Test Method |
|--------|--------|-------------|
| Generation Speed | 1000 fixtures in < 30 min | Batch test with timer |
| XML Validation Rate | 100% | Schema validator |
| PDF File Size | < 1MB per file | Check file sizes |
| Memory Usage | < 2GB for 1000 fixtures | Monitor during batch |
| CPU Usage | < 80% average | Monitor during batch |

### Quality Metrics
| Metric | Target | Test Method |
|--------|--------|-------------|
| Visual Similarity | > 90% vs real samples | Manual review + similarity score |
| Mexican Data Realism | Pass native speaker review | Manual review |
| Legal Text Quality | Professional tone | Manual review |
| Chaos Realism | Errors feel authentic | Manual review |

### Completeness Metrics
| Metric | Target | Test Method |
|--------|--------|-------------|
| Required Fields | 100% present | Automated check |
| Format Coverage | All 5 formats | Check file existence |
| Requirement Types | Support 4+ types | CLI test |

---

## 🚨 Risk Analysis & Mitigation

### High Risk Items

#### Risk 1: LLM Generation Failure
**Probability:** Medium
**Impact:** Medium
**Mitigation:**
- Implement robust fallback templates
- Cache successful LLM outputs for reuse
- Make LLM optional (--no-llm flag)
- Test with Ollama down

#### Risk 2: DOCX Export Complexity
**Probability:** High
**Impact:** Medium
**Mitigation:**
- Start simple, iterate
- Accept "good enough" vs "pixel perfect"
- Focus on content over formatting
- Have manual fallback process

#### Risk 3: Chrome Not Installed
**Probability:** Low
**Impact:** High
**Mitigation:**
- Check for Chrome/Edge at startup
- Provide clear installation instructions
- Support both Chrome and Edge
- Document system requirements

#### Risk 4: Performance Issues with Large Batches
**Probability:** Medium
**Impact:** Low
**Mitigation:**
- Implement batch chunking
- Add memory cleanup between iterations
- Monitor resource usage
- Provide progress feedback

### Medium Risk Items

#### Risk 5: Mexican Data Quality
**Probability:** Medium
**Impact:** Medium
**Mitigation:**
- Manual review by native speaker
- Use real examples as reference
- Iterate on catalogs based on feedback

#### Risk 6: XML Schema Changes
**Probability:** Low
**Impact:** High
**Mitigation:**
- Keep schema validation modular
- Version control schemas
- Document schema source
- Make schema configurable

---

## 📅 Timeline & Milestones

### Week 1: Foundation
- **Day 1-2:** Setup + Catalogs (Tasks 1.1, 1.2)
- **Day 3-4:** Mexican Data Generator (Task 1.3)
- **Day 5:** Legal Catalog + Testing (Task 1.4)
- **Milestone:** Can generate realistic Mexican data

### Week 2: Templates & Export
- **Day 1-2:** HTML Templates (Task 2.1)
- **Day 2:** PDF Export (Task 2.2)
- **Day 3-4:** DOCX Export (Task 2.3)
- **Day 5:** XML/MD + Testing (Task 2.4)
- **Milestone:** Can export to all 5 formats

### Week 3: LLM & Chaos
- **Day 1-3:** LLM Integration (Task 3.1)
- **Day 4-5:** Enhanced Chaos (Task 3.2)
- **Milestone:** Can generate realistic imperfect fixtures

### Week 4: Integration & Launch
- **Day 1-3:** Main Orchestrator (Task 4.1)
- **Day 4:** Validation Suite (Task 4.2)
- **Day 5:** Documentation (Task 4.3)
- **Milestone:** Production ready system

---

## 🛠️ Technical Requirements

### Development Environment
```yaml
Python: 3.10+
OS: Windows (primary), Linux (secondary)
Browser: Chrome or Edge installed
Memory: 8GB+ RAM
Disk: 10GB free space
```

### Dependencies
```txt
# Core
faker>=20.0.0
jinja2>=3.1.0
python-docx>=1.1.0
markdown>=3.5.0

# Data
pyyaml>=6.0.0
jsonschema>=4.20.0

# LLM
requests>=2.31.0

# Testing
pytest>=7.4.0
pytest-cov>=4.1.0

# Utils
tqdm>=4.66.0
python-dateutil>=2.8.0
```

### External Services
- Ollama (optional, for LLM generation)
- Chrome/Edge (required, for PDF generation)

---

## 📁 Deliverables Checklist

### Code Deliverables
- [ ] Complete Python package structure
- [ ] All modules implemented
- [ ] Unit tests (>80% coverage)
- [ ] Integration tests
- [ ] CLI interface
- [ ] Configuration system

### Documentation Deliverables
- [ ] Comprehensive README
- [ ] Usage guide
- [ ] Configuration guide
- [ ] Troubleshooting guide
- [ ] API documentation
- [ ] Example fixtures

### Quality Deliverables
- [ ] All tests passing
- [ ] Linting clean (black, flake8)
- [ ] Type hints (mypy clean)
- [ ] Performance benchmarks
- [ ] Manual review complete

---

## 🎓 Lessons from AAAV2 (Applied)

### What Worked
✅ HTML+Chrome for PDF (simple, reliable)
✅ Base64 logo embedding (portable)
✅ CSS for styling (maintainable)
✅ Watermark via CSS (elegant)
✅ Timestamped folders (organized)
✅ Chrome --no-pdf-header-footer flag (clean output)

### What to Improve
🔧 Add DOCX export
🔧 More realistic data (Mexican-specific)
🔧 LLM for narrative
🔧 Controlled chaos
🔧 Batch generation at scale

---

## 🚀 Post-Launch Enhancements (Future)

### Phase 5 (Optional)
- [ ] Web UI for fixture generation
- [ ] Real-time preview
- [ ] Template editor
- [ ] More requirement types
- [ ] Multi-language support (if needed beyond Spanish)
- [ ] Integration with C# test suite
- [ ] Fixture versioning system

---

## 📞 Support & Maintenance

### Ongoing Maintenance
- Monitor for Faker library updates
- Update legal article catalog as regulations change
- Refine LLM prompts based on feedback
- Add new requirement types as needed

### Known Limitations
- DOCX watermark may be simplified
- LLM requires Ollama running
- Large batches (10k+) may need chunking
- Some CSS effects may not translate to DOCX

---

## ✅ Ready to Begin

**This plan is complete and ready for implementation.**

Next steps:
1. Review and approve this plan
2. Create task tracking board (Jira/Trello/GitHub Projects)
3. Begin Phase 1, Task 1.1: Project Setup

**Estimated Total Effort:** 90-100 hours (3-4 weeks, single developer)
**Estimated Calendar Time:** 4 weeks (with normal work schedule)

---

## 📝 Sign-Off

- [ ] Plan reviewed by: _______________
- [ ] Budget approved: _______________
- [ ] Timeline accepted: _______________
- [ ] Ready to start: _______________

**Let's build this! 🚀**
