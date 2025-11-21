# PRP1 Document Generator - Enhanced Edition

A sophisticated document generator for creating realistic Mexican legal requerimiento documents (PRP1-style) with automated LLM orchestration, professional PDF rendering, and comprehensive testing.

## Features

### 🚀 Core Capabilities

- **Automated LLM Integration**: Automatic Docker orchestration for Ollama with model management
- **Professional Document Generation**: Creates CNBV-compliant PDFs, DOCX, PNG, and XML files
- **Realistic Output**: Scan artifacts simulation, human-like errors, and authentic formatting
- **Fallback System**: Graceful degradation to template-based generation when LLM is unavailable
- **Comprehensive Testing**: Full test suite with unit, integration, and fixture tests
- **Reproducible Results**: Seed-based generation for consistent output
- **Profile-Based Sampling**: Uses real PRP1 fixture profiles for authentic metadata

### 📄 Output Formats

1. **PDF**: Professional CNBV-compliant layout with watermarks, logos, and tables
2. **DOCX**: Editable Word documents with proper formatting
3. **PNG**: Scanned document simulation with artifacts (noise, blur, rotation)
4. **XML**: Structured metadata export
5. **JSON**: Complete corpus with all metadata and generated text

### 🎯 Best-of-Both-Worlds Design

This implementation combines:
- **Modular architecture** from `prp1_generator` (clean separation of concerns)
- **Docker orchestration** from `Prisma-dumy-generator-AAA` (automated infrastructure)
- **Enhanced testing** covering all major components
- **Professional documentation** with examples and troubleshooting

## Installation

### Prerequisites

- Python 3.9+
- Docker (for automated Ollama orchestration)
- Git

### Quick Start

```bash
# Clone or navigate to the generators directory
cd /path/to/ExxerCube.Prisma/generators/AAA

# Create virtual environment
python -m venv venv

# Activate virtual environment
# Windows:
venv\Scripts\activate
# Linux/Mac:
source venv/bin/activate

# Install dependencies
pip install -r requirements.txt

# Or install with dev dependencies
pip install -e ".[dev]"
```

### Manual Installation

```bash
pip install requests tqdm Faker python-docx reportlab Pillow pdf2image
```

## Usage

### Basic Usage

Generate 10 documents with default settings:

```bash
python generate_documents.py --num 10
```

### Advanced Usage

```bash
# Generate with specific model and output directory
python generate_documents.py \
    --num 5 \
    --model llama3.2 \
    --fixtures-output ./output/fixtures \
    --output ./output/corpus.json

# Generate with reproducible seed
python generate_documents.py \
    --num 10 \
    --seed 42 \
    --fixtures-format both  # Generate both PNG and PDF

# Generate without Docker orchestration (manual Ollama setup)
python generate_documents.py \
    --num 3 \
    --skip-orchestration \
    --ollama-url http://localhost:11434

# Generate with GPU disabled
python generate_documents.py \
    --num 10 \
    --no-gpu

# Generate with audit logging
python generate_documents.py \
    --num 5 \
    --audit-log ./logs/audit.jsonl \
    --debug
```

### Command-Line Options

#### Generation Parameters
- `--num N`: Number of documents to generate (default: 5)
- `--output PATH`: Output JSON corpus path (default: test_corpus.json)
- `--seed N`: Random seed for reproducibility

