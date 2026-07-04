---
stepsCompleted: ["step-01-validate-prerequisites", "step-02-design-epics", "step-03-create-stories"]
inputDocuments:
  - /home/abel/.claude/projects/-home-abel-ExxerProjects-IndFusion-ExxerCube-Prisma/memory/veriqan-live-demo-ui-design-2026-07-04.md
  - Prisma/Code/Src/CSharp/03 Orchestration/Veriqan.Orchestration/Pipeline/IVerificationPipeline.cs
  - Prisma/Code/Src/CSharp/03 Orchestration/Veriqan.Orchestration/Pipeline/VerificationPipeline.cs
  - Prisma/Code/Src/CSharp/03 Orchestration/Veriqan.Orchestration/Pipeline/VerificationOutcome.cs
  - Prisma/Code/Src/CSharp/03 Orchestration/Veriqan.Orchestration/Pipeline/StatementSubmission.cs
  - Prisma/Code/Src/CSharp/03 Orchestration/Veriqan.Orchestration/Batch/IBatchProcessor.cs
  - Prisma/Code/Src/CSharp/03 Orchestration/Veriqan.Orchestration/DependencyInjection/VeriqanOrchestrationExtensions.cs
  - Prisma/Code/Src/CSharp/01 Core/Veriqan.Domain/Verification/RuleFinding.cs
  - Prisma/Code/Src/CSharp/01 Core/Veriqan.Domain/Extraction/FieldLocator.cs
  - Prisma/Code/Src/CSharp/02 Infrastructure/Veriqan.Infrastructure.Reporting/IMarkedPdfGenerator.cs
  - Prisma/Code/Src/CSharp/02 Infrastructure/Veriqan.Infrastructure.Reporting/MarkedPdfGenerator.cs
  - Prisma/Code/Src/CSharp/02 Infrastructure/Veriqan.Infrastructure.Extraction/PdfPigStatementFieldExtractor.cs (PDFtoImage usage pattern)
  - Prisma/Code/Src/CSharp/04 Services/Veriqan.Worker/Program.cs (AddVeriqan(config) + /verify wiring reference)
  - Prisma/Code/Src/CSharp/04 Services/Veriqan.Worker/Dockerfile
  - Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI/** (existing canned demo UI to evolve)
  - Prisma/Code/Src/CSharp/08 Tests/03 Orchestration/Veriqan.Orchestration.Tests/VecChecklistDemoE2ETests.cs (real DI wiring recipe + real fixture verdicts)
  - Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/Components/Pages/HybridExtraction.razor (sibling live-pipeline Blazor page to mirror)
  - docker-compose.veriqan.yml
  - docs/planning-artifacts/epics-veriqan-vec-demo-2026-06-27.md (house format reference)
  - docs/planning-artifacts/TRACKER-veriqan-epic4.md (house format reference)
generatedBy: "architect (Winston) driven by orchestrator on owner's behalf"
status: "PLANNED — for a fresh-context implementing agent. NOT yet built."
branch: Liv
---

# Veriqan Live Demo UI — Epic Breakdown

## Overview

Today `ExxerCube.Prisma.Veriqan.Web.UI` is a **canned mockup**: `DemoDataService` hard-codes
four `DemoStatementCase` instances (Green/Yellow/Red/ExtractionGap) and renders them on four
separate static Razor pages (`GreenCase.razor`, `YellowCase.razor`, `RedCase.razor`,
`BlockedCase.razor`). It never touches the real pipeline, has zero tests, and is not deployed
in any compose stack.

This epic set builds a **live, pipeline-driven** replacement: the audience (bank legal/compliance
personnel, medium tech, software-appropriation mindset) clicks a fixture card, the **real**
`IVerificationPipeline.ProcessAsync` runs end-to-end (ingest → extract → bind → validate →
aggregate), and the result renders as a **rasterized "Marked Page" hero** (the statement itself,
with amber/red highlight boxes and numbered callouts already drawn by the existing
`MarkedPdfGenerator`) next to a **findings rail** in plain language with CONDUSEF DOF-numeral
citations. Visual findings (typography, logo, ad-placement) are the hero; data/arithmetic
findings ride the same rail as a secondary wave. A canned fallback (the *existing*
`DemoDataService`, kept genuinely separate) protects the demo if the live path times out or
errors.

**What is reuse vs new, up front (so the implementing agent isn't guessing):**
- **100% reuse, zero changes needed:** `IVerificationPipeline`/`VerificationPipeline`,
  `IMarkedPdfGenerator`/`MarkedPdfGenerator` (already draws amber/red boxes + numbered callouts,
  handles page rotation, never mutates the source PDF), `RuleFinding`/`FieldLocator`
  (already carry `DofNumeral`, `Confidence`, bounding-box `Locator`), `AddVeriqan(config)` DI
  composition root, the 4 demo fixture PDFs (`good.pdf`, `bad-math-cl21.pdf`, `bad-font-cl35.pdf`,
  `scanned.pdf`) and their reference bundle, `DemoDataService`/`DemoStatementCase`/`DemoFinding`
  shape (becomes the canned-fallback AND the shared live/canned view-model).
- **New code required:** a DI wire-up in the Web.UI host, an `IDemoRunner` live/fallback seam, a
  PDF→PNG rasterization wrapper service, a `VerificationOutcome → DemoStatementCase` mapper, ONE
  new templated result view, ONE new live page (fixture cards + narration), a brand-new test
  project (the Web.UI currently has none), a Dockerfile + compose service.
- **Real gap discovered while grounding this doc (not previously documented):** the demo's
  `ChecklistIds.cs` (55 labels/DofNumerals/tiers) is a **synthetic, demo-only catalog** — grepping
  the real rule implementations shows only 11 CheckIds are actually implemented as visual rules
  (`CL-28, CL-29, CL-31, CL-33, CL-34, CL-35, CL-48, LAW-ADS-PLACEMENT, LAW-SEC-SIZECAP,
  LAW-TYPO-BOLD, LAW-TYPO-MINSIZE`) and the real engine's CheckId universe (~56 distinct IDs
  across `Veriqan.Infrastructure.Validation` + `.Visual`) does **not** line up 1:1 with
  `ChecklistIds.cs`'s fictional `CL-1..CL-55` numbering (e.g. the real `CL-32` is
  "Compara-tu-Tarjeta section present", not the fake label's "Correlación de páginas continua").
  **The live path must NOT reuse `ChecklistIds.Label()`/`DofNumeral()`/`Tier()` for real
  findings** — `RuleFinding` already carries the authoritative `DofNumeral` from the engine, and a
  trustworthy plain-language label per *real* CheckId is a deliverable of the S-prep-B ledger
  story below, not an existing asset.

## GLOBAL CONSTRAINT — read before any story

- **Stack:** .NET 10; `Result<T>` (no exceptions for business logic — including the new
  `IDemoRunner`/mapper/renderer seams); `CancellationToken` on every async method, propagated
  end-to-end; nullable reference types + `TreatWarningsAsErrors`; `GenerateDocumentationFile`.
- **Tests:** xUnit v3 + Shouldly + NSubstitute. **NO Moq, NO FluentAssertions.**
  **CRITICAL gotcha:** `dotnet test <csproj>` in this repo can falsely report "Zero tests ran."
  Always build first, then run the built binary directly:
  `dotnet exec <BuildArtifacts>/Prisma/bin/<AssemblyName>/net10.0/<AssemblyName>.dll`
  (see CLAUDE.md for the exact `BuildArtifacts` root). Use `TestContext.Current.CancellationToken`
  in tests, never a manually constructed token.
- **Dependency direction:** `Veriqan.Web.UI → Veriqan.{Domain,Application,Orchestration,
  Infrastructure.*}` only. **Never** reference anything under `01 Core/*` or `02 Infrastructure/*`
  that belongs to the Prisma (non-Veriqan) product. An architecture test enforces the one-way
  `Veriqan → Prisma` rule (nothing may go the other way) — do not fight it, do not add a Prisma
  reference to any Veriqan project.
- **Blazor traps (all previously paid for in blood on this codebase — do not re-learn them):**
  1. `IVerificationPipeline` is registered **Scoped** (bounds the scoped `VeriqanDbContext`). A
     Blazor Server **circuit is NOT a per-request scope** — it is long-lived. Every submission run
     MUST resolve its own fresh scope via `IServiceScopeFactory.CreateScope()` (or
     `CreateAsyncScope()`), resolve `IVerificationPipeline` from that scope, and dispose the scope
     when the run completes. Never inject `IVerificationPipeline` directly into a component or a
     singleton service.
  2. **Never call `.ConfigureAwait(false)` anywhere in the interactive-component call chain before
     a `StateHasChanged()`** — a prior Prisma Blazor page (`HybridExtraction.razor`, S3b) crashed
     the circuit this exact way; the fix was removing `ConfigureAwait(false)` from the
     component-triggered async chain. Library/service code below the component boundary may still
     use `ConfigureAwait(false)` as normal .NET convention — the trap is specifically the
     component's own render-triggering await chain.
  3. **Warm up `ProcessAsync` at host startup** (a `IHostedService`) — the first real call touches
     CSV reference-data loading, tenant-profile resolution, and (if SQL-backed)
     `SqlLegalToleranceProvider` cache warming; an unwarmed first call in front of a live audience
     is a visible multi-second stall.
  4. **Guard PDF upload size before invoking the pipeline** (fast, synchronous, pre-`IDemoRunner`)
     — `PdfExtractionOptions.MaxSizeBytes` (default 50 MB, section `Veriqan:PdfExtraction`,
     `Prisma/Code/Src/CSharp/02 Infrastructure/Veriqan.Infrastructure.Extraction/PdfExtractionOptions.cs`)
     is already enforced deep inside extraction, but a UI that lets someone start a 200 MB upload
     and then narrate stage-labels for 30 seconds before failing is a demo-credibility bug. Reject
     fast with a snackbar, do not even call `IDemoRunner`.
  5. A **"baseline loaded / ready" flag** must gate the Run button — do not let a click race the
     startup warm-up hosted service.
- **Golden-master honesty (carried from the Epic 4 tracker, still applies):** `good.pdf` is a
  genuine RED verdict (13 structural failures, LAW-SEC-PRESENCE etc.) — this is the **true**
  result, not a bug to "fix" before the demo. Never suppress, relabel, or cherry-pick a finding to
  make a fixture look better than its real verdict. Abstain (`InsufficientData`) findings render
  grey/"couldn't measure," never hidden.
- **Verify every claim from ground truth** (build/test/git) before marking a story done. Commit in
  self-contained chunks.

## Requirements Inventory

### Functional Requirements
- **FR-V1** The demo UI submits a real PDF to the real `IVerificationPipeline.ProcessAsync` and
  renders the actual `VerificationOutcome` (not a hard-coded case) when live mode succeeds.
- **FR-V2** The result view renders the `MarkedPdfGenerator` output rasterized to an image — the
  "Marked Page" hero — showing amber (bank-tier-only) and red (CONDUSEF/Both/unmapped)
  highlight boxes with the engine's own sequential numbered callouts.
- **FR-V3** Each Fail finding appears on a "rail" beside the hero as: plain-language description,
  `CheckId`, `DofNumeral` citation, Expected/Observed, and a tier tag
  (`[RED · fails checklist AND law]` / `[AMBER · fails law only]`).
- **FR-V4** Findings are split into a visual-primary group and a data-secondary group, surfaced via
  a ribbon (`[N VISUAL][M DATA]`); visual findings are visually foregrounded.
- **FR-V5** `InsufficientData` (abstain) findings render distinctly (grey), never hidden or folded
  into Pass/Fail counts.
- **FR-V6** A single live page replaces the current 4 static verdict pages + Upload + Overview:
  fixture cards (ghost "not yet analyzed" chips) → click → narrated stage progression (~5–10 s) →
  hero + rail result.
- **FR-V7** When live mode is disabled, times out, or errors, the UI falls back to the existing
  canned `DemoDataService` case for that signal, and visibly badges the result `LIVE` vs
  `DEMO DATA` — the fallback must not share a failure mode with the live path (a live-path bug must
  never also break the fallback).
- **FR-V8** The service is deployable as its own container in the Veriqan compose stack, reachable
  on a distinct host port, health-checked, using a distinct compose project name.
- **FR-V9** Every LAW-tier finding cites a verbatim CONDUSEF article reference, and every CL-tier
  (bank-brand) finding is explicitly labelled "brand standard, not law" — verified against the real
  tenant reference bundle (`checklist-tiers.csv`) / Iqubica source CSV, not the demo's drifted
  `ChecklistIds.cs` labels.

### Non-Functional Requirements
- **NFR-V1** No exceptions cross the `IDemoRunner`/mapper/renderer public surface — `Result<T>`
  throughout; cancellation is honored end-to-end.
- **NFR-V2** The live path and the canned-fallback path must be **independently testable and
  independently failable** — a defect in one must not be observable as a defect in the other
  (enforced by the fallback unit tests in VLD-S2).
- **NFR-V3** `dotnet build` of the full solution stays 0 warnings / 0 errors throughout (this repo
  treats warnings as errors); all pre-existing Veriqan test suites
  (`Veriqan.Orchestration.Tests`, `Veriqan.Infrastructure.Reporting.Tests`, etc.) stay green.
- **NFR-V4** No PII/production-shaped values are hardcoded into new labels/citations beyond what
  the existing demo fixtures already contain.

## Epic List

**Dependency graph** (arrows = "depends on"):

```
VLD-P1 (locator bbox audit)  ⟂  VLD-P2 (LAW-vs-BRAND ledger)      ← run in parallel, anytime,
                                                                      independent of all code stories
VLD-S1 (DI wiring + first visible win)
   ├──> VLD-S2 (IDemoRunner + config + NEW test project + warm-up)
   └──> VLD-S3 (Marked-page PDF→PNG rasterization service)
                          │
                          ▼
VLD-S4 (VerificationOutcome→DemoStatementCase mapper + ONE templated result view)
   [also consumes VLD-P1 + VLD-P2 outputs for label/citation accuracy]
                          │
                          ▼
VLD-S5 (the ONE live page: fixture cards + narration + hero/rail + LIVE/DEMO badge)
   [depends on VLD-S2 + VLD-S4]
                          │
              ┌───────────┴───────────┐
              ▼                       ▼
VLD-S6 (retire 4 static pages)   VLD-S7 (Docker Compose deployment)
```

**Suggested build order:** VLD-S1 → VLD-S3 → VLD-S2 → (VLD-P1, VLD-P2 in parallel, ideally
finished before S4) → VLD-S4 → VLD-S5 → {VLD-S6, VLD-S7 in parallel}.

1. **Epic A — Demo Credibility Prep Gates** (VLD-P1, VLD-P2): non-UI verification/data work that
   gates whether the visual hero and its legal citations are honest. Not code-blocking for Epic B,
   but must land before Epic C's copy is written.
2. **Epic B — Live Pipeline Backend Seam** (VLD-S1, VLD-S2, VLD-S3): wires the real pipeline into
   the Web.UI process, adds the live/fallback seam, adds the rasterization service. Produces the
   first visible win (a real PDF rendered as a marked PNG) before any new page exists.
3. **Epic C — Unified Result UI** (VLD-S4, VLD-S5): the templated result view and the one live
   page that ties everything together into the actual demo experience.
4. **Epic D — Cleanup & Deployment** (VLD-S6, VLD-S7): retires the old static pages and ships the
   service as a container in the Veriqan compose stack.

---

## Epic A — Demo Credibility Prep Gates

These two stories are **not UI code**. They gate whether the visual-hero story the sales pitch
depends on is actually true. Both were flagged by the design party as "verify before you build the
persuasive UI around it."

### VLD-P1: Locator bounding-box audit per visual rule

**Goal:** confirm — for each of the 11 real visual-rule CheckIds — whether a Fail finding actually
carries a tight `FieldLocator.HasBoundingBox` (precise rectangle) or only a page-level hint. If a
rule only ever produces a page hint, `MarkedPdfGenerator` degrades to a small margin marker
(`DrawPageMarker`), not a tight highlight box — which quietly undercuts the "see the exact
logo/font problem" sales pitch. This must be known **before** VLD-S4/S5 promise a tight box in the
UI copy.

**Acceptance Criteria:**
- **Given** the 11 real visual CheckIds (`CL-28, CL-29, CL-31, CL-33, CL-34, CL-35, CL-48,
  LAW-ADS-PLACEMENT, LAW-SEC-SIZECAP, LAW-TYPO-BOLD, LAW-TYPO-MINSIZE`) — found via
  `grep -rhoE '"CL-[0-9]+"|"LAW-[A-Za-z0-9§-]+"' "Prisma/Code/Src/CSharp/02 Infrastructure/Veriqan.Infrastructure.Visual/Rules"`
  **When** each rule's source is inspected (and, where feasible, exercised against the demo
  fixtures `good.pdf` / `bad-font-cl35.pdf`) **Then** a table is produced recording, per CheckId:
  whether it constructs `FieldLocator` with all 4 of `Left/Bottom/Width/Height` populated (tight
  box) or only `PageHint`/`NoPage` (marker-only), and under what conditions each occurs.
- **And** for `bad-font-cl35.pdf` specifically (the fixture whose whole narrative is "see the wrong
  font"), confirm CL-35's actual finding (from a real pipeline run, not a guess) carries a tight
  box — this is the fixture the demo leads with.
- **And** the table is committed as a short markdown note (no code changes) at
  `docs/planning-artifacts/veriqan-locator-bbox-audit-2026-07.md`, and VLD-S4/S5's copy is written
  to match reality (if a rule is marker-only, the rail must say "flagged on page N" rather than
  implying a tight box exists).

**Files to touch/create:**
- New: `docs/planning-artifacts/veriqan-locator-bbox-audit-2026-07.md` (findings table).
- No production code changes in this story — it is pure investigation. If the audit finds a rule
  that SHOULD have a tight box but the code takes a lazy `PageHint` shortcut, file that as a
  follow-up note in the table; do not fix it inline (scope creep) unless it is a one-line fix with
  an existing test to protect it — if so, note the diff separately in the table.

**Tests:** none required (investigation-only story). If a code fix is made per the note above, the
existing `Veriqan.Infrastructure.Visual.Tests` suite must stay green (verify via `dotnet exec`).

**Verify from ground truth:** re-run the grep above against the actual repo state (rule set may
have grown since this doc was written); for the "real pipeline run" check, reuse the DI recipe in
`VecChecklistDemoE2ETests.cs` (lines ~310–345) in a throwaway console/test harness pointed at
`Prisma/Fixtures/PRP2/demo/bad-font-cl35.pdf` with reference bundle root
`Prisma/Fixtures/PRP2/demo/reference-bundle/` and context key
`new StatementContextKey("Demo Bank (Iqubica)", PeriodLabel: "Mar-Abr 2026")`; inspect the
returned `RuleFinding.Locator` for CL-35 directly — do not trust documentation comments.

---

### VLD-P2: LAW-vs-BRAND citation ledger

**Goal:** produce the one-page, verified ledger the sales narrative depends on: for every real
CheckId that can appear in a demo run, an honest verbatim CONDUSEF article citation (for LAW-*) or
an explicit "brand standard, not law" label (for CL-*), sourced from the **real tenant bundle**
(`Prisma/Data/Veriqan/reference-bundles/Demo_Bank_(Iqubica)/checklist-tiers.csv`, 56 rows,
`checkId,tier` columns) and the raw Iqubica source
(`Prisma/Fixtures/PRP2/Check+list+demo+v2+Iqubica_Check_List_VEC.csv` and sibling files under
`Prisma/Fixtures/PRP2/`) — **not** the demo's `ChecklistIds.cs`, which this investigation already
found to be a disconnected, partially-fictional 55-ID catalog (e.g. its `CL-32` label
"Correlación de páginas continua" does not match the real `CL-32` rule, which is about the
Compara-tu-Tarjeta section).

**Acceptance Criteria:**
- **Given** the real CheckId universe (`grep -rhoE '"CL-[0-9]+"|"LAW-[A-Za-z0-9§-]+"'` across
  `Veriqan.Infrastructure.Validation/Rules` and `Veriqan.Infrastructure.Visual/Rules` — 56 distinct
  IDs as of this writing) **When** each is cross-referenced against `checklist-tiers.csv` (tier:
  Bank/Condusef/Both) and, for LAW-* IDs, the rule's own `DofNumeral` string (already stamped by
  the engine — read it directly off a real `RuleFinding`, don't re-derive it)
  **Then** a ledger is produced with columns: CheckId, Tier, DofNumeral-or-"(brand standard, not
  law)", one-sentence plain-language description suitable for the rail UI, and a `IsVisual`
  boolean (see classification note below).
- **And** the ledger explicitly flags any CL-* ID whose UI-facing label would otherwise imply legal
  force (the memory doc's example: `CL-33` logo presence is "Acuerdo §1" but is literally "is there
  any embedded image on the page" — a blunt proxy; label it plainly as brand/visual, not as strong
  law).
- **And** the `IsVisual` classification (used by VLD-S4's `[N VISUAL][M DATA]` ribbon) is
  documented with its rationale per ID: seed it from "implemented in `Veriqan.Infrastructure.
  Visual`" (11 IDs) **plus** `CL-37` (contrast — semantically visual though implemented in
  `Validation`, confirmed by grep: `grep -rn '"CL-37"' "Prisma/Code/Src/CSharp/02 Infrastructure/Veriqan.Infrastructure.Validation"`).
  This is a demo-narrative classification, not an engine concept — mark it clearly as such and
  flag it for owner sign-off, since a wrong split undercuts the "visual is the hero" pitch.
- **And** the ledger is committed as data the mapper (VLD-S4) actually loads (not just a doc) — a
  small static C# lookup or embedded JSON/CSV resource under the Web.UI project, e.g.
  `Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI/Services/RealCheckLedger.cs`
  (or `.json` resource + loader) — is committed, keyed by CheckId, and covers at minimum every
  CheckId that appears in a live run of the 4 demo fixtures (verify by actually running them, per
  VLD-P1's harness).

**Files to touch/create:**
- New: `docs/planning-artifacts/veriqan-law-vs-brand-ledger-2026-07.md` (the human-readable ledger
  + methodology).
- New: `Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI/Services/RealCheckLedger.cs`
  (or equivalent data file) — the machine-readable form VLD-S4 consumes.

**Tests:**
- New: a small unit test (in the VLD-S2 test project once it exists, or stubbed here and moved) —
  `RealCheckLedgerTests.Lookup_ForEveryCheckIdSeenInDemoFixtures_ReturnsEntry` — feed it the actual
  set of CheckIds observed from running all 4 fixtures through the real pipeline (hardcode the
  observed set from the VLD-P1 harness run) and assert every one resolves to a ledger entry.

**Verify from ground truth:** re-run both grep commands above; diff the resulting CheckId sets
against `checklist-tiers.csv`'s 56 rows to make sure nothing is missing or stale; do not hand-copy
`ChecklistIds.cs` content into the new ledger — that catalog is the thing being replaced for the
live path (the OLD catalog can stay as-is for the canned-fallback pages, since those already work
against `ChecklistIds.cs` and are not being rewritten by this epic).

---

## Epic B — Live Pipeline Backend Seam

### VLD-S1: Wire the real pipeline into the Web.UI process (first visible win)

**Goal:** get `AddVeriqan(config)` composing successfully inside
`ExxerCube.Prisma.Veriqan.Web.UI`, and prove — via the smallest possible harness, not yet a full
page — that `IVerificationPipeline.ProcessAsync` + `IMarkedPdfGenerator.Generate` + PDFtoImage's
`Conversion.ToImage` together turn `good.pdf` into a viewable PNG. This is the "early visible win"
the mission asks for.

**Acceptance Criteria:**
- **Given** the Web.UI csproj currently references only `Veriqan.Domain` + `Veriqan.Application`
  **When** a `ProjectReference` to
  `..\..\..\03 Orchestration\Veriqan.Orchestration\ExxerCube.Prisma.Veriqan.Orchestration.csproj`
  is added **Then** the solution still builds 0 warnings/0 errors (this single reference pulls in
  the full `Infrastructure.{Extraction,Persistence,ReferenceData,Reporting,Validation,Visual}`
  graph transitively via `Veriqan.Orchestration`'s own references — do not add those individually).
- **And** `Program.cs` calls `builder.Services.AddVeriqan(builder.Configuration)` (mirroring
  `Veriqan.Worker/Program.cs`) in addition to the existing `AddSingleton<DemoDataService>()` (kept,
  not removed — it is the fallback).
- **And** `appsettings.json` gains a `Veriqan:CsvReferenceData:RootDirectory` entry pointed at the
  demo reference bundle root (`Prisma/Fixtures/PRP2/demo/reference-bundle`, resolved to an absolute
  path at startup the same way `VecChecklistDemoE2ETests.ComputeDemoCorpusDir()` walks up to find
  `CLAUDE.md` — reuse/port that resolution logic rather than a hardcoded relative path, since the
  Web.UI's working directory differs between `dotnet run` and the published container).
- **And** for local/dev runs `ConnectionStrings:VeriqanDb` is **omitted** (falls back to
  `AddVeriqanInMemoryPersistence()` inside `AddVeriqan`, per the conditional logic in
  `VeriqanOrchestrationExtensions.cs` lines ~108–134) — no SQL Server dependency for `dotnet run`.
  **VERIFY FIRST:** decide (and document the decision in this story's commit message) whether the
  deployed container (VLD-S7) should instead point at the *same* `VeriqanDb` the worker uses (for a
  durable audit trail of live demo runs) or stay in-memory (simpler, no risk of a demo run
  colliding with worker data). Default recommendation: **in-memory**, since this is a sales-demo
  surface, not a system of record — but this is an explicit open decision, not a silent default.
- **And** a throwaway proof (a `[Fact]`-style test or a `dotnet run`-able console snippet — your
  choice, but it must be checked in as a real automated test, not a manual scratch script) does the
  following against `good.pdf`: builds the DI container the same way `Program.cs` does, resolves
  `IVerificationPipeline` from a scope, calls `ProcessAsync`, asserts the outcome's
  `Summary.Signal == VerdictSignal.Red` (the known-true verdict per `VecChecklistDemoE2ETests`),
  then feeds `outcome.Findings` + the original PDF bytes into `IMarkedPdfGenerator.Generate`,
  asserts success, then calls `PDFtoImage.Conversion.ToImage(stream, leaveOpen: false, page: 0,
  options: new RenderOptions(Dpi: 150))` (the exact pattern already used at
  `PdfPigStatementFieldExtractor.cs:3447` and `:3635` — copy it, don't reinvent) and asserts a
  non-null `SKBitmap` with `Width > 0 && Height > 0`.

**Files to touch/create:**
- Edit: `Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI/ExxerCube.Prisma.Veriqan.Web.UI.csproj`
  (add the Orchestration `ProjectReference`).
- Edit: `Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI/Program.cs`
  (`AddVeriqan(config)` call).
- Edit: `Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI/appsettings.json`
  (reference-data root config).
- New: a repo-root-finder helper, e.g.
  `Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI/Services/DemoCorpusPathResolver.cs`
  (port the walk-up-to-`CLAUDE.md` logic).
- New (test project stub — full project setup happens in VLD-S2, but this story's proof test can
  live here as the first file in it if you sequence VLD-S2's csproj creation first):
  a proof test such as `LivePipelineWiringProofTests.cs`.

**Tests:** the proof test described above (1 test is enough for this story — it is deliberately a
thin vertical slice, not full coverage). Pre-existing suites
(`Veriqan.Orchestration.Tests`, `Veriqan.Infrastructure.Reporting.Tests`, `Veriqan.Application.Tests`)
must stay green — this story only adds a reference, it must not touch any existing production code.

**Verify from ground truth:**
`dotnet build "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"` → 0/0; then build + `dotnet exec` the
new test assembly and confirm the proof test passes with a real `Red` verdict and a real
`SKBitmap`. Do not accept "it compiles" as done — this story's entire point is a working vertical
slice.

---

### VLD-S2: `IDemoRunner` live/fallback seam + config + warm-up + NEW test project

**Goal:** the seam that decides live-vs-canned per submission, the config that controls it, the
startup warm-up, and the dedicated test project the Web.UI has never had.

**Acceptance Criteria:**
- **Given** a new `DemoOptions` class (`Veriqan:Demo` config section) with
  `LiveModeEnabled` (bool, default `true`), `FallbackOnFailure` (bool, default `true`), and
  `LiveTimeout` (`TimeSpan`, default 20 s) **When** bound via `services.Configure<DemoOptions>
  (config.GetSection(DemoOptions.Section))` **Then** all three are overridable via
  `appsettings.json` / env vars (`Veriqan__Demo__LiveModeEnabled` etc.).
- **Given** a new `IDemoRunner` with
  `Task<Result<DemoRunOutcome>> RunAsync(byte[] pdf, string fileName, CancellationToken ct = default)`
  where `DemoRunOutcome(DemoStatementCase Case, bool IsLive)`
  **When** `LiveModeEnabled == false` **Then** it returns a canned case (matched by filename against
  the 4 known demo fixtures where possible, else round-robins/defaults to a specific signal) with
  `IsLive = false`, without ever touching `IServiceScopeFactory` or `IVerificationPipeline`.
- **When** `LiveModeEnabled == true` and the live call succeeds within `LiveTimeout`
  **Then** it opens a scope via `IServiceScopeFactory.CreateAsyncScope()`, resolves
  `IVerificationPipeline` from that scope, calls `ProcessAsync` with a `CancellationTokenSource`
  linked to both the caller's token and `LiveTimeout`, maps the result via
  `IVerificationOutcomeMapper` (VLD-S4) to a `DemoStatementCase`, disposes the scope, and returns
  `IsLive = true`.
- **When** the live call throws, returns a failure `Result`, or times out, and
  `FallbackOnFailure == true` **Then** it returns the matching canned `DemoDataService` case with
  `IsLive = false` (never lets the exception/failure escape to the caller).
- **When** the live call fails and `FallbackOnFailure == false` **Then** the failure/timeout
  **propagates** as a failed `Result<DemoRunOutcome>` (not an exception) — the caller (the page, in
  VLD-S5) is responsible for rendering an error state.
- **Given** a PDF larger than `PdfExtractionOptions.MaxSizeBytes` **When** `RunAsync` is called
  **Then** `IDemoRunner` rejects it with a failure `Result` **before** touching
  `IServiceScopeFactory`/`IVerificationPipeline` at all (verified by an NSubstitute
  `pipeline.DidNotReceive()` assertion, not just an integration check).
- **Given** a new `IHostedService` (e.g. `PipelineWarmupHostedService`) **When** the host starts
  **Then** it runs one no-op-shaped `ProcessAsync` call (or a lighter warm-up the implementer
  judges sufficient — e.g. resolving `IVecReferenceDataProvider` and triggering its cache load) in
  a background scope, sets a shared `IReadinessFlag`/`IOptionsMonitor`-backed "ready" bit, and never
  blocks `StartAsync` itself (fire-and-forget with logged failure, matching the "readiness probes
  present but real" convention already used elsewhere in this repo — do not block host startup on
  this).
- **Given** the new test project **When** built **Then** it contains, at minimum, these named test
  cases (NSubstitute-mocked `IVerificationPipeline` + `IServiceScopeFactory`, no real PDF I/O
  needed for these): `RunAsync_LiveDisabled_ReturnsCannedCase`,
  `RunAsync_LiveFails_FallbackEnabled_ReturnsCannedCase`,
  `RunAsync_LiveThrows_FallbackEnabled_ReturnsCannedCase`,
  `RunAsync_LiveTimesOut_FallbackEnabled_ReturnsCannedCase`,
  `RunAsync_LiveFails_FallbackDisabled_PropagatesFailure`,
  `RunAsync_OversizedPdf_PipelineNeverInvoked`, plus a scope-lifetime test
  `RunAsync_Live_ResolvesPipelineFromFreshScopePerCall` (assert
  `IServiceScopeFactory.CreateAsyncScope()` — or `CreateScope()` — is invoked once per `RunAsync`
  call, and that the scope is disposed after the call, e.g. via a spy scope or NSubstitute
  `Received(1)`).

**Files to touch/create:**
- New project:
  `Prisma/Code/Src/CSharp/08 Tests/07 UI/ExxerCube.Prisma.Veriqan.Web.UI.Tests/ExxerCube.Prisma.Veriqan.Web.UI.Tests.csproj`
  (mirror an existing Veriqan test csproj's package refs — xunit.v3.mtp-v2, Shouldly, NSubstitute,
  Meziantou logging — plus a `ProjectReference` to the Web.UI project itself). Add it to
  `Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln`.
- New: `.../ExxerCube.Prisma.Veriqan.Web.UI/Options/DemoOptions.cs`.
- New: `.../ExxerCube.Prisma.Veriqan.Web.UI/Services/IDemoRunner.cs` + `DemoRunner.cs` +
  `DemoRunOutcome.cs`.
- New: `.../ExxerCube.Prisma.Veriqan.Web.UI/Services/PipelineWarmupHostedService.cs`.
- Edit: `Program.cs` (`services.Configure<DemoOptions>(...)`, `services.AddScoped<IDemoRunner,
  DemoRunner>()` — note: **scoped**, not singleton, because it depends on a fresh scope per call
  and is itself called from the Blazor circuit's own DI scope; `services.AddHostedService
  <PipelineWarmupHostedService>()`).
- New test files under the new test project:
  `Services/DemoRunnerTests.cs`, `Services/DemoRunnerScopeLifetimeTests.cs`.

**Tests:** all named above; run via `dotnet exec` per the global constraint. Target: 100% of the
new `DemoRunner` branches covered (it is a small, branch-heavy class — this is realistic).

**Verify from ground truth:** build the new test project, run it via the `dotnet exec` pattern,
confirm all 7 named tests pass; confirm `dotnet build` of the full solution stays 0/0 with the new
project added to the `.sln`.

---

### VLD-S3: Marked-page PDF→PNG rasterization service

**Goal:** a small, independently-testable wrapper that takes a marked PDF (already produced by the
existing `IMarkedPdfGenerator`) plus the findings list, and returns one PNG per page that has at
least one Fail finding (falling back to page 1 if there are none — e.g. a GREEN case still needs
*a* hero image).

**Acceptance Criteria:**
- **Given** an `IMarkedPageRenderer` with
  `Result<IReadOnlyDictionary<int, byte[]>> RenderFindingPages(byte[] markedPdf,
  IReadOnlyList<RuleFinding> findings, CancellationToken cancellationToken = default)`
  **When** called with a valid marked PDF and findings that reference pages 2 and 4
  **Then** the result contains exactly PNG bytes for pages 2 and 4 (keyed by 1-based page number),
  rendered via `PDFtoImage.Conversion.ToImage(..., page: pageNumber - 1, options: new
  RenderOptions(Dpi: 150))` (150 DPI matches the existing `FiscalPageRenderDpi` convention at
  `PdfPigStatementFieldExtractor.cs:3525` — reuse the same constant value for visual consistency,
  do not invent a new DPI), then `SKBitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100)` to get
  PNG bytes.
- **And** when `findings` contains no Fail entries at all (e.g. a GREEN verdict), the result
  contains page 1 only (the "cover" view).
- **And** cancellation is honored (mid-render `OperationCanceledException` is caught and converted
  to a cancelled `Result`, per the `MarkedPdfGenerator.Generate` convention already in this
  codebase — copy that try/catch shape).
- **And** an invalid/empty `markedPdf` byte array returns a failure `Result`, not a thrown
  exception.
- **And** a page number from a finding's `Locator.PageNumber` that is out of range (0 = `NoPage`
  sentinel, or > actual page count) is silently skipped (matches `MarkedPdfGenerator`'s own
  behavior for consistency) rather than failing the whole render.

**Files to touch/create:**
- New: `.../ExxerCube.Prisma.Veriqan.Web.UI/Services/IMarkedPageRenderer.cs` + `MarkedPageRenderer.cs`.
- Edit: `Program.cs` (`services.AddSingleton<IMarkedPageRenderer, MarkedPageRenderer>()` — this
  service is stateless, singleton is fine unlike the pipeline itself).
- Edit: `.../ExxerCube.Prisma.Veriqan.Web.UI.csproj` — add `PackageReference Include="SkiaSharp"`
  if not already transitively available (it should be, via the Orchestration→Extraction chain
  added in VLD-S1, but confirm; central version comes from `Directory.Packages.props`, do not pin a
  local version).

**Tests** (in the VLD-S2 test project):
`Services/MarkedPageRendererTests.cs` — `RenderFindingPages_FindingsOnPages2And4_ReturnsThoseTwoPngs`,
`RenderFindingPages_NoFailFindings_ReturnsPageOneOnly`,
`RenderFindingPages_InvalidPdfBytes_ReturnsFailure`,
`RenderFindingPages_OutOfRangePageNumber_SkipsGracefully`,
`RenderFindingPages_Cancelled_ReturnsCancelledResult`.
Use one of the real demo fixture PDFs (or a small synthetic multi-page PDF built with PdfSharp, if
you want to avoid a dependency on the fixtures existing) for the "real render" tests — a
synthetic PDF is safer/faster and keeps this test project independent of the fixture corpus.

**Verify from ground truth:** build + `dotnet exec` the test project; visually confirm at least
once (not required as an automated assertion, but worth doing manually) that a decoded PNG from
`good.pdf`'s LAW-SEC-PRESENCE finding actually looks like a readable page with a red-ish
highlight — open the bytes in an image viewer during development.

---

## Epic C — Unified Result UI

### VLD-S4: `VerificationOutcome → DemoStatementCase` mapper + ONE templated result view

**Goal:** the mapper that lets the SAME Razor rendering path serve both a live
`VerificationOutcome` and a canned `DemoDataService` case, and the single templated component that
replaces `GreenCase.razor`/`YellowCase.razor`/`RedCase.razor`/`BlockedCase.razor`.

**Acceptance Criteria:**
- **Given** an `IVerificationOutcomeMapper` with
  `Result<DemoStatementCase> Map(VerificationOutcome outcome, IReadOnlyDictionary<int, byte[]>
  markedPagePngs, string fileName, CancellationToken cancellationToken = default)`
  **When** called **Then** it populates `DemoStatementCase` fields from real data:
  `Signal = outcome.Summary.Signal`, `BankTierVerdict`/`CondusefTierVerdict` from
  `outcome.Summary`, `Findings` mapped from `outcome.Findings` (real `CheckId`, `Verdict`,
  `Expected`, `Observed`, `Confidence`, and — **new field, add to `DemoFinding`** — `Locator` so the
  rail can later cross-reference the hero's numbered callouts), `ProcessingDuration =
  outcome.ProcessingDuration`, `MarkedPdfPath` left null (PNGs are passed separately, not written to
  wwwroot — keep them in-memory / as a Blazor data-URI, per the design's "rasterize to PNG for
  Blazor" intent, not a file on disk).
- **And** the plain-language label + `DofNumeral`-or-"brand standard" tag + `IsVisual` flag for
  each finding are looked up from **`RealCheckLedger`** (VLD-P2's deliverable) by real `CheckId` —
  **explicitly NOT** from `ChecklistIds.cs` (that catalog stays reserved for the canned-fallback
  pages' pre-existing behavior only). A `CheckId` absent from the ledger renders with a visible
  "(uncatalogued check — engineering gap, not a compliance signal)" label rather than silently
  guessing — honesty over polish.
- **And** a new `DemoFinding.IsVisual` (bool) and `DemoFinding.Locator` (nullable) property are
  added (extend, don't replace, the existing `DemoFinding` model) so the SAME model serves both
  live and canned data; the canned `DemoDataService` cases get a reasonable default for the new
  fields (e.g. `IsVisual` derived from the same ledger lookup by CheckId, since its CheckIds are
  fictional-but-formatted-the-same — degrade gracefully, don't crash the existing canned pages).
- **Given** ONE new Razor component, e.g. `Components/Shared/VerdictResult.razor`, parameterized by
  a single `DemoStatementCase` **When** rendered **Then** it shows: the verdict banner (reuse the
  existing `VerdictBanner.razor`), the `[N VISUAL][M DATA]` ribbon (count `Findings.Where(f =>
  f.Verdict == Fail)` split by `IsVisual`), the hero image (an `<img>` with a `data:image/png;
  base64,...` src built from the mapped PNG bytes — plain `<img>`, no extra JS needed), and the
  rail (an ordered list of Fail findings first — sorted to match the hero's numbered callouts,
  i.e. same order `MarkedPdfGenerator` assigns callout numbers in, which is simply finding order —
  do not re-sort by severity or you'll desync the numbers — then grey `InsufficientData` entries,
  then a collapsed/summarized Pass count).
- **And** each rail card shows: plain-language description, `CheckId`, DofNumeral-or-brand tag,
  Expected/Observed, tier chip (`[RED · fails checklist AND law]` for Condusef/Both,
  `[AMBER · fails law only — not on bank checklist]` for Bank-tier-only — match these exact phrases
  from the design memo).
- **And** `VerdictResult.razor` works identically whether fed a live-mapped `DemoStatementCase` or
  one of the 4 existing canned cases (this is the acceptance test for "ONE templated view" — prove
  it by rendering all 4 canned cases plus one live-mapped case through the same component in a
  bUnit test, see below).

**Files to touch/create:**
- New: `.../ExxerCube.Prisma.Veriqan.Web.UI/Services/IVerificationOutcomeMapper.cs` +
  `VerificationOutcomeMapper.cs`.
- Edit: `.../ExxerCube.Prisma.Veriqan.Web.UI/Models/DemoFinding.cs` (add `IsVisual`, `Locator`).
- New: `.../ExxerCube.Prisma.Veriqan.Web.UI/Components/Shared/VerdictResult.razor` (+ code-behind
  if you prefer `.razor.cs`).
- Edit: `.../ExxerCube.Prisma.Veriqan.Web.UI.csproj` — add `bunit` (+ its xunit.v3 adapter package,
  version TBD — see VERIFY FIRST below) for the render test.

**Tests** (in the VLD-S2 test project, or a bUnit-specific subfolder within it):
`Services/VerificationOutcomeMapperTests.cs` (`Map_RedOutcome_PopulatesFindingsWithRealCheckIds`,
`Map_UncataloguedCheckId_ShowsEngineeringGapLabel`, `Map_NoFailFindings_MarkedPagePngsHasPageOne`);
`Components/VerdictResultRenderTests.cs` (bUnit) —
`VerdictResult_RendersAllFourCannedSignals_WithoutThrowing`,
`VerdictResult_RendersLiveMappedRedCase_ShowsRibbonCounts`,
`VerdictResult_InsufficientDataFindings_RenderGreyNotHidden`.

**VERIFY FIRST:** bUnit is **not currently used anywhere in this repository** (confirmed: no
`bunit` package reference exists in `Directory.Packages.props` or any `.csproj`). This repo's
xUnit v3 + Microsoft.Testing.Platform combination is explicitly documented in CLAUDE.md as
"version-coupled and fragile." Before writing the bUnit tests, the implementing agent must: (1)
add `bunit` to `Directory.Packages.props` at a version compatible with `xunit.v3.mtp-v2` 3.2.2 +
MTP 2.1.0 (check bUnit's own release notes for xUnit v3 support — as of early 2026 bUnit's xUnit v3
integration is newer and may need a specific adapter package), (2) prove it with a trivial
render-a-`<p>` smoke test **before** writing the real `VerdictResult` tests, so a version-mismatch
failure is diagnosed against a 5-line test, not the full component. If bUnit + this repo's MTP
stack proves incompatible, fall back to testing `VerdictResult.razor`'s logic by extracting a plain
C# view-model-building method (no bUnit needed) and only smoke-testing the render via manual
`dotnet run` + browser, documenting that deviation.

**Verify from ground truth:** build + `dotnet exec` the test project; manually run the app
(`dotnet run` against the Web.UI project) and navigate to a throwaway route rendering
`VerdictResult` with each of the 4 canned cases to eyeball it before wiring the full live page in
VLD-S5.

---

### VLD-S5: The live page — fixture cards, narration, hero/rail, LIVE/DEMO badge

**Goal:** the actual page the audience sees. Fixture cards for the 4 known demo PDFs (plus a
generic upload slot), a click triggers a narrated ~5–10 s stage progression while the real pipeline
runs in the background, then the `VerdictResult` component (VLD-S4) renders the outcome with a
`LIVE`/`DEMO DATA` badge.

**Acceptance Criteria:**
- **Given** a new page `Components/Pages/LiveVerification.razor` at route e.g. `/live-verification`
  **When** it loads **Then** it shows 4 fixture cards (`good.pdf`, `bad-math-cl21.pdf`,
  `bad-font-cl35.pdf`, `scanned.pdf` — file names and known-true verdicts per
  `VecChecklistDemoE2ETests.DemoFixtures`: good.pdf→RED, bad-math-cl21.pdf→RED, bad-font-cl35.pdf→
  RED, scanned.pdf→ExtractionGap) each showing a "not yet analyzed" ghost chip, plus an upload
  drop-zone for a custom PDF.
- **And** the Run button/card-click is **disabled** until the `PipelineWarmupHostedService`'s
  "ready" flag is set (per the global constraint's baseline-ready-flag trap) — show a "warming up…"
  chip in the meantime.
- **And** clicking a card (or submitting an upload) first runs the PDF-size guard (global
  constraint #4) synchronously — oversized files show an immediate MudBlazor `Snackbar` error and
  never reach `IDemoRunner`.
- **And** on a valid submission, the page shows a sequence of narrated stage labels (e.g.
  "Leyendo documento…" → "Extrayendo campos…" → "Vinculando expediente…" → "Ejecutando validación…"
  → "Generando veredicto…") for a minimum visible duration (design target ~5–10 s total — pad with
  a short artificial delay per stage if the real pipeline returns faster, so the narration doesn't
  flash by illegibly fast; do NOT pad if the real pipeline is *slower* than 10 s — never lie about
  elapsed time, just show real progress) while `IDemoRunner.RunAsync` runs in the background via
  `IServiceScopeFactory` from within the component (per the global constraint's scope-per-submission
  rule — resolve `IDemoRunner` itself from a scope created in the component, not injected directly,
  OR make `IDemoRunner` scoped and rely on the circuit's per-render scope semantics carefully — pick
  whichever is simpler to get right and justify the choice in a code comment, since Blazor Server's
  component DI scope lifetime rules are exactly the trap called out in the global constraints).
- **And** every `await` in this component's stage-progression / result-rendering chain omits
  `ConfigureAwait(false)` (per the global constraint's circuit-crash lesson) — call it out in a
  code comment at the top of the component so a future editor doesn't "fix" it.
- **And** on completion, `VerdictResult.razor` (VLD-S4) renders the outcome, with a
  `MudChip`/badge reading `LIVE` (green) when `DemoRunOutcome.IsLive == true` or `DEMO DATA`
  (grey/amber) when `false` — always visible, never silently omitted.
- **And** when `IDemoRunner` returns a failed `Result` (fallback disabled + live failure) the page
  shows an error banner (MudAlert, severity Error) with the failure message — **not** an unhandled
  exception / blank circuit crash. This is the scenario the bUnit test
  `VerdictResult_or_LiveVerificationPage_FailureResult_ShowsErrorBanner` must cover (place it
  wherever it naturally fits — on the page component if `VerdictResult` itself never receives a
  failure Result, i.e. the page is the one branching on success/failure).

**Files to touch/create:**
- New: `.../ExxerCube.Prisma.Veriqan.Web.UI/Components/Pages/LiveVerification.razor` (+ code-behind
  if preferred).
- Edit: `.../ExxerCube.Prisma.Veriqan.Web.UI/Components/Layout/NavMenu.razor` (add a nav link to the
  new page — do not remove old links yet, that is VLD-S6).

**Tests** (VLD-S2 test project):
`Components/LiveVerificationPageTests.cs` (bUnit, subject to the same VERIFY FIRST as VLD-S4) —
`LiveVerificationPage_RunDisabled_UntilWarmupReady`,
`LiveVerificationPage_OversizedUpload_ShowsSnackbar_NeverCallsRunner`,
`LiveVerificationPage_RunnerReturnsFailure_ShowsErrorBanner_NoException`,
`LiveVerificationPage_RunnerReturnsCannedFallback_ShowsDemoDataBadge`,
`LiveVerificationPage_RunnerReturnsLiveOutcome_ShowsLiveBadge`.

**Verify from ground truth:** `dotnet build` 0/0; run the new test project via `dotnet exec`;
**manually run the app** (`dotnet run`, or via `docker compose` once VLD-S7 lands) and click through
all 4 fixture cards for real — confirm `good.pdf` really shows RED with a LAW-SEC-PRESENCE finding
on the rail and a red-boxed hero image, confirm `scanned.pdf` really shows ExtractionGap with the
"couldn't read this document" framing, not a crash.

---

## Epic D — Cleanup & Deployment

### VLD-S6: Retire the 4 static verdict pages + Overview/Upload

**Goal:** finish the "collapse 4 pages into ONE templated view" design decision by removing the
now-redundant static pages and pointing navigation at the new live page as the primary experience.

**Acceptance Criteria:**
- **Given** `LiveVerification.razor` (VLD-S5) is working end-to-end **When** this story lands
  **Then** `GreenCase.razor`, `YellowCase.razor`, `RedCase.razor`, `BlockedCase.razor` are deleted
  (their content is now reachable by running the corresponding fixture through
  `LiveVerification.razor`, or via the canned fallback badge — nothing is lost, it's the same
  `VerdictResult` component either way).
  **VERIFY FIRST:** confirm with the actual demo audience/owner expectation (check
  `docs/planning-artifacts/` for any prior demo-script docs referencing these exact routes by
  name, e.g. `veriqan-demo-plan` memory) whether any external bookmark/script depends on the old
  route names (`/green`, `/red`, `/yellow`, `/blocked`) before deleting — if so, keep thin redirect
  routes (`@page "/red"` that immediately navigates to `/live-verification?fixture=bad-math-cl21`
  or similar) instead of a hard 404.
- **And** `Overview.razor` and `Upload.razor` are either merged into `LiveVerification.razor`
  (the new page already covers "upload" as a drop-zone) or updated to link into it — do not leave
  orphaned pages describing a flow that no longer matches reality.
- **And** `NavMenu.razor` is updated to reflect the single live-page-centric flow (keep
  `/disposition` — out of scope for this epic, untouched).
- **And** `DemoDataService` itself is **NOT deleted** — it remains the fallback data source
  consumed by `IDemoRunner` (VLD-S2).

**Files to touch/create:**
- Delete: `Components/Pages/{GreenCase,YellowCase,RedCase,BlockedCase}.razor` (or convert to thin
  redirects per the VERIFY FIRST above).
- Edit or delete: `Components/Pages/{Overview,Upload}.razor`.
- Edit: `Components/Layout/NavMenu.razor`.

**Tests:** update/remove any test that referenced the deleted routes (there shouldn't be any yet,
since this project had zero tests before VLD-S2 — but check the new test project for route-name
assumptions introduced in VLD-S4/S5).

**Verify from ground truth:** `dotnet build` 0/0; run the app, click through the nav — confirm no
dead links, confirm `/disposition` still works unchanged (out of scope, must not regress).

---

### VLD-S7: Docker Compose deployment

**Goal:** ship the Web.UI as its own container in the Veriqan stack, per the design memo's explicit
instruction.

**Acceptance Criteria:**
- **Given** a new Dockerfile mirroring `Veriqan.Worker/Dockerfile`'s structure (repo-root build
  context, multi-stage SDK→aspnet, `ArtifactsBaseDir` override) **When** built **Then** it produces
  a runnable container exposing the Web.UI on port 8080 internally.
- **And** `docker-compose.veriqan.yml` gains a `veriqan-web-ui` service: builds from the new
  Dockerfile, `depends_on: sqlserver: condition: service_healthy` (per the design memo's explicit
  instruction — even if VLD-S1's default is in-memory persistence, keep this ordering dependency so
  the service is available if/when the persistence decision changes), maps host port **18091** to
  container 8080 (distinct from the worker's 18090), sets `Veriqan:CsvReferenceData:RootDirectory`
  to the same bind-mounted reference-bundle path the worker uses OR the demo-fixture bundle path
  (decide per VLD-S1's resolution and document it), and a healthcheck hitting `/health/ready` (add
  this endpoint to the Web.UI's `Program.cs` alongside the existing `/health` — mirror the
  worker's `/health/live` + `/health/ready` + `/health` triad if time allows, or at minimum
  `/health/ready` reading the `PipelineWarmupHostedService` ready flag so the container genuinely
  isn't marked healthy until the pipeline has warmed up).
- **And** the compose file's project name stays distinct (`-p veriqan`) per the existing
  multi-stack convention documented in the `prisma-veriqan-staging-e2e` memory (never let this
  collide with the Prisma stack's `-p prisma`).
- **And** `docker compose -f docker-compose.veriqan.yml -p veriqan up --build` brings up all 3
  services (`sqlserver`, `veriqan-worker`, `veriqan-web-ui`) healthy, and `curl
  http://localhost:18091/health/ready` returns 200 once warm-up completes.

**Files to touch/create:**
- New: `Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI/Dockerfile`.
- Edit: `docker-compose.veriqan.yml` (new service block).
- Edit: `Program.cs` (add `/health/ready` mapped to the warm-up readiness flag).

**Tests:** no new automated test required beyond what VLD-S2's warm-up tests already cover for the
readiness-flag logic itself; this story's verification is operational.

**Verify from ground truth:** actually run
`docker compose -f docker-compose.veriqan.yml -p veriqan up --build` (requires `.env.veriqan` per
the existing worker's setup instructions at the top of that compose file) and hit
`/health/ready` for real; do not mark this done from reading the compose YAML alone.

---

## VERIFY-FIRST flags (collected, for quick scanning by the implementing agent)

1. **VLD-S1:** whether the deployed Web.UI should share `ConnectionStrings:VeriqanDb` with the
   worker (durable audit trail) or stay in-memory (isolated demo surface) — open decision, default
   recommendation is in-memory.
2. **VLD-S4/VLD-S5:** bUnit is unused anywhere in this repo; its compatibility with the pinned
   xUnit v3 `mtp-v2` 3.2.2 + Microsoft.Testing.Platform 2.1.0 combination is **unverified** — prove
   it with a trivial smoke test before investing in real component tests, and have a non-bUnit
   fallback plan ready (extract testable view-model logic, manual browser verification for render
   correctness).
3. **VLD-P1:** whether any of the 11 real visual rules produce only page-level hints (not tight
   boxes) is unknown until audited — this could weaken the "see the exact problem" pitch for
   specific fixtures and must be reflected honestly in the rail copy, not glossed over.
4. **VLD-P2:** the `IsVisual` classification for the `[N VISUAL][M DATA]` ribbon is a
   demo-narrative construct with no engine-level source of truth — the seed classification given
   in this doc (11 Visual-assembly rules + CL-37) should be sanity-checked with the owner before
   it's treated as settled.
5. **VLD-S6:** whether external demo scripts/bookmarks depend on the exact old route names
   (`/green`, `/red`, `/yellow`, `/blocked`) — check before hard-deleting vs. redirecting.
6. **VLD-S7:** whether the Web.UI container should point at the demo-fixture reference bundle
   (`Prisma/Fixtures/PRP2/demo/reference-bundle`) or the worker's production-shaped bundle
   (`Prisma/Data/Veriqan/reference-bundles`) when deployed — these currently hold similar but not
   necessarily identical `Demo_Bank_(Iqubica)` data; confirm they're either identical or that the
   choice is deliberate.
