# Build Fixes Needed

## Summary
After migrating test files, several build errors need to be fixed. This document tracks the fixes needed.

## Fixed Issues ✅
1. ✅ Deleted duplicate `TestContextLogger.cs` (conflicted with `NoOpLogger.cs`)
2. ✅ Added `using ExxerCube.Prisma.Domain.Entities;` to `TestImageDataGenerator.cs`
3. ✅ Added missing project references to `Tests.Application`
4. ✅ Added missing project references to `Tests.Infrastructure.Database`
5. ✅ Added missing project references to `Tests.Domain`
6. ✅ Removed duplicate `PlaywrightEndToEndTests.cs` from `Tests.Application` (moved to `Tests.EndToEnd`)
7. ✅ Removed duplicate `EndToEndPipelineTests.cs` from `Tests.Application` (moved to `Tests.System`)

## Remaining Issues

### 1. Python Wrapper (Known Issue - Skip for Now)
- **File**: `Infrastructure/Python/PrismaOcrWrapperAdapter.cs`
- **Error**: `IPrismaOcrWrapper` not found (CSnakes code generation issue)
- **Status**: User requested to skip Python wrapper issues
- **Action**: Exclude from build or fix CSnakes configuration

### 2. Missing Using Statements in GlobalUsings.cs
- **Files**: Multiple test projects
- **Issue**: Some types not found due to missing using statements
- **Action**: Add missing using statements to GlobalUsings.cs files

### 3. Missing Project References
- **Files**: Various test projects
- **Issue**: Tests reference types from projects not referenced
- **Action**: Add missing project references

## Next Steps
1. Fix remaining GlobalUsings.cs files
2. Add missing project references to all test projects
3. Fix any remaining compilation errors
4. Run full test suite to verify migration



