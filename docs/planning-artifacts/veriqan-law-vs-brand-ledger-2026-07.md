# Veriqan LAW-vs-BRAND Citation Ledger (VLD-P2)

**Status:** investigation-only deliverable for story VLD-P2 (see
`docs/planning-artifacts/epics-veriqan-live-demo-ui-2026-07-04.md`). No production
code or test project was created by this pass — the machine-readable companion
(`veriqan-real-check-ledger-2026-07.json`) is a plain data file for a later story
(VLD-S4) to load; wiring it into the Web.UI and writing
`RealCheckLedgerTests` remain TODO for that story.

**Audience:** bank legal departments. Every LAW badge here must survive "show me
the article, then show me the pixel." Where the code's own citation is weak,
generic, or a proxy, this ledger says so instead of repeating the code's claim
uncritically.

## Methodology

1. Enumerated every `CheckId =>` / `DofNumeral =>` pair by reading the rule source
   directly (not `ChecklistIds.cs`, not the epics doc's prose) under:
   - `Prisma/Code/Src/CSharp/02 Infrastructure/Veriqan.Infrastructure.Visual/Rules/*.cs` (11 rules)
   - `Prisma/Code/Src/CSharp/02 Infrastructure/Veriqan.Infrastructure.Validation/Rules/*.cs` (47 rules: 28
     `CL-*`/`ITEM-58`, 18 `LAW-§N-*`/`LAW-SEC-*`/`LAW-DUC-*`, 1 `CLIENT-IMG-CATALOG`)
2. Cross-referenced every CheckId against the **real tenant bundle**
   `Prisma/Data/Veriqan/reference-bundles/Demo_Bank_(Iqubica)/checklist-tiers.csv`
   (byte-identical to the copy under `Prisma/Fixtures/PRP2/demo/reference-bundle/...`;
   56 non-blank rows: 10 Bank / 26 Both / 20 Condusef, matching the counts documented
   in `ChecklistIds.cs`'s own comment).
3. Classified each rule `law` / `brand` / `data`:
   - **law** — the `DofNumeral` cites a real, specific CONDUSEF *Acuerdo* article (or
     the Annex/Guía de llenado) **and** the rule's own doc comments corroborate that
     citation (a "Legal basis" remark, or a verbatim-text catalog tied to the actual
     DOF instrument — see below). Includes cases where the citation is real but the
     *implementation* is a blunt proxy (flagged honestly, not silently upgraded).
   - **brand** — the requirement traces to the tenant's own style/brand catalog, not
     a CONDUSEF mandate, even if the code attaches a `DofNumeral` string to it.
   - **data** — a scalar/arithmetic reconciliation check (sums, formulas, tolerances)
     whose `DofNumeral` points at the statement section where the figure is
     *displayed*, but whose content is "these two printed numbers must agree,"
     not a formatting/legend mandate. Still law-adjacent, but not part of the
     visual LAW-vs-BRAND debate this story is scoped to.
4. `IsVisual` follows the epics doc's own seeding rule: true for the 11 CheckIds
   physically implemented in `Veriqan.Infrastructure.Visual` — **this ledger
   disputes one part of that seed** (see Discrepancy D1 below) and flags a
   candidate omission (D4).
5. One instrument backs almost every `LAW-*`/typography citation: the
   **CONDUSEF *Acuerdo relativo al formato de estado de cuenta estandarizado de
   tarjeta de crédito para personas físicas*** (DOF 29-Dec-2022, mandatory since
   17-Oct-2024) — this is transcribed verbatim in the header comment of
   `CondusefVerbatimCatalog.cs` (Validation/Rules) and is the instrument this
   ledger's "recommended on-screen citation" strings point to for `Acuerdo §N`
   entries. The one exception is `LAW-DUC-ART27-GAT`, which cites a **different**
   instrument — the *Disposición Única de la CONDUSEF, Artículo 27* (GAT legend
   for savings/deposit products) — do not conflate the two.

I did **not** execute the pipeline against the demo fixtures (that is VLD-P1's
harness, a separate story, and this task was scoped to no test project / no
production code). This ledger is derived by static inspection of rule source +
CSV, which is sufficient to answer "what does the code claim and is the claim
honest" — it is not a substitute for VLD-P1's live-run confirmation of which
CheckIds actually fire on the 4 demo fixtures.

---

## Table 1 — The 11 true visual rules (`Veriqan.Infrastructure.Visual/Rules`)

This is the core deliverable: these are the CheckIds the demo's visual hero /
marked-PDF narrative depends on.

| Rule file | CheckId | DofNumeral (verbatim) | Tier (CSV) | Class | Legal basis / honest caveat | Recommended on-screen citation |
|---|---|---|---|---|---|---|
| `Cl33LogoPresenceRule.cs` | `CL-33` | `Acuerdo §1` | Both | **law** (weak proxy) | §1 does mandate a logo (SIPRES) be present — confirmed independently by the sibling `CatalogImagePresenceRule` doc comment ("Anchored to Acuerdo §1: SIPRES logo required"). But CL-33's own implementation only checks `ImageCount ≥ 1` per page — **any** embedded image passes, including a stray decorative graphic. It does not verify the image *is* a logo. Real law, blunt test — never claim "verified logo" on-screen, only "an image is present/absent." | "CONDUSEF Acuerdo §1 (logo requirement) — image-presence proxy, not logo-identity verification." |
| `Cl35FontComplianceRule.cs` | `CL-35` | `Acuerdo Anexo — Tipografía` | Bank | **brand** | The required font family (default `"Aptos"`) is **tenant-configurable** via `ValidationConstants.RequiredFontFamily` in the reference bundle — CONDUSEF cannot mandate a specific commercial font family per bank, only generic typographic properties (size, weight). The Annex citation is therefore over-claimed: it's real for "some typography standard exists," false for "this specific font family is law." Tier=Bank in the CSV agrees this is a bank-specific concern, not shared/Condusef. | "Client brand typography standard (font: Aptos) — not a CONDUSEF-mandated font family." |
| `Cl28TextOverlapRule.cs` | `CL-28` | `Acuerdo Anexo — Tipografía` | Both | **law** (generic citation, VERIFY exact clause) | Layout word-overlap is plausibly covered by a legibility clause in the Annex, but the citation carries no article/clause number and the file has no verbatim quote to anchor it (unlike CL-48's quoted "sin espacio en blanco mayor a 2 cm"). Treat as a real-but-unconfirmed Annex legibility requirement. | "CONDUSEF Acuerdo, Anexo (Tipografía/legibilidad) — VERIFY exact clause before quoting an article number." |
| `Cl29HeaderStylingRule.cs` | `CL-29` | `Acuerdo Anexo — encabezados de sección en negritas y mayúsculas` | Both | **law** (specific, named requirement; no numeral) | The DofNumeral itself states the requirement in full ("section headers in bold and uppercase") — this reads as a genuine, specific Annex clause even without a numbered article. | "CONDUSEF Acuerdo, Anexo — encabezados de sección en negritas y mayúsculas." |
| `Cl31PaginationRule.cs` | `CL-31` | `Acuerdo §2` | Both | **law** | Specific numbered article; pagination label consistency ("N de M") vs actual page count is a concrete, verifiable structural requirement. | "CONDUSEF Acuerdo §2 — paginación." |
| `Cl34CardNumberPresenceRule.cs` | `CL-34` | `Acuerdo §15` | Both | **law** | Specific numbered article; exact-substring / masked-card matching, not a fuzzy proxy — one of the cleanest law citations in the set. | "CONDUSEF Acuerdo §15 — identificación de tarjeta en cada página." |
| `Cl48BlankPageRule.cs` | `CL-48` | `Acuerdo Anexo — sin espacio en blanco mayor a 2 cm` | Both | **law** | DofNumeral is a verbatim quote of the actual clause text ("no blank space greater than 2 cm"), and the 56.7pt threshold in code is explicitly derived from it (2cm × 28.35pt/cm). Among the strongest, most literal citations in this set. | "CONDUSEF Acuerdo, Anexo — 'sin espacio en blanco mayor a 2 cm'." |
| `AdvertisingPlacementRule.cs` | `LAW-ADS-PLACEMENT` | `Acuerdo §12` | Condusef | **law** | Only rule in the Visual set with an explicit `<b>Legal basis:</b>` remark: §12 restricts "Mensajes importantes" to legal/product notices; advertising confined to the optional free sections §21/§28. Deliberately conservative (documented "NEVER false-Fail" gate). | "CONDUSEF Acuerdo §12 — mensajes importantes; publicidad restringida a secciones libres §21/§28." |
| `SectionSizeCapRule.cs` | `LAW-SEC-SIZECAP` | `Acuerdo §17 (¼ página) / §21·§28 (⅓ página)` | Condusef | **law** | Specific numeric caps tied to named articles. | "CONDUSEF Acuerdo §17 (máx. ¼ página) / §21 y §28 (máx. ⅓ página)." |
| `MandatedBoldFieldsRule.cs` | `LAW-TYPO-BOLD` | `Acuerdo Anexo — Tipografía (negrillas)` | Condusef | **law** | Backed by the actual DOF instrument name+date transcribed in `CondusefVerbatimCatalog.cs` ("Acuerdo relativo al formato de estado de cuenta estandarizado de tarjeta de crédito para personas físicas," DOF 29-Dec-2022, mandatory since 17-Oct-2024). Verifies specific *named* mandated fields render in bold; has a documented never-false-Fail quorum gate, so any on-screen Fail is a confident finding. | "CONDUSEF Acuerdo (DOF 29-dic-2022), Anexo — negrillas obligatorias en campos mandatados." |
| `TypographyPointSizeFloorRule.cs` | `LAW-TYPO-MINSIZE` | `Acuerdo Anexo / Guía de llenado — Tipografía (puntaje mínimo)` | Condusef | **law** | Same instrument family as LAW-TYPO-BOLD; body-text ≥8pt floor and a named field (fecha límite de pago) ≥10pt floor. | "CONDUSEF Acuerdo / Guía de llenado — tamaño mínimo de fuente (8pt cuerpo, 10pt fecha límite de pago)." |

**Visual-set count: 10 law / 1 brand / 0 data.**

---

## Table 2 — Scalar/data rules (`Veriqan.Infrastructure.Validation/Rules`)

Included because it was quick given the same pass, and because two entries
(`CL-37`, `CLIENT-IMG-CATALOG`) directly bear on the visual-vs-not classification
question (see Discrepancies). Kept terser than Table 1 since these are not the
demo's visual hero.

| CheckId | DofNumeral | Tier (CSV) | Class | One-line basis |
|---|---|---|---|---|
| `CL-10` | Acuerdo §9 | Both | data | CAT formula recomputation. |
| `CL-17` | Acuerdo §7 | Both | data | Adeudo periodo anterior vs prior statement, ±$0.50. |
| `CL-18` | Acuerdo §7 | Both | data | Cargos regulares = sum of non-MSI charges. |
| `CL-19` | Acuerdo §7 | Both | data | Cargos a meses (capital) = sum of MSI charges. |
| `CL-20` | Acuerdo §7 | Both | data | Pagos y abonos = sum of credit movements. |
| `CL-21` | Acuerdo §7 | Both | data | Pago-para-no-generar-intereses formula recompute. |
| `CL-22` | Acuerdo §13 | Both | data | Saldo cargos regulares ≈ Pago-para-no-generar-intereses, ±$0.50. |
| `CL-23` | Acuerdo §13 | Both | data | Saldo cargos a meses = sum of COMPRAS A MESES saldo pendiente. |
| `CL-24` | Acuerdo §13 | Both | data | Saldo deudor total = sum of two nivel-de-uso subtotals. |
| `CL-25` | Acuerdo §13 | Both | data | Crédito disponible = línea − saldo deudor total. |
| `CL-26` | Acuerdo §13 | Both | data | Crédito disponible efectivo ≈ crédito disponible, ±$0.50. |
| `CL-32` | Acuerdo §11 | Both | **law** | Mandatory-section presence ("COMPARA TU TARJETA") — presence, not arithmetic. **Note:** `ChecklistIds.cs`'s CL-32 label ("Correlación de páginas continua") does not match this rule at all — see Discrepancy D2. |
| `CL-36` | Acuerdo §18 | Both | data | Rewards opening balance continuity vs prior statement. |
| `CL-37` | Acuerdo §18 | **Bank** | data | Points-to-pesos exchange rate check. **Not** a contrast/color rule — see Discrepancy D1, the highest-priority finding in this ledger. |
| `CL-39` | Acuerdo §18 | Both | data | Saldo total puntos running-balance check. |
| `CL-40` | Acuerdo §13 | Both | data | MSI saldo pendiente per open purchase. |
| `CL-41` | Acuerdo §13 | Condusef¹ | data | MSI número-de-pago increments by exactly 1/month. |
| `CL-42` | Acuerdo §22 | Condusef¹ | data | Movement dates fall inside billing period. |
| `CL-43` | Acuerdo §22 | Condusef¹ | data | Per-page DESGLOSE period-range confirmation (best-effort). |
| `CL-44` | Acuerdo §22 | Both | data | Printed total cargos/abonos = sum of extracted movements. |
| `CL-45` | Acuerdo §22 | **Bank** | data | Bidirectional transaction-description matching vs reference bundle. Tier=Bank is a little surprising for a content-accuracy check tied to §22 — flagged, not fixed. |
| `CL-46` | Acuerdo §14/§17/§24 | Both | **law** | Mandatory-legend verbatim presence (multi-article). |
| `CL-48`² | — | Both | law | See Table 1 (visual). |
| `CL-49` | Acuerdo §18 | Bank | data | Promotional-insert validity window vs statement period. |
| `CL-50` | Acuerdo §4 | Bank | data | Fiscal (CFDI) QR present/decodable. §4 is a loose anchor — CFDI/QR format is chiefly SAT/fiscal regulation, not the CONDUSEF Acuerdo's core subject. |
| `CL-51` | Acuerdo §4 | Bank | data | Folio fiscal (CFDI UUID) present/non-empty. Same §4 caveat as CL-50. |
| `CL-52` | Acuerdo §4 | Bank | data | Issuer RFC present + pattern match. Same §4 caveat. |
| `CL-53` | Acuerdo §4 | Bank | data | Receiver RFC present + pattern match. Same §4 caveat. |
| `ITEM-58` | Acuerdo §22 | Bank | data | Per-transaction printed amount vs expected amount (depends on CL-45 match). |
| `CLIENT-IMG-CATALOG` | Acuerdo §1 (logo SIPRES) + tenant brand catalog | Bank | **brand** (hybrid) | Own doc comment is explicit: "Anchored to Acuerdo §1 (SIPRES logo required; product/card imagery **optional per tenant brand catalog**)." The SIPRES-seal half is law; the product/card-image perceptual-hash matching half is pure brand catalog. Net classification: brand, with the law sliver called out. Also implicated in Discrepancy D4 (visual-classification gap). |
| `LAW-SEC-PRESENCE` | Acuerdo §1–§28 | Condusef | law | All 28 mandatory sections present. |
| `LAW-§11-URLS` | Acuerdo §11 | Condusef | law | Two mandated URLs present in §11. |
| `LAW-§13-TRANSFERENCIA` | Acuerdo §13 | Condusef | law | "Crédito disponible para transferencia" field shown when §13 applicable. |
| `LAW-§16-OTRASLINEAS` | Acuerdo §16 | Condusef | data | Per-row arithmetic for other-credit-lines table (conditional section). |
| `LAW-§17-LEGENDS` | Acuerdo §17 | Condusef | law | Four art-6-IV mandatory legends present in §17. |
| `LAW-§18-COMPLETE` | Acuerdo §18 | Condusef | law | All nine mandated benefit-program concepts shown (incl. explicit "0"). |
| `LAW-§19-INTERES` | Acuerdo §19 | Condusef | data | Per-row interest recomputation + cross-check vs §10 annual rate. |
| `LAW-§20-WATERFALL` | Acuerdo §20 | Condusef | data | 7-column payment-distribution waterfall identity. |
| `LAW-§23-ABONO-LINK` | Acuerdo §23 | **NOT IN CSV** | law | Concluded-dispute amounts linked to DESGLOSE line items. See Discrepancy D3. |
| `LAW-§23-STATUS` | Acuerdo §23 | Condusef | law | Mandated CONDUSEF status token present for disputes. |
| `LAW-§24-QUEJAS` | Acuerdo §24 | Condusef | law | Invariant "Atención de quejas" legend + UNE phones present. |
| `LAW-§25-REESTRUCTURA` | Acuerdo §25 | Condusef | law | §25 present when statement indicates restructured debt. |
| `LAW-§26-NOTAS` | Acuerdo §26 | Condusef | law | Thirteen mandatory verbatim "Notas aclaratorias" present. |
| `LAW-DUC-ART27-GAT` | Disposición Única CONDUSEF, Art. 27 | **NOT IN CSV** | law | GAT legend for operaciones pasivas — **different instrument** from the Acuerdo (Disposición Única, not the Acuerdo). See Discrepancy D3. |
| `LAW-§27-GLOSARIO` | Acuerdo §27 | Condusef | law | Fifteen mandatory verbatim glossary definitions present. |
| `LAW-§6-SIMULACION` | Acuerdo §6 | Condusef | data | §6 payment-simulation table recomputed (Banxico Circular 13/2011 recursion). |
| `LAW-§8-INDICADORES` | Acuerdo §8 | Condusef | law | Three mandatory 12-month cost indicators present and non-negative. |
| `LAW-SEC-ORDER-GAP` | Acuerdo Anexo — guía de llenado | Condusef | law | §1→§28 reading order + no inter-section gap >2cm. |

¹ CL-41/42/43 fall through `ChecklistIds.cs`'s default-to-Condusef rule because they're absent
from the CSV's explicit Bank/Both list — but they ARE present in the real CSV as `Both`... actually
verified: CL-41/42/43 are **not** rows in `checklist-tiers.csv` at all. Tier shown here is
"Condusef" by the same conservative-default convention `ChecklistIds.cs` documents (unmapped ⇒
Condusef floor), not a CSV-confirmed value. Treat as **VERIFY**, not confirmed.
² CL-48 row repeated for table completeness; full detail in Table 1.

---

## Discrepancies (task item 4)

**D1 — CL-37 is not a contrast rule. This is the single highest-value catch in
this pass.** The epics doc itself (VLD-P2 AC, "plus `CL-37` (contrast — semantically
visual though implemented in `Validation`...)") asserts CL-37 = color/text contrast.
Ground truth: `Cl37TipoCambioRewardsRule.cs` — CheckId `CL-37`, validates the
**points-to-pesos exchange rate** (a rewards-program arithmetic check), nothing to
do with contrast or color. This is the exact same failure mode the epics doc
correctly diagnosed in `ChecklistIds.cs` (whose CL-37 label reads *"Contraste
texto/fondo ≥ 4.5:1"*) — the epic's own author appears to have carried that
stale label forward instead of re-deriving it from the rule source, exactly the
mistake this story exists to prevent. **Recommendation:** drop CL-37 from the
`IsVisual=true` set; there is currently **no** implemented contrast-ratio rule
anywhere in the codebase (grepped both rule directories — nothing checks a 4.5:1
ratio). If a contrast rule is wanted for the demo, it does not exist yet and
must be tracked as new work, not assumed present.

**D2 — Confirms the epics doc's own CL-32 example.** `ChecklistIds.cs` labels
CL-32 "Correlación de páginas continua"; the real `Cl32ComparaTuTarjetaRule.cs`
is a mandatory-section-presence check for "COMPARA TU TARJETA," unrelated to
page correlation. (No new finding — cross-verified as stated in the epic.)

**D3 — Two real CheckIds are absent from `checklist-tiers.csv` entirely:**
`LAW-§23-ABONO-LINK` and `LAW-DUC-ART27-GAT`. Both exist in code with real
`DofNumeral` citations but have no tier row, so `ChecklistIds.cs`'s
default-to-Condusef fallback is the only tier signal — that fallback is a
*ChecklistIds.cs* convention, not a CSV-confirmed fact. Flag as **VERIFY** for
owner: either the CSV needs these 2 rows added, or there's a reason they're
excluded that isn't documented anywhere I found.

**D4 — `CLIENT-IMG-CATALOG` is arguably more "visual" than `CL-37`, but the
epics doc's `IsVisual` seed (by source-project only) misses it.** It does
perceptual-hash image matching against a page render — a genuinely visual
technique — yet lives in `Veriqan.Infrastructure.Validation/Rules`, not
`...Visual/Rules`, so the "implemented in Visual" seed rule excludes it while
(incorrectly) including CL-37. **Recommendation for owner sign-off (per the
epic's own caveat that this classification needs it):** swap the two — drop
CL-37, add CLIENT-IMG-CATALOG — if the demo's `[N VISUAL][M DATA]` ribbon is
meant to reflect genuinely visual techniques rather than source-folder
location.

**D5 — CL-33 and CL-35 look similar (both "identidad visual," both cite a law
numeral) but resolve oppositely.** CL-33 (logo) is a real law anchor (§1) tested
by a weak proxy — still fundamentally a **law** finding, just a soft one. CL-35
(font) cites law-shaped language ("Acuerdo Anexo — Tipografía") but the
specific font family is proven tenant-configurable in the same file — it is a
**brand** finding wearing a law citation. Do not let the superficial similarity
of their DofNumeral strings collapse this distinction on-screen.

**D6 — CL-50/51/52/53 (fiscal/CFDI block) all cite `Acuerdo §4`, which is a
loose anchor.** These check CFDI UUID/RFC format, which is governed by SAT
(tax authority) rules more directly than the CONDUSEF Acuerdo's core subject
(statement format for cardholders). Kept as `data` classification with the
caveat surfaced rather than upgraded to a confident `law` citation.

---

## Summary for the orchestrator

- **Files written:**
  - `docs/planning-artifacts/veriqan-law-vs-brand-ledger-2026-07.md` (this file)
  - `docs/planning-artifacts/veriqan-real-check-ledger-2026-07.json` (machine-readable companion)
- **Counts (11 true visual rules, the story's core scope):** 10 law / 1 brand / 0 data.
- **Counts (full 58-CheckId universe incl. scalar/data rules):** 26 law / 2 brand
  (CL-35, CLIENT-IMG-CATALOG) / 30 data. (Verified by counting `classification`
  values in the JSON companion — 58 entries, zero duplicate keys.)
- **Strongest LAW-backed visual finding to lead the demo with:** `LAW-TYPO-BOLD`
  (mandated fields not rendered bold). It is anchored to the actual named DOF
  instrument (Acuerdo, DOF 29-Dec-2022, mandatory since 17-Oct-2024, transcribed
  in `CondusefVerbatimCatalog.cs`), it has a documented never-false-Fail quorum
  gate (so any on-screen Fail is a high-confidence finding, not a heuristic
  guess), and the defect ("this field should be bold, it visibly isn't") is
  trivial for a legal reviewer to eyeball against the pixel. `CL-48`
  (blank-space >2cm, also a verbatim Annex quote) and `CL-34` (card number
  missing from a page, §15, exact-match not proxy) are strong seconds.
- **Do NOT lead with `CL-35`** (the current flagship `bad-font-cl35.pdf`
  fixture) as a *law* finding — per this ledger it is a **brand** finding. It's
  still a fine demo point, but the on-screen citation must say "client brand
  typography standard," not cite the Acuerdo, or it fails the "show me the law"
  test with a bank legal audience.
- **Flagged VERIFY (not fabricated):** the exact Annex clause/article backing
  `CL-28` (word overlap) and `CL-29` (header bold+uppercase) — real-sounding but
  no numeral or verbatim quote found to anchor them; and the tier for
  `LAW-§23-ABONO-LINK` / `LAW-DUC-ART27-GAT` (absent from the CSV) and for
  CL-41/CL-42/CL-43 (also absent from the CSV, currently only Condusef by
  `ChecklistIds.cs` fallback convention).
- **Not done in this pass (by design, per task scope):** did not create
  `RealCheckLedgerTests`, did not touch `ChecklistIds.cs` or any production
  code, did not run the pipeline against the 4 demo fixtures (that's VLD-P1).
