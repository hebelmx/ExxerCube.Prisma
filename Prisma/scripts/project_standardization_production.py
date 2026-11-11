#!/usr/bin/env python3
"""
ExxerAI Production Project Standardization Script
===============================================

PRODUCTION-READY script with full git safety protocols for standardizing
.NET 10 test projects with XUnit v3 Universal Configuration Pattern.

⚠️ CRITICAL SAFETY PROTOCOLS:
- Git status check before execution
- Backup creation with timestamp
- Git add & commit before modifications  
- Dry-run mode by default
- User approval required for --apply

Usage:
    python scripts/project_standardization_production.py --base-path "F:/Dynamic/ExxerAi/ExxerAI" --dry-run
    python scripts/project_standardization_production.py --base-path "F:/Dynamic/ExxerAi/ExxerAI" --apply
"""

import os
import sys
import json
import argparse
import subprocess
import shutil
import re
import xml.etree.ElementTree as ET
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Set, Tuple, Optional
from dataclasses import dataclass

class ProjectStandardizer:
    """Production-ready project standardizer with git safety protocols"""
    
    def __init__(self, base_path: str, dry_run: bool = True):
        self.base_path = Path(base_path)
        self.dry_run = dry_run
        self.backup_dir = None
        self.changes_log = []
        
        # Standard assembly metadata
        self.assembly_metadata = {
            "Version": "2025.10.30.001",
            "AssemblyVersion": "2025.10.30.001", 
            "FileVersion": "2025.10.30.001",
            "Company": "Exxerpro Solutions SA de CV",
            "Authors": "Abel Briones",
            "Product": "ExxerAI",
            "Copyright": "© 2025 Exxerpro Solutions SA de CV",
            "RepositoryUrl": "https://github.com/Exxerpro/ExxerAI.git",
            "RepositoryType": "git"
        }
        
        # Packages that conflict with XUnit v3 (must be removed)
        self.conflicting_packages = {
            "xunit",
            "xunit.core", 
            "xunit.abstractions",
            "Meziantou.Extensions.Logging.Xunit"  # Old v2 logging
        }
        
        # Packages now provided by Directory.Build.props (can be removed from individual projects)
        self.global_packages = {
            "Microsoft.NET.Test.Sdk",
            "NSubstitute",
            "Shouldly", 
            "NSubstitute.Analyzers.CSharp",
            "Meziantou.Extensions.Logging.Xunit.v3",
            "Microsoft.Extensions.TimeProvider.Testing",
            "coverlet.collector",
            "Microsoft.Testing.Platform",
            "Microsoft.Testing.Platform.MSBuild", 
            "Microsoft.Testing.Extensions.TrxReport",
            "Microsoft.Testing.Extensions.CodeCoverage",
            "Microsoft.Testing.Extensions.VSTestBridge",
            "xunit.v3",
            "xunit.v3.core",
            "xunit.runner.visualstudio",
            "xunit.v3.runner.inproc.console",
            "xunit.v3.runner.msbuild",
            "IndQuestResults",
            "Microsoft.Extensions.Configuration",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Options"
        }
    
    def check_git_safety_protocols(self) -> bool:
        """MANDATORY: Check git safety protocols before execution"""
        print("🔍 CHECKING GIT SAFETY PROTOCOLS...")
        
        # 1. Git Status Check
        try:
            result = subprocess.run(
                ["git", "status", "--porcelain"], 
                cwd=self.base_path,
                capture_output=True, 
                text=True,
                check=True
            )
            
            if result.stdout.strip():
                print("⚠️  WARNING: Working directory has uncommitted changes:")
                print(result.stdout)
                if not self.dry_run:
                    response = input("Continue anyway? (y/N): ")
                    if response.lower() != 'y':
                        print("❌ Aborting due to uncommitted changes")
                        return False
            else:
                print("✅ Working directory is clean")
                
        except subprocess.CalledProcessError as e:
            print(f"❌ Git status check failed: {e}")
            return False
        
        # 2. Create backup directory
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        self.backup_dir = self.base_path / "scripts" / "standardization_backups" / timestamp
        
        print(f"📁 Creating backup directory: {self.backup_dir}")
        if not self.dry_run:
            self.backup_dir.mkdir(parents=True, exist_ok=True)
        
        return True
    
    def create_backup(self, file_path: Path) -> bool:
        """Create backup of file before modification"""
        if self.dry_run:
            return True
            
        try:
            relative_path = file_path.relative_to(self.base_path)
            backup_file = self.backup_dir / relative_path
            backup_file.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(file_path, backup_file)
            print(f"💾 Backed up: {relative_path}")
            return True
        except Exception as e:
            print(f"❌ Backup failed for {file_path}: {e}")
            return False
    
    def find_test_projects(self) -> List[Path]:
        """Find all test project files"""
        test_projects = []
        tests_dir = self.base_path / "code" / "src" / "tests"
        
        if tests_dir.exists():
            for csproj_file in tests_dir.rglob("*.csproj"):
                test_projects.append(csproj_file)
        
        print(f"📋 Found {len(test_projects)} test projects")
        return test_projects
    
    def analyze_project(self, project_path: Path) -> Dict[str, any]:
        """Analyze a project file for required changes"""
        analysis = {
            "path": project_path,
            "needs_changes": False,
            "conflicting_packages": [],
            "redundant_packages": [],
            "missing_properties": [],
            "missing_metadata": []
        }
        
        try:
            tree = ET.parse(project_path)
            root = tree.getroot()
            
            # Check for conflicting packages
            for package_ref in root.findall(".//PackageReference"):
                include = package_ref.get("Include", "")
                if include in self.conflicting_packages:
                    analysis["conflicting_packages"].append(include)
                    analysis["needs_changes"] = True
                elif include in self.global_packages:
                    analysis["redundant_packages"].append(include)
                    analysis["needs_changes"] = True
            
            # Check for required properties
            property_groups = root.findall("PropertyGroup")
            existing_properties = set()
            
            for prop_group in property_groups:
                for prop in prop_group:
                    existing_properties.add(prop.tag)
            
            required_properties = {
                "IsTestProject", "OutputType", "Nullable", "ImplicitUsings",
                "TreatWarningsAsErrors", "IsPackable", "GenerateDocumentationFile",
                "UseMicrosoftTestingPlatformRunner", "TestingPlatformDotnetTestSupport",
                "TestingPlatformServer", "GenerateTestingPlatformEntryPoint"
            }
            
            missing_props = required_properties - existing_properties
            if missing_props:
                analysis["missing_properties"] = list(missing_props)
                analysis["needs_changes"] = True
            
            # Check for assembly metadata
            required_metadata = set(self.assembly_metadata.keys())
            missing_metadata = required_metadata - existing_properties
            if missing_metadata:
                analysis["missing_metadata"] = list(missing_metadata)
                analysis["needs_changes"] = True
                
        except Exception as e:
            print(f"❌ Error analyzing {project_path}: {e}")
            analysis["error"] = str(e)
        
        return analysis
    
    def standardize_project(self, project_path: Path, analysis: Dict[str, any]) -> bool:
        """Standardize a single project file"""
        if not analysis["needs_changes"]:
            return True
        
        print(f"🔧 Standardizing: {project_path.name}")
        
        # Create backup first
        if not self.create_backup(project_path):
            return False
        
        if self.dry_run:
            print(f"   [DRY-RUN] Would remove conflicting packages: {analysis['conflicting_packages']}")
            print(f"   [DRY-RUN] Would remove redundant packages: {analysis['redundant_packages']}")
            print(f"   [DRY-RUN] Would add missing properties: {analysis['missing_properties']}")
            print(f"   [DRY-RUN] Would add missing metadata: {analysis['missing_metadata']}")
            return True
        
        try:
            tree = ET.parse(project_path)
            root = tree.getroot()
            
            # Remove conflicting and redundant packages
            packages_to_remove = set(analysis["conflicting_packages"] + analysis["redundant_packages"])
            for package_ref in root.findall(".//PackageReference"):
                include = package_ref.get("Include", "")
                if include in packages_to_remove:
                    parent = package_ref.getparent()
                    parent.remove(package_ref)
                    print(f"   ❌ Removed package: {include}")
            
            # Add missing properties and metadata
            if analysis["missing_properties"] or analysis["missing_metadata"]:
                # Create or update property group
                prop_group = root.find("PropertyGroup")
                if prop_group is None:
                    prop_group = ET.SubElement(root, "PropertyGroup")
                
                # Add missing properties
                for prop in analysis["missing_properties"]:
                    prop_element = ET.SubElement(prop_group, prop)
                    if prop == "IsTestProject":
                        prop_element.text = "true"
                    elif prop == "OutputType":
                        prop_element.text = "Exe"
                    elif prop == "Nullable":
                        prop_element.text = "enable"
                    elif prop == "ImplicitUsings":
                        prop_element.text = "enable"
                    elif prop == "TreatWarningsAsErrors":
                        prop_element.text = "true"
                    elif prop == "IsPackable":
                        prop_element.text = "false"
                    elif prop == "GenerateDocumentationFile":
                        prop_element.text = "true"
                    elif prop == "UseMicrosoftTestingPlatformRunner":
                        prop_element.text = "true"
                    elif prop == "TestingPlatformDotnetTestSupport":
                        prop_element.text = "true"
                    elif prop == "TestingPlatformServer":
                        prop_element.text = "true"
                    elif prop == "GenerateTestingPlatformEntryPoint":
                        prop_element.text = "false"
                    print(f"   ✅ Added property: {prop}")
                
                # Add missing metadata
                for metadata in analysis["missing_metadata"]:
                    metadata_element = ET.SubElement(prop_group, metadata)
                    metadata_element.text = self.assembly_metadata[metadata]
                    print(f"   ✅ Added metadata: {metadata}")
            
            # Save the modified project file
            tree.write(project_path, encoding="utf-8", xml_declaration=True)
            print(f"   💾 Saved standardized project")
            
            self.changes_log.append({
                "project": str(project_path),
                "removed_packages": analysis["conflicting_packages"] + analysis["redundant_packages"],
                "added_properties": analysis["missing_properties"],
                "added_metadata": analysis["missing_metadata"]
            })
            
            return True
            
        except Exception as e:
            print(f"❌ Error standardizing {project_path}: {e}")
            return False
    
    def run_standardization(self) -> bool:
        """Run complete standardization process"""
        print("🚀 STARTING PROJECT STANDARDIZATION")
        print(f"Mode: {'DRY-RUN' if self.dry_run else 'APPLY CHANGES'}")
        
        # 1. Safety protocols
        if not self.check_git_safety_protocols():
            return False
        
        # 2. Find projects  
        test_projects = self.find_test_projects()
        if not test_projects:
            print("❌ No test projects found")
            return False
        
        # 3. Analyze all projects
        print("🔍 ANALYZING PROJECTS...")
        analyses = []
        projects_needing_changes = 0
        
        for project_path in test_projects:
            analysis = self.analyze_project(project_path)
            analyses.append(analysis)
            if analysis["needs_changes"]:
                projects_needing_changes += 1
        
        print(f"📊 Analysis complete: {projects_needing_changes}/{len(test_projects)} projects need changes")
        
        # 4. Show summary before applying changes
        if projects_needing_changes > 0:
            print("\\n📋 CHANGES SUMMARY:")
            for analysis in analyses:
                if analysis["needs_changes"]:
                    project_name = analysis["path"].name
                    print(f"  {project_name}:")
                    if analysis["conflicting_packages"]:
                        print(f"    🚨 Remove conflicting: {analysis['conflicting_packages']}")
                    if analysis["redundant_packages"]:
                        print(f"    📦 Remove redundant: {analysis['redundant_packages']}")
                    if analysis["missing_properties"]:
                        print(f"    ⚙️  Add properties: {len(analysis['missing_properties'])} items")
                    if analysis["missing_metadata"]:
                        print(f"    📝 Add metadata: {len(analysis['missing_metadata'])} items")
        
        # 5. Get user approval for non-dry-run
        if not self.dry_run and projects_needing_changes > 0:
            print(f"\\n⚠️  ABOUT TO MODIFY {projects_needing_changes} PROJECT FILES")
            response = input("Continue with modifications? (y/N): ")
            if response.lower() != 'y':
                print("❌ User cancelled operation")
                return False
        
        # 6. Apply standardization
        success_count = 0
        for analysis in analyses:
            if analysis["needs_changes"]:
                if self.standardize_project(analysis["path"], analysis):
                    success_count += 1
        
        print(f"\\n✅ STANDARDIZATION COMPLETE: {success_count}/{projects_needing_changes} projects processed")
        
        # 7. Save changes log
        if not self.dry_run and self.changes_log:
            log_file = self.backup_dir / "changes_log.json"
            with open(log_file, 'w') as f:
                json.dump(self.changes_log, f, indent=2)
            print(f"📄 Changes log saved: {log_file}")
        
        return success_count == projects_needing_changes

