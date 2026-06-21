# ADR-014: Identity as Infrastructure Adapter

**Date**: 2026-06-20
**Status**: Accepted
**Deciders**: Owner + Development Team
**Tags**: identity, auth, infrastructure, asp-net-identity, seeding, sql-server, hexagonal-architecture
**Related**: ADR-012 (Per-Process Security Spine), ADR-010 (SIARA Authentication Strategies),
`Prisma/Code/Src/CSharp/04 Services/Auth/Prisma.Auth.Infrastructure/EfCoreIdentityAdapter.cs`,
`Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/Data/ApplicationDbContext.cs`

---

## Context and Problem Statement

The Web.UI currently wires ASP.NET Core Identity **directly** inside its own assembly:

- `ApplicationDbContext` (inheriting `IdentityDbContext<ApplicationUser>`) lives in
  `07 UI/.../Data/`.
- `ApplicationUser` lives in `07 UI/.../Data/`.
- `Program.cs` calls `services.AddIdentityCore<ApplicationUser>()`, `AddEntityFrameworkStores<ApplicationDbContext>()`,
  `AddSignInManager()`, and `AddIdentityCookies()` inline in `ConfigureServices`.
- Connection strings are supplied externally (env vars / user-secrets); the base `appsettings.json`
  intentionally ships them empty.

A parallel, **never-registered** auth abstraction already exists:

| Artifact | Location |
|---|---|
| `IIdentityProvider` | `01 Core/Domain/Interfaces/IIdentityProvider.cs` line 6 |
| `ITokenService` | `01 Core/Domain/Interfaces/ITokenService.cs` line 7 |
| `IUserContextAccessor` | `01 Core/Domain/Interfaces/IUserContextAccessor.cs` line 7 |
| `UserIdentity` record | `01 Core/Domain/Models/UserIdentity.cs` |
| `TokenValidationResult` record | `01 Core/Domain/Models/TokenValidationResult.cs` |
| `EfCoreIdentityAdapter<TUser>` | `04 Services/Auth/Prisma.Auth.Infrastructure/EfCoreIdentityAdapter.cs` |
| `EfCoreIdentityConfiguration` | `04 Services/Auth/Prisma.Auth.Infrastructure/EfCoreIdentityConfiguration.cs` |
| `InMemoryIdentityProvider` | `04 Services/Auth/Prisma.Auth.Infrastructure/InMemoryIdentityProvider.cs` |

The Phase-2 PRD review (EPIC-1/G-I1) identified two **critical gaps** blocked by this state:

1. `/sla-dashboard` and `/dashboard` are unprotected (no `[Authorize]`).
2. The seeder that creates `Reviewer` and `Admin` roles and seed users does not exist,
   so the deployed system has no authenticated principal available for testing or
   for `/manual-review` (which correctly requires `Roles = "Reviewer,Admin"`).

**Owner rulings (binding, not re-litigated here):**

- Domain interfaces for identity live in **01 Core / Domain** (reuse the existing trio).
- All ASP.NET Identity scaffolding (DbContext, ApplicationUser, seeder, migrations) moves
  to a **dedicated infrastructure adapter project under 02 Infrastructure**.
- SQL Server instance for identity is `DESKTOP-FB2ES22\SQL2025` with **Windows authentication**.
- Seed two users: one with role `Reviewer`, one with role `Administrator` (matching the role
  string that `DatabaseMigration.razor` and `ConnectionStringConfig.razor` already assert).
- Azure AD is de-scoped (NFR16 removed). Cookie-based authentication is the MVP auth scheme.

---

## Surveyed State of Protected Routes

The following Razor pages were inspected. All are in
`Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/Components/Pages/`.

