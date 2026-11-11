# ADR-004: EF Core Application Layer Violation - Generic Repository Pattern Remediation

**Status:** ✅ **Implemented**  
**Date:** 2025-01-15  
**Deciders:** Development Team, Architecture Team  
**Tags:** clean-architecture, hexagonal-architecture, dependency-inversion, repository-pattern, ef-core

**Related ADRs:**
- [ADR-002: Test Project Split - Clean Architecture Violations](./adr-002-test-project-split-clean-architecture-violations.md)
- [ADR-003: Test Suite Split Decision](./adr-003-test-suite-split-decision.md)

---

## Context

During code review and architecture analysis, a critical clean architecture violation was discovered:

**Violation:** The Application layer was directly using Entity Framework Core (`DbContext`, `DbSet<T>`) for data access operations.

**Problems Identified:**

1. **Infrastructure Leakage:** Application layer had direct dependencies on `Microsoft.EntityFrameworkCore` package
2. **Tight Coupling:** Application services were tightly coupled to EF Core implementation details
3. **Testability Issues:** Difficult to unit test Application services without EF Core infrastructure
4. **Architecture Violation:** Violates Dependency Inversion Principle - Application depends on Infrastructure instead of abstractions
5. **Technology Lock-in:** Application layer cannot switch persistence technologies without code changes

**Clean Architecture Requirements:**
- Application layer should depend only on Domain interfaces (Ports)
- Infrastructure layer implements Domain interfaces (Adapters)
- Application layer must not reference Infrastructure projects or packages
- Data access should be abstracted through Domain-defined contracts

---

## Decision

**Introduce a Generic Repository Pattern with Specification Pattern in the Domain layer to abstract data access operations.**

### Solution Architecture

#### 1. Domain Layer Contracts (Ports)

**Location:** `Domain/Interfaces/Contracts/`

**IRepository<T, TId> Interface:**
- Generic repository abstraction for CRUD operations
- Uses `Expression<Func<T, bool>>` predicates for filtering (LINQ-compatible)
- Returns `Result<T>` for Railway-Oriented Programming error handling
- Domain-agnostic - no EF Core dependencies
- Supports specifications for complex queries

**ISpecification<T> Interface:**
- Represents query intent using expression trees
- Supports filtering, sorting, eager loading, and paging
- Pure expression tree-based (no infrastructure types)
- LINQ-to-SQL/EF Core transpiler compliant
- Domain-agnostic design

#### 2. Infrastructure Layer Implementation (Adapter)

**Location:** `Infrastructure.Database/Repositories/`

**EfCoreRepository<T, TId> Class:**
- Implements `IRepository<T, TId>` using EF Core
- Uses `SpecificationEvaluator<T>` to apply specifications to EF Core queries
- Translates Domain expressions to EF Core operations
- Handles all EF Core-specific concerns (change tracking, async operations)

**SpecificationEvaluator<T> Class:**
- Applies `ISpecification<T>` to `IQueryable<T>`
- Translates specification properties to EF Core LINQ operations
- Handles includes, ordering, filtering, and paging

### Key Design Decisions

1. **Expression Trees for Query Abstraction:**
   - Uses `Expression<Func<T, bool>>` instead of raw EF Core queries
   - Enables LINQ-to-SQL translation by EF Core
   - Keeps Domain layer infrastructure-agnostic

2. **Specification Pattern:**
   - Encapsulates complex query logic in reusable specifications
   - Supports composition and reuse
   - Expression tree-based for transpiler compatibility

3. **Result<T> Return Types:**
   - All repository methods return `Result<T>` for error handling
   - Follows Railway-Oriented Programming pattern
   - Consistent with existing Domain patterns

4. **Generic Repository:**
   - Single generic interface for all entity types
   - Reduces code duplication
   - Maintains type safety through generics

---

## Implementation

### Phase 1: Domain Contracts ✅ **Completed**

#### 1.1 Created IRepository<T, TId> Interface

**File:** `Domain/Interfaces/Contracts/IRepository.cs`

**Key Features:**
- Query methods: `GetByIdAsync`, `FindAsync`, `ExistsAsync`, `CountAsync`, `ListAsync`
- Specification support: `ListAsync(ISpecification<T>)`, `FirstOrDefaultAsync(ISpecification<T>)`
- Command methods: `AddAsync`, `AddRangeAsync`, `UpdateAsync`, `RemoveAsync`, `RemoveRangeAsync`
- Unit of Work: `SaveChangesAsync`
- Projection support: `SelectAsync<TResult>` for DTOs

**Validation:**
- ✅ No EF Core references
- ✅ Uses `System.Linq.Expressions` only
- ✅ Returns `Result<T>` for all operations
- ✅ Domain-agnostic design

#### 1.2 Created ISpecification<T> Interface

**File:** `Domain/Interfaces/Contracts/ISpecification.cs`

