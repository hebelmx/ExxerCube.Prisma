---
name: epics-tranche2-regulatory
date: 2026-06-17
status: DRAFT (pending owner approval) — STAGED here to avoid concurrent-append collision on epics.md
                                          (another agent is appending E6–E8). Merge into epics.md once that agent is done.
inputDocuments:
  - docs/planning-artifacts/LAW-VS-CHECKLIST-GAP-2026-06-17.md
  - docs/planning-artifacts/CERTIFICATION-RESEARCH-2026-06-17.md
  - docs/planning-artifacts/epics.md            # FR-28..39, NFR-7,8 already added to Requirements Inventory
  - docs/legal/regulations/Acuerdo_estado_de_cuenta.pdf
shapedBy: bmad-party-mode roundtable (John/Winston/Victor/Amelia/Mary), 2026-06-17
---

# Veriqan VEC — Tranche 2: Regulatory Completeness (Epics E9–E13)

Extends the built E1–E8 plan with full CONDUSEF *Acuerdo* legal-compliance coverage. **Re-sequenced
after the 2026-06-17 strategy roundtable** — the original E9–E12 proposal was restructured on four
agreed corrections (below). New FRs (FR-28..39) and NFRs (NFR-7,8) are already in `epics.md`'s
Requirements Inventory; this file holds only the new epic list + coverage map for that tranche.

## Corrections folded in from the roundtable

1. **The seam comes first, not last.** The deterministic-verdict + tenant/baseline/tolerance +
   rule→DOF-numeral traceability layer is a *constraint on how every rule is authored*, not a
   deferred feature. Authoring 30 new rules without it = a 30-rule rewrite later. → **E9 (was E12-lite).**
2. **Abstain is a first-class verdict.** Computation rules recompute from extracted figures; a misread
   digit must yield **`InsufficientData` / "cannot verify"**, never a false "bank non-compliant" (which,
   as a preventive pipeline gate, would wrongly halt a billing run — instant rip-out). Dual verdict:
   *compliant-with-law* vs *compliant-with-tenant-profile*, always separable.
3. **Computation is the moat; structural completeness is the quick win.** Ship a thin structural slice
   to earn trust, then pivot hard to recomputation (§6/§19/§20) — the part a competitor can't copy from
   the public spec. Table-row extraction is the real cost there (a spike, not a story).
4. **Productization is roadmap-gated.** Full multi-tenant "sell-to-many-banks" scope waits on
   identifying the economic buyer (who eats the CONDUSEF fine). The *seam* (E9) is core-and-early; the
   *product surface* (E13) is gated.

## Epic list (Tranche 2)

### Epic 9 — Compliance Verdict Core & Traceability Seam  *(FIRST — load-bearing)*
The rule-evaluation contract every other rule is authored against. **No UI, minimal scope (~the
load-bearing 15% of productization).** Drive it with TWO genuinely different real tenant profiles
(the original bank + one deliberately different), not hypotheticals.
- Dual verdict (legal-baseline vs tenant-profile), separable.
- `InsufficientData` / abstain as a first-class verdict, driven by per-field extraction confidence.
- Tenant rule profile = baseline ⊕ overlay ⊕ tolerances, where each rule is classified
  **baseline-locked / tenant-tightenable-only / tenant-overridable**; sub-legal "loosening" is refused
  at config time (or surfaced loudly as "tenant deviation from law", never a silent pass).
- Tolerances are **typed, per-rule, range-bounded** overrides with a legal-default (not a free k/v bag).
- Every rule carries its **exact DOF *Acuerdo* numeral** (NFR-7) → the auditable evidence chain.
- Retrofit the ~24 existing E4/E5 rules onto the seam.
**FRs:** FR-39 (seam half), NFR-7, NFR-8.

### Epic 10 — Regulatory Structural & Textual Completeness  *(quick-win breadth)*
Confirm every legally-required section is present, in fixed order, with the exact mandated wording.
- §1–28 presence + fixed order + no inter-section gap >2 cm.
- Verbatim text blocks (§26 13 notas, §27 15-term glosario, §24 quejas legend, §17 art-6-IV legends,
  §11 two URLs) — **matched after Unicode-NFC normalization + whitespace/soft-hyphen collapse, with a
  per-legend similarity threshold (Levenshtein/token-ratio), NOT string equality** (PdfPig emits
  draw-order glyphs; accents/ligatures/NBSP vary).
