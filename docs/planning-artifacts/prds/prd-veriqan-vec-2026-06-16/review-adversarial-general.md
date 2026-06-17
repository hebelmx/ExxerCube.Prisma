---
title: Adversarial Review — Veriqan VEC PRD
status: review
created: 2026-06-16
reviewer: cynical/adversarial (general)
target: docs/planning-artifacts/prds/prd-veriqan-vec-2026-06-16/prd.md (+ addendum.md)
source-of-truth: Prisma/Fixtures/PRP2/Check+list+demo+v2+Iqubica.xlsx ("Check List VEC", 55 items)
---

# Adversarial Review — Veriqan VEC

**Stance:** find what will bite us, not what reads well. Every claim verified against the Excel
checklist (via `Check+list+demo+v2+Iqubica_Check_List_VEC.csv`, the extracted sheet) and the
reference-data schema/README. The PRD is a competent piece of writing; that is exactly why its gaps
are dangerous — they are well-disguised.

## TL;DR verdict

The PRD is honest about *some* of its risks (it flags scale, reference data, and the prototype as
risks) but **the headline coverage claim is false**: it does not map all 55 checklist items to FRs,
and the most load-bearing success metric (SM-1 "≥90% evaluated") is mathematically unachievable
without the reference data the client has not defined. Several "deterministic" checks quietly depend
on ML/heuristics. The brownfield "non-breaking" claim rests entirely on a refactor (Phase 0,
`IDataFuser<T>` extraction) the PRD does not own and an architecture test that does not yet exist.

---

## Finding counts by severity

| Severity | Count |
|---|---|
| Critical | 3 |
| High | 6 |
| Medium | 6 |
| Low | 4 |
| **Total** | **19** |

---

## CRITICAL

### C1 — Coverage gap: at least 5 checklist items have NO FR home (the "55-item coverage" claim is false)

**PRD location:** §0 ("references those items as CL-1…CL-55"), §4 (FR-4…FR-14), SM-1 (§7,
"≥ 90% evaluated").

I mapped every CL citation in the PRD (grep of `CL-\d+`) against all 55 rows of the Excel sheet.
The following items are **named in no FR and in no Consequences block**:

| CL | Excel text (sheet "Check List VEC") | Where it should live | Status in PRD |
|---|---|---|---|
| **CL-42** | "Validar que todas las operaciones estén dentro del rango de fechas del periodo." | a transaction-detail check | **No FR.** Falls numerically inside FR-6's cited range "CL-36…CL-44" but FR-6 is titled *arithmetic*; a date-range membership test is not arithmetic and is not in any Consequence. |
| **CL-43** | "Todas las páginas de desglose deben indicar el rango de fechas del periodo." | a layout/per-page check (like FR-11) | **No FR.** Not arithmetic, not in FR-11's per-page consequences (logo/card-number only). |
| **CL-45** | "La descripción del desglose de movimientos debe coincidir con la descripción de las operaciones en la pestaña 'Detalle de operaciones'." | a transaction-reconciliation FR | **No FR at all.** Prioritario (x). Requires `expectedTransactions` reference data. |
| **(unnumbered, "58")** | "Los importes del desglose de movimientos deben coincidir con los importes en 'Detalle de operaciones'." | same reconciliation FR | **No FR.** The reference README itself calls this "item 58" and the schema's `expectedTransactions` exists to feed it — but no FR consumes it. |
| **CL-49** | "Validar que las promociones insertadas sean vigentes." | a promotions/validity FR | **No FR.** Prioritario (x). The schema *has* a `promotions[]` section with validFrom/validTo built specifically for this — and no FR reads it. |
| **CL-51** | "Extraer código Fiscal." | FR-14 | **Half-covered.** FR-14 prose says it "extracts fiscal code," but the testable Consequences cite only CL-50, CL-52, CL-53. CL-51 has no acceptance criterion. |

