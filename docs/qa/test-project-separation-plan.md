# Test Project Separation Plan

## Overview

This document outlines the plan to reorganize the monolithic `ExxerCube.Prisma.Tests` project into properly separated test projects following IITDD (Interface-based Integration Test-Driven Development) and Clean Architecture principles.

## Current State Analysis

### Current Structure
- **Monolithic Test Project**: `ExxerCube.Prisma.Tests`
  - Contains all test types mixed together
  - Application tests (already partially separated)
  - Infrastructure tests (by concern, but mixed unit/integration)
  - Domain tests (minimal)
  - Interface contract tests (exists but minimal)
  - E2E tests (mixed with Application tests)
  - UI tests (Playwright mixed with other tests)

### Issues Identified
1. ❌ No clear separation between unit and integration tests
2. ❌ Infrastructure tests don't run interface contract tests
3. ❌ E2E tests mixed with Application tests
4. ❌ No dedicated architectural constraint tests
5. ❌ UI tests not separated
6. ❌ Test data management not centralized
7. ❌ No shared test infrastructure project

## Proposed Test Project Structure

### ⚠️ IMPORTANT: xUnit Best Practices

**Key Constraints:**
- ❌ **NO dependencies between test projects** (xUnit best practice)
- ✅ **Shared infrastructure must be library projects** (not test projects)
- ✅ **Fixtures cannot live outside test class** (use base fixture wrappers)
- ✅ **Use `xunit.v3.core`** for libraries (not `xunit.v3`)
- ✅ **Logging must use deferred execution** (TestContext.SendMessage() or abstracted logger)

---

## Library Projects (Not Test Projects)

### 1. **ExxerCube.Prisma.Testing.Abstractions**
**Purpose**: Test abstractions and base fixtures (library project)

**Contents**:
- Base fixture abstract classes
- Test interfaces and abstractions
- Test configuration interfaces

**Dependencies**: 
- `xunit.v3.core` only (not `xunit.v3`)
- No test framework dependencies

**Key Principle**: Pure abstractions, no test execution logic

---

### 2. **ExxerCube.Prisma.Testing.Infrastructure**
**Purpose**: Test infrastructure utilities (library project)

**Contents**:
- Test data generators
- Mock builders and factories
- Deferred logger factories (using TestContext.SendMessage() or abstracted logger)
- Test configuration utilities
- Common test data (PDFs, images, XMLs)

**Dependencies**: 
- `Testing.Abstractions`
- `ExxerCube.Prisma.Domain` (for test data models)
- Shouldly, NSubstitute (for utilities)

**Key Principle**: No xUnit test attributes, just utilities

---

### 3. **ExxerCube.Prisma.Testing.Contracts**
**Purpose**: Interface contract test methods (library project)

**Contents**:
- Contract test methods (not test classes)
- Reusable contract verification logic
- One contract test class per Domain interface

**Dependencies**:
- `Testing.Abstractions`
- `ExxerCube.Prisma.Domain.Interfaces`
- Shouldly (for assertions)

**Key Principle**: Methods that verify contracts, called by Infrastructure test projects

**Example Pattern**:
```csharp
// In Testing.Contracts (library)
public static class IPersonIdentityResolverContractTests
{
    public static void VerifyFindByRfcAsync_WithValidRfc_ReturnsSuccessWithNullValue(
        IPersonIdentityResolver resolver)
    {
        var result = resolver.FindByRfcAsync("PEGJ850101ABC", CancellationToken.None).Result;
        result.IsSuccessMayBeNull.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }
}
```

---

### 4. **ExxerCube.Prisma.Testing.Python**
**Purpose**: Python/CSnakes test utilities (library project, separate concern)

**Contents**:
- Python environment test fixtures
- CSnakes-specific test helpers
- Python test data generators
- Python environment setup utilities

**Dependencies**:
- `Testing.Abstractions`
- `CSnakes.Runtime`
- `ExxerCube.Prisma.Infrastructure.Python`

**Key Principle**: Isolated Python/CSnakes concerns, independent evolution

---

### 2. **ExxerCube.Prisma.Tests.Domain**
**Purpose**: Pure domain layer tests

**Contents**:
- Entity tests
- Value object tests
- Domain service tests
- Domain rule validation tests
- Business logic tests

**Dependencies**:
- `ExxerCube.Prisma.Domain` only
- `Tests.Shared` for utilities

