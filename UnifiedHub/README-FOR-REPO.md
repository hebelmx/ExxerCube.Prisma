# ExxerCube.Prisma.SignalR.Abstractions

[![NuGet Version](https://img.shields.io/nuget/v/ExxerCube.Prisma.SignalR.Abstractions.svg)](https://www.nuget.org/packages/ExxerCube.Prisma.SignalR.Abstractions)
[![Mutation Score](https://img.shields.io/badge/mutation%20score-%3E80%25-brightgreen)](https://stryker-mutator.io/)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

A **transport-agnostic real-time communication abstraction** following Clean Architecture and Hexagonal Architecture principles. Think of it as a **transportation hub** (train station/airport) where:
- **Something moves** → Messages and data flow through hubs
- **Something tracks** → Health and metrics are monitored
- **Something displays** → Dashboards expose real-time information

Currently implemented with **SignalR**, but the abstraction layer allows easy transport swapping (WebSockets, gRPC streaming, etc.) without changing application code.

## 🎯 Purpose

This package provides **transport-agnostic real-time communication abstractions** used by **5+ projects** to ensure:
- ✅ **DRY Principle** - Single source of truth for real-time communication patterns
- ✅ **Code Drift Prevention** - Consistent patterns across all projects
- ✅ **Transport Independence** - Clean Architecture allows swapping SignalR → WebSockets → gRPC without code changes
- ✅ **Maintainability** - Fix bugs once, all projects benefit
- ✅ **Testability** - Comprehensive test coverage with >80% mutation score
- ✅ **Universal Use Cases** - Dashboards and health checks are needed everywhere

### The Transportation Hub Metaphor

Like a **train station or airport**, this package abstracts the infrastructure:
- **Platforms/Tracks** → Transport layer (SignalR, WebSockets, gRPC)
- **Traffic Control** → Connection management, routing, health monitoring
- **Information Displays** → Dashboards showing real-time status

Your application code depends only on **abstractions**, not the transport technology. Change the transport implementation without touching your business logic.

## 🏗️ Architecture

### Three Core Abstractions

1. **`ExxerHub<T>`** - Generic SignalR hub abstraction
   - Hexagonal architecture compliance
   - Built-in connection management
   - Standardized error handling with `Result<T>`

2. **`ServiceHealth<T>`** - Service health monitoring
   - Real-time health status updates via SignalR
   - Multiple health check types (liveness, readiness, custom)
   - Health status change notifications

3. **`Dashboard<T>`** - Dashboard component abstraction
   - Automatic SignalR connection management
   - Message batching and throttling
   - Automatic reconnection handling
   - MudBlazor integration

### Three Actors Pattern

- **Something going** → `ExxerHub<T>` (events/data flowing through hubs)
- **Something tracking it** → `ServiceHealth<T>` (monitoring health/metrics)
- **Something displaying** → `Dashboard<T>` (dashboards showing real-time updates)

## 📦 Installation

```bash
dotnet add package ExxerCube.Prisma.SignalR.Abstractions
```

## 🚀 Quick Start

### 1. Configure Services

```csharp
// In Program.cs or Startup.cs
builder.Services.AddSignalR();
builder.Services.AddSignalRAbstractions();
builder.Services.AddServiceHealth<MyHealthData>();
```

### 2. Create a Hub

```csharp
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Hubs;

public class MyHub : ExxerHub<MyMessage>
{
    public MyHub(ILogger<MyHub> logger) : base(logger) { }

    public async Task SendMessage(MyMessage message, CancellationToken cancellationToken)
    {
        var result = await ValidateMessage(message);
        if (result.IsFailure)
            return;

        await Clients.All.SendAsync("ReceiveMessage", message, cancellationToken);
    }
}

// Map in Program.cs
app.MapHub<MyHub>("/hubs/mymessages");
```

### 3. Create a Dashboard

```csharp
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Dashboards;
using Microsoft.AspNetCore.SignalR.Client;

public class MyDashboard : Dashboard<MyMessage>
{
    public MyDashboard(
        HubConnection hubConnection,
        ReconnectionStrategy reconnectionStrategy,
        ILogger<MyDashboard> logger)
        : base(hubConnection, reconnectionStrategy, logger)
    {
        hubConnection.On<MyMessage>("ReceiveMessage", OnMessageReceived);
    }
}

// Use in Blazor component
@inject MyDashboard Dashboard

<MudCard>
    <MudCardContent>
        @foreach (var message in Dashboard.Data)
        {
            <p>@message.Content</p>
        }
    </MudCardContent>
</MudCard>
```

### 4. Monitor Health

```csharp
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class MyServiceHealth : ServiceHealth<MyHealthData>
{
    public MyServiceHealth(ILogger<MyServiceHealth> logger) : base(logger) { }
}

// Update health status
await serviceHealth.UpdateHealthAsync(
    HealthStatus.Healthy,
    new MyHealthData { Status = "Operational", Metrics = metrics },
    cancellationToken);
```

## 📚 Documentation

- **[Architecture Decision Record (ADR-001)](docs/adr/ADR-001-SignalR-Unified-Hub-Abstraction.md)** - Complete architectural context
- **[API Documentation](docs/api/)** - Detailed API reference
- **[Usage Examples](examples/)** - Real-world usage examples
- **[Testing Guide](docs/testing.md)** - How to test with these abstractions

## 🧪 Testing

This package includes comprehensive test coverage:

- ✅ **Unit Tests** - Test abstractions independently
- ✅ **Integration Tests** - Test with real SignalR hubs
- ✅ **E2E Tests** - Test complete server→client flows
- ✅ **Mutation Testing** - >80% mutation score

### Running Tests

```bash
# Run all tests
dotnet test

# Run mutation testing
dotnet stryker
```

## 🎯 Use Cases

### Universal Needs (Every Application Has These)

**Dashboards** - Real-time data visualization
```csharp
// Any dashboard: monitoring, analytics, status displays
public class MonitoringDashboard : Dashboard<MetricUpdate> { }
public class AnalyticsDashboard : Dashboard<AnalyticsData> { }
public class StatusDashboard : Dashboard<SystemStatus> { }
```

**Health Checks** - Service monitoring and observability
```csharp
// Any service needs health monitoring
public class ApiHealth : ServiceHealth<ApiHealthData> { }
public class DatabaseHealth : ServiceHealth<DbHealthData> { }
public class MicroserviceHealth : ServiceHealth<ServiceMetrics> { }
```

### Domain-Specific Examples

**Document Processing**
```csharp
public class DocumentIngestionHub : ExxerHub<FileMetadata> { }
public class DocumentIngestionDashboard : Dashboard<FileMetadata> { }
```

**Classification & AI**
```csharp
public class ClassificationHub : ExxerHub<ClassificationResult> { }
public class ClassificationDashboard : Dashboard<ClassificationResult> { }
```

**SLA & Performance**
```csharp
public class SlaHub : ExxerHub<SlaStatus> { }
public class SlaDashboard : Dashboard<SlaStatus> { }
```

**E-Commerce**
```csharp
public class OrderHub : ExxerHub<OrderUpdate> { }
public class OrderDashboard : Dashboard<OrderUpdate> { }
```

**IoT & Telemetry**
```csharp
public class TelemetryHub : ExxerHub<SensorData> { }
public class TelemetryDashboard : Dashboard<SensorData> { }
```

## 🔧 Configuration

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
      }
    }
  }
}
```

## 🏛️ Architecture Principles

- **Clean Architecture** - Application code depends only on abstractions, not implementations
- **Hexagonal Architecture** - Clean separation of concerns, transport-agnostic design
- **Transport Independence** - Swap SignalR → WebSockets → gRPC without changing application code
- **Railway-Oriented Programming** - `Result<T>` pattern, no exceptions for control flow
- **Dependency Injection** - Full DI support
- **Type Safety** - Generic type parameters for compile-time safety
- **Testability** - Easy to mock and test

### Why Transport Independence Matters

```csharp
// Your application code depends only on abstractions
public class MyService
{
    private readonly IExxerHub<MyMessage> _hub;  // Abstraction, not SignalR!
    
