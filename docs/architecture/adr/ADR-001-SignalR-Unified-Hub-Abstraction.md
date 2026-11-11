# ADR-001: SignalR Unified Hub Abstraction Infrastructure

## Status
**APPROVED** - SignalR unified hub abstraction pattern for real-time UI components

## Context

The project requires real-time UI updates for multiple dashboard components across Stories 1.1-1.9:
- **Story 1.1**: Document Ingestion Dashboard (real-time download feed)
- **Story 1.2**: Classification Results Display (real-time processing updates)
- **Story 1.3**: Field Matching Visualization (real-time matching progress)
- **Story 1.4**: Identity Resolution Display (real-time resolution updates)
- **Story 1.5**: SLA Dashboard (real-time SLA tracking)
- **Story 1.6**: Manual Review Interface (real-time review status)
- **Story 1.7**: Export Management Dashboard (real-time export progress)
- **Story 1.8**: PDF Export Status (real-time signing progress)
- **Story 1.9**: Audit Trail Viewer (real-time audit log updates)

**The Problem**:
- Each UI component would need to implement SignalR connection logic independently
- Duplication of connection management, error handling, reconnection logic
- Inconsistent patterns across components
- Difficult to maintain and test
- No standardized approach for service health monitoring
- No reusable dashboard component patterns

**Architectural Requirements**:
- Follow Hexagonal Architecture principles
- Support Blazor Server hosting model
- Independent package without ExxerAI project dependencies
- Reusable abstractions for hub, health monitoring, and dashboard components
- Clean separation of concerns (Hub logic, Connection management, UI components)

## Decision

**Implement Unified SignalR Hub Abstraction Infrastructure with three core abstractions:**

1. **`ExxerHub<T>`** - Generic SignalR hub abstraction following hexagonal architecture
2. **`ServiceHealth<T>`** - Service health monitoring with real-time updates
3. **`Dashboard<T>`** - Dashboard component abstraction for Blazor Server

### Architecture Pattern

**Three Actors Pattern** (inspired by ADR-017):
- **Something going** - Events and data flowing through hubs (`ExxerHub<T>`)
- **Something tracking it** - Services monitoring health and metrics (`ServiceHealth<T>`)
- **Something displaying** - Dashboards presenting real-time updates (`Dashboard<T>`)

### Package Structure

```
ExxerCube.Prisma.SignalR.Abstractions/
├── Abstractions/
│   ├── Hubs/
│   │   ├── IExxerHub.cs
│   │   └── ExxerHub.cs
│   ├── Health/
│   │   ├── IServiceHealth.cs
│   │   └── ServiceHealth.cs
│   └── Dashboards/
│       ├── IDashboard.cs
│       └── Dashboard.cs
├── Infrastructure/
│   ├── Connection/
│   │   ├── ConnectionState.cs
│   │   ├── ConnectionManager.cs
│   │   └── ReconnectionStrategy.cs
│   └── Messaging/
│       ├── MessageBatcher.cs
│       └── MessageThrottler.cs
├── Presentation/
│   └── Blazor/
│       ├── DashboardComponent.cs
│       ├── ConnectionStateIndicator.cs
│       └── ErrorDisplay.cs
├── Extensions/
│   ├── ServiceCollectionExtensions.cs
│   └── MudBlazorExtensions.cs
└── Tests/
    ├── Unit/
    ├── Integration/
    └── Helpers/
```

## Rationale

### **ExxerHub<T> Advantages**
- ✅ **Generic Type Safety**: Compile-time type checking for hub data types
- ✅ **Hexagonal Architecture**: Clean separation between domain logic and SignalR infrastructure
- ✅ **Reusability**: Single abstraction for all hub implementations
- ✅ **Testability**: Easy to mock and test hub logic independently
- ✅ **Connection Management**: Built-in connection state tracking and reconnection logic
- ✅ **Error Handling**: Standardized error handling with Result<T> pattern

