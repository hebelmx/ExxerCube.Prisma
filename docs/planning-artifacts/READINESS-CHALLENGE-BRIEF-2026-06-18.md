# Scope-vs-Build Readiness Challenge — Brief (for approval)

**Date:** 2026-06-18 · **Branch:** `Liv` · **Status:** DRAFT BRIEF — awaiting owner approval before fan-out.
**Target bar:** **Full production.** **Sequence:** **Veriqan VEC first, then the whole ExxerCube.Prisma MVP.**

> This is **not a code review** (which audits code that *exists* for correctness). It is a **readiness
> challenge**: it hunts the **negative space** — every intended capability with no *working, end-to-end-
> verified* proof behind it (not a green unit test, not a stub, not a synthetic fixture). It refreshes and
> extends the prior gap-analysis art (`docs/planning/gap-analysis/GAP-MATRIX-2026-06-11.md`, `MVP-PATH`),
> it does not duplicate it.

---

## 0. Why this finds what the epic ledger can't

"All epics done" measures *planned work executed*. "Full-production-ready" measures *fitness for the real
purpose*. They diverge for three structural reasons (each already visible in this repo):
1. **The map isn't the territory** — epics only contain *foreseen* work; readiness gaps are dominated by
   *unforeseen* work that never became an epic, task, or red test (unknown-unknowns).
2. **Green tests are tautological to their fixtures** — proven live: the calibration harness showed the 3
   "Dummie VEC" fixtures aren't even compliant statements, yet every prior suite was green.
3. **Deferral debt compounds invisibly** — the union of honest "carry-forward / InsufficientData /
   uncalibrated / Stub-*" items is never itself an epic, so it doesn't appear in the count.

## 1. Two meta-gaps we already know — so we measure the *engineering tail beneath them*

Full production has two largest dependencies that are **not engineering tasks**; the challenge must state
them up front and then quantify everything that sits *under* them:
- **E13 (multi-tenant productization) is required for full production and is BUSINESS-GATED** on GitHub
  issue #17 (economic-buyer discovery, still OPEN). Full production cannot ship without 13.1–13.4
  (embeddable pipeline-gate, per-tenant onboarding, traceability-matrix export, regulatory-version
  provenance). **The critical-path blocker is a buyer decision, not code.**
- **A real CONDUSEF-statement corpus is required for calibration** (E11 §6/§16 extractor X-ranges, §20
  saldo-a-favor sign, all E12 typography/geometry thresholds). The calibration *harness* now exists; the
  *data* does not. **This is a data-acquisition/business task.**

The challenge's job: assuming those two unlock, **what is the full engineering + ops + security gap to a
production-grade regulatory pipeline-gate?**

## 2. What "full production" means here (the readiness dimensions we score against)

A preventive compliance gate that lives *inside a bank's statement-generation pipeline* (load-bearing
infra) must satisfy all of:
1. **Functional correctness on real data** — calibrated thresholds, real reference data, synthetic
   fixtures replaced/supplemented; the cardinal rule (never false-block) holds on real inputs.
2. **End-to-end deployable composition** — a real composition root runs ingest → extract → bind →
   validate → verdict → report → persist → notify against real inputs (not in-memory/stub substrates).
3. **Multi-tenancy (E13)** — per-tenant legal-baseline ⊕ overlay, traceability export, regulatory-version
   provenance, embeddable gate API/SDK.
4. **Ingestion** — real statements arrive through a real path (not `StubDocumentDownloader`); for the
   broader Prisma, the security-mandated **3-process split (A1–A6)**.
5. **Persistence & data** — production migrations, the encrypted legal-baseline store (built E9) with real
   **key management**, immutable audit trail.
6. **Security & compliance posture** — secrets, encryption-at-rest (built) + in-transit, authn/authz,
   audit immutability, ISO 27001 / SOC 2 trajectory, CNBV CUB outsourcing-regime alignment.
