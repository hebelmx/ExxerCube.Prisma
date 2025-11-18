#!/usr/bin/env python3
"""
Test Migration Verification Script
Ensures all test classes from migration backup still exist in current test projects
Part of ExxerAI ADR migration validation and closing initiative
"""

import re
from pathlib import Path
from dataclasses import dataclass
from typing import List, Set, Dict
from collections import defaultdict


@dataclass
class TestClass:
    """Represents a test class found in source code"""
    class_name: str
    namespace: str
    file_path: str
    method_count: int
    location: str  # 'backup' or 'current'


def extract_test_classes(root_path: Path, location: str) -> List[TestClass]:
    """
    Extract all test classes from .cs files using [Fact/[Theory attribute detection

    Args:
        root_path: Root directory to scan
        location: 'backup' or 'current' to identify source

    Returns:
        List of TestClass objects found
    """
    test_classes = []

    # Find all .cs files
    cs_files = list(root_path.rglob('*.cs'))
    print(f"   Scanning {len(cs_files)} .cs files in {location}...")

    for cs_file in cs_files:
        try:
            with open(cs_file, 'r', encoding='utf-8') as f:
                content = f.read()

            # Skip if no test attributes
            if '[Fact' not in content and '[Theory' not in content:
                continue

            # Extract namespace
            namespace_match = re.search(r'namespace\s+([\w\.]+)', content)
            namespace = namespace_match.group(1) if namespace_match else 'Unknown'

            # Find all public classes (test files can have multiple test classes)
            class_pattern = r'public\s+(?:sealed\s+)?class\s+(\w+)\s*(?::|$|<|\()'
            class_names = re.findall(class_pattern, content, re.MULTILINE)

            if not class_names:
                continue

            # Count test methods
            method_pattern = r'\[(?:Fact|Theory)(?:\([^\)]*\))?\]'
            method_count = len(re.findall(method_pattern, content, re.MULTILINE))

            # For each class, check if it actually has test methods
            for class_name in class_names:
                # Simple heuristic: if file has test attributes and this is a public class, count it
                test_classes.append(TestClass(
                    class_name=class_name,
                    namespace=namespace,
                    file_path=str(cs_file.relative_to(root_path)),
                    method_count=method_count,
                    location=location
                ))

        except Exception as e:
            print(f"   ⚠️ Error processing {cs_file}: {e}")
            continue

    return test_classes


def analyze_migration(backup_classes: List[TestClass], current_classes: List[TestClass]) -> Dict:
    """
    Analyze test migration to find missing, preserved, and new test classes

    Returns:
        Dictionary with analysis results
    """
    # Build lookup dictionaries by class name
    backup_by_name = defaultdict(list)
    current_by_name = defaultdict(list)

    for tc in backup_classes:
        backup_by_name[tc.class_name].append(tc)

    for tc in current_classes:
        current_by_name[tc.class_name].append(tc)

    # Find missing, preserved, and new tests
    missing = []
    preserved = []
    new = []

    # Check each backup test
    for class_name, backup_instances in backup_by_name.items():
        if class_name in current_by_name:
            # Class exists in current - preserved
            for backup_tc in backup_instances:
                preserved.append({
                    'class_name': class_name,
                    'backup_namespace': backup_tc.namespace,
                    'backup_path': backup_tc.file_path,
                    'current_instances': current_by_name[class_name]
                })
        else:
            # Class missing from current tests
            for backup_tc in backup_instances:
                missing.append({
                    'class_name': class_name,
                    'namespace': backup_tc.namespace,
                    'file_path': backup_tc.file_path,
                    'method_count': backup_tc.method_count
                })

    # Find new tests (in current but not in backup)
    for class_name, current_instances in current_by_name.items():
        if class_name not in backup_by_name:
            for current_tc in current_instances:
                new.append({
                    'class_name': class_name,
                    'namespace': current_tc.namespace,
                    'file_path': current_tc.file_path,
                    'method_count': current_tc.method_count
                })

    return {
        'missing': missing,
        'preserved': preserved,
        'new': new,
        'backup_total': len(backup_classes),
        'current_total': len(current_classes)
    }