| Route | File | Current `[Authorize]` | Target role(s) |
|---|---|---|---|
| `/auth` | `Auth.razor` line 5 | `[Authorize]` (bare) | any authenticated user |
| `/audit/viewer` | `Audit/AuditTrailViewer.razor` line 8 | `[Authorize]` (bare) | any authenticated user |
| `/audit-trail` | `AuditTrailViewer.razor` line 9 | `[Authorize]` (bare) | any authenticated user |
| `/export-management` | `ExportManagement.razor` line 12 | `[Authorize]` (bare) | any authenticated user |
| `/manual-review` | `ManualReviewDashboard.razor` line 8 | `[Authorize(Roles = "Reviewer,Admin")]` | Reviewer or Admin |
| `/admin/database-migration` | `Admin/DatabaseMigration.razor` line 7 | `[Authorize(Roles = "Administrator")]` | Administrator |
| `/admin/connection-string` | `Admin/ConnectionStringConfig.razor` line 3 | `[Authorize(Roles = "Administrator")]` | Administrator |
| `/dashboard` | `Dashboard.razor` | **MISSING** — no `[Authorize]` | any authenticated user |
| `/sla-dashboard` | `SlaDashboard.razor` | **MISSING** — no `[Authorize]` | any authenticated user |

The two unprotected routes (`/dashboard`, `/sla-dashboard`) are the EPIC-2/G-H2+G-H3 gap
items and must be fixed as part of this work.

**Role name collision note:** `ManualReviewDashboard.razor` asserts `"Reviewer,Admin"` while
`DatabaseMigration.razor` and `ConnectionStringConfig.razor` assert `"Administrator"`. The
seeder must create roles with both spellings if both are needed, or the page declarations must
be harmonised. The **recommended resolution is to unify on `"Admin"` and `"Administrator"` as
two distinct roles** (Admin for content review, Administrator for system administration),
seeding one user per role and a third seed user that holds both. The implementer must confirm
the role strings before writing the seeder — this ADR treats the page declarations as the
authoritative requirement.

---

## Decision

### 1. Domain seam — reuse existing trio

The existing three interfaces (`IIdentityProvider`, `ITokenService`, `IUserContextAccessor`)
and their supporting records (`UserIdentity`, `TokenValidationResult`) in
`01 Core/Domain/Interfaces/` and `01 Core/Domain/Models/` **are sufficient and are reused
without modification**. They already model the complete auth contract the UI needs:

- `IIdentityProvider.GetCurrentAsync` — retrieve the authenticated principal.
- `ITokenService.CreateTokenAsync` / `ValidateTokenAsync` — JWT issue/validate (worker
  security path, per ADR-012; not used by the Web.UI cookie path but retained for future
  extensibility).
- `IUserContextAccessor.Current` — synchronous accessor for non-async Razor component code.

The `Prisma.Auth.Domain` project (`04 Services/Auth/Prisma.Auth.Domain/`) currently has
only an empty `Interfaces/` folder with no code. It **remains a placeholder** — do not move
the Domain auth interfaces there. They live in the main Domain project
(`01 Core/Domain/ExxerCube.Prisma.Domain.csproj`) and that is the correct location for a
hexagonal architecture where Domain has no external dependencies.

### 2. New infrastructure adapter project

Create a new project:

```
Prisma/Code/Src/CSharp/
  02 Infrastructure/
    Infrastructure.Identity/
      ExxerCube.Prisma.Infrastructure.Identity.csproj
```

This project hosts:

- `PrismaIdentityDbContext` — inherits `IdentityDbContext<PrismaApplicationUser>`.
  Named `Prisma` to avoid collision with the existing `ApplicationDbContext` and
  `ApplicationUser` that currently live in the Web.UI project.
- `PrismaApplicationUser` — inherits `IdentityUser`. Add `FullName string?` property
  for display; otherwise minimal.
- `PrismaIdentityRoles` — a static class declaring the three canonical role name
  constants: `Reviewer`, `Admin`, `Administrator`.
- `PrismaIdentityAdapter` — a concrete class implementing `IIdentityProvider` and
  `IUserContextAccessor` by reading from `IHttpContextAccessor` (replaces the
  `EfCoreIdentityAdapter<TUser>` approach of injecting `HttpContext` via a setter).
  `ITokenService` is also implemented for completeness (identical JWT logic as the
  existing adapter, but cleaner `IHttpContextAccessor` injection).
- `PrismaIdentitySeeder` — an `IHostedService` (or static helper called from
  `Program.cs`) that idempotently creates roles and seed users on startup.
- `PrismaIdentityExtensions` — `IServiceCollection AddPrismaIdentity(this IServiceCollection, IConfiguration)` extension method. This is the single DI registration point.
- `PrismaIdentityMigrator` — a startup helper (called from `Program.cs`) that applies
  EF migrations programmatically so the identity schema self-provisions on first run.
