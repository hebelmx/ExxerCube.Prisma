# RC.1 — Requirement → Evidence Trace · VISUAL / TYPOGRAPHY cluster (Veriqan VEC / Epic 12 + Epic 5)

**Date:** 2026-06-18 · **Branch:** `Liv` · **Auditor:** Claude Code (RC.1 visual/typography cluster, read-only — no production code modified)
**Anchor:** `docs/planning-artifacts/READINESS-CHALLENGE-BRIEF-2026-06-18.md` · **Inputs:** RC0a scope register, RC0b reality map.
**Bar:** Full production — **end-to-end evidence on real input, NOT a green unit test.** Believe wiring + file:line; never comments/prose.

---

## 0. Scope & method

Cluster covers **FR-9, FR-10, FR-11, FR-12** plus the **Epic-12** legal-form rules (section-size cap, point-size floor, advertising placement, mandated bold). Concretely, 11 production rules in `02 Infrastructure/Veriqan.Infrastructure.Visual/Rules/` and the PdfPig extraction methods in `02 Infrastructure/Veriqan.Infrastructure.Extraction/PdfPigStatementFieldExtractor.cs` that populate the facts they consume.

**Method:** read every rule, the extractor methods that produce its facts, the DI wiring, and the test substrate. Cross-checked: (a) is the fact real PDF-derived or hand-built; (b) is the rule wired into the production engine; (c) is there any assertion of a *correct visual verdict on a real-rendered statement*.

**Headline ground truth (decides every "E2E" cell below):**
- The visual facts ARE produced by **real PdfPig parsing** of the actual PDF bytes (font dictionary, word geometry, CTM-accounted point sizes, embedded-image counts, page geometry). The extractor is real, not a stub. (`PdfPigStatementFieldExtractor.cs:1955, 2036, 2177, 2285, 2486`).
- **But every Visual.Tests test is "pure in-memory — no PDF fixtures required"** (`Cl35FontComplianceRuleTests.cs:16-17`): rules are exercised against hand-built `StatementModel`/`TextTypographySample`/`PageInspectionFacts` objects authored by the test, never the extractor output.
- The **only** place a visual rule meets a real-rendered PDF is the orchestration pipeline E2E test (`VerificationPipelineEndToEndTests.cs:224`), which runs the whole engine over a real PRP2 PDF — but asserts **only** `findingCount > 0` and `signal != Blocked` (`:243, :248`). It never asserts that any specific visual rule produced the *correct* verdict.
- The 3 PRP2 PDFs are **`KnownSynthetic`, non-compliant** (RC0b §2.3): they deliberately fail CL-31/34/35 + LAW-TYPO-MINSIZE etc. There are **zero `KnownGood`** specimens, so a "correct PASS" of any visual rule has never been observed on real input.
- **FR-12 (catalog image / perceptual hash) has no rule at all.** The reference-data schema defines `ImageRef.perceptualHash`/`sha256` and `SequentialImage` (`VecReferenceBundle.cs:98-104, 166`), but **no rule in Visual or Validation consumes them** — no pHash is computed over any rendered page. CL-27/30/47 are unimplemented.

So the cluster's honest readiness class is dominated by two facts: the rules are **real + wired** logic over **real-extracted facts**, but the *only proof they run on real input* is a smoke-level "engine ran" assertion — there is **no E2E proof of a correct visual verdict on a real statement**, and one whole FR (FR-12) is missing.

---

## 1. Requirement → Evidence trace table

> Class ∈ {Real+Wired+E2E, Real-unwired, Partial, Stub, Missing, Unknown}.
> "Wired" = discovered by Scrutor into the production `VecValidationEngine` via `AddVeriqanVisual()` (`VeriqanVisualServiceCollectionExtensions.cs:48-52`) — confirmed for all 11 rules below.
> "E2E" here would require a correct verdict proven on a real rendered statement — **not achieved for any rule** (see §0).