### **ServiceHealth<T> Advantages**
- ✅ **Real-time Monitoring**: Automatic health status updates via SignalR
- ✅ **Multiple Health Types**: Support for liveness, readiness, and custom health checks
- ✅ **Type-Safe**: Generic type parameter for health data structures
- ✅ **Integration**: Seamless integration with existing health check infrastructure
- ✅ **Observability**: Health status change notifications for UI components

### **Dashboard<T> Advantages**
- ✅ **Component Lifecycle**: Automatic SignalR connection management in Blazor components
- ✅ **Message Batching**: Built-in batching and throttling for high-frequency updates
- ✅ **Automatic Reconnection**: Handles connection loss and reconnection automatically
- ✅ **MudBlazor Integration**: Native integration with MudBlazor components
- ✅ **Performance**: Optimized for Blazor Server with virtual scrolling support

### **Independent Package Advantages**
- ✅ **No ExxerAI Dependencies**: Clean separation from ExxerAI project
- ✅ **Reusability**: Can be used across multiple projects
- ✅ **NuGet Distribution**: Can be packaged and distributed independently
- ✅ **Versioning**: Independent versioning strategy

## Consequences

### **Positive Consequences** ✅

**1. Code Reusability**
- Single implementation of SignalR connection logic
- Consistent patterns across all UI components
- Reduced code duplication

**2. Maintainability**
- Centralized connection management
- Single point of change for SignalR patterns
- Easier to update and maintain

**3. Testability**
- Abstractions are easily mockable
- Unit tests don't require SignalR infrastructure
- Integration tests can use test hubs

**4. Performance**
- Message batching reduces SignalR traffic
- Throttling prevents UI overload
- Optimized for Blazor Server

**5. Developer Experience**
- Simple, consistent API for all components
- Clear patterns and examples
- IntelliSense support with generic types

**6. Hexagonal Architecture Compliance**
- Clean separation of concerns
- Domain logic independent of SignalR
- Infrastructure details isolated

### **Negative Consequences** ❌

**1. Additional Abstraction Layer**
- More interfaces and classes to understand
- Slight learning curve for developers
- **Mitigation**: Comprehensive documentation and examples

**2. Package Management**
- Additional NuGet package to maintain
- Version compatibility considerations
- **Mitigation**: Semantic versioning and clear versioning strategy

**3. Initial Development Effort**
- Time required to build abstractions
- **Mitigation**: Reusable across all stories, amortized cost

## Implementation Plan

### **Phase 1: Package Structure and Foundation**
- [ ] Create `ExxerCube.Prisma.SignalR.Abstractions` project
- [ ] Remove ExxerAI dependencies
- [ ] Configure NuGet package metadata
- [ ] Set up project structure

### **Phase 2: Core Abstractions**
- [ ] Implement `ExxerHub<T>` base class
- [ ] Implement `ServiceHealth<T>` abstraction
- [ ] Implement `Dashboard<T>` base component
- [ ] Add connection state management

### **Phase 3: Connection Management**
- [ ] Implement reconnection strategies
- [ ] Implement error handling and recovery
- [ ] Implement message batching and throttling
- [ ] Add connection state indicators

### **Phase 4: Blazor Integration**
- [ ] Create Blazor Server components
- [ ] Integrate with MudBlazor
- [ ] Create helper extensions
- [ ] Add DI registration methods

### **Phase 5: Testing**
- [ ] Write unit tests for abstractions
- [ ] Write integration tests
- [ ] Create test utilities
- [ ] Performance testing

### **Phase 6: Documentation**
- [ ] Create API documentation
- [ ] Create usage examples
- [ ] Document configuration options
- [ ] Create integration guides

## Design Decisions

### **1. Generic Type Parameters**
**Decision**: Use `ExxerHub<T>`, `ServiceHealth<T>`, `Dashboard<T>` with generic type parameters

**Rationale**:
- Type safety at compile time
- Flexible for different data types
- IntelliSense support
- Prevents runtime type errors

### **2. Result<T> Pattern**
**Decision**: All operations return `Result<T>` instead of throwing exceptions

**Rationale**:
- Aligns with project's Railway-Oriented Programming patterns
- Consistent error handling
- No exceptions for control flow
- Better error propagation

### **3. Connection State Management**
**Decision**: Explicit connection state tracking with events

