# Architecture Violation Guidelines - .NET Clean Architecture

**Status:** 📋 **Active Guidelines**  
**Date:** 2025-01-15  
**Purpose:** Prevent and detect clean architecture violations in .NET applications  
**Tags:** clean-architecture, hexagonal-architecture, guidelines, code-review

---

## Purpose

This document provides comprehensive guidelines for identifying, preventing, and remediating clean architecture violations in .NET applications. Use these guidelines during code reviews, architecture reviews, and automated checks.

---

## Core Architecture Principles

### 1. Dependency Rule

**Principle:** Dependencies point inward toward the Domain.

```
┌─────────────────────────────────────┐
│         Infrastructure              │  ← Outermost layer
│  (Database, External APIs, etc.)   │
└──────────────┬──────────────────────┘
               │ depends on
┌──────────────▼──────────────────────┐
│         Application                  │  ← Business logic
│  (Services, Use Cases, Handlers)    │
└──────────────┬──────────────────────┘
               │ depends on
┌──────────────▼──────────────────────┐
│           Domain                    │  ← Innermost layer
│  (Entities, Value Objects,          │
│   Interfaces/Ports)                │
└─────────────────────────────────────┘
```

**Rules:**
- ✅ Domain has NO dependencies (except standard library)
- ✅ Application depends ONLY on Domain
- ✅ Infrastructure depends on Domain and Application
- ❌ Domain cannot depend on Application or Infrastructure
- ❌ Application cannot depend on Infrastructure

---

## Common Violation Patterns

### Category 1: Infrastructure Leakage

#### Violation 1.1: EF Core in Application Layer

**Symptoms:**
- `using Microsoft.EntityFrameworkCore;` in Application code
- `DbContext` or `DbSet<T>` injected into Application services
- Direct LINQ queries using `_dbContext.Entities.Where(...)`
- Application project references `Microsoft.EntityFrameworkCore` package

**Detection:**
```bash
# Check for EF Core references in Application
grep -r "Microsoft.EntityFrameworkCore" Prisma/Code/Src/CSharp/Application/
grep -r "DbContext\|DbSet" Prisma/Code/Src/CSharp/Application/

# Check project references
dotnet list Prisma/Code/Src/CSharp/Application/*.csproj package | grep EntityFrameworkCore
```

**Remediation:**
- Create `IRepository<T, TId>` interface in Domain
- Implement `EfCoreRepository<T, TId>` in Infrastructure
- Inject `IRepository<T, TId>` into Application services
- Remove EF Core package reference from Application project

**Reference:** [ADR-004: EF Core Application Layer Violation](./adr-004-efcore-application-layer-violation.md)

---

#### Violation 1.2: Infrastructure Types in Application

**Symptoms:**
- Application services instantiate Infrastructure classes directly
- Application code uses `new InfrastructureService(...)`
- Application tests instantiate Infrastructure types instead of mocks

**Examples:**
```csharp
// ❌ VIOLATION
public class ApplicationService
{
    public ApplicationService()
    {
        _resolver = new PersonIdentityResolverService(logger); // Infrastructure type
    }
}

// ✅ CORRECT
public class ApplicationService
{
    public ApplicationService(IPersonIdentityResolver resolver) // Domain interface
    {
        _resolver = resolver;
    }
}
```

**Detection:**
```bash
# Check for Infrastructure namespace usage in Application
grep -r "Infrastructure\." Prisma/Code/Src/CSharp/Application/
grep -r "new.*Infrastructure" Prisma/Code/Src/CSharp/Application/
```

**Remediation:**
- Define Domain interfaces for all Infrastructure services
- Inject Domain interfaces into Application services
- Use dependency injection for all dependencies

---

#### Violation 1.3: External Library Dependencies in Domain

**Symptoms:**
- Domain entities reference external NuGet packages
- Domain code uses third-party libraries (e.g., Newtonsoft.Json, AutoMapper)
- Domain depends on Application or Infrastructure packages

**Examples:**
```csharp
// ❌ VIOLATION
using Newtonsoft.Json; // External library in Domain

public class Entity
{
    [JsonProperty("id")] // External attribute
    public int Id { get; set; }
}

// ✅ CORRECT
public class Entity
{
    public int Id { get; set; } // Pure Domain, no external dependencies
}
```

**Detection:**
```bash
# Check Domain project dependencies
dotnet list Prisma/Code/Src/CSharp/Domain/*.csproj package
# Should only show standard .NET packages
```

**Remediation:**
- Remove external package references from Domain
- Use standard .NET attributes/types only
- Move serialization concerns to Infrastructure adapters

---

### Category 2: Layer Boundary Violations

#### Violation 2.1: Application Services in Domain

