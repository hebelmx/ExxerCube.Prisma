# Veriqan Reference Bundle Authoring Guide

This directory contains one sub-directory per institution (bank).
Each institution sub-directory is a **reference bundle**: a set of CSV files
that the `CsvReferenceDataAdapter` reads at runtime to supply
`IVecReferenceDataProvider.GetBundleAsync()` with institution-specific reference
data (rates, products, legends, prior statement balances, etc.).

The adapter resolves the institution directory by normalising
`StatementContextKey.Institution` to a filesystem-safe name
(spaces become `_`, invalid chars are stripped) and looking for a matching
sub-directory under the configured `CsvReferenceData:RootDirectory`.

Example: institution `"Demo Bank (Iqubica)"` → directory `Demo_Bank_(Iqubica)/`.

---

## Important: Demo_Bank_(Iqubica) is SYNTHETIC TEST DATA

The `Demo_Bank_(Iqubica)` bundle is **synthetic**. It was generated solely to
exercise the CSV adapter in unit tests. It does **not** represent any real bank's
rates, products, client accounts, or financial obligations.

A real bank's bundle must be authored by the bank's data team and signed off
through the process described in the "Authoring & Sign-off Process" section below.

> **BLOCKED on bank relationship** — a production bundle for any real institution
> (e.g. Iqubica's live environment) cannot be created until the bank engagement
> is formalised. This is a commercial/legal dependency, not a code dependency.
> Track progress on this in the project's issue #17 (buyer discovery).

---

## CSV File Schema Reference

All CSV files use comma-separated values with a header row.
All files are **optional** except `bundle-metadata.csv`, which is required.
A missing optional file causes the corresponding bundle section to be `null`;
the adapter logs a warning and the affected VEC checks emit
`INSUFFICIENT_REFERENCE_DATA` findings (graceful degradation — no hard failure).

---

### bundle-metadata.csv (REQUIRED)

One data row. Identifies the bundle and its coverage period.

| Column | Type | Description | Example |
|---|---|---|---|
| `schemaVersion` | string | CSV schema version. Current: `1.0.0` | `1.0.0` |
| `institution` | string | Full institution name (must match `StatementContextKey.Institution`) | `Demo Bank (Iqubica)` |
| `bundleId` | string? | Opaque bundle identifier for traceability | `demo-csv-test` |
| `generatedAt` | ISO 8601 datetime? | When the bundle was generated | `2026-06-16T00:00:00Z` |
| `periodLabel` | string? | Human-readable period label | `Sep-Oct 2025` |
| `periodStart` | ISO 8601 date? | Start of the coverage period | `2025-09-01` |
| `periodEnd` | ISO 8601 date? | End of the coverage period | `2025-09-30` |
| `sourceMechanism` | string? | How data was obtained (`csv`, `api`, `manual`) | `csv` |
| `sourceReference` | string? | Internal reference to the source artefact | `test-data` |
| `sourceNotes` | string? | Free-text notes for auditors | `CSV adapter test data` |

Example row:
```
1.0.0,Demo Bank (Iqubica),demo-csv-test,2026-06-16T00:00:00Z,Sep-Oct 2025,2025-09-01,2025-09-30,csv,test-data,CSV adapter test data
```

---

### products.csv

One row per credit-card product offered by the institution.
Aliases are pipe-separated (`|`).

| Column | Type | Description | Example |
|---|---|---|---|
| `productId` | string | Canonical product identifier | `TC-NL` |
| `productName` | string | Full display name | `Tarjeta de Crédito NL` |
| `aliases` | string? | Pipe-separated alternate identifiers | `NL\|TC-NL-ALIAS` |
| `hasRewardsProgram` | bool? | Whether the product earns reward points | `true` |
| `annualCommission` | decimal? | Annual commission in `currency` | `1500` |
| `currency` | string? | ISO 4217 currency code for commission | `MXN` |

Example row:
```
TC-NL,Tarjeta de Crédito NL,NL,true,1500,MXN
```

---

### interest-rates.csv

One row per product + billing period. Multiple rows per product are allowed;
the adapter groups them by `productId`.

| Column | Type | Description | Example |
|---|---|---|---|
| `productId` | string | Must match a `productId` in `products.csv` | `TC-NL` |
| `periodLabel` | string? | Human-readable period label | `Sep Oct` |
| `periodStart` | ISO 8601 date? | Start of the rate period | `2025-09-01` |
| `periodEnd` | ISO 8601 date? | End of the rate period | `2025-09-30` |
| `annualOrdinaryFixedRate` | decimal | Annual ordinary interest rate (0–1 fraction) | `0.1975` |

Example row:
```
TC-NL,Sep Oct,,,0.1975
```

---

### tolerance-config.csv

Single data row. Numerical tolerances used when comparing computed amounts
against statement-printed amounts.

| Column | Type | Description | Example |
|---|---|---|---|
| `currencyToleranceMxn` | decimal? | Acceptable rounding delta in MXN | `0.50` |
| `pointsTolerance` | decimal? | Acceptable delta in reward points | `1.00` |
| `rewardsPesosToleranceMxn` | decimal? | Acceptable delta in rewards-pesos value | `1.00` |
| `pointsToPesosExchangeRate` | decimal? | Exchange rate: 1 reward point = N MXN | `0.10` |

Example row:
```
0.50,1.00,1.00,0.10
```

---

### validation-constants.csv

Single data row. Typography and regulatory constants used by structural checks.

| Column | Type | Description | Example |
|---|---|---|---|
| `requiredFontFamily` | string? | Required font family name (typography check) | `Aptos` |
| `bankingYearDays` | int? | Days in a banking year for interest calc | `360` |
| `catAnnualCommissionMxn` | decimal? | CAT annual commission to verify in MXN | `1500` |

Example row:
```
Aptos,360,1500
```

---

### mandatory-legends.csv

One row per required legal legend. `appliesToProducts` is pipe-separated;
empty means the legend applies to all products.

| Column | Type | Description | Example |
|---|---|---|---|
| `legendId` | string | Stable identifier for this legend | `repr-impresa` |
| `text` | string | Exact text that must appear on the statement | `ESTE DOCUMENTO ES UNA REPRESENTACIÓN IMPRESA SIN VALIDEZ FISCAL` |
| `matchMode` | string? | Matching strategy: `exact`, `normalized`, `contains` | `normalized` |
| `appliesToProducts` | string? | Pipe-separated product IDs; empty = all | *(empty)* |
| `section` | string? | Statement section where the legend must appear | `fiscal` |
| `required` | bool? | Whether absence is a fatal finding | `true` |

Example row:
```
repr-impresa,ESTE DOCUMENTO ES UNA REPRESENTACIÓN IMPRESA SIN VALIDEZ FISCAL,normalized,,fiscal,true
```

---

### sequential-images.csv

One row per ordered sequential image (e.g. numbered illustrations that appear
in a fixed sequence on the statement).

| Column | Type | Description | Example |
|---|---|---|---|
| `order` | int | 1-based display order | `1` |
| `productId` | string? | Product the image belongs to | `TC-NL` |
| `imageId` | string? | Stable identifier for the image | `seq-nl-1` |
| `imageUri` | string? | Relative path or URL in the image catalog | `catalog/sequential/nl-1.png` |
| `imageSha256` | string? | SHA-256 of the canonical image file | *(empty if not yet hashed)* |
| `imagePerceptualHash` | string? | Perceptual hash for fuzzy comparison | *(empty)* |
| `imageWidth` | int? | Expected pixel width | *(empty)* |
| `imageHeight` | int? | Expected pixel height | *(empty)* |
| `imageDescription` | string? | Human-readable description | *(empty)* |

Example row:
```
1,TC-NL,seq-nl-1,catalog/sequential/nl-1.png,,,,,
```

---

### promotions.csv

One row per promotional image. `appliesToProducts` is pipe-separated.

| Column | Type | Description | Example |
|---|---|---|---|
| `promotionId` | string | Stable identifier for this promotion | `promo-sep-2025-a` |
| `validFrom` | ISO 8601 date | Start of promotion validity | `2025-09-01` |
| `validTo` | ISO 8601 date | End of promotion validity | `2025-09-30` |
| `appliesToProducts` | string? | Pipe-separated product IDs; empty = all | `TC-NL` |
| `imageId` | string? | Stable image identifier | `promo-a` |
| `imageUri` | string? | Relative path or URL in the image catalog | `catalog/promotions/sep-a.png` |
| `imageSha256` | string? | SHA-256 of the image | *(empty)* |
| `imagePerceptualHash` | string? | Perceptual hash | *(empty)* |
| `imageWidth` | int? | Expected width | *(empty)* |
| `imageHeight` | int? | Expected height | *(empty)* |
| `imageDescription` | string? | Description | *(empty)* |

Example row:
```
promo-sep-2025-a,2025-09-01,2025-09-30,TC-NL,promo-a,catalog/promotions/sep-a.png,,,,,
```

---

### client-accounts.csv

One row per client. Address fields are optional. The corresponding account
entries are in `client-accounts-entries.csv` (joined by `clientId`).

| Column | Type | Description | Example |
|---|---|---|---|
| `clientId` | string | Unique client identifier | `CLI-0001` |
| `firstNames` | string? | Given names | `Eugenio` |
| `lastNames` | string? | Family names | `Garcia Zavala` |
| `fullName` | string? | Full formatted name | `Eugenio Garcia Zavala` |
| `rfc` | string? | Mexican RFC | `GAZE800101AAA` |
| `clientNumber` | string? | Bank-assigned client number | `1234567` |
| `street` | string? | Street address | `Av. Cumbres` |
| `number` | string? | Building number | `100` |
| `neighborhood` | string? | Colonia / neighbourhood | `Cumbres` |
| `postalCode` | string? | CP | `64610` |
| `state` | string? | State name | `Nuevo Leon` |

Example row:
```
CLI-0001,Eugenio,Garcia Zavala,Eugenio Garcia Zavala,GAZE800101AAA,1234567,Av. Cumbres,100,Cumbres,64610,Nuevo Leon
```

---

### client-accounts-entries.csv

One row per account. Joined to `client-accounts.csv` via `clientId`.

| Column | Type | Description | Example |
|---|---|---|---|
| `clientId` | string | FK to `client-accounts.csv` | `CLI-0001` |
| `accountRef` | string | Account reference identifier | `ACC-0001` |
| `productId` | string | FK to `products.csv` | `TC-NL` |
| `cardNumber` | string? | Masked card number | `5512 34** **** 7890` |
| `clabe` | string? | CLABE (18-digit interbank code) | `012345678901234567` |
| `branchNumber` | string? | Branch number | `0420` |
| `creditLine` | decimal? | Credit limit in MXN | `100000` |
| `accountOpenDate` | ISO 8601 date? | Date account was opened | `2020-01-15` |

Example row:
```
CLI-0001,ACC-0001,TC-NL,5512 34** **** 7890,012345678901234567,0420,100000,2020-01-15
```

---

### prior-statements.csv

One row per prior billing statement (closing balances for the previous period).
MSI installment breakdown is in `prior-statements-installments.csv` (joined by `accountRef`).

| Column | Type | Description | Example |
|---|---|---|---|
| `accountRef` | string | FK to `client-accounts-entries.csv` | `ACC-0001` |
| `periodLabel` | string? | Period label | `Ago-Sep 2025` |
| `periodStart` | ISO 8601 date? | Period start | `2025-08-01` |
| `periodEnd` | ISO 8601 date? | Period end | `2025-08-31` |
| `pagoParaNoGenerarIntereses` | decimal? | Minimum payment to avoid interest (MXN) | `18540.32` |
| `saldoDeudorTotal` | decimal? | Total debtor balance (MXN) | `18540.32` |
| `rewardsPointsBalance` | decimal? | Rewards points closing balance | `5400` |
| `rewardsPesosBalance` | decimal? | Rewards pesos closing balance (MXN) | `540.0` |
| `documentRefUri` | string? | URI of the prior statement PDF for audit | `PRP2/01+Dummie+VEC+jul_ago+20252.pdf` |
| `documentRefSha256` | string? | SHA-256 of the prior statement PDF | *(empty)* |

Example row:
```
ACC-0001,Ago-Sep 2025,2025-08-01,2025-08-31,18540.32,18540.32,5400,540.0,PRP2/01+Dummie+VEC+jul_ago+20252.pdf,
```

---

### prior-statements-installments.csv

One row per MSI (Meses Sin Intereses) installment. Joined to
`prior-statements.csv` via `accountRef`.

| Column | Type | Description | Example |
|---|---|---|---|
| `accountRef` | string | FK to `prior-statements.csv` | `ACC-0001` |
| `purchaseId` | string | Unique identifier for the purchase | `MSI-DONCOLCHON` |
| `description` | string? | Merchant / purchase description | `DON COLCHON CUMBRES` |
| `saldoPendiente` | decimal? | Remaining balance on this purchase (MXN) | `2914.38` |
| `pagoRequerido` | decimal? | Required monthly installment (MXN) | `485.73` |
| `numeroDePago` | int? | Current payment number | `5` |
| `totalPagos` | int? | Total number of payments in the plan | `12` |

Example row:
```
ACC-0001,MSI-DONCOLCHON,DON COLCHON CUMBRES,2914.38,485.73,5,12
```

---

### expected-transactions.csv

One row per expected transaction. Multiple rows per `accountRef` are allowed;
the adapter groups them into `ExpectedTransactionGroup` records.

| Column | Type | Description | Example |
|---|---|---|---|
| `accountRef` | string | FK to `client-accounts-entries.csv` | `ACC-0001` |
| `description` | string | Transaction description | `NETFLIX COM CR NME 110513PI3` |
| `amount` | decimal | Transaction amount (absolute, positive) | `329.00` |
| `operationDate` | ISO 8601 date? | Date the operation was performed | `2025-07-05` |
| `chargeDate` | ISO 8601 date? | Date the charge was applied | `2025-07-07` |
| `sign` | string? | `+` = credit to account, `-` = debit | `+` |

Example row:
```
ACC-0001,NETFLIX COM CR NME 110513PI3,329.00,2025-07-05,2025-07-07,+
```

---

## Authoring & Sign-off Process

### Who authors each CSV

| File | Authored by | Reviewed by |
|---|---|---|
| `bundle-metadata.csv` | Bank data team (data analyst) | Veriqan integration engineer |
| `products.csv` | Bank product team | Veriqan integration engineer |
| `interest-rates.csv` | Bank actuarial / pricing team | Veriqan integration engineer + bank compliance |
| `tolerance-config.csv` | Veriqan integration engineer (proposed) | Bank compliance officer |
| `validation-constants.csv` | Veriqan integration engineer (proposed) | Bank compliance officer + typography owner |
| `mandatory-legends.csv` | Bank legal / compliance team | Bank legal counsel + Veriqan integration engineer |
| `sequential-images.csv` | Bank design team | Bank product owner |
| `promotions.csv` | Bank marketing team | Bank product owner |
| `client-accounts.csv` | Bank data team (anonymised/synthetic for testing) | Veriqan integration engineer |
| `client-accounts-entries.csv` | Bank data team | Veriqan integration engineer |
| `prior-statements.csv` | Bank data team | Veriqan integration engineer + bank compliance |
| `prior-statements-installments.csv` | Bank data team | Veriqan integration engineer |
| `expected-transactions.csv` | Bank data team | Veriqan integration engineer |

### What each file represents

- **products / interest-rates** — the bank's official credit-card product catalogue
  and the legally-published interest rates for each billing period. These are
  regulatory disclosures; accuracy is legally required (CONDUSEF, CNBV).
- **tolerance-config** — numerical thresholds within which rounding differences
  are acceptable. These must be agreed in writing with the bank's compliance team.
- **validation-constants** — typography and calendar constants that drive
  structural checks (e.g. required font family, 360-day banking year).
- **mandatory-legends** — verbatim legal texts that regulators require on every
  statement. The bank's legal team must supply and sign off on each entry.
- **sequential-images / promotions** — image catalogues used to verify visual
  integrity of printed statements (correct logo, promotion images). Authored by
  the bank's design/marketing team and signed off by the product owner.
- **client-accounts / entries** — anonymised or synthetic client data used to
  verify personal data rendering on statements. For production use, this data
  must be obtained under the bank's data governance process (LFPDPPP).
- **prior-statements / installments** — closing balances from the previous billing
  cycle. Used to verify carry-over amounts. Must match the bank's core banking
  export exactly.
- **expected-transactions** — the ground-truth list of expected transactions for
  a given account and period. Used to verify transaction completeness checks.

### Sign-off required before a bundle enters production

1. The bank's **compliance officer** signs off on `interest-rates.csv`,
   `tolerance-config.csv`, `mandatory-legends.csv`, and `validation-constants.csv`.
2. The bank's **legal counsel** reviews `mandatory-legends.csv` for regulatory accuracy.
3. The Veriqan **integration engineer** validates the bundle against the JSON Schema
   by running `CsvReferenceDataAdapter` against a sample statement — confirms 0 schema errors.
4. A **Veriqan lead engineer** reviews the pull request adding the bundle.
5. The bundle is tagged with its effective date range in `bundle-metadata.csv`
   before merging.

> **BLOCKED on bank relationship** — no production bundle can progress through
> this sign-off gate until the bank engagement (issue #17) is active.
> The `Demo_Bank_(Iqubica)` bundle bypasses this process because it is synthetic
> test data with no legal standing.

---

## Deployment Notes

The `Prisma/Data/Veriqan/reference-bundles/` directory is the **single source of
truth** for reference bundles. A deployed Veriqan Worker is configured via:

```json
{
  "CsvReferenceData": {
    "RootDirectory": "/data/veriqan/reference-bundles"
  }
}
```

or via environment variable `CsvReferenceData__RootDirectory`.

The Worker resolves the institution directory at runtime; no recompilation is
needed to add a new institution bundle. Deploy the bundle by:

1. Placing the new institution directory under `RootDirectory`.
2. Restarting (or signalling) the Worker (the adapter reads from disk on every call;
   there is no startup cache — bundles can be hot-swapped between statements).

The test project copies the `Demo_Bank_(Iqubica)` bundle into its output via a
`<Content>` MSBuild item that links from this directory. Tests therefore always
run against the canonical deployable bundle, not a stale copy.
