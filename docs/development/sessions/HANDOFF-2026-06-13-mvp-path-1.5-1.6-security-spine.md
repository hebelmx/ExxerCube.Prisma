# Handoff — MVP-PATH 1.5 + 1.6 + follow-ups (per-process security spine) — DONE

**Date:** 2026-06-13 · **Branch:** `Kt2` (pushed) · **Driver:** BMAD orchestrator
**ADR:** `ADR-012-Per-Process-Security-Spine.md` · **Tracker:** `TRACKER-2026-06-12-mvp-path-1.5-1.6.md`
**Supersedes the "1.5/1.6 next" prompt in:** `HANDOFF-2026-06-12-mvp-path-1.4-reconciliator-edge.md`

## TL;DR
The 3-process security split (ADR-011) is now **secured + audited + exporting**. Items **1.5 (A5)** and **1.6 (A6)** are complete and adversarially hardened, plus all four owner-approved follow-ups (hub auth, SmartEnum handoff fidelity, real SIRO-XML export, all-real-wire 3-host E2E). All verified from ground truth (build 0/0 + the touched test projects, incl. Docker/Testcontainers).

## Owner decisions (settled this session)
- **1.5 identity:** config-bound per-process service identity (reuse `ISiaraActorIdentityProvider`/`SiaraActor` + `ProcessIdentityOptions.Clearance`); clearance token modelled on `EfCoreIdentityAdapter`'s JWT-signing half, **no Identity DB** (A5 DoD wording updated to match).
- **Run scope:** 1.5 + 1.6 + hub auth + SmartEnum converter + real SIRO-XML export + all-real-wire E2E.
- **1.6:** direct shared-audit-DB per worker + first-class `AuditRecord.ProcessId` column + EF migration.
- **1.6 gate fix:** drop the `AuditRecords→FileMetadata` FK (immutable audit trail; audit-by-FileId works).
- **#8:** dedicated `TemplateConnection` (turned out **moot** — SIRO path is DB-free) + full SIRO-XML now.

