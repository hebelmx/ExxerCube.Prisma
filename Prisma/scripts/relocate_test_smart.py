#!/usr/bin/env python3
"""
Smart Test File Relocation with Dependency Analysis
===================================================

PURPOSE: Relocates test files while automatically managing dependencies using
         enhanced_dependency_analysis.json metadata.

FEATURES:
- Parses test file to extract used types
- Looks up required namespaces in JSON metadata
- Verifies destination project's GlobalUsings.cs
- Adds missing usings automatically
- Updates file namespace
- Moves file to destination
- Full dry-run mode for safety

USAGE:
    # Dry run (recommended first)
    python scripts/relocate_test_smart.py --file "code/src/Losetests/NPOIAdapterTests.cs" \
        --destination "04AdapterTests/ExxerAI.Nexus.Adapter.Tests" \
        --namespace "ExxerAI.Nexus.Adapter.Tests" \
        --dry-run

    # Actual move
    python scripts/relocate_test_smart.py --file "code/src/Losetests/NPOIAdapterTests.cs" \
        --destination "04AdapterTests/ExxerAI.Nexus.Adapter.Tests" \
        --namespace "ExxerAI.Nexus.Adapter.Tests"
"""

import os
import json
import re
import shutil
from pathlib import Path
from typing import Dict, List, Set, Tuple
from dataclasses import dataclass, asdict
import argparse


@dataclass
class DependencyInfo:
    """Information about a required dependency."""
    type_name: str
    namespace: str
    source: str
    found_in_metadata: bool


@dataclass
class RelocationPlan:
    """Plan for relocating a test file."""
    source_file: str
    destination_file: str
    old_namespace: str
    new_namespace: str
    used_types: List[str]
    required_namespaces: Set[str]
    missing_in_global_usings: Set[str]
    global_usings_to_add: List[str]
    warnings: List[str]
    errors: List[str]


