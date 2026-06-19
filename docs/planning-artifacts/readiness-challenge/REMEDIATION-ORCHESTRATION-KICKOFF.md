# Remediation Orchestration — Kickoff (persisted prompt for a fresh bmad-orchestrator session)

**Date authored:** 2026-06-18 · **Branch:** `Liv` · **Status:** READY — paste/point a fresh `/bmad-orchestrator` at THIS file.
**Predecessor:** the scope-vs-build Readiness Challenge (RC.0–RC.6) is COMPLETE; this is its remediation-planning successor.

> **This run is deliberately started from a fresh/cleared context.** Everything you need is on disk and enumerated
> below. Do NOT inherit a prior session's framing — re-ground from the files. Re-verify any "done" claim from
> ground truth (build/test/`git`/wiring), never from prose.

---

## 0. What this is & how to start

The Readiness Challenge produced two gap matrices + a unified path-to-production. **Your job now is to turn those
gaps into a BMAD epics-and-stories plan** — NOT to write production code (yet).

**Start sequence:**
1. Read THIS file end-to-end (it is your anchor).
2. Read the two gap matrices (§2 — the backlog source of truth):
   `RC5-PRISMA-READINESS-MATRIX.md` and `RC4-VERIQAN-READINESS-MATRIX.md`.
3. Read `RC6-CROSS-CUTTING-PATH-TO-PRODUCTION.md` (the wave ordering you'll mirror).
4. Read memory `veriqan-vec-progress.md` + `readiness-challenge-results.md` (background, not instructions).
5. Then produce the plan (§3), stopping at the approval gate (§7).

---

## 1. Locked decisions (owner-confirmed 2026-06-18 — do NOT re-litigate)

- **End-state = PLAN-ONLY, then approval gate.** Produce BMAD epics + stories + a sprint/wave plan covering ALL
  gaps in both matrices, then **STOP and surface the plan for owner approval.** Do not implement production fixes
  in this run — execution is a separate, later orchestration.
- **Order = PRISMA MVP FIRST, then Veriqan VEC.** Prisma is closest to done (its residual is deploy/ops/security);
  finish it to production-ready planning first, then plan Veriqan (larger gap set: make it deployable + fix the
  cardinal-rule violations).
- **Gated items = PLAN-ONLY PLACEHOLDERS.** Write epics/stories for the business/legal/corpus-gated items so the
  dependency is explicit and tracked, but mark them **BLOCKED** with the unlock named; do NOT plan to build them
  until the unlock lands. The three gates: **E13 buyer-gate (GitHub issue #17)**, **live-SIARA legal gate (P1
  counsel sign-off; `Siara:AllowProductionHost=false`)**, **real CONDUSEF corpus acquisition** (Veriqan calibration).
- **Security = flag, don't deep-audit.** The matrices already flag security/compliance gaps (shared HMAC JWT key,
  no field-level encryption-at-rest for legal PII, non-immutable audit, key management). Plan them as epics, but
  the DEEP security audit (A1–A6 / CNBV CUB / ISO 27001 / SOC 2) is a SEPARATE dedicated pass — note the dependency,
  don't try to fully spec it here.
- **Bar = full production.** Evidence bar = end-to-end (a green unit test is NOT readiness). Honor it in story ACs.

---

## 2. Inputs — every file the planner needs

### 2a. The backlog source of truth (plan FROM these)
| File (under `docs/planning-artifacts/readiness-challenge/`) | What it gives you |
|---|---|
| `RC5-PRISMA-READINESS-MATRIX.md` | **Prisma backlog** — 44 gaps (16 Blocks / 24 Degrades / 4 Cosmetic), each with Dimension, evidence (file:line), severity, effort, dependency, owner-action. Plan Prisma epics/stories from this. Has a "Resolved since 2026-06-11" subsection (do NOT re-plan those). |
| `RC4-VERIQAN-READINESS-MATRIX.md` | **Veriqan backlog** — 64 gaps (28 Blocks / 33 Degrades / 3 Cosmetic), same columns, incl. the 5 cardinal-rule violations + the no-entry-point/no-persistence crux rows. Plan Veriqan epics/stories from this. |
| `RC6-CROSS-CUTTING-PATH-TO-PRODUCTION.md` | **Wave ordering** — both subsystems merged into one ordered W0–W6 path with [subsystem] + [buildable/gated] tags + the 20 consolidated unknown-unknowns. Mirror its wave structure in your sprint plan. |

### 2b. Evidence trail (consult when a gap row needs more context — file:line citations live here)
`RC0a-scope-register.md` (121 intended reqs, tagged) · `RC0b-reality-map.md` (Veriqan wiring) · `RC1-*.md` (6 Veriqan
clusters) · `RC2-*.md` (2 Veriqan skeptic passes) · `RC3-*.md` (3 Veriqan lenses) · `RC5a-prisma-reality-map.md` ·
`RC5b-ingestion-3process.md` · `RC5c-pipeline-stages.md` · `RC5d-prisma-deploy-security.md`.