7. **Operational readiness** — deploy artifacts, config with **no hardcoded `Server=DESKTOP-...`**, real
   health/readiness probes (currently stubbed), observability wired to a backend, alerting, runbook.
8. **Performance / scale / SLA** — throughput, p95, bounded concurrency (built E8) under real volume; SLA
   surface.
9. **Failure modes** — malformed PDF, missing/partial reference data, partial extraction, downstream
   outage — all must degrade to abstain/InsufficientData, never false-block or crash.

## 3. The two ground truths (Phase 0)

- **0a — Intended-scope register.** Flatten **FR-1..39, NFR-1..8**, every **epic AC (E1–E13)**, the
  mission/PRD intent, and the "preventive pipeline-gate" product definition into ONE numbered requirement
  list. Tag each: *technical / E13-gated / corpus-gated / business-gated*.
- **0b — Built-reality map.** Read the **composition roots** (`Veriqan.Worker`, and for Prisma the
  Athena/Orion Workers + Web.UI) — what is actually DI-wired vs stubbed. Build a **test-substrate
  inventory**: which suites run on real data/Testcontainers/real infra vs fakes/in-memory/synthetic.
  Capture current build/test status. **Believe wiring and ground-truth runs, never comments or prose.**

## 4. The challenge phases (how the fan-out is structured)

| Phase | What | Agents |
|-------|------|--------|
| **0** | Two ground truths (§3) | 2 parallel readers (scope register; reality map) |
| **1** | Requirement → evidence trace; classify each *Real+Wired+E2E / Real-unwired / Partial / Stub / Missing / Unknown*. **Bar = end-to-end evidence, not a unit test.** | fan-out by requirement cluster: extraction · validation/computation · visual/typography · verdict/reporting · persistence/store · ingestion · multi-tenancy(E13) · observability/ops · security |
| **2** | Adversarial refutation of every "done/real" claim — find the stub behind the green, the fake substrate, the hardcoded value, the unhandled failure | skeptics per high-value claim |
| **3** | **Negative-space lenses** (full-production specific) — see §5 | fan-out by lens |
| **4** | Synthesis → **Readiness Gap Matrix** + ordered **Path-to-Production** + explicit *unknown-unknowns surfaced* | 1 synthesizer + completeness critic |

## 5. Negative-space lenses (Phase 3 — the unknown-unknown sweep)

- **Real-data lens** — walk one real end-to-end statement journey; log every place it can't run on real
  inputs; corpus/calibration gaps; every synthetic/mock substrate that would behave differently in prod.
- **Deploy/ops lens** — what does a production deploy need that doesn't exist? config, secrets, migrations,
  health/readiness, observability backend, alerting, runbook, the hardcoded-server-name class of issue.
- **Security/compliance lens** — the 3-process split (A1–A6), encryption key management, audit
  immutability, authz model, regulatory regime (CNBV CUB), cert trajectory; what a compliance auditor
  demands of a load-bearing gate.
- **Scale/SLA/failure-mode lens** — real volume, concurrency, malformed inputs, downstream outage; does
  the cardinal rule (never false-block) survive stress?
- **Integration lens** — how does it embed *inside the bank's PDF-generation pipeline* as a gate (E13
  13.1)? API/SDK surface, latency budget, the host's failure semantics.

## 6. Outputs

1. **Readiness Gap Matrix** — one row per gap: `{ Dimension, Requirement/Intent, Built-state, Evidence (or
   its absence), Severity [Blocks-production / Degrades / Cosmetic], Effort, Dependency (technical /
   E13-gated / corpus-gated / business-gated), Owner-action }`.
2. **Path-to-Production** — the gaps ordered into a buildable sequence, with the business-gated items
   (E13, corpus) called out as the critical-path unlocks they are.
3. **Unknown-unknowns surfaced** — a named list of things no requirement captured.
4. Refreshes/supersedes the 2026-06 GAP-MATRIX for *current* state.

## 7. Sequencing & honesty rules

