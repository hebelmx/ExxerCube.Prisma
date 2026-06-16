# ADR-012 Addendum — Asymmetric Per-Process Clearance Signing Keys

**Status:** PROPOSED
**Date:** 2026-06-16
**Branch:** `Kt2` (proposal only — no production code changed)
**Addendum to:** `ADR-012-Per-Process-Security-Spine.md` (Accepted, 2026-06-13)
**Implements:** GitHub issue #2, item #4 — "Hardening: per-process asymmetric signing keys"
**Owner gate:** implementation blocked pending owner rulings on Open Decisions (§9)

---

## 1. Context

### 1.1 Current state

ADR-012 §Consequences (line 38) records the accepted limitation verbatim:

> "Symmetric HMAC means any process holding the shared secret can mint any clearance —
> clearance separation is honesty-by-configuration within a single trust boundary
> (asymmetric per-process keys are a future hardening)."

Concretely:

- `ProcessIdentityOptions.JwtSecret` is a single shared HMAC-SHA256 key provisioned
  identically to all three pipeline processes (`ProcessIdentityOptions.cs:31`).
- `JwtProcessClearanceTokenService.MintAsync` signs with
  `new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSecret))` and
  `SecurityAlgorithms.HmacSha256` (`JwtProcessClearanceTokenService.cs:90-91`).
- `ValidateAsync` accepts any token whose HMAC verifies against that same shared key
  (`JwtProcessClearanceTokenService.cs:147-159`), regardless of which process produced it.
- The three worker hosts inject `ProcessIdentity:JwtSecret` from config/environment:
  `Prisma.Orion.Worker/Program.cs:57`, `Prisma.Athena.Worker/Program.cs:134`,
  `Prisma.Reconciliator.Worker/Program.cs:203`.
- The E2E gate and AllRealWire test harness each inject a single `SharedJwtSecret`
  constant to all three hosts:
  `MaxFidelityGateE2EBase.cs:63` (`SharedJwtSecret`), `AllRealWireThreeHostE2ETests.cs:77`.
- The domain port `IProcessClearanceTokenService` and value type `ClearanceTokenClaims`
  (`IProcessClearanceTokenService.cs:23-104`) make no assumption about the signing
  algorithm — this is the seam this proposal preserves.

### 1.2 Threat model gap

With the shared symmetric key, all three processes are **cryptographically equivalent
signers**. The current `clearance` claim (e.g. `"Download"`) is enforced only by the
`ProcessIdentityOptions.Clearance` config value read at mint time. A compromised
Reconciliator process — which holds the shared key — can mint a `clearance=Download`
token for any `file_id` and inject a forged `DocumentDownloadedEvent` directly into
Athena's `IngestionEventForwarder` as if it originated from Orion.

This proposal addresses that gap.

---

## 2. Goal and Threat Model Delta

### 2.1 What asymmetric signing buys

Under the proposed scheme each process has a **unique private signing key** and every
other process holds only the corresponding **public verification key**. The verification
key cannot be used to mint tokens.

Concrete gains:

| Property | Symmetric HMAC (current) | Asymmetric (proposed) |
|---|---|---|
| Compromised verifier can mint sender tokens | YES (holds shared key) | NO (holds only public key) |
| Sender identity is cryptographically bound | NO (any holder can impersonate) | YES (only private-key holder can sign) |
| Non-repudiation per sender | NO | YES — token carries `kid` that maps to a specific sender's public key |
| Blast radius of a single process compromise | All three clearances | That process's own clearance only |

### 2.2 What it does NOT fix

- **A compromised SENDER process** can still mint clearance tokens for its own clearance
  level. A compromised Orion Downloader can still forge `clearance=Download` tokens.
  Asymmetric signing limits blast radius per sender; it does not stop the sending process
  itself from misbehaving.
- **Token replay** within `TokenLifetime`. The existing `jti`-based replay guard
  (`IClearanceReplayGuard`) remains the correct mitigation for within-lifetime replay.
  This proposal is orthogonal to that guard.
- **Hub-connection authentication** (`[Authorize]` on `/hubs/ingestion` and
  `/hubs/reconciliation`). That mechanism uses the same `JwtSecret` for bearer auth today
  (`Orion/Program.cs:73-89`, `Athena/Program.cs:131-144`). It is a separate concern
  addressed in the hub-auth section of ADR-012 §Decision 3 (connection-scope tokens).
  Migration of the hub bearer auth to asymmetric keys is a follow-up — see Open Decision
  OD-6.

