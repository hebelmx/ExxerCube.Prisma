# Task: Improve SignalR Abstractions Test Coverage and Kill Surviving Mutants

## Context

This task is part of the SignalR Unified Hub Abstraction implementation as an independent NuGet package. The core implementation is complete, and we have a green test bed (50/50 tests passing), but the mutation testing score is low (31.30%), indicating significant gaps in test coverage.

## Current Status

### ✅ Completed
- Independent SignalR Abstractions NuGet package solution created
- Core abstractions implemented:
  - `ExxerHub<T>` - Generic SignalR hub base class
  - `ServiceHealth<T>` - Service health monitoring
  - `Dashboard<T>` - Blazor dashboard base class
- Infrastructure components implemented:
  - `MessageBatcher<T>` - Message batching
  - `MessageThrottler<T>` - Message throttling
  - `ReconnectionStrategy` - Connection retry logic
- Comprehensive test suite: 50 tests, all passing
- xUnit v2 configured (for Stryker compatibility)
- Stryker mutation testing configured and working

### 📊 Current Mutation Testing Results

**Last Run:** 2025-11-19 08:46:37

**Results:**
- **Total mutants created:** 326
- **Mutants tested:** 121
- **Mutants killed:** 67 ✅
- **Mutants survived:** 53 ❌ (NEEDS ATTENTION)
- **NoCoverage:** 108 (not covered by any test)
- **Ignored:** 86 (removed by block already covered filter)
- **CompileError:** 10
- **Already failing:** 1

**Current Mutation Score:** 29.57%

**Note:** Numbers may vary slightly between runs. Focus on the pattern: ~50 surviving mutants and ~108 uncovered code paths.

**Target Mutation Score:** 
- Minimum: 60%
- Target: 80%+
- High: 90%+

## Project Structure

```
ExxerCube.Prisma/
├── ExxerCube.Prisma.SignalR.Abstractions/          # Main package project
│   ├── Abstractions/
│   │   ├── Hubs/                                    # ExxerHub<T> implementation
│   │   ├── Health/                                  # ServiceHealth<T> implementation
│   │   └── Dashboards/                              # Dashboard<T> implementation
│   ├── Infrastructure/
│   │   ├── Connection/                             # ConnectionState, ReconnectionStrategy
│   │   └── Messaging/                               # MessageBatcher, MessageThrottler
│   ├── Presentation/
│   │   └── Blazor/                                  # Blazor components
│   ├── Common/                                      # ResultExtensions
│   └── Extensions/                                  # DI extensions
│
├── ExxerCube.Prisma.SignalR.Abstractions.Tests/    # Test project
│   ├── Abstractions/                                # Unit tests for abstractions
│   ├── Infrastructure/                              # Unit tests for infrastructure
│   ├── Integration/                                 # Integration tests
│   └── Common/                                      # Common test utilities
│
└── docs/
    ├── adr/
    │   └── ADR-001-SignalR-Unified-Hub-Abstraction.md
    └── tasks/
        └── improve-signalr-abstractions-test-coverage.md (this file)
```

## Stryker Report Location

**HTML Report:** `ExxerCube.Prisma.SignalR.Abstractions/StrykerOutput/[LATEST]/reports/mutation-report.html`

**Note:** Check the StrykerOutput folder for the most recent report. The folder name includes timestamp.

Open this file in a browser to see:
- Which mutants survived (49)
- Which code paths have no coverage (109)
- Specific line numbers and mutation types
- File-by-file breakdown

## Task Objectives

### Primary Goal
Improve mutation testing score from 29.57% to at least 60% (target: 80%+) by:
1. Adding tests for all surviving mutants (~53)
2. Adding tests for uncovered code paths (~108 NoCoverage mutants)
3. Investigating the "already failing" mutant (1)

### Secondary Goals
- Maintain 100% test pass rate
- Ensure all tests follow project standards (xUnit v2, Shouldly, NSubstitute)
- Document test coverage improvements

## Approach

### Step 1: Analyze Stryker Report
1. Find the latest HTML report:
   ```powershell
   Get-ChildItem -Path "ExxerCube.Prisma.SignalR.Abstractions/StrykerOutput" -Recurse -Filter "mutation-report.html" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
   ```
2. Open the report in a browser
3. Identify surviving mutants by file and line number
4. Identify NoCoverage mutants by file and line number
5. Prioritize by:
   - Critical paths (error handling, edge cases)
   - Public API methods
   - Infrastructure components

### Step 2: Add Tests for Surviving Mutants
For each surviving mutant:
1. Understand what mutation survived (e.g., changed `>` to `>=`, `==` to `!=`, etc.)
2. Write a test that would fail if that mutation occurred
3. Ensure the test passes with the original code
4. Verify the test fails with the mutation (if possible)

**Example:**
If a mutant changed `if (count > 0)` to `if (count >= 0)` and survived:
- Add a test that verifies behavior when `count == 0`
- The test should fail if the mutation is present

### Step 3: Add Tests for Uncovered Code
For each NoCoverage mutant:
1. Identify the code path that's not covered
2. Write a test that exercises that path
3. Verify the test passes

**Common uncovered paths:**
- Error handling branches
- Edge cases (null, empty, boundary values)
- Alternative code paths (else clauses, switch cases)
- Exception handling

### Step 4: Investigate "Already Failing" Mutant
1. Identify which mutant is marked as "already failing"
2. Determine if it's a false positive or indicates a real test issue
3. Either:
   - Fix the test if it's incorrectly written
   - Mark as acceptable if it's an expected edge case
   - Add proper test coverage if needed