**Symptoms:**
- Domain entities call Application services
- Domain code references Application types
- Domain depends on Application project

**Examples:**
```csharp
// ❌ VIOLATION
public class Entity
{
    public void Process(ApplicationService service) // Application dependency
    {
        service.DoSomething();
    }
}

// ✅ CORRECT
public class Entity
{
    public void Process(IDomainService service) // Domain interface
    {
        service.DoSomething();
    }
}
```

**Detection:**
```bash
# Check Domain project references
dotnet list Prisma/Code/Src/CSharp/Domain/*.csproj reference
# Should NOT reference Application project
```

**Remediation:**
- Define Domain interfaces for required services
- Move service logic to Domain or Application as appropriate
- Use dependency inversion principle

---

#### Violation 2.2: Cross-Infrastructure Dependencies

**Symptoms:**
- Infrastructure.Classification depends on Infrastructure.Extraction
- Infrastructure projects reference each other
- Infrastructure services call other Infrastructure services directly

**Examples:**
```csharp
// ❌ VIOLATION
namespace Infrastructure.Classification;

public class Service
{
    public Service()
    {
        _extractor = new ExtractionService(); // Cross-Infrastructure dependency
    }
}

// ✅ CORRECT
namespace Infrastructure.Classification;

public class Service
{
    public Service(IMetadataExtractor extractor) // Domain interface
    {
        _extractor = extractor;
    }
}
```

**Detection:**
```bash
# Check Infrastructure project references
dotnet list Prisma/Code/Src/CSharp/Infrastructure.*/*.csproj reference
# Should NOT reference other Infrastructure projects
```

**Remediation:**
- Define Domain interfaces for cross-Infrastructure communication
- Use dependency injection to wire Infrastructure services
- Follow hexagonal architecture port/adapter pattern

---

### Category 3: Test Architecture Violations

#### Violation 3.1: Infrastructure Types in Application Tests

**Symptoms:**
- Application tests instantiate Infrastructure classes
- Application tests use `new InfrastructureService(...)`
- Application test project references Infrastructure projects

**Examples:**
```csharp
// ❌ VIOLATION
public class ApplicationServiceTests
{
    public ApplicationServiceTests()
    {
        _resolver = new PersonIdentityResolverService(logger); // Infrastructure type
    }
}

// ✅ CORRECT
public class ApplicationServiceTests
{
    public ApplicationServiceTests()
    {
        _resolver = Substitute.For<IPersonIdentityResolver>(); // Mock Domain interface
    }
}
```

**Detection:**
```bash
# Check Application test project references
dotnet list Prisma/Code/Src/CSharp/Tests.Application/*.csproj reference
# Should NOT reference Infrastructure projects (except Testing.Infrastructure)
```

**Remediation:**
- Mock Domain interfaces using NSubstitute
- Remove Infrastructure project references from test projects
- Use test doubles for all Infrastructure dependencies

**Reference:** [ADR-002: Test Project Split Violations](./adr-002-test-project-split-clean-architecture-violations.md)

---

#### Violation 3.2: Application Services in Infrastructure Tests

**Symptoms:**
- Infrastructure tests instantiate Application services
- Infrastructure test project references Application project
- Infrastructure tests test Application logic instead of Infrastructure adapters

**Examples:**
```csharp
// ❌ VIOLATION
public class InfrastructureTests
{
    public InfrastructureTests()
    {
        _appService = new DocumentIngestionService(...); // Application service
    }
}

// ✅ CORRECT - Option 1: Move to Application tests
// Move test to Tests.Application project

// ✅ CORRECT - Option 2: Mock Application interfaces
public class InfrastructureTests
{
    public InfrastructureTests()
    {
        _appService = Substitute.For<IDocumentIngestion>(); // Mock if needed
    }
}
```

**Remediation:**
- Move tests to appropriate test project
- Test Infrastructure adapters in isolation
- Mock Application interfaces if integration testing needed

---

### Category 4: Data Access Violations

#### Violation 4.1: Direct Database Access in Application

**Symptoms:**
- Application services use `DbContext` directly
- Application code executes raw SQL queries
- Application bypasses repository abstraction

**Examples:**
```csharp
// ❌ VIOLATION
public class ApplicationService
{
    private readonly DbContext _context;
    
    public async Task<List<Entity>> GetEntities()
    {
        return await _context.Database
            .SqlQueryRaw<Entity>("SELECT * FROM Entities")
            .ToListAsync();
    }
}

// ✅ CORRECT
public class ApplicationService
{
    private readonly IRepository<Entity, int> _repository;
    
    public async Task<Result<List<Entity>>> GetEntities()
    {
        return await _repository.ListAsync(cancellationToken);
    }
}
```

