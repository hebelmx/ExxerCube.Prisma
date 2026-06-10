# Finding — FieldMatcherService XML-vs-OCR additional-field conflict detection is dead code

**Found:** 2026-06-08, via Stryker.NET mutation testing of `FieldMatcherService<T>` (see
`docs/qa/test-plans/mutation-testing.md`, "Third unit").
**Severity:** Medium — a real reconciliation feature silently does nothing. Not a crash; a *missing behavior*.
**Status:** ✅ **RESOLVED (2026-06-10, commit fd0afd1) — but in a DIFFERENT class than this finding named.**
Tracing the wiring revealed that `FieldMatcherService<T>` (this finding's subject) is **registered but never
invoked in production**, and being parameterized by a single source type T (`List<T> sources`) it **cannot**
compare across XML/OCR within a call anyway. The real, production-wired multi-source orchestrator is the
**Application `FieldMatchingService`** (receives Docx+Pdf+Xml together) — which previously never populated
`AdditionalMerged`/`AdditionalConflicts` either. The reconciliation was therefore implemented **there** (the
owner chose "derive origin from the source list"): additional fields are collected per source (origin-tagged),
run through the matching policy, and `AdditionalMerged` + `AdditionalConflicts` are populated; name keys route
to the name-aware `INameMatchingPolicy`; this also revived `DeriveSlaFromAdditional`. Tests:
`FieldMatchingServiceReconciliationTests` (+8).
**`FieldMatcherService<T>` itself is left as documented dead/legacy** (superseded by the Application path) — a
candidate for deletion in a future cleanup. (Originally: OPEN, recommended as its own focused session.)

---

## TL;DR

`FieldMatcherService<T>.MergeAdditionalFields(...)` is supposed to merge the non-core (`AdditionalFields`)
values coming from **XML** sources and from **PDF/OCR** sources, and record a **conflict** when the two
disagree on the same key. It never records a conflict, because the two inputs it compares are **always
identical**. The XML-vs-OCR conflict detection (and the `Normalize` helper that exists only to support it)
is **unreachable dead code**.

Mutation testing made this concrete: ~35 mutants in that region survive/are-uncovered no matter what tests
you write through the public API, because no public input can change their outcome.

## Where

`Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Classification/FieldMatcherService.cs`

- `MatchFieldsAsync` (the call site), ~L130-138:
  ```csharp
  var xmlFields = CollectAdditional(additionalFromSources, FieldOrigin.Xml);
  var ocrFields = CollectAdditional(additionalFromSources, FieldOrigin.PdfOcr);
  var mergeResult = MergeAdditionalFields(xmlFields, ocrFields);
  ```
- `CollectAdditional(List<ExtractedFields> sources, FieldOrigin origin)`, ~L291-314
- `MergeAdditionalFields(xmlFields, ocrFields)`, ~L316-352
- `Normalize(string?)`, ~L354-355

## Root cause

`CollectAdditional` takes an `origin` parameter but **never uses it to filter by the field's real origin**.
Both the `origin == FieldOrigin.Xml` branch and the `origin == FieldOrigin.PdfOcr` branch execute the *same*
statement:

```csharp
if (origin == FieldOrigin.Xml && !kvp.Key.Equals("Origin", StringComparison.OrdinalIgnoreCase))
{
    dict[kvp.Key] = kvp.Value;          // identical body
}
else if (origin == FieldOrigin.PdfOcr && !kvp.Key.Equals("Origin", StringComparison.OrdinalIgnoreCase))
{
    dict[kvp.Key] = kvp.Value;          // identical body
}
```

So `CollectAdditional(sources, Xml)` and `CollectAdditional(sources, PdfOcr)` return **identical
dictionaries** (every non-`"Origin"` key from every source, regardless of where that field actually came
from). Feeding two identical dictionaries into `MergeAdditionalFields` means:

- the conflict branch (`existingNormalized != ocrValue`) can never be true → `Conflicts` is always empty;
- the "fill empty from OCR" branch can never be true → dead;
- `Normalize` is only used inside those dead branches → effectively dead.

There is also no mechanism to know a field's origin in the first place: `ExtractedFields.AdditionalFields`
is a flat `Dictionary<string,string?>` with no per-field origin tag (the loop even special-cases a magic
`"Origin"` key, which suggests an earlier, abandoned design).

## Evidence (mutation testing)

After hardening `FieldMatcherService` with 16 new tests, ~35 of the ~54 residual not-killed mutants are in
`CollectAdditional` (the `origin ==` checks), `MergeAdditionalFields` (conflict/fill), and `Normalize`. They
are **equivalent mutants relative to the current code** — e.g. flipping `origin == FieldOrigin.Xml` to
`!=`, or removing the conflict-add block, produces no observable difference through `MatchFieldsAsync`,
because the two collected dictionaries are identical either way. Full per-line list:
`docs/qa/test-plans/mutation-testing.md` (third-unit section) and the Stryker HTML report under
`Tests.Infrastructure.Classification/StrykerOutput/`.

## Impact

- **Functional:** when an XML source and an OCR source provide *different* values for the same non-core
  field (e.g. `Telefono`, `Email`, `Direccion`), the system does **not** flag it as a conflict and does not
  prefer one over the other in a defined way. `AdditionalConflicts` is always empty, so any UI/reviewer
  gate that relies on it never triggers for additional fields. (Core fields — Expediente/Causa/
  AccionSolicitada — are unaffected; they go through a different, working path.)
- **Why it matters here:** this is a bank legal-document reconciliation system where XML (structured) and
  OCR (scanned) representations of the same document are cross-checked. Silently swallowing disagreements on
  contact/address fields undermines the reconciliation guarantee.

## Suggested direction (for the agent who takes this)

This needs a small design decision, not just a code tweak. Options, roughly in order of effort:

1. **Tag origin at extraction time.** Give additional fields a real origin (e.g. change
   `AdditionalFields` to carry `FieldValue`/origin, or keep a parallel `Dictionary<string, FieldOrigin>`),
   then make `CollectAdditional` actually filter by `origin`. This makes `MergeAdditionalFields`'
   XML-vs-OCR comparison meaningful and is the "do it properly" path. Largest blast radius (touches
   `ExtractedFields` and every extractor that fills `AdditionalFields`).
2. **Derive origin from the source list.** `MatchFieldsAsync` already knows each source's type
   (`GetSourceType`). Collect additional fields **per source** and pass the XML-origin subset and the
   OCR-origin subset to `MergeAdditionalFields`. No domain-model change; contained to this service.
   Likely the best effort/value trade-off.
3. **Delete the dead code** if the conflict feature isn't actually wanted. Remove the `origin` param,
   `Normalize`, and the conflict branch; document that additional fields are a simple first-wins union.
   Smallest change, but loses the (intended) reconciliation feature — confirm with the product owner first.

Whichever path: drive it with tests **before** changing code (ITDD per CLAUDE.md), then re-run Stryker on
`FieldMatcherService.cs` — the ~35 dead mutants should become killable, pushing this unit well above its
current 61.43% ceiling. The scoped config already exists at
`Tests.Infrastructure.Classification/stryker-config.json` (run `StrykerCompat=true dotnet stryker` from that
dir).

## Guardrails

- Don't "fix" this by changing tests to assert the broken behavior — pin the *intended* behavior and change
  the code.
- `ExtractedFields` is a widely-used domain value object; option 1 is a cross-cutting change — scope it and
  check every `IFieldExtractor<T>` implementation before committing.
- Keep the core-field merge path (`MergeAdditionalFields` is only for additional/non-core fields) intact.
