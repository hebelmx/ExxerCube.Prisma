# Testing Library Projects

This directory contains library projects that provide shared test infrastructure for all test projects.

## Projects

### 1. **ExxerCube.Prisma.Testing.Abstractions**
Base abstractions for test fixtures and test infrastructure.

**Dependencies**: None (pure abstractions)

**Contents**:
- `TestFixtureBase` - Base class for test fixtures

### 2. **ExxerCube.Prisma.Testing.Infrastructure**
Test infrastructure utilities and helpers.

**Dependencies**: 
- `Testing.Abstractions`
- `Domain`
- Shouldly, NSubstitute
- `xunit.v3.extensibility.core`

**Contents**:
- `Logging/ITestLogger` - Test logging abstraction
- `Logging/NoOpLogger` - No-op logger for libraries
- `Logging/XUnitLoggerAdapter` - Adapter for ITestOutputHelper (uses reflection)
- `Logging/TestLoggerFactory` - Factory for creating test loggers
- Test data generators (to be added)

### 3. **ExxerCube.Prisma.Testing.Contracts**
Interface contract test methods (IITDD pattern).

**Dependencies**:
- `Testing.Abstractions`
- `Domain.Interfaces`
- Shouldly
- `xunit.v3.extensibility.core`

**Contents** (ADR-005 abstract contract bases + their mock factories):
- `FileTypeIdentifierContract` / `FieldMergeStrategyContract` / Export.Adaptive + Adaptive-DOCX +
  Classification contracts / `RepositoryContract<T,TId>` / `PersonIdentityResolverContract` — each an
  abstract base inherited by a `Mock{Name}ContractTests` blueprint + one `{Impl}ContractTests` per
  implementation, with a `{Name}MockFactory` reference fake.
- See `docs/architecture/adr/ADR-005-itdd-contract-tests-injected-sut.md`.

### 4. **ExxerCube.Prisma.Testing.Python**
Python/CSnakes-specific test utilities.

**Dependencies**:
- `Testing.Abstractions`
- `Testing.Infrastructure`
- `Infrastructure` (for Python environment)
- CSnakes.Runtime
- `xunit.v3.extensibility.core`

**Contents**:
- `PythonEnvironmentTestFixture` - Base fixture for Python tests

## Usage

### In Test Projects

```csharp
// Use base fixture
public class DatabaseTestFixture : TestFixtureBase, IAsyncLifetime
{
    protected override async Task SetupAsync() { /* setup */ }
    protected override async Task TeardownAsync() { /* cleanup */ }
    
    public async Task InitializeAsync() => await InitializeAsync();
    public async Task DisposeAsync() => await DisposeAsync();
}

// Use logger factory
var logger = TestLoggerFactory.Create(_output); // _output is ITestOutputHelper
logger.Log("Test message");

// Use contract tests (ADR-005): inherit the abstract contract base — every [Fact] runs against your SUT
public sealed class MyServiceContractTests : PersonIdentityResolverContract
{
    public MyServiceContractTests(ITestOutputHelper output)
        : base(new MyService(XUnitLogger.CreateLogger<MyService>(output))) { }
}
```

## Key Principles

1. **No test project dependencies** - Test projects reference library projects, not each other
2. **Library projects use `xunit.v3.extensibility.core`** - Not `xunit.v3` (which is for test projects)
3. **Deferred execution** - Loggers use no-op pattern in libraries, test projects provide ITestOutputHelper
4. **Contract tests as abstract base classes** (ADR-005) - inherited per implementation (`Mock{Name}ContractTests` blueprint + one `{Impl}ContractTests` each); the older static-helper shape is superseded

