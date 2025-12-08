# Folder Structure Cleanup Plan

**Current State:** 50 directories in `Code/Src/CSharp/` - VERY MESSY
**Goal:** Clean, organized solution structure following .NET best practices

---

## Problems Identified

### 🗑️ **1. Temporary/Test Output Clutter (Should be in .gitignore)**

```
Code/Src/CSharp/
├── Results/                    ❌ DELETE (temp output)
├── temp_output/                ❌ DELETE (temp output)
├── test_causa_output/          ❌ DELETE (temp output)
├── test_output/                ❌ DELETE (temp output)
├── test_output2/               ❌ DELETE (temp output)
├── test_causa.txt              ❌ DELETE (temp file)
├── test_input.txt              ❌ DELETE (temp file)
├── test_input2.txt             ❌ DELETE (temp file)
├── test_output.log             ❌ DELETE (temp file)
├── TestResults/                ❌ DELETE (test results - should be gitignored)
├── bin/                        ❌ DELETE (build output - should be gitignored)
└── .vs/                        ❌ DELETE (Visual Studio temp)
```

**Action:** Delete all, add to `.gitignore`

---

### 📁 **2. Unclear/Duplicate Project Folders**

```
Code/Src/CSharp/
├── Testing/                    ❓ What is this? (vs Tests.*)
├── Testing.Infrastructure/     ❓ Duplicate of Tests.Infrastructure.*?
├── Tests/                      ❓ What is this? (vs Tests.*)
├── Python/                     ❓ What is this? (vs Infrastructure.Python.GotOcr2)
└── scripts/                    ⚠️  Should be at repo root, not in CSharp/
```

**Action:** Investigate contents, consolidate or delete

---

### 🏗️ **3. Projects Mixed with Test/Infra/App**

**Current (FLAT - 50 directories):**
```
Code/Src/CSharp/
├── Application
├── Domain
├── Infrastructure
├── Infrastructure.BrowserAutomation
├── Infrastructure.Classification
├── Infrastructure.Database
├── Infrastructure.Export
├── Infrastructure.Extraction
├── Infrastructure.FileStorage
├── Infrastructure.Imaging
├── Infrastructure.Metrics
├── Infrastructure.Python.GotOcr2
├── Tests.Application
├── Tests.Architecture
├── Tests.Domain
├── Tests.Domain.Interfaces
├── Tests.EndToEnd
├── Tests.Infrastructure.BrowserAutomation.E2E
├── Tests.Infrastructure.Classification
├── Tests.Infrastructure.Database
├── Tests.Infrastructure.Export
├── Tests.Infrastructure.Extraction
├── Tests.Infrastructure.Extraction.GotOcr2
├── Tests.Infrastructure.Extraction.Teseract
├── Tests.Infrastructure.FileStorage
├── Tests.Infrastructure.Imaging
├── Tests.Infrastructure.Metrics
├── Tests.Infrastructure.Python
├── Tests.Infrastructure.XmlExtraction
├── Tests.System
├── Tests.UI
├── Web.UI
└── ConsoleApp.GotOcr2Demo
```

---

## Proposed Clean Structure

### **Option 1: Organize by Type (Recommended for .NET)**

```
Code/Src/CSharp/
├── src/                              # Production code
│   ├── Core/                         # Core domain layer
│   │   ├── Domain/
│   │   └── Application/
│   │
│   ├── Infrastructure/               # Infrastructure implementations
│   │   ├── Infrastructure/           # Core infrastructure
│   │   ├── Infrastructure.BrowserAutomation/
│   │   ├── Infrastructure.Classification/
│   │   ├── Infrastructure.Database/
│   │   ├── Infrastructure.Export/
│   │   ├── Infrastructure.Extraction/
│   │   ├── Infrastructure.FileStorage/
│   │   ├── Infrastructure.Imaging/
│   │   ├── Infrastructure.Metrics/
│   │   └── Infrastructure.Python.GotOcr2/
│   │
│   └── Presentation/                 # UI/API/Console apps
│       ├── Web.UI/
│       └── ConsoleApp.GotOcr2Demo/
│
├── tests/                            # All tests
│   ├── Unit/                         # Unit tests
│   │   ├── Tests.Domain/
│   │   ├── Tests.Domain.Interfaces/
│   │   └── Tests.Application/
│   │
│   ├── Integration/                  # Integration tests
│   │   ├── Tests.Infrastructure.BrowserAutomation.E2E/
│   │   ├── Tests.Infrastructure.Classification/
│   │   ├── Tests.Infrastructure.Database/
│   │   ├── Tests.Infrastructure.Export/
│   │   ├── Tests.Infrastructure.Extraction/
│   │   ├── Tests.Infrastructure.Extraction.GotOcr2/
│   │   ├── Tests.Infrastructure.Extraction.Teseract/
│   │   ├── Tests.Infrastructure.FileStorage/
│   │   ├── Tests.Infrastructure.Imaging/
│   │   ├── Tests.Infrastructure.Metrics/
│   │   ├── Tests.Infrastructure.Python/
│   │   └── Tests.Infrastructure.XmlExtraction/
│   │
│   ├── System/                       # System integration tests
│   │   └── Tests.System/
│   │
│   ├── EndToEnd/                     # E2E tests
│   │   ├── Tests.EndToEnd/
│   │   └── Tests.UI/
│   │
│   └── Architecture/                 # Architecture tests
│       └── Tests.Architecture/
│
└── testing/                          # Test utilities/helpers
    ├── Testing.Abstractions/
    └── Testing.Infrastructure/
```

