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

All FR-1…FR-27 covered. v2 deferrals: FR-12 full match+order, ML ports impl (AR-8).

## Epic List

1. **E1 — Foundation & Isolation** (brownfield-safe scaffolding; gate Solution 1 safety)
2. **E2 — Ingestion & Reference Data** (jobs in, reference bundle bound, graceful degradation)
3. **E3 — Field Extraction** (statement → typed model)
4. **E4 — Financial Consistency Engine** (the deterministic core; highest $-value)
5. **E5 — Visual & Print-Quality Inspection**
6. **E6 — Regulatory & Fiscal Checks**
7. **E7 — Findings, Reporting & QA Console** (human-in-the-loop)
8. **E8 — Batch Processing & Observability** (scale to the sample)

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

## v2 / Deferred (not in this breakdown's MVP)

- FR-12 full catalog match + order (CLIP); ML extraction (LayoutLMv3) behind AR-8 ports.
- OCR path for scanned/image-only statements.
- Automated quality gate (pass/fail without human).
- Non-credit-card statement families.
- Full-population (100% coverage) scale-out.
- Deferred Phase-0 genericization of `FusionExpedienteService` (only if a shared need emerges).
