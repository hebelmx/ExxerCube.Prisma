#!/usr/bin/env python3
"""
Analyze test coverage between monolithic Tests project and split test projects.
Identifies:
1. Tests in ExxerCube.Prisma.Tests that are NOT in any other test project (missing migrations)
2. Tests in split test projects that are NOT in ExxerCube.Prisma.Tests (new/placeholder tests)
3. Test method counts per project

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


class TestCoverageAnalyzer:
    """Analyzes test coverage across test projects."""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.src_path = self.base_path / "Prisma" / "Code" / "Src" / "CSharp"
        
        # Test method patterns - match both Fact and Theory attributes
        self.fact_pattern = r'\[Fact[^\]]*\]'
        self.theory_pattern = r'\[Theory[^\]]*\]'
        # Match test methods with Fact or Theory attributes (handles various method signatures)
        self.test_method_pattern = r'\[(?:Fact|Theory)[^\]]*\]\s*(?:public\s+)?(?:async\s+)?(?:Task\s+)?(?:void\s+)?(\w+)\s*\('
        # Also match test methods that might have attributes on separate lines
        self.test_method_multiline_pattern = r'\[(?:Fact|Theory)[^\]]*\]\s*(?:.*\n)*?\s*(?:public\s+)?(?:async\s+)?(?:Task\s+)?(?:void\s+)?(\w+)\s*\('
        
        # Results storage
        self.original_tests = {}  # file_path -> set of test methods
        self.split_project_tests = defaultdict(dict)  # project_name -> {file_path -> set of test methods}
        self.test_file_paths = {}  # normalized_path -> actual_path
        
    def normalize_path(self, path: Path) -> str:
        """Normalize path for comparison (relative to src_path)."""
        try:
            rel_path = path.relative_to(self.src_path)
            return str(rel_path).replace('\\', '/')
        except ValueError:
            return str(path)
    
    def normalize_filename(self, path: Path) -> str:
        """Get just the filename for comparison."""
        if isinstance(path, str):
            path = Path(path)
        return path.name.lower()
    
    def extract_test_methods(self, file_path: Path) -> Set[str]:
        """Extract test method names from a C# test file."""
        test_methods = set()
        try:
            content = file_path.read_text(encoding='utf-8')
            
            # Skip Python interop tests (being dropped)
            if 'PythonInteropServiceTests' in file_path.name or 'PrismaOcrWrapperAdapter' in content:
                return test_methods
            
            # Find all test methods - try both patterns
            matches = re.finditer(self.test_method_pattern, content, re.MULTILINE | re.DOTALL)
            for match in matches:
                method_name = match.group(1)
                test_methods.add(method_name)
            
            # Also try multiline pattern for methods with attributes on separate lines
            matches2 = re.finditer(self.test_method_multiline_pattern, content, re.MULTILINE | re.DOTALL)
            for match in matches2:
                method_name = match.group(1)
                test_methods.add(method_name)
                
        except Exception as e:
            print(f"Error reading {file_path}: {e}")
            
        return test_methods
    
    def scan_project(self, project_path: Path, project_name: str) -> Dict[str, Set[str]]:
        """Scan a test project for test files and methods."""
        test_files = {}
        
        if not project_path.exists():
            print(f"Warning: Project path does not exist: {project_path}")
            return test_files
        
        # Find all .cs files in the project
        for cs_file in project_path.rglob("*.cs"):
            # Skip bin/obj directories
            if any(part in ['bin', 'obj', '.vs', '.git'] for part in cs_file.parts):
                continue
                
            # Check if file contains test attributes
            try:
                content = cs_file.read_text(encoding='utf-8')
                if re.search(self.fact_pattern, content) or re.search(self.theory_pattern, content):
                    normalized_path = self.normalize_path(cs_file)
                    test_methods = self.extract_test_methods(cs_file)
                    if test_methods:  # Only add if has test methods
                        test_files[normalized_path] = test_methods
                        self.test_file_paths[normalized_path] = str(cs_file)
            except Exception as e:
                print(f"Error scanning {cs_file}: {e}")
        
        return test_files
    
    def analyze(self):
        """Run the analysis."""
        print("=" * 80)
        print("Test Coverage Analysis")
        print("=" * 80)
        
        # Scan original monolithic Tests project
        original_path = self.src_path / "Tests"
        print(f"\n1. Scanning original Tests project: {original_path}")
        self.original_tests = self.scan_project(original_path, "ExxerCube.Prisma.Tests")
        print(f"   Found {len(self.original_tests)} test files")
        
        # Count test methods in original
        original_method_count = sum(len(methods) for methods in self.original_tests.values())
        print(f"   Found {original_method_count} test methods")
        
        # Scan split test projects
        print(f"\n2. Scanning split test projects:")
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
        
        total_split_methods = 0
        for project_name, project_path in split_projects:
            if project_path.exists():
                tests = self.scan_project(project_path, project_name)
                self.split_project_tests[project_name] = tests
                method_count = sum(len(methods) for methods in tests.values())
                total_split_methods += method_count
                print(f"   {project_name}: {len(tests)} files, {method_count} methods")
            else:
                print(f"   {project_name}: Project not found")
        
        print(f"\n   Total split projects: {total_split_methods} test methods")
        
        # Build combined set of all split project test files
        # Use filename as key for comparison (since paths differ)
        all_split_tests_by_name = {}  # filename -> {path: methods}
        all_split_tests_by_path = {}  # path -> methods (for reporting)
        
        for project_name, project_tests in self.split_project_tests.items():
            for file_path, methods in project_tests.items():
                actual_path = Path(self.test_file_paths.get(file_path, file_path))
                filename = self.normalize_filename(actual_path)
                
                if filename not in all_split_tests_by_name:
                    all_split_tests_by_name[filename] = {}
                all_split_tests_by_name[filename][file_path] = methods
                all_split_tests_by_path[file_path] = methods
        
        # Find tests in original that are NOT in split projects
        print(f"\n3. Finding missing tests (in original, not in split projects):")
        print("   (Excluding Python interop tests - being dropped)")
        missing_tests = {}
        missing_by_filename = {}
        
        for file_path, methods in self.original_tests.items():
            actual_path = Path(self.test_file_paths.get(file_path, file_path))
            filename = self.normalize_filename(actual_path)
            
            # Skip Python interop tests (being dropped)
            if 'pythoninteropservicetests' in filename or 'prismaocrwrapperadapter' in filename.lower():
                print(f"   Skipping {filename} (Python interop - being dropped)")
                continue
            
            # Check if this filename exists in split projects
            if filename not in all_split_tests_by_name:
                # File completely missing
                missing_tests[file_path] = methods
                missing_by_filename[filename] = {
                    "original_path": file_path,
                    "methods": list(methods),
                    "method_count": len(methods)
                }
            else:
                # File exists, check methods
                split_occurrences = all_split_tests_by_name[filename]
                all_split_methods = set()
                for split_path, split_methods in split_occurrences.items():
                    all_split_methods.update(split_methods)
                
                missing_methods = methods - all_split_methods
                if missing_methods:
                    missing_tests[file_path] = missing_methods
                    if filename not in missing_by_filename:
                        missing_by_filename[filename] = {
                            "original_path": file_path,
                            "methods": [],
                            "method_count": 0
                        }
                    missing_by_filename[filename]["methods"].extend(list(missing_methods))
                    missing_by_filename[filename]["method_count"] += len(missing_methods)
        
        missing_file_count = len([f for f in missing_by_filename.keys() if missing_by_filename[f]["method_count"] == len(self.original_tests.get(missing_by_filename[f]["original_path"], set()))])
        missing_method_count = sum(len(methods) for methods in missing_tests.values())
        print(f"   Found {missing_file_count} files completely missing")
        print(f"   Found {missing_method_count} test methods missing")
        
        # Find tests in split projects that are NOT in original
        print(f"\n4. Finding new tests (in split projects, not in original):")
        new_tests = {}
        new_by_filename = {}
        
        # Build original tests by filename
        original_by_filename = {}
        for file_path, methods in self.original_tests.items():
            actual_path = Path(self.test_file_paths.get(file_path, file_path))
            filename = self.normalize_filename(actual_path)
            if filename not in original_by_filename:
                original_by_filename[filename] = set()
            original_by_filename[filename].update(methods)
        
        for file_path, methods in all_split_tests_by_path.items():
            actual_path = Path(self.test_file_paths.get(file_path, file_path))
            filename = self.normalize_filename(actual_path)
            
            if filename not in original_by_filename:
                # Completely new file
                new_tests[file_path] = methods
                if filename not in new_by_filename:
                    new_by_filename[filename] = {
                        "split_paths": [],
                        "methods": list(methods),
                        "method_count": len(methods)
                    }
                new_by_filename[filename]["split_paths"].append(file_path)
            else:
                # File exists, check for new methods
                original_methods = original_by_filename[filename]
                new_methods = methods - original_methods
                if new_methods:
                    if file_path not in new_tests:
                        new_tests[file_path] = set()
                    new_tests[file_path].update(new_methods)
                    if filename not in new_by_filename:
                        new_by_filename[filename] = {
                            "split_paths": [],
                            "methods": [],
                            "method_count": 0
                        }
                    new_by_filename[filename]["split_paths"].append(file_path)
                    new_by_filename[filename]["methods"].extend(list(new_methods))
                    new_by_filename[filename]["method_count"] += len(new_methods)
        
        new_file_count = len([f for f in new_tests.keys() if f not in self.original_tests])
        new_method_count = sum(len(methods) for methods in new_tests.values())
        print(f"   Found {new_file_count} new files")
        print(f"   Found {new_method_count} new test methods")
        
        # Generate report
        report = {
            "analysis_date": datetime.now().isoformat(),
            "original_project": {
                "name": "ExxerCube.Prisma.Tests",
                "file_count": len(self.original_tests),
                "method_count": original_method_count,
                "files": {path: list(methods) for path, methods in self.original_tests.items()}
            },
            "split_projects": {
                project_name: {
                    "file_count": len(tests),
                    "method_count": sum(len(m) for m in tests.values()),
                    "files": {path: list(methods) for path, methods in tests.items()}
                }
                for project_name, tests in self.split_project_tests.items()
            },
            "total_split_methods": total_split_methods,
            "missing_tests": {
                path: list(methods) for path, methods in missing_tests.items()
            },
            "missing_by_filename": missing_by_filename,
            "missing_summary": {
                "file_count": missing_file_count,
                "method_count": missing_method_count
            },
            "new_tests": {
                path: list(methods) for path, methods in new_tests.items()
            },
            "new_by_filename": new_by_filename,
            "new_summary": {
                "file_count": new_file_count,
                "method_count": new_method_count
            },
            "file_paths": self.test_file_paths
        }
        
        # Save report
        output_file = self.base_path / "Prisma" / "scripts" / "test_coverage_analysis.json"
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
        
        print(f"\n5. Report saved to: {output_file}")
        
        # Print summary
        print(f"\n" + "=" * 80)
        print("SUMMARY")
        print("=" * 80)
        print(f"Original Tests project: {original_method_count} test methods in {len(self.original_tests)} files")
        print(f"Split test projects: {total_split_methods} test methods")
        print(f"Missing from split: {missing_method_count} test methods")
        print(f"New in split: {new_method_count} test methods")
        print(f"Expected total (if no duplicates): {original_method_count + new_method_count}")
        print(f"Actual total discovered: {original_method_count + new_method_count - missing_method_count}")
        
        return report


def main():
    import argparse
    
    parser = argparse.ArgumentParser(description="Analyze test coverage between monolithic and split test projects")
    parser.add_argument("--base-path", type=str, default=".", help="Base path to ExxerCube.Prisma repository")
    parser.add_argument("--output", type=str, default=None, help="Output JSON file path (default: scripts/test_coverage_analysis.json)")
    
    args = parser.parse_args()
    
    analyzer = TestCoverageAnalyzer(args.base_path)
    report = analyzer.analyze()
    
    if args.output:
        output_path = Path(args.output)
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
        print(f"\nReport also saved to: {output_path}")


if __name__ == "__main__":
    main()

