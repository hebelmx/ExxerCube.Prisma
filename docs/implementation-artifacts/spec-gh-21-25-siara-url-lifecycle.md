---
title: 'GH#21 + GH#25 — SIARA "Open" dead link + Windows-only simulator lifecycle in the Linux container'
type: 'bugfix'
created: '2026-07-02'
status: 'done'
baseline_commit: 'a9680b5e'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:**
- **#21** Home "Open SIARA" redirects the *user's* browser to the server-side config
  `NavigationTargets:SiaraUrl` = `http://siara-simulator:8080` — a Docker-internal DNS name that
  does not resolve from the browser → dead link. The sim is host-published on `localhost:8084`.
- **#25** `/browser-automation` SIARA Start/Stop uses `Process.Start` of a **Windows** `Siara.Simulator.exe`
  — impossible in the Linux container, so "Start" never sets `_simulatorStartedByUI` and "Stop" always
  reports *"Cannot stop simulator - it was not started by this page"*. The simulator already runs as the
  containerized `prisma-siara-simulator` service.

**Key nuance:** the Playwright automation agent's browser process runs **inside** the container (X11 only
forwards the window), so the scraping agent correctly uses the **server-facing** `siara-simulator:8080`.
Only the redirect of the *user's* browser needs the host-facing URL. So the fix is to *split* the two
URLs, not replace one.

**Approach:** (1) Add `NavigationTargetOptions.SiaraBrowserUrl` (browser-facing, `localhost:8084`) distinct
from `SiaraUrl` (server-facing). Home "Open SIARA" and the new browser-automation "Open SIARA" button use
it; the agent + availability polling keep `SiaraUrl`. (2) Replace the `.exe` lifecycle with a
container-service model: "Check Availability" polls the service; "Refresh Status" re-checks; nothing is
launched or killed. Wire `NavigationTargets__SiaraBrowserUrl` in the web-ui compose service.

## Boundaries & Constraints

**Always:** Keep `SiaraUrl` server-facing for the container-hosted agent + polling. `SiaraBrowserUrl`
falls back to `SiaraUrl` then `http://localhost:8084` (dev non-container: same host, still works).

**Never:** Do NOT point the Playwright agent at the browser-facing URL (its browser is in the container).
Do NOT reintroduce `Process.Start` of a platform-specific exe. Do NOT change host port 8084 (18090 = Veriqan worker).

## I/O & Edge-Case Matrix

| Scenario | State | Expected |
|----------|-------|----------|
| Home "Open SIARA" (container) | SiaraBrowserUrl=localhost:8084 | user browser → 302 SIARA portal (no dead link) |
| Agent scrape SIARA (container) | SiaraUrl=siara-simulator:8080 | container Chromium resolves service name; works |
| "Check Availability" | sim container up | polls SiaraUrl → "Available" |
| "Refresh Status" | container-managed | info snackbar, status refreshed — never "not started by this page" |
| Dev box (non-container) | SiaraBrowserUrl unset | falls back to SiaraUrl / localhost:8084 |

</frozen-after-approval>

## Code Map

- `.../NavigationTargets/NavigationTargetOptions.cs` — add `SiaraBrowserUrl` (+ doc on server-vs-browser).
- `.../Web.UI/Components/Pages/Home.razor:211` — `"siara"` → `SiaraBrowserUrl ?? SiaraUrl ?? localhost:8084`.
- `.../Web.UI/Components/Pages/BrowserAutomationDemo.razor` — drop `GetSimulatorPath`/`Process` fields/`@using System.Diagnostics`; `StartSimulator`→availability check; `StopSimulator`→status refresh; `Dispose` no-op; add `_siaraBrowserUrl` + "Open SIARA" button + relabel controls (Check Availability / Open SIARA / Refresh Status; chip Available/Unreachable).
- `.../Web.UI/appsettings.json:19` — add `"SiaraBrowserUrl": ""`.
- `docker-compose.dev.yml` (web-ui, ~321) — add `NavigationTargets__SiaraBrowserUrl: "http://localhost:8084"`.

## Tasks & Acceptance

- [x] Options + Home + appsettings + compose: browser-facing URL split (#21).
- [x] BrowserAutomationDemo: container-service lifecycle, remove `.exe` model, "Open SIARA" button (#25).

**Acceptance / Verified 2026-07-02:** build 0/0; container env `SiaraBrowserUrl=http://localhost:8084`,
`SiaraUrl=http://siara-simulator:8080`; `curl localhost:8084` → 302 (host-reachable target of "Open SIARA");
web-ui boots, `GET /` → 200. Visual click-through of "Open SIARA" / "Check Availability" = demo step.

**Note:** host port map documented in `docker-compose.staging.override.yml` (8084 sim; 18090 = Veriqan worker).
