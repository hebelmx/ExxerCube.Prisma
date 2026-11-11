#!/usr/bin/env python3
"""
Fixes missing dependencies for Infrastructure projects (Datastream and Gatekeeper).
These projects have global usings working correctly, but need package/project references.
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


class InfrastructureProjectFixer:
    """Fixes Infrastructure project dependencies."""
    
    def __init__(self, base_path: str, dry_run: bool = True):
        self.base_path = Path(base_path)
        self.src_path = self.base_path / "code" / "src"
        self.infrastructure_path = self.src_path / "Infrastructure"
        self.dry_run = dry_run
        
        # Projects to fix
        self.target_projects = {
            'ExxerAI.Datastream': self.infrastructure_path / 'ExxerAI.Datastream' / 'ExxerAI.Datastream.csproj',
            'ExxerAI.Gatekeeper': self.infrastructure_path / 'ExxerAI.Gatekeeper' / 'ExxerAI.Gatekeeper.csproj'
        }
        
        # Known package requirements based on missing types
        self.required_packages = {
            'ExxerAI.Datastream': [
                'Microsoft.Extensions.Options',
                'Microsoft.Extensions.Configuration',
                'Microsoft.Extensions.Configuration.Abstractions',
                'System.Collections.Concurrent'  # For ConcurrentDictionary
            ],
            'ExxerAI.Gatekeeper': [
                'Microsoft.Extensions.Options',
                'Microsoft.Extensions.Configuration',
                'Microsoft.Extensions.Configuration.Abstractions',
                'Microsoft.Extensions.Http'
            ]
        }
        
        # Known project references needed
        self.required_projects = {
            'ExxerAI.Datastream': [
                'ExxerAI.Domain',
                'ExxerAI.Application'
            ],
            'ExxerAI.Gatekeeper': [
                'ExxerAI.Domain',
                'ExxerAI.Application'
            ]
        }
        
        if not dry_run:
            self.backup_dir = self.base_path / "scripts" / "infrastructure_fix_backups" / datetime.now().strftime("%Y%m%d_%H%M%S")
            self.backup_dir.mkdir(parents=True, exist_ok=True)
            print(f"Backup directory: {self.backup_dir}")
    
    def analyze_project(self, project_name: str, project_path: Path) -> Dict:
        """Analyze a project for missing dependencies."""
        print(f"\n=== Analyzing {project_name} ===")
        
        if not project_path.exists():
            print(f"  ERROR: Project file not found: {project_path}")
            return {}
        
        # Parse existing references
        tree = ET.parse(project_path)
        root = tree.getroot()
        
        # Get existing packages
        existing_packages = set()
        for ref in root.findall(".//PackageReference"):
            include = ref.get('Include')
            if include:
                existing_packages.add(include)
        
        # Get existing project references
        existing_projects = set()
        for ref in root.findall(".//ProjectReference"):
            include = ref.get('Include')
            if include:
                # Extract project name from path
                proj_name = Path(include).stem
                existing_projects.add(proj_name)
        
        # Determine what's missing
        missing_packages = [pkg for pkg in self.required_packages.get(project_name, []) 
                          if pkg not in existing_packages]
        missing_projects = [proj for proj in self.required_projects.get(project_name, []) 
                          if proj not in existing_projects]
        
        return {
            'existing_packages': list(existing_packages),
            'existing_projects': list(existing_projects),
            'missing_packages': missing_packages,
            'missing_projects': missing_projects
        }
    
    def add_references(self, project_name: str, project_path: Path, analysis: Dict):
        """Add missing references to a project."""
        if not analysis['missing_packages'] and not analysis['missing_projects']:
            print(f"  {project_name}: All required references already present")
            return
        
        if self.dry_run:
            print(f"\n[DRY RUN] Would update: {project_path}")
            if analysis['missing_packages']:
                print(f"  Would add {len(analysis['missing_packages'])} package references:")
                for pkg in analysis['missing_packages']:
                    print(f"    + {pkg}")
            if analysis['missing_projects']:
                print(f"  Would add {len(analysis['missing_projects'])} project references:")
                for proj in analysis['missing_projects']:
                    print(f"    + {proj}")
        else:
            # Backup the file
            self._backup_file(project_path)
            
            # Parse the project file
            tree = ET.parse(project_path)
            root = tree.getroot()
            
            # Add missing package references
            if analysis['missing_packages']:
                # Find or create ItemGroup for packages
                pkg_group = None
                for group in root.findall("ItemGroup"):
                    if group.find("PackageReference") is not None:
                        pkg_group = group
                        break
                
                if pkg_group is None:
                    pkg_group = ET.SubElement(root, "ItemGroup")
                    pkg_group.set("Label", "Package References")
                
                for package in analysis['missing_packages']:
                    pkg_ref = ET.SubElement(pkg_group, "PackageReference")
                    pkg_ref.set("Include", package)
            
            # Add missing project references
            if analysis['missing_projects']:
                # Find or create ItemGroup for projects
                proj_group = None
                for group in root.findall("ItemGroup"):
                    if group.find("ProjectReference") is not None:
                        proj_group = group
                        break
                
                if proj_group is None:
                    proj_group = ET.SubElement(root, "ItemGroup")
                    proj_group.set("Label", "Project References")
                
                for project in analysis['missing_projects']:
                    # Find the actual project path
                    proj_path = self._find_project_path(project)
                    if proj_path:
                        relative_path = os.path.relpath(proj_path, project_path.parent).replace('\\', '/')
                        proj_ref = ET.SubElement(proj_group, "ProjectReference")
                        proj_ref.set("Include", relative_path)
            
            # Save the file
            self._indent_xml(root)
            tree.write(project_path, encoding='utf-8', xml_declaration=True)
            
            print(f"  Updated: {project_path}")
            if analysis['missing_packages']:
                print(f"  Added {len(analysis['missing_packages'])} package references")
            if analysis['missing_projects']:
                print(f"  Added {len(analysis['missing_projects'])} project references")
    
    def _find_project_path(self, project_name: str) -> Optional[Path]:
        """Find the actual path to a project file."""
        # Search in common locations
        search_patterns = [
            f"Core/{project_name}/{project_name}.csproj",
            f"Infrastructure/{project_name}/{project_name}.csproj",
            f"Application/{project_name}/{project_name}.csproj",
        ]
        
        for pattern in search_patterns:
            path = self.src_path / pattern
            if path.exists():
                return path
        
        # Fallback: search recursively
        for csproj in self.src_path.rglob(f"{project_name}.csproj"):
            if 'tests' not in str(csproj) and 'bin' not in str(csproj) and 'obj' not in str(csproj):
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
    
    def fix_all_projects(self):
        """Fix all infrastructure projects."""
        print(f"\n{'DRY RUN MODE' if self.dry_run else 'APPLYING FIXES'}")
        print("=" * 50)
        print("Fixing Infrastructure Projects (Datastream and Gatekeeper)")
        print("Note: These projects have global usings working correctly.")
        print("We're only adding missing package/project references.")
        
        for project_name, project_path in self.target_projects.items():
            analysis = self.analyze_project(project_name, project_path)
            if analysis:
                self.add_references(project_name, project_path, analysis)
        
        print("\n=== SUMMARY ===")
        if self.dry_run:
            print("DRY RUN - No actual changes were made")
            print("\nTo apply changes, run with: --apply")
        else:
            print(f"Backup directory: {self.backup_dir}")
            print("\n✅ Next steps:")
            print("  1. Run 'dotnet restore' to restore packages")
            print("  2. Run 'dotnet build' on these projects")
            print("  3. Fix any remaining type definition issues")
        
        print("\n⚠️  Note: Many types referenced in these projects don't exist yet.")
        print("  You may need to:")
        print("  - Create missing domain types (enums, entities)")
        print("  - Update interface implementations")
        print("  - Complete the evocative architecture migration")


def main():
    parser = argparse.ArgumentParser(description='Fix Infrastructure project dependencies')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--dry-run', action='store_true', default=True,
                       help='Run in dry-run mode (default: true)')
    parser.add_argument('--apply', action='store_true',
                       help='Actually apply the fixes')
    
    args = parser.parse_args()
    
    dry_run = not args.apply
    
    if not dry_run:
        response = input("⚠️  This will modify Infrastructure project files. Are you sure? (yes/no): ")
        if response.lower() != 'yes':
            print("Aborted.")
            return
    
    fixer = InfrastructureProjectFixer(args.base_path, dry_run=dry_run)
    fixer.fix_all_projects()


if __name__ == "__main__":
    main()