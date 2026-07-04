---
name: veriqan-locator-bbox-audit-2026-07
date: 2026-07-04
story: VLD-P1 (docs/planning-artifacts/epics-veriqan-live-demo-ui-2026-07-04.md)
status: DONE — read-only audit; feeds VLD-S4/S5 demo copy
---

# VLD-P1 — Visual-rule locator bounding-box quality audit

Decides, per visual rule, whether the "Marked Page" hero draws a **tight box** around the
actual defect or only a **page-level marker**. Drives which findings are safe to LEAD the
visual demo with.

## Key facts (from ground truth)
- `FieldLocator.HasBoundingBox` ⇔ `Left && Bottom && Width && Height` all non-null (`Veriqan.Domain/Extraction/FieldLocator.cs:50`).
- Exactly 11 rule files in `Veriqan.Infrastructure.Visual/Rules/` — confirmed, no others.
- Locator quality is set by the **fact type** each rule reads `.Locator` from:
  - **Tight bbox** — `BoundingBoxOf(words,…)` (`PdfPigStatementFieldExtractor.cs:2551`) or PdfPig `Word.BoundingBox` / `Letter.BoundingBox`.
  - **Page-only** — `PageInspectionFacts.Locator` is always `FieldLocator.PageHint(pageIndex)` (`…Extractor.cs:3271,3284,3299`); no X/Y/W/H ever.

## Findings table

| Rule | CheckId | DofNumeral | Locator | Evidence | Notes |
|---|---|---|---|---|---|
| Cl33LogoPresenceRule | CL-33 | Acuerdo §1 | page-only | `Cl33…:90` → PageHint (`…Extractor.cs:3271`) | Defect = absence of image; no geometry to box. |
| Cl35FontComplianceRule | CL-35 | Acuerdo Anexo — Tipografía | TIGHT | `Cl35…:125` → `FontUsage.Locator` = `Letter.BoundingBox` | Box = first glyph of offending run (tight but tiny). |
| TypographyPointSizeFloorRule | LAW-TYPO-MINSIZE | Acuerdo Anexo / Guía — Tipografía (puntaje mínimo) | TIGHT | `…:181` & `:287` → `TextTypographySample.Locator` = `Word.BoundingBox` | Wraps the exact offending word. Best-in-class. |
| MandatedBoldFieldsRule | LAW-TYPO-BOLD | Acuerdo Anexo — Tipografía (negrillas) | TIGHT | `…:212` → extracted field `.Locator` = `BoundingBoxOf(band)` | Box on the exact mandated field. |
| SectionSizeCapRule | LAW-SEC-SIZECAP | Acuerdo §17 (¼ pág) / §21·§28 (⅓ pág) | TIGHT | `…:162` → `DetectedSection.Locator` = heading-band bbox | Marks the section heading (not the overrun). |
| Cl28TextOverlapRule | CL-28 | Acuerdo Anexo — Tipografía | TIGHT | `…:140` → union bbox of the two overlapping words | Box straddles both colliding words — striking. |
| Cl29HeaderStylingRule | CL-29 | Acuerdo Anexo — encabezados negritas/mayúsculas | TIGHT | `…:120` → `SectionHeaderStyle.Locator` = band bbox | Wraps the offending header. |
| Cl31PaginationRule | CL-31 | Acuerdo §2 | page-only | `…:104,120,142,164` → PageHint | Label parsed via regex; word bbox discarded. |
| Cl34CardNumberPresenceRule | CL-34 | Acuerdo §15 | page-only | `…:164` → PageHint | Defect = absence of card number; no geometry. |
| Cl48BlankPageRule | CL-48 | Acuerdo Anexo — sin espacio en blanco > 2 cm | page-only | `…:133` → PageHint | Gap Y-band not retained. |
| AdvertisingPlacementRule | LAW-ADS-PLACEMENT | Acuerdo §12 | TIGHT | `…:275` → `DetectedSection.Locator` = heading-band bbox | Box on offending section heading. |

**Summary: 7 of 11 emit a TIGHT bbox** (CL-35, LAW-TYPO-MINSIZE, LAW-TYPO-BOLD, LAW-SEC-SIZECAP, CL-28, CL-29, LAW-ADS-PLACEMENT). 4 are page-only (CL-33, CL-31, CL-34, CL-48) — all page-scoped presence/absence/pagination checks whose defect has no single geometry.

## Demo implication
- **Tight box (safe to lead):** LAW-TYPO-MINSIZE, LAW-TYPO-BOLD, CL-28, CL-29 box the actual defect. LAW-SEC-SIZECAP / LAW-ADS-PLACEMENT box the section heading (accurate but indirect). CL-35 = single-glyph box.
- **Page marker only (no box, don't lead):** CL-33 logo, CL-31 pagination, CL-34 card number, CL-48 blank page.

## Top-3 to LEAD the visual demo (tight box + strong LAW-* citation)
1. **LAW-TYPO-MINSIZE** — tight box on the exact undersized word; concrete floor (≥8pt body / ≥10pt fecha límite).
2. **LAW-SEC-SIZECAP** — tight box on offending section heading; crispest legal citation (Acuerdo §17/§21·§28) + real geometric %.
3. **LAW-TYPO-BOLD** — tight box on the exact mandated field; strong Acuerdo-Anexo *negrillas* citation.

Runner-up "wow": **CL-28 text-overlap** (box straddling two overlapping words) — most striking visual, but weaker generic citation; use as follow-up not legal lead.

## Rules needing a small code change to get a tight box (future, optional)
- **CL-31 pagination — SMALL** (~15–30 LOC): extractor already regex-locates the "N de M" label; capture its `BoundingBox` into `PageInspectionFacts`.
- **CL-48 blank-gap — SMALL/MEDIUM**: whole-blank page → full-page box is ~3 LOC (not "tight"); the >2cm intra-page gap needs retaining the gap Y-band (~20–40 LOC).
- **CL-33 logo / CL-34 card number — NOT WORTH IT**: absence defects; a box would need a template/anchor model that doesn't exist (separate larger story).
