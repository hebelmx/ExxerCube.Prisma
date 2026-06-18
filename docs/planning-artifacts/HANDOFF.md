# Veriqan VEC — Handoff

**Date:** 2026-06-16 · **Branch:** `Liv` · **Status:** Planning complete (PRD + Architecture + Epics),
reviewed. Ready for sprint planning / Epic 1 implementation. No production code written yet.

Read this first, then the authoritative artifacts below. Everything else under `Prisma/Fixtures/PRP2/`
that predates 2026-06-16 carries a **SUPERSEDED** banner — history only.

---

## 0. Implementation status (updated 2026-06-18)

> **Resuming the build?** Read `ORCHESTRATION-HANDOFF-2026-06-18c-E11.md` first — it is the canonical resume
> point. The original MVP (**E1–E8**) plus Tranche-2 **E9 (seam)** and **E10 (structural & textual
> completeness)** are all COMPLETE + adversarially reviewed + remediated on `Liv`. **Next = Epic 11
> (Regulatory Computation Verification — the moat), gated on a table-extraction spike (Story 11.1) + a
> real-statement corpus.** That doc has the building blocks, the E11 plan + gate, carry-forwards, and the
> orchestration mechanics/gotchas. (`ORCHESTRATION-HANDOFF-2026-06-18.md` remains accurate for the
> E1–E10 done-state detail.) The summary below is the short form.

**Epic 1 — Foundation & Isolation: DONE** (branch `Liv`, commits `83e5701f`→`f5ef52c0`),
adversarially reviewed (1.1/1.2/1.4 genuinely done; 1.3 done-with-caveat). All additive — no
Solution 1 production source modified.
- **1.1** 10 `ExxerCube.Prisma.Veriqan.*` projects scaffolded across the numbered layers; full sln
  builds 0/0.
- **1.2** `VeriqanDependencyDirectionTests` (in `Tests.Architecture`) enforces `Veriqan → Prisma` only
  via two non-vacuous checks (CLR manifest + `.deps.json`); Architecture suite 26/26.
- **1.3** Separate `VeriqanDbContext` + isolated `veriqan` schema + `InitialVeriqanSchema` migration
  (zero `dbo` refs; history table pinned to `veriqan`). PrismaDbContext untouched.
- **1.4** Net-new pinned packages `ZXing.Net 0.16.11` + `CoenM.ImageSharp.ImageHash 1.3.6`; pure-C#
  smoke test (PdfPig/PDFtoImage/ZXing/pHash) 4/4, no Python/GPU.
- **Incidental fix (`7da2547f`):** a pre-existing compile break in
  `Tests.Infrastructure.BrowserAutomation.E2E` (missing global `using`s — the branch was NOT green at
  handoff despite the §8 claim) was repaired so the full-solution green gate holds.

