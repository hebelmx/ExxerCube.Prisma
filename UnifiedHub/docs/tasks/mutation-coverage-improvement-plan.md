# Mutation Coverage Improvement Plan

## Current Status
- **Mutation Score:** 46.02% (Target: 80%)
- **Total Mutants:** 322
- **Mutants Tested:** 166
- **Mutants Skipped:** 156
  - 10 CompileError
  - **60 NoCoverage** ⚠️ CRITICAL
  - 86 Ignored
- **Test Results:**
  - Killed: 90
  - **Survived: 73** ⚠️ CRITICAL
  - Timeout: 3

## Gap Analysis

### Critical Issues
1. **60 NoCoverage Mutants** - Code paths not covered by any test
2. **73 Surviving Mutants** - Tests exist but don't catch mutations
3. **46.02% Score** - Need to improve by ~34% to reach 80%

## Strategy

### Phase 1: Address NoCoverage Mutants (60 mutants)
**Goal:** Add tests to cover all code paths

**Areas to investigate:**
1. **Infrastructure Layer:**
   - `MessageThrottler` - Edge cases, error paths
   - `MessageBatcher` - Batching logic, flush scenarios
   - `ReconnectionStrategy` - Reconnection attempts, backoff logic

2. **Presentation Layer (Blazor):**
   - `ConnectionStateIndicator` - UI state transitions
   - `DashboardComponent` - Component lifecycle, error handling

3. **Extension Methods:**
   - `ServiceCollectionExtensions` - All registration paths
   - Edge cases in service registration

4. **Abstractions:**
   - `ExxerHub<T>` - Error paths, cancellation scenarios
   - `Dashboard<T>` - Connection failure scenarios
   - `ServiceHealth<T>` - Health status transitions

### Phase 2: Kill Surviving Mutants (73 mutants)
**Goal:** Improve test assertions and edge case coverage

**Common causes:**
1. **Weak Assertions** - Tests pass even when code is mutated
2. **Missing Edge Cases** - Boundary conditions not tested
3. **Error Paths** - Exception handling not verified
4. **State Transitions** - Intermediate states not checked

## Action Items

### Immediate (High Priority)
- [ ] Analyze Stryker HTML report to identify specific NoCoverage locations
- [ ] Create test files for uncovered infrastructure components
- [ ] Add integration tests for Blazor components
- [ ] Review surviving mutants to understand why they survived

### Short Term
- [ ] Add tests for all error paths in `ExxerHub<T>`
- [ ] Test all reconnection strategy scenarios
- [ ] Cover all message throttling edge cases
- [ ] Test batching logic thoroughly

### Medium Term
- [ ] Improve assertions in existing tests
- [ ] Add boundary condition tests
- [ ] Test cancellation token propagation
- [ ] Verify all state transitions

## Success Metrics
- **NoCoverage Mutants:** 60 → 0
- **Surviving Mutants:** 73 → <20 (target: <10%)
- **Mutation Score:** 46.02% → 80%+

## Notes
- Focus on quality over quantity - each test should kill multiple mutants
- Use integration tests where unit tests can't cover behavior
- Consider property-based testing for complex scenarios
- Review mutation report HTML for specific locations

