# ADR-010: SIARA Authentication — Three Client-Selectable Strategies (No Credential Storage)

**Date**: 2026-06-11
**Status**: Accepted (auth-mechanism design) — **automation modes gated on legal sign-off (§ Legal preconditions)**
**Deciders**: Owner (MVP scope ruling, 2026-06-11) + Development Team — **legal/compliance sign-off REQUIRED and PENDING** for any automation against the real SIARA
**Tags**: siara, authentication, security, ingestion, browser-automation, itdd, mvp, technical-legal-control, liability

> Promotes the planning-level design `docs/planning/gap-analysis/SIARA-AUTH-DESIGN-2026-06.md`
> to an architectural decision. That document holds the full ITDD/DI/config detail and the
> task breakdown (S0–S9); this ADR records the decision and its rationale. It feeds MVP-PATH
> Workstream **1.1** (the real SIARA downloader), whose auth seam this defines.

> ⚠️ **Liability notice.** This ADR's *technical* design (no credential **storage**) is sound and
> test-enforced, but the residual exposure is **legal and operational**, concentrated in
> `AutomatedLogin`. **No automation mode (`AutomatedLogin`, and arguably `SessionPassthrough`) may
> be enabled against the real `siara.cnbv.gob.mx` until the client's legal/compliance has confirmed
> in writing** that automated, credentialed access under the chosen account model is permitted by
> SIARA's terms of use and applicable Mexican law. See **§ Legal preconditions & residual risk**.
> Adversarial review: 2026-06-11 (this document records, not resolves, the legal questions).

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
| `SessionPassthrough` | Ride an already-authenticated browser context handed in from outside Prisma — **storage-state import (default)** or **CDP attach (opt-in)**, chosen by config. | A human/system **outside** Prisma. | A bearer session secret¹ — no username/password. |
| `InteractiveLogin` | A human logs in **once** on the real SIARA page in a headed browser; Prisma captures the authenticated context and keeps it warm for headless downloads. | A human, on **SIARA's own page**. | A bearer session secret¹ — no username/password. |
| `AutomatedLogin` | Prisma drives SIARA's login form **unattended** with credentials sourced at runtime from a client-owned secret store (Key Vault / env / secrets manager) via `ISiaraCredentialSource`, used **transiently** and **never persisted**. Enables 24/7 operation. | Prisma, with the client's vault-held credentials it never stores. | A bearer session secret¹ — no *stored* username/password (creds held only transiently in memory during the login). |

> ¹ **The storage-state is a bearer secret, not a benign "handle."** A Playwright storage-state contains
> the **session cookie**, which fully impersonates the authenticated SIARA user for the session
> lifetime. It must receive credential-grade controls (short TTL, never logged in full, encrypted at
> rest if ever persisted, revocable). Earlier "credential-free / opaque handle" phrasing understated
> this; the precise guarantee is **no credential *storage***, not "no secrets held."

All three produce the **same artifact** — a `SiaraSession` value object that carries **no
username/password** — so they sit behind a single strategy port:

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

## Legal preconditions & residual risk

> Recorded from the 2026-06-11 adversarial review. This section **documents** the exposure for the
> record and for counsel; it does not (and cannot) resolve the legal questions. The technical
> no-storage design is sound; the residual risk is **legal and operational**, concentrated in
> `AutomatedLogin`.

**P1 — Authorization to automate (deployment gate).** No automation mode may be enabled against the
real SIARA until the client's legal/compliance confirms **in writing** that automated, credentialed
access — under the chosen account model — is permitted by SIARA's terms of use and applicable
Mexican law (incl. data-protection / LFPDPPP and any computer-misuse provisions). Building the
capability does not establish that using it is lawful; **config-selection by the client does not
transfer to the client the vendor's liability for shipping the capability.**

**P2 — Non-repudiation / account-sharing.** SIARA accounts are issued to identified, need-to-know
users. `AutomatedLogin` (and a human-handed `SessionPassthrough`) attribute every legal-document
download to a named human/service account and may violate a "personal, non-transferable credential"
policy. Counsel must confirm a sanctioned service/automation-account model before unattended use; the
audit trail must bind each download to the acquiring identity (see Consequences).

**P3 — Self-inflicted lockout / missed-deadline compliance failure.** SIARA has **max-attempt
lockout**, and its auth surface (MFA/CAPTCHA) "may change at any time." An unattended loop driving the
form the day SIARA changes can lock out the bank's account → the bank stops receiving legal
requirements → **missed regulatory deadlines / fines.** Before `AutomatedLogin` (S6b) ships it MUST
implement: lockout/anomaly detection, exponential backoff + circuit-breaker, and a hard **stop-and-
alert-a-human after N failures** (never a retry storm). "Fail-closed" does not by itself prevent this.

**P4 — Anti-automation controls.** If SIARA presents a CAPTCHA/MFA specifically to prevent automation,
capturing the post-challenge session to drive a bot is a different legal posture from "a human logged
in." Flag per-mode for counsel; do not assume "the human cleared it, so we may automate the rest."

