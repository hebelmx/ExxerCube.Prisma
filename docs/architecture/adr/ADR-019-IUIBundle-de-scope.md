# ADR-019: IUIBundle — De-Scope; Capability Fulfilled by Blazor + MudBlazor Component Model

**Date**: 2026-06-20
**Status**: Accepted
**Deciders**: Development Team
**Tags**: itdd, interface-conformance, ui, blazor, mudblazor, prp
**Related**: PRP.md §Feature 24; `07 UI/UI/ExxerCube.Prisma.Web.UI/Components/`; ADR-009

---

## Context

The PRP (§Feature 24, §Stage 3 Interfaces) defines `IUIBundle`:

> **Purpose**: Technology-agnostic frontend UI library for validation, editing, and submission.
>
> **Note**: This interface is technology-agnostic and may have different implementations
> for Blazor, React, Vue, etc.

```csharp
RenderValidationFormAsync(UnifiedMetadataRecord, ValidationRule[]) → Result<UIComponent>
RenderEditingFormAsync(UnifiedMetadataRecord, string[])             → Result<UIComponent>
RenderSubmissionWorkflowAsync(UnifiedMetadataRecord)                → Result<UIComponent>
```

**Architectural reality:**

The project committed to **Blazor Server with MudBlazor** as its single UI technology
(confirmed by the Web.UI project and its MudBlazor 8.x dependency). The concept of a
"technology-agnostic UI component library" injected via a C# interface does not map to
Blazor's component model: Blazor components are `.razor` files composed by the framework
at render time, not objects returned from async methods.

The actual UI component surface covering the IUIBundle intent is:

| PRP intent | Actual component | Path |
|---|---|---|
| Validation form | `DocumentProcessing.razor`, `ReviewCaseDetail.razor` | `Components/Pages/` |
| Editing form | `ManualReviewDashboard.razor`, `ReviewCaseDetail.razor` | `Components/Pages/` |
| Submission workflow | `ExportManagement.razor`, `Mission1HappyPath.razor` | `Components/Pages/` |
| Shared field view | `FieldMatchingView.razor`, `ClassificationResultsCard.razor` | `Components/Shared/` |
| SLA overview | `SlaTimelineView.razor` | `Components/Shared/` |
| Report generation | `ClassificationReportGenerator.razor` | `Components/Shared/` |

A `UIComponent` object returned from a C# async method cannot be rendered by Blazor's
DOM diffing engine — the framework requires component types resolved at compile time.
Implementing `IUIBundle.RenderValidationFormAsync` as a C# interface in a Blazor project
is architecturally unsound.

**No search hit for `IUIBundle`, `UIComponent`, or `RenderValidation` was found in the
production `01 Core` through `04 Services` tree**, confirming the interface was never
declared in production code.

## Decision

Do **not** declare `IUIBundle` as a production interface. The technology-agnostic UI
abstraction the PRP imagined is superseded by the **Blazor + MudBlazor component model**,
which is the committed UI technology for this system.

The Blazor component library (`07 UI/.../Components/`) is the operative implementation of
PRP Feature 24. Its composition — pages, shared components, and dialogs — fulfils the
validation, editing, and submission use cases.

Update PRP.md §Feature 24 to read:

> "The UI component library (F-24) is realised by the **Blazor + MudBlazor component set**
> in `07 UI/`. The `IUIBundle` C# interface placeholder is retired as incompatible with
> Blazor's compile-time component model."

## Rationale

1. **Blazor's component model is compile-time, not runtime-injection** — `RenderXAsync`
   returning a `UIComponent` object cannot integrate with Blazor's render tree without
   a custom, fragile intermediate layer.
2. **Single-technology commitment** — ADR-009 retired the SignalR in-repo duplication in
   favour of `IndFusion.Ember`; the same principle applies here: the team picked Blazor
   as the UI technology and there is no active multi-framework requirement.
3. **Components already exist** — `ManualReviewDashboard.razor`, `FieldMatchingView.razor`,
   `ClassificationReportGenerator.razor`, and `ExportManagement.razor` cover all three
   PRP use cases (validate, edit, submit).
4. **No test or pipeline depends on `IUIBundle`** — zero production usages found.

## Consequences

- PRP §Feature 24 to be annotated as fulfilled by the Blazor component tree.
- If a future requirement demands embedding Prisma UI components in a non-Blazor host
  (e.g., a React micro-frontend shell), a dedicated adapter approach (Web Components,
  Blazor hybrid embedding, or Blazor WASM) should be evaluated under a new ADR at that
  time.
- No production code change required.
