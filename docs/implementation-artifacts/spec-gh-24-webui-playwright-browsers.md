---
title: 'GH#24 — Visible, recordable browser-automation demo: install Chromium + X11-forward the Web.UI container'
type: 'bugfix'
created: '2026-07-01'
status: 'done'
baseline_commit: '2aa9858e37e57d7c95ccbde6bf305b624392248e'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `/browser-automation` fails — the Web.UI image never installs Playwright's Chromium (`Executable doesn't exist at /root/.cache/ms-playwright/chromium-1223/…`). Goal is a **shareholder demo we can watch and record**: the automation must be *visible*, not just non-erroring. The demo runs Playwright server-side in the container, so a headed browser normally has no display.

**Approach:** (1) Install the version-matched Chromium into the image via the bundled Playwright CLI. (2) X11-forward the container to the host (Wayland+Xwayland, `DISPLAY=:0`) so the headed Chromium opens a **real window on the desktop**, recordable with any screen recorder. (3) Add the launch args a root-in-container headed Chromium needs (`--no-sandbox`).

## Boundaries & Constraints

**Always:** Install Chromium via the *bundled* CLI (`/app/.playwright/…/node` + `package/cli.js`) so the browser build tracks pinned `Microsoft.Playwright 1.60.0`. Install `--with-deps` (runtime = Ubuntu 24.04 noble). Chromium only. Keep the demo's headed default (`_showBrowser=true`) — it is now correct. Keep host-specific display wiring (`DISPLAY`, socket mount) in the compose **override**, never baked into the image.

**Ask First:** RESOLVED (hebelmx, 2026-07-01): visibility approach = **X11 socket forwarding to the host desktop** (not noVNC, not video capture).

**Never:** Do NOT change the `Microsoft.Playwright` version. Do NOT set `Channel`/`ExecutablePath`. Do NOT install firefox/webkit. Do NOT hardcode `DISPLAY`/X11 paths in the Dockerfile.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Headed + X11 forward | `xhost +local:` done, socket mounted, DISPLAY=:0 | Chromium window opens on host desktop; navigation runs | N/A |
| Root headed, no `--no-sandbox` | container runs as root | Would fail to launch — default args include `--no-sandbox` | Prevented by default LaunchArgs |
| X11 not authorized | `xhost` not run | Launch fails "cannot open display :0" | Snackbar error; documented prerequisite |
| Missing browser (regression) | image without install step | "Executable doesn't exist" error | Surfaced in snackbar |

</frozen-after-approval>

## Code Map

- `Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/Dockerfile` -- runtime stage; add browser-install `RUN` after the fixtures COPY (GH#23).
- `/app/.playwright/node/linux-x64/node` + `/app/.playwright/package/cli.js` -- bundled Playwright CLI in publish output.
- `.../Infrastructure.BrowserAutomation/BrowserAutomationOptions.cs:11` -- add `LaunchArgs`.
- `.../Infrastructure.BrowserAutomation/PlaywrightBrowserAutomationAdapter.cs:48-52` -- pass `Args` to `LaunchAsync`.
- `docker-compose.staging.override.yml:23` -- add `web-ui` `DISPLAY` env + X11 socket mount (merges with dev.yml).
- `QUICKSTART.md` -- document the `xhost +local:` prerequisite.
- `.../Web.UI/Components/Pages/BrowserAutomationDemo.razor:198,248` -- headed default (`_showBrowser=true`); UNCHANGED, now correct.

## Tasks & Acceptance

**Execution:**
- [x] `Web.UI/Dockerfile` -- add `RUN /app/.playwright/node/linux-x64/node /app/.playwright/package/cli.js install --with-deps chromium` (after fixtures COPY) -- version-matched browser + OS libs into `/root/.cache/ms-playwright`.
- [x] `BrowserAutomationOptions.cs` -- add `List<string> LaunchArgs { get; set; } = new() { "--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu" };` -- container-safe headed-Chromium flags, applied by default at every call site.
- [x] `PlaywrightBrowserAutomationAdapter.cs` -- set `Args = _options.LaunchArgs` in the `BrowserTypeLaunchOptions` -- pass the flags through.
- [x] `docker-compose.staging.override.yml` -- under `web-ui`, add `environment: { DISPLAY: ":0" }` and `volumes: [ "/tmp/.X11-unix:/tmp/.X11-unix:rw" ]` -- forward the host X server (merges, no `!override`).
- [x] `QUICKSTART.md` -- add a note: before the browser-automation demo run `xhost +local:` on the host (demo-only; loosens local X access control) -- documents the one manual host step.

**Acceptance Criteria:**
- Given the rebuilt image, when `docker run … ls /root/.cache/ms-playwright`, then a `chromium-*` dir exists.
- Given the stack up with the X11 override and `xhost +local:`, when automation is launched (headed default), then a Chromium window appears on the host desktop and navigation returns a result — no "Executable doesn't exist" and no "cannot open display".
- Given the build, when `docker … build web-ui` and `dotnet build` of the BrowserAutomation project, then both succeed (warnings-as-errors).

## Verification

**Commands:**
- `docker compose -p prisma -f docker-compose.dev.yml -f docker-compose.staging.override.yml build web-ui` -- expected: success.
- `docker run --rm --entrypoint sh prisma-webui:dev -lc 'ls /root/.cache/ms-playwright'` -- expected: a `chromium-*` folder.
- `dotnet build "Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.BrowserAutomation/ExxerCube.Prisma.Infrastructure.BrowserAutomation.csproj"` -- expected: 0 warnings/errors.

**Manual checks:**
- Host: `xhost +local:`; bring up the stack with the override; on `http://localhost:8085/browser-automation` launch automation -- expected: a real Chromium window opens on the desktop and drives the target site (recordable).

## Suggested Review Order

**Browser install (the core fix)**

- Entry point: installs the version-matched Chromium via the bundled CLI; arch-glob keeps it portable.
  [`Dockerfile:67`](../../Prisma/Code/Src/CSharp/07%20UI/UI/ExxerCube.Prisma.Web.UI/Dockerfile#L67)

**Headed-in-container launch flags**

- Default launch args a root-in-container headed Chromium needs; benign for headless/CI callers (verified — no caller sets Args).
  [`BrowserAutomationOptions.cs:20`](../../Prisma/Code/Src/CSharp/02%20Infrastructure/Infrastructure.BrowserAutomation/BrowserAutomationOptions.cs#L20)

- Threads those args into the actual `LaunchAsync` call.
  [`PlaywrightBrowserAutomationAdapter.cs:51`](../../Prisma/Code/Src/CSharp/02%20Infrastructure/Infrastructure.BrowserAutomation/PlaywrightBrowserAutomationAdapter.cs#L51)

**X11 forwarding (makes it visible/recordable)**

- Forwards the host X display + socket into web-ui; `${DISPLAY:-:0}` follows the host; merges with base service.
  [`docker-compose.staging.override.yml:46`](../../docker-compose.staging.override.yml#L46)

- Documents the one manual host step and the failure mode.
  [`QUICKSTART.md:48`](../../QUICKSTART.md#L48)

## Design Notes

Security follow-up tracked as **GH#27** — require auth + authorization on all demo
pages, with `/browser-automation` gated most strictly (it now launches a real
headed browser via X11 forwarding). Intentionally OUT OF SCOPE here to keep this
change focused on making the demo visible; noted so it is not forgotten.
