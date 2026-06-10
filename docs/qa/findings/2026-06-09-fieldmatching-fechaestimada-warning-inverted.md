# Finding — FieldMatchingService: FechaEstimadaConclusion validation warning is inverted

**Date:** 2026-06-09
**Severity:** Low (advisory warning only — does not affect `IsValid` / processing outcome)
**Surfaced by:** mutation testing, Unit 40 (`FieldMatchingService`), §2.6 Core/Application.
**Status:** documented; behavior pinned by tests (not "fixed" — owner decision).

## What

In `FieldMatchingService.AggregateValidation` (`01 Core/Application/Services/FieldMatchingService.cs`):

```csharp
validation.WarnIf(record.Expediente.FechaEstimadaConclusion == default, "FechaEstimadaConclusion");
```

`ValidationState.WarnIf(condition, name)` adds the warning **when `condition` is _false_** (its doc:
"Adds a warning when the condition is false … condition: Condition that should hold true."):

```csharp
public void WarnIf(bool condition, string fieldName)
{
    if (!condition) { Warn(fieldName); }
}
```

So the call passes `FechaEstimadaConclusion == default` as "the condition that should hold true". The net
effect is **inverted** relative to intent:

| FechaEstimadaConclusion | `== default` | warning added? | intended? |
|---|---|---|---|
| **missing** (default)   | true  | **no warning**  | should warn |
| **present** (set)       | false | **warns**       | should be silent |

i.e. the system warns when the estimated-conclusion date **is present**, and is **silent when it is missing** —
the opposite of every other field in the same method (which use `Require(<presence-condition>, name)`).

## Likely fix (for a dedicated bug-fix session, not test hardening)

Either:
- `validation.WarnIf(record.Expediente.FechaEstimadaConclusion != default, "FechaEstimadaConclusion");` (match
  the `WarnIf`-warns-when-false contract), **or**
- `validation.Warn(...)` guarded by an explicit `if (… == default)`.

Compare the four lines above it, which correctly use `Require(!string.IsNullOrWhiteSpace(...), …)` /
`Require(<set>, …)`.

## Test status

The Unit 40 mutation tests **pin the current (inverted) behavior** so the mutants on this branch are killed:
- `AggregateValidation_FechaEstimadaConclusionSet_AddsWarning_InvertedBehavior`
- `AggregateValidation_DefaultFechaEstimadaConclusion_NoWarning_InvertedBehavior`

When the bug is fixed, **flip both assertions** (and rename them).

## Related

- `[[2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection]]` (same service family; the
  `AdditionalMerged`/conflict-detection surface is dead through this method and reserved separately).
