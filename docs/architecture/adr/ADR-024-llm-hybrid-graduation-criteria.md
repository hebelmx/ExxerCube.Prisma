# ADR-024: Graduation Criteria for the LLM/Hybrid Field-Extraction Path

**Date**: 2026-07-03
**Status**: Accepted — this ADR ratifies the **measurement bar**, not a graduation event. No flag is
flipped and no production code changes by virtue of this document (see Scope).
**Deciders**: Owner + Development Team (via `/bmad-orchestrator`, S4-A)
**Tags**: llm, hybrid-extraction, evaluation, graduation-gate, feature-flag, reconciler, ocr, expediente
**Related**: `docs/implementation-artifacts/spec-llm-hybrid-extractor.md` (S1–S3b contracts, dark flags,
reconciler honesty policy), `docs/implementation-artifacts/spec-llm-hybrid-extractor-S4A.md` (this
story's intended solution), `docs/evaluation/llm-hybrid-extraction-baseline-2026-07.md` (D3, evidence
source), ADR-023 (Unified Confidence Model — sibling precedent for corpus-gated, config-tunable
thresholds)

---

## Context

### Why this ADR exists

The LLM/hybrid field-extraction path (S1–S3b) shipped **dark**: `LlmProviders:TextExtractorEnabled`
and `LlmProviders:VisionExtractorEnabled` both default `false`
(`Infrastructure.Classification/Llm/LlmProvidersOptions.cs:23,28`). Flipping either flag currently has
**zero effect on production document processing** — see the two-gate distinction below — but no
measurable evidence existed that the LLM tracks match or beat the deterministic extractor on the fields
that matter. S4-A (`spec-llm-hybrid-extractor-S4A.md`) built out the evaluation harness
(`Tests.Infrastructure.Extraction/LlmExtractionEvalHarness.cs`) to produce that evidence and committed
it as a baseline artifact (D3). This ADR is D4: it converts that evidence requirement into **explicit,
measurable graduation criteria** so that any future decision to flip a flag — demo or production — is
made against a stated bar rather than a feeling.

**This ADR does not itself authorize flipping any flag.** It sets the bar; a future, separately-gated
story evaluates the live baseline (and any later, larger-corpus run) against the bar and makes the
call.

### The two-gate distinction (critical — read before anything else in this document)

There are **two structurally different gates** hiding behind the phrase "turn on the LLM extractor,"
and conflating them is the single biggest risk this ADR guards against.

**Gate A — Demo-visible (`/hybrid-extraction` page only).**
`LlmProviders:TextExtractorEnabled` / `VisionExtractorEnabled` are read **inside**
`HybridExtractionService.ExtractAsync` (`Infrastructure.Extraction/Llm/HybridExtractionService.cs:127,185`).
That service is registered and reachable **only** from
`07 UI/UI/ExxerCube.Prisma.Web.UI/Components/Pages/HybridExtraction.razor` — a single authenticated demo
page (S3b). Flipping these flags changes what that one page shows. It has **no effect whatsoever** on
any document that flows through the real Athena processing pipeline.

**Gate B — Pipeline-graduated (production Athena worker).**
The production worker binds `IFieldExtractor<TxtSource>` to `AdaptiveTxtFieldExtractor`
**unconditionally**, with no flag check:
`04 Services/Athena/Prisma.Athena.Worker/Program.cs:174` —
`builder.Services.AddSingleton<IFieldExtractor<TxtSource>, AdaptiveTxtFieldExtractor>();` — and nothing
in `ProcessingOrchestrator` / `ExtractionOrchestrator` / `ExtractionPipelineService` calls
`IHybridExtractionService`. Every document that is actually exported today is extracted **exclusively**
by the deterministic path, full stop, regardless of either flag's value. Making the LLM/hybrid path
influence a real, exported document requires a separate, deliberate wiring change — rewiring the
`IFieldExtractor<TxtSource>` (and/or `PdfSource`) binding, or inserting `IHybridExtractionService` into
the orchestrator call chain. **That wiring change is out of scope for this ADR** — it is the future
story S4-C. This ADR only states the evidence bar S4-C must clear before it is authorized.

Consequence: "flip the demo flag" is a **low-blast-radius, reversible, single-page UI change**.
"Graduate into the pipeline" is a **production behavior change to every processed document** and
carries a correspondingly higher evidence bar. The two must never be described or approved as the same
decision.

### Evidence source and its honest limits

