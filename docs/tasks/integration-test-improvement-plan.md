# Integration Test Improvement Plan

## Current Status

**Mutation Score:** 39.38% (86 killed, 59 survived, 3 timeout)  
**Total Mutants:** 322  
**Tested Mutants:** 148  
**Issue:** Low mutation score due to lack of integration tests

## Problem Analysis

The SignalR Abstractions package was designed to be abstracted out for better testability, but currently only has unit tests. Integration tests are needed to:

1. **Test real SignalR hub behavior** - Actual SignalR server/client interactions
2. **Test DI container integration** - Verify proper dependency injection setup
3. **Test end-to-end flows** - Complete message sending/receiving cycles
4. **Improve mutation coverage** - Integration tests exercise code paths that unit tests miss

## Existing Projects Found ✅

**Discovery:** Projects already exist and provide integration test infrastructure:
- ✅ `ExxerAI.RealTimeCommunication.Server` - Minimal ASP.NET Core Web app with SignalR hubs
- ✅ `ExxerAI.RealTimeCommunication.Server.Tests` - Integration tests (currently xUnit v3)

**Current State Analysis:**

### Server Project (`ExxerAI.RealTimeCommunication.Server`)
- **Framework:** .NET 10.0 Web app (`Microsoft.NET.Sdk.Web`)
- **Current Dependency:** References `ExxerAI.RealTimeCommunication` library
- **Hubs Configured:** SystemHub, AgentHub, TaskHub, DocumentHub, EconomicHub
- **Program.cs:** Simple SignalR setup with hub mappings
- **Needs:** Update to use `ExxerCube.Prisma.SignalR.Abstractions` instead

### Test Project (`ExxerAI.RealTimeCommunication.Server.Tests`)
- **Framework:** .NET 10.0 Web SDK (for `WebApplicationFactory`)
- **Test Framework:** xUnit v3 (needs conversion to v2 for Stryker)
- **Current Packages:**
  - `xunit.v3`
  - `Microsoft.Testing.Platform.MSBuild` (MTP - not compatible with Stryker)
  - `Microsoft.AspNetCore.Mvc.Testing` (for `WebApplicationFactory`)
  - `Microsoft.AspNetCore.SignalR.Client`
- **Test Infrastructure:**
  - `SignalRServerFactory` - `WebApplicationFactory<Program>` setup
  - `SystemHubIntegrationTests` - Real hub integration tests
  - Uses `TestContext.Current.CancellationToken` (xUnit v3 API)
- **Needs:** 
  - Convert from xUnit v3 → xUnit v2
  - Replace `TestContext.Current.CancellationToken` → `CancellationToken.None`
  - Remove Microsoft Testing Platform dependencies
  - Update project references to adapted server project

## Plan Overview

### Phase 1: Adapt Existing Server Project ⏳ **TODO**

**Tasks:**
- [ ] Update `ExxerAI.RealTimeCommunication.Server` to use `ExxerCube.Prisma.SignalR.Abstractions`
- [ ] Replace `ExxerAI.RealTimeCommunication` references with `ExxerCube.Prisma.SignalR.Abstractions`
- [ ] Update hub implementations to use `ExxerHub<T>` base class
- [ ] Configure DI container with SignalR abstractions from new package
- [ ] Update `Program.cs` to register services from `ExxerCube.Prisma.SignalR.Abstractions`

**Current Server Structure:**
```csharp
// Current: Uses ExxerAI.RealTimeCommunication
app.MapHub<SystemHub>("/hubs/system");
app.MapHub<AgentHub>("/hubs/agent");
// etc.

// Target: Use ExxerCube.Prisma.SignalR.Abstractions
// Hub implementations should inherit from ExxerHub<T>
```

### Phase 2: Convert Integration Test Project ⏳ **TODO**

**Tasks:**
- [ ] Convert `ExxerAI.RealTimeCommunication.Server.Tests` from xUnit v3 to xUnit v2
- [ ] Remove Microsoft Testing Platform dependencies:
  - Remove: `Microsoft.Testing.Platform.MSBuild`
  - Remove: `UseMicrosoftTestingPlatformRunner`, `TestingPlatformDotnetTestSupport`, `TestingPlatformServer` properties
