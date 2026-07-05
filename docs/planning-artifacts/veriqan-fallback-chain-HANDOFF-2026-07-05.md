# HANDOFF — Veriqan progressive fallback chain implementation (next orchestrator)

**For:** the next agent running `/bmad-orchestrator` on branch `Liv`.
**Date:** 2026-07-05. **Status:** design complete + pushed; **implementation not started.**

You are picking up a clean boundary. Phase 1 (extractor recalibration) shipped. Phase 2
(fallback-chain **design**) shipped as docs only. Your job is to **orchestrate the
implementation of epics E1–E7**, one epic at a time, verifying from ground truth.

---

## 1. Read these first (canonical, on disk + pushed)

1. **Design + program plan (your spec — do not drift from it):**
   `docs/planning-artifacts/veriqan-fallback-chain-design-2026-07-05.md`
   — architecture (per-field resolver pipeline behind an `EscalatingStatementFieldExtractor`
   decorator), the field escalation matrix, validators, determinism/honesty rules, the 7-epic
   plan + sequencing, and the 4 open owner decisions.
2. **Phase-1 recalibration record (what already changed + why):**
   `docs/planning-artifacts/veriqan-extractor-recalibration-2026-07-05.md`
3. **Tracker:** tasks **#8–#14** are the epic backlog (E1–E7) with `blockedBy` deps already set.
   #1–#7 (Phase 1 + design party) are `completed`. Use TaskList/TaskUpdate.
4. **Memory:** `veriqan-extractor-recalibration-2026-07-05.md` (in the auto-memory dir) has the
   condensed design + the gotchas. `[[prisma-llm-hybrid-extractor]]` is the sibling LLM-seam pattern.

Commits this session: `bf8629c2`, `8c118e35`, `00f1b3e0`, `5dee3ca0`, `f94f812e` (all on `Liv`, pushed).

---

## 2. Start here — PI-1, Epic E1 (recommended first move)

**E1 = the per-field resolver seam, behavior-neutral (field-status diff MUST be zero).** This is the
foundation everything else hangs off. Do NOT add any fuzzy/semantic/LLM behavior in E1 — only the
seam + provenance + the escalation-policy plumbing, wrapping today's positional extractor as "stage 1"
via a strangler-fig `EscalatingStatementFieldExtractor : IStatementFieldExtractor` decorator.

Pragmatic decision already recommended in the design (ratify it, don't re-litigate): E1 runs the
existing `ExtractFullAsync` once and escalates only *higher* stages per field — do **not** refactor
the 25 `private static` per-field methods now.

After E1 lands green + behavior-neutral: **E2** (fuzzy/Levenshtein — cheap wins, closes the P1.2-deferred
PaymentDueDate + TASA/CAT) and **E7** (real product resolution, retires the alias hack) are the
highest-value next epics. E3 (validators) grows *alongside* E2. E4 (semantic) only after E2 proves
residual gaps. E5 (LLM) last. E6.S6.2 (synthetic variance corpus) can start in parallel from day one.

---

## 3. Resolve with the owner BEFORE building (4 decisions — see design §"Open decisions")

Each changes what you build; get a ruling (AskUserQuestion) or proceed on the recommended default and
say so:
1. **Comparer reuse** — add `FuzzySharp` pkg-ref directly to `Veriqan.Infrastructure.Extraction` + copy
   Levenshtein into `Veriqan.Domain` (**recommended**) vs. extract a shared `IndFusion.*` lib.
   ⚠️ Do NOT reference `Infrastructure.Imaging` from Veriqan — hexagonal violation + drags in Emgu.CV.
2. **Embeddings provider (E4)** — local Ollama (**recommended**, bank-data residency) vs. hosted.
3. **Model widening** — new `ExtractedByInference` status vs. a provenance-only sibling field. Decide
   BEFORE building stages — it ripples into every `ExtractionStatus` consumer.
4. **Product gate re-spec (E7)** — how `VerificationPipeline:657`'s null/unknown-product gate behaves
   once Product has a ladder (allow inference-sourced-but-catalog-resolved; clean-abstain on exhaustion;
   NO alias hack).

---

## 4. Verification recipe — this is non-negotiable (the P1.4 lesson)

The extractor's own unit tests were **189 green while a real regression shipped** (null Product →
whole-verdict `ExtractionGap`). It was caught ONLY by the downstream verdict suites. So for EVERY chunk:

