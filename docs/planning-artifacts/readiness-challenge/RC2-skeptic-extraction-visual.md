# RC.2 — Adversarial Skeptic: EXTRACTION / VISUAL / REPORTING (Veriqan VEC)

**Date:** 2026-06-18 · **Branch:** `Liv` · **Auditor:** Claude Code (RC.2 adversarial skeptic, read-only on production code)
**Anchor:** `docs/planning-artifacts/READINESS-CHALLENGE-BRIEF-2026-06-18.md`
**Refuting:** RC1-extraction.md · RC1-visual-typography.md · RC1-verdict-reporting.md · RC0b-reality-map.md
**Stance:** Default to **refuted / not-ready** when uncertain. Every "real" is a claim to be broken against a concrete input.
**Verdict legend:** REFUTED (a concrete real-bank input produces wrong/unsafe behaviour) · SURVIVES (claim holds as stated) · UNPROVABLE-WITHOUT-CORPUS (cannot be confirmed correct, but no concrete break found).

---

## Target 1 — FR-9 font-dictionary extraction (Aptos check) · **REFUTED**

**Claimed (RC1-extraction row FR-9, RC1-visual row CL-35):** "Genuinely deterministic + real" font-family extraction;
subset-prefix stripping handled; non-Aptos → FAIL with locator.

**Code read:** `PdfPigStatementFieldExtractor.cs:1955-2011` (`ExtractFontRuns`), `:2099-2118` (`NormalizeFontFamily`),
`:1929-1930` (`s_fontStyleSuffixes`); `Cl35FontComplianceRule.cs:39-164` (rule + its own `NormalizeFamily`, `:127-152`);
`FontUsage.cs:19` (the domain record).

**Refutations (concrete breaking inputs):**

1. **Non-embedded fonts pass the "embedded font is Aptos" check — the requirement is not actually tested.**
   FR-9's intent (RC1) is *"every **embedded** font … is Aptos, read from the PDF font dictionary."* But the extractor
   reads `letter.FontName` (`:1970`) — PdfPig surfaces the font's **BaseFont name regardless of whether a font program
   is embedded**. `FontUsage` (`FontUsage.cs:19`) carries **no `IsEmbedded` / FontFile-presence flag**, and the rule
   (`Cl35FontComplianceRule.cs:83-91`) compares only the normalized name. **Breaking input:** a real statement that
   *references* `Aptos` by name but embeds **no** `FontFile3` (the viewer substitutes a metric-compatible font, or fails
   to render Aptos at all). CL-35 returns **PASS** — the exact non-compliance the rule exists to catch (a statement that
   will not render in the mandated typeface on a machine without Aptos installed) sails through. The check verifies a
   *name string*, not *embedding*.

2. **Real Aptos weight/style variants false-FAIL.** The suffix-strip list is a **closed 8-entry allowlist**
   (`:1930` / `Cl35FontComplianceRule.cs:128`): `-BoldItalic, -Bold, -Italic, -Light, -SemiBold, -Medium, -Regular,
   -Thin`. It omits common real variants. **Breaking inputs:** `ABCDEF+Aptos-Black`, `ABCDEF+Aptos-ExtraBold`,
   `Aptos-Heavy`, `Aptos-Condensed`, `Aptos-Display`, or a **space-separated** name `Aptos Display` / `Aptos Bold`
   (space, not hyphen — how many foundries name subfamilies). None match a suffix, so the family resolves to
   `"Aptos-Black"` / `"Aptos Display"` ≠ `"Aptos"` → **FAIL** on a statement that is *legitimately set in Aptos*. This is
   a **cardinal-rule violation (false-block)** on a compliant real input.

3. **Type0 / CID composite fonts are an unhandled class.** The Aptos family on a Spanish statement (accented glyphs
   `á é í ó ú ñ`) is very likely embedded as a **Type0/CID** font. PdfPig's `letter.FontName` for a Type0 font returns
   the descendant CIDFont BaseFont name (often still `ABCDEF+Aptos`), but this is **untested** — every fixture is a
   simple embedded TrueType (RC0b §2.3). If any real CID encoding surfaces an empty or composite `FontName`
   (`""`), the letter is silently skipped (`:1971-1972`); if *all* letters are skipped the rule sees
   `FontRuns.Count == 0` → **InsufficientData** (safe) — but a *partial* skip means the offending run is simply absent
   from the scan and a non-Aptos run can go **undetected** → false PASS.

