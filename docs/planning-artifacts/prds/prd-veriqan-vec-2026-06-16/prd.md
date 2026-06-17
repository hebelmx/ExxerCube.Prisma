---
title: Veriqan VEC — Bank Statement Quality Verifier
status: final
created: 2026-06-16
updated: 2026-06-16
---

# PRD: Veriqan VEC — Bank Statement Quality Verifier
*Working title — confirm.*

## 0. Document Purpose

This PRD is for the PM (hebelmx), the architect, and the downstream BMAD workflows
(`bmad-create-architecture`, `bmad-create-epics-and-stories`). It specifies **Veriqan VEC**, a
bank credit-card statement quality verifier, built as an **additive module of the existing
ExxerCube.Prisma platform** (Solution 1 = "Atención a Autoridades" / oficio processing). It is
vocabulary-anchored by the Glossary (§3), groups capabilities into Features (§4) with globally
numbered Functional Requirements, isolates cross-cutting quality in NFRs (§10), and tags every
inferred decision inline as `[ASSUMPTION]` (indexed in §15). Technology choices (specific ML
models, libraries, interop) live in `addendum.md`, not here. The authoritative behavioral source
of truth is the Excel checklist `Prisma/Fixtures/PRP2/Check+list+demo+v2+Iqubica.xlsx` (55 items);
this PRD references those items as **CL-1 … CL-55**. Supporting analysis lives in
`Prisma/Fixtures/PRP2/REUSE-VS-STANDALONE-RECOMMENDATION.md` and the reference-data contract in
`Prisma/Fixtures/PRP2/reference-data/`.

## 1. Vision

Mexican banks are legally and reputationally obligated to ensure every customer statement is
correct — arithmetic, customer data, fonts, images, legends, fiscal codes. Today this quality gate
is **manual and sampled**: a person spends 2–4 hours per statement, so only a tiny fraction of the
13–20 million monthly statements can ever be checked. Errors that slip through become customer
complaints, regulatory findings, and remediation cost.

**Veriqan VEC** turns that manual gate into an automated one. It ingests a statement PDF plus the
reference data it should agree with (interest rates, client/account master data, the prior month's
closing balances, image catalogs, mandatory legends), runs the bank's 55-point checklist, and
produces a **findings report** — a color-marked PDF and an email alert — that a bank QA analyst
reviews and acts on. VEC **assists** the human; it does not auto-reject.

By starting with deterministic checks (all arithmetic, embedded-font verification, layout geometry)
plus basic image-presence detection, VEC delivers reliable value in weeks rather than waiting on
fragile ML, while reusing ~50–60% of the proven ExxerCube.Prisma extraction/imaging/export
infrastructure. It raises sampled coverage dramatically and lays the path to 100% coverage.

## 2. Target User

### 2.1 Jobs To Be Done

- **Bank Statement QA Analyst** — "Tell me which statements in this sample have problems and
  exactly where, so I can review and dispose of them in minutes instead of hours."
- **QA Supervisor / Compliance Owner** — "Give me confidence that every sampled statement was
  checked against the full checklist consistently, with an audit trail I can show the regulator."
- **Operations Lead** — "Process the monthly sample within the review window without adding
  headcount, and show me the throughput and exception backlog."
- **Veriqan Product/Eng (internal)** — "Add new checks and reference-data sources without touching
  Solution 1, and without breaking it."

### 2.2 Non-Users (v1)

- The **end banking customer** never interacts with VEC (it operates behind the QA process).
- **Non-credit-card statement types** (Vector brokerage / Casa de Bolsa statements, debit, loans)
  are out of scope for v1. `[ASSUMPTION: v1 is credit-card statements only, per the Excel.]`
- **Statement authoring/generation systems** — VEC verifies, it does not produce statements.

### 2.3 Key User Journeys

- **UJ-1. Lucía clears the monthly sample before the SLA clock runs out.**
  - **Persona + context:** Lucía is a Statement QA Analyst; this month's 1% sample landed in her
    queue and she has a 3–5 business-day window.
  - **Entry state:** authenticated to the Veriqan QA console; the monthly batch has been ingested.
  - **Path:** she opens the queue sorted by severity → picks a statement flagged `RED` → sees the
    color-marked PDF with each finding highlighted and a side panel listing CL-item, expected vs.
    found, and tolerance → confirms two real errors and dismisses one false positive.
  - **Climax:** she dispositions the statement (accept finding / reject finding) in ~2 minutes
    instead of 2–4 hours; the decision is recorded with her identity and timestamp.
  - **Resolution:** the statement moves to "reviewed"; the queue count drops; the audit trail
    captures her disposition.
  - **Edge case:** if reference data for that product's interest rate was missing, the affected
    checks show `INSUFFICIENT_DATA` (not a false fail) and she routes it to the data team.

