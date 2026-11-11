# ADR-012 Addendum — Zero-Downtime HMAC Secret Rotation via Overlapping-Key Grace Period

**Status:** Accepted
**Date:** 2026-07-23
**Branch:** `Liv`
**Addendum to:** `ADR-012-Per-Process-Security-Spine.md` (Accepted, 2026-06-13)
**Implements:** RC6 item 3.9 — zero-downtime rotation of the Prisma process-clearance JWT
shared HMAC secret
**Relationship to the asymmetric-keys addendum:** does **not** supersede
`ADR-012-addendum-asymmetric-clearance-keys-2026-06-16.md` (still `PROPOSED`, still
owner-gated). This addendum is a minimal, forward-compatible improvement to the existing
symmetric HMAC scheme; it does not touch algorithm choice, key topology, or `kid`-based
per-process keys. Both addenda can coexist — see §6.

**Correction (2026-07-23, same day, adversarial review):** the rotation procedure
originally documented in §3 below (and described in commit `00a062ee`'s message) was a
**single-phase** "swap `JwtSecret` to new + old into `PreviousJwtSecrets`, rolling-restart
in any order" step, claimed to produce zero validation failures in any restart ordering.
That claim was **wrong** — see §3.0 for the failure mode it missed. §3 has been rewritten
as a **three-phase** procedure. The mechanism in §2 (`PreviousJwtSecrets`,
`ProcessIdentitySigningKeys.BuildAcceptedKeys`) is unaffected and remains correct; only the
*documented operational procedure* for using it has changed.

---

## 1. Problem

`ProcessIdentityOptions.JwtSecret` (`ProcessIdentityOptions.cs:31`) is a single shared
HMAC-SHA256 secret provisioned identically to Orion, Athena, and the Reconciliator. Both
signing paths read it:

- `JwtProcessClearanceTokenService.MintAsync` (`JwtProcessClearanceTokenService.cs:90`)
  and `ValidateAsync` (`JwtProcessClearanceTokenService.cs:147-159`, prior to this
  addendum) built a single `SymmetricSecurityKey` from `_options.JwtSecret`.
- Each worker's connection-level `AddJwtBearer` (`Orion/Program.cs:~182-193`,
  `Athena/Program.cs:~224-235`, prior to this addendum) built the same single key
  independently, for the SignalR hub `[Authorize]` bearer check.

Before this addendum, rotating that shared secret required an **atomic** cutover: every
process had to update its `JwtSecret` and restart at the same instant, or the following
window of failure occurred —

1. Process A restarts first with the new secret.
2. Process A mints a token signed with the new secret.
3. Process B (not yet restarted) validates with the old secret → **rejects** a legitimate,
   freshly-minted token.

Given a rolling restart (the normal deployment mode — see `docs/operations/`), that window
is unavoidable without either downtime (stop-the-world restart of all three processes) or
an overlapping-key grace period. This addendum implements the latter.

---

## 2. Mechanism: Overlapping-Key Grace Period

### 2.1 New configuration

`ProcessIdentityOptions` gains one new property
(`ProcessIdentityOptions.cs`, `PreviousJwtSecrets`):

```csharp
/// <summary>
/// Retired signing secrets still accepted for token VALIDATION during a rotation grace
/// window. JwtSecret remains the sole secret used to MINT new tokens.
/// </summary>
public IReadOnlyList<string> PreviousJwtSecrets { get; set; } = Array.Empty<string>();
```

- **`JwtSecret`** — unchanged role: the *only* secret used to mint new tokens
  (`MintAsync` is untouched by this addendum).
- **`PreviousJwtSecrets`** — validate-only. A token signed with any of these is still
  accepted by `ValidateAsync` and by each worker's hub bearer auth, but no process will
  ever mint a *new* token with one of these values.

This asymmetry (mint from exactly one secret; validate against a set) is the entire
mechanism. It requires no new cryptographic primitive, no new claim, and no change to
`IProcessClearanceTokenService` (the domain port is untouched — this is purely an
`Infrastructure` + host-composition change).

