# TRACKER — W3 Buildable-Now Security Epic (RC6 3.5 + 3.9)

## ✅ EPIC CLOSED 2026-07-23 — `58b4621e` → `be5f1df9` (4 commits), all stories done, adversarial gate passed post-remediation

**Opened:** 2026-07-23 · **Closed:** 2026-07-23 · **Branch:** Liv · **Baseline:** `d1b27794` (RC1-residuals closed)
**Intended-solution doc:** `docs/planning-artifacts/readiness-challenge/RC6-CROSS-CUTTING-PATH-TO-PRODUCTION.md` §Wave 3, items 3.5 and 3.9 (the only two W3 items tagged `[now]`, no procurement/legal gate).
**Owner selection:** 2026-07-23 — orchestrator asked "which epic"; owner chose "Veriqan W3 buildable-now" (= 3.5 + 3.9 as offered).

## Scope (locked)
- **S1 / RC6-3.5 [Veriqan][now][Degrades][S]** — reference-bundle SHA-256/HMAC signature verification at load time.
- **S2 / RC6-3.9 [Prisma][now][Degrades][M]** — zero-downtime JWT secret rotation: overlapping-key grace period in `IProcessClearanceTokenService` (sign with current, accept current+previous).
- NOT in scope: every other W3 item (3.1–3.4, 3.6–3.8, 3.10–3.16 are biz/ops/legal-gated or separate-security-review backlog).

## Status
| ID | Story | Status |
|----|-------|--------|
| S0 | Ground-truth scout: verify 3.5/3.9 still open; map bundle-load path + clearance-token path | DONE — BOTH CONFIRMED OPEN (3.5 greenfield; 3.9 single static HMAC, no rotation seam) |
| S1 | Veriqan 3.5 bundle signature verification | DONE 2026-07-23 — commit `58b4621e`. Verifier + writer in `Veriqan.Infrastructure.ReferenceData/Integrity/`; wired into BOTH `GetBundleAsync` + `GetChecklistTiersAsync`; fail-closed via existing InvalidBundle path; unsigned+no-key = warn-once compat mode. Orchestrator-verified: ReferenceData.Tests **69/69** (46+23 new), builds 0/0 |
| S2 | Prisma 3.9 JWT rotation grace period | DONE 2026-07-23 — commit `00a062ee`. `PreviousJwtSecrets` validate-only list; `ProcessIdentitySigningKeys.BuildAcceptedKeys` single accept-set source feeding ValidateAsync + Orion/Athena AddJwtBearer; ADR-012 HMAC-rotation addendum (Accepted; asymmetric addendum stays PROPOSED). Orchestrator-verified: builds 0/0 ×4, BrowserAutomation 229/230 (12 new green), Athena wire 3/3, Orion wire 2/3 — both fails proven PRE-EXISTING via stash re-runs |
| S3 | Adversarial-review gate (both stories vs intended design) | DONE 2026-07-23 — 2 skeptics; findings triaged below |
| S1.b | Remediate S1 findings: B1 identity-binding (manifest v2 signed header: institution+generatedAt), M1 TOCTOU (parse from verified bytes), M2 ships-dark (prod template + config validator), minors | DONE 2026-07-23 — commit `be5f1df9`. Manifest v2 (`bundle:` ordinal-checked + `generatedAt:` ISO-8601, HMAC covers header); adapter parses from verified bytes when key set (disk path byte-identical when disabled); BundleHmacKey in Worker appsettings + prod template + VeriqanConfigurationValidator (warn-only). Orchestrator-verified: ReferenceData.Tests **78/78**, validator tests **9/9**, Worker build 0/0 |
| S2.b | Remediate S2 findings: B1 rewrite rotation procedure to 3-phase (docs+XML only, mechanism correct), M1 bearer-level grace wire tests ×2 | DONE 2026-07-23 — commit `0cad7904`. ADR §3 = Phase A/B/C with §3.0 failure-mode table + dated Correction note; options XML corrected; grace wire pin per host (also first IConfiguration list-binding pin). Orchestrator-verified: Athena wire 4/4, Orion wire 3/4 (known pre-existing flake), JWT service 9/9, builds 0/0 |

## S3 adversarial-review triage (2026-07-23)
**S1 (`58b4621e`) — mechanism survives modification attacks; 3 findings fixed in S1.b:**
- B1 BLOCKER: no identity/version binding in signed manifest → whole-directory replay of another institution's (or an older) validly-signed bundle passes. Design-level (matched S1 design as written). Fix: manifest v2 signed header (`bundle:` = institution dir leaf, ordinal-checked; `generatedAt:` ISO-8601). Same-institution rollback replay remains a residual (needs external state/TTL policy) — documented.
- M1 MAJOR: TOCTOU — CSVs re-read from disk after verification (Transient adapter, per-submission window). Fix: VerifyAsync returns verified bytes; adapter parses from them when key set.
- M2 MAJOR: control ships dark — key absent from appsettings.Production.json.template and VeriqanConfigurationValidator. Fix: template entry + validator warn-not-crash.
- Minors fixed: IsNullOrWhiteSpace key gate, dead DecoderFallbackException catch, hex-case doc drift, warn-once-per-Transient-instance doc note. Reviewer verified: 69/69 true, tests honest/non-vacuous, no key-material leaks, gate runs before ALL reads, cancellation honored.

