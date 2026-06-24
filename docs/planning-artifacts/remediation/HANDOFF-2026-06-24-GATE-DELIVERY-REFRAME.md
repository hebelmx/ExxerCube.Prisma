# Handoff — Max-Fidelity Gate: Delivery Reframe (2026-06-24)

**Branch:** `Liv` · **HEAD:** `9fdfcff2` (all pushed) · **Tree:** clean
**Owner steer this session:** "try again without Docker" → "fix findings #1 & #2" → "investigate the Athena OCR stall" → **"stop here + clear handoff."**

This session ran across three steers; this doc is the resume point. The detailed running log is the
top **Handoff** block of `EXECUTION-TRACKER.md` (sessions 2026-06-23b, 2026-06-23c, 2026-06-24).

---

## TL;DR

1. **Docker/WSL2 is HARD-DOWN** on the box (`Wsl/Service/CreateInstance/CreateVm/0x800705b4`) — the whole WSL2
   VM layer fails (even plain Ubuntu won't cold-start), caused by **VMware Workstation running a live VM that
   contends for the host hypervisor**. Per owner, VMware stays untouched; Docker is "under repair." All
   non-elevated self-heals failed; the owner's `Repair-Docker-WSL.ps1` is gone.

2. **Worked around Docker with a local-SQL gate path** (committed, reusable): set
   `PRISMA_GATE_LOCAL_SQL` to a *master* conn string for `DESKTOP-FB2ES22\SQL2025` (or `\SQL2022`) and the gate
   provisions/drops a GUID-unique isolated DB there instead of Testcontainers. Default/CI path unchanged.

3. **The gate's real blocker is NOT Athena OCR / CPU** (the prior framing was wrong). **Extraction never runs.**
   The `DocumentDownloadedEvent` is **never delivered** to Athena's ingestion handler on the **in-memory
   SignalR test seam**. Serialization is ruled out (committed round-trip guards pass). It is a delivery failure
   that the gate's ~3-min idle download window + heavy CPU contention make consistent.

4. **Two real production findings FIXED** (downloader per-file portal re-scrape; per-file browser leak).

---

## Commits this session (branch `Liv`)

| Commit | What |
|--------|------|
| `66fd8e20` | `test(gate)`: Docker-free local-SQL path in `MaxFidelityGateE2EBase` (env `PRISMA_GATE_LOCAL_SQL`). |
| `db6c5c35` | docs: record Docker-free local-SQL gate session. |
| `9cbeec30` | `fix(ingestion)`: **Finding #1** (skip full-portal re-scrape for absolute-URL ids) + **Finding #2** (balanced browser close per `DownloadAsync`). 4 TDD tests; 218/218; adversarially reviewed → no real issues. |
| `86bfedc6` | docs: record findings #1 & #2 fixed. |
| `3fd8a41c` | `test(ingestion)`: serialization guards (Domain `CaseFilesWireRoundTripTests` 2/2 + Orion `JsonHubProtocolCaseFilesTests` 1/1) — rule OUT serialization as the gate cause. |
| `9fdfcff2` | docs: reframe gate blocker (lost ingestion event, not OCR). |

---

## The reframe — evidence

- **Zero extraction-stage logs** in the full 833-line gate run (no "Stage 1: Quality Analysis" / "Stage 2: OCR"
  / "Stage 3: Fusion" / "Error extracting" — all Information level). `ExtractionPipelineService.ProcessAsync` →
  `ExtractAsync` **never ran**.
- **No delivery to Athena's handler.** Chain: Orion `SignalRIngestionBroadcaster.SendToAllAsync` →
  `Clients.All.SendAsync("ReceiveMessage", …)` (logs "Broadcast … to all ingestion subscribers") → Athena
  `SiaraIngestionHubClient.On<DocumentDownloadedEvent>("ReceiveMessage")` → `IngestionEventForwarder.ForwardAsync`
  (logs "Forwarding…" Info / "rejected…" Warning) → local publish → `ProcessAsync`. In the gate the broadcast
  logged, but **no "Forwarding"/"rejected"/"clearance" log ever** → the `.On` handler never fired. The Athena
  ingestion client dropped ~1s after the broadcast.
- **Serialization ruled out** (`3fd8a41c`): a `DocumentDownloadedEvent` with 3 companion `CaseFiles` (SmartEnum
  `Format`) round-trips cleanly through (a) pure System.Text.Json Web-defaults + `EnumModelJsonConverterFactory`
  and (b) the real SignalR `JsonHubProtocol` envelope. Both server (Orion `Program.cs:195`) and client
  (`SiaraIngestionHubClient:133`) register the converter.
- **It's a delivery failure on the in-memory SignalR seam.** A live wire test (real Orion host + 1 client over
  `server.CreateHandler()`) reproduced the drop **intermittently** — 1 of 2 immediate-broadcast runs lost the
  event; a 40s-idle-then-broadcast run delivered fine. The gate's **consistent** 3/3 failure aligns with its
  conditions an isolated test can't replicate: ~3-min idle download window between connect and broadcast +
  heavy CPU contention starving the transport pump. The fast harness (`AllRealWireThreeHostE2ETests` 29/29)
  passes because it broadcasts **immediately** (stubbed download → no idle).

