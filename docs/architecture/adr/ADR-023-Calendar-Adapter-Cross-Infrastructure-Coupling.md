# ADR-023: Resolve `Infrastructure.Database → Infrastructure.Calendar` Cross-Adapter Coupling

**Date**: 2026-06-27 (implemented 2026-06-28)
**Status**: Accepted — Option A implemented on branch `Liv`. Owner approved Option A + dynamic test
enumeration. **Scope note:** dynamic enumeration surfaced a *second, identical* edge
(`Infrastructure.Classification → Infrastructure.Calendar`); it was fixed in the same change rather than
allow-listed (same bug, same one-line fix). Pending: full build/test green + owner ratification of the
Classification inclusion.
**Deciders**: Owner + Development Team
**Tags**: hexagonal-architecture, adapter-isolation, dependency-direction, netarchtest, composition-root, sla, calendar
**Related**:
- ADR-003 (Metrics Services — Infrastructure-Layer Placement)
- ADR-014 (Identity as Infrastructure Adapter)
- `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Database/ExxerCube.Prisma.Infrastructure.Database.csproj` line 31
- `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Database/DependencyInjection/ServiceCollectionExtensions.cs` lines 3, 57–73
- `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Calendar/MexicoBusinessDayCalculator.cs`
- `Prisma/Code/Src/CSharp/01 Core/Domain/Interfaces/IBusinessDayCalculator.cs`
- `Prisma/Code/Src/CSharp/08 Tests/09 Architecture/Tests.Architecture/HexagonalArchitectureTests.cs` lines 515–567

---

## Context and Problem Statement

The hexagonal-architecture contract for this solution states (see `ARCHITECTURE_AND_SOLUTION_GUIDELINES.md` §2.2 and §6.3):

> **Infrastructure projects never reference each other.** Adapter composition happens one level up, in the Orchestration composition root.

This rule is violated in production code today.

### The violation (grounded in code)

`Infrastructure.Database` declares a **`ProjectReference` to `Infrastructure.Calendar`**:

```xml
<!-- 02 Infrastructure/Infrastructure.Database/ExxerCube.Prisma.Infrastructure.Database.csproj : line 31 -->
<ProjectReference Include="..\Infrastructure.Calendar\ExxerCube.Prisma.Infrastructure.Calendar.csproj" />
```

The reference exists for **exactly one reason** — a DI registration inside the Database adapter's own service-collection extension:

```csharp
// 02 Infrastructure/Infrastructure.Database/DependencyInjection/ServiceCollectionExtensions.cs
using ExxerCube.Prisma.Infrastructure.Calendar;          // line 3
// ...
// line 61:
services.TryAddSingleton<IBusinessDayCalculator, MexicoBusinessDayCalculator>();
```

That is, the **Database adapter reaches into the Calendar adapter to register the Calendar adapter's concrete type**. This is precisely the cross-adapter coupling the architecture forbids: one adapter compiling against, and wiring, another adapter.

### What is *not* the problem

The consumer is clean. `SLAEnforcerService` depends only on the **Domain port** `IBusinessDayCalculator` (defined at `01 Core/Domain/Interfaces/IBusinessDayCalculator.cs`), injected as an optional constructor dependency:

```csharp
// 02 Infrastructure/Infrastructure.Database/SLAEnforcerService.cs : ctor
public SLAEnforcerService(
    PrismaDbContext dbContext,
    ILogger<SLAEnforcerService> logger,
    IOptions<SLAOptions> options,
    SLAMetricsCollector metricsCollector,
    IBusinessDayCalculator? businessDayCalculator = null)   // ← port only; null ⇒ weekend-only fallback
```

So `SLAEnforcerService` has **no source coupling to Calendar** — when the calculator is `null` it falls back to a built-in weekend-only computation. The *only* thing pulling Calendar into Database is the single DI line above. The `Infrastructure.Calendar` adapter itself is well-behaved: it references only `Domain` + the `PublicHoliday` package.

### Secondary finding — the architecture test has a blind spot

