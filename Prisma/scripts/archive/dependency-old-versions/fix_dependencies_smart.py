#!/usr/bin/env python3
"""
Smart dependency fixer that only adds project and package references.
Does NOT modify GlobalUsings.cs since namespaces are injected by Directory.Build.props.
"""

import os
import re
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Set, List, Dict, Optional
from datetime import datetime
import argparse
import shutil


class SmartDependencyFixer:
    """Smart fixer that respects Directory.Build.props."""
    
    def __init__(self, base_path: str, dry_run: bool = True):
        self.base_path = Path(base_path)
        self.tests_path = self.base_path / "code" / "src" / "tests"
        self.src_path = self.base_path / "code" / "src"
        self.dry_run = dry_run
        
        # Track changes
        self.changes_made = {
            'added_project_refs': 0,
            'added_package_refs': 0,
            'updated_files': set()
        }
        
        if not dry_run:
            self.backup_dir = self.base_path / "scripts" / "smart_fix_backups" / datetime.now().strftime("%Y%m%d_%H%M%S")
            self.backup_dir.mkdir(parents=True, exist_ok=True)
            print(f"Backup directory: {self.backup_dir}")
    
    def load_analysis_report(self, report_file: str) -> Dict:
        """Load the smart analysis report."""
        with open(report_file, 'r', encoding='utf-8') as f:
            return json.load(f)
    
    def add_project_references(self, project_refs_by_project: Dict[str, List[str]]):
        """Add missing project references to .csproj files."""
        print("\n=== Adding Project References ===")
        
        for project_name, required_projects in project_refs_by_project.items():
            # Find the test project file
            project_files = list(self.tests_path.rglob(f"{project_name}/{project_name}.csproj"))
            if not project_files:
                project_files = list(self.tests_path.rglob(f"{project_name}/*.csproj"))
            
            if not project_files:
                print(f"  Warning: Could not find .csproj file for {project_name}")
                continue
            
            project_file = project_files[0]
            
            # Parse existing references
            tree = ET.parse(project_file)
            root = tree.getroot()
            
            existing_refs = set()
            for ref in root.findall(".//ProjectReference"):
                include = ref.get('Include')
                if include:
                    ref_name = Path(include).stem
                    existing_refs.add(ref_name)
            
            # Filter out already existing
            new_refs = [ref for ref in required_projects if ref not in existing_refs]
            
            if not new_refs:
                print(f"  {project_name}: All required project references already present")
                continue
            
            if self.dry_run:
                print(f"\n[DRY RUN] Would update: {project_file}")
                print(f"  Would add {len(new_refs)} project references:")
                for ref in new_refs:
                    print(f"    + {ref}")
            else:
                self._backup_file(project_file)
                
                # Find or create ItemGroup
                item_group = None
                for group in root.findall("ItemGroup"):
                    if group.find("ProjectReference") is not None:
                        item_group = group
                        break
                
                if item_group is None:
                    item_group = ET.SubElement(root, "ItemGroup")
                    item_group.set("Label", "Project References")
                
                # Add new references
                for ref_project in new_refs:
                    ref_path = self._find_project_path(ref_project)
                    if ref_path:
                        relative_path = os.path.relpath(ref_path, project_file.parent).replace('\\', '/')
                        proj_ref = ET.SubElement(item_group, "ProjectReference")
                        proj_ref.set("Include", relative_path)
                        self.changes_made['added_project_refs'] += 1
                    else:
                        print(f"    Warning: Could not find project {ref_project}")
                
                # Save
                self._indent_xml(root)
                tree.write(project_file, encoding='utf-8', xml_declaration=True)
                self.changes_made['updated_files'].add(str(project_file))
                
                print(f"  Updated: {project_file}")
                print(f"  Added {len(new_refs)} project references")
    
    def add_package_references(self, package_refs_by_project: Dict[str, List[str]]):
        """Add missing NuGet package references."""
        print("\n=== Adding NuGet Package References ===")
        
        for project_name, required_packages in package_refs_by_project.items():
            # Find project file
            project_files = list(self.tests_path.rglob(f"{project_name}/{project_name}.csproj"))
            if not project_files:
                project_files = list(self.tests_path.rglob(f"{project_name}/*.csproj"))
            
            if not project_files:
                print(f"  Warning: Could not find .csproj file for {project_name}")
                continue
            
            project_file = project_files[0]
            
            # Parse existing packages
            tree = ET.parse(project_file)
            root = tree.getroot()
            
            existing_packages = set()
            for ref in root.findall(".//PackageReference"):
                include = ref.get('Include')
                if include:
                    existing_packages.add(include)
            
            # Filter out already existing
            new_packages = [pkg for pkg in required_packages if pkg not in existing_packages]
            
            if not new_packages:
                print(f"  {project_name}: All required package references already present")
                continue
            
            if self.dry_run:
                print(f"\n[DRY RUN] Would update: {project_file}")
                print(f"  Would add {len(new_packages)} package references:")
                for pkg in new_packages:
                    print(f"    + {pkg}")
            else:
                self._backup_file(project_file)
                
                # Find or create ItemGroup
                item_group = None
                for group in root.findall("ItemGroup"):
                    if group.find("PackageReference") is not None:
                        item_group = group
                        break
                
                if item_group is None:
                    item_group = ET.SubElement(root, "ItemGroup")
                    item_group.set("Label", "Additional Packages")
                
                # Add new packages
                for package in new_packages:
                    pkg_ref = ET.SubElement(item_group, "PackageReference")
                    pkg_ref.set("Include", package)
                    # Note: Version managed by Directory.Packages.props
                    self.changes_made['added_package_refs'] += 1
                
                # Save
                self._indent_xml(root)
                tree.write(project_file, encoding='utf-8', xml_declaration=True)
                self.changes_made['updated_files'].add(str(project_file))
                
                print(f"  Updated: {project_file}")
                print(f"  Added {len(new_packages)} package references")
    
    def _find_project_path(self, project_name: str) -> Optional[Path]:
        """Find the actual path to a project file."""
        # Map common project names to their locations
        project_locations = {
            'ExxerAI.Domain.Common': '00Domain',
            'ExxerAI.Domain.Cortex': '00Domain',
            'ExxerAI.Domain.Nexus': '00Domain',
            'ExxerAI.Domain.CubeExplorer': '00Domain',
            'ExxerAI.Datastream': '02Infrastructure',
            'ExxerAI.Infrastructure.Test': '03UnitTests'
        }
        
        # Try known location first
        if project_name in project_locations:
            folder = project_locations[project_name]
            project_path = self.src_path / folder / project_name / f"{project_name}.csproj"
            if project_path.exists():
                return project_path
        
        # Search common locations
        search_patterns = [
            f"00Domain/{project_name}/{project_name}.csproj",
            f"01Application/{project_name}/{project_name}.csproj",
            f"02Infrastructure/{project_name}/{project_name}.csproj",
            f"03Infrastructure/{project_name}/{project_name}.csproj",
            f"04Api/{project_name}/{project_name}.csproj",
            f"{project_name}/{project_name}.csproj"
        ]
        
        for pattern in search_patterns:
            path = self.src_path / pattern
            if path.exists():
                return path
        
        # Last resort: search recursively
        for csproj in self.src_path.rglob(f"{project_name}.csproj"):
            if 'bin' not in str(csproj) and 'obj' not in str(csproj) and 'tests' not in str(csproj):
                return csproj
        
        return None
    
    def _backup_file(self, file_path: Path):
        """Backup a file before modifying."""
        if self.dry_run:
            return
        
        relative_path = file_path.relative_to(self.base_path)
        backup_path = self.backup_dir / relative_path
        backup_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(file_path, backup_path)
    
    def _indent_xml(self, elem, level=0):
        """Pretty print XML."""
        i = "\n" + level * "  "
        if len(elem):
            if not elem.text or not elem.text.strip():
                elem.text = i + "  "
            if not elem.tail or not elem.tail.strip():
                elem.tail = i
            for e in elem:
                self._indent_xml(e, level + 1)
            if not e.tail or not e.tail.strip():
                e.tail = i
        else:
            if level and (not elem.tail or not elem.tail.strip()):
                elem.tail = i
    
    def fix_dependencies(self, report_file: str):
        """Apply fixes based on smart analysis report."""
        report = self.load_analysis_report(report_file)
        
        print(f"\n{'DRY RUN MODE' if self.dry_run else 'APPLYING FIXES'}")
        print("=" * 50)
        print(f"Using Directory.Build.props injected namespaces: {len(report.get('directory_build_props_namespaces', []))}")
        
        # Add project references
        proj_refs = report.get('actions_needed', {}).get('project_references', {})
        if proj_refs:
            self.add_project_references(proj_refs)
        
        # Add package references
        pkg_refs = report.get('actions_needed', {}).get('package_references', {})
        if pkg_refs:
            self.add_package_references(pkg_refs)
        
        # Show types needing investigation
        investigate = report.get('actions_needed', {}).get('investigate', {})
        if investigate:
            print("\n=== Types Needing Manual Investigation ===")
            total_unknown = sum(len(types) for types in investigate.values())
            print(f"Total unknown types: {total_unknown}")
            
            # Show top projects with unknowns
            sorted_projects = sorted(investigate.items(), key=lambda x: len(x[1]), reverse=True)
            for project, types in sorted_projects[:5]:
                print(f"\n  {project} ({len(types)} unknown types):")
                for t in list(types)[:5]:
                    print(f"    - {t}")
                if len(types) > 5:
                    print(f"    ... and {len(types) - 5} more")
        
        # Print summary
        self._print_summary()
    
    def _print_summary(self):
        """Print summary of changes."""
        print("\n=== SUMMARY ===")
        if self.dry_run:
            print("DRY RUN - No actual changes were made")
        else:
            print(f"Backup directory: {self.backup_dir}")
        
        print(f"\nChanges {'that would be' if self.dry_run else ''} made:")
        print(f"  Added project references: {self.changes_made['added_project_refs']}")
        print(f"  Added package references: {self.changes_made['added_package_refs']}")
        print(f"  Files updated: {len(self.changes_made['updated_files'])}")
        
        if not self.dry_run:
            print("\n✅ Next steps:")
            print("  1. Run 'dotnet restore' to restore packages")
            print("  2. Run 'dotnet build' to verify fixes")
            print("  3. Investigate any remaining unknown types")
            print("\n⚠️  Note: GlobalUsings.cs files were NOT modified")
            print("  Namespaces are already injected by Directory.Build.props")


def main():
    parser = argparse.ArgumentParser(description='Smart dependency fixer')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--report', default='smart_dependency_analysis.json',
                       help='Path to the smart analysis report')
    parser.add_argument('--dry-run', action='store_true', default=True,
                       help='Run in dry-run mode (default: true)')
    parser.add_argument('--apply', action='store_true',
                       help='Actually apply the fixes')
    
    args = parser.parse_args()
    
    dry_run = not args.apply
    
    if not dry_run:
        # Skip confirmation in automated mode
        print("⚠️  Applying fixes to project files...")
    
    fixer = SmartDependencyFixer(args.base_path, dry_run=dry_run)
    fixer.fix_dependencies(args.report)


if __name__ == "__main__":
    main()