---

## 3. Algorithm Choice

**Recommended: ES256 (ECDSA with P-256 curve, SHA-256 hash)**

Rationale:

| Criterion | RS256 (RSA 2048-bit) | ES256 (ECDSA P-256) |
|---|---|---|
| Key size (private) | ~1700 bytes PEM | ~121 bytes PEM |
| Signature size | 256 bytes | ~64 bytes (fixed) |
| Generation speed | Slow (RSA keygen ~100ms) | Fast (~1ms) |
| Verification speed | Fast | Fast |
| Standard support | Ubiquitous | RFC 7518 §3.4, widely supported |
| .NET support | Full (`ECDsa`, `Microsoft.IdentityModel.Tokens`) | Full — same |
| NIST/CNBV compliance | FIPS 186 | FIPS 186-4 (P-256 is NIST-approved) |

ES256 was chosen because:
1. Smaller key/signature material reduces config payload and PEM storage size.
2. `Microsoft.IdentityModel.Tokens.ECDsaSecurityKey` + `SecurityAlgorithms.EcdsaSha256`
   are available in the existing `Microsoft.IdentityModel.Tokens` package (already
   referenced by `JwtProcessClearanceTokenService.cs:1-10`). No new NuGet dependency.
3. P-256 keys are fast to generate deterministically for tests (vs RSA keygen cost in CI).

RS256 is the safe fallback if the client's compliance requirements mandate RSA; the
code changes are identical except for the key type and algorithm constant.

---

## 4. Key Topology

### 4.1 Per-process signing keys

Each pipeline process owns exactly one private signing key and one key id (`kid`):

| Process | Signs | `kid` (proposed) | Clearance minted |
|---|---|---|---|
| Orion Downloader | `DocumentDownloadedEvent.ClearanceToken` | `orion-downloader-v1` | `Download` |
| Athena Extractor | `ExtractionCompletedEvent.ClearanceToken` | `athena-extractor-v1` | `Extract` |
| Reconciliator | (terminal; no downstream handoff) | `reconciliator-v1` | `Reconcile` |

The Reconciliator signs its own hub-connection token (connection-scope, `file_id=Guid.Empty`)
but does not mint handoff tokens for downstream consumers. Orion and Athena are the
operational minters.

### 4.2 Per-edge public key trust sets

Each verifier is configured with only the public key(s) of the sender(s) it accepts on
that specific edge. This is the cryptographic expression of the existing single-equality
clearance check.

| Verifier | Edge | Trusted public keys (by `kid`) |
|---|---|---|
| Athena `IngestionEventForwarder` | Orion → Athena | `orion-downloader-v1` only |
| Reconciliator `ReconciliationEventForwarder` | Athena → Reconciliator | `athena-extractor-v1` only |
| Orion hub `[Authorize]` | Reconciliator connection | `reconciliator-v1` (connection token) |
| Athena hub `[Authorize]` | Athena Extractor self-connection | `athena-extractor-v1` |

The verifier loads the public key set at startup and selects the key to use for validation
by matching the JWT `kid` header claim.

### 4.3 `kid` in the JWT header

The minter sets `kid` in the JWT header so that the verifier can select the correct
public key without trying all known keys:

```csharp
// In JwtProcessClearanceTokenService.MintAsync (proposed change):
var signingCredentials = new SigningCredentials(privateKey, SecurityAlgorithms.EcdsaSha256);
signingCredentials.Key.KeyId = _options.SigningKeyId;  // e.g. "orion-downloader-v1"
```

Verifier-side:

```csharp
// TokenValidationParameters.IssuerSigningKeyResolver selects key by kid:
IssuerSigningKeyResolver = (token, secToken, kid, _) =>
    _options.TrustedPublicKeys.TryGetValue(kid, out var k) ? [k] : []
```

A token whose `kid` is not in the trust set is rejected before signature verification.

---

## 5. Config and Key Distribution

### 5.1 Proposed `ProcessIdentityOptions` shape

