#!/usr/bin/env python3
"""
Script to collect all using statements from Infrastructure projects and generate GlobalUsing.cs files for test projects.

This script:
1. Collects all using/global using statements from Infrastructure projects
2. Saves them to a JSON file
3. Generates GlobalUsing.cs files avoiding duplicates
4. Deploys to test projects with dry run option
"""

import os
import re
import json
import argparse
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Set, List, Dict, Tuple
from datetime import datetime


class UsingCollector:
    """Collects and processes using statements from C# files."""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.infrastructure_path = self.base_path / "code" / "src" / "Infrastructure"
        self.tests_path = self.base_path / "code" / "src" / "tests"
        
        # Patterns to match using statements
        self.using_pattern = re.compile(r'^\s*using\s+([^;]+);', re.MULTILINE)
        self.global_using_pattern = re.compile(r'^\s*global\s+using\s+([^;]+);', re.MULTILINE)
        
        # Common test-related namespaces to include
        self.test_namespaces = {
            'Xunit',
            'NSubstitute',
            'Shouldly',
            'System.Threading',
            'System.Threading.Tasks',
            'Microsoft.Extensions.DependencyInjection',
            'Microsoft.Extensions.Logging'
        }
        
    def collect_using_statements(self) -> Dict[str, Set[str]]:
        """Collect all using statements from Infrastructure projects."""
        print(f"Collecting using statements from: {self.infrastructure_path}")
        
        usings = set()
        global_usings = set()
        
        # Walk through all .cs files in Infrastructure
        for cs_file in self.infrastructure_path.rglob("*.cs"):
            # Skip generated files
            if any(skip in str(cs_file) for skip in ['obj/', 'bin/', '.g.cs', '.Designer.cs']):
                continue
                
            try:
                content = cs_file.read_text(encoding='utf-8')
                
                # Extract regular using statements
                for match in self.using_pattern.finditer(content):
                    using_stmt = match.group(1).strip()
                    # Filter out alias usings and static usings for now
                    if '=' not in using_stmt and not using_stmt.startswith('static '):
                        usings.add(using_stmt)
                
                # Extract global using statements
                for match in self.global_using_pattern.finditer(content):
                    using_stmt = match.group(1).strip()
                    if '=' not in using_stmt and not using_stmt.startswith('static '):
                        global_usings.add(using_stmt)
                        
            except Exception as e:
                print(f"Error reading {cs_file}: {e}")
        
        # Combine and deduplicate
        all_usings = usings | global_usings
        
        # Add essential test namespaces
        for ns in self.test_namespaces:
            all_usings.add(ns)
        
        print(f"Found {len(all_usings)} unique using statements")
        
        return {
            "usings": sorted(list(all_usings)),
            "collection_date": datetime.now().isoformat(),
            "source_path": str(self.infrastructure_path)
        }
    
    def parse_directory_build_props(self) -> Set[str]:
        """Parse Directory.Build.props to find already defined global usings."""
        props_usings = set()
        
        # Check for Directory.Build.props in tests folder
        props_file = self.tests_path / "Directory.Build.props"
        if props_file.exists():
            print(f"Reading existing usings from: {props_file}")
            try:
                tree = ET.parse(props_file)
                root = tree.getroot()
                
                # Find all Using elements
                for using in root.findall(".//Using"):
                    include = using.get('Include')
                    if include:
                        props_usings.add(include)
                
                print(f"Found {len(props_usings)} usings in Directory.Build.props")
            except Exception as e:
                print(f"Error parsing Directory.Build.props: {e}")
        
        return props_usings
    
    def save_to_json(self, data: Dict, output_file: str):
        """Save collected using statements to JSON file."""
        output_path = Path(output_file)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        
        print(f"Saved using statements to: {output_path}")
    
    def generate_global_using_content(self, json_file: str, exclude_props_usings: bool = True) -> str:
        """Generate GlobalUsing.cs content from JSON file."""
        with open(json_file, 'r', encoding='utf-8') as f:
            data = json.load(f)
        
        usings = set(data['usings'])
        
        # Exclude usings already in Directory.Build.props if requested
        if exclude_props_usings:
            props_usings = self.parse_directory_build_props()
            usings = usings - props_usings
            print(f"Excluded {len(props_usings)} usings already in Directory.Build.props")
        
        # Sort usings by category
        system_usings = sorted([u for u in usings if u.startswith('System')])
        microsoft_usings = sorted([u for u in usings if u.startswith('Microsoft') and not u.startswith('System')])
        exxerai_usings = sorted([u for u in usings if u.startswith('ExxerAI')])
        other_usings = sorted([u for u in usings if not any(u.startswith(p) for p in ['System', 'Microsoft', 'ExxerAI'])])
        
        # Build content
        content = [
            "// Global using directives for test projects",
            f"// Generated on: {datetime.now().isoformat()}",
            "// Source: Infrastructure projects using statements",
            ""
        ]
        
        # Add usings by category
        if system_usings:
            content.append("// System namespaces")
            content.extend([f"global using {u};" for u in system_usings])
            content.append("")
        
        if microsoft_usings:
            content.append("// Microsoft namespaces")
            content.extend([f"global using {u};" for u in microsoft_usings])
            content.append("")
        
        if exxerai_usings:
            content.append("// ExxerAI namespaces")
            content.extend([f"global using {u};" for u in exxerai_usings])
            content.append("")
        
        if other_usings:
            content.append("// Third-party namespaces")
            content.extend([f"global using {u};" for u in other_usings])
            content.append("")
        
        return '\n'.join(content)
    
    def deploy_to_test_projects(self, content: str, dry_run: bool = True):
        """Deploy GlobalUsing.cs to all test projects."""
        test_projects = []
        
        # Find all test project directories (including those in numbered folders)
        for csproj_file in self.tests_path.rglob("*.csproj"):
            # Skip bin and obj directories
            if any(skip in str(csproj_file) for skip in ['bin/', 'obj/']):
                continue
            
            # Check if it's a test project (name ends with .Test or .Tests)
            project_name = csproj_file.stem
            if project_name.endswith('.Test') or project_name.endswith('.Tests'):
                test_projects.append(csproj_file.parent)
        
        print(f"\nFound {len(test_projects)} test projects")
        
        for project_dir in test_projects:
            global_using_file = project_dir / "GlobalUsings.cs"
            
            if dry_run:
                print(f"[DRY RUN] Would write to: {global_using_file}")
                
                # Check if file already exists
                if global_using_file.exists():
                    print(f"  WARNING: File already exists, would overwrite")
            else:
                # Check if GlobalUsings.cs already exists
                backup_created = False
                if global_using_file.exists():
                    backup_file = global_using_file.with_suffix('.cs.bak')
                    global_using_file.rename(backup_file)
                    backup_created = True
                    print(f"  Backed up existing file to: {backup_file}")
                
                # Write new content
                global_using_file.write_text(content, encoding='utf-8')
                print(f"  Written to: {global_using_file}")
                
                if backup_created:
                    print(f"  (Existing file backed up)")