### 2.2 Single source of truth: `ProcessIdentitySigningKeys`

A new static helper,
`ProcessIdentitySigningKeys.BuildAcceptedKeys(ProcessIdentityOptions)`
(`Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.BrowserAutomation/ProcessIdentity/ProcessIdentitySigningKeys.cs`),
computes the accepted key set once:

```csharp
public static IReadOnlyList<SecurityKey> BuildAcceptedKeys(ProcessIdentityOptions options)
{
    // current JwtSecret first, then each non-blank PreviousJwtSecrets entry,
    // de-duplicated by secret value.
}
```

Both validation call sites use this helper — no path recomputes its own key set:

- `JwtProcessClearanceTokenService.ValidateAsync` sets
  `TokenValidationParameters.IssuerSigningKeys = ProcessIdentitySigningKeys.BuildAcceptedKeys(_options)`
  (replacing the old single `IssuerSigningKey` assignment).
- Orion's `AddJwtBearer` (`Prisma.Orion.Worker/Program.cs`) and Athena's `AddJwtBearer`
  (`Prisma.Athena.Worker/Program.cs`) both set
  `options.TokenValidationParameters.IssuerSigningKeys = ProcessIdentitySigningKeys.BuildAcceptedKeys(processIdentityOptions)`
  in place of their previous single-key construction.

This closes the exact drift risk the asymmetric-keys addendum flagged in a different
context (§6.3 of that addendum): the per-message path and the per-connection hub-auth path
must never diverge on which keys they accept. Sharing one helper makes divergence a
compile-time impossibility rather than a code-review discipline.

The Reconciliator only mints (no `AddJwtBearer` registration), so it needs no change
beyond the shared `ProcessIdentityOptions` shape and the config file.

### 2.3 Secret hygiene

`PreviousJwtSecrets` values are never logged. `ProcessIdentityOptions` does not override
`ToString()`, so default `object.ToString()` (the type name) is the only formatting
surface, and no code path in this codebase passes a `ProcessIdentityOptions` instance to a
logger directly — only individual non-secret claims (`ActorId`, `Clearance`, `FileId`) are
ever logged. This property is guarded by a unit test
(`JwtProcessClearanceTokenServiceTests.ProcessIdentityOptions_ToString_DoesNotContainSecretMaterial`).

---

## 3. Rotation Procedure (config-only, no code deploy, no downtime)

### 3.0 Why a single-phase rollout is unsafe

Accept-sets are **baked at process start**: each process reads
`ProcessIdentity:PreviousJwtSecrets` once, at startup, into the singleton
`IOptions<ProcessIdentityOptions>` consumed by `ProcessIdentitySigningKeys.BuildAcceptedKeys`
(§2.2). A process's validation behaviour cannot change again until it is restarted. That is
fine — restarts are an expected part of rotation — but it means the **order** in which hosts
learn the new secret matters, because every process in this system is **both a minter and a
validator**, not a one-directional chain. The mint→validate edges are:

| # | Mints                                                                | Validated by                                        |
|---|------------------------------------------------------------------------|-------------------------------------------------------|
| 1 | Orion — per-message clearance token on each downloaded-document event  | Athena `JwtProcessClearanceTokenService.ValidateAsync` |
| 2 | Athena — hub-connection bearer to Orion's `hubs/ingestion`             | Orion `AddJwtBearer` (connection-level policy check)   |
| 3 | Athena — per-message clearance token on each extraction-completed event | Reconciliator `ValidateAsync`                         |
| 4 | Reconciliator — hub-connection bearer to Athena's `hubs/reconciliation` | Athena `AddJwtBearer` (connection-level policy check) |

