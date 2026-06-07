# Repository Cleanup — June 2026

**Branch**: `chore/repo-reorg`
**Date**: 2026-06-07
**Goal**: Remove accumulated clutter from a dormant period, organize loose files,
and bring the repo structure in line with the already-consolidated `docs/` tree.

This is the traceability record for the reorganization, so future readers don't
have to do "digital archaeology" to understand what moved and why.

## Repo identity (resolved during cleanup)

- **This repo (`ExxerCube.Prisma`) is canonical** — 474 commits, history through
  2026-02-18.
- **`Veriqan`** (`github.com/hebelmx/Veriqan`) is a **drifted clone** — a squashed
  Dec-2025 snapshot on `main` plus older branches (`kat`, `kat9`, `katX`). It is
  *not* a clean extraction; it holds the same in-tree copies + the same junk.
  Out of scope here — reconcile or archive it separately.

## Removed (history-preserving `git rm`; recoverable from git)

| Item | Reason |
|------|--------|
| `git-lfs-tmp/` (77 files) | Accidentally committed git-lfs 3.5.1 download |
| `pwd/` (287 tracked + leftovers) | Misplaced BMAD install (literal `pwd` folder). Reinstall via `npx bmad-method install` if needed |
| `ExxerAICode/` + `UnifiedHub/` (141 files) | SignalR/RealTimeCommunication — superseded by the published **`IndFusion.Ember`** NuGet package (see ADR-009). Unreferenced by Prisma |
| `all-files.txt`, `nul` | Stale file-listing dump / Windows redirect artifact |
| `COMMIT_MESSAGE.md`, `COMMIT_MESSAGE_SHORT.txt`, `fusion_methods_output.txt` | Ephemeral working files |
| `search_ollama*.ps1`, `search_filters_py.ps1`, `RunDockerHelloWord.ps1`, `commit*-changes.ps1` | One-off debug / duplicate scripts |
| `CSharp/ExxerCube.Prisma.sln.backup` + `.backup2` | Stale solution backups |
| `build-output.log` (root + CSharp) | Build log artifact (gitignored) |

## Moved

| From (repo root, unless noted) | To |
|------|-----|
| 17 planning/audit/handoff/session `.md` | `docs/development/{sessions,implementation,archive,guides}`, `docs/qa/{reviews,test-plans}`, `docs/planning/`, `docs/reference/` |
| 5 screenshots (`ExxercubeSln*.png`, `pdfviewer*.png`) | `docs/assets/screenshots/` |
| 5 JSON data dumps (`*_extracted.json`, `consolidated_entity_catalog.json`) | `docs/reference/data/` (unreferenced by code; preserved) |
| Process/docker/build/db/generator/extraction scripts | `scripts/{docker,build,db,generators,data-extraction}/` |
| `Siara.Simulator/` (self-contained, own `.sln`) | `tools/Siara.Simulator/` |
| `CSharp/PHASE2_*.md` status docs | `docs/development/archive/` |

## Kept at repo root (intentional)

`CLAUDE.md`, `AGENTS.md`, `.gitignore`, `.gitattributes`, `nuget.config`,
`package.json`, `package-lock.json`, `playwright.config.ts`,
`coverage.runsettings`, and `run-coverage.ps1` / `run-coverage-ci.ps1`
(root-coupled via `$PSScriptRoot` + CI-conventional; moving would break path
resolution).

## Known issues deferred (NOT done in this cleanup)

1. **Duplicate Python trees** — `Prisma/Code/Src/CSharp/Python/` is **build-wired**
   (CSnakes `SetPythonPathForCSnakes` MSBuild target points at
   `$(_ProjectRoot)\..\..\Python\ocr_modules`, and `<PythonRoot>Python/python</PythonRoot>`),
   so it must NOT be removed. It overlaps/diverges with
   `Prisma/Code/Src/Python/Prisma-dumy-generator-AAA/` (the document generator).
   Reconciling these needs a focused pass — do not mechanically dedupe.
2. **Stale DB scripts** — `scripts/db/rebuild_migrations.ps1` and `rebuild_all.sh`
   hardcode the old `F:\` drive and dash-style folder names (`02-Infrastructure`,
   `03-UI`) that no longer match the space-style structure. Fix paths before use.
3. **`Veriqan` drifted clone** — decide whether to reconcile, re-sync, or archive.
4. **`ExxerAICode/` + `UnifiedHub/` already removed** here; if Prisma ever needs
   real-time comm, reference the `IndFusion.Ember` NuGet package.
