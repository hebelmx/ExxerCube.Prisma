# Fixture Status After Folder Reorganization

## Current Status
**Build**: ✅ Succeeds (0 errors)
**Fixture Paths**: ⚠️ Partially fixed

## Working Projects (Fixtures Copied Successfully)
✅ `Tests.Infrastructure.Extraction.Teseract` (02-Infrastructure) - 6 levels up
✅ `Tests.Infrastructure.Extraction.GotOcr2` (02-Infrastructure) - 6 levels up
✅ `Tests.Infrastructure.XmlExtraction` (02-Infrastructure) - 6 levels up
✅ `Tests.System (Teseract duplicate)` (03-System) - 6 levels up

## Partially Working / Needs Investigation
⚠️ `Tests.System.Ocr.Pipeline` (03-System) - Fixtures not copying despite .csproj configuration
⚠️ `Tests.Infrastructure.BrowserAutomation.E2E` (03-System) - Has filename typos (6 sixes vs 7 sixes)

## Issues Found

### 1. Tests.System.Ocr.Pipeline - Fixtures Not Copying
**Problem**: PRP1_Degraded fixtures not being copied to output directory
**Path Tried**: `..\..\..\..\..\..\..\Fixtures\PRP1_Degraded\...` (6 levels)
**Expected Output**: `Code/Src/bin/Debug/ExxerCube.Prisma.Tests.System.Ocr.Pipeline/net10.0/Fixtures/PRP1_Degraded/`
**Actual**: Directory not created

**Tests Affected**:
- `AnalyticalFilterE2ETests` - All theory test data
- `PolynomialFilterE2ETests` - All theory test data

### 2. Filename Typos (6 sixes vs 7 sixes)
**Problem**: Test code references wrong filenames
**Test Code Has**: `333ccc-666666662025.pdf` (6 sixes)
**Actual Filename**: `333ccc-6666666662025.pdf` (7 sixes)

**Similarly**:
- Test: `555CCC-6666662025.pdf` (6 sixes)
- Actual: `555CCC-66666662025.pdf` (7 sixes)

**Files Affected**:
- `Tests.Infrastructure.BrowserAutomation.E2E/OcrExtractionE2ETests.cs`
- `Tests.Infrastructure.BrowserAutomation.E2E/XmlExtractionE2ETests.cs`

### 3. Missing PRP1 XML Fixtures
Some test projects expect XML fixtures to be copied but references are missing:
- Need to add `<None Include>` for `*.xml` files in PRP1 folder

## Path Depth Reference

From test project to Prisma root Fixtures/:

| Project Location | Depth | Path |
|-----------------|-------|------|
| `04-Tests/02-Infrastructure/ProjectName/` | 6 | `..\..\..\..\..\..\..\Fixtures\` |
| `04-Tests/03-System/ProjectName/` | 6 | `..\..\..\..\..\..\..\Fixtures\` |

## Next Steps

1. **Investigate why Tests.System.Ocr.Pipeline fixtures aren't copying**
   - Verify .csproj path syntax
   - Check if project uses different build output path
   - May need absolute path or different approach

2. **Fix filename typos**
   - Update test code to use correct 7-six filenames
   - OR rename fixture files to match test code (less preferable)

3. **Add missing XML fixture references**
   - Ensure all test projects that need XML files have `<None Include>` entries

4. **Run full test suite** to identify any other missing fixtures

## Commits
- `186cb97` - Restored PRP1_Degraded fixtures
- `4faccbc` - Restored PRP1 fixtures
- `73b70ac` - Fixed paths to 6 levels (working for most projects)
- Latest - WIP: Trying to fix Tests.System.Ocr.Pipeline
