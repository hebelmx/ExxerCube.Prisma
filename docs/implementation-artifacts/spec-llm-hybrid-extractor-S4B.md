# Intended Solution — S4-B: Extend LLM extractor (oficio + authority + CNBV expediente)

Anti-drift reference for the orchestrated S4-B build. Parent specs:
`spec-llm-hybrid-extractor.md` (S1–S3b contracts + reconciler honesty policy),
`spec-llm-hybrid-extractor-S4A.md` (the eval harness / measurement). Owner ruling 2026-07-03: after the
S4-A adversarial gate found the LLM extractor never attempts NumeroOficio/AutoridadNombre and uses a
non-CNBV expediente format, EXTEND the extractor (functional), then measure. Design panel (architect+qa)
died on a transient API error — design below is grounded directly in code; the **mandatory adversarial
gate after implementation is the honesty backstop** (it caught C1–C4).

## Why (confirmed from code + gold)
- C1: `LlmExtractionGate.ExpedienteRegex` = `^\d{3,6}/\d{4}$` rejects real CNBV expedientes
  (`A/AS1-1111-222222-AAA`, `H/IN1-…`, `E/DE-3333-4444444-AAA`, `A/AS1-4444-5555555-HHHH`).
- C2: `LlmExpedienteDto` has no `NumeroOficio`/`AutoridadNombre`; prompts never request them; mapper never sets them.

## Overriding principle (CNBV legal): a plausible WRONG value is worse than an abstention.
Every extension below must ABSTAIN (null the field; reconciler review-flags) rather than emit a guessed value.

## Scope
IN (all DARK path — flags default false → **zero production runtime impact**): DTO fields, gate parity
regexes, text+vision prompts, mapper wiring, reconciler per-field merge for the two new fields, unit tests.
OUT: flipping any flag; pipeline graduation (S4-C); changing the DETERMINISTIC extractor (it is the source
of truth — REUSE its patterns, do not touch it).

## Blast radius (files that change)
- `02 Infrastructure/Infrastructure.Extraction.Txt/Llm/LlmExpedienteDto.cs` — add 2 props.
- `…/Llm/LlmExtractionGate.cs` — new anchored expediente regex + oficio regex; field-level abstention.
- `…/Llm/LlmExpedienteMapper.cs` — set the 2 new fields on `Expediente`.
- `…/Llm/LlmTxtFieldExtractor.cs` and `02 Infrastructure/Infrastructure.Extraction/Llm/LlmVisionFieldExtractor.cs` — prompts.
- `02 Infrastructure/Infrastructure.Extraction/Llm/ExtractionReconciler.cs` — 2 merge blocks.
Test projects that MUST stay green: `Tests.Infrastructure.Extraction.Txt` (gate/DTO/mapper/txt),
`Tests.Infrastructure.Extraction` (vision/reconciler). Full-solution build 0/0.

## Design decisions (grounded — cite these anchors)

### 1. Expediente gate regex — ANCHORED PARITY with the deterministic extractor
Deterministic `AdaptiveTxtFieldExtractor.ExtractExpediente` (`:316`) accepts (unanchored search):
`[A-Z]/[A-Z]{1,4}\d*[-–]\d+[-–]\d+[-–][A-Z]+`. The gate validates a single field value, so use the
**anchored** form: `^[A-Z]/[A-Z]{1,4}\d*[-–]?\d+[-–]?…$` — precisely:
`^[A-Z]/[A-Z]{1,4}\d*[-–]\d+[-–]\d+[-–][A-Z]+$` (accept both hyphen `-` and en-dash `–` like the
deterministic pattern). **Rationale:** a value the deterministic extractor would accept is exactly a value
the gate accepts — no drift, no second source of truth. REPLACE the `ddd/yyyy` regex (all CNBV gold is the
new format; the old format never appears in the corpus). Keep `RegexOptions.CultureInvariant`.
Honesty note: this is intentionally not looser than the deterministic pattern — do NOT broaden it to bare
"alphanumeric" (that reintroduces the boundary-shift false-match risk M3 flagged in the metric layer).

### 2. DTO
Add to `LlmExpedienteDto`: `[property: JsonPropertyName("numeroOficio")] string? NumeroOficio` and
`[property: JsonPropertyName("autoridadNombre")] string? AutoridadNombre`. Nullable, like all others.

