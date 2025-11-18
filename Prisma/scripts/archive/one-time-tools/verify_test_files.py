#!/usr/bin/env python3
"""
Verify Test Files with Regex Pattern Matching
==============================================

PURPOSE: Identifies real test files by checking for xUnit test attributes
         using precise regex patterns to avoid false positives.

PATTERNS:
- [Fact] or [Fact(...)]
- [Theory] or [Theory(...)]
- Matches only when attributes are properly formatted

USAGE:
    python scripts/verify_test_files.py --path "code/src/Losetests"
"""

import re
from pathlib import Path
from typing import Dict, List, Tuple
from dataclasses import dataclass, asdict
import argparse
import json


@dataclass
class FileAnalysis:
    """Analysis results for a single file."""
    filename: str
    is_test_file: bool
    fact_count: int
    theory_count: int
    total_tests: int
    file_type: str  # 'test', 'fixture', 'helper', 'unknown'
    namespace: str
    class_name: str


class TestFileVerifier:
    """Verifies which files are actual test files using regex patterns."""

    def __init__(self, directory: str):
        self.directory = Path(directory)
        self.results: List[FileAnalysis] = []

        # Regex patterns for xUnit test attributes
        # Matches: [Fact], [Fact()], [Fact(Timeout = 30_000)], etc.
        self.fact_pattern = re.compile(r'\[\s*Fact\w*\s*(?:\([^)]*\))?\s*\]', re.IGNORECASE)

        # Matches: [Theory], [Theory()], [Theory(Timeout = 30_000)], etc.
        self.theory_pattern = re.compile(r'\[\s*Theory\w*\s*(?:\([^)]*\))?\s*\]', re.IGNORECASE)

        # Pattern for namespace
        self.namespace_pattern = re.compile(r'namespace\s+([\w.]+)\s*;?')

        # Pattern for class name
        self.class_pattern = re.compile(r'(?:public|internal|private|protected)?\s*(?:sealed|abstract)?\s*class\s+(\w+)')

    def analyze_file(self, file_path: Path) -> FileAnalysis:
        """Analyze a single C# file for test attributes."""
        filename = file_path.name

        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()

            # Count test attributes
            fact_matches = self.fact_pattern.findall(content)
            theory_matches = self.theory_pattern.findall(content)

            fact_count = len(fact_matches)
            theory_count = len(theory_matches)
            total_tests = fact_count + theory_count

            # Extract namespace
            namespace_match = self.namespace_pattern.search(content)
            namespace = namespace_match.group(1) if namespace_match else ""

            # Extract class name
            class_match = self.class_pattern.search(content)
            class_name = class_match.group(1) if class_match else ""

            # Determine file type
            is_test_file = total_tests > 0

            if is_test_file:
                file_type = 'test'
            elif 'Fixture' in class_name:
                file_type = 'fixture'
            elif 'Helper' in class_name or 'Extensions' in class_name:
                file_type = 'helper'
            else:
                file_type = 'unknown'

            return FileAnalysis(
                filename=filename,
                is_test_file=is_test_file,
                fact_count=fact_count,
                theory_count=theory_count,
                total_tests=total_tests,
                file_type=file_type,
                namespace=namespace,
                class_name=class_name
            )

        except Exception as e:
            print(f"⚠️  Error analyzing {filename}: {e}")
            return FileAnalysis(
                filename=filename,
                is_test_file=False,
                fact_count=0,
                theory_count=0,
                total_tests=0,
                file_type='error',
                namespace='',
                class_name=''
            )

    def verify_directory(self) -> Dict:
        """Verify all C# files in directory."""
        print("\n" + "="*80)
        print("🔍 VERIFYING TEST FILES WITH REGEX PATTERNS")
        print("="*80)

        if not self.directory.exists():
            print(f"\n❌ ERROR: Directory not found: {self.directory}")
            return {}

        print(f"\n📂 Scanning: {self.directory}")

        # Get all C# files
        cs_files = sorted([f for f in self.directory.iterdir() if f.suffix == '.cs'])

        print(f"📊 Found {len(cs_files)} C# files\n")

        # Analyze each file
        for cs_file in cs_files:
            analysis = self.analyze_file(cs_file)
            self.results.append(analysis)

        # Generate summary
        test_files = [r for r in self.results if r.is_test_file]
        fixture_files = [r for r in self.results if r.file_type == 'fixture']
        helper_files = [r for r in self.results if r.file_type == 'helper']
        unknown_files = [r for r in self.results if r.file_type == 'unknown']

        total_test_methods = sum(r.total_tests for r in test_files)

        summary = {
            'total_files': len(cs_files),
            'test_files': len(test_files),
            'fixture_files': len(fixture_files),
            'helper_files': len(helper_files),
            'unknown_files': len(unknown_files),
            'total_test_methods': total_test_methods,
            'fact_methods': sum(r.fact_count for r in test_files),
            'theory_methods': sum(r.theory_count for r in test_files)
        }

        return {
            'summary': summary,
            'test_files': [asdict(r) for r in test_files],
            'fixture_files': [asdict(r) for r in fixture_files],
            'helper_files': [asdict(r) for r in helper_files],
            'unknown_files': [asdict(r) for r in unknown_files],
            'all_files': [asdict(r) for r in self.results]
        }

    def print_report(self, report: Dict):
        """Print verification report to console."""
        summary = report['summary']

        print("\n" + "="*80)
        print("📊 VERIFICATION SUMMARY")
        print("="*80)
        print(f"Total files:         {summary['total_files']}")
        print(f"  ✅ Test files:      {summary['test_files']} ({summary['total_test_methods']} test methods)")
        print(f"     - [Fact]:        {summary['fact_methods']}")
        print(f"     - [Theory]:      {summary['theory_methods']}")
        print(f"  🔧 Fixture files:   {summary['fixture_files']}")
        print(f"  📦 Helper files:    {summary['helper_files']}")
        print(f"  ❓ Unknown files:   {summary['unknown_files']}")

        # Test Files
        if report['test_files']:
            print("\n" + "="*80)
            print(f"✅ TEST FILES ({len(report['test_files'])})")
            print("="*80)
            for file_info in sorted(report['test_files'], key=lambda x: x['filename']):
                print(f"\n📄 {file_info['filename']}")
                print(f"   Class: {file_info['class_name']}")
                print(f"   Namespace: {file_info['namespace']}")
                print(f"   Tests: {file_info['total_tests']} ([Fact]: {file_info['fact_count']}, [Theory]: {file_info['theory_count']})")

        # Fixture Files
        if report['fixture_files']:
            print("\n" + "="*80)
            print(f"🔧 FIXTURE FILES ({len(report['fixture_files'])})")
            print("="*80)
            for file_info in sorted(report['fixture_files'], key=lambda x: x['filename']):
                print(f"\n📄 {file_info['filename']}")
                print(f"   Class: {file_info['class_name']}")
                print(f"   Namespace: {file_info['namespace']}")
                print(f"   Type: Test fixture/helper class (NOT a test)")

        # Helper Files
        if report['helper_files']:
            print("\n" + "="*80)
            print(f"📦 HELPER FILES ({len(report['helper_files'])})")
            print("="*80)
            for file_info in sorted(report['helper_files'], key=lambda x: x['filename']):
                print(f"\n📄 {file_info['filename']}")
                print(f"   Class: {file_info['class_name']}")
                print(f"   Type: Helper/Extension class (NOT a test)")

        # Unknown Files
        if report['unknown_files']:
            print("\n" + "="*80)
            print(f"❓ UNKNOWN FILES ({len(report['unknown_files'])})")
            print("="*80)
            for file_info in sorted(report['unknown_files'], key=lambda x: x['filename']):
                print(f"\n📄 {file_info['filename']}")
                print(f"   Class: {file_info['class_name']}")
                print(f"   Namespace: {file_info['namespace']}")

        print("\n" + "="*80)
        print("💡 CONCLUSIONS")
        print("="*80)
        print(f"✅ {summary['test_files']} files are actual test files (contain [Fact] or [Theory])")
        print(f"⚠️  {summary['fixture_files'] + summary['helper_files']} files are support files (fixtures/helpers)")
        if summary['unknown_files'] > 0:
            print(f"❓ {summary['unknown_files']} files need manual review")
        print("\n")

    def save_report(self, report: Dict, output_file: str):
        """Save verification report to JSON."""
        try:
            with open(output_file, 'w', encoding='utf-8') as f:
                json.dump(report, f, indent=2)
            print(f"✅ Report saved to: {output_file}")
        except Exception as e:
            print(f"❌ Error saving report: {e}")


def main():
    parser = argparse.ArgumentParser(
        description='Verify test files using regex pattern matching for xUnit attributes',
        formatter_class=argparse.RawDescriptionHelpFormatter
    )

    parser.add_argument('--path', type=str, required=True,
                       help='Path to directory containing test files')
    parser.add_argument('--output', type=str, default='test_verification_report.json',
                       help='Output JSON report filename')

    args = parser.parse_args()

    # Run verification
    verifier = TestFileVerifier(args.path)
    report = verifier.verify_directory()

    if report:
        verifier.print_report(report)
        verifier.save_report(report, args.output)

    return 0


if __name__ == '__main__':
    exit(main())
