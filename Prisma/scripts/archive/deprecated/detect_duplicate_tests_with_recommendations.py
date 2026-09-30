#!/usr/bin/env python3
"""
Smart Test Placement Analyzer with Recommendations
Analyzes duplicate tests and recommends correct locations based on type definitions.

Principle: Test objects in their creation layer
"""
import argparse
import fnmatch
import json
import re
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Set, Tuple

# Test naming patterns to extract what's being tested
TEST_NAMING_PATTERNS = [
    r'^(.+)Tests?$',                    # GoogleDriveServiceTests → GoogleDriveService
    r'^(.+)Test$',                       # OpenAIProviderTest → OpenAIProvider
    r'^I(.+)Tests?$',                    # IDocumentProcessorTests → IDocumentProcessor (interfaces)
    r'^Mock(.+)Tests?$',                 # MockServiceTests → MockService
    r'^(.+)IntegrationTests?$',          # HybridKnowledgeIntegrationTests → HybridKnowledge
    r'^(.+)AdapterTests?$',              # SemanticSearchAdapterTests → SemanticSearch
    r'^(.+)BehavioralTests?$',           # GoogleDriveServiceBehavioralTests → GoogleDriveService
]

# Layer to test project mapping
LAYER_TO_TEST_PROJECT = {
    'Core': {
        'Domain': '00Domain/ExxerAI.Domain.Test',
        'Application': '01Application/ExxerAI.Application.*.Test',
        'Axioms': '00Domain/ExxerAI.Axioms.Test',
    },
    'Infrastructure': {
        # Infrastructure components tested at adapter level
        'default': '04AdapterTests/ExxerAI.{component}.Adapter.Test',
    },
    'Presentation': {
        'UI': '08Presentation/ExxerAI.UI.Test',
        'Api': '03UnitTests/ExxerAI.Api.Test',
    },
}

DEFAULT_EXCLUDE_PROJECTS: Set[str] = {
    'ExxerAI.Vault.Adapter.Test',
    'ExxerAI.Cortex.Adapter.Test',
    'ExxerAI.Datastream.Adapter.Test',
    'ExxerAI.Nexus.Adapter.Test',
    'ExxerAI.Gatekeeper.Adapter.Test',
    'ExxerAI.Sentinel.Adapter.Test',
    'ExxerAI.Conduit.Adapter.Test',
    'ExxerAI.Core.Adapter.Test',
}


@dataclass
class TestOccurrence:
    test_class_name: str
    test_file_path: str
    test_project: str
    layer: str
    namespace: str
    tested_type: Optional[str] = None
    test_method_count: int = 0


@dataclass
class TypeDefinition:
    type_name: str
    namespace: str
    kind: str  # class, interface, record, enum
    project: str
    layer: str


