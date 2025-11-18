# Scripts Directory - Active Automation Tools

## Overview

This directory contains **33 active Python automation scripts** for ExxerAI development workflow. These scripts handle testing, analysis, dependency management, and code quality tasks.

**Archival Status**: Consolidated from 74 scripts to 33 (55% reduction). 43 obsolete scripts archived to `archive/` subdirectories.

---

## 📋 Script Categories

### 🧪 Test Management (11 scripts)

**add_test_timeouts.py**
- Adds timeout attributes to test methods to prevent hanging tests
- Ensures all tests complete within 30 seconds
- Usage: `python scripts/add_test_timeouts.py --base-path "F:/Dynamic/ExxerAi/ExxerAI"`

**apply_timeouts.py**
- Applies timeout configurations to test projects
- Batch processing for multiple test files
- Usage: `python scripts/apply_timeouts.py --project-path "code/src/tests"`

**enhanced_test_fixer.py**
- Comprehensive test modernization (XUnit v3, null safety, cancellation tokens)
- Fixes common test patterns and anti-patterns
- Usage: `python scripts/enhanced_test_fixer.py --test-file "path/to/Tests.cs"`

**test_method_duplicate_analyzer_fast.py**
- Fast detection of duplicate test methods across projects
- Identifies copy-paste test anti-patterns
- Usage: `python scripts/test_method_duplicate_analyzer_fast.py`

**summarize_test_folders_05_plus.py**
- Generates test coverage summaries for integration/system test layers
- Reports test counts, coverage, and gaps
- Usage: `python scripts/summarize_test_folders_05_plus.py`

**test_without_warnings_as_errors.py**
- Temporarily disables warnings-as-errors for test debugging
- Use sparingly - warnings should be fixed, not suppressed
- Usage: `python scripts/test_without_warnings_as_errors.py --project "ExxerAI.Tests.csproj"`

**relocate_test_smart.py** ⭐ **ESSENTIAL**
- Smart test file relocation with automatic dependency resolution
- Updates namespaces and GlobalUsings.cs automatically
- Requires: `enhanced_dependency_analysis.json`
- Usage: `python scripts/relocate_test_smart.py --file "path/to/Tests.cs" --destination "05IntegrationTests/ExxerAI.Component.Integration.Tests" --namespace "ExxerAI.Component.Integration.Tests"`

**format_all_projects.py**
- Applies consistent formatting across all project files
- Uses dotnet format under the hood
- Usage: `python scripts/format_all_projects.py`

**analyze_test_coverage.py** ⭐ **NEW - TEST MIGRATION ANALYSIS**
- Analyzes test coverage between monolithic and split test projects
- Identifies missing tests (not yet migrated) and new tests (in split projects)
- Compares test methods by filename and method names (handles file relocation)
- Excludes Python interop tests (deprecated feature)
- Output: `test_coverage_analysis.json` with detailed comparison
- Usage: `python scripts/analyze_test_coverage.py --base-path "F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma"`
- **Updated**: 2025-01-15 - Added multiline test method pattern matching, filename normalization, Python interop exclusion

**count_test_methods.py** ⭐ **NEW - TEST COUNTING**
- Counts test methods across all test projects
- Provides detailed breakdown by project and file
- Handles both `[Fact]` and `[Theory]` attributes
- Supports multiline attribute patterns
- Excludes Python interop tests (deprecated feature)
- Output: `test_method_counts.json` with per-project statistics
- Usage: `python scripts/count_test_methods.py --base-path "F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma"`
- **Updated**: 2025-01-15 - Added multiline test method pattern matching, Python interop exclusion

**show_missing_tests.py** ⭐ **NEW - MISSING TESTS DISPLAY**
- Displays missing and new tests from coverage analysis in readable format
- Parses `test_coverage_analysis.json` and presents information clearly
- Shows original paths, split paths, and method names
- Usage: `python scripts/show_missing_tests.py`
- **Created**: 2025-01-15 - Helper script for test migration analysis

