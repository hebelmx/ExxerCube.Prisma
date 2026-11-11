# ADR-013: ProcessingMetricsService Persistence — In-Memory Accepted for MVP

**Date**: 2026-06-19
**Status**: Accepted
**Deciders**: Owner + Development Team
**Tags**: metrics, infrastructure, mvp, observability, dashboard
**Related**: ADR-003 (Metrics Services Infrastructure Layer Placement), `Infrastructure.Metrics/ProcessingMetricsService.cs`

## Context

`ProcessingMetricsService` (`ExxerCube.Prisma.Infrastructure.Metrics`) is the sole
implementation of `IProcessingMetricsService`. It holds all in-flight and completed
document metrics in two purely in-memory structures:

- `ConcurrentDictionary<string, ProcessingMetrics> _documentMetrics` — one entry per
  completed document (keyed by document ID, accumulated for the process lifetime).
- `ConcurrentQueue<ProcessingEvent> _processingEvents` — ordered event stream used for
  windowed throughput and success-rate calculations.

There is **no write path to any database or external store.** `GetAllMetrics()` and
`GetRecentEvents()` return snapshots of these in-process collections. On service restart
both structures are reset to empty and all historical data is lost.

The Web.UI `/dashboard` page (`Components/Pages/Dashboard.razor`) injects
`IProcessingMetricsService` directly and reads from it live on each render cycle. This
makes the dashboard a **live operational view** of the current process run, not a
persistent audit surface.

**SLA-of-record is separate and DB-backed.** `SLAStatus` (domain entity) is persisted
via EF Core through `SLAStatusConfiguration` in `Infrastructure.Database`, enforced by
`SLAEnforcerService` (also DB-backed) with its own health checks and `SLAMetricsCollector`.
The `SlaDashboard.razor` page reads from that separate surface. The SLA tracking path is
independent of `IProcessingMetricsService` and does **not** depend on metrics history
surviving a restart.

No MVP acceptance criterion, SLA-of-record surface, or audit trail requirement was found to
depend on `ProcessingMetricsService` history surviving process restart.

## Decision

**Accept in-memory metrics as a known, documented limitation for MVP.**

Historical / trend metrics persistence — writing `_documentMetrics` or `_processingEvents`
to a database or time-series store — is **explicitly out of MVP scope** and is deferred to
a post-MVP observability hardening pass.

The single permitted code change under this ADR is a clarifying comment placed on the
in-memory field declarations in `ProcessingMetricsService.cs` (see Consequences below).

## Rationale

1. **Dashboard intent is live-operational, not audit.** The OCR Processing Dashboard shows
   the current run's throughput, success rate, and active document count. Operators restart
   the service intentionally (deployments, config changes); a blank dashboard on restart is
   the expected and correct state, not data loss.

2. **SLA-of-record is already DB-backed and independent.** `SLAStatus` rows, deadlines, and
   escalation state survive restarts through the EF Core / SQL Server path. Mixing
   operational counters into that store would add coupling with no MVP requirement behind it.

3. **Adding a persistence layer now is premature complexity.** It would require a new DB
   table (or time-series store), EF migration, background flush logic, bounded-retention
   policy, and a migration path for the in-memory aggregation timer — all before a single
   production run has validated which metrics are actually useful to retain.

4. **Accepted technical debt is explicitly named.** Documenting the limitation here is
   preferable to either leaving it undocumented or adding infrastructure that has no
   validated requirement.

## Consequences

- **Metrics history is lost on every restart.** Operators should not rely on the Web.UI
  dashboard for historical trend analysis during MVP. A restart clears all counts.
- **Acceptable for MVP** because the dashboard is a live operational view; SLA deadlines and
  escalation audit trails live in the DB and are unaffected.
- **Code comment added** at lines 13–14 of `ProcessingMetricsService.cs` (the
  `ConcurrentDictionary` / `ConcurrentQueue` field declarations):
  ```
  // MVP: in-memory only — metrics reset on restart. Accepted limitation, see ADR-013.
  ```
  This is the only code change permitted by this ADR (no behavioural change).
- **Revisit trigger:** when any of the following occur — (a) an operator or compliance
  requirement asks for metrics trend history or audit queries against processing counters;
  (b) a scheduled or SLA dashboard requires cross-restart aggregation; (c) an OpenTelemetry
  / Prometheus export target is wired up (at which point the external collector becomes the
  persistence layer and this ADR's concern is resolved automatically).
