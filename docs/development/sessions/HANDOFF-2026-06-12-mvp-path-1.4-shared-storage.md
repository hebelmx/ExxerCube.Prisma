# Handoff — MVP-PATH 1.4 (shared document storage) — DONE

**Date:** 2026-06-12 · **Branch:** `Kt2` (pushed) · **Supersedes the "shared storage" follow-up in:**
`HANDOFF-2026-06-12-mvp-path-1.2-1.3.md`
**Scope:** the highest-leverage deferred item from MVP-PATH 1.3 — make a downloaded document actually reach
Athena's pipeline, not just the event.

---

## TL;DR

MVP-PATH 1.3 delivered the `DocumentDownloadedEvent` across the process boundary, but the **file** was not
shared: the event carried only a bare `{id}.pdf`, so Athena's loader could not find it and logged-and-continued.
1.4 closes that with a **shared-storage contract**:

```
Orion stores  {Storage:BasePath}/2026/06/12/{id}.pdf   and puts Path="2026/06/12/{id}.pdf" on the event
        ⇒⇒ cross-process (SignalR, 1.3) ⇒⇒
Athena IngestionEventForwarder.Resolve(Path) against ITS OWN {Storage:BasePath}
        → absolute path stamped onto FileName → ProcessingOrchestrator loads it → pipeline runs
```

**Owner-chosen contract (AskUserQuestion):** *relative path on the event + per-process `Storage:BasePath`*
(not an absolute path on the event) — so the Downloader and Extractor can mount the shared volume at
**different** absolute paths (separate containers in the 3-process split).

**Everything green, build 0/0:** Domain 187 · Domain.Interfaces 321 · Infrastructure.FileSystem 30 ·
Athena.Processing 42 · Orion.Ingestion 15 · Architecture 22 · Athena.Worker 9 · Orion.Worker 15 · EndToEnd 27.

### Commits (on `Kt2`)
| Commit | What |
|---|---|
| `a9afa8d` | **1.4** shared document storage — relative path on event + `IStoragePathResolver` + forwarder resolution + DI + ITDD + E2E proof |
| *(this)* | **1.4 docs** ADR-011 update + handoff |

---

## What landed

- **Domain.** `DocumentDownloadedEvent.Path` is now `init`-settable and documented as the **storage-relative**
  path. New port `IStoragePathResolver` (`Result<string> Resolve(string relative)`). New pure helper
  `StoragePathResolution` (`Domain.Services`) is the **single source** of the security-sensitive confinement
  guard — blank / rooted / `../` traversal / invalid-char all **fail closed** as a `Result` failure, no disk
  access (deterministic; file existence stays the loader's concern).
- **Orion.** `IngestionOrchestrator` computes `YYYY/MM/DD/{id}.pdf` (forward-slash, mount-path independent) and
  stamps it onto the event; `Storage:BasePath` is config-driven (defaults to `./storage`).
- **Athena.** `SharedStoragePathResolver` + `StorageOptions` (Infrastructure.FileSystem, `Storage` section).
  `IngestionEventForwarder` now resolves the relative path → absolute → stamps onto `FileName` before
  republishing, **tolerant of config faults**: on a blank path or resolution failure it forwards the event
  unchanged (the orchestrator already log-and-continues). DI wired on both workers.
- **ITDD (ADR-005).** `StoragePathResolverContract` + `FakeStoragePathResolver` (delegates to the same
  `StoragePathResolution` primitive, so the fake can't drift from real on the traversal guard) + two
  inheritors (fake in `Tests.Domain.Interfaces`, real in `Tests.Infrastructure.FileSystem`). New forwarder
  resolution tests + `SharedStorageForwardingEndToEndTests` proving a relative-path event resolves and the
  pipeline opens the **absolute** path.

## Deployment note
Set `Storage:BasePath` on **both** workers to the same shared volume (mount path may differ per process). With
it blank, resolution fails closed and ingestion still runs (the file just won't load) — safe for single-box dev.

## Deferred follow-ups (unchanged — see ADR-011)
1. **Reconciliator edge (Extractor → Reconciliator)** — the third actor; same host-hub + subscribe + republish
   pattern as 1.3. Not built.
2. **Hub auth/authz** — `/hubs/ingestion` is an internal, network-bounded endpoint with no auth; harden before
   untrusted-network exposure.
3. Real PDF rasterization in `FileSystemLoader` (`LoadPdfAsImage` returns empty) — pre-existing gap; 1.4 proves
   the file **resolves and loads**, content extraction for PDFs is a separate item.

## HARD CONSTRAINTS (carry forward)
xUnit v3 + Shouldly + NSubstitute (no Moq/FluentAssertions); `TestContext.Current.CancellationToken`;
`Result<T>` + `CancellationToken`; ITDD per ADR-005; broadcast outbound via `IHubContext`; `dotnet test
<csproj>` no extra flags; commit code+tests separately from docs; push `Kt2`.