**find_duplicate_tests.py**
- Finds duplicate test files across split test projects
- Identifies tests that may have been copied to multiple projects
- Usage: `python scripts/find_duplicate_tests.py`

---

### 📁 Test Fixture Management (2 scripts)

**sample_kpi_fixtures.py** ⭐ **ESSENTIAL - 1-SIGMA DISTRIBUTION SAMPLER**
- Intelligently samples test fixtures from KpiExxerpro/Fixture using normal distribution (±1σ)
- Multi-format support: PDF, PNG, JPG, JPEG, TIFF
- Flexible CLI: configurable count, size limit, source/target directories
- Statistical approach: selects files within 1 standard deviation of mean (removes outliers)
- Default: 40 files, ~20MB total
- Output: Sequential naming (`helix_001.pdf`, `helix_002.png`, etc.)
- Usage: `python scripts/sample_kpi_fixtures.py --target-count 50 --target-size-mb 4 --dry-run`
- Best for: Helix OCR tests, Datastream integration tests, general document testing

**select_pdf_fixtures_sigmoid.py** - SIGMOID DISTRIBUTION SAMPLER
- PDF-only sampler using inverse sigmoid function for natural distribution
- Size categorization: tiny (10%), small (25%), medium (35%), large (20%), xlarge (10%)
- Fixed target: 100 PDFs
- Output: Metadata-rich naming (`test_001_medium_524288.pdf`) + JSON metadata file
- Usage: `python scripts/select_pdf_fixtures_sigmoid.py` (hardcoded paths)
- Best for: PdfPig adapter tests, size-stratified testing

**📚 Documentation**: See `README_FIXTURE_SAMPLING.md` for complete guide, statistical background, and comparison matrix.

---

### 🔍 Dependency & Type Analysis (10 scripts)

**analyze_dependencies_smart_v2.py** ⭐ **ESSENTIAL**
- Latest version of dependency analyzer
- Creates `enhanced_dependency_analysis.json` (978KB type-to-namespace lookup)
- Required by: relocate_test_smart.py, fix_dependencies_smart_v2.py
- Usage: `python scripts/analyze_dependencies_smart_v2.py --base-path "F:/Dynamic/ExxerAi/ExxerAI" --errors "Errors/CS0246.txt"`

**scan_exxerai_types.py** ⭐ **ESSENTIAL - PRODUCTION CODE ANALYZER**
- Scans entire codebase for all C# types (classes, interfaces, enums, records, delegates)
- Generates comprehensive production code metadata with project/namespace/file locations
- Output: `exxerai_types_YYYYMMDD.json` with complete type inventory
- Scans 3,100+ files, 3,200+ types across 86 projects in seconds
- Required by: cross_reference_sut.py for architectural alignment analysis
- Usage: `python scripts/scan_exxerai_types.py --base-path "F:/Dynamic/ExxerAi/ExxerAI" --output "exxerai_types.json"`

**build_dependency_tree.py**
- Builds a lightweight dependency tree using namespace declarations and `using` directives
- Output: `scripts/type_dependency_tree.json` (post-commit hook keeps latest + timestamped backups)
- Future architecture rules can consume this to enforce layer boundaries without reflection
- Usage: `python scripts/build_dependency_tree.py --base-path "F:/Dynamic/ExxerAi/ExxerAI"`

**scan_test_sut.py** ⭐ **ESSENTIAL - TEST CODE ANALYZER**
- Advanced test analyzer that identifies Systems Under Test (SUTs)
- Detects mock frameworks (NSubstitute, Moq, FakeItEasy) and extracts mock targets
- Infers SUT types from test class names, instantiation patterns, and field declarations
- Analyzes test organization (folders, concerns, namespaces)
- Three modes: `basic` (fast), `advanced` (default), `very_advanced` (prepared)
- Output: `test_sut_analysis.json` with test-to-SUT mappings and project analysis
- Required by: cross_reference_sut.py for test coverage analysis
- Usage: `python scripts/scan_test_sut.py --base-path "F:/Dynamic/ExxerAi/ExxerAI" --output "test_sut_analysis.json" --mode advanced`