**P5 — Credential-in-memory handling (`AutomatedLogin`, before S6b).** "Transient + discarded" must be
enforced, not asserted: credentials handled as cleared `char[]` (not `string`), never logged (the
adapter already logs only password *length*), never serialized, no `ToString`; Playwright
tracing/HAR/video **disabled** in any credentialed mode (they capture the typed password + cookies).
`ISiaraCredentialSource`'s return type must carry these constraints, and the no-leak test must cover
the login path — not only `SiaraSession`.

**P6 — `ISiaraLoginService` isolation.** ✅ **Satisfied (S8.4).** The login driver takes **no** raw
configuration dependency — its constructor is only an `ILogger` plus the secret-free `SiaraHostPolicy`, so
it structurally cannot read persisted credentials; credentials reach it only as transient method parameters
supplied by `ISiaraCredentialSource` through the `AutomatedLogin` provider. Two reflection guardrail tests
(`SiaraLoginServiceIsolationTests`) lock this in: (1) no `ISiaraLoginService` implementation may depend on
`IConfiguration`/`IConfigurationSection`, and (2) the only type that may inject `ISiaraLoginService` is
`AutomatedLoginSiaraSessionProvider` — so a future composition root cannot quietly wire app-config
credentials through a new consumer.

**P7 — Session blast radius & revocation.** The bearer session secret grants full SIARA access for its
lifetime. Mandate short TTLs, encryption at rest if ever persisted (prefer in-memory), and a documented
**revocation/rotation/incident playbook** (kill switch) beyond `ReleaseAsync`.

**P8 — Simulator ↔ production guardrail.** ✅ **Satisfied (S8.5).** The login driver consults a
`SiaraHostPolicy` against the **current page URL** (via the new `IBrowserAutomationAgent.GetCurrentUrlAsync`)
**before entering any credentials**, and **fails closed** on the real `siara.cnbv.gob.mx` host (and its
subdomains) unless a deployment has explicitly set `Siara:AllowProductionHost = true`. That flag defaults to
`false` and is intended to be flipped only after legal/compliance sign-off (P1) — a configuration change,
not a code change. The fake-credential simulator and the E2E harness never set it, so the automation tooling
cannot be repointed at the regulator's portal. (Chosen mechanism: guard inside the login driver, surfaced to
and approved by the owner 2026-06-12.)

**Residual risks accepted / to-confirm:** P1, P2, P4 are **legal questions for counsel** (recorded, not
resolved here). P3, P5, P6, P8 are **engineering preconditions** — now **all satisfied** (P3 in S6b, P5 in
S8.1/8.2, P2 audit in S8.3, P6 in S8.4, P8 in S8.5). **P7** (short session TTL + revocation/rotation/incident
playbook) remains **open**.

## Rationale

1. **No-credential-*storage* guarantee is structural for the session; the credential *path* is
   guarded separately.** No mode persists a username/password, and `SiaraSession` carries no credential
   members (reflection contract test). That structural guarantee covers the **session object**; the
   `AutomatedLogin` credential *flow* (vault-source → transient use → discard) is guarded by the P5
   controls and login-path no-leak tests, which are procedural-plus-tested, not type-enforced. Do not
   conflate "no credential storage" with "no secrets held" (the session secret is a bearer token).
2. **The mode choice is the client's to make, but it does not absolve the vendor.** Passthrough vs.
   interactive vs. automated is a deployment-time, auditable client control — yet offering
   `AutomatedLogin` at all is a vendor decision whose lawfulness is gated by P1. Config-selection
   documents *who chose*, not *that it is lawful*.
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
- Security posture (must-haves): the storage-state is a **bearer session secret** and the vault
  credentials are secrets (never logged in full; storage-state encrypted at rest if persisted, prefer
  in-memory; credentials never persisted at all — see P5/P7). "Fail-closed" prevents *unauthenticated
  scraping* but NOT the lockout failure mode — P3's backoff/circuit-breaker is the control for that.
- Audit / forensics (must-haves, ties to MVP A6): the acquiring **actor identity must be mandatory and
  trustworthy** — *not* the current optional, caller-supplied `SiaraSession.AcquiredBy string?`;
  every `Acquire/EnsureValid/Release` **and every `ISiaraCredentialSource` read** emits an audit event;
  each downloaded document is bound to its session id + actor + timestamp (per-document
  non-repudiation); the audit trail is **immutable, tamper-evident, and retained** per the client's
  financial-record retention obligation. (The data-model change to make actor identity mandatory is a
  follow-up on `SiaraSession`/the audit seam.)
- The reference fake unblocks downstream ingestion tests early; mutation testing scopes the
  providers + resolver after green, excluding the live-Playwright glue.

## Related

- **Adversarial review (liability & production safety), 2026-06-11:** `ADR-010-adversarial-review-2026-06-11.md` (full graded findings; source of § Legal preconditions)
- Design (full detail + tasks S0–S9): `docs/planning/gap-analysis/SIARA-AUTH-DESIGN-2026-06.md`
- MVP path (Workstream 1.1/1.2): `docs/planning/gap-analysis/MVP-PATH-2026-06-11.md`
- Gap matrix: `docs/planning/gap-analysis/GAP-MATRIX-2026-06-11.md`
- ADR-005 (ITDD contract tests) — the test shape this feature follows
- ADR-009 (`IndFusion.Ember`) — the event transport the 3-process ingestion split will use