**Key Features:**
- `Criteria`: `Expression<Func<T, bool>>?` for filtering
- `OrderBy` / `OrderByDescending`: `Expression<Func<T, object>>?` for sorting
- `Includes`: `IReadOnlyList<Expression<Func<T, object>>>` for eager loading
- `Skip` / `Take`: `int?` for paging
- `IsPagingEnabled`: Computed property

**Validation:**
- ✅ Pure expression tree-based
- ✅ No infrastructure dependencies
- ✅ LINQ-to-SQL transpiler compliant

### Phase 2: Infrastructure Implementation ✅ **Completed**

#### 2.1 Created EfCoreRepository<T, TId> Implementation

**File:** `Infrastructure.Database/Repositories/EfCoreRepository.cs`

**Key Features:**
- Implements `IRepository<T, TId>`
- Uses `PrismaDbContext` internally (Infrastructure concern)
- Applies specifications via `SpecificationEvaluator<T>`
- Handles EF Core async operations
- Wraps exceptions in `Result<T>` failures

**Validation:**
- ✅ Implements Domain interface correctly
- ✅ EF Core dependencies isolated to Infrastructure
- ✅ Proper error handling and cancellation support

#### 2.2 Created SpecificationEvaluator<T> Helper

**File:** `Infrastructure.Database/Repositories/SpecificationEvaluator.cs`

**Key Features:**
- Applies `ISpecification<T>` to `IQueryable<T>`
- Translates specification properties to EF Core LINQ operations
- Handles includes, ordering, filtering, paging

**Validation:**
- ✅ Correctly translates specifications to EF Core queries
- ✅ Maintains query composition for efficient SQL generation

### Phase 3: Dependency Injection ✅ **Completed**

#### 3.1 Registered Repository in DI Container

**File:** `Infrastructure.Database/DependencyInjection/ServiceCollectionExtensions.cs`

**Registration:**
```csharp
services.AddScoped(typeof(IRepository<,>), typeof(EfCoreRepository<,>));
```

**Validation:**
- ✅ Generic type registration works correctly
- ✅ Scoped lifetime appropriate for DbContext

### Phase 4: Application Layer Refactoring ✅ **Completed**

#### 4.1 Removed EF Core Dependencies

**Actions:**
- Removed `Microsoft.EntityFrameworkCore` package reference from Application project
- Removed `using Microsoft.EntityFrameworkCore` statements
- Removed direct `DbContext` / `DbSet<T>` usage

**Validation:**
- ✅ No EF Core references in Application layer
- ✅ Application project compiles without EF Core package
- ✅ All data access goes through `IRepository<T, TId>`

#### 4.2 Updated Application Services

**Changes:**
- Application services now inject `IRepository<T, TId>` instead of `DbContext`
- Query logic uses repository methods with expression predicates
- Complex queries use `ISpecification<T>` pattern

**Example Migration:**

**Before (Violation):**
```csharp
public class SomeApplicationService
{
    private readonly PrismaDbContext _dbContext;
    
    public SomeApplicationService(PrismaDbContext dbContext)
    {
        _dbContext = dbContext; // ❌ Infrastructure dependency
    }
    
    public async Task<Result<List<Entity>>> GetEntitiesAsync()
    {
        var entities = await _dbContext.Entities
            .Where(e => e.IsActive)
            .ToListAsync(); // ❌ Direct EF Core usage
        return Result<List<Entity>>.Success(entities);
    }
}
```

**After (Compliant):**
```csharp
public class SomeApplicationService
{
    private readonly IRepository<Entity, int> _repository;
    
    public SomeApplicationService(IRepository<Entity, int> repository)
    {
        _repository = repository; // ✅ Domain interface dependency
    }
    
    public async Task<Result<List<Entity>>> GetEntitiesAsync()
    {
        var result = await _repository.FindAsync(
            e => e.IsActive, // ✅ Expression predicate
            cancellationToken);
        return result; // ✅ Already returns Result<T>
    }
}
```

---

## Consequences

### Positive Consequences ✅

1. **Architecture Compliance:**
   - Application layer no longer depends on Infrastructure
   - Dependency Inversion Principle properly enforced
   - Clean architecture boundaries respected

2. **Testability:**
   - Application services can be unit tested with mock repositories
   - No need for EF Core in-memory database for unit tests
   - Faster test execution

3. **Flexibility:**
   - Can switch persistence technology without changing Application code
   - Repository implementation can be swapped (EF Core → Dapper → MongoDB)
   - Domain contracts remain stable

4. **Maintainability:**
   - Clear separation of concerns
   - Infrastructure changes don't affect Application layer
   - Easier to understand and modify

5. **Reusability:**
   - Generic repository works for all entity types
   - Specification pattern enables reusable query logic
   - Consistent data access patterns across the application

### Negative Consequences ⚠️

1. **Abstraction Overhead:**
   - Additional layer of indirection
   - Slightly more complex than direct EF Core usage
   - **Mitigation:** Benefits outweigh costs for clean architecture compliance

2. **Learning Curve:**
   - Team needs to understand repository and specification patterns
   - Expression tree syntax may be unfamiliar
   - **Mitigation:** Well-documented patterns with examples