- Conditional sections §23 (status enum), §25; §18/§13 completeness (all concepts incl. "0").
**FRs:** FR-28, FR-34, FR-35, FR-38.

### Epic 11 — Regulatory Computation Verification  *(the moat — highest $-value)*
Recompute the law's mandated calculations from the statement's OWN reported figures (NFR-8); abstain
on low-confidence inputs (E9).
- **Spike first:** PdfPig table-row/cell reconstruction for §19/§20/§8 grids (the largest hidden cost;
  schedule ahead of the rules). Acquire a **corpus of real CONDUSEF statements** to calibrate against
  ground-truth totals — without it the rules are only synthetically testable.
- §6 payment-simulation recursion, §19 per-row `monto ≈ base×(tasa/360)×días`, §20 7-col waterfall,
  §8 12-month indicators, §16 other credit lines.
**FRs:** FR-29, FR-30, FR-31, FR-32, FR-33.  **NFR:** NFR-8.

### Epic 12 — Legal Form & Typography  *(gated on E5 visual, which is WIP)*
- Typography legal floor (≥8 pt Arial-equiv; fecha límite ≥10 pt bold; ~10 specific bold fields).
  **Point-size via PdfPig is reliable; "bold" is a heuristic** (font-name/weight, mangled by subset
  embedding) → return `InsufficientData` when weight is indeterminate, never a false FAIL.
- Advertising placement (no ads outside free sections; §12 ≤700 chars; §17 ≤¼, §21/§28 ≤⅓ page).
- Separate from the client Aptos brand rule (CL-35), which is a tenant overlay, not the legal floor.
**FRs:** FR-36, FR-37.

### Epic 13 — Multi-Tenant Productization & Pipeline-Gate  *(ROADMAP-GATED on economic-buyer discovery)*
The "sell to many banks" surface. **Do not scope/build until the economic buyer is identified.**
- Embeddable **preventive pipeline-gate / library mode** (fail-the-build on legal violation) in addition
  to batch QC — turns the product into critical-path infrastructure (architecture call: Winston).
- Per-tenant profile management + onboarding-as-configuration (not consulting).
- Full **compliance traceability-matrix export** — the numeral-by-numeral evidence a bank hands CONDUSEF
  in a *Programa de Cumplimiento Forzoso* (the deck draws from this).
- Versioning across regulatory amendments.
**FRs:** FR-39 (product half).  **NFR:** NFR-7.

## FR / NFR coverage map (Tranche 2)

| Epic | FRs | NFRs |
|---|---|---|
| E9 Verdict Core & Traceability Seam | FR-39 (seam) | NFR-7, NFR-8 |
| E10 Structural & Textual Completeness | FR-28, FR-34, FR-35, FR-38 | — |
| E11 Computation Verification | FR-29, FR-30, FR-31, FR-32, FR-33 | NFR-8 |
| E12 Legal Form & Typography | FR-36, FR-37 | — |
| E13 Productization & Pipeline-Gate | FR-39 (product) | NFR-7 |

All FR-28..39 + NFR-7,8 covered. Sequencing: **E9 → E10 (thin slice) → E11 (pivot to the moat) → E12
(after E5) → E13 (gated).** Dependencies: E10/E11 author against E9's seam; E11 needs the table-spike +
real-statement corpus; E12 waits on E5; E13 waits on the buyer.

## Non-engineering actions (carried, not epics)

- **Buying-center discovery** before scoping E13 — ask, in the next meeting: *whose name is on the
  remediation plan when El Calificador downgrades the bank?* (+ Mary's/John's budget-line, sign-off,
  consequence-owner questions). Stop taking SW-dept integration asks as product requirements until known.
- **GTM:** **Convención Bancaria** is the buyer-concentration venue for the productized tranche (post-ship)
  — solves *reach*, not *who-pays-per-deal*.
- **Coverage deck** (.pptx) — generated from E9's traceability data once the tranche is locked.
- **Credibility:** no statement-format certification exists; pursue ISO 27001 / SOC 2 and design to the
  CNBV CUB outsourcing regime (see CERTIFICATION-RESEARCH-2026-06-17.md).

---

# Tranche 2 — Stories

## Epic 9: Compliance Verdict Core & Traceability Seam

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
