# Task: Kill Remaining Mutants - SignalR Abstractions

## Current Status

**Mutation Score:** 37.83% (improved from 29.57% baseline)

**Mutant Statistics:**
- ✅ **Killed:** 80 mutants
- ❌ **Survived:** 47 mutants (need to kill these)
- ⚠️ **NoCoverage:** 96 mutants (need tests to cover these)
- ⏱️ **Timeout:** 7 mutants (may need investigation)
- 🔧 **CompileError:** 10 mutants (ignored)
- 🚫 **Ignored:** 86 mutants (filtered out)

**Total Mutants Created:** 326
**Mutants Tested:** 134

## Target Goal

**95%+ mutation score** - Kill all surviving mutants and cover all NoCoverage mutants.

## What Has Been Done

### Tests Added (50 → 100 tests)
- ✅ ExxerHub<T> - Exception handling, cancellation, whitespace validation
- ✅ MessageBatcher<T> - Boundary tests, timer behavior, constructor validation
- ✅ MessageThrottler<T> - Timing boundaries, message replacement, cancellation
- ✅ ServiceHealth<T> - Data updates, multiple subscribers, event verification
- ✅ Dashboard<T> - State transitions, null handling, message accumulation
- ✅ ReconnectionStrategy - Exponential backoff, max delay capping, edge cases

### Test Quality Standards
- ✅ xUnit v2 (not v3 - Stryker compatibility)
- ✅ Shouldly assertions
- ✅ NSubstitute mocking
- ✅ CancellationToken.None
- ✅ AAA pattern
- ✅ Behavioral testing (no getters/setters)
- ✅ XML documentation

## What Needs to Be Done

### Step 1: Analyze Surviving Mutants

**Location:** `ExxerCube.Prisma.SignalR.Abstractions/StrykerOutput/2025-11-19.12-42-55/reports/mutation-report.html`

**Action:**
1. Open the HTML report in a browser
2. Filter by "Survived" status
3. Identify which files have the most survivors
4. Document specific mutants by file/line/mutation type

**Expected Files with Survivors:**
- `ExxerHub.cs` - Likely exception handling paths, null checks
- `MessageBatcher.cs` - Timer callback paths, edge cases
- `MessageThrottler.cs` - Timing logic, message equality
- `Dashboard.cs` - State transitions, connection handling
- `ServiceHealth.cs` - Status comparison logic
- `ReconnectionStrategy.cs` - Delay calculation boundaries

### Step 2: Analyze NoCoverage Mutants

**96 mutants with NoCoverage** - These are code paths not executed by any test.

**Action:**
1. Filter report by "NoCoverage" status
2. Identify uncovered branches/conditions
3. Add tests to execute these paths

**Common NoCoverage Patterns:**
- `else` branches that are never taken
- Exception catch blocks
- Null check branches
- Boundary conditions (==, !=, <, >)
- Switch case statements
- Ternary operator branches

### Step 3: Add Tests for Surviving Mutants

**Strategy:**
1. **For each surviving mutant:**
   - Understand what mutation was made (e.g., `==` → `!=`, `true` → `false`)
   - Write a test that would FAIL if the mutation existed
   - Ensure test passes with original code

2. **Common mutation patterns to target:**
   - **Conditional mutations:** `if (condition)` → `if (!condition)` or `if (true)` or `if (false)`
   - **Comparison mutations:** `==` → `!=`, `>` → `<=`, `>=` → `<`
   - **Arithmetic mutations:** `+` → `-`, `*` → `/`
   - **Logical mutations:** `&&` → `||`, `!condition` → `condition`
   - **Return mutations:** `return value` → `return null` or `return default`

3. **Example approach:**
   ```csharp
   // If mutant survives: if (count > 0) → if (count >= 0)
   // Add test that verifies behavior when count == 0
   [Fact]
   public void Method_WhenCountIsZero_HandlesCorrectly()
   {
       // This test will fail if mutation changes > to >=
       // because it tests the exact boundary
   }
   ```

