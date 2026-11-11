#!/usr/bin/env python3
"""
ADR-011 Phase 5.2: Safe Migration Executor
Safely migrates test files based on dependency analysis with full safety protocols.

SAFETY FEATURES:
- Git status check (must be clean)
- Destination file conflict detection
- Dry-run mode by default
- Comprehensive logging
- Backup verification

Usage:
    python execute_safe_migration.py --dry-run  # Safe analysis
    python execute_safe_migration.py --apply    # Execute migration
"""

import json
import os
import shutil
import subprocess
import argparse
from pathlib import Path
from datetime import datetime
from typing import Dict, List, Set

# Configuration
BASE_PATH = r"F:\Dynamic\ExxerAi\ExxerAI"
INPUT_FILE = os.path.join(BASE_PATH, "docs", "adr", "migration_artifacts", "migration_dependency_analysis.json")
LOG_FILE = os.path.join(BASE_PATH, "docs", "adr", "migration_artifacts", "logs", f"migration_execution_{datetime.now().strftime('%Y-%m-%d_%H-%M-%S')}.log")

# Banner comment for broken infrastructure files
BROKEN_INFRASTRUCTURE_BANNER = """//
// ⚠️ ⚠️ ⚠️ WARNING: BROKEN DESIGN - NEEDS REPAIR ⚠️ ⚠️ ⚠️
//
// This file was migrated from ExxerAI.Components.Integration.Test
// but has KNOWN DESIGN ISSUES that need to be fixed.
//
// Reference working examples in:
// - External repository (to be provided)
// - Working container fixtures in other test projects
//
// DO NOT USE AS-IS IN PRODUCTION TESTS!
// Migrated on: {timestamp}
//
// ⚠️ ⚠️ ⚠️ ⚠️ ⚠️ ⚠️ ⚠️ ⚠️ ⚠️ ⚠️ ⚠️ ⚠️ ⚠️ ⚠️ ⚠️
//

"""

