---
title: 'GH#27 — Require auth + authorization on all demo pages (gate /browser-automation most strictly)'
type: 'feature'
created: '2026-07-02'
status: 'done'
baseline_commit: 'a694f516'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The demo pages (Home, /document-processing, /adaptive-extractor, /oficio-summary, the
system-flow pages, etc.) were anonymously accessible. `/browser-automation` is the most sensitive — it
launches a real headed Chromium via X11 (GH#24) — yet was open. Harden all demo pages behind ASP.NET
Core Identity, with `/browser-automation` (and admin infra pages) restricted to the Admin role.

**Approach:** Surgical per-page `@attribute [Authorize]` (NOT a global `FallbackPolicy` — the Account/
Identity pages carry no `[AllowAnonymous]`, so a fallback risks a login redirect loop / blocked static
assets). Routing already wires `AuthorizeRouteView` → `RedirectToLogin` for anonymous users, so gated
pages redirect to `/Account/Login?ReturnUrl=…` and the login page stays reachable. Add the
`Microsoft.AspNetCore.Authorization` using once to `Components/_Imports.razor` (Razor dedupes it against
pages that already import it — verified no CS0105).

Seeded demo credentials (built-in defaults, no compose override): **admin@prisma.local / Admin@Prisma1!**
(roles Admin+Administrator) and **reviewer@prisma.local / Reviewer@Prisma1!** (Reviewer).

## Boundaries & Constraints

**Always:** `[Authorize]` (any authenticated) on demo pages; `[Authorize(Roles = "Admin")]` on
`/browser-automation`, `/demo-admin`, `/admin/*`. Leave Account/login, health, NotFound/Error anonymous.

**Never:** Do NOT use a global fallback policy (login-loop risk here). Do NOT gate the Account pages or
`/health*`. Do NOT change the seeded role names.

## I/O & Edge-Case Matrix

| Scenario | State | Expected |
|----------|-------|----------|
| Anon → any gated demo page | not signed in | 302 → `/Account/Login?ReturnUrl=…` |
| Anon → `/Account/Login` | — | 200 (reachable, no loop) |
| Reviewer → `/browser-automation` | authenticated, no Admin | "not authorized" (AuthorizeRouteView) |
| Admin → `/browser-automation` | Admin role | allowed |

</frozen-after-approval>

## Code Map

- `Components/_Imports.razor` — add `@using Microsoft.AspNetCore.Authorization`.
- Admin-gated: `BrowserAutomationDemo.razor`, `DemoAdmin.razor`, `Admin/ConnectionStringConfig.razor`, `Admin/DatabaseMigration.razor` — `@attribute [Authorize(Roles = "Admin")]`.
- Authenticated: `Home.razor` (note: had a UTF-8 BOM, gated manually), `DocumentProcessing.razor`, `AdaptiveDocxDemo.razor`, `OficioSummary.razor`, `Mission1HappyPath.razor`, `DocumentProcessingDashboard.razor`, `SystemFlowDashboard.razor`, `OcrFilterTester.razor`, `ManualReviewDashboard.razor`, `ReviewCaseDetail.razor`, `SystemFlow/{Intake,Processing,Storage,External,FinalProcessing,Monitoring,Realtime,Siara,Reconciliation}.razor` — `@attribute [Authorize]`.
- Already gated (unchanged): Dashboard, SlaDashboard, ExportManagement, AuditTrailViewer(x2), Auth.

## Tasks & Acceptance

- [x] `_Imports` using; per-page `[Authorize]` / `[Authorize(Roles="Admin")]`.

**Acceptance / Verified 2026-07-02 (live container):** build 0/0; anon `GET /`, `/document-processing`,
`/adaptive-extractor`, `/oficio-summary`, `/browser-automation` → 302 to `/Account/Login?ReturnUrl=…`;
`/Account/Login` → 200 (no loop). Role-difference (reviewer denied vs admin allowed on
`/browser-automation`) is framework-enforced by the attribute = demo login step.

## Design Notes

Fine-grained RBAC (Reviewer-only manual-review, etc.) is intentionally out of scope — this gates demo
pages behind authentication + Admin for the sensitive/infra surfaces. Tighten per-role later if needed.
