# Code Coverage Guide - ExxerCube Prisma

**Last Updated**: 2025-12-05
**Status**: Ready for ITDD → Refactoring Phase Transition
**Primary Goal**: Branch Coverage for Mutation Testing Readiness
**.NET Version**: 10.0 (uses coverlet/XPlat Code Coverage)

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [Understanding Branch Coverage](#understanding-branch-coverage)
3. [Running Coverage](#running-coverage)
4. [CI/CD Integration](#cicd-integration)
5. [Interpreting Results](#interpreting-results)
6. [Coverage Thresholds](#coverage-thresholds)
7. [Mutation Testing Preparation](#mutation-testing-preparation)
8. [Troubleshooting](#troubleshooting)

---

## Quick Start

### Run Full Coverage Analysis

```powershell
# From repository root
.\run-coverage.ps1
```

This will:
1. Build the solution (Release configuration)
2. Run **all** tests with coverage collection
3. Generate **Cobertura XML** (coverage.xml) for CI/CD
4. Generate **HTML report** (coverage/html/index.html)
5. Open the HTML report in your browser

### Run Coverage for Specific Tests

```powershell
# Filter by test name pattern
.\run-coverage.ps1 -Filter "FullyQualifiedName~Domain"

# Skip rebuild (faster for iterative development)
.\run-coverage.ps1 -SkipBuild

# Generate only Cobertura XML (no HTML)
.\run-coverage.ps1 -GenerateHtml:$false
```

---

## Understanding Branch Coverage

### Why Branch Coverage Matters

**Line Coverage** tells you which lines executed.
**Branch Coverage** tells you which decision paths executed.

**Example:**
```csharp
public bool IsValid(string value)
{
    if (string.IsNullOrEmpty(value))  // <-- BRANCH POINT
        return false;                  // <-- Branch 1
    return true;                       // <-- Branch 2
}
```

- **Line Coverage**: 100% if any test calls `IsValid()`
- **Branch Coverage**: 100% only if tests call both `IsValid(null)` AND `IsValid("valid")`

### Why It's Critical for Mutation Testing

**Mutation testing** creates code mutants like:
```csharp
// Original
if (value > 10) return true;

// Mutant 1: Boundary change
if (value >= 10) return true;  // Did tests catch this?

// Mutant 2: Operator change
if (value < 10) return true;   // Did tests catch this?
```

**Branch coverage ensures**:
- All conditional paths are tested
- Mutants in decision logic are detected
- False sense of security from line coverage is avoided

---

## Running Coverage

### Local Development Workflow

```powershell
# 1. Run coverage after making changes
.\run-coverage.ps1

# 2. Review HTML report (opens automatically)
#    Look for RED areas = uncovered branches

# 3. Write tests for uncovered branches

# 4. Re-run coverage to verify
.\run-coverage.ps1 -SkipBuild
```

### CI/CD Workflow

```powershell
# Run in CI/CD pipeline
.\run-coverage-ci.ps1

# With coverage thresholds (fails if not met)
.\run-coverage-ci.ps1 -MinimumBranchCoverage 80 -MinimumLineCoverage 85
```

**Output**:
- `coverage.xml` → Upload to Azure DevOps / GitHub Actions / GitLab CI

---

## CI/CD Integration

### Azure DevOps

```yaml
# azure-pipelines.yml
steps:
  - task: PowerShell@2
    displayName: 'Run Code Coverage'
    inputs:
      filePath: '$(Build.SourcesDirectory)/run-coverage-ci.ps1'
      arguments: '-MinimumBranchCoverage 75 -MinimumLineCoverage 80'

  - task: PublishCodeCoverageResults@2
    displayName: 'Publish Coverage Results'
    inputs:
      summaryFileLocation: '$(Build.SourcesDirectory)/coverage.xml'
      codecoverageTool: 'Cobertura'
      failIfCoverageEmpty: true
```

### GitHub Actions

```yaml
# .github/workflows/coverage.yml
name: Code Coverage

on: [push, pull_request]

jobs:
  coverage:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Run Coverage
        shell: pwsh
        run: ./run-coverage-ci.ps1 -MinimumBranchCoverage 75

      - name: Upload Coverage to Codecov
        uses: codecov/codecov-action@v4
        with:
          files: ./coverage.xml
          flags: unittests
          fail_ci_if_error: true
```

### GitLab CI

```yaml
# .gitlab-ci.yml
coverage:
  stage: test
  script:
    - pwsh ./run-coverage-ci.ps1 -MinimumBranchCoverage 75
  coverage: '/Line Coverage: (\d+\.?\d*)%/'
  artifacts:
    reports:
      coverage_report:
        coverage_format: cobertura
        path: coverage.xml
```

---

## Interpreting Results

### HTML Report Structure

Open `coverage/html/index.html` to see:

```
┌─ Summary
│  ├─ Line Coverage: 87.3%
│  ├─ Branch Coverage: 82.1%  <-- PRIMARY METRIC
│  └─ Method Coverage: 91.5%
│
├─ ExxerCube.Prisma.Domain
│  ├─ Validators/
│  │  └─ FieldPatternValidator.cs
│  │     ├─ Lines: 95.2% (20/21)
│  │     ├─ Branches: 90.0% (18/20)  <-- Focus here
│  │     └─ Methods: 100% (7/7)
│  └─ Events/
│     └─ DocumentDownloadedEvent.cs
│        └─ ... (drill down)
```

### Color Coding

- **GREEN**: Fully covered (100%)
- **YELLOW**: Partially covered (50-99%)
- **RED**: Not covered (0-49%)

### Coverage Gaps to Prioritize

1. **RED Branches** in Domain Logic (highest risk)
2. **RED Branches** in Validators (data integrity risk)
3. **YELLOW Branches** in Application Services
4. **RED Branches** in Infrastructure (integration risk)

---

## Coverage Thresholds

### Recommended Thresholds (Phase-Based)

| Phase | Line Coverage | Branch Coverage | Rationale |
|-------|---------------|-----------------|-----------|
| **ITDD (Current)** | 70% | 65% | Behavioral tests cover happy paths |
| **Pre-Refactor** | 80% | **75%** | Safety net before structural changes |
| **Post-Refactor** | 85% | **80%** | Ensure refactoring didn't break edge cases |
| **Mutation-Ready** | 90% | **85%** | Ready for mutation testing |

### Current Status

**Baseline Coverage (as of 2025-12-05):**
- **Line Coverage**: 31.95%
- **Branch Coverage**: 36.08%

**Gap Analysis:**
- Need **+48% line coverage** to reach 80% target
- Need **+39% branch coverage** to reach 75% target

Run this to check current coverage:
```powershell
.\run-coverage.ps1
```

**Next Actions:**
1. Review HTML report (`coverage\html\index.html`) to identify RED areas
2. Focus on Domain layer and validators (highest business risk)
3. Add tests for uncovered branches systematically
4. Aim for incremental improvement (5-10% per iteration)

**Target for next milestone**: **75% branch coverage**

---

## Mutation Testing Preparation

### When Stryker.NET is Fixed

Your `coverage.runsettings` is already configured for mutation testing:

```xml
<!-- Branch coverage enabled -->
<UseVerifiableInstrumentation>True</UseVerifiableInstrumentation>
```

### Simulating Mutation Testing Today

**Manual mutation simulation**:
1. Identify a critical branch (e.g., `IsValidRFC` validator)
2. Manually mutate the code:
   ```csharp
   // Original
   if (value.Length == 13) return true;

   // Mutant
   if (value.Length >= 13) return true;  // Should tests catch this?
   ```
3. Run tests:
   ```powershell
   dotnet test --filter "FullyQualifiedName~FieldPatternValidator"
   ```
4. **If tests still pass** → Your tests have a mutation gap!
5. Write a test that fails with the mutant
6. Revert the mutation

**Repeat for high-risk branches**:
- Validators
- Business rule conditionals
- Security checks
- Edge case handling

---

## Troubleshooting

### "No coverage files found!"

**Cause**: Coverage collector didn't run.

**Fix**:
```powershell
# Ensure Microsoft.Testing.Extensions.CodeCoverage is installed
cd Prisma/Code/Src/CSharp
dotnet list package | Select-String "CodeCoverage"

# If missing, add to test projects
dotnet add package Microsoft.Testing.Extensions.CodeCoverage
```

### "Branch coverage shows 0%"

**Cause**: Instrumentation not enabled.

**Fix**: Verify `coverage.runsettings`:
```xml
<UseVerifiableInstrumentation>True</UseVerifiableInstrumentation>
```

### "Coverage report doesn't open"

**Fix**:
```powershell
# Manually open report
start coverage/html/index.html
```

### "Tests pass but coverage shows gaps in obvious code"

**Likely Cause**: Code is executing but branches aren't tested.

**Example**:
```csharp
// This executes...
public bool Validate(string value)
{
    if (value == null) return false;  // <-- Not tested!
    return true;                      // <-- Tested
}
```

**Fix**: Add test:
```csharp
[Fact]
public void Validate_NullValue_ReturnsFalse()
{
    var result = Validate(null);
    result.ShouldBeFalse();
}
```

---

## Next Steps

### Immediate Actions

1. **Run initial coverage**:
   ```powershell
   .\run-coverage.ps1
   ```

2. **Review coverage gaps**:
   - Open `coverage/html/index.html`
   - Sort by "Branch Coverage" (ascending)
   - Prioritize RED files in Domain layer

3. **Write tests for uncovered branches**:
   - Focus on validators first (highest risk)
   - Then domain logic
   - Then infrastructure

4. **Set CI/CD threshold**:
   - Start with current coverage as baseline
   - Increase by 5% per sprint

### Long-Term Strategy

1. **Achieve 75% branch coverage** (prerequisite for refactoring)
2. **Implement fluent API** (with tests!)
3. **Add benchmark tests** for hot paths
4. **Refactor with confidence** (TDD Red-Green-Refactor)
5. **Prepare for mutation testing** when Stryker.NET is stable

---

## Files Created

- `coverage.runsettings` → Coverage configuration (branch coverage enabled)
- `run-coverage.ps1` → Local development script
- `run-coverage-ci.ps1` → CI/CD pipeline script
- `COVERAGE.md` (this file) → Documentation
- `.gitignore` (updated) → Exclude coverage artifacts

## Additional Resources

- [Martin Fowler - Test Coverage](https://martinfowler.com/bliki/TestCoverage.html)
- [Mutation Testing Intro](https://stryker-mutator.io/docs/)
- [Cobertura XML Spec](https://cobertura.github.io/cobertura/)
- [ReportGenerator Docs](https://github.com/danielpalme/ReportGenerator)

---

**Questions?** Review this guide or check HTML report tooltips for inline help.

**Ready to run?** Execute `.\run-coverage.ps1` now!
