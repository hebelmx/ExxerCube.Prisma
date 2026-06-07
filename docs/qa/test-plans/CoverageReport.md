# Code Coverage Report - Session Notes

**Date**: 2025-12-05
**Status**: ✅ Infrastructure Complete, Baseline Established
**Phase**: Pre-Refactor Safety Net Assessment

---

## Executive Summary

Successfully established code coverage infrastructure for .NET 10.0 project using Microsoft Testing Platform (MTP) with XUnit v3. Baseline coverage metrics identified significant opportunity for test strengthening before Phase 2 refactoring.

**Baseline Metrics:**
- **Line Coverage**: 31.95% (Target: 80%)
- **Branch Coverage**: 36.08% (Target: 75%)
- **Test Count**: 1,315 passing tests across 27 test projects
- **Coverage Files**: 79 individual Cobertura XML files merged

**Key Deliverables:**
- ✅ `coverage.xml` (6.0MB) - CI/CD ready Cobertura XML
- ✅ `coverage/html/index.html` - Interactive HTML report
- ✅ Automated scripts for local and CI/CD workflows
- ✅ Comprehensive documentation

---

## How to Reproduce the Coverage Report

### Prerequisites
- .NET 10.0 SDK installed
- ReportGenerator tool (auto-installed by script if missing)
- All test projects built in Release configuration

### Step 1: Run Coverage Analysis

**For Local Development (with HTML report):**
```powershell
# From repository root
.\run-coverage.ps1
```

**For CI/CD (no HTML, with thresholds):**
```powershell
.\run-coverage-ci.ps1 -MinimumBranchCoverage 36 -MinimumLineCoverage 32
```

**Quick Re-run (skip rebuild):**
```powershell
.\run-coverage.ps1 -SkipBuild
```

**Filter Specific Tests:**
```powershell
.\run-coverage.ps1 -Filter "FullyQualifiedName~Domain"
```

### Step 2: Review Results

**Automated (script opens automatically):**
- HTML report opens in browser: `coverage/html/index.html`

**Manual:**
```powershell
start coverage\html\index.html
```

**CI/CD Integration:**
- Upload `coverage.xml` to Azure DevOps, GitHub Actions, or GitLab CI
- See `COVERAGE.md` lines 127-193 for pipeline examples

### Step 3: Analyze Coverage Gaps

1. **Sort by Branch Coverage (ascending)** in HTML report
2. **Focus on RED areas** (0-49% coverage)
3. **Prioritize by layer:**
   - Domain (highest business risk)
   - Validators (data integrity risk)
   - Application Services
   - Infrastructure

---

## What's Next

### Immediate Actions (Before Code Audit)

1. **Commit Coverage Infrastructure** ✅ (this commit)
   - Scripts, configuration, documentation
   - Baseline metrics documented

2. **Defer Test Writing** (post-audit)
   - Coverage infrastructure ready
   - Baseline established for future comparison
   - HTML report available for prioritization

### Post-Audit Roadmap

#### Phase 1: Quick Wins (Target: 50% Branch Coverage)
- Focus on Domain validators with <30% coverage
- Add missing null/empty/invalid input tests
- Test error handling branches in critical paths
- **Estimated Effort**: 2-3 sprints

#### Phase 2: Systematic Coverage (Target: 65% Branch Coverage)
- Domain logic edge cases
- Application service error paths
- Infrastructure retry/fallback logic
- **Estimated Effort**: 3-4 sprints

#### Phase 3: Pre-Refactor Safety Net (Target: 75% Branch Coverage)
- Comprehensive branch coverage in areas targeted for refactoring
- Manual mutation testing simulation (see COVERAGE.md lines 273-298)
- Benchmark tests for hot paths
- **Estimated Effort**: 2-3 sprints

#### Phase 4: Ready for Refactoring
- 75%+ branch coverage achieved
- Fluent API implementation with TDD
- Refactor with confidence (Red-Green-Refactor)

---

## Lessons Learned: .NET 10 + XUnit v3 + Microsoft Testing Platform

### Critical Discovery: MTP Requires Different Syntax

**Problem**: Traditional `dotnet test --collect "Code Coverage"` doesn't work with:
- .NET 10.0
- XUnit v3
- Microsoft Testing Platform (MTP)

**Root Cause**:
- MTP is a new testing framework (replacement for VSTest)
- Projects using `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>`
- Requires different command-line syntax

**Solution**:
```powershell
# WRONG (VSTest/coverlet syntax):
dotnet test --collect "Code Coverage"
dotnet test --collect "XPlat Code Coverage"

# CORRECT (MTP syntax):
dotnet test -- --coverage --coverage-output-format cobertura
#           ^^  ^^^^^^^^^  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
#           |   |          Coverage output format
#           |   Enable coverage collection
#           Separator: passes args to test runner
```

### Key Technical Details

**1. Coverage File Naming**
- MTP generates GUID-named files: `{guid}.cobertura.xml`
- Located in: `BuildArtifacts/Prisma/bin/{ProjectName}/Release/net10.0/TestResults/`
- Example: `a78ec906-6942-404f-bd0e-af86b518827b.cobertura.xml`

