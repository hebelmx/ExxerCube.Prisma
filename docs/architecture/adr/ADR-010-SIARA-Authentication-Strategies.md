# ADR-010: SIARA Authentication — Two Client-Selectable, Credential-Free Strategies

**Date**: 2026-06-11
**Status**: Accepted
**Deciders**: Owner (MVP scope ruling, 2026-06-11) + Development Team
**Tags**: siara, authentication, security, ingestion, browser-automation, itdd, mvp, technical-legal-control

> Promotes the planning-level design `docs/planning/gap-analysis/SIARA-AUTH-DESIGN-2026-06.md`
> to an architectural decision. That document holds the full ITDD/DI/config detail and the
> task breakdown (S0–S9); this ADR records the decision and its rationale. It feeds MVP-PATH
> Workstream **1.1** (the real SIARA downloader), whose auth seam this defines.

## Context and Problem Statement

SIARA (the CNBV authority-requirements portal) exposes **no API and no auth API** — login is a
basic web form. The system processes Spanish legal/banking documents, so a hard, owner-mandated
constraint applies: **Prisma must never hold raw credentials** (no username/password storage,
ever). Ingestion (MVP-PATH Workstream 1) nonetheless needs an authenticated browser session to
scrape documents, and different client deployments have different, legally-driven policies about
*who* may authenticate and *how* a session may be handed to an automated process.

The existing `ISiaraLoginService.LoginAsync(agent, username, password, ct)` takes **raw
credentials** and therefore cannot sit on the MVP path. We need an auth seam that is
credential-free and lets the client choose the mechanism that fits its own safety policy.

## Decision

Provide **both** sanctioned, credential-free mechanisms behind **one Domain port**, selected by
**configuration** so the choice is a deployment-time, **client-controlled technical-legal
control** (the client's own policy decides; neither is hardcoded):

| Mode | Mechanism | Who authenticates | What Prisma holds |
|---|---|---|---|
| `SessionPassthrough` | Ride an already-authenticated browser context handed in from outside Prisma (CDP attach or imported storage-state). | A human/system **outside** Prisma. | An opaque storage-state handle only. |
| `InteractiveLogin` | A human logs in **once** on the real SIARA page in a headed browser; Prisma captures the authenticated context and keeps it warm for headless downloads. | A human, on **SIARA's own page**. | An opaque storage-state handle only. |

Both produce the **same artifact** — a credential-free `SiaraSession` value object — so they sit
behind a single strategy port:

- **Domain port** `ISiaraSessionProvider` (`Mode`, `AcquireAsync`, `EnsureValidAsync`,
  `ReleaseAsync`), plus credential-free value objects `SiaraSession` / `SiaraSessionRequest` and
  the `SiaraAuthMode` enum. (`01 Core/Domain`.)
- **Selection** via a fail-closed `ISiaraSessionProviderResolver` that reads
  `IOptions<SiaraAuthOptions>.AuthMode` and resolves a provider **keyed by `SiaraAuthMode`**;
  an unregistered/unknown mode returns `Result.WithFailure` (never throws, never silently
  defaults) so the downloader cannot proceed unauthenticated.
- **One net-new infrastructure capability**: session/storage-state support on the browser agent
  (Playwright `StorageState` / `ConnectOverCDP`) — the agent has none today.
- Authored **ITDD-first** (ADR-005): an abstract `SiaraSessionProviderContract` + mock blueprint
  + a stateful reference fake (`FakeSiaraSessionProvider`) ship **before** the production
  strategies, so the downloader/watch-loop tests (MVP-PATH 1.1/1.2) need no live browser.

`ISiaraLoginService` (raw-credential) is **kept off the MVP path**; its fate (deprecate vs. reuse
only with human-supplied, non-persisted values) is an open decision in the design doc §14.

## Rationale

1. **No-credential guarantee is structural, not procedural** — neither mode accepts or persists a
   username/password; `SiaraSession` carries no credential members (asserted by a reflection
   contract test). The guarantee is enforced by the type system + tests, not by convention.
2. **The legal choice belongs to the client** — passthrough vs. interactive is a safety-policy
   decision that varies per deployment (e.g. environments that forbid CDP). Making it pure
   configuration turns it into the client's documented, auditable control rather than a Prisma
   code decision.
3. **One port, two strategies** keeps the downloader (1.1) and watch loop (1.2) ignorant of the
   mechanism — switching is config-only, no code redeploy.
4. **Session, not one-shot login** — `EnsureValidAsync` keep-warm/refresh is what lets a single
   acquisition drive hours of headless polling.
5. **Fail-closed everywhere** — resolver and providers return `Result` failures on any auth
   problem, so the system never scrapes unauthenticated.

## Consequences

- New Domain surface: `ISiaraSessionProvider`, `ISiaraSessionProviderResolver`, `SiaraSession`,
  `SiaraSessionRequest`, `SiaraAuthMode`. Until their production adapters land (design tasks
  S5/S6 providers, S7 resolver), the two interfaces are **intentionally allowlisted** in the
  `All_Domain_Interfaces_Should_Have_At_Least_One_Implementation` arch test (ports-before-adapters,
  ITDD) — **remove from the allowlist when S5–S7 implement them**.
- `IBrowserAutomationAgent` gains session/storage-state methods (or a companion
  `IBrowserSessionContext` port), backed by Playwright.
- Security posture (must-haves): storage-state refs are secrets (never logged in full; encrypt at
  rest if persisted, prefer in-memory); every Acquire/EnsureValid/Release is audited via
  `IAuditLogger` (ties to MVP A6); honor `ExpiresAt`/`ReleaseAsync`.
- The reference fake unblocks downstream ingestion tests early; mutation testing scopes the
  providers + resolver after green, excluding the live-Playwright glue.

## Related

- Design (full detail + tasks S0–S9): `docs/planning/gap-analysis/SIARA-AUTH-DESIGN-2026-06.md`
- MVP path (Workstream 1.1/1.2): `docs/planning/gap-analysis/MVP-PATH-2026-06-11.md`
- Gap matrix: `docs/planning/gap-analysis/GAP-MATRIX-2026-06-11.md`
- ADR-005 (ITDD contract tests) — the test shape this feature follows
- ADR-009 (`IndFusion.Ember`) — the event transport the 3-process ingestion split will use