The existing `ProcessIdentityOptions` (`ProcessIdentityOptions.cs:22-59`) is extended with
asymmetric fields while the `JwtSecret` field is **retained but deprecated** to enable the
dual-validation migration window (§6):

```csharp
public sealed class ProcessIdentityOptions
{
    public const string SectionName = "ProcessIdentity";

    // --- DEPRECATED (symmetric HMAC; retained for migration window) ---
    /// <summary>Deprecated: shared HMAC-SHA256 secret. Set to empty once asymmetric is live.</summary>
    [Obsolete("Use SigningPrivateKeyPem + TrustedPublicKeys instead.")]
    public string JwtSecret { get; set; } = string.Empty;

    // --- NEW: asymmetric fields ---
    /// <summary>
    /// PEM-encoded ECDSA P-256 private key for this process (used by MintAsync).
    /// Example env var: ProcessIdentity__SigningPrivateKeyPem
    /// </summary>
    public string SigningPrivateKeyPem { get; set; } = string.Empty;

    /// <summary>Key id stamped in the JWT kid header. Must match a key in peers' TrustedPublicKeys.</summary>
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>
    /// Map of kid → PEM-encoded ECDSA P-256 public key for each trusted upstream sender.
    /// Keyed by the sender's SigningKeyId. Each verifier only needs the senders on its edge.
    /// Example config section:
    ///   ProcessIdentity:TrustedPublicKeys:orion-downloader-v1 = "-----BEGIN PUBLIC KEY-----..."
    /// </summary>
    public Dictionary<string, string> TrustedPublicKeys { get; set; } = [];

    // --- Unchanged ---
    public string JwtIssuer  { get; set; } = "prisma-pipeline";
    public string JwtAudience { get; set; } = "prisma-pipeline";
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(5);
    public ProcessClearance Clearance { get; set; } = ProcessClearance.Download;
}
```

### 5.2 Key provisioning per environment

**Development / local:**

Generate a per-developer key pair using `dotnet` or `openssl`. Store in
`appsettings.Development.json` or user secrets. A helper script
(e.g. `scripts/dev/generate-process-keys.ps1`) should generate and print the three
key pairs on first run. The public key of each process goes into each verifier's
`TrustedPublicKeys` dictionary in the dev config.

**CI / tests:**

A deterministic test key fixture (`TestProcessKeyPairFixture`) generates three
in-memory `ECDsa` key pairs once per test session (fast, ~1ms each) and injects them
via `IWebHostBuilder.ConfigureAppConfiguration`. This replaces the current
`SharedJwtSecret` constant in `MaxFidelityGateE2EBase.cs:63` and
`AllRealWireThreeHostE2ETests.cs:77`. See §7 for details.

**Production / staging (Docker/Kubernetes):**

- Mount each private key as a Kubernetes `Secret` projected into the container at a
  well-known path, e.g. `/run/secrets/process-identity/signing-key.pem`.
- Alternatively use an Azure Key Vault config provider reference:
  `ProcessIdentity:SigningPrivateKeyPem = @Microsoft.KeyVault(SecretUri=...)`.
- The public keys of upstream senders are **not sensitive** (they are verification-only)
  and can safely live in `appsettings.json` or a ConfigMap. Only private keys require
  Secret storage.
- Each process's `TrustedPublicKeys` map only needs the public keys of its immediate
  upstream senders (one entry per edge), not all three processes' keys.

### 5.3 New `JwtProcessClearanceTokenService` mint/validate sketch

The port `IProcessClearanceTokenService` (`IProcessClearanceTokenService.cs:23`) is
unchanged — this is an implementation swap, not a port change.

```csharp
// MintAsync (asymmetric path):
var ecdsa = ECDsa.Create();
ecdsa.ImportFromPem(_options.SigningPrivateKeyPem);
var privateKey = new ECDsaSecurityKey(ecdsa) { KeyId = _options.SigningKeyId };
var creds = new SigningCredentials(privateKey, SecurityAlgorithms.EcdsaSha256);
// ... rest of JwtSecurityToken construction unchanged ...

// ValidateAsync (asymmetric path):
var validationParams = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKeyResolver = (_, __, kid, ___) =>
    {
        if (!_options.TrustedPublicKeys.TryGetValue(kid, out var pem)) return [];
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);
        return [new ECDsaSecurityKey(ecdsa) { KeyId = kid }];
    },
    ValidateIssuer    = true,  ValidIssuer   = _options.JwtIssuer,
    ValidateAudience  = true,  ValidAudience = _options.JwtAudience,
    ValidateLifetime  = true,
    ClockSkew         = TimeSpan.FromSeconds(30),
};
```