#### Ollama Configuration
- `--ollama-model MODEL`: Model to use (default: llama3.2:latest)
- `--ollama-url URL`: Ollama API URL (default: http://localhost:11434)
- `--ollama-port PORT`: Ollama API port (default: 11434)

#### Docker Orchestration
- `--skip-orchestration`: Skip Docker orchestration (use existing Ollama)
- `--container-name NAME`: Docker container name (default: ollama)
- `--no-gpu`: Disable GPU acceleration
- `--skip-prewarm`: Skip model prewarming

#### Output Options
- `--fixtures-output DIR`: Directory for generated files
- `--fixtures-format {png,pdf,both}`: Output format (default: png)
- `--audit-log PATH`: JSONL audit log file

#### Advanced Options
- `--batch PROFILE`: Requirement profile identifier to bias sampling
- `--prp1-summary PATH`: Path to PRP1 summary JSON
- `--allow-fallback`: Continue with fallback if Ollama fails
- `--continue-on-error`: Continue even if individual records fail

#### Debugging
- `--debug`: Enable verbose logging
- `--debug-prompts`: Store prompts in output

## Architecture

### Project Structure

```
generators/AAA/
├── prp1_generator/              # Core package
│   ├── __init__.py              # Package exports
│   ├── config.py                # Configuration management
│   ├── context.py               # Context sampling & profiles
│   ├── ollama_client.py         # Enhanced LLM client
│   ├── ollama_orchestrator.py   # Docker automation
│   ├── fixtures.py              # Multi-format rendering
│   ├── validators.py            # Metadata validation
│   ├── exporters.py             # Corpus export
│   ├── fallback.py              # Template fallback
│   └── authority_templates.py   # Authority templates
├── tests/                       # Test suite
│   ├── test_ollama_client.py    # Client tests
│   ├── test_orchestrator.py     # Orchestrator tests
│   └── test_integration.py      # Integration tests
├── generate_documents.py        # Main entry point
├── requirements.txt             # Dependencies
├── pyproject.toml              # Project metadata
├── entities.json               # Sample data entities
├── prompt_template.txt         # LLM prompt template
├── requerimientos_schema.json  # Validation schema
└── README.md                   # This file
```

### Component Overview

#### `OllamaClient`
Enhanced HTTP client with:
- Streaming and non-streaming support
- Persona-based generation
- Comprehensive error handling
- Timeout management

#### `OllamaOrchestrator`
Automated Docker management:
- Container lifecycle management
- Model pulling and caching
- Service health checks
- Model prewarming
- GPU detection

#### `FixtureRenderer`
Professional document rendering:
- CNBV-compliant PDF layout
- DOCX with proper formatting
- PNG with scan artifacts
- XML structured export

#### `ContextSampler`
Intelligent metadata generation:
- Profile-based sampling
- Error injection for realism
- Seed-based reproducibility
- Schema-aware generation

## Testing

### Run All Tests

```bash
pytest
```

### Run with Coverage

```bash
pytest --cov=prp1_generator --cov-report=html
```

### Run Specific Tests

```bash
# Client tests only
pytest tests/test_ollama_client.py

# Integration tests only
pytest tests/test_integration.py -v

# Specific test
pytest tests/test_orchestrator.py::TestContainerManagement::test_is_container_running_true
```

## Configuration

### Data Files

The generator uses several data files:

- **entities.json**: Sample data for generating realistic metadata
- **prompt_template.txt**: Template for LLM prompts
- **requerimientos_schema.json**: Validation schema
- **fictitious_requerimientos_raw.md**: Fallback templates

### Custom Profiles

Create a PRP1 summary JSON to define custom requirement profiles:

```json
{
  "requirement_profiles": [
    {
      "id": "profile_001",
      "name": "Embargo Precautorio",
      "authority": "Tribunal Superior de Justicia",
      "requirement_type": "Embargo Precautorio",
      "mandatory_fields": ["monto", "moneda"],
      "sla_days": [3, 5],
      "aseguramiento": true,
      "hints": ["urgent", "freeze_assets"]
    }
  ]
}
```

Use with:

```bash
python generate_documents.py \
    --num 10 \
    --prp1-summary ./custom_profiles.json \
    --batch profile_001
```

## Troubleshooting

### Docker Issues

**Problem**: `Docker command not found`
```bash
# Install Docker: https://docs.docker.com/get-docker/
# Verify installation:
docker --version
```

**Problem**: `Permission denied while trying to connect to Docker`
```bash
# Linux: Add user to docker group
sudo usermod -aG docker $USER
# Logout and login again
```

**Problem**: GPU not detected
```bash
# Check NVIDIA Docker support:
docker run --rm --gpus all nvidia/cuda:11.0-base nvidia-smi

# If fails, use --no-gpu flag:
python generate_documents.py --num 5 --no-gpu
```

### Ollama Issues

**Problem**: Model pull timeout
```bash
# Increase timeout or pull manually:
docker exec ollama ollama pull llama3.2
```

**Problem**: Service not ready
```bash
# Check if container is running:
docker ps | grep ollama

# Check logs:
docker logs ollama

# Restart container:
docker restart ollama
```

### Generation Issues

**Problem**: All generations use fallback
```bash
# Check Ollama is accessible:
curl http://localhost:11434

# Use debug mode:
python generate_documents.py --num 1 --debug

# Enable fallback explicitly:
python generate_documents.py --num 5 --allow-fallback
```

**Problem**: PDF generation fails
```bash
# Install poppler (required for pdf2image):
# Ubuntu/Debian:
sudo apt-get install poppler-utils
# macOS:
brew install poppler
# Windows:
# Download from: https://github.com/oschwartz10612/poppler-windows/releases
```

## Performance Tips

1. **Skip prewarming for batch jobs**:
   ```bash
   python generate_documents.py --num 100 --skip-prewarm
   ```

2. **Use GPU acceleration**:
   ```bash
   # Ensure NVIDIA Docker runtime is installed
   python generate_documents.py --num 50  # GPU enabled by default
   ```

3. **Generate only PDFs (faster than PNG)**:
   ```bash
   python generate_documents.py --num 20 --fixtures-format pdf
   ```

4. **Use existing Ollama instance**:
   ```bash
   python generate_documents.py --num 30 --skip-orchestration
   ```

## Development

### Code Style

```bash
# Format code
black .

# Lint
flake8 prp1_generator/

# Type check
mypy prp1_generator/
```

### Adding New Features

1. Create feature branch
2. Add tests first (TDD)
3. Implement feature
4. Update documentation
5. Run full test suite

### Release Checklist

- [ ] All tests pass
- [ ] Documentation updated
- [ ] Version bumped in `pyproject.toml`
- [ ] Changelog updated
- [ ] Tagged release

## Comparison with Original Implementations

### Features from `prp1_generator`
✅ Modular package architecture
✅ Professional PDF rendering
✅ Scan artifacts simulation
✅ Profile-based generation
✅ Comprehensive validation
✅ Audit logging
✅ Fallback templates

### Features from `Prisma-dumy-generator-AAA`
✅ Docker orchestration
✅ Model management
✅ Service health checks
✅ Test coverage
✅ CNBV XML schema

### New Features in Enhanced Edition
🆕 Unified CLI with extensive options
🆕 Comprehensive test suite
🆕 Enhanced error handling
🆕 Progress logging
🆕 Reproducible builds
🆕 Professional documentation

## License

Internal use - ExxerCube Team

## Support

For issues or questions:
1. Check the Troubleshooting section
2. Review test cases for usage examples
3. Enable debug mode: `--debug`
4. Check Docker logs: `docker logs ollama`

## Changelog

### Version 2.0.0 (Current)
- Merged best features from both implementations
- Added Docker orchestration
- Enhanced test coverage
- Professional documentation
- Unified CLI interface
- Improved error handling

### Version 1.0.0
- Initial modular implementation
- Basic fixture generation
- LLM integration