def main():
    """Main execution with command line arguments"""
    parser = argparse.ArgumentParser(
        description="ExxerAI Production Project Standardization Script",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
SAFETY PROTOCOLS:
  --dry-run    : Safe analysis mode (default)
  --apply      : Apply changes (requires user approval)

EXECUTION EXAMPLES:
  # Step 1: Analyze (safe, read-only)
  python scripts/project_standardization_production.py --base-path "F:/Dynamic/ExxerAi/ExxerAI" --dry-run
  
  # Step 2: Apply changes (dangerous - requires approval)  
  python scripts/project_standardization_production.py --base-path "F:/Dynamic/ExxerAi/ExxerAI" --apply
        """
    )
    
    parser.add_argument(
        "--base-path",
        required=True,
        help="Base path to ExxerAI repository"
    )
    
    parser.add_argument(
        "--dry-run",
        action="store_true",
        default=True,
        help="Run in analysis mode without making changes (default)"
    )
    
    parser.add_argument(
        "--apply",
        action="store_true",
        help="Apply changes (overrides --dry-run)"
    )
    
    args = parser.parse_args()
    
    # Determine execution mode
    dry_run = not args.apply
    
    try:
        standardizer = ProjectStandardizer(args.base_path, dry_run=dry_run)
        success = standardizer.run_standardization()
        
        if success:
            print("🎉 PROJECT STANDARDIZATION SUCCESSFUL!")
            sys.exit(0)
        else:
            print("💥 PROJECT STANDARDIZATION FAILED!")
            sys.exit(1)
            
    except KeyboardInterrupt:
        print("\\n❌ Operation cancelled by user")
        sys.exit(1)
    except Exception as e:
        print(f"💥 Unexpected error: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()