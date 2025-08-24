# Corpus Generation and Document Simulation Pipeline

This pipeline generates synthetic Spanish legal documents for testing OCR and document extraction systems.

## Requirements

### Python Dependencies
Install using uv:
```bash
uv pip install pillow numpy tqdm requests opencv-python
```

### Ollama Setup
1. Install Ollama: https://ollama.ai
2. Start Ollama service:
```bash
ollama serve
```
3. Pull a Spanish-capable model:
```bash
ollama pull llama3.2:latest
# or for better Spanish:
ollama pull mistral:latest
```

## Usage

### Step 1: Generate Corpus with Ollama
Generate synthetic legal requirements using AI:

```bash
# Generate 100 documents (default)
uv run python generate_corpus.py

# Generate specific number
uv run python generate_corpus.py --num 50

# Use different model
uv run python generate_corpus.py --model mistral:latest --num 100

# Custom output file
uv run python generate_corpus.py --output my_corpus.json --num 200
```

This creates:
- `corpus_requerimientos.json` - JSON format with metadata
- `corpus_requerimientos.md` - Human-readable markdown format

### Step 2: Create Degraded Document Images
Simulate scanned documents with various degradations:

```bash
# Generate all documents from corpus
uv run python simulate_documents.py

# Generate specific number
uv run python simulate_documents.py --num 20

# Use markdown input
uv run python simulate_documents.py --input corpus_requerimientos.md

# Control degradation level
uv run python simulate_documents.py --degradation heavy --num 10

# Custom output directory
uv run python simulate_documents.py --output TestFixtures
```

Degradation levels:
- `light` - Minor blur, slight rotation
- `medium` - Moderate noise, contrast issues
- `heavy` - Significant artifacts, stains, compression
- `random` - Mix of all levels (default)

### Step 3: Test with SmolVLM Extractor
Test the OCR extraction on generated documents:

```bash
# Test on a generated fixture
uv run python smolvlm_extractor.py --image Fixtures/Fixture001.png

# Test on multiple images
for img in Fixtures/*.png; do
    echo "Processing $img..."
    uv run python smolvlm_extractor.py --image "$img"
done
```

## File Structure

```
.
├── entities.json              # Legal/financial entities database
├── requerimientos_schema.json # JSON schema for documents
├── prompt_template.txt        # Ollama prompt template
├── generate_corpus.py         # Corpus generation script
├── simulate_documents.py      # Document simulation script
├── smolvlm_extractor.py      # OCR extraction script
├── corpus_requerimientos.json # Generated corpus (JSON)
├── corpus_requerimientos.md   # Generated corpus (Markdown)
└── Fixtures/                  # Generated document images
    ├── Fixture001.png
    ├── Fixture001.pdf
    ├── Fixture002.png
    ├── Fixture002.pdf
    └── ...
```

## Customization

### Modify Entities
Edit `entities.json` to add/remove:
- Court names (`autoridades`)
- Requirement types (`tipos_requerimiento`)
- Legal foundations (`fundamentos_legales`)
- Person/company names
- Common amounts

### Adjust Prompt Template
Edit `prompt_template.txt` to change:
- Document structure
- Legal language style
- Required fields
- Formatting rules

### Document Appearance
In `simulate_documents.py`, adjust:
- Page size (A4, Letter, Legal)
- Font sizes and margins
- Watermark opacity
- Degradation parameters

## Troubleshooting

### Ollama Connection Error
```bash
# Check if Ollama is running
curl http://localhost:11434/api/tags

# Start Ollama if needed
ollama serve
```

### Font Issues
If fonts don't render properly:
```bash
# Install liberation fonts (Linux)
sudo apt-get install fonts-liberation

# Or use system fonts
ls /usr/share/fonts/truetype/
```

### Memory Issues
For large corpus generation:
```bash
# Generate in batches
uv run python generate_corpus.py --num 50
# Then append more...
```

## Performance Tips

1. **Ollama Model Selection**:
   - Smaller models (3B-7B) are faster
   - Larger models (13B+) produce better quality
   - Spanish-specific models give better results

2. **Parallel Processing**:
   - Generate corpus first (uses Ollama sequentially)
   - Document simulation can be parallelized

3. **Degradation Levels**:
   - Light degradation is fastest
   - Heavy degradation adds processing time
   - Random provides good variety

## Example Workflow

```bash
# 1. Setup
uv pip install -r requirements.txt
ollama pull llama3.2:latest

# 2. Generate 50 diverse legal documents
uv run python generate_corpus.py --num 50

# 3. Create degraded images
uv run python simulate_documents.py --degradation random

# 4. Test extraction on first document
uv run python smolvlm_extractor.py --image Fixtures/Fixture001.png

# 5. Batch test and save results
for i in {001..010}; do
    uv run python smolvlm_extractor.py --image "Fixtures/Fixture${i}.png" \
        > "results/result_${i}.json"
done
```