**cross_reference_sut.py** ⭐ **ESSENTIAL - ARCHITECTURAL ALIGNMENT ANALYZER**
- Cross-references production code with test code using in-memory hash tables (O(1) lookups)
- Detects architectural alignment: **perfect** (1:1), **mixed** (many:many), **orphaned** (no production code)
- Generates split recommendations for mixed-concern test projects
- Analyzes test coverage gaps and architectural violations
- Uses deterministic, data-driven analysis to inform refactoring decisions
- Output: `cross_reference_report.json` with alignment analysis and split suggestions
- Required for: Test project split decisions (e.g., ADR-014 Nexus Adapter split)
- Usage: `python scripts/cross_reference_sut.py --production "exxerai_types.json" --tests "test_sut_analysis.json" --output "cross_reference_report.json" --focus "ExxerAI.Nexus.Adapter.Tests"`

**analyze_foreign_types.py**
- Identifies external package types vs. ExxerAI internal types
- Helps determine GlobalUsings.cs additions
- Usage: `python scripts/analyze_foreign_types.py`

**analyze_with_type_dictionary.py**
- Uses curated type dictionary for dependency resolution
- Maps System.*, Microsoft.*, and package types to namespaces
- Usage: `python scripts/analyze_with_type_dictionary.py`

**detect_duplicate_type_names_smart.py**
- Smart detection of duplicate type names across projects
- Recommends namespace prefixing or renaming
- Usage: `python scripts/detect_duplicate_type_names_smart.py`

**search_type_fuzzy.py** ⭐ **NEW - INTELLIGENT TYPE SEARCH**
- Fuzzy search for types using Levenshtein distance (typo tolerance)
- Find types without exact name knowledge
- Returns type metadata: name, project, namespace, file location
- Auto-detects latest type database JSON
- Supports exact match, fuzzy match, substring match, project search, namespace search
- Usage examples:
  ```bash
  # Fuzzy search with typo tolerance
  python scripts/search_type_fuzzy.py "DocumentNotifcation" --max-distance 3

  # Find types with partial name
  python scripts/search_type_fuzzy.py "IDocNotif" --limit 5

  # Exact match only
  python scripts/search_type_fuzzy.py "NPOIAdapter" --exact

  # Search by project
  python scripts/search_type_fuzzy.py --project "ExxerAI.Application"

  # Verbose output with file paths
  python scripts/search_type_fuzzy.py "DocumentAsset" --verbose
  ```

**update_type_database.py** ⭐ **NEW - AUTOMATED DATABASE UPDATES**
- Automated scheduled task to keep type database current
- Runs scanner, compares changes, maintains history (keeps last 10 scans)
- Reports statistics on types added/removed/modified
- Creates symlink to latest database
- Can be run manually or automated via cron/Task Scheduler
- Usage: `python scripts/update_type_database.py --verbose`
- Schedule daily:
  ```bash
  # Linux/Mac (crontab -e)
  0 2 * * * cd /path/to/ExxerAI && python scripts/update_type_database.py

  # Windows Task Scheduler
  Program: python
  Arguments: F:\Dynamic\ExxerAi\ExxerAI\scripts\update_type_database.py
  Start in: F:\Dynamic\ExxerAi\ExxerAI
  ```

---

### 🔧 Dependency & Package Fixing (4 scripts)

**fix_dependencies_smart_v2.py** ⭐ **ESSENTIAL**
- Smart GlobalUsings.cs updater using dependency analysis
- Dry-run mode available
- Requires: `enhanced_dependency_analysis.json`
- Usage: `python scripts/fix_dependencies_smart_v2.py --base-path "F:/Dynamic/ExxerAi/ExxerAI" --report "enhanced_dependency_analysis.json" --dry-run`

