# Veriqan Epic 6 — Story S6.6: Fail-Closed Gate Policy + Circuit Breaker + Entry Timeout

**Date:** 2026-06-30
**Branch:** Liv
**Story:** Epic 6 S6.6 — host→gate fail-closed policy + circuit breaker + entry timeout

---

## 1. Decision: Fail-Closed Policy

**Context:** `IVerificationPipeline.ProcessAsync` is the compliance gate that determines whether a credit-card statement is RED, YELLOW, or GREEN. The host (`/verify`, `/batch`) calls this gate for every submitted PDF.

**Problem:** Without resilience, an infra fault in the gate (DB timeout, unhandled exception, slow dependency) can cause the host to either throw a 500 or — if the pipeline were to silently swallow faults and return a stub — manufacture a false GREEN verdict.

**Decision: fail-closed.** The decorator `ResilientVerificationPipeline` wraps the inner gate with Polly v8 strategies. When any of the following occurs, the decorator MUST return a non-success `Result<VerificationOutcome>` and MUST NOT return a success or throw an exception:

| Condition | Decorator behaviour | HTTP mapping |
|-----------|---------------------|--------------|
| Inner pipeline exceeds timeout | `Result.WithFailure("Gate.Timeout: ...")` | 503 |
| Circuit breaker is open | `Result.WithFailure("Gate.CircuitOpen: ...")` | 503 |
| Inner throws unhandled exception | `Result.WithFailure("Gate.Error: ...")` | 503 |
| Outer `CancellationToken` cancelled | `ResultExtensions.Cancelled<T>()` | 499 (existing behaviour) |
| Inner returns success | Pass through unchanged | 202 Accepted |
| Inner returns business failure | Pass through unchanged | 422 Unprocessable Entity |

**Rationale:** "Never manufacture a verdict" (Epic 4 honesty taxonomy). A system-unavailability event is not a compliance verdict — it must be surfaced as an infra error (503) so callers know to retry or alert operations, rather than silently pass or fail the statement.

---

## 2. Design: Decorator Pattern

A decorator `ResilientVerificationPipeline : IVerificationPipeline` wraps the real `VerificationPipeline`:

```
[ResilientVerificationPipeline] (scoped)
   ↳ holds shared ResiliencePipeline<Result<VerificationOutcome>> (singleton)
   ↳ delegates to VerificationPipeline (scoped, concrete type)
```

**Why the Polly pipeline is a Singleton:** The circuit-breaker state (failure count, circuit status) must be shared across all HTTP requests. A per-request (scoped) Polly pipeline would reset the circuit on every request, making it useless. The singleton Polly pipeline holds the state; the scoped decorator holds the per-request inner pipeline.

**Why manual decoration (no Scrutor `.Decorate`):** `VerificationPipeline` is `internal sealed`. The extension method `VeriqanOrchestrationExtensions.AddVeriqan` is in the same assembly, so it can reference the concrete type directly via `sp.GetRequiredService<VerificationPipeline>()`. Adding Scrutor to the Orchestration project just for `.Decorate` is not needed.

---

## 3. Resilience Pipeline Strategies

**Order (Polly v8 — outermost to innermost):**

```
[Timeout] → [CircuitBreaker] → actual call to VerificationPipeline
```

Timeout is outermost so it caps the total wall-clock time including any delays the circuit breaker might introduce. The circuit breaker is inner, shielding the real call from hammering when in OPEN state.

### 3.1 Timeout Strategy

| Config key | Path | Default |
|------------|------|---------|
| `TimeoutPerRequest` | `Veriqan:Gate:Resilience:TimeoutPerRequest` | `00:00:30` (30 s) |

When the timeout fires, Polly cancels the inner `CancellationToken` (linked from the outer + Polly's own source). The inner pipeline respects cancellation and throws `OperationCanceledException`. Polly converts this to `TimeoutRejectedException`. The decorator catches `TimeoutRejectedException` and returns `"Gate.Timeout: ..."`.

### 3.2 Circuit Breaker Strategy

| Config key | Path | Default |
|------------|------|---------|
| `FailureRatio` | `Veriqan:Gate:Resilience:FailureRatio` | `0.8` (80 %) |
| `MinimumThroughput` | `Veriqan:Gate:Resilience:MinimumThroughput` | `5` |
| `SamplingDuration` | `Veriqan:Gate:Resilience:SamplingDuration` | `00:01:00` (60 s) |
| `BreakDuration` | `Veriqan:Gate:Resilience:BreakDuration` | `00:00:30` (30 s) |

**Trip condition:** within the sampling window, if at least `MinimumThroughput` calls occurred AND the failure ratio exceeds `FailureRatio`, the circuit opens for `BreakDuration`. During break, all new calls immediately return `Gate.CircuitOpen` without invoking the inner gate.

**`ShouldHandle` predicate (what counts as a failure):**
- Any exception, EXCEPT `OperationCanceledException` (user cancellation is not an infra fault).
- `Result.IsFailure` where `!IsCancelled()` — this catches infra failures the inner pipeline already absorbed into a `Result.WithFailure` (e.g., persist errors), preventing repeated hammering of a broken DB.

**Transition logging:** circuit state changes (OPENED / HALF-OPEN / CLOSED) are logged at ERROR/INFO level for Ops visibility.

---

## 4. HTTP 503 Mapping

In `Veriqan.Worker/Program.cs`, the `/verify` endpoint distinguishes the two classes of non-success result by the `Gate.` error prefix:

```
Gate.*  error → 503 Service Unavailable   (infra/gate fault)
other failure → 422 Unprocessable Entity  (business/extraction failure)
cancellation  → 499 Client Closed Request (pre-existing)
success       → 202 Accepted
```

The `/batch` endpoint processes items inside `BatchProcessor` scopes; each item's gate failure is recorded in the batch report. The batch endpoint itself returns 422 only when the batch submission itself fails (not per-item gate faults).

---

## 5. Configuration Example

`appsettings.json` (all optional — defaults are safe):

```json
{
  "Veriqan": {
    "Gate": {
      "Resilience": {
        "TimeoutPerRequest": "00:00:30",
        "FailureRatio": 0.8,
        "MinimumThroughput": 5,
        "SamplingDuration": "00:01:00",
        "BreakDuration": "00:00:30"
      }
    }
  }
}
```

---

## 6. Files Changed

| File | Change |
|------|--------|
| `03 Orchestration/Veriqan.Orchestration/Pipeline/GateResilienceOptions.cs` | **NEW** — options class, bound from `Veriqan:Gate:Resilience` |
| `03 Orchestration/Veriqan.Orchestration/Pipeline/ResilientVerificationPipeline.cs` | **NEW** — fail-closed decorator + static `BuildResiliencePipeline` factory |
| `03 Orchestration/Veriqan.Orchestration/ExxerCube.Prisma.Veriqan.Orchestration.csproj` | **MOD** — added `<PackageReference Include="Polly" />` |
| `03 Orchestration/Veriqan.Orchestration/DependencyInjection/VeriqanOrchestrationExtensions.cs` | **MOD** — registered options, singleton Polly pipeline, concrete inner, and scoped decorator |
| `04 Services/Veriqan.Worker/Program.cs` | **MOD** — `/verify` endpoint maps `Gate.*` → 503 |
| `08 Tests/.../ResilientVerificationPipelineTests.cs` | **NEW** — 6 tests (throw, timeout, circuit open, success pass-through, cancellation, Result failure predicate) |
| `docs/planning-artifacts/veriqan-epic6-s6.6-fail-closed-circuit-breaker.md` | **NEW** — this document |
