# MVP Audit Plan — Intent-vs-Discovery Reconciliation & Path to MVP

**Status:** PLAN ONLY (not yet executed) · **Date:** 2026-06-11 · **Branch:** Kt2
**Author:** audit-planning pass
**Supersedes the scope of:** `GAP-MATRIX-2026-06-dual-ground-truth.md` (2026-06-07) — that matrix is now ~stale; real development happened since (ITDD test-suite hardening Phases 0–6 + Stryker mutation marathon, both COMPLETE).

---

## 0. Why this audit exists

The last review (2026-06-07 dual-ground-truth reconciliation) traced static wiring and produced a Done/Partial/Planned matrix. Since then the work was **deliberately about hardening the test suite to enable confident refactor/implementation** — that hardening is now done. Consequently:

1. Code has **drifted** from the June matrix; some gaps are likely closed, some changed, and new ones may exist.
2. The remaining gaps are **small enough to be easily missed but real enough that the project cannot honestly be called demo/MVP-ready.** The audit must be *skeptical and evidence-driven* to surface the subtle ones (implemented-but-unwired, stubbed-readiness, single-source paths, placeholder returns, zero-returning metrics).
3. We want to **challenge the PRD itself** — reconcile what we *assumed* at the start against what we *discovered* during the build — so the MVP target reflects reality, not the original guess.

**Goal:** produce (a) a refreshed, evidence-backed gap matrix and (b) a concrete, ordered, closeable path to MVP.

---

## 1. Success criteria for the audit

The audit is complete when we have:

- [ ] A **PRD-vs-Discovery reconciliation** — every material PRD assumption marked **Validated / Refuted / Superseded / Re-scoped**, with the discovery that changed it.
- [ ] A **derived MVP definition** — an explicit acceptance checklist that reflects what we *actually* need (not the original guess), traceable back to the reconciliation.
- [ ] A **refreshed gap matrix** — every critical-path + service component classified **Done / Partial / Planned / Dormant-by-design**, each line carrying `file:line` evidence and a one-line verdict.
- [ ] A **concrete MVP path** — remaining items ordered by dependency + leverage, each with a crisp **Definition of Done** and rough size (S/M/L).
- [ ] A clear statement of **what makes it demo-able** vs **what makes it MVP** vs **what is post-MVP**.

---

## 2. Inputs (sources of truth to read, in order)

| Source | Path | Role in audit |
|--------|------|---------------|
| PRD | `docs/product/requirements/prd.md` | The **intent** to refute |
| Mission docs | `docs/planning/missions/` (Mission_1, MISSION_2_ANALYSIS, MISSIONS_3_4_5, handoffs) | Original target behaviour / happy-path intent |
| Prior gap matrix | `docs/planning/gap-analysis/GAP-MATRIX-2026-06-dual-ground-truth.md` | Baseline to diff against |
| Path to production | `docs/planning/path-to-production-2026-06.md` | Prior roadmap; check what was done |
| Gap-analysis archive | `docs/planning/gap-analysis/*` (FINAL_REALISTIC, Pipeline_Gap_Analysis, ORCHESTRATION_READINESS, REVISED_SCOPE, CRITICAL_GAP_CLASSIFICATION_RULES, etc.) | Prior discovery already captured — mine before re-deriving |
| CLAUDE.md Release Status | repo root | Last-known Done/Partial/Planned narrative |
| Feasibility / funding | `docs/planning/VEC_STATEMENT_FEASIBILITY_ASSESSMENT.md`, `PropostaPostFunding.md` | Business framing of "MVP" |

**Ground truth = code, not docs.** Docs are claims to verify. Every gap verdict cites `file:line`.

---

## 3. Method & evidence standard (applies to every phase)

- **Trace, don't trust.** A component is "wired" only if reached from a real DI composition root: Web.UI (`Program.cs`/startup), Athena Worker, Orion Worker. Implemented-but-unregistered = **Partial (unwired)**, not Done.
- **Three states beyond Done/Partial/Planned must be distinguished:**
  - **Dormant-by-design** — intentional optionality, NOT a gap. Known cases: Python/CSnakes VLM path (ADR-001, Tesseract is the engine of record), in-repo SignalR (extracted to `IndFusion.Ember`, ADR-009). Do **not** flag these as gaps.
  - **Stub-in-critical-path** — a stub that sits on the happy path (e.g. `StubDocumentDownloader`) — this DOES block MVP.
  - **Stub-out-of-band** — a stub on a parallel/unused path (e.g. worker `/dashboard` returning zeros while UI uses a real service) — does NOT block MVP; record but de-prioritise.
