# Handoff — Dual Ground-Truth Documentation/Code Reconciliation

**Prepared:** 2026-06-07 (end of the repo-housekeeping + dependency-modernization session)
**For:** the next agent
**Branch:** `Kt2` (build green 0/0, 0 vulnerabilities)

---

## 1. Mission

Reconcile the **documentation** with the **current state of the code**, while
**preserving the intended architectural and functional targets**. Maintain a
*dual ground truth*:

- **Ground Truth 1 — Current implementation:** what the code actually does today.
- **Ground Truth 2 — Intended state:** what the requirements, architecture, and
  project objectives say the system should eventually become.

The goal is **NOT** to make it look finished. It is to leave an **accurate** picture
of where things stand, keep the intended direction intact, and hand the *next* agent a
clear path to a fully functional, production-ready solution.

### What success looks like
1. Gaps between **documentation ↔ implemented code** identified.
2. Gaps between **implemented code ↔ intended design** identified.
3. **Lessons learned** captured in the docs.
4. Work clearly classified as **Done / Partial / Planned**.
5. An **actionable handoff**: priorities, known limitations, technical debt,
   unresolved issues, recommended next steps.

### Guardrails (read these carefully)
- **Keep both ground truths.** Do not overwrite intent with current state — annotate.
  When code falls short of a documented target, record the target AND the actual state;
  do not delete or downgrade the target.
- **Do not assert the project is done.** No "production-ready ✅" unless verified end to end.
- **Be truthful and specific.** Prefer "Partial: X works, Y is a stub" over vague claims.
- **Do not weaken tests or requirements** to make reality look closer to intent.
- This is primarily a **documentation/analysis** task. If you fix code, fix small,
  obvious, verified things; otherwise record them as gaps for a later coding pass.

---

## 2. The overall objective (Ground Truth 2 anchor)

**ExxerCube.Prisma — Regulatory Compliance Automation System.** A C#/.NET 10 + Python
system that OCR-processes Spanish legal/banking documents (CNBV / SIARA "requerimientos"),
extracts mandatory fields, reconciles/fuses multi-source data, classifies, and exports
compliance artifacts. Hexagonal/Clean architecture; Python ML/OCR via CSnakes;
Railway-Oriented Programming (`Result<T>`).