## Commits (on `Kt2`, pushed) — 9bd9d4e..HEAD
| Commit | What |
|---|---|
| 54df0a3 | docs: 1.5 design + tracker + A5 DoD reword |
| 43bc888 | 1.5a Domain foundation (ProcessClearance, IProcessClearanceTokenService, ClearanceToken event props, ITDD) |
| 4854a80 | 1.5b JwtProcessClearanceTokenService + AddProcessIdentity + 3-worker wiring + send-side mint |
| 9e5158e | 1.5c forwarder clearance enforcement (A5 gate) + reject tests |
| 1fecf73 | 1.5 hardening: config-driven clearance + JWT rejection tests (gate #2/#3) |
| f8714fb | 1.5 doc: jti/replay overstatement corrected (gate #1) |
| c9be684 | hub auth on both SignalR edges (JWT bearer + downstream-clearance policies) |
| 01c5aef | 1.6a AuditRecord.ProcessId + migration + LogAuditAsync processId + AddDatabaseServices in workers |
| a65d7f2 | 1.6b audit call sites (3 paths) + A6 gate tests |
| 43452d0 | 1.6 hardening: drop Audit→FileMetadata FK + A6-by-FileId tests + queued-path test + drain-bug fix |
| 1e346af | SmartEnum handoff JSON converter |
| e183013 | #8 SIRO export design |
| c508199 | #8 real SIRO-XML export (SiroXmlExporter via IResponseExporter) + AddSiroExportServices DI split + Reconciliator host-DI tests |
| 8123793 | #9 all-real-wire 3-host E2E |
| *(this)* | ADR-012 + handoff + memory + E2E comment/limitation nits (final-gate #1/#3) |

## Verification (ground truth)
Build 0/0. Green: Domain.Interfaces 339 · Infrastructure.BrowserAutomation 199 · Athena.Processing 70 · Orion.Worker 24 · Athena.Worker 24 · Reconciliator.Worker 3 (host-DI) · System.Storage 43 (A6 by FileId, Testcontainers) · Infrastructure.FileSystem 54 · AllRealWireE2E 1. Two adversarial gates (1.5, 1.6) + a final plan-completion gate — all MAJORs closed; residual findings are MINOR + logged.

## Known limitations / deferred (honest — see ADR-012 §Consequences) — tracked in **issue #2**
1. `jti` minted but not enforced → same-document within-lifetime replay not blocked (idempotent; mitigated by short lifetime + file_id).
2. Connection-scope token `file_id=Guid.Empty` edge — not an in-practice bypass (real docs have real GUIDs); logged.
3. SIRO XSD schema validation hook (`XmlSchemaSet`) dormant pending the Banamex XSD; structural assertions cover MVP.
4. All-real-wire E2E uses in-memory TestServer transport (real SignalR protocol + auth + pipeline, not TCP).
5. Symmetric HMAC: clearance separation is honesty-by-configuration within one trust boundary (asymmetric per-process keys = future hardening).
6. SIRO XML produced to a stream/event (not written to shared storage) — deferred per design D1.

## ⚠️ Pre-existing failure to fix (OUT of this scope, surfaced here) — **issue #1**
`EfCoreRepositoryIntegrationTests.FileMetadataRepository_RemoveAsync_ShouldDeleteEntity` **fails on clean HEAD** (verified via `git stash`): "Entity of type FileMetadata with id file-004 not found." Unrelated to this work (file-metadata repo, not audit). Likely a tail from the ITDD Phase 6 repository not-found tightening. Worth a dedicated fix. Tracked: https://github.com/hebelmx/ExxerCube.Prisma/issues/1

## Next Workstream items (per MVP-PATH-2026-06-11.md)
WS1 ingestion (1.1–1.4) + the security spine (1.5/1.6) are now done. Remaining toward MVP:
- **2.1** multi-source fusion (feed XML+DOCX into the worker path) · **2.2** native PDF text · **3.1** persist reviewer decisions to the unified record · **4.1** SLA metrics export · **4.2** real readiness probes · **4.3** externalize config/remove template pages.
- **5.1/5.2** the MVP E2E gate — note #8 already delivered the SIRO half of F1/5.2 (SIRO XSD validation still pending the Banamex XSD).

## HARD CONSTRAINTS (carry forward)
xUnit v3 + Shouldly + NSubstitute (no Moq/FluentAssertions) · `TestContext.Current.CancellationToken` · `Result<T>`+`CancellationToken` · ITDD per ADR-005 · broadcast via `IHubContext` · `dotnet test <csproj>` no extra flags · single-project builds · commit code+tests separately from docs · push `Kt2` · **verify every chunk from ground truth** (this session caught a silent-drop FK bug, a broken worker DI graph, and a missing-test gap that subagent summaries had reported as "done").

---

## Short prompt for the next orchestration agent

> Workstream 1 (ingestion 1.1–1.4 + the security spine 1.5/1.6 + follow-ups) is **DONE** on `Kt2` (build 0/0, all suites green; ADR-012; this handoff). Read this handoff + the auto-memory `mvp-path-1.5-1.6-security-spine.md` first. Then drive the remaining path to the MVP gate, per `docs/planning/gap-analysis/MVP-PATH-2026-06-11.md` — these are **independent of Workstream 1 and of each other**, so fan them out:
>
> - **2.1 (B1)** multi-source fusion — feed XML + DOCX into the worker path (today `null` at `ProcessingOrchestrator.cs:535-542,819-823`); fuse all three sources; 3-source test.
> - **2.2 (B2)** native PDF text — `PdfMetadataExtractor.TryExtractTextFromPdfAsync` reads embedded text via PdfPig/PdfSharp instead of `string.Empty`; OCR fallback retained.
> - **3.1 (C2+C3)** persist reviewer decisions + field annotations into `UnifiedMetadataRecord` (today only the review table).
> - **4.1 (D2)** wire SLA metrics export (`AddMeter("ExxerCube.Prisma.SLA")`) + real gauge values · **4.2 (E1)** real readiness probes · **4.3 (E2)** externalize config + delete `Counter.razor`/`Weather.razor` (demo-blocker, do early).
> - **5.1/5.2** the MVP E2E gate. **Note:** #8 already delivered the SIRO-XML half of F1/5.2; the remaining SIRO piece is **XSD schema validation** — needs the Banamex `.xsd` (see **issue #2**, item 3).
>
> **First, clear the decks:** fix **issue #1** (pre-existing `EfCoreRepository FileMetadataRepository_RemoveAsync` failure — fails on clean HEAD, blocks a fully-green `Tests.Infrastructure.Database`). The deferred security-spine hardening is **issue #2** (owner-gate before pulling any of it forward).
>
> **Constraints:** ITDD per ADR-005; `Result<T>`+`CancellationToken`; xUnit v3 + Shouldly + NSubstitute (no Moq/FluentAssertions); `TestContext.Current.CancellationToken`; `dotnet test <csproj>` no extra flags; broadcast via `IHubContext`; commit code+tests separately from docs; push `Kt2`. **Use the BMAD orchestrator and verify every chunk from ground truth — do NOT trust subagent "all green / full solution 0/0" summaries; run the directly-affected test suites yourself** (a WebApplicationFactory `ValidateOnBuild` host-DI test per worker is the cheapest guard against DI-graph regressions). Docker IS available for Testcontainers suites.
