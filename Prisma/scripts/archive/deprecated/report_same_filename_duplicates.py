#!/usr/bin/env python3
"""
Same Filename Duplicate Report
Identifies test files with identical filenames in different locations.
Helps identify copy/paste duplication that should be consolidated.

IMPORTANT: Respects pyramidal testing strategy - duplicates across allowed
test projects (Adapter, Integration, Components) are INTENTIONAL.
"""
import json
import os
from collections import defaultdict
from pathlib import Path
from typing import Dict, List, Set

# Allowed test projects for pyramidal testing (intentional duplicates)
ALLOWED_PYRAMIDAL_PROJECTS = {
    # Adapter layer
    'ExxerAI.Vault.Adapter.Test',
    'ExxerAI.Cortex.Adapter.Test',
    'ExxerAI.Datastream.Adapter.Test',
    'ExxerAI.Nexus.Adapter.Test',
    'ExxerAI.Gatekeeper.Adapter.Test',
    'ExxerAI.Sentinel.Adapter.Test',
    'ExxerAI.Conduit.Adapter.Test',
    'ExxerAI.Core.Adapter.Test',
    'ExxerAI.MCP.Adapter.Test',
    'ExxerAI.Axis.Adapter.Test',
    # Integration layer
    'ExxerAI.Integration.Test',
    'ExxerAI.Components.Integration.Test',
    'ExxerAI.Nexus.Integration.Test',
    'ExxerAI.Vault.Integration.Test',
    '05IntegrationTests',  # Project folder name
    '04AdapterTests',      # Project folder name
}


def load_duplicate_analysis(json_file: Path) -> dict:
    """Load the duplicate analysis JSON"""
    with open(json_file, 'r', encoding='utf-8') as f:
        return json.load(f)


def group_by_filename(data: dict) -> Dict[str, List[dict]]:
    """Group duplicate test occurrences by filename"""
    filename_groups = defaultdict(list)

    for group in data['duplicate_groups']:
        # Get all unique filenames in this group
        filenames = {}
        for occ in group['occurrences']:
            filename = os.path.basename(occ['test_file_path'])
            if filename not in filenames:
                filenames[filename] = []
            filenames[filename].append({
                'test_class': group['test_class_name'],
                'path': occ['test_file_path'],
                'project': occ['test_project'],
                'layer': occ['layer'],
                'namespace': occ['namespace']
            })

        # Only track filenames that appear multiple times
        for filename, occurrences in filenames.items():
            if len(occurrences) >= 2 or len(filenames) > 1:
                filename_groups[filename].extend(occurrences)

    return filename_groups


def is_pyramidal_duplication(occurrences: List[dict]) -> bool:
    """Check if duplication is across allowed pyramidal test projects (INTENTIONAL)"""
    projects = set(occ['project'] for occ in occurrences)

    # If all occurrences are in allowed pyramidal projects, it's intentional
    if all(proj in ALLOWED_PYRAMIDAL_PROJECTS for proj in projects):
        return True

    return False


def analyze_duplication_severity(occurrences: List[dict]) -> tuple:
    """Analyze how severe the duplication is

    Returns: (severity, priority, duplicate_count, same_project, same_layer, is_pyramidal)
    """
    # Count unique file paths
    unique_paths = set(occ['path'] for occ in occurrences)
    duplicate_count = len(unique_paths)

    # Check if they're in same project
    projects = set(occ['project'] for occ in occurrences)
    same_project = len(projects) == 1

    # Check if they're in same layer
    layers = set(occ['layer'] for occ in occurrences)
    same_layer = len(layers) == 1

    # Check if this is intentional pyramidal duplication
    is_pyramidal = is_pyramidal_duplication(occurrences)

    # EXCLUDE pyramidal duplicates - they're intentional for safety
    if is_pyramidal:
        return None, None, duplicate_count, same_project, same_layer, True

    # Only flag TRUE problems: duplicates within same project/folder
    if duplicate_count >= 4 and same_project:
        severity = "CRITICAL"
        priority = 1
    elif duplicate_count == 3 and same_project:
        severity = "HIGH"
        priority = 2
    elif duplicate_count == 2 and same_project:
        severity = "MEDIUM"
        priority = 3
    else:
        # Different projects, not pyramidal - unusual
        severity = "LOW"
        priority = 4

    return severity, priority, duplicate_count, same_project, same_layer, is_pyramidal