**generate_targeted_globalusings.py**
- Generates project-specific GlobalUsings.cs from analysis
- Avoids namespace duplication
- Usage: `python scripts/generate_targeted_globalusings.py --project "ExxerAI.Component"`

**deduplicate_package_references.py**
- Removes duplicate PackageReference entries from .csproj files
- Ensures clean project files
- Usage: `python scripts/deduplicate_package_references.py`

**ensure_project_settings.py**
- Validates and fixes project settings (TargetFramework, nullable, etc.)
- Ensures consistency across projects
- Usage: `python scripts/ensure_project_settings.py`

---

### 🩺 XUnit Modernization (2 scripts)

**fix_xunit1026_v5_async_support.py** ⭐ **LATEST VERSION**
- Fixes XUnit1026 warnings (async test methods without async assertions)
- Adds proper .ShouldBeTrue(), .ShouldBe() assertions
- Version 5 with enhanced async pattern support
- Usage: `python scripts/fix_xunit1026_v5_async_support.py --file "path/to/Tests.cs"`

**fix_xunit1051_v3_surgical_precision.py** ⭐ **LATEST VERSION**
- Fixes XUnit1051 warnings (missing cancellation token parameters)
- Surgical precision - only modifies necessary test methods
- Version 3 with improved pattern matching
- Usage: `python scripts/fix_xunit1051_v3_surgical_precision.py --file "path/to/Tests.cs"`

---

### 🏗️ Project & Architecture Management (7 scripts)

**project_standardization_analyzer.py**
- Analyzes project structure for standardization opportunities
- Reports non-compliant projects
- Usage: `python scripts/project_standardization_analyzer.py`

**project_standardization_production.py**
- Applies standardization fixes to production projects
- Creates backup before modifications
- Usage: `python scripts/project_standardization_production.py --dry-run`

**resolve_class_duplicates.py**
- Resolves class naming conflicts across projects
- Suggests refactoring strategies
- Usage: `python scripts/resolve_class_duplicates.py`

**find_orphaned_projects_efficient.py**
- Fast detection of projects not included in solution file
- Helps maintain solution integrity
- Usage: `python scripts/find_orphaned_projects_efficient.py`

**execute_safe_migration.py**
- Executes migration scripts with safety checks and rollback
- Used for major refactoring tasks
- Usage: `python scripts/execute_safe_migration.py --migration "migration_name"`

**automation_recovery_manager_v2.py**
- Manages automation script failures and recovery
- Latest version with enhanced error handling
- Usage: `python scripts/automation_recovery_manager_v2.py`

**get_current_error_counts.py**
- Quickly reports current build error counts by type
- Useful for tracking build health
- Usage: `python scripts/get_current_error_counts.py`

---

### 📊 Specialized Analysis (5 scripts)

**analyze_google_api_usage.py**
- Analyzes Google Drive API usage patterns
- Helps optimize API calls and quota usage
- Usage: `python scripts/analyze_google_api_usage.py`

**analyze_integration_tests_evocative.py**
- Analyzes integration tests for evocative architecture compliance
- Ensures tests are in correct evocative domains
- Usage: `python scripts/analyze_integration_tests_evocative.py`

**analyze_missing_xml_docs.py**
- Identifies CS1591 warnings (missing XML documentation)
- Generates reports for documentation gaps
- Usage: `python scripts/analyze_missing_xml_docs.py`

**detailed_docker_inspection.py**
- Inspects Docker container configurations in tests
- Validates Testcontainers setup
- Usage: `python scripts/detailed_docker_inspection.py`

**detect_docker_usage_in_integration_tests.py**
- Detects Docker/Testcontainers usage across integration tests
- Helps standardize container usage patterns
- Usage: `python scripts/detect_docker_usage_in_integration_tests.py`

---

## 🗂️ Archive Structure

Obsolete scripts have been organized into categories:

```
scripts/archive/
├── migration/                   # Completed migration scripts (~30)
├── xunit-old-versions/         # Older XUnit fixer versions (~23)
├── dependency-old-versions/    # Older dependency analyzers (~20)
├── one-time-tools/             # Completed one-time tasks (~40)
└── deprecated/                 # Redundant/replaced scripts (~15)
```