**Key Principle**: No infrastructure, no application, pure domain logic

---

### 3. **ExxerCube.Prisma.Tests.Domain.Interfaces**
**Purpose**: Interface contract tests (IITDD core)

**Contents**:
- `IBrowserAutomationAgentContractTests.cs`
- `IDownloadTrackerContractTests.cs`
- `IFieldExtractorContractTests.cs`
- `IMetadataExtractorContractTests.cs`
- `IPersonIdentityResolverContractTests.cs`
- `IOcrExecutorContractTests.cs`
- ... (one contract test suite per Domain interface)

**Dependencies**:
- `ExxerCube.Prisma.Domain.Interfaces` only
- `Tests.Shared` for utilities

**Key Principle**: Tests define behavioral contracts, reusable across implementations

---

### 4. **ExxerCube.Prisma.Tests.Application**
**Purpose**: Application orchestration tests

**Contents**:
- Application service tests (already exists)
- Workflow orchestration tests
- Error handling tests
- Logging behavior tests

**Dependencies**:
- `ExxerCube.Prisma.Application`
- `ExxerCube.Prisma.Domain.Interfaces` (for mocks)
- `Testing.Abstractions`
- `Testing.Infrastructure` for utilities

**Key Principle**: Mock all Domain interfaces, never reference Infrastructure

---

### 5. **ExxerCube.Prisma.Tests.Infrastructure.Database**
**Purpose**: Database adapter tests

**Structure**:
```
Tests.Infrastructure.Database/
├── Unit/
│   ├── SLAEnforcerServiceTests.cs
│   ├── AuditLoggerServiceTests.cs
│   └── EfCoreRepositoryTests.cs
├── Integration/
│   ├── SLAEnforcerServiceIntegrationTests.cs
│   └── DatabaseIntegrationTests.cs
└── Contracts/
    └── IRepositoryContractTests.cs (runs contract tests from Tests.Domain.Interfaces)
```

**Dependencies**:
- `ExxerCube.Prisma.Infrastructure.Database`
- `ExxerCube.Prisma.Domain.Interfaces`
- `Testing.Abstractions`
- `Testing.Infrastructure` for utilities
- `Testing.Contracts` (to call contract test methods)

**Key Principle**: No dependency on other test projects

---

### 6. **ExxerCube.Prisma.Tests.Infrastructure.Classification**
**Purpose**: Classification adapter tests

**Structure**:
```
Tests.Infrastructure.Classification/
├── Unit/
│   ├── LegalDirectiveClassifierServiceTests.cs
│   ├── PersonIdentityResolverServiceTests.cs
│   └── MatchingPolicyServiceTests.cs
├── Integration/
│   └── ClassificationIntegrationTests.cs
└── Contracts/
    └── IPersonIdentityResolverContractTests.cs
```

**Dependencies**:
- `ExxerCube.Prisma.Infrastructure.Classification`
- `ExxerCube.Prisma.Domain.Interfaces`
- `Testing.Abstractions`
- `Testing.Infrastructure`
- `Testing.Contracts`

---

### 7. **ExxerCube.Prisma.Tests.Infrastructure.Export**
**Purpose**: Export adapter tests

**Structure**:
```
Tests.Infrastructure.Export/
├── Unit/
│   ├── PdfRequirementSummarizerServiceTests.cs
│   └── DigitalPdfSignerTests.cs
├── Integration/
│   └── ExportIntegrationTests.cs
└── Contracts/
    └── IResponseExporterContractTests.cs
```

**Dependencies**:
- `ExxerCube.Prisma.Infrastructure.Export`
- `ExxerCube.Prisma.Domain.Interfaces`
- `Testing.Abstractions`
- `Testing.Infrastructure`
- `Testing.Contracts`

---

### 8. **ExxerCube.Prisma.Tests.Infrastructure.Extraction**
**Purpose**: Extraction adapter tests

**Structure**:
```
Tests.Infrastructure.Extraction/
├── Unit/
│   ├── PdfMetadataExtractorTests.cs
│   ├── DocxMetadataExtractorTests.cs
│   └── XmlMetadataExtractorTests.cs
├── Integration/
│   └── ExtractionIntegrationTests.cs
└── Contracts/
    └── IMetadataExtractorContractTests.cs
```

**Dependencies**:
- `Testing.Abstractions`
- `Testing.Infrastructure`
- `Testing.Contracts`
- `ExxerCube.Prisma.Infrastructure.Extraction`
- `ExxerCube.Prisma.Domain.Interfaces`