def generate_report(analysis: Dict, output_path: Path):
    """Generate detailed text report of migration verification"""

    missing = analysis['missing']
    preserved = analysis['preserved']
    new = analysis['new']

    report_lines = []
    report_lines.append("=" * 100)
    report_lines.append("TEST MIGRATION VERIFICATION REPORT")
    report_lines.append("=" * 100)
    report_lines.append("")
    report_lines.append("📊 SUMMARY")
    report_lines.append("=" * 100)
    report_lines.append("")
    report_lines.append(f"Backup Test Classes:   {analysis['backup_total']}")
    report_lines.append(f"Current Test Classes:  {analysis['current_total']}")
    report_lines.append(f"")
    report_lines.append(f"✅ Preserved:          {len(preserved)}")
    report_lines.append(f"❌ Missing:            {len(missing)}")
    report_lines.append(f"➕ New:               {len(new)}")
    report_lines.append("")

    if missing:
        report_lines.append("=" * 100)
        report_lines.append("❌ MISSING TEST CLASSES (In Backup, NOT in Current)")
        report_lines.append("=" * 100)
        report_lines.append("")

        for i, test in enumerate(missing, 1):
            report_lines.append(f"{i}. {test['class_name']}")
            report_lines.append(f"   Namespace: {test['namespace']}")
            report_lines.append(f"   Backup Path: {test['file_path']}")
            report_lines.append(f"   Test Methods: {test['method_count']}")
            report_lines.append("")

        report_lines.append("=" * 100)
        report_lines.append("")

    if new:
        report_lines.append("=" * 100)
        report_lines.append("➕ NEW TEST CLASSES (In Current, NOT in Backup)")
        report_lines.append("=" * 100)
        report_lines.append("")

        for i, test in enumerate(new, 1):
            report_lines.append(f"{i}. {test['class_name']}")
            report_lines.append(f"   Namespace: {test['namespace']}")
            report_lines.append(f"   Current Path: {test['file_path']}")
            report_lines.append(f"   Test Methods: {test['method_count']}")
            report_lines.append("")

        report_lines.append("=" * 100)
        report_lines.append("")

    # Write report
    report_text = "\n".join(report_lines)
    output_path.write_text(report_text, encoding='utf-8')

    return report_text


def main():
    """Main execution"""
    print("🔍 Test Migration Verification")
    print("=" * 100)
    print()

    # Paths
    backup_path = Path("migration_backup/ExxerAI.IntegrationTests")  # OLD integration tests (pre-ADR-005)
    current_path = Path("code/src/tests")  # CURRENT tests (post-ADR-005)

    if not backup_path.exists():
        print(f"❌ Backup path not found: {backup_path}")
        return

    if not current_path.exists():
        print(f"❌ Current tests path not found: {current_path}")
        return

    # Extract test classes from backup
    print("📦 Scanning BACKUP test classes...")
    backup_classes = extract_test_classes(backup_path, 'backup')
    print(f"   Found {len(backup_classes)} test classes in backup")
    print()

    # Extract test classes from current
    print("📂 Scanning CURRENT test classes...")
    current_classes = extract_test_classes(current_path, 'current')
    print(f"   Found {len(current_classes)} test classes in current tests")
    print()

    # Analyze migration
    print("🔄 Analyzing migration...")
    analysis = analyze_migration(backup_classes, current_classes)
    print()

    # Generate report
    print("📄 Generating report...")
    output_path = Path("docs/TestMigrationVerification.txt")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    report = generate_report(analysis, output_path)
    print(f"   Report saved to: {output_path}")
    print()

    # Print summary
    print("=" * 100)
    print("📊 VERIFICATION SUMMARY")
    print("=" * 100)
    print()
    print(f"Backup Test Classes:   {analysis['backup_total']}")
    print(f"Current Test Classes:  {analysis['current_total']}")
    print()
    print(f"✅ Preserved:          {len(analysis['preserved'])}")
    print(f"❌ Missing:            {len(analysis['missing'])}")
    print(f"➕ New:               {len(analysis['new'])}")
    print()

    if analysis['missing']:
        print("⚠️  WARNING: Some test classes from backup are MISSING in current tests!")
        print(f"   See report for details: {output_path}")
    else:
        print("✅ SUCCESS: All backup test classes found in current tests!")

    print()
    print("=" * 100)


if __name__ == "__main__":
    main()
