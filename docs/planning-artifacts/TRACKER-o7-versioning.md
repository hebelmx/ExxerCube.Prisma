# TRACKER — O7: Repo versioning scheme (MinVer) so EngineVersion provenance is real

**Origin:** `OPEN-BACKLOG-2026-07-25.md` O7 (new residual from W2-closeout adversarial review, F1 in
`TRACKER-veriqan-w2-closeout.md`). `JobVerdict.EngineVersion` / `Finding.EngineVersion` columns are
real but the stamped value is a constant `1.0.0.0` because no versioning scheme exists.

**Owner ruling (2026-07-28):** **MinVer** (git-tag-driven SemVer). Chosen over manual-base+SHA,
Nerdbank.GitVersioning, and manual-only.

**Branch:** `Liv`. Baseline HEAD: `4a933ce8`.

## Design facts (verified before delegation)

- `VerificationPipeline.cs:63` resolves `EngineVersion` from `Assembly.GetName().Version` — 4-part
  numeric only, can NEVER carry SemVer/metadata. Must switch to `AssemblyInformationalVersionAttribute`
  (with `MinVerBuildMetadata` = git SHA it becomes e.g. `1.0.1-alpha.0.N+<sha>`).
- MinVer sets `AssemblyVersion` to `MAJOR.0.0.0` by default → binary-identity unchanged (`1.0.0.0`).
- `.dockerignore` excludes `.git/` → MinVer is blind in Docker builds → each production Dockerfile
  needs `ARG APP_VERSION` → `ENV MinVerVersionOverride` plumbing; CI/build scripts pass
  `--build-arg APP_VERSION=$(git describe --tags --always)`.
- `TreatWarningsAsErrors` is compiler-only (no `MSBuildTreatWarningsAsErrors`) → MinVer's MINVER1001
  warning in git-less contexts does not break builds.
- No test pins `1.0.0.0`; provenance tests assert round-trip/non-null only.
- CI (`quality-gates.yml`) is DORMANT (O8) but must be kept forward-correct: checkout steps need
  `fetch-depth: 0` (tags), docker jobs need the build-arg.
- `JobVerdict.EngineVersion` column has a length cap (check `JobVerdictConfiguration`) — the new
  informational-version string must be guarded against it.

## Stories

| # | Story | Status |
|---|-------|--------|
| S1 | Baseline tag: repo ALREADY has `v1.4.0-rc.1` (2026-06-16) + `v1.3.0-production-grade`, both reachable from `Liv` HEAD — MinVer derives from `v1.4.0-rc.1` → `1.4.0-rc.1.<height>+<sha>`. Requires `MinVerTagPrefix=v`. (A fresh `v1.0.0` was briefly created then deleted as redundant/misleading.) | DONE |
| S2 | MinVer wired solution-wide: `Directory.Packages.props` pin (MinVer 7.0.0) + reference (PrivateAssets=all) + `MinVerTagPrefix=v` + `IncludeSourceRevisionInInformationalVersion=false` + git-failure-tolerant `MinVerBuildMetadata`=short-SHA target | DONE |
| S3 | `EngineVersionResolver` (new, testable) reads `AssemblyInformationalVersion` (fallback: assembly version → "unknown"); 50-char column-cap guard matching `HasMaxLength(50)` on JobVerdict+Finding configs; `VerificationPipeline.EngineVersion` delegates to it; 4 unit tests | DONE |
| S4 | Docker: `ARG APP_VERSION=0.0.0-docker` → `ENV MinVerVersionOverride` in the 6 production Dockerfiles; both compose files pass `${APP_VERSION:-0.0.0-compose}`; dormant CI: `fetch-depth: 0` ×7 checkouts + `--build-arg APP_VERSION=$(git describe --tags --always)` ×5 docker steps | DONE |
| S5 | Verification (orchestrator-run): build 0/0; Orchestration.Tests + Persistence.IntegrationTests green; stamped version proven | DONE |
| S6 | Docs: backlog O7 closed; memory updated | DONE |

## Verification log

(entries appended only after the orchestrator has run the check itself — never from subagent claims)

- 2026-07-28 orchestrator: `dotnet build` full solution → **0 warnings / 0 errors** with MinVer active.
- 2026-07-28 orchestrator: compiled `obj/.../ExxerCube.Prisma.Veriqan.Orchestration.AssemblyInfo.cs`
  carries `AssemblyInformationalVersionAttribute("1.4.0-rc.1.547+4a933ce8")` +
  `AssemblyFileVersionAttribute("1.4.0.0")` — discriminating (height+SHA), derived from existing
  `v1.4.0-rc.1` tag.
