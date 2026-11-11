#!/usr/bin/env python3
"""
Smart Duplicate Type Detection - Enhanced with Helper Exclusions and Test Method Analysis
Focuses on actual business logic duplicates, not infrastructure/helper code.
"""
import argparse
import fnmatch
import re
from collections import defaultdict
from dataclasses import dataclass
from difflib import SequenceMatcher
from pathlib import Path
from typing import Dict, Iterable, List, Sequence, Set, Tuple

SKIP_DIR_NAMES: Set[str] = {
    '.git', '.svn', '.vs', '.idea', '.venv',
    'bin', 'obj', 'TestResults', 'node_modules',
    'packages', 'artifacts', 'artifactsTmp', 'Generated', 'objDebug',
}

DEFAULT_IGNORE_NAMES: Set[str] = {
    'AssemblyInfo', 'GlobalUsings', 'Usings', 'Program', 'NamespaceDoc',
}

# ✅ HELPER CLASS PATTERNS - Intentional infrastructure duplicates
HELPER_CLASS_PATTERNS: Set[str] = {
    # Extension classes
    '*Extensions',
    '*Extension',
    'ServiceCollectionExtensions',
    'ConfigurationExtensions',
    'ApplicationBuilderExtensions',

    # Repository patterns (infrastructure)
    '*Repository',
    'InMemory*Repository',
    'Mock*Repository',

    # Test infrastructure
    '*Helper',
    '*TestHelper',
    '*Fixture',
    '*TestFixture',
    '*ContainerFixture',

    # Base classes and abstracts
    '*Base',
    '*TestBase',
    'TestContext',

    # Configuration and setup
    '*Configuration',
    '*Config',
    '*Setup',
    '*Builder',

    # Exceptions and utilities
    'SkipException',
    '*Utility',
    '*Utilities',
    '*Utils',

    # Migrations and database
    '*Migration',
    '*Snapshot',
    'ApplicationDbContext*',
    'CreateIdentitySchema*',

    # DTOs and models (often duplicated intentionally)
    '*Dto',
    '*Model',
    '*Response',
    '*Request',

    # Placeholder tests
    'PlaceholderTests',
    'PlaceholderTest',
}

# ✅ DEFAULT PROJECT EXCLUSIONS for Pyramidal Testing Strategy
DEFAULT_EXCLUDE_PROJECTS: Set[str] = {
    # Adapter Tests
    'ExxerAI.Vault.Adapter.Test',
    'ExxerAI.Cortex.Adapter.Test',
    'ExxerAI.Datastream.Adapter.Test',
    'ExxerAI.Nexus.Adapter.Test',
    'ExxerAI.Gatekeeper.Adapter.Test',
    'ExxerAI.Sentinel.Adapter.Test',
    'ExxerAI.Conduit.Adapter.Test',
    'ExxerAI.Core.Adapter.Test',
    'ExxerAI.A2A.Adapter.Test',
    'ExxerAI.MCP.Adapter.Test',
    'ExxerAI.CubeXplorer.Adapter.Test',

    # Integration Tests
    'ExxerAI.Components.Integration.Test',
    'ExxerAI.*.IntegrationTests',
}

TOP_LEVEL_LAYER_HINTS: Set[str] = {
    'agents', 'core', 'infraestructure', 'infrastructure',
    'orchestration', 'presentation', 'tests', 'samples', 'config', 'tools',
}

TEST_LAYER_NAMES: Set[str] = {'tests'}


@dataclass
class TypeOccurrence:
    name: str
    path: Path
    relative_path: Path
    layer: str
    project: str
    namespace_hint: str
    is_test_class: bool = False
    test_method_count: int = 0


