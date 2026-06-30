# Veriqan Epic 6 Story 6.3 — Unified Secrets Provider Abstraction

**Delivered:** 2026-06-30, branch `Liv`
**Status:** Done — all three test suites green (118 + 75 + 31 = 224 tests, 0 failed)

---

## Summary

Story 6.3 introduces a two-layer secrets abstraction that decouples the three existing
hard-wired `IConfiguration` reads from their transport mechanism, enabling a future Key
Vault implementation to be plugged in without touching any consumer code.

**What was built (additive only — no schema change, no migration):**

| Component | Location | Role |
|-----------|----------|------|
| `SecretValue` (record) | `Veriqan.Application/Ports/` | Carries value + rotation metadata |
| `ISecretProvider` (interface) | `Veriqan.Application/Ports/` | Async, Result-returning contract |
| `ConfigurationSecretProvider` (class) | `Veriqan.Application/Ports/` | Default IConfiguration-backed implementation |
| `SmtpPasswordSecretPopulator` (class) | `Veriqan.Orchestration/Secrets/` | IPostConfigureOptions wiring for SMTP |

---

## Architecture decisions

### Why `ISecretProvider` lives in Application (not Orchestration)

All three consumers that needed rewiring are in different layers:
- `ConfigurationCryptoKeyProvider` — `Veriqan.Infrastructure.Persistence` (layer 02)
- `SmtpPasswordSecretPopulator` — `Veriqan.Orchestration` (layer 03)
- `Program.cs` JWT wiring — `Veriqan.Worker` (layer 04)

Every infrastructure and orchestration layer references `Veriqan.Application`, making it the
only location accessible to all three. Placing the interface in Application is also consistent
with the existing `IEmailSender`, `IPasswordProvider`, and `ITokenService` ports.

### Why `ConfigurationSecretProvider` was placed in Application (not Orchestration)

Initially placed in Orchestration, the class was moved to Application during implementation
when it became clear that `AddVeriqanPersistence` (Persistence layer) needs a fallback
`ISecretProvider` registration for standalone usage by `MigrateCommand` and direct integration
tests. Since Persistence cannot reference Orchestration (violates hexagonal layer order),
`ConfigurationSecretProvider` was moved to `Application/Ports/` alongside the interface.

This placement is architecturally sound: .NET ships many default implementations alongside
their interfaces in the same assembly (e.g. `IMemoryCache`/`MemoryCache`). The rule is that
Application must not reference Infrastructure — nothing prevents a default implementation from
living in Application.

### `TryAddSingleton` in both `AddVeriqan` AND `AddVeriqanPersistence`

Both registration points use `TryAddSingleton<ISecretProvider>`:

- `AddVeriqan` (Orchestration) — normal runtime composition root
- `AddVeriqanPersistence` (Persistence) — standalone usage guard (MigrateCommand, integration tests)

When `AddVeriqan` is called (normal path), it registers first; `AddVeriqanPersistence`'s TryAdd
becomes a no-op. When `AddVeriqanPersistence` is called directly (MigrateCommand, test hosts),
its TryAdd fires and provides the config-backed fallback.

A Key Vault adapter wins by registering `ISecretProvider` before either `AddVeriqan` or
`AddVeriqanPersistence` is called — both TryAdds become no-ops.

---

## How each secret is resolved

### 1. AES-256 encryption key (`Veriqan:LegalBaseline:EncryptionKey`)

`ConfigurationCryptoKeyProvider` (Persistence layer) constructor was rewired:

```
Before: IConfiguration → _configuration["Veriqan:LegalBaseline:EncryptionKey"]
After:  ISecretProvider → GetSecretAsync("Veriqan:LegalBaseline:EncryptionKey").GetAwaiter().GetResult()
```

The 32-byte Base64 and AES validation guards are unchanged and remain fail-loud.
Blocks once at singleton construction (startup).

### 2. JWT signing key (`Veriqan:Auth:Jwt:SigningKey`)

`Program.cs` resolves the key before the DI container is built (ASP.NET JWT setup
requires it at host construction time). Uses `ConfigurationSecretProvider` directly:

