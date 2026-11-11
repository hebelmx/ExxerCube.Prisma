#!/usr/bin/env python3
"""
Test Coverage Verification from Backup
Ensures all production classes from backup have corresponding test classes
Part of ExxerAI ADR migration validation and closing initiative
"""

import re
from pathlib import Path
from dataclasses import dataclass
from typing import List, Dict
from collections import defaultdict


@dataclass
class ProductionClass:
    """Represents a production class"""
    class_name: str
    namespace: str
    file_path: str
    class_type: str  # 'class', 'interface', 'record', 'struct'


def extract_production_classes(root_path: Path) -> List[ProductionClass]:
    """Extract all production classes from .cs files"""
    classes = []

    cs_files = list(root_path.rglob('*.cs'))
    print(f"   Scanning {len(cs_files)} .cs files...")

    for cs_file in cs_files:
        try:
            with open(cs_file, 'r', encoding='utf-8') as f:
                content = f.read()

            # Extract namespace
            namespace_match = re.search(r'namespace\s+([\w\.]+)', content)
            namespace = namespace_match.group(1) if namespace_match else 'Unknown'

            # Find classes, interfaces, records, structs
            patterns = [
                (r'public\s+(?:sealed\s+|static\s+|abstract\s+)?class\s+(\w+)', 'class'),
                (r'public\s+interface\s+(I\w+)', 'interface'),
                (r'public\s+record\s+(\w+)', 'record'),
                (r'public\s+(?:readonly\s+)?struct\s+(\w+)', 'struct')
            ]

            for pattern, class_type in patterns:
                matches = re.findall(pattern, content, re.MULTILINE)
                for class_name in matches:
                    # Skip test classes, builders, and base classes
                    if any(skip in class_name for skip in ['Test', 'Tests', 'TestBase', 'Builder']):
                        continue

                    classes.append(ProductionClass(
                        class_name=class_name,
                        namespace=namespace,
                        file_path=str(cs_file.relative_to(root_path)),
                        class_type=class_type
                    ))

        except Exception as e:
            print(f"   ⚠️  Error processing {cs_file}: {e}")
            continue

    return classes


def extract_test_class_names(root_path: Path) -> set:
    """Extract all test class names (not full TestClass objects, just names)"""
    test_names = set()

    cs_files = list(root_path.rglob('*.cs'))

    for cs_file in cs_files:
        try:
            with open(cs_file, 'r', encoding='utf-8') as f:
                content = f.read()

            # Skip if no test attributes
            if '[Fact' not in content and '[Theory' not in content:
                continue

            # Find all public classes
            class_pattern = r'public\s+(?:sealed\s+)?class\s+(\w+)'
            class_names = re.findall(class_pattern, content, re.MULTILINE)
            test_names.update(class_names)

        except:
            continue

    return test_names


def find_missing_tests(production_classes: List[ProductionClass], test_names: set) -> Dict:
    """Find production classes without corresponding test classes"""

    missing = []
    covered = []

    for prod_class in production_classes:
        # Look for test class with common naming patterns
        possible_test_names = [
            f"{prod_class.class_name}Tests",
            f"{prod_class.class_name}Test",
            f"{prod_class.class_name}UnitTests",
            f"{prod_class.class_name}IntegrationTests",
            f"{prod_class.class_name}AdapterTests",
        ]

        # Check if any of the possible test names exist
        has_test = any(name in test_names for name in possible_test_names)

        if has_test:
            covered.append(prod_class)
        else:
            # Skip interfaces and certain types that don't need tests
            if prod_class.class_type == 'interface':
                continue
            if any(skip in prod_class.class_name for skip in ['Model', 'Dto', 'Request', 'Response', 'Options', 'Settings', 'Config']):
                continue

            missing.append(prod_class)

    return {
        'missing': missing,
        'covered': covered,
        'total_production': len(production_classes),
        'total_tests': len(test_names)
    }