---

### 9. **ExxerCube.Prisma.Tests.Infrastructure.FileSystem**
**Purpose**: File system adapter tests

**Structure**:
```
Tests.Infrastructure.FileSystem/
├── Unit/
│   ├── FileSystemLoaderTests.cs
│   └── FileSystemOutputWriterTests.cs
├── Integration/
│   └── FileSystemIntegrationTests.cs
└── Contracts/
    └── IFileLoaderContractTests.cs
```

**Dependencies**:
- `ExxerCube.Prisma.Infrastructure.FileSystem`
- `ExxerCube.Prisma.Domain.Interfaces`
- `Testing.Abstractions`
- `Testing.Infrastructure`
- `Testing.Contracts`

---

### 10. **ExxerCube.Prisma.Tests.Infrastructure.Python**
**Purpose**: Python interop adapter tests

**Structure**:
```
Tests.Infrastructure.Python/
├── Unit/
│   └── CSnakesOcrProcessingAdapterTests.cs
├── Integration/
│   └── PythonIntegrationTests.cs
└── Contracts/
    └── IOcrExecutorContractTests.cs
```

**Dependencies**:
- `ExxerCube.Prisma.Infrastructure.Python`
- `ExxerCube.Prisma.Domain.Interfaces`
- `Testing.Abstractions`
- `Testing.Infrastructure`
- `Testing.Contracts`
- `Testing.Python` (for Python-specific test utilities)

---

### 11. **ExxerCube.Prisma.Tests.System**
**Purpose**: System integration tests

**Contents**:
- Full system tests with real adapters
- Cross-cutting concern tests (audit, SLA, logging)
- Pipeline integration tests
- Real database, real file system
- Mocked external services (APIs, etc.)

**Dependencies**:
- All Infrastructure projects
- `ExxerCube.Prisma.Application`
- `Testing.Abstractions`
- `Testing.Infrastructure`

**Key Principle**: Real adapters, mocked external services

---

### 12. **ExxerCube.Prisma.Tests.EndToEnd**
**Purpose**: End-to-end workflow tests

**Contents**:
- Complete business workflow tests
- Full user journeys
- Real external services (or test doubles)
- End-to-end data flows

**Dependencies**:
- All projects
- `Testing.Abstractions`
- `Testing.Infrastructure`

**Key Principle**: Most realistic tests, slowest execution

---

### 13. **ExxerCube.Prisma.Tests.Architecture**
**Purpose**: Architectural constraint tests

**Contents**:
- Dependency rule tests (NetArchTest or custom)
- Layer violation tests
- Hexagonal architecture constraint tests
- Naming convention tests
- Interface location tests

**Dependencies**:
- All projects (for analysis)
- `Testing.Abstractions`
- NetArchTest (optional)

**Key Principle**: Prevent architectural violations

---

### 14. **ExxerCube.Prisma.Tests.UI**
**Purpose**: UI and browser tests

**Structure**:
```
Tests.UI/
├── Components/
│   └── BlazorComponentTests.cs (using BUnit)
├── Browser/
│   └── PlaywrightTests.cs
└── Accessibility/
    └── AccessibilityTests.cs
```

**Dependencies**:
- `ExxerCube.Prisma.Web.UI`
- All projects
- `Testing.Abstractions`
- `Testing.Infrastructure`
- Playwright, BUnit

---

## Dependency Graph

```
Library Projects (not test projects):
Testing.Abstractions (xunit.v3.core only)
    ↑
    ├── Testing.Infrastructure (Domain)
    ├── Testing.Contracts (Domain.Interfaces)
    └── Testing.Python (CSnakes.Runtime)

Test Projects (NO dependencies on each other):
- Tests.Domain → Domain + Testing.Abstractions + Testing.Infrastructure
- Tests.Domain.Interfaces → Domain.Interfaces + Testing.Abstractions + Testing.Infrastructure + Testing.Contracts
- Tests.Application → Application + Testing.Abstractions + Testing.Infrastructure
- Tests.Infrastructure.* → Infrastructure.* + Testing.Abstractions + Testing.Infrastructure + Testing.Contracts
- Tests.Infrastructure.Python → Infrastructure.Python + Testing.Abstractions + Testing.Infrastructure + Testing.Contracts + Testing.Python
- Tests.System → All projects + Testing.Abstractions + Testing.Infrastructure
- Tests.EndToEnd → All projects + Testing.Abstractions + Testing.Infrastructure
- Tests.Architecture → All projects + Testing.Abstractions
- Tests.UI → UI + All projects + Testing.Abstractions + Testing.Infrastructure

Key Principle: NO test project depends on another test project
```

