# SCOPING + TRACKER — S4-M: Gate-B Measurement Harness

**Branch:** `Liv` · **Scoped:** 2026-07-18 · **Status:** SCOPED, un-gated, ready to build (no owner sign-off needed to *start* — this is measurement, not the flip).

Parent specs: `docs/implementation-artifacts/spec-llm-hybrid-extractor-S4C.md` (the build, owner-gated) ·
`docs/architecture/adr/ADR-024-llm-hybrid-graduation-criteria.md` (Gate-B checklist) ·
`docs/planning-artifacts/TRACKER-llm-hybrid-S4A.md` (S4-B CLOSED; measurement harness origin).

---

## Why this story exists (the one-line problem)

S4-C (un-dark the LLM fallback in the Athena worker) is **owner-gated on a number that does not exist in any
artifact**: `P(LLM-text NumeroExpediente correct | deterministic extractor returned NULL)` — the *precision of
the fallback on the only slice where it ever runs*. The S4-B baselines report **full-corpus** accuracy
(NumeroExpediente 94%, Autoridad 81%); those numbers **do not describe the fallback subpopulation**. Without
the conditional number the owner cannot responsibly sign off (Gate-B checklist §7 item 1 + item 8).

**This story produces that number.** It is measurement-only, additive, touches **zero production `.cs`**, and
does **not** flip anything. It converts "I can't decide yet" into "here is the evidence packet — go / no-go."

## Hard scope boundary

**IN:** extend the existing eval harness to compute the deterministic-NULL conditional slice; grow the corpus's
deterministic-NULL slice to a decidable N; measure the deterministic-fail rate; commit a repeatable
CI-gated metric test + an evidence artifact for the owner.

**OUT (all = S4-C, separately owner-gated — do NOT touch here):** `ExtractionOrchestrator` seam
(`ExtractionOrchestrator.cs:392` failure branch), the `IDeterministicFallbackLlmExtractor` service, the worker
DI registration (`Athena.Worker/Program.cs`), any `LlmProviders:FallbackEnabled` flag, any flip. **No
production `.cs` changes. Do NOT modify `AdaptiveTxtFieldExtractor` or `ExtractionReconciler`.** The measurement
must observe today's deterministic path exactly as it ships.

## Ground truth (verified file:line — the building blocks already exist)

- **Existing harness:** `08 Tests/02 Infrastructure/Tests.Infrastructure.Extraction/LlmExtractionEvalHarness.cs`
  — skip-gated manual harness (`[Fact(Skip=…)]`), runs 3 tracks (deterministic `PdfOcrFieldExtractor` →
  `AdaptiveTxtFieldExtractor`; LLM-text over the SAME OCR text; LLM-vision) over `Prisma/Fixtures/PRP1-golden`,
  emits committed JSON+MD baselines. Measures **full-corpus per-field** accuracy — NOT the conditional slice.
- **Pure metric engine (CI-gated):** `LlmExtractionMetrics.cs` + `LlmExtractionMetricsTests.cs` (deterministic,
  mocked, IS a CI gate). This is where the new conditional-slice math lands and gets unit-tested.
- **Trustworthy corpus:** `Prisma/Fixtures/PRP1-golden/` — **20 docs**, each with a generator-stamped
  `ground_truth.json` guaranteed **source-contained** (rendered into the body), per its `README.md`. (The older
  `Prisma/Fixtures/PRP1/` gold is NOT source-contained — never use it as the oracle; see D7.1.)
- **Generator:** `Prisma/Code/Src/Python/Prisma-dumy-generator-AAA/prp1_generator/` (P1 golden generator that
  produced PRP1-golden with source-contained manifests).
- **Gate-B checklist:** `spec-llm-hybrid-extractor-S4C.md` §7 (8 items) · corpus preconditions ADR-024 D6
  (N≥30, stratified) + D7.1 (source-text containment guard).
- **The gold discipline (foundational):** the oracle is ALWAYS the generator manifest, NEVER the delivered
  companion JSON/XML — the arriving docs are unreliable by domain design (`[[prisma-domain-3-docs-unreliable]]`).

## The number, defined precisely

