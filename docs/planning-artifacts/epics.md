---
stepsCompleted: [1, 2, 3]
inputDocuments:
  - docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/prd.md
  - docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/addendum.md
  - docs/planning-artifacts/architecture.md
  - docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/.decision-log.md
authoringMode: 'fast-one-pass'
project_name: 'veriqan-vec'
---

# Veriqan VEC - Epic Breakdown

## Overview

Decomposes the Veriqan VEC PRD (27 FRs, 6 NFRs, 55-item checklist) and architecture (8 ADRs) into
implementable, value-sequenced epics and stories. Phasing follows the architecture's deterministic-
first, brownfield-safe order: **isolate first (no Solution 1 risk) → bind data → extract → validate
arithmetic (highest value) → visual → regulatory → report → scale.** v1 is pure C#; ML is v2.

## Requirements Inventory

### Functional Requirements

- FR-1: Ingest a statement for verification (idempotent by content hash).
- FR-2: Bind a Reference Bundle to a statement (schema-validated; missing → INSUFFICIENT_DATA).
- FR-3: Detect product type and resolve to canonical productId.
- FR-4: Extract header identity fields (name/addr/branch/card/CLABE/client#/RFC) — CL-2..8.
- FR-5: Extract period & summary fields (product, rate, dates, amounts) — CL-1, 9..16.
- FR-6: Evaluate intra-statement arithmetic checks — CL-10, 18..26.
- FR-7: Evaluate cross-period checks vs Prior Statement — CL-17, 36..41.
- FR-8: Apply configurable Tolerance Bands.
- FR-9: Verify embedded font compliance (Aptos) — CL-35.
- FR-10: Detect overlapping text and header styling — CL-28, 29.
- FR-11: Verify pagination, blank pages, per-page elements — CL-31, 33, 34, 48.
- FR-12: Verify catalog image presence (v1) / order & match (v2) — CL-27, 30, 47.
- FR-13: Verify mandatory legends and regulatory sections — CL-32, 46.
- FR-14: Extract and validate the fiscal block (QR, fiscal code, RFCs) — CL-50..53.
- FR-15: Aggregate findings into a statement verdict.
- FR-16: Generate a color-marked PDF — CL-55.
- FR-17: Send a findings email alert — CL-54.
- FR-18: Disposition findings (human-in-the-loop).
- FR-19: Provide reference data via a single contract (adapters).
- FR-20: Degrade gracefully on partial reference data.
- FR-21: Process statements in batch with bounded concurrency.
- FR-22: Resume and reprocess.
- FR-23: Additive, non-breaking integration (Veriqan→Prisma only).
- FR-24: Reuse the Shared Core extraction/imaging/export/interop.
- FR-25: Reconcile movement detail vs expectedTransactions — CL-42..45, item 58.
- FR-26: Verify promotions are current — CL-49.
- FR-27: Phase-0 non-breaking isolation setup (no Solution 1 refactor in v1).

#### Tranche 2 — Regulatory Completeness (CONDUSEF Acuerdo, added 2026-06-17)

Derived from `docs/planning-artifacts/LAW-VS-CHECKLIST-GAP-2026-06-17.md`. Intra-statement, self-contained
(verify against the statement's own reported figures; see NFR-8).

- FR-28: Verify all 28 mandatory sections are present and in the fixed legal order, no inter-section blank gap >2 cm (§1–28).
- FR-29: Verify §6 "Cuánto pagarías por tus compras regulares" by recomputing months-to-pay & total interest from reported pago mínimo / tasa ordinaria / pago para no generar intereses (revolving-balance recursion).
- FR-30: Verify §19 "Saldo sobre el que se calcularon los intereses": per-row `monto ≈ saldo_base × (tasa/360) × días` over the 6 interest types.
- FR-31: Verify §20 "Distribución de tu último pago" 7-column waterfall identity.
- FR-32: Verify §8 "Indicadores del costo anual" (12-month interest/commission/annuity) presence & coherence.
- FR-33: Verify §16 "Información de otras líneas de crédito" (conditional 9-column table + arithmetic).
- FR-34: Verify mandatory verbatim text blocks — §26 (13 notas aclaratorias), §27 (15-term glosario), §24 (atención de quejas legend), §17 (art-6-IV mensajes adicionales legends), §11 (two exact URLs).
- FR-35: Verify conditional sections §23 "Cargos no reconocidos" (status enum) and §25 "Reestructura".
- FR-36: Verify the typography legal floor (≥8 pt Arial-equivalent; fecha límite ≥10 pt bold; the ~10 specific bold fields) — separate from the client Aptos brand rule (CL-35).
- FR-37: Verify advertising placement (no ads outside free sections; §12 mensajes importantes ≤700 chars; section size caps §17 ≤¼, §21/§28 ≤⅓ page).
- FR-38: Verify §18 "Programas de beneficios" structural completeness (all concepts incl. "0", CL-38) and §13 "crédito disponible para transferencia".
- FR-39: Multi-tenant rule profile — per-tenant selection of legal-baseline rules (default-on) + client-overlay rules + tolerance bands, as configuration with no per-bank hardcoding.

### NonFunctional Requirements

- NFR-1: Performance/throughput (≤10s p95/statement; ≥1 stmt/s/worker; sample within window).
- NFR-2: Accuracy (deterministic ≥99% P/R; false-positive <1%; no silent passes).
- NFR-3: Reliability (one failure never halts a batch; exception queue; resumable).
- NFR-4: Observability (per-job correlation, metrics: throughput/latency/verdict/exceptions).
- NFR-5: Determinism/reproducibility (same inputs → same Findings).
- NFR-6: Error handling (Result<T>; no exceptions for control flow).

### Additional Requirements (from Architecture)

- AR-1: New `ExxerCube.Prisma.Veriqan.*` projects mirroring the numbered layers (ADR-V1).
- AR-2: Separate `VeriqanDbContext` + own `veriqan` schema + own EF migrations (ADR-V5).
- AR-3: NetArchTest rule enforcing `Veriqan → Prisma` one-way dependency (created, not assumed).
- AR-4: v1 pure C# — PdfPig (fonts/geometry), PDFtoImage/EmguCV (render); no Python/GPU (ADR-V7).
- AR-5: Net-new packages: ZXing.NET (QR), perceptual-hash nuget (image presence).
- AR-6: `IVecValidationRule` engine; tolerances/thresholds as data (ADR-V2/V3).
- AR-7: `IVecReferenceDataProvider` + CSV/DB/API adapters against the JSON contract (ADR-V4).
- AR-8: v2 ML ports defined now (`IStatementMlExtractor`, `IVisualComplianceMl`); impl deferred (ADR-V8).
- AR-9: Append-only Disposition audit table; EngineVersion + ReferenceBundleVersion provenance.

### UX Design Requirements

No standalone UX spec yet. The QA Console (Epic 7) is the only UI surface; its interaction
requirements are captured inline in Epic 7 stories. `[NOTE: a UX spec for the QA Console can be
produced via bmad-ux if richer interaction design is wanted.]`

### FR Coverage Map

| Epic | FRs | NFRs / AR |
|---|---|---|
| E1 Foundation & Isolation | FR-23, FR-24, FR-27 | AR-1, AR-2, AR-3, AR-4, AR-5, NFR-6 |
| E2 Ingestion & Reference Data | FR-1, FR-2, FR-3, FR-19, FR-20 | AR-7, NFR-3 |
| E3 Field Extraction | FR-4, FR-5 | AR-4 |
| E4 Financial Consistency Engine | FR-6, FR-7, FR-8, FR-25 | AR-6, NFR-2, NFR-5 |
| E5 Visual & Print-Quality | FR-9, FR-10, FR-11, FR-12 | AR-4, AR-5, AR-8 |
| E6 Regulatory & Fiscal | FR-13, FR-14, FR-26 | AR-5 |
| E7 Findings, Reporting & QA Console | FR-15, FR-16, FR-17, FR-18 | AR-9 |
| E8 Batch & Observability | FR-21, FR-22 | NFR-1, NFR-3, NFR-4 |
| E9 Verdict Core & Traceability Seam | FR-39 (seam) | NFR-7, NFR-8 |
| E10 Structural & Textual Completeness | FR-28, FR-34, FR-35, FR-38 | — |
| E11 Computation Verification | FR-29, FR-30, FR-31, FR-32, FR-33 | NFR-8 |
| E12 Legal Form & Typography | FR-36, FR-37 | — |
| E13 Productization & Pipeline-Gate | FR-39 (product) | NFR-7 |

All FR-1…FR-27 covered. v2 deferrals: FR-12 full match+order, ML ports impl (AR-8).

**Tranche 2 — Regulatory Completeness (merged 2026-06-17):** FR-28…39 + NFR-7,8 covered by E9–E13. Design rationale & roundtable decisions: `epics-tranche2-regulatory.md`.

## Epic List

1. **E1 — Foundation & Isolation** (brownfield-safe scaffolding; gate Solution 1 safety)
2. **E2 — Ingestion & Reference Data** (jobs in, reference bundle bound, graceful degradation)
3. **E3 — Field Extraction** (statement → typed model)
4. **E4 — Financial Consistency Engine** (the deterministic core; highest $-value)
5. **E5 — Visual & Print-Quality Inspection**
6. **E6 — Regulatory & Fiscal Checks**
7. **E7 — Findings, Reporting & QA Console** (human-in-the-loop)
8. **E8 — Batch Processing & Observability** (scale to the sample)
9. **E9 — Compliance Verdict Core & Traceability Seam** (the seam every rule is authored against; first)
10. **E10 — Regulatory Structural & Textual Completeness** (28-section presence/order + verbatim legends; quick-win breadth)
11. **E11 — Regulatory Computation Verification** (recompute §6/§19/§20/§8/§16; the moat; table-extraction spike first)
12. **E12 — Legal Form & Typography** (font floor, bold fields, advertising/size caps; gated on E5)
13. **E13 — Multi-Tenant Productization & Pipeline-Gate** (embeddable gate + traceability export; roadmap-gated on issue #17)

---

## Epic 1: Foundation & Isolation

Stand up Veriqan as additive, isolated projects that cannot break Solution 1, with the dependency
rule mechanically enforced — before any feature code. Realizes FR-23, FR-24, FR-27.

### Story 1.1: Create the Veriqan project skeleton

As a Veriqan engineer,
I want the `ExxerCube.Prisma.Veriqan.*` projects created in the existing solution mirroring the
numbered layers,
So that VEC code has a home that reuses the Shared Core without touching Solution 1.

**Acceptance Criteria:**

**Given** the existing ExxerCube.Prisma solution
**When** the Veriqan.Domain/Application/Infrastructure.*/Orchestration/Worker projects are added
**Then** the solution builds green
**And** no Solution 1 project or file is modified
**And** Solution 1's full test suite passes unchanged (SM-5).

### Story 1.2: Enforce the one-way dependency with a NetArchTest rule

As a Veriqan engineer,
I want a NetArchTest rule asserting `Veriqan → Prisma` only,
So that an accidental `Prisma → Veriqan` reference fails the build.

**Acceptance Criteria:**

**Given** the Veriqan projects exist
**When** a test references a Prisma project from a Veriqan type (allowed) it passes
**And When** any Prisma project references a Veriqan type
**Then** the NetArchTest rule fails the build
**And** the existing cross-infra isolation guardrails still pass for Veriqan's multi-infra composition.

### Story 1.3: Add the separate VeriqanDbContext and schema

As a Veriqan engineer,
I want a separate `VeriqanDbContext` with its own `veriqan` schema and migrations,
So that persistence is additive and never alters the Solution 1 schema.

**Acceptance Criteria:**

**Given** the shared database
**When** Veriqan migrations are applied
**Then** only `veriqan`-schema tables are created/modified
**And** `PrismaDbContext` and its migrations are untouched
**And** both contexts coexist against the same database.

### Story 1.4: Register net-new packages and v1 compute libraries

As a Veriqan engineer,
I want ZXing.NET, a perceptual-hash package, and confirmed PdfPig/PDFtoImage usage wired in,
So that v1 runs pure C# with no Python/GPU.

**Acceptance Criteria:**

**Given** `Directory.Packages.props`
**When** the new packages are added
**Then** they are centrally versioned and restore cleanly
**And** a smoke test reads an embedded font name via PdfPig, renders a page via PDFtoImage, decodes a
sample QR via ZXing.NET, and computes a pHash — all in C#, no Python process.

---

## Epic 2: Ingestion & Reference Data

Get statements into the system and bind the reference data they are judged against, degrading
gracefully when data is missing. Realizes FR-1, FR-2, FR-3, FR-19, FR-20.

### Story 2.1: Ingest a statement and create a verification job

As an operations user,
I want to submit a statement PDF and get a verification job,
So that the statement enters the pipeline exactly once.

**Acceptance Criteria:**

**Given** a valid statement PDF
**When** it is submitted via the configured intake
**Then** exactly one VerificationJob is created with a unique id and received timestamp
**And** resubmitting the same content (by hash) does not create a duplicate
**And** a non-PDF/corrupt file is rejected with a typed `Result` error and no job.

### Story 2.2: Provide reference data through one contract with adapters

As a Veriqan engineer,
I want `IVecReferenceDataProvider` with a CSV (or DB) adapter emitting a schema-valid Reference
Bundle,
So that reference data can come from any source without engine changes.

**Acceptance Criteria:**

**Given** reference data in the first delivery mechanism
**When** the adapter produces a bundle
**Then** the bundle validates against the JSON schema
**And** an invalid bundle blocks the job with `BLOCKED` and a typed error
**And** adding a second adapter requires no validation-engine change.

### Story 2.3: Bind bundle and resolve product, degrading gracefully

As a QA analyst,
I want missing reference sections to surface as INSUFFICIENT_DATA rather than false fails,
So that partial data still yields a useful, honest result.

**Acceptance Criteria:**

**Given** a job whose bundle omits the TASA section
**When** verification runs
**Then** only rate-dependent Checks report INSUFFICIENT_DATA
**And** all other Checks still run
**And** an unresolvable product yields `BLOCKED` with `UNKNOWN_PRODUCT` (no silent default).

---

## Epic 3: Field Extraction

Turn a statement PDF into a typed model the engine can evaluate. Realizes FR-4, FR-5.

### Story 3.1: Extract header identity fields

As the validation engine,
I want client name (split), address (split), branch, card number, CLABE, client number, and RFC,
So that identity checks (CL-2..8) have data with locators.

**Acceptance Criteria:**

**Given** a text-layer statement
**When** header extraction runs
**Then** card number is 16 digits, CLABE 18 digits, RFC matches the pattern
**And** each field has a confidence score and a page/region locator
**And** a not-found field is reported as `not-extracted` with a locator hint (never blank-and-silent).

### Story 3.2: Extract period and summary fields

As the validation engine,
I want product, interest rate, CAT inputs, period/cut/limit dates, day count, and summary amounts,
So that period and arithmetic checks (CL-1, 9..16) have data.

**Acceptance Criteria:**

**Given** a text-layer statement
**When** period extraction runs
**Then** period parses into start and cut dates and day count equals the span
**And** the extracted rate is matched against the bundle TASA for product+period.

---

## Epic 4: Financial Consistency Engine

The deterministic, highest-value core: all arithmetic, cross-period, and transaction-detail
reconciliation, with tolerance bands. Realizes FR-6, FR-7, FR-8, FR-25. (NFR-2, NFR-5.)

### Story 4.1: Rule engine skeleton with technique class and provenance

As a Veriqan engineer,
I want `IVecValidationRule` + `VecValidationEngine` with DI rule discovery,
So that each checklist item is an independent, pure, testable rule.

**Acceptance Criteria:**

**Given** registered rules
**When** the engine runs a statement
**Then** each rule returns a `Finding` (Pass/Fail/InsufficientData) with expected, observed,
tolerance applied, severity, locator, and technique class
**And** re-running with identical inputs yields identical Findings (NFR-5)
**And** no rule throws for control flow (Result<T>, NFR-6).

### Story 4.2: Intra-statement arithmetic checks

As a QA analyst,
I want the resumen/nivel-de-uso math validated within tolerance,
So that internal inconsistencies are caught (CL-10, 18..26).

**Acceptance Criteria:**

**Given** an extracted statement and tolerance config
**When** the arithmetic rules run
**Then** "pago para no generar intereses" formula deviation > tolerance ⇒ FAIL (CL-21)
**And** category sums (cargos regulares/a meses/pagos y abonos) match within tolerance (CL-18..20)
**And** saldo deudor total, crédito disponible, and CAT are validated (CL-24, 25, 10)
**And** each Finding records the exact tolerance applied (FR-8).

### Story 4.3: Cross-period checks against the Prior Statement

As a QA analyst,
I want values that must match last month verified against the prior statement,
So that carry-over errors are caught (CL-17, 36..41).

**Acceptance Criteria:**

**Given** a bundle with prior-statement closing values
**When** cross-period rules run
**Then** "adeudo del periodo anterior" = prior "pago para no generar intereses" within tolerance (CL-17)
**And** rewards opening balance and points math validate (CL-36, 39); exchange rate = 0.1 (CL-37)
**And** installment saldo carry-over and número de pago increment validate (CL-40, 41)
**And** absent prior statement ⇒ these Checks emit INSUFFICIENT_DATA.

### Story 4.4: Movement-detail reconciliation

As a QA analyst,
I want the printed DESGLOSE reconciled against expectedTransactions,
So that altered/missing movements are caught (CL-42..45, item 58).

**Acceptance Criteria:**

**Given** a bundle with expectedTransactions
**When** reconciliation runs
**Then** each printed movement matches an expected transaction by normalized description (CL-45) and
amount within tolerance (item 58); unmatched either-direction ⇒ FAIL
**And** total de cargos/abonos equals the printed detail sum within tolerance (CL-44)
**And** every operation date is within the period (CL-42) and each desglose page states the range (CL-43)
**And** absent expectedTransactions ⇒ description/amount checks INSUFFICIENT_DATA; date checks still run.

---

## Epic 5: Visual & Print-Quality Inspection

Rule-based visual checks: pure-deterministic where possible, lightweight CV (no ML) for image
presence. Realizes FR-9, FR-10, FR-11, FR-12. ML ports defined for v2 (AR-8).

### Story 5.1: Embedded-font (Aptos) verification

As a QA analyst,
I want every text run's embedded font checked against Aptos,
So that non-standard fonts are flagged (CL-35).

**Acceptance Criteria:**

**Given** a statement with a non-Aptos embedded font run
**When** the font rule runs (PdfPig font dictionary, deterministic)
**Then** a FAIL is produced with the page/region locator
**And** an all-Aptos statement passes.

### Story 5.2: Text-overlap and header styling

As a QA analyst,
I want overlapping text and non-bold/upper headers flagged,
So that rendering defects are caught (CL-28, 29).

**Acceptance Criteria:**

**Given** glyph bounding boxes from PdfPig
**When** the overlap rule runs
**Then** overlap beyond the configured threshold ⇒ FAIL (CL-28)
**And** a section header not bold+uppercase ⇒ FAIL (CL-29)
**And** thresholds come from config, recorded on the Finding.

### Story 5.3: Pagination, blank pages, per-page elements

As a QA analyst,
I want pagination, blank pages, and per-page logo/card-number verified,
So that structural defects are caught (CL-31, 33, 34, 48).

**Acceptance Criteria:**

**Given** a rendered statement
**When** the layout rules run
**Then** a blank page ⇒ FAIL (CL-48); wrong pagination ⇒ FAIL (CL-31)
**And** a page missing the logo (presence) or card number ⇒ FAIL (CL-33, 34).

### Story 5.4: Catalog image presence (v1)

As a QA analyst,
I want card/important-message/sequential images detected as present via perceptual hashing,
So that missing required images are caught (CL-27, 30, 47 presence-only).

**Acceptance Criteria:**

**Given** a bundle with catalog image hashes
**When** the presence rule runs
**Then** absence of an expected image ⇒ FAIL (technique = lightweight-cv)
**And** the Finding notes presence-only (full match+order is v2)
**And** absent catalog data ⇒ INSUFFICIENT_DATA.

---

## Epic 6: Regulatory & Fiscal Checks

Legends, mandatory sections, fiscal/QR block, and promotion validity. Realizes FR-13, FR-14, FR-26.

### Story 6.1: Mandatory legends and "COMPARA TU TARJETA"

As a compliance owner,
I want required legends and the mandatory section verified present,
So that regulatory omissions are caught (CL-32, 46).

**Acceptance Criteria:**

**Given** a bundle with mandatoryLegends
**When** the rule runs
**Then** a missing legend ⇒ FAIL with its id (CL-46); missing "COMPARA TU TARJETA" ⇒ FAIL (CL-32)
**And** absent legends data ⇒ INSUFFICIENT_DATA.

### Story 6.2: Fiscal block and QR extraction

As a compliance owner,
I want the fiscal QR/code and issuer/receiver RFC extracted and validated,
So that fiscal representation is correct (CL-50..53).

**Acceptance Criteria:**

**Given** a statement carrying the fiscal block
**When** the fiscal rule runs (ZXing.NET)
**Then** a decodable QR yields the fiscal code; an unreadable required QR ⇒ FAIL (CL-50)
**And** fiscal code and issuer/receiver RFC are extracted and shape-validated (CL-51..53)
**And** absence on a statement without reembolsos/comisiones/IVA is not a FAIL.

### Story 6.3: Promotions currency

As a QA analyst,
I want promotional inserts checked against validity windows,
So that expired promotions are flagged (CL-49).

**Acceptance Criteria:**

**Given** a bundle with promotions validity windows
**When** the rule runs
**Then** a promotion whose insert appears but `validTo` precedes the period ⇒ FAIL
**And** absent promotions data ⇒ INSUFFICIENT_DATA.

---

## Epic 7: Findings, Reporting & QA Console

Aggregate, report, and let a human decide. Realizes FR-15, FR-16, FR-17, FR-18. (AR-9.)

### Story 7.1: Aggregate findings into a verdict

As a QA analyst,
I want a statement-level verdict rollup,
So that I can triage by severity.

**Acceptance Criteria:**

**Given** a job's Findings
**When** aggregation runs
**Then** any FAIL ⇒ RED; all PASS/n-a ⇒ GREEN; any blocking error ⇒ BLOCKED
**And** INSUFFICIENT_DATA Checks are reported separately and do not alone make RED.

### Story 7.2: Generate a color-marked PDF

As a QA analyst,
I want each finding highlighted on a copy of the statement,
So that I can see exactly where the problem is (CL-55).

**Acceptance Criteria:**

**Given** a job with FAIL findings
**When** the marked PDF is generated (PdfSharp)
**Then** every FAIL has a visible highlight at its locator
**And** original pages are preserved and the marked PDF is downloadable.

### Story 7.3: Email alert on RED statements

As a QA supervisor,
I want an email summary for flagged statements,
So that the team is notified (CL-54).

**Acceptance Criteria:**

**Given** a RED statement
**When** reporting completes
**Then** exactly one alert email is sent with verdict + finding summary
**And** dispatch failures are retried and logged, never silently dropped.

### Story 7.4: Human disposition with immutable audit

As a QA analyst,
I want to accept/reject each finding and disposition the statement,
So that VEC assists rather than auto-rejects (FR-18).

**Acceptance Criteria:**

**Given** a reviewed statement
**When** I disposition a finding or the statement
**Then** the decision records actor, timestamp, and before/after in an append-only audit table
**And** VEC issues no automated accept/reject without a human disposition in v1.

---

## Epic 8: Batch Processing & Observability

Process the monthly sample within the window, reliably and observably. Realizes FR-21, FR-22;
NFR-1, NFR-3, NFR-4.

### Story 8.1: Batch worker with bounded concurrency and exception queue

As an operations lead,
I want statements processed concurrently with failures isolated,
So that the sample completes without a single bad file halting it.

**Acceptance Criteria:**

**Given** a batch of statements
**When** the worker processes them
**Then** progress reports pending/in-progress/completed/blocked counts
**And** a failing statement moves to an exception queue with a typed error (batch continues, NFR-3)
**And** concurrency is bounded with backpressure (no unbounded fan-out).

### Story 8.2: Resume and reprocess idempotently

As an operations lead,
I want to resume an interrupted batch and reprocess a single statement,
So that recovery doesn't duplicate results.

**Acceptance Criteria:**

**Given** an interrupted batch
**When** it resumes
**Then** completed statements are not reprocessed
**And** explicitly reprocessing one statement replaces its prior result and is audit-logged.

### Story 8.3: Observability and throughput validation

As an operations lead,
I want per-job metrics and a measured throughput baseline,
So that the ≥1 stmt/s/worker target is validated, not assumed.

**Acceptance Criteria:**

**Given** the worker running a representative batch
**When** metrics are collected
**Then** throughput, latency (p95 ≤ 10s target), verdict distribution, and exception counts are emitted with correlation ids (NFR-4)
**And** the measured per-statement cost (parse + render + rules) is recorded to confirm or revise NFR-1.

---

<!-- Tranche 2 — Regulatory Completeness epics merged 2026-06-17. Design rationale/roundtable decisions: epics-tranche2-regulatory.md -->

## Epic 9: Compliance Verdict Core & Traceability Seam

> **✅ DONE — verified 2026-06-30 (orchestrator ground-truth pass, branch `Liv`).** All 6 stories
> (9.1–9.6) are implemented in production code and tested; this spec **predates** the code — the
> work landed incrementally during Epics 4–6 under the `Story 9.x` tags already in the source.
> Evidence: dual-verdict `RuleFinding.LegalBaselineVerdict`/`TenantProfileVerdict` + `InsufficientData`
> (9.1); `IVecValidationRule.DofNumeral` + queryable `VecValidationEngine.GetCoverageMap()` (9.2);
> `RuleClassification` + `TenantProfileResolver` config-time sub-legal refusal (9.3); typed range-bounded
> `Tolerance` + `DefaultLegalToleranceProvider` (9.4); `VerificationContext.ConfidenceBelowThreshold` +
> per-tenant `MinFieldConfidence` abstain (9.5); all **58 concrete rules** (47 validation + 11 visual)
> carry real DOF numerals + deliberate classifications, 0 on stubs (9.6). **Build-failing enforcement
> gates green:** `Veriqan.Orchestration.Tests/DofNumeralRegistryTests` 5/5 (empty-numeral +
> undefined-classification fail the build via real DI-container enumeration of both rule assemblies).
> Baselines: Validation.Tests 519/519, Application.Tests 151/151, build 0/0. Non-AC tails logged for
> downstream epics: (1) non-scalar confidence sentinel (deferred to Epic 6, `RuleFinding.cs:118`);
> (2) per-rule confidence threshold (currently per-tenant — AC says "rule/tenant", satisfied);
> (3) tolerance ceilings are `⚠️ ESTIMATED` pending legal sign-off (`DefaultLegalToleranceProvider.cs`).

The rule-evaluation contract every Tranche-2 rule (and the retrofitted E4/E5 rules) is authored against.
Realizes FR-39 (seam half), NFR-7, NFR-8. Drive it with two genuinely different real tenant profiles.

### Story 9.1: Dual verdict + abstain in the rule result contract

As a compliance engineer,
I want every rule evaluation to return a *legal-baseline* verdict and a *tenant-profile* verdict that are
separable, plus a first-class `InsufficientData` outcome,
So that a tenant override can never silently mask a breach of the CONDUSEF floor, and a low-confidence
input yields "cannot verify" rather than a false PASS/FAIL.

**Acceptance Criteria:**

**Given** a rule is evaluated
**When** it produces a finding
**Then** the result carries both a legal-baseline verdict and a tenant-profile verdict, independently readable
**And** `InsufficientData` is a distinct outcome from `Fail` and from `Pass`
**And** the result is a `Result<T>` (no exceptions) and is deterministic for identical inputs (NFR-5, NFR-8)
**And** existing rules compile against the extended contract with no behavioural change (default tenant = legal baseline).

### Story 9.2: Require the DOF Acuerdo numeral as rule metadata

As a compliance engineer,
I want every `IVecValidationRule` to declare the exact DOF *Acuerdo* numeral / *Guía de Llenado* field it enforces,
So that findings produce an auditable rule→numeral evidence chain (NFR-7) a bank can hand CONDUSEF.

**Acceptance Criteria:**

**Given** the rule registry
**When** the solution is built (or an architecture test runs)
**Then** any rule missing a non-empty DOF numeral citation fails the build/test
**And** each finding exposes its cited numeral
**And** the citation is queryable to emit a coverage map (consumed later by E13's traceability export).

### Story 9.3: Tenant rule profile with rule classification and sub-legal refusal

As a product owner onboarding a new bank,
I want a tenant profile = legal baseline ⊕ client overlay, where each rule is classified
**baseline-locked / tenant-tightenable-only / tenant-overridable**,
So that personalization can add or tighten checks but a sub-legal *loosening* is refused or surfaced as a
"tenant deviation from law", never a silent pass.

**Acceptance Criteria:**

**Given** a tenant overlay that tightens a `tenant-tightenable-only` rule
**When** the profile is resolved
**Then** the tightened threshold applies and both verdicts still compute
**And Given** an overlay that loosens a `baseline-locked` rule or pushes a rule below the legal floor
**When** the profile is validated at configuration time
**Then** configuration is rejected with a clear `Result` failure (or the deviation is flagged loudly, per owner ruling)
**And** the legal-baseline verdict is always evaluable regardless of overlay.

### Story 9.4: Typed, range-bounded per-rule tolerances with legal defaults

As a compliance engineer,
I want tolerances modelled as typed, per-rule, range-bounded values with a legal-default,
So that tolerance is a property of the computation (from the law's own rounding math), not a free-form
tenant key-value bag where silent compliance holes breed.

**Acceptance Criteria:**

**Given** a computation rule with a legal-default tolerance
**When** no tenant override is set
**Then** the legal-default applies
**And When** a tenant override is within the rule's permitted range
**Then** it applies; **and When** outside the range or below the legal floor **Then** it is rejected at config time
**And** the existing Epic-4 tolerance-band behaviour (CL-10 pp-space, CL-20 abonos) is preserved.

### Story 9.5: Drive abstain from per-field extraction confidence

As a compliance engineer,
I want the verdict to consume a per-field extraction-confidence signal from the StatementModel,
So that a computation rule abstains (`InsufficientData`) when an input field it needs is low-confidence,
preventing a false "bank non-compliant" verdict (critical for the preventive pipeline-gate).

**Acceptance Criteria:**

**Given** a rule needs a field whose extraction confidence is below the configured threshold
**When** the rule evaluates
**Then** it returns `InsufficientData` citing the field, not `Fail`
**And Given** all needed fields are high-confidence
**Then** the rule evaluates normally
**And** the confidence threshold is part of the rule/tenant configuration.

### Story 9.6: Retrofit the existing E4/E5 rules onto the seam

As a compliance engineer,
I want the ~24 already-implemented rules (CL-10/17–26/36–45/Item58; visual CL-28/29/35) migrated to the
dual-verdict + classification + numeral + tolerance contract,
So that the whole rule set is uniform before ~30 new rules are authored against it.

**Acceptance Criteria:**

**Given** the existing rules
**When** migrated
**Then** each carries its DOF numeral, classification, and typed tolerance
**And** their existing tests stay green (no behavioural regression)
**And** each emits the dual verdict and can abstain on low-confidence inputs.

## Epic 10: Regulatory Structural & Textual Completeness

> **✅ DONE — verified + tail-closed 2026-06-30 (orchestrator pass, branch `Liv`).** Like Epic 9,
> the bulk of Epic 10 was already built (spec predates code): 10.1 real geometry segmenter
> (`PdfPigStatementFieldExtractor.ExtractDetectedSections`, 28-anchor table, page+bbox); 10.2 order +
> PdfPig blank-gap >2cm (`SectionOrderAndGapRule` + `ComputeSectionGaps`, `SectionGap.TwoCmInPoints`);
> **10.3 shared normalize+match primitive DONE** (`VecTextNormalizer` + `VecTextMatcher`
> Levenshtein/Jaccard + `VerbatimBlockMatcher`); 10.4 verbatim blocks with exact mandated counts
> (§26=13, §27=15) + similarity score; 10.5 conditional N/A semantics + `DisputeStatus` enum; 10.6
> §18/§13 completeness incl. explicit-0. Baselines: Validation.Tests 531/531, Application 151/151,
> Orchestration 122/122, build 0/0.
>
> **Tail closed this pass:** (a) **S10.3 threshold now tenant-configurable** — `ResolveThreshold(ctx)`
> was a `_ = ctx;` stub returning the 0.82 constant; wired to `TenantProfile.VerbatimSimilarityThreshold`
> (commit `1f0de6a3`). (b) **Two accepted limitations documented (owner ruling 2026-06-30 "trigger =
> heading"):** §25 restructure trigger is heading-derived, so an account that *omits* §25 entirely
> can't be flagged; and §23 row-status is checked at section level — a per-row `Unknown` check would be
> **dead code** because `ExtractDisputeRows` keys rows off the 3 valid tokens (an `Unknown` row is
> unreachable). Catching an invalid/absent-status row needs status-token-independent row detection =
> a deferred extraction spike. Both noted in `Section25ReestructuraRule`/`Section23CargosNoReconocidosRule`.
> **Not done (deliberate):** CL-38 UI label / `ChecklistIds.cs` relabel — owner-gated checklist backlog.

Confirm every legally-required section is present, in the fixed order, with the exact mandated wording.
Realizes FR-28, FR-34, FR-35, FR-38. All rules authored against the Epic-9 seam.

### Story 10.1: Detect presence of the 28 mandatory sections

As a compliance engineer,
I want each statement segmented and every mandatory CONDUSEF section (§1–28) detected by its heading/anchor,
So that a missing mandatory section is reported as a finding citing the section numeral.

**Acceptance Criteria:**

**Given** a statement
**When** section detection runs
**Then** each mandatory section is located (with its page + bounding region) or reported missing
**And** conditional sections (§16, §23, §25) are marked not-applicable rather than missing when their trigger is absent
**And** each finding cites its §-numeral (NFR-7) and abstains when the text layer is unreadable (NFR-8).

### Story 10.2: Verify fixed section order and no blank gap >2 cm

As a compliance engineer,
I want the detected sections checked for the legally fixed order and for inter-section blank gaps,
So that reordered sections or oversized blank gaps (> 2 cm) are flagged per the *guía de llenado*.

**Acceptance Criteria:**

**Given** the sections located in Story 10.1
**When** order/spacing is evaluated
**Then** any section out of the mandated sequence is a finding
**And** any inter-section vertical blank gap exceeding 2 cm is a finding (measured via PdfPig geometry)
**And** the rule abstains if section boundaries could not be established.

### Story 10.3: Verbatim-text normalization and tolerant matcher

As a compliance engineer,
I want a shared text-normalization + similarity-matching primitive (Unicode NFC, whitespace/soft-hyphen
collapse, ligature folding) with a configurable per-use threshold,
So that legally-fixed wording can be matched without false failures from PDF draw-order, accents, or wrapping.

**Acceptance Criteria:**

**Given** an extracted text fragment and an expected verbatim string
**When** compared through the matcher
**Then** comparison is on normalized forms (NFC + collapsed whitespace + stripped soft-hyphens/ligatures)
**And** a similarity score (e.g. token-ratio/Levenshtein) is returned and compared to a configured threshold
**And** exact-equality is NOT used; the threshold is a parameter (tenant/rule-configurable per Epic 9).

### Story 10.4: Verify mandatory verbatim text blocks

As a compliance engineer,
I want the legally-fixed text blocks verified using the Story-10.3 matcher,
So that missing or altered mandatory wording is flagged.

**Acceptance Criteria:**

**Given** a statement
**When** the verbatim-text rules run
**Then** the §26 thirteen *notas aclaratorias*, §27 fifteen *glosario* terms, §24 *atención de quejas* legend,
§17 art-6-IV *mensajes adicionales* legends, and §11 two URLs are each matched above threshold or flagged
**And** each finding cites its numeral and reports the similarity score
**And** the rule abstains when the host section was not detected (defers to Story 10.1).

### Story 10.5: Verify conditional sections and unrecognized-charge status

As a compliance engineer,
I want §23 *Cargos no reconocidos* and §25 *Reestructura* verified when applicable,
So that, when present, §23 rows carry a valid status from the mandated enum and §25 is present when the debt was restructured.

**Acceptance Criteria:**

**Given** a statement containing §23
**When** the rule runs
**Then** each unrecognized-charge row has a status in {pendiente-en-revisión, concluida-procedente, concluida-improcedente}, else a finding
**And Given** the statement indicates a restructured debt **Then** §25 must be present, else a finding
**And** when neither trigger is present the rules return not-applicable, not Fail.

### Story 10.6: Verify benefit-program and card-usage completeness

As a compliance engineer,
I want §18 *Programas de beneficios* and §13 *Nivel de uso* checked for structural completeness,
So that all mandated benefit concepts are shown (including "0") and the card-usage fields are complete.

**Acceptance Criteria:**

**Given** a statement with a benefit program (§18)
**When** the rule runs
**Then** every mandated concept (saldo inicial, generados, redimidos/utilizados, vencidos, por vencer, saldo final, unidad+equivalencia en pesos, contacto) is present, including explicit "0" values (CL-38)
**And Given** §13 **Then** *crédito disponible para transferencia de saldo de otras tarjetas* is present when applicable
**And** each finding cites its numeral; the rule abstains if the section text is unreadable.

## Epic 11: Regulatory Computation Verification

> **✅ DONE — verified 2026-06-30 (orchestrator ground-truth pass, branch `Liv`).** Like Epics 9 & 10,
> the whole epic was already built ahead of this spec (work landed during Epics 4–6). All 6 stories are
> real, DI-registered (Scrutor scan of `IVecValidationRule`), tolerance-aware
> (`ILegalToleranceProvider` + tenant tightening), and green — verified via build + the fixture-driven
> suites, not prose:
> - **11.1 structured table extraction** — `PdfPigStatementFieldExtractor.ExtractFinancialTables`
>   fans out to `ExtractSection8/19/20/16/6Table` → `IReadOnlyList<FinancialTable>` on
>   `StatementModel.FinancialTables`; typed `TableCell` with per-cell `Confidence` + `CellKind`
>   (Amount/Rate/Days/NotApplicable/ParseFailure/Missing) and abstain contract
>   (`FinancialTable.NotFound/Indeterminate/NoRows`). Fixture-driven `FinancialTableExtractionTests`.
> - **11.2 §20 waterfall** — `Section20PaymentDistributionRule` (`LAW-§20-WATERFALL`): 7-column identity
>   `pagosYAbonos = Σ(components) − saldoAFavor`, both sign conventions, per-cell confidence gate.
> - **11.3 §19 per-row interest** — `Section19InterestPerRowRule` (`LAW-§19-INTERES`):
>   `monto ≈ saldoBase × (tasa/360) × días` per row + §10 ordinary-rate cross-check.
> - **11.4 §6 recursion** — `Section6PaymentSimulationRule` (`LAW-§6-SIMULACION`): revolving-balance
>   recursion, scenarios k∈{1,2,5}, months ±1 + pre-IVA interest, IVA from provider (0.16).
> - **11.5 §8 indicators** — `Section8AnnualCostIndicatorsRule` (`LAW-§8-INDICADORES`): presence +
>   non-negative on the 3 indicators.
> - **11.6 §16 other lines** — `Section16OtherCreditLinesRule` (`LAW-§16-OTRASLINEAS`): per-row
>   interest + IVA reconcile + §16-total-vs-§19 tie; not-applicable when §16 absent.
>
> Baselines: build 0/0, Extraction.Tests **187/187**, Validation.Tests **531/531**, Orchestration.Tests
> **122/122** (the `DofNumeralRegistryTests` gate proves all 5 Section rules carry DofNumeral +
> Classification and are wired).
>
> **Deliberately NOT built — corpus-gated, owner ruling (same class as Epic 10's deferred spikes):**
> the residual is **calibration, not code**, and building it now would *violate* this epic's own rule
> ("a misread digit must never produce a false non-compliant verdict") absent a ground-truth corpus to
> validate against. Logged: (a) **§6 has no fixture** — §6 is absent from all 3 PRP2 Dummie VEC PDFs, so
> `ExtractSection6Table` returns `NotFound` and the rule abstains; end-to-end unverified until a real §6
> specimen exists. (b) **§20 saldo-a-favor sign convention** `TODO(corpus)` — logic already handles both
> signs; only calibration pending. (c) **§16 column-map** calibration guard (abstains if observed column
> count ≠ expected map). (d) **§8 derivable-coherence** cross-check (beyond presence) — tolerance stored
> "for future use"; deferred pending corpus. All four are abstain-safe today (never false-Fail).

Recompute the law's mandated calculations from the statement's OWN reported figures and confirm the
printed values reconcile. Realizes FR-29, FR-30, FR-31, FR-32, FR-33; NFR-8. All rules abstain (Epic 9)
on low-confidence inputs — a misread digit must never produce a false "bank non-compliant" verdict.

> **Prerequisites baked into this epic:** (a) Story 11.1 delivers the table-row extraction the recompute
> rules depend on (the largest hidden cost — a spike, not a one-line rule); (b) a **ground-truth corpus**
> of real/representative statements (start from the PRP2 `01/02/03 Dummie VEC` fixtures + any real
> samples) with known-good and deliberately-broken computed values, so rules are validated beyond
> synthetic round-trips. §6 references Banxico **Circular 13/2011** (pago-mínimo method) and the Acuerdo's
> revolving-balance recursion; recompute from reported figures and abstain if a needed input is absent.

### Story 11.1: Extract structured financial table rows and cells

As a compliance engineer,
I want the §19 / §20 / §8 / §16 financial grids reconstructed from PdfPig geometry into typed rows and
cells with per-cell extraction confidence,
So that the recompute rules have structured numeric inputs (not free text) and can abstain on low confidence.

**Acceptance Criteria:**

**Given** a statement containing the financial-summary tables
**When** table extraction runs
**Then** each target table is returned as ordered rows of typed cells (labels + decimal amounts + rates/days where present)
**And** each cell carries an extraction-confidence value usable by Epic-9 abstain logic
**And** column/row association is validated against the ground-truth corpus fixtures
**And** a table that cannot be reconstructed yields `InsufficientData`, not partial/guessed rows.

### Story 11.2: Verify §20 "Distribución de tu último pago" waterfall

As a compliance engineer,
I want the §20 7-column payment-distribution identity recomputed,
So that a statement whose last-payment breakdown does not reconcile is flagged. (Cheapest, highest-confidence identity — first.)

**Acceptance Criteria:**

**Given** the §20 row values from Story 11.1
**When** the rule evaluates
**Then** it confirms pagos y abonos = Σ(cargos regulares + compras a meses s/i + compras a meses c/i + intereses y comisiones + IVA) − saldo a favor, within the rule's typed tolerance
**And** a mismatch beyond tolerance is a Fail citing §20; **and** missing/low-confidence cells yield `InsufficientData`.

### Story 11.3: Verify §19 per-row interest identity

As a compliance engineer,
I want each row of §19 "Saldo sobre el que se calcularon los intereses" recomputed,
So that an interest amount inconsistent with its own base/rate/days is flagged across the 6 interest types.

**Acceptance Criteria:**

**Given** a §19 row with saldo base, núm. días, tasa anual, monto
**When** the rule evaluates
**Then** it confirms `monto ≈ saldo_base × (tasa/360) × días` within typed tolerance, per row
**And** the §19 ordinary-rate row equals the §10 "Tasa de interés anual ordinaria" value
**And** rows marked NA are skipped; low-confidence rows yield `InsufficientData`; mismatches cite §19.

### Story 11.4: Verify §6 payment-simulation recursion

As a compliance engineer,
I want the §6 "Cuánto pagarías" table recomputed via the Acuerdo's revolving-balance recursion,
So that the printed months-to-pay and total-interest for the pago-mínimo / 2× / 5× scenarios are confirmed.

**Acceptance Criteria:**

**Given** the reported pago mínimo, ordinary rate, IVA rate, and "pago para no generar intereses"
**When** the recursion is run for k ∈ {1,2,5} until the revolving balance reaches zero
**Then** the computed months and summed ordinary interest match the printed §6 columns within tolerance
**And** the NA/"Este periodo" cases (zero or ≤ pago mínimo) are handled per the guía
**And** if a required input (rate/method) is indeterminable the rule returns `InsufficientData`, citing §6 / Circular 13/2011.

### Story 11.5: Verify §8 annual-cost indicators

As a compliance engineer,
I want the §8 12-month indicators (interest / commissions / annuity) sanity-checked for presence and coherence,
So that absent or internally inconsistent annual-cost indicators are flagged.

**Acceptance Criteria:**

**Given** §8
**When** the rule evaluates
**Then** the three indicators are present and non-negative, and (where derivable) coherent with reported period figures within tolerance
**And** missing indicators are a Fail citing §8; unreadable values yield `InsufficientData`.

### Story 11.6: Verify §16 other credit lines

As a compliance engineer,
I want the conditional §16 "Información de otras líneas de crédito" table verified when present,
So that the per-disposition figures (saldo pendiente, intereses, IVA, pago requerido) reconcile.

**Acceptance Criteria:**

**Given** a statement that includes §16
**When** the rule evaluates
**Then** per-row arithmetic (interest vs rate/days, IVA vs interest) reconciles within tolerance and totals tie to the relevant summary fields
**And** when §16 is not applicable the rule returns not-applicable; mismatches cite §16; low-confidence rows yield `InsufficientData`.

## Epic 12: Legal Form & Typography

Verify the document's *form* obeys the law — typography floor, mandated bold fields, advertising
placement, section size caps. Realizes FR-36, FR-37. **Gated on Epic 5 (visual/print-quality) finishing,
since it reuses PdfPig geometry + render infra.** Kept separate from the client Aptos brand rule (CL-35),
which is a tenant overlay (Epic 9), not the legal floor.

### Story 12.1: Verify the typography point-size floor

As a compliance engineer,
I want text point sizes verified against the legal minimum,
So that statements below the ≥8 pt Arial-equivalent floor (or whose *fecha límite de pago* is below ≥10 pt) are flagged.

**Acceptance Criteria:**

**Given** a statement's text with PdfPig point sizes (accounting for CTM scale)
**When** the rule evaluates
**Then** body text below the ≥8 pt floor is a finding, and the *fecha límite de pago* below ≥10 pt is a finding
**And** the rule cites the *guía* numeral and abstains (`InsufficientData`) where the rendered size cannot be determined reliably.

### Story 12.2: Verify the mandated bold fields (heuristic, abstain-safe)

As a compliance engineer,
I want the ~10 legally-bold fields checked,
So that a field the law requires in negrillas that is not bold is flagged — without manufacturing false fails when weight is indeterminate.

**Acceptance Criteria:**

**Given** the fields the Acuerdo mandates bold (fecha límite, pago para no generar intereses, pago mín+compras a meses, adeudo del periodo anterior, saldo deudor total, total cargos/abonos, tasa ordinaria, CAT, last-page fiscal block)
**When** the rule evaluates bold via font-name/weight heuristics
**Then** a confidently non-bold mandated field is a Fail citing its numeral
**And** when font weight is indeterminate (subset-embedded/mangled font names) the rule returns `InsufficientData`, never Fail.

### Story 12.3: Verify advertising placement

As a compliance engineer,
I want advertising restricted to the legally permitted free sections,
So that ads outside §21/§28/página-cero, or advertising/over-length text in §12 *Mensajes importantes*, are flagged.

**Acceptance Criteria:**

**Given** a statement
**When** the rule evaluates
**Then** §12 *Mensajes importantes* exceeding 700 characters or containing advertising is a finding
**And** advertising detected outside the permitted free sections is a finding citing the relevant numeral
**And** the rule abstains where section boundaries (Epic 10) are unavailable.

### Story 12.4: Verify section size caps

As a compliance engineer,
I want the legally bounded sections measured,
So that §17 exceeding ¼ page, or §21/§28 exceeding ⅓ page, is flagged.

**Acceptance Criteria:**

**Given** the located sections (Epic 10) and page geometry
**When** the rule evaluates
**Then** §17 occupying more than ¼ of a page is a finding, and §21/§28 occupying more than ⅓ of a page is a finding
**And** each finding cites its numeral; the rule abstains if the section area cannot be measured.

## Epic 13: Multi-Tenant Productization & Pipeline-Gate

The "sell to many banks" surface. **ROADMAP-GATED: do not build until the economic buyer is identified
(GitHub issue #17 — business discovery).** Realizes FR-39 (product half); NFR-7. Stories defined now so the
seam (Epic 9) is built compatibly, but execution waits on the gate.

### Story 13.1: Embeddable preventive pipeline-gate (library mode)

As a bank's statement-generation pipeline,
I want to invoke Veriqan as an embeddable gate that fails the build/batch when a legal-baseline violation is found,
So that a non-compliant statement cannot be emitted — making verification a preventive (not detective) control.

**Acceptance Criteria:**

**Given** Veriqan is invoked as a library/gate in a generation pipeline
**When** a statement is evaluated
**Then** a legal-baseline Fail returns a non-zero/blocking result, an `InsufficientData` returns a distinct non-blocking-but-flagged result (never a false block), and a Pass is non-blocking
**And** batch-mode QC (non-blocking report) remains available as an alternative invocation
**And** behaviour is deterministic and the result carries the cited numerals (NFR-5, NFR-7).

### Story 13.2: Per-tenant profile management and onboarding-as-configuration

As a product owner,
I want to onboard a new bank by configuring a tenant profile (overlay + tolerances + enabled client checks),
So that a new customer is added by configuration, not by code — keeping it a product, not a consultancy.

**Acceptance Criteria:**

**Given** the Epic-9 tenant profile model
**When** a new tenant is onboarded
**Then** its profile (overlay, typed tolerances, enabled client-layer checks) is created without code changes
**And** sub-legal loosening is refused at configuration time (Epic 9)
**And** two genuinely different tenant profiles produce correctly different effective rule sets over the same statement.

### Story 13.3: Compliance traceability-matrix export

As a bank compliance officer,
I want an exportable numeral-by-numeral compliance report,
So that I can hand CONDUSEF an auditable evidence chain (e.g. in a *Programa de Cumplimiento Forzoso*) and feed the coverage deck.

**Acceptance Criteria:**

**Given** a verified statement (or batch)
**When** the traceability matrix is exported
**Then** every evaluated rule is listed with its DOF *Acuerdo* numeral, verdict (legal + tenant), and evidence locator
**And** the export states the engine version and the Acuerdo version it was evaluated against
**And** the matrix is reproducible for identical inputs.

### Story 13.4: Regulatory-version provenance

As a compliance engineer,
I want every verdict stamped with engine + reference-bundle + Acuerdo version,
So that results remain auditable across regulatory amendments and rule-set changes.

**Acceptance Criteria:**

**Given** a verdict is produced
**When** it is persisted/exported
**Then** it records EngineVersion, ReferenceBundleVersion, and the Acuerdo edition/date applied
**And** re-running an older statement against a newer engine is detectable via the stamped versions.
</content>

---

## v2 / Deferred (not in this breakdown's MVP)

- FR-12 full catalog match + order (CLIP); ML extraction (LayoutLMv3) behind AR-8 ports.
- OCR path for scanned/image-only statements.
- Automated quality gate (pass/fail without human).
- Non-credit-card statement families.
- Full-population (100% coverage) scale-out.
- Deferred Phase-0 genericization of `FusionExpedienteService` (only if a shared need emerges).