**Rationale**:
- UI components need to know connection status
- Enables connection state indicators
- Better user experience
- Easier debugging

### **4. Message Batching and Throttling**
**Decision**: Built-in batching and throttling mechanisms

**Rationale**:
- Prevents SignalR overload
- Reduces UI update frequency
- Better performance
- Configurable thresholds

### **5. Blazor Server Focus**
**Decision**: Optimize for Blazor Server hosting model

**Rationale**:
- Project uses Blazor Server
- Server-side SignalR is more efficient
- Better integration with existing infrastructure
- Can extend to WebAssembly later if needed

## Integration Points

### **With Existing System**
- Integrates with existing SignalR infrastructure
- Extends existing hub implementations
- Works alongside MudBlazor components
- Compatible with existing DI container

### **With Future Stories**
- Story 1.1: `Dashboard<FileMetadata>` for ingestion dashboard
- Story 1.2: `Dashboard<ClassificationResult>` for classification display
- Story 1.3: `Dashboard<MatchedFields>` for field matching view
- Story 1.4: `Dashboard<ResolvedPerson>` for identity resolution
- Story 1.5: `Dashboard<SlaStatus>` for SLA dashboard
- Story 1.6: `Dashboard<ReviewCase>` for manual review interface
- Story 1.7: `Dashboard<ExportStatus>` for export management
- Story 1.8: `Dashboard<PdfSigningStatus>` for PDF signing progress
- Story 1.9: `Dashboard<AuditRecord>` for audit trail viewer

## Configuration

```json
{
  "SignalR": {
    "Abstractions": {
      "Reconnection": {
        "MaxRetries": 5,
        "InitialDelay": 1000,
        "MaxDelay": 30000,
        "BackoffMultiplier": 2.0
      },
      "Messaging": {
        "BatchSize": 50,
        "BatchInterval": 1000,
        "ThrottleInterval": 100
      },
      "Connection": {
        "Timeout": 30000,
        "KeepAliveInterval": 15000
      }
    }
  }
}
```

## Testing Strategy

### **Unit Tests**
- Test each abstraction independently
- Mock SignalR dependencies
- Test connection state transitions
- Test error handling scenarios
- Test message batching/throttling logic

### **Integration Tests**
- Test with real SignalR hub
- Test with Blazor Server components
- Test reconnection scenarios
- Test concurrent connections
- Test high-frequency updates

### **Performance Tests**
- Test message throughput
- Test connection scalability
- Test memory usage with many connections
- Test CPU usage during high-frequency updates

## Risk Mitigation

### **High Risks**
1. **Complexity**: Abstractions may be too complex for simple use cases
   - **Mitigation**: Provide simple examples and clear documentation
2. **Performance**: Abstraction layer may introduce overhead
   - **Mitigation**: Performance testing and optimization
3. **Dependency Management**: Package dependencies may conflict
   - **Mitigation**: Careful dependency analysis and versioning

### **Medium Risks**
1. **Learning Curve**: Developers need to learn new patterns
   - **Mitigation**: Comprehensive documentation and examples
2. **Migration**: Existing SignalR code may need refactoring
   - **Mitigation**: Gradual migration, backward compatibility where possible

## References

- [Story 1.10: SignalR Unified Hub Abstraction](../stories/1.10.signalr-unified-hub-abstraction.md)
- [UI Enhancements: Stories 1.1-1.3](../qa/ui-enhancements-stories-1.1-1.3-bmad.md)
- [Hexagonal Architecture Principles](https://alistair.cockburn.us/hexagonal-architecture/)
- [Railway-Oriented Programming](https://fsharpforfunandprofit.com/rop/)
- [ASP.NET Core SignalR Documentation](https://docs.microsoft.com/aspnet/core/signalr/introduction)

## Approval

**Approved by**: System Architect  
**Date**: 2025-01-15  
**Review Date**: 2025-07-15 (6 months)

---

**Note**: This ADR establishes the SignalR unified hub abstraction as the standard approach for all real-time UI components in the ExxerCube.Prisma project. All future SignalR implementations should use these abstractions.

