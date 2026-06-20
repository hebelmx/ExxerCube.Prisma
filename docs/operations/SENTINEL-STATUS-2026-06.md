# Sentinel Service Status — 2026-06

**Traced:** 2026-06-20
**Branch:** Liv
**Classification: (b) — Partial scaffold: real domain logic, no host, no DI registration, IProcessRestarter unimplemented**

---

## 1. Classification

**Bucket (b): Real logic but not hosted / not registered / incomplete.**

The Sentinel Monitor library contains a functional in-memory heartbeat-failure detector
and a `SentinelService` monitoring loop, but it is not wired into any running process.
No `Program.cs`, no `IHostedService`, no DI registration exists anywhere in the solution
outside the library itself and its test project.

---

## 2. File Inventory

| File | Path | Notes |
|------|------|-------|
| `SentinelService.cs` | `04 Services/Sentinel/Prisma.Sentinel.Monitor/SentinelService.cs` | Core orchestrator: `CheckWorkersAsync`, `MonitorAsync` (polling loop), `CheckWorkersWithResultAsync` (Result<T>) |
| `HeartbeatMonitor.cs` | `…/HeartbeatMonitor.cs` | Implements `IHeartbeatMonitor`; in-memory `ConcurrentDictionary` of `WorkerHeartbeat`; detects missed-heartbeat threshold |
| `IHeartbeatMonitor.cs` | `…/IHeartbeatMonitor.cs` | Port: `RecordHeartbeatAsync`, `GetFailedWorkersAsync` |
| `IProcessRestarter.cs` | `…/IProcessRestarter.cs` | Port: `RestartAsync(workerId, workerName, ct) → bool` |
| `ISentinelConfiguration.cs` | `…/ISentinelConfiguration.cs` | `HeartbeatTimeout`, `MissedHeartbeatThreshold`, `CheckInterval` |
| `DefaultSentinelConfiguration.cs` | `…/DefaultSentinelConfiguration.cs` | Defaults: 30 s timeout, 3-miss threshold, 10 s check interval |
| `CheckWorkersResult.cs` | `…/CheckWorkersResult.cs` | Record: `WorkersChecked`, `WorkersRestarted`, `WorkersFailed`, `FailedWorkerIds` |
| `GlobalUsings.cs` | `…/GlobalUsings.cs` | `IndQuestResults`, `IndQuestResults.Operations` |
| `WorkerHeartbeat.cs` | `01 Core/Domain/Models/WorkerHeartbeat.cs` | Domain record referenced by Sentinel (defined in Domain, not in Sentinel lib) |

Test project: `08 Tests/04 Services/Sentinel/Prisma.Sentinel.Monitor.Tests/`
- `SentinelServiceTests.cs` — 10 tests covering `CheckWorkersAsync`, `MonitorAsync`, `CheckWorkersWithResultAsync`
- `HeartbeatMonitorTests.cs` — 6 tests covering heartbeat recording and failure detection

---

## 3. What Works (logic quality)

### HeartbeatMonitor (real, functional)
- `RecordHeartbeatAsync` (line 30): stores heartbeat in `ConcurrentDictionary<string, WorkerHeartbeat>`.
- `GetFailedWorkersAsync` (line 41): computes `missedHeartbeats = elapsed / timeout`; returns worker IDs where count >= `MissedHeartbeatThreshold`.
- Forgive-on-recovery: the dictionary is updated on each `RecordHeartbeatAsync`, so a fresh heartbeat clears the failure window automatically.

### SentinelService (real, functional)
- `MonitorAsync` (line 68): continuous polling loop with `Task.Delay(CheckInterval, ct)`, graceful `OperationCanceledException` catch, error resilience (logs and continues on non-cancellation exceptions).
- `CheckWorkersAsync` (line 42): iterates failed workers, calls `IProcessRestarter.RestartAsync`, logs results.
- `CheckWorkersWithResultAsync` (line 105): Railway-Oriented Programming (Result<T>) variant; returns `CheckWorkersResult` with per-run statistics; handles cancellation with `ResultExtensions.Cancelled<T>()`.

### Configuration
- `DefaultSentinelConfiguration` (internal): 30 s heartbeat timeout, 3-miss threshold, 10 s check interval. Sensible production defaults.

### Tests: 16/16 green
All 10 `SentinelServiceTests` + 6 `HeartbeatMonitorTests` pass. Coverage spans:
- No failures → no restarts
- Failed workers → restarts triggered
- Multiple workers → all restarted
- Restart failure → logged, continues
- `MonitorAsync` cancellation → graceful stop
- `MonitorAsync` interval → at least 2+ invocations in time window
- ROP: zero stats, cancellation, all-succeed, partial-fail

---

## 4. What Is Missing

