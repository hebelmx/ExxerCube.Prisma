---
name: law-vs-checklist-gap
date: 2026-06-17
branch: Liv
status: analysis — input to Epic 6+ (Regulatory Completeness)
sources:
  - docs/legal/regulations/Acuerdo_estado_de_cuenta.pdf   # CONDUSEF, DOF 29-Dec-2022, mandatory 17-Oct-2024
  - Prisma/Fixtures/PRP2/Check+list+demo+v2+Iqubica.csv     # client 55-item checklist
  - docs/planning-artifacts/epics.md                        # built scope (E1-E8)
---

# Veriqan VEC — Law vs. Client-Checklist vs. Built: Gap Matrix

## Purpose

The client (Iqubica/Banamex) gave us a **55-item checklist**. The underlying law — the CONDUSEF
*Acuerdo … formato de estado de cuenta estandarizado de tarjeta de crédito para personas físicas*
(DOF 29-Dec-2022, **mandatory for every Mexican issuer since 17-Oct-2024**) — defines a **28-section
mandatory format** plus a *guía de llenado* of global form rules. This document maps the three
layers (Law ↔ Client ↔ Built) to find:

1. **What the law requires that the client did not ask for** → the additive-value opportunity.
2. **What the client asked for that the law does not mandate** → genuine client-specific value (keep).
3. **What is already built** → so new epics start from ground truth.

### The reframing that unlocks the "hard" checks

The verifier's job is **internal-consistency verification**: recompute each mandated figure **from the
statement's own reported data** and confirm it reconciles. The **reported figures are the source of
truth** to verify against. Therefore the sections previously flagged as "needs external inputs"
(§6 simulation, §19 interest basis) are in fact **self-contained**:

- **§19** reports, per interest-type row: `saldo base · núm. días · tasa anual · monto`. We verify
  `monto ≈ saldo_base × (tasa/360) × días` — no external daily balances required.
- **§6** reports the simulation result columns. We recompute months-to-pay & total interest from the
  reported *pago mínimo*, *tasa ordinaria*, and *pago para no generar intereses* using the Acuerdo's
  revolving-balance recursion, and confirm the printed values match.

External reference data (TASA tab, image catalog, legends catalog, prior statement) is only needed for
**cross-source** checks, not for these **intra-statement** legal-consistency checks.

---

## Legend

- **Built** = rule exists & tested on `Liv`. **WIP** = in the active worktree. **—** = not started.
- **Gap class**: `NEW` (law-mandated, client didn't ask) · `PARTIAL` (client/built touches it but not
  to the legal standard) · `COVERED` (client asked & built/planned) · `CLIENT+` (client value beyond law).

## A. The 28 mandatory sections

| Law § | Mandated content | Client CL | Built | Gap class |
|---|---|---|---|---|
| 1 | Logo del Banco, top-left **all** pages, must match SIPRES | CL-33 | WIP (CL-33) | PARTIAL (SIPRES match) |
| 2 | "Página X de Y", top-right **all** pages | CL-31 | WIP (CL-31) | COVERED |
| 3 | Sección de datos de envío (ventana del sobre) | — | — | **NEW** |
| 4 | Identificación del producto (denominación/categoría, núm. tarjeta 16/últ.4, RFC, sucursal, núm. cliente, QR, CLABE) | CL-1,4,5,6,7,8,50 | extraction | COVERED (extraction) |
| 5 | "Tu pago requerido": periodo, fecha corte, núm. días, **fecha límite (bold, ≥10pt)**, **pago no generar int (bold)**, **pago mín+compras meses (bold)**, pago mínimo | CL-11..16 | CL-15,16 built; extraction | PARTIAL (per-field bold/size) |
| **6** | **"Cuánto pagarías por tus compras regulares"** — 3-col simulation (pago mín / 2× / 5× → meses & intereses) + revolving-balance formula | — | — | **NEW — high value** |
| 7 | Resumen de cargos y abonos (adeudo anterior **bold**, cargos reg., compras a meses capital, intereses, comisiones, IVA, pagos/abonos, **PAGO NO GENERAR INT bold**) | CL-17..21 | CL-17..21 built | PARTIAL (bold) |
| **8** | **Indicadores del costo anual** (intereses / comisiones / anualidad pagados últimos 12 meses) | — | — | **NEW** |
| 9 | CAT (**bold**, "Sin IVA") | CL-10 | CL-10 built | PARTIAL (bold/label) |
| 10 | Tasa interés anual ordinaria **[FIJA o VARIABLE]** (**bold**) | CL-9 | extraction | PARTIAL (fija/variable label, bold) |
| 11 | "Compara tu tarjeta" — **two exact URLs** (tarjetas.condusef.gob.mx, comparador.banxico.org.mx) | CL-32 | — | PARTIAL (exact URL text) |
| 12 | Mensajes importantes — **≤700 chars, no advertising** | CL-30 (image only) | — | **NEW** (textual rule) |
| 13 | Nivel de uso (saldo reg., saldo a meses, **saldo deudor total bold**, límite, crédito disp., +efectivo, +transferencia) | CL-22..26 | CL-22..26 built | PARTIAL (bold; +transferencia) |
| 14 | Notas al calce (per-page footer legend; **last page: nombre fiscal/domicilio/tel/URL bold**) | CL-46 (partial) | — | **NEW** (footers + last-page block) |
| 15 | Número de cuenta, top-left page 2+ | (rel. CL-34) | WIP (CL-34) | PARTIAL |
| 16 | **Información de otras líneas de crédito** (9 columns) | — | — | **NEW** (conditional) |
| 17 | Mensajes adicionales — **mandatory art-6-IV legends**, ≤1/4 page | CL-46 | — | PARTIAL (specific legends + size) |
| 18 | Programas de beneficios (nombre, unidad+equiv pesos, saldo inicial, utilizados, vencidos, generados, saldo final, por vencer, contacto) | CL-36..39 | CL-36,37,39 built | PARTIAL (CL-38 all-concepts-incl-0; section structure) |
| **19** | **Saldo sobre el que se calcularon los intereses** — 6 interest types, per-row `base·días·tasa·monto` (+PSD avg-daily formulas) | — | — | **NEW — high value** |
| **20** | **Distribución de tu último pago** — 7-column waterfall identity | — | — | **NEW — cheap, high-confidence** |
| 21 | Sección opcional libre (≤1/3 página) | — | — | NEW (size rule) |
| 22 | Desglose de movimientos (a: meses sin int; b: meses con int; c: cargos/abonos regulares; **Total cargos/abonos bold**; **cronológico**) | CL-42..45, item-58 | built | PARTIAL (chronological order; 3-subtable structure) |
| 23 | **Cargos no reconocidos** (status enum: pendiente / concluida procedente / improcedente) | — | — | **NEW** (conditional) |
| 24 | **Atención de quejas** — fixed CONDUSEF legend (UNE + 800-999-8080 / 55-53-40-09-99) | CL-46 (partial) | — | **NEW** (specific legend) |
| 25 | **Reestructura de tu deuda** | — | — | **NEW** (conditional) |
| **26** | **Notas aclaratorias** — **13 verbatim mandatory notes** | CL-46 (partial) | — | **NEW — easy, high value** |
| **27** | **Glosario de términos** — **15 verbatim terms**, alphabetical | — | — | **NEW — easy** |
| 28 | Sección opcional libre | — | — | NEW (size rule) |