- **Track A = Veriqan VEC** (run first — we have deep context; smaller surface). **Track B = whole Prisma
  MVP** (second pass; reconciles the existing GAP-MATRIX/MVP-PATH against today).
- **Demand end-to-end evidence.** A green unit test is not evidence of readiness; a system/integration run
  or a manual end-to-end execution is.
- **Refute, don't accept.** Every "done" is a claim to be broken against ground truth.
- **Classify gated vs buildable.** Separate "blocked on a human/business/data decision" from "engineering
  we can do now" — so the buildable tail is actionable immediately.
- **No fabricated readiness.** If something can't be verified, it's *Unknown*, not *Pass*.

## 8. Cost & what I need from you

- **Effort:** Track A ≈ one orchestrated multi-agent run (Phase 0–4, ~10–16 agents incl. skeptics);
  Track B a second, larger run. Token cost is meaningful (this is a deliberate fan-out) — that's why this
  is gated on your approval.
- **From you:** (a) approve this brief (or adjust scope/lenses); (b) point me at the canonical PRD /
  mission docs you consider authoritative for the *intended* full-production scope (I'll use FR/NFR +
  epics.md + the GAP-MATRIX set unless you name others); (c) confirm whether the security 3-process split
  (A1–A6) and CNBV/cert posture are in-scope for *this* audit or a separate security review.

## 9. Tracker (this plan, not yet executed)
- **RC.0** Ground truths (scope register + reality map) — Veriqan.
- **RC.1** Requirement→evidence trace — Veriqan.
- **RC.2** Adversarial refutation — Veriqan.
- **RC.3** Negative-space lenses — Veriqan.
- **RC.4** Synthesis: Veriqan readiness matrix + path-to-production.
- **RC.5** Repeat 0–4 for the whole Prisma MVP (Track B).
- **RC.6** Merge into one cross-cutting path-to-production + unknown-unknowns.

## 10. Kickoff for a fresh context (resume here)

This challenge is **deliberately run from a fresh/cleared context** — an audit must re-ground from files,
not inherit the prior session's framing (a loaded context biases toward confirming its own conclusions).
Everything needed is on disk. To start:

1. **Read this brief** (the intended-solution anchor) + the tracker tasks **RC.0–RC.6** + memory
   `veriqan-vec-progress.md` (esp. the calibration-harness note: the 3 PRP2 fixtures are NON-compliant).
2. **Locked decisions** (do not re-litigate): bar = **full production**; sequence = **Veriqan (Track A)
   first, then whole Prisma MVP (Track B)**; this is a **scope-vs-build readiness challenge**, not a code
   review; demand **end-to-end evidence**, refute every "done", classify gated-vs-buildable.
3. **Two inputs to confirm at start** (proceed on the stated DEFAULT if the owner is absent):
   - *Authoritative intended-scope docs* — DEFAULT: `FR-1..39` + `NFR-1..8` (epics.md) + the
     `GAP-MATRIX-2026-06-11` / `MVP-DEFINITION-2026-06` / `PRD-RECONCILIATION-2026-06` set + mission docs.
   - *Security 3-process split (A1–A6) + CNBV/ISO/SOC posture* — RECOMMENDED: flag gaps here but audit it
     in a SEPARATE dedicated security review (it's heavy enough to deserve its own pass). Confirm or fold in.
4. **Gotchas to respect** (from `veriqan-vec-progress.md` + prior handoffs): E: filesystem is SLOW
   (builds 2–4 min — prefer `git ls-files`); `dotnet test <csproj>` plain, never `--nologo`; believe
   wiring + ground-truth runs, never comments/prose; the owner edits in the same local repo (fetch first).
5. **Run Track A** (RC.0→RC.4) as one orchestrated fan-out; surface findings; then Track B; then RC.6.
   Output = the Readiness Gap Matrix + Path-to-Production (§6), superseding the 2026-06 GAP-MATRIX for
   current state. The two biggest gaps are non-engineering (E13 buyer-gate, real corpus) — quantify the
   engineering/ops/security tail beneath them.
