---
title: PRD ↔ Architecture Consistency Review — Veriqan VEC
status: review
created: 2026-06-16
reviewer: traceability / contradiction-hunt pass
inputs:
  - docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/prd.md
  - docs/planning-artifacts/architecture.md
---

# Consistency Review: PRD vs Architecture (Veriqan VEC)

**Scope of this review:** traceability (does every PRD requirement have an architectural home?) and
contradiction-hunting (does the architecture conflict with a PRD decision?). Not style, not prose
quality.

**Verdict:** The architecture is **strongly consistent** with the PRD — every FR and NFR has an
architectural home and all six load-bearing PRD decisions (deterministic-first, human-in-the-loop,
additive/non-breaking, v1-vs-v2 scope, 55-CL coverage, SM-1 honesty, tolerance-as-data) are honored.
Issues found are **minor traceability gaps and a few unstated assumptions**, not contradictions. No
Critical or High findings.

---

## 1. FR Coverage Matrix (FR-1 … FR-27)

Cross-referenced against architecture §4 (component pipeline), §13 (Component→FR/ADR index), and
inline ADR mentions. "Home" = a named component or ADR that owns the FR.

| FR | PRD capability | Architectural home | ADR | Covered? |
|----|----------------|--------------------|-----|----------|
| FR-1 | Ingest statement / create job | Intake/Job (`Veriqan.Application`), §4 + §13 | ADR-V1 | Yes |
| FR-2 | Bind Reference Bundle | Context Binding via `IVecReferenceDataProvider`, §4 + §6 + §13 | ADR-V4 | Yes |
| FR-3 | Detect product → canonical id | Context Binding, §4 + §13 | ADR-V4 | Yes |
| FR-4 | Extract header identity fields | `Veriqan.Infrastructure.Extraction`, §4 + §13 | ADR-V1, ADR-V8 | Yes |
| FR-5 | Extract period/summary fields | Extraction, §4 + §13 | ADR-V1, ADR-V8 | Yes |
| FR-6 | Intra-statement arithmetic | Validation Engine, §5 + §13 | ADR-V2, ADR-V3 | Yes |
| FR-7 | Cross-period checks (Prior Statement) | Validation Engine (`PriorStatement` in `VerificationContext`), §5 + §13 | ADR-V2, ADR-V3 | Yes |
| FR-8 | Configurable Tolerance Bands | Validation Engine; tolerance-as-data, §5 + §13 | ADR-V3 | Yes |
| FR-9 | Embedded-font (Aptos) | Visual Inspection, §4 + §9 + §13 | ADR-V2, ADR-V7 | Yes |
| FR-10 | Overlap + header styling | Visual Inspection (Shapely geometry), §9 + §13 | ADR-V2, ADR-V7 | Yes |
| FR-11 | Pagination / blank / per-page logo+card | Visual Inspection, §13 | ADR-V2, ADR-V7 | Yes |
| FR-12 | Image presence (v1) / match+order (v2) | Visual Inspection (pHash) + ML boundary (v2), §9 + §13 | ADR-V7 (v1), ADR-V8 (v2) | Yes |
| FR-13 | Mandatory legends + COMPARA TU TARJETA | Regulatory/Fiscal, §13 | ADR-V2, ADR-V7 | Yes |
| FR-14 | Fiscal block / QR extraction | Regulatory/Fiscal, §13; QR lib in ADR-V7 | ADR-V2, ADR-V7 | Yes |
| FR-15 | Aggregate findings → Verdict | Reporting / Application, §4 + §13 | ADR-V1 | Yes |
| FR-16 | Color-marked PDF | Reporting (Marked-PDF annotator), §4 + §13 | ADR-V1 | Yes |
| FR-17 | Findings email alert | Reporting, §4 + §13 | ADR-V1 | Yes |
| FR-18 | Disposition (human-in-the-loop) | QA Console, §4 + §7 + §13 | ADR-V1 (+ ADR-V5 audit) | Yes |
| FR-19 | Single reference-data contract | Reference Data adapters, §6 + §13 | ADR-V4 | Yes |
| FR-20 | Degrade gracefully on partial data | Reference Data graceful degradation, §6 + §13 | ADR-V4 | Yes |
| FR-21 | Batch w/ bounded concurrency | `Veriqan.Worker`, §10 + §13 | ADR-V1 | Yes |
| FR-22 | Resume & reprocess | `Veriqan.Worker` (idempotent by hash), §10 + §13 | ADR-V1 | Yes |
| FR-23 | Additive non-breaking integration | Brownfield/Phase-0, §8 + §13 | ADR-V5, ADR-V6 | Yes |
| FR-24 | Reuse Shared Core infra | Shared-Core Reuse Map §3; §13 row Brownfield/Phase-0 | ADR-V6 (§3 map) | **Yes — but weakly indexed (see L-1)** |
| FR-25 | Movement-detail reconciliation | Validation Engine, §4 + §13 | ADR-V2, ADR-V3 | Yes |
| FR-26 | Promotions current | Regulatory/Fiscal, §13 | ADR-V2, ADR-V7 | Yes |
| FR-27 | Phase-0 genericization of seams | Brownfield/Phase-0, §8 + §13 | ADR-V6 | Yes |

