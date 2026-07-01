# Staging & E2E Bring-Up Guide — Prisma + Veriqan

> **Purpose.** How to *stage* (build + run) the two deployable applications in this
> repo as live Docker stacks so that **end-to-end (E2E) quality testing** can begin.
> This is the missing "how do I stand it up" companion to the deeper operational
> runbooks (`PRISMA-PRODUCTION-RUNBOOK.md`, `VERIQAN-RUNBOOK.md`) and the E2E test
> material under `docs/qa/` and `docs/planning/gap-analysis/Stage_8_E2E_Implementation_Plan.md`.
>
> **Audience.** Whoever is standing up a shared dev/staging box to run the E2E suites.
>
> **Last verified:** 2026-07-01 (Docker 29.6, Compose v5) on Linux.

---

## 0. TL;DR

```bash
cd <repo-root>   # /home/abel/ExxerProjects/IndFusion/ExxerCube.Prisma

# One-time: create env files from templates and fill in secrets
cp .env.example .env                    # set SA_PASSWORD (strong)
cp .env.veriqan.example .env.veriqan    # set VERIQAN_SA_PASSWORD + VERIQAN_ENCRYPTION_KEY

# Stage Veriqan (2 containers) — ports 1434 (SQL) + 8080 (worker)
#   -p veriqan            : distinct Compose project (see §1c — MANDATORY)
#   --env-file .env.veriqan : Veriqan's vars live here, not the default .env (see §5a)
docker compose -p veriqan --env-file .env.veriqan -f docker-compose.veriqan.yml up --build -d

# Stage Prisma (8 containers) — staging override remaps colliding host ports
#   -p prisma             : distinct Compose project (see §1c — MANDATORY)
docker compose -p prisma -f docker-compose.dev.yml -f docker-compose.staging.override.yml up --build -d

# Smoke-check (see §4 / §5 for full checks)
curl -s http://localhost:8080/health/ready   # Veriqan worker
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

### 1b. Veriqan — CONDUSEF compliance verification (VEC)

| Service          | Role                                    | Container        | Host port |
|------------------|-----------------------------------------|------------------|-----------|
| `sqlserver`      | SQL Server 2022 (`VeriqanDb`)           | veriqan-sqlserver| **1434**  |
| `veriqan-worker` | VEC verification worker (ASP.NET host)  | veriqan-worker   | **8080**  |

The worker bind-mounts the reference bundle CSVs from
`./Prisma/Data/Veriqan/reference-bundles` (read-only) and falls back to
**in-memory persistence** if `ConnectionStrings:VeriqanDb` is left blank — so it
can run DB-less for a quick smoke.

**Veriqan Web.UI demo** (`07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI`) is a
self-contained, DB-less demo (uses `DemoDataService`). It has **no Dockerfile** —
run it directly (see §5c).

### 1c. ⚠️ Run each stack under a distinct Compose project name (`-p`)

**This is mandatory when both stacks run on the same box.** Neither compose file
sets a `name:`, so Docker Compose derives the project name from the working
directory — **both default to `exxercubeprisma`**. Two stacks sharing one project
name get merged: a `down`/`up`/recreate on one stack removes the *other* stack's
containers and volumes (observed: bringing Prisma up deleted Veriqan's SQL
container, flipping its readiness to `sqlConnectivity:false`).

Always pass an explicit, distinct project name:

```bash
docker compose -p prisma  -f docker-compose.dev.yml -f docker-compose.staging.override.yml ...
docker compose -p veriqan --env-file .env.veriqan  -f docker-compose.veriqan.yml ...
```

Verify isolation: `docker compose ls` should list **two** projects (`prisma`,
`veriqan`), not one merged `exxercubeprisma`. (The `container_name:` values —
`prisma-*`, `veriqan-*` — are already globally unique, so only networks/volumes/
project membership needed disambiguating.)

---

## 2. Prerequisites

- **Docker** ≥ 24 and the **Compose v2/v5** plugin (`docker compose`, not
  `docker-compose`). Verify: `docker version`, `docker compose version`.
- **Disk / RAM**: two SQL Server 2022 containers run simultaneously; budget
  ~4 GB RAM and a few GB of image layer space. The multi-stage .NET builds pull
  the SDK image on first build (slow first run, cached after).
- **Repo root** as working directory — every compose build context is the repo
  root so the Dockerfiles can `COPY` the CSharp source tree and root `nuget.config`.
- **`.NET 10 SDK`** only needed if you also run the E2E suites *from source*
  (§6) or the Veriqan Web.UI demo (§5c) on the host, rather than in containers.

---

## 3. Environment files (secrets)

Secrets live in gitignored `.env` files, injected into the containers at
compose-up time. **Never hardcode secrets in the compose YAML.**

| App     | File           | Template               | Required keys |
|---------|----------------|------------------------|---------------|
| Prisma  | `.env`         | `.env.example`         | `SA_PASSWORD` (strong: ≥8, mixed case + digit + symbol) |
| Veriqan | `.env.veriqan` | `.env.veriqan.example` | `VERIQAN_SA_PASSWORD`, `VERIQAN_ENCRYPTION_KEY` (Base64 32-byte AES key) |

```bash
cp .env.example .env
cp .env.veriqan.example .env.veriqan

