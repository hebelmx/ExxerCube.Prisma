# QA Harness — Execution Tracker

**Mission:** `docs/TaskQAHarnes.md` — Phase 1 (build the harness) THEN GATE before Phase 2 (independent review).
**Intended-solution doc (anti-drift anchor):** `docs/qa/harness/ARCHITECTURE.md` (ratified 2026-06-20). Every diff is reviewed against THIS, never against subagent prose.
**Branch:** `Liv` · **Mode:** EXECUTION (orchestrator + isolated subagents; verify every result from ground truth).

## Owner rulings (this run)
1. Scope = **Phase 1 then GATE** (do not start Phase 2 review without owner sign-off).
2. Phase-2 PRD baseline (for later) = formal `docs/product/requirements/prd.md` (FR1–FR32 + NFRs). RC5/gap-matrices are FORBIDDEN inputs to the independent review (they are prior assessments).
3. Harness = **substantial first-class `ExxerCube.Prisma.QaHarness`** library + Cli + Tests.
4. Traceability attributes = coverage declarations only (never judgments); seed IDs from real prd.md.
5. Video/HAR opt-in; self-test isolated containers first pass; System.CommandLine decision deferred to Chunk 5.

## Environment ground truth (verified 2026-06-20)
- Docker LIVE 29.5.3 (Testcontainers + browser E2E runnable).
- SIARA corpus `Prisma/Deployments/Siara.Simulator/bulk_generated_documents_all_formats/` **MISSING** → live ingestion serves 0 cases; harness degrades gracefully (CorpusSeeder fallback + documented limitation).
- HEAD = 127a1eab. dotnet 10.0.301. E: drive slow → build single projects, not the .sln.
- Project root: `Prisma/Code/Src/CSharp/`. New work under `09 Testing/02 QaHarness/`.

## Chunk plan (see ARCHITECTURE.md §10)
| # | Task | Status | Parallel group | Verified by |
|---|------|--------|----------------|-------------|
| 0 | Scaffolding (3 csproj + sln) | in_progress | — (seq) | build 0/0 each + sln lists 3 |
| 1 | Core abstractions | pending | — (seq, after 0) | lib build 0/0 |
| 2A | Provisioning impl | pending | 2A‖2B‖2C‖2D | lib 0/0 + CorpusSeeder fast test |
| 2B | Hosting impl | pending | 2A‖2B‖2C‖2D | lib 0/0 + startup JSON round-trip |
| 2C | Evidence + Traceability | pending | 2A‖2B‖2C‖2D | lib 0/0 + Trace/File fast tests |
| 2D | Reporting | pending | 2A‖2B‖2C‖2D | lib 0/0 + Markdown/Json fast tests |
| 3 | Domain validators (3A–3D) | pending | seq after 2x; 3A–3D ‖ | lib 0/0 + 1 fast test/validator |
| 4 | Workflow catalog + runner | pending | seq after 3; 4A–4C ‖ | lib 0/0 + HealthCheck fast test |
| 5 | DI wiring + CLI | pending | seq after 4 | --provision-only exit 0 + JSON |
| 6 | Self-test + representative-workflow proof | pending | seq after 5 | [fast] green + 1 live workflow |
| R | Adversarial review + finalize 4 docs | pending | gate | skeptic pass + docs landed |

## Conflict points
- `.sln` — single edit in Chunk 0 only.
- `QaHarnessServiceCollectionExtensions.cs` — touched by 2A–2D/3/4; each chunk adds a separate `Register*` static; consolidate in Chunk 5.
- `DefaultWorkflowRunner.cs` — Chunk 4, owned by sub-chunk 4A only.
- `.csproj` ProjectReference additions (Chunk 2B) isolated to the QaHarness lib csproj.

## Handoff (update each context clear)
**2026-06-20 — PHASE 1 COMPLETE. GATED before Phase 2 (owner ruling: Phase 1 then gate).** Branch `Liv`, HEAD after docs commit. All chunks 0–6 + remediation done, every result ground-truth-verified (build 0/0 + tests at each step). Commits: 25418d7b (C0) · 55296342 (C1) · 4a4b1bd4 (C2A/2C/2D) · b4358586 (C2B) · ab631f3a (C3) · 777f277d (C4) · 76fe6b1d (review remediation: 4 Major+3 Minor) · 20849f5d (C5 DI+CLI) · e22e9572 (C6 self-test + LIVE proof) · +docs.
- **Built:** first-class `ExxerCube.Prisma.QaHarness` lib + `.Cli` + `.Tests` (3 projects, in .sln). All 7 mission responsibility areas. **116 self-tests green** (114 fast/no-Docker + 2 integration). All builds 0/0 under warnings-as-errors.
- **PROVEN LIVE (Docker):** `HarnessIntegration_ProvisionBootHealthCheck` (~2m45s) — provision SQL container → boot real Web UI → run HealthCheckWorkflow via runner → capture evidence + traceability → write report. Re-run by orchestrator in isolation 2/2. = Success Criterion #2.
- **2 adversarial reviews:** (1) phase-boundary (2 skeptics) → 4 Major + 3 Minor, ALL remediated (76fe6b1d) incl. a REAL bug (CorpusSeeder generator args `--count`→`--num`, dir→file) + HealthEndpointValidator text/plain (would never conform vs real /health) + AuditTrail legacy-null + ThreeProcessHost hardcoded-healthy + workflows throwing OCE. (2) final completeness → COMPLETE WITH GAPS, no Blockers/Majors, 4 Minor doc-drift → all fixed by doc edits.
- **Invariant held:** harness makes NO PASS/FAIL/quality judgment (verified in code + a test asserts the report emits no **PASS**/**FAIL**). Validators report findings + structural IsConformant only.
- **4 Phase-1 deliverables landed under docs/qa/harness/:** ARCHITECTURE.md, CAPABILITY-INVENTORY.md, LIMITATIONS.md, README.md (+ this tracker). Harness Implementation = the code.
- **KEY AS-BUILT FACTS:** Result<T> via IndQuestResults pkg (not in-repo). Playwright 1.60.0 pinned. SIRO ns/Excel-24-headers grounded in real exporters. SIARA corpus still ABSENT → ingestion/export workflows Abort("CorpusAbsent"). Repo-root resolved via --repo-root / PRISMA_REPO_ROOT / walk-up (BuildArtifacts is OUTSIDE the repo tree). xUnit v3 MTP filter = /Asm/Ns/Class/Method (NOT [category=fast]); no --nologo.
- **NEXT = PHASE 2 (owner-gated, NOT started):** independent PRD review against `docs/product/requirements/prd.md` (FR1–FR32 + NFRs). MUST run in FRESH-CONTEXT agent(s) with NO access to prior reports/RC5/gap-matrices/memory/CLAUDE.md-release-status (mission independence rule). Restore the SIARA corpus first to exercise live ingestion/export, else those requirements → NOT TESTED / NEEDS HUMAN REVIEW.
