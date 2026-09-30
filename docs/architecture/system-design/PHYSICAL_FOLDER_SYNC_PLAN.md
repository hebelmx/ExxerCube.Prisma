# Physical Folder Sync Plan

**Goal:** Make physical folder structure match your clean Visual Studio solution structure

---

## Your Visual Studio Structure (From Screenshots)

```
ExxerCube.Prisma (38 of 38 projects)
│
├── 📁 00 Solution Items
│   ├── .editorconfig
│   ├── Directory.Build.props
│   └── Directory.Packages.props
│
├── 📁 01 Core
│   ├── 📦 ExxerCube.Prisma.Application
│   └── 📦 ExxerCube.Prisma.Domain
│
├── 📁 02 Infrastructure
│   ├── 📦 ExxerCube.Prisma.Infrastructure
│   ├── 📦 ExxerCube.Prisma.Infrastructure.BrowserAutomation
│   ├── 📦 ExxerCube.Prisma.Infrastructure.Classification
│   ├── 📦 ExxerCube.Prisma.Infrastructure.Database
│   ├── 📦 ExxerCube.Prisma.Infrastructure.Export
│   ├── 📦 ExxerCube.Prisma.Infrastructure.Extraction
│   ├── 📦 ExxerCube.Prisma.Infrastructure.FileStorage
│   ├── 📦 ExxerCube.Prisma.Infrastructure.Imaging
│   ├── 📦 ExxerCube.Prisma.Infrastructure.Metrics
│   └── 📦 ExxerCube.Prisma.Infrastructure.Python.GotOcr2
│
├── 📁 03 UI
│   └── 📦 ExxerCube.Prisma.Web.UI
│
├── 📁 04 Tests
│   ├── 📁 01 Core
│   │   ├── 📦 ExxerCube.Prisma.Tests.Application
│   │   ├── 📦 ExxerCube.Prisma.Tests.Domain
│   │   └── 📦 ExxerCube.Prisma.Tests.Domain.Interfaces
│   │
│   ├── 📁 02 Infrastructure
│   │   ├── 📦 ExxerCube.Prisma.Tests.Infrastructure.Classification
│   │   ├── 📦 ExxerCube.Prisma.Tests.Infrastructure.Database
│   │   ├── 📦 ExxerCube.Prisma.Tests.Infrastructure.Export
│   │   ├── 📦 ExxerCube.Prisma.Tests.Infrastructure.Extraction
│   │   ├── 📦 ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2
│   │   ├── 📦 ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract
│   │   ├── 📦 ExxerCube.Prisma.Tests.Infrastructure.FileStorage
│   │   ├── 📦 ExxerCube.Prisma.Tests.Infrastructure.FileSystem
│   │   ├── 📦 ExxerCube.Prisma.Tests.Infrastructure.Imaging
│   │   ├── 📦 ExxerCube.Prisma.Tests.Infrastructure.Metrics
│   │   └── 📦 ExxerCube.Prisma.Tests.Infrastructure.Python
│   │
│   ├── 📁 03 System
│   │   ├── 📦 ExxerCube.Prisma.Tests.System.BrowserAutomation.E2E
│   │   ├── 📦 ExxerCube.Prisma.Tests.System.Ocr.Pipeline
│   │   └── 📦 ExxerCube.Prisma.Tests.System.XmlExtraction
│   │
│   ├── 📁 04 UI
│   │   └── 📦 ExxerCube.Prisma.Tests.UI
│   │
│   ├── 📁 05 E2E
│   │   └── 📦 ExxerCube.Prisma.Tests.EndToEnd
│   │
│   ├── 📁 06 Architecture
│   │   └── 📦 ExxerCube.Prisma.Tests.Architecture
│   │
│   └── 📁 Tests.Infrastructure.BrowserAutomation (orphaned folder?)
│
├── 📁 05 ConsoleApp.GotOcr2Demo
│   └── 📦 ExxerCube.Prisma.ConsoleApp.GotOcr2Demo
│
└── 📁 05 Testing
    ├── 📁 01 Abstractions
    │   └── 📦 ExxerCube.Prisma.Testing.Abstractions
    │
    ├── 📁 02 Contracts
    │   └── 📦 ExxerCube.Prisma.Testing.Contracts
    │
    ├── 📁 03 Infrastructure
    │   └── 📦 ExxerCube.Prisma.Testing.Infrastructure
    │
    └── 📁 04 (empty or has Python?)
```

