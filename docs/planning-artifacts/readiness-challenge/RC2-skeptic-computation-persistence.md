# RC.2 — Adversarial Skeptic: Computation / Persistence / Ingestion

**Date:** 2026-06-18 · **Branch:** `Liv` · **Phase:** RC.2 (adversarial refutation) · **Role:** ADVERSARIAL SKEPTIC
**Auditor:** Claude Code (read-only on production code; no source modified)
**Mandate:** Refute the RC.1 "Real / Wired / done" claims in the computation, persistence, and ingestion clusters.
Default to *refuted/not-ready* on doubt; the burden of proof is on "done." Every refutation cites file:line and a concrete breaking input.

> **Method:** Attack against the **intended solution** (Brief §2 readiness dimensions: cardinal "never false-block",
> encryption-at-rest with real key management, real computations, idempotent ingestion), **not** against agent prose.
> Believe wiring + file:line, never comments.

---

## Target 1 — FR-15 verdict "never false-blocks" (RC1 class: **Real+Wired+E2E**)

**Files:** `01 Core/Veriqan.Application/Verdict/VerdictAggregator.cs`, `VerdictSummary.cs`.

### What genuinely holds
The InsufficientData → never-RED property is **structurally correct**. `InsufficientData` findings are partitioned into
`insufficientIds` (`VerdictAggregator.cs:92-94`) and are **never** added to `failIds`; the RED gate fires only on
`failIds.Count > 0` (`:106`). An all-InsufficientData (or empty) finding set therefore yields GREEN (`:123-130`). The
tenant-deviation separation is also benign for InsufficientData: a finding only reaches the legal/tenant split inside the
`FindingVerdict.Fail` branch (`:81-90`), so InsufficientData can never populate `legalBreachIds`/`tenantOnlyFailIds`, and
`LegalBaselineSignal` (`VerdictSummary.cs:157-162`) is informational and does not gate. **This core claim survives.**

### Refutation 1a — unknown/future verdict values silently become PASS (latent false-GREEN)
`VerdictAggregator.cs:96-99`:
```csharp
case FindingVerdict.Pass:
default:
    passCount++;
    break;
```
The `default` arm lumps **any `FindingVerdict` value that is not Fail/InsufficientData/Pass into `passCount`.** Today the
enum has exactly those three members, so this is dormant. But it is a **fail-open** construction: if a future severity
(e.g. a `Warn`, `Error`, or a deserialized out-of-range enum from a persisted finding) is introduced, an unhandled value is
counted as a *Pass* and the statement goes GREEN. For a preventive gate the safe default is to *abstain or block on an
unrecognized verdict*, never to pass it. This is a false-PASS risk, not a false-BLOCK — but it is the inverse cardinal-rule
hazard (letting a non-compliant statement through), and it is a real defect in "exactly one RED on any FAIL" because an
unmodelled fail-like value never reaches `failIds`.

**Severity:** Degrades (latent). **Verdict: SURVIVES as a never-false-BLOCK claim; REFUTED as a fully-defensive aggregator** —
the `default`-to-Pass arm is a fail-open gap a regulator-grade gate should not have.

### Refutation 1b — BLOCKED *is* a block, and a degraded/missing reference bundle produces it
The brief's cardinal rule is "never false-block on a known-good statement." `VerdictAggregator.cs:64-66` emits
`VerdictSummary.Blocked` **whenever a `BlockedOutcome` is supplied** — and per RC1-persistence (`BundleBinder.cs:73-76`) a
*missing/invalid reference bundle* maps to exactly such a `BlockedOutcome`. So a **known-good statement** processed when the
reference bundle is absent, schema-invalid, or the CSV root is unconfigured (the *as-shipped Worker* state — RC1-persistence
§3, no appsettings.json → `CsvReferenceDataAdapter` finds no institution dir → every bind BLOCKED) yields **BLOCKED on a
good statement.** The aggregator is faithfully propagating the binder's decision — it is not *itself* buggy — but the
end-to-end FR-15 property "never false-blocks" is **violated by the binding layer in the shipped configuration**, and the
aggregator has no compensating logic. BLOCKED is operationally a block of the bank's billing run.

