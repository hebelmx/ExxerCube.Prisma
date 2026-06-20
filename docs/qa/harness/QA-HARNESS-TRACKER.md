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
**2026-06-20 START.** Architecture ratified. Tracker + tasks #1–#11 created. Beginning Chunk 0 (scaffolding) delegated to dev subagent; verify build from ground truth before Chunk 1. Nothing committed yet.