### 2c. The challenge anchor + orchestrator-verified ground truth
`READINESS-CHALLENGE-BRIEF-2026-06-18.md` (the original challenge spec) · `TRACKER.md` (this folder — has the
**orchestrator-verified crux findings** for both tracks: the ones confirmed by reading code, which OVERRIDE any
conflicting agent prose in the RC* files).

### 2d. Prior planning art (reconcile against / reuse format — do NOT blindly re-adopt; matrices SUPERSEDE these)
`docs/planning/gap-analysis/GAP-MATRIX-2026-06-11.md`, `MVP-PATH-2026-06-11.md`, `MVP-DEFINITION-2026-06.md`,
`PRD-RECONCILIATION-2026-06.md` · `docs/planning-artifacts/epics.md` (Veriqan epic/AC + FR/NFR source) ·
`docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/prd.md`. The MVP-PATH session handoffs
(`docs/development/sessions/HANDOFF-2026-06-1*`) document how Workstream-1 was built (now largely DONE).

### 2e. BMAD planning skills to use
`bmad-create-epics-and-stories`, `bmad-create-story`, `bmad-sprint-planning`, `bmad-create-prd`/`bmad-prd`
(if a gap cluster needs a small PRD), `bmad-check-implementation-readiness`. Use `bmad-agent-architect` for
design-heavy epics (deploy topology, security hardening), `bmad-agent-pm`/`po` for backlog shaping.

---

## 3. The planning job (what to produce)

