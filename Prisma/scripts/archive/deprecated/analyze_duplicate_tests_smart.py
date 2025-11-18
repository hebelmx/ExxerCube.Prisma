#!/usr/bin/env python3
"""
Smart Duplicate Test Analyzer - Phase 1: Find True Duplicates
Groups duplicate test classes and analyzes their placement across layers.

Principle: Test objects in their creation layer
- Pyramidal testing (Unit → Adapter → Integration) is INTENTIONAL and GOOD
- Same test in wrong layer (e.g., Core test in Integration) is BAD
- Interface in Application, Implementation in Infrastructure is NORMAL
"""
import argparse
import fnmatch
import json
import re
from collections import defaultdict
from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Dict, List, Optional, Set

# Test naming patterns to extract what's being tested
TEST_NAMING_PATTERNS = [
    r'^(.+)Tests?$',                    # GoogleDriveServiceTests → GoogleDriveService
    r'^I(.+)Tests?$',                    # IDocumentProcessorTests → IDocumentProcessor
    r'^(.+)IntegrationTests?$',          # HybridKnowledgeIntegrationTests → HybridKnowledge
    r'^(.+)AdapterTests?$',              # SemanticSearchAdapterTests → SemanticSearch
]

# Helper class patterns to EXCLUDE (intentional duplicates)
HELPER_CLASS_PATTERNS: Set[str] = {
    '*Extensions', '*Extension', 'ServiceCollectionExtensions', 'ConfigurationExtensions',
    'ApplicationBuilderExtensions',
    '*Repository', 'InMemory*Repository', 'Mock*Repository',
    '*Helper', '*TestHelper', '*Fixture', '*TestFixture', '*ContainerFixture',
    '*Base', '*TestBase', 'TestContext',
    '*Configuration', '*Config', '*Setup', '*Builder',
    '*Migration', '*Snapshot', 'ApplicationDbContext*', 'CreateIdentitySchema*',
    '*Dto', '*Model', '*Response', '*Request',
    'SkipException', '*Utility', '*Utilities', '*Utils',
    'PlaceholderTests', 'PlaceholderTest',
}


@dataclass
class TestOccurrence:
    """Single occurrence of a test class"""
    test_class_name: str
    test_file_path: str
    test_project: str
    layer: str  # Unit, Adapter, Integration, Core, System, Unknown
    namespace: str
    tested_type: Optional[str] = None


@dataclass
class TypeDefinition:
    """Type definition from type mapping JSON"""
    type_name: str
    namespace: str
    kind: str  # class, interface, record, enum
    project: str
    layer: str  # Core, Infrastructure, Presentation, Unknown


@dataclass
class DuplicateTestGroup:
    """Group of duplicate test classes"""
    test_class_name: str
    tested_type: str
    type_definition: Optional[TypeDefinition]
    occurrences: List[TestOccurrence]
    duplicate_count: int
    layers_found: Set[str]
    is_pyramidal: bool  # True if intentional multi-layer testing
    is_problematic: bool  # True if same test in wrong layers


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description='Smart duplicate test analyzer - groups duplicates and analyzes patterns',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Analyze duplicate tests with type mapping
  python %(prog)s --type-mapping scripts/exxerai_types.json

  # Focus on specific component
  python %(prog)s --type-mapping scripts/exxerai_types.json --component Vault

  # Generate detailed JSON report
  python %(prog)s --type-mapping scripts/exxerai_types.json --output-json duplicates_analysis.json
