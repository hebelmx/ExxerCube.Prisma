# RC.1 — Requirement→Evidence Trace: VERDICT / REPORTING / DISPOSITION cluster

**Date:** 2026-06-18 · **Branch:** `Liv` · **Auditor:** Claude Code (RC.1 cluster agent, read-only on production code)
**Cluster scope:** FR-15 (verdict aggregation), FR-16 (marked PDF), FR-17 (email alert), FR-18 (disposition + audit)
**Anchor:** `docs/planning-artifacts/READINESS-CHALLENGE-BRIEF-2026-06-18.md`; ground truth from RC0a/RC0b.
**Bar:** End-to-end evidence on real input — NOT a green unit test. Believe wiring + file:line, never comments/prose.

---

## Established context (from RC0b, re-confirmed here)

The Veriqan Worker (`04 Services/Veriqan.Worker/Program.cs`, 21 lines) is a **health-check-only ASP.NET shell**. It DI-wires the full stack via `AddVeriqan(config)` but exposes **no statement-submission entry point** (no HTTP `POST /verify`, no hosted service, no queue/folder watcher). The real orchestration class `VerificationPipeline` (`03 Orchestration/.../Pipeline/VerificationPipeline.cs`) is the only thing that chains stages — and it is invoked **only from test projects**. Therefore the entire verdict→report→alert→disposition chain has **no production trigger path**: it is reachable only from test harnesses.

---

## Requirement → Evidence table

| Requirement-ID | Intent | Built-state class | Evidence (file:line / test / none) | Gap to E2E-readiness |
|---|---|---|---|---|
| **FR-15** | Aggregate findings → GREEN/RED/BLOCKED; any FAIL→RED; all PASS/n-a→GREEN; blocking error→BLOCKED; InsufficientData reported separately, never alone causes RED. | **Real + Wired + E2E** (within pipeline) | `01 Core/Veriqan.Application/Verdict/VerdictAggregator.cs:46-131` (precedence BLOCKED→RED→GREEN; InsufficientData partitioned to `insufficientIds`, never added to `failIds` → never escalates, lines 92-94, 106). Wired in pipeline `VerificationPipeline.cs:201-204` (blocked path) and `:306-310` (normal path). Unit: `VerdictAggregatorTests.cs`; orchestration E2E (37/37) exercises it on the 3 synthetic PDFs. | Correctness is solid. **Never-false-block confirmed**: InsufficientData is structurally incapable of producing RED. Only gap is upstream — runs on synthetic non-compliant fixtures, not real CONDUSEF statements (corpus-gated). |
| **FR-16** | Color-marked PDF (PdfSharp) with every FAIL highlighted at its locator; original pages preserved; downloadable from QA console. CL-55. | **Real-unwired** | `02 Infrastructure/Veriqan.Infrastructure.Reporting/MarkedPdfGenerator.cs:41-320` — REAL PdfSharp rendering: opens in `Modify` mode (preserves pages, `:139`), draws semi-transparent highlight rect at the locator with correct PdfPig→PdfSharp Y-axis flip (`:252`), page-margin marker for box-less findings (`:289`). DI-registered `ServiceCollectionExtensions.cs:40`. Test `MarkedPdfGeneratorTests.cs` does **pixel-level** highlight verification via PDFtoImage→SKBitmap (real render, not a stub). | **Highlights are real, not placeholder.** BUT: no production caller — grep for `IMarkedPdfGenerator` finds only its definition, DI reg, and test. The pipeline (`VerificationPipeline.cs`) ends at Stage 7 verdict; it never calls Generate(). **No QA console exists** (no Veriqan UI/Razor/controller in `git ls-files`), so "downloadable" has no surface. |
| **FR-17** | Exactly one email per RED; dispatch failures retried + logged, never silently dropped. CL-54. | **Real-unwired** | `02 Infrastructure/.../VecAlertService.cs:34-209` — REAL: non-RED→no email + success (`:72-80`); RED→compose one message + Polly exponential-backoff retry (`:175-208`); permanent failure→`Error` log + typed `Result.WithFailure` (`:114-125`, never dropped). Transport `SmtpEmailSender.cs:29-137` is real `System.Net.Mail.SmtpClient`. DI: `ServiceCollectionExtensions.cs:55,58`. Tests: `VecAlertServiceTests.cs` (fake `IEmailSender`). | **Retry-not-drop is genuinely enforced** inside one call. **"Exactly one per RED" is only intra-call** (one invocation = one message + retries); there is NO cross-call/persisted "already-alerted" guard — a second invocation for the same RED statement would send a second email. Moot today: **no production caller** (grep confirms only def/DI/test). No trigger, and Worker has no SMTP config (no appsettings.json — RC0b). |
| **FR-18** | Human-in-the-loop disposition (accept/reject finding & statement); record actor, timestamp, before/after in append-only audit table; no auto accept/reject in v1. | **Partial** (service+entity+table real; append-only is app-convention only; no trigger; no UI) | Service `01 Core/.../Services/DispositionService.cs:32-184` — human-actor guard rejects blank actor (`:72-79`, `:111-118`); only ever calls `_repository.AppendAsync` (never update/delete). Entity `Domain/Entities/Disposition.cs:29-162` immutable, ctor enforces non-empty actor (`:83-85`). Repo `EfDispositionRepository.cs:43-75` insert-only. Migration `20260618001142_AddDispositionAudit.cs`. Tests `DispositionServiceTests.cs`. | Append-only is enforced **only in application + repository code**, NOT at DB level (see assessment below). **No production caller** — grep for `IDispositionService` finds only def/DI/test; pipeline never invokes it; **no QA console** for a human to act through. So "human-in-the-loop" has no human-facing surface. |