3. **Performance Considerations:**
   - Expression trees are translated to SQL efficiently by EF Core
   - No significant performance impact
   - **Mitigation:** Specifications enable query optimization

---

## Alternatives Considered

### Alternative 1: Keep Direct EF Core Usage
- **Pros:** Simpler, less abstraction
- **Cons:** Architecture violation, tight coupling, poor testability
- **Decision:** ❌ Rejected - violates clean architecture principles

### Alternative 2: Unit of Work Pattern Only
- **Pros:** Simpler than repository pattern
- **Cons:** Still requires EF Core in Application layer
- **Decision:** ❌ Rejected - doesn't solve the dependency issue

### Alternative 3: CQRS with Separate Read/Write Models
- **Pros:** Excellent separation, optimized for reads/writes
- **Cons:** More complex, overkill for current needs
- **Decision:** ⚠️ Considered but deferred - can evolve to this pattern later

### Alternative 4: Generic Repository with Specifications (Selected)
- **Pros:** Clean abstraction, testable, flexible, LINQ-compatible
- **Cons:** Some abstraction overhead
- **Decision:** ✅ **Selected** - Best balance of architecture compliance and practicality

---

## Validation

### Success Criteria ✅

- [x] Application layer has no EF Core package references
- [x] Application layer has no `DbContext` or `DbSet<T>` usage
- [x] All data access goes through `IRepository<T, TId>` interface
- [x] Domain contracts are infrastructure-agnostic
- [x] Infrastructure implements Domain contracts correctly
- [x] All tests pass with new repository pattern
- [x] Performance is acceptable (no degradation)

### Architecture Validation

**Dependency Check:**
```bash
# Verify Application has no EF Core references
grep -r "Microsoft.EntityFrameworkCore" Prisma/Code/Src/CSharp/Application/
# Should return no results

# Verify Application uses IRepository
grep -r "IRepository" Prisma/Code/Src/CSharp/Application/
# Should show repository usage
```

**Project References Check:**
- Application project should NOT reference Infrastructure.Database project
- Application project should reference Domain project (for interfaces)
- Infrastructure.Database project implements Domain interfaces

---

## Implementation Details

### Repository Interface Signature

```csharp
public interface IRepository<T, in TId>
    where T : class
{
    // Queries
    Task<Result<T?>> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<T>>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<Result<bool>> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<Result<int>> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<T>>> ListAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<T>>> ListAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<T>>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);
    Task<Result<T?>> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TResult>>> SelectAsync<TResult>(Expression<Func<T, bool>> predicate, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);
    
    // Commands
    Task<Result> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task<Result> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task<Result> RemoveAsync(T entity, CancellationToken cancellationToken = default);
    Task<Result> RemoveRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

### Specification Interface Signature

```csharp
public interface ISpecification<T>
    where T : class
{
    Expression<Func<T, bool>>? Criteria { get; }
    Expression<Func<T, object>>? OrderBy { get; }
    Expression<Func<T, object>>? OrderByDescending { get; }
    IReadOnlyList<Expression<Func<T, object>>> Includes { get; }
    int? Skip { get; }
    int? Take { get; }
    bool IsPagingEnabled => Skip.HasValue || Take.HasValue;
}
```

### Usage Example

**Simple Query:**
```csharp
var result = await _repository.FindAsync(
    e => e.IsActive && e.CreatedDate > DateTime.UtcNow.AddDays(-30),
    cancellationToken);
```

**Specification Query:**
```csharp
var spec = new ActiveEntitiesSpecification(DateTime.UtcNow.AddDays(-30))
{
    OrderBy = e => e.CreatedDate,
    Skip = 0,
    Take = 10
};

var result = await _repository.ListAsync(spec, cancellationToken);
```

---

## References

- **[📋 Architecture Violation Guidelines](./architecture-violation-guidelines.md)** - Comprehensive guidelines for detecting and preventing violations
- [ADR-002: Test Project Split Violations](./adr-002-test-project-split-clean-architecture-violations.md)
- [ADR-003: Test Suite Split Decision](./adr-003-test-suite-split-decision.md)
- [Clean Architecture Patterns](../../.cursor/rules/1008_CleanArchitecturePatterns.mdc)
- [Domain-Driven Design Patterns](../../.cursor/rules/1007_DomainDrivenDesignPatterns.mdc)
- [C# Coding Standards](../../.cursor/rules/1001_CSharpCodingStandards.mdc)
- [Repository Pattern - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)
- [Specification Pattern - Martin Fowler](https://www.martinfowler.com/apsupp/spec.pdf)

---

## Notes

- Repository pattern is domain-agnostic - works with any entity type
- Specifications use expression trees for LINQ-to-SQL translation compatibility
- EF Core efficiently translates expression trees to SQL queries
- Future: Can add more specialized repository interfaces if needed (e.g., `IReadRepository<T>`, `IWriteRepository<T>`)
- Future: Can evolve to CQRS pattern if read/write optimization becomes critical

---

**Last Updated:** 2025-01-15  
**Next Review:** After 3 months of production use

