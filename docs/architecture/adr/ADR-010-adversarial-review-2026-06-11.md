# ADR-010 — Adversarial Review (Liability & Production Safety)

**Date:** 2026-06-11 · **Reviews:** `ADR-010-SIARA-Authentication-Strategies.md` (+ design doc + the
implemented Domain/Infra code) · **Lens:** liability + production safety for a regulated Mexican
banking client integrating with the real CNBV SIARA, which we will not access until deployment.

> **Purpose:** record the full graded findings for counsel and the engineering team. The actionable
> posture is folded into ADR-010 (§ Legal preconditions & residual risk, title/wording fixes, audit
> requirements). This file is the complete analysis. It records, it does not resolve, the legal
> questions — "the lawyers do as they think."

**Headline:** the *technical* no-credential-**storage** design is sound and test-enforced. The
residual exposure is **legal and operational**, and it concentrates almost entirely in
**`AutomatedLogin`** — the mode a bank most wants for 24/7 ingestion and the one the ADR was most
silent on. None of the criticals are code bugs; they are decisions/disclosures the ADR must make
explicit before `AutomatedLogin` (task S6b) is built.

## 🔴 Critical

| # | Finding | Liability | ADR disposition |
|---|---|---|---|
| C1 | No recorded authorization that automating a regulator portal is permitted (no SIARA ToS / Mexican-law review cited). | Operating an unauthorized bot against CNBV. | P1 (deployment gate, written legal sign-off). |
| C2 | `AutomatedLogin` breaks non-repudiation / likely violates personal-non-transferable-credential rules (downloads attributed to a named human/service account). | Legal-evidence chain + account-misuse. | P2 + mandatory-actor audit. |
| C3 | Unattended login against a changeable auth surface + SIARA lockout = self-inflicted account lockout → missed regulatory deadlines → fines. | Compliance failure / availability. | P3 (backoff + circuit-breaker + stop-and-alert). |
| C4 | "Client-controlled technical-legal control" used as a liability shield it cannot provide (shipping the capability is a vendor decision). | False sense of liability transfer. | P1 + Rationale §2 reworded. |

## 🟠 High

| # | Finding | ADR disposition |
|---|---|---|
| H1 | Title was false ("Two … Credential-Free"; now three, and `AutomatedLogin` handles raw creds). | Title fixed → "Three … (No Credential Storage)". |
| H2 | "credential-free / no credentials held" overstated — storage-state contains the **session cookie = bearer credential**. | Table footnote ¹ + bearer-secret reclassification. |
| H3 | "Structural" no-credential guarantee only covers `SiaraSession`, not the credential flow (`ISiaraCredentialSource` / `ISiaraLoginService`). | P5 + Rationale §1 reworded. |
| H4 | "Transient + discarded" unenforceable with .NET `string` (heap/crash/page-file dumps). | P5 (`char[]`, cleared, no serialization). |
| H5 | `ISiaraLoginService` "never registered for persisted creds" is convention, not structure — it IS registered in the general browser DI today. | P6 (structural isolation). |
| H6 | Capturing a post-CAPTCHA/MFA session to drive a bot may circumvent an anti-automation control (distinct legal posture). | P4 (per-mode counsel question). |
| H7 | Safety narrative ("humans handle the unknown auth") applies to 2 of 3 modes; silent on the bot mode operating against the unseen system. | P3/P4 + §Legal preconditions. |

## 🟡 Medium

| # | Finding | ADR disposition |
|---|---|---|
| M1 | No legal/compliance in `Deciders` on a regulatory-liability decision. | Deciders line: legal sign-off REQUIRED + PENDING. |
| M2 | Audit actor (`SiaraSession.AcquiredBy`) is optional, caller-supplied `string?`. | Consequences: mandatory, trustworthy actor identity (data-model follow-up). |
| M3 | No per-document non-repudiation link (audit is session-level only). | Consequences: bind each download to session id + actor + time. |
| M4 | No audit of `ISiaraCredentialSource` reads. | Consequences: audit every credential fetch. |
| M5 | No tamper-evidence / retention requirement for the audit trail. | Consequences: immutable, tamper-evident, retained. |
| M6 | Single-selector `IsAuthenticatedAsync` probe is fragile vs. the unseen site (false pos/neg → garbage scrape or re-auth storm). | P3 + note: multi-signal verification. |
| M7 | Warm-session blast radius; no revocation/rotation/incident playbook beyond `ReleaseAsync`. | P7. |
| M8 | Headless `EnsureValidAsync` can't satisfy a step-up/MFA challenge mid-session. | P3/P4 (fallback to human). |

## 🟢 Low–Medium

| # | Finding | ADR disposition |
|---|---|---|
| L1 | CDP attach code ships regardless of "opt-in" (attack surface a bank may reject). | Note: compile-time/feature-flag exclusion possible. |
| L2 | Playwright tracing/HAR/video can capture the typed password + cookies. | P5 (tracing/HAR off in credentialed modes). |
| L3 | Nothing structurally stops the fake-credential simulator/test driver from targeting the real SIARA host. | P8. |
| L4 | Auth-path logs (session refs, document URLs, case ids) may carry PII under LFPDPPP. | Note in P-set; PII-aware scrubbing on the auth path. |

## Bottom line

For the "free of liability" goal: the highest-leverage items are **C1–C4** (legal gate, non-repudiation,
lockout, vendor-liability framing) plus the honesty fixes **H1/H2** (title, bearer-secret). These are
now documented in ADR-010. The legal questions (P1, P2, P4) are recorded for counsel; the engineering
preconditions (P3, P5, P6, P7, P8) are to be satisfied before/within the relevant tasks.