**Epic 2 — Ingestion & Reference Data: DONE** (commits `1f3ce25d`→`f73f77c2`), adversarially reviewed
(findings closed in `f73f77c2`). Built on the client-independent path + a documented-default CSV adapter
(per owner ruling — the client's first-adapter choice in §6 is still open but the port makes swapping cheap).
- **2.1** Idempotent `StatementIngestionService` (SHA-256 hash, `%PDF-` guard, EF-backed dedupe) → exactly
  one `VerificationJob`. Application.Tests 13/13.
- **2.2** `IVecReferenceDataProvider` + `VecReferenceBundle` model + standalone draft-2020-12
  `ReferenceBundleSchemaValidator` (embedded schema) + `CsvReferenceDataAdapter`. Net-new pinned:
  `JsonSchema.Net 7.3.3`, `CsvHelper 33.0.1`. ReferenceData.Tests 17/17 (incl. example.json round-trip).
- **2.3** `ProductResolver` (alias match, no silent default → BLOCKED `UNKNOWN_PRODUCT`) + `BundleBinder`
  → `VerificationContext` + per-capability `ReferenceDataAvailability` (missing TASA degrades only
  rate checks). `StatementModel` is a null placeholder for Epic 3.

**Epic 3 — Field Extraction: DONE** (commits `346614cf`, `c9d8cf45`, findings `1a9ace21`), adversarially
reviewed. PdfPig text-layer extraction (pure C#, no OCR/Python) of the real Dummie VEC fixtures.
- **3.1** `StatementModel` + `ExtractedField<T>`/`FieldLocator`/`ExtractionStatus`; header identity
  fields via PdfPig bounding-box label↔value association; card=16/CLABE=18/RFC validated. Honest finding:
  the fixtures carry a 13-digit CLABE → reported `ExtractedInvalidFormat` (not hidden).
- **3.2** `PeriodSummary` (dates, day-count consistency, pago amounts, CAT/TASA, saldos) + Spanish date
  parsing; `TasaMatcher` (extracted rate vs bundle TASA → Match/Mismatch/InsufficientData). Extraction.Tests 33/33.
- **OQ-4 (digital vs scanned):** the 3 fixtures are confirmed text-layer, so v1 text extraction is proven;
  a scanned-PDF/OCR path remains v2 (still gated on whether production PDFs are ever scanned).

**Carry-forwards:** ✅ CLOSED 2026-06-17 — (1) Story 1.3 coexistence is now proven by a real
Testcontainers SQL Server test (`b7634048`); (2) the CSV adapter now fills ALL bundle sections
(`70da5f4d`, ReferenceData.Tests 38). Remaining note (not actionable): Veriqan extraction is PdfPig-native
and does not consume a Prisma Shared-Core extraction type — the arch "reuse" claim is aspirational;
revisit only if a shared seam emerges.

**Epic 4 — Financial Consistency Engine: DONE** (2026-06-17; commits `6cf5683d`→`f51d20ff`),
adversarially reviewed — every checklist formula confirmed correct, two semantics points owner-adjudicated
(CL-10 percentage-point tolerance; CL-20 credits-only per MX cargo/abono + CONDUSEF). `IVecValidationRule`
engine (Scrutor DI, deterministic) + RuleFinding; real PASS/FAIL on CL-10/17/18/19/20/21/22/24/25/42/44/45/
item-58. Validation.Tests 96, Extraction.Tests 54. Carry-forwards (P2, honest InsufficientData): TotalCargos
extraction gap (CL-44 charge side; credits validated exactly), CL-26 separate efectivo field, COMPRAS-A-MESES
installment extraction (CL-23/40/41), rewards extraction+fixture (CL-36/37/39), CL-43 page-range header.

**Legal context (authoritative):** `docs/legal/regulations/` — CONDUSEF `Acuerdo_estado_de_cuenta.pdf` +
SIARA/DGAAC docs. Terminology + regulatory checks (esp. Epic 6) must accord with it.

**Epic 5 — Visual & Print-Quality: DONE** (commits through `424351d0`), reviewed. Font/Aptos (CL-35),
text-overlap+headers (CL-28/29), pagination/blank/per-page logo+card (CL-31/33/34/48) via PdfPig geometry.
**5.4 catalog image-presence (CL-27/30/47, pHash) DEFERRED** — needs the §6 image-catalog client answer.

**Epic 6 — Regulatory & Fiscal: DONE** (commits through `e88249de`), reviewed. Legends + COMPARA
(CL-32/46, shared `VecTextNormalizer`), fiscal QR/RFC (CL-50..53, ZXing+PDFtoImage; never false-FAILs a
no-comisiones/IVA statement), promotions currency (CL-49, never false-FAILs — expired→InsufficientData
pending image presence).

**Epic 7 — Findings, Reporting & QA Console: DONE** (commits `19246bb7`→`5bed45be`), reviewed (all 4
genuinely done). VerdictAggregator (BLOCKED>RED>GREEN; InsufficientData never alone makes RED); color-marked
PDF (PdfSharp, PdfPig→PdfSharp Y-flip, pixel-verified); RED email alert (Polly retry, exactly-one,
log-not-drop); human Disposition append-only audit (actor required, no auto-disposition; veriqan-schema
migration). QA Console UI is a deferred thin surface.

**Epic 8 — Batch Processing & Observability: DONE** (commits `067a58b4`→`80acd33e`), reviewed. End-to-end
`VerificationPipeline` (ingest→extract→bind→engine→verdict) + bounded-concurrency `BatchProcessor` +
exception queue (NFR-3); resume (skip-completed by content hash) + reprocess (replace + append-only audit,
actor required); metrics (Meter duration histogram / verdict-tagged counter / exceptions, throughput + p95)
+ correlation-id scopes. **PROVEN END-TO-END:** the pipeline ran over a real Dummie fixture → verdict RED,
35 findings (CL-10/17/18/19/20…). One corrective Solution-1 touch (a `<Compile Remove>` so the Prisma
Orchestration project stops globbing the additive `Veriqan.Orchestration` subfolder; arch suite still 26/26).

**🎉 The original 55-item-checklist MVP (Epics 1–8) is COMPLETE** on `Liv`, end-to-end proven, ~430+
Veriqan tests green, full solution 0/0.

**Next:** the Tranche 2 regulatory-completeness tranche **E9–E13** (`docs/planning-artifacts/epics-tranche2-regulatory.md`)
— **E9 (the rule-contract seam) first** (it retrofits the existing rules), then the new CONDUSEF checks;
E11 needs a table-extraction spike; E13 gated on issue #17 (buyer discovery). Data-alignment carry-forward:
reference-data product aliases must cover the extracted product token (e.g. "Tarjeta de Crédito BSSB", not
just "BSSB") or binding BLOCKs. Full plan + carry-forwards + orchestration gotchas:
`ORCHESTRATION-HANDOFF-2026-06-17.md`.

---

## 1. What Veriqan VEC is

An automated quality gate for **bank credit-card statements** (PDF). It runs the bank's **55-item
checklist** (financial/arithmetic consistency, field extraction, visual/print-quality, regulatory &
fiscal) and produces a **color-marked PDF + email alert** that a human QA analyst reviews. It is built
as an **additive module of the existing ExxerCube.Prisma platform** (which hosts Solution 1, the
oficio / "Atención a Autoridades" product). VEC = Solution 2.

## 2. Authoritative artifacts (source of truth, in reading order)

| Artifact | Path |
|---|---|
| Reuse vs standalone analysis | `Prisma/Fixtures/PRP2/REUSE-VS-STANDALONE-RECOMMENDATION.md` |
| **Checklist source of truth (the Excel)** | `Prisma/Fixtures/PRP2/Check+list+demo+v2+Iqubica.xlsx` |
| Reference-data JSON contract | `Prisma/Fixtures/PRP2/reference-data/` (README + schema + example) |
| **PRD** (final) | `docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/prd.md` |
| PRD addendum (tech how) | `docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/addendum.md` |
| **Architecture** (8 ADRs) | `docs/planning-artifacts/architecture.md` |
| **Epics & Stories** | `docs/planning-artifacts/epics.md` |
| Decision log (24 decisions) | `docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/.decision-log.md` |
| Review reports (4) | `docs/planning-artifacts/review-*.md` + `…/prds/…/review-*.md` |

## 3. Decisions already made (do not relitigate without reason)

1. **Module, not standalone.** New `ExxerCube.Prisma.Veriqan.*` projects in the existing solution;
   dependency `Veriqan → Prisma` only; ~50–60% Shared-Core reuse.
2. **Excel is the source of truth** = **55 checklist items** (CL-1…CL-55). Docs saying "115+" are wrong.
3. **Font = Aptos** (not Arial/Times).
4. **Deterministic-first, pure C# in v1** — PdfPig (fonts/geometry), PDFtoImage/EmguCV (render),
   ZXing.NET (QR), a perceptual-hash nuget (image presence). **No Python, no GPU in v1.** ML
   (LayoutLMv3/CLIP) + catalog-order image matching are **v2** behind defined ports.
5. **Human-in-the-loop** — VEC flags; it never auto-rejects in v1.
6. **Reference data** via one JSON contract + pluggable CSV/DB/API adapters, with **graceful
   degradation** (missing data ⇒ `INSUFFICIENT_DATA`, never a false FAIL).
7. **Existing PRP2 VEC scaffolding is an untrusted prototype (~5–15% real)** — it cannot import, never
   ran. Salvage only verified-good files; rebuild the rest.
8. **Phase-0 does NOT refactor Solution 1.** VEC v1 is single-source, so the planned genericization of
   the 2,811-line `FusionExpedienteService` is **deferred** (biggest risk removed from v1).
9. **Persistence = separate `VeriqanDbContext` + `veriqan` schema** (never touch `PrismaDbContext`).

## 4. Plan shape (8 epics, value-sequenced — see epics.md)

E1 Foundation & Isolation → E2 Ingestion & Reference Data → E3 Field Extraction →
**E4 Financial Consistency Engine (highest value)** → E5 Visual & Print-Quality → E6 Regulatory &
Fiscal → E7 Findings/Reporting/QA Console → E8 Batch & Observability.

All 27 FRs mapped; every CL item has an FR home (PRD §17 + epics coverage map).

## 5. Recommended next step

**Start Epic 1 (Foundation & Isolation)** — it is low-risk scaffolding that cannot break Solution 1
and establishes the safety net (NetArchTest dependency rule, separate `VeriqanDbContext`, net-new
packages, pure-C# smoke test). It needs **none** of the open client answers below. Suggested entry:
`bmad-sprint-planning` then `bmad-dev-story` (or `bmad-quick-dev`) on Story 1.1.

## 6. Open questions — BLOCKED ON CLIENT (do not guess)

These don't block Epic 1 but gate scope/dates for E2+:
1. **Sample size + SLA window** (docs assume 1% of 13–20M ≈ 130k–200k/month, 3–5 business days).
2. **Which reference-data delivery mechanism ships first** (CSV / DB / API) → picks the first adapter.
3. **How image catalogs are delivered/keyed** (needed even for v1 presence checks).
4. **Are production PDFs digital (text layer) or scanned?** → drives OCR investment (v1 assumes text layer).
5. Accuracy/false-positive acceptance bar; alert recipients/channel; final namespace
   (`ExxerCube.Prisma.Veriqan`).

## 7. Gotchas for the next agent

- **Brownfield safety is non-negotiable:** any change must keep Solution 1's test suite green
  (SM-5). The NetArchTest rule (Story 1.2) must be authored — it does not exist yet.
- The C# arch test stack is **NetArchTest** (not ArchUnitNET). Existing cross-infra isolation
  guardrails will need curation for Veriqan's multi-infra composition.
- `ZXing.NET` and a perceptual-hash nuget are **net-new** dependencies (add to
  `Directory.Packages.props`) — they are outside the "reuse" headline.
- Don't resurrect the Python ML path for v1. It's v2-only.
- Reference-bundle JSON validates against `Prisma/Fixtures/PRP2/reference-data/vec-reference-bundle.schema.json`.

## 8. Repo state at handoff

- Branch `Liv` (off `kat` dev line). `main` and `kat` are at the prior release (`v1.4.0-rc.1` tag).
- This commit adds **planning artifacts only** (no production code) + SUPERSEDED banners on the stale
  PRP2 docs. Nothing in Solution 1 was touched.
