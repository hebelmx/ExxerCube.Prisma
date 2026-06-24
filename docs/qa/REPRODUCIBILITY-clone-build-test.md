# Reproducibility — clone, build, and run the tests (incl. the max-fidelity gate)

**Audience:** an engineer on a *fresh machine* who wants to `git clone` this repo and run the test
suites — from the fast unit tests up to the heavyweight **max-fidelity 3-process E2E gate**.

**Status:** written 2026-06-24 against branch `Liv` (HEAD around `54440dad`). Windows is the
validated platform; the SIARA simulator publish step is `win-x64`. Linux/macOS can run the
non-gate suites but the gate's simulator step assumes Windows.

> **Why this doc exists.** Most suites run with nothing but the .NET SDK. Three things gate the
> rest: **Docker** (Testcontainers SQL), **Playwright** (browser), and the **SIARA simulator +
> its generated corpus**. The max-fidelity gate needs *all* of them plus native Tesseract, on an
> **uncontended** box. This doc spells out each tier so a clean clone is reproducible.

---

## 0. TL;DR

```bash
# 1. Clone the working branch
git clone https://github.com/hebelmx/ExxerCube.Prisma.git
cd ExxerCube.Prisma
git checkout Liv

# 2. Restore + build the solution (~70 projects)
dotnet restore "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"
dotnet build   "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"

# 3. Run the fast suites (no Docker / no browser needed)
dotnet test "Prisma/Code/Src/CSharp/08 Tests/01 Core/Tests.Domain/ExxerCube.Prisma.Tests.Domain.csproj"
# …and the other unit/integration projects (see §4).

# 4. The max-fidelity gate is a separate, heavyweight run — see §6.
```

---

## 1. Prerequisites