**Uncovered FRs: none.** All 27 have a component/ADR home.

**Caveat (L-1):** FR-24 ("reuse Shared Core") is listed in §13 only inside the
"Brownfield/Phase-0" row alongside FR-23/FR-27 and is really *realized* by the §3 Shared-Core Reuse
Map. It is covered, but the index attribution is loose — FR-24 is about reuse, not Phase-0
genericization, and is not tied to a dedicated ADR. Cosmetic; trace it to §3 explicitly.

---

## 2. NFR Coverage (NFR-1 … NFR-6)

| NFR | PRD intent | Architectural treatment | Covered? |
|-----|-----------|--------------------------|----------|
| NFR-1 Performance/Throughput | ≤10s p95 single statement; ≥1 stmt/s/worker; window 3–5 d; bounded concurrency | §10 Batch/throughput: bounded concurrency + backpressure, ≈1 stmt/s/worker, horizontal scale; §11 topology | Yes — but **latency budget not decomposed (M-1)** |
| NFR-2 Accuracy | det. ≥99% P/R; FP <1% (SM-C1); no silent passes (SM-C2) | §5 pure deterministic rules; §12 pHash FP tracked vs SM-C1 | Partial — **SM-C2 "no silent passes" not architecturally enforced (M-2)** |
| NFR-3 Reliability | one failure never halts batch; exception queue; resumable | §10: failures → exception queue, no batch halt, resumable, idempotent by hash | Yes |
| NFR-4 Observability | per-job/check/finding logging, correlation ids, metrics | §10 Observability: Serilog + metrics, correlation id per job, throughput/latency/verdict/exception counters | Yes |
| NFR-5 Determinism/Reproducibility | same inputs → same Findings | §5: rules "pure and deterministic (NFR-5)"; ReferenceBundleVersion + EngineVersion stamping (§7) | Yes — strong |
| NFR-6 Error handling | `Result<T>` everywhere; no exceptions for control flow | §5: rules return `Result<T>`, "no rule throws for control flow (NFR-6)"; §3 reuse `Result<T>` | Yes |

**NFR gaps:**
- **M-1 (NFR-1):** The PRD gives a concrete per-statement latency budget (**≤10s p95** for
  deterministic + lightweight-CV). The architecture restates throughput (≈1 stmt/s/worker) but
  never decomposes or commits to the *latency* budget per stage (extraction vs engine vs visual vs
  reporting). Throughput ≈1/s/worker is roughly consistent with ≤10s latency only under
  concurrency, so this isn't a contradiction — but the latency NFR is effectively unaddressed as a
  design target.
- **M-2 (NFR-2 / SM-C2):** "No silent passes" (a PASS emitted without truly evaluating) is a
  first-class PRD counter-metric (SM-C2, "must be 0"). The architecture enforces the
  `INSUFFICIENT_DATA`-not-`FAIL` direction well (§6) but says nothing about preventing the opposite
  failure mode — a Check that can't evaluate silently returning PASS. No architectural guard
  (e.g., a rule must return InsufficientData, never Pass, when its inputs are absent) is described.

