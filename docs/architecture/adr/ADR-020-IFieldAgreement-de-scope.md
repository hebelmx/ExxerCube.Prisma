# ADR-020: IFieldAgreement — De-Scope; Capability Embedded in FieldFusionResult + MatchedFields

**Date**: 2026-06-20
**Status**: Accepted
**Deciders**: Development Team
**Tags**: itdd, interface-conformance, fusion, confidence, field-agreement, prp
**Related**: PRP.md §Feature 31; `FieldFusionResult.cs`; `MatchedFields.cs`; `FusionResult.cs`; ADR-014

---

## Context

The PRP (§Feature 31, §Stage 3 Interfaces) defines `IFieldAgreement`:

> **Purpose**: Reports field-level match confidence and origin trace.
>
> **Dependencies**: `IFieldMatcher<T>`

```csharp
AnnotateFieldAgreementAsync(MatchedFields) → Result<FieldAgreementAnnotations>
GetFieldConfidenceAsync(string, MatchedFields) → Result<int>   // 0-100
GetFieldOriginTraceAsync(string, MatchedFields) → Result<FieldOriginTrace>
```

**Production equivalents:**

`IFieldAgreement` is explicitly designed as a post-processing view over `IFieldMatcher<T>`
results. Because `IFieldMatcher<T>` itself was retired (ADR-014) in favour of
`IFusionExpediente` and `IFieldMatchingService`, the question is whether the three
capabilities this interface provides exist in the production data structures.

1. **Field-level confidence** (`GetFieldConfidenceAsync`) is embedded in:
   - `FieldFusionResult.Confidence` (0.0–1.0 double, `Domain/ValueObjects/FieldFusionResult.cs`,
     line 28) — present on every field result returned by `IFusionExpediente.FuseFieldAsync`.
   - `FieldMatchResult.AgreementLevel` (used in `MatchedFields.OverallAgreement` calculation
     in `FieldMatchingService.cs`, line 230).

2. **Origin trace** (`GetFieldOriginTraceAsync`) is embedded in:
   - `FieldFusionResult.WinningSource` (`SourceType?`, line 44) — which source won the
     weighted vote.
   - `FieldFusionResult.ContributingSources` (`List<SourceType>`, line 38) — all sources
     that had a non-null value.
   - `FieldFusionResult.ConflictingValues` (`List<(SourceType, string?)>`, line 68) — per
     source values for conflict cases.
   - `FieldMatchResult` in `MatchedFields` tracks `MatchedValue` and `HasConflict`.

3. **Agreement annotation** (`AnnotateFieldAgreementAsync`) is embedded in:
   - `FieldFusionResult.Decision` (`FusionDecision` enum at `Domain/Enum/FusionDecision.cs`)
     — encodes AllAgree, FuzzyAgreement, WeightedVoting, BestEffort, Conflict, etc.
   - `FieldFusionResult.RequiresManualReview` and `SuggestReview` flags.
   - `FusionResult.ConflictingFields` and `FusionResult.MissingRequiredFields` (collection-
     level view of agreement state).

**All three capabilities are present as value object properties returned directly from
`IFusionExpediente.FuseAsync` and `FuseFieldAsync`.** Introducing `IFieldAgreement` would
require writing a service that reads these properties and re-exposes them through a method
call — pure indirection with no new information.

The `FieldMatchingServiceMutationTests.cs` (`08 Tests/01 Core/Tests.Application/Services/`)
confirms the confidence and conflict paths are tested in the existing field matching service.

## Decision

Do **not** declare `IFieldAgreement` as a standalone interface. The field-level
confidence reporting and origin-trace capabilities described by PRP Feature 31 are
fulfilled by the value objects returned by `IFusionExpediente`:

- **Confidence** → `FieldFusionResult.Confidence`
- **Origin trace** → `FieldFusionResult.WinningSource` + `ContributingSources`
- **Agreement annotation** → `FieldFusionResult.Decision` + `RequiresManualReview`

Update PRP.md §Feature 31 to read:

> "Field-level agreement reporting (F-31) is delivered via the **`FieldFusionResult`**
> value object returned by `IFusionExpediente.FuseFieldAsync`, which carries confidence,
> decision rationale, origin trace, and conflict details per field. The `IFieldAgreement`
> interface placeholder is retired."

## Rationale

1. All information the PRP intended `IFieldAgreement` to surface is already present as
   first-class properties on `FieldFusionResult` and `FusionResult`.
2. Adding a stateless service interface over value-object properties is an anti-pattern
   in a domain that already applies Railway-Oriented Programming: callers already receive
   the full result graph and can query it directly.
3. `IFieldAgreement` depends on `IFieldMatcher<T>` (per PRP), which was retired in ADR-014.
   Retaining `IFieldAgreement` without its dependency would be architecturally inconsistent.
4. The Blazor UI renders confidence and conflict data directly from `FusionResult` through
   `FieldMatchingView.razor` (`07 UI/.../Components/Shared/`) — confirming the data path
   is already usable end-to-end.

## Consequences

- PRP §Feature 31 mapping to be amended to `FieldFusionResult` / `FusionResult`.
- If a future requirement needs a queryable read-model of per-field confidence data
  (e.g., persisted to the database for dashboard analytics), a dedicated read-model
  projection can be introduced at that time without the `IFieldAgreement` name.
- No production code change required.