**Documented pipeline (5 stages):** Quality Analysis → OCR → Fusion/Reconciliation →
Classification → Export. *(Verify each stage's real status — see §5.)*

### Where the intended state lives (GT2 sources)
- `docs/product/requirements/prd.md` — Brownfield Enhancement PRD (**v1.0, "Draft", dated 2025-01-12** — may lag current intent; confirm).
- `docs/product/requirements/` — `Requirements.md`, `FusionRequirment.md`, `QA-Requirements.md`, `architecture.md`, `ui-*` specs.
- `docs/product/epics/` — `epic-1-regulatory-compliance-automation-system.md` + Missions 1–7 (HappyPath, DegradedPDF, ManualReview, ExportCompliance, Observability, SchemaDrift, AdaptiveExtractor) + SystemTesting.
- `docs/legal/requirements/` — MandatoryFields, ClassificationRules, SmartEnum types (the regulatory source of truth).
- `docs/architecture/` — ADRs (001–009), system-design (data model, entity catalog, flow), Python/CSnakes design.
- `docs/planning/missions/` — Mission analyses + `PRODUCTION_READY_SUMMARY.md` (**likely over-claims — reconcile against reality**).

---

## 3. Where the current state lives (GT1 sources)

- **Solution:** `Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln` (200+ projects), numbered layers
  `01 Core` (Domain+Application) · `02 Infrastructure` · `03 Orchestration` · `04 Services`
  (Orion ingestion, Athena processing, Sentinel monitoring) · `07 UI` (Blazor/MudBlazor) ·
  `08 Tests` · `09 Testing`.
- **The authoritative "what's actually wired" source:** the DI composition roots —
  `07 UI/UI/ExxerCube.Prisma.Web.UI/Program.cs` and the Worker/service DI extensions.
  Read these to see which implementations are registered (real vs `Stub*`).
- **Python:** `Prisma/Code/Src/Python/` (prisma-ocr-pipeline, prisma-ai-extractors,
  `Prisma-dumy-generator-AAA` = document generator) and the build-vendored
  `Prisma/Code/Src/CSharp/Python/ocr_modules` (see that folder's README).
- **Doc claims to reconcile:** `CLAUDE.md` (esp. its "Release Status" — already flagged
  **unverified since dormancy**) and the consolidated `docs/` tree (`docs/README.md`).

### How to establish GT1 rigorously
- Build: `dotnet build "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"` → currently 0/0.
- Tests run via Microsoft.Testing.Platform; a root `global.json` opts `dotnet test` into MTP.
  Unit suites verified green this session: Tests.Domain 337, Tests.Application 157,
  Tests.Architecture 19. **Integration/system/E2E suites were NOT run** (need Docker/SQL/
  Ollama/Playwright) — their true status is **unknown**; establish it.
- Trace each of the 5 pipeline stages to its production implementation; mark real / stub / placeholder.
- Enumerate and classify debt markers in production code (current counts, non-test):
  **4 `class Stub`, 18 "placeholder", 27 `// TODO`** (0 `NotImplementedException`). Read each.

---

## 4. Verified current state (as of 2026-06-07)

- ✅ Solution builds: **0 errors / 0 warnings**; `dotnet list package --vulnerable` → **none**.
- ✅ Unit suites green (Domain/Application/Architecture). ⚠️ Integration/E2E unverified.
- ✅ Dependencies on latest stable, except documented holds: **MudBlazor 8** (9 = major UI
  migration), **SixLabors.ImageSharp 3** (4 = paid license), **Emgu.CV 4.12**,
  **Testcontainers 4.9**, **BouncyCastle 2.7.0-beta**. Testing platform modernized to
  xunit `mtp-v2` + MTP 2.1.0 (see `CLAUDE.md` "Testing stack" — it's version-fragile).
- ✅ Repo hygiene done (see `docs/development/archive/root-cleanup-2026-06.md`).

---

## 5. Seed findings — known gaps (start here, then verify & expand)

These are leads gathered while doing the housekeeping; **the next agent must verify each**,
then build the full gap matrix.

### Code ↔ intended-design gaps (GT1 < GT2)
- **CSnakes Python ML interop is a documented PLACEHOLDER** (`CLAUDE.md`). `PrismaPythonEnvironment`
  uses `FromRedistributable`; `ocr_modules` is needed mainly at codegen. **Whether real
  runtime OCR-via-Python is wired is unconfirmed** — this is potentially the biggest gap,
  since OCR is a core pipeline stage. Verify against the OCR stage's intent.
- **9 domain interfaces have no implementation in the scanned assemblies** — currently
  *allowlisted* in `Tests.Architecture` (see `docs/qa/reports/test-debt-2026-06.md`):
  `IHealthCheckService`, `IDashboardService`, `IDocumentDownloader` (only `StubDocumentDownloader`),
  `IIdentityProvider`, `IIngestionJournal`, `ITokenService`, `IUserContextAccessor`,
  `IEventHandler<T>` (legacy), `IFieldMatchingService` (lives in Application). Some impls may
  live in `Orion`/`Athena`/`Auth` (not scanned) — **confirm which are real vs stub vs missing.**
  This list is effectively a ready-made code↔design gap inventory.
- **Worker `/dashboard` endpoints** (`OrionDashboardService`, `AthenaDashboardService`) are
  stubs returning zeros. (The UI `Dashboard.razor` uses a different, working
  `IProcessingMetricsService` — don't conflate them.)
- **`PersonIdentityResolver` DB persistence** deferred to v1.1 (`CLAUDE.md`).
- **3 skipped TXT-extractor edge cases.**
- **Field-extraction coverage**: git history mentions "5% → 46–61%" — verify current real coverage vs the mandatory-fields requirement.
- The 4 stubs / 18 placeholders / 27 TODOs above — classify each as intended-gap vs cleanup.

### Documentation ↔ code gaps
- `CLAUDE.md` **"Release Status"** is explicitly unverified-since-dormancy (last asserted 2026-02-18). Re-audit and rewrite as Done/Partial/Planned.
- `docs/planning/missions/PRODUCTION_READY_SUMMARY.md` and the Mission READMEs likely **over-claim** completion — reconcile.
- The PRD is **v1.0 "Draft" (2025-01-12)** — confirm it still represents intended scope; note divergence from what's been built since.
- Verify the documented **5-stage pipeline** matches the real orchestrator (`ProcessingOrchestrator`) and that the Rx.NET event architecture description in `CLAUDE.md` is current.

---

## 6. Recommended deliverables for this assignment

1. **A gap matrix** (the core artifact). Suggested location `docs/planning/gap-analysis/`.
   Columns: *Capability/Feature · Documented intent (GT2, with source link) · Actual state
   (GT1, with code link) · Gap · Status {Done|Partial|Planned} · Priority · Notes*.
   Cover every pipeline stage, every service (Orion/Athena/Sentinel), the UI, auth, observability,
   export/compliance, and the Python interop.
2. **Rewrite `CLAUDE.md` "Release Status"** to verified reality (Done/Partial/Planned),
   **keeping the targets** as targets.
3. **A path-to-production roadmap** (`docs/planning/`): prioritized next steps, known
   limitations, technical debt, unresolved issues — sequenced toward a working product.
4. **Consolidate lessons learned** (`docs/development/lessons-learned/`), including this
   session's: package transitive-pinning cascade, the xunit↔MTP ABI lockstep, repo-hygiene
   anti-archaeology practice, native-binding (Emgu) and licensing (ImageSharp) upgrade traps.
5. Mark aspirational/Mission docs as **targets**, not achievements, where they over-claim.

### Suggested priority order (the agent should validate/re-rank)
1. Establish true test status — run the integration/system/E2E suites; know real green.
2. Verify the 5-stage pipeline end-to-end and the **CSnakes OCR real wiring** (core).
3. Close the v1.1 interface gaps that block real operation: `IDocumentDownloader`
   (ingestion), identity/auth (`IIdentityProvider`/`ITokenService`/`IUserContextAccessor`),
   health/dashboards/observability.
4. Decide the held UI upgrade (MudBlazor 9) and ImageSharp licensing.
5. Then: harden the architecture test (replace the §5 allowlist by scanning Orion/Athena/Auth).

---

## 7. Key pointers
- Repo conventions & current status: `CLAUDE.md` (read first).
- This session's record: `docs/development/archive/root-cleanup-2026-06.md`.
- Test-debt detail + the allowlist rationale: `docs/qa/reports/test-debt-2026-06.md`.
- Docs index: `docs/README.md`.
- Intended state: `docs/product/`, `docs/legal/requirements/`, `docs/architecture/`.
- "What's wired" truth: the DI composition roots (Web.UI `Program.cs` + Worker DI).

> Reminder: the deliverable is an *honest map and a path*, not a finished product.
> Preserve the destination; describe the terrain accurately; mark the route.