**Why Archived?**
- ✅ **Migration**: Completed ADR-010, ADR-011, ADR-012 migrations
- ✅ **XUnit**: Older versions replaced by v3/v5 scripts
- ✅ **Dependencies**: Replaced by smart_v2 analyzers/fixers
- ✅ **One-time**: Task completed (propagation, verification, recovery)
- ✅ **Deprecated**: Functionality replaced by newer/better scripts

---

## 🎯 Quick Reference - Most Used Scripts

### Daily Development

```bash
# 1. Build and check for errors
dotnet build

# 2. Get error counts by type
python scripts/get_current_error_counts.py

# 3. Fix XUnit warnings
python scripts/fix_xunit1026_v5_async_support.py --file "path/to/Tests.cs"
python scripts/fix_xunit1051_v3_surgical_precision.py --file "path/to/Tests.cs"

# 4. Add test timeouts
python scripts/add_test_timeouts.py --base-path "F:/Dynamic/ExxerAi/ExxerAI"
```

### Architectural Analysis Workflow (Intelligence-Driven Split Decisions)

```bash
# 1. Scan production code for all types
python scripts/scan_exxerai_types.py \
  --base-path "F:/Dynamic/ExxerAi/ExxerAI" \
  --output "scripts/exxerai_types_20251108.json"

# 2. Scan test code for SUTs and organization
python scripts/scan_test_sut.py \
  --base-path "F:/Dynamic/ExxerAi/ExxerAI" \
  --output "scripts/test_sut_analysis_20251108.json" \
  --mode advanced

# 3. Cross-reference and analyze alignment
python scripts/cross_reference_sut.py \
  --production "scripts/exxerai_types_20251108.json" \
  --tests "scripts/test_sut_analysis_20251108.json" \
  --output "scripts/cross_ref_nexus_20251108.json" \
  --focus "ExxerAI.Nexus.Adapter.Tests"

# 4. Review analysis report
cat scripts/cross_ref_nexus_20251108.json

# Result: Deterministic split recommendations based on architectural analysis
# - Perfect alignment: No split needed
# - Mixed alignment: Split recommendations with distribution percentages
# - Orphaned tests: Tests without production code (may need cleanup)
```

### Dependency Resolution Workflow

```bash
# 1. Analyze dependencies and create metadata
python scripts/analyze_dependencies_smart_v2.py --base-path "F:/Dynamic/ExxerAi/ExxerAI" --errors "Errors/CS0246.txt"

# 2. Review the analysis report
cat enhanced_dependency_analysis.json

# 3. Dry-run dependency fixes
python scripts/fix_dependencies_smart_v2.py --base-path "F:/Dynamic/ExxerAi/ExxerAI" --report "enhanced_dependency_analysis.json" --dry-run

# 4. Apply fixes (requires user approval)
python scripts/fix_dependencies_smart_v2.py --base-path "F:/Dynamic/ExxerAi/ExxerAI" --report "enhanced_dependency_analysis.json" --apply
```

### Test Relocation Workflow

```bash
# 1. Ensure dependency analysis is up-to-date
python scripts/analyze_dependencies_smart_v2.py --base-path "F:/Dynamic/ExxerAi/ExxerAI"

# 2. Dry-run test relocation
python scripts/relocate_test_smart.py \
  --file "code/src/Losetests/SomeTests.cs" \
  --destination "05IntegrationTests/ExxerAI.Component.Integration.Tests" \
  --namespace "ExxerAI.Component.Integration.Tests" \
  --dry-run

# 3. Apply relocation
python scripts/relocate_test_smart.py \
  --file "code/src/Losetests/SomeTests.cs" \
  --destination "05IntegrationTests/ExxerAI.Component.Integration.Tests" \
  --namespace "ExxerAI.Component.Integration.Tests"
```

---

## 🔗 **Git Hook: Auto-Update Type Database**

