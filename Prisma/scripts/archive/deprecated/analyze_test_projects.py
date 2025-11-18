#!/usr/bin/env python3
"""
Test Project Analysis Script
Identifies empty, superfluous, and active test projects in the solution
Part of ExxerAI ADR-005 to ADR-011 closing verification
"""

import re
from pathlib import Path
from dataclasses import dataclass
from typing import List, Dict
from collections import defaultdict


@dataclass
class TestProject:
    """Represents a test project in the solution"""
    name: str
    path: str
    test_class_count: int
    test_method_count: int
    cs_file_count: int
    category: str  # Derived from folder structure
    has_placeholder: bool


def find_all_test_projects(solution_path: Path) -> List[Path]:
    """Find all test project files in the solution"""
    test_projects = []

    # Search for all .csproj files in tests directory
    tests_dir = solution_path / "code" / "src" / "tests"
    if tests_dir.exists():
        test_projects = list(tests_dir.rglob("*.csproj"))

    return test_projects


def analyze_test_project(project_path: Path, tests_root: Path) -> TestProject:
    """Analyze a single test project"""
    project_dir = project_path.parent
    project_name = project_path.stem

    # Determine category from folder structure
    relative_path = project_dir.relative_to(tests_root)
    category_folder = str(relative_path.parts[0]) if relative_path.parts else "Unknown"

    # Count CS files
    cs_files = list(project_dir.rglob("*.cs"))
    cs_file_count = len(cs_files)

    # Count test classes and methods
    test_class_count = 0
    test_method_count = 0
    has_placeholder = False

    for cs_file in cs_files:
        try:
            with open(cs_file, 'r', encoding='utf-8') as f:
                content = f.read()

            # Check for placeholder tests
            if 'PlaceholderTests' in cs_file.name or 'PlaceholderTest' in content:
                has_placeholder = True

            # Skip if no test attributes
            if '[Fact' not in content and '[Theory' not in content:
                continue

            # Count test classes
            class_pattern = r'public\s+(?:sealed\s+)?class\s+(\w+)'
            class_matches = re.findall(class_pattern, content, re.MULTILINE)
            test_class_count += len(class_matches)

            # Count test methods
            method_pattern = r'\[(?:Fact|Theory)(?:\([^\)]*\))?\]'
            method_matches = re.findall(method_pattern, content, re.MULTILINE)
            test_method_count += len(method_matches)

        except Exception as e:
            print(f"   ⚠️ Error processing {cs_file}: {e}")
            continue

    return TestProject(
        name=project_name,
        path=str(project_path.relative_to(tests_root.parent.parent)),
        test_class_count=test_class_count,
        test_method_count=test_method_count,
        cs_file_count=cs_file_count,
        category=category_folder,
        has_placeholder=has_placeholder
    )


def categorize_projects(projects: List[TestProject]) -> Dict:
    """Categorize projects as active, empty-intentional, or superfluous"""

    active = []
    empty_intentional = []
    empty_suspicious = []

    for project in projects:
        if project.test_method_count > 0:
            # Has actual tests
            active.append(project)
        elif project.has_placeholder:
            # Empty but has placeholder - intentionally waiting for SUTs
            empty_intentional.append(project)
        elif project.cs_file_count == 0:
            # Completely empty - suspicious
            empty_suspicious.append(project)
        else:
            # Has CS files but no tests - needs investigation
            empty_suspicious.append(project)

    return {
        'active': active,
        'empty_intentional': empty_intentional,
        'empty_suspicious': empty_suspicious,
        'total': len(projects)
    }


