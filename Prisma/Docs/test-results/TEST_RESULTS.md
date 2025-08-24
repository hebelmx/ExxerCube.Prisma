# Test Results - Document Generation

## Status
✅ Successfully generated 10 test documents similar to DumyPrisma1.png

## Generated Files
- **Corpus**: `test_corpus.json` and `test_corpus.md`
- **Images**: `TestFixtures/Fixture001.png` through `Fixture010.png`
- **PDFs**: `TestFixtures/Fixture001.pdf` through `Fixture010.pdf`

## Visual Composition Features

### Similar to DumyPrisma1.png:
1. **Red diagonal watermark** with SHA256 hash
2. **Official header** "PODER JUDICIAL DE LA FEDERACIÓN"
3. **Structured sections**:
   - CAUSA QUE MOTIVA EL REQUERIMIENTO
   - ACCIÓN SOLICITADA
   - APERCIBIMIENTO
4. **Legal formatting** with proper Spanish legal language
5. **Medium degradation** applied (blur, contrast, slight rotation)

## Document Variations
Each document includes:
- Different court authorities (Juzgados)
- Various requirement types (Embargo, Aseguramiento, etc.)
- Random case numbers and dates
- Different party names
- Varying monetary amounts when applicable
- Unique SHA256 hashes

## File Sizes
- PNG files: ~850KB to 1.7MB (300 DPI, A4 size)
- PDF files: ~280KB to 520KB

## Next Steps for Evaluation

1. **Visual Review**: Check the generated documents in `TestFixtures/` folder
2. **Adjust if needed**:
   - Font size: Edit line 67 in `simulate_documents.py`
   - Degradation level: Use `--degradation light/heavy/random`
   - Watermark opacity: Edit line 156 in `simulate_documents.py`
   - Paper texture: Adjust lines 47-51 in `simulate_documents.py`

3. **Generate more documents**:
```bash
# Generate 100 documents with mixed degradation
uv run python generate_test_corpus.py  # Modify to generate more
uv run python simulate_documents.py --input test_corpus.json --degradation random
```

4. **Test OCR extraction**:
```bash
# Once dependencies are installed
uv run python smolvlm_extractor.py --image TestFixtures/Fixture001.png
```

## Commands Used
```bash
# Generate corpus
uv run python generate_test_corpus.py

# Create simulated documents
uv run python simulate_documents.py --input test_corpus.json --output TestFixtures --degradation medium
```

## Notes
- Ollama models were slow (~60s per document), so used template-based generation
- Font defaults to system font if specific fonts not found
- Degradation includes: blur, noise, rotation, contrast changes, scan lines
- Each document has unique content and hash for testing variety