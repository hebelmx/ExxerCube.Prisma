# Prisma Code Quality Audit Workflow

## Overview

This directory contains **two complementary approaches** for detecting and fixing ITTDD technical debt in the Prisma codebase:

1. **Architecture Tests** (C#) - Fast, automated guards that run on every build
2. **Python Audit Tools** - Comprehensive static analysis and systematic fixing workflow

---

## 🏗️ Architecture Tests (Automated Guards)

### Location
```
Prisma/Code/Src/CSharp/Tests.Architecture/HexagonalArchitectureTests.cs
```

### What They Do
- Run automatically with `dotnet test` or `dotnet build`
- Fail the build if technical debt is detected
- Enforce Hexagonal Architecture (Ports & Adapters) constraints

### Key Tests
1. **`All_Domain_Interfaces_Should_Have_At_Least_One_Implementation`**
   - Detects interfaces created via ITTDD but never implemented
   - Found 3 unimplemented interfaces in Prisma:
     - `IProcessingContext`
     - `IProcessingMetricsService`
     - `ISpecification<T>`

2. **`No_Stub_Implementations_Should_Exist`**
   - Detects placeholder implementations (NotImplementedException, empty methods)
   - Found 2 stub implementations in Prisma:
     - `FileSystemLoader.GetSupportedExtensions` (12 bytes IL)
     - `OcrProcessingServiceAdapter.ProcessDocumentAsync` (15 bytes IL)

### How to Run
```bash
# Run all architecture tests
dotnet test Tests.Architecture

# Run specific ITTDD tests
dotnet test --filter "FullyQualifiedName~All_Domain_Interfaces_Should_Have_At_Least_One_Implementation"
dotnet test --filter "FullyQualifiedName~No_Stub_Implementations_Should_Exist"
```

### Advantages
- **Fast**: < 1 second execution
- **CI/CD Integration**: Runs on every build
- **Prevention**: Stops new debt from being committed

### Limitations
- Simple detection (IL byte count for stubs)
- No detailed guidance or audit templates
- No prioritization or batch processing

---

## 🐍 Python Audit Tools (Comprehensive Analysis)

### Files
```
suspicious_code_detector.py        # Step 1: Scan codebase for patterns
generate_audit_batches.py          # Step 2: Generate organized audit templates
```

### What They Do
- Deep static analysis of C# code
- Detect 30+ suspicious patterns across 3 severity levels
- Generate prioritized audit batches with comprehensive templates

### Detected Patterns

#### 🔴 HIGH Severity (Immediate Action)
- `not_implemented_exception` - NotImplementedException thrown
- `not_supported_exception` - NotSupportedException thrown
- `stub_class_name` - Class name contains "Stub"
- `mock_class_name` - Class name contains "Mock"
- `fake_class_name` - Class name contains "Fake"
- `mock_extracted_text` - Hardcoded mock data
- `placeholder_interface` - Placeholder comments in interfaces

#### 🟡 MEDIUM Severity (Should Address Soon)
- `todo_comment` - TODO/FIXME/HACK comments
- `placeholder_comment` - Placeholder/temporary comments
- `mock_in_comment` - Mock/stub comments
- `generic_exception_catch` - catch (Exception ex)
- `hardcoded_localhost` - Hardcoded localhost/127.0.0.1
- `hardcoded_credentials` - Hardcoded passwords/keys
- `hardcoded_timeout` - Hardcoded TimeSpan values
- `magic_numbers` - Magic numbers (100, 1000, etc.)
- `task_from_result` - Task.FromResult usage

#### 💡 LOW Severity (Technical Debt)
- `test_skip_attribute` - Skipped tests
- `empty_method` - Empty method bodies
- `static_return_value` - Static return values
- `return_null` - Returning null
- `task_completed_task` - Task.CompletedTask usage

---

## 📋 Complete Workflow

### Step 1: Run Suspicious Code Detector

```bash
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\scripts

# Full scan (all 335 C# files)
python suspicious_code_detector.py

# Output: suspicious_code_analysis.json
```

**What it does:**
- Scans all .cs files in `Prisma/Code/Src/CSharp`
- Detects 30+ suspicious patterns
- Categorizes by severity (HIGH/MEDIUM/LOW)
- Generates JSON report with all findings

**Expected output:**
```
🎯 Starting Prisma Suspicious Code Analysis
📊 Found 335 C# files to analyze
🚀 Starting analysis...

✅ Analysis Complete!
📊 Final Statistics:
   📁 Files Processed: 335
   🎯 Total Findings: ~500-1000 (depends on codebase state)
   🚨 HIGH Severity: X
   ⚠️  MEDIUM Severity: Y
   💡 LOW Severity: Z

💾 Report saved to: suspicious_code_analysis.json
```

---

### Step 2: Generate Audit Batches

```bash
# Full batch generation
python generate_audit_batches.py

# Quick mode (first 100 items, for testing)
python generate_audit_batches.py --quick
```

**What it does:**
- Reads `suspicious_code_analysis.json`
- Organizes findings by:
  - **Priority**: P1_CRITICAL → P2_IMPORTANT → P3_MAINTENANCE
  - **Namespace**: Groups related issues together
- Generates comprehensive audit templates (markdown)
- Creates batch summaries and master index

**Output structure:**
```
scripts/audits/audit_batches_20251124_153000/
├── P1_CRITICAL/                    # HIGH severity issues
│   ├── B001_Domain_Interfaces_1/
│   │   ├── BATCH_SUMMARY.md
│   │   ├── audit_01_IProcessingContext_...md
│   │   ├── audit_01_IProcessingContext_...metadata.json
│   │   └── ...
│   └── B002_Infrastructure_FileSystem_1/
│       └── ...
├── P2_IMPORTANT/                   # MEDIUM severity issues
│   ├── B003_Application_Services_1/
│   └── ...
├── P3_MAINTENANCE/                 # LOW severity issues
│   └── ...
└── MASTER_INDEX.md                 # Start here!
```

---

### Step 3: Review Audit Templates

Each audit template contains:

1. **📋 Issue Metadata**
   - Severity, project, namespace, class, method, line number
   - Pattern type and category
   - File path and test/production indicator

2. **🔍 Code Context**
   - Line preview
   - File path for navigation

3. **🎯 Pattern Analysis**
   - Description of the issue
   - ITTDD impact analysis
   - Recommended actions
   - Severity-specific checklist

4. **🏗️ Hexagonal Architecture Alignment**
   - Layer identification (Domain/Application/Infrastructure)
   - Architectural considerations
   - Dependency flow validation

5. **🛠️ Fix Template**
   - Current code
   - Proposed fix (to be filled in)
   - Fix reasoning documentation

6. **✅ Audit Checklist**
   - Investigation phase tasks
   - Implementation phase tasks
   - Verification phase tasks

7. **✍️ Audit Results**
   - Final decision tracking
   - Auditor notes
   - Follow-up tasks

---

### Step 4: Systematic Fixing

**Priority order:**
1. Start with **P1_CRITICAL** batches
2. Move to **P2_IMPORTANT** batches
3. Clean up **P3_MAINTENANCE** batches

**For each issue:**
1. Open the audit template markdown file
2. Read the code context and analysis
3. Navigate to the file:line in your IDE
4. Review ±10 lines of surrounding code
5. Determine if:
   - ✅ **Fix Implemented**: Complete the implementation
   - 📋 **Fix Planned**: Create tracking ticket
   - 📝 **Documented as Intentional**: Justify why it's correct
   - 🗑️ **Marked for Removal**: Delete unused code
   - 🔄 **Needs Further Analysis**: Escalate for discussion

6. Fill in the audit results section
7. **Run architecture tests after each fix**:
   ```bash
   dotnet test Tests.Architecture
   ```

---

### Step 5: Verify with Architecture Tests

After fixing issues, verify compliance:

```bash
# Run all architecture tests
cd Prisma/Code/Src/CSharp
dotnet test Tests.Architecture

# Run specific ITTDD tests
dotnet test Tests.Architecture --filter "FullyQualifiedName~HexagonalArchitectureTests"
```

**All tests should pass** before considering a batch complete.

---

## 🎯 Current State (Initial Run)

Based on the architecture tests that were just added:

### Unimplemented Interfaces (3)
1. **`ExxerCube.Prisma.Domain.Interfaces.IProcessingContext`**
   - Action: Implement in Infrastructure or remove if unused

2. **`ExxerCube.Prisma.Domain.Interfaces.IProcessingMetricsService`**
   - Action: Implement in Infrastructure.Metrics or remove

3. **`ExxerCube.Prisma.Domain.Interfaces.Contracts.ISpecification<T>`**
   - Action: Implement specification pattern or remove

### Stub Implementations (2)
1. **`Infrastructure.FileSystem.FileSystemLoader.GetSupportedExtensions`** (12 bytes IL)
   - Action: Implement or return Result<T>.Failure()

2. **`Infrastructure.DependencyInjection.OcrProcessingServiceAdapter.ProcessDocumentAsync`** (15 bytes IL)
   - Action: Implement or return Result<T>.Failure()

---

## 📊 Integration with CI/CD

### Recommended Setup

1. **Architecture Tests** run on every build:
   ```yaml
   # .github/workflows/build.yml
   - name: Run Architecture Tests
     run: dotnet test Tests.Architecture
   ```

2. **Python Audit Tools** run weekly via scheduled job:
   ```yaml
   # .github/workflows/weekly-audit.yml
   schedule:
     - cron: '0 0 * * 0'  # Every Sunday
   steps:
     - name: Run Suspicious Code Detector
       run: python scripts/suspicious_code_detector.py
     - name: Generate Audit Batches
       run: python scripts/generate_audit_batches.py
     - name: Upload Audit Reports
       uses: actions/upload-artifact@v3
       with:
         name: audit-reports
         path: scripts/audits/
   ```

---

## 🛠️ Maintenance

### Adding New Pattern Detection

Edit `suspicious_code_detector.py`:

```python
patterns = {
    # Add your new pattern
    "my_new_pattern": r"regex_pattern_here",
}

pattern_severity = {
    "my_new_pattern": "HIGH",  # or MEDIUM/LOW
}
```

### Customizing Audit Templates

Edit `generate_audit_batches.py`:

```python
def get_pattern_guidance(self, pattern, category, severity):
    # Add custom guidance for your pattern
    if pattern == "my_new_pattern":
        return {
            "description": "...",
            "ittdd_impact": "...",
            "actions": "...",
            "checklist": "..."
        }
```

---

## 📚 References

- **Hexagonal Architecture**: https://alistair.cockburn.us/hexagonal-architecture/
- **ITTDD**: Interface-Test-Driven Development (test interfaces before implementations)
- **Result<T> Pattern**: Railway-Oriented Programming for error handling
- **NetArchTest**: https://github.com/BenMorris/NetArchTest

---

## 🎓 Training Resources

### For New Team Members

1. Read this README
2. Run architecture tests to see current state
3. Run Python detector on a single file to understand patterns
4. Review 2-3 audit templates to understand the format
5. Fix 1-2 LOW severity issues to practice the workflow
6. Attend code review session for HIGH severity fixes

### For Code Reviewers

- Ensure all audit templates are filled out completely
- Verify architecture tests pass after fixes
- Check that fixes align with Hexagonal Architecture principles
- Validate ITTDD cycle completion (interface + implementation + tests)

---

**Generated:** 2025-11-24
**Version:** 1.0
**Maintained by:** Code Quality Team