@dataclass
class TestMethod:
    name: str
    class_name: str
    project: str
    file_path: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description='Smart duplicate detection focusing on business logic, not helpers/infrastructure.',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Smart scan - exclude helpers by default
  python %(prog)s

  # Focus on test methods only (check for duplicate test logic)
  python %(prog)s --test-methods-only

  # Include all helpers (see full duplication)
  python %(prog)s --include-helpers

  # Custom exclusions
  python %(prog)s --exclude-helper "*Manager" --exclude-helper "*Handler"

  # Very strict - only business logic classes
  python %(prog)s --business-logic-only
        """
    )
    parser.add_argument(
        '--root',
        type=Path,
        default=Path('code/src'),
        help='Root directory to scan. Default: code/src',
    )
    parser.add_argument(
        '--threshold',
        type=float,
        default=0.88,
        help='Similarity threshold for code drift detection. Default: 0.88',
    )
    parser.add_argument(
        '--exclude-tests',
        action='store_true',
        help='Skip all test layer projects.',
    )
    parser.add_argument(
        '--ignore-name',
        action='append',
        default=[],
        help='Additional exact type names to ignore (repeatable).',
    )
    parser.add_argument(
        '--exclude-project',
        action='append',
        default=[],
        help='Project patterns to exclude (wildcards supported, repeatable).',
    )
    parser.add_argument(
        '--exclude-helper',
        action='append',
        default=[],
        help='Additional helper class patterns to exclude (wildcards supported, repeatable).',
    )
    parser.add_argument(
        '--include-helpers',
        action='store_true',
        help='Include helper classes (Extensions, Repositories, Fixtures, etc.).',
    )
    parser.add_argument(
        '--test-methods-only',
        action='store_true',
        help='For test projects: only check duplicate test methods (with [Fact]/[Theory] attributes).',
    )
    parser.add_argument(
        '--business-logic-only',
        action='store_true',
        help='Very strict mode: exclude tests, helpers, and infrastructure. Only business logic.',
    )
    parser.add_argument(
        '--no-default-exclusions',
        action='store_true',
        help='Disable default project exclusions for pyramidal testing.',
    )
    parser.add_argument(
        '--list-exclusions',
        action='store_true',
        help='List all default exclusions and exit.',
    )
    parser.add_argument(
        '--max-similar',
        type=int,
        default=60,
        help='Maximum similar-name pairs to display. Default: 60',
    )
    parser.add_argument(
        '--output',
        type=Path,
        default=Path('SmartDuplicatesReport.txt'),
        help='Output report file. Default: SmartDuplicatesReport.txt',
    )
    return parser.parse_args()


def is_helper_class(class_name: str, helper_patterns: Set[str]) -> bool:
    """Check if class matches helper patterns."""
    for pattern in helper_patterns:
        if fnmatch.fnmatch(class_name, pattern):
            return True
    return False


def should_exclude_project(project: str, exclude_patterns: Set[str]) -> bool:
    """Check if project should be excluded."""
    for pattern in exclude_patterns:
        if fnmatch.fnmatch(project, pattern) or project == pattern:
            return True
    return False


def extract_test_methods(file_path: Path) -> List[str]:
    """Extract test method names from a test file (those with [Fact] or [Theory] attributes)."""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()

        # Find methods with [Fact] or [Theory] attributes
        test_methods = []

        # Pattern: [Fact] or [Theory] followed by method signature
        pattern = r'\[(?:Fact|Theory)(?:\([^\)]*\))?\]\s*(?:public|private|protected|internal)?\s*(?:async\s+)?(?:Task|void)\s+(\w+)\s*\('

        for match in re.finditer(pattern, content, re.MULTILINE | re.IGNORECASE):
            method_name = match.group(1)
            test_methods.append(method_name)

        return test_methods

    except Exception as e:
        print(f"Warning: Could not read {file_path}: {e}")
        return []


def is_test_file(file_path: Path) -> bool:
    """Check if file is in a test project."""
    parts = file_path.parts
    return any('test' in part.lower() for part in parts)


def collect_type_occurrences(
    root: Path,
    ignore_names: Set[str],
    include_tests: bool,
    exclude_projects: Set[str],
    helper_patterns: Set[str],
    test_methods_only: bool,
) -> Tuple[List[TypeOccurrence], Dict[str, int]]:
    """
    Collect type occurrences with smart filtering.
    Returns: (occurrences, stats_dict)
    """
    occurrences: List[TypeOccurrence] = []
    stats = {
        'total_scanned': 0,
        'excluded_by_project': 0,
        'excluded_as_helper': 0,
        'excluded_no_test_methods': 0,
    }

    for path in root.rglob('*.cs'):
        if not path.is_file():
            continue
        if any(part in SKIP_DIR_NAMES for part in path.parts):
            continue

        stats['total_scanned'] += 1

        stem = extract_type_name(path.name)
        if not stem or stem in ignore_names:
            continue

        relative_path = path.relative_to(root)
        parts = relative_path.parts
        layer = parts[0] if parts else ''

        if not include_tests and layer.lower() in TEST_LAYER_NAMES:
            continue

        project = determine_project(parts)

        # Check project exclusions
        if should_exclude_project(project, exclude_projects):
            stats['excluded_by_project'] += 1
            continue

        # Check if it's a helper class
        if helper_patterns and is_helper_class(stem, helper_patterns):
            stats['excluded_as_helper'] += 1
            continue

        namespace_hint = build_namespace_hint(parts)
        is_test = is_test_file(path)
        test_method_count = 0

        # If test-methods-only mode and this is a test file, extract test methods
        if test_methods_only and is_test:
            test_methods = extract_test_methods(path)
            test_method_count = len(test_methods)

            # Skip if no test methods found
            if test_method_count == 0:
                stats['excluded_no_test_methods'] += 1
                continue

        occurrence = TypeOccurrence(
            name=stem,
            path=path,
            relative_path=relative_path,
            layer=layer,
            project=project,
            namespace_hint=namespace_hint,
            is_test_class=is_test,
            test_method_count=test_method_count,
        )
        occurrences.append(occurrence)

    return occurrences, stats


def collect_test_method_duplicates(
    root: Path,
    exclude_projects: Set[str],
) -> Tuple[Dict[str, List[TestMethod]], int]:
    """
    Collect duplicate test methods across projects.
    Returns: (method_dict, total_methods_scanned)
    """
    method_occurrences: Dict[str, List[TestMethod]] = defaultdict(list)
    total_scanned = 0

    for path in root.rglob('*.cs'):
        if not path.is_file():
            continue
        if any(part in SKIP_DIR_NAMES for part in path.parts):
            continue
        if not is_test_file(path):
            continue

        relative_path = path.relative_to(root)
        parts = relative_path.parts
        project = determine_project(parts)

        if should_exclude_project(project, exclude_projects):
            continue

        class_name = extract_type_name(path.name)
        test_methods = extract_test_methods(path)
        total_scanned += len(test_methods)

        for method_name in test_methods:
            method_occurrences[method_name].append(TestMethod(
                name=method_name,
                class_name=class_name,
                project=project,
                file_path=str(relative_path),
            ))

    return method_occurrences, total_scanned


def extract_type_name(filename: str) -> str:
    base = filename.split('.', 1)[0]
    return base.strip()


def determine_project(parts: Sequence[str]) -> str:
    if not parts:
        return ''
    if parts[0].lower() in TEST_LAYER_NAMES:
        return parts[1] if len(parts) > 1 else parts[0]
    if len(parts) > 1 and parts[1] and parts[1][0].isupper():
        return parts[1]
    if len(parts) > 2 and parts[2] and parts[2][0].isupper():
        return parts[2]
    return parts[0]


def build_namespace_hint(parts: Sequence[str]) -> str:
    if not parts:
        return ''
    dir_parts = list(parts[:-1])
    if dir_parts and dir_parts[0].lower() in TOP_LEVEL_LAYER_HINTS:
        dir_parts = dir_parts[1:]
    return '.'.join(dir_parts)


def group_by_name(occurrences: Iterable[TypeOccurrence]) -> Dict[str, List[TypeOccurrence]]:
    grouped: Dict[str, List[TypeOccurrence]] = defaultdict(list)
    for occurrence in occurrences:
        grouped[occurrence.name].append(occurrence)
    return grouped


def find_exact_duplicates(grouped: Dict[str, List[TypeOccurrence]]) -> List[Tuple[str, List[TypeOccurrence]]]:
    duplicates: List[Tuple[str, List[TypeOccurrence]]] = []
    for name, items in grouped.items():
        project_set = {item.project for item in items}
        namespace_set = {item.namespace_hint for item in items}
        if len(items) > 1 and (len(project_set) > 1 or len(namespace_set) > 1):
            duplicates.append((name, items))
    duplicates.sort(key=lambda entry: (-len(entry[1]), entry[0].lower()))
    return duplicates


def find_similar_names(
    grouped: Dict[str, List[TypeOccurrence]],
    threshold: float,
) -> List[Tuple[float, str, str, List[TypeOccurrence], List[TypeOccurrence]]]:
    by_first_letter: Dict[str, List[str]] = defaultdict(list)
    for name in grouped:
        if not name:
            continue
        by_first_letter[name[0].lower()].append(name)

    results: List[Tuple[float, str, str, List[TypeOccurrence], List[TypeOccurrence]]] = []
    for names in by_first_letter.values():
        names.sort(key=str.lower)
        for idx, left in enumerate(names):
            left_occurrences = grouped[left]
            for right in names[idx + 1:]:
                right_occurrences = grouped[right]
                if not should_compare(left, right, left_occurrences, right_occurrences):
                    continue
                ratio = SequenceMatcher(None, left, right).ratio()
                if threshold <= ratio < 1.0:
                    combined_projects = {occ.project for occ in left_occurrences + right_occurrences}
                    if len(combined_projects) < 2:
                        continue
                    results.append((ratio, left, right, left_occurrences, right_occurrences))
    results.sort(key=lambda item: (-item[0], item[1].lower(), item[2].lower()))
    return results


def should_compare(
    left: str,
    right: str,
    left_occurrences: Sequence[TypeOccurrence],
    right_occurrences: Sequence[TypeOccurrence],
) -> bool:
    length_gap = abs(len(left) - len(right))
    if length_gap > max(4, int(min(len(left), len(right)) * 0.6)):
        return False
    if left.endswith('Tests') and right.endswith('Tests'):
        return False
    same_projects = {occ.project for occ in left_occurrences}.intersection(
        {occ.project for occ in right_occurrences}
    )
    if same_projects and len(same_projects) == len({occ.project for occ in left_occurrences + right_occurrences}):
        return False
    return True


def build_report(
    duplicates: List[Tuple[str, List[TypeOccurrence]]],
    similar_names: List[Tuple[float, str, str, List[TypeOccurrence], List[TypeOccurrence]]],
    test_method_duplicates: Dict[str, List[TestMethod]],
    stats: Dict[str, int],
    exclude_projects: Set[str],
    helper_patterns: Set[str],
    test_methods_only: bool,
    max_similar: int,
) -> str:
    lines: List[str] = []
    lines.append('=' * 80)
    lines.append('SMART DUPLICATE DETECTION REPORT - Business Logic Focus')
    lines.append('=' * 80)
    lines.append('')

    lines.append('--- Scan Statistics ---')
    lines.append(f"Total files scanned: {stats.get('total_scanned', 0)}")
    lines.append(f"Files analyzed: {stats.get('total_scanned', 0) - stats.get('excluded_by_project', 0) - stats.get('excluded_as_helper', 0)}")
    lines.append(f"Excluded by project filters: {stats.get('excluded_by_project', 0)}")
    lines.append(f"Excluded as helpers: {stats.get('excluded_as_helper', 0)}")
    if test_methods_only:
        lines.append(f"Excluded (no test methods): {stats.get('excluded_no_test_methods', 0)}")
    lines.append('')

    if helper_patterns:
        lines.append(f'--- Helper Class Exclusions ({len(helper_patterns)} patterns) ---')
        for pattern in sorted(list(helper_patterns)[:10]):
            lines.append(f'  ✗ {pattern}')
        if len(helper_patterns) > 10:
            lines.append(f'  ... and {len(helper_patterns) - 10} more')
        lines.append('')

    if test_methods_only and test_method_duplicates:
        lines.append('=' * 80)
        lines.append('DUPLICATE TEST METHODS (Exact Name Matches)')
        lines.append('=' * 80)

        # Filter to only methods that appear in multiple projects
        multi_project_methods = {
            name: methods for name, methods in test_method_duplicates.items()
            if len({m.project for m in methods}) > 1 and len(methods) > 1
        }

        lines.append(f'Total duplicate test method names: {len(multi_project_methods)}')
        lines.append('')

        if multi_project_methods:
            sorted_methods = sorted(multi_project_methods.items(),
                                  key=lambda x: (-len(x[1]), x[0]))

            for method_name, methods in sorted_methods[:50]:
                projects = {m.project for m in methods}
                lines.append(f'- {method_name} ({len(methods)} occurrences across {len(projects)} projects)')
                for method in methods[:10]:
                    lines.append(f'    - {method.project} → {method.class_name} ({method.file_path})')
                if len(methods) > 10:
                    lines.append(f'    ... and {len(methods) - 10} more occurrences')

            if len(multi_project_methods) > 50:
                lines.append(f'... and {len(multi_project_methods) - 50} more duplicate test methods')
        else:
            lines.append('✅ No duplicate test method names found across projects!')

        lines.append('')

    lines.append('=' * 80)
    lines.append('CLASS-LEVEL DUPLICATES')
    lines.append('=' * 80)
    lines.append(f'Exact class duplicates: {len(duplicates)}')
    lines.append(f'Similar class names (ratio >= threshold): {len(similar_names)}')
    lines.append('')

    if duplicates:
        lines.append('=== Exact Class Duplicates ===')
        for name, occurrences in duplicates[:50]:
            lines.append(f'- {name} ({len(occurrences)} occurrences)')
            for occ in occurrences:
                test_info = f' [Tests: {occ.test_method_count}]' if occ.test_method_count > 0 else ''
                lines.append(
                    f'    - layer={occ.layer or "?"} | project={occ.project}{test_info} | {occ.relative_path}'
                )
        if len(duplicates) > 50:
            lines.append(f'... and {len(duplicates) - 50} more class duplicates')
        lines.append('')
    else:
        lines.append('✅ No exact class duplicates found!')
        lines.append('')

    if similar_names:
        lines.append('=== Similar Names (Code Drift Candidates) ===')
        for ratio, left, right, left_occ, right_occ in similar_names[:max_similar]:
            lines.append(f'- {left} <-> {right} (similarity={ratio:.2f})')
            for occ in left_occ[:3]:
                lines.append(f'    - {left} → {occ.project} ({occ.relative_path})')
            for occ in right_occ[:3]:
                lines.append(f'    - {right} → {occ.project} ({occ.relative_path})')
        if len(similar_names) > max_similar:
            lines.append(f'... and {len(similar_names) - max_similar} more similar pairs')
    else:
        lines.append('✅ No similar class names found!')

    lines.append('')
    lines.append('=' * 80)

    return '\n'.join(lines)


def write_report(report: str, destination: Path) -> Path:
    output_path = destination if destination.is_absolute() else Path.cwd() / destination
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(report + '\n', encoding='utf-8')
    return output_path


def list_default_exclusions(helper_patterns: Set[str]) -> None:
    """List all default exclusions."""
    print('=' * 80)
    print('DEFAULT EXCLUSIONS - Smart Duplicate Detection')
    print('=' * 80)
    print('')
    print('PROJECT EXCLUSIONS (Pyramidal Testing):')
    for pattern in sorted(DEFAULT_EXCLUDE_PROJECTS):
        print(f'  ✗ {pattern}')
    print('')
    print(f'HELPER CLASS PATTERNS ({len(helper_patterns)} patterns):')
    for pattern in sorted(helper_patterns):
        print(f'  ✗ {pattern}')
    print('=' * 80)


def main() -> None:
    args = parse_args()

    # Build helper patterns
    helper_patterns: Set[str] = set()
    if not args.include_helpers:
        helper_patterns.update(HELPER_CLASS_PATTERNS)
    helper_patterns.update(pattern.strip() for pattern in args.exclude_helper if pattern)

    # Handle --list-exclusions
    if args.list_exclusions:
        list_default_exclusions(helper_patterns)
        return

    # Business logic only mode
    if args.business_logic_only:
        args.exclude_tests = True
        if not args.include_helpers:
            helper_patterns.update(HELPER_CLASS_PATTERNS)

    root = args.root.resolve()
    if not root.exists():
        raise SystemExit(f'Root path {root} does not exist.')

    ignore_names = {name.strip() for name in DEFAULT_IGNORE_NAMES}
    ignore_names.update(name.strip() for name in args.ignore_name if name)

    # Build exclude projects
    exclude_projects: Set[str] = set()
    if not args.no_default_exclusions:
        exclude_projects.update(DEFAULT_EXCLUDE_PROJECTS)
    exclude_projects.update(pattern.strip() for pattern in args.exclude_project if pattern)

    print('=' * 80)
    print('SMART DUPLICATE DETECTION - Business Logic Focus')
    print('=' * 80)
    print(f'Root: {root}')
    print(f'Mode: {"Test Methods Only" if args.test_methods_only else "Class Duplicates"}')
    print(f'Helper exclusions: {len(helper_patterns)} patterns')
    print(f'Project exclusions: {len(exclude_projects)} patterns')
    print('=' * 80)
    print('')

    # Collect test method duplicates if requested
    test_method_duplicates = {}
    if args.test_methods_only:
        print('🔍 Scanning for duplicate test methods...')
        test_method_duplicates, total_methods = collect_test_method_duplicates(
            root=root,
            exclude_projects=exclude_projects,
        )
        print(f'   Found {total_methods} test methods')
        print('')

    # Collect type occurrences
    print('🔍 Scanning for duplicate classes...')
    occurrences, stats = collect_type_occurrences(
        root=root,
        ignore_names=ignore_names,
        include_tests=not args.exclude_tests,
        exclude_projects=exclude_projects,
        helper_patterns=helper_patterns,
        test_methods_only=args.test_methods_only,
    )
    print(f'   Analyzed {len(occurrences)} classes')
    print('')

    if not occurrences and not test_method_duplicates:
        print('⚠️  No types found after applying filters.')
        return

    grouped = group_by_name(occurrences)
    duplicates = find_exact_duplicates(grouped)
    similar_names = find_similar_names(grouped, args.threshold)

    report_text = build_report(
        duplicates=duplicates,
        similar_names=similar_names,
        test_method_duplicates=test_method_duplicates,
        stats=stats,
        exclude_projects=exclude_projects,
        helper_patterns=helper_patterns,
        test_methods_only=args.test_methods_only,
        max_similar=max(0, args.max_similar),
    )

    print(report_text)
    output_path = write_report(report_text, args.output)
    print(f'\n📄 Report saved to {output_path}')
    print('\n✅ Smart analysis complete!')


if __name__ == '__main__':
    main()