    public MyService(IExxerHub<MyMessage> hub) => _hub = hub;
    
    // This code works regardless of transport (SignalR, WebSocket, gRPC)
    public async Task SendMessage(MyMessage msg, CancellationToken ct)
    {
        await _hub.BroadcastAsync(msg, ct);
    }
}
```

**Benefits**:
- ✅ Test with mocks (no transport needed)
- ✅ Swap transports without code changes
- ✅ Support multiple transports simultaneously
- ✅ Future-proof your architecture

## 📊 Features

- ✅ **Connection Management** - Automatic reconnection, state tracking
- ✅ **Message Batching** - Built-in batching for high-frequency updates
- ✅ **Message Throttling** - Prevents UI overload
- ✅ **Error Handling** - Standardized `Result<T>` pattern
- ✅ **Health Monitoring** - Real-time health status updates
- ✅ **Blazor Integration** - Native Blazor Server support
- ✅ **MudBlazor Support** - Integration with MudBlazor components

## 🔗 Dependencies

- `Microsoft.AspNetCore.SignalR` - SignalR infrastructure
- `Microsoft.AspNetCore.SignalR.Client` - SignalR client
- `IndQuestResults` - Result pattern implementation
- `MudBlazor` - UI components (optional)

## 🤝 Contributing

This package is used by multiple projects. When contributing:

1. **Test Thoroughly** - Ensure all tests pass
2. **Maintain Mutation Score** - Keep mutation score >80%
3. **Update Documentation** - Keep examples and docs current
4. **Consider Impact** - Changes affect 5+ projects

## 📝 License

[Your License Here]

## 🙏 Acknowledgments

- Inspired by ADR-017 Hexagonal Architecture patterns
- Railway-Oriented Programming patterns from F# community
- SignalR best practices from Microsoft documentation

---

**Version**: 1.0.0  
**Target Framework**: .NET 10.0  
**Mutation Score**: >80%  
**Status**: Production Ready ✅