- **UJ-2. Marco proves coverage to the regulator.**
  - Marco, Compliance Owner, exports the month's verification ledger — every sampled statement, the
    55 checks run, verdicts, dispositions, and who decided — as an immutable, retained report.

- **UJ-3. Sofía (internal eng) adds a new reference-data source.**
  - The bank starts delivering interest rates by API instead of file. Sofía writes one adapter that
    emits the existing JSON reference bundle; no validation-engine or Solution 1 code changes.

## 3. Glossary

- **VEC** — *Verificación de Estado de Cuenta*. The bank's statement quality-gate process and, by
  extension, this product's domain.
- **Veriqan** — the product/module name for VEC inside ExxerCube.Prisma. Namespace
  `ExxerCube.Prisma.Veriqan.*`.
- **Statement** — one credit-card account statement (a multi-page PDF) for one client/account for
  one Period.
- **Period** — the statement's billing cycle, bounded by a start date and a cut date.
- **Prior Statement** — the immediately preceding Period's statement for the same account; source
  of cross-period expected values.
- **Checklist Item (CL-N)** — one of the 55 numbered rules in the source-of-truth Excel.
- **Check** — an automated evaluation of one Checklist Item against a Statement, yielding a Finding.
- **Finding** — the result of a Check: a verdict (`PASS` / `FAIL` / `INSUFFICIENT_DATA`), a
  severity, the expected and observed values, the tolerance applied, and a page/region locator.
- **Verdict** — statement-level rollup of all Findings: `GREEN` (no fails), `RED` (one or more
  fails), or `BLOCKED` (could not be evaluated).
- **Tolerance Band** — the allowed deviation for a numeric Check (e.g. ±$0.50 MXN, ±1.00 point).
- **Reference Bundle** — the JSON document (see `reference-data/`) carrying all reference data for a
  Statement: product catalog, interest rates (TASA), client/account master data, prior-statement
  closing values, image catalogs, mandatory legends, promotions, tolerance config.
- **Reference Adapter** — a component that produces a Reference Bundle from a specific delivery
  mechanism (CSV, database, API, manual).
- **Disposition** — a human QA analyst's decision on a Finding or Statement (accept / reject /
  escalate), recorded with identity and timestamp.
- **Marked PDF** — the input Statement PDF re-rendered with each Finding highlighted in color.
- **Solution 1** — the existing oficio / Atención a Autoridades product in ExxerCube.Prisma.
- **Shared Core** — ExxerCube.Prisma infrastructure reused by both Solution 1 and Veriqan
  (extraction, imaging, export, storage, events, Python interop, `Result<T>`).

## 4. Features

### 4.1 Statement Ingestion & Context Binding

**Description:** A Statement PDF enters VEC (via folder/queue/API) and is bound to its Reference
Bundle for the same client/account/Period. The system identifies the product type, resolves it to a
canonical `productId`, and confirms which reference sections are available — driving graceful
degradation downstream. Realizes UJ-1.

**Functional Requirements:**

#### FR-1: Ingest a statement for verification

The system can accept a Statement PDF through a configured intake (folder watch, queue message, or
API call) and create a verification job.

**Consequences (testable):**
- A valid PDF produces exactly one verification job with a unique id and `received` timestamp.
- A non-PDF or corrupt file is rejected with a typed error and does not create a job.
- Ingestion is idempotent: the same file content (by hash) does not create duplicate jobs.

#### FR-2: Bind a Reference Bundle to a statement

The system can resolve and attach the Reference Bundle for the job's client/account/Period via a
Reference Adapter.

**Consequences (testable):**
- The bound bundle validates against the `reference-data` JSON schema; an invalid bundle blocks the
  job with `BLOCKED` and a typed error.
- When a bundle section is absent, the job proceeds and the dependent Checks emit
  `INSUFFICIENT_DATA` — never `FAIL`. Realizes UJ-1 edge case.

#### FR-3: Detect product type and resolve to canonical product

The system can determine the statement's product and map it (including aliases) to a `productId`.

**Consequences (testable):**
- A statement naming "Tarjeta de Crédito NL" resolves to `productId = TC-NL`.
- An unresolvable product yields `BLOCKED` with reason `UNKNOWN_PRODUCT`, not silent defaults.

### 4.2 Field Extraction (Carátula / Header)

**Description:** VEC extracts the header/identity and summary fields from page 1 and the
period/balance sections (CL-1 … CL-16), normalizing Mexican formats (peso amounts, `DD/MMM/YYYY`
dates, RFC, CLABE, 16-digit card number). Extraction is deterministic-first (PDF text layer +
pattern/fuzzy matching reusing the Shared Core `IFieldExtractor` pattern), with OCR fallback for
image-only PDFs. Realizes UJ-1.