A previously documented single-phase procedure said: "set `JwtSecret` = new, move old into
`PreviousJwtSecrets` on all three hosts, then rolling-restart in any order — no combination
of restarted/non-restarted processes produces a validation failure." **That claim is
false.** Consider the first host restarted under that single-phase config change — call it
Athena — the moment it comes back up:

1. Athena's new startup config is `JwtSecret = new`, `PreviousJwtSecrets = [old]`. It now
   **mints** with `new`.
2. Athena mints a per-message clearance token signed with `new` on the next
   extraction-completed event and hands it to the Reconciliator (edge 3 above).
3. The Reconciliator has **not yet restarted** — its accept-set, baked at ITS last startup,
   is still `{old}` only (its `PreviousJwtSecrets` was empty, or whatever it was before this
   rotation began).
4. The Reconciliator's `ValidateAsync` check rejects the `new`-signed token: **the handoff
   drops, fail-closed.** The identical failure shape recurs on edge 2 (Athena's new hub
   bearer to Orion's `hubs/ingestion`, rejected by Orion's not-yet-restarted
   `AddJwtBearer`) and on edges 1/4 depending on which host happens to restart first — the
   defect is not specific to any one edge, it is inherent to letting *any* host mint the new
   secret before *every* host can validate it.

The single-phase step conflates two things that must happen in a strict order across *all*
hosts before anyone is allowed to mint with the new secret: (a) *every* validator must
already accept the new secret, and (b) only then may *any* minter start using it. A rolling
restart of a single "set new secret + old-as-previous" config cannot guarantee (a) happens
before (b), because the very first host to restart under that config starts minting new
immediately while its peers are still validate-old-only.

The fix is to split what was one config change into two, so that the phase in which
validators learn the new secret is *fully complete* (all three hosts restarted) before the
phase in which any host is allowed to mint with it begins.

### 3.1 Phase A — pre-stage the new secret as an accepted (not yet minted) value

1. Generate a new `JwtSecret` value (call it `new`); the current value is `old`.
2. On **all three** processes, set:
   - `ProcessIdentity:JwtSecret` = `old` (**unchanged** — nobody mints `new` yet).
   - `ProcessIdentity:PreviousJwtSecrets` = `[new]`.
3. Rolling-restart the three processes, **in any order** — order does not matter in this
   phase because every mint, restarted or not, is still signed with `old`, and every host
   (restarted or not) still accepts `old` (either as `JwtSecret` pre-restart, or as
   `PreviousJwtSecrets` post-restart). **Zero validation failures possible during this
   phase.**
4. End state, once all three have restarted: every validator accepts `{old, new}`; every
   minter still only mints `old`.

### 3.2 Phase B — cut over minting to the new secret

Only begin once Phase A has completed on **all three** processes (every validator now
accepts `new`).

1. On **all three** processes, set:
   - `ProcessIdentity:JwtSecret` = `new`.
   - `ProcessIdentity:PreviousJwtSecrets` = `[old]`.
2. Rolling-restart the three processes, **in any order**. At every point during this
   rollout, a not-yet-restarted host mints `old` (accepted by everyone, per Phase A's end
   state) and an already-restarted host mints `new` (also accepted by everyone, because
   Phase A already made `new` universally acceptable). **Zero validation failures
   possible.**