def main():
    parser = argparse.ArgumentParser(description='Collect using statements and generate GlobalUsing.cs files')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI', 
                       help='Base path of the ExxerAI project')
    parser.add_argument('--json-output', default='using_statements.json',
                       help='Output JSON file for collected using statements')
    parser.add_argument('--step', choices=['collect', 'generate', 'deploy', 'all'], default='all',
                       help='Which step to run')
    parser.add_argument('--dry-run', action='store_true',
                       help='Run in dry-run mode (no files will be written in deploy step)')
    parser.add_argument('--no-exclude-props', action='store_true',
                       help='Do not exclude usings already in Directory.Build.props')
    
    args = parser.parse_args()
    
    collector = UsingCollector(args.base_path)
    
    # Step 1: Collect using statements
    if args.step in ['collect', 'all']:
        print("=== Step 1: Collecting using statements ===")
        using_data = collector.collect_using_statements()
        collector.save_to_json(using_data, args.json_output)
    
    # Step 2: Generate GlobalUsing.cs content
    if args.step in ['generate', 'all']:
        print("\n=== Step 2: Generating GlobalUsing.cs content ===")
        if not os.path.exists(args.json_output):
            print(f"Error: JSON file not found: {args.json_output}")
            print("Please run with --step collect first")
            return
        
        content = collector.generate_global_using_content(
            args.json_output, 
            exclude_props_usings=not args.no_exclude_props
        )
        
        if args.dry_run:
            print("\n[DRY RUN] Generated GlobalUsing.cs content:")
            print("=" * 60)
            print(content)
            print("=" * 60)
        else:
            # Save to a preview file
            preview_file = "GlobalUsing.cs.preview"
            with open(preview_file, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"Preview saved to: {preview_file}")
    
    # Step 3: Deploy to test projects
    if args.step in ['deploy', 'all']:
        print("\n=== Step 3: Deploying to test projects ===")
        if args.step == 'deploy':
            # Load content from preview file if running deploy separately
            preview_file = "GlobalUsing.cs.preview"
            if not os.path.exists(preview_file):
                print(f"Error: Preview file not found: {preview_file}")
                print("Please run with --step generate first")
                return
            
            with open(preview_file, 'r', encoding='utf-8') as f:
                content = f.read()
        
        collector.deploy_to_test_projects(content, dry_run=args.dry_run)
        
        if args.dry_run:
            print("\n[DRY RUN] No files were modified")
        else:
            print("\nDeployment complete!")


if __name__ == "__main__":
    main()