- EF Core migrations folder for the identity schema (`Migrations/`).

**Why a new project rather than moving into the existing `Prisma.Auth.Infrastructure`
project?**
The existing project (`04 Services/Auth/Prisma.Auth.Infrastructure/`) already contains
`EfCoreIdentityAdapter<TUser>`, which is intentionally unregistered (ADR-012 §Context
explains why it cannot be reused in workers). Merging the new Web.UI-specific adapter into
that project would make a worker-scoped project depend on ASP.NET Core Web hosting
abstractions (`IHttpContextAccessor`, `IWebHostEnvironment`). Keeping the two projects
separate preserves the worker/UI boundary. The existing adapter project is **not touched**
by this ADR.

### 3. What moves out of Web.UI

The following artifacts currently living inside `07 UI/.../Data/` are **moved to (or
replaced by) the new Infrastructure.Identity project**:

| Currently in Web.UI | Target in Infrastructure.Identity |
|---|---|
| `Data/ApplicationDbContext.cs` | Replaced by `PrismaIdentityDbContext` |
| `Data/ApplicationUser.cs` | Replaced by `PrismaApplicationUser` |
| `Data/Migrations/` | Replaced by a new migrations folder in Infrastructure.Identity |

The Web.UI project reference to `Microsoft.AspNetCore.Identity.EntityFrameworkCore` and
`Microsoft.EntityFrameworkCore.SqlServer` **moves from the `.csproj` into the new
Infrastructure.Identity project**. The Web.UI project adds a `ProjectReference` to
`Infrastructure.Identity` and calls `AddPrismaIdentity(configuration)` in `Program.cs`.

The Web.UI's existing `IdentityUserAccessor`, `IdentityRedirectManager`,
`IdentityRevalidatingAuthenticationStateProvider`, and `IdentityNoOpEmailSender` are
**Web-layer scaffolding** (they depend on `HttpContext` / Blazor rendering abstractions).
They stay in the Web.UI project. The Infrastructure.Identity project does **not** depend on
Blazor or Razor; it depends only on ASP.NET Core Identity + EF Core + the Domain project.

### 4. SQL Server connection string approach

The existing `appsettings.json` ships both `DefaultConnection` and `ApplicationConnection`
as empty strings, populated at runtime via environment variables or user-secrets. This
pattern is correct and must be preserved.

The identity adapter re-uses the `DefaultConnection` key (already consumed by `ApplicationDbContext`
in Program.cs at line 258). No new connection string key is introduced for the identity DB.

The connection string for `DESKTOP-FB2ES22\SQL2025` with Windows authentication takes the form:

```
Server=DESKTOP-FB2ES22\SQL2025;Database=PrismaIdentity;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

Key differences from the existing hardcoded `DESKTOP-FB2ES22\SQL2022` pattern (which should
never have been in source control and was removed from `appsettings.json`):

- Instance name changes from `SQL2022` to `SQL2025`.
- `TrustServerCertificate=True` is required in .NET 10 for local dev instances without
  a valid certificate chain.
- This value is supplied via **dotnet user-secrets** for local development (key:
  `ConnectionStrings:DefaultConnection`) or via `CONNECTIONSTRINGS__DEFAULTCONNECTION`
  environment variable in CI/staging. It is never committed to source.
- The `appsettings.Development.json` (git-ignored) may hold the value for convenience
  during local development. Add `appsettings.Development.json` to `.gitignore` if it is
  not already there.

### 5. Seeding strategy

The `PrismaIdentitySeeder` runs at application startup, inside the `Program.cs` pipeline
(after `app = builder.Build()`, before `app.Run()`). It is idempotent:

```
for each role in [Reviewer, Admin, Administrator]:
    if role does not exist: create it

for each seed user:
    if user does not exist by email: create it with a bcrypt-hashed password
    ensure user is in the required roles