The rule that should have caught this — `Infrastructure_Projects_Should_Not_Depend_On_Each_Other` (`HexagonalArchitectureTests.cs:515`) — currently **passes**, because it iterates a **hard-coded list of 8 namespaces** that **omits `Infrastructure.Calendar`** (and also `Identity`, `Imaging`, `Events`, `Export.Adaptive`, `Extraction.Adaptive`, `Extraction.Txt`, `Python.*`):

```csharp
var infrastructureNamespaces = new[]
{
    "ExxerCube.Prisma.Infrastructure.Database",
    "ExxerCube.Prisma.Infrastructure.Classification",
    "ExxerCube.Prisma.Infrastructure.Extraction",
    "ExxerCube.Prisma.Infrastructure.Export",
    "ExxerCube.Prisma.Infrastructure.FileStorage",
    "ExxerCube.Prisma.Infrastructure.BrowserAutomation",
    "ExxerCube.Prisma.Infrastructure.FileSystem",
    "ExxerCube.Prisma.Infrastructure.Metrics",
};
```

Because `Calendar` is not an enumerated **target**, `Database → Calendar` is never asserted. The violation is therefore a **false negative**: latent, not red. Any fix must also close this coverage gap, or the next cross-adapter reference will slip through the same way.

### Blast radius

`AddDatabaseServices` (which carries the implicit Calendar registration) is called directly by **four hosts**:

| Host | Call site |
|---|---|
| Athena Worker | `04 Services/Athena/Prisma.Athena.Worker/Program.cs:107` |
| Orion Worker | `04 Services/Orion/Prisma.Orion.Worker/Program.cs:104` |
| Reconciliator Worker | `04 Services/Reconciliator/Prisma.Reconciliator.Worker/Program.cs:97` |
| Web.UI | `07 UI/.../Program.cs:323` |

Each of these currently receives `MexicoBusinessDayCalculator` *implicitly* via the Database extension.

> **Correction to the original draft (verified in code):** although the `SLAEnforcerService` *constructor*
> accepts an optional `IBusinessDayCalculator? = null` (weekend-only fallback), the **DI factory resolves it
> with `GetRequiredService<IBusinessDayCalculator>()`** (`ServiceCollectionExtensions.cs`, SLA factory). So a
> host that wires `AddDatabaseServices` but forgets `AddCalendarServices()` **fails loudly at resolution**
> (throws when `ISLAEnforcer`/`SLAEnforcerService` is resolved), it does **not** degrade silently. The same is
> true of `Infrastructure.Classification`'s `IFusionExpediente` factory (`GetRequiredService`). The per-host
> guard test (below) turns that runtime throw into a test-time failure.

**Second edge (discovered during implementation):** `Infrastructure.Classification` has the **identical**
coupling — a `ProjectReference` to `Infrastructure.Calendar` plus
`services.TryAddSingleton<IBusinessDayCalculator, MexicoBusinessDayCalculator>()` in its
`ServiceCollectionExtensions`, consumed by the `IFusionExpediente` (`FusionExpedienteService`) factory. It is
consumed by the Orchestration composition root (`AddClassificationServices`) and the Web.UI. **Athena's host
already registers the calculator directly** (`Prisma.Athena.Worker/Program.cs`), which is exactly the
Option-A pattern and is left intact.

---

## Decision Drivers

- Restore adapter isolation (the documented, test-enforced invariant).
- **Preserve behaviour**: production SLA calculations must keep their Mexican-federal-holiday awareness; no silent regression to weekend-only math.
- Keep `Infrastructure.Calendar` a reusable capability (the `Veriqan` SLA/business-day surfaces are plausible future consumers; do not bury it inside Database).
- Make the architecture test actually enforce the rule it claims to.

---

## Considered Options

### Option A — Move the Calendar registration to the host/composition root *(recommended)*

1. Add a `ServiceCollectionExtensions.AddCalendarServices()` to **`Infrastructure.Calendar`** (it already references `Microsoft.Extensions.DependencyInjection.Abstractions`):

   ```csharp
   // 02 Infrastructure/Infrastructure.Calendar/DependencyInjection/ServiceCollectionExtensions.cs
   public static IServiceCollection AddCalendarServices(this IServiceCollection services)
   {
       services.TryAddSingleton<IBusinessDayCalculator, MexicoBusinessDayCalculator>();
       return services;
   }
   ```