**Severity:** Blocks-production (in the shipped, unconfigured Worker). **Verdict: the aggregator SURVIVES; the end-to-end
"never false-blocks" claim is REFUTED** — a missing bundle false-blocks a good statement, and that is reachable today.

### Refutation 1c — "+E2E" is unearned
RC1-verdict itself caveats this (`RC1-verdict-reporting.md:62`): `VerificationPipeline` is invoked **only from tests**; the
Worker has no trigger path. The "E2E" exercises run on the **3 synthetic non-compliant fixtures**, which the calibration
harness has shown are *not even compliant statements*. There is **zero** known-good real CONDUSEF statement, so "never
false-blocks" has never been demonstrated on a real good input — it is **designed-in and unit-asserted, not empirically
demonstrated.**

**Target-1 overall verdict:** **DOWNGRADE `Real+Wired+E2E` → `Real-unwired` (designed-correct, empirically unproven).**
The aggregator's InsufficientData handling survives; the global "never false-blocks" claim is **REFUTED** via 1b (missing
bundle → false BLOCK on good input, reachable in the shipped Worker) and the latent fail-open `default` arm (1a).

---

## Target 2 — E9 encrypted legal-baseline store (RC1 class: **Real+Wired (conditional), 5/5 green**)

**Files:** `AesEncryptedDecimalConverter.cs`, `ConfigurationCryptoKeyProvider.cs`, `Stores/SqlLegalBaselineStore.cs`.

### What genuinely holds
- **IV reuse: NOT present.** `aes.GenerateIV()` is called on **every** encrypt (`AesEncryptedDecimalConverter.cs:42`) and the
  fresh 16-byte IV is prepended (`:47`), read back as the first 16 bytes on decrypt (`:66-68`). The IV-reuse attack **fails** —
  this survives.
- **Key-length / key-missing: fails loud.** `ConfigurationCryptoKeyProvider` throws `InvalidOperationException` on a
  missing/empty key (`:39-42`), non-Base64 (`:49-52`), or a decoded length ≠ 32 bytes (`:55-58`). Wrong-*length* fails loud at
  construction. This survives.
- **Ciphertext is genuinely in the column.** The converter is a `ValueConverter<decimal,string>` that stores `Base64(IV||CBC)`;
  RC1's integration test asserts the raw column is ciphertext. Not bypassable through EF materialization. Survives.

### Refutation 2a — CBC without a MAC is unauthenticated: tamper is undetectable (and partly malleable)
`AesEncryptedDecimalConverter.cs:43-44` selects `CipherMode.CBC` + `PaddingMode.PKCS7` with **no HMAC, no AEAD (GCM), and no
integrity tag of any kind.** For a *legal compliance baseline* (the tolerance floors that decide pass/fail on a bank's billing
run) this is the wrong primitive. Concrete attack: an actor with write access to `veriqan.LegalBaselineTolerances` (the exact
threat encryption-at-rest is meant to mitigate — a DBA, a compromised app login, a backup thief) can **flip CBC ciphertext
bits to corrupt the decrypted tolerance** without the key. CBC bit-flipping in block *n* deterministically flips the
corresponding plaintext bits in block *n+1* and randomizes block *n*; because the plaintext is a short invariant-culture
decimal string (e.g. `"0.50"`), an attacker can with non-trivial probability mangle a tolerance to a different valid-parsing
value (e.g. widen `0.50` toward a larger number) and **there is no integrity check to reject it.** A widened legal tolerance
silently *loosens the gate* — the cardinal "never false-PASS a non-compliant statement" is defeated at the data layer. An
auditor reviewing a load-bearing regulated store will reject column-level CBC-without-HMAC as non-tamper-evident.

**Severity:** Blocks-production for a regulated store. **Verdict: REFUTED** — "encrypted-at-rest" is satisfied for
*confidentiality* only; it provides **no integrity/authenticity**, and tamper of the legal floor is undetectable.

