# Handoff — MVP-PATH 1.1: real SIARA document downloader (DONE)

**Date:** 2026-06-12 · **Branch:** `Kt2` (pushed) · **Supersedes (for 1.1):** `HANDOFF-2026-06-12-siara-auth-S8.md`
**Scope:** MVP-PATH Workstream 1.1 — adapt the working browser-automation scraper into the headless
`IDocumentDownloader` port, behind the credential-free SIARA auth seam (ADR-010).

---

## TL;DR

The Orion worker now pulls **real documents from SIARA through the whole real path** —
`ISiaraSessionProviderResolver` → `SessionPassthroughSiaraSessionProvider` → `SiaraDocumentDownloader` —
proven by a **live headless-Playwright E2E against the cookie-faithful simulator** that asserts real bytes
plus the trustworthy **actor + session provenance** stamped on each document (ADR-010 P2). `A1` of the MVP
definition is met: real downloader wired, stub no longer registered, integration test pulls ≥1 document, no
raw credentials persisted.

**Everything green:** Domain.Interfaces 313 · BrowserAutomation 179 · BrowserAutomation.E2E **32 (incl. the
live test)** · Orion.Ingestion 9 · Orion.Worker host 9 · Architecture 22 · EndToEnd 27. Build 0/0.

### Commits (all on `Kt2`, pushed: `afbeb89..dde5c83`, plus `e56f135..e5e5bc8`)
| Commit | What |
|---|---|
| `e56f135` | **P1** ROP-ify `IDocumentDownloader` (`Task<byte[]>` → `Task<Result<DownloadedDocument>>`) + typed provenance threaded into the journal manifest |
| `e011e45` | **P2** `SiaraDocumentDownloader` adapter + full ITDD (contract + fake + 2 inheritors + mechanics) |
| `e5e5bc8` | **P3** worker DI wiring (scoped; worker owns the per-work scope; no captive dependency) |
| `afbeb89` | **fix** launch-before-acquire (passthrough lifecycle) |
| `596b741` | **fix** SessionPassthrough must navigate to SIARA before probing auth (S5 gap) |
| `dde5c83` | **P4** live simulator E2E + adapter download fixes (API-request fetch + `IgnoreHttpsErrors`) |

---

## What 1.1 turned out to be

Not a new scraper — an **adapter** that composes the already-working `SiaraNavigationTarget` /
`IBrowserAutomationAgent` scraper with the S0–S9 auth seam, plus the port evolution to functional ROP.

### Three real bugs the live E2E surfaced (each fixed + regression-tested)
1. **SUT lifecycle** — the adapter fails closed until `LaunchBrowserAsync`, and SessionPassthrough attaches
   to an *existing* browser; the downloader launched *after* acquisition. Fixed: pre-launch for passthrough
   (the login modes launch their own browser; the adapter's launch is not idempotent, so we do not
   double-launch them).
2. **SessionPassthrough never navigated before probing (S5 gap)** — `LoadStorageStateAsync` leaves a blank
   page, so the `IsAuthenticated(postLoginSelector)` probe always saw nothing against a real browser. The
   S5 unit tests missed it (mocked `IBrowserSessionContext`). Fixed: the provider now navigates to
   `SiaraPassthroughOptions.DashboardUrl` before probing, in `AcquireAsync` and `EnsureValidAsync`; injects
   `IBrowserAutomationAgent`. Regression test added.
3. **Adapter download mechanism** — `DownloadFileAsync` used `page.GotoAsync`, which throws
   "Download is starting" for attachment-served files; and Playwright's API-request stack validates TLS
   independently of the browser, rejecting the sim's self-signed cert. Fixed: fetch via
   `page.APIRequest.GetAsync` (cookie-authenticated, any content type) + opt-in
   `BrowserAutomationOptions.IgnoreHttpsErrors` (default FALSE — production SIARA has a valid cert).

### Decisions taken (with the owner)
- Port evolved to `Task<Result<DownloadedDocument>>` (functional ROP) — the `byte[]` signature was a mishap.
- **Typed provenance** (`DownloadedDocument` carries `SiaraActor` + `SessionId`), not audit-log-only —
  per-document non-repudiation rides through the Result and onto the journal manifest.
- **Worker owns the per-pull DI scope** (`IServiceScopeFactory` in `OrionWorkerService`); orchestrator +
  downloader + health/dashboard are scoped. The 1.2 watch loop refines this to one scope per document.

---

## Key files
- Port + provenance: `01 Core/Domain/Interfaces/IDocumentDownloader.cs`, `.../ValueObjects/DownloadedDocument.cs`,
  `.../Interfaces/IngestionManifestEntry.cs` (gained `ActorId`/`SessionId`).
- Adapter: `02 Infrastructure/Infrastructure.BrowserAutomation/Siara/SiaraDocumentDownloader.cs`.
- Provider fix: `.../Siara/SessionPassthroughSiaraSessionProvider.cs` (+ `SiaraAuthOptions.DashboardUrl`).
- Adapter download: `.../PlaywrightBrowserAutomationAdapter.cs`, `.../BrowserAutomationOptions.cs`.
- Worker wiring: `04 Services/Orion/Prisma.Orion.Worker/Program.cs`, `OrionWorkerService.cs`.
- ITDD: `09 Testing/01 Abstractions/Testing/Contracts/{DocumentDownloaderContract,FakeDocumentDownloader}.cs`.
- Live E2E: `08 Tests/05 System/Tests.Infrastructure.BrowserAutomation.E2E/SiaraDocumentDownloaderE2ETests.cs`
  (force-added — the `*.e2e` gitignore rule matches the `.E2E` dir case-insensitively on Windows).

## Running the live E2E
Needs Chromium (`playwright install chromium`) + the published simulator at
`Deployments/Siara.Simulator/app/` (the test starts it if it is not already on `http://localhost:5001`).
```
dotnet test "Prisma/Code/Src/CSharp/08 Tests/05 System/Tests.Infrastructure.BrowserAutomation.E2E/*.csproj" \
  --filter-query "/*/*/SiaraDocumentDownloaderE2ETests/*"
```

---

## NEXT (MVP-PATH 1.2 / 1.3)
- **1.2 watch loop** — implement `IngestionOrchestrator.StartAsync` (still a placeholder): a poll/watcher
  that discovers SIARA documents, and per discovered document creates a DI scope and calls
  `IngestDocumentAsync`. Uses `ISiaraSessionProvider.EnsureValidAsync` to keep the session warm across the
  loop. The downloader and provenance plumbing are ready.
- **1.3 Ember hub** — replace `StubExxerHub` in the worker with the real `IndFusion.Ember` transport
  (folded into the 3-process Downloader/Extractor/Reconciliator design, ADR-009). Today `StubExxerHub`
  remains, clearly labelled.
- **Known follow-ups:** the login-mode (`Interactive`/`Automated`) browser-lifecycle vs. the non-idempotent
  adapter launch (passthrough is the MVP path and is correct); a real cross-impl `IBrowserSessionContext`
  test that would have caught the navigate-before-probe gap directly.