**Functional Requirements:**

#### FR-4: Extract header identity fields

[QA Analyst] receives extracted client name (split into names/surnames), address (split into
components), branch number, card number, CLABE, client number, and RFC. Realizes CL-2…CL-8.

**Consequences (testable):**
- Card number is captured as 16 digits; CLABE as 18 digits; RFC matches the RFC pattern.
- Each extracted field carries a confidence score and a page/region locator.
- Fields not found are reported as `not-extracted` with a locator hint, never blank-and-silent.

#### FR-5: Extract period & summary fields

The system extracts product name, interest rate (resolved from TASA), CAT inputs, Period
start/cut/limit dates, day count, and the summary amounts (CL-1, CL-9…CL-16).

**Consequences (testable):**
- Period parses into start date and cut date; day count equals the date span.
- Extracted interest rate is matched against the Reference Bundle TASA for that product+period.

**Notes:** `[NOTE FOR PM] Extraction accuracy for image-only (scanned) statements depends on OCR;
v1 targets digitally-generated PDFs with a real text layer as the primary case.`

### 4.3 Financial Consistency Validation Engine

**Description:** The deterministic heart of VEC. Each numeric Checklist Item is a rule that compares
extracted values, computed values, reference values, and cross-section/cross-period values within
its Tolerance Band, producing a Finding. This covers the "RESUMEN DE CARGOS Y ABONOS", "NIVEL DE
USO", rewards points, and installment math (CL-10, CL-15…CL-26, CL-36…CL-44). All values are pure
arithmetic — no ML. Realizes UJ-1.

**Functional Requirements:**

#### FR-6: Evaluate intra-statement arithmetic checks

The system computes and verifies each balance/total formula against the printed value within
tolerance.

**Consequences (testable):**
- "Pago para no generar intereses" equals (adeudo anterior + cargos regulares + cargos a meses +
  intereses + comisiones + IVA − pagos y abonos); deviation > $0.50 MXN ⇒ `FAIL` (CL-21).
- Sum of detailed regular operations equals "Cargos regulares (no a meses)" within tolerance
  (CL-18); analogous for cargos a meses (CL-19) and pagos y abonos (CL-20).
- "Saldo deudor total" = saldo regulares + saldo a meses; "Crédito disponible" = límite − saldo
  deudor total (CL-24, CL-25).
- CAT is computed per the formula and compared within tolerance (CL-10).

#### FR-7: Evaluate cross-period checks against the Prior Statement

The system verifies values that must agree with the prior month's closing values.

**Consequences (testable):**
- "Adeudo del periodo anterior" equals Prior Statement "pago para no generar intereses" within
  tolerance (CL-17).
- Rewards opening balance (points & pesos) equals Prior Statement closing balance (CL-36); points
  math `Saldo Total = inicial + generados − redimidos − vencidos` holds (CL-39).
- Installment "Saldo pendiente" = prior saldo − pago requerido; "Número de pago" increments by one
  (CL-40, CL-41).
- If no Prior Statement is in the bundle, these Checks emit `INSUFFICIENT_DATA`.

#### FR-8: Apply configurable Tolerance Bands

Every numeric Check reads its tolerance from the Reference Bundle `toleranceConfig`.

**Consequences (testable):**
- Changing `currencyToleranceMxn` changes pass/fail boundaries without code changes.
- Each Finding records the tolerance value it applied.

#### FR-25: Reconcile the movement detail against expected transactions

The system reconciles the printed "DESGLOSE DE MOVIMIENTOS DEL PERIODO" against the Reference
Bundle `expectedTransactions`, by description and amount, and verifies date-range integrity.
Realizes CL-42, CL-43, CL-44, CL-45, and item 58.

**Consequences (testable):**
- Every printed movement matches an expected transaction by normalized description (CL-45) and by
  amount within tolerance (item 58); unmatched movements (either direction) produce a `FAIL`.
- "Total de cargos" and "Total de abonos" equal the sum of the printed detail within tolerance
  (CL-44).
- Every operation's date falls within the Period range (CL-42); each desglose page states the
  Period date range (CL-43).
- If `expectedTransactions` is absent, description/amount reconciliation emits `INSUFFICIENT_DATA`;
  the date-range checks (CL-42, CL-43) still run from the statement alone.

### 4.4 Visual & Print-Quality Inspection

**Description:** VEC inspects rendering quality and layout. **v1 uses rule-based techniques — pure
deterministic where possible, lightweight computer vision (no learned/ML models) where the PDF
forces it.** Deterministic: embedded-font verification (Aptos, from the font dictionary),
text-overlap geometry, header styling, pagination, blank-page detection. Lightweight-CV (threshold-
based, no learned models): logo-present-on-every-page and card/important-message **image presence**
via perceptual hashing/template match. **v2** adds catalog-order matching and CLIP-based (ML) visual
compliance. The technique class of each Check is recorded on its Finding. Realizes UJ-1.