def generate_report(filename_groups: Dict[str, List[dict]], output_file: Path):
    """Generate detailed same-filename duplication report"""

    # Analyze and sort by severity
    analyzed_groups = []
    for filename, occurrences in filename_groups.items():
        severity, priority, dup_count, same_proj, same_layer = analyze_duplication_severity(occurrences)

        if dup_count >= 2:  # Only show actual duplicates
            analyzed_groups.append({
                'filename': filename,
                'occurrences': occurrences,
                'severity': severity,
                'priority': priority,
                'duplicate_count': dup_count,
                'same_project': same_proj,
                'same_layer': same_layer
            })

    # Sort by priority (most severe first)
    analyzed_groups.sort(key=lambda x: (x['priority'], -x['duplicate_count']))

    output_lines = []

    def out(line=''):
        output_lines.append(line)
        print(line)

    out("=" * 100)
    out("SAME FILENAME DUPLICATE REPORT")
    out("Focus: Identify copy/paste file duplication for consolidation")
    out("=" * 100)
    out()
    out(f"Total files with same-name duplicates: {len(analyzed_groups)}")

    # Summary by severity
    critical = sum(1 for g in analyzed_groups if g['severity'] == 'CRITICAL')
    high = sum(1 for g in analyzed_groups if g['severity'] == 'HIGH')
    medium = sum(1 for g in analyzed_groups if g['severity'] == 'MEDIUM')
    low = sum(1 for g in analyzed_groups if g['severity'] == 'LOW')

    out(f"  🔴 CRITICAL (4+ copies): {critical}")
    out(f"  🟠 HIGH (3 copies): {high}")
    out(f"  🟡 MEDIUM (2 copies, same project): {medium}")
    out(f"  🟢 LOW (2 copies, different projects): {low}")
    out()

    # CRITICAL severity
    critical_files = [g for g in analyzed_groups if g['severity'] == 'CRITICAL']
    if critical_files:
        out("=" * 100)
        out("🔴 CRITICAL - 4+ Copies (Immediate Consolidation Needed)")
        out("=" * 100)
        out()

        for group in critical_files:
            out(f"📄 {group['filename']} ({group['duplicate_count']} copies)")
            out(f"   Test classes: {', '.join(set(occ['test_class'] for occ in group['occurrences']))}")
            out()

            # Show each location
            for i, occ in enumerate(sorted(group['occurrences'], key=lambda x: x['path']), 1):
                out(f"   Copy {i}: {occ['layer']} → {occ['project']}")
                out(f"           {occ['path']}")
                out(f"           Namespace: {occ['namespace']}")

            out()
            out(f"   💡 RECOMMENDATION: Consolidate into ONE file and delete {group['duplicate_count'] - 1} copies")
            out(f"      Consider keeping the most comprehensive version and removing others")
            out()
            out("-" * 100)
            out()

    # HIGH severity
    high_files = [g for g in analyzed_groups if g['severity'] == 'HIGH']
    if high_files:
        out("=" * 100)
        out("🟠 HIGH - 3 Copies (Review and Consolidate)")
        out("=" * 100)
        out()

        for group in high_files:
            out(f"📄 {group['filename']} ({group['duplicate_count']} copies)")
            out(f"   Test classes: {', '.join(set(occ['test_class'] for occ in group['occurrences']))}")
            out()

            for i, occ in enumerate(sorted(group['occurrences'], key=lambda x: x['path']), 1):
                out(f"   Copy {i}: {occ['layer']} → {occ['project']}")
                out(f"           {occ['path']}")

            out()
            out(f"   💡 RECOMMENDATION: Review and merge into ONE file")
            out()
            out("-" * 100)
            out()

    # MEDIUM severity
    medium_files = [g for g in analyzed_groups if g['severity'] == 'MEDIUM']
    if medium_files:
        out("=" * 100)
        out("🟡 MEDIUM - 2 Copies in Same Project (Review)")
        out("=" * 100)
        out()

        for group in medium_files[:15]:  # Show first 15
            out(f"📄 {group['filename']} (2 copies in same project)")
            out(f"   Test classes: {', '.join(set(occ['test_class'] for occ in group['occurrences']))}")
            out()

            for i, occ in enumerate(sorted(group['occurrences'], key=lambda x: x['path']), 1):
                out(f"   Copy {i}: {occ['path']}")

            out()
            out(f"   💡 RECOMMENDATION: Check if these test different scenarios or are duplicates")
            out()
            out("-" * 100)
            out()

        if len(medium_files) > 15:
            out(f"... and {len(medium_files) - 15} more medium priority duplicates")
            out()

    # LOW severity (brief listing)
    low_files = [g for g in analyzed_groups if g['severity'] == 'LOW']
    if low_files:
        out("=" * 100)
        out("🟢 LOW - 2 Copies in Different Projects (Likely Intentional)")
        out("=" * 100)
        out()

        for group in low_files[:10]:
            projects = set(occ['project'] for occ in group['occurrences'])
            out(f"📄 {group['filename']} - Projects: {', '.join(sorted(projects))}")

        if len(low_files) > 10:
            out(f"... and {len(low_files) - 10} more low priority duplicates")
        out()

    out("=" * 100)
    out("CONSOLIDATION PRIORITY SUMMARY")
    out("=" * 100)
    out()
    out("1. CRITICAL: Delete 3-7 copies, keep 1 comprehensive version")
    out("2. HIGH: Review and merge 2 duplicate copies")
    out("3. MEDIUM: Verify if testing different scenarios or true duplication")
    out("4. LOW: Likely pyramidal/architectural - probably intentional")
    out()
    out("=" * 100)
    out("END OF REPORT")
    out("=" * 100)

    # Write to file
    with open(output_file, 'w', encoding='utf-8') as f:
        f.write('\n'.join(output_lines))

    print(f"\n📄 Report saved to {output_file}")


def main():
    json_file = Path('docs/DuplicateTestsGroupedAnalysis.json')
    output_file = Path('docs/SameFilenameReview.txt')

    print(f"📚 Loading duplicate analysis from {json_file}...")
    data = load_duplicate_analysis(json_file)

    print(f"🔍 Grouping by filename...")
    filename_groups = group_by_filename(data)

    print(f"\n📊 Generating same-filename review report...")
    generate_report(filename_groups, output_file)

    print("\n✅ Report complete!")


if __name__ == '__main__':
    main()
