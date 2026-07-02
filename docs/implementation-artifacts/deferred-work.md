# Deferred work

Findings surfaced during reviews that are pre-existing / out of scope for the
triggering story. Not caused by the change under review.

## From GH#23 review (2026-07-01) — spec-gh-23-webui-fixtures-in-container

- **[low] Coarse missing-directory error path.** If the PRP1 fixtures fail to land,
  `FixtureFinder.FindFixturesPath` throws `DirectoryNotFoundException`, which the razor
  `LoadFixture` handlers do NOT catch in their `catch (FileNotFoundException)` branch —
  it falls through to the generic `catch (Exception)` and shows "Error processing PDF/XML: …"
  instead of a clear "fixture directory not found". Degrades gracefully; pre-existing.
  Files: `PdfProcessingSection.razor` / `XmlProcessingSection.razor` LoadFixture catch blocks.
- **[low] Fixture-name maintenance trap.** The old 10-digit `555CCC-6666666662025.pdf`
  remains in `Prisma/Fixtures/PRP1` unreferenced, and near-identical stems now coexist
  (`555CCC-6666662025.*` 7-digit vs `555CCC-66666662025.*` 8-digit vs the former 10-digit).
  Future mis-wire hazard; corpus-hygiene cleanup, owner-gated.
- **[low] CWD assumption in FixtureFinder.** Strategy 1 (`Directory.GetCurrentDirectory()`)
  equals `/app` only because Docker defaults CWD to WORKDIR; a compose `working_dir:` /
  k8s `workingDir:` override would break it. Currently robust by redundancy (Strategy 2 =
  `AppDomain.BaseDirectory` = `/app`). Noting only.

## From GH#24 review (2026-07-01) — spec-gh-24-webui-playwright-browsers

- **[low] Demo page ignores config-supplied `LaunchArgs`.** `BrowserAutomationDemo.razor:246`
  builds its own `BrowserAutomationOptions` and copies only Headless/timeouts, so it always uses
  the default `LaunchArgs`. Fine today (the default is what makes the container path work), but a
  `BrowserAutomation:LaunchArgs` override in config would be ignored on the demo path (the
  DI-registered adapter still honors it).
- **[med/security] X11 demo exposure.** Mounting `/tmp/.X11-unix` + `xhost +local:` + `--no-sandbox`
  Chromium as root grants the container access to the host X server (keystroke injection / screen
  scraping). Acceptable for a local demo box; must NOT load `docker-compose.staging.override.yml`
  on shared/CI hosts. Broader gating tracked in GH#27 (auth on demo pages).
- **[low] web-ui root-user assumption.** The Chromium `--no-sandbox` default and `/root/.cache/ms-playwright`
  path assume the container runs as root (no `USER` directive today). If a `user:` is added to web-ui,
  revisit both.