- [ ] Update xUnit package references:
  - Remove: `xunit.v3`
  - Add: `xunit` v2.9.2
  - Update: `xunit.runner.visualstudio` v2.8.2
  - Add: `Microsoft.NET.Test.Sdk` v17.12.0
- [ ] Update logging package:
  - Remove: `Meziantou.Extensions.Logging.Xunit.v3` (if present)
  - Add: `Meziantou.Extensions.Logging.Xunit` v1.0.20
- [ ] Update test code (found in `SystemHubIntegrationTests.cs`):
  - Replace `TestContext.Current.CancellationToken` → `CancellationToken.None` (3 occurrences)
  - Update `GlobalUsings.cs` to remove xUnit v3 references
  - Verify all tests compile and run
- [ ] Update project references:
  - Point to adapted server project using `ExxerCube.Prisma.SignalR.Abstractions`

**Files to Update:**
- `ExxerAI.RealTimeCommunication.Server.Tests.csproj` - Package references
- `GlobalUsings.cs` - Remove xUnit v3 usings
- `SystemHubIntegrationTests.cs` - Replace `TestContext.Current.CancellationToken` (lines 31, 61, 64, 67, 98, 102, 105)
- `SignalRHubMethodTests.cs` - Check for xUnit v3 APIs
- `SignalRAdapterTests.cs` - Check for xUnit v3 APIs
- `SignalRIntegrationTests_Legacy.cs` - Check for xUnit v3 APIs

**Rationale:** Stryker.NET does not support xUnit v3 and Microsoft Testing Platform. Integration tests must use xUnit v2 to be compatible with mutation testing. See `docs/LessonsLearned-SignalR-Abstractions-Implementation.md` for details.

### Phase 3: Adapt Integration Tests ⏳ **TODO**

**Tasks:**
- [ ] Update existing integration tests to work with `ExxerCube.Prisma.SignalR.Abstractions`
- [ ] Update `SignalRServerFactory` to use new abstractions
- [ ] Adapt `SystemHubIntegrationTests` and other hub tests
- [ ] Ensure tests use `ExxerHub<T>` patterns
- [ ] Verify all tests pass with adapted server

**Existing Test Files:**
- `SignalRServerFactory.cs` - WebApplicationFactory setup
- `SystemHubIntegrationTests.cs` - Hub integration tests
- `SignalRHubMethodTests.cs` - Hub method tests
- `SignalRAdapterTests.cs` - Adapter tests
- `SignalRIntegrationTests_Legacy.cs` - Legacy tests

### Phase 4: Additional Integration Test Coverage ⏳ **TODO** (User will provide)

**Expected Additional Test Coverage:**
- [ ] Hub connection/disconnection flows with `ExxerHub<T>`
- [ ] Message sending to all clients using `SendToAllAsync`
- [ ] Message sending to specific client using `SendToClientAsync`
- [ ] Message sending to groups using `SendToGroupAsync`
- [ ] Connection count tracking with `GetConnectionCountAsync`
- [ ] Dashboard connection management with `Dashboard<T>`
- [ ] Service health monitoring integration with `ServiceHealth<T>`
- [ ] Error handling and cancellation flows with `ResultExtensions.Cancelled()`
- [ ] Real SignalR transport testing (WebSockets, Server-Sent Events, Long Polling)
- [ ] Integration with `MessageThrottler<T>` and `MessageBatcher<T>`

**Note:** User will provide additional integration test implementation. Existing tests will be adapted first.

### Phase 5: Mutation Score Improvement ⏳ **TODO**

**Goal:** Achieve mutation score > 80%

**Actions:**
- [ ] Run Stryker after integration tests are added
- [ ] Analyze surviving mutants
- [ ] Add targeted tests to kill remaining mutants
- [ ] Iterate until target score is reached

## Technical Constraints

### xUnit Version Requirement

