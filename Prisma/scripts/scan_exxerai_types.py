#!/usr/bin/env python3
"""
Scan all ExxerAI projects to build a comprehensive dictionary of types.
Outputs a JSON file with type names, namespaces, kind, and location.
"""

import os
import re
import json
from pathlib import Path
from typing import Dict, List, Set
from collections import defaultdict
from datetime import datetime


class ExxerAITypeScanner:
    """Scans ExxerAI codebase for all type definitions."""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.src_path = self.base_path / "code" / "src"
        
        # Skip directories
        self.skip_dirs = {
            'bin', 'obj', '.vs', '.git', 'node_modules', 
            'TestResults', 'packages', 'artifacts'
        }
        
        # Type patterns
        self.type_patterns = [
            # Classes and structs
            (r'\bpublic\s+(?:sealed\s+|abstract\s+|static\s+)?(?:partial\s+)?class\s+(\w+)', 'class'),
            (r'\binternal\s+(?:sealed\s+|abstract\s+|static\s+)?(?:partial\s+)?class\s+(\w+)', 'class'),
            (r'\bpublic\s+(?:readonly\s+)?(?:partial\s+)?struct\s+(\w+)', 'struct'),
            (r'\binternal\s+(?:readonly\s+)?(?:partial\s+)?struct\s+(\w+)', 'struct'),
            
            # Interfaces
            (r'\bpublic\s+(?:partial\s+)?interface\s+(\w+)', 'interface'),
            (r'\binternal\s+(?:partial\s+)?interface\s+(\w+)', 'interface'),
            
            # Enums
            (r'\bpublic\s+enum\s+(\w+)', 'enum'),
            (r'\binternal\s+enum\s+(\w+)', 'enum'),
            
            # Records
            (r'\bpublic\s+(?:sealed\s+|abstract\s+)?(?:partial\s+)?record\s+(?:class\s+|struct\s+)?(\w+)', 'record'),
            (r'\binternal\s+(?:sealed\s+|abstract\s+)?(?:partial\s+)?record\s+(?:class\s+|struct\s+)?(\w+)', 'record'),
            
            # Delegates
            (r'\bpublic\s+delegate\s+\w+\s+(\w+)\s*\(', 'delegate'),
            (r'\binternal\s+delegate\s+\w+\s+(\w+)\s*\(', 'delegate')
        ]
        
        # Results storage
        self.types = {}
        self.namespace_map = defaultdict(set)
        self.project_map = defaultdict(set)
        self.kind_map = defaultdict(set)
    
    def should_skip_path(self, path: Path) -> bool:
        """Check if path should be skipped."""
        parts = path.parts
        return any(skip_dir in parts for skip_dir in self.skip_dirs)
    
    def extract_namespace(self, content: str) -> str:
        """Extract namespace from file content."""
        # Try file-scoped namespace first (C# 10+)
        match = re.search(r'^\s*namespace\s+([\w.]+)\s*;', content, re.MULTILINE)
        if match:
            return match.group(1)
        
        # Try traditional namespace
        match = re.search(r'namespace\s+([\w.]+)\s*\{', content)
        if match:
            return match.group(1)
        
        return 'Unknown'
    
    def find_project_for_file(self, file_path: Path) -> str:
        """Find the project name for a given file."""
        current = file_path.parent
        
        while current != self.base_path and current.parent != current:
            csproj_files = list(current.glob("*.csproj"))
            if csproj_files:
                return csproj_files[0].stem
            current = current.parent
        
        return 'Unknown'
    
    def scan_file(self, file_path: Path):
        """Scan a single C# file for type definitions."""
        if self.should_skip_path(file_path):
            return
        
        try:
            content = file_path.read_text(encoding='utf-8')
            namespace = self.extract_namespace(content)
            project = self.find_project_for_file(file_path)
            
            # Search for type definitions
            for pattern, kind in self.type_patterns:
                for match in re.finditer(pattern, content):
                    type_name = match.group(1)
                    
                    # Skip generic type parameters like T, TKey, TValue
                    if len(type_name) <= 3 and type_name.startswith('T'):
                        continue
                    
                    # Create type entry
                    full_name = f"{namespace}.{type_name}"
                    
                    # Handle duplicates by keeping first found
                    if full_name not in self.types:
                        self.types[full_name] = {
                            'name': type_name,
                            'namespace': namespace,
                            'kind': kind,
                            'project': project,
                            'file': str(file_path.relative_to(self.base_path))
                        }
                        
                        # Update maps
                        self.namespace_map[namespace].add(type_name)
                        self.project_map[project].add(type_name)
                        self.kind_map[kind].add(type_name)
                        
        except Exception as e:
            print(f"Error scanning {file_path}: {e}")
    
    def scan_all(self):
        """Scan all C# files in the codebase."""
        print(f"Scanning ExxerAI types in: {self.src_path}")

        cs_files = list(self.src_path.rglob("*.cs"))
        print(f"Found {len(cs_files)} C# files")

        for i, cs_file in enumerate(cs_files):
            if i % 100 == 0 and i > 0:
                print(f"  Processed {i}/{len(cs_files)} files...")
            self.scan_file(cs_file)

        print(f"\nScan complete!")
        print(f"Total types found: {len(self.types)}")
        print(f"Namespaces: {len(self.namespace_map)}")
        print(f"Projects: {len(self.project_map)}")

    def scan_incremental(self, changed_files: List[str], previous_db_path: str):
        """Incremental scan - only process changed files using previous database."""
        print(f"🚀 Incremental scan mode enabled")

        # Load previous database
        if Path(previous_db_path).exists():
            print(f"📂 Loading previous type database: {previous_db_path}")
            with open(previous_db_path, 'r', encoding='utf-8') as f:
                previous_data = json.load(f)
                self.types = previous_data.get('all_types', {})
                print(f"  Loaded {len(self.types)} existing types")
        else:
            print(f"⚠️  Previous database not found, performing full scan")
            return self.scan_all()

        # Convert changed file paths to Path objects
        changed_paths = []
        for file_path in changed_files:
            # Handle both absolute and relative paths
            if Path(file_path).is_absolute():
                changed_paths.append(Path(file_path))
            else:
                changed_paths.append(self.base_path / file_path)

        print(f"📝 Processing {len(changed_paths)} changed C# files:")
        for f in changed_paths[:10]:  # Show first 10
            print(f"  - {f.relative_to(self.base_path)}")
        if len(changed_paths) > 10:
            print(f"  ... and {len(changed_paths) - 10} more")

        # Remove types from changed files
        types_removed = 0
        for full_name, info in list(self.types.items()):
            file_path = self.base_path / info['file']
            if file_path in changed_paths:
                del self.types[full_name]
                types_removed += 1

        print(f"🗑️  Removed {types_removed} types from changed files")

        # Rebuild maps from remaining types
        self.namespace_map = defaultdict(set)
        self.project_map = defaultdict(set)
        self.kind_map = defaultdict(set)

        for full_name, info in self.types.items():
            self.namespace_map[info['namespace']].add(info['name'])
            self.project_map[info['project']].add(info['name'])
            self.kind_map[info['kind']].add(info['name'])

        # Scan only changed files
        for cs_file in changed_paths:
            if cs_file.exists() and cs_file.suffix == '.cs':
                self.scan_file(cs_file)

        print(f"\n✅ Incremental scan complete!")
        print(f"Total types: {len(self.types)} ({types_removed} removed, {len(changed_paths)} files rescanned)")
        print(f"Namespaces: {len(self.namespace_map)}")
        print(f"Projects: {len(self.project_map)}")
    
    def generate_type_lookup(self) -> Dict:
        """Generate a lookup dictionary for missing types."""
        # Build simple type name to full type mapping
        simple_lookup = {}
        
        for full_name, info in self.types.items():
            type_name = info['name']
            
            # Only add if unique or prioritize non-test projects
            if type_name not in simple_lookup:
                simple_lookup[type_name] = {
                    'namespace': info['namespace'],
                    'kind': info['kind'],
                    'project': info['project']
                }
            elif 'Test' not in info['project'] and 'Test' in simple_lookup[type_name]['project']:
                # Prefer non-test project definitions
                simple_lookup[type_name] = {
                    'namespace': info['namespace'],
                    'kind': info['kind'],
                    'project': info['project']
                }
        
        return simple_lookup
    
    def save_results(self, output_file: str):
        """Save scan results to JSON file."""
        results = {
            'scan_date': datetime.now().isoformat(),
            'base_path': str(self.base_path),
            'statistics': {
                'total_types': len(self.types),
                'total_namespaces': len(self.namespace_map),
                'total_projects': len(self.project_map),
                'types_by_kind': {kind: len(types) for kind, types in self.kind_map.items()}
            },
            'type_lookup': self.generate_type_lookup(),
            'all_types': self.types
        }
        
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(results, f, indent=2)
        
        print(f"\nResults saved to: {output_file}")
        
        # Print summary
        print("\nType counts by kind:")
        for kind, types in sorted(self.kind_map.items()):
            print(f"  {kind}: {len(types)}")
        
        print("\nTop namespaces by type count:")
        namespace_counts = [(ns, len(types)) for ns, types in self.namespace_map.items()]
        for ns, count in sorted(namespace_counts, key=lambda x: -x[1])[:10]:
            print(f"  {ns}: {count} types")


def main():
    import argparse

    parser = argparse.ArgumentParser(description='Scan ExxerAI codebase for type definitions')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--output', default='exxerai_types.json',
                       help='Output JSON file')
    parser.add_argument('--incremental', action='store_true',
                       help='Incremental mode - only scan changed files')
    parser.add_argument('--changed-files', nargs='*',
                       help='List of changed C# files (for incremental mode)')
    parser.add_argument('--previous-db',
                       help='Path to previous type database (for incremental mode)')

    args = parser.parse_args()

    scanner = ExxerAITypeScanner(args.base_path)

    if args.incremental and args.changed_files and args.previous_db:
        scanner.scan_incremental(args.changed_files, args.previous_db)
    else:
        scanner.scan_all()

    scanner.save_results(args.output)


if __name__ == "__main__":
    main()