---

## 3. Contradictions vs Load-Bearing PRD Decisions

Checked each of the seven decisions the prompt flagged. **No hard contradictions found.** Detail:

| PRD decision | Architecture stance | Conflict? |
|--------------|---------------------|-----------|
| **Deterministic-first** | §1 constraints, ADR-V7 ("v1 needs no GPU and no learned models"), ADR-V2 technique class | No — faithfully reproduced |
| **Human-in-the-loop (no auto-reject v1)** | §1, §4 QA Console, FR-18/ADR-V1; §7 Disposition immutable | No |
| **Additive / non-breaking** | ADR-V5 (additive tables only), ADR-V6 (additive genericization + gates), §8 arch test | No |
| **v1 vs v2 scope** | ADR-V8 (ML behind ports, v2), §11 (GPU service v2), FR-12 split | No |
| **55 CL coverage** | Defers to PRD §17 map; §5 "Rule→CL mapping is 1:1 or 1:many per the PRD §17 coverage map" | No — but see **L-2** |
| **SM-1 honesty (two numbers; INSUFFICIENT_DATA is a data gap, not coverage)** | §6 graceful degradation emits INSUFFICIENT_DATA; ReferenceBundleVersion supports the (b) metric | No — consistent; though SM-1(a)/(b) split is not explicitly named (see L-3) |
| **Tolerance-as-data** | ADR-V3 explicit ("tolerance and thresholds are data, not code … no magic numbers"); covers FR-8 numeric AND §4.4 visual thresholds | No — exemplary |

**Notable: no contradiction**, but two soft traceability concerns:
- **L-2:** The architecture *delegates* 55-CL coverage entirely to PRD §17 rather than asserting it,
  which is the correct, non-duplicative choice — but it means the architecture itself contains no
  independent check that all 55 rules exist. The "discovery via DI, registering a rule adds a Check"
  mechanism (§5) makes *omission of a rule* mechanically silent. Recommend an architecture/coverage
  test that asserts a registered `IVecValidationRule` exists for every CL-id in §17 (ties to SM-1(a)
  = 100% implemented coverage of in-scope items).
- **L-3:** PRD SM-1 deliberately splits into (a) implemented coverage and (b) effective evaluation
  rate. The architecture supports both but never names the split; an reader could conflate them.
  Cosmetic.

---

## 4. Glossary / Terminology Drift

Compared architecture vocabulary to PRD §3 Glossary. Mostly clean; a few drifts:

| PRD Glossary term | Architecture usage | Drift? |
|-------------------|--------------------|--------|
| **Statement** | "BankStatement" domain entity (§2 project list), "Statement PDF" | **Minor (L-4):** PRD canonical term is *Statement*; architecture introduces `BankStatement` as the type name. Harmless but a new spelling. |
| **Finding** | `Finding` entity/record (§5, §7) | Consistent |
| **Verdict** | `Verdict` (§5, §7) | Consistent |
| **Reference Bundle** | `ReferenceBundle` (§5, §6) | Consistent |
| **Check** | "Check" + `CheckId` + `IVecValidationRule` | Consistent — note the architecture realizes a *Check* as a *rule* (`IVecValidationRule`); PRD never uses "rule" as a noun for Check. Acceptable mechanism naming. |
| **Checklist Item (CL-N)** | `CheckId = "CL-21"` | Consistent |
| **Disposition** | `Disposition` (§7) | Consistent |
| **Tolerance Band** | `toleranceConfig` / `ToleranceApplied` | Consistent (PRD also uses toleranceConfig) |
| **Reference Adapter** | `CsvReferenceDataAdapter` etc. | Consistent |
| **Marked PDF** | "Marked PDF" / "Marked-PDF annotator" | Consistent |
| **Prior Statement** | `PriorStatement` (§5) | Consistent |
| **Period** | "versioned by Period" (§7) | Consistent |
| **StatementModel** | *(architecture-only term)* — extracted typed model | **New term, not in Glossary (L-5):** the architecture introduces `StatementModel` (the extracted-fields snapshot) as a distinct concept from `Statement`/`BankStatement`. Useful and safe, but it should be added to the Glossary to avoid Statement vs StatementModel vs BankStatement confusion (three names in play for closely-related concepts). |