**Current:** Test project uses xUnit v2 (compatible with Stryker)  
**Integration Test Project:** Must use xUnit v2 (not v3)

**Why:** Stryker.NET does not support xUnit v3 and Microsoft Testing Platform. See `docs/LessonsLearned-SignalR-Abstractions-Implementation.md` for details.

### Test Project Structure

```
ExxerCube.Prisma.SignalR.Abstractions.Tests/          (Unit tests - xUnit v2)
ExxerCube.Prisma.SignalR.Abstractions.Tests.Integration/  (Integration tests - xUnit v2)
```

**Note:** Integration test project will be separate to maintain clear separation of concerns.

## Dependencies Required

### Integration Test Project Packages (xUnit v2)

**Current (xUnit v3 - to be removed):**
```xml
<PackageReference Include="xunit.v3" />
<PackageReference Include="Microsoft.Testing.Platform.MSBuild" />
```

**Target (xUnit v2 - to be added):**
```xml
<!-- xUnit v2 -->
<PackageReference Include="xunit" Version="2.9.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />

<!-- Logging (v2, not v3) -->
<PackageReference Include="Meziantou.Extensions.Logging.Xunit" Version="1.0.20" />

<!-- Keep existing -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.0" />
<PackageReference Include="NSubstitute" Version="5.3.0" />
<PackageReference Include="Shouldly" Version="4.3.0" />
```

### Code Changes Required

**Replace xUnit v3 API calls:**
```csharp
// ❌ xUnit v3 (current)
await connection.StartAsync(TestContext.Current.CancellationToken);
await connection.InvokeAsync("BroadcastMessage", data, TestContext.Current.CancellationToken);

// ✅ xUnit v2 (target)
await connection.StartAsync(CancellationToken.None);
await connection.InvokeAsync("BroadcastMessage", data, CancellationToken.None);
```

### Minimal Web UI Program Packages

```xml
<PackageReference Include="Microsoft.AspNetCore.App" />
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="1.1.0" />
```

## Success Criteria

- [x] Server project adapted to use `ExxerCube.Prisma.SignalR.Abstractions` ✅
- [x] Integration test project converted from xUnit v3 to v2 ✅
- [x] Hub implementations created (`TestSystemHub`, `TestHealthHub`) ✅
- [x] Client application created with dashboard and health monitoring ✅
- [x] E2E tests created (5 scenarios) ✅
- [x] Integration tests created for dashboard and health ✅
- [x] System diagnostics health monitoring implemented ✅
- [x] Background services for testing created ✅
- [ ] Legacy test files updated/removed
- [ ] All integration tests pass
- [ ] Mutation score improves to > 80%
- [ ] Stryker runs successfully with integration tests
- [ ] Integration tests cover real SignalR hub behavior

## Relationship to ADR-001

**Note:** The existing `ExxerAI.RealTimeCommunication.Server` projects are not yet documented in ADR-001, but they provide the integration test infrastructure needed to achieve the mutation testing goals outlined in this plan.

**Future ADR Update:** ADR-001 should be updated to reference these integration test projects once they are adapted to use `ExxerCube.Prisma.SignalR.Abstractions`.

## Project Structure

**Location:** All projects are in the `UnifiedHub/` folder:

```
UnifiedHub/
├── ExxerCube.Prisma.SignalR.Abstractions/          (Main abstraction package)
├── ExxerCube.Prisma.SignalR.Abstractions.Tests/    (Unit tests - xUnit v2)
├── ExxerAI.RealTimeCommunication.Server/           (Integration test server - adapted)
├── ExxerAI.RealTimeCommunication.Server.Tests/     (Integration tests - converted to xUnit v2)
└── ExxerCube.Prisma.SignalR.Abstractions.sln       (Solution file with all projects)
```

## Conversion Progress

### ✅ Phase 1: Convert Integration Test Project (COMPLETED)

- [x] Converted from xUnit v3 to xUnit v2
- [x] Removed Microsoft Testing Platform dependencies
- [x] Updated package references (xunit v2.9.2, xunit.runner.visualstudio v2.8.2)
- [x] Added Meziantou.Extensions.Logging.Xunit v1.0.20
- [x] Replaced all `TestContext.Current.CancellationToken` → `CancellationToken.None` (63 occurrences)
- [x] Updated project references to point to UnifiedHub projects
- [x] Updated GlobalUsings.cs

