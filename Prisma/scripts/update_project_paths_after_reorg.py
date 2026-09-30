#!/usr/bin/env python3
"""
Update .sln and .csproj files after folder reorganization.

After moving projects to organized folders, this script:
1. Updates .sln file with new project paths
2. Updates all <ProjectReference> paths in .csproj files
"""

import re
from pathlib import Path
from typing import Dict, List, Tuple
import shutil
from datetime import datetime


class ProjectPathUpdater:
    """Updates project paths after reorganization."""

    def __init__(self, base_path: str = "Code/Src/CSharp"):
        self.base_path = Path(base_path)
        self.project_map = {}  # Old path -> New path mapping

    def build_project_map(self) -> Dict[str, str]:
        """Build mapping of old paths to new paths."""

        # Define the new structure (matches sync_folders_to_vs_structure.ps1)
        structure = {
            "01-Core": ["Domain", "Application"],
            "02-Infrastructure": [
                "Infrastructure",
                "Infrastructure.BrowserAutomation",
                "Infrastructure.Classification",
                "Infrastructure.Database",
                "Infrastructure.Export",
                "Infrastructure.Extraction",
                "Infrastructure.FileStorage",
                "Infrastructure.Imaging",
                "Infrastructure.Metrics",
                "Infrastructure.Python.GotOcr2"
            ],
            "03-UI": ["UI"],
            "04-Tests/01-Core": [
                "Tests.Application",
                "Tests.Domain",
                "Tests.Domain.Interfaces"
            ],
            "04-Tests/02-Infrastructure": [
                "Tests.Infrastructure.Classification",
                "Tests.Infrastructure.Database",
                "Tests.Infrastructure.Export",
                "Tests.Infrastructure.Extraction",
                "Tests.Infrastructure.Extraction.GotOcr2",
                "Tests.Infrastructure.Extraction.Teseract",
                "Tests.Infrastructure.FileStorage",
                "Tests.Infrastructure.FileSystem",
                "Tests.Infrastructure.Imaging",
                "Tests.Infrastructure.Metrics",
                "Tests.Infrastructure.Python",
                "Tests.Infrastructure.XmlExtraction"
            ],
            "04-Tests/03-System": [
                "Tests.Infrastructure.BrowserAutomation.E2E",
                "Tests.System"
            ],
            "04-Tests/04-UI": ["Tests.UI"],
            "04-Tests/05-E2E": ["Tests.EndToEnd"],
            "04-Tests/06-Architecture": ["Tests.Architecture"],
            "05-ConsoleApp": ["ConsoleApp.GotOcr2Demo"],
            "05-Testing/01-Abstractions": ["Testing"],
            "05-Testing/03-Infrastructure": ["Testing.Infrastructure"]
        }

        project_map = {}

        for target_folder, projects in structure.items():
            for project in projects:
                # Old path (flat)
                old_path = f"{project}"

                # New path (organized)
                new_path = f"{target_folder}/{project}"

                project_map[old_path] = new_path

        self.project_map = project_map
        return project_map

    def update_sln_file(self, sln_path: Path, dry_run: bool = False) -> int:
        """Update .sln file with new project paths."""

        if not sln_path.exists():
            print(f"❌ Solution file not found: {sln_path}")
            return 0

        print("📄 UPDATING .SLN FILE")
        print(f"   File: {sln_path}")
        print()

        # Read .sln file
        with open(sln_path, 'r', encoding='utf-8-sig') as f:
            content = f.read()

        original_content = content
        updates = 0

        # Pattern: Project("{...}") = "ProjectName", "Path\To\Project.csproj", "{...}"
        pattern = r'(Project\("[^"]+"\)\s*=\s*"[^"]+",\s*")([^"]+)(")'

        def replace_project_path(match):
            nonlocal updates
            prefix = match.group(1)
            old_path = match.group(2)
            suffix = match.group(3)

            # Normalize path separators
            old_path_normalized = old_path.replace('\\', '/')

            # Try to find mapping
            for old_folder, new_folder in self.project_map.items():
                if old_path_normalized.startswith(old_folder + '/'):
                    new_path = old_path_normalized.replace(old_folder + '/', new_folder + '/', 1)
                    new_path = new_path.replace('/', '\\')  # Convert back to Windows format

                    if new_path != old_path:
                        print(f"  ✓ {old_path} → {new_path}")
                        updates += 1
                        return prefix + new_path + suffix

            return match.group(0)

        # Apply replacements
        content = re.sub(pattern, replace_project_path, content)

        if dry_run:
            print(f"\n  [DRY RUN] Would update {updates} project paths in .sln")
        else:
            if updates > 0:
                # Backup original
                backup_path = sln_path.with_suffix('.sln.backup')
                shutil.copy2(sln_path, backup_path)
                print(f"  💾 Backup created: {backup_path}")

                # Write updated file
                with open(sln_path, 'w', encoding='utf-8-sig') as f:
                    f.write(content)

                print(f"  ✅ Updated {updates} project paths in .sln")
            else:
                print(f"  ℹ️ No updates needed in .sln")

        print()
        return updates

    def find_all_csproj_files(self) -> List[Path]:
        """Find all .csproj files in the new structure."""
        return list(self.base_path.rglob("*.csproj"))

    def update_project_references(self, csproj_path: Path, dry_run: bool = False) -> int:
        """Update <ProjectReference> paths in a .csproj file."""

        with open(csproj_path, 'r', encoding='utf-8') as f:
            content = f.read()

        original_content = content
        updates = 0

        # Pattern: <ProjectReference Include="..\..\Path\To\Project.csproj" />
        pattern = r'(<ProjectReference\s+Include=")([^"]+)("\s*/?>)'

        def replace_reference_path(match):
            nonlocal updates
            prefix = match.group(1)
            old_rel_path = match.group(2)
            suffix = match.group(3)

            # Normalize path separators
            old_rel_path_normalized = old_rel_path.replace('\\', '/')

            # Resolve to absolute path (relative to current csproj)
            try:
                current_dir = csproj_path.parent
                old_abs_path = (current_dir / old_rel_path_normalized).resolve()

                # Extract project folder name from path
                # e.g., "Application\ExxerCube.Prisma.Application.csproj" -> "Application"
                parts = old_rel_path_normalized.split('/')
                if len(parts) >= 2:
                    project_folder = parts[-2]

                    # Check if this project was moved
                    if project_folder in self.project_map:
                        new_folder = self.project_map[project_folder]

                        # Calculate new relative path
                        new_abs_path = self.base_path / new_folder / parts[-1]

                        if new_abs_path.exists():
                            # Calculate relative path from current csproj to new location
                            try:
                                new_rel_path = Path(os.path.relpath(new_abs_path, current_dir))
                                new_rel_path_str = str(new_rel_path).replace('/', '\\')

                                if new_rel_path_str != old_rel_path:
                                    updates += 1
                                    return prefix + new_rel_path_str + suffix
                            except ValueError:
                                # Can't create relative path (different drives?)
                                pass
            except Exception as e:
                # If resolution fails, keep original
                pass

            return match.group(0)

        # Import os for relpath
        import os

        # Apply replacements
        content = re.sub(pattern, replace_reference_path, content)

        if content != original_content:
            if dry_run:
                return updates
            else:
                # Backup original
                backup_path = csproj_path.with_suffix('.csproj.backup')
                shutil.copy2(csproj_path, backup_path)

                # Write updated file
                with open(csproj_path, 'w', encoding='utf-8') as f:
                    f.write(content)

                return updates

        return 0

    def update_all_project_references(self, dry_run: bool = False) -> int:
        """Update all .csproj files."""

        print("📦 UPDATING .CSPROJ PROJECT REFERENCES")
        print()

        csproj_files = self.find_all_csproj_files()
        total_updates = 0

        for csproj_path in csproj_files:
            rel_path = csproj_path.relative_to(self.base_path)
            updates = self.update_project_references(csproj_path, dry_run=dry_run)

            if updates > 0:
                status = "[DRY RUN]" if dry_run else "✓"
                print(f"  {status} {rel_path}: {updates} references updated")
                total_updates += updates

        print()
        if dry_run:
            print(f"  [DRY RUN] Would update {total_updates} project references across {len(csproj_files)} files")
        else:
            print(f"  ✅ Updated {total_updates} project references across {len(csproj_files)} files")

        print()
        return total_updates

    def run(self, dry_run: bool = False):
        """Run the full update process."""

        print("=" * 80)
        print("UPDATE PROJECT PATHS AFTER REORGANIZATION")
        print("=" * 80)
        print()

        if dry_run:
            print("🔍 DRY RUN MODE - No files will be modified")
            print()

        # Build project map
        print("📋 BUILDING PROJECT MAPPING")
        self.build_project_map()
        print(f"   Found {len(self.project_map)} project mappings")
        print()

        # Update .sln file
        sln_path = self.base_path / "ExxerCube.Prisma.sln"
        sln_updates = self.update_sln_file(sln_path, dry_run=dry_run)

        # Update .csproj files
        csproj_updates = self.update_all_project_references(dry_run=dry_run)

        # Summary
        print("=" * 80)
        print("SUMMARY")
        print("=" * 80)
        print(f"Mode: {'DRY RUN' if dry_run else 'LIVE'}")
        print(f".sln updates: {sln_updates}")
        print(f".csproj updates: {csproj_updates}")
        print()

        if dry_run:
            print("🔄 To perform actual updates, run without --dry-run:")
            print("   python scripts/update_project_paths_after_reorg.py")
        else:
            print("✅ All project paths updated!")
            print()
            print("📌 NEXT STEPS:")
            print("   1. Run: dotnet build Code/Src/CSharp")
            print("   2. Fix any remaining reference errors")
            print("   3. Run tests to verify everything works")

        print()


def main():
    import sys

    dry_run = "--dry-run" in sys.argv or "-n" in sys.argv

    updater = ProjectPathUpdater()
    updater.run(dry_run=dry_run)


if __name__ == "__main__":
    main()
