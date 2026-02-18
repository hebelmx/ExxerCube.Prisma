# ADR-003: Metrics Services Infrastructure Layer Placement

## Status
**APPROVED** - Metrics services moved from Application to Infrastructure layer to comply with Hexagonal Architecture

## Context

The project follows **Hexagonal Architecture** (Ports and Adapters) principles where:
- **Ports (Interfaces)**: Reside in the Domain layer
- **Adapters (Implementations)**: Reside in the Infrastructure layer
- **Application Layer**: Should handle orchestration and use ports, but *not* implement them

**The Problem**:
- `ProcessingMetricsService` and `ProcessingContext` were located in `Application.Services` namespace
- Both classes implemented Domain interfaces (`IProcessingMetricsService`, `IProcessingContext`)
- Architecture tests were failing:
  - `Application_Services_Should_Not_Implement_Domain_Interfaces`
  - `Domain_Interfaces_Should_Only_Be_Implemented_In_Infrastructure`

**Architectural Violation**:
```
Application Layer (❌ Should NOT implement Domain interfaces)
├── ProcessingMetricsService implements IProcessingMetricsService
└── ProcessingContext implements IProcessingContext
```

**Architectural Requirements**:
- Domain interfaces must only be implemented in Infrastructure layer
- Application layer should orchestrate and use interfaces, not implement them
- Maintain separation of concerns and dependency inversion

## Decision

**Move `ProcessingMetricsService` and `ProcessingContext` from Application layer to Infrastructure layer**

### **Approach**

1. **Create New Infrastructure Project**: `ExxerCube.Prisma.Infrastructure.Metrics`
   - Follows pattern from existing `Infrastructure.Classification` project
   - References Domain project only (no Application dependency)
   - Contains implementations of Domain interfaces

2. **Move Services to Infrastructure**:
   - `ProcessingMetricsService` → `Infrastructure.Metrics/ProcessingMetricsService.cs`
   - `ProcessingContext` → `Infrastructure.Metrics/ProcessingContext.cs`
   - Update namespaces accordingly

3. **Update Dependencies**:
   - Change `ProcessingContext` constructor to use `IProcessingMetricsService` interface instead of concrete type
   - Prevents circular dependencies
   - Maintains proper abstraction

4. **Create DI Extension**:
   - New `AddMetricsServices` extension method in `Infrastructure.Metrics`
   - Centralized registration of metrics services
   - Follows existing Infrastructure project patterns

5. **Update All References**:
   - Update `MetricsController` to use `IProcessingMetricsService` interface
   - Update UI components (`Dashboard.razor`, `OCRDemo.razor`) to use interface
   - Update `HealthCheckService` to use interface
   - Update Infrastructure DI registration to use new extension method

6. **Create Test Project**: `ExxerCube.Prisma.Tests.Infrastructure.Metrics`
   - Comprehensive unit tests for moved services
   - Follows xUnit v3 patterns with Shouldly and NSubstitute

### **Architecture After Refactoring**

```
Domain Layer (Ports)
├── IProcessingMetricsService (interface)
└── IProcessingContext (interface)

Application Layer (Orchestration)
├── OcrProcessingService (uses IProcessingMetricsService)
├── HealthCheckService (uses IProcessingMetricsService)
└── Other services (use interfaces, do NOT implement)

Infrastructure Layer (Adapters)
└── Infrastructure.Metrics
    ├── ProcessingMetricsService (implements IProcessingMetricsService) ✅
    └── ProcessingContext (implements IProcessingContext) ✅
```

## Rationale

### **Hexagonal Architecture Compliance** ✅

**1. Separation of Concerns**
- Domain defines contracts (interfaces)
- Infrastructure provides implementations (adapters)
- Application orchestrates using contracts
- Clear separation between layers

**2. Dependency Inversion Principle**
- High-level modules (Application) depend on abstractions (interfaces)
- Low-level modules (Infrastructure) implement abstractions
- Application layer does not depend on Infrastructure implementations

**3. Testability**
- Application services can be tested with interface mocks
- Infrastructure implementations can be tested independently
- Clear boundaries for unit and integration testing

**4. Maintainability**
- Changes to metrics implementation don't affect Application layer
- Can swap implementations without changing Application code
- Easier to understand and maintain

### **Why Infrastructure.Metrics Instead of Application.Services**

**1. Architectural Compliance**
- Domain interfaces must be implemented in Infrastructure layer
- Application layer should not implement Domain interfaces
- Follows established architectural patterns

**2. Dependency Direction**
- Infrastructure can depend on Domain (allowed)
- Application can depend on Domain (allowed)
- Application should NOT depend on Infrastructure implementations (uses interfaces)
- Infrastructure implements Domain interfaces (correct)

**3. Future Extensibility**
- Can add alternative metrics implementations in Infrastructure
- Can swap implementations via DI without changing Application code
- Supports multiple infrastructure adapters

**4. Consistency**
- Follows pattern from other Infrastructure projects (`Infrastructure.Classification`, `Infrastructure.Extraction`, etc.)
- Consistent project structure across codebase
- Easier for developers to understand

### **Why Not Keep in Application**

**1. Architectural Violation**
- Application layer implementing Domain interfaces violates Hexagonal Architecture
- Architecture tests explicitly enforce this rule
- Would require changing architectural tests (not recommended)

**2. Dependency Issues**
- Application layer should not have concrete implementations of Domain interfaces
- Breaks dependency inversion principle
- Makes testing more difficult

**3. Separation of Concerns**
- Metrics collection is infrastructure concern (monitoring, observability)
- Not core business logic (which belongs in Application)
- Infrastructure layer is appropriate for cross-cutting concerns