```

Seed users (passwords are bcrypt-hashed by Identity; store only plaintext in user-secrets
or environment variables, not in source):

| Email | Roles | Config key for password |
|---|---|---|
| `reviewer@prisma.local` | `Reviewer` | `Seeding:ReviewerPassword` |
| `admin@prisma.local` | `Admin`, `Administrator` | `Seeding:AdminPassword` |

Passwords are pulled from `IConfiguration` under the `Seeding:` prefix. If the config key
is absent, the seeder logs a Warning and skips that user (it does not throw; the app must
still boot). This ensures CI environments that deliberately omit seeds do not crash.

The seeder is called from `Program.cs` via:

```csharp
await app.Services.SeedPrismaIdentityAsync(app.Configuration);
```

mirroring the existing `await app.Services.SeedTemplatesAsync()` pattern at line 150 of the
current `Program.cs`.

### 6. Migration strategy

Use **EF Core code-first migrations** (not `EnsureCreated`). Rationale:

- `EnsureCreated` is incompatible with subsequent schema changes (it never runs after the
  schema exists).
- The project already uses EF migrations for `ApplicationDbContext` (the existing
  `Data/Migrations/ApplicationDbContextModelSnapshot.cs` proves this).
- Migrations are applied programmatically at startup via `context.Database.MigrateAsync()`
  inside `PrismaIdentityMigrator`, guarded with an `ILogger` warning on failure (fail-open,
  matching the template-seeder pattern).

The initial migration is generated once by the implementer with:

```
dotnet ef migrations add InitialIdentitySchema \
  --project "02 Infrastructure/Infrastructure.Identity/ExxerCube.Prisma.Infrastructure.Identity.csproj" \
  --startup-project "07 UI/UI/ExxerCube.Prisma.Web.UI/ExxerCube.Prisma.Web.UI.csproj" \
  --context PrismaIdentityDbContext
```

(Run from `Prisma/Code/Src/CSharp/`.)

### 7. DI composition-root change in Web.UI

The existing `ConfigureServices` block in `Program.cs` (lines 245-271) is replaced with a
single call to the extension method:

```csharp
// BEFORE (lines 245-271 in Program.cs):
services.AddCascadingAuthenticationState();
services.AddScoped<IdentityUserAccessor>();
services.AddScoped<IdentityRedirectManager>();
services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
services.AddAuthentication(options => { ... }).AddIdentityCookies();
var identityConnectionString = configuration.GetConnectionString("DefaultConnection") ?? throw ...;
services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlServer(...));
services.AddDatabaseDeveloperPageExceptionFilter();
services.AddIdentityCore<ApplicationUser>(...).AddEntityFrameworkStores<ApplicationDbContext>()...;
services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// AFTER:
services.AddPrismaIdentity(configuration);
// Web-layer wiring that stays in Web.UI (cannot move to infrastructure):
services.AddCascadingAuthenticationState();
services.AddScoped<IdentityUserAccessor>();
services.AddScoped<IdentityRedirectManager>();
services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
```

The `AddPrismaIdentity` extension registers:

```csharp
// Inside AddPrismaIdentity:
services.AddDbContextFactory<PrismaIdentityDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")));
services.AddDatabaseDeveloperPageExceptionFilter();
services.AddIdentityCore<PrismaApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<PrismaIdentityDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();
services.AddSingleton<IEmailSender<PrismaApplicationUser>, PrismaNoOpEmailSender>();
services.AddHttpContextAccessor(); // required by PrismaIdentityAdapter
services.AddScoped<IIdentityProvider, PrismaIdentityAdapter>();
services.AddScoped<IUserContextAccessor, PrismaIdentityAdapter>();
// ITokenService intentionally not registered here; only needed in worker security path
// (see ADR-012). Registration can be added later if per-user JWT issuance is needed.
```

The Web.UI `_Imports.razor` and Identity Razor components reference `ApplicationUser` in
many places (Register.razor, Login.razor, Manage/*.razor etc.). These must be updated to
reference `PrismaApplicationUser`. A global find-replace of `ApplicationUser` within
`Components/Account/` and the `IdentityUserAccessor`, `IdentityRedirectManager`, and
`IdentityRevalidatingAuthenticationStateProvider` types is required.

### 8. Protecting the two unguarded routes

As part of this implementation, add `@attribute [Authorize]` at the top of:

- `Components/Pages/Dashboard.razor` (after `@page "/dashboard"`)
- `Components/Pages/SlaDashboard.razor` (after `@page "/sla-dashboard"`)

These are bare `[Authorize]` — any authenticated user, consistent with the existing
`ExportManagement.razor` and `AuditTrailViewer.razor` treatment.

### 9. Test project

Create a test project:

```
Prisma/Code/Src/CSharp/
  08 Tests/
    02 Infrastructure/
      Tests.Infrastructure.Identity/
        ExxerCube.Prisma.Tests.Infrastructure.Identity.csproj
