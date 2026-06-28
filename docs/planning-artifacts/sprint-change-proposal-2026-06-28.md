---
name: sprint-change-proposal
date: 2026-06-28
branch: Liv
author: hebelmx (via Correct Course / bmad-correct-course)
trigger: GitHub issue #19 — Checklist integrity CL-27/CL-30/CL-47
mode: Batch
scope_classification: Moderate (backlog reorganization — PO + Dev, light Architect)
status: PROPOSED — awaiting owner approval
sources:
  - GH #19 (OPEN, 2026-06-28)
  - docs/legal/regulations/Acuerdo_estado_de_cuenta.pdf            # credit-card format, DOF 29-Dec-2022
  - docs/legal/regulations/18.+Disposición+Unica+de+la+CONDUSEF+aplicable+a+las+Entidades+Financieras.pdf  # GAT / operaciones pasivas
  - docs/planning-artifacts/LAW-VS-CHECKLIST-GAP-2026-06-17.md
  - docs/planning-artifacts/epics-tranche2-regulatory.md           # E9–E13
---

# Sprint Change Proposal — Checklist Numbering Integrity (GH #19)

## Section 1 — Issue Summary

**Problem statement.** The checklist identifiers **CL-27 / CL-30 / CL-47** are a three-way mismatch
between (a) what the law requires, (b) what the demo UI labels claim (`ChecklistIds.cs`), and (c) what
the code actually evaluates. The only rule that emits these IDs — `CatalogImagePresenceRule`, a
bank/brand **image-presence** check — has **squatted** on a single compound `CheckId = "CL-27/CL-30/CL-47"`
and carries a **fabricated** `DofNumeral = "Acuerdo §9/§14/§15"`. Meanwhile the UI labels for those same
three IDs name *different, real* compliance requirements (sign coherence, reversals, GAT legend) that the
brand rule does not implement.

