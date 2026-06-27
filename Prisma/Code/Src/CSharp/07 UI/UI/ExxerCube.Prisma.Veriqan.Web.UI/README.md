# ExxerCube.Prisma.Veriqan.Web.UI

> **NON-PRODUCTION DEMO SURFACE**
>
> This project is a visual presentation layer for banking-client demos. It does **not** connect to
> the Veriqan pipeline, database, or any live service. All data is served by `DemoDataService`
> (hard-coded, in-memory). Wiring to the real pipeline is a future work item tracked in
> `VERIQAN-MVP-PATH-2026-06-27.md` (W-UI workstream).

## Purpose

Presents the Veriqan VEC (Verificación de Estado de Cuenta) pipeline output to a mixed
technical/legal/accounting/financial audience. Each page maps to one demo capture (§4 of
`VERIQAN-DEMO-PLAN-2026-06-27.md`):

| Route | Capture | Content |
|---|---|---|
| `/` | 0 | Panorama: pipeline diagram, 55-check overview |
| `/upload` | 1 | Statement upload + extracted fields |
| `/green` | 2 | GREEN verdict — 55-check grid all green |
| `/red` | 3 | RED verdict — failed checks + marked-PDF placeholder |
| `/blocked` | 4 | BLOCKED verdict — image-only, human-review routing |
| `/disposition` | 5 | QA analyst disposition form + append-only audit trail |

## Stack

- **.NET 10** · Blazor Web App (Interactive Server)
- **MudBlazor 8.11** (centrally pinned)
- **No database, no auth, no external services** — intentional for demo simplicity

## Running locally

```bash
dotnet run --project "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI/ExxerCube.Prisma.Veriqan.Web.UI.csproj"
# Opens at http://localhost:5280
```

## Wiring to the real pipeline (future)

Replace `DemoDataService` with a typed HTTP client calling `POST /verify` on `Veriqan.Worker`,
or inject the real `IVerificationPipeline` and `IDispositionRepository` directly. The page
components are already typed against `DemoStatementCase` which mirrors `VerificationOutcome`;
adapt that ViewModel or switch to the real type once `VerdictSummary` factories are accessible.