---

## Physical Folder Mapping

### **Currently (FLAT - 50 directories):**

```
Code/Src/CSharp/
├── Application                          → Should be in: 01-Core/
├── Domain                               → Should be in: 01-Core/
├── Infrastructure                       → Should be in: 02-Infrastructure/
├── Infrastructure.BrowserAutomation     → Should be in: 02-Infrastructure/
├── Infrastructure.Classification        → Should be in: 02-Infrastructure/
├── Infrastructure.Database              → Should be in: 02-Infrastructure/
├── Infrastructure.Export                → Should be in: 02-Infrastructure/
├── Infrastructure.Extraction            → Should be in: 02-Infrastructure/
├── Infrastructure.FileStorage           → Should be in: 02-Infrastructure/
├── Infrastructure.Imaging               → Should be in: 02-Infrastructure/
├── Infrastructure.Metrics               → Should be in: 02-Infrastructure/
├── Infrastructure.Python.GotOcr2        → Should be in: 02-Infrastructure/
├── Web.UI                               → Should be in: 03-UI/
├── Tests.Application                    → Should be in: 04-Tests/01-Core/
├── Tests.Domain                         → Should be in: 04-Tests/01-Core/
├── Tests.Domain.Interfaces              → Should be in: 04-Tests/01-Core/
├── Tests.Infrastructure.Classification  → Should be in: 04-Tests/02-Infrastructure/
├── Tests.Infrastructure.Database        → Should be in: 04-Tests/02-Infrastructure/
├── Tests.Infrastructure.Export          → Should be in: 04-Tests/02-Infrastructure/
├── Tests.Infrastructure.Extraction      → Should be in: 04-Tests/02-Infrastructure/
├── Tests.Infrastructure.Extraction.GotOcr2 → Should be in: 04-Tests/02-Infrastructure/
├── Tests.Infrastructure.Extraction.Teseract → Should be in: 04-Tests/02-Infrastructure/
├── Tests.Infrastructure.FileStorage     → Should be in: 04-Tests/02-Infrastructure/
├── Tests.Infrastructure.FileSystem      → Should be in: 04-Tests/02-Infrastructure/
├── Tests.Infrastructure.Imaging         → Should be in: 04-Tests/02-Infrastructure/
├── Tests.Infrastructure.Metrics         → Should be in: 04-Tests/02-Infrastructure/
├── Tests.Infrastructure.Python          → Should be in: 04-Tests/02-Infrastructure/
├── Tests.Infrastructure.XmlExtraction   → Should be in: 04-Tests/03-System/ (as Tests.System.XmlExtraction)
├── Tests.Infrastructure.BrowserAutomation.E2E → Should be in: 04-Tests/03-System/ (as Tests.System.BrowserAutomation.E2E)
├── Tests.System                         → Should be in: 04-Tests/03-System/ (as Tests.System.Ocr.Pipeline)
├── Tests.UI                             → Should be in: 04-Tests/04-UI/
├── Tests.EndToEnd                       → Should be in: 04-Tests/05-E2E/
├── Tests.Architecture                   → Should be in: 04-Tests/06-Architecture/
├── ConsoleApp.GotOcr2Demo               → Should be in: 05-ConsoleApp.GotOcr2Demo/
├── Testing/                             → Should be in: 05-Testing/01-Abstractions/ (as Testing.Abstractions)
├── Testing.Infrastructure               → Should be in: 05-Testing/03-Infrastructure/
├── Testing/Contracts                    → Should be in: 05-Testing/02-Contracts/ (as Testing.Contracts)
├── Testing/Python                       → Should be in: 05-Testing/04/?
└── + 13 temp/output folders to DELETE
```

