# ADR-015: IRuleScorer — De-Scope in Favour of IFileClassifier + FusionCoefficients

**Date**: 2026-06-20
**Status**: Accepted
**Deciders**: Development Team
**Tags**: itdd, interface-conformance, classification, scoring, prp
**Related**: PRP.md §Feature 12; `IFileClassifier.cs`; `FileClassifierService.cs`; `FusionCoefficients.cs`

---

## Context

The PRP (§Feature 12, §Stage 2 Interfaces) defines `IRuleScorer`:

> **Purpose**: Resolves duplicate or ambiguous files using rule-based decisions.

```
ScoreFilesAsync(List<FileMetadata>, ScoringRule[]) → Result<List<ScoredFile>>
ResolveDuplicatesAsync(List<FileMetadata>)         → Result<FileMetadata>
```

**Dependencies listed in PRP**: `IFileMetadataLogger`, `IFileClassifier`.

**Production equivalents:**

1. `IFileClassifier` (`Domain/Interfaces/IFileClassifier.cs`) is declared and implemented
   by `FileClassifierService` (`Infrastructure.Classification/FileClassifierService.cs`).
   The service exposes `ClassifyAsync(ExtractedMetadata, ...) → Result<ClassificationResult>`
   which internally calculates `ClassificationScores` (a value object at
   `Domain/ValueObjects/ClassificationScores.cs`) for each regulatory category —
   effectively a rule-based scoring over extracted metadata.

2. `FusionCoefficients` (`Domain/ValueObjects/FusionCoefficients.cs`) encodes source
   reliability weights and conflict-resolution thresholds that govern how conflicts between
   duplicated data fields are resolved. These coefficients drive the `FuseAsync` call in
   `FusionExpedienteService`, which handles the "resolve ambiguous / duplicate field values"
   use case the PRP assigned to `IRuleScorer.ResolveDuplicatesAsync`.

3. `ReconciliationOrchestrator` (`04 Services/Athena/Prisma.Athena.Processing/
   ReconciliationOrchestrator.cs`) appears in the `IFileClassifier` search hits and
   coordinates cross-source reconciliation, providing the orchestration layer over
   classification scoring.

**Capability gap assessment:**  
The `IScoredFile` / `ScoringRule` types the PRP draft imagined are not present, but the
functional need — deterministic rule-based confidence scoring for classification and
duplicate/ambiguity resolution — is fully served by `ClassificationScores` (produced by
`FileClassifierService`) and `FusionCoefficients` (consumed by `FusionExpedienteService`).
The concept of "select the best candidate from duplicate files" maps to
`FusionExpedienteService.FuseFieldAsync`, which implements weighted voting with margin
detection and BestEffort / Conflict decision types.

## Decision

Do **not** declare `IRuleScorer` in production code. The scoring and duplicate-resolution
capability described by PRP Feature 12 is covered by the combination of:

- `IFileClassifier` / `FileClassifierService` — for level-1/2 rule scoring over metadata.
- `IFusionExpediente` / `FusionExpedienteService` + `FusionCoefficients` — for weighted
  field-level conflict resolution.

Update PRP.md to read:

> "Duplicate and ambiguity resolution (F-12) is handled by **`IFileClassifier`** (scoring)
> and **`IFusionExpediente`** (weighted conflict resolution). The standalone `IRuleScorer`
> placeholder is retired."

## Rationale

1. **`ClassificationScores`** already models multi-rule scoring; it is produced inside
   `FileClassifierService.ClassifyAsync` and returned as part of `ClassificationResult`.
2. **`FusionExpedienteService`** implements the "select best candidate" logic with a richer
   algorithm (exact match → fuzzy → weighted voting → BestEffort / Conflict flag) than the
   PRP's `ResolveDuplicatesAsync` stub imagined.
3. Adding a thin `IRuleScorer` adapter would duplicate logic already encapsulated in the
   above services with no architectural benefit.
4. No MVP feature or E2E test depends on a separately registered `IRuleScorer`.

## Consequences

- PRP §Feature 12 mapping row to be amended to `IFileClassifier` + `IFusionExpediente`.
- No production code change required.
- If a future use case demands standalone pluggable scoring rules (e.g., a configurable
  rule engine for new regulatory regimes), a dedicated `IRuleEngine` / `IScoringStrategy`
  can be introduced at that time under a fresh ADR.