```

Required tests (xUnit v3, Shouldly, NSubstitute, method naming `Method_Scenario_Expected`):

- `PrismaIdentityAdapter_GetCurrentAsync_ReturnsNull_WhenNoHttpContext`
- `PrismaIdentityAdapter_GetCurrentAsync_ReturnsIdentity_WhenAuthenticated`
- `PrismaIdentityAdapter_Current_ReturnsNull_WhenNotAuthenticated`
- `PrismaIdentitySeeder_SeedAsync_CreatesRolesAndUsers_WhenDatabaseIsEmpty` (uses in-memory
  provider + `UserManager<PrismaApplicationUser>`)
- `PrismaIdentitySeeder_SeedAsync_IsIdempotent_WhenCalledTwice`
- `PrismaIdentitySeeder_SeedAsync_SkipsUser_WhenPasswordConfigMissing`

The tests must use `TestContext.Current.CancellationToken`, not manual tokens.

---

## Consequences

**Positive:**
- The Domain layer owns its own auth contract (already true; now actually enforced by wiring).
- The Web.UI no longer contains Identity scaffolding — it is a consumer of the infrastructure adapter.
- `IIdentityProvider` and `IUserContextAccessor` are registered and injectable throughout the application layer.
- Roles and seed users exist on startup; the reviewer and admin scenarios are immediately testable.
- The identity schema self-migrates on startup, removing manual `dotnet ef database update` steps from the ops runbook.

**Negative / trade-offs:**
- The Razor Account pages (`Components/Account/`) contain scaffolded code that references `ApplicationUser` and `ApplicationDbContext` by type name in many places. The rename to `PrismaApplicationUser` and `PrismaIdentityDbContext` requires a mechanical but tedious find-replace across ~25 files.
- Keeping `EfCoreIdentityAdapter<TUser>` in the existing `04 Services/Auth/Prisma.Auth.Infrastructure/` project (unregistered, as today) means there are now two concrete auth adapters in the repo. The existing adapter is explicitly **not registered** anywhere and should be left as-is per ADR-012.
- The `Prisma.Auth.Domain` project (`04 Services/Auth/Prisma.Auth.Domain/`) becomes even more vestigial (empty `Interfaces/` folder). It may be removed in a cleanup pass; that is out of scope for this ADR.

**Accepted limitations:**
- The seeder uses passwords from configuration (`Seeding:ReviewerPassword`, `Seeding:AdminPassword`). These are cleartext in IConfiguration; they are hashed by `UserManager` before storage. The caller is responsible for supplying these via user-secrets / env vars, never via committed config.
- `ITokenService` is deliberately not wired in the Web.UI (`AddPrismaIdentity`). The cookie-based auth scheme does not require it. If per-user JWT issuance is needed later (e.g., for an API layer), `PrismaIdentityAdapter` can be registered as `ITokenService` with a one-line DI change.
- MudBlazor `NavMenu.razor` and `MainLayout.razor` may show nav links that require authentication to unauthenticated users. Conditional rendering (`<AuthorizeView>`) is a separate UI polish task, not part of this ADR.

---

## Ordered Implementation Checklist

Dependency order — each step may depend on the previous:

**Phase A: Create the infrastructure project (no Web.UI changes yet)**

- [ ] A1. Create folder `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Identity/`.
- [ ] A2. Create `ExxerCube.Prisma.Infrastructure.Identity.csproj` with references to:
  `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`,
  `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.AspNetCore.Http.Abstractions`,
  and a `ProjectReference` to `01 Core/Domain/ExxerCube.Prisma.Domain.csproj`.
- [ ] A3. Create `GlobalUsings.cs` with the standard global usings for the project.
- [ ] A4. Create `PrismaApplicationUser.cs` (inherits `IdentityUser`, adds `FullName string?`).
- [ ] A5. Create `PrismaIdentityDbContext.cs` (inherits `IdentityDbContext<PrismaApplicationUser>`).
- [ ] A6. Create `PrismaIdentityRoles.cs` — static class with constants: `Reviewer = "Reviewer"`,
  `Admin = "Admin"`, `Administrator = "Administrator"`.
- [ ] A7. Create `PrismaIdentityAdapter.cs` — implements `IIdentityProvider` and
  `IUserContextAccessor` via `IHttpContextAccessor`. All async methods must accept
  `CancellationToken cancellationToken = default` and call `.ConfigureAwait(false)`.
  Business errors return `Result<T>.WithFailure(...)`, never throw.
- [ ] A8. Create `PrismaNoOpEmailSender.cs` — implements `IEmailSender<PrismaApplicationUser>` as a
  no-op (mirrors the existing `IdentityNoOpEmailSender` in Web.UI).
- [ ] A9. Create `PrismaIdentitySeeder.cs` — static helper with
  `SeedAsync(IServiceProvider, IConfiguration, CancellationToken)`. Reads passwords from
  `IConfiguration` keys `Seeding:ReviewerPassword` and `Seeding:AdminPassword`. Logs
  `Warning` and skips if absent. Idempotent (check-before-create). Uses `Result<T>` for
  internal error propagation; never throws from the seeder body.
- [ ] A10. Create `PrismaIdentityMigrator.cs` — static helper with
  `MigrateAsync(IServiceProvider, ILogger, CancellationToken)`. Calls
  `context.Database.MigrateAsync(ct).ConfigureAwait(false)` inside a try/catch; logs
  Error but does not rethrow (fail-open, matching the template-seeder pattern).
- [ ] A11. Create `PrismaIdentityExtensions.cs` — `AddPrismaIdentity` extension method
  (signature and body as specified in Section 7 above). Returns `IServiceCollection`.
- [ ] A12. Generate the initial EF migration `InitialIdentitySchema` using the command in
  Section 6. Verify the migration snapshot compiles: `dotnet build` on the Infrastructure.Identity project.
- [ ] A13. Add the new project to `ExxerCube.Prisma.sln`.

**Phase B: Wire into Web.UI**

- [ ] B1. Add `ProjectReference` to `Infrastructure.Identity` in `ExxerCube.Prisma.Web.UI.csproj`.
  Remove `Microsoft.AspNetCore.Identity.EntityFrameworkCore` and
  `Microsoft.EntityFrameworkCore.SqlServer` package references from the Web.UI csproj
  (they are now transitive through Infrastructure.Identity).
- [ ] B2. In `Program.cs` replace lines 245-271 (current Identity wiring block) with the
  new `AddPrismaIdentity(configuration)` call plus the Web-layer scaffolding lines
  that stay (listed in Section 7). Keep `services.AddCascadingAuthenticationState()`
  and the three Identity scaffolding `AddScoped` registrations in Web.UI.
- [ ] B3. After `app.Build()` and before `app.Run()`, add the two startup calls:
  ```csharp
  await app.Services.MigratePrismaIdentityAsync(logger);
  await app.Services.SeedPrismaIdentityAsync(app.Configuration);
  ```
  Place them before the existing `SeedTemplatesAsync()` call.
- [ ] B4. Find-replace `ApplicationUser` → `PrismaApplicationUser` in:
  - `Components/Account/Pages/` (all ~20 Razor files)
  - `Components/Account/Shared/` (all shared components)
  - `Services/IdentityUserAccessor.cs` (if it exists)
  - `Services/IdentityRedirectManager.cs`
  - `Services/IdentityRevalidatingAuthenticationStateProvider.cs`
  - `HealthChecks/PrismaDbHealthCheck.cs`
- [ ] B5. Find-replace `ApplicationDbContext` → `PrismaIdentityDbContext` in the same
  files plus `Admin/DatabaseMigration.razor` (line 8 injects `IDbContextFactory<ApplicationDbContext>`).
- [ ] B6. Delete `Data/ApplicationUser.cs` and `Data/ApplicationDbContext.cs` from Web.UI.
  The `Data/Migrations/` folder is also deleted (replaced by migrations in Infrastructure.Identity).
- [ ] B7. Delete `Data/IdentityNoOpEmailSender.cs` from Web.UI (replaced by
  `PrismaNoOpEmailSender` in Infrastructure.Identity). If that file does not exist as a
  separate file (it may be inlined), the existing inline class is deleted.

**Phase C: Protect the two unguarded routes**

- [ ] C1. Add `@attribute [Authorize]` to `Components/Pages/Dashboard.razor`
  (after line 1, `@page "/dashboard"`).
- [ ] C2. Add `@attribute [Authorize]` to `Components/Pages/SlaDashboard.razor`
  (after line 2, `@page "/sla-dashboard"`).
- [ ] C3. Add `@using Microsoft.AspNetCore.Authorization` to both files if not already
  present (check `Components/_Imports.razor` — if it is in the global imports already, the
  individual `@using` is unnecessary).

**Phase D: Test project**

- [ ] D1. Create `Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Tests.Infrastructure.Identity/`
  with `ExxerCube.Prisma.Tests.Infrastructure.Identity.csproj` referencing `xunit.v3.mtp-v2`,
  Shouldly, NSubstitute, and `Infrastructure.Identity`.
- [ ] D2. Implement the six unit tests listed in Section 9.
- [ ] D3. Add the test project to `ExxerCube.Prisma.sln`.
- [ ] D4. Run `dotnet test` on the new test project; all six tests must pass.

**Phase E: Verification**

- [ ] E1. `dotnet build ExxerCube.Prisma.sln` — 0 errors, 0 warnings.
- [ ] E2. Start Web.UI against `DESKTOP-FB2ES22\SQL2025` (via user-secrets). Confirm:
  - Application boots.
  - `/health` returns `Healthy`.
  - `PrismaIdentityMigrator` applies the migration (check logs for "Migrations applied").
  - `PrismaIdentitySeeder` creates roles and users (check logs for "Seed users created").
- [ ] E3. Navigate to `/dashboard` unauthenticated — confirm redirect to `/Account/Login`.
- [ ] E4. Navigate to `/sla-dashboard` unauthenticated — confirm redirect to `/Account/Login`.
- [ ] E5. Log in as `reviewer@prisma.local` — confirm `/manual-review` is accessible.
- [ ] E6. Log in as `admin@prisma.local` — confirm `/admin/database-migration` is accessible.
- [ ] E7. Run the full test suite: `dotnet test ExxerCube.Prisma.sln` — no regressions.

---

## Decisions Requiring Owner Confirmation

> **RECOMMENDATION — already reflected in this ADR, flagged for explicit owner sign-off:**

1. **Role name unification:** `ManualReviewDashboard.razor` uses `"Reviewer,Admin"` while
   the Admin pages use `"Administrator"`. This ADR seeds all three names (`Reviewer`, `Admin`,
   `Administrator`) and creates one seed user per "natural" role. If the owner decides to
   collapse `Admin` and `Administrator` into one role, only the seeder and the role constants
   need updating (one-line change each). **Owner should confirm the desired role taxonomy
   before the seeder is implemented (Step A9).**

2. **Password storage for staging:** The `Seeding:ReviewerPassword` and `Seeding:AdminPassword`
   values need to be placed somewhere accessible at staging deploy time. The ADR assumes
   dotnet user-secrets (dev) and environment variables (CI/staging). **Owner should confirm
   the secrets delivery mechanism for the staging environment before Phase B is merged.**

---

## Verification of the Decision Against Architecture Principles

| Principle | Satisfied? | Evidence |
|---|---|---|
| Domain owns interfaces | Yes | `IIdentityProvider`, `IUserContextAccessor` in `01 Core/Domain` |
| Infrastructure provides implementations | Yes | `PrismaIdentityAdapter` in `02 Infrastructure/Infrastructure.Identity` |
| Web.UI depends on abstractions only | Yes | After Phase B, Web.UI `ProjectReference` → Infrastructure.Identity; calls `AddPrismaIdentity` |
| No exceptions for business logic | Yes | `PrismaIdentityAdapter` returns `Result<T>` |
| CancellationToken on all async methods | Yes | Required in checklist step A7 |
| Nullable enabled, warnings-as-errors | Yes | Inherited from `Directory.Build.props` |
| ConfigureAwait(false) in library code | Yes | Required in checklist step A7, A10 |
| Idempotent seeder | Yes | Check-before-create pattern (step A9) |
| Tests xUnit v3 + Shouldly + NSubstitute | Yes | Checklist D1-D4 |
