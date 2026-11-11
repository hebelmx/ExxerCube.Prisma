# Troubleshooting Guide

## NuGet Package Restore Errors

### Issue: "The operation failed as details for project could not be loaded"

**Symptom**:
```
NuGet package restore failed. Please see Error List window for detailed warnings and errors.
Error occurred while restoring NuGet packages: The operation failed as details for project ExxerCube.Prisma.ConsoleApp.GotOcr2Demo could not be loaded.
```

**Root Cause**: Stale or corrupted cache files in obj/bin folders after folder reorganization

**Solution**:
```powershell
# Clean the specific project
cd Code/Src/CSharp
rm -rf 05-ConsoleApp/ConsoleApp.GotOcr2Demo/obj
rm -rf 05-ConsoleApp/ConsoleApp.GotOcr2Demo/bin

# Restore the project
dotnet restore 05-ConsoleApp/ConsoleApp.GotOcr2Demo/ExxerCube.Prisma.ConsoleApp.GotOcr2Demo.csproj

# Or clean entire solution
dotnet clean ExxerCube.Prisma.sln
dotnet restore ExxerCube.Prisma.sln
```

**Prevention**: After major folder reorganizations, always clean obj/bin folders:
```powershell
# Use the cleanup script
pwsh -File ../../scripts/cleanup_temp_folders.ps1
```

---

## Fixture Path Issues

### Issue: "Degraded image not found" or "Fixture not found"

**Root Cause**: Relative paths broke after folder reorganization

**Solution**: Fixtures are now local to each test project (no relative paths)

Check that fixtures exist locally:
```bash
# Example for Tests.Infrastructure.Extraction.Teseract
ls Code/Src/CSharp/04-Tests/02-Infrastructure/Tests.Infrastructure.Extraction.Teseract/Fixtures/
```

If missing, re-run the copy script:
```powershell
pwsh -File scripts/copy_fixtures_locally.ps1
```

---

## Build Warnings

### Warning: "ocr_modules path not found"

**Symptom**:
```
warning : ocr_modules path not found at: F:\...\Code\Src\CSharp\Python\prisma-ocr-pipeline\src.
CSnakes code generation may fail. Use build-infrastructure.ps1 script or set PYTHONPATH manually.
```

**Impact**: Informational only - does not affect build success

**Solution**: This warning can be safely ignored unless you need the Python OCR pipeline

**To fix** (optional):
```powershell
# Build Python infrastructure if needed
.\build-infrastructure.ps1
```

---

## Test Discovery Issues

### Issue: Tests still discovered despite Skip attribute

**Symptom**: GotOcr2 tests with `[Fact(Skip = "...")]` still execute

**Root Cause**: `IsTestProject=false` in .csproj doesn't prevent xUnit discovery

**Solution**: Use `Assert.Skip()` at the start of each test method:
```csharp
[Fact]
public void TestMethod()
{
    Assert.Skip("Slow test (~140s). Enable manually for testing.");
    // ... rest of test
}
```

---

## Git Long Path Issues (Windows)

### Issue: "Filename too long" when committing fixtures

**Symptom**:
```
error: open("...very/long/path/file.pdf"): Filename too long
fatal: adding files failed
```

**Solution**:
```bash
# Enable long paths in git (one-time setup)
git config core.longpaths true

# Then retry
git add -A
git commit -m "message"
```

---

## After Folder Reorganization Checklist

After any major folder reorganization, follow these steps:

1. **Clean all build artifacts**
   ```bash
   cd Code/Src/CSharp
   dotnet clean ExxerCube.Prisma.sln
   rm -rf bin obj
   ```

2. **Clean temp folders**
   ```powershell
   pwsh -File scripts/cleanup_temp_folders.ps1
   ```

3. **Restore packages**
   ```bash
   dotnet restore ExxerCube.Prisma.sln
   ```

4. **Rebuild**
   ```bash
   dotnet build ExxerCube.Prisma.sln
   ```

5. **Verify fixtures**
   - Check that each test project has its local Fixtures/ folder
   - If missing, run: `pwsh -File scripts/copy_fixtures_locally.ps1`

6. **Run tests**
   ```bash
   dotnet test ExxerCube.Prisma.sln
   ```

---

## Quick Fixes

### Full Clean and Rebuild
```bash
cd Code/Src/CSharp
dotnet clean
rm -rf bin obj TestResults .vs
dotnet restore
dotnet build
```

### Reset NuGet Cache
```bash
dotnet nuget locals all --clear
dotnet restore ExxerCube.Prisma.sln
```

### Regenerate Solution User Files
```bash
rm *.suo
rm -rf .vs
# Reopen solution in Visual Studio
```