class SmartTestRelocator:
    """Relocates test files with automatic dependency management."""

    def __init__(self, base_path: str, metadata_file: str = None):
        self.base_path = Path(base_path)
        self.metadata_file = metadata_file or (self.base_path / "enhanced_dependency_analysis.json")
        self.metadata = {}
        self.type_to_namespace = {}

        self._load_metadata()

    def _load_metadata(self):
        """Load dependency metadata from JSON."""
        if not self.metadata_file.exists():
            print(f"⚠️  Metadata file not found: {self.metadata_file}")
            print("    Proceeding without dependency metadata...")
            return

        try:
            with open(self.metadata_file, 'r', encoding='utf-8') as f:
                self.metadata = json.load(f)
                print(f"✅ Loaded metadata: {len(self.metadata.get('errors_by_project', {}))} projects analyzed")
                self._build_type_lookup()
        except Exception as e:
            print(f"⚠️  Error loading metadata: {e}")

    def _build_type_lookup(self):
        """Build type-to-namespace lookup from metadata."""
        # Extract type mappings from enhanced_dependency_analysis.json
        for project, errors in self.metadata.get('errors_by_project', {}).items():
            for error in errors:
                resolution = error.get('resolution', {})
                type_name = resolution.get('type')
                namespace = resolution.get('namespace')

                if type_name and namespace and namespace != "null":
                    if type_name not in self.type_to_namespace:
                        self.type_to_namespace[type_name] = set()
                    self.type_to_namespace[type_name].add(namespace)

        print(f"   Built lookup table: {len(self.type_to_namespace)} types mapped")

    def extract_used_types(self, file_path: Path) -> Set[str]:
        """Extract all type names used in a C# file."""
        used_types = set()

        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()

            # Pattern for C# type usage (class names, interfaces, enums, attributes)
            # Matches PascalCase identifiers
            patterns = [
                r'\b([A-Z][a-zA-Z0-9_]*(?:<[^>]+>)?)\s+\w+\s*[=;(,)]',  # Variable declarations
                r':\s*([A-Z][a-zA-Z0-9_]*(?:<[^>]+>)?)',  # Inheritance/interfaces
                r'new\s+([A-Z][a-zA-Z0-9_]*(?:<[^>]+>)?)',  # Object instantiation
                r'\[([A-Z][a-zA-Z0-9_]*)\]',  # Attributes
                r'typeof\(([A-Z][a-zA-Z0-9_]*(?:<[^>]+>)?)\)',  # typeof
                r'Task<([A-Z][a-zA-Z0-9_]*)>',  # Generic Task
                r'Result<([A-Z][a-zA-Z0-9_]*)>',  # Result pattern
                r'ILogger<([A-Z][a-zA-Z0-9_]*)>',  # ILogger
                r'List<([A-Z][a-zA-Z0-9_]*)>',  # List
                r'Dictionary<([^,]+),\s*([^>]+)>',  # Dictionary
            ]

            for pattern in patterns:
                matches = re.findall(pattern, content)
                for match in matches:
                    if isinstance(match, tuple):
                        for m in match:
                            if m and m.strip():
                                used_types.add(m.strip().split('<')[0])  # Remove generics
                    else:
                        used_types.add(match.strip().split('<')[0])

            # Filter out common built-in types
            builtin_types = {'String', 'Int32', 'Boolean', 'Task', 'Object', 'Void', 'Guid',
                           'DateTime', 'TimeSpan', 'Array', 'List', 'Dictionary', 'var',
                           'async', 'await', 'public', 'private', 'protected', 'internal'}
            used_types = {t for t in used_types if t not in builtin_types and len(t) > 1}

        except Exception as e:
            print(f"⚠️  Error extracting types from {file_path}: {e}")

        return used_types

    def lookup_namespaces(self, types: Set[str]) -> Dict[str, Set[str]]:
        """Look up required namespaces for given types."""
        namespace_map = {}

        for type_name in types:
            namespaces = self.type_to_namespace.get(type_name, set())
            if namespaces:
                namespace_map[type_name] = namespaces

        return namespace_map

    def extract_current_namespace(self, file_path: Path) -> str:
        """Extract current namespace from a C# file."""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                for line in f:
                    match = re.match(r'namespace\s+([\w.]+);?', line.strip())
                    if match:
                        return match.group(1)
        except Exception as e:
            print(f"⚠️  Error extracting namespace: {e}")

        return ""

    def get_global_usings(self, project_dir: Path) -> Set[str]:
        """Get all global usings from a project's GlobalUsings.cs."""
        global_usings_file = project_dir / "GlobalUsings.cs"
        global_usings = set()

        if not global_usings_file.exists():
            return global_usings

        try:
            with open(global_usings_file, 'r', encoding='utf-8') as f:
                for line in f:
                    match = re.match(r'global\s+using\s+(.*);', line.strip())
                    if match:
                        global_usings.add(match.group(1).strip())
        except Exception as e:
            print(f"⚠️  Error reading GlobalUsings.cs: {e}")

        return global_usings

    def create_relocation_plan(self, source_file: str, destination_project: str,
                               new_namespace: str) -> RelocationPlan:
        """Create a detailed relocation plan."""
        source_path = self.base_path / source_file
        dest_project_path = self.base_path / "code" / "src" / "tests" / destination_project
        dest_file_path = dest_project_path / source_path.name

        # Extract information
        old_namespace = self.extract_current_namespace(source_path)
        used_types = self.extract_used_types(source_path)
        namespace_map = self.lookup_namespaces(used_types)

        # Get all required namespaces
        required_namespaces = set()
        for type_name, namespaces in namespace_map.items():
            required_namespaces.update(namespaces)

        # Get current global usings
        current_global_usings = self.get_global_usings(dest_project_path)

        # Find missing usings
        missing_usings = required_namespaces - current_global_usings

        # Warnings and errors
        warnings = []
        errors = []

        if not source_path.exists():
            errors.append(f"Source file does not exist: {source_path}")

        if not dest_project_path.exists():
            errors.append(f"Destination project does not exist: {dest_project_path}")

        if dest_file_path.exists():
            warnings.append(f"Destination file already exists: {dest_file_path}")

        if not used_types:
            warnings.append("No types extracted from source file - verification limited")

        if not self.type_to_namespace:
            warnings.append("No metadata loaded - cannot verify dependencies")

        return RelocationPlan(
            source_file=str(source_path.relative_to(self.base_path)),
            destination_file=str(dest_file_path.relative_to(self.base_path)),
            old_namespace=old_namespace,
            new_namespace=new_namespace,
            used_types=sorted(list(used_types)),
            required_namespaces=sorted(list(required_namespaces)),
            missing_in_global_usings=sorted(list(missing_usings)),
            global_usings_to_add=sorted(list(missing_usings)),
            warnings=warnings,
            errors=errors
        )

    def print_plan(self, plan: RelocationPlan):
        """Print relocation plan to console."""
        print("\n" + "="*80)
        print("📋 RELOCATION PLAN")
        print("="*80)

        print(f"\n📄 Source:      {plan.source_file}")
        print(f"📍 Destination: {plan.destination_file}")
        print(f"\n🔖 Namespace Change:")
        print(f"   Old: {plan.old_namespace}")
        print(f"   New: {plan.new_namespace}")

        print(f"\n🔍 Analysis:")
        print(f"   Types found: {len(plan.used_types)}")
        print(f"   Required namespaces: {len(plan.required_namespaces)}")
        print(f"   Missing in GlobalUsings.cs: {len(plan.missing_in_global_usings)}")

        if plan.used_types:
            print(f"\n📦 Used Types ({len(plan.used_types)}):")
            for i, type_name in enumerate(plan.used_types[:20], 1):  # Show first 20
                print(f"   {i:2}. {type_name}")
            if len(plan.used_types) > 20:
                print(f"   ... and {len(plan.used_types) - 20} more")

        if plan.required_namespaces:
            print(f"\n📚 Required Namespaces ({len(plan.required_namespaces)}):")
            for namespace in plan.required_namespaces[:15]:
                print(f"   ✓ {namespace}")
            if len(plan.required_namespaces) > 15:
                print(f"   ... and {len(plan.required_namespaces) - 15} more")

        if plan.missing_in_global_usings:
            print(f"\n⚠️  Missing in GlobalUsings.cs ({len(plan.missing_in_global_usings)}):")
            for namespace in plan.missing_in_global_usings:
                print(f"   + {namespace}")
        else:
            print(f"\n✅ All required namespaces present in GlobalUsings.cs")

        if plan.warnings:
            print(f"\n⚠️  WARNINGS:")
            for warning in plan.warnings:
                print(f"   • {warning}")

        if plan.errors:
            print(f"\n❌ ERRORS:")
            for error in plan.errors:
                print(f"   • {error}")

        print("\n" + "="*80)

    def execute_relocation(self, plan: RelocationPlan, dry_run: bool = True) -> bool:
        """Execute the relocation plan."""
        if plan.errors:
            print("\n❌ Cannot proceed - errors detected")
            return False

        source_path = self.base_path / plan.source_file
        dest_path = self.base_path / plan.destination_file
        dest_project_path = dest_path.parent
        global_usings_path = dest_project_path / "GlobalUsings.cs"

        print("\n" + "="*80)
        if dry_run:
            print("🔍 DRY RUN MODE - No files will be modified")
        else:
            print("🚀 EXECUTING RELOCATION")
        print("="*80)

        # Step 1: Update GlobalUsings.cs if needed
        if plan.global_usings_to_add:
            print(f"\n📝 Step 1: Update GlobalUsings.cs ({len(plan.global_usings_to_add)} additions)")

            if dry_run:
                print("   [DRY RUN] Would add the following usings:")
                for using in plan.global_usings_to_add:
                    print(f"      global using {using};")
            else:
                try:
                    with open(global_usings_path, 'a', encoding='utf-8') as f:
                        f.write("\n// Added by smart relocation script\n")
                        for using in plan.global_usings_to_add:
                            f.write(f"global using {using};\n")
                    print(f"   ✅ Added {len(plan.global_usings_to_add)} usings to GlobalUsings.cs")
                except Exception as e:
                    print(f"   ❌ Error updating GlobalUsings.cs: {e}")
                    return False
        else:
            print("\n✅ Step 1: GlobalUsings.cs - No updates needed")

        # Step 2: Update namespace in file
        print(f"\n📝 Step 2: Update namespace in file")

        if dry_run:
            print(f"   [DRY RUN] Would change namespace:")
            print(f"      {plan.old_namespace} → {plan.new_namespace}")
        else:
            try:
                with open(source_path, 'r', encoding='utf-8') as f:
                    content = f.read()

                # Replace namespace
                content = re.sub(
                    rf'namespace\s+{re.escape(plan.old_namespace)}\s*;?',
                    f'namespace {plan.new_namespace};',
                    content
                )

                with open(source_path, 'w', encoding='utf-8') as f:
                    f.write(content)

                print(f"   ✅ Updated namespace: {plan.new_namespace}")
            except Exception as e:
                print(f"   ❌ Error updating namespace: {e}")
                return False

        # Step 3: Move file
        print(f"\n📦 Step 3: Move file to destination")

        if dry_run:
            print(f"   [DRY RUN] Would move:")
            print(f"      {plan.source_file}")
            print(f"      → {plan.destination_file}")
        else:
            try:
                shutil.move(str(source_path), str(dest_path))
                print(f"   ✅ Moved file to: {plan.destination_file}")
            except Exception as e:
                print(f"   ❌ Error moving file: {e}")
                return False

        print("\n" + "="*80)
        if dry_run:
            print("✅ DRY RUN COMPLETE - No files were modified")
            print("\nTo execute for real, run without --dry-run flag")
        else:
            print("✅ RELOCATION COMPLETE")
            print("\nNext steps:")
            print("   1. Run: dotnet build")
            print("   2. Run: dotnet test")
            print("   3. If GREEN, commit changes")
            print("   4. If failures, investigate and fix")
        print("="*80 + "\n")

        return True


