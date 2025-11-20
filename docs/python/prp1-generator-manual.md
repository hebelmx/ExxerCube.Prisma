# PRP1 Generator Manual

This manual explains how to set up, run, and validate the refactored PRP1 dummy requerimiento generator. It is written with step-by-step instructions so anyone familiar with a terminal can reproduce the workflow.

## 1. Prerequisites

1. **Python 3.11+** – the scripts live under `Prisma/Code/Src/CSharp/Python`.
2. **Pip dependencies** – install from the repo root:
   ```powershell
   cd Prisma/Code/Src/CSharp/Python
   python -m venv .venv_prisma
   .\.venv_prisma\Scripts\activate
   pip install -r requirements.txt
   ```
3. **DOCX/PDF tooling** – the generator writes authority DOCX files (`python-docx`) and builds CNBV-standard PDFs with ReportLab; PNG previews come from `pdf2image` (Poppler required). No desktop Word installation is needed.
4. **Dockerized Ollama** (recommended): pull the latest container and expose a free port (11435 in this example). When a GPU is available (e.g., WSL2 with NVIDIA drivers), allocate it via `--gpus all` for faster completions:
   ```powershell
   docker run -d --gpus all --name ollama-prp1 `
       -p 127.0.0.1:11435:11434 `
       -v ollama_models:/root/.ollama `
       ollama/ollama:latest
   docker exec -it ollama-prp1 ollama pull llama3.2:latest
   docker exec -it ollama-prp1 nvidia-smi   # optional sanity check
   ```
4. **PRP1 fixtures** – ensure `Prisma/Fixtures/PRP1` contains the reference DOCX/PDF/XML/XLSX files restored in git.

## 2. Update the Fixture Summary

The generator uses a summary JSON describing each PRP1 requirement profile. Rebuild it whenever fixtures change:

```powershell
cd <repo-root>
$env:PYTHONPATH = "$env:PYTHONPATH;$(Resolve-Path 'Prisma/Code/Src/CSharp/Python')"
python Prisma/Code/Src/CSharp/Python/parse_prp1_documents.py `
    --folder Prisma/Fixtures/PRP1 `
    --summary-output Prisma/Fixtures/PRP1/prp1_summary.json `
    --output Prisma/Fixtures/PRP1/parsed_documents.json
```

- `parsed_documents.json` holds the raw extracts (DOCX paragraphs, XLSX rows, etc.).
- `prp1_summary.json` feeds the context sampler (authority names, SLA windows, mandatory fields).

## 3. Running the Generator

### 3.1 Minimal command

```powershell
$env:PYTHONPATH = "$env:PYTHONPATH;$(Resolve-Path 'Prisma/Code/Src/CSharp/Python')"
python Prisma/Code/Src/CSharp/Python/generate_corpus.py `
    --num 2 `
    --output Prisma/Code/Src/CSharp/Python/test_output/test_corpus.json `
    --fixtures-output Prisma/Code/Src/CSharp/Python/test_output/Prp1Dummy `
    --fixtures-format both `
    --audit-log Prisma/Code/Src/CSharp/Python/test_output/audit.jsonl `
    --ollama-url http://localhost:11435 `
    --ollama-model llama3.2:latest `
    --debug
```

What you get:
- `test_corpus.json` + `.md`: metadata, generated text, and hashes.
- `Prp1Dummy/REQ000x.docx`: raw oficio as issued by the authority (tribunal, IMSS, UIF, etc.).
- `Prp1Dummy/REQ000x.(pdf|png|xml)`: CNBV-vetted packet (PDF with hashed red watermark covering the sheet, PNG preview with simulated scan artifacts, XML schema-compliant snapshot).
- `audit.jsonl`: per-record seed/profile, whether Ollama or fallback was used, hashes, fixture paths.
- `job_progress.log`: append-only text log (timestamped start, periodic checkpoints every 5 records, final summary). Use it to monitor long batches or resume if the CLI times out.