4. **Subset-prefix strip is brittle by position, not by pattern.** `:2104` strips only when `name[6]=='+'` AND chars
   0–5 are A–Z. The PDF spec mandates exactly this shape, so `ABCDEF+Aptos` **survives** (correct). But a malformed or
   non-conformant producer emitting a 5- or 7-char tag, or a `+` elsewhere, leaves the prefix attached → family ≠
   "Aptos" → false FAIL. Lower-risk than (1)/(2) but still a real-input fragility.

**Net:** the happy-path subset case (`ABCDEF+Aptos-Bold`) survives, but the **central FR-9 promise ("embedded font is
Aptos") is not what the code checks** (it checks a name string, ignoring embedding), and the closed suffix allowlist
**false-blocks legitimate Aptos variants**. Both are concrete real-bank breaks.

**Verdict: REFUTED.** (RC1's "Real-unwired / genuinely deterministic" overstates it: the check is name-only, embedding-blind, and false-blocks real Aptos weights.)

---

## Target 2 — Marked-PDF generator (FR-16) Y-axis flip across page geometries · **REFUTED**

**Claimed (RC1-verdict-reporting row FR-16):** "REAL PdfSharp rendering … correct PdfPig→PdfSharp Y-axis flip …
pixel-level highlight verification."

**Code read:** `MarkedPdfGenerator.cs:241-283` (`DrawBoundingBoxHighlight`), `:249` (`page.Height.Point`),
`:252` (the flip `pageHeight - pdfPigBottom - height`); test `MarkedPdfGeneratorTests.cs:44-57, 167-221`.

**Refutations (concrete breaking inputs):**

1. **The flip ignores page rotation (`/Rotate`).** The transform is purely `pdfSharpY = MediaBoxHeight − pigBottom −
   h` (`:252`) using `page.Height.Point` (`:249`) and the raw locator. PdfPig reports word boxes in **un-rotated
   MediaBox space**, but a viewer renders a `/Rotate 90|180|270` page **rotated**, and PdfSharp's `XGraphics` draws in
   the page's *default* (un-rotated) user space. **Breaking input:** a real statement page with `/Rotate 90` (banks
   routinely rotate a landscape disclosure page). The highlight rectangle is computed for the portrait MediaBox and
   drawn un-rotated, so it lands **90° displaced** from the field the analyst is told failed. There is **no `/Rotate`
   read anywhere** in the class.

2. **The flip assumes CropBox == MediaBox at origin (0,0).** `page.Height.Point` is the MediaBox height and the X/Y
   are used raw. If the page's **CropBox is offset from the MediaBox** (very common — production PDFs crop bleed
   margins), PdfPig coordinates are relative to the CropBox/MediaBox per its own convention while PdfSharp's user space
   origin may differ, introducing a **constant X/Y offset** that shifts every highlight off-target. No CropBox
   reconciliation exists.

3. **Proven only on one page size, one orientation, zero rotation.** Every test PDF is built A4 **595×842, no
   rotation, no CropBox** (`MarkedPdfGeneratorTests.cs:51-52`), and the lone pixel test **hardcodes
   `pageHeightPt = 842`** (`:177, 201`). So the "pixel-level verification" confirms the formula **only for the exact
   geometry it was written against**. A US-**Letter** statement (612×792) is never tested; the formula would still
   compute against the *correct* `page.Height`, so size alone likely survives — but **rotation and CropBox are wholly
   unexercised and demonstrably mishandled**.

4. **Multi-page is page-count-correct but never position-verified beyond page 1.** Test 12 marks page 2 (`:416`) but
   only asserts the doc re-opens with 2 pages — **no pixel/position assertion on page 2**. A per-page `MediaBox` that
   differs between pages (mixed sizes in one statement — a portrait body + a landscape annex) is handled per-page by
   `page.Height` (good), but again **untested**, and combined with the rotation bug an annex page highlight is wrong.

5. **Offscreen / out-of-range locator is handled safely** (`:212-218` skips out-of-range pages; `:193-208` skips
   null/NoPage). **This sub-claim SURVIVES** — a null or page-0 or page-99 locator silently degrades, no crash.

