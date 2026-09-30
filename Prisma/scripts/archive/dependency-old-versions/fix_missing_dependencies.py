#!/usr/bin/env python3
"""
Fixes missing dependencies based on the analysis report.
- Removes unused global using statements
- Adds required namespaces to GlobalUsings.cs
- Adds project references to .csproj files
- Adds NuGet package references to .csproj files
"""

import os
import re
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Set, List, Dict, Tuple, Optional
from datetime import datetime
import argparse
import shutil


class DependencyFixer:
    """Fixes missing dependencies based on analysis report."""
    
    def __init__(self, base_path: str, dry_run: bool = True):
        self.base_path = Path(base_path)
        self.tests_path = self.base_path / "code" / "src" / "tests"
        self.src_path = self.base_path / "code" / "src"
        self.dry_run = dry_run
        
        # Track changes for summary
        self.changes_made = {
            'removed_usings': 0,
            'added_usings': 0,
            'added_project_refs': 0,
            'added_package_refs': 0,
            'updated_files': set()
        }
        
        if not dry_run:
            self.backup_dir = self.base_path / "scripts" / "dependency_fix_backups" / datetime.now().strftime("%Y%m%d_%H%M%S")
            self.backup_dir.mkdir(parents=True, exist_ok=True)
            print(f"Backup directory: {self.backup_dir}")
    
    def load_analysis_report(self, report_file: str) -> Dict:
        """Load the analysis report from JSON."""
        with open(report_file, 'r', encoding='utf-8') as f:
            return json.load(f)
    
    def remove_unused_global_usings(self, unused_usings: Dict[str, List[str]]):
        """Remove unused using statements from GlobalUsings.cs files."""
        print("\n=== Removing Unused Global Usings ===")
        
        for project_name, unused_list in unused_usings.items():
            # Find the GlobalUsings.cs file for this project
            globalusing_files = list(self.tests_path.rglob(f"{project_name}/GlobalUsings.cs"))
            
            if not globalusing_files:
                print(f"  Warning: Could not find GlobalUsings.cs for {project_name}")
                continue
                
            globalusing_file = globalusing_files[0]
            
            if self.dry_run:
                print(f"\n[DRY RUN] Would update: {globalusing_file}")
                print(f"  Would remove {len(unused_list)} unused usings:")
                for using in unused_list[:5]:  # Show first 5
                    print(f"    - global using {using};")
                if len(unused_list) > 5:
                    print(f"    ... and {len(unused_list) - 5} more")
            else:
                # Backup and update the file
                self._backup_file(globalusing_file)
                content = globalusing_file.read_text(encoding='utf-8')
                
                # Remove each unused using
                for using in unused_list:
                    # Match both regular and global using statements
                    pattern = rf'^\s*(?:global\s+)?using\s+{re.escape(using)}\s*;\s*$'
                    content = re.sub(pattern, '', content, flags=re.MULTILINE)
                
                # Clean up multiple blank lines
                content = re.sub(r'\n{3,}', '\n\n', content)
                
                # Write updated content
                globalusing_file.write_text(content, encoding='utf-8')
                self.changes_made['removed_usings'] += len(unused_list)
                self.changes_made['updated_files'].add(str(globalusing_file))
                
                print(f"  Updated: {globalusing_file}")
                print(f"  Removed {len(unused_list)} unused usings")
    
    def add_missing_namespaces(self, namespaces_by_project: Dict[str, List[str]]):
        """Add missing namespaces to GlobalUsings.cs files."""
        print("\n=== Adding Missing Namespaces ===")
        
        for project_name, namespaces in namespaces_by_project.items():
            # Find the project directory
            project_dirs = list(self.tests_path.rglob(project_name))
            if not project_dirs:
                print(f"  Warning: Could not find project directory for {project_name}")
                continue
                
            project_dir = project_dirs[0]
            globalusing_file = project_dir / "GlobalUsings.cs"
            
            # Get existing usings
            existing_usings = set()
            if globalusing_file.exists():
                content = globalusing_file.read_text(encoding='utf-8')
                existing_usings = set(re.findall(r'(?:global\s+)?using\s+([\w.]+)\s*;', content))
            else:
                # Create new GlobalUsings.cs if it doesn't exist
                content = f"// Global using directives for {project_name}\n// Generated on: {datetime.now().isoformat()}\n\n"
            
            # Filter out namespaces that are already present
            new_namespaces = [ns for ns in namespaces if ns not in existing_usings]
            
            if not new_namespaces:
                print(f"  {project_name}: All required namespaces already present")
                continue
            
            if self.dry_run:
                print(f"\n[DRY RUN] Would update: {globalusing_file}")
                print(f"  Would add {len(new_namespaces)} namespaces:")
                for ns in new_namespaces[:5]:
                    print(f"    + global using {ns};")
                if len(new_namespaces) > 5:
                    print(f"    ... and {len(new_namespaces) - 5} more")
            else:
                # Backup if file exists
                if globalusing_file.exists():
                    self._backup_file(globalusing_file)
                
                # Group namespaces by category
                system_ns = sorted([ns for ns in new_namespaces if ns.startswith('System')])
                microsoft_ns = sorted([ns for ns in new_namespaces if ns.startswith('Microsoft')])
                exxerai_ns = sorted([ns for ns in new_namespaces if ns.startswith('ExxerAI')])
                other_ns = sorted([ns for ns in new_namespaces if not any(ns.startswith(p) for p in ['System', 'Microsoft', 'ExxerAI'])])
                
                # Add new namespaces
                additions = []
                if system_ns:
                    additions.append("\n// System namespaces")
                    additions.extend([f"global using {ns};" for ns in system_ns])
                if microsoft_ns:
                    additions.append("\n// Microsoft namespaces")
                    additions.extend([f"global using {ns};" for ns in microsoft_ns])
                if exxerai_ns:
                    additions.append("\n// ExxerAI namespaces")
                    additions.extend([f"global using {ns};" for ns in exxerai_ns])
                if other_ns:
                    additions.append("\n// Third-party namespaces")
                    additions.extend([f"global using {ns};" for ns in other_ns])
                
                # Append to content
                if additions:
                    content = content.rstrip() + "\n" + "\n".join(additions) + "\n"
                    globalusing_file.write_text(content, encoding='utf-8')
                    
                    self.changes_made['added_usings'] += len(new_namespaces)
                    self.changes_made['updated_files'].add(str(globalusing_file))
                    
                    print(f"  Updated: {globalusing_file}")
                    print(f"  Added {len(new_namespaces)} namespaces")
    
    def add_project_references(self, project_refs_by_project: Dict[str, List[str]]):
        """Add missing project references to .csproj files."""
        print("\n=== Adding Project References ===")
        
        for project_name, required_projects in project_refs_by_project.items():
            # Find the project file
            project_files = list(self.tests_path.rglob(f"{project_name}/{project_name}.csproj"))
            if not project_files:
                # Try alternative patterns
                project_files = list(self.tests_path.rglob(f"{project_name}/*.csproj"))
                
            if not project_files:
                print(f"  Warning: Could not find .csproj file for {project_name}")
                continue
                
            project_file = project_files[0]
            
            # Parse existing project references
            tree = ET.parse(project_file)
            root = tree.getroot()
            
            existing_refs = set()
            for ref in root.findall(".//ProjectReference"):
                include = ref.get('Include')
                if include:
                    # Extract project name from path
                    ref_name = Path(include).stem
                    existing_refs.add(ref_name)
            
            # Filter out already existing references
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
                # Backup the file
                self._backup_file(project_file)
                
                # Find or create ItemGroup for ProjectReference
                item_group = None
                for group in root.findall("ItemGroup"):
                    if group.find("ProjectReference") is not None:
                        item_group = group
                        break
                
                if item_group is None:
                    item_group = ET.SubElement(root, "ItemGroup")
                
                # Add new project references
                for ref_project in new_refs:
                    # Find the actual project path
                    ref_path = self._find_project_path(ref_project)
                    if ref_path:
                        relative_path = os.path.relpath(ref_path, project_file.parent).replace('\\', '/')
                        proj_ref = ET.SubElement(item_group, "ProjectReference")
                        proj_ref.set("Include", relative_path)
                        self.changes_made['added_project_refs'] += 1
                
                # Format and save
                self._indent_xml(root)
                tree.write(project_file, encoding='utf-8', xml_declaration=True)
                self.changes_made['updated_files'].add(str(project_file))
                
                print(f"  Updated: {project_file}")
                print(f"  Added {len(new_refs)} project references")
    
    def add_package_references(self, package_refs_by_project: Dict[str, List[str]]):
        """Add missing NuGet package references to .csproj files."""
        print("\n=== Adding NuGet Package References ===")
        
        for project_name, required_packages in package_refs_by_project.items():
            # Find the project file
            project_files = list(self.tests_path.rglob(f"{project_name}/{project_name}.csproj"))
            if not project_files:
                project_files = list(self.tests_path.rglob(f"{project_name}/*.csproj"))
                
            if not project_files:
                print(f"  Warning: Could not find .csproj file for {project_name}")
                continue
                
            project_file = project_files[0]
            
            # Parse existing package references
            tree = ET.parse(project_file)
            root = tree.getroot()
            
            existing_packages = set()
            for ref in root.findall(".//PackageReference"):
                include = ref.get('Include')
                if include:
                    existing_packages.add(include)
            
            # Filter out already existing packages
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
                # Backup the file
                self._backup_file(project_file)
                
                # Find or create ItemGroup for PackageReference
                item_group = None
                for group in root.findall("ItemGroup"):
                    if group.find("PackageReference") is not None:
                        item_group = group
                        break
                
                if item_group is None:
                    item_group = ET.SubElement(root, "ItemGroup")
                    item_group.set("Label", "NuGet Packages")
                
                # Add new package references
                for package in new_packages:
                    pkg_ref = ET.SubElement(item_group, "PackageReference")
                    pkg_ref.set("Include", package)
                    # Note: Version will be managed by Directory.Build.props or needs to be specified
                    self.changes_made['added_package_refs'] += 1
                
                # Format and save
                self._indent_xml(root)
                tree.write(project_file, encoding='utf-8', xml_declaration=True)
                self.changes_made['updated_files'].add(str(project_file))
                
                print(f"  Updated: {project_file}")
                print(f"  Added {len(new_packages)} package references")
    
    def _find_project_path(self, project_name: str) -> Optional[Path]:
        """Find the actual path to a project file."""
        # Search in common locations
        search_paths = [
            self.src_path / "Core" / project_name / f"{project_name}.csproj",
            self.src_path / "Infrastructure" / project_name / f"{project_name}.csproj",
            self.src_path / "Application" / project_name / f"{project_name}.csproj",
            self.src_path / "Api" / project_name / f"{project_name}.csproj",
            self.src_path / project_name / f"{project_name}.csproj",
        ]
        
        for path in search_paths:
            if path.exists():
                return path
        
        # Fallback: search recursively
        for csproj in self.src_path.rglob(f"{project_name}.csproj"):
            if 'bin' not in str(csproj) and 'obj' not in str(csproj):
                return csproj
                
        return None
    
    def _backup_file(self, file_path: Path):
        """Backup a file before modifying it."""
        if self.dry_run:
            return
            
        relative_path = file_path.relative_to(self.base_path)
        backup_path = self.backup_dir / relative_path
        backup_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(file_path, backup_path)
    
    def _indent_xml(self, elem, level=0):
        """Indent XML elements for pretty printing."""
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
    
    def fix_all_dependencies(self, report_file: str):
        """Fix all dependencies based on the analysis report."""
        report = self.load_analysis_report(report_file)
        
        print(f"\n{'DRY RUN MODE' if self.dry_run else 'APPLYING FIXES'}")
        print("=" * 50)
        
        # 1. Remove unused global usings
        if report.get('unused_global_usings'):
            self.remove_unused_global_usings(report['unused_global_usings'])
        
        # 2. Add missing namespaces
        if report.get('summary', {}).get('namespaces_to_add'):
            self.add_missing_namespaces(report['summary']['namespaces_to_add'])
        
        # 3. Add project references
        if report.get('summary', {}).get('project_references_needed'):
            self.add_project_references(report['summary']['project_references_needed'])
        
        # 4. Add package references
        if report.get('summary', {}).get('nuget_packages_needed'):
            self.add_package_references(report['summary']['nuget_packages_needed'])
        
        # Print summary
        self._print_summary()
    
    def _print_summary(self):
        """Print a summary of changes made."""
        print("\n=== SUMMARY ===")
        if self.dry_run:
            print("DRY RUN - No actual changes were made")
        else:
            print(f"Backup directory: {self.backup_dir}")
        
        print(f"\nChanges {'that would be' if self.dry_run else ''} made:")
        print(f"  Removed unused usings: {self.changes_made['removed_usings']}")
        print(f"  Added namespaces: {self.changes_made['added_usings']}")
        print(f"  Added project references: {self.changes_made['added_project_refs']}")
        print(f"  Added package references: {self.changes_made['added_package_refs']}")
        print(f"  Files updated: {len(self.changes_made['updated_files'])}")
        
        if not self.dry_run and self.changes_made['added_package_refs'] > 0:
            print("\n⚠️  Note: Package versions are not specified. You may need to:")
            print("  1. Add version numbers to the PackageReference entries")
            print("  2. Or manage versions centrally in Directory.Build.props")
            print("  3. Run 'dotnet restore' to restore packages")


def main():
    parser = argparse.ArgumentParser(description='Fix missing dependencies based on analysis report')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--report', default='missing_dependencies_analysis.json',
                       help='Path to the analysis report JSON file')
    parser.add_argument('--dry-run', action='store_true', default=True,
                       help='Run in dry-run mode (default: true)')
    parser.add_argument('--apply', action='store_true',
                       help='Actually apply the fixes (disables dry-run)')
    
    args = parser.parse_args()
    
    # If --apply is specified, disable dry-run
    dry_run = not args.apply
    
    if not dry_run:
        response = input("⚠️  This will modify your project files. Are you sure? (yes/no): ")
        if response.lower() != 'yes':
            print("Aborted.")
            return
    
    fixer = DependencyFixer(args.base_path, dry_run=dry_run)
    fixer.fix_all_dependencies(args.report)


if __name__ == "__main__":
    main()