## Key Implementation Details

### Logging Pattern (Deferred Execution)

Since loggers require `ITestOutputHelper` which is only available during test execution, use deferred execution:

```csharp
// In Testing.Infrastructure (library)
public interface ITestLogger
{
    void Log(string message);
    void LogDebug(string message);
    void LogError(string message, Exception? exception = null);
}

public class TestContextLogger : ITestLogger
{
    public void Log(string message) => TestContext.Current?.SendMessage(message);
    public void LogDebug(string message) => TestContext.Current?.SendMessage($"[DEBUG] {message}");
    public void LogError(string message, Exception? exception = null) 
        => TestContext.Current?.SendMessage($"[ERROR] {message} {exception}");
}

public class XUnitLoggerAdapter : ITestLogger
{
    private readonly ITestOutputHelper _output;
    public XUnitLoggerAdapter(ITestOutputHelper output) => _output = output;
    public void Log(string message) => _output.WriteLine(message);
    public void LogDebug(string message) => _output.WriteLine($"[DEBUG] {message}");
    public void LogError(string message, Exception? exception = null) 
        => _output.WriteLine($"[ERROR] {message} {exception}");
}

// Factory with deferred execution
public static class TestLoggerFactory
{
    public static ITestLogger Create(ITestOutputHelper? output = null)
    {
        return output != null 
            ? new XUnitLoggerAdapter(output)
            : new TestContextLogger();
    }
    
    // For use in libraries where output is not available
    public static ITestLogger CreateDeferred() => new TestContextLogger();
}
```

### Fixture Pattern (Base Classes in Library)

Since fixtures cannot live outside test classes, create base fixture classes:

```csharp
// In Testing.Abstractions (library, depends on xunit.v3.core)
public abstract class TestFixtureBase : IAsyncLifetime
{
    protected abstract Task SetupAsync();
    protected abstract Task TeardownAsync();
    
    public async Task InitializeAsync() => await SetupAsync();
    public async Task DisposeAsync() => await TeardownAsync();
}

// In test project
public class DatabaseTestFixture : TestFixtureBase
{
    private PrismaDbContext _dbContext;
    
    protected override async Task SetupAsync() 
    { 
        // Setup database
    }
    
    protected override async Task TeardownAsync() 
    { 
        // Cleanup database
    }
}
```

### Contract Test Pattern

Contract tests as reusable methods (not test classes):

```csharp
// In Testing.Contracts (library)
public static class IPersonIdentityResolverContractTests
{
    public static async Task VerifyFindByRfcAsync_WithValidRfc_ReturnsSuccessWithNullValue(
        IPersonIdentityResolver resolver,
        CancellationToken cancellationToken = default)
    {
        var result = await resolver.FindByRfcAsync("PEGJ850101ABC", cancellationToken);
        result.IsSuccessMayBeNull.ShouldBeTrue();
        result.IsSuccessValueNull.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }
}

// In Tests.Infrastructure.Classification
[Fact]
public async Task PersonIdentityResolverService_SatisfiesContract_FindByRfcAsync()
{
    var service = new PersonIdentityResolverService(_logger);
    await IPersonIdentityResolverContractTests
        .VerifyFindByRfcAsync_WithValidRfc_ReturnsSuccessWithNullValue(
            service, 
            TestContext.Current.CancellationToken);
}
```

## Migration Strategy

### Phase 1: Foundation (Week 1)
1. ✅ Create `Testing.Abstractions` library project (depends on `xunit.v3.core` only)
2. ✅ Create `Testing.Infrastructure` library project
3. ✅ Create `Testing.Contracts` library project
4. ✅ Create `Testing.Python` library project (separate concern)
5. ✅ Move common test utilities to library projects
6. ✅ Move test data to `Testing.Infrastructure/TestData`
7. ✅ Create test data generators
8. ✅ Implement deferred logger factory pattern

### Phase 2: Domain Tests (Week 1)
1. ✅ Create `Tests.Domain` project
2. ✅ Move domain entity/value object tests
3. ✅ Create `Tests.Domain.Interfaces` project
4. ✅ Create interface contract test templates