**Likely a test-harness limitation, not a product defect** — real-TCP SignalR has keepalive, and the
OutboxRetryWorker / event-persistence add recovery. **Open product question:** does the real Orion→Athena edge
guarantee delivery if the connection blips during a quiet period?

---

## Next-step options for gate-green (owner to pick when resumed)

1. **Harden gate delivery** — re-assert/refresh the Athena ingestion hub connection (or emit a keepalive)
   right before Orion broadcasts, so the multi-minute download idle can't stale the connection. Most direct
   harness fix; pair with a deterministic regression test.
2. **Confirm the prod edge first** — verify whether the real Orion→Athena SignalR edge guarantees delivery on a
   connection blip (reconnect + outbox replay), to decide if this is a product concern at all.
3. **Real-TCP / non-contended box** — run the gate over a real TCP transport (not the in-memory seam) and/or on
   a box without the VMware CPU contention, where SignalR keepalive works; likely passes with no code change.
4. **Defer** (current choice) — investigation complete; fix deferred.

---

## Reusable assets created this session

- **Docker-free gate run:** `PRISMA_GATE_LOCAL_SQL="Server=DESKTOP-FB2ES22\SQL2025;Database=master;Integrated Security=True;TrustServerCertificate=True;Encrypt=False"`
  then `dotnet test … --filter-query "/*/*/MaxFidelityGateFullPipelineE2ETests/RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit"`.
- **Sim pre-warm (Docker-independent):** start `Siara.Simulator.exe` from `Deployments/Siara.Simulator/app`
  via `Start-Process -NoNewWindow` (survives across tool calls; `-WindowStyle Hidden` does NOT). Reset
  `app/cases.json` to `[]`. Keep the served set small: `SimulatorSettings__AverageArrivalsPerMinute=1.0` env.
  Authenticate via static-SSR form POST (GET `/login` → `__RequestVerificationToken`; POST
  `_handler=loginForm` + `_model.Username=BANAMEX` + `_model.Password=password123` + token) then GET `/` to
  fire `Dashboard.OnInitialized` → `CaseService.Start()` (the singleton timer then serves regardless of circuit).
- **Serialization guards** (`3fd8a41c`) — fast deterministic regression guards for the CaseFiles wire payload.

---

## Incidental real bug (logged, not fixed)

`AuditRecords.ErrorMessage` column truncates long error strings → the audit INSERT throws
`Microsoft.Data.SqlClient.SqlException "String or binary data would be truncated … column 'ErrorMessage'"`
(seen in the gate log when a browser-launch timeout error was being audited). **Fix:** widen the column
(e.g. to `nvarchar(max)` or a generous length) + migration.

---

## Still gated / unchanged

PRISMA-E3 security (biz/ops), PRISMA-GATED ingestion (legal/corpus), PRISMA-E4 PersonIdentityResolver
(owner ruled persisted-identity OUT of MVP), Veriqan-E3+ (issue #17). Docker remains owner-repair-gated.
