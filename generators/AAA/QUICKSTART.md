# Quick Start Guide

Get up and running with the PRP1 Document Generator in 5 minutes!

## Prerequisites Check

```bash
# Check Python version (need 3.9+)
python --version

# Check Docker is running
docker ps

# Check Git
git --version
```

## Installation (3 steps)

### Step 1: Navigate to the directory

```bash
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\generators\AAA
```

### Step 2: Create virtual environment

```bash
python -m venv venv

# Activate it:
# Windows (PowerShell):
.\venv\Scripts\Activate.ps1

# Windows (Command Prompt):
venv\Scripts\activate.bat

# Linux/Mac:
source venv/bin/activate
```

### Step 3: Install dependencies

```bash
pip install -r requirements.txt
```

## First Document Generation

### Option A: Fully Automated (Recommended)

Let the system handle everything:

```bash
python generate_documents.py --num 3
```

This will:
1. ✅ Start Ollama Docker container automatically
2. ✅ Pull the llama3.2 model if needed
3. ✅ Warm up the model
4. ✅ Generate 3 complete document packages
5. ✅ Create JSON corpus with all metadata

**Output**: `test_corpus.json` with 3 records

### Option B: Manual Ollama Setup

If you prefer to manage Ollama yourself:

```bash
# Terminal 1: Start Ollama manually
docker run -d --name ollama -p 11434:11434 ollama/ollama
docker exec ollama ollama pull llama3.2

# Terminal 2: Generate documents
python generate_documents.py --num 3 --skip-orchestration
```

### Option C: CPU-Only Mode

If you don't have GPU support:

```bash
python generate_documents.py --num 3 --no-gpu
```

## What Gets Generated?

For each document, you get:
- 📄 **PDF**: Professional CNBV-compliant layout
- 📝 **DOCX**: Editable Word document
- 🖼️ **PNG**: Scanned document simulation (optional)
- 📋 **XML**: Structured metadata
- 📊 **JSON**: Complete corpus entry

## View Your Results

```bash
# Check the corpus
cat test_corpus.json

# If you generated fixtures:
python generate_documents.py --num 2 --fixtures-output ./my_documents

# View generated files
ls -la my_documents/
```

## Common First-Time Issues

### Issue: "Docker command not found"
**Solution**: Install Docker Desktop from https://www.docker.com/get-started

### Issue: "Permission denied" (Linux)
**Solution**:
```bash
sudo usermod -aG docker $USER
# Logout and login again
```

### Issue: Model download is slow
**Solution**: This is normal for first run. The model (~4GB) is cached.

### Issue: "Module not found"
**Solution**:
```bash
# Make sure venv is activated
pip install -r requirements.txt
```

## Next Steps

### Generate More Documents

```bash
# Generate 10 with fixtures
python generate_documents.py \
    --num 10 \
    --fixtures-output ./output \
    --fixtures-format both

# Use a specific seed for reproducibility
python generate_documents.py \
    --num 5 \
    --seed 42
```

### Run Tests

```bash
# Install dev dependencies
pip install -e ".[dev]"

# Run tests
pytest
```

### Customize Generation

Edit `entities.json` to add your own:
- Company names
- Authority names
- Legal foundations
- Requirement types

## Usage Examples

### Example 1: Quick Test (1 document)
```bash
python generate_documents.py --num 1 --debug
```

### Example 2: Production Batch (100 documents)
```bash
python generate_documents.py \
    --num 100 \
    --fixtures-output ./production_output \
    --audit-log ./logs/audit.jsonl \
    --seed 123456
```

### Example 3: Development Mode
```bash
python generate_documents.py \
    --num 5 \
    --debug \
    --debug-prompts \
    --continue-on-error
```

## File Structure After First Run

```
generators/AAA/
├── test_corpus.json          # Generated corpus
├── my_documents/              # Fixture files (if --fixtures-output used)
│   ├── REQ0001.pdf
│   ├── REQ0001.docx
│   ├── REQ0001.xml
│   └── ...
└── job_progress.log          # Generation log
```

## Performance Notes

- **First run**: ~2-5 minutes (model download + warming)
- **Subsequent runs**: ~10-20 seconds per document
- **With GPU**: 2-3x faster
- **Batch mode**: More efficient for 10+ documents

## Need Help?

1. **Enable debug mode**: `--debug`
2. **Check logs**: `cat job_progress.log`
3. **Check Docker**: `docker logs ollama`
4. **Read full docs**: See `README.md`

## Clean Up

```bash
# Remove generated files
rm test_corpus.json
rm -rf my_documents/

# Stop Docker container
docker stop ollama

# Deactivate virtual environment
deactivate
```

## You're Ready! 🎉

Try this command to verify everything works:

```bash
python generate_documents.py --num 1 --fixtures-output ./test --debug
```

If you see "Generation Complete!" - you're all set!