The `ECDsa.ImportFromPem` call is available on .NET 5+ (`System.Security.Cryptography`)
with no additional NuGet package. The `ECDsaSecurityKey` wrapper is in
`Microsoft.IdentityModel.Tokens` (already referenced).

**Performance note:** `ECDsa.Create()` + `ImportFromPem` per validation call is
inexpensive (~0.1ms) but can be cached. A `ConcurrentDictionary<string, ECDsaSecurityKey>`
keyed by `kid` avoids re-parsing PEM on every token. This is an implementation detail
inside `JwtProcessClearanceTokenService` and does not affect the port contract.

---

## 6. Migration / Backward Compatibility

### 6.1 Recommended strategy: dual-validation window, then hard cutover

A three-phase rollout:

**Phase M1 — Implement asymmetric service, add dual-validation (no config change required)**

Add `EcdsaProcessClearanceTokenService` (new class, same port). The existing
`JwtProcessClearanceTokenService` is kept unchanged. `AddProcessIdentity` registers the
asymmetric implementation when `ProcessIdentity:SigningPrivateKeyPem` is non-empty, and
falls back to the symmetric implementation otherwise. This allows incremental config
migration without a flag day.

**Phase M2 — Roll out new config per process (staggered or simultaneous)**

Set `SigningPrivateKeyPem`, `SigningKeyId`, and each process's `TrustedPublicKeys` entries
in the deployment config. Because both implementations coexist, a mixed deployment
(one process on asymmetric, others on symmetric) can be tested in staging.

Dual-validation during the window: the asymmetric implementation can optionally
attempt HMAC fallback validation if the `kid` in the token is absent and
`JwtSecret` is still configured. This permits in-flight tokens minted by the old code to
drain before the symmetric path is disabled. Given the 5-minute `TokenLifetime`, the
drain window is short.

**Phase M3 — Disable symmetric fallback**

Once all three processes are confirmed on asymmetric keys, set `JwtSecret = ""` in
each process's config. The implementation fails fast (config guard) if neither path is
configured.

### 6.2 Monolith composition path

`ProcessingOrchestrator` (`03 Orchestration`) composes the three pipeline stages
in-process for the Web.UI host. It does not use cross-process JWT handoff tokens today —
clearance tokens ride `DocumentDownloadedEvent` and `ExtractionCompletedEvent`, which in
the monolith path flow through the local `EventPublisher` without SignalR. The
`IProcessClearanceTokenService` is not called on the monolith path; the in-memory
`FakeProcessClearanceTokenService` (used in unit tests) satisfies DI. Migration: no
change required to the monolith composition.

### 6.3 Hub bearer auth (connection-scope tokens)

The hub `[Authorize]` JWT bearer middleware reads the signing key directly from
`ProcessIdentityOptions.JwtSecret` in `Orion/Program.cs:89` and `Athena/Program.cs:144`
— not through `IProcessClearanceTokenService`. Migrating hub auth to asymmetric keys
requires updating those two `AddAuthentication` / `AddJwtBearer` registrations to use an
`IssuerSigningKeyResolver` that reads from `TrustedPublicKeys`. This is
straightforward but touches the ASP.NET Core auth pipeline separately from the
per-message token validation path. Recommended: defer hub bearer migration to a
follow-up commit after per-message asymmetric tokens are confirmed working (see
Open Decision OD-6).

---

## 7. Test Impact

### 7.1 `ProcessClearanceTokenServiceContract` (ITDD base)

The abstract base in `Tests.Domain.Interfaces` does not test the algorithm — it tests the
port contract: mint + round-trip validate, wrong clearance returns failure, expired token
returns failure, `file_id` mismatch returns failure, cancelled token returns
`ResultExtensions.Cancelled`. This contract applies equally to the symmetric and
asymmetric implementations.

