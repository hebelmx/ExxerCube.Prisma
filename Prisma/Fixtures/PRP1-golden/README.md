# PRP1-golden — trustworthy extraction-eval corpus

A **golden** synthetic corpus for measuring field-extraction accuracy. Unlike the client-supplied
`Prisma/Fixtures/PRP1/` fixtures (kept as a hard/adversarial demo set), every document here ships with a
machine-readable **god's-eye answer key** (`ground_truth.json`) whose values are **guaranteed to be
present in the rendered document body**.

## Why a separate gold, and why it's the manifest (not a delivered file)

By law a CNBV requerimiento should arrive as 3 files (a structured JSON + documents), but in reality the
JSON is often missing or hand-built from unreliable sources — mangled, incomplete, contradictory.
Reconciling truth from those unreliable inputs is the entire reason this product exists. Therefore the
**delivered files (PDF/DOCX/XML/JSON) are noisy observations, never truth.** The only trustworthy gold is
what the generator itself stamped, recorded here as `ground_truth.json` **before** any degradation.

> The client `PRP1/` fixtures scored ~0% in the first baseline precisely because their "gold"
> (expediente/oficio/authority) was filename/XML-derived and **never rendered into the document body** —
> not recoverable by OCR. This corpus fixes that class of defect by construction.

## How it was generated (reproducible)

Generator: `Prisma/PRP/PRP1/research/generators/AAAV2_refactored/` (deterministic field values; the LLM,
if enabled, only writes one prose block — it never touches gold values). Regenerate exactly:

```bash
cd Prisma/PRP/PRP1/research/generators/AAAV2_refactored
python3 main_generator.py --count 15 --types judicial --formats md xml html pdf docx --chaos none --seed 1000 -o <dest>
python3 main_generator.py --count 5  --types fiscal informacion pld aseguramiento --formats md xml html pdf docx --chaos none --seed 2000 -o <dest>
```

Composition: **15 judicial + 5 other types**, 9 distinct requesting authorities, `--chaos none`, seeded.

## Source-containment gate (guarantees the gold is real)

At generation the `.md` body is the **hard** gate: every body-intended gold field must literally appear in
the rendered Markdown, or generation **fails loudly**. `pdfContained` (from normalized `pdftotext`) is
recorded as **advisory only** — the eval harness reads the PDF via **OCR of the pixels**, not Chrome's
lossy text layer (Chrome can drop a hyphen in the text layer that OCR actually preserves), so a pdftotext
miss is never fatal.

## `ground_truth.json` gold fields (measurement targets)

`numeroExpediente` (CNBV format, rendered on its own labeled line), `numeroOficio`, `autoridadNombre`
(the **requesting** authority — the meaningful, varied entity; **not** the constant CNBV recipient, which
is recorded separately as non-gold `recipientInstitucion`), `nombreSolicitante`, `personaNombre`,
`personaRfc`, `monto`, `solicitudPartes`. `bodyContained` / `pdfContained` record per-field recoverability.

## Known clean-set simplifications (deliberate; the realistic set will vary these)

- Expediente is rendered for **all** types here (measurement convenience); a future realistic set will omit
  it for some types to exercise correct **abstention**.
- Requesting-authority ↔ requirement-type pairing is not constrained (an authority may appear on an
  atypical type); irrelevant to extraction-accuracy measurement, will be tightened for the realistic set.
- `chaos none` — no OCR-noise/degradation. The realistic set adds missing-file / mangled-field scenarios
  to exercise fusion + the honesty gate.
