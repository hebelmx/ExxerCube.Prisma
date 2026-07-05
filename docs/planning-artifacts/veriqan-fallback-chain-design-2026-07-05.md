# Veriqan Progressive Fallback Extraction Chain — Design & Program Plan (2026-07-05)

Branch `Liv`. Owner: hebelmx. Produced by a BMAD design party (architect + analyst + QA +
dev + PM) after the Phase-1 extractor recalibration
([veriqan-extractor-recalibration-2026-07-05.md](./veriqan-extractor-recalibration-2026-07-05.md)).
This is the canonical Phase-2 design; no code has been written.

## Problem & goal

The positional `PdfPigStatementFieldExtractor` is calibrated per-layout; real-world statements
vary far more than the current 4 demo fixtures (different banks, products, phrasing, page order).
Phase-1 proved several fields can't be reliably read positionally on the real Banamex layout
(Product, TASA/CAT, PaymentDueDate). The owner wants a **per-field PROGRESSIVE FALLBACK CHAIN**
that escalates **only as needed**, cheap→expensive:

```
Stage 1 exact/positional → 2 fuzzy → 3 Levenshtein → 4 semantic search → 5 LLM extraction
```

Cardinal constraints (non-negotiable): **honesty — abstain, never fabricate** ("misread ≠ false
value"); **NFR-5 determinism** — same PDF → same result; additive — never break the demo verdicts
or the Dummie fixtures; `Result<T>`+`CancellationToken`; nullable + warnings-as-errors.

## Two facts that shape the whole design (verified in code)

1. `VerificationPipeline` reads only `ExtractedField.Status` (for a coverage count) — `Confidence`
   and `Locator` are consulted by **zero** verdict logic today. So the chain can be added *under*
   the port with zero verdict-logic change — but the model has no vocabulary for "a model guessed
   this, treat as low-trust." That vocabulary must be added deliberately.
2. Prisma's `HybridExtractionService` escalates on a **config flag** and runs **all** tracks then
   reconciles — right for Prisma (multi-source fusion), **wrong** for Veriqan's "escalate only as
   needed." Veriqan needs **trigger-gated, per-field** escalation. Reuse Prisma's *pattern* and its
   provider infrastructure, not its extraction/reconciliation types.

## Architecture — per-field resolver pipeline (not a monolithic decorator)

Escalation is per **field**, not per document (Movements may resolve at stage 1 while Tasa needs
stage 4, same call). New abstractions (Veriqan-local):

- **`IFieldResolutionStage<TValue>`** — one rung: `Task<Result<FieldCandidate<TValue>>> TryResolveAsync(FieldResolutionContext ctx, CancellationToken ct)`.
  `FieldResolutionContext` carries the once-parsed PdfPig word/page corpus, the field's `FieldKind`,
  prior-stage candidates, and a `StageBudget`. `FieldCandidate<TValue>` is stage-local (Value +
  stage-native score + `StageId`) — not yet an `ExtractedField<T>`.
- **`FieldEscalationLadder`** — per-`FieldKind` **declarative data**: which stages apply, in order,
  and the trigger between each. Adding/reordering a field's ladder is a registration change, not new code.
- **`FieldResolutionOrchestrator`** — walks each field's ladder honoring triggers + budget, and is the
  **sole** place a winning candidate becomes a domain `ExtractedField<T>` (with provenance).
- **`EscalatingStatementFieldExtractor : IStatementFieldExtractor`** — decorator: runs the existing
  positional extractor first (it *is* stage 1, unchanged), inspects the returned `StatementModel`
  field-by-field, and escalates only fields that don't clear their trigger. `VerificationPipeline`
  keeps calling `ExtractFullAsync` exactly as today — escalation is invisible to the orchestration layer.

Each rung is an `IFieldResolutionStage`: `PositionalStage` (wraps current logic), `FuzzyLabelStage`
(FuzzySharp — locate a label phrase positional exact-match missed, read the value band relative to it),
`LevenshteinStage` (char edit-distance for token garbling / context disambiguation), `SemanticSearchStage`
(greenfield — locate the *concept* on the page, hand a narrowed window downstream), `LlmExtractionStage`
(Veriqan-scoped `ILlmFieldExtractor` over a bounded window + strict single-field JSON schema).

### Escalation triggers (evaluated by the orchestrator, uniformly)

A field escalates to stage N+1 only when stage N fails a gate:
- **Status gate** — result is `NotExtracted` (nothing to lose).
- **Confidence-floor gate** — `Extracted` but stage-native confidence < a **per-`FieldKind`** floor
  (Product/Tasa stricter than Address).
- **Validator-failure gate** — value extracted but fails a domain validator (e.g. Product doesn't
  alias-resolve via the existing `ProductResolver`; Tasa outside `[0,200]%`; due date outside the
  period window). **This is the direct fix for the F1 Product false-confidence bug: run `ProductResolver`
  as a post-stage validator; a resolution miss triggers escalation, not an immediate block.**
- **Disagreement gate** — once ≥2 stages produced candidates that diverge beyond a per-field
  tolerance → escalate/abstain, never pick a winner by fiat (honesty).

### Cost/latency discipline (three orchestrator-enforced guards)

1. **Per-`FieldKind` ladder length, authored explicitly.** Identity fields (CardNumber/CLABE/RFC/
   ClientName), period dates, day counts → **1-rung (positional only)**, must never reach the LLM.
   Only the proven-brittle fields get multi-rung ladders. This allowlist is a party-ratified decision.
2. **Document-level LLM budget** (`max N LLM calls / document`); overflow → distinguishable abstain,
   not silent degradation.
3. **Ladder order is a cost ramp**; the semantic stage's job is partly to *narrow the LLM's input
   window* so the eventual LLM call (if any) is cheap, low-token, and less prone to hallucinate.

### Provenance & honesty vocabulary (additive model changes)

- **Confidence becomes stage-derived** (orchestrator computes it, not the factory) — positional keeps
  1.0; fuzzy/Levenshtein/semantic/LLM populate their real score. No `ExtractedField<T>` schema change.
- **`ExtractionSource`/provenance** on the field (StageId; for LLM: provider + model id + prompt/response
  content-hash) — mandatory for a compliance tool; route into the Epic-6.2 audit ledger.
- **New terminal status `ExtractedByInference`** for values that reached `Extracted` via semantic/LLM —
  so a downstream consumer can require human confirmation before an inferred value gates a verdict
  (mirrors the Prisma `RequiresManualReview` pattern). Do **not** collapse an LLM value into plain
  `Extracted`.

## Field escalation matrix (analyst)

Indexed on **field semantics + verdict stakes**, not on the 4 fixtures. New-bank coverage should come
90%+ from growing the **label-alias dictionary** feeding the fuzzy tier; semantic/LLM stay a small,
heavily-validated, constant tail.

| Field(s) | Ladder | Why |
|---|---|---|
| ClientName, BranchNumber, CardNumber, Clabe, ClientNumber, Rfc | **Positional only** (fuzzy safety net at most) | Dual-calibrated + strong format validators; no evidence of need. |
| PeriodStart, PeriodCutDate, DayCount(Printed) | **Positional only** | Simple labels + built-in cross-checks (`DayCountVerification`). |
| **PaymentDueDate** | Positional→**fuzzy/Levenshtein, STOP** | Pure phrasing variance ("…para realizar el pago"); CONDUSEF language is a bounded set. LLM = wasted cost. |
| Balance/payment amounts (SaldoDeudorTotal, CreditoDisponible, PagoMinimo, PagoParaNoGenerarIntereses, PagoMinimoMasMeses) | Positional→**fuzzy** (+ remove page-1-only scan limit) | Position/page variance, not conceptual ambiguity. |
| RESUMEN 7 (Adeudo/Cargos.../Monto.../IVA/Pagos y abonos), NIVEL DE USO 2, DESGLOSE totals 2 | Positional→fuzzy, then **ABSTAIN — never LLM** | Regulator-fixed vocabulary; feed **arithmetic** checks (CL-21/22/24/44). A wrong number = false verdict; honest `InsufficientData` is strictly better. Strongest validator = the arithmetic identity itself + sum-of-movements checksum. |
| **Tasa** | Positional→**Levenshtein (footer reflow)→semantic** | Rendered as a fragile split footer (`7.77%/29/.72%`) — a token-fragmentation problem. |
| **Cat** | Positional→**semantic (disambiguation)→LLM (garbled only)** | Homonym collision: literal "CAT" is also "Centro de Atención Telefónica CAT:" (phone). Needs contextual disambiguation, not string matching. |
| **Product** | Positional→fuzzy/Levenshtein vs **catalog**→semantic→LLM, **output MUST catalog-validate** | Gates the *entire* verdict; naming varies hugely. LLM guess must resolve in the reference bundle or **abstain** — never accept a freeform string. |
| **Movements** (table) | Structural (already fixed P1.3)→**fuzzy heading**→semantic table-shape→LLM row-repair on a *localized region only* | Anchor/table-detection problem. P1.3 fixed the demo; fuzzy-heading + shape-detection generalize to other banks. |
| Sections/FinancialTables/Fonts/Typography/Overlap/Gaps/DisputeRows/PageHashes | **NOT in this chain** | Geometric/visual facts — no ambiguous label to resolve. Use a per-section heading-alias list (28 fixed CONDUSEF sections); LLM only as OCR repair, scoped separately. |

**Validators gate every stage's output** (a stage result is a *candidate* until it clears the same
validator the positional path would): format regex (RFC/CLABE/Card), catalog membership (Product),
%-plausibility + same-section co-location (Tasa/Cat), date sanity (period window), amount format+sign,
**cross-field arithmetic identity** (RESUMEN group), **sum-of-movements checksum** (totals).

