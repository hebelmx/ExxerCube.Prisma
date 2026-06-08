# Finding — NameMatchingPolicy self-pairing defeats fuzzy name matching

**Found:** 2026-06-08, via Stryker.NET mutation testing of `NameMatchingPolicy` (see
`docs/qa/test-plans/mutation-testing.md`, "Fifth unit").
**Severity:** High — the entire purpose of this class (fuzzy, alias-aware name matching with conservative
thresholds) does not function. It always reports a perfect match and never a conflict, for *any* inputs.
**Status:** OPEN. Intentionally **not fixed** in the mutation-testing pass. **Recommended as its own focused
session** (it is a real logic/behavior change with identity-matching and possibly security implications).

---

## TL;DR

`NameMatchingPolicy` is meant to decide whether two person/authority names refer to the same entity, using
fuzzy similarity (FuzzySharp), an alias map (e.g. `PEREZ|PERES`), and accent-insensitive normalization, with
conservative `AcceptThreshold` (0.95) / `ConflictThreshold` (0.80) gates. It does none of that in practice:
because it scores every value **against itself**, and `ScorePair(x, x)` returns `1.0`, the maximum score is
**always 1.0**. So:

- `SelectBestValueAsync` always returns the **first** value, with `Confidence = AgreementLevel = 1.0` and
  `HasConflict = false` — regardless of how different the candidate names are.
- `CalculateAgreementLevelAsync` always returns `1.0`.
- `HasConflictAsync` always returns `false` (for any sane threshold).

The fuzzy scoring, the alias map, and the accent normalization are computed but their results never affect
the outcome — they are unreachable in effect (~70% of the file).

## Where

`Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Classification/NameMatchingPolicy.cs`

`SelectBestValueAsync`, ~L59-62:
```csharp
var best = normalized
    .SelectMany(a => normalized.Select(b => (A: a, B: b, Score: ScorePair(a.Normalized, b.Normalized))))
    .OrderByDescending(p => p.Score)
    .First();
```
`CalculateAgreementLevelAsync`, ~L104-109:
```csharp
double best = 0;
foreach (var a in normalized)
foreach (var b in normalized)
{
    best = Math.Max(best, ScorePair(a, b));   // includes a == b
}
```
`ScorePair`, ~L148-153:
```csharp
private double ScorePair(string a, string b)
{
    if (string.Equals(a, b, StringComparison.Ordinal))
        return 1.0;                            // the diagonal: always hit, always wins
    ...
}
```

## Root cause

Both aggregations form the **full cross-product including the diagonal** (`a` paired with itself). The
diagonal pair always scores `1.0` (`ScorePair(x, x)` → exact-equal short-circuit). Since the code takes the
**maximum** score across all pairs, the diagonal's `1.0` dominates every off-diagonal (genuinely-comparing)
score. The intent was almost certainly to compare *distinct* values and take the best/representative
agreement among them.

## Impact

- **Conflict detection is dead:** two sources reporting clearly different names (e.g. "Juan Pérez" vs
  "María González") are reported as agreeing perfectly with no conflict. Any reviewer gate keyed on
  `HasConflict` / agreement for name fields never fires.
- **Alias + accent logic is wasted:** the `PEREZ|PERES`, accent-stripping, and FuzzySharp work has no effect
  on results, so a regression in any of it would be invisible — which is exactly why mutation testing leaves
  ~70% of this file uncovered/equivalent (the mutants there change nothing observable).
- **Domain relevance:** this is a bank legal-document system where matching the requesting authority/person
  across XML and OCR sources matters. Silently treating all name pairs as identical undermines that.

## Suggested direction (for the agent who takes this)

1. **Exclude the diagonal** in both aggregations — compare only *distinct* pairs (by index, not by value, so
   two sources that legitimately carry the same name are still compared). For a single comparable value,
   define the intended semantics explicitly (probably "trivially agrees / no conflict"), don't fall through
   to the degenerate `1.0`.
2. Decide the **representative score** for >2 values (min? mean? worst-pair?). For *conflict* detection you
   most likely want the **lowest** pairwise score (the most disagreement), not the highest.
3. Then the alias/normalization/fuzzy paths become live — re-run Stryker on `NameMatchingPolicy.cs` and the
   ~40 currently-unkillable mutants there should become killable. Add real cross-name tests (alias match,
   accent-equal, clearly-different → conflict) at that point.

Drive it with tests first (ITDD). The scoped config already includes this file at
`Tests.Infrastructure.Classification/stryker-config.json`
(run `StrykerCompat=true dotnet stryker` from that dir).

## Guardrails

- The existing mutation-killing tests (`NameMatchingPolicyMutationKillingTests.cs`) deliberately assert only
  behavior that stays correct after the fix (guards, result construction, identical-name → 1.0). They should
  **keep passing** once you fix the diagonal; you are adding new cross-name tests, not rewriting these.
- Don't "fix" by asserting the current broken behavior anywhere.
- This is closely analogous to the other open finding
  (`2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md`): both are conflict-detection paths
  that silently never fire. Consider tackling them in the same session.