- 2026-07-28 orchestrator: `Veriqan.Orchestration.Tests` **280/280** (full suite incl. LiveOcr,
  incl. 4 new `EngineVersionResolverTests`); `Veriqan.Infrastructure.Persistence.IntegrationTests`
  **37/37** on live Testcontainers SQL (incl. both provenance round-trip tests).
- 2026-07-28 orchestrator: diffs read in full — scope exactly matches brief (12 modified + 2 new files).
- 2026-07-28 orchestrator: `a1a5f480` accidentally swept in 2 regenerated calibration side-effect
  files (my own test runs re-dirtied them post-dev-revert); reverted in `4d3b7f99`.

## Adversarial gate (2026-07-28, post-commit `a1a5f480`+`4d3b7f99`) — 2 independent reviewers

Correctness skeptic (ran the actual MinVer 7.0.0 binary against the shipped strings) + completeness
auditor (fresh `--no-incremental` build 0/0, every tracker claim re-verified file:line). In-repo
wiring (props/target/resolver/SDK-suffix suppression) survived all attacks; target name+timing and
MTP-coupling angles explicitly REFUTED-clean. Real findings, all in the container/CI story:

| F | Severity | Finding | Disposition |
|---|----------|---------|-------------|
| F1 | BLOCKER | CI `--build-arg APP_VERSION=$(git describe --tags --always)` emits `v`-prefixed string; MinVer does NOT apply `MinVerTagPrefix` to overrides → `MINVER1005`, exit 2, docker build dies (×5 CI steps). Latent only because CI is dormant (O8). | FIXED (gate-fix commit) |
| F2 | MAJOR | Nothing in the repo sets `APP_VERSION` (no `.env*`, no script, staging guide silent) → the one LIVE image-build path (compose per STAGING-AND-E2E-GUIDE) stamps constant `0.0.0-compose` — recreates the O7 defect. | FIXED (gate-fix commit) |
| F3 | Should-fix | Explicitly empty `--build-arg APP_VERSION=` → MinVer treats empty override as unset → silent constant `0.0.0-alpha.0`. | FIXED (gate-fix commit) |
| F4 | Note | MSBuild Exec captures stderr and `;`-joins lines; MinVer accepts garbage metadata. | FIXED (gate-fix commit) |
| F5 | Minor | Test-class doc overclaims "three resolution branches"; `"unknown"` branch untested (reachability disputed — empirical check delegated). | FIXED (gate-fix commit) |
| F6 | Note | `[..50]` truncation would sever `+sha` first — unreachable today (realistic max ~30 chars; canary test exists). | RESIDUAL — logged, not fixed |
| F7 | Note | `Disposition.EngineVersion` column exists but no production caller wires the resolver into it. | RESIDUAL — follow-up candidate |
| F8 | Note | QaHarness CLI report header derives from `Assembly.GetName().Version` → still `1.0.0` (pre-existing, unchanged by O7; QA tooling, not a shipped service). | RESIDUAL — no action |

## Gate-fix round (2026-07-28) — orchestrator-verified

- F1: all 5 CI docker steps derive `APP_VERSION` safely (strip `v`, `0.0.0-sha.<hex>` bare-SHA guard).
- F2: `APP_VERSION` documented in `.env.example` (placeholder `0.0.0-local`) + `.env.veriqan.example`;
  STAGING-AND-E2E-GUIDE exports the real derivation at TL;DR/§4b/§5a + new §3 callout.
- F3: `ENV MinVerVersionOverride=${APP_VERSION:-0.0.0-docker}` in all 6 Dockerfiles.
- F4: `MinVerBuildMetadata` condition also requires `^[0-9a-fA-F]{7,40}$`.
- F5: reviewer's null-Version claim empirically REFUTED (dynamic assembly synthesizes `0.0.0.0`);
  test-class doc corrected instead of adding an unreachable-branch test.
- Orchestrator verification: solution build 0/0; `quality-gates.yml` parses (python yaml);
  `EngineVersionResolverTests` 4/4 (MTP note: run from project dir, 4-segment filter `/*/*/Class/*`).
- Residuals F6–F8 stay open as Notes (see gate table); `TRACKER-veriqan-w2-closeout.md` cited by the
  backlog does not exist in-repo — backlog edit is the canonical O7 closure.