**Net:** the generator is real PdfSharp and survives the *happy* geometry (A4 portrait, no rotation, CropBox==MediaBox)
and the null/offscreen-locator cases. It **breaks on the first rotated page and on any CropBox offset** — both of which
occur in real bank statements — placing the highlight on the wrong region while reporting success.

**Verdict: REFUTED** (for "highlights the right region on a real multi-page statement"). The *no-crash / page-preservation* sub-claims SURVIVE.

---

## Target 3 — Email "exactly one alert per RED, retry-not-drop" (FR-17) · **REFUTED**

**Claimed (RC1-verdict-reporting row FR-17):** RED→one email + Polly retry; "retry-not-drop is genuinely enforced …
exactly one per RED." (RC1 itself flags the intra-call caveat; this confirms and hardens it to a refutation against the
brief's full-production bar.)

**Code read:** `VecAlertService.cs:57-132` (`SendRedAlertAsync`), `:175-208` (retry pipeline); test
`VecAlertServiceTests.cs:115-124` ("exactly one email"); grep for callers (only def/DI/test, no production caller).

**Refutations (concrete breaking scenarios):**

1. **"Exactly once" is per-invocation only — there is NO persisted already-sent guard.** The method takes a
   `VerdictSummary` + `AlertContext`, composes one message, sends with retry, returns. There is **no read/write of any
   "already-alerted" state** keyed by statement/content-hash anywhere in the class or its dependencies. **Breaking
   scenario:** the same RED statement is reprocessed (the brief explicitly anticipates reprocess; `IReprocessService`
   is DI-wired per RC0b) → `SendRedAlertAsync` is invoked again → **a second email is sent** for the same RED. "Exactly
   one per RED" is false the moment the statement passes through twice.

2. **Crash-after-send is unrecoverable and ambiguous.** Even within one call: `SmtpClient` reports success at
   `:99-101`, then the process can crash before the orchestrator records the outcome (no persistence exists to record
   into). On restart there is **no record that the email was sent**, so any retry-the-job logic re-sends. Conversely a
   send that *timed out client-side but actually delivered* (classic SMTP ambiguity) returns failure → Polly retries
   (`:182-207`) → **duplicate delivery**. The "exactly once" property is **structurally unachievable** without a
   persisted dedup token, which does not exist.

3. **Partial-success across recipients is not idempotent.** `EmailMessage.To` is a recipient list (`:159-162`) sent in
   one `SendAsync`. If the SMTP server accepts some RCPTs and rejects others, `SmtpEmailSender` returns a single
   failure → Polly retries the **whole message** → recipients who already received it get a **duplicate**, while the
   "exactly one" invariant is measured per-call, not per-recipient.

4. **It is moot in production anyway — no caller.** Grep for `SendRedAlertAsync` / `IVecAlertService` returns **only**
   the interface, impl, DI registration, and tests — **zero production callers**; `VerificationPipeline` stops at
   verdict (RC0b, RC1-verdict §43). And the Worker ships **no appsettings.json** → no SMTP host/recipients configured.
   So the property "exactly one per RED" has **never been exercised end-to-end at all**; the only test
   (`VecAlertServiceTests.cs:124`) asserts one `SendAsync` **within a single call**, which is precisely the scope that
   does *not* generalize.

**Net:** retry-not-drop *within one call* is real and well-built. "Exactly one per RED" is **only intra-call**, has **no
durable dedup**, and is **unprovable + structurally impossible** across reprocess/retry/crash — which is exactly the
regime full production runs in.

**Verdict: REFUTED.** (Intra-call retry-not-drop SURVIVES as a component; the cross-call "exactly once" guarantee is refuted.)

---

## Target 4 — Pagination / blank-page / card-number visual rules · **REFUTED**

**Claimed (RC1-visual rows CL-31, CL-48, CL-34):** "Real+Wired … structurally complete and sound … the strongest rules
in the cluster."

**Code read:** `Cl31PaginationRule.cs:38-174`, `Cl48BlankPageRule.cs:29-88`, `Cl34CardNumberPresenceRule.cs:34-115`,
`Cl33LogoPresenceRule.cs:32-92`, and the facts that feed them: `PdfPigStatementFieldExtractor.cs:2485-2583`
(`ExtractPageInspectionFacts`).

