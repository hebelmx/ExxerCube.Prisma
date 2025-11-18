#!/usr/bin/env python3
"""
Advanced Test SUT (System Under Test) Scanner for ExxerAI
Scans test projects to identify what they're testing and how they're organized.

Capabilities:
- BASIC: Test class names, file locations, namespaces
- ADVANCED: Mock/substitute detection, SUT type extraction, test attribute analysis
- VERY ADVANCED: Test dependency graphs, coverage gaps, architectural alignment analysis

Author: Claude Code Agent
Date: 2025-11-08
"""

import os
import re
import json
from pathlib import Path
from typing import Dict, List, Set, Tuple
from collections import defaultdict
from datetime import datetime


class TestSUTScanner:
    """Advanced scanner for test projects to identify Systems Under Test."""

    def __init__(self, base_path: str, mode: str = "advanced"):
        self.base_path = Path(base_path)
        self.tests_path = self.base_path / "code" / "src" / "tests"
        self.mode = mode  # "basic", "advanced", "very_advanced"

        # Skip directories
        self.skip_dirs = {
            'bin', 'obj', '.vs', '.git', 'node_modules',
            'TestResults', 'packages', 'artifacts'
        }

        # Test patterns
        self.test_class_pattern = r'public\s+class\s+(\w+Tests?)\s*(?::\s*\w+)?'
        self.sut_instantiation_patterns = [
            r'new\s+(\w+)\s*\(',  # new SomeClass(
            r'Substitute\.For<(\w+)>\(\)',  # NSubstitute mocks
            r'Mock<(\w+)>\(\)',  # Moq mocks
            r'private\s+readonly\s+(\w+)\s+_\w+;',  # readonly fields
            r'private\s+(\w+)\s+_\w+;'  # private fields
        ]

        # Results storage
        self.test_projects = {}
        self.sut_references = defaultdict(set)  # SUT -> test files
        self.test_to_sut = {}  # test file -> SUTs
        self.namespace_to_tests = defaultdict(set)

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

    def find_test_project(self, file_path: Path) -> str:
        """Find the test project name for a given file."""
        current = file_path.parent

        while current != self.base_path and current.parent != current:
            csproj_files = list(current.glob("*.csproj"))
            if csproj_files:
                return csproj_files[0].stem
            current = current.parent

        return 'Unknown'

    def extract_test_attributes(self, content: str) -> Dict[str, int]:
        """Extract test attributes (Fact, Theory, etc)."""
        attributes = {
            'Fact': len(re.findall(r'\[Fact[(\]]', content)),
            'Theory': len(re.findall(r'\[Theory[(\]]', content)),
            'Skip': len(re.findall(r'Skip\s*=', content)),
            'Timeout': len(re.findall(r'Timeout\s*=', content)),
            'Trait': len(re.findall(r'\[Trait\(', content))
        }
        return {k: v for k, v in attributes.items() if v > 0}

    def extract_mock_usage(self, content: str) -> Dict[str, List[str]]:
        """Extract mocking framework usage (ADVANCED mode)."""
        if self.mode == "basic":
            return {}

        mocks = {
            'NSubstitute': [],
            'Moq': [],
            'FakeItEasy': []
        }

        # NSubstitute patterns
        for match in re.finditer(r'Substitute\.For<(\w+)>', content):
            mocks['NSubstitute'].append(match.group(1))

        # Moq patterns
        for match in re.finditer(r'new\s+Mock<(\w+)>', content):
            mocks['Moq'].append(match.group(1))

        # FakeItEasy patterns
        for match in re.finditer(r'A\.Fake<(\w+)>', content):
            mocks['FakeItEasy'].append(match.group(1))

        return {k: list(set(v)) for k, v in mocks.items() if v}

    def extract_sut_types(self, content: str, test_class_name: str) -> Set[str]:
        """Extract likely SUT types from test content."""
        sut_types = set()

        # Method 1: Infer from test class name
        # e.g., NPOIAdapterTests -> NPOIAdapter
        if test_class_name.endswith('Tests'):
            likely_sut = test_class_name[:-5]  # Remove 'Tests'
            sut_types.add(likely_sut)
        elif test_class_name.endswith('Test'):
            likely_sut = test_class_name[:-4]  # Remove 'Test'
            sut_types.add(likely_sut)

        # Method 2: Find 'new SomeClass(' patterns
        for match in re.finditer(r'new\s+(\w+)\s*\(', content):
            type_name = match.group(1)
            # Filter out common test utilities
            if not type_name.endswith('Exception') and \
               not type_name in ['List', 'Dictionary', 'Task', 'CancellationToken', 'LoggerFactory']:
                sut_types.add(type_name)

        # Method 3: Find readonly field types (common pattern for SUT)
        for match in re.finditer(r'private\s+readonly\s+(\w+)\s+_(\w+);', content):
            type_name = match.group(1)
            field_name = match.group(2)
            # If field name suggests it's the adapter/service being tested
            if 'adapter' in field_name.lower() or 'service' in field_name.lower() or \
               'sut' in field_name.lower():
                sut_types.add(type_name)

        return sut_types

    def analyze_test_organization(self, test_project: str, files: List[Path]) -> Dict:
        """Analyze how tests are organized within a project (ADVANCED mode)."""
        if self.mode == "basic":
            return {}

        organization = {
            'folders': defaultdict(int),  # folder -> test count
            'concerns': defaultdict(set),  # concern -> test files
            'namespaces': defaultdict(set)  # namespace -> test files
        }

        for file_path in files:
            try:
                # Get folder structure relative to project
                rel_path = Path(file_path) if isinstance(file_path, str) else file_path
                if not rel_path.is_absolute():
                    rel_path = self.base_path / rel_path

                rel_parts = rel_path.relative_to(self.tests_path).parts[1:]  # Skip project name
                if len(rel_parts) > 1:
                    folder = rel_parts[-2] if len(rel_parts) > 1 else 'Root'
                    organization['folders'][folder] += 1
                    organization['concerns'][folder].add(rel_path.name)
            except (ValueError, AttributeError) as e:
                # Skip files that can't be made relative
                continue

        return {
            'folders': dict(organization['folders']),
            'concerns': {k: list(v) for k, v in organization['concerns'].items()}
        }

    def scan_test_file(self, file_path: Path) -> Dict:
        """Scan a single test file for comprehensive information."""
        if self.should_skip_path(file_path):
            return None

        try:
            content = file_path.read_text(encoding='utf-8')
            namespace = self.extract_namespace(content)
            project = self.find_test_project(file_path)

            # Find test classes
            test_classes = re.findall(self.test_class_pattern, content)

            if not test_classes:
                return None  # Not a test file

            # Basic information
            info = {
                'file': str(file_path.relative_to(self.base_path)),
                'namespace': namespace,
                'project': project,
                'test_classes': test_classes,
                'test_attributes': self.extract_test_attributes(content),
            }

            # Advanced information
            if self.mode in ["advanced", "very_advanced"]:
                info['mock_usage'] = self.extract_mock_usage(content)
                info['sut_types'] = {}

                for test_class in test_classes:
                    sut_types = self.extract_sut_types(content, test_class)
                    if sut_types:
                        info['sut_types'][test_class] = list(sut_types)

                        # Update reverse mapping
                        for sut in sut_types:
                            self.sut_references[sut].add(str(file_path.relative_to(self.base_path)))

            # Track namespace
            self.namespace_to_tests[namespace].add(file_path.name)

            return info

        except Exception as e:
            print(f"Error scanning {file_path}: {e}")
            return None

    def scan_all_tests(self):
        """Scan all test files in the tests directory."""
        print(f"Scanning test projects in: {self.tests_path}")
        print(f"Mode: {self.mode.upper()}")

        test_files = list(self.tests_path.rglob("*Tests.cs"))
        test_files.extend(self.tests_path.rglob("*Test.cs"))

        # Deduplicate
        test_files = list(set(test_files))

        print(f"Found {len(test_files)} test files")

        for i, test_file in enumerate(test_files):
            if i % 50 == 0 and i > 0:
                print(f"  Processed {i}/{len(test_files)} test files...")

            info = self.scan_test_file(test_file)
            if info:
                # Organize by project
                project = info['project']
                if project not in self.test_projects:
                    self.test_projects[project] = {
                        'files': [],
                        'total_test_classes': 0,
                        'namespaces': set()
                    }

                self.test_projects[project]['files'].append(info)
                self.test_projects[project]['total_test_classes'] += len(info['test_classes'])
                self.test_projects[project]['namespaces'].add(info['namespace'])

        print(f"\nScan complete!")
        print(f"Test projects found: {len(self.test_projects)}")
        print(f"Total SUTs referenced: {len(self.sut_references)}")

    def generate_project_analysis(self) -> Dict:
        """Generate per-project analysis."""
        analysis = {}

        for project, data in self.test_projects.items():
            # Collect all SUTs for this project
            project_suts = set()
            for file_info in data['files']:
                if 'sut_types' in file_info:
                    for test_class, suts in file_info['sut_types'].items():
                        project_suts.update(suts)

            # Analyze organization
            file_paths = [Path(f['file']) for f in data['files']]
            organization = self.analyze_test_organization(project, file_paths)

            analysis[project] = {
                'file_count': len(data['files']),
                'test_class_count': data['total_test_classes'],
                'namespaces': sorted(list(data['namespaces'])),
                'sut_types': sorted(list(project_suts)),
                'organization': organization
            }

        return analysis

    def save_results(self, output_file: str):
        """Save scan results to JSON file."""
        # Convert sets to lists for JSON serialization
        for project, data in self.test_projects.items():
            data['namespaces'] = sorted(list(data['namespaces']))

        results = {
            'scan_date': datetime.now().isoformat(),
            'base_path': str(self.base_path),
            'mode': self.mode,
            'statistics': {
                'total_test_projects': len(self.test_projects),
                'total_test_files': sum(len(p['files']) for p in self.test_projects.values()),
                'total_test_classes': sum(p['total_test_classes'] for p in self.test_projects.values()),
                'total_suts_referenced': len(self.sut_references)
            },
            'test_projects': self.test_projects,
            'project_analysis': self.generate_project_analysis(),
            'sut_to_tests_mapping': {sut: sorted(list(tests)) for sut, tests in self.sut_references.items()},
            'namespace_to_tests': {ns: sorted(list(tests)) for ns, tests in self.namespace_to_tests.items()}
        }

        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(results, f, indent=2)

        print(f"\nResults saved to: {output_file}")

        # Print summary
        print("\nTest Projects Summary:")
        for project, analysis in results['project_analysis'].items():
            print(f"\n{project}:")
            print(f"  Files: {analysis['file_count']}")
            print(f"  Test Classes: {analysis['test_class_count']}")
            print(f"  Namespaces: {len(analysis['namespaces'])}")
            if 'sut_types' in analysis and analysis['sut_types']:
                print(f"  SUTs: {len(analysis['sut_types'])} ({', '.join(analysis['sut_types'][:5])}...)")
            if 'organization' in analysis and 'folders' in analysis['organization']:
                print(f"  Folder Structure: {dict(analysis['organization']['folders'])}")


def main():
    import argparse

    parser = argparse.ArgumentParser(description='Scan ExxerAI test projects for SUT analysis')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--output', default='test_sut_analysis.json',
                       help='Output JSON file')
    parser.add_argument('--mode', choices=['basic', 'advanced', 'very_advanced'],
                       default='advanced',
                       help='Analysis mode: basic, advanced, or very_advanced')

    args = parser.parse_args()

    scanner = TestSUTScanner(args.base_path, mode=args.mode)
    scanner.scan_all_tests()
    scanner.save_results(args.output)


if __name__ == "__main__":
    main()