"""
    )

    parser.add_argument('--type-mapping', type=str, required=True,
                        help='Path to type mapping JSON file (e.g., scripts/exxerai_types.json)')
    parser.add_argument('--root', type=str, default='code/src/tests',
                        help='Root directory for test files (default: code/src/tests)')
    parser.add_argument('--component', type=str,
                        help='Focus on specific component (e.g., Vault, Nexus, Cortex)')
    parser.add_argument('--output', type=str, default='docs/DuplicateTestsAnalysis.txt',
                        help='Output text report file')
    parser.add_argument('--output-json', type=str,
                        help='Output JSON file with detailed analysis')
    parser.add_argument('--min-duplicates', type=int, default=2,
                        help='Minimum number of duplicates to report (default: 2)')

    return parser.parse_args()


def load_type_mapping(mapping_file: Path) -> Dict[str, TypeDefinition]:
    """Load type definitions from JSON mapping file"""
    with open(mapping_file, 'r', encoding='utf-8') as f:
        data = json.load(f)

    type_map = {}
    for type_name, type_info in data.get('type_lookup', {}).items():
        type_map[type_name] = TypeDefinition(
            type_name=type_name,
            namespace=type_info.get('namespace', ''),
            kind=type_info.get('kind', 'unknown'),
            project=type_info.get('project', 'Unknown'),
            layer=determine_layer_from_namespace(type_info.get('namespace', ''))
        )

    return type_map


def determine_layer_from_namespace(namespace: str) -> str:
    """Determine architectural layer from namespace"""
    if not namespace:
        return 'Unknown'

    # Core layer
    if any(x in namespace for x in ['Domain', 'Application', 'Axioms']):
        return 'Core'

    # Infrastructure layer
    if any(x in namespace for x in [
        'Vault', 'Cortex', 'Datastream', 'Nexus', 'Gatekeeper',
        'Sentinel', 'Conduit', 'Chronos', 'Signal', 'Helix'
    ]):
        return 'Infrastructure'

    # Presentation layer
    if any(x in namespace for x in ['UI', 'Web', 'Api', 'Controllers']):
        return 'Presentation'

    return 'Unknown'


def determine_test_layer(file_path: str) -> str:
    """Determine test layer from file path"""
    path_lower = file_path.lower()

    # Test layer patterns
    if '00domain' in path_lower or 'domain.test' in path_lower:
        return 'Domain'
    if '01application' in path_lower or 'application.test' in path_lower:
        return 'Application'
    if '03unittests' in path_lower or '.unit.test' in path_lower:
        return 'Unit'
    if '04adaptertests' in path_lower or '.adapter.test' in path_lower:
        return 'Adapter'
    if '05integrationtests' in path_lower or '.integration' in path_lower:
        return 'Integration'
    if 'system.test' in path_lower or 'e2e' in path_lower:
        return 'System'

    return 'Unknown'


def matches_helper_pattern(name: str) -> bool:
    """Check if a test name matches helper class patterns (should be excluded)"""
    for pattern in HELPER_CLASS_PATTERNS:
        if fnmatch.fnmatch(name, pattern):
            return True
    return False


def extract_tested_type(test_class_name: str) -> Optional[str]:
    """Extract the type being tested from test class name"""
    for pattern in TEST_NAMING_PATTERNS:
        match = re.match(pattern, test_class_name, re.IGNORECASE)
        if match:
            return match.group(1)
    return None


def scan_test_files(root_path: Path, component_filter: Optional[str] = None) -> List[TestOccurrence]:
    """Scan test files and collect test occurrences"""
    occurrences = []

    for cs_file in root_path.rglob('*.cs'):
        # Skip if component filter specified
        if component_filter and component_filter.lower() not in str(cs_file).lower():
            continue

        try:
            with open(cs_file, 'r', encoding='utf-8') as f:
                content = f.read()

            # Extract namespace
            namespace_match = re.search(r'namespace\s+([\w\.]+)', content)
            namespace = namespace_match.group(1) if namespace_match else 'Unknown'

            # Find test classes (public class ending with Test/Tests)
            test_classes = re.findall(
                r'public\s+(?:sealed\s+)?class\s+(\w+Tests?)\s*(?::|$)',
                content
            )

            if not test_classes:
                continue

            # Determine test project and layer from path
            parts = cs_file.parts
            test_project = 'Unknown'
            for part in parts:
                if part.endswith('.Test') or part.endswith('Tests'):
                    test_project = part
                    break

            layer = determine_test_layer(str(cs_file))

            for test_class in test_classes:
                # Skip helper class patterns (intentional duplicates)
                if matches_helper_pattern(test_class):
                    continue

                tested_type = extract_tested_type(test_class)

                occurrences.append(TestOccurrence(
                    test_class_name=test_class,
                    test_file_path=str(cs_file),
                    test_project=test_project,
                    layer=layer,
                    namespace=namespace,
                    tested_type=tested_type
                ))

        except Exception as e:
            print(f"Warning: Could not process {cs_file}: {e}")
            continue

    return occurrences


def group_duplicates(
    occurrences: List[TestOccurrence],
    type_map: Dict[str, TypeDefinition]
) -> List[DuplicateTestGroup]:
    """Group test occurrences by test class name and analyze patterns"""

    # Group by test class name
    groups_by_name = defaultdict(list)
    for occ in occurrences:
        groups_by_name[occ.test_class_name].append(occ)

    duplicate_groups = []

    for test_name, occs in groups_by_name.items():
        if len(occs) < 2:
            continue  # Not a duplicate

        # Get tested type (should be same across all occurrences)
        tested_type = occs[0].tested_type or test_name

        # Look up type definition
        type_def = type_map.get(tested_type)

        # Collect layers where this test appears
        layers_found = {occ.layer for occ in occs}

        # Determine if this is pyramidal testing (intentional)
        is_pyramidal = is_pyramidal_testing(layers_found)

        # Determine if this is problematic
        is_problematic = is_problematic_duplication(occs, type_def)

        duplicate_groups.append(DuplicateTestGroup(
            test_class_name=test_name,
            tested_type=tested_type,
            type_definition=type_def,
            occurrences=occs,
            duplicate_count=len(occs),
            layers_found=layers_found,
            is_pyramidal=is_pyramidal,
            is_problematic=is_problematic
        ))

    return duplicate_groups


def is_pyramidal_testing(layers: Set[str]) -> bool:
    """Check if layers represent intentional pyramidal testing"""
    # Pyramidal: Unit + Adapter, or Adapter + Integration, or Unit + Adapter + Integration
    pyramidal_patterns = [
        {'Unit', 'Adapter'},
        {'Adapter', 'Integration'},
        {'Unit', 'Adapter', 'Integration'},
        {'Domain', 'Application'},
        {'Application', 'Adapter'},
    ]

    return any(layers == pattern for pattern in pyramidal_patterns) or \
           any(layers.issuperset(pattern) for pattern in pyramidal_patterns)


def is_problematic_duplication(
    occurrences: List[TestOccurrence],
    type_def: Optional[TypeDefinition]
) -> bool:
    """Determine if duplication is problematic (wrong layer placement)"""

    if not type_def:
        return False  # Can't determine without type info

    # Check if any occurrence is in the wrong layer
    for occ in occurrences:
        if is_wrong_layer(occ, type_def):
            return True

    return False


def is_wrong_layer(occ: TestOccurrence, type_def: TypeDefinition) -> bool:
    """Check if a test occurrence is in the wrong layer"""

    # Core types (Domain/Application) should NOT be in Integration layer
    if type_def.layer == 'Core':
        if occ.layer == 'Integration':
            return True  # Core logic shouldn't be tested in integration

    # Infrastructure types should be in Adapter tests, not Integration (usually)
    if type_def.layer == 'Infrastructure':
        if occ.layer == 'Integration' and 'integration' not in occ.tested_type.lower():
            # Exception: If it's explicitly an integration test, it's OK
            return True

    return False


def generate_recommendations(group: DuplicateTestGroup) -> List[str]:
    """Generate actionable recommendations for a duplicate group"""
    recommendations = []

    if not group.type_definition:
        recommendations.append("⚠️ Cannot find type definition - manual review needed")
        return recommendations

    type_def = group.type_definition

    # Identify wrong layer occurrences
    wrong_layer_occs = [
        occ for occ in group.occurrences
        if is_wrong_layer(occ, type_def)
    ]

    if wrong_layer_occs:
        for occ in wrong_layer_occs:
            recommendations.append(
                f"🔴 MOVE {occ.test_class_name} FROM {occ.layer} ({occ.test_project})"
            )

        # Recommend correct location
        correct_location = recommend_location(type_def)
        recommendations.append(f"   ✅ TO: {correct_location}")
        recommendations.append(
            f"   Reason: {type_def.type_name} is a {type_def.layer} layer type "
            f"({type_def.namespace})"
        )

    # If pyramidal, note it's intentional
    if group.is_pyramidal and not wrong_layer_occs:
        recommendations.append(
            f"✅ KEEP - Pyramidal testing across {', '.join(sorted(group.layers_found))}"
        )

    return recommendations


def recommend_location(type_def: TypeDefinition) -> str:
    """Recommend correct test location based on type definition"""

    if type_def.layer == 'Core':
        if 'Domain' in type_def.namespace or 'Axioms' in type_def.namespace:
            return '00Domain/ExxerAI.Domain.Test or ExxerAI.Axioms.Test'
        elif 'Application' in type_def.namespace:
            return '01Application/ExxerAI.Application.*.Test'
        else:
            return '03UnitTests (Core layer)'

    elif type_def.layer == 'Infrastructure':
        # Extract component name
        component = extract_component(type_def.namespace)
        if component:
            return f'04AdapterTests/ExxerAI.{component}.Adapter.Test'
        return '04AdapterTests/ExxerAI.*.Adapter.Test'

    elif type_def.layer == 'Presentation':
        return '08Presentation or 03UnitTests'

    return 'Unknown - manual review needed'


def extract_component(namespace: str) -> Optional[str]:
    """Extract component name from namespace"""
    components = [
        'Vault', 'Cortex', 'Datastream', 'Nexus', 'Gatekeeper',
        'Sentinel', 'Conduit', 'Chronos', 'Signal', 'Helix', 'Axis'
    ]

    for component in components:
        if component in namespace:
            return component

    return None


def print_analysis(groups: List[DuplicateTestGroup], output_file: Path):
    """Print analysis to console and file"""

    # Sort by problematic first, then by duplicate count
    sorted_groups = sorted(
        groups,
        key=lambda g: (not g.is_problematic, -g.duplicate_count)
    )

    output_lines = []

    def out(line=''):
        output_lines.append(line)
        print(line)

    out("=" * 100)
    out("SMART DUPLICATE TEST ANALYSIS")
    out("=" * 100)
    out()
    out(f"Total duplicate test groups: {len(groups)}")
    out(f"Problematic duplicates: {sum(1 for g in groups if g.is_problematic)}")
    out(f"Pyramidal (intentional): {sum(1 for g in groups if g.is_pyramidal)}")
    out()

    # Problematic duplicates
    problematic = [g for g in sorted_groups if g.is_problematic]
    if problematic:
        out("=" * 100)
        out("🔴 PROBLEMATIC DUPLICATES - Immediate Action Required")
        out("=" * 100)
        out()

        for group in problematic:
            out(f"Test: {group.test_class_name}")
            out(f"Testing: {group.tested_type} ({group.type_definition.kind if group.type_definition else 'unknown'})")

            if group.type_definition:
                out(f"Type defined in: {group.type_definition.namespace} ({group.type_definition.layer} layer)")

            out(f"Found in {group.duplicate_count} locations:")
            for occ in group.occurrences:
                marker = "❌" if group.type_definition and is_wrong_layer(occ, group.type_definition) else "✅"
                out(f"  {marker} {occ.layer} → {occ.test_project}")
                out(f"     {occ.test_file_path}")

            out()
            out("Recommendations:")
            for rec in generate_recommendations(group):
                out(f"  {rec}")
            out()
            out("-" * 100)
            out()

    # Pyramidal (intentional)
    pyramidal = [g for g in sorted_groups if g.is_pyramidal and not g.is_problematic]
    if pyramidal:
        out("=" * 100)
        out("✅ PYRAMIDAL TESTING - Intentional Multi-Layer (OK)")
        out("=" * 100)
        out()

        for group in pyramidal[:10]:  # Show first 10
            out(f"Test: {group.test_class_name}")
            out(f"Testing: {group.tested_type}")
            out(f"Layers: {', '.join(sorted(group.layers_found))}")

            for occ in group.occurrences:
                out(f"  ✅ {occ.layer} → {occ.test_project}")
            out()

        if len(pyramidal) > 10:
            out(f"... and {len(pyramidal) - 10} more pyramidal test patterns")
        out()

    # Other duplicates
    other = [g for g in sorted_groups if not g.is_pyramidal and not g.is_problematic]
    if other:
        out("=" * 100)
        out("🟡 OTHER DUPLICATES - Review Recommended")
        out("=" * 100)
        out()

        for group in other[:10]:
            out(f"Test: {group.test_class_name} ({group.duplicate_count} occurrences)")
            out(f"Testing: {group.tested_type}")
            out(f"Layers: {', '.join(sorted(group.layers_found))}")

            for occ in group.occurrences:
                out(f"  • {occ.layer} → {occ.test_project}")
            out()

        if len(other) > 10:
            out(f"... and {len(other) - 10} more duplicate groups")
        out()

    out("=" * 100)
    out("END OF ANALYSIS")
    out("=" * 100)

    # Write to file
    with open(output_file, 'w', encoding='utf-8') as f:
        f.write('\n'.join(output_lines))

    print(f"\n📄 Report saved to {output_file}")


def generate_json_report(groups: List[DuplicateTestGroup], output_file: Path):
    """Generate detailed JSON report"""

    report = {
        'generated_date': '2025-11-03',
        'analysis_type': 'duplicate_test_grouping',
        'statistics': {
            'total_duplicate_groups': len(groups),
            'problematic_duplicates': sum(1 for g in groups if g.is_problematic),
            'pyramidal_intentional': sum(1 for g in groups if g.is_pyramidal),
        },
        'duplicate_groups': []
    }

    for group in groups:
        group_data = {
            'test_class_name': group.test_class_name,
            'tested_type': group.tested_type,
            'duplicate_count': group.duplicate_count,
            'layers_found': sorted(group.layers_found),
            'is_pyramidal': group.is_pyramidal,
            'is_problematic': group.is_problematic,
            'type_definition': asdict(group.type_definition) if group.type_definition else None,
            'occurrences': [asdict(occ) for occ in group.occurrences],
            'recommendations': generate_recommendations(group)
        }
        report['duplicate_groups'].append(group_data)

    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(report, f, indent=2)

    print(f"📄 JSON report saved to {output_file}")


def main():
    args = parse_args()

    # Load type mapping
    print(f"📚 Loading type mapping from {args.type_mapping}...")
    type_map = load_type_mapping(Path(args.type_mapping))
    print(f"   Loaded {len(type_map)} type definitions")

    # Scan test files
    print(f"\n🔍 Scanning test files in {args.root}...")
    root_path = Path(args.root)
    occurrences = scan_test_files(root_path, args.component)
    print(f"   Found {len(occurrences)} test class occurrences")

    # Group duplicates
    print(f"\n🤖 Grouping duplicate tests and analyzing patterns...")
    duplicate_groups = group_duplicates(occurrences, type_map)

    # Filter by minimum duplicates
    duplicate_groups = [g for g in duplicate_groups if g.duplicate_count >= args.min_duplicates]
    print(f"   Found {len(duplicate_groups)} duplicate test groups")

    # Print analysis
    print(f"\n📊 Generating analysis report...")
    print_analysis(duplicate_groups, Path(args.output))

    # Generate JSON if requested
    if args.output_json:
        print(f"\n📄 Generating JSON report...")
        generate_json_report(duplicate_groups, Path(args.output_json))

    print("\n✅ Analysis complete!")


if __name__ == '__main__':
    main()