**Refutations (concrete breaking inputs):**

### 4a. Blank-page (CL-48) — image-only and whitespace-glyph breaks
- **`HasContent = words.Count > 0`** (`:2507`) and blank = `!HasContent && ImageCount == 0` (`Cl48BlankPageRule.cs:64`).
  **Breaking input 1 (image-only statement):** a **scanned/flattened** statement where every page is a single
  full-page image and has *no text layer* → `HasContent=false` but `ImageCount≥1` → **never flagged blank**, while CL-34
  (card) and CL-31 (pagination) both fall to InsufficientData. The system reports "no blank pages" on a statement it
  cannot actually read — a misleading PASS.
- **Breaking input 2 (whitespace-only glyph):** a visually-blank trailing page carrying a single stray invisible/white
  word or a lone whitespace token → `GetWords()` yields ≥1 word → `HasContent=true` → **not flagged blank**, defeating
  the check.
- **Intent drift:** `DofNumeral` = *"sin espacio en blanco mayor a 2 cm"* (no whitespace **gap** > 2 cm), but the rule
  detects only **whole-blank pages** — a 2 cm intra-page gap (the actual legal control) is **never measured**. The rule
  does not implement the requirement it cites.

### 4b. Card-number (CL-34) — false-block on image/masked rendering, false-pass on substring
- `ContainsCardNumber` is a **text `Contains`** of digits-only card vs digits-only page text (`:2517-2527`).
  **Breaking input 1 (card in an image):** statements commonly render the masked/full card number inside the **header
  graphic**, not the text layer → `ContainsCardNumber=false` on every page → CL-34 **FAILs the whole statement**
  (`Cl34...:105-113`) → **cardinal-rule false-block** on a compliant statement.
- **Breaking input 2 (masking mismatch):** header shows `**** **** **** 1234` (masked) while the extractor extracts the
  full 16 digits, or vice-versa → the full digit string is **not a substring** of the masked page text → false FAIL;
  conversely if only the last-4 are extracted, those 4 digits appear coincidentally in amounts/dates/account numbers →
  **false PASS**.
- **Breaking input 3 (substring collision):** digits-only `Contains` means a 16-digit card whose digits appear as a
  **substring of a longer concatenated number** on the page (account number + amount run together after space-strip)
  yields a **false PASS**. There is no word-boundary or formatting guard.

### 4c. Pagination (CL-31) — first-match regex and single-page edge
- The fact is parsed with `PaginationPattern = \b(\d+)\s+de\s+(\d+)\b` taking the **first match** on the page
  (`:2469, 2534`). **Breaking input (body-text collision):** Spanish statement body text contains a phrase like
  `"5 de 10 pagos"` / `"cuenta 3 de 7"` **above** the actual footer `"1 de 3"` → the regex matches the **first**
  occurrence → `PaginationCurrent=5, Total=10` → CL-31 sees `Total(10) ≠ PageCount(3)` → **false FAIL** on a correctly
  paginated statement. The extractor does not anchor to a footer band/position.
- **Single-page statement:** if it carries no "N de M" label → InsufficientData (**safe, SURVIVES**). If a one-page
  statement *does* print "1 de 1", it passes — fine. So the single-page edge itself is handled; the **body-text
  collision is the real break**.

### 4d. Logo (CL-33, in scope as "card-number/pagination/blank-page visual rules") — pure proxy, false-pass
- `ImageCount >= 1` per page (`Cl33...:66-67`) from `page.GetImages().Count()`. **Breaking input:** a page with a
  promotional banner, a QR, or any decorative image **but no bank logo** → PASS. A page **missing the logo** but
  carrying any other image → PASS. This is an image-*count* proxy, **not** logo identification (self-labelled
  `:78` "escudo content matching deferred to v2"). FR-11's logo check is effectively unimplemented.

**Net:** CL-31/34/48 are structurally sound *for the synthetic fixtures' shape* but each has a concrete real-bank input
that produces a **wrong verdict** — and three of those wrong verdicts are **false-blocks** (CL-34 image/masked card,
CL-31 body-text "N de M" collision), violating the cardinal "never false-block" rule on real input. CL-33 is a proxy
that **false-passes** a missing logo.

**Verdict: REFUTED.** (The InsufficientData/abstain paths and the single-page-no-label edge SURVIVE; the positive-verdict logic breaks on real rendering.)