| Requirement | Needed for | Notes |
|---|---|---|
| **.NET 10 SDK** (10.0.30x) | everything | `global.json` pins the **Microsoft.Testing.Platform** test runner; the .NET 10 SDK is required (VSTest path is not supported here). |
| Git | clone | Use a clean checkout; do **not** use cygwin git (mangles line endings). |
| **Docker** (Desktop/Engine) | Testcontainers SQL suites + the gate (default path) | Pulls `mcr.microsoft.com/mssql/server:2022-latest` and (for some suites) Ollama. **Optional** for the gate if you use a local SQL instance instead — see §6.2. |
| **SQL Server 2022+** *(alternative to Docker)* | the gate's `PRISMA_GATE_LOCAL_SQL` path | Ledger tables need SQL Server **2019 CU13+/2022+**. A local `DESKTOP\SQL2022`/`SQL2025` instance with Integrated Security works. |
| **Playwright + Chromium** | UI tests, browser-automation E2E, the gate | `playwright install chromium` (or the generated `playwright.ps1 install` under a built test project's output). |
| **Native Tesseract + tessdata** | real-OCR tests + the gate | Language data **`spa`** (primary) **+ `eng`** must be resolvable by the Athena worker (its working dir `./tessdata` or `TESSDATA_PREFIX`). |
| **Published SIARA simulator + corpus** | browser-automation E2E + the gate | See §6.3. The corpus is **gitignored generated data** and is **not** in a clean clone — you must regenerate/restore it. |

The build writes artifacts to the path configured in `Directory.Build.props`
(`…/BuildArtifacts/Prisma/`), **not** to per-project `bin/`. That is expected.

---

## 2. Clone and build

```bash
git clone https://github.com/hebelmx/ExxerCube.Prisma.git
cd ExxerCube.Prisma
git checkout Liv

dotnet restore "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"
dotnet build   "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"
```

A clean build is **0 warnings / 0 errors** (the repo is `TreatWarningsAsErrors=true`).

> **Tip (slow disk / contended CPU):** building the whole solution can be slow. To iterate, build
> a single project, e.g.
> `dotnet build "Prisma/Code/Src/CSharp/04 Services/Athena/Prisma.Athena.Worker/ExxerCube.Prisma.Athena.Worker.csproj"`.
> Do **not** run multiple builds in parallel against the shared `BuildArtifacts` output dir — they DLL-lock.

---

## 3. Test runner notes (read once)

- The repo uses **xUnit v3 + Microsoft.Testing.Platform (MTP)**. `global.json` opts `dotnet test`
  into MTP. **Do not pass `--nologo`** (unsupported by the MTP bridge — it errors).
- **Filter a single test (MTP query syntax):**
  `dotnet test <project.csproj> --filter-query "/Namespace/Class/Method"`.
- **Capture per-test results:** add `--report-trx` (writes a TRX you can inspect for failures).
- Logging in tests is real (Meziantou xUnit logging), so failing runs print useful diagnostics.

---

## 4. Run the fast suites (no Docker, no browser)

These need only the SDK. Representative green baselines (from prior runs):

```bash
dotnet test "Prisma/Code/Src/CSharp/08 Tests/01 Core/Tests.Domain/ExxerCube.Prisma.Tests.Domain.csproj"            # 337
dotnet test "Prisma/Code/Src/CSharp/08 Tests/01 Core/Tests.Application/ExxerCube.Prisma.Tests.Application.csproj"   # 157
dotnet test "Prisma/Code/Src/CSharp/08 Tests/04 Services/Orion/Prisma.Orion.Worker.Tests/…csproj"                  # 25/26*
dotnet test "Prisma/Code/Src/CSharp/08 Tests/04 Services/Athena/Prisma.Athena.Worker.Tests/…csproj"                # 25
dotnet test "Prisma/Code/Src/CSharp/08 Tests/04 Services/Reconciliator/Prisma.Reconciliator.Worker.Tests/…csproj"  # 8
# Real-OCR (needs native Tesseract + spa/eng tessdata):
dotnet test "Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/…/Extraction.Teseract/…csproj"                      # 159
```

\* The **one** known Orion failure (`IngestionHubWireTests`, a SignalR-timing test) is a
pre-existing, environment-gated flake — **not** a regression. Everything else is green.

The three **Worker.Tests** projects boot the *real* worker host via `WebApplicationFactory<Program>`,
so a green run there is your fast proof that the worker composition roots wire up correctly.

---

## 5. Docker-gated suites (Testcontainers SQL / Ollama)

With Docker running:

```bash
dotnet test "Prisma/Code/Src/CSharp/08 Tests/05 System/Tests.System.Storage/…csproj"            # SQL via Testcontainers
dotnet test "Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Tests.Infrastructure.Database/…csproj"
```

The SQL suites share **one** SQL container per assembly and give each write-heavy class its own
isolated database (`CREATE DATABASE`, dropped on disposal). First run is slow (image pull / model
download for Ollama-backed tests). No special flags are needed beyond a running Docker daemon.

---

## 6. The max-fidelity 3-process gate (the heavyweight E2E)

This is the capstone: one real SIARA case is driven through **Orion → Athena → Reconciliator**
with **nothing stubbed** (real browser download, real Tesseract OCR, real fusion/classification,
real SIRO XML export) and audit persisted to **real SQL**. As of 2026-06-24 the gate runs its
cross-process SignalR over a **real TCP (Kestrel) transport** (each worker is booted as a single
real Kestrel `IHost`), so SignalR keepalive/auto-reconnect run as in production.

**Project:** `Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E/`
**Two scenarios** (run **one per `dotnet test` invocation** — each boots its own SQL + Playwright +
3 hosts, ~6–20 min):

| Scenario | Filter query |
|---|---|
| Full pipeline | `/*/*/MaxFidelityGateFullPipelineE2ETests/RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit` |
| Partial case  | `/*/*/MaxFidelityGatePartialCaseE2ETests/PartialCase_MissingCompanionFile_StillProcessesBestEffort_AndFlagsIncomplete` |

> **Run it on an UNCONTENDED box.** Native Tesseract + Emgu + PDFium OCR/fusion on 3-page docs plus
> Testcontainers SQL plus Playwright is CPU-heavy. On a CPU-starved machine the per-stage budgets
> can be missed (false "timeout"). A dedicated/CI box or a quiet workstation is required for a
> trustworthy green.

### 6.1 SQL — Option A: Docker / Testcontainers (default)
Just have Docker running. The gate provisions an isolated DB on a throwaway SQL 2022 container.
Nothing to configure.

### 6.2 SQL — Option B: local SQL Server (Docker-free)
If Docker is unavailable, point the gate at a **local** SQL instance by exporting a *master*
connection string. The gate then creates/drops a GUID-unique isolated DB on it:

```powershell
# PowerShell
$env:PRISMA_GATE_LOCAL_SQL = "Server=YOURHOST\SQL2022;Database=master;Integrated Security=True;TrustServerCertificate=True"
```
```bash
# bash
export PRISMA_GATE_LOCAL_SQL='Server=YOURHOST\SQL2022;Database=master;Integrated Security=True;TrustServerCertificate=True'
```
When the variable is unset, the Testcontainers path (Option A) is used. The instance must be SQL
Server **2019 CU13+/2022+** (ledger-table support).

### 6.3 SIARA simulator + corpus (required by the gate)
The gate auto-starts the published simulator if it is not already running on
`http://localhost:5001`. You must provide the published exe **and** its document corpus.

1. **Publish the simulator** (`win-x64`, self-contained):
   ```bash
   dotnet publish "tools/Siara.Simulator/Siara.Simulator.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   ```
   Copy the publish output (`Siara.Simulator.exe` + its files) into:
   `Prisma/Deployments/Siara.Simulator/app/`
   (the gate's path resolver looks for `Deployments/Siara.Simulator/app/Siara.Simulator.exe`,
   walking up from the test output dir).

2. **Provide the corpus.** `Prisma/Deployments/Siara.Simulator/bulk_generated_documents_all_formats/`
   holds the curated PDF+DOCX+XML case bundles. It is **gitignored generated data** and is **absent
   in a clean clone** — the simulator will serve **0 cases** without it. Regenerate it with the
   document generator under `Prisma/PRP/PRP1/research/generators/AAAV2_refactored/`
   (`batch_generate.py`), or copy a known-good corpus into that folder.

3. **Reset the served-set** before a run so discovery isn't bloated by stale state:
   delete / empty `Prisma/Deployments/Siara.Simulator/app/cases.json` (set it to `[]`), or set
   `ResetCasesOnStartup: true` in the simulator config. Keep the simulator's arrival rate **low**
   so the served set stays small.

### 6.4 Playwright + Tesseract
- `playwright install chromium` (so the headless login + browser download work).
- Native **Tesseract** with **`spa` + `eng`** traineddata resolvable by the Athena worker
  (its working dir `./tessdata`, or set `TESSDATA_PREFIX`). See the gate's class doc in
  `MaxFidelityGateE2EBase.cs` for the exact environment expectations.

### 6.5 Run a gate scenario
```bash
# (Option B shown; for Option A just have Docker running and skip the env var)
export PRISMA_GATE_LOCAL_SQL='Server=YOURHOST\SQL2022;Database=master;Integrated Security=True;TrustServerCertificate=True'

dotnet test "Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E/ExxerCube.Prisma.Tests.AllRealWireE2E.csproj" \
  --filter-query "/*/*/MaxFidelityGateFullPipelineE2ETests/RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit" \
  --report-trx
```

**Expected green:** the case flows download → OCR → fusion → classify → export; a `*.siro.xml`
and a `*.datos-carga-oficio.xlsx` are written to shared storage; `FileId`/`CorrelationId` survive
all three processes; audit rows from ≥2 distinct process identities land in SQL, all carrying a
non-null `ProcessId`.

---

## 7. The audit ledger migration (production note)

`AuditRecords` is an **append-only SQL Server ledger table** guarded by the
`TR_ProtectCriticalSchema` database DDL trigger. Migration
`20260624120000_WidenAuditErrorMessageColumn` widens `ErrorMessage` to `nvarchar(4000)` by
**dropping the trigger → `ALTER COLUMN` → recreating the byte-identical trigger** (a varlen length
increase is a documented ledger-safe `ALTER COLUMN` — no table rebuild, no digest reset).

- **Tests/gate:** unaffected. They build the schema with EF `EnsureCreated`, which produces a plain
  (non-ledger) table at `nvarchar(4000)` and never installs the trigger.
- **Production:** the workers run migrations at boot (`PrismaDbMigrationRunner.MigrateAsync`).
  Per repo convention these migrations carry a **"DO NOT apply to live SQL without owner approval"**
  note — apply to a live ledger DB only with DBA/owner sign-off.

---

## 8. Troubleshooting / gotchas

| Symptom | Cause / fix |
|---|---|
| `dotnet test` errors about VSTest / runner | Missing/old SDK. Install **.NET 10**; `global.json` requires the MTP runner. |
| `--nologo` errors | Don't pass it (MTP bridge limitation). |
| `MissingMethodException` at *test run* time | xUnit v3 / MTP version skew — do a clean `restore`; the versions are pinned and coupled (see `CLAUDE.md` → Testing stack). |
| Gate: "no PDF link within 90s" / simulator serves 0 cases | Corpus missing (§6.3) or `cases.json` stale — restore the corpus and reset `cases.json`. |
| Gate: stage timeouts on a busy laptop | CPU contention — run on an uncontended/CI box (§6). |
| Gate SQL collapse under load (Docker) | Cold/throttled SQL container under concurrent load; prefer a warm container or local SQL (Option B), and an uncontended box. |
| Worker won't boot off-box (`Server=DESKTOP-…`) | Standalone app `appsettings.json` may hardcode a dev SQL host; the gate overrides connection config in-process, so this only affects running the app directly. |

---

## 9. What a clean reproduction proves (and what it can't)

- **Build 0/0** + the **unit/integration suites** + the **three Worker.Tests** (which boot the real
  hosts) prove the composition roots and wiring are intact on the target machine — these run with
  no Docker/browser.
- The **max-fidelity gate** is the only tier that proves the *full cross-process pipeline over real
  TCP SignalR* end-to-end. It requires the complete environment in §6 and an uncontended box; it is
  intentionally heavy and is the canonical "is the 3-process split actually green" signal.