The measurement evidence is `docs/evaluation/llm-hybrid-extraction-baseline-2026-07.md` (+ its `.json`
companion), produced by a **live run** of `LlmExtractionEvalHarness` against Ollama
(`llama3.1:8b` text / `gemma3:12b` vision — the models actually installed on the build box, not the
config defaults `llama3.2` / `minicpm-v`) over the PRP1 gold fixtures. Three tracks are compared —
Deterministic (`AdaptiveTxtFieldExtractor`, the bar to beat), LLM-text (`LlmTxtFieldExtractor`), and
LLM-vision (`LlmVisionFieldExtractor`) — on four gold fields per fixture: `NumeroExpediente`,
`NumeroOficio`, `AutoridadNombre`, and `ParteCount` (count of `SolicitudPartes` — the **headline**
field per the parent spec).

This baseline is honestly limited:

- **N = 3** (PRP1 fixtures `222AAA`, `333BBB`, `333ccc`) — a directional signal, not a statistically
  powered claim. A per-field "accuracy" of 2/3 or 3/3 is not distinguishable from noise at this N.
- **Ollama-only.** No Gemini API key exists on the build box; the Gemini track is structurally
  supported but reports `SkippedNoKey`. The criteria below therefore certify **Ollama specifically**;
  Gemini (or any future provider) needs its own evidence run before it inherits graduation status —
  see Decision D4.
- Matching is normalized-exact (trim, collapse whitespace, case-insensitive for authority;
  digit/format-normalized for expediente/oficio); it does not credit "close" answers.

**Baseline numbers** (from `llm-hybrid-extraction-baseline-2026-07.md`, live run 2026-07-04 04:24Z).
Per-field accuracy = matches / fixtures-with-gold; coverage = non-null candidates / total evaluations.

| Field | Deterministic accuracy | LLM-text accuracy | LLM-vision accuracy | Deterministic coverage | LLM-text coverage | LLM-vision coverage |
|---|---|---|---|---|---|---|
| NumeroExpediente | 0% (0/3) | — (0 evaluable) | 0% (0/1) | (track) 50% | (track) 0% | (track) 25% |
| NumeroOficio | 0% (0/3) | — (0 evaluable) | 0% (0/1) | 6/12 | 0/12 | 3/12 |
| AutoridadNombre | 0% (0/3) | — (0 evaluable) | 0% (0/1) | | | |
| ParteCount (SolicitudPartes) | — (track-skipped, architectural) | — (0 evaluable) | 0% (0/1) | | | |

N = 3 (`222AAA-…`, `333BBB-…`, `333ccc-…`); models `llama3.1:8b` (text) / `gemma3:12b` (vision); run
date 2026-07-04 04:24Z. Coverage is per-track (12 evaluations = 3 fixtures × 4 fields): Deterministic
6/12, LLM-text 0/12, LLM-vision 3/12.

> ### ⚠️ D3 measurement-validity finding — THIS BASELINE DOES NOT YET CERTIFY OR REFUTE ANY TRACK
>
> **Both** the deterministic oracle **and** the LLM tracks score ~0% accuracy — and when the
> designated bar-to-beat also scores zero, the measurement, not the extractor, is what failed. Direct
> inspection of the committed OCR text (`222AAA-…_page-0001.ocr.txt`) confirms the root cause: **the
> eval gold values are not present in the document's OCR-able text.**
>
> | Gold value (from XML/filename) | Appears in OCR text? | What the extractors actually read (present in text) |
> |---|---|---|
> | expediente `A/AS1-1111-222222-AAA` | **No (0 matches)** | (deterministic returns Missing; LLM emits the oficio-shaped string) |
> | oficio `222/AAA/-4444444444/2025` | **No (0 matches)** | `AGAFADAFSON2/2025/000084` (real oficio in the body) |
> | authority `SUBDELEGACION 8 SAN ANGEL` | **No (0 matches)** | `Comisión Nacional Bancaria y de Valores` (the addressee/intermediary) |
>
> The gold is synthetic, filename/XML-derived identifiers that were **never rendered into the PDF
> body**; the extractors are correctly reading the real strings that *are* in the document. The 0%
> is therefore an **eval-gold artifact**, not extractor quality. This is the empirical confirmation of
> precondition **D7.1 (source-text containment guard)** below — it is now observed, not hypothesized.
>
> **Consequence:** D1/D2 accuracy comparisons are **not yet evaluable**. Before this harness can gate
> anything (Gate A or B), the gold set must be rebuilt/validated so every gold field is source-contained
> in the OCR text of the fixture it is keyed to (owner-gated corpus work — see D7.1). Until then this
> artifact certifies only that **(a) the harness runs correctly end-to-end and (b) the honesty gate
> holds**: in 5 of 6 LLM track-fixtures the gate correctly refused to emit a wrong expediente rather
> than pass a plausible-but-wrong value (upholding the "a wrong value is worse than an abstention"
> invariant). The one non-rejected case (333ccc vision) abstained on expediente (null) and surfaced
> oficio + authority candidates, proving S4-B's new fields *are* wired and reachable.
>
> **Secondary finding (gate coupling):** the gate rejects the *entire* DTO when `expediente` is
> present-but-invalid, discarding otherwise-usable oficio/authority/partes on the same document. This
> is correct for honesty but throttles coverage; whether to loosen it to per-field abstention (keep the
> valid fields, null only the bad expediente) is a Gate-B design question, logged here, not changed in S4.