## Determinism & honesty for semantic/LLM (concrete)

- **Content-hash cache is the NFR-5 mechanism** (not temperature alone): key = `SHA256(pdf bytes) +
  FieldKind + StageId + prompt-version (+ model tag/digest)`. Reprocessing the same PDF returns the
  cached candidate. Embeddings are pure functions of input → semantic stage is the *easiest* to make
  deterministic.
- **Temperature 0** as defense-in-depth (both Prisma providers already pin it). Pin exact model tags
  (not `latest`); key cache on tag+digest to catch silent model swaps.
- **LLM/semantic output is a candidate** → must re-pass validators (the Product/ProductResolver case).
- **Disagreement → abstain.** **No silent retries** with perturbed prompts. Non-determinism at temp 0
  across N replays → treat the field as `ExtractionGap`/abstain by policy (explicit, tested path).

## Stack & reuse (dev, code-verified)

- **Fuzzy:** `FuzzySharp` already CPM-pinned → add `PackageReference` directly to
  `Veriqan.Infrastructure.Extraction` (zero new `Directory.Packages.props` entries). **Do NOT** take a
  Veriqan→`Infrastructure.Imaging` reference (hexagonal violation; drags in Emgu.CV).
- **Levenshtein:** `LevenshteinTextComparer` is ~15 lines, dependency-free → copy into `Veriqan.Domain`
  (dependency-free, no boundary objection). *Alternative:* extract `ITextComparer` into a shared
  `IndFusion.*` lib both products consume (cleaner, larger). **Decision needed (E2.S2.1).**
