# Staging & E2E Bring-Up Guide — Prisma

> **Purpose.** How to *stage* (build + run) Prisma as a live Docker stack so that **end-to-end (E2E) quality testing** can begin.
> This is the missing "how do I stand it up" companion to the deeper operational
> runbook (`PRISMA-PRODUCTION-RUNBOOK.md`) and the E2E test projects.
>
> **Audience.** Whoever is standing up a shared dev/staging box to run the E2E suites.
>
> **Last verified:** 2026-07-01 (Docker 29.6, Compose v5) on Linux.

---

## 0. TL;DR

```bash
cd <repo-root>

# One-time: create env files from templates and fill in secrets
cp .env.example .env                    # set SA_PASSWORD (strong)

# O7: stamp a real, git-traceable MinVer version into the images instead of the
# .env template's 0.0.0-local placeholder (see §3 and .env.example's APP_VERSION
# comment for why this is needed — MinVer can't see .git/ inside the build).
APP_VERSION=$(git describe --tags --always)
APP_VERSION=${APP_VERSION#v}
case "$APP_VERSION" in *.*) ;; *) APP_VERSION="0.0.0-sha.$APP_VERSION";; esac
export APP_VERSION


# Stage Prisma (8 containers) — staging override remaps colliding host ports
#   -p prisma             : distinct Compose project (see §1c — MANDATORY)
docker compose -p prisma -f docker-compose.dev.yml -f docker-compose.staging.override.yml up --build -d

# Smoke-check (see §4 for full checks)
curl -s http://localhost:8085/health          # Prisma Web UI
```

Then run the E2E suites — see **§6**.

---

## 1. The two applications

Both apps share one solution
(`Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln`) and one source tree, but they
deploy as **two independent Docker stacks**.

### 1a. Prisma — OCR document-processing pipeline

The security-mandated **three-process split** plus UI and dependencies:

| Service          | Role                                    | Container         | Host port (staging) | Base port |
|------------------|-----------------------------------------|-------------------|---------------------|-----------|
| `sqlserver`      | SQL Server 2022 (Prisma + PrismaID DBs) | prisma-sqlserver  | **1435**            | 1433      |
| `sqlserver-init` | one-shot: creates the two empty DBs     | prisma-sqlserver-init | —               | —         |
| `seq`            | structured-log UI + ingestion           | prisma-seq        | **15341** / 15342   | 5341/5342 |
| `siara-simulator`| SIARA portal mock (ingestion target)    | prisma-siara-simulator | 8084           | 8084      |
| `orion`          | ingestion worker (downloader)           | prisma-orion      | **18081**           | 8081      |
| `athena`         | processing worker (OCR→fusion→export)   | prisma-athena     | 8082                | 8082      |
| `reconciliator`  | reconciliation worker (3rd actor)       | prisma-reconciliator | 8083            | 8083      |
| `web-ui`         | Blazor Server + MudBlazor UI            | prisma-webui      | 8085                | 8085      |

Data path: Orion writes documents to a shared named volume `/data/documents`;
Athena / Reconciliator / Web UI read from the same path. Athena hosts a
SignalR reconciliation hub that Reconciliator connects to (Ember, ADR-009).

### 1c. ⚠️ Run each stack under a distinct Compose project name (`-p`)

**Always name the project explicitly.** The compose files do not
set a `name:`, so Docker Compose derives the project name from the working
directory (`exxercubeprisma`). Any other stack started from the same folder would share
that name and a `down`/`up` on one would remove the other's containers and volumes.

Always pass an explicit project name:

```bash
docker compose -p prisma  -f docker-compose.dev.yml -f docker-compose.staging.override.yml ...
```

Verify: `docker compose ls` lists a `prisma` project.

---

## 2. Prerequisites

- **Docker** ≥ 24 and the **Compose v2/v5** plugin (`docker compose`, not
  `docker-compose`). Verify: `docker version`, `docker compose version`.
- **Disk / RAM**: SQL Server 2022 plus the workers; budget
  ~4 GB RAM and a few GB of image layer space. The multi-stage .NET builds pull
  the SDK image on first build (slow first run, cached after).
- **Repo root** as working directory — every compose build context is the repo
  root so the Dockerfiles can `COPY` the CSharp source tree and root `nuget.config`.