**Functional Requirements:**

#### FR-9: Verify embedded font compliance (Aptos)

The system verifies every text run's embedded font family is **Aptos** (the canonical bank font).

**Consequences (testable):**
- A text run in a non-Aptos embedded font produces a `FAIL` Finding locating the page/region
  (CL-35).
- Font family is read from the PDF font dictionary (deterministic), not inferred visually.

#### FR-10: Detect overlapping text and header styling

The system detects overlapping text regions and verifies section headers are bold + uppercase.

**Consequences (testable):**
- Two text bounding boxes whose overlap exceeds a configured threshold produce a `FAIL` (CL-28).
- A section header not bold-and-uppercase produces a `FAIL` (CL-29).

#### FR-11: Verify pagination, blank pages, and per-page elements

The system verifies correct pagination, absence of blank pages, and that the bank logo and card
number appear on every page.

**Consequences (testable):**
- A page with no rendered content is flagged `FAIL` (CL-48); incorrect page numbering ⇒ `FAIL`
  (CL-31).
- A page missing the logo (by presence detection) or card number ⇒ `FAIL` (CL-33, CL-34).

#### FR-12: Verify presence of catalog images (v1) / order & match (v2)

The system verifies that the card image, important-messages image, and sequential images are
**present** (v1, perceptual-hash presence) and, in v2, match the catalog **in the correct order**.

**Consequences (testable):**
- v1: absence of an expected card/important-message/sequential image ⇒ `FAIL` (CL-27, CL-30, CL-47
  presence-only).
- v2 `[NON-GOAL for MVP]`: wrong image vs. product catalog or wrong order ⇒ `FAIL` (full CL-27,
  CL-30, CL-47).

**Feature-specific NFRs / Notes:**
- All visual thresholds (overlap ratio, header-styling rules, pHash distance) are sourced from
  config/the Reference Bundle, recorded on each Finding — same discipline as FR-8 tolerances. No
  magic numbers in code.
- Each Check records its **technique class** (`deterministic` | `lightweight-cv` | `ml`). v1 emits
  only the first two; `ml` appears in v2.
- `[NOTE FOR PM] Lightweight-CV checks (logo/image presence) are threshold-based, not exact; their
  false-positive contribution is tracked against SM-C1. CL-27/30/47 are PRESENCE-ONLY in v1 — full
  catalog match+order is explicitly v2.`

**Description:** VEC verifies mandatory legends, the regulatory "COMPARA TU TARJETA" section, and
extracts/validates the fiscal block (QR code, fiscal code, issuer/receiver RFC) when present
(CL-32, CL-46, CL-50…CL-53). Realizes UJ-2.

**Functional Requirements:**

#### FR-13: Verify mandatory legends and regulatory sections

The system verifies that every legend in the Reference Bundle `mandatoryLegends` is present and that
the mandatory "COMPARA TU TARJETA" section exists.

**Consequences (testable):**
- A missing mandatory legend ⇒ `FAIL` with the legend id (CL-46).
- Missing "COMPARA TU TARJETA" ⇒ `FAIL` (CL-32).

#### FR-14: Extract and validate the fiscal block

When the statement carries the "representación impresa" fiscal block, the system reads the QR code
and extracts fiscal code, issuer RFC, and receiver RFC.

**Consequences (testable):**
- A decodable QR yields the fiscal code; an unreadable QR on a statement that should have one ⇒
  `FAIL` (CL-50).
- The fiscal code is extracted as a discrete field and shape-validated; absence when required ⇒
  `FAIL` (CL-51).
- Issuer/receiver RFC are extracted and shape-validated (CL-52, CL-53).
- `[ASSUMPTION: the fiscal block is only present on statements with reembolsos/comisiones/IVA, per
  the Excel note; absence on others is not a FAIL.]`

#### FR-26: Verify promotions are current

The system verifies that promotional inserts in the statement are valid for the Period against the
Reference Bundle `promotions` validity windows. Realizes CL-49.

**Consequences (testable):**
- A promotion whose insert appears but whose `validTo` precedes the Period ⇒ `FAIL` (expired
  promotion).
- If `promotions` is absent from the bundle, this Check emits `INSUFFICIENT_DATA`.

### 4.6 Findings, Reporting & Human-in-the-Loop Disposition

**Description:** VEC aggregates Findings into a statement Verdict, generates a Marked PDF and an
email alert, and presents findings to a QA Analyst who dispositions them. VEC never auto-rejects.
Realizes UJ-1, UJ-2.

**Functional Requirements:**

#### FR-15: Aggregate findings into a statement verdict