- **Semantic:** nothing exists. Cheapest stack-native path = `OllamaSharp` (already pinned) `/api/embeddings`
  + a small model (`nomic-embed-text`) on the **host** Ollama already reached via `host.docker.internal`
  (proven in `docker-compose.staging.override.yml`). Cosine + brute-force page-local NN (no vector DB).
  Deterministic. Needs an ADR: local vs hosted (compliance data → default local). **Largest new build.**
- **LLM:** `ILlmProvider`/`ILlmProviderFactory`/`LlmProvidersOptions` (Ollama/Gemini switch) are clean &
  reusable; both providers **already pin temperature 0**. `ILlmExpedienteExtractor<T>`/`HybridExtractionService`/
  reconciler/`Expediente` are Oficio-shaped → **rebuild** a Veriqan `ILlmFieldExtractor` returning
  `ExtractedField<T>`. Add a `Temperature` to the Veriqan request type. **Fix the `CancellationToken.None`
  bug in `LlmVisionFieldExtractor` — thread the real token.** Content-hash cache is new + small.
- **Integration:** `IStatementFieldExtractor` has 2 methods → decorator-friendly. `StatementModel` is a
  sealed class with `init` collections + a ctor → per-field rebuild is mechanical (no `with`). DI swap in
  `VeriqanExtractionExtensions` is small. Pragmatic stage-1 decision: run `ExtractFullAsync` once, escalate
  only *higher* stages per-field (stage 1 is deterministic/idempotent) — avoids refactoring 25 `private
  static` per-field methods now (see risk 1).

