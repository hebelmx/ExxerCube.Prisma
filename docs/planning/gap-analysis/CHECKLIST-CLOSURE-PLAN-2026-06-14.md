# Client Checklist Closure Plan — Ordered Work Plan (2026-06-14)

**Purpose:** close every gap between the client demo checklist (`docs/legal/samples/Checklist+2+Demo+AA.xlsx`,
the "Atención a Autoridades" 7-step demo) and the product, so the full 7 steps run end-to-end.
**This doc is the tracker.** A fresh orchestrator should read this + the gap matrix, then run the loop below.

- **Gap analysis (evidence, file:line):** `docs/planning/gap-analysis/CLIENT-CHECKLIST-VS-PRODUCT-2026-06-14.md`
- **Branch:** `Kt2` (never commit to `main`).
- **GitHub tracking issues (one per gap, label `client-checklist`):**

| Item | Checklist | Issue | Verdict today | Size |
|------|-----------|-------|---------------|------|
| A | Step 6 — Excel "Datos Carga de Oficio" layout produced + wired into Stage 5 | **#7** | ✅ **DONE** (cdb9d4f + hardening b6abdc6) — closed | L |
| B | Step 3 — Manifest reconciliation (missing + extra vs expected Listado) | **#8** | ✅ **DONE** (db65f52 range + b6abdc6) — closed | M |
| C | Step 5 — Surface cross-validation mismatch as an alertamiento | **#9** | ✅ **DONE** (395fb3c range) — closed; dashboard value-hydration deferred | M |
| D | Step 4 — Field-extraction completeness (XML Domicilio/NumeroOficio, Descripción, Docx requerimiento regex, Word signature OCR) | **#10** | ✅ **DONE** (D1 f7c808e + D2 c13584a) — closed | M (image-OCR: L) |
| E | Step 7 — Per-category sub-answer extraction | **#11** | ✅ **DONE** (E1 f120e20 + E2 4768c3f) — closed | L–XL |
| F | Step 2 — Explicit per-cycle downloaded-file list report | **#12** | ✅ **DONE** (folded into B, db65f52 range) — closed | S |
| G | Cross-cutting — Mexican-holiday business-day calendar (conclusion date) | **#13** | ✅ **DONE** (56bc68e + wiring fix 6493d37) — closed | S–M |

> **✅ ALL 7 ITEMS COMPLETE — issues #7–#13 closed (branch Kt2, 2026-06-15).** Every item ITDD-tested and
> ground-truth verified by the orchestrator (per-project build 0/0; architecture 22/22 throughout; targeted
> `dotnet test` green). Two adversarial-review gates (plan-completion-reviewer) ran at the Phase 1 and Phase 2/3
> boundaries and BOTH caught real, ground-truth-confirmed defects that were fixed: Phase 1 → 2 arch-guardrail
> Blockers + Orion storage wiring (`b6abdc6`); Phase 2/3 → Athena Worker fusion calculator not injected =
> weekend-only in production (`6493d37`). Phase 4 capstone (`24252e9`): a deterministic 7-step demo test over the
> real sample corpus (`docs/legal/samples/*`) GREEN, plus the live `MaxFidelityGateE2ETests` extended to assert
> the Datos-Carga Excel alongside SIRO XML on the real 3-process pipeline (builds; runs on a Docker+Playwright+
> Tesseract host).
>
> **Owner decisions applied:** A=template-repo driven; B=config/file Listado; C=annotate+route to review;
> D=include Word-image OCR; G=PublicHoliday `MexicoPublicHoliday` adapter; E=hybrid regex + Ollama.
>
> **Per-item design notes:** `ITEM-{A,BF,C,D,E,G}-*.md` in this folder.
>
> **Known carry-overs / deferred (logged, non-blocking):**
> - (C) persist `UnifiedMetadataRecord` to `IUnifiedMetadataStore` so the dashboard hydrates per-source conflict
>   *values* (today the case is flagged `FieldMismatch` + alerts live on the record).
> - (A) classifier-driven Tipo de asunto — `ComplianceActions` aren't available at Stage 5, so it uses the
>   documented `TieneAseguramiento`/`AreaDescripcion` heuristic (itself a `Requirements.md` signal).
> - (E) Ollama free-text enrichment is `Enabled=false` by default + Docker-gated integration test (not run in CI);
>   structured sub-answers run everywhere.
> - (Minor, accepted) the `Infrastructure_Projects_Should_Not_Depend_On_Each_Other` arch test doesn't enumerate the
>   new `Infrastructure.Calendar` shared-kernel (leaf, no cycle); adding it would false-fail the rule.
> - Pre-existing, UNRELATED red: `IngestionHubWireTests.Broadcast_DocumentDownloadedEvent_IsReceivedByConnectedClient`
>   (SignalR timing; fails identically on clean HEAD — not introduced by this work).