### Phase 3: Application Tests (Week 1)
1. ✅ Verify `Tests.Application` isolation
2. ✅ Ensure no Infrastructure references
3. ✅ Add missing Application tests

### Phase 4: Infrastructure Tests (Week 2)
1. ✅ Create separate Infrastructure test projects
2. ✅ Split unit vs integration tests
3. ✅ Add contract test fixtures
4. ✅ Move tests to appropriate projects

### Phase 5: System/E2E Tests (Week 2)
1. ✅ Create `Tests.System` project
2. ✅ Extract system tests from Application folder
3. ✅ Create `Tests.EndToEnd` project
4. ✅ Extract E2E workflow tests

### Phase 6: Architecture/UI Tests (Week 3)
1. ✅ Create `Tests.Architecture` project
2. ✅ Add architectural constraint tests
3. ✅ Create `Tests.UI` project
4. ✅ Move Playwright tests

### Phase 7: Cleanup (Week 3)
1. ✅ Remove old monolithic test project
2. ✅ Update CI/CD pipelines
3. ✅ Update documentation
4. ✅ Verify all tests pass

## Library Project Template (Testing.Abstractions)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <!-- NOT a test project -->
  </PropertyGroup>

  <ItemGroup>
    <!-- Only xunit.v3.core for library projects -->
    <PackageReference Include="xunit.v3.core" />
  </ItemGroup>
</Project>
```

## Library Project Template (Testing.Infrastructure)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Testing.Abstractions\ExxerCube.Prisma.Testing.Abstractions.csproj" />
    <ProjectReference Include="..\Domain\ExxerCube.Prisma.Domain.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Shouldly" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>
</Project>
```

## Test Project Template

Each test project should follow this structure:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <!-- Inherit from Directory.Build.props -->
  </PropertyGroup>

  <ItemGroup>
    <!-- Standard test packages -->
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="Meziantou.Extensions.Logging.Xunit.v3" />
  </ItemGroup>

  <ItemGroup>
    <!-- Project references based on test type -->
    <ProjectReference Include="..\..\Domain\ExxerCube.Prisma.Domain.csproj" />
    <!-- Add other references as needed -->
    
    <!-- Library projects (NOT test projects) -->
    <ProjectReference Include="..\Testing.Abstractions\ExxerCube.Prisma.Testing.Abstractions.csproj" />
    <ProjectReference Include="..\Testing.Infrastructure\ExxerCube.Prisma.Testing.Infrastructure.csproj" />
    <ProjectReference Include="..\Testing.Contracts\ExxerCube.Prisma.Testing.Contracts.csproj" />
    
    <!-- NO references to other test projects -->
  </ItemGroup>
</Project>
```

## CI/CD Test Execution Strategy

### Fast Tests (Run on Every Commit)
- `Tests.Domain`
- `Tests.Domain.Interfaces`
- `Tests.Application`
- `Tests.Infrastructure.*/Unit`
- `Tests.Architecture`

### Medium Tests (Run on PR)
- `Tests.Infrastructure.*/Integration`
- `Tests.System`

### Slow Tests (Run on Merge to Main)
- `Tests.EndToEnd`
- `Tests.UI`

## Benefits

1. ✅ **Clear Separation of Concerns**: Each test project has a single responsibility
2. ✅ **IITDD Compliance**: Interface contract tests properly separated
3. ✅ **xUnit Best Practices**: No test project dependencies, proper library separation
4. ✅ **Faster Feedback**: Run fast tests on every commit
5. ✅ **Better Organization**: Easy to find and maintain tests
6. ✅ **Architectural Enforcement**: Dedicated architecture tests prevent violations
7. ✅ **Scalability**: Easy to add new test projects as system grows
8. ✅ **Test Data Management**: Centralized test data in library projects
9. ✅ **Contract Testing**: Infrastructure adapters verified against interface contracts
10. ✅ **Proper Logging**: Deferred execution pattern works in libraries
11. ✅ **Fixture Reusability**: Base fixture classes in libraries, concrete in tests
12. ✅ **Python Isolation**: Python/CSnakes concerns in separate projects

## Next Steps

1. Review and approve this plan
2. Create `Tests.Shared` project first
3. Begin Phase 1 migration
4. Iterate through phases systematically
5. Update CI/CD pipelines as projects are created

