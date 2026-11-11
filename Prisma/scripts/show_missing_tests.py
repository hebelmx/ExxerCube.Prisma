#!/usr/bin/env python3
"""Display missing tests from coverage analysis."""

import json
from pathlib import Path

# Load the analysis report
report_path = Path("Prisma/scripts/test_coverage_analysis.json")
with open(report_path, encoding='utf-8') as f:
    data = json.load(f)

print("=" * 80)
print("MISSING TESTS ANALYSIS")
print("=" * 80)

missing = data.get('missing_by_filename', {})
print(f"\nTotal missing test methods: {data['missing_summary']['method_count']}")
print(f"Total files with missing methods: {len(missing)}\n")

for filename, info in sorted(missing.items()):
    print(f"{filename}:")
    print(f"  Original path: {info['original_path']}")
    print(f"  Missing methods ({info['method_count']}): {', '.join(info['methods'][:10])}")
    if len(info['methods']) > 10:
        print(f"    ... and {len(info['methods']) - 10} more")
    print()

print("\n" + "=" * 80)
print("NEW TESTS (in split projects, not in original)")
print("=" * 80)

new = data.get('new_by_filename', {})
print(f"\nTotal new test methods: {data['new_summary']['method_count']}")
print(f"Total new files: {data['new_summary']['file_count']}\n")

for filename, info in sorted(new.items()):
    print(f"{filename}:")
    print(f"  Split paths: {', '.join(info['split_paths'])}")
    print(f"  New methods ({info['method_count']}): {', '.join(info['methods'][:10])}")
    if len(info['methods']) > 10:
        print(f"    ... and {len(info['methods']) - 10} more")
    print()