---

## How to run this (orchestrator loop)

Use the `bmad-orchestrator` skill. **Prime directive: verify every chunk from ground truth** (build + targeted
`dotnet test` + `git diff`), never trust a subagent's summary. One iteration per item:

1. **Re-ground:** read this tracker + the gap matrix + the item's GitHub issue. Pick the next actionable item by the sequencing below.
2. **Settle the decision gate first** (each item below lists one). These are owner/design forks — use `AskUserQuestion` with a recommended option BEFORE delegating. Do not silently resolve.
3. **Delegate** the implementation to an isolated `bmad-agent-dev` / `dev` subagent (test design to `qa`). Tight brief: the item's DoD, the start-from file:line, hard constraints below, the test project to turn green.
4. **Verify from ground truth:** build the touched project(s), run the relevant test project(s), inspect `git diff`. Red → re-delegate with the failure or fix the small gap yourself; do not advance.
5. **Commit** code+tests in a self-contained chunk (`feat(#N): …` / `test(#N): …`), **separately from docs**. Reference the issue. **Push `Kt2`.**
6. **Record:** update this tracker (mark item done with commit refs), comment/close the GitHub issue, update memory if a non-obvious fact emerged.
7. **Adversarial review** every ~3 items or at the phase boundary: fan out `bmad-review-adversarial-general` / `plan-completion-reviewer` to refute the work *against this plan + the gap matrix*. Triage findings back into the tracker.

### Hard constraints (non-negotiable)
- **ITDD per ADR-005**; `Result<T>` + `CancellationToken` on every async method; xUnit v3 + Shouldly + NSubstitute (no Moq/FluentAssertions).
- Tests use `TestContext.Current.CancellationToken`. MTP filter-query needs 4 segments `/Assembly/Namespace/Class/Method`.
- Single-project builds where possible (200+ projects). Build must stay **0 errors / 0 warnings** (TreatWarningsAsErrors).
- Never pipe `dotnet test` through `tail`. Background Stryker + `StrykerCompat=true` flatten obj/ref → use fresh `--no-incremental` if stale test failures appear.
- Commit code+tests SEPARATELY from docs. Push `Kt2`, never `main`.

---

## Sequencing (dependency- and demo-ordered)