**Files Updated:**
- `ExxerAI.RealTimeCommunication.Server.Tests.csproj` ✅
- `GlobalUsings.cs` ✅
- `SystemHubIntegrationTests.cs` ✅
- `SignalRHubMethodTests.cs` ✅
- `SignalRAdapterTests.cs` ✅
- `SignalRIntegrationTests_Legacy.cs` ✅

### ✅ Phase 2: Adapt Server Project (COMPLETED)

- [x] Updated project reference to use `ExxerCube.Prisma.SignalR.Abstractions`
- [x] Updated `Program.cs` to use `AddSignalRAbstractions()` extension
- [x] Commented out old hub mappings (to be implemented in Phase 3)
- [x] Added TODO comments for future hub implementations

**Files Updated:**
- `ExxerAI.RealTimeCommunication.Server.csproj` ✅
- `Program.cs` ✅

### ✅ Phase 3: Add Projects to Solution (COMPLETED)

- [x] Added `ExxerAI.RealTimeCommunication.Server` to solution
- [x] Added `ExxerAI.RealTimeCommunication.Server.Tests` to solution

**Solution File:** `UnifiedHub/ExxerCube.Prisma.SignalR.Abstractions.sln` ✅

### ✅ Phase 4: Create Hub Implementations (COMPLETED)

**Tasks:**
- [x] Create hub implementations inheriting from `ExxerHub<T>`
- [x] Implement `TestSystemHub` and `TestHealthHub` for testing/examples
- [x] Update `Program.cs` to map the new hubs
- [x] Add `SystemDiagnosticsCollector` for system metrics (CPU, Memory, Process/Thread counts)
- [x] Add `BackgroundMessageService` that sends periodic messages and health updates
- [x] Verify server builds and runs

**Created Files:**
- `Hubs/TestSystemHub.cs` - System message hub implementation
- `Hubs/TestHealthHub.cs` - Health monitoring hub implementation
- `Services/SystemDiagnosticsCollector.cs` - System.Diagnostics-based metrics collector
- `Services/BackgroundMessageService.cs` - Background service sending test messages
- `Models/SystemDiagnosticsData.cs` - System diagnostics data model

### ✅ Phase 5: Create Client Application and E2E Tests (COMPLETED)

**Tasks:**
- [x] Create `ExxerAI.RealTimeCommunication.Client` web app project
- [x] Implement `SystemDashboard` and `HealthDashboard` using `Dashboard<T>`
- [x] Implement `ClientServiceHealth` using `ServiceHealth<T>`
- [x] Add `DashboardBackgroundService` for client-side testing
- [x] Create E2E tests in `E2E/EndToEndTests.cs`
- [x] Create integration tests in `Integration/DashboardIntegrationTests.cs`
- [x] Add system diagnostics health monitoring (CPU, Memory, Process/Thread counts)
- [x] Add MudBlazor support for graphs (inherited from SignalR Abstractions)

**Created Files:**
- `ExxerAI.RealTimeCommunication.Client/Program.cs` - Client application entry point
- `Client/Dashboards/SystemDashboard.cs` - System message dashboard
- `Client/Dashboards/HealthDashboard.cs` - Health update dashboard
- `Client/Health/ClientServiceHealth.cs` - Client health monitoring
- `Client/Services/DashboardBackgroundService.cs` - Background service demonstrating dashboard usage
- `Server.Tests/E2E/EndToEndTests.cs` - Complete E2E tests (5 test scenarios)
- `Server.Tests/Integration/DashboardIntegrationTests.cs` - Dashboard integration tests