**RECOMMENDED**: Install git hook to automatically update type database on every commit with C# file changes.

### Installation

```bash
# Copy hook to git hooks directory
cp scripts/hooks/pre-commit-update-types.sh .git/hooks/pre-commit

# Make executable (Linux/Mac)
chmod +x .git/hooks/pre-commit

# Windows (no chmod needed, but ensure Python is in PATH)
```

### What It Does

- ✅ Detects C# file changes in commit
- ✅ Runs type scanner automatically
- ✅ Stages updated JSON file
- ✅ Keeps last 5 type databases (auto-cleanup)
- ✅ Creates `exxerai_types_latest.json` symlink
- ⚡ Only runs when `.cs` files are modified (fast)

### Testing the Hook

```bash
# Make a small change to a C# file
echo "// test" >> code/src/Core/ExxerAI.Domain/SomeFile.cs

# Commit (hook will run automatically)
git add code/src/Core/ExxerAI.Domain/SomeFile.cs
git commit -m "test: trigger type database update"

# You'll see:
# 🔍 Pre-commit: Checking for C# file changes...
# 📝 C# files changed, updating type database...
# 🔄 Running type scanner...
# ✅ Type database updated: scripts/exxerai_types_20251108_143022.json
```

### Manual Update (Without Committing)

```bash
python scripts/update_type_database.py --verbose
```

---

## 📖 Documentation

For detailed information on script workflows, see:
- **SCRIPTS_INVENTORY_AND_INDEX.md**: Complete script inventory and archival plan
- **LOSETESTS_RELOCATION_MAP.md**: Test relocation guide
- **ADR-012**: Losetests orphan relocation plan
- **CLAUDE.md**: Project development standards and patterns

---

## ⚠️ Safety Guidelines

1. **Always use dry-run mode first** for scripts that modify files
2. **Commit changes before running file-modifying scripts**
3. **Review script output** before applying changes
4. **Keep backups** of critical files (scripts auto-backup when possible)
5. **Never skip git safety protocols** - see CLAUDE.md for details

---

## 🔄 Script Versioning

**Active Latest Versions:**
- analyze_dependencies_smart_v2.py *(latest)*
- fix_dependencies_smart_v2.py *(latest)*
- fix_xunit1026_v5_async_support.py *(latest)*
- fix_xunit1051_v3_surgical_precision.py *(latest)*
- automation_recovery_manager_v2.py *(latest)*

**Archived Older Versions:**
- All v1, v3, v4 versions archived to `archive/` subdirectories

---

## 📞 Support

If you encounter issues with scripts:
1. Check the script's help: `python scripts/script_name.py --help`
2. Review dry-run output before applying
3. Check archived versions in `archive/` if needed
4. Refer to CLAUDE.md for development standards

**Last Updated**: 2025-11-08
**Total Active Scripts**: 36 (33 + 3 new: search_type_fuzzy.py, update_type_database.py, pre-commit hook)
**Total Archived Scripts**: 43

---

## 🎯 Intelligence-Driven Development

The three **ESSENTIAL** analysis scripts (`scan_exxerai_types.py`, `scan_test_sut.py`, `cross_reference_sut.py`) form a powerful intelligence-gathering pipeline for architectural decisions:

**Use Case: Test Project Split Analysis**
1. **Scan production code** → Know what exists and where it lives
2. **Scan test code** → Know what's being tested and how
3. **Cross-reference** → Detect architectural alignment and violations
4. **Make decisions** → Data-driven split/refactor recommendations

**Example: ADR-014 Nexus Adapter Split**
- Found mixed alignment: 40% Axis, 30% Domain, 20% Application, 10% Nexus
- Detected architectural violations: Domain/Application tests in adapter layer
- Recommended hybrid split: Fix violations THEN split by technology
- Result: 5-6 focused test projects from 1 monolithic project

**Philosophy**: "Let the analysis tell us" - Deterministic, automated intelligence gathering eliminates guesswork and ensures architecturally sound decisions.