@dataclass
class TestRecommendation:
    test_class: str
    current_location: str
    tested_type: str
    type_definition: TypeDefinition
    recommended_location: str
    reason: str
    priority: str  # high, medium, low
    action: str  # move, keep, consolidate


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description='Smart test placement analyzer with actionable recommendations',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Analyze with recommendations
  python %(prog)s --type-mapping scripts/exxerai_types.json

  # Generate migration plan
  python %(prog)s --type-mapping scripts/exxerai_types.json --generate-migration-plan

  # Focus on specific component
  python %(prog)s --type-mapping scripts/exxerai_types.json --component Gatekeeper
        """
    )
    parser.add_argument(
        '--type-mapping',
        type=Path,
        default=Path('scripts/exxerai_types.json'),
        help='Path to type mapping JSON file. Default: scripts/exxerai_types.json',
    )
    parser.add_argument(
        '--root',
        type=Path,
        default=Path('code/src/tests'),
        help='Root test directory. Default: code/src/tests',
    )
    parser.add_argument(
        '--component',
        type=str,
        help='Focus on specific component (e.g., Gatekeeper, Cortex, Vault)',
    )
    parser.add_argument(
        '--generate-migration-plan',
        action='store_true',
        help='Generate actionable migration plan JSON',
    )
    parser.add_argument(
        '--output',
        type=Path,
        default=Path('TestPlacementRecommendations.txt'),
        help='Output report file. Default: TestPlacementRecommendations.txt',
    )
    parser.add_argument(
        '--migration-output',
        type=Path,
        default=Path('TestMigrationPlan.json'),
        help='Migration plan JSON output. Default: TestMigrationPlan.json',
    )
    return parser.parse_args()


def load_type_mapping(type_mapping_file: Path) -> Dict[str, TypeDefinition]:
    """Load type mapping from JSON file."""
    try:
        with open(type_mapping_file, 'r', encoding='utf-8') as f:
            data = json.load(f)

        type_defs = {}
        for type_name, info in data.get('type_lookup', {}).items():
            namespace = info.get('namespace', '')

            # Determine layer from namespace
            layer = 'Unknown'
            if 'Domain' in namespace or 'Axioms' in namespace:
                layer = 'Core'
            elif any(comp in namespace for comp in ['Axis', 'Datastream', 'Cortex', 'Gatekeeper',
                                                      'Vault', 'Sentinel', 'Conduit', 'Nexus',
                                                      'Chronos', 'Signal', 'Helix']):
                layer = 'Infrastructure'
            elif 'UI' in namespace or 'Api' in namespace or 'Presentation' in namespace:
                layer = 'Presentation'
            elif 'Application' in namespace:
                layer = 'Core'

            type_defs[type_name] = TypeDefinition(
                type_name=type_name,
                namespace=namespace,
                kind=info.get('kind', 'class'),
                project=info.get('project', 'Unknown'),
                layer=layer,
            )

        return type_defs

    except Exception as e:
        print(f"Warning: Could not load type mapping from {type_mapping_file}: {e}")
        return {}


def extract_tested_type(test_class_name: str) -> Optional[str]:
    """Extract what type is being tested from test class name."""
    for pattern in TEST_NAMING_PATTERNS:
        match = re.match(pattern, test_class_name)
        if match:
            return match.group(1)
    return None


def determine_layer_from_path(file_path: Path) -> str:
    """Determine test layer from file path."""
    parts = file_path.parts
    if 'tests' in [p.lower() for p in parts]:
        test_idx = next(i for i, p in enumerate(parts) if p.lower() == 'tests')
        if test_idx + 1 < len(parts):
            layer_folder = parts[test_idx + 1]
            if 'Domain' in layer_folder or '00Domain' in layer_folder:
                return 'Core'
            elif 'Application' in layer_folder or '01Application' in layer_folder:
                return 'Core'
            elif 'UnitTests' in layer_folder or '03UnitTests' in layer_folder:
                return 'Unit'
            elif 'AdapterTests' in layer_folder or '04AdapterTests' in layer_folder:
                return 'Adapter'
            elif 'Integration' in layer_folder or '05IntegrationTests' in layer_folder:
                return 'Integration'
            elif 'SystemTests' in layer_folder or '06SystemTests' in layer_folder:
                return 'System'
    return 'Unknown'


def extract_component_from_namespace(namespace: str) -> Optional[str]:
    """Extract component name from namespace (e.g., ExxerAI.Gatekeeper.* → Gatekeeper)."""
    components = ['Axis', 'Datastream', 'Cortex', 'Gatekeeper', 'Vault', 'Sentinel',
                  'Conduit', 'Nexus', 'Chronos', 'Signal', 'Helix', 'Nebula', 'Wisdom']

    for component in components:
        if component in namespace:
            return component
    return None


def recommend_test_location(
    test_class: str,
    tested_type: str,
    type_def: TypeDefinition,
    current_layer: str,
) -> TestRecommendation:
    """Generate recommendation for where test should live."""

    # Extract component
    component = extract_component_from_namespace(type_def.namespace)

    # Determine recommended location based on type layer
    if type_def.layer == 'Core':
        if 'Domain' in type_def.namespace or 'Axioms' in type_def.namespace:
            recommended = f'00Domain/ExxerAI.Domain.Test or ExxerAI.Axioms.Test'
            reason = f'{tested_type} is a Domain/Axioms type - tests belong in Domain test project'
        elif 'Application' in type_def.namespace:
            recommended = f'01Application/ExxerAI.Application.*.Test'
            reason = f'{tested_type} is an Application type - tests belong in Application test project'
        else:
            recommended = '00Domain or 01Application test projects'
            reason = f'{tested_type} is a Core type'

    elif type_def.layer == 'Infrastructure' and component:
        # Infrastructure components should be tested at adapter level
        recommended = f'04AdapterTests/ExxerAI.{component}.Adapter.Test'
        reason = f'{tested_type} is an Infrastructure/{component} component - adapter tests recommended'

    elif type_def.layer == 'Presentation':
        if 'UI' in type_def.namespace:
            recommended = '08Presentation/ExxerAI.UI.Test'
        elif 'Api' in type_def.namespace:
            recommended = '03UnitTests/ExxerAI.Api.Test'
        else:
            recommended = '08Presentation test projects'
        reason = f'{tested_type} is a Presentation component'

    else:
        recommended = 'Unknown - manual review needed'
        reason = f'Cannot determine appropriate location for {tested_type}'

    # Determine priority and action
    if current_layer == 'Integration' and type_def.layer in ['Core', 'Infrastructure']:
        priority = 'HIGH'
        action = 'MOVE'
        reason += ' | Currently in wrong layer (Integration vs Core/Infrastructure)'
    elif current_layer == 'Adapter' and recommended.startswith('04AdapterTests'):
        priority = 'LOW'
        action = 'KEEP'
        reason += ' | Already in recommended location'
    elif current_layer in ['Domain', 'Application'] and type_def.layer == 'Core':
        priority = 'LOW'
        action = 'KEEP'
        reason += ' | Already in Core layer'
    else:
        priority = 'MEDIUM'
        action = 'REVIEW'

    return TestRecommendation(
        test_class=test_class,
        current_location=current_layer,
        tested_type=tested_type,
        type_definition=type_def,
        recommended_location=recommended,
        reason=reason,
        priority=priority,
        action=action,
    )


def collect_test_occurrences(root: Path, component_filter: Optional[str] = None) -> List[TestOccurrence]:
    """Collect all test class occurrences."""
    occurrences = []

    for test_file in root.rglob('*Tests.cs'):
        if not test_file.is_file():
            continue

        relative_path = test_file.relative_to(root.parent.parent)
        parts = relative_path.parts

        # Determine project
        project = 'Unknown'
        if len(parts) > 2:
            project = parts[2] if parts[0] == 'tests' else parts[1]

        # Skip if filtering by component
        if component_filter and component_filter not in str(test_file):
            continue

        test_class_name = test_file.stem
        layer = determine_layer_from_path(test_file)

        # Try to read namespace
        namespace = 'Unknown'
        try:
            with open(test_file, 'r', encoding='utf-8') as f:
                content = f.read(2000)  # Just first part
                ns_match = re.search(r'namespace\s+([\w\.]+)', content)
                if ns_match:
                    namespace = ns_match.group(1)
        except:
            pass

        # Extract tested type
        tested_type = extract_tested_type(test_class_name)

        occurrences.append(TestOccurrence(
            test_class_name=test_class_name,
            test_file_path=str(relative_path),
            test_project=project,
            layer=layer,
            namespace=namespace,
            tested_type=tested_type,
        ))

    return occurrences


def analyze_test_placement(
    occurrences: List[TestOccurrence],
    type_mapping: Dict[str, TypeDefinition],
) -> List[TestRecommendation]:
    """Analyze test placement and generate recommendations."""

    recommendations = []

    # Group by tested type
    by_tested_type = defaultdict(list)
    for occ in occurrences:
        if occ.tested_type:
            by_tested_type[occ.tested_type].append(occ)

    # Analyze each group
    for tested_type, tests in by_tested_type.items():
        # Skip if only one test and no type definition
        if len(tests) == 1 and tested_type not in type_mapping:
            continue

        # Look up type definition
        type_def = type_mapping.get(tested_type)

        if not type_def:
            # Try with 'I' prefix for interfaces
            type_def = type_mapping.get(f'I{tested_type}')

        if not type_def:
            # Try without 'Service' suffix
            if tested_type.endswith('Service'):
                type_def = type_mapping.get(tested_type[:-7])

        if type_def:
            # Generate recommendations for each test
            for test in tests:
                rec = recommend_test_location(
                    test_class=test.test_class_name,
                    tested_type=tested_type,
                    type_def=type_def,
                    current_layer=test.layer,
                )
                recommendations.append(rec)

    return recommendations


def build_report(
    recommendations: List[TestRecommendation],
    type_mapping_stats: Dict,
) -> str:
    """Build comprehensive report with recommendations."""
    lines = []

    lines.append('=' * 100)
    lines.append('SMART TEST PLACEMENT ANALYSIS - Recommendations Report')
    lines.append('=' * 100)
    lines.append('')
    lines.append('Principle: Test objects in their creation layer')
    lines.append('')

    lines.append('--- Type Mapping Statistics ---')
    lines.append(f"Total types in mapping: {type_mapping_stats.get('total_types', 0)}")
    lines.append(f"Total namespaces: {type_mapping_stats.get('total_namespaces', 0)}")
    lines.append(f"Total projects: {type_mapping_stats.get('total_projects', 0)}")
    lines.append('')

    lines.append('--- Analysis Results ---')
    lines.append(f"Total test classes analyzed: {len(recommendations)}")

    # Count by priority
    by_priority = defaultdict(int)
    by_action = defaultdict(int)
    for rec in recommendations:
        by_priority[rec.priority] += 1
        by_action[rec.action] += 1

    lines.append(f"High priority moves: {by_priority.get('HIGH', 0)}")
    lines.append(f"Medium priority reviews: {by_priority.get('MEDIUM', 0)}")
    lines.append(f"Low priority (keep): {by_priority.get('LOW', 0)}")
    lines.append('')

    lines.append(f"Actions: MOVE={by_action.get('MOVE', 0)}, REVIEW={by_action.get('REVIEW', 0)}, KEEP={by_action.get('KEEP', 0)}")
    lines.append('')

    # High priority recommendations
    high_priority = [r for r in recommendations if r.priority == 'HIGH']
    if high_priority:
        lines.append('=' * 100)
        lines.append('HIGH PRIORITY RECOMMENDATIONS - Immediate Action Required')
        lines.append('=' * 100)
        for rec in high_priority[:30]:
            lines.append(f'\n🔴 {rec.test_class}')
            lines.append(f'   Testing: {rec.tested_type} ({rec.type_definition.kind})')
            lines.append(f'   Type defined in: {rec.type_definition.namespace} ({rec.type_definition.layer} layer)')
            lines.append(f'   Current location: {rec.current_location}')
            lines.append(f'   ✅ Recommended: {rec.recommended_location}')
            lines.append(f'   Reason: {rec.reason}')
            lines.append(f'   Action: {rec.action}')

        if len(high_priority) > 30:
            lines.append(f'\n... and {len(high_priority) - 30} more high priority items')

    # Medium priority
    medium_priority = [r for r in recommendations if r.priority == 'MEDIUM']
    if medium_priority:
        lines.append('')
        lines.append('=' * 100)
        lines.append('MEDIUM PRIORITY RECOMMENDATIONS - Review and Consider')
        lines.append('=' * 100)
        for rec in medium_priority[:20]:
            lines.append(f'\n🟡 {rec.test_class}')
            lines.append(f'   Testing: {rec.tested_type} ({rec.type_definition.kind})')
            lines.append(f'   Current: {rec.current_location} → Recommended: {rec.recommended_location}')
            lines.append(f'   Reason: {rec.reason}')

        if len(medium_priority) > 20:
            lines.append(f'\n... and {len(medium_priority) - 20} more medium priority items')

    # Keep (already correct)
    keep_items = [r for r in recommendations if r.action == 'KEEP']
    if keep_items:
        lines.append('')
        lines.append('=' * 100)
        lines.append(f'✅ CORRECTLY PLACED TESTS - {len(keep_items)} tests already in right location')
        lines.append('=' * 100)

        # Group by component
        by_component = defaultdict(list)
        for rec in keep_items:
            component = extract_component_from_namespace(rec.type_definition.namespace) or 'Core'
            by_component[component].append(rec)

        for component, recs in sorted(by_component.items()):
            lines.append(f'\n{component}: {len(recs)} tests correctly placed')
            for rec in recs[:5]:
                lines.append(f'  ✅ {rec.test_class} → {rec.current_location}')
            if len(recs) > 5:
                lines.append(f'  ... and {len(recs) - 5} more')

    lines.append('')
    lines.append('=' * 100)
    lines.append('END OF REPORT')
    lines.append('=' * 100)

    return '\n'.join(lines)


def generate_migration_plan(recommendations: List[TestRecommendation], output_file: Path):
    """Generate actionable migration plan JSON."""

    migration_plan = {
        'generated_date': '2025-11-03',
        'principle': 'Test objects in their creation layer',
        'statistics': {
            'total_recommendations': len(recommendations),
            'high_priority': len([r for r in recommendations if r.priority == 'HIGH']),
            'medium_priority': len([r for r in recommendations if r.priority == 'MEDIUM']),
            'low_priority': len([r for r in recommendations if r.priority == 'LOW']),
            'to_move': len([r for r in recommendations if r.action == 'MOVE']),
            'to_review': len([r for r in recommendations if r.action == 'REVIEW']),
            'keep': len([r for r in recommendations if r.action == 'KEEP']),
        },
        'migrations': []
    }

    for rec in recommendations:
        if rec.action in ['MOVE', 'REVIEW']:
            migration_plan['migrations'].append({
                'test_class': rec.test_class,
                'tested_type': rec.tested_type,
                'type_namespace': rec.type_definition.namespace,
                'type_kind': rec.type_definition.kind,
                'type_layer': rec.type_definition.layer,
                'current_location': rec.current_location,
                'recommended_location': rec.recommended_location,
                'priority': rec.priority,
                'action': rec.action,
                'reason': rec.reason,
            })

    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(migration_plan, f, indent=2)

    return migration_plan


def main():
    args = parse_args()

    print('=' * 100)
    print('SMART TEST PLACEMENT ANALYZER')
    print('=' * 100)
    print(f'Type mapping: {args.type_mapping}')
    print(f'Test root: {args.root}')
    if args.component:
        print(f'Component filter: {args.component}')
    print('=' * 100)
    print('')

    # Load type mapping
    print('📚 Loading type mapping...')
    type_mapping = load_type_mapping(args.type_mapping)
    print(f'   Loaded {len(type_mapping)} type definitions')
    print('')

    # Load type mapping stats
    try:
        with open(args.type_mapping, 'r', encoding='utf-8') as f:
            data = json.load(f)
            type_mapping_stats = data.get('statistics', {})
    except:
        type_mapping_stats = {}

    # Collect test occurrences
    print('🔍 Scanning test files...')
    occurrences = collect_test_occurrences(args.root, args.component)
    print(f'   Found {len(occurrences)} test classes')
    print('')

    # Analyze and generate recommendations
    print('🤖 Analyzing test placement...')
    recommendations = analyze_test_placement(occurrences, type_mapping)
    print(f'   Generated {len(recommendations)} recommendations')
    print('')

    # Build report
    report = build_report(recommendations, type_mapping_stats)
    print(report)

    # Write report
    with open(args.output, 'w', encoding='utf-8') as f:
        f.write(report)
    print(f'\n📄 Report saved to {args.output}')

    # Generate migration plan if requested
    if args.generate_migration_plan:
        print('\n🚀 Generating migration plan...')
        plan = generate_migration_plan(recommendations, args.migration_output)
        print(f'   Migration plan saved to {args.migration_output}')
        print(f'   To move: {plan["statistics"]["to_move"]} tests')
        print(f'   To review: {plan["statistics"]["to_review"]} tests')

    print('\n✅ Analysis complete!')


if __name__ == '__main__':
    main()
