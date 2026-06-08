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

### Pilot result (2026-06-07b) — ⚠️ wired & runs, but score detection BLOCKED

The toolchain runs **end-to-end**: analyze → build → discover **35 tests** → initial test run (passes) →
generate **257 mutants** → produce an HTML report. This proves the **xunit.v3.mtp-v2 + MTP build/discovery
integration works**. Getting here required two repo-specific accommodations (guarded, off by default):

- `Directory.Build.props`: when `StrykerCompat=true` (env), flatten `OutputPath` **and**
  `IntermediateOutputPath` to drop the `<Configuration>` segment, because this repo uses the non-standard
  `bin\<project>\<config>\<tfm>` layout and Stryker reconstructs `bin\<project>\<tfm>` (and `obj\…\ref\`).
  Run mutation testing with that env var set: `StrykerCompat=true dotnet stryker`.

**BUT the mutation score comes back 0.00 % (Killed 0 / Survived 257), even with `coverage-analysis: off`.**
A uniform 0% with a suite we know is meaningful (35 real assertions) is **not** a "tests are worthless"
result — it means **Stryker is not observing test kills per mutant** on this stack. Evidence:
`[ERR] It looks like the test coverage capture failed. Disable coverage based optimisation.` and kills stay
0 even after disabling coverage. Leading hypothesis: under the MTP runner + the custom artifacts layout, the
test host loads the **original (un-instrumented) assembly**, so no mutant is ever active.

**Status: capability is scaffolded and proven to build/discover/run, but does NOT yet produce a trustworthy
mutation score.** Do not gate anything on it yet (`thresholds.break` stays 0).

**Next steps to make the score real (focused follow-up):**
1. Confirm the instrumented-assembly load path — check Stryker's sandbox vs. the flattened BuildArtifacts
   output; the custom layout may misdirect which `Infrastructure.Extraction.Txt.dll` the MTP host loads.
2. Try a temporary **conventional output layout** for the two projects (default `bin/Debug/net10.0`,
   no BuildArtifacts redirect) to isolate whether the custom layout is the cause.
3. Try the **VSTest bridge** path (`test-runner`) instead of the native MTP runner, or a newer Stryker.
4. If unresolved, file/track upstream (Stryker + MTP + SDK custom `OutputPath`).

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