- **Subtle-gap tells to grep for explicitly** (these are how small gaps hide):
  - `Stub`, `Placeholder`, `Dummy`, `Fake` in production (non-test) registrations
  - `TODO`, `FIXME`, `NotImplementedException`, `throw new NotSupported`
  - methods returning `Result...Success(null)` / empty collections / `0` / `string.Empty` unconditionally
  - `// TODO: orchestrator.IsStarted` style stubbed readiness probes
  - registered adapter with no caller; interface with no production implementation
  - single-source fan-in (e.g. Fusion fed only PDF source; XML/DOCX null in worker path)
- **Don't accept doc claims that contradict code** — if CLAUDE.md says "real" but the method returns empty, the code wins; flag the doc as stale too.
- **Project count discipline:** ~70 projects (33 prod + 37 test), NOT 195/200. ~1,670+ tests. Don't repeat inflated figures.

---

## 4. Phases

### Phase 1 — PRD-vs-Discovery Reconciliation (the "refute the PRD" pass)

**Objective:** separate original assumptions from validated reality.

1. Extract from the PRD + Mission docs a flat list of **material assumptions/requirements** (functional + architectural), each as a testable statement.
   - e.g. "SIARA exposes an API for document download", "OCR via VLM (DocTR/GOT-OCR2)", "Fusion reconciles multi-source (PDF+XML+DOCX)", "single-process pipeline", etc.