**S2 (`00a062ee`) — mechanism correct + complete (no missed validation sites: Veriqan.Worker bearer is a separate token universe; EfCoreIdentityAdapter unregistered; Reconciliator validates only via forwarder); 2 findings fixed in S2.b:**
- B1 BLOCKER (design flaw, orchestrator's): documented SINGLE-PHASE rotation is not zero-downtime — new-mint→old-validator edge exposed in every restart order (all 3 processes are both minters and validators; accept-sets baked at startup). Fix: 3-phase procedure (pre-stage new as accepted → cut over minting → drain+retire) in ADR addendum + options XML; commit message `00a062ee` claim corrected by the ADR "Correction" note. No production code change.
- M1 MAJOR: no bearer/hub-level test exercises the grace path (Program.cs revert to single-key would pass suite). Fix: grace wire test per host, also pinning IConfiguration list binding.
- Minors → residuals (below): blank-JwtSecret asymmetry between paths (fails closed overall); no weak/stale-previous-secret startup diagnostic (forgotten Phase C = permanent second key, silently).

## Residuals (owner-visible, deliberately NOT fixed in this epic)
- S1: same-institution rollback replay (old signed snapshot of the SAME bank) — needs freshness policy/external state; `generatedAt` header now gives the hook. Per-row imageSha256/documentRefSha256 stay unverified payload (pre-existing). Web.UI appsettings `CsvReferenceData` section has no `BundleHmacKey` entry (defaults to disabled — safe; doc-parity only).
- S2: startup diagnostic for weak (<128-bit) or never-retired previous secrets; blank-JwtSecret path asymmetry. Both misconfig-only, fail closed.
- Asymmetric ES256 per-process keys remain owner-gated (RC6 3.6, ADR-012 addendum PROPOSED).

## Pre-existing reds surfaced (NOT from this epic — owner attention)
- `SiaraHostPolicyTests.Validate_UnparseableUrl_FailsClosed(url: "/relative/path")` FAILS on `Liv` HEAD (proven via stash re-run without epic changes). A fail-closed guard test failing in the Siara host-policy area (legal-gate adjacent) — worth a look.
- `IngestionHubWireTests.Broadcast_DocumentDownloadedEvent_IsReceivedByConnectedClient` times out on HEAD (SignalR TestServer delivery flake; auth-rejection tests pass).

## S0.a scout findings (Veriqan bundle — ground truth)
- Loader: `02 Infrastructure/Veriqan.Infrastructure.ReferenceData/Adapters/CsvReferenceDataAdapter.cs` — `GetBundleAsync` (L97-169, dir resolve L242-247, schema validation L155), `GetChecklistTiersAsync` (L172-238). Options: `CsvReferenceDataOptions.RootDirectory` only; config key `Veriqan:CsvReferenceData:RootDirectory`. DI: `VeriqanReferenceDataServiceCollectionExtensions.AddVeriqanReferenceData`.
- NO integrity mechanism exists (no SHA/HMAC/signature/manifest; `System.Security.Cryptography` unreferenced in project; git -S empty). Per-row `imageSha256`/`documentRefSha256` CSV fields are payload data, never verified — do not confuse.
- Encrypted legal-baseline store (`SqlLegalBaselineStore` + AES converter) is a SEPARATE SQL confidentiality store; does not cover CSV bundle.
- Failure path: adapter `Result.WithFailure` → `BundleBinder.BindAsync` (L65-77) → `BlockedOutcome(BlockReason.InvalidBundle)` = existing fail-closed hook. Optional sections silently `catch → null` → INSUFFICIENT_DATA.
- Tests: `Veriqan.Infrastructure.ReferenceData.Tests` 46 tests; also Orchestration DI/E2E + Worker health-check tests touch RootDirectory presence.

## S1 design (orchestrator-decided)
- Per-file SHA-256 manifest (`bundle-manifest.sha256`: relative-path + hex hash per line, covers every `*.csv` in the bundle dir) + detached HMAC-SHA256 signature of the manifest bytes (`bundle-manifest.hmac`), key from `Veriqan:CsvReferenceData:BundleHmacKey`.
- Key configured → verification REQUIRED: missing manifest/sig, bad HMAC, hash mismatch, or unlisted `*.csv` present → `Result.WithFailure` (fail-closed via existing InvalidBundle path). Key absent → log warning once, proceed (opt-in; matches Degrades severity; existing fixtures/demo unaffected).
- Authoring: `BundleManifestWriter` (same infra project) to generate manifest+sig; document in bundle-authoring guide (from commit `046f6905`).
- No key material in logs/ToString.

## Gotchas / constraints
- RC6 matrix may be STALE (memory: Wave-0/1 + parts of Wave-2 already landed post-RC6). S0 exists to prevent building something Epic 6 already shipped.
- Result<T> + CancellationToken mandatory; no exceptions for business logic. xUnit v3 + Shouldly + NSubstitute. `dotnet test <csproj>` plain.
- Epic 6 lesson: record `ToString` leaks secrets — audit any new secret-bearing types.
- Ember package owns hub coordination; token service impl lives in this repo (verify in S0 which side validates).
- Fail behavior for 3.5 must be honest/fail-closed-aware — no silent fallback to unverified data (Veriqan honesty doctrine).

## S0.b scout findings (Prisma clearance JWT — ground truth)
- Port: `01 Core/Domain/Interfaces/IProcessClearanceTokenService.cs` (Result-based, fail-closed). Sole impl: `02 Infrastructure/Infrastructure.BrowserAutomation/ProcessIdentity/JwtProcessClearanceTokenService.cs` — HS256, single `SymmetricSecurityKey` from `ProcessIdentityOptions.JwtSecret`, mint L90-97, validate L147-159, `ClockSkew=30s`, TTL default 5 min. Singleton via `AddProcessIdentity` (`ProcessIdentityServiceCollectionExtensions.cs:59`).
- Secret read in TWO places per host: (a) `AddProcessIdentity` for the token service; (b) each host's `AddJwtBearer` re-reads it (Orion `Program.cs:179-186`, Athena `Program.cs:221-228`). Reconciliator mints only (no AddJwtBearer). All 3 worker appsettings share placeholder secret under `ProcessIdentity` section.
- Validation surfaces: per-message `ValidateAsync` in `IngestionEventForwarder.cs:99` + `ReconciliationEventForwarder.cs:85`; per-connection JWT bearer + `[Authorize]` policies in Orion/Athena.
- NO multi-key/kid/grace support anywhere (definitive). Naive swap breaks both paths during staggered rollout (5-min tokens signed by old key rejected).
- Tests: `Tests.Infrastructure.BrowserAutomation` (4 direct + ITDD contract inheritor; base = `09 Testing/.../ProcessClearanceTokenServiceContract.cs` 5 tests); HubWire tests in Orion/Athena worker test projects; E2E AllRealWire uses one shared secret const.
- **Design fork resolved:** ADR-012 addendum (`ADR-012-addendum-asymmetric-clearance-keys-2026-06-16.md`) proposes full ES256+kid — that is RC6 **3.6 [biz]**, PROPOSED, owner-gated. S2 does NOT implement it. S2 = minimal HMAC overlapping-key grace (RC6 3.9 [now]), forward-compatible with the addendum.

## S2 design (orchestrator-decided)
- `ProcessIdentityOptions` gains `PreviousJwtSecrets` (list, default empty, validate-only). `JwtSecret` stays the sole minting secret.
- One shared key-set builder (new small static/helper in ProcessIdentity folder) produces the accept-set (current + previous, empty/whitespace filtered, distinct) — used by `JwtProcessClearanceTokenService.ValidateAsync` (`TokenValidationParameters.IssuerSigningKeys`) AND by Orion/Athena `AddJwtBearer` so the two validation paths cannot drift.
- ~~Zero-downtime rotation procedure = config-only: (1) push new secret as `JwtSecret`, old into `PreviousJwtSecrets`, rolling-restart hosts; (2) after TTL+skew drain (~6 min), remove old.~~ **CORRECTED by S3 adversarial review (design flaw, mine):** single-phase swap is NOT zero-downtime — every process both mints and validates, so the first restarted host mints new-signed tokens that not-yet-restarted validators reject. Correct procedure = THREE-phase: (A) `PreviousJwtSecrets=[new]` everywhere, mint stays old, rolling restart; (B) swap `JwtSecret=new`/`Previous=[old]`, rolling restart; (C) after TTL+skew drain, clear list. ADR addendum + options XML doc rewritten accordingly (S2.b).
- No secrets in logs/ToString. appsettings of all 3 workers gain empty `PreviousJwtSecrets: []`.

## Verification log (all runs executed by the orchestrator, not taken from agent prose)
- S1 (`58b4621e`): ReferenceData.Tests 69/69 (46+23); adapter diff inspected (gate before all reads in both load paths).
- S2 (`00a062ee`): BrowserAutomation 229/230; Athena wire 3/3; Orion wire 2/3. BOTH failures stash-proven pre-existing on HEAD (SiaraHostPolicy fail-closed test; Orion broadcast timeout). Diff inspected: single BuildAcceptedKeys helper in ValidateAsync + both AddJwtBearer blocks.
- S2.b (`0cad7904`): Athena wire 4/4; Orion wire 3/4 (known flake only); JWT service 9/9; ADR §3 three-phase + Correction note read and confirmed.
- S1.b (`be5f1df9`): ReferenceData.Tests 78/78; VeriqanConfigurationValidatorTests 9/9; Veriqan.Worker build 0/0.

## Test floors at close
ReferenceData 78 · BrowserAutomation 230 (1 pre-existing red) · Orion wire 4 (1 pre-existing flake) · Athena wire 4 · Orchestration validator 9.
