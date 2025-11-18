#!/usr/bin/env python3
"""
Verify if Components.Integration.Test is the renamed ExxerAI.IntegrationTests
Quick deterministic check using our existing verification logic
"""

import re
from pathlib import Path
from dataclasses import dataclass
from typing import List, Set
from collections import defaultdict


@dataclass
class TestClass:
    """Represents a test class found in source code"""
    class_name: str
    namespace: str
    file_path: str
    method_count: int


def extract_test_classes(root_path: Path, label: str) -> List[TestClass]:
    """Extract all test classes from .cs files"""
    test_classes = []
    cs_files = list(root_path.rglob('*.cs'))

    print(f"   Scanning {len(cs_files)} .cs files in {label}...")

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

            # Find all public classes
            class_pattern = r'public\s+(?:sealed\s+)?class\s+(\w+)\s*(?::|$|<|\()'
            class_names = re.findall(class_pattern, content, re.MULTILINE)

            if not class_names:
                continue

            # Count test methods
            method_pattern = r'\[(?:Fact|Theory)(?:\([^\)]*\))?\]'
            method_count = len(re.findall(method_pattern, content, re.MULTILINE))

            for class_name in class_names:
                test_classes.append(TestClass(
                    class_name=class_name,
                    namespace=namespace,
                    file_path=str(cs_file.relative_to(root_path)),
                    method_count=method_count
                ))

        except Exception as e:
            print(f"   ⚠️ Error processing {cs_file}: {e}")
            continue

    return test_classes


def main():
    """Main execution"""
    print("🔍 Verifying if Components.Integration.Test is the renamed old IntegrationTests")
    print("=" * 100)
    print()

    # Paths to compare
    old_integration = Path("migration_backup/ExxerAI.IntegrationTests")
    components_test = Path("code/src/tests/05IntegrationTests/ExxerAI.Components.Integration.Test")

    if not old_integration.exists():
        print(f"❌ Old integration tests not found: {old_integration}")
        return

    if not components_test.exists():
        print(f"❌ Components test project not found: {components_test}")
        return

    # Extract test classes from both
    print("📦 Scanning OLD integration tests (backup)...")
    old_classes = extract_test_classes(old_integration, "OLD")
    print(f"   Found {len(old_classes)} test classes")
    print()

    print("📂 Scanning COMPONENTS integration tests (current)...")
    current_classes = extract_test_classes(components_test, "COMPONENTS")
    print(f"   Found {len(current_classes)} test classes")
    print()

    # Build class name sets
    old_class_names = {tc.class_name for tc in old_classes}
    current_class_names = {tc.class_name for tc in current_classes}

    # Compare
    print("🔄 Comparing test classes...")
    matches = old_class_names & current_class_names
    old_only = old_class_names - current_class_names
    current_only = current_class_names - old_class_names

    print()
    print("=" * 100)
    print("📊 COMPARISON RESULTS")
    print("=" * 100)
    print()
    print(f"OLD Integration Tests:      {len(old_class_names)} unique classes")
    print(f"Components Integration:     {len(current_class_names)} unique classes")
    print()
    print(f"✅ Matching Classes:        {len(matches)} ({len(matches)/len(old_class_names)*100:.1f}% of OLD)")
    print(f"📦 OLD only:                {len(old_only)}")
    print(f"➕ Components only:         {len(current_only)}")
    print()

    # Determine if they're the same
    match_percentage = len(matches) / len(old_class_names) * 100 if old_class_names else 0

    if match_percentage > 80:
        print("=" * 100)
        print("✅ CONFIRMED: Components.Integration.Test IS the renamed old IntegrationTests!")
        print("=" * 100)
        print()
        print(f"Match rate: {match_percentage:.1f}%")
        print()
        print("This project should be migrated to evocative architecture projects:")
        print("  - A2A tests → ExxerAI.Conduit.IntegrationTest (📡)")
        print("  - Container tests → Infrastructure integration tests")
        print("  - Authentication tests → ExxerAI.Sentinel.IntegrationTest (🛡️)")
        print("  - Knowledge Store tests → ExxerAI.Vault.IntegrationTest (🏛️)")
        print("  - LLM tests → ExxerAI.Cortex.IntegrationTest (🧠)")
        print("  - Document tests → ExxerAI.Nexus.IntegrationTest (⚡)")
        print()
    else:
        print("=" * 100)
        print("❌ NOT A MATCH: Components.Integration.Test is NOT the old IntegrationTests")
        print("=" * 100)
        print(f"Match rate: {match_percentage:.1f}% (too low)")
        print()

    # Show missing classes if any
    if old_only and match_percentage > 80:
        print("📦 Classes from OLD not in Components (likely moved elsewhere):")
        print("-" * 100)
        for class_name in sorted(old_only):
            print(f"   • {class_name}")
        print()

    if current_only:
        print("➕ New classes in Components (not in OLD backup):")
        print("-" * 100)
        for class_name in sorted(current_only):
            print(f"   • {class_name}")
        print()

    print("=" * 100)


if __name__ == "__main__":
    main()
