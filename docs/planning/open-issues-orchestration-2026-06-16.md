# Open-issues orchestration tracker — 2026-06-16

Branch: `Kt2`. Orchestrator-driven pass over the 3 open GitHub issues (#15, #16, #2).
Scope ruling (owner, 2026-06-16): **Everything feasible** — incl. #2.4 as a design proposal first.

## Intended solutions (do not drift from these)

| # | Task | Source | Status |
|---|------|--------|--------|
| 1 | **#15** SIRO XML → shared storage + content assertion in gate | Issue #15 (= Issue #2 item #6, partly #3) | pending |
| 2 | **#2.1 + #2.2** forwarder hardening: jti replay cache + reject `file_id==Guid.Empty` | Issue #2 items 1,2 | pending |
| 3 | **#16** split MaxFidelity gate into 2 isolated scenario classes; bound waits; document per-scenario CI | Issue #16 | blocked by 1 |
| 4 | **#2.4** asymmetric per-process signing keys — **design proposal + checkpoint** (no impl yet) | Issue #2 item 4 | pending |

### Blocked / deferred (not in this pass)
- **#2.3 SIRO XSD validation** — blocked: needs Banamex's official `.xsd`; exporter hook (`XmlSchemaSet?`) already exists. Drop the `.xsd` under `Prisma/Fixtures/schemas/` + register as singleton to activate (zero exporter code change).
- **#2.5 Kestrel-on-dynamic-ports E2E variant** — stretch / larger infra; deferred.

## Key facts grounded from code (2026-06-16)

- **#15 pattern to mirror:** `ReconciliationOrchestrator.cs:388-417` (`ExecuteStage5DatosCargaAsync`) — fail-open `IStoragePathResolver.Resolve` → `Directory.CreateDirectory` → `FileStream` write. The SIRO method (`ExecuteStage5…`, ~cs:306-356) currently exports to a `MemoryStream`, records size, discards. `_storagePathResolver` field already injected.
- **#2.1/#2.2:** `ClearanceTokenClaims` has no `Jti` yet; `JwtProcessClearanceTokenService.ValidateAsync` (cs:163-201) extracts sub/actor_type/clearance/file_id but NOT jti. Both forwarders (`IngestionEventForwarder.cs:113`, `ReconciliationEventForwarder.cs:99`) do `claims.FileId != event.FileId`. Forwarders live in different processes → per-process (per-forwarder) replay cache is correct.
- **#16:** gate is one class `MaxFidelityGateE2ETests` (`08 Tests/06 E2E/Tests.AllRealWireE2E/`) with 2 `[Fact(Timeout=900_000)]`. It news its OWN `SqlServerContainerFixture` per class instance → splitting into 2 classes already gives full SQL/host isolation. Shared helpers → abstract base.

## Verification reality
- Build + deterministic unit tests (forwarder/token/Athena.Processing) are verified here.
- The heavy MaxFidelity gate (Docker + Playwright/chromium + native Tesseract/tessdata + published sim, ~6–20 min, flaky) is **out-of-band** — validated in the owner's gate environment, not this session.