# Generate the Veriqan AES-256 key:
openssl rand -base64 32     # paste into VERIQAN_ENCRYPTION_KEY
```

Optional Veriqan keys (`VERIQAN_SMTP_*`, `VERIQAN_ALERT_RECIPIENTS`) are only
exercised by RED-verdict alert email dispatch; leave the SMTP host as the
placeholder for E2E that does not assert on email.

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

## 5. Staging the Veriqan stack

### 5a. Bring it up

No collision override needed — the Veriqan SQL maps to host `1434` and the worker
to `8080`, both normally free.

```bash
docker compose -p veriqan --env-file .env.veriqan -f docker-compose.veriqan.yml up --build -d
```

> **Critical — the `--env-file .env.veriqan` flag is required.** The compose file
> declares `env_file: .env.veriqan` on the worker, but `env_file:` only injects
> variables into the *container's* environment; it does **not** feed Compose's
> `${VAR}` interpolation. Interpolation reads the shell or the file named by
> `--env-file` (default `.env`). Since `VERIQAN_SA_PASSWORD` lives only in
> `.env.veriqan`, omitting `--env-file` interpolates the SA password to **empty**,
> SQL Server rejects it as "password too short," and the container stays
> `unhealthy` (login error 18456). Prisma does not need this flag because its vars
> live in the default `.env`, which Compose auto-loads.

If SQL was previously started with a different/empty SA password, its data volume
retains that password (SA is only set on first init). Recreate the volume:

```bash
docker compose -p veriqan --env-file .env.veriqan -f docker-compose.veriqan.yml down -v
```

### 5b. Smoke checks

```bash
docker compose -p veriqan --env-file .env.veriqan -f docker-compose.veriqan.yml ps
curl -s http://localhost:8080/health/live    # {"status":"Healthy", ...}
curl -s http://localhost:8080/health/ready   # sqlConnectivity/csvReferenceDataRoot/toleranceCacheWarm all true
```

See `VERIQAN-RUNBOOK.md` §2 for submitting a monthly batch (the real functional
E2E path: POST a base64 PDF batch, then watch the exception queue and verdicts).

### 5c. Veriqan Web.UI demo (optional, DB-less)

```bash
dotnet run --project "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI"
# then browse the printed http://localhost:<port>/ — self-contained demo data
```

---

## 6. Running the E2E suites

There is no separate Veriqan E2E project; Veriqan's functional E2E is exercised
via the worker API (§5b + runbook §2). Prisma has a mature multi-project E2E
suite. Key projects (all under `Prisma/Code/Src/CSharp/08 Tests/`):

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

# Veriqan
docker compose -p veriqan --env-file .env.veriqan -f docker-compose.veriqan.yml down     # keep VeriqanDb data
docker compose -p veriqan --env-file .env.veriqan -f docker-compose.veriqan.yml down -v  # wipe it
```

---

## 8. Troubleshooting

| Symptom | Cause / fix |
|---------|-------------|
| `bind: address already in use` on 1433/5341/8081 | Another stack holds the base port. Use the staging override (§4a), or stop the other container. |
| `!override` reported as invalid / ports still doubled | Compose plugin too old for `!override`. Upgrade to Compose v2.24+/v5, or temporarily stop the conflicting container and use the base file only. |
| Worker exits with SQL error `4060` on first boot | The target DB did not exist before the Serilog MSSql sink initialised. `sqlserver-init` handles this for Prisma; for Veriqan the worker creates/migrates `VeriqanDb` — ensure `sqlserver` is `healthy` first. |
| SQL container unhealthy | Weak/empty `SA_PASSWORD` / `VERIQAN_SA_PASSWORD` (needs ≥8 chars, mixed case + digit + symbol), or slow first boot — healthcheck has a 30 s `start_period` + 12 retries. Check `docker logs prisma-sqlserver`. |
| Veriqan worker logs "using in-memory persistence" | `ConnectionStrings:VeriqanDb` blank. Expected for DB-less smoke; set it (compose already does) for persistent E2E. `VERIQAN_ENCRYPTION_KEY` is required once SQL persistence is on. |
| Build can't find nuget.config / source | You ran compose from a subdirectory. All build contexts are the **repo root** — run from there. |
| `NU1903 ... Microsoft.OpenApi 2.0.0 ... high severity` (build fails, warnings-as-errors) | `Microsoft.AspNetCore.OpenApi 10.0.8` transitively pulls the vulnerable `Microsoft.OpenApi 2.0.0` (advisory GHSA-v5pm-xwqc-g5wc, fixed in 2.7.5). Resolved by a transitive pin `Microsoft.OpenApi 2.7.5` in `Directory.Packages.props`. When new advisories land against transitive deps, pin the fixed version there (transitive pinning is enabled). |
| Veriqan SQL `unhealthy`, `resolved-length` of SA password is small/empty | Missing `--env-file .env.veriqan` — see §5a. |
| Bringing one stack up/down deletes the other stack's containers | Both stacks defaulted to the same Compose project name. Always pass distinct `-p prisma` / `-p veriqan` — see §1c. `docker compose ls` must show two projects. |
| Seq shows no logs | Workers send to `http://seq:80` inside the network; browse the host UI at `http://localhost:15341` (staging) / `http://localhost:5341` (base). |

---

## 9. Related docs

- `docs/operations/PRISMA-PRODUCTION-RUNBOOK.md` — startup sequence, migrations, restart, on-call.
- `docs/operations/VERIQAN-RUNBOOK.md` — batch submission, exception-queue triage, reference-bundle updates.
- `docs/planning/gap-analysis/Stage_8_E2E_Implementation_Plan.md` — E2E validation plan.
- `docs/qa/test-plans/docker-test-isolation-2026-06.md` — Testcontainers isolation for integration/E2E.
- `docs/qa/REPRODUCIBILITY-clone-build-test.md` — clone → build → test.
- `deployment/QUICK-DEPLOYMENT-GUIDE.md` / `deployment/DEPLOYMENT-CHECKLIST.md` — air-gapped production deploy.
</content>
</invoke>