### Refutation 2b — a wrong key fails *inconsistently*, and can fail SILENTLY (return garbage)
`Decrypt` (`AesEncryptedDecimalConverter.cs:57-80`) does `CreateDecryptor()` → `StreamReader.ReadToEnd()` →
`decimal.Parse(...)`. With a **wrong key** there is no authentication, so behaviour depends on the random PKCS7-unpad outcome:
1. **Most often** the final block's padding is invalid → `CryptographicException` ("padding is invalid") thrown out of the
   `CryptoStream` — i.e. a *loud crash*, but an **uncaught raw exception**, not a typed `Result.WithFailure` (violates the
   project's no-throw-for-business-logic contract; it surfaces as an unhandled exception inside EF materialization).
2. With probability ≈ 1/256 the corrupted last byte is a valid PKCS7 pad length and unpadding *succeeds*, yielding **garbage
   plaintext bytes**. That garbage then hits `decimal.Parse` (`:79`): usually `FormatException` (again an **uncaught throw**),
   but for the subset of garbage that happens to be a parseable decimal string, **`Decrypt` returns a wrong decimal with no
   error at all** — a **silent** wrong tolerance loaded into the gate.

So the answer to "does a wrong key fail loud or silently return garbage?" is: **mostly loud-but-uncaught (crash), and in a
low-probability tail, silently wrong.** Neither is acceptable: there is no key-id/version on the ciphertext and no
authentication tag, so the store cannot *distinguish* "wrong key" from "tampered data" from "corrupt row," and the silent-tail
path returns an unverified value. (Compounding: `ConfigurationCryptoKeyProvider` caches the key for process lifetime with no
rotation and no key-id on ciphertext — RC1-persistence §2 — so a rotated key cannot decrypt historical rows at all.)

**Severity:** Blocks-production. **Verdict: REFUTED** — wrong-key handling is unauthenticated; it crashes (uncaught) in the
common case and can silently return garbage in the tail. Auth-tag/AEAD + key-id is required.

### Refutation 2c — scope and TDE
`SqlLegalBaselineStore.cs:18-22` admits DB-level TDE is "an additional OPS provisioning step (not enforced here)" and the
store is read-only-by-convention (db_datareader is an *ops* expectation, not a DB grant). Only the tolerance decimals are
encrypted; `VerificationJob.ContentHash`, findings, and dispositions are plaintext. Encryption coverage is narrow.

**Target-2 overall verdict:** **DOWNGRADE `Real+Wired` → `Partial (confidentiality-only, not auditor-grade)`.** IV-handling
and key-length validation **survive**; the **integrity/authenticity, wrong-key-safety, key-management, and TDE** dimensions
are **REFUTED**. The cryptographic *confidentiality* primitive is sound; the *security control* a bank/auditor needs is not.

---

## Target 3 — §-recompute rules (chose §20-WATERFALL and §16-OTRASLINEAS) (RC1 class: **Real-unwired / Partial**)

### §20-WATERFALL — `Section20PaymentDistributionRule.cs`

**The recompute is real** (`:143-149`): `|pagos| ≈ Regulares + SIN + CON + Intereses + IVA − SaldoAFavor`, with full
7-cell confidence/kind/parse guards (`:117-132`) that abstain (InsufficientData) on any bad cell — so the **abstain-safety
survives**. The attack is on **sign/column semantics**, which are an *uncalibrated assumption*:

**Breaking input — saldo-a-favor sign + pagos sign.** The rule hard-codes:
- col[0] `Math.Abs(pagosYAbonos)` (`:143`) — assumes "pagos y abonos" is printed *negative*;
- col[6] **subtracted** as `− SaldoAFavor` (`:149`) — assumes saldo-a-favor is printed as a *positive* magnitude.

Consider a real statement where the bank prints **saldo a favor as a negative signed amount** (a credit shown with a leading
minus, which is a common convention). Then the extractor's `ParsedValue` for col[6] is *negative*, and `− (negative)` *adds*
the credit instead of subtracting it — the computed `components` is off by `2 × saldoAFavor`. If that exceeds the 0.50 MXN
legal tolerance (any non-trivial credit balance does), the rule returns **Fail on a perfectly correct statement** →
false-BLOCK. Symmetrically, if a bank prints "pagos y abonos" as a *positive* number while a genuine arithmetic error exists,
`Math.Abs` can mask a sign-flipped discrepancy → false-PASS. The memory note confirms "§20 saldo-a-favor sign also deferred to
corpus" — i.e. **the sign convention is an unverified guess**, and a single real statement with the opposite convention
breaks it in the false-FAIL direction. This is precisely the cardinal-rule violation the gate must not have.

**Verdict: UNPROVABLE-WITHOUT-CORPUS, leaning REFUTED.** The identity arithmetic is correct *given the assumed sign
convention*; the convention itself (col[0] negative, col[6] positive-magnitude) is uncalibrated, and the plausible
opposite-sign real statement yields a **false-FAIL beyond tolerance**. Class stays **Partial**, with an explicit false-BLOCK
hazard on the saldo-a-favor sign.

### §16-OTRASLINEAS — `Section16OtherCreditLinesRule.cs`

The arithmetic is real (Check-1 interest `:269`, Check-2 IVA×0.16 `:291`, Check-3 §19 tie `:385-441`) and the
conditional-absent path correctly returns **Pass/N-A** (`:167-177`), and too-few-columns / low-confidence correctly **abstain**
(`:214-220`, `:241-249`). So the abstain-safety on *shape* survives. The attack is the **9-column index map**, which the code
*itself* flags as a guess (`:83-86`, `:355-356`).

**Breaking input — column-order mismatch false-PASS (the dangerous direction).** The map pins
`ColSaldoPendiente=3, ColIntereses=4, ColIva=5, ColTasa=8` (`:98-101`). Suppose a real bank's §16 orders columns differently
— e.g. `Pago requerido` at [4] and `Intereses del periodo` at [6] (banks reorder these conditional tables freely; there is no
fixture to pin the true order). Then:
- Check-2 computes `IVA ≈ |Pago requerido| × 0.16` against whatever sits at [5]. For most real rows this mismatch is **large**,
  so it would tend to *false-FAIL* (`:298-307`) → false-BLOCK. **But** consider the more insidious case: the rule's own guard
  at `:229-238` **silently `continue`s** (skips the row) whenever the cells it *reads* at [4]/[5] are `NotApplicable`/`Empty`.
  If the real table's true `Pago requerido`/`Núm. de pago` columns (which legitimately carry NA/empty for some rows) land at
  the indices the rule reads as Intereses/IVA, **whole rows are skipped as "not applicable"** and never checked. A statement
  whose *actual* Intereses/IVA (sitting at different indices) are wrong then sails through: `rowsChecked` may still be > 0 from
  other rows, so the rule returns **Pass** (`:359-368`) — a **false-PASS that lets a non-compliant §16 through the gate.**
- Conversely a correct statement with a different column order false-FAILs Check-1/Check-2 → false-BLOCK.

Either way the 9-index map is load-bearing and unverified. The code admits it ("no real §16 fixture available to calibrate
column positions," `:85-86`), which is an honest **self-refutation**: the rule cannot guarantee either cardinal property on a
real §16 whose column order differs from the assumed one.

**Verdict: REFUTED (self-declared).** On any real §16 whose column order differs from the hard-coded `[3,4,5,8]` map, the rule
can **false-PASS** (skipped rows / mis-read cells, `:229-238`) or **false-FAIL** (`:298-307`). Corpus-gated, but the hazard is
concrete and present in code, not vague. Class stays **Partial** with a both-directional cardinal-rule hazard.

---

## Target 4 — Idempotent ingestion / FR-1 hash-dedup (RC1 class: **Partial**, dedup "production-grade")

**Files:** `StatementIngestionService.cs`; wired repo `InMemory/InMemoryVerificationJobRepository.cs`;
`Repositories/EfVerificationJobRepository.cs`; `Configurations/VerificationJobConfiguration.cs`.

The dedup is a **classic check-then-act with no atomicity** (`StatementIngestionService.cs:80` Find, `:97-105` return-existing,
`:108-115` create-and-Add). It is idempotent **only single-threaded**. Two refutations, one per wired substrate.

### Refutation 4a — InMemory wired path (the as-shipped Worker): TOCTOU race → duplicate jobs, no dedup
The shipped Worker has no `VeriqanDb` connection string, so persistence resolves to `InMemoryVerificationJobRepository`
(RC1-persistence §3). Its `FindByContentHashAsync` does a bare `_byHash.TryGetValue` (`InMemoryVerificationJobRepository.cs:32`)
and `AddAsync` does a bare `_byHash[job.ContentHash] = job` (`:44`) — **no locking, no `GetOrAdd`, no compare-and-swap.**

**Breaking sequence (two identical PDFs in flight):**
1. Thread A: `IngestAsync(pdf)` → `FindByContentHash(H)` → `TryGetValue` miss → null.
2. Thread B: `IngestAsync(pdf)` (same bytes, same hash H) → `FindByContentHash(H)` → miss → null. *(Both passed the dedup
   check; neither has written yet.)*
3. Thread A: creates `VerificationJob{Id=GuidA}` (`:109-113`) → `AddAsync` → `_byHash[H]=jobA`.
4. Thread B: creates `VerificationJob{Id=GuidB}` → `AddAsync` → `_byHash[H]=jobB` (**overwrites jobA**).

Result: **two distinct `VerificationJob` GUIDs were minted and returned to two callers for one document** (FR-1's "idempotent
hash-dedup job" violated), and the dictionary now holds only `jobB` — `jobA` is **orphaned/leaked** (any downstream that kept
`GuidA` references a job no longer findable by hash). For a billing-gate, the same statement could be **processed and
alerted-on twice** (compounding the FR-17 "exactly one email per RED" gap RC1-verdict already flagged). The InMemory repo's
own doc-comment calls itself a "stub" (`:13-15`) — believing the wiring, the **shipped dedup is not concurrency-safe.**

**Severity:** Blocks-production (this is the *as-shipped* substrate). **Verdict: REFUTED.**

### Refutation 4b — EF/SQL path: the unique index saves the DB, but the service is non-idempotent under the race
With a SQL connection string the DB has a **unique index** on `ContentHash`
(`VerificationJobConfiguration.cs:29-31`, `IX_VerificationJobs_ContentHash`, `IsUnique()`). Good — the DB will *not* store two
rows. But the **service does not recover idempotently** from the collision:

**Breaking sequence:**
1. Thread A & B both `FindByContentHashAsync(H)` → `FirstOrDefaultAsync` → null (neither committed yet — `EfVerificationJobRepository.cs:46-51`).
2. Both build a job and call `AddAsync` → `SaveChangesAsync` (`:76-79`).
3. One commits; the other's `SaveChangesAsync` throws a **unique-constraint `DbUpdateException`**, caught at `:88-91` and
   returned as `Result.WithFailure($"Database error while persisting...")`.
4. `StatementIngestionService` sees `addResult.IsFailure` (`:121`) and returns **`Result.WithFailure`** (`:127-128`) to the
   caller.

So under concurrency, **one of two identical submissions receives a hard error** instead of the idempotent "here is the
existing job." FR-1's contract — *idempotent: a re-submitted identical PDF returns the existing job* — is **violated**: the
correct behaviour on a unique-violation is to *re-query and return the winner*, which the code does **not** do (there is no
retry/`FindByContentHash`-on-conflict). It is non-idempotent precisely at the boundary the requirement names.

**Severity:** Degrades→Blocks. **Verdict: REFUTED** (even with SQL, the dedup is not idempotent under concurrent identical
submissions; it surfaces a DB error rather than the existing job).

> Note: there is also no DB transaction/serializable isolation around find-then-add, and the InMemory path has no unique
> constraint at all — so the only thing preventing duplicate *rows* is the SQL unique index, which the as-shipped Worker
> (InMemory) does not have.

**Target-4 overall verdict:** **REFUTED.** "Idempotent hash-dedup" holds **only single-threaded**. Under two concurrent
identical PDFs: the shipped InMemory path **creates duplicate jobs** (TOCTOU, no atomicity); the SQL path **errors one caller**
instead of returning the existing job. Class stays **Partial** with a confirmed concurrency defect; the "production-grade
dedup logic" sub-claim is downgraded.

---

## Confirmed-real (survived the attack)

- **FR-15 InsufficientData → never-RED partitioning** (`VerdictAggregator.cs:92-94, 106`) — InsufficientData and empty finding
  sets cannot produce RED; tenant-deviation split cannot be populated by InsufficientData. *Structurally sound.*
- **E9 IV handling** — fresh `GenerateIV()` per write, prepended/read-back correctly (`AesEncryptedDecimalConverter.cs:42, 47, 66-68`).
  No IV reuse.
- **E9 key-length / key-missing validation** — loud `InvalidOperationException` on missing/non-Base64/≠32-byte key
  (`ConfigurationCryptoKeyProvider.cs:39-58`).
- **E9 ciphertext-in-column** — value converter genuinely stores `Base64(IV||CBC)`; not bypassable via EF.
- **§-rules abstain-safety on shape** — every recompute rule returns InsufficientData on missing/low-confidence/too-few-cells
  cells (§20 `:117-132`; §16 `:214-220, 241-249`); §16 conditional-absent → Pass/N-A (`:167-177`). The *plumbing* is honest.
- **EF unique index on ContentHash** prevents duplicate *rows* at the DB (`VerificationJobConfiguration.cs:29-31`).

## Refuted / downgraded

| Target | RC1 class | Corrected class | Breaking scenario (file:line) |
|---|---|---|---|
| **FR-15 "never false-blocks" (E2E)** | Real+Wired+E2E | **Real-unwired** (designed-correct, unproven; + reachable false-BLOCK) | Missing/unconfigured reference bundle → `BlockedOutcome` → BLOCKED on a *good* statement (`VerdictAggregator.cs:64-66` ← `BundleBinder.cs:73-76`; shipped Worker has no CSV root). Plus fail-open `default`→Pass arm (`VerdictAggregator.cs:96-99`). |
| **E9 encrypted store** | Real+Wired (5/5) | **Partial (confidentiality-only, not auditor-grade)** | CBC + no HMAC/AEAD → undetectable tamper of the legal tolerance floor (`AesEncryptedDecimalConverter.cs:43-44`); wrong key crashes uncaught or (≈1/256) silently returns garbage (`:57-80`). |
| **§20-WATERFALL** | Real-unwired | **Partial (false-BLOCK hazard)** | Statement printing saldo-a-favor as a *negative* signed amount → `− (negative)` adds the credit → diff ≈ 2×credit > 0.50 MXN → **false-FAIL** on a correct statement (`Section20PaymentDistributionRule.cs:143, 149`). |
| **§16-OTRASLINEAS** | Partial | **Partial (both-directional cardinal hazard, self-declared)** | Real §16 with a column order ≠ `[3,4,5,8]` → mis-read/skip rows → **false-PASS** (`Section16OtherCreditLinesRule.cs:229-238, 359-368`) or **false-FAIL** (`:298-307`). Code admits the map is a guess (`:85-86`). |
| **FR-1 idempotent dedup** | Partial ("production-grade logic") | **Partial (concurrency-unsafe)** | Two concurrent identical PDFs: InMemory path mints **two jobs** (TOCTOU, `InMemoryVerificationJobRepository.cs:32, 44`); SQL path **errors one caller** instead of returning the existing job (`EfVerificationJobRepository.cs:88-91` → `StatementIngestionService.cs:121-128`). |

---

*Scope: computation / persistence / ingestion skeptic pass only. Verdict-reporting and §19/§6/§8 left to their RC1 traces and
the RC.3 lenses. No production code was modified.*
