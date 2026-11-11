# Dependency Architecture Diagram

## Hexagonal Architecture Dependency Rules

### Core Principle: Dependency Inversion
**All dependencies flow INWARD toward the Domain layer**

```
┌─────────────────────────────────────────────────────────────┐
│                    UI / Host Layer                          │
│  (ExxerCube.Prisma.Web.UI)                                 │
│                                                             │
│  Dependencies:                                              │
│  → Application (orchestration)                             │
│  → Domain (entities, interfaces)                           │
│  → All Infrastructure projects (for DI wiring only)        │
└─────────────────────────────────────────────────────────────┘
                            │
                            │ depends on
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                  Application Layer                           │
│  (ExxerCube.Prisma.Application)                             │
│                                                             │
│  Dependencies:                                              │
│  → Domain ONLY (interfaces, entities)                        │
│  ❌ NO Infrastructure dependencies                          │
│                                                             │
│  Uses Domain interfaces (ports) via Dependency Injection     │
└─────────────────────────────────────────────────────────────┘
                            │
                            │ depends on
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                    Domain Layer                             │
│  (ExxerCube.Prisma.Domain)                                  │
│                                                             │
│  Contains:                                                  │
│  • Entities (business objects)                              │
│  • Value Objects                                            │
│  • Domain Interfaces (PORTS)                                │
│    - IPrismaDbContext (should be here!)                     │
│    - IDownloadTracker                                       │
│    - IFileMetadataLogger                                    │
│    - IBrowserAutomationAgent                                │
│    - IMetadataExtractor                                     │
│    - etc.                                                   │
│                                                             │
│  Dependencies:                                              │
│  • NO dependencies on other projects                        │
│  • Only NuGet packages (IndQuestResults, etc.)              │
└─────────────────────────────────────────────────────────────┘
                            ▲
                            │ implements
                            │
        ┌───────────────────┼───────────────────┐
        │                   │                   │
        │                   │                   │
┌───────┴────────┐  ┌───────┴────────┐  ┌───────┴────────┐
│ Infrastructure │  │ Infrastructure │  │ Infrastructure │
│   .Database    │  │ .BrowserAuto   │  │  .Extraction   │
│                │  │                │  │                │
│ Implements:    │  │ Implements:    │  │ Implements:    │
│ • IPrismaDbCtx │  │ • IBrowserAuto │  │ • IMetadataExt │
│ • IDownloadTrk │  │                │  │                │
│ • IFileMetaLog │  │                │  │                │
│                │  │                │  │                │
│ Dependencies:  │  │ Dependencies:  │  │ Dependencies:  │
│ → Domain       │  │ → Domain       │  │ → Domain       │
│ ❌ NO App      │  │ ❌ NO App      │  │ ❌ NO App      │
│ ❌ NO other    │  │ ❌ NO other    │  │ ❌ NO other    │
│    Infra       │  │    Infra       │  │    Infra       │
└────────────────┘  └────────────────┘  └────────────────┘
```

## Project Dependency Matrix

| Project | Can Depend On | Cannot Depend On |
|---------|--------------|------------------|
| **Domain** | NuGet packages only | Nothing (no project references) |
| **Application** | Domain | Infrastructure projects, UI |
| **Infrastructure.* ** | Domain | Application, other Infrastructure projects, UI |
| **UI** | Application, Domain, All Infrastructure | - |

## Current Architecture: IPrismaDbContext Location

### ✅ Correct Architecture (Current)
```
Infrastructure.Database/
  └── EntityFramework/
      ├── IPrismaDbContext.cs  ← Internal Infrastructure abstraction (OK)
      └── PrismaDbContext.cs    ← Implementation (correct location)
```

**Important**: `IPrismaDbContext` is an **Infrastructure-internal interface** used for:
- Abstracting EF Core operations within Infrastructure layer
- Enabling testability of Infrastructure components (like `Repository<TEntity>`)
- **NOT** exposed to Domain or Application layers

### Domain Layer Abstraction Pattern

Domain should **NOT** depend on EF Core or database context interfaces. Instead:

```
Domain/
  └── Interfaces/
      ├── IFileMetadataRepository.cs    ← Domain-level repository (no EF Core types)
      ├── IPersonaRepository.cs          ← Domain-level repository
      ├── IDownloadTracker.cs            ← Service abstraction (already exists)
      └── IFileMetadataLogger.cs        ← Service abstraction (already exists)

Infrastructure.Database/
  └── Implementations/
      ├── FileMetadataRepository.cs     ← Implements Domain interface
      └── Uses IPrismaDbContext internally (Infrastructure concern)
```

**Key Principle**: Domain defines **what** it needs (repository methods), Infrastructure defines **how** to implement it (using EF Core internally).

## Dependency Flow Rules

### Rule 1: Domain Defines Ports (Interfaces)
- All interfaces that represent external dependencies MUST be in Domain
- Domain has NO project dependencies (only NuGet packages)
- Domain is the core, everything depends on it

### Rule 2: Infrastructure Implements Ports (Adapters)
- Infrastructure projects implement Domain interfaces
- Each Infrastructure project handles ONE concern
- Infrastructure projects are independent (no cross-dependencies)

### Rule 3: Application Uses Ports (Orchestration)
- Application depends ONLY on Domain
- Application uses interfaces via Dependency Injection
- Application NEVER references Infrastructure projects

### Rule 4: UI Wires Everything (Composition Root)
- UI references Application and Domain
- UI references all Infrastructure projects (for DI registration only)
- UI is the composition root that wires everything together

## Migration Plan: Moving IPrismaDbContext to Domain

### Step 1: Create Interface in Domain
1. Create `Domain/Interfaces/IPrismaDbContext.cs`
2. Move interface definition from Infrastructure.Database
3. Update namespace to `ExxerCube.Prisma.Domain.Interfaces`

### Step 2: Update Implementation
1. Update `PrismaDbContext.cs` to implement Domain interface
2. Add `using ExxerCube.Prisma.Domain.Interfaces;`
3. Keep implementation in Infrastructure.Database

### Step 3: Update Project References
1. **Domain.csproj**: Add EF Core abstractions package (if needed for interface)
2. **Infrastructure.Database.csproj**: Already references Domain (no change)
3. **Application.csproj**: Already references Domain (no change)

### Step 4: Update All Usages
1. Update all `using` statements to reference Domain namespace
2. Update Repository.cs to use Domain interface
3. Update any other files using IPrismaDbContext

## Benefits of Correct Architecture

1. **Testability**: Application can be tested without Infrastructure
2. **Flexibility**: Can swap Infrastructure implementations easily
3. **Independence**: Infrastructure projects don't depend on each other
4. **Clear Boundaries**: Domain is the core, everything flows inward
5. **Maintainability**: Changes to Infrastructure don't affect Domain/Application

## Visual Dependency Flow

```
┌─────────────┐
│     UI      │
└──────┬──────┘
       │
       ├──→ Application ──→ Domain
       │
       ├──→ Infrastructure.Database ──→ Domain
       │
       ├──→ Infrastructure.BrowserAutomation ──→ Domain
       │
       └──→ Infrastructure.Extraction ──→ Domain
```

**Key Point**: All arrows point toward Domain. Domain has no outgoing dependencies to other projects.

