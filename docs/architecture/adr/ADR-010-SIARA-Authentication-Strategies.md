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

Provide **three** sanctioned mechanisms behind **one Domain port**, selected by **configuration**
so the choice is a deployment-time, **client-controlled technical-legal control** (the client's own
policy decides; none is hardcoded). None ever **persists** raw credentials:

| Mode | Mechanism | Who authenticates | What Prisma holds |
|---|---|---|---|
| `SessionPassthrough` | Ride an already-authenticated browser context handed in from outside Prisma — **storage-state import (default)** or **CDP attach (opt-in)**, chosen by config. | A human/system **outside** Prisma. | An opaque storage-state handle only. |
| `InteractiveLogin` | A human logs in **once** on the real SIARA page in a headed browser; Prisma captures the authenticated context and keeps it warm for headless downloads. | A human, on **SIARA's own page**. | An opaque storage-state handle only. |
| `AutomatedLogin` | Prisma drives SIARA's login form **unattended** with credentials sourced at runtime from a client-owned secret store (Key Vault / env / secrets manager) via `ISiaraCredentialSource`, used **transiently** and **never persisted**. Enables 24/7 operation. | Prisma, with the client's vault-held credentials it never stores. | An opaque storage-state handle only — **no credentials**. |

All three produce the **same artifact** — a credential-free `SiaraSession` value object — so they sit
behind a single strategy port:

- **Domain port** `ISiaraSessionProvider` (`Mode`, `AcquireAsync`, `EnsureValidAsync`,
  `ReleaseAsync`), plus credential-free value objects `SiaraSession` / `SiaraSessionRequest` and
  the `SiaraAuthMode` enum. (`01 Core/Domain`.)
- **Selection** via a fail-closed `ISiaraSessionProviderResolver` that reads
  `IOptions<SiaraAuthOptions>.AuthMode` and resolves a provider **keyed by `SiaraAuthMode`**;
  an unregistered/unknown mode returns `Result.WithFailure` (never throws, never silently
  defaults) so the downloader cannot proceed unauthenticated.
- **Net-new infrastructure capability** (delivered in S4): session/storage-state support on the
  browser agent via the companion port `IBrowserSessionContext` (Playwright `StorageState` /
  `ConnectOverCDP`) — implemented by `PlaywrightBrowserAutomationAdapter`.
- **`ISiaraCredentialSource`** (new Domain port, for `AutomatedLogin` only): yields the SIARA
  credentials at the moment of login from a client-owned secret store; Prisma never persists them
  and `SiaraSession` never carries them.
- Authored **ITDD-first** (ADR-005): an abstract `SiaraSessionProviderContract` + mock blueprint
  + a stateful reference fake (`FakeSiaraSessionProvider`) ship **before** the production
  strategies, so the downloader/watch-loop tests (MVP-PATH 1.1/1.2) need no live browser.