**Verdict on terminology:** consistent overall; the only real drift is the **Statement /
BankStatement / StatementModel** triple (L-4 + L-5) — recommend one canonical naming note.

---

## 5. Decisions the Architecture Introduces (not anticipated by the PRD)

These are architecture-originated choices. Each assessed for safety against PRD constraints.

| # | Architecture decision | In PRD? | Safe? |
|---|------------------------|---------|-------|
| D1 | **Specific libraries**: PdfPig/pdfplumber (extraction), Shapely (geometry), perceptual-hash lib, ZXing.NET (QR, pending), LayoutLMv3/CLIP (v2 ML) | PRD explicitly defers tech choices to `addendum.md` (§0). | **Safe** — within PRD's mandate; ZXing is flagged pending (§14.1). Confirm they match `addendum.md`. |
| D2 | **`IVecValidationRule` + DI discovery** (one rule per CL, auto-registered) | PRD describes Checks abstractly; not the rule interface | **Safe & good** — enables additive growth. Caveat L-2 (silent omission). |
| D3 | **EF Core entity set** (`VerificationJob`, `Finding`, `Verdict`, `Disposition`, `ReferenceBundleVersion`, `EngineVersion`) | PRD §13/§14 imply persistence + provenance; specific schema is new | **Safe** — additive-only per ADR-V5, matches FR-23/§13. |
| D4 | **`ReferenceBundleVersion` versioned by Period + `EngineVersion` stamping** | PRD §14 wants engine version on each Check; bundle-versioning-by-Period is an architecture add | **Safe & beneficial** — directly strengthens NFR-5 determinism/reproducibility. |
| D5 | **ML ports defined in v1** (`IStatementMlExtractor`, `IVisualComplianceMl`) so v2 slots in | PRD wants stable boundary; the specific ports are new | **Safe** — honors "behind a stable boundary." |
| D6 | **`Sentinel` reused directly for worker health; `InMemoryEventBus` for events** | Not in PRD | **Safe** — reuse-aligned (FR-24). But `InMemoryEventBus` may not survive the horizontal-scale / independently-deployable-worker requirement (NFR-1, §12 open #2). Flag as **W-1 below**. |
| D7 | **Open arch decisions punted** (§14): QR lib, queue transport, QA Console host, PAN-at-rest, first adapter | PRD leaves OQ-2/PCI open | **Safe** — explicitly tracked, tied back to PRD OQs. |

**W-1 (the one to watch):** §3 maps Eventing to `InMemoryEventBus` and §14.2 lists queue transport
as open (in-proc vs Service Bus vs SignalR/Ember). An **in-memory** bus contradicts the
**independently-deployable, horizontally-scaled worker** posture (NFR-1, §11) if cross-process
eventing is ever needed. Not a present contradiction (it's listed as open), but the §3 "reuse
`InMemoryEventBus`" entry and the §14 open decision are in mild tension — resolve before batch scale-out.

---

## 6. PRD §8 Open Questions / §15 Risks Silently Assumed Away

Checked whether the architecture quietly resolved a PRD open question or risk without flagging it.

**Open Questions (§8):**

| OQ | Handled by architecture? | Silently assumed? |
|----|--------------------------|--------------------|
| OQ-1 Sample size/cadence/SLA | Restated as design point (≈1 stmt/s) | No — kept as assumption |
| OQ-2 Reference-data delivery mechanism | §14.5 open, tied to OQ-2; ADR-V4 covers all mechanisms | No — explicitly open |
| OQ-3 Image catalog sourcing | Implied needed for pHash presence; **not called out** | **Partly (G-1):** architecture assumes reference images exist for pHash presence checks (FR-12 v1) but never surfaces OQ-3's "how are images delivered/keyed." pHash presence needs a reference image to hash against; if none is delivered, FR-12 v1 silently degrades. Should be flagged as a data dependency. |
| OQ-4 PDF nature (text vs scan) | §4/§12 scope to text-layer, OCR sequenced | No — handled as risk R5 |
| OQ-5 Accuracy acceptance bar | Not addressed (validation-set production) | **Partly (G-2):** architecture asserts ≥99% determinism but is silent on how the labeled validation set (OQ-5) is produced/where it lives. Acceptable for an arch doc, but SM-3 measurement has no home. |
| OQ-6 Notification targets | FR-17 email in Reporting; channel assumed email | Minor — assumes email-only, matches PRD default |
| OQ-7 Product naming | §2 picks `ExxerCube.Prisma.Veriqan.*` | **Resolved (G-3, benign):** architecture *commits* to `ExxerCube.Prisma.Veriqan.*`, silently closing OQ-7 (which offered `ExxerCube.Veriqan` as alternative). This is a reasonable resolution but should be noted as "OQ-7 decided" rather than assumed. |

**Risks (§15):**

| Risk | Architecture mitigation present? |
|------|----------------------------------|
| R1 Reference data messy/absent | Yes — ADR-V4 graceful degradation (§6, §12) |
| R2 Salvage > rebuild | Yes — ADR-V8 selective salvage list |
| R3 Visual ML disappoints | Yes — ML deferred to v2 behind ports (ADR-V8) |
| R4 Brownfield breaks Solution 1 | Yes — ADR-V6 gates + arch test (§8) |
| R5 Scanned PDFs degrade extraction | Yes — text-layer scope, OCR sequenced (§12) |
| R6 Scale to 100% | **Partly (G-4):** architecture sizes for the sample (≈1 stmt/s/worker, "scale horizontally") but, as W-1 notes, the in-memory eventing/queue choice is unsettled; the §12 risk list omits R6 (100% scale) entirely. R6 is neither contradicted nor explicitly carried into the architecture risk register. |

---

## 7. Findings Ranked by Severity

**Critical:** none.
**High:** none.

**Medium:**
- **M-1** — NFR-1 latency budget (≤10s p95) not decomposed/committed in the architecture (only
  throughput is). §10/§11.
- **M-2** — SM-C2 "no silent passes" not architecturally enforced; only the
  INSUFFICIENT_DATA-not-FAIL direction is guarded. §5/§6.
- **W-1** — `InMemoryEventBus` reuse (§3) is in tension with independently-deployable, horizontally
  scaled workers (NFR-1, §11) and the still-open queue-transport decision (§14.2). Resolve before
  scale-out.

**Low / Cosmetic:**
- **L-1** — FR-24 (Shared-Core reuse) loosely indexed in §13 (bundled into Brownfield/Phase-0 row);
  should trace to §3 reuse map.
- **L-2** — No coverage test asserting an `IVecValidationRule` exists per CL-id (§17); DI discovery
  makes a missing rule silently absent. Ties to SM-1(a).
- **L-3** — SM-1 (a)/(b) split supported but never named in the architecture.
- **L-4 / L-5** — Naming triple **Statement / BankStatement / StatementModel**; `StatementModel`
  not in the Glossary. Add one canonical naming note.
- **G-1** — OQ-3 (image catalog sourcing) is a silent data dependency for FR-12 v1 pHash presence;
  not surfaced.
- **G-2** — OQ-5 validation-set production / SM-3 measurement has no architectural home.
- **G-3** — OQ-7 product naming silently resolved to `ExxerCube.Prisma.Veriqan.*` (benign; note as
  decided).
- **G-4** — R6 (100% scale) omitted from the architecture risk register (§12).

---

## 8. Recommendation

Accept the architecture as **consistent with the PRD**. Before epics/stories, fold in:
1. A latency-budget decomposition for NFR-1 (M-1).
2. A "no silent PASS" rule contract + the per-CL coverage test (M-2, L-2) — both directly serve
   SM-1/SM-C2 honesty.
3. Resolve the eventing/queue transport so it survives horizontal scale (W-1).
4. One Glossary line reconciling Statement / BankStatement / StatementModel (L-4/L-5).
5. Note OQ-3 image sourcing as an explicit FR-12 v1 data dependency (G-1).

None of these block architecture sign-off; they are tightening, not rework.