- **`.NET 10 SDK`** only needed if you also run the E2E suites *from source*
  (§6) on the host, rather than in containers.

---

## 3. Environment files (secrets)

Secrets live in gitignored `.env` files, injected into the containers at
compose-up time. **Never hardcode secrets in the compose YAML.**

| App     | File           | Template               | Required keys |
|---------|----------------|------------------------|---------------|
| Prisma  | `.env`         | `.env.example`         | `SA_PASSWORD` (strong: ≥8, mixed case + digit + symbol) |

```bash
cp .env.example .env

```


> **`APP_VERSION` (O7):** the template also declares `APP_VERSION=0.0.0-local` — a
> build ARG that stamps MinVer's version into every image (`ENV
> MinVerVersionOverride` in each Dockerfile). The templated value is a harmless
> ad-hoc placeholder; §4b below exports a real, git-derived value right
> before `up --build` so staged images carry a traceable provenance stamp instead
> of the placeholder. Without that export, every image just bakes
> `APP_VERSION` from your `.env` copy — silently
> reproducing the exact "constant `EngineVersion`" defect O7 fixed.

> On this box both `.env` files already exist and are populated (created 2026-06-26).

---

## 4. Staging the Prisma stack

### 4a. Host-port collisions — why the override exists

On a shared box, other stacks frequently already hold the base host ports
`1433` (SQL), `5341` (Seq) and `8081` (Orion). The base `docker-compose.dev.yml`
publishes exactly those. To avoid disturbing other running containers,
**`docker-compose.staging.override.yml`** remaps only the *published host* ports:

| Service   | Base host port | Staging host port |
|-----------|----------------|-------------------|
| sqlserver | 1433           | **1435**          |
| seq       | 5341 / 5342    | **15341 / 15342** |
| orion     | 8081           | **18081**         |

The override uses the Compose **`!override`** tag on each `ports:` list. This is
important: without it, Compose **appends** list entries when merging files, so the
colliding base ports would *still* be published. Internal, service-to-service
wiring is untouched — containers reach each other by service name on container
ports (`sqlserver:1433`, `seq:80`, `athena:8080`), which the host remap does not
affect.

> If your box has *no* collisions, you can skip the override and just run
> `docker compose -f docker-compose.dev.yml up --build -d` (ports 1433/5341/8081).

### 4b. Bring it up

```bash
# O7: derive a real MinVer version for the images (see §3 / TL;DR) — optional,
# but without it every image bakes the .env template's 0.0.0-local placeholder.
APP_VERSION=$(git describe --tags --always)
APP_VERSION=${APP_VERSION#v}
case "$APP_VERSION" in *.*) ;; *) APP_VERSION="0.0.0-sha.$APP_VERSION";; esac
export APP_VERSION

docker compose -p prisma -f docker-compose.dev.yml -f docker-compose.staging.override.yml up --build -d
```

Boot order is enforced by healthchecks/`depends_on`:
`sqlserver` (healthy) → `sqlserver-init` (creates `Prisma` + `PrismaID`, exits 0)
→ `seq`/`siara-simulator` → `orion`/`athena` → `reconciliator`, `web-ui`.
Workers run their EF Core migrations at boot inside the freshly created DBs.

### 4c. Smoke checks

```bash
docker compose -p prisma -f docker-compose.dev.yml -f docker-compose.staging.override.yml ps

# Worker liveness/readiness (all workers expose /health, /health/live, /health/ready)
curl -s http://localhost:18081/health   # Orion
curl -s http://localhost:8082/health    # Athena
curl -s http://localhost:8083/health    # Reconciliator

# Web UI: home page + health
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8085/         # expect 200
curl -s http://localhost:8085/health                                     # expect Healthy