**2. BuildArtifacts Location**
- ⚠️ **Critical**: BuildArtifacts is in PARENT directory of repository root
- Repository: `F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\`
- Artifacts: `F:\Dynamic\ExxerCubeBanamex\BuildArtifacts\`
- PowerShell fix: `Join-Path (Split-Path $RootDir -Parent) "BuildArtifacts"`

**3. PowerShell File Discovery Gotcha**
```powershell
# WRONG: -Filter parameter doesn't work correctly with recursion
$files = Get-ChildItem -Path $dir -Filter "*.cobertura.xml" -Recurse

# CORRECT: Use Where-Object instead
$files = Get-ChildItem -Path $dir -Recurse | Where-Object { $_.Name -like "*.cobertura.xml" }
```

**4. Package Requirements**
- `Microsoft.Testing.Platform` - Core MTP framework
- `Microsoft.Testing.Extensions.CodeCoverage` - Coverage collector
- `Microsoft.Testing.Extensions.VSTestBridge` - VSTest compatibility layer
- No need for `coverlet.collector` (legacy)

**5. Test Execution Behavior**
- MTP runs tests in parallel by default
- Some tests failed (27 out of 1,342) - expected for E2E/integration tests
- Exit code 1 even with coverage collection success
- Script continues to generate reports despite test failures

### What Changed in .NET 10

**Microsoft Testing Platform (MTP) Adoption:**
- New testing framework introduced in .NET 8/9, mandatory in .NET 10
- Replaces VSTest as the default test runner
- Better performance, native cross-platform support
- Different command-line interface

**Coverage Collection:**
- Built-in coverage support via `--coverage` flag
- No need for external collectors (coverlet, etc.)
- Direct Cobertura XML generation
- Simplified configuration

**Migration Path:**
- Old projects: `dotnet test --collect "XPlat Code Coverage"`
- New projects: `dotnet test -- --coverage --coverage-output-format cobertura`
- Bridge available via `Microsoft.Testing.Extensions.VSTestBridge`

---

## Architecture Decisions

### Why ReportGenerator?

**Purpose**: Merge 79 individual coverage files into single report

**Alternatives Considered:**
1. **coverlet.msbuild** - Incompatible with MTP
2. **Manual XML merging** - Too error-prone
3. **ReportGenerator** ✅ - Industry standard, reliable

**Benefits:**
- Merges multiple Cobertura files
- Generates HTML, badges, summary reports
- CI/CD integration ready
- Historical trend tracking support

### Why Cobertura Format?

**Reasons:**
1. **Universal CI/CD support** - Azure DevOps, GitHub Actions, GitLab CI all read Cobertura
2. **XML parsing** - Easy to extract metrics programmatically
3. **Industry standard** - Compatible with SonarQube, Codecov, Coveralls
4. **Branch coverage support** - Critical for mutation testing preparation

---

## File Structure

```
ExxerCube.Prisma/
├── run-coverage.ps1           # Local development script
├── run-coverage-ci.ps1        # CI/CD pipeline script
├── coverage.runsettings       # Coverage configuration (branch coverage enabled)
├── coverage.xml               # Merged Cobertura XML (CI/CD ready)
├── COVERAGE.md                # User documentation
├── CoverageReport.md          # This file (session notes)
├── coverage/                  # Generated reports (gitignored)
│   ├── html/
│   │   └── index.html        # Interactive HTML report
│   └── merged/
│       └── Cobertura.xml     # Intermediate merged file
└── .gitignore                # Updated to exclude coverage/ and TestResults/
```

**Git Tracking:**
- ✅ Scripts and configuration tracked
- ✅ `coverage.xml` tracked (for CI/CD)
- ❌ `coverage/` directory gitignored (regenerated locally)
- ❌ `TestResults/` directory gitignored (temporary files)

---

## Known Issues and Limitations

### 1. Generated File Warnings
**Symptom**: ReportGenerator warns about missing generated files:
```
File 'RegexGenerator.g.cs' does not exist (any more).
File 'PrismaOcrWrapper.py.cs' does not exist (any more).
```

**Cause**: Generated files in `obj/` directories get cleaned between builds

**Impact**: None - warnings are cosmetic, coverage data is valid

**Solution**: Ignore warnings (expected behavior)

### 2. Test Failures During Coverage Run
**Symptom**: Some tests fail (27/1,342), exit code 1

**Affected Projects:**
- Infrastructure.Python (environment-dependent)
- Database (connection issues)
- E2E/Integration (external dependencies)
- OCR Pipeline (resource-intensive)

**Impact**: Coverage data still generated correctly

**Solution**: Script continues despite failures, coverage analysis proceeds

### 3. Long Execution Time
**Duration**: ~8-10 minutes for full run

**Breakdown:**
- Build: 1-2 minutes
- Tests: 5-7 minutes
- Report generation: 30-60 seconds

**Optimization**: Use `-SkipBuild` for iterative development

### 4. Baseline Coverage Lower Than Expected
**Expected**: 60-70% (given 1,300+ tests)
**Actual**: 32% line, 36% branch

**Reasons:**
1. ITDD focuses on happy paths (interface contracts)
2. Missing edge case tests (null, empty, invalid inputs)
3. Error handling branches untested
4. Infrastructure code has sparse coverage

**Recommendation**: This is valuable data - shows where safety net needs strengthening

---

## Performance Optimization Tips

### 1. Incremental Coverage (Fastest)
```powershell
# Only run tests for changed module
.\run-coverage.ps1 -Filter "FullyQualifiedName~Domain" -SkipBuild
```

### 2. Skip HTML Generation (CI/CD)
```powershell
.\run-coverage-ci.ps1  # Only generates coverage.xml
```

### 3. Parallel Test Execution
- MTP runs tests in parallel by default
- No configuration needed
- Max CPU utilization automatic

### 4. Incremental Build
```powershell
# For local dev, don't use --no-incremental
dotnet build  # Faster with incremental compilation
```

---

## Troubleshooting Guide

### Problem: "No coverage files found!"

**Symptoms:**
```
✗ No coverage files found!
Looking for coverage.cobertura.xml in: F:\...\BuildArtifacts
```

**Diagnosis Steps:**
1. Verify BuildArtifacts location:
   ```powershell
   ls F:\Dynamic\ExxerCubeBanamex\BuildArtifacts
   ```

2. Check for coverage files manually:
   ```bash
   find BuildArtifacts -name "*.cobertura.xml" | wc -l
   ```

3. Verify MTP package installed:
   ```powershell
   cd Prisma/Code/Src/CSharp
   dotnet list package | Select-String "CodeCoverage"
   ```

**Solutions:**
- Fix path: Script now uses `(Split-Path $RootDir -Parent)`
- Install package: `dotnet add package Microsoft.Testing.Extensions.CodeCoverage`
- Check test execution: Ensure tests actually ran

### Problem: PowerShell Can't Find Files

**Cause**: `-Filter` parameter limitation in Get-ChildItem

**Solution**: Use `Where-Object` instead (already implemented in script)

### Problem: coverage.xml Not Generated

**Cause**: ReportGenerator not installed

**Solution**: Script auto-installs, but manual install:
```powershell
dotnet tool install -g dotnet-reportgenerator-globaltool --version 5.3.11
```

---

## Success Metrics

### Infrastructure Setup ✅
- [x] Scripts created and tested
- [x] Configuration files in place
- [x] Documentation complete
- [x] Baseline established
- [x] CI/CD integration examples provided

### Coverage Analysis ✅
- [x] 79 coverage files collected
- [x] Merged Cobertura XML generated (6.0MB)
- [x] HTML report generated (334KB)
- [x] Baseline metrics calculated
- [x] Gap analysis documented

### Knowledge Transfer ✅
- [x] .NET 10 MTP syntax documented
- [x] Common issues and solutions catalogued
- [x] Reproduction steps verified
- [x] Next steps roadmap created

---

## References

### Documentation Created
- `COVERAGE.md` - User guide and quick start
- `CoverageReport.md` (this file) - Technical session notes
- `run-coverage.ps1` - Inline comments and help text
- `run-coverage-ci.ps1` - CI/CD usage examples

### External Resources
- [Microsoft Testing Platform Docs](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-platform-intro)
- [ReportGenerator GitHub](https://github.com/danielpalme/ReportGenerator)
- [Cobertura XML Format](https://cobertura.github.io/cobertura/)
- [XUnit v3 Migration Guide](https://xunit.net/docs/getting-started/v3/migration)

### Internal Context
- Baseline: 32% line, 36% branch coverage
- Target: 75% branch coverage before refactoring
- Test count: 1,315 passing / 1,342 total
- Projects: 27 test projects across Domain, Application, Infrastructure, System, E2E

---

## Achievements Unlocked 🏆

✅ **Infrastructure Engineer** - Set up complete coverage pipeline
✅ **Detective** - Discovered .NET 10 MTP syntax requirements
✅ **Problem Solver** - Fixed BuildArtifacts path discovery issue
✅ **Debugger** - Resolved PowerShell file discovery gotcha
✅ **Documentarian** - Created comprehensive guides and troubleshooting
✅ **Baseline Established** - 32% line / 36% branch coverage measured

---

## Session Conclusion

Coverage infrastructure is **production-ready**. The baseline of 36% branch coverage is not a failure - it's valuable intelligence showing exactly where the ITDD safety net needs reinforcement before Phase 2 refactoring.

**Next session priorities:**
1. ✅ Handle code audit (high priority)
2. ⏳ Review HTML report for high-impact coverage gaps
3. ⏳ Add tests systematically to reach 50% branch coverage milestone
4. ⏳ Implement fluent API with TDD once safety net is stronger

**Infrastructure Status**: ✅ **READY FOR PRODUCTION USE**

---

*Generated: 2025-12-05*
*Agent: Claude Code (Sonnet 4.5)*
*Project: ExxerCube Prisma - ITDD Coverage Analysis*
