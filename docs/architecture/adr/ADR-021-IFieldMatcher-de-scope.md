# ADR-021: IFieldMatcher<T> — De-Scope in Favour of IFieldMatchingService + IFusionExpediente

**Date**: 2026-06-20
**Status**: Accepted
**Deciders**: Development Team
**Tags**: itdd, interface-conformance, fusion, field-matching, prp
**Related**: PRP.md §Features 28,29,33; `IFusionExpediente.cs`; `IFieldMatchingService.cs`; ADR-004

---

## Context

The PRP (§Interface Inventory, §Interface Contracts) names `IFieldMatcher<T>` as a generic
interface spanning three distinct features:

| Feature | Role of IFieldMatcher<T> |
|---------|--------------------------|
| F-28 (Stage 2) | Match field values across XML, DOCX, PDF sources |
| F-29 (Stage 3) | Generate a single unified metadata record from matched fields |
| F-33 (Stage 4) | Validate completeness and consistency of the final match result |

The PRP contract defines three methods:
```
MatchFieldsAsync(List<T> sources, FieldDefinition[])   → Result<MatchedFields>
GenerateUnifiedRecordAsync(MatchedFields)              → Result<UnifiedMetadataRecord>
ValidateMatchResultAsync(MatchedFields, string[])      → Result<ValidationResult>
```

**Production equivalents already present:**

1. `IFieldMatchingService` (`Domain/Interfaces/IFieldMatchingService.cs`, line 4) — single
   method `MatchFieldsAndGenerateUnifiedRecordAsync(DocxSource?, PdfSource?, XmlSource?,
   FieldDefinition[], ...)` collapses F-28 + F-29 + F-33 into one coordinated workflow.
   Implemented by `FieldMatchingService` (`Application/Services/FieldMatchingService.cs`).

2. `IFusionExpediente` (`Domain/Interfaces/IFusionExpediente.cs`, line 47) — dedicated
   interface for the heavier multi-source fusion at `Expediente` entity level, implemented
   by `FusionExpedienteService` (`Infrastructure.Classification/FusionExpedienteService.cs`).
   Exposes `FuseAsync(xml, pdf, docx, ...) → Result<FusionResult>` and
   `FuseFieldAsync(fieldName, candidates, ...) → Result<FieldFusionResult>`.

The split between `IFieldMatchingService` (cross-format field-level matching producing
`UnifiedMetadataRecord`) and `IFusionExpediente` (weighted multi-source reconciliation
producing a `FusionResult` with per-field confidence and conflict maps) reflects a
deliberate separation of application-layer orchestration from infrastructure-layer fusion
logic, consistent with the hexagonal architecture of the solution.

`FusionResult` (`Domain/ValueObjects/FusionResult.cs`) carries
`Dictionary<string, FieldFusionResult> FieldResults`, `SourceReliabilities`,
`ConflictingFields`, `MissingRequiredFields`, and `NextAction` — a richer contract than
the PRP's `MatchedFields` stub. `FieldFusionResult` (`Domain/ValueObjects/FieldFusionResult.cs`)
records `Confidence`, `Decision` (enum `FusionDecision`), `ContributingSources`,
`WinningSource`, `ConflictingValues`, and `RequiresManualReview`.

**The capability described by all three PRP features is present and E2E-tested**
(`MultiSourceFusionTests.cs`, `MultiSourceFusionIntegrationTests.cs`,
`FusionExpedienteServiceContractTests.cs`, `FusionExpedienteServiceMutationTests.cs`).

## Decision

Do **not** declare `IFieldMatcher<T>` in the production code. The interface name and its
generic type parameter are superseded by the concrete, richer pair
`IFieldMatchingService` / `IFusionExpediente`.

Update PRP.md wording for Features 28, 29, 33 to reference the actual interfaces:

> "Field matching across sources is performed by **`IFieldMatchingService`** (application
> layer) and **`IFusionExpediente`** (infrastructure fusion). The generic `IFieldMatcher<T>`
> placeholder from the initial ITDD inventory is retired."

## Rationale

1. **Capability is live and tested** — `FusionExpedienteService` runs in the Athena Worker
   pipeline and is covered by over 30 test files including mutation tests.
2. **Design is richer than the PRP stub** — the actual implementation adds source reliability
   weighting, genetic-algorithm-tunable coefficients, fuzzy matching via FuzzySharp, and a
   `FusionDecision` taxonomy the PRP contract never specified.
3. **Generic type parameter adds no value** — both production interfaces are already typed
   to the domain sources (`DocxSource`, `PdfSource`, `XmlSource`); introducing a third
   generic interface would add an abstraction layer with no implementation.
4. **No DI registration gap** — `IFieldMatchingService` and `IFusionExpediente` are
   registered in `Infrastructure.Classification/DependencyInjection/ServiceCollectionExtensions.cs`.

## Consequences

- The ITDD contract-test suite (`Tests.Domain.Interfaces/MockFusionExpedienteContractTests.cs`
  and `FusionExpedienteContract.cs` in `09 Testing`) should be understood as the operative
  ITDD contract for this capability.
- PRP.md §Feature-to-Interface Mapping rows for F-28, F-29, F-33 require a one-line
  amendment pointing to the real interfaces.
- No production code change is needed.