## Consequences

### **Positive Consequences** ✅

**1. Architectural Compliance**
- ✅ Architecture tests now pass
- ✅ Complies with Hexagonal Architecture principles
- ✅ Proper separation of concerns
- ✅ Dependency inversion maintained

**2. Code Organization**
- ✅ Clear project structure
- ✅ Consistent with other Infrastructure projects
- ✅ Easier to locate metrics-related code
- ✅ Better namespace organization

**3. Maintainability**
- ✅ Changes to metrics implementation isolated to Infrastructure layer
- ✅ Application layer unaffected by infrastructure changes
- ✅ Clear boundaries between layers
- ✅ Easier to understand and maintain

**4. Testability**
- ✅ Application tests use interface mocks (already correct)
- ✅ Infrastructure tests can test implementations independently
- ✅ Clear separation for unit and integration tests
- ✅ Better test organization

**5. Extensibility**
- ✅ Can add alternative metrics implementations
- ✅ Can swap implementations via DI
- ✅ Supports multiple infrastructure adapters
- ✅ Future-proof architecture

### **Negative Consequences** ❌

**1. Additional Project**
- More projects to manage
- **Mitigation**: Follows existing pattern, minimal overhead

**2. Migration Effort**
- Required updating multiple files
- **Mitigation**: One-time effort, automated via refactoring tools

**3. Learning Curve**
- Developers need to know where metrics services are located
- **Mitigation**: Clear documentation, consistent with other Infrastructure projects

**4. Project References**
- Additional project references in solution
- **Mitigation**: Standard practice, minimal impact

## Implementation Details

### **Project Structure**

```
Infrastructure.Metrics/
├── ExxerCube.Prisma.Infrastructure.Metrics.csproj
├── ProcessingMetricsService.cs
├── ProcessingContext.cs
└── DependencyInjection/
    └── ServiceCollectionExtensions.cs

Tests.Infrastructure.Metrics/
├── ExxerCube.Prisma.Tests.Infrastructure.Metrics.csproj
├── ProcessingMetricsServiceTests.cs
└── ProcessingContextTests.cs
```

### **Dependency Injection**

**Before**:
```csharp
// In Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs
services.AddSingleton<ProcessingMetricsService>(provider => {
    var logger = provider.GetRequiredService<ILogger<ProcessingMetricsService>>();
    return new ProcessingMetricsService(logger, pythonConfiguration.MaxConcurrency);
});
```

**After**:
```csharp
// In Infrastructure.Metrics/DependencyInjection/ServiceCollectionExtensions.cs
public static IServiceCollection AddMetricsServices(
    this IServiceCollection services, 
    int maxConcurrency = 5)
{
    services.AddSingleton<IProcessingMetricsService>(provider =>
    {
        var logger = provider.GetRequiredService<ILogger<ProcessingMetricsService>>();
        return new ProcessingMetricsService(logger, maxConcurrency);
    });
    return services;
}

// In Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs
services.AddMetricsServices(pythonConfiguration.MaxConcurrency);
```

### **Interface Usage**

**Before**:
```csharp
// MetricsController.cs
private readonly ProcessingMetricsService _metricsService; // Concrete type ❌
```

**After**:
```csharp
// MetricsController.cs
private readonly IProcessingMetricsService _metricsService; // Interface ✅
```

## Testing Strategy

### **Architecture Tests**
- ✅ `Application_Services_Should_Not_Implement_Domain_Interfaces` - Now passes
- ✅ `Domain_Interfaces_Should_Only_Be_Implemented_In_Infrastructure` - Now passes

### **Unit Tests**
- ✅ `ProcessingMetricsServiceTests.cs` - Comprehensive tests for metrics service
- ✅ `ProcessingContextTests.cs` - Tests for context lifecycle and disposal
- ✅ Uses NSubstitute for mocking
- ✅ Uses Shouldly for assertions
- ✅ Follows xUnit v3 patterns

### **Integration Tests**
- ✅ Application tests already use interface mocks (no changes needed)
- ✅ DI registration verified to work correctly
- ✅ All references updated and verified

## Migration Notes

### **Breaking Changes**
- None - All public APIs use interfaces, no breaking changes

### **Backward Compatibility**
- ✅ Fully backward compatible
- ✅ All existing code continues to work
- ✅ Only internal structure changed

### **Developer Impact**
- Developers should use `IProcessingMetricsService` interface
- Concrete `ProcessingMetricsService` is now internal to Infrastructure layer
- DI registration moved to `AddMetricsServices` extension method

## Related Decisions

- **ADR-001**: SignalR Unified Hub Abstraction - Similar pattern for infrastructure abstractions
- **ADR-002**: Custom PDF Signing - Infrastructure layer implementation pattern
- **Hexagonal Architecture**: Core architectural pattern being enforced

## References

- [Hexagonal Architecture (Ports and Adapters)](https://alistair.cockburn.us/hexagonal-architecture/)
- [Clean Architecture Principles](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Dependency Inversion Principle](https://en.wikipedia.org/wiki/Dependency_inversion_principle)
- [Implementation Document](../implementation/metrics-services-infrastructure-refactoring.md)
- Architecture Tests: `Tests.Architecture/HexagonalArchitectureTests.cs`

## Approval

**Approved by**: System Architect  
**Date**: 2025-01-15  
**Review Date**: 2025-07-15 (6 months)

---

**Note**: This ADR documents the decision to move metrics services from Application to Infrastructure layer to comply with Hexagonal Architecture principles. This ensures proper separation of concerns and maintains architectural integrity while preserving all existing functionality.

