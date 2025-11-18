#!/usr/bin/env python3
"""
Find duplicate test files across split test projects (excluding original Tests project).
Identifies test files that exist in multiple split test projects.

Author: Claude Code Agent
Date: 2025-01-15
"""

import os
import re
import json
from pathlib import Path
from typing import Dict, Set, List, Tuple
from collections import defaultdict
from datetime import datetime


class DuplicateTestFinder:
    """Finds duplicate test files across test projects."""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.src_path = self.base_path / "Prisma" / "Code" / "Src" / "CSharp"
        
        # Test method patterns
        self.fact_pattern = r'\[Fact[^\]]*\]'
        self.theory_pattern = r'\[Theory[^\]]*\]'
        self.test_method_pattern = r'\[(?:Fact|Theory)[^\]]*\]\s*(?:public\s+)?(?:async\s+)?(?:Task\s+)?(?:void\s+)?(\w+)\s*\('
        
        # Results storage
        self.project_files = defaultdict(list)  # normalized_filename -> [(project, full_path)]
        self.duplicates = defaultdict(list)  # normalized_filename -> [(project, full_path, methods)]
        
    def normalize_filename(self, file_path: Path) -> str:
        """Get just the filename for comparison."""
        return file_path.name.lower()
    
    def extract_test_methods(self, file_path: Path) -> Set[str]:
        """Extract test method names from a C# test file."""
        test_methods = set()
        try:
            content = file_path.read_text(encoding='utf-8')
            matches = re.finditer(self.test_method_pattern, content, re.MULTILINE)
            for match in matches:
                method_name = match.group(1)
                test_methods.add(method_name)
        except Exception as e:
            print(f"Error reading {file_path}: {e}")
        return test_methods
    
    def scan_project(self, project_path: Path, project_name: str) -> Dict[str, Tuple[Path, Set[str]]]:
        """Scan a test project for test files."""
        test_files = {}
        
        if not project_path.exists():
            return test_files
        
        for cs_file in project_path.rglob("*.cs"):
            # Skip bin/obj directories
            if any(part in ['bin', 'obj', '.vs', '.git'] for part in cs_file.parts):
                continue
                
            try:
                content = cs_file.read_text(encoding='utf-8')
                if re.search(self.fact_pattern, content) or re.search(self.theory_pattern, content):
                    normalized_name = self.normalize_filename(cs_file)
                    test_methods = self.extract_test_methods(cs_file)
                    if test_methods:  # Only add if has test methods
                        test_files[normalized_name] = (cs_file, test_methods)
            except Exception as e:
                print(f"Error scanning {cs_file}: {e}")
        
        return test_files
    
    def analyze(self):
        """Run the duplicate analysis."""
        print("=" * 80)
        print("Duplicate Test File Analysis")
        print("=" * 80)
        print("(Excluding original ExxerCube.Prisma.Tests project)")
        
        # Scan split test projects only (exclude original)
        split_projects = [
            ("Tests.Application", self.src_path / "Tests.Application"),
            ("Tests.Domain", self.src_path / "Tests.Domain"),
            ("Tests.Domain.Interfaces", self.src_path / "Tests.Domain.Interfaces"),
            ("Tests.Infrastructure.Extraction", self.src_path / "Tests.Infrastructure.Extraction"),
            ("Tests.Infrastructure.Classification", self.src_path / "Tests.Infrastructure.Classification"),
            ("Tests.Infrastructure.Database", self.src_path / "Tests.Infrastructure.Database"),
            ("Tests.Infrastructure.Export", self.src_path / "Tests.Infrastructure.Export"),
            ("Tests.Infrastructure.FileSystem", self.src_path / "Tests.Infrastructure.FileSystem"),
            ("Tests.Infrastructure.FileStorage", self.src_path / "Tests.Infrastructure.FileStorage"),
            ("Tests.Infrastructure.Python", self.src_path / "Tests.Infrastructure.Python"),
            ("Tests.System", self.src_path / "Tests.System"),
            ("Tests.EndToEnd", self.src_path / "Tests.EndToEnd"),
            ("Tests.Architecture", self.src_path / "Tests.Architecture"),
            ("Tests.UI", self.src_path / "Tests.UI"),
        ]
        
        print(f"\n1. Scanning split test projects:")
        all_files = {}  # normalized_name -> list of (project, path, methods)
        
        for project_name, project_path in split_projects:
            if project_path.exists():
                files = self.scan_project(project_path, project_name)
                for normalized_name, (file_path, methods) in files.items():
                    if normalized_name not in all_files:
                        all_files[normalized_name] = []
                    all_files[normalized_name].append((project_name, str(file_path), list(methods)))
                print(f"   {project_name}: {len(files)} test files")
            else:
                print(f"   {project_name}: Project not found")
        
        # Find duplicates
        print(f"\n2. Finding duplicate test files:")
        duplicates = {}
        for normalized_name, occurrences in all_files.items():
            if len(occurrences) > 1:
                duplicates[normalized_name] = occurrences
        
        duplicate_count = len(duplicates)
        total_duplicate_occurrences = sum(len(occurrences) for occurrences in duplicates.values())
        print(f"   Found {duplicate_count} duplicate filenames")
        print(f"   Total duplicate occurrences: {total_duplicate_occurrences}")
        
        # Analyze method overlap
        print(f"\n3. Analyzing test method overlap in duplicates:")
        method_overlap_analysis = {}
        for normalized_name, occurrences in duplicates.items():
            if len(occurrences) > 1:
                # Get all methods from all occurrences
                all_methods = set()
                methods_by_project = {}
                for project, path, methods in occurrences:
                    method_set = set(methods)
                    all_methods.update(method_set)
                    methods_by_project[project] = method_set
                
                # Find common methods
                if len(occurrences) == 2:
                    common_methods = methods_by_project[occurrences[0][0]] & methods_by_project[occurrences[1][0]]
                else:
                    # For 3+ occurrences, find methods present in all
                    common_methods = set.intersection(*[set(m) for _, _, m in occurrences])
                
                method_overlap_analysis[normalized_name] = {
                    "total_unique_methods": len(all_methods),
                    "common_methods": len(common_methods),
                    "common_method_names": list(common_methods),
                    "occurrences": occurrences
                }
        
        # Generate report
        report = {
            "analysis_date": datetime.now().isoformat(),
            "duplicate_files": duplicates,
            "duplicate_summary": {
                "unique_duplicate_filenames": duplicate_count,
                "total_duplicate_occurrences": total_duplicate_occurrences
            },
            "method_overlap": method_overlap_analysis
        }
        
        # Save report
        output_file = self.base_path / "Prisma" / "scripts" / "duplicate_tests_analysis.json"
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
        
        print(f"\n4. Report saved to: {output_file}")
        
        # Print detailed duplicate information
        if duplicates:
            print(f"\n" + "=" * 80)
            print("DUPLICATE FILES DETAIL")
            print("=" * 80)
            for normalized_name, occurrences in sorted(duplicates.items()):
                print(f"\n{normalized_name}:")
                for project, path, methods in occurrences:
                    print(f"  - {project}: {len(methods)} methods")
                    print(f"    Path: {path}")
                    if normalized_name in method_overlap_analysis:
                        overlap = method_overlap_analysis[normalized_name]
                        print(f"    Common methods: {overlap['common_methods']}/{overlap['total_unique_methods']}")
        
        return report


def main():
    import argparse
    
    parser = argparse.ArgumentParser(description="Find duplicate test files across split test projects")
    parser.add_argument("--base-path", type=str, default=".", help="Base path to ExxerCube.Prisma repository")
    parser.add_argument("--output", type=str, default=None, help="Output JSON file path (default: scripts/duplicate_tests_analysis.json)")
    
    args = parser.parse_args()
    
    finder = DuplicateTestFinder(args.base_path)
    report = finder.analyze()
    
    if args.output:
        output_path = Path(args.output)
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
        print(f"\nReport also saved to: {output_path}")


if __name__ == "__main__":
    main()