## B. Global *guía de llenado* form rules

| Rule | Client CL | Built | Gap class |
|---|---|---|---|
| Idioma español | — | — | NEW (low) |
| Tipografía **mínima ≥8pt Arial-equivalente** (alto/ancho/grosor); **fecha límite ≥10pt bold** | CL-35 (Aptos) | CL-35 built | **PARTIAL / CONFLICT** — see note |
| ~10 **specific bold fields** (fecha límite, pago no gen. int, pago mín+compras, adeudo anterior, saldo deudor total, total cargos/abonos, tasa, CAT, last-page fiscal block) | CL-29 (headers only) | CL-29 built | PARTIAL |
| **Fixed section order**; **no blank gap >2cm** between sections | CL-48 (blank pages) | WIP (CL-48) | PARTIAL |
| **No advertising** outside free sections (§21/§28/página cero) | — | — | **NEW** |
| Electronic version must be **color** | — | — | NEW (low) |
| **One statement per product** (no consolidated) | — | — | NEW (low) |
| Date format **DD-MMM-AAAA** | — | — | NEW (low) |

> **Font conflict resolved by layering.** The law sets a *font-agnostic floor* (≥8pt; fecha límite
> ≥10pt bold; legibility/contrast). "Aptos" is purely Banamex's **brand standard**, stricter than the
> floor. Model them as two layers: **legal floor** (any issuer) + **client brand rule** (per-tenant
> config). No contradiction once separated.

## C. Client value beyond the law (keep — do NOT commoditize away)

These are real client requirements the CONDUSEF format does **not** mandate; they live in the
**client/tenant layer**, configurable per customer:

- CL-2, CL-3 — name & address **structured split**.
- CL-27, CL-30, CL-47 — **card / message / marketing images vs the product catalog** (law only mandates
  the SIPRES logo). Requires the tenant's image catalog + perceptual hashing.
- CL-45, item-58 — reconcile movement detail vs the client's **"Detalle de operaciones"** source tab.
- CL-49 — **promotions current** (tenant promo calendar).
- CL-50..53 — **QR / CFDI fiscal block** (fiscal code, RFC emisor/receptor).
- CL-54, CL-55 — **email alert + color-marked PDF** (reporting mechanism).

---

## Summary of the opportunity

| | Count | Notes |
|---|---|---|
| Sections **fully covered** by client+built | ~6 | identity extraction, CAT, resumen sums, nivel de uso |
| Sections **PARTIAL** vs legal standard | ~10 | mostly bold/typography/exact-text precision on built rules |
| **NEW** legal-mandated, client didn't ask | **~12** | §3, §6, §8, §16, §19, §20, §23, §24, §25, §26, §27 + form rules |
| **CLIENT+** beyond law | ~8 | catalog images, fiscal, reconciliation, reporting |

**The NEW bucket is the value.** ~70% of it is **deterministic** and reuses the **already-built**
`IVecValidationRule` engine (Epic 4) + PdfPig geometry (Epic 5): the §20 waterfall and §6/§19 formulas
are arithmetic; §26/§27/§24 are verbatim text-presence; typography/bold/blank-gap are geometry. The
self-contained reframing means **none of the high-value NEW checks are blocked on external reference
data** — they verify the statement against its own reported numbers.

## Productization stance (per owner)

Build the **legal baseline as a reusable, multi-tenant core** (sellable to any MX issuer) but **keep it
personalizable** — tenant config selects which client-layer checks (brand font, image catalogs, fiscal,
reporting) and tolerance bands apply. Legal layer = product; client layer = per-tenant configuration.
**Do not hardcode to one bank.**
</content>
</invoke>