That is **5 fully-missing items (CL-42, 43, 45, 49, 58/importes) plus 1 untestable (CL-51)** — and
three of them (45, 49, and importes) are marked **Prioritario** in the Excel. The whole transaction-
detail reconciliation dimension (match the printed DESGLOSE against "Detalle de operaciones": dates,
descriptions, amounts — CL-42, 45, 58) is essentially absent from the FRs even though the
reference-data team built `expectedTransactions` to support it. This is the single biggest substance
gap. **The PRD cannot claim it covers the 55-item checklist; it covers ~49 with FRs and leaves the
transaction-detail/promotions cluster orphaned.**

### C2 — SM-1 ("≥90% of 55 items evaluated per statement") is unachievable given the reference-data dependency, and the metric definition hides this

**PRD location:** §7 SM-1; §4.2 FR-2 (INSUFFICIENT_DATA), FR-20; §8 OQ-2/OQ-3; reference-data README
("the bank has **not yet defined a schema or delivery mechanism**").

SM-1 targets ≥90% of 55 = ≥50 items *evaluated* per statement, "remainder INSUFFICIENT_DATA only
when data truly absent." But count the items that *cannot* be evaluated without reference data the
client has not defined or delivered:

- Header identity match (CL-2…8) needs `clientAccounts`.
- Rate / CAT (CL-9, 10) need `interestRates`/TASA.
- All cross-period (CL-17, 36, 37, 40, 41) need `priorStatements`.
- Transaction reconciliation (CL-42, 45, 58, and the sums 20/44) need `expectedTransactions`.
- Image-presence (CL-27, 30, 47) need image catalogs with `perceptualHash`.
- Legends (CL-46) need `mandatoryLegends`.
- Promotions (CL-49) need `promotions`.

That is on the order of **20+ of the 55 items** gated on reference data. OQ-2 and OQ-3 admit the
delivery mechanism *and the image-catalog sourcing are unknown*. So on day one, the realistic
"evaluated" count is the document-intrinsic checks only (pagination, blank pages, font, overlap,
logo-presence, QR, plus intra-statement-only arithmetic) — well under 50. **SM-1 ≥90% is therefore
not a v1 metric; it is a metric that only becomes meaningful after the client delivers data that §8
says is undefined.** The metric as written lets the team report failure as "INSUFFICIENT_DATA" and
still feel on-track, which is precisely the SM-C2 "silent passes" failure mode the PRD says it wants
to avoid — relabeled.

**Bite:** stakeholders will read "≥90% coverage target" as a v1 deliverable. It is not. State the
coverage SM as a function of which reference sections are present, with an explicit "intrinsic-only
floor" number for the no-reference-data case.

### C3 — The "non-breaking, additive, Veriqan→Prisma only" guarantee depends on a refactor the PRD doesn't own and a control that doesn't exist yet

**PRD location:** §4.9 FR-23/FR-24; SM-5; R4; Addendum §A ("Phase 0 work = extract generic seams…
**additively**") and §F ("enforced by an architecture test (extend the existing
`HexagonalArchitectureTests`)").

FR-23's testable consequence is "an architecture test fails the build on a `Prisma → Veriqan`
reference" and "Solution 1's existing test suite passes unchanged." Two problems:

1. The architecture test is **aspirational** — the addendum says "extend the existing
   `HexagonalArchitectureTests`." The guarantee is only as good as a test nobody has written or
   verified covers this direction. An FR whose acceptance criterion is an unbuilt test is theater
   until proven.
2. The real risk is **not** a stray `Prisma → Veriqan` reference — that is the easy case. The risk
   is Phase 0: "extract generic seams (`IDataFuser<T>`, generic pipeline/export) **out of
   oficio-specific code**." Refactoring `FusionExpedienteService` / `ExtractionOrchestrator` into
   generic interfaces *is* touching Solution 1's code paths. "Additive" extraction of an interface
   from a concrete class that Solution 1 already uses is one renamed method or one changed signature
   away from breaking Solution 1. The PRD asserts this is additive; it does not demonstrate that the
   oficio code can be generalized without a single behavioral change, and "Solution 1 regression
   suite as the gate" is only credible if that suite has real coverage (unverified here — the reuse
   analysis notes the existing solution is "MVP/beta").

