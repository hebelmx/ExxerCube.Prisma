# QUICKSTART — Stage & run Prisma + Veriqan for E2E

Fast path to bring both apps up and run the tests. Full reference:
[`docs/operations/STAGING-AND-E2E-GUIDE.md`](docs/operations/STAGING-AND-E2E-GUIDE.md).

Run everything from the repo root
(`/home/abel/ExxerProjects/IndFusion/ExxerCube.Prisma`).

## 1. Bring up the stacks

```bash
# Veriqan  (SQL 1434, worker 8080)
docker compose -p veriqan --env-file .env.veriqan -f docker-compose.veriqan.yml up --build -d

# Prisma   (Web UI 8085, workers 18081/8082/8083, SIARA 8084, Seq 15341, SQL 1435)
docker compose -p prisma -f docker-compose.dev.yml -f docker-compose.staging.override.yml up --build -d
```

> **Three things that must not be skipped:**
> 1. Distinct `-p veriqan` / `-p prisma` project names — otherwise both default to
>    the same name and bringing one up **deletes the other's containers/volumes**.
> 2. `--env-file .env.veriqan` on the Veriqan line — `env_file:` alone does not feed
>    Compose `${VAR}` interpolation, so the SA password resolves empty and SQL rejects login.
> 3. `docker-compose.staging.override.yml` on the Prisma line — remaps colliding host
>    ports (1433/5341/8081) via the Compose `!override` tag.

## 2. Verify

```bash
docker compose ls                     # expect: prisma running(7), veriqan running(2)
curl -s localhost:8085/               # Prisma Web UI      -> 200
curl -s localhost:18081/health        # Orion             -> Healthy
curl -s localhost:8082/health         # Athena            -> Healthy
curl -s localhost:8083/health         # Reconciliator     -> Healthy
curl -s localhost:8080/health/ready   # Veriqan worker    -> Healthy
```

Browser: Prisma UI <http://localhost:8085> · Seq logs <http://localhost:15341> · Veriqan worker <http://localhost:8080>

### Visible browser-automation demo (X11)

The `/browser-automation` page launches a **real Chromium window on your desktop**
via X11 forwarding (configured in `docker-compose.staging.override.yml`). On a Linux
host (X11 or Wayland+Xwayland), grant the container access to your X server **once**
before the demo:

```bash
xhost +local:        # demo-only: loosens local X access control; revoke later with `xhost -local:`
```

Then open <http://localhost:8085/browser-automation> and start automation — the
Chromium window appears on your desktop (record it with OBS/any screen recorder).
Without `xhost +local:` the launch fails with "cannot open display :0".

## 3. Run the E2E tests

The suites self-host (WebApplicationFactory + Testcontainers), so they spin up their
own dependencies — the compose stacks don't have to be running for these.

```bash
# Full multi-project E2E suite (MaxFidelityGate / AllRealWire / RealTcp / FailureMode / Soak)
dotnet test "Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E/ExxerCube.Prisma.Tests.AllRealWireE2E.csproj"

# Playwright UI E2E
dotnet test "Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.EndToEnd/ExxerCube.Prisma.Tests.EndToEnd.csproj"
```

## 4. Tear down

```bash
docker compose -p prisma  -f docker-compose.dev.yml -f docker-compose.staging.override.yml down
docker compose -p veriqan -f docker-compose.veriqan.yml down
```

Add `-v` to either `down` to also drop that stack's SQL data volume.