`ISiaraLoginService` (raw username/password) is **retained** as the form-driver the `AutomatedLogin`
provider invokes with the transient, vault-sourced credentials — and as the simulator/E2E login
driver (using the simulator's public fake credentials). It is **never** registered to receive
persisted credentials.

## Resolved decisions (2026-06-11)

Settled with the owner after researching the real platform (the SIARA design doc §14 open items are
now closed):

1. **Real-SIARA research.** [`siara.cnbv.gob.mx`](https://siara.cnbv.gob.mx/) is a **username/password
   web form** (no API), session-cookie based, with **max-attempt lockout** and the usual "incorrect
   password / non-existent user / inactive account" errors. No published MFA/CAPTCHA — but a regulated
   portal may add either at any time. **We will not have access to the real system until deployment**
   (it is a need-to-know, legally-restricted platform), which is precisely why two of the three modes
   are human-/external-driven: whatever auth SIARA presents (password, MFA, CAPTCHA, certificate), the
   human or external system completes it and we only capture the resulting session.
2. **Passthrough transport = both, default storage-state.** Storage-state import is the portable,
   headless, bank-safe default; CDP `ConnectOverCDP` is opt-in for environments that allow a
   remote-debugging browser (most hardened bank environments do not). Selected by
   `Siara:Passthrough:Transport` (`StorageState` | `Cdp`).
3. **Credentialed `AutomatedLogin` is a production mode** — but only via `ISiaraCredentialSource`
   over a runtime secret store, **never** persisted by Prisma (the hard no-storage rule is preserved
   structurally: `SiaraSession` exposes no credential members; credentials are used transiently and
   discarded).
4. **Simulator faithfulness.** `tools/Siara.Simulator` is upgraded from demo auto-login (Blazor
   *circuit* state, which storage-state cannot capture) to a **cookie-based session** with the fake
   credentials enforced + max-attempt lockout + real-style Spanish error messages — so all three
   modes are provably testable end-to-end against it, matching the real cookie-session model. The
   public fake credentials remain in the simulator only.

## Rationale

1. **No-credential-storage guarantee is structural, not procedural** — no mode *persists* a
   username/password; `SiaraSession` carries no credential members (asserted by a reflection
   contract test). For `AutomatedLogin`, credentials are vault-sourced at the moment of login and
   discarded. The guarantee is enforced by the type system + tests, not by convention.
2. **The legal choice belongs to the client** — passthrough vs. interactive vs. automated is a
   safety-policy decision that varies per deployment (e.g. environments that forbid CDP, or that
   mandate unattended operation). Making it pure configuration turns it into the client's
   documented, auditable control rather than a Prisma code decision.
3. **One port, three strategies** keeps the downloader (1.1) and watch loop (1.2) ignorant of the
   mechanism — switching is config-only, no code redeploy. `AutomatedLogin` is the mode that makes
   the unattended 24/7 watch loop viable without a human.
4. **Session, not one-shot login** — `EnsureValidAsync` keep-warm/refresh is what lets a single
   acquisition drive hours of headless polling.
5. **Fail-closed everywhere** — resolver and providers return `Result` failures on any auth
   problem, so the system never scrapes unauthenticated.

## Consequences

- New Domain surface: `ISiaraSessionProvider`, `ISiaraSessionProviderResolver`, `SiaraSession`,
  `SiaraSessionRequest`, `SiaraAuthMode` (now `SessionPassthrough` / `InteractiveLogin` /
  `AutomatedLogin`), and `ISiaraCredentialSource`. Until their production adapters land
  (S5 passthrough, S6 interactive, S6b automated + credential source, S7 resolver), the impl-less
  ports are **intentionally allowlisted** in the
  `All_Domain_Interfaces_Should_Have_At_Least_One_Implementation` arch test (ports-before-adapters,
  ITDD) — **remove each from the allowlist as its adapter lands**.
- `IBrowserSessionContext` (S4, done) is implemented by `PlaywrightBrowserAutomationAdapter`.
- Security posture (must-haves): storage-state refs and vault credentials are secrets (never logged
  in full; storage-state encrypted at rest if persisted, prefer in-memory; credentials never
  persisted at all); every Acquire/EnsureValid/Release is audited via `IAuditLogger` (ties to MVP
  A6); honor `ExpiresAt`/`ReleaseAsync`.
- The reference fake unblocks downstream ingestion tests early; mutation testing scopes the
  providers + resolver after green, excluding the live-Playwright glue.

## Related

- Design (full detail + tasks S0–S9): `docs/planning/gap-analysis/SIARA-AUTH-DESIGN-2026-06.md`
- MVP path (Workstream 1.1/1.2): `docs/planning/gap-analysis/MVP-PATH-2026-06-11.md`
- Gap matrix: `docs/planning/gap-analysis/GAP-MATRIX-2026-06-11.md`
- ADR-005 (ITDD contract tests) — the test shape this feature follows
- ADR-009 (`IndFusion.Ember`) — the event transport the 3-process ingestion split will use