2. For each, assign a verdict with evidence:
   - **Validated** — built and matches intent.
   - **Refuted** — discovery proved the assumption wrong (e.g. "no SIARA API exists → web-scraping is the real approach").
   - **Superseded** — replaced by a better-understood approach (e.g. Python.NET → CSnakes → Tesseract-C#; in-repo SignalR → Ember; single-process → 3-process Downloader/Extractor/Reconciliator).
   - **Re-scoped** — still wanted but smaller/larger/later than the PRD implied.
3. Capture the **discovery narrative** per item — the *why* it changed (this is the institutional knowledge the user explicitly wants preserved).
4. **Output:** `PRD-RECONCILIATION-2026-06.md` — a table: Assumption | Verdict | Discovery | Evidence | Impact on MVP scope.

> Mine `FINAL_REALISTIC_GAP_ASSESSMENT.md`, `REVISED_SCOPE_CLASSIFICATION.md`, `ORCHESTRATION_READINESS_ASSESSMENT.md`, ADR-001/ADR-009 first — much discovery is already written down; reconcile rather than re-derive.

### Phase 2 — Derive the real MVP definition

From Phase 1, write an explicit **MVP acceptance checklist** — the minimum truthful bar. Frame three tiers:
- **Demo-able:** the narrowest credible end-to-end path that can be shown.
- **MVP:** demo-able + the gaps that currently disqualify it (no stubs on critical path, honest readiness, one real document type fully through).
- **Post-MVP:** everything deferred (with rationale).

Each MVP line must be a binary, checkable acceptance criterion. **This is the bar the rest of the audit measures against.**

### Phase 3 — Ground-truth re-trace (full pipeline + 3 services)

Trace the **current** code from each composition root. Cover:

**Pipeline (critical path):**
1. **Quality Analysis** — which analyzer is wired in Worker vs UI; trained-model vs stub coefficients.
2. **OCR** — Tesseract executor path; confirm engine-of-record; confirm dormant VLM is *not* counted as gap.
3. **Fusion / Reconciliation** — `FusionExpedienteService`; **which sources actually reach it** in the worker path (PDF only? XML/DOCX null?).
4. **Classification** — real path vs deeper semantic extraction TODO.
5. **Export** — `AdaptiveExporter` (SIRO XML / Excel / PDF) wiring in Worker + UI.

**Services:**
6. **Orion (ingestion)** — `IDocumentDownloader` (real scraper vs `StubDocumentDownloader`), `StubExxerHub`, `IngestionOrchestrator.StartAsync` (poll/watcher placeholder?), the 3-process Ember split status.
7. **Athena (processing)** — `ProcessingOrchestrator` end-to-end thread; OCR→Fusion wiring; readiness probes (`// TODO: orchestrator.IsStarted`).
8. **Sentinel (monitoring)** — **never traced before; trace it now.** What exists, what's wired, what's stub.

**Cross-cutting:**
9. **Auth abstraction** — `IIdentityProvider`/`ITokenService`/`IUserContextAccessor` + `EfCoreIdentityAdapter`/JWT — registered anywhere, or UI uses Identity directly?
10. **Dashboard/metrics** — worker `/dashboard` (Orion/AthenaDashboardService zeros) vs UI `IProcessingMetricsService` (real). Confirm which is on the MVP path.
11. **Persistence gaps** — PersonIdentityResolver DB persistence; PDF text extraction (empty pending iText/PdfSharp).

For each: **what's there · is it wired · does it meet intent · evidence (file:line)**.

### Phase 4 — Adversarial verification of candidate gaps

For every gap surfaced in Phases 1–3, try to **refute it before recording it**:
- Is it actually unreachable, or did the trace miss a registration? (re-grep for the type in all `*.cs` DI files)
- Is it dormant-by-design rather than a true gap?
- Does a test already exercise the "missing" behaviour (meaning it's implemented elsewhere)?
- Reproduce the claim (read the method body; if it returns empty/zero/null unconditionally, confirm).

Only gaps that **survive refutation** go in the matrix as real. This mirrors the ITDD adversarial-review discipline already used in this repo.

### Phase 5 — Synthesise: refreshed matrix + concrete MVP path

1. **Refreshed gap matrix** — `GAP-MATRIX-2026-06-11.md`: every component, state, evidence, one-line verdict; explicit Diff column vs the 2026-06-07 matrix (Closed / Still-open / Changed / New).
2. **Concrete MVP path** — `MVP-PATH-2026-06-11.md`: ordered task list, each with:
   - Definition of Done (binary, ties to a Phase-2 acceptance line)
   - Dependencies (what must precede it)
   - Rough size (S/M/L) + leverage note (does it unblock others?)
   - Risk / unknowns
   - Ordered so the **highest-leverage MVP-blocking** items come first (current prime suspect: Orion headless ingestion chain / `IDocumentDownloader` real adapter + `IngestionOrchestrator` watcher).
3. **Honest readiness statement** — one paragraph: what is demo-able today, what's the shortest credible distance to MVP, what's explicitly post-MVP.

---

## 5. Deliverables (all under `docs/planning/gap-analysis/`)

| # | File | Phase |
|---|------|-------|
| 1 | `PRD-RECONCILIATION-2026-06.md` | 1 |
| 2 | `MVP-DEFINITION-2026-06.md` (demo / MVP / post-MVP tiers) | 2 |
| 3 | `GAP-MATRIX-2026-06-11.md` (refreshed, with diff vs June 7) | 3–4 |
| 4 | `MVP-PATH-2026-06-11.md` (ordered, DoD per item) | 5 |
| 5 | Update `CLAUDE.md` Release Status + memory pointers once verified | post |

---

## 6. Execution options (decide after reviewing this plan)

- **Solo sequential** — I run Phases 1→5 myself, you stay close to each step. Lower cost, slower, good for a tight feedback loop on the PRD reconciliation.
- **Multi-agent workflow (opt-in)** — fan out Phase 3 traces (one agent per composition root / service) + adversarial verifiers in Phase 4, synthesise centrally. Far more thorough and faster, higher token cost. Requires explicit "use a workflow" opt-in.

**Recommendation:** do **Phase 1 (PRD reconciliation) solo and interactively** with you — it's judgment-heavy and you hold the discovery context — then consider a workflow for the mechanical breadth of Phase 3–4 if desired.

---

## 7. Guardrails / do-not-do

- Do **not** flag dormant-by-design Python/VLM or extracted SignalR as gaps.
- Do **not** reintroduce "production-ready" language — owner's standing call is "almost ready / beta with gaps".
- Do **not** repeat 195/200 projects or 600+ tests.
- Do **not** treat the Python tree duplication as a bug to dedupe in this pass (it's a separate deferred task; build is wired to `CSharp/Python/`).
- Evidence or it didn't happen — every verdict cites code.