**E2E Test Scenarios:**
1. ✅ `E2E_ServerHubToClientDashboard_CompleteFlow` - Server → Hub → Client dashboard
2. ✅ `E2E_HealthMonitoring_ServerToClient_CompleteFlow` - Health monitoring flow
3. ✅ `E2E_MultipleClients_ReceiveBroadcastMessages` - Multiple clients receiving broadcasts
4. ✅ `E2E_ConnectionStateManagement_CompleteFlow` - Connection state management
5. ✅ `E2E_HealthStatusChanges_TriggerEvents` - Health status change events

### ⏳ Phase 6: Fix Legacy Test Files (TODO)

**Tasks:**
- [ ] Update or remove `SignalRAdapterTests.cs` (references old library)
- [ ] Update or remove `SignalRIntegrationTests_Legacy.cs` (references old library)
- [ ] Update `SignalRHubMethodTests.cs` if needed
- [ ] Verify all tests compile and run

**Note:** Legacy test files reference old `ExxerAI.RealTimeCommunication` library. These will be updated or removed in favor of new E2E and integration tests.

### ⏳ Phase 7: Additional Test Coverage (TODO - User will provide)

**Expected Additional Test Coverage:**
- [ ] Hub connection/disconnection flows with `ExxerHub<T>`
- [ ] Message sending to all clients using `SendToAllAsync`
- [ ] Message sending to specific client using `SendToClientAsync`
- [ ] Message sending to groups using `SendToGroupAsync`
- [ ] Connection count tracking with `GetConnectionCountAsync`
- [ ] Dashboard connection management with `Dashboard<T>`
- [ ] Service health monitoring integration with `ServiceHealth<T>`
- [ ] Error handling and cancellation flows with `ResultExtensions.Cancelled()`
- [ ] Real SignalR transport testing (WebSockets, Server-Sent Events, Long Polling)
- [ ] Integration with `MessageThrottler<T>` and `MessageBatcher<T>`

### ⏳ Phase 8: Mutation Score Improvement (TODO)

**Goal:** Achieve mutation score > 80%

**Actions:**
- [ ] Run Stryker after integration tests are added
- [ ] Analyze surviving mutants
- [ ] Add targeted tests to kill remaining mutants
- [ ] Iterate until target score is reached

## Current Mutation Test Results

**Date:** 2025-01-15  
**Stryker Version:** 4.8.1  
**Results:**
- **Total Mutants:** 322
- **Tested:** 148
- **Killed:** 86
- **Survived:** 59
- **Timeout:** 3
- **No Coverage:** 78
- **Compile Error:** 10
- **Ignored:** 86
- **Mutation Score:** 39.38%

**Key Insight:** The 78 "No Coverage" mutants and 59 "Survived" mutants indicate significant gaps in test coverage that integration tests should address.

---

## Features Implemented

### System Diagnostics Health Monitoring
- ✅ **CPU Usage**: Process-based CPU calculation (cross-platform compatible)
- ✅ **Memory Usage**: Working set and total memory tracking
- ✅ **Process/Thread Counts**: System-wide metrics
- ✅ **Health Status**: Automatic status determination based on thresholds:
  - Healthy: CPU < 90%, Memory < 90%
  - Degraded: CPU 90-95%, Memory 90-95%
  - Unhealthy: CPU > 95%, Memory > 95%

### Dashboard Examples
- ✅ **SystemDashboard**: Receives and displays system messages
- ✅ **HealthDashboard**: Receives and displays health updates
- ✅ **Connection State Management**: Automatic reconnection handling
- ✅ **Event Handling**: Data received and connection state change events

### Background Services
- ✅ **Server**: `BackgroundMessageService` - Sends periodic test messages and health updates
- ✅ **Client**: `DashboardBackgroundService` - Connects dashboards and monitors health

### E2E Test Coverage
- ✅ Complete server-to-client message flow
- ✅ Health monitoring end-to-end flow
- ✅ Multiple clients receiving broadcasts
- ✅ Connection state management
- ✅ Health status change events

---

**Created:** 2025-01-15  
**Last Updated:** 2025-01-15  
**Status:** ✅ Phase 1-5 Completed | ⏳ Phase 6-8 Pending  
**Next Steps:** Fix legacy test files (Phase 6), then run Stryker to improve mutation score (Phase 8)

