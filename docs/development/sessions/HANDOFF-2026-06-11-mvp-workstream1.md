# HANDOFF — MVP Workstream 1 (ingestion + 3-process split)

**Date:** 2026-06-11 · **Branch:** Kt2 · **From:** MVP-audit session (audit complete, quick wins shipped + pushed)

> Next-agent handoff. The full MVP audit is DONE and committed/pushed; the job now is to start
> executing the planned work. This file is the pasteable brief — the canonical detail lives in the
> linked docs, read those rather than trusting any summary.

---

## Start by reading (in this order)

1. Auto-memory `MEMORY.md` → entry **"MVP audit 2026-06-11 (in progress)"** (`mvp-audit-2026-06-11.md`) — live state + all owner decisions.
2. `docs/planning/gap-analysis/MVP-PATH-2026-06-11.md` — the ordered work plan (Workstreams 1–5, DoD + size + deps per item). **This is your task list.**
3. `docs/planning/gap-analysis/GAP-MATRIX-2026-06-11.md` — adversarially-verified ground truth.
4. `docs/planning/gap-analysis/SIARA-AUTH-DESIGN-2026-06.md` — feature design for the first real work item (tasks S0–S9).
   - Background only if needed: `MVP-DEFINITION-2026-06.md`, `PRD-RECONCILIATION-2026-06.md`, `MVP-AUDIT-PLAN-2026-06-11.md`.

---

## Where things stand

- Core processing + manual-review dashboard + SLA dashboard are **BUILT and green**.
- MVP is gated almost entirely by **Workstream 1 = ingestion + the security-mandated 3-process split (A1–A6)**. A short tail of partials remains (C3 persist-to-unified-record, D2 SLA telemetry, A5/A6 security, E1 readiness).
- Quick wins already shipped this session: **2.2 native PDF text** (PdfPig) and **4.3 config/template cleanup**.
- Item **1.3 (real Ember in workers) is DEFERRED by design** — see `MVP-PATH` Appendix A. It's topology-coupled to the 1.4 split; don't bolt it on standalone (would be a zero-subscriber fake-real hub).

### Committed & pushed this session (`origin/Kt2`, `a9a9c42..73d0d39`)
| Commit | Chunk |
|---|---|
| `df2b351` | BMAD v4→v6.8.0 upgrade (BMM/BMB/CIS) |
| `083e301` | MVP audit docs + CLAUDE.md Release Status pointer |
| `5a36b3a` | feat: native PDF text via PdfPig (B2) |
| `73d0d39` | chore: externalize DB config + remove template pages (E2) |

---

## Recommended next task — begin Workstream 1 via the SIARA auth feature

Do **S0–S3 first** (lowest-risk, highest-leverage ITDD scaffolding):

- **S0** — author `ADR-010` (two client-selectable auth mechanisms as a technical-legal control).
- **S1** — Domain port `ISiaraSessionProvider` + `SiaraSession`/`SiaraSessionRequest` VOs + `SiaraAuthMode` enum + `ISiaraSessionProviderResolver`.
- **S2** — abstract `SiaraSessionProviderContract` (in `ExxerCube.Prisma.Testing.Contracts`) + mock blueprint instance + `SiaraSessionProviderMockFactory` — green with **no impl yet**.
- **S3** — stateful reference fake `FakeSiaraSessionProvider` + its contract inheritor. **Do this early** — it unblocks the 1.1/1.2 downloader tests without a live browser.

Then S4–S9 (agent session capability, the two impls, resolver, DI, security tests, mutation). Full breakdown + signatures in the design doc.

---

## Hard constraints (verify in CLAUDE.md)

- **No raw credential storage, EVER** (both SIARA mechanisms). `SiaraSession` must expose no credential fields — there's a test for this. The existing `ISiaraLoginService` takes raw `username/password` → keep it **OFF the MVP path**.
- **ITDD discipline (ADR-005):** every new port gets a `*Contract` abstract base in `ExxerCube.Prisma.Testing.Contracts` + ≥1 inheritor (an architecture test enforces this).
- **Result<T>** (`IndQuestResults`) for all business logic, never throw; **CancellationToken** on every async method (pre-cancelled → `ResultExtensions.Cancelled<T>()`); the pre-cancelled-token contract test is mandatory.
- **Tests:** xUnit v3 + MTP. Run with `dotnet test <project.csproj>` — **NO extra flags** like `--nologo` (they break the MTP runner and report "Zero tests ran").
- Build single projects when possible (~70-project solution). Build artifacts → `E:\Dynamic\ExxerCubeBanamex\BuildArtifacts\Prisma\`.

---

## Open decisions to resolve as you go (design doc §14)

- SIARA passthrough handoff transport: CDP connect vs imported storage-state (or both).
- Evolve `IDocumentDownloader.DownloadAsync` from `Task<byte[]>` → `Task<Result<byte[]>>` during item 1.1.
- `ISiaraLoginService`: reuse human-driven, or deprecate (recommend deprecate).

---

## Working agreement

Commit/push **only when the owner asks**; commit in logical chunks ending with the
`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>` trailer; update `MEMORY.md`
as you progress; confirm the plan before large/irreversible changes.