**Remediation:**
- Use repository pattern for all data access
- Define Domain interfaces for data access
- Implement repositories in Infrastructure layer

---

#### Violation 4.2: Domain Entities with EF Core Attributes

**Symptoms:**
- Domain entities have `[Key]`, `[Required]`, `[ForeignKey]` attributes
- Domain entities reference EF Core configuration
- Domain depends on EF Core for entity definition

**Examples:**
```csharp
// ❌ VIOLATION
using Microsoft.EntityFrameworkCore;

public class Entity
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; }
}

// ✅ CORRECT
public class Entity
{
    public int Id { get; set; }
    public string Name { get; set; }
}

// EF Core configuration in Infrastructure
public class EntityConfiguration : IEntityTypeConfiguration<Entity>
{
    public void Configure(EntityTypeBuilder<Entity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired();
    }
}
```

**Remediation:**
- Remove EF Core attributes from Domain entities
- Move EF Core configuration to Infrastructure layer
- Use `IEntityTypeConfiguration<T>` in Infrastructure

---

## Automated Detection

### Static Analysis Rules

#### 1. Project Dependency Checks

**Rule:** Application project must not reference Infrastructure projects

**Check:**
```bash
dotnet list Prisma/Code/Src/CSharp/Application/*.csproj reference | grep Infrastructure
# Should return no results
```

#### 2. Package Dependency Checks

**Rule:** Application project must not reference Infrastructure packages

**Check:**
```bash
dotnet list Prisma/Code/Src/CSharp/Application/*.csproj package | grep -E "EntityFrameworkCore|Microsoft.EntityFrameworkCore"
# Should return no results
```

#### 3. Namespace Usage Checks

**Rule:** Application code must not use Infrastructure namespaces

**Check:**
```bash
grep -r "using.*Infrastructure" Prisma/Code/Src/CSharp/Application/
# Should return no results (except Testing.Infrastructure)
```

#### 4. Type Instantiation Checks

**Rule:** Application code must not instantiate Infrastructure types

**Check:**
```bash
grep -r "new.*Infrastructure\." Prisma/Code/Src/CSharp/Application/
# Should return no results
```

---

## Code Review Checklist

### Application Layer Review

- [ ] No `using Microsoft.EntityFrameworkCore` statements
- [ ] No `DbContext` or `DbSet<T>` usage
- [ ] No Infrastructure namespace imports
- [ ] No `new InfrastructureService(...)` instantiation
- [ ] All dependencies injected via constructor
- [ ] All dependencies are Domain interfaces
- [ ] No direct database queries
- [ ] Repository pattern used for data access

### Domain Layer Review

- [ ] No external NuGet package references (except standard library)
- [ ] No Application or Infrastructure dependencies
- [ ] No EF Core attributes on entities
- [ ] Pure domain logic (no infrastructure concerns)
- [ ] Interfaces defined for all external dependencies
- [ ] Entities are POCOs (Plain Old CLR Objects)

### Infrastructure Layer Review

- [ ] Implements Domain interfaces
- [ ] No cross-Infrastructure dependencies
- [ ] EF Core configuration in Infrastructure only
- [ ] Adapters translate Domain to Infrastructure concerns
- [ ] No Domain logic in Infrastructure

### Test Layer Review

- [ ] Application tests mock Domain interfaces (not Infrastructure types)
- [ ] Infrastructure tests test adapters in isolation
- [ ] No Infrastructure project references in Application test project
- [ ] No Application project references in Infrastructure test projects
- [ ] Tests follow clean architecture boundaries

---

## Remediation Process

### Step 1: Identify Violation

1. Use automated detection tools (grep, dotnet list)
2. Review code during pull requests
3. Run architecture validation scripts

### Step 2: Document Violation

1. Create ADR documenting the violation
2. Describe the problem and impact
3. Propose remediation approach

### Step 3: Plan Remediation

1. Identify affected code
2. Design solution following clean architecture
3. Estimate effort and dependencies

### Step 4: Implement Remediation

1. Create Domain interfaces if needed
2. Refactor Application code to use interfaces
3. Implement Infrastructure adapters
4. Update dependency injection configuration

### Step 5: Validate Fix

1. Run automated checks
2. Verify no new violations introduced
3. Ensure tests pass
4. Update ADR with completion status

---

## Prevention Strategies

### 1. Project Structure Enforcement

**Use Solution Folders:**
```
Solution
├── Domain/
│   └── Domain.csproj (no dependencies)
├── Application/
│   └── Application.csproj (depends on Domain)
└── Infrastructure/
    ├── Infrastructure.Database/
    │   └── Infrastructure.Database.csproj (depends on Domain, Application)
    └── Infrastructure.Classification/
        └── Infrastructure.Classification.csproj (depends on Domain)
```

