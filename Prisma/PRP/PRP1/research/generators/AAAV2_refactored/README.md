# CNBV E2E Fixture Generator v2.0

Generate realistic Mexican banking regulatory requirement fixtures for end-to-end testing.

## 🎯 Overview

This tool generates synthetic CNBV (Comisión Nacional Bancaria y de Valores) requirement documents in multiple formats with realistic Mexican data, legal frameworks, and controlled imperfections to simulate authentic bureaucratic documents.

## ✨ Features

- **5 Export Formats**: Markdown, XML, HTML, PDF, DOCX
- **Realistic Mexican Data**: Names, RFC, CURP, addresses using Faker(es_MX)
- **Legal Framework**: Structured catalog of Mexican banking/legal articles
- **Controlled Chaos**: Realistic typos, formatting errors, accent omissions
- **HTML+Chrome PDF**: Superior rendering using proven AAAV2 approach
- **Batch Generation**: Generate hundreds of fixtures quickly
- **LLM Integration**: Use Ollama to generate varied legal text in Spanish
- **Authority-Specific**: Generate documents for IMSS, SAT, UIF, FGR, and more
- **Batch Scripts**: Easy generation of large datasets by authority

## 📦 Installation

### Prerequisites

- Python 3.8+
- Google Chrome or Microsoft Edge (for PDF generation)
- **Optional:** Ollama (for LLM-based text generation)

### Setup

```bash
# Navigate to project directory
cd AAAV2_refactored

# Create virtual environment (recommended)
python -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt
```

## 🚀 Quick Start

### Generate Single Fixture

```bash
python main_generator.py
```

This generates one fixture with all formats (MD, XML, HTML, PDF, DOCX) in the `output/` directory.

### Generate Multiple Fixtures

```bash
# Generate 10 fixtures
python main_generator.py --count 10

# Generate 50 fixtures with specific output directory
python main_generator.py --count 50 --output my_fixtures
```

### Control Chaos Level

```bash
# No errors (perfect documents)
python main_generator.py --chaos none

# Low level of errors
python main_generator.py --chaos low

# Medium level (default - realistic)
python main_generator.py --chaos medium

# High level (more errors)
python main_generator.py --chaos high
```

### Specific Requirement Types

```bash
# Generate only fiscal requirements
python main_generator.py --count 20 --types fiscal

# Generate judicial and PLD requirements
python main_generator.py --count 10 --types judicial pld

# All types: fiscal, judicial, pld, aseguramiento, informacion
```

### Select Output Formats

```bash
# Only PDF and DOCX
python main_generator.py --formats pdf docx

# Only HTML (useful for quick preview)
python main_generator.py --formats html
```

### Reproducible Generation

```bash
# Use seed for reproducibility
python main_generator.py --count 5 --seed 42
```

### Authority-Specific Generation

```bash
# Generate 100 IMSS documents
python main_generator.py --count 100 --authority IMSS

# Generate 40 UIF documents
python main_generator.py --count 40 --authority UIF

# Available authorities: IMSS, SAT, UIF, FGR, SEIDO, PJF, INFONAVIT, SHCP, CONDUSEF
```

### LLM-Enhanced Generation (Ollama)

**Prerequisites:** Install and start Ollama

```bash
# Install Ollama (visit ollama.ai)
# Pull a Spanish-capable model
ollama pull llama2
# or for better Spanish:
ollama pull llama3

# Start Ollama server
ollama serve
```

**Generate with LLM:**

```bash
# Basic LLM generation
python main_generator.py --count 10 --llm

# Specify model
python main_generator.py --count 10 --llm --llm-model llama3

# Combine with authority and chaos
python main_generator.py --count 50 --authority IMSS --llm --chaos medium
```

### Batch Generation by Authority

Generate large datasets for multiple authorities:

```bash
# Generate for all authorities (default counts)
python batch_generate.py --all --count 50

# Generate specific counts for specific authorities
python batch_generate.py --authorities IMSS:100 SAT:100 UIF:40 FGR:60

# With LLM
python batch_generate.py --all --count 20 --llm --llm-model llama3

# Custom output directory
python batch_generate.py --authorities IMSS:100 --output my_batch --chaos high
```

**Default Batch Counts:**
- IMSS: 100
- SAT: 100
- UIF: 40
- FGR: 60
- SEIDO: 50
- PJF: 60
- INFONAVIT: 50
- SHCP: 30
- CONDUSEF: 20

## 📁 Output Structure

Each fixture is generated in its own timestamped directory:

```
output/
├── AGAFADAFSON2-2025-000084_20250121_103045/
│   ├── AGAFADAFSON2-2025-000084.md
│   ├── AGAFADAFSON2-2025-000084.xml
│   ├── AGAFADAFSON2-2025-000084.html
│   ├── AGAFADAFSON2-2025-000084.pdf
│   └── AGAFADAFSON2-2025-000084.docx
└── UIF-2025-123456_20250121_103046/
    ├── UIF-2025-123456.md
    ├── UIF-2025-123456.xml
    ├── UIF-2025-123456.html
    ├── UIF-2025-123456.pdf
    └── UIF-2025-123456.docx
```

## 🏗️ Architecture

### Core Modules