def main():
    parser = argparse.ArgumentParser(
        description='Smart test file relocation with automatic dependency management',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Dry run (recommended first)
  python scripts/relocate_test_smart.py \\
      --file "code/src/Losetests/NPOIAdapterTests.cs" \\
      --destination "04AdapterTests/ExxerAI.Nexus.Adapter.Tests" \\
      --namespace "ExxerAI.Nexus.Adapter.Tests" \\
      --dry-run

  # Execute relocation
  python scripts/relocate_test_smart.py \\
      --file "code/src/Losetests/NPOIAdapterTests.cs" \\
      --destination "04AdapterTests/ExxerAI.Nexus.Adapter.Tests" \\
      --namespace "ExxerAI.Nexus.Adapter.Tests"
        """
    )

    parser.add_argument('--file', type=str, required=True,
                       help='Source file path (relative to base-path)')
    parser.add_argument('--destination', type=str, required=True,
                       help='Destination project path (e.g., 04AdapterTests/ExxerAI.Nexus.Adapter.Tests)')
    parser.add_argument('--namespace', type=str, required=True,
                       help='New namespace for the file (e.g., ExxerAI.Nexus.Adapter.Tests)')
    parser.add_argument('--base-path', type=str, default='.',
                       help='Base path to ExxerAI repository (default: current directory)')
    parser.add_argument('--metadata', type=str,
                       help='Path to enhanced_dependency_analysis.json (default: base-path/enhanced_dependency_analysis.json)')
    parser.add_argument('--dry-run', action='store_true',
                       help='Show what would be done without making changes')

    args = parser.parse_args()

    # Create relocator
    relocator = SmartTestRelocator(args.base_path, args.metadata)

    # Create plan
    plan = relocator.create_relocation_plan(
        args.file,
        args.destination,
        args.namespace
    )

    # Print plan
    relocator.print_plan(plan)

    # Execute
    if plan.errors:
        print("\n❌ Cannot proceed due to errors")
        return 1

    if plan.warnings and not args.dry_run:
        response = input("\n⚠️  Warnings detected. Continue? [y/N]: ")
        if response.lower() != 'y':
            print("Aborted by user")
            return 0

    success = relocator.execute_relocation(plan, dry_run=args.dry_run)

    return 0 if success else 1


if __name__ == '__main__':
    exit(main())
