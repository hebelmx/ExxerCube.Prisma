---
title: 'GH#22 — Home "Internet Archive"/"Project Gutenberg" cards are plain external links, not IDownloader demos'
type: 'bugfix'
created: '2026-07-02'
status: 'done'
baseline_commit: 'af665940'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** On Home, the "Internet Archive" and "Project Gutenberg" cards just `NavigateTo(archive.org |
gutenberg.org, forceLoad: true)` — a raw external redirect, no ingestion. They should demo the
`IDownloader` / browser-automation download → pipeline path (as SIARA does).

**Approach:** The `/browser-automation` page already IS the real IDownloader/browser-automation demo
(Gutenberg + Archive download flows via `IBrowserAutomationAgent`, with a source `MudSelect`). Route the
two Home cards to it internally with the source preselected via `?source=archive|gutenberg`, instead of
an external link. SIARA keeps opening the portal (GH#21). No new download UI is built — the demo already
exists; this just wires the cards to it.

## Boundaries & Constraints

**Always:** Use internal Blazor nav (no `forceLoad`) for archive/gutenberg → `/browser-automation?source=…`.
Preselect only whitelisted sources (`gutenberg|archive|siara`); ignore anything else (keep the default).

**Never:** Do NOT external-redirect archive/gutenberg. Do NOT duplicate the download logic on Home.

## I/O & Edge-Case Matrix

| Scenario | Input | Expected |
|----------|-------|----------|
| Home "Download Demo" (Archive) | click | internal nav `/browser-automation?source=archive`; source dropdown = Archive |
| Home "Download Demo" (Gutenberg) | click | internal nav `?source=gutenberg`; source = Gutenberg |
| Deep-link `?source=siara` | direct URL | SIARA panel renders (proven) |
| `?source=bogus` / none | direct URL | default `gutenberg`, no crash |

</frozen-after-approval>

## Code Map

- `.../Web.UI/Components/Pages/Home.razor` — `NavigateToSource`: archive/gutenberg → internal `/browser-automation?source=…`; SIARA → browser-facing portal URL. Card captions/buttons relabeled "Download Demo" (IDownloader pipeline).
- `.../Web.UI/Components/Pages/BrowserAutomationDemo.razor` — add `[SupplyParameterFromQuery(Name="source")] SourceQuery`; in `OnInitializedAsync` preselect `_selectedSource` when whitelisted.

## Tasks & Acceptance

- [x] BrowserAutomationDemo — query-param preselect of `_selectedSource`.
- [x] Home — route archive/gutenberg cards internally with `?source`; relabel cards.

**Acceptance / Verified 2026-07-02:** build 0/0; `GET /`, `/browser-automation?source=archive`,
`?source=gutenberg` → 200; `?source=siara` renders the SIARA panel (grep=2) while the no-query default
does not (grep=0) — proves the preselect drives render. Full download click-through = demo step.