```csharp
var jwtKeyResult = new ConfigurationSecretProvider(builder.Configuration)
    .GetSecretAsync("Veriqan:Auth:Jwt:SigningKey")
    .GetAwaiter().GetResult();
var signingKey = jwtKeyResult.IsSuccess ? jwtKeyResult.Value?.Value : null;
```

Existing fail-open/warn behavior for missing JWT key is preserved unchanged.

### 3. SMTP password (`Veriqan:Smtp:Password`)

`SmtpPasswordSecretPopulator` implements `IPostConfigureOptions<SmtpOptions>` and runs at
first options resolution. Only overrides `SmtpOptions.Password` when it is null (i.e., not
already set by appsettings/env vars — "config wins"). Graceful degradation: if the provider
returns failure, password stays null and unauthenticated relay is used.

Registered from `AddVeriqan` (not from `AddVeriqanReporting`) so that Reporting integration
tests that call `AddVeriqanReporting` directly continue to work without registering
`ISecretProvider`.

---

## KeyId/Version rotation contract

| Field | Value | Notes |
|-------|-------|-------|
| `KeyId` | `"config:<logicalName>"` | Stable across rotations; safe to log |
| `Version` | first 8 hex chars of SHA-256(UTF-8(value)) | Changes on rotation; never reversible |
| `RetrievedAtUtc` | UTC timestamp at resolution time | Allows cache-age auditing |

The raw `Value` must NEVER be logged or serialized to structured log fields.

---

## Extension point (cloud vault)

To plug in Azure Key Vault or AWS KMS:

```csharp
// Register BEFORE AddVeriqan (or AddVeriqanPersistence for standalone tools):
services.AddSingleton<ISecretProvider, AzureKeyVaultSecretProvider>();

// Then call the normal composition:
services.AddVeriqan(config);       // TryAddSingleton is a no-op
services.AddVeriqanPersistence(cs); // TryAddSingleton is a no-op
```

No consumer code changes are required. The abstraction is additive — removing
`ConfigurationSecretProvider` from production in favor of Key Vault is a single DI line change.

---

## Files modified / created

**New files:**
- `Veriqan.Application/Ports/SecretValue.cs`
- `Veriqan.Application/Ports/ISecretProvider.cs`
- `Veriqan.Application/Ports/ConfigurationSecretProvider.cs`
- `Veriqan.Orchestration/Secrets/SmtpPasswordSecretPopulator.cs`
- `Tests/03 Orchestration/Veriqan.Orchestration.Tests/ConfigurationSecretProviderTests.cs` (9 tests)
- `Tests/03 Orchestration/Veriqan.Orchestration.Tests/SmtpPasswordSecretPopulatorTests.cs` (4 tests)

**Modified files:**
- `Veriqan.Application/ExxerCube.Prisma.Veriqan.Application.csproj` — added `Microsoft.Extensions.Configuration.Abstractions`
- `Veriqan.Infrastructure.Persistence/Crypto/ConfigurationCryptoKeyProvider.cs` — constructor rewired to `ISecretProvider`
- `Veriqan.Infrastructure.Persistence/DependencyInjection/VeriqanPersistenceExtensions.cs` — added `TryAddSingleton<ISecretProvider>` fallback
- `Veriqan.Orchestration/DependencyInjection/VeriqanOrchestrationExtensions.cs` — registers `ISecretProvider` + `SmtpPasswordSecretPopulator`
- `Veriqan.Orchestration/Startup/VeriqanConfigurationValidator.cs` — added `Veriqan:Auth:Jwt:SigningKey` to CriticalKeys
- `Veriqan.Worker/Program.cs` — JWT key resolved via `ConfigurationSecretProvider`
- `Tests/03 Orchestration/Veriqan.Orchestration.Tests/VeriqanConfigurationValidatorTests.cs` — added JWT key to "all present" configs

**Deleted files:**
- `Veriqan.Orchestration/Secrets/ConfigurationSecretProvider.cs` — moved to Application/Ports

---

## Test results

| Suite | Passed | Failed | Skipped |
|-------|--------|--------|---------|
| Veriqan.Orchestration.Tests | 118 | 0 | 0 |
| Veriqan.Infrastructure.Reporting.Tests | 75 | 0 | 0 |
| Veriqan.Infrastructure.Persistence.IntegrationTests | 31 | 0 | 0 |

Worker build: **0 warnings, 0 errors.**