# SIARA simulator + Seq UI
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8084/
open http://localhost:15341    # Seq log UI (or browse manually)
```

Expected: each `/health*` returns HTTP 200 with `"status":"Healthy"`.

---

## 6. Running the E2E suites

Prisma has a mature multi-project E2E suite. Key projects (all under `Prisma/Code/Src/CSharp/08 Tests/`):

| Project | What it proves |
|---------|----------------|
| `06 E2E/Tests.AllRealWireE2E` | MVP gate: `MaxFidelityGateFullPipelineE2ETests` (single live full run), `MaxFidelityGatePartialCaseE2ETests`, `AllRealWireThreeHostE2ETests`, `RealTcpThreeProcessE2ETests` (spawns real worker child processes over TCP), `FailureModeE2ETests` (chaos), `SoakE2ETests`, `DemoChecklistSevenStepsTests`, `OficioSummaryDemoSamplesTests`. |
| `06 E2E/Tests.EndToEnd` | Playwright UI E2E + metadata-extraction integration/perf + DI container tests. |
| `05 System/Tests.Infrastructure.BrowserAutomation.E2E` | Browser-automation system E2E: OCR/XML extraction, SIARA downloader + simulator, Playwright session context. |

Most of these are **self-hosting** (they spin up their own hosts /
`WebApplicationFactory` / child processes and their own Testcontainers SQL), so
they do **not** require the compose stacks above to be running — the staged
stacks are for *manual / exploratory / smoke* E2E and for pointing external tools
(browser, curl, Playwright) at a live system.

```bash
SLN="Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"

# Full-pipeline MVP gate (live, heavy)
dotnet test "Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E/ExxerCube.Prisma.Tests.AllRealWireE2E.csproj"

# Playwright UI E2E
dotnet test "Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.EndToEnd/ExxerCube.Prisma.Tests.EndToEnd.csproj"

# Browser-automation system E2E
dotnet test "Prisma/Code/Src/CSharp/08 Tests/05 System/Tests.Infrastructure.BrowserAutomation.E2E/ExxerCube.Prisma.Tests.System.BrowserAutomation.E2E.csproj"

# Single test (xUnit v3 filter-query syntax)
dotnet test <proj.csproj> --filter-query "/Namespace/ClassName/MethodName"
```

Playwright browsers: `npx playwright install` (config: root `playwright.config.ts`).
Testcontainers-based suites need Docker available (they provision their own SQL /
Ollama containers, isolated per class — see `docs/qa/test-plans/docker-test-isolation-2026-06.md`).

---

## 7. Teardown

```bash
# Prisma (keep volumes)
docker compose -p prisma -f docker-compose.dev.yml -f docker-compose.staging.override.yml down
# Prisma (also drop SQL/Seq/document volumes for a clean slate)
docker compose -p prisma -f docker-compose.dev.yml -f docker-compose.staging.override.yml down -v

```

---

## 8. Troubleshooting

| Symptom | Cause / fix |
|---------|-------------|
| `bind: address already in use` on 1433/5341/8081 | Another stack holds the base port. Use the staging override (§4a), or stop the other container. |
| `!override` reported as invalid / ports still doubled | Compose plugin too old for `!override`. Upgrade to Compose v2.24+/v5, or temporarily stop the conflicting container and use the base file only. |
| Build can't find nuget.config / source | You ran compose from a subdirectory. All build contexts are the **repo root** — run from there. |
| `NU1903 ... Microsoft.OpenApi 2.0.0 ... high severity` (build fails, warnings-as-errors) | `Microsoft.AspNetCore.OpenApi 10.0.8` transitively pulls the vulnerable `Microsoft.OpenApi 2.0.0` (advisory GHSA-v5pm-xwqc-g5wc, fixed in 2.7.5). Resolved by a transitive pin `Microsoft.OpenApi 2.7.5` in `Directory.Packages.props`. When new advisories land against transitive deps, pin the fixed version there (transitive pinning is enabled). |
| Seq shows no logs | Workers send to `http://seq:80` inside the network; browse the host UI at `http://localhost:15341` (staging) / `http://localhost:5341` (base). |

---

## 9. Related docs

- `docs/operations/PRISMA-PRODUCTION-RUNBOOK.md` — startup sequence, migrations, restart, on-call.
- `docs/planning/gap-analysis/Stage_8_E2E_Implementation_Plan.md` — E2E validation plan.
- `docs/qa/test-plans/docker-test-isolation-2026-06.md` — Testcontainers isolation for integration/E2E.
- `docs/qa/REPRODUCIBILITY-clone-build-test.md` — clone → build → test.
- `deployment/QUICK-DEPLOYMENT-GUIDE.md` / `deployment/DEPLOYMENT-CHECKLIST.md` — air-gapped production deploy.
</content>
</invoke>