---

## Confirmed-real (survived the attack)

- **Marked-PDF page preservation + safe degradation** — `Modify`-mode open preserves original pages
  (`MarkedPdfGenerator.cs:139`); null / NoPage / out-of-range locators are silently skipped, no crash
  (`:193-218`). The PdfSharp rendering itself is genuine (pixel test confirms a red-tinted highlight on the canonical
  A4 geometry).
- **Email retry-not-drop, intra-call** — Polly exponential-backoff retry, permanent failure logged at Error + returned
  as typed `Result.WithFailure`, never silently swallowed (`VecAlertService.cs:114-125, 175-208`). As a *component*
  this is real.
- **Abstain/InsufficientData discipline across all four visual rules** — every rule has explicit InsufficientData paths
  (null model / empty pages / not-extracted card / no pagination label) and returns them rather than guessing
  (`Cl31...:62-85`, `Cl48...:53-61`, `Cl34...:58-88`, `Cl35...:64-74`). The *plumbing* of "abstain over false-fail" is
  genuinely present (it's the *facts* that can be wrong, not the abstain wiring).
- **Subset-prefix happy path (`ABCDEF+Aptos-Bold` → `Aptos`)** — correctly stripped by both extractor and rule
  (`:2104`, `Cl35...:138`).
- **Pagination logic correctness (given correct facts)** — duplicate/gap/total-mismatch detection in `Cl31` is sound
  arithmetic (`:88-165`); the bug is in the *fact extraction*, not the rule.
- **Single-page-no-pagination-label edge** — correctly yields InsufficientData, not a false FAIL.

## Refuted / downgraded

| Target | RC.1 class | RC.2 verdict | Most important break (file:line + input) |
|---|---|---|---|
| **FR-9 / CL-35 font (Aptos)** | Real-unwired / Partial | **REFUTED** | Name-only check ignores embedding: non-embedded "Aptos" PASSes (`Cl35...:83-91`, `FontUsage.cs:19` has no IsEmbedded); real `Aptos-Black`/`Aptos Display` false-FAILs (suffix allowlist `:1930`). |
| **FR-16 marked-PDF Y-flip** | Real-unwired | **REFUTED** (position) | Flip ignores `/Rotate` and CropBox offset (`MarkedPdfGenerator.cs:249-252`); a `/Rotate 90` page highlights 90° off. Page-preservation/null-locator SURVIVE. |
| **FR-17 "exactly one per RED"** | Real-unwired | **REFUTED** (cross-call) | No persisted dedup guard anywhere in `VecAlertService.cs`; reprocess/retry/crash → duplicate or ambiguous send. Intra-call retry-not-drop SURVIVES. |
| **CL-31 pagination** | Real+Wired | **REFUTED** | First-match regex (`:2469, 2534`) grabs body-text "5 de 10" before footer "1 de 3" → false FAIL. Abstain paths SURVIVE. |
| **CL-34 card-number** | Real+Wired | **REFUTED** | Text `Contains` (`:2517-2527`): card rendered in header image, or masked-vs-full mismatch → false FAIL (cardinal violation); substring collision → false PASS. |
| **CL-48 blank-page** | Real+Wired | **REFUTED** | `HasContent=words>0` (`:2507`) misses whitespace-glyph blanks; image-only scan never flagged; cited 2 cm-gap control unimplemented. |
| **CL-33 logo** | Partial/proxy | **REFUTED** | `ImageCount>=1` proxy (`:66-67`) false-passes a missing-logo page that has any other image. |

**Cross-cutting (inherited, applies to all four targets):** none of these has *ever* been exercised on a real CONDUSEF
statement — the only PDFs are the 3 `KnownSynthetic` non-compliant fixtures (RC0b §2.3), and the
verdict→report→alert chain has **no production caller / no Worker entry point** (RC0b Part 1). So even the SURVIVING
components are **UNPROVABLE-WITHOUT-CORPUS at the end-to-end bar** the brief demands.

---

*This refutation covers the four assigned targets only (FR-9 font, FR-16 marked-PDF, FR-17 email, the
pagination/blank-page/card-number/logo visual rules). Validation-computation rules (E11), section/table geometry (E10),
persistence immutability, and ingestion are other skeptics' scope.*