3. End state: every minter mints `new`; every validator still accepts `{new, old}` (so any
   token minted moments before a peer's Phase-B restart, still in flight, is not rejected).

### 3.3 Phase C — retire the old secret once the grace window has drained

1. Wait at least `TokenLifetime` (default 5 minutes) plus the validation `ClockSkew`
   (30 seconds) — comfortably covered by waiting **~6 minutes** after the *last* Phase-B
   restart completes, so every in-flight token minted with `old` has expired naturally.
2. On each process, set `PreviousJwtSecrets` = `[]` and rolling-restart again. Mixed states
   during this rollout mint `new` only (Phase B already retired `old` from every minter) and
   every host — restarted or not — still accepts `new` (it's `JwtSecret` everywhere already).
   **Zero validation failures possible.**
3. The old secret is no longer accepted anywhere; rotation is complete.

All three phases are configuration-only; no code path changes, no process stops serving
traffic, and no in-flight cross-process handoff is rejected mid-transit — **provided each
phase is allowed to complete on all three processes before the next phase begins.** The
three-phase shape (stage as accepted → cut over minting → retire) is exactly the standard
key-rotation pattern precisely because rotation must never let "accepted-everywhere" trail
behind "minted-somewhere."

---

## 4. What This Does NOT Do

- It does **not** implement per-process asymmetric keys (`kid`-scoped ECDSA/RSA). That
  remains the separate, `PROPOSED`, owner-gated
  `ADR-012-addendum-asymmetric-clearance-keys-2026-06-16.md`. This addendum operates
  entirely within the existing single shared-HMAC trust boundary; it makes *rotating that
  shared secret* safe, it does not change *who can mint what* (any process holding
  `JwtSecret` at a given moment can still mint any clearance — that limitation is
  unchanged and is exactly what the asymmetric addendum addresses).
- It does **not** add automatic/scheduled rotation (e.g. Key Vault auto-rotation
  polling). The procedure in §3 is manual/config-driven, matching OD-7 of the asymmetric
  addendum ("manual rotation procedure... acceptable for MVP").
- It does **not** change `IProcessClearanceTokenService`, `ClearanceTokenClaims`, the
  `ProcessClearanceTokenServiceContract` ITDD base, or any per-message forwarder
  (`IngestionEventForwarder`, `ReconciliationEventForwarder`) — all of those go through
  `ValidateAsync` unchanged and are unaffected by the accepted-key-set widening.

---

## 5. Compatibility with the Asymmetric-Keys Addendum

The two addenda are complementary, not competing:

- If/when the asymmetric proposal is implemented, the *mint* side moves from
  `SymmetricSecurityKey` to a per-process `ECDsaSecurityKey`
  (`ADR-012-addendum-asymmetric-clearance-keys-2026-06-16.md` §5.3). The *validate* side
  moves from a fixed key list to an `IssuerSigningKeyResolver` keyed by `kid`
  (same addendum, §6.3 discusses exactly this for hub bearer auth).
- `PreviousJwtSecrets` / `ProcessIdentitySigningKeys.BuildAcceptedKeys` would simply become
  vestigial (HMAC-specific) once/if `JwtSecret` is fully retired under Phase M3 of that
  addendum's migration plan — no conflict, no code that needs to be un-written first. The
  overlapping-key *pattern* (grace-list of retiring credentials, single-source-of-truth
  helper, config-only rotation) generalizes directly to asymmetric `kid` rotation (see that
  addendum's §8.1, "kid-based rotation" — the same two-step add-then-drain-then-remove
  shape).
- Nothing in this addendum forecloses OD-1 through OD-7 of the asymmetric addendum; all
  remain open pending owner ruling.

---

## References

- `docs/architecture/adr/ADR-012-Per-Process-Security-Spine.md` — parent ADR
- `docs/architecture/adr/ADR-012-addendum-asymmetric-clearance-keys-2026-06-16.md` — proposed, owner-gated, unaffected
- `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.BrowserAutomation/ProcessIdentity/ProcessIdentityOptions.cs`
- `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.BrowserAutomation/ProcessIdentity/ProcessIdentitySigningKeys.cs`
- `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.BrowserAutomation/ProcessIdentity/JwtProcessClearanceTokenService.cs`
- `Prisma/Code/Src/CSharp/04 Services/Orion/Prisma.Orion.Worker/Program.cs`
- `Prisma/Code/Src/CSharp/04 Services/Athena/Prisma.Athena.Worker/Program.cs`
- `Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Tests.Infrastructure.BrowserAutomation/JwtProcessClearanceTokenServiceTests.cs`
- `Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Tests.Infrastructure.BrowserAutomation/ProcessIdentitySigningKeysTests.cs`
