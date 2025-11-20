# UnifiedHub - SignalR Abstractions Testing & Examples

## 🎯 Purpose

This folder contains the **Transport-Agnostic Real-Time Communication Abstraction** package (`ExxerCube.Prisma.SignalR.Abstractions`) with comprehensive testing infrastructure, usage examples, and integration tests.

**Critical Context**: 
- This package is a **shared foundation** used by **5+ projects** to prevent code duplication and drift
- It's not just for Stories 1.1-1.9—it's infrastructure that multiple teams depend on
- **Transport-Agnostic Design**: Currently uses SignalR, but the abstraction layer allows swapping to WebSockets, gRPC, etc. without changing application code
- **Universal Needs**: Dashboards and health checks are needed everywhere—this package provides the abstraction

### The Transportation Hub Metaphor

Like a **train station or airport**, this package abstracts the infrastructure:
- **Platforms/Tracks** → Transport layer (SignalR, WebSockets, gRPC)
- **Traffic Control** → Connection management, routing, health monitoring  
- **Information Displays** → Dashboards showing real-time status

Your application code depends only on **abstractions**, not the transport technology.

## 📋 Architectural Context

### ADR-001: SignalR Unified Hub Abstraction

**Status**: APPROVED  
**Reference**: `docs/adr/ADR-001-SignalR-Unified-Hub-Abstraction.md`

This package implements the architectural decision to create a unified SignalR abstraction infrastructure with three core abstractions:

1. **`ExxerHub<T>`** - Generic SignalR hub abstraction (hexagonal architecture)
2. **`ServiceHealth<T>`** - Service health monitoring with real-time updates
3. **`Dashboard<T>`** - Dashboard component abstraction for Blazor Server

### Three Actors Pattern

- **Something going** → `ExxerHub<T>` (events/data flowing through hubs)
- **Something tracking it** → `ServiceHealth<T>` (monitoring health/metrics)
- **Something displaying** → `Dashboard<T>` (dashboards showing real-time updates)

### Why This Package Exists

**Problem Solved**:
- Without this package: Each of 5+ projects would implement SignalR connection logic independently
- Result: Code duplication, inconsistent patterns, difficult maintenance, code drift

**Solution**:
- Single source of truth for SignalR abstractions
- Consistent patterns across all projects
- Centralized bug fixes and improvements benefit all projects
- Prevents code drift through shared abstractions

## 📁 Project Structure

```
UnifiedHub/
├── ExxerCube.Prisma.SignalR.Abstractions/          # Main abstraction package
│   ├── Abstractions/                                # Core abstractions (ExxerHub, ServiceHealth, Dashboard)
│   ├── Infrastructure/                             # Connection management, messaging
│   ├── Presentation/                                # Blazor components
│   └── Extensions/                                  # DI extensions
│
├── ExxerCube.Prisma.SignalR.Abstractions.Tests/    # Unit tests (xUnit v2)
│   └── [Unit tests for all abstractions]
│
├── ExxerAI.RealTimeCommunication.Server/            # Test server (usage example)
│   ├── Hubs/                                        # Hub implementations using ExxerHub<T>
│   ├── Services/                                    # Background services, diagnostics
│   └── Program.cs                                   # Server entry point
│
├── ExxerAI.RealTimeCommunication.Client/           # Test client (usage example)
│   ├── Dashboards/                                  # Dashboard implementations using Dashboard<T>
│   ├── Health/                                      # Health monitoring using ServiceHealth<T>
│   └── Services/                                    # Background services
│
├── ExxerAI.RealTimeCommunication.Server.Tests/     # Integration & E2E tests (xUnit v2)
│   ├── Integration/                                 # Integration tests
│   ├── E2E/                                         # End-to-end tests
│   └── SignalRServerFactory.cs                      # WebApplicationFactory for testing
│
└── ExxerCube.Prisma.SignalR.Abstractions.sln      # Solution file
```

## 🎯 Current Objectives

### Primary Goal
**Improve mutation testing score from 39.38% to >80%** by adding comprehensive integration tests that exercise real SignalR behavior.

### Why Integration Tests Matter
- **Unit tests alone can't achieve high mutation scores** - They use mocks, missing real SignalR behavior
- **Integration tests exercise code paths** that unit tests miss (real connections, DI, end-to-end flows)
- **Multi-project dependency** - If this package has bugs, 5+ projects are affected

### Testing Strategy (ADR-001 Phase 5)

1. ✅ **Unit Tests** - Complete (test abstractions independently with mocks)
2. ⏳ **Integration Tests** - In Progress (test with real SignalR hubs)
3. ⏳ **E2E Tests** - In Progress (test complete server→client flows)
4. ⏳ **Performance Tests** - Future (message throughput, scalability)

## ✅ What's Been Completed

### Core Package
- ✅ `ExxerHub<T>` - Generic SignalR hub abstraction
- ✅ `ServiceHealth<T>` - Health monitoring abstraction
- ✅ `Dashboard<T>` - Dashboard component abstraction
- ✅ Connection management (reconnection, error handling, state tracking)
- ✅ Message batching and throttling
- ✅ Blazor Server integration
- ✅ DI extensions (`AddSignalRAbstractions()`)