---

## Action Plan

### **Option 1: Simple Rename (Safest)**

Just rename the top-level folders to match VS numbering:

```bash
# Rename folders to match VS numbering
mv Application "01-Core"
mv Infrastructure "02-Infrastructure"
mv Web.UI "03-UI"
# etc.
```

**Pros:** Minimal changes, easy to revert
**Cons:** Still somewhat flat (7 top-level folders instead of 50)

---

### **Option 2: Full Reorganization (Cleanest)**

Create nested folder structure to EXACTLY match VS:

```
Code/Src/CSharp/
├── 01-Core/
│   ├── Application/
│   └── Domain/
├── 02-Infrastructure/
│   ├── Infrastructure/
│   ├── Infrastructure.BrowserAutomation/
│   └── ... (10 projects)
├── 03-UI/
│   └── Web.UI/
├── 04-Tests/
│   ├── 01-Core/
│   │   ├── Tests.Application/
│   │   ├── Tests.Domain/
│   │   └── Tests.Domain.Interfaces/
│   ├── 02-Infrastructure/
│   │   └── ... (11 projects)
│   ├── 03-System/
│   │   └── ... (3 projects)
│   ├── 04-UI/
│   ├── 05-E2E/
│   └── 06-Architecture/
├── 05-ConsoleApp.GotOcr2Demo/
│   └── ConsoleApp.GotOcr2Demo/
└── 05-Testing/
    ├── 01-Abstractions/
    ├── 02-Contracts/
    ├── 03-Infrastructure/
    └── 04/
```

**Pros:** EXACTLY matches VS, very clean
**Cons:** ALL project references need updating (risky)

---

### **Option 3: Hybrid (RECOMMENDED)**

1. **First:** Delete temp clutter (13 folders) - **SAFE**
2. **Then:** Create top-level folders matching VS, move projects in - **MODERATE RISK**
3. **Finally:** Let Visual Studio automatically fix project references

---

## Recommended Steps

### **Step 1: Backup**
```bash
# Create a git commit or branch first!
git add .
git commit -m "Before folder reorganization"
git branch backup-before-reorg
```

### **Step 2: Clean Temp**
```powershell
.\scripts\cleanup_temp_folders.ps1 -DryRun
# Review, then:
.\scripts\cleanup_temp_folders.ps1
```

### **Step 3: Run Sync Script**
```powershell
.\scripts\sync_folders_to_vs_structure.ps1 -DryRun
# Review carefully, then:
.\scripts\sync_folders_to_vs_structure.ps1
```

### **Step 4: Fix References in Visual Studio**
1. Open solution in VS
2. Some projects will show as "unavailable"
3. Right-click unavailable projects → Remove
4. Right-click solution folder → Add → Existing Project
5. Navigate to new location and add project back
6. OR: Edit .sln file to update paths

---

## Questions to Answer First

1. **Do you want physical folders to EXACTLY match VS structure?**
   - If YES → Use Option 2 (full reorganization)
   - If NO → Keep flat, just clean temp folders

2. **Are you comfortable updating project references?**
   - If YES → We can reorganize
   - If NO → Just clean temp folders for now

3. **Is the solution file manually edited or generated?**
   - Manual → We need to update it carefully
   - Generated → Can regenerate after moving

---

## My Recommendation

**For now (while tests are running):**
1. Just clean temp clutter (safe, quick win)
2. Decide on full reorganization later
3. Your VS structure is already good - physical folders are just cosmetic

**Later (if you want clean physical structure):**
1. Create backup/branch
2. Run full reorganization
3. Let Visual Studio resync the solution
4. Test that everything compiles

**Want me to wait for your test results first, then we decide on the approach?**