**Bite:** the headline brownfield safety claim is credible *in principle* and hand-wavy *in
practice*. The dangerous work (Phase 0 genericization) is mentioned only in the addendum, is not an
FR, has no acceptance criteria, and is the most likely thing to break Solution 1. Promote Phase 0 to
a first-class, separately-gated work item with its own "Solution 1 byte-for-byte behavior unchanged"
criterion.

---

## HIGH

### H1 — "Deterministic" image-presence (CL-27/30/47, FR-12 v1) is not deterministic and depends on undelivered catalog data

**PRD location:** §1 ("plus basic image-presence detection"), §4.4 FR-12, §6.1, Addendum §B
("perceptual hashing (pHash/dHash) of rendered regions vs. catalog `perceptualHash`").

Perceptual hashing is a *similarity* technique with a threshold, not a deterministic equality test.
Worse, the Excel items it claims to satisfy are not "presence" checks at all:

- CL-27: "La imagen de la tarjeta debe **corresponder según el nombre del producto**" — i.e. the
  card image must **match the product**, not merely be present.
- CL-30: "debe **coincidir según el catálogo** de cada producto" — must match the catalog.
- CL-47: images "**coincidan con el catálogo … en el mismo orden**" — match catalog *and order*.

None of these is "is an image present." The PRD redefines all three as "presence-only" for v1 and
defers the actual requirement (match + order) to v2/CLIP. That is a legitimate scoping choice **but
it means CL-27, CL-30, CL-47 are NOT satisfied in v1** — they are partially stubbed. Presence-only
will pass a statement that has the *wrong* card image, which is the exact defect CL-27 exists to
catch. Counting these toward SM-1 coverage inflates the number. Also: pHash presence still needs the
catalog images keyed by `perceptualHash`, which OQ-3 says is unsourced — so even the stub can't run
on day one.

### H2 — Logo-on-every-page (CL-33, FR-11) is image detection dressed as "deterministic"

**PRD location:** §4.4 FR-11 ("the bank logo … appear on every page"; "A page missing the logo (by
presence detection)"); §6.1 lists FR-11 under "**Deterministic** visual/print-quality."

A bank logo in a PDF may be a vector drawing, an embedded raster, or part of a flattened page
background. "Logo present on this page" is not a font-dictionary lookup; it is template/region
matching against a known logo — perceptual or CV, with thresholds and false-positive risk
(especially on pages where the logo is small or watermarked). The PRD files FR-11 under
"deterministic" alongside genuinely deterministic checks (pagination, blank-page). Card-number
on every page (CL-34) is text and is fine; **logo presence is not deterministic** and should be
grouped with FR-12's image work and its catalog dependency, not sold as a cheap deterministic win.

### H3 — QR/fiscal on the "representación impresa" (CL-50, FR-14) assumes a decodable embedded QR; on scanned statements this is OCR/CV, and the PRD's own scope says scans are the fallback

**PRD location:** §4.5 FR-14; Addendum §B ("ZXing.NET … `[DECISION PENDING: confirm QR library.]`");
§4.2 NOTE ("v1 targets digitally-generated PDFs"); R5.

ZXing decodes a QR from a rendered image. On a digitally-generated PDF you must first rasterize the
region at sufficient DPI; on a scanned statement the QR may be low-res, skewed, or compressed —
classic OCR/CV territory, not "deterministic." FR-14's consequence "an unreadable QR on a statement
that should have one ⇒ FAIL" is reasonable, but the determination of "should have one" is itself
the §4.5 ASSUMPTION (only on statements with reembolsos/comisiones/IVA) — which depends on having
correctly parsed those very sections. The QR library is still `[DECISION PENDING]`. This is a check
the PRD treats as in-scope-deterministic but is realistically threshold-driven and scan-fragile.

### H4 — Throughput/NFR story is underspecified for 130k–200k/month (the scale honesty problem)