def generate_report(categorization: Dict, output_path: Path):
    """Generate detailed analysis report"""

    active = categorization['active']
    empty_intentional = categorization['empty_intentional']
    empty_suspicious = categorization['empty_suspicious']
    total = categorization['total']

    report_lines = []
    report_lines.append("=" * 100)
    report_lines.append("TEST PROJECT ANALYSIS REPORT")
    report_lines.append("ADR-005 to ADR-011 Closing Verification")
    report_lines.append("=" * 100)
    report_lines.append("")

    report_lines.append("📊 SUMMARY")
    report_lines.append("=" * 100)
    report_lines.append("")
    report_lines.append(f"Total Test Projects:        {total}")
    report_lines.append(f"✅ Active (with tests):     {len(active)}")
    report_lines.append(f"⏳ Empty (intentional):     {len(empty_intentional)}")
    report_lines.append(f"⚠️  Empty (suspicious):     {len(empty_suspicious)}")
    report_lines.append("")

    # Active projects
    if active:
        report_lines.append("=" * 100)
        report_lines.append("✅ ACTIVE TEST PROJECTS (With Test Methods)")
        report_lines.append("=" * 100)
        report_lines.append("")

        # Group by category
        by_category = defaultdict(list)
        for proj in active:
            by_category[proj.category].append(proj)

        for category in sorted(by_category.keys()):
            report_lines.append(f"📁 {category}")
            report_lines.append("-" * 100)
            for proj in by_category[category]:
                report_lines.append(f"   • {proj.name}")
                report_lines.append(f"     Classes: {proj.test_class_count}, Methods: {proj.test_method_count}, Files: {proj.cs_file_count}")
                report_lines.append(f"     Path: {proj.path}")
            report_lines.append("")

        report_lines.append("=" * 100)
        report_lines.append("")

    # Empty intentional
    if empty_intentional:
        report_lines.append("=" * 100)
        report_lines.append("⏳ EMPTY TEST PROJECTS (Intentional - Waiting for SUTs)")
        report_lines.append("=" * 100)
        report_lines.append("")

        by_category = defaultdict(list)
        for proj in empty_intentional:
            by_category[proj.category].append(proj)

        for category in sorted(by_category.keys()):
            report_lines.append(f"📁 {category}")
            report_lines.append("-" * 100)
            for proj in by_category[category]:
                report_lines.append(f"   • {proj.name}")
                report_lines.append(f"     Status: Has PlaceholderTests - Waiting for implementation")
                report_lines.append(f"     Path: {proj.path}")
            report_lines.append("")

        report_lines.append("=" * 100)
        report_lines.append("")

    # Suspicious empty
    if empty_suspicious:
        report_lines.append("=" * 100)
        report_lines.append("⚠️  EMPTY TEST PROJECTS (Suspicious - Potential Superfluous)")
        report_lines.append("=" * 100)
        report_lines.append("")

        by_category = defaultdict(list)
        for proj in empty_suspicious:
            by_category[proj.category].append(proj)

        for category in sorted(by_category.keys()):
            report_lines.append(f"📁 {category}")
            report_lines.append("-" * 100)
            for proj in by_category[category]:
                report_lines.append(f"   • {proj.name}")
                if proj.cs_file_count == 0:
                    report_lines.append(f"     Status: Completely empty (no CS files)")
                else:
                    report_lines.append(f"     Status: Has {proj.cs_file_count} CS files but no test methods")
                report_lines.append(f"     Path: {proj.path}")
                report_lines.append(f"     ⚠️  ACTION: Review if this project is needed or can be removed")
            report_lines.append("")

        report_lines.append("=" * 100)
        report_lines.append("")

    # Recommendations
    report_lines.append("=" * 100)
    report_lines.append("💡 RECOMMENDATIONS")
    report_lines.append("=" * 100)
    report_lines.append("")

    if empty_suspicious:
        report_lines.append("⚠️  SUSPICIOUS PROJECTS FOUND:")
        report_lines.append("")
        report_lines.append("Review the suspicious projects listed above:")
        report_lines.append("  1. If waiting for SUTs: Add PlaceholderTests.cs to mark as intentional")
        report_lines.append("  2. If no longer needed: Remove from solution and delete directory")
        report_lines.append("  3. If migration artifact: Verify and clean up")
        report_lines.append("")
    else:
        report_lines.append("✅ NO SUSPICIOUS PROJECTS FOUND")
        report_lines.append("")
        report_lines.append("All empty projects have PlaceholderTests indicating they're waiting for SUTs.")
        report_lines.append("Solution structure is clean and ready for ADR-005 to ADR-011 closing.")
        report_lines.append("")

    report_lines.append("=" * 100)

    report_text = "\n".join(report_lines)
    output_path.write_text(report_text, encoding='utf-8')

    return report_text


def main():
    """Main execution"""
    print("🔍 Test Project Analysis")
    print("=" * 100)
    print()

    solution_root = Path(".")
    tests_dir = solution_root / "code" / "src" / "tests"

    if not tests_dir.exists():
        print(f"❌ Tests directory not found: {tests_dir}")
        return

    # Find all test projects
    print("📦 Scanning for test projects...")
    project_files = find_all_test_projects(solution_root)
    print(f"   Found {len(project_files)} test projects")
    print()

    # Analyze each project
    print("🔄 Analyzing test projects...")
    projects = []
    for project_file in project_files:
        project = analyze_test_project(project_file, tests_dir)
        projects.append(project)
    print(f"   Analyzed {len(projects)} projects")
    print()

    # Categorize projects
    print("📊 Categorizing projects...")
    categorization = categorize_projects(projects)
    print()

    # Generate report
    print("📄 Generating report...")
    output_path = Path("docs/TestProjectAnalysis.txt")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    generate_report(categorization, output_path)
    print(f"   Report saved to: {output_path}")
    print()

    # Print summary
    print("=" * 100)
    print("📊 ANALYSIS SUMMARY")
    print("=" * 100)
    print()
    print(f"Total Test Projects:        {categorization['total']}")
    print(f"✅ Active (with tests):     {len(categorization['active'])}")
    print(f"⏳ Empty (intentional):     {len(categorization['empty_intentional'])}")
    print(f"⚠️  Empty (suspicious):     {len(categorization['empty_suspicious'])}")
    print()

    if categorization['empty_suspicious']:
        print("⚠️  WARNING: Found suspicious empty test projects!")
        print(f"   Review report for details: {output_path}")
    else:
        print("✅ SUCCESS: All empty projects are intentional placeholders!")
        print("   Solution is clean and ready for ADR closing.")

    print()
    print("=" * 100)


if __name__ == "__main__":
    main()