### 2. Dependency Injection Configuration

**Centralize DI in Infrastructure:**
- All Infrastructure implementations registered in Infrastructure projects
- Application only knows about Domain interfaces
- Use extension methods for clean DI setup

### 3. Code Review Guidelines

**Mandatory Checks:**
- Review project references in `.csproj` files
- Check `using` statements in code files
- Verify constructor dependencies are interfaces
- Ensure tests follow architecture boundaries

### 4. Automated Validation

**CI/CD Pipeline Checks:**
```yaml
# Example GitHub Actions check
- name: Check Architecture Violations
  run: |
    # Check Application has no Infrastructure references
    dotnet list Application/*.csproj reference | grep Infrastructure && exit 1 || exit 0
    
    # Check Application has no EF Core package
    dotnet list Application/*.csproj package | grep EntityFrameworkCore && exit 1 || exit 0
    
    # Check for Infrastructure namespaces in Application
    grep -r "using.*Infrastructure" Application/ && exit 1 || exit 0
```

---

## Examples of Correct Patterns

### Correct: Application Service with Repository

```csharp
namespace ExxerCube.Prisma.Application.Services;

public class DocumentIngestionService
{
    private readonly IRepository<FileMetadata, int> _fileMetadataRepository;
    private readonly IDownloadTracker _downloadTracker;
    private readonly IDownloadStorage _downloadStorage;
    
    public DocumentIngestionService(
        IRepository<FileMetadata, int> fileMetadataRepository,
        IDownloadTracker downloadTracker,
        IDownloadStorage downloadStorage)
    {
        _fileMetadataRepository = fileMetadataRepository;
        _downloadTracker = downloadTracker;
        _downloadStorage = downloadStorage;
    }
    
    public async Task<Result> IngestDocumentAsync(string url, CancellationToken cancellationToken)
    {
        // Application logic using Domain interfaces
        var existsResult = await _downloadTracker.IsDuplicateAsync(checksum, cancellationToken);
        if (existsResult.IsFailure) return existsResult.ToResult();
        
        // ... rest of logic
    }
}
```

### Correct: Domain Interface Definition

```csharp
namespace ExxerCube.Prisma.Domain.Interfaces;

public interface IDownloadTracker
{
    Task<Result<bool>> IsDuplicateAsync(string checksum, CancellationToken cancellationToken = default);
    Task<Result<FileMetadata?>> GetFileMetadataByChecksumAsync(string checksum, CancellationToken cancellationToken = default);
}
```

### Correct: Infrastructure Implementation

```csharp
namespace ExxerCube.Prisma.Infrastructure.Database;

public class DownloadTrackerService : IDownloadTracker
{
    private readonly PrismaDbContext _dbContext;
    
    public DownloadTrackerService(PrismaDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<Result<bool>> IsDuplicateAsync(string checksum, CancellationToken cancellationToken)
    {
        // EF Core implementation details hidden here
        var exists = await _dbContext.FileMetadata
            .AnyAsync(f => f.Checksum == checksum, cancellationToken);
        return Result<bool>.Success(exists);
    }
}
```

---

## Related Documents

- [ADR-002: Test Project Split Violations](./adr-002-test-project-split-clean-architecture-violations.md)
- [ADR-003: Test Suite Split Decision](./adr-003-test-suite-split-decision.md)
- [ADR-004: EF Core Application Layer Violation](./adr-004-efcore-application-layer-violation.md)

---

## Quick Reference: Violation Detection Commands

```bash
# Check Application for Infrastructure references
dotnet list Prisma/Code/Src/CSharp/Application/*.csproj reference | grep Infrastructure

# Check Application for EF Core package
dotnet list Prisma/Code/Src/CSharp/Application/*.csproj package | grep EntityFrameworkCore

# Check for Infrastructure namespaces in Application code
grep -r "using.*Infrastructure" Prisma/Code/Src/CSharp/Application/

# Check for Infrastructure type instantiation in Application
grep -r "new.*Infrastructure\." Prisma/Code/Src/CSharp/Application/

# Check Domain for external packages
dotnet list Prisma/Code/Src/CSharp/Domain/*.csproj package

# Check Domain for Application/Infrastructure references
dotnet list Prisma/Code/Src/CSharp/Domain/*.csproj reference

# Check Infrastructure for cross-dependencies
dotnet list Prisma/Code/Src/CSharp/Infrastructure.*/*.csproj reference | grep Infrastructure
```

---

**Last Updated:** 2025-01-15  
**Next Review:** Quarterly or when new violation patterns are discovered

