# RC0a — Intended-Scope Register
## Veriqan VEC · Readiness Challenge Phase 0a

**Date:** 2026-06-18 · **Branch:** `Liv` · **Status:** Phase 0a deliverable (read-only; no production code touched)

---

## 1. Authoritative Sources

| Role | File | Precedence |
|------|------|-----------|
| FR/NFR canonical source | `docs/planning-artifacts/epics.md` (Requirements Inventory section) | **Primary** — FR-1..39, NFR-1..6 listed verbatim; NFR-7/NFR-8 defined inline by cross-references to E9/E11 (no separate enumeration found) |
| Epic ACs (E1–E13) | `docs/planning-artifacts/epics.md` (Epic/Story sections) | **Primary** |
| Tranche-2 epic design + NFR-7/NFR-8 context | `docs/planning-artifacts/epics-tranche2-regulatory.md` | **Primary** (supplement; stages E9–E13 and explicit NFR-7/8 prose) |
| Product intent | `docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/prd.md` | **Primary** |
| Tech choices & reuse mapping | `docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/addendum.md` | Supporting |
| Readiness dimensions | `docs/planning-artifacts/READINESS-CHALLENGE-BRIEF-2026-06-18.md` (§2) | Dimension taxonomy used here |
| Gap context | `docs/planning/gap-analysis/GAP-MATRIX-2026-06-11.md` | Context (Prisma MVP, not Veriqan-specific) |
| MVP scope decisions | `docs/planning/gap-analysis/MVP-DEFINITION-2026-06.md` | Context |
| PRD reconciliation | `docs/planning/gap-analysis/PRD-RECONCILIATION-2026-06.md` | Context |
| Law-vs-checklist gaps | `docs/planning-artifacts/LAW-VS-CHECKLIST-GAP-2026-06-17.md` | Input to FR-28..39 |
| Corpus calibration intent | `docs/planning-artifacts/CORPUS-CALIBRATION-HARNESS-DESIGN-2026-06-18.md` | Informs corpus-gated tags |
| Missions (Prisma Solution-1 history) | `docs/planning/missions/` | Not authoritative for Veriqan scope; used only to confirm Solution-1 isolation boundary |

**NFR-7 / NFR-8 source confirmation:** These IDs appear in `epics.md` coverage map and story ACs but have no enumerated prose definition separate from their usage. NFR-7 = *DOF Acuerdo numeral traceability: every rule finding must cite its exact DOF Acuerdo / Guía de Llenado numeral, enabling an auditable evidence chain for regulatory handoff.* NFR-8 = *Intra-statement self-containment & abstain-safety: computation rules verify from the statement's own reported figures; rules must abstain (`InsufficientData`) on low-confidence inputs rather than produce a false FAIL.* Both definitions derived from `epics-tranche2-regulatory.md` §E9 and `epics.md` §Epic-9 story ACs.

---

## 2. Numbered Requirement Register

> **Dimension key** (from Brief §2): D1 = Functional Correctness on Real Data · D2 = End-to-End Deployable Composition · D3 = Multi-tenancy (E13) · D4 = Ingestion · D5 = Persistence & Data · D6 = Security & Compliance Posture · D7 = Operational Readiness · D8 = Performance / Scale / SLA · D9 = Failure Modes
>
> **Dependency-tag key**: `technical` = engineering we can do now · `E13-gated` = blocked on issue #17 buyer discovery · `corpus-gated` = needs a real CONDUSEF-statement corpus for calibration · `business-gated` = needs a human/business/data decision not yet made

