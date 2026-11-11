# CNBV Fixture Generator - Quick Start Guide

## 🚀 Common Usage Scenarios

### Scenario 1: Generate 100 IMSS Documents

```bash
python main_generator.py --count 100 --authority IMSS --chaos medium --output imss_fixtures
```

**Result:** 100 IMSS documents with medium chaos level in `imss_fixtures/` directory

---

### Scenario 2: Generate 100 SAT + 100 IMSS Documents

```bash
python batch_generate.py --authorities IMSS:100 SAT:100 --chaos medium
```

**Result:** Organized by authority in `batch_output/IMSS/` and `batch_output/SAT/`

---

### Scenario 3: Generate 40 UIF Documents (Anti-Money Laundering)

```bash
python main_generator.py --count 40 --authority UIF --chaos low --output uif_fixtures
```

**Result:** 40 UIF (Unidad de Inteligencia Financiera) documents for PLD testing

---

### Scenario 4: Generate All Authorities (E2E Testing Suite)

```bash
python batch_generate.py --all --count 50 --chaos medium
```

**Result:** Complete E2E testing suite with 50 documents per authority:
- IMSS: 50
- SAT: 50
- UIF: 50
- FGR: 50
- SEIDO: 50
- PJF: 50
- INFONAVIT: 50
- SHCP: 50
- CONDUSEF: 50

**Total: 450 documents**

---

### Scenario 5: Generate with LLM Variations (Requires Ollama)

**Step 1:** Install and start Ollama

```bash
# Install from ollama.ai
# Pull Spanish model
ollama pull llama3

# Start server
ollama serve
```

**Step 2:** Generate with LLM

```bash
python main_generator.py --count 50 --authority IMSS --llm --llm-model llama3 --chaos medium
```

**Result:** 50 IMSS documents with LLM-generated unique legal text in Spanish

---

### Scenario 6: Production Batch (100 IMSS, 100 SAT, 40 UIF)

```bash
python batch_generate.py --authorities IMSS:100 SAT:100 UIF:40 --chaos medium --output production_fixtures
```

**Result:** Production-ready fixtures organized by authority:
```
production_fixtures/
├── IMSS/
│   └── [100 fixtures]
├── SAT/
│   └── [100 fixtures]
└── UIF/
    └── [40 fixtures]
```

---

### Scenario 7: Judicial Documents Only

```bash
python main_generator.py --count 60 --authority FGR --types judicial --chaos high
```

**Result:** 60 FGR judicial orders with high chaos (stress testing)

---

### Scenario 8: Quick Test (Single Document, All Formats)

```bash
python main_generator.py --count 1 --authority IMSS --chaos none
```

**Result:** Perfect document with all 5 formats (MD, XML, HTML, PDF, DOCX) for validation

---

### Scenario 9: Reproducible Dataset

```bash
python main_generator.py --count 100 --authority SAT --seed 12345 --chaos medium
```

**Result:** Always generates the same 100 documents (useful for testing)

---

### Scenario 10: Lightweight Generation (No PDF/DOCX)

```bash
python main_generator.py --count 500 --authority IMSS --formats md xml html --chaos medium
```

**Result:** 500 fixtures without expensive PDF/DOCX generation (faster)

---

## 📊 Authority Reference

| Authority | Code | Type | Typical Use Case |
|-----------|------|------|------------------|
| Instituto Mexicano del Seguro Social | `IMSS` | Fiscal | Social security tax collection |
| Servicio de Administración Tributaria | `SAT` | Fiscal | Federal tax audits |
| Unidad de Inteligencia Financiera | `UIF` | PLD | Anti-money laundering |
| Fiscalía General de la República | `FGR` | Judicial | Federal prosecutor |
| SEIDO | `SEIDO` | Judicial | Organized crime |
| Poder Judicial Federal | `PJF` | Judicial | Court orders |
| INFONAVIT | `INFONAVIT` | Fiscal | Housing fund collection |
| Secretaría de Hacienda | `SHCP` | Fiscal | Treasury department |
| CONDUSEF | `CONDUSEF` | Info | Consumer protection |

---

## 🎯 Chaos Level Guide

| Level | Typos | Format Errors | Use Case |
|-------|-------|---------------|----------|
| `none` | 0% | 0% | Perfect documents for schema validation |
| `low` | 2% | 5% | Minimal errors, high quality |
| `medium` | 5% | 10% | Realistic bureaucratic documents (recommended) |
| `high` | 10% | 20% | Stress testing, edge cases |

---

## 💡 Pro Tips

1. **Start small:** Test with `--count 1` first to verify output
2. **Use LLM for variety:** Ollama generates unique legal text each time
3. **Batch for organization:** Use `batch_generate.py` for authority-specific folders
4. **Test without PDF:** Use `--formats md xml html` for faster generation during development
5. **Reproducible tests:** Always use `--seed` for CI/CD pipelines
6. **Monitor Ollama:** Check `ollama list` to see available models

---

## 🐛 Troubleshooting

### PDF Generation Fails
```
⚠️ Chrome or Edge not found
```
**Solution:** Install Google Chrome or Microsoft Edge

### LLM Not Working
```
⚠️ Warning: Ollama not available
```
**Solution:**
```bash
# Check if Ollama is running
curl http://localhost:11434/api/tags

# Start Ollama
ollama serve
```

### Slow Generation
**Solution:** Disable PDF/DOCX for faster generation:
```bash
python main_generator.py --formats md xml html
```

---

## 📞 Need Help?

Check the full [README.md](README.md) for complete documentation.