- **Run the full verdict pipeline, not just extractor units.** The regression gate is all of:
  - Extraction: `Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Veriqan.Infrastructure.Extraction.Tests/*.csproj`
  - Validation: `.../08 Tests/02 Infrastructure/Veriqan.Infrastructure.Validation.Tests/*.csproj`
  - Orchestration: `.../08 Tests/03 Orchestration/Veriqan.Orchestration.Tests/*.csproj`
    (the demo E2E lives in `VecChecklistDemoE2ETests` — 4 fixtures → known verdicts; these MUST stay green).
- **Ground-truth diagnostic (rebuild it — I deleted the temp one so it wouldn't linger):** a throwaway
  `[Fact]` in the Extraction.Tests project that (a) runs real `ExtractFullAsync` over the 5 demo fixtures
  (`Prisma/Fixtures/PRP2/demo/{good,compliant-master,bad-math-cl21,bad-font-cl35,scanned}.pdf`) and dumps
  each `ExtractedField.Status` via reflection, and (b) dumps PdfPig words `(Left,Bottom,Right,Text)` per
  page. **Calibrate from PdfPig coords, NEVER pdftotext/PyMuPDF** (tokenization + origin differ). Delete it
  before committing. (Its exact shape is described in the recalibration memory; the constructor is
  `new PdfPigStatementFieldExtractor(XUnitLogger.CreateLogger<…>(), Options.Create(new PdfExtractionOptions()), new NullPasswordProvider())`.)
- **Additive only.** Keep the Dummie VEC fixtures green; any change to an asserted Dummie value is a
  deliberate, visible decision, not a silent gold edit.

---

## 5. Hard-won gotchas (carry these forward)

- **Product is load-bearing.** `ExtractProductName` returns a wrong value (`"Número de tarjeta 4111…"`)
  that resolves ONLY because the demo bundle registers it as a `TC-BSSB` alias. Making it abstain →
  `UnknownProduct` blocks the whole verdict. E7 fixes this properly (catalog resolution + gate re-spec);
  until then, don't "clean up" Product.
- **Honesty is cardinal:** abstain, never fabricate. Every stage output is a *candidate* until it passes
  the same validator the positional path would. False-confidence rate is a first-class metric (0-ceiling
  on verdict-gating fields).
- **Determinism (NFR-5):** LLM/semantic stages need a content-hash cache (the real guarantee); temperature
  0 is already pinned in both providers; disagreement / non-determinism → abstain; no silent retries;
  synthetic-only few-shot examples (echo-leak risk).
- **Reuse map:** `ILlmProvider`/`ILlmProviderFactory`/`LlmProvidersOptions` lift cleanly; Prisma's
  `HybridExtractionService`/`ILlmExpedienteExtractor<T>`/`Expediente` do NOT (Oficio-shaped) — copy the
  pattern, rebuild the types. Fix the `CancellationToken.None` bug in `LlmVisionFieldExtractor` when porting.
- Build is `dotnet build`/`dotnet test` per project (see CLAUDE.md); global.json opts into MTP;
  warnings-as-errors + nullable are on.

---

## 6. Orchestration loop for this work

One epic at a time. For each: re-ground from the tracker + design doc → delegate cohesive chunks to
`dev` subagents with tight briefs + the PdfPig coordinate ground truth → **verify from ground truth
yourself** (build + the 3 suites above + the field-status diagnostic + `git diff`) → commit in
meaningful chunks with a verification line → push `Liv` → adversarial-review at each epic boundary
(a `general-purpose` skeptic refuting against the design doc). Stop + hand off at epic boundaries; do
not roll into the next epic without a deliberate decision. Never drive the semantic-infra (E4) or the
model-widening (decision #3) as a silent autonomous refactor — surface + scope with the owner.
