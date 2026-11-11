# Scripts Archive - Obsolete & Completed Tools

## Overview

This archive contains **43 Python scripts** that have been retired from active use. Scripts are organized by reason for archival.

**Archive Date**: 2025-11-07
**Archived From**: scripts/ directory (74 → 31 active scripts)

---

## 📂 Archive Categories

### migration/ (~30 scripts)
**Completed migration tasks for ADR-010, ADR-011, ADR-012**

Contains scripts used for:
- Infrastructure extraction from domain projects
- Test file relocation and organization
- Evocative architecture migrations
- Domain file extraction and consolidation

**Status**: ✅ Migrations complete, scripts no longer needed

---

### xunit-old-versions/ (~20 scripts)
**Superseded XUnit warning fixers**

Contains older versions of:
- XUnit1026 fixers (v1, v2, v3, v4 - replaced by v5)
- XUnit1051 fixers (v1, v2 - replaced by v3)
- XUnit assertion fixers
- XUnit package update scripts

**Status**: ✅ Replaced by latest versions (v5, v3) in parent directory

**Active Versions**:
- `../fix_xunit1026_v5_async_support.py` (latest)
- `../fix_xunit1051_v3_surgical_precision.py` (latest)

---

### dependency-old-versions/ (~20 scripts)
**Superseded dependency analyzers and fixers**

Contains older versions of:
- Dependency analyzers (v1, v3 - replaced by smart_v2)
- Dependency fixers (v1, basic versions - replaced by smart_v2)
- CS0246, CS0234 error fixers
- Specific namespace error fixers

**Status**: ✅ Replaced by smart_v2 analyzers/fixers

**Active Versions**:
- `../analyze_dependencies_smart_v2.py` (latest)
- `../fix_dependencies_smart_v2.py` (latest)

---

### one-time-tools/ (~40 scripts)
**Completed one-time tasks**

Contains scripts for:
- Test file relocation and migration
- Fixture propagation (manual, system, e2e, vault)
- Validation and verification scripts
- Solution file fixes
- Configuration updates
- Recovery and restore operations
- Null safety fixes
- Phase-specific migration tasks

**Status**: ✅ Tasks completed, scripts preserved for reference

**Examples**:
- Losetests relocation → Complete
- Fixture propagation → Complete
- Solution GUID fixes → Complete
- Null reference fixes → Complete

---

### deprecated/ (~15 scripts)
**Redundant or replaced scripts**

Contains scripts that were:
- Replaced by newer/better implementations
- Made redundant by workflow changes
- Duplicates of functionality elsewhere
- Experimental scripts that didn't pan out

**Status**: ✅ Functionality available elsewhere or no longer needed

---

## 🔍 Finding Archived Scripts

### By Functionality

**Need to fix XUnit warnings?**
- ❌ Don't use archived versions in `xunit-old-versions/`
- ✅ Use: `../fix_xunit1026_v5_async_support.py` or `../fix_xunit1051_v3_surgical_precision.py`

**Need to analyze dependencies?**
- ❌ Don't use old analyzers in `dependency-old-versions/`
- ✅ Use: `../analyze_dependencies_smart_v2.py` and `../fix_dependencies_smart_v2.py`

**Need to migrate test files?**
- ❌ Migration-specific scripts in `migration/` and `one-time-tools/` are for completed tasks
- ✅ Use: `../relocate_test_smart.py` for new relocations

---

## 📋 Archive Inventory

### Migration Scripts (~30)
```
automated_migration_executor.py
complete_migration_executor.py
evocative_migration_executor.py
final_migration_executor.py
infrastructure_migration_executor.py
migration_executor_phase2.py
migration_executor_phase2_enhanced.py
consolidation_analyzer.py
consolidation_executor.py
consolidation_executor_fixed.py
execute_infrastructure_extraction.py
extract_domain_files.py
extract_interfaces_snapshot.py
migration_gap_analyzer.py
migration_strategy_planner.py
refined_migration_strategy.py
test_infrastructure_migration.py
analyze_remaining_migration.py
... (and more)
```

### XUnit Old Versions (~20)
```
fix_xunit1026_batch.py
fix_xunit1026_comprehensive.py
fix_xunit1026_corrected.py
fix_xunit1026_focused.py
fix_xunit1026_scale.py
fix_xunit1026_v2_safe.py
fix_xunit1026_v3_vs_format.py
fix_xunit1026_v4_improved_brace.py
fix_xunit1026_xuint_logger.py
fix_xunit_advanced.py
fix_xunit1051.py
fix_xunit1051_v2_advanced_patterns.py
robust_xunit1051_fixer.py
run_robust_xunit1051.py
xunit1051_cancellation_token_fixer.py
... (and more)
```

### One-Time Tools (~40)
```
add_testcontainers_phase2.py
application_null_safety_fixer.py
check_build_status.py
classify_integration_vs_system_tests.py
classify_orphaned_tests.py
collect_using_statements.py
complete_phase1_merges.py
fix_all_globalusings.py
fix_configuration_validator.py
fix_documenttype_rename.py
fix_enum_model_conversions.py
move_legacy_cs_files_v2.py
propagate_fixtures_manual.py
propagate_fixtures_system_e2e.py
propagate_vault_fixtures.py
rename_integration_tests_phase1.py
rescue_migration_assets.py
validate_propagation_script.py
verify_test_migration.py
... (and more)
```

---

## 🔄 Why Archive Instead of Delete?

1. **Historical Reference**: Understanding past approaches helps avoid repeating mistakes
2. **Learning Resource**: Archived scripts show evolution of tooling
3. **Recovery**: If needed, old approaches can be resurrected
4. **Audit Trail**: Demonstrates what was tried and why it was replaced
5. **Documentation**: Code is self-documenting for what was done

---

## ⚠️ Usage Warning

**DO NOT use archived scripts for active development!**

Archived scripts may:
- ❌ Contain bugs that were fixed in newer versions
- ❌ Use outdated patterns or approaches
- ❌ Operate on file structures that no longer exist
- ❌ Conflict with current codebase organization
- ❌ Lack safety features added in later versions

**ALWAYS use the active scripts in the parent directory.**

---

## 📞 Need Help?

**If you think you need an archived script:**
1. Check the parent directory for active equivalents
2. Review the script categories in `../README.md`
3. Ask: "Why was this archived?" (see category descriptions above)
4. Consider whether the task is already complete
5. If still needed, contact the development team

**Active Script Documentation:**
- `../README.md` - Complete guide to active scripts
- `../../SCRIPTS_INVENTORY_AND_INDEX.md` - Full inventory
- `../../CLAUDE.md` - Development standards

---

**Last Updated**: 2025-11-07
**Total Archived**: 43 scripts
**Archive Categories**: 5
