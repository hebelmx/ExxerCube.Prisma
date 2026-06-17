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

> **Merged into `epics.md` on 2026-06-17** (canonical location for Epics E9–E13 and all their stories).
> This file is retained for the Tranche-2 design rationale, roundtable corrections, coverage map, and non-engineering actions above.