The system rolls all Findings into a Verdict (`GREEN` / `RED` / `BLOCKED`) with counts by severity.

**Consequences (testable):**
- Any `FAIL` ⇒ `RED`; all `PASS`/not-applicable ⇒ `GREEN`; any blocking error ⇒ `BLOCKED`.
- `INSUFFICIENT_DATA` Checks are reported separately and do not by themselves make a `RED`.

#### FR-16: Generate a color-marked PDF

The system produces a copy of the Statement PDF with each Finding highlighted at its locator
(CL-55).

**Consequences (testable):**
- Every `FAIL` Finding has a visible highlight anchored to the correct page/region.
- The marked PDF preserves the original pages and is downloadable from the QA console.

#### FR-17: Send a findings email alert

The system emails a configured recipient a summary of findings for `RED` statements (CL-54).

**Consequences (testable):**
- A `RED` statement triggers exactly one alert email containing the verdict and finding summary.
- Alert dispatch failures are retried and logged; never silently dropped.

#### FR-18: Disposition findings (human-in-the-loop)

[QA Analyst] can accept or reject each Finding and disposition the Statement; decisions are recorded
with identity and timestamp. Realizes UJ-1.

**Consequences (testable):**
- A disposition records actor, timestamp, and before/after state immutably.
- VEC issues no automated accept/reject of a Statement without a human disposition in v1.

### 4.7 Reference-Data Management

**Description:** The flexible JSON Reference Bundle contract plus pluggable Reference Adapters
(CSV / database / API / manual) that all emit the same bundle. Realizes UJ-3.

**Functional Requirements:**

#### FR-19: Provide reference data via a single contract

The system exposes one provider interface returning a schema-valid Reference Bundle regardless of
source.

**Consequences (testable):**
- A CSV adapter and a database adapter both produce bundles that pass the same schema validation.
- Adding a new adapter requires no change to the validation engine or Solution 1.

#### FR-20: Degrade gracefully on partial reference data

The system marks Checks dependent on absent reference sections as `INSUFFICIENT_DATA` and continues.

**Consequences (testable):**
- Removing the TASA section causes only rate-dependent Checks to become `INSUFFICIENT_DATA`; all
  other Checks still run.

### 4.8 Batch Processing & Throughput

**Description:** VEC processes the monthly sample as a batch within the QA review window, with
queueing, parallelism, retry, and an exception queue. Realizes UJ-1, the Operations Lead JTBD.

**Functional Requirements:**

#### FR-21: Process statements in batch with bounded concurrency

The system processes a batch of statements concurrently and reports progress.

**Consequences (testable):**
- A batch reports counts of pending/in-progress/completed/blocked.
- Failures move to an exception queue with the typed error; they do not halt the batch.

#### FR-22: Resume and reprocess

The system can resume an interrupted batch and reprocess individual statements without duplicating
results.

**Consequences (testable):**
- Re-running a completed statement replaces its prior result and is recorded in the audit trail.

### 4.9 Brownfield Integration & Compatibility

**Description:** Veriqan is an additive module that depends on the Shared Core one-directionally and
must not change Solution 1 behavior. It may extend shared abstractions only in backward-compatible
ways. Realizes the internal-eng JTBD.

**Functional Requirements:**

#### FR-23: Additive, non-breaking integration

Veriqan introduces no breaking change to Solution 1 code, data, or behavior.

**Consequences (testable):**
- Dependency direction is `Veriqan → Prisma` only; an architecture test fails the build on a
  `Prisma → Veriqan` reference.
- Solution 1's existing test suite passes unchanged after Veriqan is added.
- Shared-abstraction changes are additive (new members/overloads/generics), verified by Solution 1
  regression tests.

#### FR-24: Reuse the Shared Core extraction/imaging/export/interop

Veriqan builds on the existing extraction, imaging, export, storage, events, and Python-interop
infrastructure rather than duplicating it.

**Consequences (testable):**
- VEC field extraction uses the existing `IFieldExtractor` pattern; image quality uses the existing
  analyzer; Python ML uses the existing CSnakes interop pattern.

#### FR-27: Phase-0 non-breaking isolation setup

Before VEC features are built, Veriqan is stood up as isolated, non-breaking projects. **v1 does NOT
refactor or genericize Solution 1 code** — the architecture review established that VEC v1 is
single-source verification and does not need the `FusionExpedienteService` reconciliation pattern, so
that high-risk refactor is removed from the v1 path (deferred until a real shared need emerges).

**Consequences (testable):**
- The `Veriqan.*` projects and a separate `VeriqanDbContext` (own `veriqan` schema/migrations) are
  added with no modification to Solution 1 code or schema.
- A new architecture test (NetArchTest) asserts the `Veriqan → Prisma` one-way dependency and fails
  the build on violation; this test is created in this phase, not assumed to exist.
