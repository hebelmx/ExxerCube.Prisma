#!/usr/bin/env python3
"""
ADR-011 Phase 1: Rename Integration Test Projects to Evocative Architecture
Safe automation with dry-run mode
"""

import os
import re
import shutil
from pathlib import Path
from typing import List, Tuple, Dict


class ProjectRenamer:
    """Handles safe renaming and merging of test projects"""

    def __init__(self, base_path: str, dry_run: bool = True):
        self.base_path = Path(base_path)
        self.integration_tests_dir = self.base_path / "code/src/tests/05IntegrationTests"
        self.dry_run = dry_run
        self.operations = []

    def log_operation(self, operation: str):
        """Log an operation for review"""
        self.operations.append(operation)
        prefix = "[DRY-RUN]" if self.dry_run else "[EXECUTING]"
        print(f"{prefix} {operation}")

    def rename_project(self, old_name: str, new_name: str):
        """Rename a single project with all its references"""
        old_dir = self.integration_tests_dir / old_name
        new_dir = self.integration_tests_dir / new_name

        if not old_dir.exists():
            print(f"⚠️  WARNING: {old_name} does not exist, skipping...")
            return

        self.log_operation(f"RENAME: {old_name} → {new_name}")

        # 1. Rename directory
        if not self.dry_run:
            old_dir.rename(new_dir)
            self.log_operation(f"  ✅ Directory renamed")

        # 2. Rename .csproj file
        old_csproj = new_dir / f"{old_name}.csproj" if not self.dry_run else old_dir / f"{old_name}.csproj"
        new_csproj = new_dir / f"{new_name}.csproj"

        if old_csproj.exists():
            if not self.dry_run:
                old_csproj.rename(new_csproj)
            self.log_operation(f"  ✅ Renamed {old_name}.csproj → {new_name}.csproj")

        # 3. Update namespace in all .cs files
        cs_files = list((new_dir if not self.dry_run else old_dir).rglob("*.cs"))
        old_namespace = old_name
        new_namespace = new_name

        for cs_file in cs_files:
            if not self.dry_run:
                self.update_namespace_in_file(cs_file, old_namespace, new_namespace)

        self.log_operation(f"  ✅ Updated namespaces in {len(cs_files)} CS files")

    def update_namespace_in_file(self, file_path: Path, old_ns: str, new_ns: str):
        """Update namespace declarations in a C# file"""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()

            # Update namespace declarations
            # Pattern 1: namespace OldName; (file-scoped)
            content = re.sub(
                rf'^namespace\s+{re.escape(old_ns)}\s*;',
                f'namespace {new_ns};',
                content,
                flags=re.MULTILINE
            )

            # Pattern 2: namespace OldName { (block-scoped)
            content = re.sub(
                rf'namespace\s+{re.escape(old_ns)}\s*{{',
                f'namespace {new_ns} {{',
                content
            )

            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)

        except Exception as e:
            print(f"⚠️  Error updating {file_path}: {e}")

    def merge_projects(self, source_names: List[str], target_name: str):
        """Merge multiple projects into one"""
        self.log_operation(f"MERGE: {', '.join(source_names)} → {target_name}")

        # Create target directory
        target_dir = self.integration_tests_dir / target_name

        if not self.dry_run:
            target_dir.mkdir(exist_ok=True)

        # Use first source as template for .csproj
        first_source = self.integration_tests_dir / source_names[0]
        first_csproj = first_source / f"{source_names[0]}.csproj"

        if first_csproj.exists():
            target_csproj = target_dir / f"{target_name}.csproj"
            if not self.dry_run:
                shutil.copy2(first_csproj, target_csproj)
                # Update project file references
                self.update_namespace_in_file(target_csproj, source_names[0], target_name)
            self.log_operation(f"  ✅ Created {target_name}.csproj from {source_names[0]}")

        # Copy all CS files from all sources
        all_cs_files = []
        for source_name in source_names:
            source_dir = self.integration_tests_dir / source_name
            if not source_dir.exists():
                print(f"⚠️  WARNING: {source_name} does not exist, skipping...")
                continue

            cs_files = list(source_dir.rglob("*.cs"))
            all_cs_files.extend(cs_files)

            for cs_file in cs_files:
                relative_path = cs_file.relative_to(source_dir)
                target_file = target_dir / relative_path

                if not self.dry_run:
                    target_file.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(cs_file, target_file)
                    # Update namespace
                    self.update_namespace_in_file(target_file, source_name, target_name)

            self.log_operation(f"  ✅ Copied {len(cs_files)} files from {source_name}")

        # Mark old directories for deletion
        for source_name in source_names:
            source_dir = self.integration_tests_dir / source_name
            if source_dir.exists():
                if not self.dry_run:
                    shutil.rmtree(source_dir)
                self.log_operation(f"  ✅ Deleted old directory: {source_name}")

    def update_solution_file(self, renames: Dict[str, str]):
        """Update solution file with new project references"""
        sln_file = self.base_path / "code/src/ExxerAI.sln"

        if not sln_file.exists():
            print("⚠️  WARNING: Solution file not found")
            return

        self.log_operation(f"UPDATE: Solution file references")

        if not self.dry_run:
            with open(sln_file, 'r', encoding='utf-8') as f:
                content = f.read()

            # Update project references
            for old_name, new_name in renames.items():
                # Update project path
                content = re.sub(
                    rf'tests\\05IntegrationTests\\{re.escape(old_name)}\\{re.escape(old_name)}\.csproj',
                    f'tests\\\\05IntegrationTests\\\\{new_name}\\\\{new_name}.csproj',
                    content
                )

                # Update project name in solution
                content = re.sub(
                    rf'"{re.escape(old_name)}"',
                    f'"{new_name}"',
                    content
                )

            with open(sln_file, 'w', encoding='utf-8') as f:
                f.write(content)

        self.log_operation(f"  ✅ Updated {len(renames)} project references in solution")

    def execute_phase1(self):
        """Execute all Phase 1 renaming operations"""
        print("=" * 100)
        print("🎯 ADR-011 PHASE 1: RENAME TO EVOCATIVE ARCHITECTURE")
        print("=" * 100)
        print()

        # Track all renames for solution file update
        all_renames = {}

        # Simple renames
        simple_renames = [
            ("ExxerAI.Analytics.Integration.Test", "ExxerAI.Signal.Integration.Test"),
            ("ExxerAI.Authentication.Integration.Test", "ExxerAI.Sentinel.Integration.Test"),
            ("ExxerAI.EnhancedRag.Integration.Test", "ExxerAI.Cortex.Integration.Test"),
        ]

        print("📝 SIMPLE RENAMES")
        print("-" * 100)
        for old_name, new_name in simple_renames:
            self.rename_project(old_name, new_name)
            all_renames[old_name] = new_name
        print()

        # Merges
        print("🔀 MERGES")
        print("-" * 100)

        # Merge: Cache + Database → Datastream
        cache_database_sources = [
            "ExxerAI.Cache.Integration.Test",
            "ExxerAI.Database.Integration.Test"
        ]
        datastream_target = "ExxerAI.Datastream.Integration.Test"
        self.merge_projects(cache_database_sources, datastream_target)
        for source in cache_database_sources:
            all_renames[source] = datastream_target
        print()

        # Merge: GoogleDrive projects → Gatekeeper
        gatekeeper_sources = [
            "ExxerAI.GoogleDriveM2M.Integration.Test",
            "ExxerAI.Infrastructure.GoogleDriveM2M.Integration.Test"
        ]
        gatekeeper_target = "ExxerAI.Gatekeeper.Integration.Test"
        self.merge_projects(gatekeeper_sources, gatekeeper_target)
        for source in gatekeeper_sources:
            all_renames[source] = gatekeeper_target
        print()

        # Update solution file
        print("📋 SOLUTION FILE UPDATE")
        print("-" * 100)
        self.update_solution_file(all_renames)
        print()

        # Summary
        print("=" * 100)
        print("📊 PHASE 1 SUMMARY")
        print("=" * 100)
        print(f"Mode: {'DRY-RUN (no changes made)' if self.dry_run else 'EXECUTION (changes applied)'}")
        print(f"Total operations: {len(self.operations)}")
        print()
        print("Resulting structure (8 evocative projects):")
        print("  1. ExxerAI.Signal.Integration.Test 📊 (Analytics)")
        print("  2. ExxerAI.Sentinel.Integration.Test 🛡️ (Authentication)")
        print("  3. ExxerAI.Cortex.Integration.Test 🧠 (EnhancedRag)")
        print("  4. ExxerAI.Datastream.Integration.Test 🌊 (Cache + Database)")
        print("  5. ExxerAI.Gatekeeper.Integration.Test 🚪 (GoogleDrive projects)")
        print("  6. ExxerAI.Components.Integration.Test (Keep - will break up in Phase 6)")
        print("  7. ExxerAI.Nexus.Integration.Test ⚡ (Keep - already evocative)")
        print()
        print("=" * 100)


def main():
    import argparse

    parser = argparse.ArgumentParser(description="ADR-011 Phase 1: Rename integration tests")
    parser.add_argument("--base-path", default="F:/Dynamic/ExxerAi/ExxerAI", help="Base path to ExxerAI repo")
    parser.add_argument("--apply", action="store_true", help="Apply changes (default is dry-run)")

    args = parser.parse_args()

    renamer = ProjectRenamer(args.base_path, dry_run=not args.apply)
    renamer.execute_phase1()

    if renamer.dry_run:
        print()
        print("⚠️  This was a DRY-RUN. No changes were made.")
        print("💡 Run with --apply to execute the renaming operations.")
    else:
        print()
        print("✅ Phase 1 completed successfully!")
        print("📝 Next step: git add, git commit, then proceed to Phase 2")


if __name__ == "__main__":
    main()