**Issue type:** Misunderstanding of original requirements + traceability-integrity defect (not a technical
limitation, not a strategic pivot). It directly violates **Epic 9 / NFR-7** ("every rule carries its
**exact** DOF *Acuerdo* numeral → the auditable evidence chain").

**Discovery context.** Surfaced during Epic 4 (verdict confidence + ExtractionGap), filed as GH #19
(2026-06-28), which explicitly asked for a `bmad-correct-course` pass rather than a mid-epic patch.

**Evidence (ground-truth, verified against code + law in this session):**

1. `CatalogImagePresenceRule.cs:65` → `CheckId => "CL-27/CL-30/CL-47"`; `:68` → `DofNumeral => "Acuerdo §9/§14/§15"`.
   The rule body only does perceptual-hash image matching (§1 SIPRES-logo / tenant brand catalog territory)
   — it has **nothing** to do with §9/§14/§15.
2. `ChecklistIds.cs` labels claim three unrelated requirements: CL-27 = "Signo de cargo/abono coherente"
   (`:54`), CL-30 = "Reversos vinculados a cargos" (`:57`), CL-47 = "Leyenda de CETES/GAT presente" (`:76`).
3. `ChecklistIds.cs` §-map is independently wrong: it maps CL-23..27 → §10 (`:133`) and CL-46/47 → §25
   (`:141`), neither of which matches the law for those concepts.
4. The `Tier()` compound row resolves correctly (`checklist-tiers.csv` line 2: `CL-27/CL-30/CL-47,Bank`
   ↔ `VerdictAggregator` `TryGetValue`). **This is the "tier bug" false alarm — do NOT split the CSV row.**
5. **A divergent numbering scheme exists**: `LAW-VS-CHECKLIST-GAP-2026-06-17.md` maps CL-33 → §1 logo, while
   `ChecklistIds.cs` maps CL-33 → "Color primario de marca". So CL-27/30/47 is **demonstrably not the only
   squatter** — a systematic audit is warranted.

---

## Section 2 — Impact Analysis

### 2.1 Ground-truth refinement (changes the issue's severity)

The issue feared CL-27/CL-30 were "likely UNIMPLEMENTED." Tracing the actual rule inventory shows the
codebase runs **two CheckId conventions** — `CL-NN` (client-checklist anchored) and `LAW-§NN-XXX`
(law-direct) — and most of the underlying law is **already built under the `LAW-§` names**:

| Issue's corrected mapping | Reality in `Veriqan.Infrastructure.Validation/Rules` | Net-new work |
|---|---|---|
| CL-27 → §7 / §22 / §20 (sign coherence) | §7 resumen sums = `CL-17/18/19/20`; §20 waterfall = `LAW-§20-WATERFALL`; §22 desglose = `CL-42/43/44/45` + `ITEM-58` — **all built**. **No rule checks the literal `+`/`−` sign prefix.** | **1 small new rule** (sign-prefix coherence) |
| CL-30 → §23 (abono linked to original cargo) | `Section23CargosNoReconocidosRule` (`LAW-§23-STATUS`) **built** — checks the status enum. The *resolved-procedente abono ↔ original cargo* linkage is the only possible residual. | **Spike → likely a small extension** of the existing §23 rule |
| CL-47 → CETES/GAT (wrong product = savings) | **Genuinely absent.** Now grounded: **Disposición Única de la CONDUSEF, Art. 27** mandates the GAT legend (siglas en negrillas, ≥8 pt) for **operaciones pasivas**. | **1 new rule**, product-gated to pasivos |
| Brand-image rule | `CatalogImagePresenceRule` — real, Bank tier correct, but **mislabeled CheckId + fabricated DofNumeral**. | **Re-home** (rename + correct numeral) |

**Bottom line:** the net-new *implementation* is far smaller than the issue implied — one sign rule, one
GAT rule, a §23 spike, and a rule re-home. The larger work is the **integrity audit** and the **honest
re-labeling**, not new compute.

### 2.2 Epic Impact

- **Epic 9 (Verdict Core & Traceability Seam) — PRIMARY.** This is an NFR-7 integrity defect. The
  re-home + the full CL-1…CL-55 numbering audit belong here (the seam is *defined* by "every rule carries
  its exact DOF numeral"). Epic 9's completion claim is **weakened** until the audit closes — a known
  squatter means the auditable evidence chain has at least one broken link.
- **Epic 10 (Structural & Textual Completeness) — SECONDARY.** The genuine gap-fills land here:
  §7 sign-coherence, the §23 residual extension, and the §23/§26/§27-style verbatim/structural patterns
  already live in E10.
- **Epic 11/12/13 — no impact.** No computation, typography, or productization change.

### 2.3 Story Impact

No *completed* story is invalidated. New stories are **added** (see Section 4). The existing
`SlashCheckId_ResolvesCorrectly` test must be **migrated** (not deleted) to assert the new honest
compound/renamed key.

### 2.4 Artifact Conflicts

| Artifact | Change needed |
|---|---|
| `CatalogImagePresenceRule.cs` | Rename `CheckId`; correct `DofNumeral`; update XML doc-comment header (`:15-21`, `:64-68`). |
| `ChecklistIds.cs` | Fix CL-27/30/47 labels + `Tier()` (`:101-102`) + `DofNumeral()` §-map (`:133,:141`); broader §-map corrections from the audit. |
| `checklist-tiers.csv` (×2 copies: reference-bundles + Fixtures/PRP2) | Rename the compound key row **in lockstep** with the rule's new `CheckId`. **Do not split into 3 rows.** |
| Validation-rule registry / DI | Register the new sign-coherence rule and the new GAT rule; confirm `Section23` extension keeps its registration. |
| Tests | Migrate `SlashCheckId_ResolvesCorrectly`; add tests for the two new rules + the §23 extension. |
| `LAW-VS-CHECKLIST-GAP-2026-06-17.md` | Reconcile / annotate with the audit's corrected mapping (it currently lists CL-27/30/47 as CLIENT+ image checks — that half is right; its CL-33↔§1 mapping is the part that exposes the second numbering scheme). |
| `epics-tranche2-regulatory.md` | Add the new stories under E9 / E10. |

### 2.5 Technical / Architecture Impact

One **micro-decision for the Architect**: the repo already has three CheckId namespaces (`CL-NN` client,
`LAW-§NN-XXX` law-direct, `ITEM-58`). The re-homed brand rule needs an **honest, non-CL** namespace
(proposed: `CLIENT-IMG-CATALOG`, DofNumeral `"Acuerdo §1 (logo SIPRES) + tenant brand catalog"`). No schema,
API, or data-model change. No PRD goal conflict; MVP unaffected.

---

## Section 3 — Recommended Path Forward

**Selected: Option 1 — Direct Adjustment (add stories within the existing E9/E10 structure).**
Not a rollback (nothing wrong was *shipped* to roll back — the brand rule works; only its label lies).
Not an MVP review (scope/goals unchanged).

- **Effort:** Medium (audit dominates; 2 small rules + 1 spike + a rename).
- **Risk:** Low-Medium. The one real footgun is the **CSV compound-key rename**: the tier lookup matches the
  rule's `CheckId` string verbatim, so the rule rename and **both** CSV copies + `ChecklistIds.cs` `Tier()`
  must change atomically or the brand rule silently mis-tiers (the exact failure the issue warns against).
- **Timeline:** fits within the current tranche; no resequencing of E11/E12/E13.

---

## Section 4 — Detailed Change Proposals

### New stories

> IDs use a `.Cx` ("course-correction") suffix to mark them as injected by this proposal.

---

**Story E9.C1 — Re-home the brand-image rule onto an honest CheckId + correct DofNumeral**

*Epic:* 9 (Traceability Seam / NFR-7). *Tier outcome:* unchanged (Bank).

Acceptance criteria:
- `CatalogImagePresenceRule.CheckId` changes from `"CL-27/CL-30/CL-47"` to `"CLIENT-IMG-CATALOG"`
  (final string pending Architect sign-off).
- `DofNumeral` changes from `"Acuerdo §9/§14/§15"` to `"Acuerdo §1 (logo SIPRES) + tenant brand catalog"`.
- XML doc-comment header rewritten to describe a **client/brand** check, not §9/§14/§15.
- **Both** `checklist-tiers.csv` copies have their `CL-27/CL-30/CL-47,Bank` row **renamed** to
  `CLIENT-IMG-CATALOG,Bank` (single row, **not** split).
- `ChecklistIds.cs` `Tier()` updated so the freed CL-27/30/47 no longer hard-map to Bank via the compound
  comment, and `CLIENT-IMG-CATALOG` resolves Bank.
- `SlashCheckId_ResolvesCorrectly` migrated to assert the new key resolves Bank; a regression test asserts
  the old compound key is gone.
- Build 0/0, all Veriqan validation tests green.

```
OLD (CatalogImagePresenceRule.cs)
  public string CheckId   => "CL-27/CL-30/CL-47";
  public string DofNumeral => "Acuerdo §9/§14/§15";

NEW
  public string CheckId   => "CLIENT-IMG-CATALOG";
  public string DofNumeral => "Acuerdo §1 (logo SIPRES) + tenant brand catalog";

Rationale: the rule is a perceptual-hash brand/image-catalog presence check. The law mandates only the
SIPRES logo (§1); product/card imagery is explicitly optional. The §9/§14/§15 numeral was fabricated and
breaks the NFR-7 evidence chain.
```

---

**Story E9.C2 — Full CL-1…CL-55 numbering-integrity audit**

*Epic:* 9. *Type:* audit + corrective edits.

Acceptance criteria:
- For **every** CL-1…CL-55, cross-check three sources: (1) the emitting rule's `CheckId` + `DofNumeral`,
  (2) `ChecklistIds.cs` `Label()` + `DofNumeral()` §-map + `Tier()`, (3) the governing law text.
- Produce a corrected mapping table (new doc `docs/planning-artifacts/checklist-numbering-audit-2026-06.md`)
  flagging every squatter / mis-section (the CL-33↔§1-vs-"Color primario" divergence is a known starter).
- Fix the demonstrably-wrong `ChecklistIds.cs` §-map entries (at minimum CL-23..27→§10 and CL-46/47→§25)
  and any label that names a requirement its rule does not implement.
- Reconcile against `LAW-VS-CHECKLIST-GAP-2026-06-17.md`; annotate that doc with the resolved mapping.
- Output explicitly lists any **other** unimplemented-but-labeled IDs found, as candidate E10/E11 stories.

---

**Story E10.C3 — §7 sign-coherence rule (the genuine CL-27 gap)**

*Epic:* 10. *Law:* Acuerdo §7 ("*Todos los montos precedidos por un signo positivo (+) … cargos … signo
negativo (-) … abonos*"), cross-ref §20/§22.

Acceptance criteria:
- New `IVecValidationRule` verifying every monto in the resumen/desglose carries a sign prefix coherent
  with its cargo/abono nature (cargos `+`, abonos `−`).
- Honest `CheckId` (e.g. `LAW-§7-SIGNOS`) and `DofNumeral => "Acuerdo §7"`.
- Abstain (`InsufficientData`) when signs/amounts are not extractable — never a false RED.
- Unit tests for coherent, incoherent, and missing-sign cases.

---

**Story E10.C4 — §23 residual: resolved-procedente abono ↔ original cargo linkage (CL-30)**

*Epic:* 10. *Law:* Acuerdo §23 (cargos no reconocidos). *Type:* **spike-first**, then extend.

Acceptance criteria:
- Spike: determine whether `Section23CargosNoReconocidosRule` (`LAW-§23-STATUS`) already asserts that a
  *concluida-procedente* dispute shows its **abono linked to the original cargo** in DESGLOSE. If yes →
  story closes as N/A with a note. If no → extend that rule (do **not** add a parallel rule).
- If extended: tests for procedente-with-linked-abono (pass), procedente-without-linkage (fail),
  pendiente/improcedente (out of scope).

---

**Story E10.C5 — GAT-legend rule for operaciones pasivas (CL-47), grounded in Disposición Única Art. 27**

*Epic:* 10. *Law:* **Disposición Única de la CONDUSEF aplicable a las Entidades Financieras, Artículo 27**
("*Cuando sea obligatoria la inclusión del CAT o la GAT … Anteponer las siglas 'CAT' o 'GAT' … en negrillas
… al menos 8 puntos*").

Acceptance criteria:
- New `IVecValidationRule`, **product-gated to operaciones pasivas / savings products** (not credit-card
  statements — the credit-card Acuerdo correctly does **not** mention GAT).
- Honest `CheckId` (e.g. `LAW-DUC-ART27-GAT`) and
  `DofNumeral => "Disposición Única CONDUSEF Art. 27"`.
- Verifies the GAT legend present, siglas in negrillas, ≥8 pt (typography aspects may return
  `InsufficientData` per the E12 bold-heuristic caveat).
- `ChecklistIds.cs` CL-47 label/`Tier()`/§-map corrected to point at this rule (or CL-47 retired in favor
  of the law-direct ID, per the E9.C2 audit decision).
- Tests for present/absent/wrong-product cases.

---

## Section 5 — Implementation Handoff

**Scope classification: Moderate** → backlog reorganization, **Product Owner + Developer**, with a
**light-touch Architect** decision.

| Recipient | Responsibility |
|---|---|
| **Architect** (Winston) | Approve the honest CheckId namespace for the re-homed brand rule (`CLIENT-IMG-CATALOG` vs alternative) and confirm the `LAW-§`/`CL-`/`CLIENT-` convention. One decision, unblocks E9.C1. |
| **Product Owner** | Insert E9.C1, E9.C2, E10.C3, E10.C4, E10.C5 into the tranche backlog; sequence **E9.C1 → E9.C2 → (E10.C3, E10.C4 spike, E10.C5)**; update epic docs / sprint status. |
| **Developer** (Amelia) | Implement E9.C1 (atomic rename across rule + 2 CSVs + `ChecklistIds.cs` + tests), then the audit edits (E9.C2), then the three rule stories. Honor the project Result<T> / CancellationToken / xUnit-v3-Shouldly-NSubstitute conventions. |
| **QA** | Verify build 0/0 + green Veriqan validation suite after E9.C1 (the mis-tier regression guard) and after each rule story. |

**Success criteria:**
- No rule in `Veriqan.Infrastructure.Validation/Rules` carries a fabricated DofNumeral (NFR-7 restored).
- The brand rule is honestly labeled; its Bank tier still resolves (regression test green).
- CL-27 sign-coherence and CL-47 GAT legend implemented and tested; CL-30 §23 residual resolved or closed N/A.
- A published CL-1…CL-55 audit table; `ChecklistIds.cs` labels/§-map match the rules they describe.
- GH #19 closed with a comment linking this proposal and the audit doc.

**Sequencing note:** E9.C1 is the long pole only because of the atomic CSV-rename footgun — do it first and
in one commit. The audit (E9.C2) can surface additional stories; treat its output as a backlog feed, not a
blocker for the three already-scoped rule stories.

---

*Generated via the BMAD Correct Course workflow (Batch mode), grounded in code + law read this session.*