- Solution 1's full regression suite passes unchanged before and after (a hard gate, SM-5).

**Out of Scope:**
- Genericizing `FusionExpedienteService` / the orchestrator into shared seams — deferred to a later,
  separately-gated additive change, only if a shared need appears.

**Notes:** `[NOTE FOR PM] The existing PRP2 VEC scaffolding (python module + Infrastructure.Python.VecExtraction)
is treated as an untrusted prototype (~5–15% real). Salvageable parts (image_quality.py,
font_detector.py, model_cache.py, CSnakes wrapper, pinned requirements) are reused; the rest is
rebuilt against this PRD. See decision #5.`

## 5. Non-Goals (Explicit)

- VEC does **not** generate, correct, or re-issue statements — it only verifies them.
- VEC does **not** auto-reject statements in v1 (human-in-the-loop only).
- VEC does **not** cover non-credit-card statement types (brokerage/Vector, debit, loans) in v1.
- VEC does **not** modify Solution 1 or share a writable dependency back into it.
- VEC does **not** own the reference-data systems of record; it consumes them via adapters.
- VEC is **not** a fraud-detection or transaction-legitimacy system; it checks document correctness.

## 6. MVP Scope

### 6.1 In Scope (v1)

- Ingestion, product detection, Reference Bundle binding (FR-1…FR-3).
- Deterministic header/period field extraction for digitally-generated PDFs (FR-4, FR-5).
- The full **financial consistency engine** — all arithmetic and cross-period checks (FR-6…FR-8).
- **Deterministic** visual/print-quality: Aptos font, overlap, header styling, pagination,
  blank-page, logo/card-number per page (FR-9…FR-11).
- **Image-presence** checks via perceptual hashing (FR-12 v1 path).
- Mandatory legends, "COMPARA TU TARJETA", and fiscal/QR extraction (FR-13, FR-14).
- Findings aggregation, Marked PDF, email alert, human disposition (FR-15…FR-18).
- Reference-data contract + at least one adapter (CSV or DB) (FR-19, FR-20).
- Batch processing for sampled volume with audit trail (FR-21, FR-22).
- Additive brownfield integration (FR-23, FR-24).

### 6.2 Out of Scope for MVP (→ v2+)

- **Catalog-order image matching and CLIP-based visual compliance** (FR-12 v2 path) — needs model
  fine-tuning; `[NOTE FOR PM] this is the visually "smart" differentiator; revisit once deterministic
  v1 is in production.`
- **OCR-heavy image-only statements** as the primary path — v1 targets text-layer PDFs.
- **Automated quality gate** (pass/fail without human) — pending proven accuracy.
- **Non-credit-card statement families.**
- **100% coverage scale-out** — v1 is sized for the sampled volume; full-population throughput is a
  later hardening phase.

## 7. Success Metrics

**Primary**
- **SM-1 — Checklist evaluability (two honest numbers, not one).**
  (a) *Implemented coverage*: % of the 55 CL items for which a Check exists and runs in v1 — target
  **100% of in-scope items** (the v2 deferrals in §6.2 are excluded by definition, not hidden).
  (b) *Effective evaluation rate*: of the Checks that ran, % that produced `PASS`/`FAIL` rather than
  `INSUFFICIENT_DATA`. This number is **gated by reference-data availability (OQ-2/OQ-3)** and is
  reported, not targeted, until that data is confirmed — `INSUFFICIENT_DATA` is surfaced as a data
  gap, never counted as coverage. Validates FR-6…FR-14, FR-25, FR-26.
- **SM-2 — Analyst handling time.** Median minutes to disposition a flagged statement. Target ≤ 5
  min (vs. 2–4 hr manual). Validates FR-15…FR-18.
- **SM-3 — Detection accuracy on the arithmetic checks.** Precision/recall vs. a labeled set.
  Target ≥ 99% on deterministic checks. Validates FR-6, FR-7.

**Secondary**
- **SM-4 — Batch completion within window.** % of monthly sample verified within the 3–5 business-day
  SLA. Target 100%. Validates FR-21, FR-22.
- **SM-5 — Zero Solution 1 regressions.** Solution 1 test pass rate after each Veriqan release.
  Target 100%. Validates FR-23.

**Counter-metrics (do not optimize)**
- **SM-C1 — False-positive rate.** % of `FAIL` Findings the analyst rejects as not real. Keep < 1%;
  do **not** chase coverage (SM-1) by emitting speculative fails. Counterbalances SM-1.
- **SM-C2 — Silent passes.** Checks that report `PASS` without truly evaluating (e.g. masking
  missing data as pass). Must be 0; counterbalances SM-2/SM-4 (speed must not hide gaps).

## 8. Open Questions