### (a) No concrete `IProcessRestarter` implementation
`IProcessRestarter` (`IProcessRestarter.cs:15`) defines `RestartAsync(workerId, workerName, ct) → bool`.
**No concrete class implements this interface anywhere in the solution.** All test usage is via
`NSubstitute` mocks. Without a real implementation, the `SentinelService` cannot actually
restart workers. This is the single most critical functional gap.

### (b) No host / no DI registration
`SentinelService` and `HeartbeatMonitor` are never registered in any DI container:
- Grep for `SentinelService` across `Prisma/Code/Src/CSharp`: matches only the source file and
  its test file — zero hits in Athena, Orion, UI, or any composition root.
- Grep for `AddSentinel`: zero hits.
- Grep for `IHeartbeatMonitor` registration: zero hits outside the Sentinel library itself.
- No `Program.cs` in `04 Services/Sentinel/` — the library has no runnable entry point.

### (c) No heartbeat senders
The `WorkerHeartbeat` domain record (`Domain/Models/WorkerHeartbeat.cs`) is defined with full
documentation ("emitted periodically by workers"), but Orion and Athena workers do not call
`IHeartbeatMonitor.RecordHeartbeatAsync` anywhere. There is no mechanism for workers to
announce themselves to Sentinel.

### (d) Process restart mechanism undefined
What "restarting" a managed .NET worker process means in this architecture is not yet decided.
Options include: `IndFusion.Ember` signalling, hosted-service `IHostApplicationLifetime.StopApplication()`,
external process supervision (systemd/Windows Services), or Ember's Three Actors coordination.
This decision is required before `IProcessRestarter` can be implemented.

---

## 5. Non-issues / Design Intent Confirmed

- **`IProcessRestarter` is deliberately abstract** — it models a port whose adapter depends on the
  deployment topology. This is correct Hexagonal Architecture; the gap is the missing adapter, not the design.
- **In-memory `ConcurrentDictionary`** for heartbeats is acceptable for MVP scope: workers and
  Sentinel share a process, or Sentinel is lightweight enough that in-memory suffices.
  Distributed/Redis-backed state is a future concern.
- **`DefaultSentinelConfiguration` is `internal sealed`** — correct; external callers use the
  `ISentinelConfiguration` port.

---

## 6. MVP-Scope Recommendation

**Recommendation: OUT OF MVP (deferred to post-MVP operational hardening track)**

### Rationale

1. **Functional but un-wired:** Sentinel has real domain logic that works correctly in isolation,
   but zero integration with the running system. No worker sends heartbeats, no host runs the
   monitor loop, and `IProcessRestarter` has no implementation.

2. **Blocking dependency not yet resolved:** The Three Actors / Ember coordination (ADR-009) for
   Orion's headless ingestion chain is itself not yet complete. Sentinel's restart mechanism
   depends on the same decision about process topology.

3. **MVP is already gated on Workstream 1** (Orion headless + 3-process split). Adding Sentinel
   wiring before those actors exist means building a watchdog for processes that are not yet
   running end-to-end. Risk: wiring effort becomes moot if the process model changes.

4. **Risk profile is low:** Orion and Athena have `/health/live` and `/health/ready` probes.
   For MVP-level operation (demo + controlled pilot), manual restart is acceptable. Sentinel's
   value multiplies with autonomous 24/7 operation.

### Suggested post-MVP path

When Workstream 1 (3-process split: Downloader / Extractor / Reconciliator via Ember) is
stable and running in staging:

1. Implement `IProcessRestarter` as an adapter over `IndFusion.Ember` restart signals (or
   `IHostApplicationLifetime` per process).
2. Add heartbeat emission to Orion and Athena workers (call `IHeartbeatMonitor.RecordHeartbeatAsync`
   from their main processing loops or `IHostedService.ExecuteAsync`).
3. Wire Sentinel into a minimal host: either as an `IHostedService` inside Athena's host
   (side-car model) or as a standalone worker process with its own `Program.cs`.
4. Add health endpoint (`/health/sentinel`) if hosted standalone.

No changes to the existing Sentinel library code are needed for this path — the domain logic
is ready; only the adapter and host are missing.

---

## 7. Build & Test Evidence

```
dotnet build ExxerCube.Prisma.Sentinel.Monitor.csproj
  Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test ExxerCube.Prisma.Sentinel.Monitor.Tests.csproj
  total: 16   failed: 0   succeeded: 16   skipped: 0
```

---

## 8. Related Documents

- `docs/architecture/adr/ADR-009-*` — Three Actors / Ember process coordination (Orion)
- `docs/planning/gap-analysis/GAP-MATRIX-2026-06-11.md` — MVP gap matrix
- `CLAUDE.md` — services table (Sentinel row updated to reference this doc)