**Advantages:**
- ✅ Clear separation: src, tests, testing helpers
- ✅ Tests organized by type (Unit, Integration, System, E2E, Architecture)
- ✅ Easy to run all tests of a certain type
- ✅ Standard .NET solution structure
- ✅ IDE-friendly (Visual Studio, Rider)

---

### **Option 2: Keep Flat but Group (Simpler Migration)**

```
Code/Src/CSharp/
├── Core.Domain/
├── Core.Application/
│
├── Infrastructure/
├── Infrastructure.BrowserAutomation/
├── Infrastructure.Classification/
... (all infrastructure projects)
│
├── Presentation.Web.UI/
├── Presentation.ConsoleApp.GotOcr2Demo/
│
├── Tests.Unit.Domain/
├── Tests.Unit.Domain.Interfaces/
├── Tests.Unit.Application/
│
├── Tests.Integration.Infrastructure.*/
... (all integration tests)
│
├── Tests.System/
├── Tests.EndToEnd/
├── Tests.UI/
├── Tests.Architecture/
│
├── Testing.Abstractions/
└── Testing.Infrastructure/
```

**Advantages:**
- ✅ Easier migration (just rename, don't move)
- ✅ Clear naming convention
- ✅ Groups related projects
- ⚠️  Still somewhat flat (36 directories)

---

## Recommended Cleanup Steps

### **Phase 1: Delete Temp/Output Clutter (IMMEDIATE)**

```bash
cd Code/Src/CSharp

# Delete temp/output folders
rm -rf Results/
rm -rf temp_output/
rm -rf test_causa_output/
rm -rf test_output/
rm -rf test_output2/
rm -rf TestResults/
rm -rf bin/
rm -rf .vs/

# Delete temp files
rm -f test_causa.txt
rm -f test_input.txt
rm -f test_input2.txt
rm -f test_output.log

# Update .gitignore
echo "bin/" >> .gitignore
echo "obj/" >> .gitignore
echo "TestResults/" >> .gitignore
echo ".vs/" >> .gitignore
echo "**/test_output*" >> .gitignore
echo "**/temp_output*" >> .gitignore
echo "Results/" >> .gitignore
```

**Expected Result:** 50 → 37 directories

---

### **Phase 2: Investigate Unclear Folders**

**A. Check what's in Testing/ vs Testing.Infrastructure/ vs Tests.\*:**

```bash
# Check if Testing/ and Testing.Infrastructure/ are duplicates
ls -la Code/Src/CSharp/Testing/
ls -la Code/Src/CSharp/Testing.Infrastructure/

# If duplicates, delete and consolidate
```

**B. Check Python/ folder:**

```bash
ls -la Code/Src/CSharp/Python/

# If it's just GotOcr2 Python files, consolidate with Infrastructure.Python.GotOcr2
```

**C. Check Tests/ folder:**

```bash
ls -la Code/Src/CSharp/Tests/

# If empty or placeholder, delete
```

**D. Move scripts/ to repo root:**

```bash
# scripts should be at repo root, not in CSharp/
mv Code/Src/CSharp/scripts/ ./scripts/
```

---

### **Phase 3: Organize into src/ and tests/ (RECOMMENDED)**

**This is the BIG cleanup - do this after Phase 1 & 2.**

```bash
cd Code/Src/CSharp

# Create new structure
mkdir -p src/Core
mkdir -p src/Infrastructure
mkdir -p src/Presentation
mkdir -p tests/Unit
mkdir -p tests/Integration
mkdir -p tests/System
mkdir -p tests/EndToEnd
mkdir -p tests/Architecture
mkdir -p testing

# Move Core projects
mv Domain/ src/Core/
mv Application/ src/Core/

# Move Infrastructure projects
mv Infrastructure/ src/Infrastructure/
mv Infrastructure.*/ src/Infrastructure/

# Move Presentation projects
mv Web.UI/ src/Presentation/
mv ConsoleApp.GotOcr2Demo/ src/Presentation/

# Move Unit tests
mv Tests.Domain/ tests/Unit/
mv Tests.Domain.Interfaces/ tests/Unit/
mv Tests.Application/ tests/Unit/

# Move Integration tests
mv Tests.Infrastructure.*/ tests/Integration/

# Move System tests
mv Tests.System/ tests/System/

# Move E2E tests
mv Tests.EndToEnd/ tests/EndToEnd/
mv Tests.UI/ tests/EndToEnd/

# Move Architecture tests
mv Tests.Architecture/ tests/Architecture/

# Move Testing utilities
mv Testing/ testing/Abstractions/
mv Testing.Infrastructure/ testing/Infrastructure/
```

**Expected Result:** 37 → 6 top-level folders (src, tests, testing, + maybe .github)

---

### **Phase 4: Update Project References**

After moving folders, you'll need to update `.csproj` files' `<ProjectReference>` paths:

```xml
<!-- Before -->
<ProjectReference Include="..\Domain\ExxerCube.Prisma.Domain.csproj" />

<!-- After -->
<ProjectReference Include="..\..\src\Core\Domain\ExxerCube.Prisma.Domain.csproj" />
```

**Tool to help:** Visual Studio can automatically update references when you move projects in Solution Explorer.

---

## Migration Risk Assessment

| Phase | Risk | Time | Impact |
|-------|------|------|--------|
| **Phase 1: Delete temp** | 🟢 LOW | 5 min | None (just clutter removal) |
| **Phase 2: Investigate** | 🟡 MEDIUM | 30 min | Need to understand folder purposes |
| **Phase 3: Reorganize** | 🔴 HIGH | 2-4 hours | ALL project references need updating |
| **Phase 4: Update refs** | 🔴 HIGH | 1-2 hours | Build will break until fixed |

---

## Recommended Approach

1. **Start with Phase 1** (delete temp clutter) - **SAFE, IMMEDIATE BENEFIT**
2. **Do Phase 2** (investigate unclear folders) - **LOW RISK**
3. **Decide on Phase 3** (full reorganization) - **DO LATER** after discussing with team

**Why Phase 3 is risky:**
- All project references need updating
- Solution file needs updating (if you have one)
- CI/CD paths may need updating
- Team members need to sync
- Git history becomes harder to trace (file moves)

**Alternative to Phase 3:**
- Use **Option 2 (rename in place)** instead - much safer, same benefit

---

## Immediate Action (Low Risk)

Let me create a script for Phase 1 (delete temp clutter):

```bash
#!/bin/bash
# cleanup_temp_folders.sh

cd Code/Src/CSharp

echo "=== Cleaning up temporary folders and files ==="
echo ""

# Folders to delete
TEMP_FOLDERS=(
  "Results"
  "temp_output"
  "test_causa_output"
  "test_output"
  "test_output2"
  "TestResults"
  "bin"
  ".vs"
)

# Files to delete
TEMP_FILES=(
  "test_causa.txt"
  "test_input.txt"
  "test_input2.txt"
  "test_output.log"
)

# Delete folders
for folder in "${TEMP_FOLDERS[@]}"; do
  if [ -d "$folder" ]; then
    echo "Deleting folder: $folder"
    rm -rf "$folder"
  else
    echo "Folder not found (already deleted?): $folder"
  fi
done

echo ""

# Delete files
for file in "${TEMP_FILES[@]}"; do
  if [ -f "$file" ]; then
    echo "Deleting file: $file"
    rm -f "$file"
  else
    echo "File not found (already deleted?): $file"
  fi
done

echo ""
echo "✅ Cleanup complete!"
echo ""
echo "To prevent these from coming back, add to .gitignore:"
echo "  bin/"
echo "  obj/"
echo "  TestResults/"
echo "  .vs/"
echo "  **/test_output*"
echo "  **/temp_output*"
echo "  Results/"
```

---

## Summary

**What we have now:** 50 directories - MESSY
**After Phase 1:** 37 directories - CLEANER
**After Phase 3:** 6 directories - CLEAN (but risky)

**My recommendation:**
1. ✅ **DO NOW:** Phase 1 (delete temp clutter)
2. ✅ **DO SOON:** Phase 2 (investigate unclear folders)
3. ⚠️  **DO LATER:** Phase 3 (full reorganization) - discuss with team first

Want me to create the cleanup scripts?