Partition PRP1-golden by the **control field** `NumeroExpediente` (S4-C's D2 anchor):
- **Deterministic-NULL slice** = fixtures where the deterministic track returns NULL/empty for `NumeroExpediente`.
  *This is the only population the fallback ever runs on.*
On that slice, run LLM-text + the S4-C control-discard gate (plausibility **and** source-containment on
`NumeroExpediente`) and report, all against **manifest gold**:
- **pass-rate** (gate accepts) vs **abstain-rate** (gate discards → NULL → zero regression by construction);
- **precision of the pass set** — *the safety-critical number*: of the records the gate ACCEPTED, how many are
  actually correct. A wrong pass (a hallucinated / companion-echoed expediente stamped into a legal record) is
  the **only** outcome that makes prod worse than today.
- of the abstain set: **correct-abstain** vs **false-reject** (value present but OCR-mangled → lost, accepted cost).

## Stories (composition-root never touched — there is none to touch here)

| ID | Story | DoD | Gate-B item |
|----|-------|-----|-------------|
| **S4-M.0** | **Slice-size probe (MAKE-OR-BREAK, cheap).** Run the existing harness's *deterministic* track over PRP1-golden; count the current deterministic-NULL(`NumeroExpediente`) slice. Validate the spec's "~3/20" claim from ground truth. | A committed count. If slice ≈ 3, the corpus MUST grow (S4-M.1) before any conditional number is decidable — state that verdict loudly. | 2 (gate) |
| **S4-M.1** | **Grow the deterministic-NULL slice to N≥30 (corpus, source-contained).** Extend the P1 generator to emit fixtures that deliberately TRIGGER deterministic-NULL on `NumeroExpediente` — layouts/formats `AdaptiveTxt` cannot parse — while the true value **is still rendered into the body** (so LLM+source-containment can legitimately recover it). Gate every new fixture on D7.1 (source-contained manifest). | ≥30 deterministic-NULL fixtures, each D7.1-verified; generator change is additive; `PRP1-golden` (or a sibling `PRP1-golden-nullslice`) grown. | 2 |
| **S4-M.2** | **The conditional measurement.** Extend `LlmExtractionMetrics` (pure) + the harness to compute the deterministic-NULL slice metrics above; add the math to the CI-gated `LlmExtractionMetricsTests` (deterministic, mocked `ILlmProvider`). Emit a committed artifact `docs/evaluation/llm-hybrid-fallback-precision-2026-07.{json,md}`. | Pure-metric unit tests green in CI; artifact written on a live run (skip-gated like S4-A); precision-of-pass-set is the headline. | 1 |
| **S4-M.3** | **Deterministic-fail rate.** Report `P(deterministic NULL)` on the corpus (and note if a real-doc sample is available). | A number + a one-line "does the fallback fire often enough to be worth it?" read. | 5 |
| **S4-M.4** | **Partes no-regression (cheap add).** On the NULL slice, count `SolicitudPartes` the fallback recovers vs the deterministic-null baseline (which produces none) — the real value-add. | A number in the artifact. | 3 |

**Ordering:** S4-M.0 first — it may reveal the corpus is too thin to decide, gating S4-M.1. S4-M.2 depends on
S4-M.1 (need N to be meaningful). S4-M.3/M.4 fold into the same live run.

## Deliverable to the owner (the evidence packet)

One committed artifact + a ≤1-page summary answering: **(i)** precision of the pass set on the deterministic-NULL
slice, **(ii)** pass vs abstain split, **(iii)** how often deterministic actually fails, **(iv)** partes
recovered, plus **~10 real accept/discard examples** to eyeball and a recommended go/no-go framing. That packet
is exactly what Gate-B item 8 asks the owner to review before the written go/no-go.

## Gate-B checklist — what this closes vs leaves

- **Closes:** item 1 (the missing measurement), item 2 (N≥30 corpus), item 3 (partes no-regression), item 5
  (deterministic-fail rate). **Feeds** item 8 (the ~10 examples + the number to sign off on).
- **Leaves for the S4-C build / owner:** item 4 (Ollama-down fault injection — proven in staging *during* S4-C),
  item 6 (**Ollama-vs-Gemini compliance ruling — a pure owner decision, decidable now**, independent of this
  story), item 7 (provenance visible in review UI — built *as part of* S4-C), item 8 (the final written go/no-go,
  after this packet lands).

## Cardinal risk (read before building S4-M.1)

**The synthetic-corpus trap.** If the deliberately-NULL-triggering fixtures aren't *realistic* — grounded in how
the deterministic extractor fails on **real** Banamex/authority docs (real OCR mangling, real layout variance) —
the precision number is a synthetic artifact that won't hold on live traffic. Construct the NULL triggers from
**observed** deterministic failures, not invented ones; if you can't build a realistic deterministic-NULL-yet-
source-contained doc, that itself is a finding worth surfacing (it would mean the fallback's value proposition is
thin). This is the same discipline as `[[prisma-domain-3-docs-unreliable]]` (god's-eye gold only) and the C2
"discriminating power proven synthetic-only" caveat — measure honestly, log the caveat, no silent caps.

## What this does NOT do

It does not authorize, pre-stage, or write one line of the S4-C production wiring. The flip stays owner-gated on
the full Gate-B checklist and the explicit written go/no-go. This story exists to make that decision *evidence-based*
instead of blind.