**PRD location:** §10 NFR-1 ("completes in seconds … the monthly sample … completes within the
review window via batch concurrency `[ASSUMPTION: confirm volume/window per OQ-1]`"); §6.2
("100% coverage scale-out … later hardening phase"); reuse analysis §5 (26–40k/day peak).

NFR-1 gives no concurrency target, no per-node throughput, no horizontal-scaling model, no infra
sizing. 200k/month over a 3–5 business-day window ≈ 26–40k/day ≈ **~1 statement/second sustained,
all day, with zero slack**, and that is just the *sample*. "Completes in seconds per statement" ×
"batch concurrency" is asserted, not derived. A statement is multi-page PDF rasterization + pHash +
QR rasterize/decode + arithmetic + marked-PDF re-render — the marked-PDF and rasterization steps are
not "milliseconds." There is no NFR for: max queue depth, retry storm behavior, the per-statement
p95 latency budget, or what hardware achieves 1/s. **"Throughput is a first-class requirement" (per
the reuse analysis) but the NFR treats it as a one-line assumption.** This will be discovered as a
capacity problem during the first real batch, not before.

### H5 — Reference-data graceful degradation is a real architecture but a soft product promise — it can "ship something that checks almost nothing"

**PRD location:** §4.7 FR-19/FR-20; R1; reference-data README ("the bank has not yet defined a
schema or delivery mechanism").

The contract + adapters + INSUFFICIENT_DATA design is genuinely good engineering. But as a *product*
strategy it has a failure mode the PRD does not confront: with no reference data delivered, VEC runs,
produces GREEN/INSUFFICIENT_DATA verdicts, looks healthy, and **catches almost nothing of value**
(no rate/CAT, no cross-period, no transaction reconciliation, no image match, no legends). The
"degrade gracefully" story is the mitigation for R1 ("reference data never arrives / arrives
messy"), but degradation is not a substitute for the data — it is a way to *ship a hollow product on
time*. There is no FR or metric that says "VEC is not production-acceptable below X% of reference
sections present." Combined with C2, this lets the program declare success while the verifier is
mostly an INSUFFICIENT_DATA generator. Define a minimum reference-data completeness bar for
go-live.

### H6 — SM-3 / NFR-2 set ≥99% precision/recall and <1% false-positive with no labeled set and no definition of the denominator

**PRD location:** §7 SM-3 ("vs. a labeled set"); §10 NFR-2; §8 OQ-5 ("confirm … how the labeled
validation set is produced").

The accuracy bar is asserted (≥99% on deterministic checks, FP <1%) while OQ-5 simultaneously admits
the labeled validation set does not exist and its production method is unknown. You cannot claim 99%
on something you cannot measure. For pure arithmetic, ≥99% is trivially true (it's deterministic, so
either the formula is right or it isn't — precision/recall is the wrong frame; the real risk is
*extraction* error feeding the arithmetic, which SM-3 doesn't isolate). For the image/QR/font checks,
≥99% precision is aggressive for threshold-based methods and there is no plan to validate it. SM-3
conflates "the arithmetic engine is correct" (provable by unit tests) with "the field extraction is
correct" (the actual hard problem, unmeasured).

---

## MEDIUM

### M1 — CL-37 (points↔pesos exchange rate = 0.1) and CL-38 (all reward buckets shown even if 0) are not clearly mapped

**PRD location:** §4.3 FR-7 (cites CL-36, 39, 40, 41); FR-6 cites the range "CL-36…CL-44."

CL-37 (validate the 0.1 points-to-pesos exchange) and CL-38 (presence of Generados/Redimidos/Por
vencer/Vencidos buckets, "aunque sea 0") are inside the cited *range* but neither has its own
Consequence. CL-37 is a specific reference-constant check (`pointsToPesosExchangeRate` exists in the
schema). CL-38 is a presence/structure check, not arithmetic. Both are Prioritario. They are
plausibly intended but not pinned by any testable criterion — same disease as C1, milder.

### M2 — CL-16 (Pago mínimo + cargos diferidos) and CL-26 (crédito disponible para disposiciones) are range-cited but not individually asserted

**PRD location:** FR-5 (CL-1, 9…16), FR-6 (CL-15…26).

CL-16 ("Pago mínimo + cargos a meses = suma…") and CL-26 ("crédito disponible para disposiciones de
efectivo = crédito disponible") are covered only by range inclusion, not by a Consequence example.
FR-6 gives worked examples for CL-18/19/20/21/24/25/10 but skips these. Low substance risk (they're
arithmetic and fall under FR-6's general statement) but they illustrate that "cite a range" is doing
a lot of unverified work — a downstream story author may not realize CL-16/26 are in scope.

### M3 — "Idempotent by file hash" (FR-1) collides with "re-running replaces prior result" (FR-22)

**PRD location:** FR-1 ("the same file content (by hash) does not create duplicate jobs"); FR-22
("Re-running a completed statement replaces its prior result").

These two are in tension and the PRD doesn't reconcile them. If ingestion is idempotent by content
hash, how does a deliberate reprocess (FR-22) get a new job for the same bytes? Is reprocess a
distinct operation that bypasses the hash guard? What about the legitimate case where reference data
changed but the PDF didn't (you'd *want* to re-verify the same bytes)? The de-dup key is probably
file-hash *plus* reference-bundle-version, but the PRD says only file content. Ambiguity that will
surface as a bug.

### M4 — Verdict model omits the INSUFFICIENT_DATA-only statement; "GREEN" can mask an unverified statement

**PRD location:** §3 Glossary (GREEN = no fails); FR-15 ("all PASS/not-applicable ⇒ GREEN";
"INSUFFICIENT_DATA … do not by themselves make a RED").

A statement where 40 of 55 checks are INSUFFICIENT_DATA and the remaining 15 PASS rolls up to
**GREEN** under FR-15's rule ("all PASS/not-applicable ⇒ GREEN"; INSUFFICIENT_DATA doesn't make
RED). To an analyst or regulator, GREEN reads as "verified clean." But it might mean "we checked
almost nothing and found no fault in the little we checked." This is the SM-C2 silent-pass risk
realized at the verdict layer. There should be a third top-line state (e.g. PARTIAL/UNVERIFIED) or
GREEN must carry a coverage figure. As written, the verdict is misleadingly reassuring — the most
dangerous kind of bug in a compliance tool.

### M5 — ROI section admits the business case is internally inconsistent and defers it

**PRD location:** §16 ("`[NOTE FOR PM] the docs cite very large annual-savings figures that are
internally inconsistent; restate against the confirmed sample size (OQ-1) before quoting.`").

The PRD is honest here, but a PRD that cannot state a defensible ROI is a PRD that cannot justify its
own scope decisions (e.g. "deterministic-first," "defer ML"). The cost/value of each phase is
asserted ("highest-$-value checks") without numbers. Acceptable as a draft caveat; not acceptable to
carry into architecture/epics. Flagging so it isn't forgotten.

### M6 — PCI-DSS / PAN storage is deferred to a NOTE, but FR-4 mandates capturing the full 16-digit card number

**PRD location:** FR-4 ("Card number is captured as 16 digits"); §11 ("`[NOTE FOR PM] confirm
PCI-DSS scope for storing card numbers; prefer masked-at-rest`").

FR-4's acceptance criterion *requires* capturing the full PAN (16 digits) and CL-34 requires
verifying the card number on every page — yet §11 simultaneously says prefer masked-at-rest and
"confirm PCI-DSS scope." If the resolution is masked-at-rest, FR-4's "16 digits captured" and the
storage/audit-trail (FR-18, §14 "before/after state immutably") need to define how full PANs flow
through extraction, verification, marked-PDF, and the immutable audit log without dragging the whole
platform into PCI scope. This is a compliance landmine left as a parenthetical, on a banking product.

---

## LOW

### L1 — Working title unconfirmed; product naming unresolved

**PRD location:** title ("*Working title — confirm.*"); OQ-7; §3 Glossary; Addendum §G
(`ExxerCube.Prisma.Veriqan` vs `ExxerCube.Veriqan`). The namespace appears as settled
(`ExxerCube.Prisma.Veriqan.*`) in §3/Addendum §F while OQ-7/§G still list it as open. Pick one.

### L2 — Font conflict acknowledged only in the addendum/reuse doc, not surfaced as a risk in the PRD body

The Excel says **Aptos** (CL-35); the PRP REQ-030 says Arial/Times. Addendum §G and the reuse
analysis flag this; the PRD body (FR-9, §11) just asserts Aptos as "the canonical bank font" with no
note that an existing internal doc contradicts it. If REQ-030 reflects a real prior client
statement, FR-9 could be verifying the wrong font. Confirm with the client, not just by docs hygiene.

### L3 — UJ-3 ("Sofía adds an API adapter, no engine changes") overstates adapter independence

FR-19 promises new adapters need "no change to the validation engine." True for *delivery*
mechanism. But if the API delivers a *different shape* of reference data (the whole point of OQ-2
being open), the adapter must map it into the bundle — and if the API exposes data the schema
doesn't model, the schema (a shared contract) changes. The journey paints adapter swaps as
zero-friction; in practice the first real source will likely force schema revision.

### L4 — "≥90%", "≤5 min", "100% within window" targets have no baseline measurement or instrument defined

SM-2 (≤5 min median disposition) and SM-4 (100% within SLA) are reasonable but there is no current
baseline captured (the "2–4 hours manual" is cited from docs, not measured here) and no statement of
how disposition time will be instrumented in v1. Metrics without a measurement plan tend to become
narrative rather than gates.

---

## Cross-check summary: CL → FR coverage matrix (against the Excel, 55 items)

Covered with a testable Consequence: CL-1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,17,18,19,20,21,22,23,24,
25,27(stub),28,29,30(stub),31,32,33,34,35,36,39,40,41,44(via FR-6 sums),46,47(stub),48,50,52,53,54,55.

Range-cited only, no individual Consequence (M1/M2): **CL-16, 26, 37, 38**.

Untestable / prose-only (C1): **CL-51** (fiscal code — in FR-14 prose, not in any Consequence).

**No FR at all (C1):** **CL-42** (operations within period dates), **CL-43** (period range on every
desglose page), **CL-45** (descriptions match Detalle), **CL-49** (promotions current), and the
unnumbered **"importes del desglose" (item 58)** (amounts match Detalle).

So the defensible statement is: **the PRD provides testable FR coverage for ~46 of 55 items, stubs 3
(CL-27/30/47 reduced to presence-only), leaves 4 range-cited but unasserted, 1 untestable, and 5 with
no FR home.** The "55-item checklist" framing in §0 is therefore not yet earned.

---

## What I'd demand before architecture/epics

1. Add FRs for the transaction-detail reconciliation cluster (CL-42, 43, 45, 58) and promotions
   validity (CL-49) — the `expectedTransactions`/`promotions` schema sections already exist to feed
   them. (Closes C1.)
2. Redefine SM-1 as coverage *conditional on reference sections present*, with an explicit
   intrinsic-only floor, and add a minimum reference-data completeness bar for go-live. (Closes
   C2/H5.)
3. Promote Phase 0 (generic-seam extraction) to a first-class work item with a "Solution 1 behavior
   unchanged" acceptance criterion, and confirm the architecture test actually exists and covers the
   `Prisma → Veriqan` direction before relying on FR-23. (Closes C3.)
4. Reclassify FR-11 logo-presence and FR-12 image checks as non-deterministic, threshold/CV-based,
   and stop counting CL-27/30/47 as "covered" until match+order (v2) lands. (Closes H1/H2.)
5. Give NFR-1 a real throughput budget (target statements/sec/node, p95 latency, scaling model) for
   the 130–200k/month case. (Closes H4.)
6. Add a PARTIAL/UNVERIFIED verdict state or attach coverage to GREEN. (Closes M4.)
7. Resolve PAN/PCI handling as a requirement, not a NOTE. (Closes M6.)