2. **Remove** from `Infrastructure.Database`: the `using ExxerCube.Prisma.Infrastructure.Calendar;`, the `TryAddSingleton<IBusinessDayCalculator, MexicoBusinessDayCalculator>()` line, and the `ProjectReference` to `Infrastructure.Calendar` (`.csproj:31`).

3. **Re-register at each composition site** — call `services.AddCalendarServices()` alongside `AddDatabaseServices(...)` in the four hosts (Athena, Orion, Reconciliator, Web.UI). `TryAddSingleton` keeps it idempotent if more than one path registers it.

4. `SLAEnforcerService` is **unchanged** (it already depends on the port only).

**Pros:** Textbook composition-root fix; matches §2.8 / §6.3 of the guidelines exactly. Calendar stays reusable. Consumer untouched. Minimal, mechanical diff.
**Cons:** Four host call-sites must be updated in lockstep; a missed host silently loses holiday awareness (mitigated by Option-A step 5 below — a behaviour test).

5. **Guard the behaviour**: add/keep a test asserting that each host's DI graph resolves `IBusinessDayCalculator` to `MexicoBusinessDayCalculator` (not null), so a forgotten `AddCalendarServices()` fails loudly instead of degrading silently.

### Option B — Merge `Infrastructure.Calendar` into `Infrastructure.Database`

Collapse the Calendar adapter's single class into Database.
**Rejected:** eliminates a legitimately reusable capability, couples holiday math to the persistence adapter, and blocks future non-Database consumers (e.g. `Veriqan` SLA). It removes the *reference* but worsens the *design*.

### Option C — Promote `MexicoBusinessDayCalculator` into Domain or `CrossConcerns`

**Rejected:** the calculator depends on the `PublicHoliday` NuGet package — it is an **adapter**, not domain logic, so it cannot live in Domain (which must stay dependency-free). `CrossConcerns` sits in the Services layer (`04 Services/CrossConcerns`) and is the wrong altitude for an infrastructure adapter.

### Option D — Keep the reference; allow-list it in the architecture test

**Rejected as the resolution.** Allow-listing documents the deviation but leaves the coupling in place; the guidelines call this out explicitly as "do nothing but acknowledge." (It would still be an improvement over the current *invisible* state, so if Option A is deferred, an interim allow-list **with this ADR referenced in the comment** is acceptable as a stopgap — but it is not the fix.)

---

## Decision (proposed)

Adopt **Option A**: relocate the `IBusinessDayCalculator → MexicoBusinessDayCalculator` registration out of the Database adapter and into the Calendar adapter's own `AddCalendarServices()` extension, called from each host's composition root; drop the `Database → Calendar` project reference; and **harden the architecture test** so the cross-adapter rule enumerates *all* infrastructure adapter assemblies (preferably derived dynamically from the loaded `InfrastructureAssemblies` set) rather than a hand-maintained list.

This decision is **not implemented in this ADR**. Implementation is gated on owner sign-off (see open questions).

---

## Consequences

**Positive**
- Adapter isolation restored; `Database` no longer compiles against `Calendar`.
- The cross-adapter architecture rule becomes truthful and self-extending (no blind spots as new adapters are added).
- `Infrastructure.Calendar` remains independently consumable by future hosts/contexts.

**Negative / trade-offs**
- Four host `Program.cs` files must add `AddCalendarServices()`. Until the behaviour guard (step 5) lands, a missed host silently loses holiday awareness.
- One more line of explicit wiring per host (the cost of explicit composition over implicit transitive registration — an accepted trade in this codebase).

**Accepted limitations**
- This ADR addresses only the `Database → Calendar` edge. The architecture-test hardening will likely surface other currently-invisible cross-adapter edges; those are to be triaged separately (do not bundle unrelated fixes into this change).

---

