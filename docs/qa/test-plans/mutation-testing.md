# Mutation Testing (Stryker.NET) — setup, pilot, and how to expand

**Added:** 2026-06-07b. **Tool:** Stryker.NET **4.14.2**, pinned as a repo-local tool in
`.config/dotnet-tools.json` (run `dotnet tool restore` once after cloning).

## Why this only works now
Stryker.NET could not drive **xUnit v3 + Microsoft.Testing.Platform (MTP)** test projects until the
4.x line added MTP support (~March 2026). This repo's test stack is `xunit.v3.mtp-v2` + MTP 2.1.0 (see
CLAUDE.md "Testing stack"), so mutation testing was previously unavailable. Stryker 4.14.2 supports it.

## What it is
Mutation testing measures **test-suite effectiveness**, not coverage. Stryker injects small faults
("mutants") into production code (flip `>` to `>=`, `&&` to `||`, drop a statement, etc.) and re-runs the
tests. A mutant **killed** = a test failed (good — the suite caught the fault). A mutant **survived** =
all tests still passed (a blind spot — code a test should pin but doesn't). The **mutation score** =
killed / (total non-error mutants).

## Pilot (intentionally tiny — proves the toolchain)
Scoped to one deterministic, well-tested unit so the first run is fast and the MTP compatibility is proven:

- **Config:** `Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Tests.Infrastructure.Extraction.Txt/stryker-config.json`
- **Under test:** `Infrastructure.Extraction.Txt` → `AdaptiveTxtFieldExtractor.cs`
- **Driven by:** `Tests.Infrastructure.Extraction.Txt` (35 tests, deterministic — no OCR/DB/network)

Run it:
```bash
dotnet tool restore   # once
cd "Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Tests.Infrastructure.Extraction.Txt"
dotnet stryker
# HTML report -> ./StrykerOutput/<timestamp>/reports/mutation-report.html
```

### Pilot result (2026-06-07b) — ✅ WORKING, real mutation score

```
Coverage capture complete: 272 mutations covered, 0 static
26 mutants NoCoverage · Killed 64 · Survived 167 · Timeout 0
Final mutation score: 24.90 %   (1m10s)
```

The toolchain runs end-to-end and produces a **real, trustworthy score** on the xunit.v3 + MTP +
custom-artifacts stack. 24.90% is a meaningful first baseline: the 35 passing tests pin only ~¼ of
`AdaptiveTxtFieldExtractor`'s behavior — the 167 survivors are concrete targets for stronger assertions.

**Two settings are MANDATORY here (this is the whole story — both were the cause of an earlier 0% run):**

1. **`"test-runner": "mtp"` in `stryker-config.json`.** Without it Stryker defaults to the **VSTest bridge**,
   which against MTP-native test projects (xunit.v3 / `xunit.v3.mtp-v2`) runs tests but **cannot observe
   kills** → a uniform **0% score that is always broken**. This is non-negotiable for any MTP project.
2. **Output-path accommodation for this repo's custom layout.** `Directory.Build.props` has a guarded,
   OFF-by-default block: when `StrykerCompat=true` (env var, surfaced to MSBuild), `OutputPath` and
   `IntermediateOutputPath` flatten to drop the `<Configuration>` segment, because this repo uses the
   non-standard `bin\<project>\<config>\<tfm>` layout and Stryker reconstructs `bin\<project>\<tfm>`
   (and `obj\…\ref\`). Run mutation testing with it set:
   ```bash
   StrykerCompat=true dotnet stryker   # from the test project dir
   ```
   Repos that use the **default** `bin/<config>/<tfm>` layout (e.g. IndFusion.Ember) need **only** setting #1.

## Cross-repo guideline (for restoring mutation testing org-wide)
Confirmed by diffing this repo against the known-working **IndFusion.Ember** setup (same Stryker 4.14.2,
same MTP `global.json`):

| Concern | Required | Notes |
|---|---|---|
| Stryker version | `dotnet-stryker` **4.x** (4.14.2 verified) | 4.x is the first line supporting xUnit v3 + MTP (~2026-03). Pin it in `.config/dotnet-tools.json`. |
| **Test runner** | **`"test-runner": "mtp"`** | MANDATORY for MTP/xunit.v3 projects. Omitting it = silent 0% via the VSTest bridge. The single most important setting. |
| Coverage analysis | `"perTest"` works once test-runner is `mtp` | With the VSTest default, coverage capture *fails* ("coverage capture failed"); with `mtp` it succeeds. |
| Config location | next to the **project under test** (Ember) or the **test project** (this repo) | Both work; set `project` + `test-projects` to disambiguate. |
| Output layout | default `bin/<config>/<tfm>` → nothing needed | Custom/redirected `OutputPath` (this repo) needs the `StrykerCompat` flatten above, or Stryker can't find the binaries / `ref` assemblies. |
| Strict config | no unknown keys (not even `_comment`) | Stryker validates `stryker-config.json` strictly and aborts on extras. |

## How to expand
Mutation runs are **expensive** (each mutant re-runs the suite), so scale deliberately:

1. **Widen within a project** — drop the `mutate` filter (or add globs) to cover the whole project, not
   just one file.
2. **Add more projects** — give each a `stryker-config.json` next to its test project. Best candidates are
   **deterministic, fast** suites: Domain, Application, Classification, Export(.Adaptive), Extraction(.Txt/
   .Adaptive/XML), Imaging. **Avoid** mutating code whose tests depend on live OCR (`Extraction.Teseract`,
   `System.Ocr.Pipeline` — flaky), Docker (`System.Storage`, `Infrastructure.Database`), Playwright
   (`Tests.UI`, `BrowserAutomation.E2E`), or the dormant Python/VLM paths — non-determinism and slowness
   make mutants ambiguous.
3. **CI:** run with `--since` (diff-based) so PRs only mutate changed files; reserve full runs for nightly.
   Set `thresholds.break` above 0 once a baseline is agreed to fail the build on regressions.

## Notes / gotchas
- `stryker-config.json` is **strictly validated** — unknown keys (even `_comment`) fail the run. Keep
  prose in this doc, not the config.
- Stryker auto-detects the project under test from the test project's references; `project` +
  `test-projects` in the config disambiguate when there are several.
- Output (`StrykerOutput/`) is build-artifact noise — it is git-ignored at the repo level; don't commit it.