1. **Sample size & cadence** — actual % and volume per month, and the exact SLA window (docs say 1%
   / 3–5 business days; client has not confirmed).
2. **Reference-data delivery** — which mechanism does the client actually use first (CSV? DB? API?),
   and ownership of each catalog (TASA, images, legends, prior statement).
3. **Image catalog sourcing** — how are reference card/message/sequential/promo images delivered and
   keyed (needed even for v1 presence checks).
4. **Statement PDF nature** — are production statements digitally-generated (text layer) or scanned
   images? Drives extraction strategy and OCR investment.
5. **Accuracy acceptance** — confirm the precision/recall bar and how the labeled validation set is
   produced.
6. **Notification targets** — who receives alerts; channel (email only, or also dashboard/queue).
7. **Product naming** — `ExxerCube.Prisma.Veriqan` vs `ExxerCube.Veriqan` final form.

## 9. Assumptions Index

- §2.1 / §6 — v1 is credit-card statements only (Excel scope).
- §4.2 — v1 primary case is digitally-generated PDFs with a real text layer; scanned/OCR is fallback.
- §4.5 — fiscal block present only on statements with reembolsos/comisiones/IVA; absence elsewhere
  is not a FAIL.
- §10 — sampled volume (≈1% of 13–20M/month) is the v1 design point; full-population is later.
- §4.9 — existing PRP2 scaffolding is an untrusted prototype; salvage selectively, rebuild the rest.

## 10. Cross-Cutting NFRs

- **NFR-1 Performance/Throughput.** Targets (assumption-tagged pending OQ-1, but concrete so
  architecture can size to them):
  - *Latency*: a single text-layer statement completes deterministic + lightweight-CV verification in
    **≤ 10 s** (p95), excluding any v2 ML.
  - *Sustained throughput*: design for **≥ 1 statement/second sustained per worker**, scaling
    horizontally; the monthly sample design point is **≈130k–200k statements/month (~26k–40k/day
    peak)** assuming 1% of 13–20M.
  - *Window*: the full monthly sample completes within the QA review window (3–5 business days)
    with headroom for reprocessing.
  - *Concurrency*: bounded worker pool with backpressure; no unbounded fan-out.
  `[ASSUMPTION: volume, peak shape, and window confirmed per OQ-1; targets revised on real numbers.]`
- **NFR-2 Accuracy.** Deterministic checks ≥ 99% precision/recall; false-positive rate < 1%
  (SM-C1). No silent passes (SM-C2).
- **NFR-3 Reliability.** A single statement failure never halts a batch; all failures land in the
  exception queue with typed errors; processing is resumable.
- **NFR-4 Observability.** Every job, Check, and Finding is logged with correlation ids and metrics
  (throughput, latency, verdict distribution, exception counts), reusing Shared Core logging/metrics.
- **NFR-5 Determinism/Reproducibility.** Re-running a statement with the same inputs yields the same
  Findings (critical for audit and for the deterministic-first philosophy).
- **NFR-6 Error handling.** All operations use the Shared Core `Result<T>` pattern; no exceptions for
  control flow.

## 11. Compliance & Regulatory

- **CNBV / banking supervision.** Verification ledger and dispositions must be exportable for
  regulatory review (UJ-2).
- **Fiscal (CFDI/timbrado).** Fiscal block extraction (FR-14) must read the regulated QR/fiscal
  codes accurately.
- **PII handling.** Statements contain client PII (name, address, RFC, card number, CLABE).
  Card numbers are masked in logs and UI by default; full values appear only where a Check requires
  and access is authorized. `[NOTE FOR PM] confirm PCI-DSS scope for storing card numbers; prefer
  masked-at-rest unless a Check provably needs the full PAN.`
- **Retention.** Verification artifacts (findings, marked PDFs, ledger) retained per the bank's
  policy (docs cite 7 years). See §13.

## 12. Operational Requirements

- **SLA.** Monthly sample verified within the QA review window (3–5 business days, confirm OQ-1).
- **Exception handling.** Blocked/failed statements are queued, visible, and re-drivable.
- **Deployment posture.** Veriqan is a module in the repo but **independently deployable**; the ML
  components (v2) run as a separate (GPU-capable) service. `[ASSUMPTION: deterministic v1 needs no
  GPU.]`
- **Support/on-call.** `[NOTE FOR PM] define support tier and on-call for the monthly batch window.]`

## 13. Data Governance

- **Classification.** Statements and reference data are confidential PII; access-controlled.
- **Residency/sovereignty.** `[ASSUMPTION: data stays in the bank's jurisdiction; confirm hosting.]`
- **Retention & disposal.** Retain verification artifacts per policy (≈7 years), then disposed on
  schedule; reference bundles versioned by Period.
- **Reuse boundary.** Veriqan data is isolated from Solution 1 data (additive tables only, FR-23).