### Supporting / adjacent findings

| Item | Class | Evidence | Note |
|---|---|---|---|
| `VerdictSummary` legal-baseline separability (LegalBreach vs TenantOnly fail; `LegalBaselineSignal`) | Real + Wired | `Verdict/VerdictSummary.cs:117-162`; threaded in `VerdictAggregator.cs:85-88,116-117`. | Pipeline gate stays on effective `Verdict` (FR-15 intact); legal signal is informational. Sound. |
| `JobVerdict` provenance stamp (AR-9: EngineVersion + ReferenceBundleVersion on every verdict) | **Missing** (on the verdict rollup) | `JobVerdictConfiguration.cs:13-34` persists **only** `Id`, `VerificationJobId`, `Signal` — no version columns. Provenance fields exist only on `Disposition` (`Disposition.cs:155,161`). | AR-9 says "every verdict stamped with EngineVersion + ReferenceBundleVersion". The `JobVerdict` entity has no such columns. Provenance is on disposition (optional, nullable) only. |
| `VerdictSignal.Blocked` XML doc | Cosmetic defect (code correct) | `Enums/VerdictSignal.cs:15-19` doc says Blocked = "InsufficientData and no fail" — but aggregator only emits Blocked from a `BlockedOutcome` (binding failure); InsufficientData-only correctly yields Green. | Misleading doc, not a logic bug. Flag for cleanup. |

---

## Audit-immutability assessment

**Verdict: append-only is an application-layer convention only — it is NOT enforced at the database level.** The `AddDispositionAudit` migration (`20260618001142_AddDispositionAudit.cs:14-47`) creates an ordinary table with a primary key and two non-unique indexes. There is **no** `DENY UPDATE`/`DENY DELETE` grant, no `AFTER UPDATE`/`AFTER DELETE` rollback trigger, no SQL Server temporal/system-versioned table, no append-only ledger table (SQL Server 2022 `LEDGER = ON`), and no row-hash/tamper-evidence chain. Immutability rests entirely on (a) `DispositionService` only calling `AppendAsync`, (b) `EfDispositionRepository` exposing only insert + read, and (c) the entity having private setters. Any code path with the `DbContext` — or anyone with direct SQL `UPDATE`/`DELETE` rights on `veriqan.Dispositions` — can mutate or erase a record with no constraint stopping it and no tamper-evidence revealing it. For a 7-year regulatory audit trail (PRD §13 / PG-5), this is **not tamper-evident** and would not satisfy a "tamper-evident immutable ledger" control claim. The XML comments asserting "insert-only … no UPDATE or DELETE should ever target it" describe an intent the schema does not enforce.

---

## Biggest readiness gaps (≤5)

1. **No trigger path for the entire cluster.** Marked-PDF, email alert, and disposition each have a real implementation but **zero production caller**. `VerificationPipeline` stops at verdict aggregation (Stage 7) and never invokes report/alert/disposition; the Worker has no entry point at all. This is the dominant gap — the cluster is unreachable in a running host.
2. **No QA console / UI / API surface for Veriqan exists.** FR-16 ("downloadable from QA console") and FR-18 ("QA analyst can disposition") both presuppose a console that is absent from the codebase (no Veriqan Razor/controller/endpoint). The human-in-the-loop has no human-facing surface.
3. **Disposition audit is not DB-enforced immutable.** Append-only is convention, not constraint — no triggers, no ledger table, no tamper-evidence. Fails the regulatory "immutable audit" bar (PG-5).
4. **"Exactly one email per RED" is not durably guaranteed.** Enforced only within a single `SendRedAlertAsync` call; no persisted "already-alerted" state, so a reprocess/retry at the orchestration layer (once wired) could double-send. No de-dup guard exists.
5. **AR-9 verdict provenance missing on `JobVerdict`.** The verdict rollup persists only the signal; EngineVersion/ReferenceBundleVersion are not stamped on it (only optionally on Disposition). Plus all of this runs against synthetic non-compliant fixtures, not a real corpus (corpus-gated, inherited).

---

## Per-class tally

| Class | Count | Requirements |
|---|---|---|
| Real + Wired + E2E | 1 | FR-15 (E2E only within the test-reachable pipeline) |
| Real-unwired | 2 | FR-16, FR-17 |
| Partial | 1 | FR-18 |
| Stub | 0 | — |
| Missing | 0 (cluster FRs) | (AR-9 verdict-stamp is Missing as a supporting item) |
| Unknown | 0 | — |

> Caveat on FR-15's "+E2E": it is end-to-end only through the test harness (`VerificationPipeline` is never invoked by the Worker). Against the brief's bar ("E2E evidence on real input, not a green unit test"), no cluster requirement clears full production E2E, because (a) there is no running-host trigger and (b) inputs are the 3 synthetic non-compliant fixtures, not real CONDUSEF statements.