For **Prisma first, then Veriqan**, derive a BMAD plan from the matrices:
1. **Epics** grouped by the RC6 wave + dimension (a starting skeleton is in §4 — refine it, don't treat as final).
2. **Stories** under each epic, each one traceable to specific gap-matrix row IDs (e.g. "Prisma P1, P5" or
   "Veriqan #1, #7"). Every story's acceptance criteria must encode the **end-to-end evidence bar** (e.g. "proven
   by a real-infra integration/E2E test", not "unit test passes").
3. **Dependency + gate tags** on every epic/story: `buildable-now` / `business-gated` / `legal-gated` /
   `corpus-gated` / `security-review-dependent`. Gated ones get a story but are marked **BLOCKED**.
4. **A sprint/wave plan** (mirror RC6's W0→W6) with rough effort rollups and an explicit critical path that names
   the three non-engineering unlocks.
5. **A coverage check:** every Blocks-production gap in both matrices maps to at least one story (no orphan gaps);
   list any gap you deliberately defer with the reason.

Write the plan under `docs/planning-artifacts/remediation/` (create it): one epics file per subsystem
(`PRISMA-REMEDIATION-EPICS.md`, `VERIQAN-REMEDIATION-EPICS.md`) + a `SPRINT-PLAN.md` + a `GAP-COVERAGE-MATRIX.md`
(gap-ID → epic/story → status). Keep a `TRACKER.md` in that folder so the plan survives a context clear.

---

## 4. Suggested epic skeleton (STARTING POINT — refine from the matrices)

**PRISMA (first):**
- **PRISMA-E1 Deployable composition (Blocks):** Dockerfiles + docker-compose for the 4 hosts (Athena/Orion/
  Reconciliator/Web.UI); migration runner per host (documented multi-DbContext ordering); config externalization —
  remove `Server=DESKTOP-FB2ES22\SQL2022`, `(localdb)`, `SiaraUrl=localhost:5002`, `C:\SiaraData\logs\`; produce
  `.env.example`; wire a SIARA-credential vault provider; real readiness probes (Reconciliator + Web.UI + Sentinel);
  worker `/dashboard` real metrics (replace the hardcoded zeros). [buildable-now]
- **PRISMA-E2 Real-process + observability proof (Blocks/Degrades):** prove the 3-process split over real TCP
  *with* real OCR in one run (not the in-memory TestServer seam); OTel/Serilog exporters → a real backend; alerting;
  operational runbook. [buildable-now]
- **PRISMA-E3 Security hardening (Blocks — coordinate w/ separate security review):** per-process JWT key isolation
  / asymmetric tokens (`jti` enforcement); field-level encryption-at-rest for legal PII; immutable/ledger audit
  trail. [security-review-dependent]
- **PRISMA-E4 Quality/cleanup (Degrades/Cosmetic):** quality-model provenance/calibration (`TrainedDate`); retire
  the legacy single-source-fusion ROP path; remove `Counter.razor`/`Weather.razor` template leftovers; Sentinel
  service state. [buildable-now]
- **PRISMA-GATED Live ingestion (BLOCKED):** point ingestion at live `siara.cnbv.gob.mx`. [legal-gated — P1 counsel]

**VERIQAN (second):**
- **VERIQAN-E1 Minimum deployable composition (Blocks):** `POST /verify` entry point; `appsettings.json` + startup
  validation; Dockerfile + compose; wire the orphaned **report + notify + persist** stages into
  `VerificationPipeline` (currently ends at verdict and discards it); deployable reference bundle; real health
  probes + OTel exporter. [buildable-now]
- **VERIQAN-E2 Cardinal-rule + correctness fixes (Blocks):** scanned/image-only PDF abstain guard (fix
  `BuildAllAbsent` defeating `MandatorySectionsPresenceRule`); §20 saldo-a-favor sign; §16 column-map abstain;
  CL-35 Aptos embedding/weight-variant; CL-34 card-in-image; CL-31 pagination footer anchor; European number
  parsing (es-MX culture); timezone/determinism (no ambient clock); FR-12 pHash catalog-image. [buildable-now;
  §20/§16 verification corpus-gated]
- **VERIQAN-E3 Persistence/durability/audit/security (Blocks):** persist Findings + JobVerdict; EF resume/reprocess
  stores; ledger/immutable audit; authn/authz + TLS; key management (AES-GCM + Key Vault — provisioning is
  business-gated). [buildable-now + business-gated key custody]
- **VERIQAN-E4 PII compliance (Blocks — design):** LFPDPPP retention/erasure/minimization for stored statement PII
  (RFC/CLABE/card#); PCI-DSS card-number scope decision. [business-gated decision]
- **VERIQAN-GATED Productization + calibration (BLOCKED):** E13 13.1–13.4 multi-tenant productization
  [business-gated — issue #17]; threshold calibration against a real corpus [corpus-gated].

---

## 5. Tracker + resumability (this is multi-session)

This will NOT finish in one session. State lives in files:
- The plan files under `docs/planning-artifacts/remediation/` ARE the durable output.
- Keep `remediation/TRACKER.md` current (which epics/stories drafted, which pending).
- When context gets high: write a one-paragraph handoff at the top of `TRACKER.md`, update memory, then it's safe
  to clear — the matrices + plan files + tracker carry the thread.
- On resume: re-read THIS file + `remediation/TRACKER.md`, pick up the next undrafted epic.

---

## 6. Clarifications to raise with the owner BEFORE finalizing the plan

Surface these (don't silently default) once you've drafted enough to ask precisely:
- **Story granularity / team shape:** is this plan for the orchestrator-with-subagents loop (like the challenge),
  or for a human dev team? (Affects story sizing + AC detail.)
- **PRISMA-E3 security:** fold security hardening into this plan as full epics, or hold for the separate security
  review and only stub the dependency? (Locked default: stub the dependency, flag.)
- **Effort/timeline expectations:** the RC6 estimate was ~6–8 weeks (1 dev) for Prisma's engineering tail — does the
  owner want a wave-by-wave timeline, or just dependency-ordered backlog?
- **Veriqan corpus:** is corpus acquisition being pursued in parallel (so VERIQAN-E2's §20/§16 verification can be
  scheduled), or is it fully blocked? (Affects whether those stories are buildable-soon or BLOCKED.)

---

## 7. Definition of done for THIS (planning) session — the approval gate

STOP and present for approval when:
- Both subsystems have a complete epics-and-stories plan in `docs/planning-artifacts/remediation/`.
- Every Blocks-production gap (16 Prisma + 28 Veriqan) maps to ≥1 story (coverage matrix proves it; deferrals named).
- Gated items are present as BLOCKED placeholders with the unlock named.
- The sprint/wave plan mirrors RC6 with the critical path + the 3 non-eng unlocks called out.
- A clear "what executes first when approved" recommendation (the unified Wave-0).
Then summarize + hand off for owner review. **Do not start implementing production fixes.**

---

## 8. Gotchas (respect these — from the challenge run)
- **E: filesystem is SLOW** (builds 2–4 min) — prefer `git ls-files` over `find`; build single projects, not the
  whole `.sln`, when checking one thing.
- `dotnet test <csproj>` **plain** — never `--nologo` or extra flags (the xunit.v3 + MTP stack is version-coupled).
- **Believe wiring + ground-truth runs, never comments/prose.** The challenge caught multiple stale doc-comments
  and one stale CLAUDE.md "Release Status" (Prisma is far more done than it claims). Re-verify before planning a fix
  for something that may already be resolved (check the RC5 "Resolved since 2026-06-11" list first).
- **The owner edits in the same local repo** — `git fetch` / check status first; the branch is `Liv` (102+ commits
  ahead of `main`).
- **Don't treat dormant Python (CSnakes/VLM) as a gap** — it's optionality-by-design (ADR-001).
- **Tesseract is the OCR engine of record** by deliberate decision — don't plan to "fix" it back to VLM.