## 14. Audit Trail / Decision Provenance

- Every Check records inputs, expected vs. observed, tolerance, verdict, and engine version.
- Every human Disposition records actor identity, timestamp, and before/after state, immutably.
- The monthly verification ledger is exportable and immutable (UJ-2), reusing Shared Core audit
  logging where possible.

## 15. Risks & Mitigations

- **R1 — Reference data never arrives / arrives messy.** Mitigation: own the JSON contract + adapters
  + graceful degradation, so VEC delivers value on whatever subset exists (FR-19, FR-20).
- **R2 — Salvaging the untrusted prototype costs more than rebuilding.** Mitigation: treat it as
  prototype; salvage only verified-good files; rebuild against this PRD (decision #5).
- **R3 — Visual ML accuracy disappoints.** Mitigation: deterministic-first; ML deferred to v2 and
  gated on a measured accuracy bar (SM-3, NFR-2).
- **R4 — Brownfield change breaks Solution 1.** Mitigation: one-directional dependency enforced by
  architecture test; Solution 1 regression suite as a release gate (FR-23, SM-5).
- **R5 — Scanned/image-only statements degrade extraction.** Mitigation: v1 scopes to text-layer
  PDFs; OCR investment sequenced after volume/nature is confirmed (OQ-4).
- **R6 — Scale to 100% coverage.** Mitigation: v1 sized for sample; throughput hardening is an
  explicit later phase, not an MVP promise.

## 16. ROI / Business Case (indicative)

- Manual cost: 2–4 hours/statement; even the sampled volume is far beyond manual capacity.
- VEC target: seconds/statement deterministic + minutes of human disposition only on flagged
  statements → order-of-magnitude labor reduction on the sample, with a path to 100% coverage that
  is infeasible manually. `[NOTE FOR PM] the docs cite very large annual-savings figures that are
  internally inconsistent; restate against the confirmed sample size (OQ-1) before quoting.]`

## 17. Checklist → FR Coverage Map

Every one of the 55 source-of-truth checklist items (CL-N) is mapped to a Functional Requirement.
"v1 partial" means the item is addressed at presence level in v1 with full match/order deferred to
v2. Infrastructure FRs (FR-1/2/3 ingestion, FR-8 tolerance, FR-15 verdict, FR-18 disposition,
FR-19/20 reference data, FR-21/22 batch, FR-23/24/27 brownfield) are cross-cutting and not tied to a
single CL.

| CL items | Capability | FR | v1 state |
|---|---|---|---|
| CL-1, 9, 11, 12, 13, 14 | Product/period/rate/date header extraction | FR-5 | Full |
| CL-2, 3, 4, 5, 6, 7, 8 | Identity field extraction (name/addr/branch/card/CLABE/client#/RFC) | FR-4 | Full |
| CL-10 | CAT computation | FR-6 | Full |
| CL-15, 16 | Pago-para-no-generar / pago-mínimo extraction + sum | FR-5, FR-6 | Full |
| CL-18, 19, 20, 21, 22, 23, 24, 25, 26 | Intra-statement arithmetic (resumen / nivel de uso) | FR-6 | Full |
| CL-17 | Adeudo anterior vs prior statement | FR-7 | Full (needs prior data) |
| CL-36, 37, 38, 39 | Rewards points balance, exchange rate, completeness, math | FR-7 | Full (needs prior data) |
| CL-40, 41 | Installment saldo carry-over & número de pago | FR-7 | Full (needs prior data) |
| CL-42, 43, 44, 45, item 58 | Movement-detail reconciliation (dates, totals, descriptions, amounts) | FR-25 | Full (needs expectedTransactions) |
| CL-35 | Font is Aptos | FR-9 | Full (deterministic) |
| CL-28, 29 | Text overlap, header styling | FR-10 | Full (deterministic) |
| CL-31, 33, 34, 48 | Pagination, logo/card per page, blank pages | FR-11 | Full (det. + light-CV) |
| CL-27, 30, 47 | Card / important-message / sequential images | FR-12 | v1 partial (presence) → v2 match+order |
| CL-32 | "COMPARA TU TARJETA" mandatory section | FR-13 | Full |
| CL-46 | Mandatory legends present | FR-13 | Full (needs legends data) |
| CL-49 | Promotions current | FR-26 | Full (needs promotions data) |
| CL-50, 51, 52, 53 | QR / fiscal code / issuer RFC / receiver RFC | FR-14 | Full (text-layer; scan-fragile) |
| CL-54 | Email alert on findings | FR-17 | Full |
| CL-55 | Color-marked PDF of findings | FR-16 | Full |

**Coverage:** all 55 CL items + item 58 have an FR home. v2 deferrals are limited to the full
match+order semantics of CL-27/30/47 (presence is delivered in v1).