### 2.1 Functional Requirements (FR-1..39)

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| FR-1 | Ingest a statement PDF via configured intake (folder/queue/API), create an idempotent verification job (hash-dedup), reject non-PDF/corrupt files with a typed Result error. | epics.md §FR-1; prd.md §4.1/FR-1 | D4, D2 | technical |
| FR-2 | Bind a schema-validated Reference Bundle to each job via a Reference Adapter; invalid bundle → BLOCKED; missing section → INSUFFICIENT_DATA on dependent checks. | epics.md §FR-2; prd.md §4.1/FR-2 | D4, D5 | technical |
| FR-3 | Detect product type from statement content and resolve to canonical `productId`; unresolvable product → BLOCKED with reason UNKNOWN_PRODUCT (no silent defaults). | epics.md §FR-3; prd.md §4.1/FR-3 | D1, D4 | technical |
| FR-4 | Extract header identity fields (client name, address, branch, card#, CLABE, client#, RFC) with confidence scores and page/region locators; missing field = `not-extracted`, never blank-and-silent. Realizes CL-2..8. | epics.md §FR-4; prd.md §4.2/FR-4 | D1 | technical |
| FR-5 | Extract period and summary fields (product, rate, CAT inputs, period/cut/limit dates, day count, summary amounts); day count must equal date span; rate matched against bundle TASA. Realizes CL-1, 9..16. | epics.md §FR-5; prd.md §4.2/FR-5 | D1 | technical |
| FR-6 | Evaluate all intra-statement arithmetic checks (CL-10, 18..26): resumen/nivel-de-uso formula validation within configurable tolerance bands; each Finding records the exact tolerance applied. | epics.md §FR-6; prd.md §4.3/FR-6 | D1 | technical |
| FR-7 | Evaluate cross-period checks against Prior Statement values (CL-17, 36..41): adeudo anterior, rewards balances, installment carry-over; absent prior statement → INSUFFICIENT_DATA. | epics.md §FR-7; prd.md §4.3/FR-7 | D1 | technical |
| FR-8 | Apply configurable Tolerance Bands: every numeric check reads tolerance from bundle `toleranceConfig`; changing `currencyToleranceMxn` changes pass/fail boundaries without code change; tolerance recorded on each Finding. | epics.md §FR-8; prd.md §4.3/FR-8 | D1 | technical |
| FR-9 | Verify every text run's embedded font is Aptos by reading the PDF font dictionary (deterministic, not visual inference); non-Aptos run → FAIL with page/region locator. Realizes CL-35. | epics.md §FR-9; prd.md §4.4/FR-9 | D1 | technical |
| FR-10 | Detect overlapping text (glyph bounding-box intersection beyond configured threshold → FAIL CL-28) and verify section headers are bold+uppercase (not → FAIL CL-29); thresholds from config, recorded on Finding. | epics.md §FR-10; prd.md §4.4/FR-10 | D1 | technical |
| FR-11 | Verify pagination correctness, absence of blank pages, and presence of bank logo and card number on every page (CL-31, 33, 34, 48); blank page or missing per-page element → FAIL. | epics.md §FR-11; prd.md §4.4/FR-11 | D1 | technical |
| FR-12 | v1: verify presence of catalog images (card, important-message, sequential) via perceptual hashing vs. catalog; absent → FAIL; absent catalog data → INSUFFICIENT_DATA. Realizes CL-27, 30, 47 (presence-only; full match+order is v2). | epics.md §FR-12; prd.md §4.4/FR-12 | D1 | technical |
| FR-13 | Verify every legend in bundle `mandatoryLegends` is present (missing → FAIL with legend id, CL-46) and "COMPARA TU TARJETA" mandatory section exists (missing → FAIL, CL-32). | epics.md §FR-13; prd.md §4.5/FR-13 | D1 | technical |
| FR-14 | Extract and validate the fiscal block (QR, fiscal code, issuer/receiver RFC) when present; unreadable required QR → FAIL CL-50; fiscal code and RFCs shape-validated CL-51..53; absent on non-IVA statements is not a FAIL. | epics.md §FR-14; prd.md §4.5/FR-14 | D1 | technical |
| FR-15 | Aggregate all Findings into a statement-level Verdict (GREEN / RED / BLOCKED); any FAIL → RED; all PASS/n-a → GREEN; any blocking error → BLOCKED; INSUFFICIENT_DATA reported separately, never alone causes RED. | epics.md §FR-15; prd.md §4.6/FR-15 | D1, D2 | technical |
| FR-16 | Generate a color-marked copy of the statement PDF (PdfSharp) with every FAIL highlighted at its locator; original pages preserved; marked PDF downloadable from QA console. Realizes CL-55. | epics.md §FR-16; prd.md §4.6/FR-16 | D1, D7 | technical |
| FR-17 | Send exactly one email alert per RED statement containing verdict and finding summary; dispatch failures are retried and logged, never silently dropped. Realizes CL-54. | epics.md §FR-17; prd.md §4.6/FR-17 | D7 | technical |
| FR-18 | Human-in-the-loop disposition: QA analyst can accept/reject each Finding and disposition the Statement; every disposition records actor, timestamp, before/after state in an append-only audit table; no automated accept/reject in v1. | epics.md §FR-18; prd.md §4.6/FR-18 | D1, D5, D6 | technical |
| FR-19 | Provide reference data via a single `IVecReferenceDataProvider` contract regardless of source (CSV/DB/API); adding a new adapter requires no validation-engine or Solution-1 change. | epics.md §FR-19; prd.md §4.7/FR-19 | D2, D4 | technical |
| FR-20 | Degrade gracefully on partial reference data: only checks dependent on a missing section become INSUFFICIENT_DATA; all other checks still run. | epics.md §FR-20; prd.md §4.7/FR-20 | D9 | technical |
| FR-21 | Process statements in batch with bounded concurrency (backpressure, no unbounded fan-out); report pending/in-progress/completed/blocked counts; failures move to exception queue, never halt the batch. | epics.md §FR-21; prd.md §4.8/FR-21 | D8, D9 | technical |
| FR-22 | Resume an interrupted batch without reprocessing already-completed statements; reprocess a single statement idempotently, replacing its prior result and recording it in the audit trail. | epics.md §FR-22; prd.md §4.8/FR-22 | D8, D9 | technical |
| FR-23 | Additive, non-breaking integration: dependency direction is `Veriqan → Prisma` only (architecture test enforces it; build fails on reverse reference); Solution-1 test suite passes unchanged after every Veriqan release. | epics.md §FR-23; prd.md §4.9/FR-23 | D2 | technical |
| FR-24 | Reuse Shared Core (IFieldExtractor, imaging analyzers, export infrastructure, EF Core, events, Result<T>, CSnakes pattern) rather than duplicating it; VEC field extraction uses the existing pattern. | epics.md §FR-24; prd.md §4.9/FR-24 | D2 | technical |
| FR-25 | Reconcile printed DESGLOSE DE MOVIMIENTOS against bundle `expectedTransactions` by normalized description (CL-45) and amount within tolerance (item 58); unmatched either-direction → FAIL; date checks (CL-42, 43) run even without expectedTransactions; absent expectedTransactions → description/amount INSUFFICIENT_DATA. | epics.md §FR-25; prd.md §4.3/FR-25 | D1 | technical |
| FR-26 | Verify promotional inserts are current against bundle `promotions` validity windows; expired insert → FAIL; absent promotions data → INSUFFICIENT_DATA. Realizes CL-49. | epics.md §FR-26; prd.md §4.5/FR-26 | D1 | technical |
| FR-27 | Phase-0 isolation: Veriqan.* projects and separate VeriqanDbContext (own `veriqan` schema + migrations) created without any Solution-1 file or schema modification; NetArchTest rule asserting one-way dependency created in this phase, not assumed. | epics.md §FR-27; prd.md §4.9/FR-27 | D2, D5 | technical |
| FR-28 | Verify all 28 mandatory CONDUSEF sections (§1–28) are present and in the fixed legal order, with no inter-section blank gap exceeding 2 cm; conditional sections (§16, §23, §25) marked not-applicable when their trigger is absent. | epics.md §FR-28; LAW-VS-CHECKLIST §A | D1 | technical |
| FR-29 | Verify §6 "Cuánto pagarías" by recomputing months-to-pay and total interest for pago-mínimo / 2× / 5× scenarios using the Acuerdo's revolving-balance recursion from the statement's own reported figures; abstain if needed input is indeterminable. | epics.md §FR-29; LAW-VS-CHECKLIST §A §6 | D1 | corpus-gated |
| FR-30 | Verify §19 per-row interest identity: `monto ≈ saldo_base × (tasa/360) × días` for each of the 6 interest types; mismatches cite §19; low-confidence rows → INSUFFICIENT_DATA. | epics.md §FR-30; LAW-VS-CHECKLIST | D1 | corpus-gated |
| FR-31 | Verify §20 "Distribución de tu último pago" 7-column waterfall identity within typed tolerance; missing/low-confidence cells → INSUFFICIENT_DATA. | epics.md §FR-31; LAW-VS-CHECKLIST | D1 | corpus-gated |
| FR-32 | Verify §8 "Indicadores del costo anual" (12-month interest/commission/annuity): all three indicators present and non-negative; coherence with period figures within tolerance; missing → FAIL; unreadable → INSUFFICIENT_DATA. | epics.md §FR-32; LAW-VS-CHECKLIST | D1 | corpus-gated |
| FR-33 | Verify conditional §16 "Información de otras líneas de crédito" when present: per-row arithmetic (interest vs rate/days, IVA vs interest) and totals reconcile within tolerance; not-applicable when absent; low-confidence rows → INSUFFICIENT_DATA. | epics.md §FR-33; LAW-VS-CHECKLIST | D1 | corpus-gated |
| FR-34 | Verify mandatory verbatim text blocks (§26 13 notas aclaratorias, §27 15-term glosario, §24 atención de quejas legend, §17 art-6-IV mensajes adicionales legends, §11 two exact URLs) using Unicode-NFC normalization + whitespace collapse + similarity threshold (not string equality). | epics.md §FR-34; LAW-VS-CHECKLIST | D1 | technical |
| FR-35 | Verify conditional sections §23 "Cargos no reconocidos" (each row has a valid status from the mandated enum) and §25 "Reestructura" (present when debt was restructured); neither trigger → not-applicable. | epics.md §FR-35; LAW-VS-CHECKLIST | D1 | technical |
| FR-36 | Verify typography legal floor: body text ≥ 8 pt Arial-equivalent; fecha límite de pago ≥ 10 pt bold; the ~10 specifically-mandated bold fields bold by font-name/weight heuristic; indeterminate weight → INSUFFICIENT_DATA (never false FAIL). Separate from client Aptos rule (FR-9). | epics.md §FR-36; LAW-VS-CHECKLIST; epics-tranche2 §E12 | D1 | corpus-gated |
| FR-37 | Verify advertising placement: no ads outside legally permitted free sections (§21/§28/página-cero); §12 Mensajes importantes ≤ 700 chars and no advertising; §17 ≤ ¼ page; §21/§28 ≤ ⅓ page; abstain if section boundaries unavailable. | epics.md §FR-37; LAW-VS-CHECKLIST | D1 | technical |
| FR-38 | Verify §18 "Programas de beneficios" structural completeness (all mandated concepts including explicit "0" values: saldo inicial, generados, redimidos, vencidos, por vencer, saldo final, unidad+pesos equivalent, contacto, CL-38) and §13 crédito disponible para transferencia when applicable. | epics.md §FR-38; LAW-VS-CHECKLIST | D1 | technical |
| FR-39 | Multi-tenant rule profile (seam half E9 + product half E13): per-tenant legal-baseline ⊕ overlay ⊕ typed tolerance bands as configuration; baseline-locked rules cannot be loosened; sub-legal loosening refused at config time; embeddable preventive pipeline-gate API for E13. | epics.md §FR-39; epics-tranche2 §E9/E13 | D3, D2 | E13-gated (product half); technical (seam half E9) |

---

### 2.2 Non-Functional Requirements (NFR-1..8)

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| NFR-1 | Performance/throughput: single text-layer statement ≤ 10 s p95 (deterministic + lightweight-CV); ≥ 1 statement/s/worker sustained; monthly sample (≈130k–200k stmts) completes within 3–5 business-day QA window; bounded worker pool with backpressure. | epics.md §NFR-1; prd.md §10 | D8 | technical |
| NFR-2 | Accuracy: deterministic checks ≥ 99% precision/recall; false-positive rate < 1% (SM-C1); no silent passes (SM-C2 = 0). | epics.md §NFR-2; prd.md §10 | D1 | corpus-gated |
| NFR-3 | Reliability: a single statement failure never halts a batch; all failures land in the exception queue with typed errors; processing is resumable. | epics.md §NFR-3; prd.md §10 | D9 | technical |
| NFR-4 | Observability: every job, Check, and Finding logged with correlation IDs; metrics emitted (throughput, latency, verdict distribution, exception counts) reusing Shared Core logging/metrics infrastructure. | epics.md §NFR-4; prd.md §10 | D7 | technical |
| NFR-5 | Determinism/reproducibility: re-running a statement with identical inputs yields identical Findings (critical for audit and the deterministic-first philosophy). | epics.md §NFR-5; prd.md §10 | D1 | technical |
| NFR-6 | Error handling: all operations use the Shared Core Result<T> pattern; no exceptions for control flow; no exception bubbles out of any rule. | epics.md §NFR-6; prd.md §10 | D9 | technical |
| NFR-7 | Regulatory traceability: every `IVecValidationRule` declares its exact DOF Acuerdo numeral / Guía de Llenado field; missing numeral fails build/architecture-test; each Finding exposes the cited numeral; coverage map queryable for the compliance traceability-matrix export (E13). | epics.md §E9 story 9.2; epics-tranche2 §NFR-7 | D1, D6 | technical |
| NFR-8 | Intra-statement self-containment and abstain-safety: computation rules verify from the statement's own reported figures; a rule needing a low-confidence input field must return InsufficientData (not FAIL); false FAIL on a known-good statement is a design violation. | epics.md §E9 story 9.5; epics-tranche2 §NFR-8 | D1, D9 | technical |

---

### 2.3 Architecture Requirements (AR-1..9 from epics.md)

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| AR-1 | Create `ExxerCube.Prisma.Veriqan.*` projects mirroring the numbered layer convention (Domain/Application/Infrastructure.*/Orchestration/Worker). | epics.md §AR-1 | D2 | technical |
| AR-2 | Separate `VeriqanDbContext` with own `veriqan` schema and own EF Core migrations; never alter `PrismaDbContext` or Solution-1 migrations. | epics.md §AR-2 | D5 | technical |
| AR-3 | NetArchTest rule asserting `Veriqan → Prisma` one-way dependency; build fails on any `Prisma → Veriqan` reference; rule must be created, not assumed. | epics.md §AR-3 | D2 | technical |
| AR-4 | v1 pure C# extraction: PdfPig (fonts/geometry), PDFtoImage/EmguCV (render); no Python/GPU dependency in the production path. | epics.md §AR-4 | D2, D7 | technical |
| AR-5 | Net-new packages (ZXing.NET for QR, perceptual-hash package for image presence) centrally versioned in `Directory.Packages.props`; restore cleanly. | epics.md §AR-5 | D2 | technical |
| AR-6 | `IVecValidationRule` engine: each rule is independent, pure, testable, returns a typed `Finding`; tolerances/thresholds are data (config/bundle), not code constants. | epics.md §AR-6 | D1, D2 | technical |
| AR-7 | `IVecReferenceDataProvider` interface with CSV/DB/API adapters all validated against the JSON reference-data contract; adapter swap requires no engine change. | epics.md §AR-7 | D2, D4 | technical |
| AR-8 | v2 ML ports defined now (`IStatementMlExtractor`, `IVisualComplianceMl`) but implementation deferred; no ML dependency in v1 production path. | epics.md §AR-8 | D2 | technical |
| AR-9 | Append-only Disposition audit table; every verdict stamped with EngineVersion + ReferenceBundleVersion provenance. | epics.md §AR-9 | D5, D6 | technical |

---

### 2.4 Epic Acceptance Criteria (E1–E13, story-level)

#### Epic 1 — Foundation & Isolation

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| E1.1-AC1 | Veriqan.Domain/Application/Infrastructure.*/Orchestration/Worker projects added; solution builds green; no Solution-1 project or file modified; Solution-1 full test suite passes unchanged. | epics.md §Story 1.1 AC | D2 | technical |
| E1.2-AC1 | NetArchTest rule asserts Veriqan→Prisma only; any Prisma project referencing a Veriqan type fails the rule; existing cross-infra isolation guardrails still pass. | epics.md §Story 1.2 AC | D2 | technical |
| E1.3-AC1 | VeriqanDbContext migrations create/modify only `veriqan`-schema tables; PrismaDbContext and its migrations untouched; both contexts coexist against the same database. | epics.md §Story 1.3 AC | D5 | technical |
| E1.4-AC1 | ZXing.NET, perceptual-hash, PdfPig, PDFtoImage packages centrally versioned and restore cleanly; smoke test reads embedded font (PdfPig), renders a page (PDFtoImage), decodes a QR (ZXing.NET), and computes a pHash — all in C#, no Python process. | epics.md §Story 1.4 AC | D2 | technical |

#### Epic 2 — Ingestion & Reference Data

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| E2.1-AC1 | Valid PDF → exactly one VerificationJob with unique id and received timestamp; same content (by hash) does not create duplicate; non-PDF/corrupt → typed Result error, no job. | epics.md §Story 2.1 AC | D4 | technical |
| E2.2-AC1 | CSV and DB adapters both produce bundles that pass the same JSON schema validation; invalid bundle → BLOCKED with typed error; new adapter requires no engine change. | epics.md §Story 2.2 AC | D4, D2 | technical |
| E2.3-AC1 | Bundle missing TASA section → only rate-dependent checks become INSUFFICIENT_DATA; all other checks still run; unresolvable product → BLOCKED with UNKNOWN_PRODUCT (no silent default). | epics.md §Story 2.3 AC | D9, D4 | technical |

#### Epic 3 — Field Extraction

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| E3.1-AC1 | Header extraction produces card# (16 digits), CLABE (18 digits), RFC (pattern match); each field has confidence score and page/region locator; not-found field = `not-extracted` with locator hint, never blank-and-silent. | epics.md §Story 3.1 AC | D1 | technical |
| E3.2-AC1 | Period extraction produces start/cut dates and day count equals the span; extracted rate matched against bundle TASA for product+period. | epics.md §Story 3.2 AC | D1 | technical |

#### Epic 4 — Financial Consistency Engine

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| E4.1-AC1 | `IVecValidationRule` engine: each rule returns a `Finding` (Pass/Fail/InsufficientData) with expected, observed, tolerance, severity, locator, and technique class; identical inputs → identical Findings (NFR-5); no rule throws for control flow (NFR-6). | epics.md §Story 4.1 AC | D1 | technical |
| E4.2-AC1 | Pago-para-no-generar-intereses formula deviation > tolerance → FAIL (CL-21); category sums within tolerance (CL-18..20); saldo deudor total and crédito disponible validated (CL-24, 25); CAT validated (CL-10); each Finding records the exact tolerance. | epics.md §Story 4.2 AC | D1 | technical |
| E4.3-AC1 | Adeudo anterior = prior pago-para-no-generar within tolerance (CL-17); rewards opening balance and points math validated (CL-36, 39); installment carry-over and número-de-pago increment validated (CL-40, 41); absent prior statement → INSUFFICIENT_DATA for these checks. | epics.md §Story 4.3 AC | D1 | technical |
| E4.4-AC1 | Each printed movement matched to expectedTransactions by normalized description (CL-45) and amount within tolerance (item 58); unmatched either direction → FAIL; total cargos/abonos = printed detail sum within tolerance (CL-44); operation dates within period (CL-42, 43); absent expectedTransactions → description/amount checks INSUFFICIENT_DATA, date checks still run. | epics.md §Story 4.4 AC | D1 | technical |

#### Epic 5 — Visual & Print-Quality Inspection

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| E5.1-AC1 | Non-Aptos embedded font run → FAIL with page/region locator; all-Aptos statement passes; font family read from PDF font dictionary, not inferred visually. | epics.md §Story 5.1 AC | D1 | technical |
| E5.2-AC1 | Overlap beyond configured threshold → FAIL (CL-28); section header not bold+uppercase → FAIL (CL-29); thresholds from config, recorded on Finding. | epics.md §Story 5.2 AC | D1 | technical |
| E5.3-AC1 | Blank page → FAIL (CL-48); wrong pagination → FAIL (CL-31); page missing logo or card number → FAIL (CL-33, 34). | epics.md §Story 5.3 AC | D1 | technical |
| E5.4-AC1 | Absence of expected catalog image → FAIL (technique = lightweight-cv); Finding notes presence-only (full match+order is v2); absent catalog data → INSUFFICIENT_DATA. | epics.md §Story 5.4 AC | D1 | technical |

#### Epic 6 — Regulatory & Fiscal Checks

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| E6.1-AC1 | Missing mandatory legend → FAIL with legend id (CL-46); missing "COMPARA TU TARJETA" → FAIL (CL-32); absent legends data → INSUFFICIENT_DATA. | epics.md §Story 6.1 AC | D1 | technical |
| E6.2-AC1 | Decodable QR yields fiscal code; unreadable required QR → FAIL (CL-50); fiscal code and issuer/receiver RFC extracted and shape-validated (CL-51..53); absence on non-IVA statement not a FAIL. | epics.md §Story 6.2 AC | D1 | technical |
| E6.3-AC1 | Promotion with `validTo` before the Period → FAIL (expired); absent promotions data → INSUFFICIENT_DATA. | epics.md §Story 6.3 AC | D1 | technical |

#### Epic 7 — Findings, Reporting & QA Console

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| E7.1-AC1 | Any FAIL → RED; all PASS/n-a → GREEN; any blocking error → BLOCKED; INSUFFICIENT_DATA reported separately and does not alone make RED. | epics.md §Story 7.1 AC | D1 | technical |
| E7.2-AC1 | Every FAIL has a visible highlight at its locator in the marked PDF (PdfSharp); original pages preserved; marked PDF downloadable from QA console. | epics.md §Story 7.2 AC | D1, D7 | technical |
| E7.3-AC1 | RED statement triggers exactly one alert email with verdict + finding summary; dispatch failures retried and logged, never silently dropped. | epics.md §Story 7.3 AC | D7 | technical |
| E7.4-AC1 | Disposition records actor, timestamp, before/after immutably (append-only audit table); VEC issues no automated accept/reject without a human disposition in v1. | epics.md §Story 7.4 AC | D5, D6 | technical |

#### Epic 8 — Batch Processing & Observability

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| E8.1-AC1 | Batch reports pending/in-progress/completed/blocked counts; failing statement moves to exception queue with typed error; batch continues; concurrency bounded with backpressure. | epics.md §Story 8.1 AC | D8, D9 | technical |
| E8.2-AC1 | Resumed batch skips completed statements; explicit reprocess of one statement replaces prior result and is audit-logged; idempotent. | epics.md §Story 8.2 AC | D8 | technical |
| E8.3-AC1 | Metrics emitted: throughput, latency (p95 ≤ 10 s target), verdict distribution, exception counts with correlation IDs (NFR-4); measured per-statement cost recorded to confirm or revise NFR-1 targets. | epics.md §Story 8.3 AC | D8, D7 | technical |

#### Epic 9 — Compliance Verdict Core & Traceability Seam

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| E9.1-AC1 | Every rule evaluation returns both a legal-baseline verdict and a tenant-profile verdict, independently readable; InsufficientData is a distinct outcome from Fail and from Pass; Result<T>, no exceptions; deterministic for identical inputs (NFR-5, NFR-8). | epics.md §Story 9.1 AC | D1, D3 | technical |
| E9.1-AC2 | Existing rules compile against the extended contract with no behavioural change (default tenant = legal baseline). | epics.md §Story 9.1 AC | D2 | technical |
| E9.2-AC1 | Every `IVecValidationRule` declares a non-empty DOF Acuerdo numeral; missing numeral fails build or architecture-test; each Finding exposes its cited numeral; citation queryable for coverage map. | epics.md §Story 9.2 AC | D1, D6 | technical |
| E9.3-AC1 | Tenant overlay that tightens a `tenant-tightenable-only` rule: tightened threshold applies; both verdicts still compute. | epics.md §Story 9.3 AC | D3 | E13-gated |
| E9.3-AC2 | Overlay that loosens a `baseline-locked` rule or pushes below the legal floor → configuration rejected with a clear Result failure; legal-baseline verdict always evaluable regardless of overlay. | epics.md §Story 9.3 AC | D3, D6 | technical |
| E9.4-AC1 | Tolerances are typed, per-rule, range-bounded values with a legal-default; legal-default applies when no tenant override set; override within permitted range applies; override outside range or below legal floor rejected at config time; existing Epic-4 tolerance-band behaviour preserved. | epics.md §Story 9.4 AC | D1 | technical |
| E9.5-AC1 | Rule needing a low-confidence field returns InsufficientData citing the field, not Fail; all high-confidence fields → rule evaluates normally; confidence threshold is per-rule/tenant configuration. | epics.md §Story 9.5 AC | D1, D9 | technical |
| E9.6-AC1 | All ~24 existing E4/E5 rules (CL-10/17-26/36-45/item58; visual CL-28/29/35) migrated to dual-verdict + classification + DOF numeral + typed tolerance contract; existing tests stay green; each can abstain on low-confidence inputs. | epics.md §Story 9.6 AC | D1 | technical |

#### Epic 10 — Regulatory Structural & Textual Completeness

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| E10.1-AC1 | Each mandatory CONDUSEF section (§1–28) located with page + bounding region or reported missing; conditional sections (§16, §23, §25) marked not-applicable when trigger absent; each Finding cites §-numeral (NFR-7); abstains when text layer unreadable (NFR-8). | epics.md §Story 10.1 AC | D1 | technical |
| E10.2-AC1 | Any section out of mandated sequence is a Finding; any inter-section vertical blank gap > 2 cm is a Finding (measured via PdfPig geometry); rule abstains if section boundaries could not be established. | epics.md §Story 10.2 AC | D1 | technical |
| E10.3-AC1 | Shared text-normalization+similarity-matching primitive: Unicode NFC + whitespace/soft-hyphen collapse + ligature folding; similarity score (token-ratio/Levenshtein) compared to configurable threshold; NOT exact equality; threshold is a tenant/rule-configurable parameter per Epic 9. | epics.md §Story 10.3 AC | D1 | technical |
| E10.4-AC1 | §26 (13 notas), §27 (15-term glosario), §24 (quejas legend), §17 (art-6-IV legends), §11 (two URLs) each matched above threshold or flagged; each Finding cites numeral and reports similarity score; rule abstains when host section not detected. | epics.md §Story 10.4 AC | D1 | technical |
| E10.5-AC1 | §23 rows each carry a status from {pendiente-en-revisión, concluida-procedente, concluida-improcedente}; §25 present when debt restructured; neither trigger → not-applicable (not Fail). | epics.md §Story 10.5 AC | D1 | technical |
| E10.6-AC1 | §18 contains all mandated benefit concepts including explicit "0" values; §13 contains crédito disponible para transferencia when applicable; each Finding cites numeral; rule abstains if section text unreadable. | epics.md §Story 10.6 AC | D1 | technical |

#### Epic 11 — Regulatory Computation Verification

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| E11.1-AC1 | §19/§20/§8/§16 financial grids reconstructed from PdfPig geometry into ordered typed rows+cells with per-cell confidence; unresolvable table → InsufficientData, not partial/guessed rows; column/row association validated against ground-truth corpus fixtures. | epics.md §Story 11.1 AC | D1 | corpus-gated |
| E11.2-AC1 | §20 7-column waterfall identity verified within typed tolerance; mismatch beyond tolerance → Fail citing §20; missing/low-confidence cells → InsufficientData. | epics.md §Story 11.2 AC | D1 | corpus-gated |
| E11.3-AC1 | Each §19 row confirms `monto ≈ saldo_base × (tasa/360) × días` within typed tolerance per row; §19 ordinary-rate row equals §10 tasa ordinaria; low-confidence rows → InsufficientData; mismatches cite §19. | epics.md §Story 11.3 AC | D1 | corpus-gated |
| E11.4-AC1 | §6 recursion computed for k ∈ {1,2,5} until revolving balance reaches zero; computed months and summed interest match printed §6 columns within tolerance; NA/"Este periodo" cases handled per guía; required input indeterminable → InsufficientData citing §6 / Circular 13/2011. | epics.md §Story 11.4 AC | D1 | corpus-gated |
| E11.5-AC1 | §8 three indicators present and non-negative; where derivable, coherent with reported period figures within tolerance; missing indicators → Fail citing §8; unreadable → InsufficientData. | epics.md §Story 11.5 AC | D1 | corpus-gated |
| E11.6-AC1 | §16 per-row arithmetic (interest vs rate/days, IVA vs interest) reconciles within tolerance; totals tie to summary fields; not-applicable when absent; mismatches cite §16; low-confidence rows → InsufficientData. | epics.md §Story 11.6 AC | D1 | corpus-gated |

#### Epic 12 — Legal Form & Typography

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| E12.1-AC1 | Body text below ≥ 8 pt floor → Finding; fecha límite de pago below ≥ 10 pt → Finding; cites guía numeral; abstains (InsufficientData) where rendered size cannot be reliably determined. | epics.md §Story 12.1 AC | D1 | corpus-gated |
| E12.2-AC1 | Confidently non-bold mandated field → Fail citing its numeral; font weight indeterminate (subset-embedded/mangled name) → InsufficientData, never Fail. | epics.md §Story 12.2 AC | D1 | corpus-gated |
| E12.3-AC1 | §12 > 700 characters or containing advertising → Finding; advertising outside permitted free sections → Finding citing relevant numeral; abstains where section boundaries (Epic 10) unavailable. | epics.md §Story 12.3 AC | D1 | technical |
| E12.4-AC1 | §17 > ¼ page → Finding; §21/§28 > ⅓ page → Finding; each Finding cites numeral; abstains if section area cannot be measured. | epics.md §Story 12.4 AC | D1 | technical |

#### Epic 13 — Multi-Tenant Productization & Pipeline-Gate

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| E13.1-AC1 | Veriqan invocable as an embeddable library/gate in a statement-generation pipeline; legal-baseline Fail → blocking result; InsufficientData → distinct non-blocking-but-flagged result (never false-blocks); Pass → non-blocking; batch-mode QC also available as alternative invocation; deterministic + cited numerals (NFR-5, NFR-7). | epics.md §Story 13.1 AC | D3, D2 | E13-gated |
| E13.2-AC1 | New tenant onboarded by configuring a profile (overlay + typed tolerances + enabled client checks) without code changes; sub-legal loosening refused at config time; two genuinely different profiles produce correctly different effective rule sets. | epics.md §Story 13.2 AC | D3 | E13-gated |
| E13.3-AC1 | Traceability matrix export: every evaluated rule listed with DOF Acuerdo numeral, verdict (legal + tenant), and evidence locator; states engine version and Acuerdo version; reproducible for identical inputs. | epics.md §Story 13.3 AC | D3, D6 | E13-gated |
| E13.4-AC1 | Every verdict stamped with EngineVersion + ReferenceBundleVersion + Acuerdo edition/date applied; re-running an older statement against a newer engine is detectable via stamped versions. | epics.md §Story 13.4 AC | D5, D6 | E13-gated |

---

### 2.5 Implicit "Preventive Pipeline-Gate" Requirements (from Brief §2 dimensions — not in FR/NFR list)

These are production requirements implicit in the product definition and Brief §2 that have no explicit FR/NFR/AR number.

| ID | Requirement (one line) | Source | Dimension | Dependency-tag |
|----|------------------------|--------|-----------|----------------|
| PG-1 | A real composition root runs the full pipeline (ingest → extract → bind → validate → verdict → report → persist → notify) end-to-end against real statement inputs, not in-memory or stub substrates. | Brief §2 D2; prd.md §12 | D2 | technical |
| PG-2 | Real statement ingestion path (not StubDocumentDownloader / stub queue); statements arrive through a production-grade intake mechanism wired in the Veriqan Worker composition root. | Brief §2 D4 | D4 | technical |
| PG-3 | Production EF Core migrations for all `veriqan`-schema tables applied and schema-stable; no pending schema drift between code and DB. | Brief §2 D5; prd.md §13 | D5 | technical |
| PG-4 | Encrypted legal-baseline store (built E9 seam): encryption key management is real (not a dev/hardcoded key); key rotation plan exists; keys are not stored in source code or unencrypted config. | Brief §2 D5/D6 | D5, D6 | business-gated |
| PG-5 | Immutable audit trail: disposition and verdict records are append-only and tamper-evident; retention policy for verification artifacts implemented (PRD §13 cites ≈ 7 years). | Brief §2 D5/D6; prd.md §14 | D5, D6 | business-gated |
| PG-6 | PII handling in production: card numbers masked in logs and UI by default; full PAN accessible only where a Check requires and access is authorized; PCI-DSS scope confirmed or documented as out-of-scope. | prd.md §11; Brief §2 D6 | D6 | business-gated |
| PG-7 | Authn/authz wired in the Veriqan Worker: the QA Console enforces authentication; role-based access to disposition actions enforced at the service layer, not just page-level `[Authorize]`. | Brief §2 D6; prd.md §11 | D6 | technical |
| PG-8 | Encryption in transit: TLS enforced for all external interfaces (API/QA Console intake, email dispatch, reference-data adapter calls); no plaintext credential or PII over the wire. | Brief §2 D6 | D6 | technical |
| PG-9 | CNBV CUB outsourcing-regime alignment: if deployed as a third-party processor for a regulated bank, the applicable CNBV CUB provisions governing outsourcing are documented and the service architecture addresses them. | Brief §2 D6; CERTIFICATION-RESEARCH | D6 | business-gated |
| PG-10 | ISO 27001 / SOC 2 trajectory: security controls are designed and documented to support a future ISO 27001 or SOC 2 audit; control gaps are explicitly known, not assumed compliant. | Brief §2 D6; epics-tranche2 §Non-engineering actions | D6 | business-gated |
| PG-11 | Deploy artifacts: a repeatable deployment package or container image exists; no hardcoded `Server=DESKTOP-...` or developer-machine paths in any config loaded in production. | Brief §2 D7; GAP-MATRIX §E2 | D7 | technical |
| PG-12 | Real health/readiness probes: each Veriqan process exposes `/health/live` and `/health/ready`; the ready probe reflects actual processing-pipeline state (not a TODO stub). | Brief §2 D7; GAP-MATRIX §E1/E3 | D7 | technical |
| PG-13 | Observability wired to a backend: structured logs, metrics, and traces are ingested by a real observability stack (not just emitted to stdout); alert rules exist for error rate, throughput drop, and SLA miss. | Brief §2 D7; prd.md §12 | D7 | business-gated |
| PG-14 | Runbook: documented runbook covers batch initiation, exception-queue triage, mid-batch resume, reference-data update, and on-call escalation path for the monthly batch window. | Brief §2 D7; prd.md §12 | D7 | technical |
| PG-15 | Real volume throughput verified: NFR-1 targets (≤ 10 s p95, ≥ 1 stmt/s/worker) measured against a representative workload (not synthetic round-trips); results recorded and accepted by the owner. | Brief §2 D8 | D8 | corpus-gated |
| PG-16 | SLA surface: monthly sample completes within the 3–5 business-day QA window under real (or representative) volume; at-risk SLA flagging provides real values (not zeros). | Brief §2 D8; prd.md §12 | D8 | corpus-gated |
| PG-17 | Malformed/corrupt PDF degrades gracefully to BLOCKED/InsufficientData, never false-blocks or crashes the worker; partial extraction surfaces the extracted subset with confidence < threshold. | Brief §2 D9 | D9 | technical |
| PG-18 | Missing or partial reference data (absent TASA, prior statement, image catalog, etc.) surfaces as InsufficientData on affected checks; no affected check emits a false FAIL; the cardinal rule (never false-block) is testable and tested. | Brief §2 D9; epics-tranche2 §2 | D9 | technical |
| PG-19 | Downstream outage (email service down, reference-data adapter unreachable): the batch job completes or parks cleanly; no false-block verdict emitted; exception queue captures the typed error with a retry path. | Brief §2 D9 | D9 | technical |
| PG-20 | Pipeline-gate integration API/SDK (E13): documented latency budget for inline use (within bank's statement-generation pipeline); host-pipeline failure semantics defined (what does the gate return if it times out?). | Brief §2 §5 Integration lens; Epic 13 | D3, D8 | E13-gated |
| PG-21 | Corpus calibration: a real labelled CONDUSEF-statement corpus (known-good + deliberately-broken specimens) is acquired and run through the calibration harness; all thresholds (NFR-1 p95, E11/E12 geometry/typography) are supported by measured distributions, not spec-derived defaults. | Brief §1 §2 D1; CORPUS-CALIBRATION-HARNESS | D1 | corpus-gated |
| PG-22 | The 3 PRP2 "Dummie VEC" synthetic fixtures are replaced or supplemented: they are not representative real statements (all ~12 format rules fail on them, labelled KnownSynthetic); real-data corpus required for any production accuracy claim. | MEMORY.md; CORPUS-CALIBRATION-HARNESS §1 | D1 | corpus-gated |

---

## 3. Conflicts and Ambiguities

| # | Conflict / Ambiguity | Files in tension | Impact |
|---|----------------------|-----------------|--------|
| C1 | **NFR-7 and NFR-8 have no enumerated prose definition in `epics.md`** — they appear only in the coverage map row and in story ACs by reference. Their definitions are reconstructed here from story text in E9 and epics-tranche2. If the owner later writes explicit prose, verify these reconstructions match. | `epics.md` Requirements Inventory (NFR-1..6 listed; NFR-7/8 absent from the list itself) vs. `epics-tranche2-regulatory.md` §E9/E11 | Traceability only; definitions are unambiguous from context |
| C2 | **FR-9 (Aptos brand rule) vs. FR-36 (typography legal floor)** — epics.md explicitly separates them (epics-tranche2 §E12: "Kept separate from the client Aptos brand rule (CL-35), which is a tenant overlay, not the legal floor"). However, E9.3 says baseline-locked rules cannot be loosened; it is not explicit whether Aptos is baseline-locked or tenant-overridable. The register treats FR-9 as a client-overlay check and FR-36 as the legal-floor check, consistent with the E12 text. | `epics.md` §FR-9 vs §FR-36; `epics-tranche2` §E12 | Rule classification (E9.3-AC2) — resolve at E9 seam implementation |
| C3 | **FR-39 spans two epics (E9 seam + E13 product)** — epics.md coverage map lists FR-39 under both E9 and E13. The seam half (E9) is tagged `technical`; the product half (E13) is tagged `E13-gated`. The Brief confirms this split. No conflict in intent, but the FR itself must be split at implementation. | `epics.md` coverage map; `epics-tranche2` §E9/E13 | Dependency tagging |
| C4 | **Sample-volume figures are unconfirmed assumptions** — PRD §10/NFR-1 states "≈130k–200k statements/month assuming 1% of 13–20M" and tags this `[ASSUMPTION pending OQ-1]`. The Open Question (OQ-1) was never resolved in any of the source documents. NFR-1 throughput targets are therefore architecture assumptions, not confirmed business requirements. | `prd.md` §10 NFR-1, §8 OQ-1 | PG-15/PG-16/NFR-1 calibration; owner must confirm volume before production sizing |
| C5 | **PCI-DSS scope is an open question** — prd.md §11 says "confirm PCI-DSS scope for storing card numbers; prefer masked-at-rest unless a Check provably needs the full PAN" — this was never resolved in any reviewed document. It affects PG-6 and the data-at-rest encryption approach. | `prd.md` §11 | D6 / PG-4/PG-6; business-gated |
| C6 | **CONDUSEF corpus does not exist yet** — every E11 and E12 threshold and the overall NFR-2 accuracy bar require calibration against real statements. The calibration harness exists (`CORPUS-CALIBRATION-HARNESS-DESIGN-2026-06-18.md`) but the corpus acquisition is unplanned/unowned. Without it, FR-29..33, E11.1-E11.6, E12.1-E12.2, NFR-2, PG-21, PG-22 cannot be verified. | `CORPUS-CALIBRATION-HARNESS-DESIGN-2026-06-18.md`; `MEMORY.md` | All corpus-gated requirements; largest single production-readiness unlock after E13 |
| C7 | **E13 product half is ROADMAP-GATED on issue #17** — economic buyer discovery is not a code task. If the buyer is never identified, the product half of FR-39, E13.1–E13.4, and PG-20 remain permanently out of scope. This is stated in the Brief and epics, but no target date or gate-clearing criterion is defined. | `epics.md` §E13; `READINESS-CHALLENGE-BRIEF-2026-06-18.md` §1 | All E13-gated requirements |
| C8 | **Data-residency / hosting is an assumption** — prd.md §13 says "CONFIRM: data stays in the bank's jurisdiction; confirm hosting." No confirmation in any reviewed document. This affects CNBV CUB outsourcing regime compliance (PG-9) and encryption key management (PG-4). | `prd.md` §13 | D5/D6; business-gated |

---

## 4. Tally

### By dependency-tag

| Tag | Count | Row IDs |
|-----|-------|---------|
| `technical` | 90 | FR-1..28, FR-34, FR-35, FR-37, FR-38; FR-39 (seam half); NFR-1..8; AR-1..9; E1.1..E1.4; E2.1..E2.3; E3.1, E3.2; E4.1..E4.4; E5.1..E5.4; E6.1..E6.3; E7.1..E7.4; E8.1..E8.3; E9.1-AC1, E9.1-AC2, E9.2-AC1, E9.3-AC2, E9.4-AC1, E9.5-AC1, E9.6-AC1; E10.1..E10.6; E12.3-AC1, E12.4-AC1; PG-1, PG-2, PG-3, PG-7, PG-8, PG-11, PG-12, PG-14, PG-17, PG-18, PG-19 |
| `corpus-gated` | 17 | FR-29, FR-30, FR-31, FR-32, FR-33, FR-36; NFR-2; E11.1..E11.6; E12.1-AC1, E12.2-AC1; PG-15, PG-16, PG-21, PG-22 |
| `E13-gated` | 7 | FR-39 (product half); E9.3-AC1; E13.1-AC1, E13.2-AC1, E13.3-AC1, E13.4-AC1; PG-20 |
| `business-gated` | 7 | PG-4, PG-5, PG-6, PG-9, PG-10, PG-13; (PCI-DSS scope open question) |
| **Total** | **121** | |

> Note: FR-39 and E9.3 are split between `technical` (seam half) and `E13-gated` (product half) and `technical` (baseline-locked enforcement) respectively — each AC row is counted once under its own tag.

### By dimension

| Dimension | Count (approximate — rows touch multiple dimensions) |
|-----------|------------------------------------------------------|
| D1 Functional Correctness | 78 |
| D2 End-to-End Deployable Composition | 20 |
| D3 Multi-tenancy (E13) | 8 |
| D4 Ingestion | 7 |
| D5 Persistence & Data | 9 |
| D6 Security & Compliance Posture | 13 |
| D7 Operational Readiness | 9 |
| D8 Performance / Scale / SLA | 8 |
| D9 Failure Modes | 12 |

> Rows with multiple dimension tags are counted once per dimension they cover; the sum exceeds 121 due to multi-dimension rows.

---

*This register covers the INTENDED scope only. What is actually built is the subject of RC0b (reality map) and RC.1 (requirement→evidence trace). Do not interpret this register as a gap assessment.*
