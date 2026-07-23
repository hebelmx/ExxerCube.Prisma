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

Two steps, each a plain configuration change followed by a rolling restart:

**Step 1 — introduce the new secret alongside the old one**

1. Generate a new `JwtSecret` value.
2. In each of the three processes' configuration (environment variable, Key Vault
   reference, or `appsettings.json` override — never a committed real secret):
   - Set `ProcessIdentity:JwtSecret` to the **new** value.
   - Move the **old** value into `ProcessIdentity:PreviousJwtSecrets` (a single-element
     array is sufficient for a normal rotation; multiple entries support stacking
     rotations if a previous grace window has not yet drained).
3. Rolling-restart the three processes in any order. At every point during the rollout:
   - A not-yet-restarted process still mints and validates with the old secret.
   - An already-restarted process mints with the new secret and validates against
     `{new, old}` — so it accepts tokens from not-yet-restarted peers.
   - No combination of restarted/non-restarted processes produces a validation failure.

**Step 2 — retire the old secret once the grace window has drained**

1. Wait at least `TokenLifetime` (default 5 minutes) plus the validation `ClockSkew`
   (30 seconds) — comfortably covered by waiting **~6 minutes** after step 1 completes on
   all three processes, so every in-flight token minted with the old secret has expired
   naturally.
2. Remove the old secret from `PreviousJwtSecrets` (set back to `[]`) on each process and
   rolling-restart again.
3. The old secret is no longer accepted anywhere; rotation is complete.

Both steps are configuration-only. No code path needs to change, no process needs to stop
serving traffic, and no in-flight cross-process handoff (Orion → Athena → Reconciliator)
is rejected mid-transit.

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
