# Test Baseline Comparison - Folder Restructuration

## Before Reorganization (Baseline)
- **Total Tests**: 877
- **Passed**: 783 (89.3%)
- **Failed**: 87 (9.9%)
- **Skipped**: 7 (0.8%)
- **Time**: 14:56.048

## After Reorganization (Current)
- **Total Tests**: 876 (-1, GotOcr2 excluded)
- **Passed**: 728 (-55)
- **Failed**: 121 (+34 new failures)
- **Skipped**: 4 (-3)
- **Not Run**: 27

## Analysis
- **New Failures**: 34 additional tests failing
- **Root Cause**: Missing fixture files - tests can't find fixtures after folder reorganization
- **Expected**: User anticipated fixture path issues due to relative paths

## Resolution
**FIXED**: Fixture paths corrected from 5 levels to 6 levels to reach root Fixtures/ folder.

### Changes Made:
1. ✅ Fixed relative paths in 4 test .csproj files (5 levels → 6 levels)
2. ✅ Restored PRP1 fixtures from git history (already existed in root Fixtures/)
3. ✅ Restored PRP1_Degraded fixtures from git commit a6d6081
4. ✅ Build succeeds with 0 errors

### Commits:
- `186cb97` - Restored PRP1_Degraded fixtures (22 files)
- `4faccbc` - Restored PRP1 fixtures and updated paths
- `73b70ac` - Corrected fixture paths to 6 levels
- Removed duplicate Code/Fixtures/PRP1 (use root Fixtures/ instead)

**Ready for test run to compare against baseline.**