Action required: **no change to the contract base itself**. Add a new inheritor
class `EcdsaProcessClearanceTokenServiceContractTests` (parallel to the existing
`JwtProcessClearanceTokenServiceContractTests` at
`Tests.Infrastructure.BrowserAutomation/JwtProcessClearanceTokenServiceContractTests.cs`),
constructing the SUT with a test-generated P-256 key pair.

### 7.2 `JwtProcessClearanceTokenService` unit tests

The existing symmetric tests remain green and continue to document the symmetric
implementation's behaviour. They are not deleted during the migration window.

### 7.3 E2E gate harnesses (`MaxFidelityGateE2EBase`, `AllRealWireThreeHostE2ETests`)

Currently both harnesses use a single `SharedJwtSecret` constant injected into all three
hosts (lines cited in §1.1). Under asymmetric keys this is replaced by a
**test key-pair fixture** pattern:

```csharp
// 09 Testing/TestProcessKeyPairFixture.cs (proposed new shared fixture)
public static class TestProcessKeyPairFixture
{
    // Generated once at class-init time — fast, deterministic, no IO.
    public static readonly (string PrivPem, string PubPem, string Kid) Orion
        = GeneratePair("orion-downloader-v1");
    public static readonly (string PrivPem, string PubPem, string Kid) Athena
        = GeneratePair("athena-extractor-v1");
    public static readonly (string PrivPem, string PubPem, string Kid) Reconciliator
        = GeneratePair("reconciliator-v1");

    private static (string, string, string) GeneratePair(string kid)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (ecdsa.ExportECPrivateKeyPem(), ecdsa.ExportSubjectPublicKeyInfoPem(), kid);
    }
}
```

Each `GateOrionApp` / `GateAthenaApp` / `GateReconciliatorApp` constructor receives its
own private key and the public keys of its upstream senders via the config injection
dictionary:

```csharp
// Orion host (mints; no TrustedPublicKeys needed for ingestion edge):
["ProcessIdentity:SigningPrivateKeyPem"] = TestProcessKeyPairFixture.Orion.PrivPem,
["ProcessIdentity:SigningKeyId"]         = TestProcessKeyPairFixture.Orion.Kid,

// Athena host (validates Orion tokens; mints for Reconciliator):
["ProcessIdentity:SigningPrivateKeyPem"]                        = TestProcessKeyPairFixture.Athena.PrivPem,
["ProcessIdentity:SigningKeyId"]                                = TestProcessKeyPairFixture.Athena.Kid,
["ProcessIdentity:TrustedPublicKeys:orion-downloader-v1"]       = TestProcessKeyPairFixture.Orion.PubPem,

// Reconciliator host (validates Athena tokens):
["ProcessIdentity:SigningPrivateKeyPem"]                        = TestProcessKeyPairFixture.Reconciliator.PrivPem,
["ProcessIdentity:SigningKeyId"]                                = TestProcessKeyPairFixture.Reconciliator.Kid,
["ProcessIdentity:TrustedPublicKeys:athena-extractor-v1"]       = TestProcessKeyPairFixture.Athena.PubPem,
```

The `MintToken` helper in `MaxFidelityGateE2EBase.cs:397` (currently mints with the
shared HMAC key) is updated to use the relevant test private key instead.

### 7.4 `ReconciliationHubWireTests` and `AthenaWorkerApplication`

`ReconciliationHubWireTests.cs:41` mints tokens using `AthenaWorkerApplication.TestJwtSecret`
(`AthenaWorkerApplication.cs:22`). These tests only exercise the hub auth bearer path
(connection-scope tokens). Migration: supply `SigningPrivateKeyPem` and
`TrustedPublicKeys` entries in `AthenaWorkerApplication`'s config override dictionary.
The hub bearer `AddJwtBearer` block in `Athena/Program.cs` must also be updated to use
`IssuerSigningKeyResolver` (see §6.3 and Open Decision OD-6).

### 7.5 Forwarder tests — unaffected

`IngestionEventForwarder` and `ReconciliationEventForwarder` unit tests inject
`IProcessClearanceTokenService` via `FakeProcessClearanceTokenService` (NSubstitute).
They never touch the signing algorithm. No changes required.

---

## 8. Blast Radius and Key Rotation

### 8.1 `kid`-based rotation

The `kid` suffix `-v1` is a rotation slot. To rotate Orion's key:

1. Generate `orion-downloader-v2` key pair.
2. Add `ProcessIdentity:TrustedPublicKeys:orion-downloader-v2` to Athena's config (alongside `v1`).
3. Update Orion's `SigningPrivateKeyPem` to the new key and `SigningKeyId` to `v2`.
4. Once all in-flight v1 tokens have expired (max `TokenLifetime` = 5 minutes), remove
   the `v1` entry from Athena's `TrustedPublicKeys`.

No code change required for rotation — it is a config-only operation.

### 8.2 Key compromise response

If a private signing key is compromised:

- **Remove** the corresponding public key from all verifier `TrustedPublicKeys` maps
  immediately. Any token signed with the compromised key is rejected on next validation
  (within seconds for live traffic; within `TokenLifetime` for tokens already in flight).
- **Rotate** the affected process's key pair (§8.1 procedure).
- **Blast radius:** limited to the clearance level of the compromised process. A
  compromised Orion private key allows forging `clearance=Download` tokens only; the
  Athena forwarder will not accept `clearance=Extract` or `clearance=Reconcile` tokens
  from it because they carry Athena's `kid`, and Orion has no access to Athena's
  private key.

### 8.3 Revocation

JWT has no revocation primitive. Within-lifetime revocation requires either:
- Removing the public key immediately (blocks all tokens from that sender including
  legitimate ones — a circuit-breaker, not graceful revocation).
- A `jti` denylist in the forwarder's `IClearanceReplayGuard` (already architected but
  not yet enforcement-populated). This is the recommended fine-grained path.

For MVP, removing the public key is the only available revocation mechanism. The
`jti` denylist approach is deferred.

---

## 9. Effort and Risk Estimate

**T-shirt size: M (medium)**

Work breakdown:
- New `EcdsaProcessClearanceTokenService` implementation: ~120 lines. (S)
- `ProcessIdentityOptions` new fields + `AddProcessIdentity` registration logic: ~30 lines. (XS)
- `TestProcessKeyPairFixture` + E2E harness updates (3 host classes, 2 base classes): ~80 lines. (S)
- Hub bearer auth migration (`AddJwtBearer` in Orion/Athena `Program.cs`): ~30 lines. (XS — but only if OD-6 is in scope)
- New ITDD contract test inheritor: ~50 lines. (XS)
- Docs/config examples: ~1 day.

**Main risks:**

| Risk | Likelihood | Mitigation |
|---|---|---|
| PEM import unavailable on target runtime | Low — .NET 10, `ImportFromPem` is .NET 5+ | Verify once on the build box |
| Clock skew causes flaky token expiry in CI | Low — same as today; 30s ClockSkew retained | Pre-existing mitigation unchanged |
| Dual-validation window is never cleaned up | Medium — common in practice | Phase M3 must be tracked as a backlog item, not a future "might do" |
| Hub bearer auth migration out-of-sync | Medium — if deferred, mixed signing in two code paths | Explicit OD-6 + bounded defer window |
| Key distribution complexity in Kubernetes | Low for dev/staging; Medium for prod | Plain env-var injection for MVP; KV upgrade tracked |

**Do-now vs defer recommendation:**

**Defer until after MVP demo gate is cleared.** The symmetric HMAC is an accepted
limitation within the existing single-host trust boundary (ADR-012 §Consequences).
Orion, Athena, and the Reconciliator are all internal pipeline services in a private
network segment; the threat of an internal process forging another's clearance is a
hardening concern, not a functional blocker. The MVP gate is the next checkpoint.

**After the MVP gate:** this is the **smallest correct first slice** for the
first implementation commit:

1. `EcdsaProcessClearanceTokenService` + updated `ProcessIdentityOptions` (new fields,
   `JwtSecret` retained).
2. `AddProcessIdentity` updated to select implementation based on presence of
   `SigningPrivateKeyPem`.
3. `TestProcessKeyPairFixture` + E2E harness config injection (no `SharedJwtSecret`).
4. New ITDD contract inheritor for `EcdsaProcessClearanceTokenService`.
5. Phase M2: dev `appsettings.Development.json` + key-gen helper script.
6. Hub bearer auth migration (OD-6) as a follow-up commit.

This slice can be implemented and fully verified (including E2E gate) without touching
the existing `JwtProcessClearanceTokenService`, the domain port, or any business logic.

