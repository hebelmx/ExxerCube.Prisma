# ExxerCube.Prisma.SignalR.Abstractions

📡 SignalR unified hub abstraction infrastructure following Hexagonal Architecture and Railway-Oriented Programming patterns.

## Overview

This package provides reusable abstractions for building real-time UI components in Blazor Server applications:

- **`ExxerHub<T>`** - Generic SignalR hub abstraction with type-safe messaging
- **`ServiceHealth<T>`** - Service health monitoring with real-time updates
- **`Dashboard<T>`** - Dashboard component abstraction for Blazor Server

## Features

✅ **Hexagonal Architecture** - Clean separation of concerns  
✅ **Railway-Oriented Programming** - Result<T> pattern for error handling  
✅ **Type Safety** - Generic type parameters for compile-time safety  
✅ **Connection Management** - Automatic reconnection and state tracking  
✅ **Message Batching** - Built-in batching and throttling for performance  
✅ **Blazor Server Integration** - Native support for Blazor Server components  
✅ **MudBlazor Support** - Integration with MudBlazor UI components  

## Installation

```bash
dotnet add package ExxerCube.Prisma.SignalR.Abstractions
```

## Quick Start

### 1. Configure Services

```csharp
using ExxerCube.Prisma.SignalR.Abstractions.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add SignalR
builder.Services.AddSignalR();

// Add SignalR Abstractions
builder.Services.AddSignalRAbstractions(options =>
{
    options.MaxRetries = 5;
    options.InitialDelay = 1000;
    options.MaxDelay = 30000;
    options.BackoffMultiplier = 2.0;
});
```

### 2. Create a Hub

```csharp
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Hubs;
using Microsoft.Extensions.Logging;

public class FileMetadataHub : ExxerHub<FileMetadata>
{
    public FileMetadataHub(ILogger<FileMetadataHub> logger)
        : base(logger)
    {
    }
}
```

### 3. Create a Dashboard Component

```csharp
using ExxerCube.Prisma.SignalR.Abstractions.Presentation.Blazor;
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Dashboards;
using Microsoft.AspNetCore.SignalR.Client;

public partial class FileMetadataDashboard : DashboardComponent<FileMetadata>
{
    protected override Dashboard<FileMetadata> CreateDashboard(
        HubConnection hubConnection,
        ReconnectionStrategy? reconnectionStrategy,
        ILogger<DashboardComponent<FileMetadata>> logger)
    {
        return new FileMetadataDashboardImpl(hubConnection, reconnectionStrategy, logger);
    }
}

public class FileMetadataDashboardImpl : Dashboard<FileMetadata>
{
    public FileMetadataDashboardImpl(
        HubConnection? hubConnection,
        ReconnectionStrategy? reconnectionStrategy,
        ILogger<Dashboard<FileMetadata>> logger)
        : base(hubConnection, reconnectionStrategy, logger)
    {
    }
}
```

### 4. Use in Razor Component

```razor
@page "/files/dashboard"
@using ExxerCube.Prisma.SignalR.Abstractions.Presentation.Blazor
@inject HubConnection HubConnection

<MudContainer>
    <ConnectionStateIndicator State="@ConnectionState" />
    
    <MudDataGrid Items="@Data" Hover="true">
        <!-- Your grid columns -->
    </MudDataGrid>
</MudContainer>

@code {
    protected override async Task OnInitializedAsync()
    {
        HubConnection = new HubConnectionBuilder()
            .WithUrl("/hubs/filemetadata")
            .Build();
        
        await base.OnInitializedAsync();
    }
}
```

## Architecture

### Hexagonal Architecture

The package follows Hexagonal Architecture principles:

- **Ports (Abstractions)**: `IExxerHub<T>`, `IServiceHealth<T>`, `IDashboard<T>`
- **Adapters (Infrastructure)**: Connection management, message batching/throttling
- **Application**: Dashboard orchestration logic

### Railway-Oriented Programming

All operations return `Result<T>` or `Result` instead of throwing exceptions:

```csharp
var result = await hub.SendToAllAsync(data, cancellationToken);
if (result.IsFailure)
{
    // Handle error
    logger.LogError("Failed to send: {Error}", result.Error);
}
```

## Configuration

### appsettings.json

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

## Examples

See the [Examples](./examples/) directory for complete usage examples.

## License

MIT License - see LICENSE file for details.

## Contributing

Contributions are welcome! Please read the contributing guidelines first.