### 3. Gate policy for the new fields — FIELD-LEVEL abstention, not whole-DTO rejection
- NumeroOficio: if present and does NOT match the anchored deterministic oficio shape
  (`^[A-Z]{4,}[A-Z0-9]{0,10}/\d{4}/\d{6}$`, from `AdaptiveTxtFieldExtractor.cs:377`), the extractor must
  drop it to null (abstain) rather than reject the whole DTO. (Prefer implementing abstention in the
  extractor/mapper path; keep `LlmExtractionGate.Validate` returning a reason only for hard-invalid DTOs.)
- AutoridadNombre: free text. Minimum honesty guard: non-empty after trim AND length ≥ 5 AND contains at
  least one space (rejects single-token garbage like "XZ"). Below that → abstain (null).
- Do NOT add these to the `hasAnyCore` gate (they are not "core identifying" fields).
These are soft guards; the REAL guard is the reconciler deterministic cross-check (§4).

### 4. Reconciler — mirror the NumeroExpediente honesty block (`ExtractionReconciler.cs:73-81`)
`CopyExpediente` already starts `best` from a full deterministic copy, so a deterministic non-empty
NumeroOficio/AutoridadNombre already wins (deterministic-wins holds for free). Add two merge blocks that
FILL ONLY WHEN deterministic is absent, using the same helper the NumeroExpediente block uses:
- if `string.IsNullOrWhiteSpace(best.NumeroOficio)` → merge from LLM candidates; two LLM candidates
  disagree → leave null + review flag (never coin-flip). Same for `AutoridadNombre`.
The existing comment "header fields copied wholesale … rarely extracted by LLMs" (`:19-20`) becomes stale —
update it to state oficio/authority now have an LLM fallback but deterministic still wins.

### 5. Mapper
In `LlmExpedienteMapper.ToExpediente`: after the existing sets, add
`if (!string.IsNullOrWhiteSpace(dto.NumeroOficio)) expediente.NumeroOficio = dto.NumeroOficio.Trim();`
and the analogous `AutoridadNombre`. (Both are first-class `Expediente` props, not `AdditionalFields`.)

### 6. Prompts (text + vision)
Extend the JSON schema block in both prompts to (a) request `expediente` in the CNBV format with a concrete
example (`A/AS1-1111-222222-AAA`) instead of `ddd/yyyy`, and (b) add `numeroOficio`
(example `AGAFADAFSON2/2025/000084`) and `autoridadNombre` (example
"Comisión Nacional Bancaria y de Valores"), each "…, o null si no aparece". Instruct: return null rather
than guessing. Keep the existing partes/rfc/curp/monto instructions.

## Tests (deterministic, CI-safe, no live Ollama — canned JSON + pure gate/mapper/reconciler)
Name `Method_Scenario_Expected`. Must include:
- Gate: `Validate_CnbvExpediente_Accepted`; `Validate_LegacyDddYyyyExpediente_Rejected`;
  `Validate_BoundaryShiftedExpediente_Rejected` (a wrong grouping that must NOT pass).
- Abstention: oficio not matching shape → mapped null; authority single-token → mapped null.
- Mapper: `ToExpediente_SetsNumeroOficioAndAutoridad_WhenPresent`.
- Reconciler: `Reconcile_DeterministicOficioPresent_DeterministicWins`;
  `Reconcile_DeterministicOficioAbsent_LlmFills`; `Reconcile_TwoLlmOficioConflict_NullAndReviewFlag`
  (and the AutoridadNombre analogues).
- Extractor (canned ILlmProvider JSON): the DTO round-trips the two new fields into the Expediente.

## Then: measure (folds in S4-A eval bug fixes — see TRACKER task 7)
Fix C3 (AggregateAccuracy exclude TrackSkipped) + C4 (per-fixture failure→TrackSkipped) + M1 (same OCR text
into deterministic & LLM-text) + M2 (diacritic fold) + M3 (boundary-shift guard test). Then run the harness
live (text=`llama3.1:8b`, vision=`gemma3:12b`) — now all 4 fields are genuinely attempted → an honest
baseline. Commit baseline + refresh ADR-024 numbers.

## Standards
.NET 10, `Result<T>` (never throw for business logic), `ConfigureAwait(false)` in library code,
warnings-as-errors, xUnit v3 + Shouldly + NSubstitute, `Method_Scenario_Expected`,
`TestContext.Current.CancellationToken`. Gate/mapper/reconciler stay PURE (no I/O) — the deterministic test anchor.

## Definition of done
Solution build 0/0; the two extraction test projects green (report counts); DARK flags still default false;
`git diff` touches only the 6 files above + tests; the extended fields are proven by unit tests to abstain
on malformed input and to let deterministic win. Then measurement (task 7 + live run) produces the honest baseline.
