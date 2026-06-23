# SIARA Simulator — Load & Continuous-Supply Roadmap (PLAN ONLY)

**Status:** PLAN — not implemented. Drafted 2026-06-23 while re-running the §2 max-fidelity gate.
**Scope:** two enhancements to the SIARA simulator (`tools/Siara.Simulator/`) and its corpus
generator, to (1) keep cases flowing continuously and (2) generate a realistic high-volume corpus
with burst arrivals. **No code in this doc — implementation is a separate, approved task.**

---

## 0. Why now — findings from the §2 gate re-run (2026-06-23)

- **The SQL-perf wall is RESOLVED** (owner's Docker/WSL patches): a standalone mssql smoke test
  did single-row INSERTs in **~5.8 ms** (was 6–35 s), and gate run 5 logged **0 SQL timeouts**.
- **New, narrower blocker surfaced — the simulator's cold-start case supply.**
  `CaseService.Start()` is deferred to `Dashboard.OnInitialized` (`Components/Pages/Dashboard.razor:242`),
  and arrivals are driven by a Poisson timer (`DistributionService.GetNextPoissonDelay`, rate
  `SimulatorSettings.AverageArrivalsPerMinute`, default 6/min). The served-set is persisted to
  `app/cases.json` (`PersistenceFilePath`), and when every available case has been served the loop
  logs **"Simulation finished"** and stops (`CaseService.ScheduleNextCase`).
- Consequence: gate runs 3 & 4 only passed the login → PDF-link probe because they **reused a
  warm, already-running sim** with active cases. Run 5 started a **cold** sim → it served 0 cases in
  90 s → the dashboard never rendered an `a[href$='.pdf']` link → login probe failed
  (`MaxFidelityGateE2EBase.cs:297`).

Both ideas below directly address this: **Idea 1** makes the sim a self-sustaining supply that
serves from boot and never "finishes"; **Idea 2** gives it a corpus big enough to model real daily
volume so it can run for hours/days without exhaustion.

---

## Idea 1 — Continuous, self-sustaining case supply

**Goal:** the simulator serves cases reliably from a cold start and never silently stops when the
pool is consumed — so any consumer (the gate, a load test, a demo) always finds cases.

### 1a. Auto-start the simulation at host boot (fixes the gate cold-start)
- **Today:** `CaseService.Start()` only runs when an authenticated dashboard is loaded
  (`Dashboard.OnInitialized`). Headless/automated consumers that don't first drive the dashboard
  get an idle sim.
- **Plan:** convert `CaseService` to also start via a hosted `BackgroundService` (or
  `IHostedService.StartAsync`) at app boot, gated by a config flag
  `SimulatorSettings.AutoStartOnBoot` (default **true** for deployment/test, can stay dashboard-driven
  for interactive demos). Keep `Start()` idempotent (it already guards on `_isStarted`).
- **Effect:** the §2 gate (and any headless client) sees cases without a manual dashboard warm-up.

### 1b. Auto-replenish / recycle when the pool is consumed
- **Today:** when `_servedCaseIds` covers all `_availableCaseIds`, `ScheduleNextCase` logs
  "Simulation finished" and stops.
- **Plan — pick a mode via `SimulatorSettings.ExhaustionPolicy`:**
  - `Recycle` (cheapest, default for tests): clear the served-set and re-serve the existing corpus
    (optionally re-stamp arrival timestamps / synthesize fresh case-IDs so downstream dedup by the
    Orion journal doesn't suppress them — note the gate already isolates its journal per run).
  - `Replenish` (most realistic): when the unserved pool drops below a low-water mark, pull the next
    batch from a **large generated corpus** (Idea 2) — i.e., the corpus is the reservoir, the sim
    streams from it.
  - `Stop` (current behaviour): keep as an explicit opt-in.
- **Re-discovery:** `DiscoverAvailableCases` already re-scans the corpus dir on `Reset()`; extend it
  to be safe to call on a low-water trigger so newly-added corpus dirs are picked up without a restart.

### 1c. `cases.json` hygiene (incidental, do alongside)
- `app/cases.json` is the **served-set persistence**, but it is **git-tracked** and accumulates across
  runs (it ballooned to a 1420-file discovery this session). **Plan:** gitignore it (it is runtime
  state) and/or default `ResetCasesOnStartup=true` for test/deploy profiles so each boot starts clean.

### Effort / risk (Idea 1)
- Small-to-medium, contained to `CaseService` + `SimulatorSettings` + `Program.cs` DI.
- Risk: re-served/recycled cases hitting downstream dedup — mitigate with fresh case-IDs or rely on
  per-consumer journals. Keep all new behaviour behind config flags so the interactive demo is
  unchanged.

---

## Idea 2 — High-volume corpus generator + burst-arrival model

**Goal:** model the real intake — order-of-magnitude **~2,500 cases/day, arriving in bursts** — so the
sim can run realistic soak/load scenarios. Target a generated corpus of **~10,000 cases**.

### 2a. The generator (located this session)
- **Real generator:** `Prisma/PRP/PRP1/research/generators/AAAV2_refactored/` (refactored, current).
  Entry points `main_generator.py` and `batch_generate.py`. **Already supports bulk** via `--count`
  and multi-authority distribution; emits the exact corpus shape (authority-prefixed
  `PREFIX-YEAR-NUMBER_TIMESTAMP/` dirs with `.pdf/.docx/.xml/.html`).
- **Do NOT use** the stale copies: `Prisma/Code/Src/Python/Prisma-dumy-generator-AAA/` (legacy) and the
  `scripts/generators/*.py` orchestrators (reference dead paths — update or retire them).
- **Deps:** Python 3.8+, `faker`/`jinja2`/`python-docx`/`lxml`, and **Chrome/Edge headless** (Pyppeteer)
  for the PDF render. No GPU. Optional Ollama for legal-text variation.

### 2b. Sizing for ~10,000 cases (verify before committing disk)
- Per-case ≈ **1.15 MB** all-formats (PDF ~379 KB, DOCX ~130 KB, HTML ~637 KB, XML ~1.8 KB).
- **10,000 cases ≈ ~11.5 GB** on disk → ensure free space on `E:` (corpus is gitignored, do **not**
  commit it). Generation time est. **~5–7 h** at ~2.5–3.5 s/case (single-threaded) — plan to
  parallelise `batch_generate.py` across cores, or generate overnight.
- Authority mix: spread across the real prefixes (PJF/UIF/FGR/SEIDO/AGAFF/AGAFADAFSON2/IMSS/SAT/…)
  via `batch_generate.py --authorities PREFIX:count …`.

### 2c. Burst-arrival model (next sim build)
- **Today:** arrivals are a stationary Poisson process at a fixed `AverageArrivalsPerMinute`.
- **Plan:** replace/augment `DistributionService` with a **time-varying / bursty** arrival model so the
  stream matches reality (~2,500/day in bursts, not a flat trickle). Options:
  - **Compound Poisson / batch arrivals:** at each tick, draw a *batch size* (e.g. Poisson or
    heavy-tailed) so multiple cases land at once = a burst.
  - **Markov-modulated / scheduled rate:** a daily profile (quiet overnight, peaks at business-hour
    windows) driving λ(t); configurable via a `BurstProfile` settings section.
  - Keep the current flat Poisson as a `Uniform` profile for back-compat.
- **Confirm the real number** with the owner/PRD before hard-coding (2,500/day was "order of
  magnitude, in bursts" — treat as a configurable target, not a constant).

### Effort / risk (Idea 2)
- Generator scale-up: low code risk (count param exists) but **operational** (disk, hours, Chrome dep).
  De-risk with a 10–100 case dry run + size/time extrapolation before the full 10k.
- Burst model: medium — new arrival-distribution code + config + tests; isolate behind a
  `BurstProfile` config so the default stays the simple Poisson.

---

## Suggested sequencing (when approved)
1. **Idea 1a** (auto-start at boot) — smallest change, unblocks the §2 gate cold-start immediately.
2. **Idea 1c** (`cases.json` hygiene) — trivial, stops the discovery-bloat foot-gun.
3. **Idea 2a/2b** (generate ~10k corpus) — operational, run a dry-run first; the reservoir for 1b.
4. **Idea 1b** (`Replenish` from the big corpus) — depends on 2a.
5. **Idea 2c** (burst arrival model) — last; confirm the real daily-volume target first.

## Open questions for the owner
- Confirm the real daily volume + burst shape (2,500/day was approximate).
- Where should the 10k corpus live (kept on `E:` only, regenerated on demand, or a shared artifact)?
- For the gate specifically: is auto-start (1a) enough, or do we also want the gate test to
  explicitly pre-warm the sim as a belt-and-suspenders step?
