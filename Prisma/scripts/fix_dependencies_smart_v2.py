#!/usr/bin/env python3
"""
Enhanced Smart Dependency Fixer v2
- Modifies GlobalUsings.cs files for both test and production projects
- Avoids namespace duplications
- Full git safety protocol (add, commit before changes)
- Comprehensive dry-run mode
- Backup system for rollback
"""

import os
import re
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Set, List, Dict, Optional, Tuple
from datetime import datetime
import argparse
import shutil
import subprocess
import sys


class EnhancedSmartDependencyFixer:
    """Enhanced fixer that modifies GlobalUsings.cs with safety protocols."""
    
    def __init__(self, base_path: str, dry_run: bool = True):
        self.base_path = Path(base_path)
        self.src_path = self.base_path / "code" / "src"
        self.tests_path = self.base_path / "code" / "src" / "tests"
        self.dry_run = dry_run
        
        # Track changes
        self.changes_made = {
            'files_modified': 0,
            'namespaces_added': 0,
            'projects_updated': set(),
            'git_commit': None
        }
        
        # Create backup directory
        if not dry_run:
            self.backup_dir = self.base_path / "scripts" / "smart_fix_backups" / datetime.now().strftime("%Y%m%d_%H%M%S")
            self.backup_dir.mkdir(parents=True, exist_ok=True)
            print(f"Backup directory: {self.backup_dir}")
    
    def load_analysis_report(self, report_file: str) -> Dict:
        """Load the enhanced analysis report."""
        with open(report_file, 'r', encoding='utf-8') as f:
            return json.load(f)
    
    def run_git_command(self, cmd: List[str]) -> Tuple[bool, str]:
        """Run a git command and return success status and output."""
        try:
            result = subprocess.run(
                cmd,
                cwd=self.base_path,
                capture_output=True,
                text=True,
                check=True
            )
            return True, result.stdout
        except subprocess.CalledProcessError as e:
            return False, f"{e.stderr}\n{e.stdout}"
    
    def perform_git_safety_protocol(self) -> bool:
        """Perform git add and commit before making changes."""
        if self.dry_run:
            print("\n[DRY RUN] Would perform git safety protocol")
            return True
        
        print("\n=== Git Safety Protocol ===")
        
        # Check git status
        success, output = self.run_git_command(['git', 'status', '--porcelain'])
        if not success:
            print(f"Error checking git status: {output}")
            return False
        
        if output.strip():
            # There are uncommitted changes
            print("Found uncommitted changes. Creating safety commit...")
            
            # Add all changes
            success, output = self.run_git_command(['git', 'add', '-A'])
            if not success:
                print(f"Error adding files: {output}")
                return False
            
            # Create commit
            commit_message = f"Safety commit before GlobalUsings.cs modifications - {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}"
            success, output = self.run_git_command(['git', 'commit', '-m', commit_message])
            if not success:
                print(f"Error creating commit: {output}")
                return False
            
            # Get commit hash
            success, output = self.run_git_command(['git', 'rev-parse', 'HEAD'])
            if success:
                self.changes_made['git_commit'] = output.strip()
                print(f"Created safety commit: {self.changes_made['git_commit'][:8]}")
        else:
            print("Working directory is clean")
        
        return True
    
    def _find_project_directory(self, project_name: str) -> Optional[Path]:
        """Find the directory containing a project."""
        # Search in both test and production paths
        search_paths = [
            # Test paths
            self.tests_path / "03UnitTests" / project_name,
            self.tests_path / "04IntegrationTests" / project_name,
            self.tests_path / "01Application" / project_name,
            self.tests_path / "02Infrastructure" / project_name,
            # Production paths
            self.src_path / "00Domain" / project_name,
            self.src_path / "01Application" / project_name,
            self.src_path / "02Infrastructure" / project_name,
            self.src_path / "03Infrastructure" / project_name,
            self.src_path / "04Api" / project_name,
            self.src_path / "05WebApps" / project_name,
            self.src_path / "Infrastructure" / project_name,
            self.src_path / "Domain" / project_name,
        ]
        
        for path in search_paths:
            if path.exists() and path.is_dir():
                return path
        
        # Fallback: search recursively
        for csproj in self.src_path.rglob(f"{project_name}.csproj"):
            if 'bin' not in str(csproj) and 'obj' not in str(csproj):
                return csproj.parent
                
        return None
    
    def _backup_file(self, file_path: Path):
        """Backup a file before modifying."""
        if self.dry_run:
            return
        
        relative_path = file_path.relative_to(self.base_path)
        backup_path = self.backup_dir / relative_path
        backup_path.parent.mkdir(parents=True, exist_ok=True)
        
        if file_path.exists():
            shutil.copy2(file_path, backup_path)
        else:
            # Create empty backup marker for new files
            backup_path.touch()
    
    def _read_global_usings(self, file_path: Path) -> Tuple[List[str], Set[str]]:
        """Read GlobalUsings.cs and return lines and existing namespaces."""
        if not file_path.exists():
            return [], set()
        
        lines = []
        namespaces = set()
        
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        # Extract existing namespaces
        pattern = r'global\s+using\s+(?:static\s+)?([^;]+);'
        for line in lines:
            match = re.match(pattern, line.strip())
            if match:
                namespace = match.group(1).strip()
                namespaces.add(namespace)
        
        return lines, namespaces
    
    def _create_global_usings_content(self, namespaces: List[str]) -> str:
        """Create content for a new GlobalUsings.cs file."""
        content = [
            "// Global using directives for common namespaces\n",
            "// Auto-generated by fix_dependencies_smart_v2.py\n",
            "\n"
        ]
        
        # Group namespaces by prefix
        system_ns = sorted([ns for ns in namespaces if ns.startswith('System')])
        microsoft_ns = sorted([ns for ns in namespaces if ns.startswith('Microsoft')])
        exxerai_ns = sorted([ns for ns in namespaces if ns.startswith('ExxerAI')])
        other_ns = sorted([ns for ns in namespaces if not ns.startswith(('System', 'Microsoft', 'ExxerAI'))])
        
        # Add grouped namespaces
        if system_ns:
            content.append("// System namespaces\n")
            for ns in system_ns:
                content.append(f"global using {ns};\n")
            content.append("\n")
        
        if microsoft_ns:
            content.append("// Microsoft namespaces\n")
            for ns in microsoft_ns:
                content.append(f"global using {ns};\n")
            content.append("\n")
        
        if exxerai_ns:
            content.append("// ExxerAI namespaces\n")
            for ns in exxerai_ns:
                content.append(f"global using {ns};\n")
            content.append("\n")
        
        if other_ns:
            content.append("// Other namespaces\n")
            for ns in other_ns:
                content.append(f"global using {ns};\n")
            content.append("\n")
        
        return ''.join(content)
    
    def modify_global_usings(self, project_name: str, namespaces_to_add: List[str]) -> bool:
        """Modify or create GlobalUsings.cs for a project."""
        project_dir = self._find_project_directory(project_name)
        if not project_dir:
            print(f"  Warning: Could not find directory for {project_name}")
            return False
        
        global_usings_path = project_dir / "GlobalUsings.cs"
        
        # Read existing content
        existing_lines, existing_namespaces = self._read_global_usings(global_usings_path)
        
        # Filter out namespaces that already exist
        new_namespaces = [ns for ns in namespaces_to_add if ns not in existing_namespaces]
        
        if not new_namespaces:
            print(f"  {project_name}: All required namespaces already present in GlobalUsings.cs")
            return False
        
        if self.dry_run:
            print(f"\n[DRY RUN] Would modify: {global_usings_path}")
            print(f"  Would add {len(new_namespaces)} namespaces:")
            for ns in sorted(new_namespaces):
                print(f"    + global using {ns};")
            return True
        
        # Backup the file
        self._backup_file(global_usings_path)
        
        # Modify or create the file
        if global_usings_path.exists():
            # Add new namespaces to existing file
            with open(global_usings_path, 'a', encoding='utf-8') as f:
                if existing_lines and not existing_lines[-1].endswith('\n'):
                    f.write('\n')
                
                f.write(f"\n// Added by fix_dependencies_smart_v2.py on {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
                for ns in sorted(new_namespaces):
                    f.write(f"global using {ns};\n")
        else:
            # Create new file
            all_namespaces = list(existing_namespaces) + new_namespaces
            content = self._create_global_usings_content(all_namespaces)
            
            with open(global_usings_path, 'w', encoding='utf-8') as f:
                f.write(content)
        
        self.changes_made['files_modified'] += 1
        self.changes_made['namespaces_added'] += len(new_namespaces)
        self.changes_made['projects_updated'].add(project_name)
        
        print(f"  Modified: {global_usings_path}")
        print(f"  Added {len(new_namespaces)} namespaces")
        
        return True
    
    def show_dry_run_summary(self, modifications: Dict[str, List[str]]):
        """Show detailed dry-run summary."""
        print("\n" + "=" * 80)
        print("DRY RUN SUMMARY - The following changes would be made:")
        print("=" * 80)
        
        total_projects = len(modifications)
        total_namespaces = sum(len(ns_list) for ns_list in modifications.values())
        
        print(f"\nProjects to update: {total_projects}")
        print(f"Total namespaces to add: {total_namespaces}")
        
        print("\nDetailed changes by project:")
        for project, namespaces in sorted(modifications.items()):
            project_dir = self._find_project_directory(project)
            if project_dir:
                global_usings_path = project_dir / "GlobalUsings.cs"
                exists_status = "UPDATE" if global_usings_path.exists() else "CREATE"
                print(f"\n  {project} [{exists_status}]")
                print(f"    File: {global_usings_path}")
                print(f"    Namespaces to add ({len(namespaces)}):")
                for ns in sorted(namespaces)[:5]:
                    print(f"      + global using {ns};")
                if len(namespaces) > 5:
                    print(f"      ... and {len(namespaces) - 5} more")
        
        print("\n" + "=" * 80)
        print("To apply these changes, run with --apply flag")
        print("=" * 80)
    
    def fix_dependencies(self, report_file: str):
        """Apply fixes based on enhanced analysis report."""
        report = self.load_analysis_report(report_file)
        
        print(f"\n{'DRY RUN MODE' if self.dry_run else 'APPLYING FIXES'}")
        print("=" * 50)
        
        modifications = report.get('globalusings_modifications', {})
        
        if not modifications:
            print("No GlobalUsings.cs modifications needed!")
            return
        
        if self.dry_run:
            self.show_dry_run_summary(modifications)
            return
        
        # Perform git safety protocol
        if not self.perform_git_safety_protocol():
            print("\nError: Git safety protocol failed. Aborting.")
            return
        
        # Apply modifications
        print("\n=== Modifying GlobalUsings.cs Files ===")
        
        for project_name, namespaces in modifications.items():
            self.modify_global_usings(project_name, namespaces)
        
        # Show unknown types if any
        unknown = report.get('unknown_types', {})
        if unknown:
            print("\n=== Types Still Needing Manual Investigation ===")
            total_unknown = sum(len(types) for types in unknown.values())
            print(f"Total unknown types: {total_unknown}")
            
            for project, types in list(unknown.items())[:5]:
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
            if self.changes_made['git_commit']:
                print(f"Git safety commit: {self.changes_made['git_commit'][:8]}")
            print(f"Backup directory: {self.backup_dir}")
        
        print(f"\nChanges {'that would be' if self.dry_run else ''} made:")
        print(f"  Files modified/created: {self.changes_made['files_modified']}")
        print(f"  Namespaces added: {self.changes_made['namespaces_added']}")
        print(f"  Projects updated: {len(self.changes_made['projects_updated'])}")
        
        if not self.dry_run and self.changes_made['files_modified'] > 0:
            print("\n✅ Next steps:")
            print("  1. Run 'dotnet build' to verify fixes")
            print("  2. Review and test the changes")
            print("  3. Commit the GlobalUsings.cs modifications")
            print("\n⚠️  Rollback options:")
            print(f"  - Restore from backup: {self.backup_dir}")
            if self.changes_made['git_commit']:
                print(f"  - Git reset to safety commit: git reset --hard {self.changes_made['git_commit'][:8]}")


def main():
    parser = argparse.ArgumentParser(description='Enhanced smart dependency fixer with GlobalUsings.cs support')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--report', default='enhanced_dependency_analysis.json',
                       help='Path to the enhanced analysis report')
    parser.add_argument('--dry-run', action='store_true', default=True,
                       help='Run in dry-run mode (default: true)')
    parser.add_argument('--apply', action='store_true',
                       help='Actually apply the fixes (disables dry-run)')
    
    args = parser.parse_args()
    
    dry_run = not args.apply
    
    if not dry_run:
        print("⚠️  SAFETY CHECKS:")
        print("  - Git status will be checked")
        print("  - Uncommitted changes will be committed")
        print("  - Files will be backed up")
        print("  - GlobalUsings.cs files will be modified")
        
        response = input("\nProceed with modifications? (yes/no): ")
        if response.lower() != 'yes':
            print("Aborted.")
            return
    
    fixer = EnhancedSmartDependencyFixer(args.base_path, dry_run=dry_run)
    fixer.fix_dependencies(args.report)


if __name__ == "__main__":
    main()