# xUnit v3 Best Practices Research

## Research Summary

Based on research and codebase analysis, here are the verified xUnit v3 best practices:

## 1. Test Project Independence

**Finding**: While not explicitly forbidden, best practices strongly recommend:
- ✅ **Test projects should NOT depend on each other**
- ✅ **Shared infrastructure should be in library projects** (not test projects)
- ✅ **Each test project should be independently executable**

**Rationale**: 
- Prevents test execution dependencies
- Allows parallel test execution
- Simplifies test discovery and execution
- Reduces coupling between test suites

## 2. xUnit v3 Package Usage

### xunit.v3.core
- **Purpose**: For library projects that need xUnit types but aren't test projects
- **Use Case**: Base fixture classes, test abstractions, utilities
- **Does NOT include**: Test execution framework, test discovery

### xunit.v3
- **Purpose**: Full xUnit v3 framework for test projects
- **Use Case**: Actual test projects that execute tests
- **Includes**: Test execution, discovery, runners

**Key Difference**: Libraries use `xunit.v3.core`, test projects use `xunit.v3`

## 3. TestContext API

**Available in xUnit v3**:
- `TestContext.Current` - Access current test context
- `TestContext.Current.CancellationToken` - Test cancellation token
- `TestContext.Current.SendMessage(string)` - Send messages to test output

**Usage Pattern**:
```csharp
// In library code (no ITestOutputHelper available)
TestContext.Current?.SendMessage("Log message");

// In test code (ITestOutputHelper available)
_output.WriteLine("Log message");
```

## 4. Fixtures and Shared Setup

**Constraints**:
- ❌ **Fixtures cannot live outside test classes** (xUnit limitation)
- ✅ **Base fixture classes can be in libraries** (abstract classes)
- ✅ **Concrete fixtures must be in test projects** (implement IAsyncLifetime)

**Pattern**:
```csharp
// Library: Testing.Abstractions
public abstract class TestFixtureBase : IAsyncLifetime
{
    protected abstract Task SetupAsync();
    protected abstract Task TeardownAsync();
}

// Test Project
public class DatabaseFixture : TestFixtureBase
{
    protected override async Task SetupAsync() { /* setup */ }
    protected override async Task TeardownAsync() { /* cleanup */ }
}
```

## 5. Logging in Libraries

**Problem**: 
- `ITestOutputHelper` only available during test execution
- Libraries cannot depend on test execution context
- Meziantou logger requires non-null output

**Solution**: Deferred execution pattern
```csharp
// Library provides interface
public interface ITestLogger
{
    void Log(string message);
}

// Library provides TestContext implementation
public class TestContextLogger : ITestLogger
{
    public void Log(string message) => TestContext.Current?.SendMessage(message);
}

// Test project provides ITestOutputHelper implementation
public class XUnitLoggerAdapter : ITestLogger
{
    private readonly ITestOutputHelper _output;
    public void Log(string message) => _output.WriteLine(message);
}

// Factory with deferred execution
public static ITestLogger Create(ITestOutputHelper? output = null)
    => output != null ? new XUnitLoggerAdapter(output) : new TestContextLogger();
```

## 6. Assembly Fixtures

**xUnit v3 Feature**: `[AssemblyFixture]` attribute
- **Purpose**: Share fixture instance across all test classes in assembly
- **Limitation**: Only works within same assembly
- **Cannot**: Share fixtures across test projects

**Conclusion**: For cross-project sharing, use library base classes

## 7. Test Project Structure

**Best Practices**:
- ✅ Separate test projects by type (Unit, Integration, E2E, UI)
- ✅ Mirror production code structure within test projects
- ✅ Use descriptive naming conventions
- ✅ Keep tests independent
- ✅ Use Arrange-Act-Assert pattern

**Avoid**:
- ❌ Test project dependencies on other test projects
- ❌ Shared test code in test projects
- ❌ Fixtures in separate test projects

## 8. Contract Testing Pattern

**Challenge**: How to share contract tests without test project dependencies?

**Solution**: Contract tests as library methods (not test classes)

```csharp
// Library: Testing.Contracts
public static class IPersonIdentityResolverContractTests
{
    public static async Task VerifyFindByRfcAsync_WithValidRfc_ReturnsSuccessWithNullValue(
        IPersonIdentityResolver resolver,
        CancellationToken cancellationToken = default)
    {
        var result = await resolver.FindByRfcAsync("PEGJ850101ABC", cancellationToken);
        result.IsSuccessMayBeNull.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }
}

// Test Project: Tests.Infrastructure.Classification
[Fact]
public async Task PersonIdentityResolverService_SatisfiesContract()
{
    var service = new PersonIdentityResolverService(_logger);
    await IPersonIdentityResolverContractTests
        .VerifyFindByRfcAsync_WithValidRfc_ReturnsSuccessWithNullValue(
            service, 
            TestContext.Current.CancellationToken);
}
```

## 9. Python/CSnakes Separation

**Best Practice**: 
- ✅ Separate Python/CSnakes concerns into independent projects
- ✅ Create `Testing.Python` library for Python-specific test utilities
- ✅ Isolate Python test infrastructure from other test infrastructure

**Rationale**:
- Python/CSnakes has distinct concerns and dependencies
- Allows independent evolution
- Reduces coupling

## 10. Verification from Codebase

**Evidence from ExxerCube.Prisma codebase**:
- ✅ Uses `TestContext.Current.CancellationToken` (xUnit v3 API)
- ✅ Uses `ITestOutputHelper` in test constructors
- ✅ Uses `XUnitLogger.CreateLogger<T>()` from Meziantou
- ✅ Test projects reference production projects, not each other
- ✅ GlobalUsings.cs shows proper xUnit v3 imports

## Conclusion

The proposed architecture aligns with xUnit v3 best practices:
1. ✅ Library projects for shared infrastructure
2. ✅ No test project dependencies
3. ✅ Proper use of `xunit.v3.core` for libraries
4. ✅ Deferred execution for logging
5. ✅ Base fixture classes in libraries
6. ✅ Contract tests as library methods
7. ✅ Separate Python/CSnakes concerns