## Eval, honesty & determinism testing (QA)

- **Golden corpus** = per-field triple: expected value + **expected status incl. legitimate abstentions**
  (③ fields) + **provenance tag** (`synthetic-Dummie` / `demo-fixture-Banamex` / `real-CONDUSEF`). Gold must
  be god's-eye (source PDF / generator manifest), **never** the extractor's own output or a delivered file
  (Prisma lesson [[prisma-domain-3-docs-unreliable]]). Real corpus is owner-gated → tag corpus
  `PROVISIONAL` until it lands.
- **First-class metric = false-confidence rate** (value emitted where truth is abstain). Pass bar: it must
  **not increase stage-over-stage**; recall may never be bought with honesty. **Zero-ceiling** on any
  verdict-gating field (Tasa/Cat/totals/PaymentDueDate/RESUMEN) for demo/real gold rows.
- **Honesty gates run the FULL verdict pipeline**, not just extractor units (the P1.4 lesson: 189 green
  units hid the Product→ExtractionGap regression). Test classes: abstention-preservation (final status
  stays abstain through all stages), verdict-flip (injected wrong value never flips PASS↔FAIL),
  cross-field contamination (stage for field A never returns field B's value).
- **Determinism:** content-hash replay + snapshot LLM I/O checked into repo; mocked LLM port for PR CI +
  a separate non-gating live-model lane + a drift monitor. **Prompt-echo guard: synthetic placeholder
  few-shot only** (Prisma S4 lesson) + an echo-detection test (doc value ≠ prompt-example value) +
  hallucination-under-absence test.
- **CI = staged supersets:** each stage's gate ⊇ the prior; "no silent Dummie change" — gold-file diffs
  to Dummie rows must be explicit, reviewed line items.

## Program plan (epics)

| Epic | Goal | Notes |
|---|---|---|
| **E1 Field-resolver seam + provenance** | Strangler-fig the positional extractor into per-field stages; provenance; the **escalation policy** (escalate on implausible/low-confidence success, not only on `NotExtracted` — the ② bug class was *worse* than NotExtracted). Behavior-neutral (field-status diff = 0). | Foundation. Everything depends on S1.4 escalation policy. |
| **E2 Fuzzy/Levenshtein label-matching** | Stage 2+3 as generic label-anchor finders. Closes the P1.2-deferred fields (PaymentDueDate; TASA/CAT disambiguation) + fuzzy movements-heading. | **Cheap wins — reuses in-repo assets.** S2.1 comparer decision blocks it. |
| **E3 Validators + abstention discipline** | Per-field validators gate every stage; confidence→status mapping; permanent anti-false-confidence CI suite; NFR-5 guard. | Grows *alongside* E2, not after. |
| **E4 Semantic stage + embeddings infra** | Stage 4 for paraphrase/reorder beyond fuzzy. | **The one real infra investment.** ADR: local vs hosted. Build only after E2 shows where fuzzy fails. Likely its own epic. |
| **E5 LLM stage + determinism/caching** | Stage 5 last-resort, dark by default; mirror Prisma seam; temp 0 + content-hash cache + budget + circuit breaker. | Lowest *engineering* cost (proven pattern), highest trust bar. Impl can start after E1; prod after E3+E6. |
| **E6 Golden-corpus + eval harness** | Per-field/per-stage precision/recall/false-confidence; baseline-locked CI gate. | S6.2 **synthetic variance generation is the non-owner-gated escape valve — start early, parallel.** S6.4 real corpus on its own clock. |
| **E7 Real product resolution** | Resolve Product against a real catalog; retire the demo `TC-BSSB` alias hack; make the null-product gate intentional. | Closes F1. Slot right after E2 (fuzzy/catalog likely suffices); independent of E4/E5. |

### Sequencing

1. **PI-1 = E1 + E2 + E3-stub + E6.S6.2** — smallest slice that measurably improves real-layout
   extraction and proves it's not fixture-specific patching.
2. **E3 grows with E2** (every stage needs a validator or it manufactures new false-confidence).
3. **E7 right after E2** (F1 is a *known named* bug; Product likely resolvable by fuzzy/catalog alone).
4. **E4 only after E2** takes cheap wins and E6 shows genuine residual gaps (don't build embeddings
   speculatively).
5. **E5 last** (needs E3 validators + E6 harness to be trusted/measured).
6. **E6.S6.4 real corpus** on its own clock from day one — never gates the program.

### Gates & dependencies

- **Owner-gated:** E6.S6.4 (real CONDUSEF/Banamex corpus + licensing) — escape valve is E6.S6.2.
- **Architect/policy-gated:** E2.S2.1 (comparer reuse vs local port — resolve sprint 1); E4.S4.1
  (embeddings provider ADR, likely local for compliance data).
- Everything downstream of E1 depends on the S1.4 escalation policy being right.

### Program DoD

All 5 stages behind one per-field seam with provenance in logs/audit; P1-deferred fields
(PaymentDueDate/TASA/CAT) + ② bugs (Product/Address) correct-or-honestly-abstaining on Dummie **and**
real fixtures with **unchanged verdicts**; CI-gated baseline-locked eval harness reporting per-field/
per-stage precision/recall/**false-confidence**; semantic + LLM ship dark, deterministic, cost-bounded;
zero regression in any green suite; build 0/0; real-corpus dependency tracked as explicitly open.

### Suggested Sprint 1

E1.S1.1 (abstraction) + S1.2 (strangler-fig wrap) + S1.5 (field-status regression harness — reuse the
P1.0 diagnostic pattern) to land the seam behavior-neutral and diff-provable; resolve **E2.S2.1** (comparer
architecture) so sprint 2 isn't blocked; stub **E3.S3.1** validators for the 2-3 fields E2 targets; kick
off **E6.S6.2** (synthetic variance) in parallel (no upstream dep).

## Open decisions for the owner

1. **E2.S2.1** — add FuzzySharp to Veriqan directly + copy Levenshtein to Veriqan.Domain (pragmatic,
   recommended) vs. extract `ITextComparer` to a shared `IndFusion.*` lib (cleaner, larger).
2. **E4.S4.1** — embeddings provider: local Ollama (recommended for bank-statement data residency) vs hosted.
3. **Model widening** — `ExtractedByInference` new status vs. provenance-only sibling field (ripples into
   every `ExtractionStatus` consumer — decide before building stages).
4. **Product gate re-spec (E7)** — how the null/unknown-product gate behaves once Product has a ladder
   (must allow inference-sourced-but-catalog-resolved, clean-abstain on exhaustion, no alias hack).