### Step 5: Re-run Stryker and Iterate
1. Run Stryker: `dotnet stryker --project ExxerCube.Prisma.SignalR.Abstractions/ExxerCube.Prisma.SignalR.Abstractions.csproj --test-project ExxerCube.Prisma.SignalR.Abstractions.Tests/ExxerCube.Prisma.SignalR.Abstractions.Tests.csproj`
2. Review new mutation score
3. Identify remaining survivors
4. Repeat steps 2-5 until target score is reached

## Commands Reference

### Run Tests
```powershell
cd ExxerCube.Prisma
dotnet test ExxerCube.Prisma.SignalR.Abstractions.Tests/ExxerCube.Prisma.SignalR.Abstractions.Tests.csproj --verbosity normal
```

### Run Stryker Mutation Testing
```powershell
cd ExxerCube.Prisma
cd ExxerCube.Prisma.SignalR.Abstractions
dotnet stryker --project ExxerCube.Prisma.SignalR.Abstractions.csproj --test-project ../ExxerCube.Prisma.SignalR.Abstractions.Tests/ExxerCube.Prisma.SignalR.Abstractions.Tests.csproj --verbosity info
```

### Build Project
```powershell
cd ExxerCube.Prisma
dotnet build ExxerCube.Prisma.SignalR.Abstractions.Tests/ExxerCube.Prisma.SignalR.Abstractions.Tests.csproj
```

## Testing Standards

### Framework
- **xUnit v2** (NOT v3 - Stryker compatibility)
- **Shouldly** for assertions
- **NSubstitute** for mocking

### Patterns
- Use `CancellationToken.None` (NOT `TestContext.Current.CancellationToken` - that's xUnit v3)
- All test data classes must be `public` (NSubstitute requirement)
- Use `using var` for disposable resources
- Follow AAA pattern (Arrange, Act, Assert)

### Example Test Structure
```csharp
public class SomeClassTests
{
    private readonly ILogger<SomeClass> _logger;

    public SomeClassTests()
    {
        _logger = Substitute.For<ILogger<SomeClass>>();
    }

    [Fact]
    public async Task MethodName_Scenario_ExpectedBehavior()
    {
        // Arrange
        var sut = new SomeClass(_logger);
        var input = new TestData();

        // Act
        var result = await sut.MethodAsync(input, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }
}
```

## Key Files to Focus On

Based on typical mutation testing patterns, prioritize:

1. **Error Handling**
   - `ExxerHub.cs` - Exception handling in SendTo* methods
   - `Dashboard.cs` - Connection error handling
   - `ServiceHealth.cs` - Health update error paths

2. **Edge Cases**
   - `MessageBatcher.cs` - Boundary conditions (batch size, timing)
   - `MessageThrottler.cs` - Throttle interval edge cases
   - `ReconnectionStrategy.cs` - Retry logic edge cases

3. **Validation**
   - All null/empty string checks
   - Boundary value validations
   - State transition validations

4. **Alternative Paths**
   - Else clauses
   - Switch default cases
   - Conditional branches

## Success Criteria

### Must Have
- ✅ Mutation score ≥ 60%
- ✅ All existing tests still pass (50/50)
- ✅ No new compilation errors
- ✅ All tests follow project standards

### Should Have
- ✅ Mutation score ≥ 80%
- ✅ All public APIs have test coverage
- ✅ All error paths have test coverage

### Nice to Have
- ✅ Mutation score ≥ 90%
- ✅ 100% code coverage (if feasible)
- ✅ Performance benchmarks

## Notes and Considerations

### Architecture
- The project follows **Hexagonal Architecture** (Ports and Adapters)
- Uses **Railway-Oriented Programming** (Result<T> pattern)
- Follows **Clean Architecture** principles

### Result<T> Pattern
- Methods return `Result<T>` or `Result` (not exceptions)
- Use `result.IsSuccess`, `result.IsFailure`, `result.IsCancelled()`
- Check `result.Error` for failure messages

### SignalR Mocking
- Use `TestHubHelper.SetupHub()` for hub testing
- Mock `IHubCallerClients`, `ISingleClientProxy`, `IClientProxy`
- For integration tests, use `TestServer` with real SignalR

### Common Pitfalls
- Don't use xUnit v3 features (`TestContext.Current`)
- Don't make test data classes `private` (NSubstitute needs `public`)
- Don't forget to dispose resources (`using var`)
- Don't mix xUnit v2 and v3 packages

## Resources

- **ADR:** `docs/adr/ADR-001-SignalR-Unified-Hub-Abstraction.md`
- **Stryker Report:** `ExxerCube.Prisma.SignalR.Abstractions/StrykerOutput/2025-11-19.08-17-31/reports/mutation-report.html`
- **Project Rules:** `.cursor/rules/` (especially testing standards)

## Progress Tracking

Update this section as you progress:

### Completed
- [ ] Analyzed Stryker report
- [ ] Identified priority mutants
- [ ] Added tests for surviving mutants
- [ ] Added tests for uncovered code
- [ ] Reached 60% mutation score
- [ ] Reached 80% mutation score (target)

### Current Status
- **Last Mutation Score:** 29.57%
- **Target Score:** 80%+
- **Tests Added:** 0 (update as you add)
- **Mutants Killed:** 0 (update as you progress)
- **Surviving Mutants:** ~53
- **Uncovered Code Paths:** ~108

## Questions or Issues?

If you encounter issues or need clarification:
1. Review the ADR document
2. Check existing tests for patterns
3. Review project rules in `.cursor/rules/`
4. Check git history for implementation context

---

**Created:** 2025-11-19  
**Last Updated:** 2025-11-19  
**Status:** Ready for Assignment