## As-Built Implementation (2026-06-28, branch `Liv`)

- [x] 1. Added `AddCalendarServices()` — `02 Infrastructure/Infrastructure.Calendar/DependencyInjection/ServiceCollectionExtensions.cs` (TryAddSingleton, idempotent).
- [x] 2. **Database** — removed the Calendar `using`, the orphaned `Microsoft.Extensions.DependencyInjection.Extensions` `using` (its only `TryAdd*` use was the removed line), and the `TryAddSingleton` registration from `Infrastructure.Database/DependencyInjection/ServiceCollectionExtensions.cs`; removed the Calendar `ProjectReference` from the `.csproj`.
- [x] 3. **Classification** — same removal (using + orphaned Extensions using + registration + `.csproj` `ProjectReference`).
- [x] 4. Wired `AddCalendarServices()` into the composition paths:
  - Orchestration composition root `PrismaServiceCollectionExtensions.AddPrismaInfrastructure` (+ Calendar `ProjectReference`).
  - Web.UI `Program.cs` (before `AddDatabaseServices`/`AddClassificationServices`) (+ Calendar `ProjectReference`).
  - Orion + Reconciliator Workers `Program.cs` (inside the gated audit-DB `if`, after `AddDatabaseServices`) (+ Calendar `ProjectReference`).
  - Athena Worker: **left as-is** — it already registers `MexicoBusinessDayCalculator` directly and unconditionally (its `IFusionExpediente` is registered unconditionally); added an explicit Calendar `ProjectReference` (was transitive via Database/Classification).
- [x] 5. Hardened `Infrastructure_Projects_Should_Not_Depend_On_Each_Other` to **dynamic, bucket-based enumeration** of all `ExxerCube.Prisma.Infrastructure.<Adapter>` assemblies (collapsing sub-adapters like `Extraction.Txt`/`Extraction.Adaptive` to their family bucket to avoid prefix false positives); `allowedEdges` is empty (both Calendar edges fixed, not allow-listed).
- [x] 6. Added the registration guard test `CalendarServiceCollectionExtensionsTests` in `Tests.Infrastructure.Calendar` (registers `MexicoBusinessDayCalculator`/singleton, resolves the port, idempotent) + the `Microsoft.Extensions.DependencyInjection` package reference it needs.
- [x] 7. `dotnet build` 0/0; architecture suite green (incl. the hardened rule); Calendar/SLA tests green. *(verified 2026-06-28: all 5 host/adapter projects + 2 test projects build 0 error/0 warning under warnings-as-errors; `Infrastructure_Projects_Should_Not_Depend_On_Each_Other` passes 1/1 with the hardened dynamic enumeration; `Tests.Infrastructure.Calendar` 17/17 green. One transitive-reference fallout fixed: `Tests.Infrastructure.Classification` instantiated the real `MexicoBusinessDayCalculator` via the now-severed transitive Calendar path, so a direct Calendar `ProjectReference` was added to that test `.csproj`.)*

---

## Verification of the Decision Against Architecture Principles

| Principle | Satisfied by Option A? | Evidence |
|---|---|---|
| Infrastructure adapters do not reference each other | Yes | `Database.csproj` Calendar `ProjectReference` removed |
| Composition happens only at the root/host | Yes | `AddCalendarServices()` called from hosts, not from another adapter |
| Domain owns the port | Already true | `IBusinessDayCalculator` in `01 Core/Domain/Interfaces` |
| Behaviour preserved | Yes (with step 6) | Hosts re-register `MexicoBusinessDayCalculator`; resolution test guards it |
| Architecture rules are enforced, not aspirational | Yes | Test hardened to enumerate all adapters |

---

## Open Questions for Owner Sign-off

1. **Approve Option A** over the merge (B) / promote (C) alternatives?
2. **Test hardening scope:** fix only the `Calendar` omission now, or switch the rule to fully dynamic adapter enumeration (which may turn other latent cross-adapter edges red and require their own triage)?
3. **Sequencing:** land the test-hardening first (to make the violation visibly red), then the fix — or fix and harden in one PR?