**Phase 1 — demo-critical client-visible surfaces (do first):**
1. **A (#7)** Excel layout + Stage-5 wiring — the headline deliverable.
2. **B (#8)** + **F (#12)** Manifest reconciliation (missing+extra) with the downloaded-list report folded in.
3. **C (#9)** Surface the mismatch alertamiento.

**Phase 2 — extraction depth + accuracy:**
4. **D (#10)** Field completeness (XML Domicilio/NumeroOficio, Descripción compose, Docx requerimiento regex; Word signature OCR as a gated stretch). Feeds richer values back into A.
5. **G (#13)** Holiday-aware conclusion date — fast-follow to A.

**Phase 3 — interpretation (largest, engine decision required):**
6. **E (#11)** Per-category sub-answers — gate on the regex-vs-LLM-vs-hybrid fork before any code.

**Phase 4 — gate:** a single demo E2E that walks all 7 checklist steps over the real sample corpus
(`docs/legal/samples/*.{xml,docx,pdf}`) — extends the existing `MaxFidelityGateE2ETests` rather than replacing it.

Phases 1, 2, 3 are largely independent and can interleave; within Phase 1, A is the priority.

---

## Per-item detail

### A — Step 6: Excel "Datos Carga de Oficio" layout (#7) · [L] · Phase 1
- **DoD:** the ~24-column layout (extracted + fixed + calculated + Tipo de asunto + Subdivisión) is produced as Excel and **invoked in Stage 5** alongside SIRO XML, artifact written to shared storage via `IStoragePathResolver`. Unit test pins columns/fixed/calculated; integration test proves Stage 5 emits it.
- **Start from:** `Infrastructure.Export/ExcelLayoutGenerator.cs:79-90` (rewrite columns); `ReconciliationOrchestrator.cs:262-290` (wire Stage 5). Consider existing `ITemplateRepository`/`ITemplateFieldMapper`.
- **Decision gate:** template-repo vs hardcoded constants; are the abogado/despacho/etc. fixed values demo placeholders → config-driven? (owner)

### B — Step 3: Manifest reconciliation (#8) · [M] · Phase 1
- **DoD:** load expected "Listado" (base name + 3 expected extensions/oficio); produce a report of **MISSING + EXTRA(sobra) + partial**; surface it (log + persisted/queryable). Test covers all three buckets.
- **Start from:** `Orion.Ingestion/IngestionOrchestrator.cs`, `SiaraCaseGrouping.cs`, `FileIngestionJournal.cs`, `DocumentDownloadedEvent.CaseFiles`.
- **Decision gate:** production source of the expected Listado (file import / config / SIARA-derived). (owner)

### C — Step 5: Surface mismatch alertamiento (#9) · [M] · Phase 1
- **DoD:** expose computed conflicts (e.g. `UnifiedMetadataRecord.Alerts[]`: field + values + sources); flag the case in the review dashboard. Test: mismatch → alert; match → none.
- **Start from:** `FusionExpedienteService` (`ConflictingFields` ~79/330); Application `FieldMatchingService` (~215-268); review-case path (`ReconciliationOrchestrator.PersistReviewCaseAsync`, GH #6).
- **Decision gate:** does a mismatch force manual review or just annotate? (owner)

### D — Step 4: Field-extraction completeness (#10) · [M] (image-OCR: L) · Phase 2
- **DoD:** `XmlFieldExtractor` reads `<Domicilio>` + `<Cnbv_NumeroOficio>` and composes "Descripción"; requerimiento id (`AGAFADAFSON2/2025/000083`) extracted; **stretch:** OCR embedded .docx images for the remitente. Per-field tests over the real corpus.
- **Start from:** `XmlFieldExtractor.cs:40-57`; `DocxFieldExtractor.cs:122-192`.
- **Decision gate:** is Word signature-image OCR in demo scope or deferred? (owner)

### E — Step 7: Per-category sub-answers (#11) · [L–XL] · Phase 3
- **DoD:** populate the detail fields for all 5 categories (accounts/products/amounts/original-block ids/doc sub-types/transfer details/info text). Per-category tests over the real corpus.
- **Start from:** `SemanticAnalyzerService.cs:143,168,193,218,243`; the requirement value objects; reuse `LegalDirectiveClassifierService.ExtractActionDetails` (~417-502).
- **Decision gate (KEY):** extraction engine — regex-only vs activate LLM (Ollama, dormant by design) vs hybrid; and demo scope (category-level vs full sub-answers). (owner REQUIRED)

### F — Step 2: Downloaded-file list report (#12) · [S] · Phase 1 (with B)
- **DoD:** per-cycle/per-case report of downloaded files (name+ext+format) at a client-visible surface. Test proves enumeration. Fold into B.

### G — Step 7… no, cross-cutting: Holiday-aware business days (#13) · [S–M] · Phase 2
- **DoD:** holiday-aware Mexican working-day calculator replaces weekend-only logic in BOTH `CalculateBusinessDays` sites; configurable holiday set; tests pin holiday-spanning deadlines.
- **Start from:** `FusionExpedienteService.cs:~2778`, `SLAEnforcerService.cs:~453`.
- **Decision gate:** holiday-set source — config file vs calendar service. (recommend config file)

---

## Where the product already EXCEEDS the checklist (do not rebuild)
Case-package ingestion (one event/oficio) + SHA-256 dedup + best-effort partial + auto-route incomplete → review dashboard; 3-source fusion w/ confidence/weighted-voting; full SIARA auth seam (3 modes) + 3-process security spine (JWT clearance + per-process audit); real SIRO XML export. See gap matrix §"more advanced".

## Definition of overall done
All 7 checklist steps demonstrable end-to-end over the real sample corpus, each tracked issue (#7–#13) closed with commit refs, build 0/0, suites green, and a demo E2E (extending `MaxFidelityGateE2ETests`) that walks the 7 steps with no stub on the client-visible path.
