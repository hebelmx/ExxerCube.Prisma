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

## Next Steps
1. Identify newly failing tests
2. Check test logs for fixture path errors
3. Fix fixture paths or copy fixtures to new locations
