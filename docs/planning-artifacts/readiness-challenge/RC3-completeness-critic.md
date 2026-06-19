# RC.3 — Completeness Critic · Unknown-Unknowns (Veriqan VEC)

**Date:** 2026-06-18 · **Branch:** `Liv` · **Lens:** Negative-space / completeness critic (RC.3)
**Role:** Name what NO requirement captured. Read-only; every claim verified against code before assertion.
**Bar:** CONDUSEF-statement compliance gate inside a Mexican bank — a *never-false-block* preventive gate.

> **How to read this:** an entry is admitted here ONLY if it is **absent from the 121-requirement
> register (RC0a)** AND **not already the headline of an RC.1 cluster gap.** Where the register touches
> an *adjacent* concern (e.g. PG-6 card-masking), the entry is narrowed to the **uncaptured slice**
> (e.g. the whole *retention/erasure/LFPDPPP* regime, which PG-6 does not name). The known dominating
> gaps — no real corpus, no Worker ingestion entry point, single-tenant verdict, uncalibrated thresholds,
> in-memory persistence, no authn/TLS, no observability backend — are **deliberately not repeated.**

---

## Theme A — Correctness (silent-correctness class — the most dangerous for a never-false-block gate)

### A1. Scanned / image-only PDF silently becomes an all-sections-FAIL (false RED on a compliant statement)
- **Why it matters for a bank gate:** Real statement archives routinely contain re-scanned, re-printed, or
  image-only PDFs (no text layer). The brief's cardinal rule is "never false-block." This input class
  silently violates it.
- **Evidence it's unaddressed:** `PdfPigStatementFieldExtractor.ExtractDetectedSections`
  (`...Extraction/PdfPigStatementFieldExtractor.cs:2996-2998`) guards an empty text layer by returning
  `BuildAllAbsent()`. `BuildAllAbsent` (`:3224-3256`) emits every non-indeterminate mandatory section as
  `DetectionStatus = Absent` **with `IsApplicable: true`** (`:3243-3251`) — i.e. a positive "this section
  is MISSING" signal, not an abstain. The §1–28 presence rule (FR-28 / E10.1) therefore **FAILs every
  section**, producing RED on a fully-compliant scanned statement. The pipeline
  (`VerificationPipeline.cs:146-167`) treats `ExtractFullAsync` success as success — there is **no
  whole-document "text layer empty/sparse → BLOCKED/InsufficientData" guard** distinguishing "I cannot
  read this PDF" from "these sections are genuinely missing." No FR/NFR/PG row names scanned-PDF
  handling or an OCR fallback (AR-4 fixes the engine to PdfPig text only). **Absent from register.**
- **Severity: Blocks** (silent-correctness; direct cardinal-rule violation on a real input class).

### A2. Non-deterministic ambient-clock in extraction → same statement can yield different verdicts (NFR-5 break)
- **Why it matters:** NFR-5 demands a reproducible verdict for the regulator (same statement + same
  rules → same Findings, months later). A wall-clock dependency in the extract path makes the verdict
  depend on *when* it ran.
- **Evidence:** `PdfPigStatementFieldExtractor.cs:1360` repairs a truncated charge-date year using
  `operationDate?.Year ?? DateTimeOffset.UtcNow.Year`. When the operation date is also unreadable, the
  repaired transaction year is taken from the **processing wall-clock** — so a statement processed in Dec
  2025 vs Jan 2026 can extract a different `chargeDate` year, flowing into the within-period date checks
  (CL-42/43) and potentially flipping a Finding. The clock is correctly abstracted via `TimeProvider`
  elsewhere (ingestion/disposition/reprocess), making this lone `DateTimeOffset.UtcNow` an inconsistency,
  not a pattern. NFR-5 is in the register but this *specific reproducibility hole inside extraction* is
  not named anywhere. **Absent from register.**