### ✅ D3-golden — TRUSTWORTHY baseline on the source-contained corpus (2026-07-04 05:51Z)

The measurement-validity problem above is **resolved.** A purpose-built golden corpus
(`Prisma/Fixtures/PRP1-golden/`, 20 docs, gold = generator god's-eye `ground_truth.json`, every gold
field verified source-contained at generation) was run through the same harness
(`Eval_PRP1Golden_…`, models `llama3.1:8b`/`gemma3:12b`). Artifact:
`docs/evaluation/llm-hybrid-extraction-baseline-golden-2026-07.md`.

| Field | Deterministic acc | LLM-text acc | LLM-vision acc |
|---|---|---|---|
| NumeroExpediente | **85% (17/20)** | 100% (3/3)* | — (no page images) |
| NumeroOficio | **100% (20/20)** | 0% (0/3)* | — |
| AutoridadNombre | **0% (0/20)** | 67% (2/3)* | — |
| ParteCount | — (architectural skip) | 33% (1/3)* | — |

Coverage: Deterministic 57/80 (71%), LLM-text 9/80 (11%), LLM-vision 0/80 (0%). *LLM-text N is only 3
— see finding (2).

**The measurement is now valid** — the deterministic bar-to-beat scores 85–100% on expediente/oficio
(vs 0% on the client fixtures), proving the harness measures real extraction quality, not a gold artifact.
Three actionable findings — the payoff of a trustworthy baseline:

1. **Deterministic `AutoridadNombre` = 0/20 (real production defect).** The deterministic
   `AdaptiveTxtFieldExtractor` consistently returns the constant **recipient** ("Comisión Nacional
   Bancaria y de Valores") instead of the **requesting** authority (the gold). The LLM-text track gets
   **67%** here where it runs — a genuine S4-B value signal (the LLM finds the requesting authority the
   deterministic path misses). → File a production ticket for the deterministic authority extractor.

2. **LLM-text coverage is only 11% — the production gate skips 17/20 — and the dominant cause is the
   `Monto` format check, not expediente.** The LLM returns monto with currency formatting
   (`$9,976,691.72`); the gate rejects it as "not a valid decimal" and discards the **whole DTO**
   (expediente/oficio/authority included). This is the **top lever** for the LLM path and a concrete
   instance of the Gate-coupling concern above: (a) relax monto parsing to accept `$`/thousands
   separators, and/or (b) move to per-field abstention so a bad monto nulls only monto. Until then S4-B's
   LLM gains are **masked** (the 3 evaluable docs are a biased sample — those where the LLM happened to
   omit monto). **D1/D2 verdict: not yet decidable for LLM-text** — N=3 and gate-selection-biased.

3. **LLM-vision = 0% coverage (harness/corpus gap, not a model result).** `PRP1-golden` ships no
   pre-rendered page images, so `LoadFixturePageImages` returns empty and the vision track structurally
   skips. Fix: render PDF→images on the fly for the golden set (the deterministic track already does this
   via `PdfToImageConverter`) — tracked as a harness follow-up before vision can be judged.

**Did S4-B work? (honest read):** promising but not yet provable. Where observable, the LLM adds real
value (requesting-authority 67% vs deterministic 0%; expediente 100% on the tiny N). But its impact is
**gated away** on 85% of docs by the monto check, and vision is unmeasured. The trustworthy baseline's
real product is not a pass/fail — it's the precise identification of the **two blockers to fix next**
(monto gate coupling; vision page-image rendering) plus a **confirmed production bug** (deterministic
authority). Deterministic remains the bar to beat on expediente (85%) and oficio (100%).

### ✅ D3-golden-v2 — post-fix re-baseline: monto gate fixed + vision track live (2026-07-04 13:01Z)

Findings (2) and (3) above are now **fixed**, and the harness was re-run on the same 20-doc golden
corpus (artifact regenerated at the same path, 2026-07-04 13:01Z; models `llama3.1:8b`/`gemma3:12b`;
run ~10m49s — vision inference over rasterized pages dominates). Both LLM tracks now report together.

- Fix for (2): `LlmExtractionGate.TryParseMonto` strips `$`/`MXN`/`USD`/`EUR`/thousands before parsing,
  cents preserved (commit `10312e51`) — the monto check no longer discards currency-formatted DTOs.
- Fix for (3): the harness rasterizes each golden PDF on the fly at 150 DPI via `PdfToImageConverter`
  when no page images are on disk (commit `e68753dd`) — the vision track now runs.

| Field | Deterministic acc | LLM-text acc | LLM-vision acc |
|---|---|---|---|
| NumeroExpediente | **85% (17/20)** | **94% (15/16)** | 75% (6/8) |
| NumeroOficio | **100% (20/20)** | **0% (0/16)** | 25% (2/8) |
| AutoridadNombre | **0% (0/20)** | **81% (13/16)** | 38% (3/8) |
| ParteCount | — (architectural skip) | 38% (6/16) | 100% (8/8) |

Coverage: Deterministic 57/80 (71%, unchanged — path untouched, so no regression), **LLM-text 48/80
(60%, up from 11%)** (~12 of the 13 unlocked docs attributable to the monto fix; 1 flipped due to
Ollama run-to-run variance), **LLM-vision 26/80 (33%, up from 0%)**.

> ⚠️ **Denominator caveat (adversarial-review finding, 2026-07-04).** The LLM-track accuracy columns
> above are **per-attempt** — their denominator is the *gate-conditioned* Evaluable count (docs the LLM's
> own gate did **not** self-reject), whereas the Deterministic column counts its extraction misses as
> `Missing` in a full-20 denominator. So the two are **not** directly comparable. On a **denominator-matched
> full-20 basis** (abstentions counted as failures — the fair basis for a track intended to *replace* the
> deterministic path in production): LLM-text NumeroExpediente = **15/20 = 75%** and AutoridadNombre =
> **13/20 = 65%**. Note D1's bar (line ~240) is currently written on the gate-conditioned number; a future
> revision should add an explicit full-N column and decide which denominator the bar uses.

**Did S4-B work? Now decidable — YES for its central claim:**
- **AutoridadNombre: LLM-text 81% per-attempt (13/16) / 65% full-N (13/20) vs deterministic 0%.** This is
  S4-B's central claim and it **survives even the strict full-N accounting** — the LLM recovers the
  *requesting* authority the deterministic path structurally cannot (it returns the constant CNBV
  recipient). Clears D1's bar on the text track under either denominator.
- **NumeroExpediente: LLM-text 94% per-attempt (15/16) but 75% full-N (15/20) vs deterministic 85%.**
  Once abstentions count as failures, **deterministic still wins** the bar-to-beat field. The LLM is
  strong *when it commits*, but its self-abstention rate keeps it below deterministic on full-corpus
  expediente. Do **not** read this as "LLM beats deterministic on expediente."

**Two real gaps surfaced by the now-trustworthy N (were hidden at N=3):**
4. **LLM-text NumeroOficio = 0% (0/16) — a specific S4-B text-path gap.** Every text candidate is empty,
   yet the vision path emits oficio (2/8) via the *identical* metric/DTO/gate/mapper code and the
   deterministic path nails it (100%) — so the field *is* extractable and the metric is not at fault. What
   the artifact **cannot** yet distinguish (it records only the post-gate mapped candidate, never the raw
   LLM JSON): whether the text model (a) never emits `numeroOficio`, or (b) emits it in a shape that fails
   `IsPlausibleNumeroOficio`'s regex every single time and is silently abstained. These need different
   fixes (prompt vs. gate calibration). → next lever: log the raw pre-gate LLM JSON for the text track to
   disambiguate, then fix accordingly.
5. **LLM-vision ~60% still gated by multi-dot monto** (`58665.271.89`, `4.526.265.05`). The vision model
   emits ambiguous dot-as-thousands numbers; the gate **correctly refuses to guess** (honesty invariant —
   a misread digit must not become a plausible-wrong value). This caps vision coverage but is *correct*
   behavior, not a bug to force-parse away. Improving it means **prompt-constraining the vision monto
   format**, not loosening the gate.

**Verdict:** S4-B is **validated for its central claim — AutoridadNombre** — on the trustworthy corpus
(65% full-N / 81% per-attempt vs deterministic 0%; survives strict accounting); the N=3 masking is removed.
On **NumeroExpediente, deterministic remains the bar** (85% full-N vs LLM-text 75% full-N) — the LLM is
strong when it commits but abstains too often. Deterministic also remains the clear bar on oficio (100%).
Remaining LLM work is now precisely scoped: LLM-text oficio (gap 4 — disambiguate emit-vs-gate first),
vision monto-format prompting (gap 5), and reducing the text track's expediente self-abstention rate. The
deterministic `AutoridadNombre` production bug (finding 1) is unchanged and still needs a ticket. **D1/D2
for `llm-text`:** authority passes under either denominator; expediente passes per-attempt but **not**
full-N — so the graduation decision hinges on which denominator D1 adopts (see the caveat above).
`llm-vision` remains below bar and gated.

### ✅ D3-golden-v3 — gap-4 (LLM-text NumeroOficio 0/16) diagnosed; prompt fix REJECTED as net-negative (2026-07-04)

Gap 4 from D3-golden-v2 ("LLM-text NumeroOficio = 0/16 — emit-side vs gate-side unknown") is now
**diagnosed to root cause**, and a candidate fix was **built, measured against the golden corpus in a
controlled paired run, and deliberately NOT shipped** because it reproducibly regresses two
more-valuable fields to fix a redundant one.

**Root cause (emit-side, semantic — confirmed by probing `llama3.1:8b` directly on the golden OCR text):**
the text model emits the value literally labelled *"No de oficio de requerimiento / orden de revisión /
auditoría"* (e.g. `OF-REV-749-2026`) instead of the **SIARA folio** (`SHCP/2023/631352`, labelled *"No.
De Identificación del Requerimiento"*) that the gold — and the XML schema `<Cnbv_NumeroOficio>` — treat
as `numeroOficio`. That `OF-REV-…` value then fails the anchored folio regex
(`^[A-Z]{4,}[A-Z0-9]{0,10}/\d{4}/\d{6}$`) in `LlmExtractionGate.IsPlausibleNumeroOficio`, so the mapper
silently abstains → **0/16, surfaced as empty (honest), not wrong.** So it is *both* an emit-side
semantic miss *and* a (correctly-strict) gate abstention — the regex is not the bug; relaxing it would
only convert "empty" into "confidently wrong".

**Candidate fix:** a terse prompt disambiguation on the `numeroOficio` line — *"folio del requerimiento
… con diagonales; no uses códigos con guiones"* (a slash-vs-hyphen structural discriminator). Applied
identically to the text and vision prompts.

**Controlled paired-run result (golden corpus, same session, `llama3.1:8b`/`gemma3:12b`; the old-prompt
control reproduced the committed baseline *exactly*, confirming temp-0 determinism → the deltas below are
CAUSAL, not run-to-run noise):**

| LLM-text field | Old prompt | New prompt | Δ |
|---|---|---|---|
| NumeroOficio | 0/16 (0%) | **14/15 (93%)** | ✅ fixed |
| NumeroExpediente | 15/16 (94%) | 12/15 (80%) | ❌ **regressed** |
| AutoridadNombre | 13/16 (81%) | 9/15 (60%) | ❌ **regressed** |

**Decision: do NOT ship the prompt change (text track).** Three reasons: (1) it reproducibly steals the
8B model's attention from **NumeroExpediente** (the bar-to-beat field, D1) and **AutoridadNombre**
(S4-B's validated central win) to fix (2) a **redundant** field — the deterministic path already extracts
NumeroOficio at **100% (20/20)** and the reconciler is deterministic-wins, so the LLM's oficio value
never reaches the reconciled output; (3) fixing a field that costs two better ones and changes no product
output is a bad trade. The durable value of this epic is the **diagnosis**, not a code change.

**Nuance (possible vision-only follow-up, owner-gated — it breaks the text/vision prompt-parity
invariant):** the *same* change **helped the vision track** (`gemma3:12b`): NumeroOficio 2/8 → 8/8,
NumeroExpediente 6/8 → 7/8, AutoridadNombre 3/8 → 2/8. The larger vision model absorbs the extra
instruction without losing other fields; the 8B text model cannot. Applying the fix to the vision prompt
only is a defensible future option but was deferred (redundant field + parity-invariant change → not
worth an autonomous divergence).

**Bonus, independently confirmed by these runs:** the deterministic `AutoridadNombre` production fix
(commit `5a4d0b86`, finding 1) is verified end-to-end on the real harness — Deterministic AutoridadNombre
**0/20 → 18/20 (90%)** on the golden corpus.

---

## Decision

### D1 — Per-field accuracy bar (applies to both gates)

For a given LLM track (`llm-text` or `llm-vision`, evaluated independently — see D5) to be considered
for graduation through **either** gate, its per-field accuracy on the baseline artifact must satisfy,
for **every one** of the four measured fields:

```
LLM-track accuracy(field) >= Deterministic accuracy(field)
```

Ties pass (`>=`, not `>`) — the bar is "no worse than deterministic," not "must exceed it." A single
field falling below the deterministic bar fails the whole track for that gate; there is no averaging
across fields that lets a strong field compensate for a weak one. Rationale: each field is
independently load-bearing downstream (expediente number drives case matching, oficio number drives
document identity, authority name drives fusion source-reliability weighting per ADR-023 D2); a
regression hidden by averaging is exactly the failure mode this ADR exists to prevent.

### D2 — SolicitudPartes no-regression rule (the headline-field rule)

`ParteCount` (`SolicitudPartes`) is called out separately, not merely as "one of the four fields in
D1," because it is the parent spec's headline field and the reason the LLM path exists in the first
place (deterministic extraction cannot populate `SolicitudPartes` at all today — see
`HybridExtractionService.MapToExpediente`, which only the LLM tracks populate via
`ILlmExpedienteExtractor<T>.ExtractExpedienteAsync`). Two rules, not one:

1. **No-regression vs. deterministic** — same as D1: `LLM ParteCount accuracy >= Deterministic
   ParteCount accuracy`. Since deterministic today produces zero partes, this bar is low by
   construction; it exists as a tripwire in case `AdaptiveTxtFieldExtractor` is ever extended to
   attempt partes extraction.
2. **No-regression vs. an empty/null result** — an LLM track must never report a `ParteCount` that is
   **lower than the true count while claiming success** (a confidently-wrong, non-abstaining answer).
   A track that cannot determine partes must surface `TrackSkipped` / a failed status (already modeled
   by `TrackStatus.Failed` / `SkippedNoCapability`) rather than emit a plausible-looking wrong count.
   This is an anti-hallucination rule specifically for the field the reconciler cannot cross-check
   against a deterministic value (D1's safety net does not exist for `SolicitudPartes` because
   deterministic never populates it).

### D3 — Coverage floor

Coverage (non-null responses / total fixtures with gold, per track per field) must be **≥ 80%** for a
track to be eligible for either gate. Coverage measures "does the track answer at all," independent of
correctness (D1/D2 measure correctness). A track that abstains on most documents cannot graduate
regardless of how accurate its rare answers are — it isn't doing the job.

> **Decision needing confirmation** — the spec (`spec-llm-hybrid-extractor-S4A.md`, D4) states the
> criteria shape as "coverage ≥ X" without fixing X. 80% is this ADR's proposed default (chosen to
> tolerate one `TrackSkipped` out of the N=3 baseline while still requiring the track answer on the
> other two); it is **not derived from the spec or from data** and should be explicitly confirmed or
> overridden by the owner before being treated as binding, especially once N grows (see D6).

### D4 — Provider scope

Graduation status (through either gate) attaches to a **specific (track, provider, model)** tuple, not
to "the LLM path" abstractly. The baseline artifact certifies Ollama with `llama3.1:8b` (text) /
`gemma3:12b` (vision) — the models actually installed on the build box, not `LlmProvidersOptions`'
defaults (`llama3.2` / `minicpm-v`, per `OllamaProviderOptions.cs:48,51`). Switching `Active` to
`"Gemini"` (or changing the Ollama model tag) requires its own evidence run against this same bar
before inheriting graduated status — a config change to `LlmProviders:Active` is not itself evidence.

### D5 — Tracks are evaluated independently

`TextExtractorEnabled` and `VisionExtractorEnabled` gate independently in
`HybridExtractionService` (`:127` and `:185` respectively), and D1–D3 are evaluated per track. It is
possible for `llm-text` to clear the bar while `llm-vision` does not (or vice versa); each flag's flip
decision (Gate A) stands on its own track's evidence. There is no rule in this ADR requiring both
tracks to graduate together.

### D6 — Production (Gate B) precondition: corpus size

Gate A (demo-flag flip) may proceed on the **N=3 directional baseline alone**, subject to D1–D5 and the
honesty guardrails below — the demo is a single opt-in, authenticated page whose output already passes
through the same reconciler used in production (see Guardrails), so its blast radius is bounded even
if the small-N evidence later proves optimistic.

Gate B (pipeline graduation, S4-C) additionally requires, **before it is authorized**, a labelled
evaluation corpus materially larger than N=3 — stratified across document type (PDF text quality
tiers, at minimum) and re-run through the same harness and the same D1–D3 bar. N=3 is not a credible
basis for a change that affects every document a real client processes.

> **Decision needing confirmation** — neither spec commits to a minimum N for Gate B. This ADR proposes
> **N ≥ 30** fixtures, stratified across at least the quality tiers already modeled by
> `PolynomialImageQualityAnalyzer` / `ImageQualityLevel` (so a corpus that is accidentally all
> high-quality scans doesn't launder a low-quality blind spot), as a starting floor — not a
> statistically rigorous power calculation, and not sourced from the spec. The owner should confirm
> this number (or set a different one) before it is treated as a hard gate for S4-C.

---

### D7 — Source-provenance preconditions (Gate B) — from the S4-B adversarial honesty gate (2026-07-03)

The S4-B extension (LLM now attempts NumeroOficio / AutoridadNombre / CNBV expediente) added **shape**
guards (`LlmExtractionGate.IsPlausibleNumeroOficio` / `IsPlausibleAutoridadNombre` + the anchored
expediente/oficio regexes) but **no provenance** guard. The adversarial gate confirmed the following
residual risks. None corrupts the **measurement baseline** (a hallucinated value that is not the model
echoing a prompt example will not match gold → it scores as an honest mismatch), and the CRITICAL
example-echo path was already closed by replacing the prompt few-shot examples with synthetic,
non-corpus placeholders. But each is a **production-honesty** risk and therefore a **Gate-B precondition**:

- **D7.1 — Source-text containment guard (required before Gate B).** No code path checks that an
  LLM-returned Expediente / NumeroOficio / AutoridadNombre actually appears in the source OCR/text.
  A shape-valid fabrication (regex-conforming but absent from the document) currently passes. Before
  pipeline graduation, add a containment check (normalized substring / fuzzy match against the source
  text) for the two structured fields (expediente, oficio) so a value that does not occur in the
  document is abstained. (Authority is free-text and OCR-variable — verbatim containment is fragile
  there; treat separately.)
- **D7.2 — Authority guard is coverage-only, not anti-hallucination (residual).** The current
  `IsPlausibleAutoridadNombre` (non-empty, len ≥ 5, contains a space) rejects single-token garbage but
  admits plausible multi-word wrong values (e.g. a person's name, a copied fragment). Because the
  deterministic path is authoritative and the LLM authority only fills when deterministic is absent,
  this is an accepted residual for Gate A; Gate B should pair it with D7.1 or a known-authority set.
- **D7.3 — Guard is over-strict for legitimate short authority acronyms.** Bare `SAT` / `CNBV`
  (len < 5, no space) are legitimate deterministic authority values but would be force-abstained if only
  the LLM produced them. Does not affect the PRP1 baseline (its gold authority is the long CNBV name),
  but before relying on the LLM authority track in production, allow a known-acronym set.

> These are logged here rather than fixed in S4-B because S4-B's scope was "extend + measure", and none of
> them changes what the honest measurement baseline reports. They become hard Gate-B checklist items.

---

## Flip procedure

**Gate A (demo-visible) — reversible, single-page blast radius**

1. Confirm the current baseline artifact (`docs/evaluation/llm-hybrid-extraction-baseline-2026-07.md`)
   shows the track(s) to be flipped clearing D1 (per-field accuracy), D2 (`SolicitudPartes`
   no-regression), and D3 (coverage ≥ 80%) for the specific provider/model in D4.
2. Set `LlmProviders:TextExtractorEnabled` and/or `LlmProviders:VisionExtractorEnabled` to `true` in
   the target environment's configuration (`appsettings.{Environment}.json`, user-secrets, or env var
   override — never hand-edit the checked-in default, which must remain `false`).
3. Smoke-test `/hybrid-extraction` against at least one known fixture per gold field and visually
   confirm the reconciled result and the per-track candidate audit trail (the page already surfaces
   both, per `HybridExtractionService.ExtractAsync`'s full-candidate-list contract).
4. Record the flip (date, environment, provider/model, linked baseline artifact commit) in the demo's
   operational notes — this ADR does not mandate a specific log location, but the flip must be
   traceable back to the evidence that authorized it.
5. **Gate A does not touch Athena Worker configuration or `Program.cs` bindings** — verify no
   production config file was edited as part of this flip.

**Gate B (pipeline-graduated) — out of scope for this ADR.** S4-C must (a) show the larger corpus of D6
clearing D1–D3, (b) design and review the actual wiring change (which `IFieldExtractor<T>` binding
changes, whether it's a full replacement or an additive hybrid call, how it interacts with
`ExtractionOrchestrator`/`ProcessingOrchestrator`'s existing `IFieldExtractor<TxtSource>` dependency),
and (c) get its own explicit sign-off — this ADR does not pre-authorize any part of that wiring.

### Rollback procedure (Gate A)

1. Set the flipped flag(s) back to `false` in the environment configuration used in step 2 above (or
   simply remove the override so the checked-in default of `false` applies).
2. No code deploy is required — this is a config-only rollback, by design (the flags are the entire
   surface area `HybridExtractionService` checks).
3. Re-verify `/hybrid-extraction` returns to deterministic-only behavior (LLM candidates report
   `TrackStatus` values indicating the flag-skip path, e.g. the disabled-by-flag log line at
   `HybridExtractionService.cs:181` / `:251`).
4. No rollback procedure is defined for Gate B here, since Gate B is not authorized by this ADR; S4-C
   must define its own rollback (which is a materially harder problem — a production wiring change,
   not a config flag — and is exactly why Gate B's evidence bar in D6 is higher).

---

## Honesty guardrails (graduation must never trade correctness for coverage)

These are not new rules — they are the S2 reconciler policy already shipped
(`Infrastructure.Extraction/Llm/ExtractionReconciler.cs`), restated here because graduation criteria
that ignored them could be gamed by a track that answers confidently and often but wrongly. Any
graduation decision (Gate A or B) is void if it would require weakening any of the following:

- **Deterministic-non-null-wins.** Per field, a non-null deterministic value wins unconditionally over
  any LLM candidate (`ExtractionReconciler.cs:12-13` — the merge policy's rule 1). Graduating an LLM
  track never means "trust the LLM over deterministic" — it means "let the LLM fill fields
  deterministic leaves null" (rule 2) "and let two independent LLMs jointly fill a field deterministic
  cannot reach" (rule 2, agreeing case) "at lower confidence" — the reconciler already encodes this;
  graduation criteria must not be read as license to change it.
- **Two-LLM-conflict → null + review, never coin-flip.** When deterministic is absent and two LLM
  candidates disagree, the field is left unresolved and a review flag is emitted
  (`ExtractionReconciler.cs:16-17`, rule 3). No graduation threshold in this ADR overrides that — a
  track clearing D1–D3 on its own accuracy does not change what happens when it disagrees with another
  track at runtime.
- **Format gate abstains on malformed structure regardless of model confidence.** `LlmExtractionGate`
  rejects an `LlmExpedienteDto` whose `Expediente`/RFC/CURP/`Monto` fail structural validation
  (`LlmExtractionGate.cs` — `ExpedienteRegex`, `RfcRegex`, `CurpRegex`, the Monto range check) before
  the DTO is ever mapped to domain objects, independent of any confidence score. Graduation criteria
  measure the track's output **after** this gate already ran; a track cannot "graduate around" the
  gate by being confident.
- **Practical effect:** because Gate A's demo page already runs every candidate through this reconciler
  and gate, flipping a flag that clears D1–D3 cannot, even in the worst case, make the reconciled
  *field-level* output less correct than deterministic-only — it can only add filled fields
  (`SolicitudPartes` chief among them) or add review flags. This is *why* Gate A's evidence bar can be
  lower than Gate B's: the reconciler is the safety net for Gate A; Gate B (if S4-C ever bypasses or
  weakens the reconciler for performance/latency reasons) would need to re-justify that removal
  separately, which this ADR does not evaluate.

---

## Consequences

### Positive

- Converts "the LLM path is unmeasured" into a stated, falsifiable bar (D1–D3) that any future flip
  decision — Gate A or B — must be checked against, closing the S4-A gap identified in
  `spec-llm-hybrid-extractor-S4A.md` §"Why S4-A exists" point 2.
- Makes the demo-vs-pipeline distinction explicit and load-bearing in policy, not just in code comments
  — preventing a plausible but wrong inference ("the flag is on in the demo, so it must be live in
  production").
- Ties the `SolicitudPartes` headline field to its own explicit no-regression rule (D2) rather than
  letting it be averaged away inside a generic four-field accuracy score.
- Keeps the correctness guarantees (reconciler honesty policy) orthogonal to the graduation bar: no
  numeric threshold in this ADR can be read as permission to relax deterministic-wins, the
  conflict-abstention rule, or the format gate.

### Negative / Trade-offs

- **Coverage floor (80%) and corpus floor (N≥30) are engineering proposals, not corpus-derived or
  spec-mandated values** (flagged explicitly in D3 and D6). They should be revisited once real
  production-shaped documents are available, the same way ADR-023's aggregation weights are flagged as
  "engineering estimates ... re-evaluated after PRISMA-GATED-S2."
- **Per-(track, provider, model) scoping (D4) means graduation does not transfer.** Switching from
  Ollama to Gemini, or bumping an Ollama model tag, re-opens the evidence question; this is
  deliberately conservative and adds friction to provider changes.
- **This ADR authorizes no code.** Gate B remains fully blocked pending S4-C's own corpus, wiring
  design, and sign-off; nothing here should be read as a green light for that story to skip its own
  review.

### No impact on

- The deterministic production path (`AdaptiveTxtFieldExtractor`, `Program.cs:174`) — untouched by this
  ADR and unaffected by either flag regardless of value.
- ADR-023's confidence aggregation model — the reconciler's per-field merge is a separate mechanism
  from the pipeline's `Confidence` VO / weighted export gate; this ADR does not change either.
- The oficio classification baseline (`docs/evaluation/oficio-classification-baseline-2026-07.md`) —
  orthogonal evaluation (`SemanticAnalyzerService`, a classification concern), not field extraction.

---

## Open decisions for owner confirmation

Collected here for visibility (each is also flagged inline above):

1. **D3 — 80% coverage floor**: proposed default, not spec-mandated or data-derived.
2. **D6 — N ≥ 30, stratified by quality tier, as the Gate B corpus precondition**: proposed default,
   not spec-mandated; no statistical power calculation behind it.
3. **D4 — provider/model scoping granularity**: this ADR treats every (track, provider, model tag) as
   requiring its own evidence; confirm this is the intended strictness rather than, e.g., certifying
   "Ollama" as a family regardless of model tag.