def generate_report(analysis: Dict, output_path: Path):
    """Generate detailed text report"""

    missing = analysis['missing']
    covered = analysis['covered']

    report_lines = []
    report_lines.append("=" * 100)
    report_lines.append("TEST COVERAGE VERIFICATION FROM BACKUP")
    report_lines.append("=" * 100)
    report_lines.append("")
    report_lines.append("📊 SUMMARY")
    report_lines.append("=" * 100)
    report_lines.append("")
    report_lines.append(f"Production Classes (Backup):  {analysis['total_production']}")
    report_lines.append(f"Test Classes (Current):       {analysis['total_tests']}")
    report_lines.append(f"")
    report_lines.append(f"✅ Covered:                   {len(covered)}")
    report_lines.append(f"❌ Missing Tests:             {len(missing)}")
    report_lines.append("")

    if missing:
        report_lines.append("=" * 100)
        report_lines.append("❌ PRODUCTION CLASSES WITHOUT TEST COVERAGE")
        report_lines.append("=" * 100)
        report_lines.append("")

        # Group by namespace
        by_namespace = defaultdict(list)
        for prod in missing:
            by_namespace[prod.namespace].append(prod)

        for namespace in sorted(by_namespace.keys()):
            report_lines.append(f"📦 {namespace}")
            report_lines.append("-" * 100)
            for prod in by_namespace[namespace]:
                report_lines.append(f"   • {prod.class_name} ({prod.class_type})")
                report_lines.append(f"     Path: {prod.file_path}")
            report_lines.append("")

        report_lines.append("=" * 100)
        report_lines.append("")

    # Coverage percentage
    if analysis['total_production'] > 0:
        coverage_pct = (len(covered) / analysis['total_production']) * 100
        report_lines.append(f"📈 Test Coverage: {coverage_pct:.1f}%")
        report_lines.append("")

    report_text = "\n".join(report_lines)
    output_path.write_text(report_text, encoding='utf-8')

    return report_text


def main():
    """Main execution"""
    print("🔍 Test Coverage Verification from Backup")
    print("=" * 100)
    print()

    backup_path = Path("migration_backup/ExxerAI.Infrastructure")
    current_tests_path = Path("code/src/tests")

    if not backup_path.exists():
        print(f"❌ Backup path not found: {backup_path}")
        return

    if not current_tests_path.exists():
        print(f"❌ Current tests path not found: {current_tests_path}")
        return

    # Extract production classes from backup
    print("📦 Scanning BACKUP production classes...")
    production_classes = extract_production_classes(backup_path)
    print(f"   Found {len(production_classes)} production classes")
    print()

    # Extract test class names from current tests
    print("📂 Scanning CURRENT test classes...")
    test_names = extract_test_class_names(current_tests_path)
    print(f"   Found {len(test_names)} test classes")
    print()

    # Find missing tests
    print("🔄 Analyzing test coverage...")
    analysis = find_missing_tests(production_classes, test_names)
    print()

    # Generate report
    print("📄 Generating report...")
    output_path = Path("docs/TestCoverageFromBackup.txt")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    generate_report(analysis, output_path)
    print(f"   Report saved to: {output_path}")
    print()

    # Print summary
    print("=" * 100)
    print("📊 COVERAGE SUMMARY")
    print("=" * 100)
    print()
    print(f"Production Classes:    {analysis['total_production']}")
    print(f"Test Classes:          {analysis['total_tests']}")
    print()
    print(f"✅ Covered:            {len(analysis['covered'])}")
    print(f"❌ Missing Tests:      {len(analysis['missing'])}")

    if analysis['total_production'] > 0:
        coverage_pct = (len(analysis['covered']) / analysis['total_production']) * 100
        print(f"\n📈 Coverage: {coverage_pct:.1f}%")

    print()
    print("=" * 100)


if __name__ == "__main__":
    main()
