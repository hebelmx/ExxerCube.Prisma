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
| S6 | Docs: backlog O7 closed; memory updated | PENDING |

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