---

## 10. Alternatives Considered

### A1 — RS256 (RSA 2048-bit HMAC)

Larger keys/signatures, slower keygen (matters in CI). Identical code change otherwise.
Recommended only if the client's compliance framework mandates RSA.

### A2 — Keep shared HMAC, add `iss`-per-process enforcement

Each process could set `JwtIssuer` to its own `ActorId` instead of the shared
`"prisma-pipeline"`, and verifiers could validate that `iss` matches the expected sender.
This is a **configuration contract**, not a cryptographic guarantee — a compromised
process can still set any `iss` value it wants. Rejected: it gives the appearance of
sender binding without the cryptographic substance.

### A3 — External JWT authority (e.g. Entra ID workload identities)

Full cloud-native solution: each process gets a managed identity; tokens are issued by
Entra ID and validated via JWKS endpoint. Zero key management per-process.
Rejected for MVP: requires Azure-specific infrastructure, not aligned with the
credential-free / self-hosted deployment model; OOScope per owner ruling on persisted
identity (MVP-DEFINITION §Out of scope).

### A4 — mTLS instead of JWT tokens

Mutual TLS between processes provides cryptographic sender authentication without
application-layer JWT. Rejected: the inter-process transport is SignalR over HTTP/1.1
(in-memory in tests, potential TCP in prod); adding mTLS requires a certificate
management layer and modifies the Ember transport contract. The JWT-in-payload
design is already proven and testable without transport-level changes.

---

## 11. Open Decisions for Owner

The owner should rule on the following items before implementation is authorized:

- **OD-1 (Algorithm):** ES256 (ECDSA P-256) as recommended, or RS256 (RSA 2048-bit)?
  ES256 is the technical recommendation; RS256 is the fallback if a compliance
  requirement mandates RSA.

- **OD-2 (Key distribution mechanism for production):** Kubernetes Secret projected as
  a mounted PEM file, or Azure Key Vault config provider reference? (Dev/test uses
  generated in-memory keys as described in §5.2 regardless.)

- **OD-3 (Migration strategy):** Dual-validation window (recommended — phased, no flag
  day) or hard cutover with simultaneous redeploy of all three processes?

- **OD-4 (Test harness key fixture):** Should `TestProcessKeyPairFixture` generate
  keys at class-init time (fast, non-deterministic) or should the test key pairs be
  committed as static PEM strings in the test codebase (deterministic, avoids ECDsa
  overhead)? Non-deterministic is recommended to avoid committed key material.

- **OD-5 (Defer gate):** Confirm: implement only after MVP demo gate is cleared (as
  recommended), or prioritize now?

- **OD-6 (Hub bearer auth scope):** Should the hub `[Authorize]` bearer auth in
  `Orion/Program.cs` and `Athena/Program.cs` (connection-scope tokens) be migrated to
  asymmetric keys in the same implementation commit, or as a follow-up commit?
  Deferring hub auth migration leaves a mixed-signing path (message tokens = ECDSA;
  connection tokens = HMAC) for the duration of the migration window.

- **OD-7 (Key rotation cadence):** Is a manual rotation procedure (config change +
  redeploy) acceptable for MVP, or is an automated rotation mechanism (e.g. Key Vault
  auto-rotation + `IssuerSigningKeyResolver` polling) required before go-live?

---

## References

- `docs/architecture/adr/ADR-012-Per-Process-Security-Spine.md` — parent ADR; item #4 in §Consequences
- `docs/development/sessions/DESIGN-2026-06-12-mvp-path-1.5-clearance-identity.md` — original clearance design
- `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.BrowserAutomation/ProcessIdentity/ProcessIdentityOptions.cs`
- `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.BrowserAutomation/ProcessIdentity/JwtProcessClearanceTokenService.cs`
- `Prisma/Code/Src/CSharp/01 Core/Domain/Interfaces/IProcessClearanceTokenService.cs`
- `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.BrowserAutomation/DependencyInjection/ProcessIdentityServiceCollectionExtensions.cs`
- `Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E/MaxFidelityGateE2EBase.cs` (lines 63, 399)
- `Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E/AllRealWireThreeHostE2ETests.cs` (line 77)
- `Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E/MaxFidelityGateHosts.cs`