- **`data_generator.py`**: Mexican data generation using Faker
  - RFC/CURP generation
  - Realistic addresses, names, phones
  - Government authorities
  - Banking data

- **`legal_catalog.py`**: Structured legal references
  - Mexican banking law articles
  - Authority faculties
  - Legal foundation templates
  - Instruction templates

- **`chaos_simulator.py`**: Realistic imperfections
  - Accent omissions
  - Common typos
  - Formatting inconsistencies
  - Field-specific errors

### Exporters

- **`html_exporter.py`**: Jinja2 templates with CSS
- **`pdf_exporter.py`**: Chrome headless conversion
- **`docx_exporter.py`**: python-docx generation
- **`markdown_exporter.py`**: Markdown format
- **`xml_exporter.py`**: CNBV XML schema

### Templates

- **`cnbv_requirement.html`**: Main Jinja2 template with proven AAAV2 CSS

### Catalogs

- **`banking_institutions.json`**: Mexican banks and financial institutions
- **`mexican_states.json`**: States, codes, and major cities
- **`common_typos.json`**: Realistic error patterns

## 🎨 Customization

### Using Custom Logo

```bash
python main_generator.py --logo path/to/logo.jpg
```

The logo will be:
- Displayed 5 times in the header
- Embedded as base64 in HTML
- Included in DOCX header

### Modifying Templates

Edit `templates/cnbv_requirement.html` to customize document layout. The template uses Jinja2 syntax:

```html
<p>{{ data.Cnbv_SolicitudSiara }}</p>
```

### Adding Legal Articles

Edit `core/legal_catalog.py` to add new legal articles:

```python
ARTICLES = {
    'fiscal': [
        'Art. 42 del Código Fiscal de la Federación',
        'Your new article here',
    ],
}
```

## 🧪 Chaos Levels

| Level  | Typo % | Format % | Word % | Use Case |
|--------|--------|----------|--------|----------|
| none   | 0%     | 0%       | 0%     | Perfect documents |
| low    | 2%     | 5%       | 3%     | Minimal errors |
| medium | 5%     | 10%      | 7%     | Realistic (default) |
| high   | 10%    | 20%      | 12%    | Stress testing |

## 🔍 Requirement Types

| Type | Description | Authority Examples |
|------|-------------|-------------------|
| `fiscal` | Tax/Fiscal requirements | SAT, IMSS |
| `judicial` | Court orders | PJF, Juzgado Federal |
| `pld` | Anti-money laundering | UIF |
| `aseguramiento` | Asset freezing | SAT, FGR |
| `informacion` | Information requests | Various |

## 📊 Performance

- **Single fixture**: ~2-3 seconds (all 5 formats)
- **100 fixtures**: ~3-5 minutes
- **1000 fixtures**: ~30-40 minutes

## 🐛 Troubleshooting

### PDF Generation Fails

**Error**: `Chrome or Edge not found`

**Solution**: Install Google Chrome or Microsoft Edge. The script auto-detects these browsers.

### Missing Dependencies

**Error**: `ModuleNotFoundError: No module named 'faker'`

**Solution**: Install requirements:
```bash
pip install -r requirements.txt
```

### Faker Locale Not Found

**Error**: `UnknownLocale: es_MX`

**Solution**: Update Faker:
```bash
pip install --upgrade faker
```

## 🎓 Examples

### E2E Testing Workflow

```bash
# 1. Generate test fixtures
python main_generator.py --count 100 --chaos medium --output e2e_fixtures

# 2. Use fixtures in your test suite
# - MD files: Source of truth for validation
# - XML files: Test XML parsing
# - HTML files: Test web UI rendering
# - PDF files: Test PDF processing
# - DOCX files: Test document import
```

### Reproducing Specific Scenarios

```bash
# Reproduce a specific random sequence
python main_generator.py --count 5 --seed 12345

# Always generates the same 5 fixtures
```

## 📝 Development

### Project Structure

```
AAAV2_refactored/
├── core/                   # Core generation logic
│   ├── data_generator.py
│   ├── legal_catalog.py
│   └── chaos_simulator.py
├── exporters/              # Format exporters
│   ├── html_exporter.py
│   ├── pdf_exporter.py
│   ├── docx_exporter.py
│   ├── markdown_exporter.py
│   └── xml_exporter.py
├── templates/              # Jinja2 templates
│   └── cnbv_requirement.html
├── catalogs/               # Data catalogs
│   ├── banking_institutions.json
│   ├── mexican_states.json
│   └── common_typos.json
├── main_generator.py       # CLI entry point
├── requirements.txt        # Dependencies
└── README.md              # This file
```

### Running Tests

```bash
# Test single generation
python main_generator.py --count 1 --chaos none

# Verify all formats generated
ls output/*/
```

## 🚧 Roadmap

- [ ] LLM integration for narrative generation (Ollama)
- [ ] XML schema validation against official CNBV XSD
- [ ] Web UI for interactive generation
- [ ] Docker containerization
- [ ] CI/CD pipeline for automated testing

## 📄 License

Internal use for ExxerCube.Prisma project.

## 🤝 Contributing

This is an internal tool. For improvements or bug fixes, contact the development team.

---

**Generated with ❤️ using the proven AAAV2 HTML+Chrome approach**
