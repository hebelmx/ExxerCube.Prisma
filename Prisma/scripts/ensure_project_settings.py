#!/usr/bin/env python3
"""
Ensures all production projects have ImplicitUsings and Nullable enabled.
"""

import os
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import List, Tuple
from datetime import datetime
import shutil
import argparse


class ProjectSettingsChecker:
    """Checks and fixes ImplicitUsings and Nullable settings in production projects."""
    
    def __init__(self, base_path: str, dry_run: bool = True):
        self.base_path = Path(base_path)
        self.src_path = self.base_path / "code" / "src"
        self.dry_run = dry_run
        
        if not dry_run:
            self.backup_dir = self.base_path / "scripts" / "project_settings_backups" / datetime.now().strftime("%Y%m%d_%H%M%S")
            self.backup_dir.mkdir(parents=True, exist_ok=True)
            print(f"Backup directory: {self.backup_dir}")
    
    def find_production_projects(self) -> List[Path]:
        """Find all production .csproj files (excluding test projects)."""
        projects = []
        
        for csproj in self.src_path.rglob("*.csproj"):
            # Skip test projects
            if 'tests' in csproj.parts or 'Test' in str(csproj):
                continue
            # Skip bin and obj folders
            if 'bin' in csproj.parts or 'obj' in csproj.parts:
                continue
            # Skip sample code
            if 'SampleCode' in str(csproj):
                continue
                
            projects.append(csproj)
        
        return sorted(projects)
    
    def check_project(self, project_path: Path) -> Tuple[bool, bool, str]:
        """
        Check if a project has ImplicitUsings and Nullable enabled.
        Returns: (has_implicit_usings, has_nullable, error_message)
        """
        try:
            tree = ET.parse(project_path)
            root = tree.getroot()
            
            has_implicit_usings = False
            has_nullable = False
            
            # Check all PropertyGroup elements
            for prop_group in root.findall(".//PropertyGroup"):
                implicit_elem = prop_group.find("ImplicitUsings")
                if implicit_elem is not None and implicit_elem.text == "enable":
                    has_implicit_usings = True
                
                nullable_elem = prop_group.find("Nullable")
                if nullable_elem is not None and nullable_elem.text == "enable":
                    has_nullable = True
            
            return has_implicit_usings, has_nullable, ""
            
        except Exception as e:
            return False, False, str(e)
    
    def fix_project(self, project_path: Path, add_implicit_usings: bool, add_nullable: bool):
        """Add missing ImplicitUsings and/or Nullable settings to a project."""
        if not add_implicit_usings and not add_nullable:
            return
        
        if not self.dry_run:
            # Backup the file
            self._backup_file(project_path)
        
        try:
            tree = ET.parse(project_path)
            root = tree.getroot()
            
            # Find the first PropertyGroup or create one
            prop_group = None
            for pg in root.findall("PropertyGroup"):
                # Prefer PropertyGroup with TargetFramework
                if pg.find("TargetFramework") is not None:
                    prop_group = pg
                    break
            
            if prop_group is None:
                prop_group = root.find("PropertyGroup")
            
            if prop_group is None:
                prop_group = ET.SubElement(root, "PropertyGroup")
            
            # Add ImplicitUsings if needed
            if add_implicit_usings and prop_group.find("ImplicitUsings") is None:
                # Add after LangVersion if it exists, otherwise after TargetFramework
                lang_version = prop_group.find("LangVersion")
                target_framework = prop_group.find("TargetFramework")
                
                implicit_elem = ET.Element("ImplicitUsings")
                implicit_elem.text = "enable"
                
                if lang_version is not None:
                    # Insert after LangVersion
                    index = list(prop_group).index(lang_version) + 1
                elif target_framework is not None:
                    # Insert after TargetFramework
                    index = list(prop_group).index(target_framework) + 1
                else:
                    # Just append
                    index = len(list(prop_group))
                
                prop_group.insert(index, implicit_elem)
            
            # Add Nullable if needed
            if add_nullable and prop_group.find("Nullable") is None:
                # Add after ImplicitUsings if it exists, otherwise after LangVersion
                implicit_usings = prop_group.find("ImplicitUsings")
                lang_version = prop_group.find("LangVersion")
                
                nullable_elem = ET.Element("Nullable")
                nullable_elem.text = "enable"
                
                if implicit_usings is not None:
                    # Insert after ImplicitUsings
                    index = list(prop_group).index(implicit_usings) + 1
                elif lang_version is not None:
                    # Insert after LangVersion
                    index = list(prop_group).index(lang_version) + 1
                else:
                    # Just append
                    index = len(list(prop_group))
                
                prop_group.insert(index, nullable_elem)
            
            if not self.dry_run:
                # Save the file
                self._indent_xml(root)
                tree.write(project_path, encoding='utf-8', xml_declaration=True)
                print(f"  Updated: {project_path.name}")
            
        except Exception as e:
            print(f"  ERROR updating {project_path.name}: {e}")
    
    def _backup_file(self, file_path: Path):
        """Backup a file before modifying."""
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
    
    def check_and_fix_all_projects(self):
        """Check and fix all production projects."""
        projects = self.find_production_projects()
        
        print(f"\n{'DRY RUN MODE' if self.dry_run else 'APPLYING FIXES'}")
        print("=" * 60)
        print(f"Found {len(projects)} production projects\n")
        
        projects_needing_implicit = []
        projects_needing_nullable = []
        projects_with_errors = []
        
        # Check all projects
        for project in projects:
            has_implicit, has_nullable, error = self.check_project(project)
            project_name = project.relative_to(self.src_path)
            
            if error:
                projects_with_errors.append((project_name, error))
            else:
                needs_implicit = not has_implicit
                needs_nullable = not has_nullable
                
                if needs_implicit or needs_nullable:
                    status_parts = []
                    if needs_implicit:
                        status_parts.append("ImplicitUsings")
                        projects_needing_implicit.append(project)
                    if needs_nullable:
                        status_parts.append("Nullable")
                        projects_needing_nullable.append(project)
                    
                    print(f"  {project_name}")
                    print(f"    Missing: {', '.join(status_parts)}")
                    
                    if not self.dry_run:
                        self.fix_project(project, needs_implicit, needs_nullable)
        
        # Summary
        print("\n" + "=" * 60)
        print("SUMMARY")
        print(f"Total production projects: {len(projects)}")
        print(f"Projects missing ImplicitUsings: {len(projects_needing_implicit)}")
        print(f"Projects missing Nullable: {len(projects_needing_nullable)}")
        print(f"Projects with errors: {len(projects_with_errors)}")
        
        if projects_with_errors:
            print("\nProjects with errors:")
            for proj, error in projects_with_errors:
                print(f"  {proj}: {error}")
        
        if self.dry_run:
            print("\n[DRY RUN] No changes were made")
            print("To apply changes, run with: --apply")
        else:
            print(f"\n✅ Changes applied!")
            print(f"Backup directory: {self.backup_dir}")
            print("\nNext steps:")
            print("  1. Run 'dotnet build' to verify changes")
            print("  2. Fix any new nullable reference warnings")


def main():
    parser = argparse.ArgumentParser(description='Ensure production projects have ImplicitUsings and Nullable enabled')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--dry-run', action='store_true', default=True,
                       help='Run in dry-run mode (default: true)')
    parser.add_argument('--apply', action='store_true',
                       help='Actually apply the fixes')
    
    args = parser.parse_args()
    
    dry_run = not args.apply
    
    checker = ProjectSettingsChecker(args.base_path, dry_run=dry_run)
    checker.check_and_fix_all_projects()


if __name__ == "__main__":
    main()