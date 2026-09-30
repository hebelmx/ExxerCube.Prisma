#!/usr/bin/env python3
"""
Analyze fixture dependencies across test projects.

Finds:
1. Which test projects have their own Fixtures/TestData
2. Which test projects might use relative paths to access shared fixtures
3. File counts and sizes for each fixture directory

This helps prepare for folder restructuration by identifying what needs to be copied.
"""

import json
import re
from pathlib import Path
from typing import Dict, List, Set
from datetime import datetime


class FixtureDependencyAnalyzer:
    """Analyzes fixture dependencies across test projects."""

    def __init__(self, base_path: str = "Code/Src/CSharp"):
        self.base_path = Path(base_path)

    def find_fixture_directories(self) -> Dict[str, Path]:
        """Find all Fixtures and TestData directories in test projects."""
        fixture_dirs = {}

        # Find all test projects
        for test_project in self.base_path.glob("Tests*"):
            if not test_project.is_dir():
                continue

            # Look for Fixtures or TestData directories
            for fixture_name in ["Fixtures", "TestData"]:
                fixture_dir = test_project / fixture_name
                if fixture_dir.exists() and fixture_dir.is_dir():
                    fixture_dirs[str(test_project.name)] = fixture_dir

        return fixture_dirs

    def analyze_fixture_directory(self, fixture_dir: Path) -> Dict:
        """Analyze a fixture directory."""
        file_count = 0
        total_size = 0
        file_types = {}

        for file in fixture_dir.rglob("*"):
            if file.is_file():
                file_count += 1
                total_size += file.stat().st_size

                # Track file types
                ext = file.suffix.lower()
                if ext not in file_types:
                    file_types[ext] = 0
                file_types[ext] += 1

        return {
            'path': str(fixture_dir),
            'file_count': file_count,
            'total_size_mb': total_size / (1024 * 1024),
            'file_types': file_types
        }

    def find_relative_path_references(self, test_project: Path) -> List[str]:
        """Find relative path references in test files."""
        references = []

        for cs_file in test_project.glob("*.cs"):
            try:
                with open(cs_file, 'r', encoding='utf-8') as f:
                    content = f.read()

                # Look for relative path patterns
                patterns = [
                    r'\.\./',  # ../
                    r'Path\.Combine\([^)]*\.\.[^)]*\)',  # Path.Combine with ..
                    r'@"[^"]*\.\.[^"]*"',  # @"..\..\path"
                    r'"[^"]*\.\.[^"]*"',  # "../path"
                ]

                for pattern in patterns:
                    matches = re.findall(pattern, content)
                    if matches:
                        references.extend(matches)

            except Exception as e:
                print(f"Error reading {cs_file}: {e}")

        return list(set(references))  # Unique references

    def analyze_all(self) -> Dict:
        """Analyze all fixture dependencies."""
        print("="*80)
        print("FIXTURE DEPENDENCY ANALYSIS")
        print("="*80)
        print("")

        fixture_dirs = self.find_fixture_directories()

        print(f"Found {len(fixture_dirs)} test projects with fixtures")
        print("")

        analysis = {
            'metadata': {
                'generated_on': datetime.now().isoformat(),
                'base_path': str(self.base_path),
                'analyzer_version': '1.0.0'
            },
            'projects': {},
            'summary': {
                'total_projects': len(fixture_dirs),
                'total_files': 0,
                'total_size_mb': 0,
                'projects_with_relative_paths': 0
            }
        }

        for project_name, fixture_dir in sorted(fixture_dirs.items()):
            print(f"📁 {project_name}")

            # Analyze fixture directory
            fixture_analysis = self.analyze_fixture_directory(fixture_dir)
            print(f"   Path: {fixture_analysis['path']}")
            print(f"   Files: {fixture_analysis['file_count']}")
            print(f"   Size: {fixture_analysis['total_size_mb']:.2f} MB")
            print(f"   Types: {', '.join(f'{k}({v})' for k, v in fixture_analysis['file_types'].items())}")

            # Find relative path references
            test_project_path = self.base_path / project_name
            relative_refs = self.find_relative_path_references(test_project_path)

            if relative_refs:
                print(f"   ⚠️  Found {len(relative_refs)} relative path references:")
                for ref in relative_refs[:5]:  # Show first 5
                    print(f"      - {ref}")
                if len(relative_refs) > 5:
                    print(f"      ... and {len(relative_refs) - 5} more")

            analysis['projects'][project_name] = {
                'fixture_analysis': fixture_analysis,
                'relative_path_references': relative_refs,
                'has_relative_paths': len(relative_refs) > 0
            }

            analysis['summary']['total_files'] += fixture_analysis['file_count']
            analysis['summary']['total_size_mb'] += fixture_analysis['total_size_mb']
            if relative_refs:
                analysis['summary']['projects_with_relative_paths'] += 1

            print("")

        print("="*80)
        print("SUMMARY")
        print("="*80)
        print(f"Total test projects with fixtures: {analysis['summary']['total_projects']}")
        print(f"Total fixture files: {analysis['summary']['total_files']}")
        print(f"Total fixture size: {analysis['summary']['total_size_mb']:.2f} MB")
        print(f"Projects with relative paths: {analysis['summary']['projects_with_relative_paths']}")
        print("")

        if analysis['summary']['projects_with_relative_paths'] > 0:
            print("⚠️  RECOMMENDATION:")
            print("   These projects use relative paths that will break after restructuration.")
            print("   Consider:")
            print("   1. Copy shared fixtures to each test project")
            print("   2. OR Update paths to use AppContext.BaseDirectory")
            print("   3. OR Use .csproj <None Include> to copy fixtures to output")
        else:
            print("✅ No relative path dependencies found!")
            print("   Restructuration should be safe for fixtures.")

        print("")

        return analysis


def main():
    import sys

    base_path = "Code/Src/CSharp" if len(sys.argv) < 2 else sys.argv[1]

    analyzer = FixtureDependencyAnalyzer(base_path)
    analysis = analyzer.analyze_all()

    # Save analysis
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_file = f"fixture_dependency_analysis_{timestamp}.json"

    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(analysis, f, indent=2, ensure_ascii=False)

    print(f"Analysis saved to: {output_file}")


if __name__ == "__main__":
    main()