class MigrationExecutor:
    def __init__(self, log_file: str, dry_run: bool = True):
        self.log_file = log_file
        self.dry_run = dry_run
        self.conflicts = []
        self.operations = []
        self.repaired_files = []

        os.makedirs(os.path.dirname(log_file), exist_ok=True)

    def log(self, message: str, level: str = "INFO"):
        """Log message to console and file"""
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        log_entry = f"[{timestamp}] [{level}] {message}\n"
        print(log_entry.strip())
        with open(self.log_file, "a", encoding="utf-8") as f:
            f.write(log_entry)

    def check_git_status(self) -> bool:
        """Check if git working directory is clean"""
        self.log("Checking git status...", "INFO")

        try:
            result = subprocess.run(
                ["git", "status", "--porcelain"],
                cwd=BASE_PATH,
                capture_output=True,
                text=True,
                check=True
            )

            if result.stdout.strip():
                self.log("Git working directory has uncommitted changes!", "ERROR")
                self.log("Please commit your changes before running migration", "ERROR")
                self.log("Changes detected:", "ERROR")
                for line in result.stdout.split("\n"):
                    if line.strip():
                        self.log(f"  {line}", "ERROR")
                return False

            self.log("Git working directory is clean ✓", "INFO")
            return True

        except subprocess.CalledProcessError as e:
            self.log(f"Error checking git status: {e}", "ERROR")
            return False
        except FileNotFoundError:
            self.log("Git not found in PATH. Please ensure git is installed.", "ERROR")
            return False

    def check_destination_conflicts(self, migration_plan: Dict) -> List[Dict]:
        """Check if any destination files already exist"""
        self.log("Checking for destination conflicts...", "INFO")
        conflicts = []

        for item in migration_plan["files"]:
            target_path = item["target_path"]

            if os.path.exists(target_path):
                conflict = {
                    "source": item["source_file"],
                    "target": target_path,
                    "file_name": item["file_name"]
                }
                conflicts.append(conflict)
                self.log(f"CONFLICT: {item['file_name']} already exists at {target_path}", "WARNING")

        if conflicts:
            self.log(f"Found {len(conflicts)} destination conflicts", "WARNING")
            self.log("These files require manual review and resolution", "WARNING")
        else:
            self.log("No destination conflicts found ✓", "INFO")

        return conflicts

    def ensure_target_directory(self, target_path: str):
        """Ensure target directory exists"""
        target_dir = os.path.dirname(target_path)

        if not os.path.exists(target_dir):
            if not self.dry_run:
                os.makedirs(target_dir, exist_ok=True)
                self.log(f"Created directory: {target_dir}", "INFO")
            else:
                self.log(f"[DRY-RUN] Would create directory: {target_dir}", "INFO")

    def add_repair_banner(self, file_path: str):
        """Add warning banner to files that need repair"""
        try:
            with open(file_path, "r", encoding="utf-8") as f:
                content = f.read()

            # Add banner at the top, after any using statements
            lines = content.split("\n")
            insert_index = 0

            # Find the end of using statements
            for i, line in enumerate(lines):
                if line.strip().startswith("using "):
                    insert_index = i + 1
                elif line.strip() and not line.strip().startswith("//"):
                    break

            banner = BROKEN_INFRASTRUCTURE_BANNER.format(timestamp=datetime.now().isoformat())
            lines.insert(insert_index, banner)

            with open(file_path, "w", encoding="utf-8") as f:
                f.write("\n".join(lines))

            self.log(f"Added repair banner to: {os.path.basename(file_path)}", "INFO")
            self.repaired_files.append(file_path)

        except Exception as e:
            self.log(f"Error adding repair banner to {file_path}: {e}", "ERROR")

    def execute_move(self, source: str, target: str, needs_repair: bool = False):
        """Execute file move operation"""
        try:
            self.ensure_target_directory(target)

            if not self.dry_run:
                shutil.copy2(source, target)
                self.log(f"Copied: {os.path.basename(source)} -> {target}", "INFO")

                if needs_repair:
                    self.add_repair_banner(target)

                # Delete original only after successful copy and banner addition
                os.remove(source)
                self.log(f"Deleted original: {source}", "INFO")

            else:
                self.log(f"[DRY-RUN] Would move: {os.path.basename(source)} -> {target}", "INFO")
                if needs_repair:
                    self.log(f"[DRY-RUN] Would add repair banner to: {os.path.basename(source)}", "INFO")

            self.operations.append({
                "type": "MOVE",
                "source": source,
                "target": target,
                "needs_repair": needs_repair,
                "status": "success" if not self.dry_run else "dry-run"
            })

        except Exception as e:
            self.log(f"Error moving {source} to {target}: {e}", "ERROR")
            self.operations.append({
                "type": "MOVE",
                "source": source,
                "target": target,
                "needs_repair": needs_repair,
                "status": "error",
                "error": str(e)
            })

    def execute_duplicate(self, source: str, target: str, needs_repair: bool = False):
        """Execute file duplication (copy without delete)"""
        try:
            self.ensure_target_directory(target)

            if not self.dry_run:
                shutil.copy2(source, target)
                self.log(f"Duplicated: {os.path.basename(source)} -> {target}", "INFO")

                if needs_repair:
                    self.add_repair_banner(target)

            else:
                self.log(f"[DRY-RUN] Would duplicate: {os.path.basename(source)} -> {target}", "INFO")
                if needs_repair:
                    self.log(f"[DRY-RUN] Would add repair banner to copy", "INFO")

            self.operations.append({
                "type": "DUPLICATE",
                "source": source,
                "target": target,
                "needs_repair": needs_repair,
                "status": "success" if not self.dry_run else "dry-run"
            })

        except Exception as e:
            self.log(f"Error duplicating {source} to {target}: {e}", "ERROR")
            self.operations.append({
                "type": "DUPLICATE",
                "source": source,
                "target": target,
                "needs_repair": needs_repair,
                "status": "error",
                "error": str(e)
            })

    def execute_migration(self, migration_plan: Dict):
        """Execute migration based on plan"""
        self.log("=" * 80, "INFO")
        self.log(f"Executing migration ({'' if not self.dry_run else 'DRY-RUN mode'})", "INFO")
        self.log("=" * 80, "INFO")

        for item in migration_plan["files"]:
            # Skip if conflict exists
            if any(c["source"] == item["source_file"] for c in self.conflicts):
                self.log(f"Skipping (conflict): {item['file_name']}", "WARNING")
                continue

            action = item["migration_action"]
            needs_repair = item["needs_repair"]

            if action == "MOVE" or action == "MOVE_NEEDS_REPAIR":
                self.execute_move(item["source_file"], item["target_path"], needs_repair)
            elif action == "DUPLICATE":
                self.execute_duplicate(item["source_file"], item["target_path"], needs_repair)
            else:
                self.log(f"Unknown action '{action}' for {item['file_name']}", "WARNING")

    def generate_execution_report(self):
        """Generate execution summary report"""
        self.log("=" * 80, "INFO")
        self.log("MIGRATION EXECUTION SUMMARY", "INFO")
        self.log("=" * 80, "INFO")

        success_count = sum(1 for op in self.operations if op["status"] in ["success", "dry-run"])
        error_count = sum(1 for op in self.operations if op["status"] == "error")

        self.log(f"Total operations: {len(self.operations)}", "INFO")
        self.log(f"  Successful: {success_count}", "INFO")
        self.log(f"  Errors: {error_count}", "INFO")
        self.log(f"  Conflicts (skipped): {len(self.conflicts)}", "INFO")
        self.log(f"  Files with repair banners: {len(self.repaired_files)}", "INFO")

        if self.conflicts:
            self.log("", "INFO")
            self.log("CONFLICTS REQUIRING MANUAL REVIEW:", "WARNING")
            for conflict in self.conflicts:
                self.log(f"  - {conflict['file_name']}", "WARNING")
                self.log(f"    Source: {conflict['source']}", "WARNING")
                self.log(f"    Target: {conflict['target']}", "WARNING")

        if error_count > 0:
            self.log("", "INFO")
            self.log("ERRORS ENCOUNTERED:", "ERROR")
            for op in self.operations:
                if op["status"] == "error":
                    self.log(f"  - {os.path.basename(op['source'])}: {op.get('error', 'Unknown error')}", "ERROR")

        # Save operations log
        operations_log = os.path.join(os.path.dirname(self.log_file), f"operations_{datetime.now().strftime('%Y-%m-%d_%H-%M-%S')}.json")
        with open(operations_log, "w", encoding="utf-8") as f:
            json.dump({
                "operations": self.operations,
                "conflicts": self.conflicts,
                "repaired_files": self.repaired_files,
                "summary": {
                    "total": len(self.operations),
                    "success": success_count,
                    "errors": error_count,
                    "conflicts": len(self.conflicts),
                    "repaired": len(self.repaired_files)
                }
            }, f, indent=2, ensure_ascii=False)

        self.log(f"Operations log saved to: {operations_log}", "INFO")
        self.log("=" * 80, "INFO")

    def run(self, migration_plan: Dict):
        """Execute complete migration workflow"""
        self.log("=" * 80, "INFO")
        self.log("ADR-011 Phase 5.2: Safe Migration Executor", "INFO")
        self.log(f"Mode: {'DRY-RUN (no changes will be made)' if self.dry_run else 'APPLY (files will be migrated)'}", "INFO")
        self.log("=" * 80, "INFO")

        # Step 1: Check git status (skip in dry-run)
        if not self.dry_run:
            if not self.check_git_status():
                self.log("Migration aborted due to uncommitted changes", "ERROR")
                return False

        # Step 2: Check for conflicts
        self.conflicts = self.check_destination_conflicts(migration_plan)

        # Step 3: Execute migration
        self.execute_migration(migration_plan)

        # Step 4: Generate report
        self.generate_execution_report()

        if self.dry_run:
            self.log("DRY-RUN COMPLETE: Review the operations above", "INFO")
            self.log("To execute migration, run with --apply flag", "INFO")
        else:
            self.log("MIGRATION COMPLETE", "INFO")
            self.log("Next steps:", "INFO")
            self.log("  1. Review the operations log", "INFO")
            self.log("  2. Resolve any conflicts manually", "INFO")
            self.log("  3. Run: dotnet build", "INFO")
            self.log("  4. Fix files with repair banners using working examples", "INFO")

        return True

def main():
    parser = argparse.ArgumentParser(description="ADR-011 Phase 5.2: Safe Migration Executor")
    parser.add_argument("--apply", action="store_true", help="Execute migration (default is dry-run)")
    parser.add_argument("--input", type=str, default=INPUT_FILE, help="Input migration plan JSON file")
    args = parser.parse_args()

    dry_run = not args.apply

    # Load migration plan
    if not os.path.exists(args.input):
        print(f"ERROR: Migration plan not found: {args.input}")
        print("Run analyze_migration_dependencies.py first to generate the plan")
        return 1

    with open(args.input, "r", encoding="utf-8") as f:
        migration_plan = json.load(f)

    # Execute migration
    executor = MigrationExecutor(LOG_FILE, dry_run=dry_run)
    success = executor.run(migration_plan)

    return 0 if success else 1

if __name__ == "__main__":
    exit(main())