### Test Infrastructure
- ✅ Unit tests (xUnit v2, Shouldly, NSubstitute)
- ✅ Test server project (`ExxerAI.RealTimeCommunication.Server`)
- ✅ Test client project (`ExxerAI.RealTimeCommunication.Client`)
- ✅ Integration test project (converted from xUnit v3 → v2)
- ✅ `SignalRServerFactory` - WebApplicationFactory for real SignalR testing

### Usage Examples
- ✅ `TestSystemHub` - Example hub using `ExxerHub<T>`
- ✅ `TestHealthHub` - Example hub for health monitoring
- ✅ `SystemDashboard` - Example dashboard using `Dashboard<T>`
- ✅ `HealthDashboard` - Example dashboard for health updates
- ✅ `ClientServiceHealth` - Example health monitoring using `ServiceHealth<T>`
- ✅ `SystemDiagnosticsCollector` - System metrics (CPU, Memory, Process/Thread counts)
- ✅ Background services demonstrating real-world usage

### E2E Tests
- ✅ `E2E_ServerHubToClientDashboard_CompleteFlow` - Server → Hub → Client
- ✅ `E2E_HealthMonitoring_ServerToClient_CompleteFlow` - Health monitoring flow
- ✅ `E2E_MultipleClients_ReceiveBroadcastMessages` - Multiple clients
- ✅ `E2E_ConnectionStateManagement_CompleteFlow` - Connection management
- ✅ `E2E_HealthStatusChanges_TriggerEvents` - Health status changes

## ⏳ Current Status

### Mutation Testing
- **Current Score**: 39.38% (86 killed, 59 survived, 78 no coverage)
- **Target Score**: >80%
- **Issue**: Low score due to lack of integration tests

### Remaining Work
1. ⏳ Fix compilation errors in test projects
2. ⏳ Run all tests to ensure they pass
3. ⏳ Run Stryker mutation testing
4. ⏳ Analyze surviving mutants
5. ⏳ Add targeted tests to kill remaining mutants
6. ⏳ Iterate until mutation score > 80%

## 🚀 Usage as Examples

These projects serve as **reference implementations** for teams using the package:

### For Hub Implementations
```csharp
// See: ExxerAI.RealTimeCommunication.Server/Hubs/TestSystemHub.cs
public class TestSystemHub : ExxerHub<string>
{
    public TestSystemHub(ILogger<TestSystemHub> logger) : base(logger) { }
    
    public async Task BroadcastMessage(string messageType, string data, CancellationToken cancellationToken)
    {
        await Clients.All.SendAsync(messageType, data, cancellationToken);
    }
}
```

### For Dashboard Components
```csharp
// See: ExxerAI.RealTimeCommunication.Client/Dashboards/SystemDashboard.cs
public class SystemDashboard : Dashboard<SystemMessage>
{
    public SystemDashboard(
        HubConnection hubConnection,
        ReconnectionStrategy reconnectionStrategy,
        ILogger<SystemDashboard> logger)
        : base(hubConnection, reconnectionStrategy, logger)
    {
        hubConnection.On<SystemMessage>("ReceiveMessage", OnMessageReceived);
    }
}
```

### For Health Monitoring
```csharp
// See: ExxerAI.RealTimeCommunication.Client/Health/ClientServiceHealth.cs
public class ClientServiceHealth : ServiceHealth<HealthUpdate>
{
    public ClientServiceHealth(ILogger<ClientServiceHealth> logger) : base(logger) { }
}
```

## 🧪 Running Tests

```bash
# Run all tests
dotnet test

# Run only E2E tests
dotnet test --filter "FullyQualifiedName~EndToEndTests"

# Run only integration tests
dotnet test --filter "FullyQualifiedName~DashboardIntegrationTests"

# Run mutation testing
cd ExxerCube.Prisma.SignalR.Abstractions
dotnet stryker
```

## 📊 System Diagnostics Health Monitoring

Both server and client monitor system health using `System.Diagnostics`:

- **CPU Usage**: Process-based calculation (cross-platform)
- **Memory Usage**: Working set and total memory
- **Process Count**: Total system processes
- **Thread Count**: Current process threads
- **Health Status**: Automatic determination (Healthy/Degraded/Unhealthy)

## 🔗 Related Documentation

- **ADR-001**: `docs/adr/ADR-001-SignalR-Unified-Hub-Abstraction.md`
- **Integration Test Plan**: `docs/tasks/integration-test-improvement-plan.md`
- **Story 1.10**: `docs/stories/1.10.signalr-unified-hub-abstraction.md`

## ⚠️ Important Notes

1. **Multi-Project Dependency**: This package is used by 5+ projects. Changes must be carefully tested.
2. **DRY Principle**: This package prevents code duplication and drift across projects.
3. **Mutation Score Goal**: >80% to ensure high-quality, maintainable code.
4. **Usage Examples**: Server and Client projects demonstrate correct usage patterns.
5. **Test Infrastructure**: All tests use xUnit v2 (Stryker compatibility).

## 🎯 Success Criteria

- ✅ Mutation score ≥ 80%
- ✅ All integration tests passing
- ✅ E2E tests covering critical flows
- ✅ Usage examples demonstrate all three abstractions
- ✅ Package ready for multi-project consumption

---

**Status**: ⏳ In Progress - Integration Testing Phase  
**Last Updated**: 2025-01-15  
**Next Steps**: Fix compilation errors → Run tests → Run Stryker → Improve mutation score