### Step 4: Add Tests for NoCoverage Mutants

**Strategy:**
1. **For each NoCoverage mutant:**
   - Identify the uncovered code path
   - Write a test that executes that path
   - Verify the behavior

2. **Common uncovered paths:**
   - Exception catch blocks
   - Null/empty string checks
   - Edge case boundaries
   - Default switch cases
   - Fallback logic

### Step 5: Investigate Timeout Mutants

**7 timeout mutants** - May indicate:
- Infinite loops
- Deadlocks
- Long-running operations
- Test setup issues

**Action:**
1. Review timeout mutants in report
2. Determine if they're legitimate timeouts or test issues
3. Add appropriate timeout handling or fix tests

## Key Files to Focus On

Based on mutation testing patterns, prioritize:

1. **ExxerHub.cs** - Error handling, null checks, exception paths
2. **MessageBatcher.cs** - Timer callbacks, batch size boundaries, empty batch handling
3. **MessageThrottler.cs** - Timing logic, message equality, delay calculations
4. **Dashboard.cs** - State machine transitions, connection error handling
5. **ServiceHealth.cs** - Status comparison, event raising logic
6. **ReconnectionStrategy.cs** - Delay calculation, boundary conditions

## Testing Standards (MUST FOLLOW)

- **Framework:** xUnit v2 (NOT v3)
- **Assertions:** Shouldly
- **Mocking:** NSubstitute
- **Cancellation:** Use `CancellationToken.None` (NOT `TestContext.Current.CancellationToken`)
- **Pattern:** AAA (Arrange, Act, Assert)
- **Documentation:** XML comments for all test methods
- **Focus:** Behavioral testing (NO getter/setter tests)

## Commands

### Run Tests
```powershell
dotnet test ExxerCube.Prisma.SignalR.Abstractions.Tests\ExxerCube.Prisma.SignalR.Abstractions.Tests.csproj --verbosity normal
```

### Run Stryker
```powershell
cd ExxerCube.Prisma.SignalR.Abstractions
dotnet stryker --project ExxerCube.Prisma.SignalR.Abstractions.csproj --test-project ../ExxerCube.Prisma.SignalR.Abstractions.Tests/ExxerCube.Prisma.SignalR.Abstractions.Tests.csproj --verbosity info
```

### View Report
Open: `ExxerCube.Prisma.SignalR.Abstractions/StrykerOutput/[LATEST]/reports/mutation-report.html`

## Success Criteria

- ✅ Mutation score ≥ 95%
- ✅ All surviving mutants killed (0 survivors)
- ✅ All NoCoverage mutants covered (0 NoCoverage)
- ✅ All tests passing (100/100)
- ✅ No new compilation errors

## Notes

- **Timing Tests:** Some tests may be flaky due to timing precision. Consider using `ManualResetEventSlim` with appropriate timeouts.
- **Integration Tests:** Some scenarios (like HubConnection mocking) require integration tests with actual SignalR infrastructure.
- **Exception Paths:** Many survivors are likely in exception handling - ensure all catch blocks are tested.
- **Boundary Conditions:** Focus on exact boundaries (==, !=) as these are common mutation targets.

## Progress Tracking

- **Baseline:** 29.57% (before new tests)
- **Current:** 37.83% (after ~50 new tests)
- **Target:** 95%+ (need ~57% more improvement)
- **Remaining Work:** ~47 survivors + ~96 NoCoverage = ~143 mutants to address

## Next Steps

1. Open the mutation report HTML file
2. Filter by "Survived" - identify top files with survivors
3. For each file, add tests targeting specific mutations
4. Filter by "NoCoverage" - add tests to cover uncovered paths
5. Re-run Stryker and iterate until 95%+ score achieved

---

**Last Updated:** 2025-11-19
**Current Mutation Score:** 37.83%
**Tests:** 100 (96 passing, 4 timing-related flaky)

