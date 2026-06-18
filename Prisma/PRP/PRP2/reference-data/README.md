# VEC Reference-Data Contract (Proposal)

**Status:** Proposed (2026-06-16, branch `Liv`)
**Purpose:** The VEC verifier needs reference/ground-truth data that the bank has **not yet
defined a schema or delivery mechanism for**. Rather than block, we define **our own canonical,
source-agnostic JSON contract**. The client can later deliver via CSV, database, API, or files —
each is handled by a small **adapter** that produces this same JSON. The C# core depends on one
interface only.

```
  CSV file ─┐
  Database ─┼──►  Adapter (CSV / DB / API / Manual)  ──►  vec-reference-bundle.json  ──►  IVecReferenceDataProvider  ──►  Validation Engine
  REST API ─┘                                              (this contract)
```

## Why this shape

- **One contract, many sources.** Adapters are cheap and isolated; the domain never learns where
  data came from. New delivery mechanism = new adapter, zero core changes.
- **Source-agnostic & codebase-friendly.** Plain JSON deserializes to C# records; fits the existing
  `Result<T>` + DI patterns. No coupling to the bank's internal systems.
- **Time-bound by design.** Interest rates (TASA) and promotions change monthly; every catalog is
  period/date-scoped so the right values are selected for the statement under test.
- **Product-keyed.** Everything hangs off a canonical `productId` with `aliases`, so the bank's
  free-text product names (`Tarjeta de Crédito NL`, `NL`, …) all resolve.
- **Graceful degradation.** Any section may be absent. Missing data produces an explicit
  `INSUFFICIENT_REFERENCE_DATA` finding for the affected checks — never a false pass/fail. This lets
  us demo with partial data and tighten as the client delivers more.
- **Images by reference, not blobs.** Catalog images are referenced by `uri` + `sha256` +
  `perceptualHash`, so the bundle stays small and visual checks can compare via hashing; binaries
  are side-loaded.

## Mapping to the 55-item checklist (source of truth = the Excel)

| Bundle section | Feeds checklist items |
|---|---|
| `products[].tariffs`, `interestRates` (TASA) | 9 (rate), 10 (CAT), and all interest math |
| `clientAccounts` | 2–8 (name/address/card/CLABE/client#/RFC), 4 (branch) |
| `priorStatements` | 17 (adeudo anterior), 36 (rewards opening), 40 (installment carry-over) |
| `expectedTransactions` (Detalle de operaciones) | 45 (descriptions), 58 (amounts), 20/44 (sums) |
| `products[].cardImage` | 27 (card image matches product) |
| `products[].importantMessageImage` | 30 (important-messages image) |
| `sequentialImages` | 47 (images after DESGLOSE, in order) |
| `mandatoryLegends` | 46 (mandatory legends present) |
| `promotions` (with validFrom/validTo) | 49 (promotions current) |
| `validationConstants.requiredFontFamily = "Aptos"` | 35 (font), 28/29 (typography) |
| `toleranceConfig` | every tolerance-band check (±$0.50 MXN, ±1.00 pt) |

Items that are intrinsic to the PDF (pagination 31, blank pages 48, logo-on-every-page 33,
QR/fiscal 50–53) need **no** reference data and are evaluated from the document alone.

## Files

- `vec-reference-bundle.schema.json` — JSON Schema (draft 2020-12) for the contract.
- `vec-reference-bundle.example.json` — a populated example using the Excel's sample values.

## Adapter contract (C# side, to be built under `Veriqan`)

```csharp
public interface IVecReferenceDataProvider
{
    Task<Result<VecReferenceBundle>> GetBundleAsync(StatementContextKey key, CancellationToken ct);
}
// Implementations: CsvReferenceDataAdapter, DatabaseReferenceDataAdapter, ApiReferenceDataAdapter.
// All return the same VecReferenceBundle deserialized from this JSON contract.
```
