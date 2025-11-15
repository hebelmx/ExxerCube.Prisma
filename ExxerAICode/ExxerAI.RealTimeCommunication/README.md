# ExxerAI.RealTimeCommunication

A clean architecture library for real-time communication with improved patterns borrowed from battle-tested industrial solutions.

## Overview

This library provides infrastructure-agnostic abstractions for real-time communication, allowing you to build applications that can work with multiple real-time providers (SignalR, WebSockets, gRPC streaming) while maintaining clean architecture principles.

## Key Features

- **Clean Architecture Compliance**: Application layer remains infrastructure-agnostic
- **Multiple Provider Support**: SignalR, WebSocket, and gRPC streaming adapters
- **Improved Patterns**: Enhanced connection management, retry logic, and error handling
- **Rate-Limited Logging**: Prevents log spam while maintaining visibility
- **Thread-Safe Operations**: Proper semaphore-based connection management
- **Result Pattern**: No exceptions for control flow - all operations return `Result<T>`
- **Comprehensive Testing**: Testable abstractions with proper mocking support

## Quick Start

### 1. Installation

```bash
dotnet add package ExxerAI.RealTimeCommunication
```

### 2. Configuration

Add to your `appsettings.json`:

```json
{
  "RealTimeCommunication": {
    "Url": "https://localhost:5001/hubs/communication",
    "Provider": "SignalR",
    "RetryIntervalSeconds": 5,
    "MaxRetryAttempts": 3,
    "EnableAutomaticReconnect": true
  }
}
```

### 3. Service Registration

```csharp
// In Program.cs or Startup.cs
services.AddRealTimeCommunication(configuration);
```

### 4. Usage in Application Layer

```csharp
public class DocumentService
{
    private readonly IEventBroadcastingPort _eventBroadcasting;
    
    public DocumentService(IEventBroadcastingPort eventBroadcasting)
    {
        _eventBroadcasting = eventBroadcasting;
    }
    
    public async Task<Result> ProcessDocumentAsync(string documentId, CancellationToken cancellationToken)
    {
        // ... business logic ...
        
        var documentEvent = new DocumentEvent("DocumentProcessed", new { documentId }, documentId);
        return await _eventBroadcasting.BroadcastDocumentEventAsync(documentEvent, cancellationToken);
    }
}
```

## Architecture

The library follows hexagonal architecture principles:

```
Application Layer (Your Code)
├── Uses: IRealTimeCommunicationPort
├── Uses: IEventBroadcastingPort
└── Uses: INotificationPort

Infrastructure Layer (This Library)
├── SignalRAdapter : IRealTimeCommunicationPort
├── WebSocketAdapter : IRealTimeCommunicationPort
└── GrpcStreamingAdapter : IRealTimeCommunicationPort
```

## Interfaces

### Core Communication

- `IRealTimeCommunicationPort` - Basic real-time communication operations
- `IEventBroadcastingPort` - Event broadcasting with typed events
- `INotificationPort` - User notifications and alerts
- `IConnectionFactory` - Connection creation with retry logic
- `IConnectionManager` - Thread-safe connection lifecycle management

### Infrastructure

- `IConnection` - Abstract connection interface
- `IRetryPolicy` - Configurable retry strategies with exponential backoff

## Improved Patterns from IndTrace

This library incorporates and improves several patterns from the IndTrace industrial solution:

### 1. Rate-Limited Logging
```csharp
// Prevents log spam while maintaining visibility
logger.LogWarningRateLimited("Connection lost", TimeSpan.FromMinutes(1));
```

### 2. Thread-Safe Connection Management
```csharp
// Semaphore-based connection management prevents race conditions
var connectionResult = await connectionManager.EnsureConnectionAsync(cancellationToken);
```

### 3. Exponential Backoff with Jitter
```csharp
// Prevents thundering herd problems in distributed systems
var retryOptions = RetryPolicyOptions.Fast; // or Aggressive, or custom
```

### 4. Result Pattern
```csharp
// No exceptions for control flow - all operations return Result<T>
var result = await communicationPort.BroadcastMessageAsync(request, cancellationToken);
if (result.IsSuccess)
{
    // Handle success
}
else
{
    // Handle failure - result.Errors contains details
}
```

## Testing

The library is designed for testability:

```csharp
[Fact]
public async Task Should_BroadcastEvent_When_DocumentProcessed()
{
    // Arrange
    var mockPort = Substitute.For<IEventBroadcastingPort>();
    var service = new DocumentService(mockPort);
    
    mockPort.BroadcastDocumentEventAsync(Arg.Any<DocumentEvent>(), Arg.Any<CancellationToken>())
        .Returns(Result.Success());
    
    // Act
    var result = await service.ProcessDocumentAsync("doc123", CancellationToken.None);
    
    // Assert
    result.IsSuccess.ShouldBeTrue();
    await mockPort.Received(1).BroadcastDocumentEventAsync(
        Arg.Is<DocumentEvent>(e => e.DocumentId == "doc123"),
        Arg.Any<CancellationToken>());
}
```

## Provider Implementations

### SignalR (Default)
- Full SignalR feature support
- Automatic reconnection with exponential backoff
- Group management
- User-specific messaging

### WebSocket (Planned)
- Direct WebSocket communication
- Custom protocol support
- Low-level control

### gRPC Streaming (Planned)
- Bi-directional streaming
- Type-safe communication
- High performance

## Quality Standards

- **Zero Warnings**: TreatWarningsAsErrors enforced
- **Nullable Reference Types**: Enabled with proper null handling
- **Result Pattern**: No exceptions for business logic
- **Comprehensive Logging**: Rate-limited to prevent spam
- **Industrial Grade**: Patterns tested in production manufacturing environments

## Contributing

This library extracts and improves patterns from battle-tested industrial solutions. When contributing:

1. Maintain clean architecture principles
2. Use Result<T> pattern instead of exceptions
3. Ensure thread safety
4. Add comprehensive tests
5. Follow existing naming conventions

## License

MIT License - see LICENSE file for details

## Support

- Documentation: [GitHub Wiki](https://github.com/exxerai/realtimecommunication/wiki)
- Issues: [GitHub Issues](https://github.com/exxerai/realtimecommunication/issues)
- NuGet: [NuGet Package](https://www.nuget.org/packages/ExxerAI.RealTimeCommunication)