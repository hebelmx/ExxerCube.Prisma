# Legacy Portal Observations — External Probe

**Date:** 2026-06-07
**Scope:** Customer-facing observations from outside the firewall, using
standard browser developer tooling and synthetic-browser scripting
(Playwright). No internal access required, no credentials used.
**Purpose:** Background notes for proposal scoping. Pitchable as evidence of
observable modernization opportunities; not intended as a defect inventory.

---

## Observable Signals

The following were captured directly from production response payloads and
client-side runtime behavior on `bancanetempresarial.banamex.com.mx` and
`bancanetempresarial.citibanamex.com.mx`:

| Signal | Observation | Implication |
| --- | --- | --- |
| Static asset stack | `jquery-1.4.2.min.js` shipped to the customer | Frontend dependency last updated January 2010; predates the HTML5 Geolocation, Permissions, and Fetch APIs that the same page now relies on. |
| URL routing | Production traffic served from both `/web-app/` (modern Angular SPA) and `/bestbanking/spanishdir/*.htm` (legacy servlet) under the same public host | Two parallel front-ends coexist in customer surface area; visit-by-visit the user may land on either. |
| Response security headers | No `Permissions-Policy` header on the document response | Browsers receive no explicit feature-policy declaration; geolocation availability depends entirely on individual user permission grants. |
| CSP composition | `Content-Security-Policy: frame-ancestors` enumerates explicit `uat.`, `uat2.`, `dev.`, and `empresarial.` hostnames in the production response | Non-production hostnames are referenced in policies served to production customers — operational hygiene signal. |
| Client-side error reporting | Geolocation failures surface as `Uncaught (in promise): GeolocationPositionError: {}` in the Angular zone error handler, with no user-actionable detail | Customer-facing remediation reduces to a generic "Activar Localización" button, regardless of underlying cause (browser permission, OS positioning, or network). |
| Telemetry integration | Heavy third-party anti-fraud beacon traffic (`frames.banamex.com.mx`) is well-instrumented | Anti-fraud observability appears mature; functional observability of customer-facing flows appears to lag. |

These were captured externally in ~20 minutes of probe work without
authentication. A continuous synthetic monitoring pipeline would surface them
automatically on every deploy.

---

## Industry Benchmark

For a Tier-1 corporate-banking SPA in 2026, the prevailing baseline is:

- Front-end dependencies pinned within a 2-year currency window with automated
  upgrade PRs (Renovate / Dependabot).
- Single SPA codebase per customer surface; legacy paths retired or
  redirect-bridged, not coexisting.
- Response headers include `Permissions-Policy`, `Strict-Transport-Security`,
  `Content-Security-Policy` with non-production hostnames stripped at edge.
- Customer-facing error surfaces include a correlation ID and a typed cause
  ("positioning unavailable" vs. "permission denied" vs. "timeout") so support
  triage is one round-trip, not many.
- Synthetic browser checks (Datadog, Checkly, Playwright-on-cron, etc.) run
  the entire login → core-transaction → logout journey on every deploy across
  the supported browser matrix, gating release.

The gap between this baseline and what is externally visible today is the
**actionable surface** for a modernization or compliance-automation
engagement.

---

## Capability Investments Implied

These are framed as constructive opportunities, not deficiencies. Each is
verifiable from outside the firewall and quantifiable as customer-experience
KPIs:

1. **Synthetic monitoring of customer journeys.** Headless browser checks
   exercising login + a representative non-presential operation, every commit
   and every 5 minutes in production. Catches the class of defect described
   above before customers raise tickets.
2. **Front-end dependency currency program.** Automated PRs against a pinned
   currency window, with regression gates supplied by item (1).
3. **Customer-facing error taxonomy.** Replace generic modals with
   correlation-IDed messages mapped to the underlying error code. Reduces
   first-call-resolution time and the volume of "ya intenté todo" support
   contacts.
4. **Response-header hardening pipeline.** `Permissions-Policy`, scoped
   `Content-Security-Policy`, environment-segregated frame ancestor lists.
   Suitable for CI policy-as-code enforcement.
5. **Legacy path retirement plan.** Mapping of `/bestbanking/*` endpoints
   still in customer traffic, with redirect-bridge timelines.

Items (1) and (3) alone have direct CONDUSEF-complaint-volume implications
and are defensible on operational risk and customer-satisfaction grounds
without requiring an underlying platform rewrite.

---

## Reproducibility

Every observation in this memo can be reproduced by any third party with a
browser and an unauthenticated visit to the public login URLs. The probe
chain was:

1. Browser DevTools Network tab → inspect `Content-Security-Policy`,
   `Permissions-Policy`, and `Last-Modified` headers on the document request.
2. DevTools Sources → enumerate `<script src>` tags, observe declared
   versions.
3. DevTools Console → call `navigator.geolocation.getCurrentPosition` with a
   trivial wrapper, observe the error-code surface area.
4. Optional: replicate (1)–(3) headlessly with Playwright and a 30-line
   script for unattended re-runs.

No credentials, no internal documentation, no privileged tooling were used.
That this is achievable externally is itself a signal about the maturity of
public-facing observability discipline.

---

## Use in Proposal

These observations are intended to inform proposal scoping and discovery
conversations. They are deliberately framed as **observable signals** and
**capability investments**, not defect calls. The strongest proposal posture
references them as evidence that a modernization-automation capability gap
exists and is externally visible — without naming individuals, contractors,
or specific code paths — and offers to close it.