### 3.2 Useful flags

| Flag | Description |
|------|-------------|
| `--batch CNBV_Bloqueo` | Filter requirement profiles by ID (see `prp1_summary.json`) to generate themed batches. |
| `--seed 1234` | Produce reproducible random choices for auditing. |
| `--fixtures-format png|pdf|both` | Control whether the CNBV packet also emits PNG previews; DOCX + CNBV PDF/XML are always written. |
| `--prp1-summary path/to/summary.json` | Override the default summary location. |
| `--debug-prompts` | Store the exact prompt used per record (helpful when refining LLM behavior). |

### 3.3 Interpreting the audit log

Each line is JSON. Example:
```json
{
  "record_id": "REQ0001",
  "profile": "333ccc-6666666662025",
  "origin": "ollama",
  "fixtures": {
    "png": ".../REQ0001.png",
    "pdf": ".../REQ0001.pdf",
    "xml": ".../REQ0001.xml"
  },
  "hash": "6a2da4..."
}
```
- `origin`: `ollama` means the LLM responded, `fallback` means the template renderer stepped in.
- `profile`: links back to the PRP1 archetype in `prp1_summary.json`.
- `hash`: SHA-256 of the generated text, useful for deduplication.

## 4. Validating Outputs

1. **Schema validation** is automatic; the generator aborts if metadata fails `requerimientos_schema.json`.
2. **Review sample fixtures** by opening the PNG/PDF/XML files under the chosen fixtures directory.
3. **Run unit tests** whenever package code changes:
   ```powershell
   $env:PYTHONPATH = "$env:PYTHONPATH;$(Resolve-Path 'Prisma/Code/Src/CSharp/Python')"
   pytest Prisma/Code/Src/CSharp/Python/tests
   ```

## 5. Tips for Novices

- **Start small**: Use `--num 1` the first time to confirm Docker + Python wiring before large batches.
- **Watch the console**: The script prints a tqdm progress bar. If it stalls, check `docker logs ollama-prp1`.
- **Fallbacks are okay**: When Ollama is busy or unavailable, the fallback template still produces schema-valid dummy data. The audit log tells you which approach was used.
- **Clean up**: Stop the dedicated Ollama container when finished (`docker stop ollama-prp1`).
- **No Word dependency**: PDFs come from the internal CNBV template (ReportLab). If you only need the raw oficios, you can skip PNG generation by using `--fixtures-format pdf`.
- **Scan artifacts baked in**: The PNG preview intentionally includes noise/blur/rotation to mimic mis-scanned documents. Use the DOCX or PDF if you need pristine text output.

## 6. Common Issues

| Symptom | Resolution |
|---------|------------|
| `jsonschema` import error | Re-run `pip install -r requirements.txt` inside your virtual environment. |
| Ollama 500 errors in logs | Make sure the container has enough RAM; reduce `--num` or use a smaller model. |
| “Folder Prisma/Fixtures/PRP1 does not exist” | Pull latest git changes or ensure you are running from repo root. |
| Fallback always triggers | Confirm `docker ps` shows `ollama-prp1` running and the port matches `--ollama-url`. |

## 7. Directory Overview

- `Prisma/Code/Src/CSharp/Python/prp1_generator/`: package modules (config, context, validators, fixtures, exporters, fallback, Ollama client).
- `Prisma/Code/Src/CSharp/Python/generate_corpus.py`: CLI entrypoint.
- `Prisma/Code/Src/CSharp/Python/parse_prp1_documents.py`: fixture parser and summary builder.
- `Prisma/Fixtures/PRP1/`: reference documents + derived JSON summaries.
- `docs/python/prp1-generator-manual.md`: this manual (update after major workflow changes).

Follow the steps in this manual whenever you need a clean batch of PRP1-like dummy requerimientos for testing or training. As long as Docker and Python dependencies are satisfied, the commands above will produce ready-to-use corpora with reproducible metadata, fixtures, and audit logs.