| Requirement-ID | Intent | Built-state class | Evidence (file:line / test / none) | Gap to E2E-readiness |
|---|---|---|---|---|
| **FR-9 / CL-35** (E5.1) | Every text run's embedded font is Aptos, read from the PDF font dictionary (deterministic, not visual). Non-Aptos → FAIL w/ locator. | **Partial** (Real+Wired; not E2E-proven) | Rule `Cl35FontComplianceRule.cs:39-117`; required family from bundle, fallback `"Aptos"` (`:77`); facts from real PdfPig `letter.FontName` per page (`PdfPigStatementFieldExtractor.cs:1968-1995`). Tests in-memory only (`Cl35FontComplianceRuleTests.cs:16`). Runs in pipeline over real PDF but no per-rule assert (`VerificationPipelineEndToEndTests.cs:243`). | Font *family* is real, but **subset/synthetic font names on the PRP2 fixtures** mean a correct PASS on a real-Aptos statement is unproven (no `KnownGood`). Bold/weight detection is name-token only — no glyph stroke-width (shared limitation w/ LAW-TYPO-BOLD). |
| **FR-10a / CL-28** (E5.2) | Detect overlapping text (glyph/word bbox X-intersection beyond threshold) → FAIL. Threshold from config, recorded on Finding. | **Partial** | Rule `Cl28TextOverlapRule.cs:47-141`; incidents from real word-geometry pairs per Y-band (`PdfPigStatementFieldExtractor.cs:2177-2219`, epsilon 2.0 pt `:2145`). Threshold is a **fixed compile-time constant 1.0 pt** — `ToleranceConfig` is read but **carries no `VisualOverlapThresholdPoints` field** so override is impossible (`Cl28TextOverlapRule.cs:60, 157-164`, self-documented as "future enhancement"). | Geometry is real, but **threshold is uncalibrated & non-configurable** despite FR-10 saying "thresholds from config." No E2E correct-verdict proof. |
| **FR-10b / CL-29** (E5.2) | Section headers bold + uppercase → else FAIL. | **Partial** | Rule `Cl29HeaderStylingRule.cs:45-121`; `SectionHeaderStyle` facts from real PdfPig (`PdfPigStatementFieldExtractor.cs:2285-2352`). Bold = font-name contains "Bold"; uppercase = char-case. | Header **detection is keyword-list matching of known Spanish titles** (`:2285+`) — a header rendered with a synthesized-bold base family (no "Bold" token) would false-Fail; abstains only when *no* header found. Uncalibrated; not E2E-proven. |
| **FR-11a / CL-31** (E5.3) | Pagination correctness ("N de M" consistent, M = page count, no gaps). | **Real+Wired** (not E2E-proven correct) | Rule `Cl31PaginationRule.cs:38-173`; `PaginationCurrent/Total` parsed from real page text via regex (`PdfPigStatementFieldExtractor.cs:2530-2547`). Logic is complete and sound. | Strongest rule in the cluster — pure structural, fully real. Gap is only the missing real-statement correct-verdict proof. |
| **FR-11b / CL-48** (E5.3) | No blank pages → else FAIL. | **Real+Wired** (not E2E-proven correct) | Rule `Cl48BlankPageRule.cs:29-87`; blank = `!HasContent && ImageCount==0` from real word/image counts (`PdfPigStatementFieldExtractor.cs:2507-2510`). | Real. Note DofNumeral cites "sin espacio en blanco mayor a 2 cm" but rule is **whole-page-blank only** — the 2 cm intra-section gap is a *different* rule (Epic 10, out of this cluster). Slight intent-vs-name drift. |
| **FR-11c / CL-33 (logo)** (E5.3) | Bank logo present on every page → else FAIL. | **Partial / proxy** | Rule `Cl33LogoPresenceRule.cs:32-91`; checks `ImageCount >= 1` per page (`:66-67`) from `page.GetImages().Count()` (`PdfPigStatementFieldExtractor.cs:2510`). | **Image-count proxy, NOT logo identification** — any embedded image (a promo banner, a QR) satisfies it; a missing logo on a page that has any other image passes. Self-labelled "escudo content matching deferred to v2" (`:78`). This is a known stand-in, not the FR-11 logo check. |
| **FR-11d / CL-34 (card #)** (E5.3) | Card number present on every page → else FAIL. | **Real+Wired** (not E2E-proven correct) | Rule `Cl34CardNumberPresenceRule.cs:34-114`; `ContainsCardNumber` = card digits in page text (`PdfPigStatementFieldExtractor.cs:2517-2528`). Abstains if card # not extracted. | Real & sound. Depends on header card-# extraction quality (FR-4, other cluster). Not E2E-proven. |
| **FR-12 / CL-27, CL-30, CL-47** (E5.4) | Verify presence of catalog images (card / important-message / sequential) via **perceptual hashing** vs. catalog; absent → FAIL; absent catalog → INSUFFICIENT_DATA. | **Missing** | **No rule file exists** (`git ls-files` Visual/Rules — none for CL-27/30/47). Reference schema defines `ImageRef.perceptualHash`/`sha256` + `SequentialImage` (`VecReferenceBundle.cs:98-104, 166`) but **no consumer** (grep of Visual+Validation for `CardImage`/`perceptualHash`/`ImageHash` → 0 rule hits). **No perceptual-hash NuGet package** is referenced anywhere (AR-5's "perceptual-hash package" is unrealized; `git ls-files | grep -i phash` → none). | Entire FR is unbuilt. Needs: a pHash library, page-image rendering (PDFtoImage/EmguCV per AR-4), a pHash-vs-catalog rule, and catalog reference data populated. CL-33 logo proxy is the only image check that exists. |
| **E12.1 / LAW-TYPO-MINSIZE** | Body text ≥ 8 pt; fecha-límite ≥ 10 pt bold; abstain when size indeterminable. | **Partial** | Rule `TypographyPointSizeFloorRule.cs:71-315`; uses **real CTM-accounted `letter.PointSize`** from PdfPig (`PdfPigStatementFieldExtractor.cs:2065`). Floors 8.0 / 10.0 pt are **hardcoded constants** (`:76-77`), epsilon 0.25 biases to Pass. Sophisticated fecha-límite locate + abstain logic. | Point size is real & CTM-correct. Floors are **legal-text-derived but uncalibrated against any real corpus** (corpus-gated per RC0a). Fecha-límite sub-check is best-effort heuristic; never proven on a real statement with a real fecha-límite field. |
| **E12.2 / LAW-TYPO-BOLD** | ~10 mandated fields bold by font-name/weight; indeterminate weight → INSUFFICIENT_DATA (never false-FAIL). | **Partial** | Rule `MandatedBoldFieldsRule.cs:74-241`; tri-state `WeightClass` classifier on **font-name tokens only** (`:373-399`), proximity-join to 9 extracted summary-field locators (`:440-478`). Quorum ≥ 3 (`:88`). | **No glyph-level stroke-width** — bold is inferred purely from the embedded font *name*. A statement rendering bold via synthesized stroke on a bare family name is correctly classified Indeterminate (safe), but that means the rule **cannot positively confirm bold** for such fonts — it can only catch explicit `-Regular`/`-Light` names. Uncalibrated; not E2E-proven. |
| **E12.3 / LAW-ADS-PLACEMENT** | §12 ≤ 700 chars & no ads; ads outside §21/§28 → FAIL; abstain if section boundaries unavailable. | **Partial / heuristic** | Rule `AdvertisingPlacementRule.cs:74-276`. §12 length sub-check is deterministic but fires only > **805 chars** (700 × 1.15 uncertainty margin, `:85-89`) to mask Epic-10 boundary bleed. Ad detection = **7-phrase hardcoded allowlist** (`:128-137`), self-described "corpus-starved … MUST be calibrated against a real CONDUSEF corpus before tightening" (`:30-37, 123-126`). | Ad heuristic is a deliberately-tiny keyword list → near-guaranteed false-negatives in the wild. §12 cap effectively raised to 805 to avoid extraction false-Fails. **página-cero permitted zone is unmodeled** (`:66-72`) — silent gap. Uncalibrated; not E2E-proven. |
| **E12.4 / LAW-SEC-SIZECAP** | §17 ≤ ¼ page; §21/§28 ≤ ⅓ page; abstain if area unmeasurable. | **Partial** | Rule `SectionSizeCapRule.cs:46-294`; real page-height + section-heading geometry from Epic-10 `DetectedSection` locators (`:300-331`); measures extent to physically-adjacent same-page heading; epsilon 0.02 (`:55`). | Geometry is real **but depends entirely on Epic-10 section detection** (out of this cluster); abstains heavily (any cross-page section → abstain). Thresholds ¼/⅓ are legal, not calibrated. On the synthetic fixtures it largely abstains. Not E2E-proven. |

**Per-class tally (12 requirement rows):**

| Class | Count | Rows |
|---|---|---|
| Real+Wired+E2E | **0** | — (no visual rule has a correct-verdict proof on a real rendered statement) |
| Real-unwired | 0 | — (all 11 rules ARE wired) |
| Partial | **9** | FR-9/CL-35, FR-10a/CL-28, FR-10b/CL-29, FR-11c/CL-33, E12.1, E12.2, E12.3, E12.4 (+ CL-33 proxy counted here) |
| Real+Wired (sound logic, only E2E-proof missing) | **3** | FR-11a/CL-31, FR-11b/CL-48, FR-11d/CL-34 |
| Stub | 0 | — |
| Missing | **1** | FR-12 / CL-27, CL-30, CL-47 |
| Unknown | 0 | — |

> The 3 "Real+Wired" pagination/blank/card rows are structurally complete and would be **Real+Wired+E2E** the moment a single real `KnownGood` statement run asserts their verdict. The 9 "Partial" rows additionally carry an uncalibrated threshold or a heuristic/proxy substitution. No row reaches E2E today because no real-statement correct-verdict assertion exists.

---

## 2. Uncalibrated-threshold inventory

Every numeric constant a visual/typography verdict depends on, with its source and configurability. **All are hardcoded; none is calibrated against a real CONDUSEF corpus** (which does not exist — RC0a §C6).

| # | Threshold | Value | Where | Source of value | Configurable? |
|---|---|---|---|---|---|
| 1 | Body-text point-size floor | **8.0 pt** | `TypographyPointSizeFloorRule.cs:76` | CONDUSEF Acuerdo legal text | No (BaselineLocked constant) |
| 2 | Fecha-límite point-size floor | **10.0 pt** | `TypographyPointSizeFloorRule.cs:77` | Legal text | No |
| 3 | Point-size Pass-bias epsilon | **0.25 pt** | `TypographyPointSizeFloorRule.cs:80` | Engineering guess | No |
| 4 | Same-line vertical band (typo join) | **5.0 pt** | `TypographyPointSizeFloorRule.cs:85`, `MandatedBoldFieldsRule.cs:80` | Matched to extractor Y-band | No |
| 5 | Horizontal proximity window (two-col safety) | **−5.0 / +300 pt** | `TypographyPointSizeFloorRule.cs:90-91`, `MandatedBoldFieldsRule.cs:83-84` | Engineering guess | No |
| 6 | Text-overlap rule threshold | **1.0 pt** | `Cl28TextOverlapRule.cs:60` | Below extractor epsilon | No — `ToleranceConfig` field absent (`:157-164`) |
| 7 | Text-overlap extractor epsilon | **2.0 pt** | `PdfPigStatementFieldExtractor.cs:2145` | Kerning exclusion guess | No |
| 8 | Overlap Y-band tolerance | **4.0 pt** | `PdfPigStatementFieldExtractor.cs:2152` | Guess | No |
| 9 | Mandated-bold quorum | **3 fields** | `MandatedBoldFieldsRule.cs:88` | Engineering guess | No |
| 10 | §12 legal char cap | **700 chars** | `AdvertisingPlacementRule.cs:79` | Legal text | No |
| 11 | §12 uncertainty margin / Fail threshold | **1.15 → 805 chars** | `AdvertisingPlacementRule.cs:85-89` | Guess (masks E10 bleed) | No |
| 12 | Promotional-marker allowlist | **7 phrases** | `AdvertisingPlacementRule.cs:128-137` | Hand-picked, "corpus-starved" | No |
| 13 | §17 size cap | **¼ page (0.25)** | `SectionSizeCapRule.cs:51` | Legal text | No |
| 14 | §21/§28 size cap | **⅓ page (0.333)** | `SectionSizeCapRule.cs:52` | Legal text | No |
| 15 | Section-size Pass-bias epsilon | **0.02** | `SectionSizeCapRule.cs:55` | Guess | No |
| 16 | Header-band tolerance (detection) | (in extractor) | `PdfPigStatementFieldExtractor.cs:2285+` | Guess | No |

**Count: 16 distinct uncalibrated thresholds/constants.** Of these, items 1, 2, 10, 13, 14 are *legal-text-derived* (likely correct but unverified against rendered real PDFs); the rest (3, 5, 6, 7, 8, 9, 11, 12, 15, 16 — **10+**) are pure engineering guesses with no legal or measured grounding. Item 12 (the 7-phrase ad allowlist) is the single most fragile.

---

## 3. Biggest readiness gaps (≤5)

1. **FR-12 is entirely missing — no perceptual-hash image verification exists.** No rule, no pHash library, no page rendering, no catalog data consumed. The reference schema has `perceptualHash`/`sha256` fields with zero consumers. This is the only *wholesale-unbuilt* FR in the cluster; CL-33's image-count proxy does not substitute for it.

2. **No visual rule has a correct-verdict proof on a real rendered statement.** 468 Visual.Tests are 100% hand-built in-memory facts; the lone real-PDF path asserts only "engine ran / not Blocked." Combined with zero `KnownGood` specimens, **no visual check is proven to PASS a compliant statement or to FAIL the right thing on a real one** — the cardinal "never false-block" property is untested on real input.

3. **CL-33 logo and CL-29 header-bold are proxy/heuristic, not the real check.** CL-33 accepts *any* embedded image as "logo present." CL-29 and LAW-TYPO-BOLD infer bold from the font *name* only (no glyph stroke-width), so synthesized-bold-on-bare-family fonts cannot be positively confirmed bold and could false-Fail header styling.

4. **Advertising detection is a 7-phrase hardcoded allowlist, self-declared corpus-starved.** It will miss almost all real advertising (false-negatives by design) and the §12 cap is silently relaxed from 700 to 805 chars to dodge Epic-10 extraction bleed — so the legal 700-char ceiling is not actually enforced. página-cero permitted zone is unmodeled.

5. **16 uncalibrated thresholds, 10+ of them pure guesses, with overlap threshold non-configurable despite FR-10 mandating config-driven thresholds.** All geometry/typography pass-fail boundaries are spec- or guess-derived and unvalidated against any real distribution (corpus does not exist — the universal Veriqan blocker).

---

## 4. Note on what IS genuinely strong here

To be fair to the build: the **extraction substrate is real, not faked**. Font family (font dictionary), word/glyph geometry, CTM-accounted point size, embedded-image counts, page geometry, and pagination labels are all pulled from genuine PdfPig parsing of the actual PDF bytes — there is no hand-waving in the extractor. The rules' abstain-logic (InsufficientData over false-Fail) is consistently and carefully implemented (every rule has explicit InsufficientData paths). The pagination (CL-31), blank-page (CL-48), and card-number (CL-34) rules are structurally complete and sound. The gap is **not** "the code is fake" — it is "the code has never been proven correct against a real compliant statement, one whole FR (image pHash) is absent, and the thresholds are uncalibrated."

---

*This trace covers the VISUAL / TYPOGRAPHY cluster only. Adversarial refutation of these "Partial/Real" claims is RC.2; corpus/real-data lens is RC.3.*
