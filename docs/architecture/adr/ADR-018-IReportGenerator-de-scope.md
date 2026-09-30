# ADR-018: IReportGenerator — De-Scope in Favour of AuditReportingService + Blazor Razor Components

**Date**: 2026-06-20
**Status**: Accepted
**Deciders**: Development Team
**Tags**: itdd, interface-conformance, reporting, audit, export, prp
**Related**: PRP.md §Feature 18; `AuditReportingService.cs`; `ClassificationReportGenerator.razor`; `AuditTrailViewer.razor`

---

## Context

The PRP (§Feature 18, §Stage 2 Interfaces) defines `IReportGenerator`:

> **Purpose**: Produces output summaries suitable for review and reporting.
>
> **Dependencies**: `IAuditLogger`, `IFileClassifier`

```
ExportSummaryAsync(ExportFormat, SummaryFilters?) → Result<string>      // file path
GenerateClassificationReportAsync(DateTime, DateTime, ExportFormat) → Result<string>
```

**Production equivalents:**

1. **`AuditReportingService`** (`Application/Services/AuditReportingService.cs`) — a
   concrete application service (not interface-backed) that provides:
   - `GenerateClassificationReportCsvAsync(DateTime, DateTime, CancellationToken)`
   - `GenerateClassificationReportJsonAsync(DateTime, DateTime, CancellationToken)`
   - `ExportAuditLogCsvAsync(DateTime, DateTime, ActionType?, userId, CancellationToken)`
   - `ExportAuditLogJsonAsync(DateTime, DateTime, ActionType?, userId, CancellationToken)`

   It is injected via constructor with `IAuditLogger` — exactly the dependency the PRP
   specified. The methods return `Result<string>` with the CSV/JSON content.

2. **`ClassificationReportGenerator.razor`** (`07 UI/.../Components/Shared/
   ClassificationReportGenerator.razor`) — a MudBlazor dialog component that presents a
   date-range picker and triggers report generation, exercising `AuditReportingService`
   from the UI layer.

3. **`AuditTrailViewer.razor`** (`07 UI/.../Components/Pages/AuditTrailViewer.razor`) —
   a full `[Authorize]`-gated page that queries `IAuditLogger` directly and renders an
   audit record table with export actions.

4. **`QaHarness/Reporting/JsonReportWriter.cs`** (`09 Testing/02 QaHarness/`) — a
   report writer in the QA harness (test infrastructure), confirms the reporting pattern
   is consistent across production and test tooling.

**Gap assessment**: `AuditReportingService` is a concrete class, not registered behind an
interface in DI. This is a conformance gap against the ITDD principle (depend on
abstractions), but the correct fix is to extract `IAuditReportingService` from the
existing class — not to introduce the unrelated `IReportGenerator` stub.

## Decision

Do **not** declare `IReportGenerator` as a standalone interface. The reporting capability
described by PRP Feature 18 is fulfilled by `AuditReportingService`.

As a follow-on improvement (separate from this ADR), extract an `IAuditReportingService`
interface from the existing concrete class to enable mocking in unit tests and allow
alternative report format implementations. This is a small refactor (estimate: 1–2 hours).

Update PRP.md §Feature 18 to read:

> "Summary reporting (F-18) is delivered by **`AuditReportingService`** (CSV/JSON export
> of classification and audit data). The `IReportGenerator` placeholder is retired;
> an `IAuditReportingService` interface extraction is tracked as a separate housekeeping
> item."

## Rationale

1. `AuditReportingService` provides CSV and JSON export for both classification and full
   audit data — covering both PRP methods (`ExportSummaryAsync` and
   `GenerateClassificationReportAsync`).
2. The Blazor UI components confirm the reporting path is usable end-to-end in the running
   system.
3. `IReportGenerator` introduces a different vocabulary (`ExportSummaryAsync`,
   `SummaryFilters`) that does not align with the existing method signatures, making it a
   dead alias rather than a meaningful contract.
4. Adding an `IAuditReportingService` interface to wrap the existing concrete class is a
   small, low-risk improvement that does not require the name `IReportGenerator`.

## Consequences

- PRP §Feature 18 mapping to be amended to `AuditReportingService` /
  `IAuditReportingService` (once extracted).
- Housekeeping ticket: extract `IAuditReportingService` from `AuditReportingService` and
  register the interface in DI. Estimate: small (1-2 hours).
- No disruptive production code change required now.