- **Severity: Degrades** (silent-correctness; narrow trigger — only on doubly-truncated dates — but a
  real NFR-5 violation that no test covers because fixtures don't exercise it).

### A3. Statement-timezone vs UTC in all date/period math is undefined
- **Why it matters:** Day-count, cut-date, and within-period checks (FR-5 day-count = span; CL-42/43
  operation-date-within-period) are **date arithmetic on `DateOnly`** extracted from the statement, but
  the statement's issuing timezone (America/Mexico_City) is never modeled. `VerificationJob.ReceivedAtUtc`
  is UTC; extracted dates are timezone-naive `DateOnly`. A statement cut at 23:59 local on the last day of
  a period can be off-by-one against any UTC-derived boundary. No requirement states which timezone the
  period boundaries are evaluated in.
- **Evidence:** `VerificationJob.cs:19,40` (UTC received-at); `TryParseSpanishDate`
  (`PdfPigStatementFieldExtractor.cs:1511-1544`) produces naive `DateOnly`; no `TimeZoneInfo`/`es-MX`
  anywhere (grep: 0 timezone references in Veriqan). **Absent from register** (register has no timezone row).
- **Severity: Degrades** (latent off-by-one on boundary statements; today masked because comparisons are
  DateOnly-to-DateOnly, but undefined-by-design).

### A4. Mexican number format is assumed to equal invariant — European-format amounts silently mis-parse
- **Why it matters:** All money parsing strips `,` then parses with `InvariantCulture` (i.e. assumes
  `1,234.56` US/MX-style). Mexican statements *usually* use that form, so this is correct for the common
  case — but the parser has **no es-MX culture and no guard against the European `1.234,56` form**: it
  would strip nothing useful, parse `1.234` as 1.234, and silently report a wrong amount as
  `Extracted`@confidence 1.0, feeding a false arithmetic Finding.
- **Evidence:** representative sites `PdfPigStatementFieldExtractor.cs:1299-1301`, `:1605-1614`,
  `:3624-3660`, `:4218-4279` (`Replace(",", "")` + `InvariantCulture`). No `es-MX`/`new CultureInfo`
  anywhere in Veriqan production code (grep). The register has no locale/number-format row.
  **Absent from register.**
- **Severity: Degrades** (silent-correctness; low probability on real CONDUSEF layouts, but unbounded —
  a wrong number passes as high-confidence and there is no corpus to catch it).

### A5. Confidence is a 3-value constant, so a *wrongly-extracted* value cannot self-abstain
- **Why it matters (uncaptured slice):** RC.1-extraction names "confidence is a constant," but the
  *consequence for the abstain-safety contract (NFR-8/E9.5)* is an unknown-unknown: NFR-8 promises "low
  confidence → InsufficientData, never false-FAIL," yet the only confidence signal is binary found(1.0)/
  not-found(0.0). A value that is *found but geometrically mis-associated* on a real (non-fixture) layout
  is emitted at confidence 1.0 and **cannot trip the abstain path** — so NFR-8's guarantee is structurally
  un-deliverable on real input, not merely uncalibrated. No requirement states a minimum confidence-model
  fidelity for NFR-8 to hold.
- **Evidence:** `ExtractedField.cs` / `TableCell.cs` constant confidences (cited in RC1-extraction);
  the NFR-8 abstain path keys off this. **Absent from register** (register asserts NFR-8 as deliverable).
- **Severity: Degrades→Blocks** (it is the mechanism by which A1/A4-class silent errors evade the
  cardinal-rule safety net).

---

## Theme B — Compliance / Legal

### B1. No PII data-retention / erasure / data-minimization regime (Mexican LFPDPPP)
- **Why it matters:** The system handles client name, address, RFC, CLABE, card# and (intended) statement
  PDFs — *datos personales* under LFPDPPP. A bank's privacy office will demand a retention schedule,
  data-minimization, a deletion/erasure (ARCO) path, and a lawful-basis/processing record. PG-6 names
  *card masking* only; nothing names retention, erasure, or minimization.
- **Evidence:** grep for `retention|erasure|LFPDPPP|purge|anonymi|data minim` over Veriqan production
  code → **0 hits**. No TTL on any entity, no delete path beyond EF defaults, no minimization (the
  extractor pulls full RFC/CLABE/card#). PRD §13 mentions ~7-year retention (→ PG-5) but only for the
  *audit trail*, not for the *personal data*. **Absent from register.**
- **Severity: Blocks** (a Mexican bank's privacy/compliance gate cannot onboard a processor with no
  retention/erasure design).

### B2. No rule-version ↔ Acuerdo-edition governance / migration process
- **Why it matters (uncaptured slice):** E13.4/AR-9 ask the *verdict* to be stamped with engine +
  bundle + Acuerdo edition. But the **operational governance** — how rules are versioned when CONDUSEF
  republishes the Acuerdo (e.g. 20/2022 → a future edition), how in-flight statements are migrated, how
  an Acuerdo-edition *field* is even modeled — has no home. There is **no `AcuerdoEdition`/`RuleVersion`
  field on any rule or verdict entity** (the E13.4 stamp is itself E13-gated and unbuilt). When the law
  changes, there is no defined process and no schema to record which edition a verdict was judged under.
- **Evidence:** grep `AcuerdoVersion|AcuerdoEdition|RuleVersion|edition` → no such field on rules or
  `JobVerdict`; DOF numerals are hardcoded string constants per rule (RC1-validation), with no edition
  qualifier. Register C5/13.4 touch versioning but not the **OPS migration process or an edition field on
  the rule model.** **Absent from register.**
- **Severity: Degrades** (becomes Blocks the first time the Acuerdo is amended in production).

### B3. Reference-bundle authenticity / provenance is untrusted input
- **Why it matters:** The verdict's correctness depends entirely on the Reference Bundle (TASA tables,
  expectedTransactions, legends, tolerances). For a regulated gate, *where the bundle came from and that
  it was not tampered with* is a control point. The CSV/DB adapter validates **schema** but not
  **authenticity** (no signature, no checksum, no provenance/approver record). A wrong-but-schema-valid
  bundle silently produces wrong verdicts at scale.
- **Evidence:** `CsvReferenceDataAdapter` + schema validation (RC0b §Bind); no signing/checksum/approval
  field on `BundleMetadata` (grep shows metadata is descriptive only). Register covers bundle *validity*
  (FR-2, BLOCKED on invalid) but not **bundle authenticity/provenance/approval.** **Absent from register.**
- **Severity: Degrades** (a silent wrong-reference-data path; bank audit would demand bundle provenance).

---

## Theme C — Operations / SRE

### C1. Poison-statement / one-PDF-crashes-the-worker resilience is unmodeled at the host level
- **Why it matters (uncaptured slice):** NFR-3/PG-17 say a single statement must not halt the batch and
  malformed PDFs must degrade gracefully. But PdfPig parsing of a *maliciously malformed or
  pathologically large* PDF can throw, hang, or OOM **inside the extractor** — and there is no per-item
  timeout, no memory bound, no sandbox. The pipeline catches `Result` failures but not a hard crash/hang
  of the parse thread. At 130k–200k statements/month, one poison PDF stalling a worker is an SRE event no
  requirement addresses (PG-17 assumes the failure surfaces as a typed `Result`, which a hang/OOM does not).
- **Evidence:** `ExtractFullAsync` opens `PdfDocument.Open(pdf)` with no timeout/size guard
  (`PdfPigStatementFieldExtractor.cs:335` and the public entry `:148/:177`); pipeline only branches on
  `IsFailure`/`IsCancelled` (`VerificationPipeline.cs:149-162`), not on a thrown/hung parse. **Absent from
  register** (PG-17 names "malformed → BLOCKED" but not parse-hang/OOM/timeout/resource bounds).
- **Severity: Degrades** (a single crafted/corrupt PDF can stall a worker in the monthly batch window).

### C2. Idempotency key is the raw-byte hash, so a re-scan / re-export of the *same logical statement* is a new job
- **Why it matters:** FR-1 dedups by SHA-256 of the PDF bytes (`VerificationJob.ContentHash`). Two
  byte-different exports of the *same logical statement* (re-rendered, re-compressed, font-subset
  re-embedded) hash differently and create **two jobs with two verdicts** — and conversely there is no
  logical (client+period+product) identity to detect duplicates or to *prevent re-verifying a statement
  already dispositioned by a human*. For an audit trail this risks duplicate/contradictory verdicts on
  "the same" statement.
- **Evidence:** `StatementIngestionService.cs` hashes bytes; `VerificationJob.ContentHash` "unique per
  job" (`VerificationJob.cs:34-37`). No logical statement-identity key anywhere. Register FR-1/FR-22 name
  *byte* idempotency only. **Absent from register.**
- **Severity: Degrades** (audit-trail integrity; duplicate verdicts on logically-identical statements).

### C3. No backpressure/quarantine for the exception queue itself, and no per-batch SLA clock
- **Why it matters (uncaptured slice):** PG-15/16 (SLA) and FR-21 (exception queue) exist, but the
  *operational shape* of the monthly run is unspecified: what happens when the exception queue itself
  grows unbounded (e.g. a bad bundle BLOCKs every statement), is there an alert when projected completion
  exceeds the 3–5 business-day window, and is there a kill-switch? These are runbook/SLA-control concerns
  the register lists only as deliverables to *measure* (PG-14/15/16), not as *failure controls*.
- **Evidence:** `BatchProcessor` bounded concurrency exists (RC0b §8) but is never invoked by the Worker;
  no SLA-projection metric, no exception-queue cap. **Absent from register** (no row for queue-overflow
  control or in-flight SLA-breach alerting).
- **Severity: Degrades.**

---

## Theme D — Security (flag-only; deep audit deferred per owner ruling — these are the *uncaptured slices*)

### D1. PDF parsing and the AES encryption key share one process/trust boundary
- **Why it matters:** The same single-process Worker that parses **untrusted PDF input** (a classic
  attack surface) also holds the legal-baseline **AES-256 key** in memory (read from `IConfiguration`).
  A PdfPig parser exploit could reach the key. The register names the 3-process split as a *Prisma* gate
  and key-management as PG-4, but **not the specific co-location of untrusted-parse and key material in
  one Veriqan process.** RC1-security flags it Medium; this critic elevates the *unknown-unknown* framing:
  Veriqan has no requirement to isolate the parser from secrets.
- **Evidence:** `Program.cs` monolithic host; `ConfigurationCryptoKeyProvider` caches the key for process
  lifetime (RC1-persistence §2). **Uncaptured slice** of register PG-4 / A1–A6.
- **Severity: Degrades** (defense-in-depth; defer to security review).

### D2. Concurrency correctness of the in-memory singletons under parallel batch is unproven
- **Why it matters:** The as-shipped Worker registers `InMemoryVerificationResultStore`,
  `InMemoryReprocessAuditRepository`, etc. as **singletons** backed by `ConcurrentDictionary`, while the
  `BatchProcessor` runs statements with bounded *parallelism* and DbContext is scoped. `ConcurrentDictionary`
  guards individual ops but not the **read-modify-write resume/dedup sequences** (check-then-act on the
  result store), which can race under parallel batch. No requirement asserts the resume/dedup operations
  are atomic, and no test exercises them concurrently.
- **Evidence:** singletons at `VeriqanOrchestrationExtensions.cs:99-100` (RC1-persistence §3);
  `BatchProcessor` parallel (RC0b §8). No concurrency/race requirement in register. **Absent from register.**
- **Severity: Degrades** (latent data race on resume-state; today masked because batch is never run from
  the host).

---

## Top-of-mind for the synthesizer (RC.4)

The two **silent-correctness** items (A1 scanned-PDF false-RED, A4 European-number mis-parse) and the
abstain-safety mechanism gap (A5) are the most dangerous for a *never-false-block* gate: each can produce
a **confidently wrong verdict on a real input that no green test and no synthetic fixture exercises** —
exactly the negative space the brief (§0) predicted. B1 (LFPDPPP retention/erasure) is the largest
*compliance* unknown-unknown for a Mexican bank onboarding a processor. None of these are corpus-gated
fixes — they are buildable now (a text-layer-density abstain guard, an explicit `es-MX`/format guard, a
real confidence model, a retention policy, a clock injection at extraction).

*This is the RC.3 completeness-critic deliverable. It names negative space only; built-state classification
and remediation effort are RC.4 synthesis scope.*
