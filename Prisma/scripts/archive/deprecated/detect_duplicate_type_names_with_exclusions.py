#!/usr/bin/env python3
"""
Detect duplicated or suspiciously similar type names across the ExxerAI solution.
Enhanced version with project exclusion capability for pyramidal testing strategy.
"""
import argparse
import fnmatch
from collections import defaultdict
from dataclasses import dataclass
from difflib import SequenceMatcher
from pathlib import Path
from typing import Dict, Iterable, List, Sequence, Set, Tuple

SKIP_DIR_NAMES: Set[str] = {
    '.git',
    '.svn',
    '.vs',
    '.idea',
    '.venv',
    'bin',
    'obj',
    'TestResults',
    'node_modules',
    'packages',
    'artifacts',
    'artifactsTmp',
    'Generated',
    'objDebug',
}

DEFAULT_IGNORE_NAMES: Set[str] = {
    'AssemblyInfo',
    'GlobalUsings',
    'Usings',
    'Program',
    'NamespaceDoc',
}

# ✅ DEFAULT PROJECT EXCLUSIONS for Pyramidal Testing Strategy
# These are intentional Infrastructure duplicates for different test abstraction levels
DEFAULT_EXCLUDE_PROJECTS: Set[str] = {
    # 04AdapterTests - Adapter layer with in-memory/mock implementations
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

    # 05IntegrationTests - Integration layer with real Docker containers
    'ExxerAI.Components.Integration.Test',
    'ExxerAI.*.IntegrationTests',  # Pattern matching support

    # Test infrastructure fixtures and helpers (intentional duplicates)
    '*ContainerFixture*',
    '*TestFixture*',
    '*TestHelper*',
    '*InMemory*Repository*',
}

TOP_LEVEL_LAYER_HINTS: Set[str] = {
    'agents',
    'core',
    'infraestructure',
    'infrastructure',
    'orchestration',
    'presentation',
    'tests',
    'samples',
    'config',
    'tools',
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


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            'Scan the repository for duplicated or similar class/interface names '
            'across projects and layers. Enhanced with project exclusion for pyramidal testing.'
        ),
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Basic usage with default exclusions
  python %(prog)s

  # Exclude specific project
  python %(prog)s --exclude-project "ExxerAI.Vault.Adapter.Test"

  # Exclude using patterns (wildcards)
  python %(prog)s --exclude-project "ExxerAI.*.Adapter.Test"

  # Multiple exclusions
  python %(prog)s --exclude-project "ExxerAI.Vault.*" --exclude-project "ExxerAI.Cortex.*"

  # Disable default exclusions
  python %(prog)s --no-default-exclusions

  # Custom threshold and output
  python %(prog)s --threshold 0.90 --output custom_report.txt
        """
    )
    parser.add_argument(
        '--root',
        type=Path,
        default=Path('code/src'),
        help='Root directory to scan. Defaults to code/src relative to the repo root.',
    )
    parser.add_argument(
        '--threshold',
        type=float,
        default=0.88,
        help='Similarity ratio threshold (0-1) for reporting look-alike names. Default: 0.88.',
    )
    parser.add_argument(
        '--exclude-tests',
        action='store_true',
        help='Skip anything under the tests layer.',
    )
    parser.add_argument(
        '--ignore-name',
        action='append',
        default=[],
        help='Additional exact type names to ignore (may be repeated).',
    )
    parser.add_argument(
        '--exclude-project',
        action='append',
        default=[],
        help=(
            'Project names or patterns to exclude from duplicate detection. '
            'Supports wildcards (* and ?). Can be repeated. '
            'Example: --exclude-project "ExxerAI.*.Adapter.Test"'
        ),
    )
    parser.add_argument(
        '--no-default-exclusions',
        action='store_true',
        help=(
            'Disable default project exclusions for pyramidal testing strategy. '
            'By default, Infrastructure test projects are excluded as they are '
            'intentional duplicates for different test abstraction levels.'
        ),
    )
    parser.add_argument(
        '--list-exclusions',
        action='store_true',
        help='List all default project exclusions and exit.',
    )
    parser.add_argument(
        '--max-similar',
        type=int,
        default=60,
        help='Maximum number of similar-name pairs to display. Default: 60.',
    )
    parser.add_argument(
        '--output',
        type=Path,
        default=Path('DuplicatedTypesReport.txt'),
        help='Where to write the duplication report. Defaults to DuplicatedTypesReport.txt in the current working directory.',
    )
    return parser.parse_args()


def should_exclude_project(project: str, exclude_patterns: Set[str]) -> bool:
    """Check if project should be excluded based on patterns."""
    if not exclude_patterns:
        return False

    for pattern in exclude_patterns:
        # Support both exact match and wildcard patterns
        if fnmatch.fnmatch(project, pattern) or project == pattern:
            return True
    return False


def collect_type_occurrences(
    root: Path,
    ignore_names: Set[str],
    include_tests: bool,
    exclude_projects: Set[str],
) -> Tuple[List[TypeOccurrence], int]:
    """
    Collect type occurrences, excluding specified projects.
    Returns: (occurrences, excluded_count)
    """
    occurrences: List[TypeOccurrence] = []
    excluded_count = 0

    for path in root.rglob('*.cs'):
        if not path.is_file():
            continue
        if any(part in SKIP_DIR_NAMES for part in path.parts):
            continue
        stem = extract_type_name(path.name)
        if not stem or stem in ignore_names:
            continue
        relative_path = path.relative_to(root)
        parts = relative_path.parts
        layer = parts[0] if parts else ''
        if not include_tests and layer.lower() in TEST_LAYER_NAMES:
            continue
        project = determine_project(parts)

        # ✅ Check if project should be excluded
        if should_exclude_project(project, exclude_projects):
            excluded_count += 1
            continue

        namespace_hint = build_namespace_hint(parts)
        occurrence = TypeOccurrence(
            name=stem,
            path=path,
            relative_path=relative_path,
            layer=layer,
            project=project,
            namespace_hint=namespace_hint,
        )
        occurrences.append(occurrence)

    return occurrences, excluded_count


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
    total_types: int,
    excluded_count: int,
    exclude_projects: Set[str],
    max_similar: int,
) -> str:
    lines: List[str] = []
    lines.append('=' * 80)
    lines.append('DUPLICATE TYPE DETECTION REPORT - Enhanced with Project Exclusions')
    lines.append('=' * 80)
    lines.append('')
    lines.append(f'Total type files scanned: {total_types}')
    lines.append(f'Files excluded by project filters: {excluded_count}')
    lines.append(f'Exact duplicates across projects/namespaces: {len(duplicates)}')
    lines.append(f'Potentially similar names (ratio >= threshold): {len(similar_names)}')
    lines.append('')

    if exclude_projects:
        lines.append('--- Excluded Projects/Patterns ---')
        for pattern in sorted(exclude_projects):
            lines.append(f'  ✗ {pattern}')
        lines.append('')

    if duplicates:
        lines.append('=== Exact duplicates ===')
        for name, occurrences in duplicates:
            lines.append(f'- {name} ({len(occurrences)} occurrences)')
            for occ in occurrences:
                lines.append(
                    f'    - layer={occ.layer or "?"} | project={occ.project} | path={occ.relative_path}'
                )
        lines.append('')
    else:
        lines.append('✅ No exact duplicates detected across distinct projects/namespaces.')
        lines.append('')

    if similar_names:
        lines.append('=== Similar names (Code Drift Candidates) ===')
        for ratio, left, right, left_occ, right_occ in similar_names[:max_similar]:
            lines.append(f'- {left} <-> {right} (similarity={ratio:.2f})')
            for occ in left_occ:
                lines.append(
                    f'    - {left} -> layer={occ.layer or "?"}, project={occ.project}, path={occ.relative_path}'
                )
            for occ in right_occ:
                lines.append(
                    f'    - {right} -> layer={occ.layer or "?"}, project={occ.project}, path={occ.relative_path}'
                )
        if len(similar_names) > max_similar:
            remaining = len(similar_names) - max_similar
            lines.append(
                f'    ... {remaining} additional similar pairs not shown (increase --max-similar to view).'
            )
    else:
        lines.append('✅ No similar type names surpassed the configured threshold.')

    lines.append('')
    lines.append('=' * 80)
    lines.append('NOTE: Pyramidal Testing Strategy - Infrastructure duplicates excluded')
    lines.append('=' * 80)

    return '\n'.join(lines)


def write_report(report: str, destination: Path) -> Path:
    output_path = destination if destination.is_absolute() else Path.cwd() / destination
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(report + '\n', encoding='utf-8')
    return output_path


def list_default_exclusions() -> None:
    """List all default project exclusions."""
    print('=' * 80)
    print('DEFAULT PROJECT EXCLUSIONS - Pyramidal Testing Strategy')
    print('=' * 80)
    print('')
    print('The following projects are excluded by default as they are intentional')
    print('Infrastructure duplicates for different test abstraction levels:')
    print('')
    print('04AdapterTests - Adapter layer (in-memory/mock implementations):')
    for pattern in sorted(DEFAULT_EXCLUDE_PROJECTS):
        if 'Adapter.Test' in pattern:
            print(f'  ✗ {pattern}')
    print('')
    print('05IntegrationTests - Integration layer (real Docker containers):')
    for pattern in sorted(DEFAULT_EXCLUDE_PROJECTS):
        if 'Integration' in pattern:
            print(f'  ✗ {pattern}')
    print('')
    print('Test Infrastructure Fixtures (intentional duplicates):')
    for pattern in sorted(DEFAULT_EXCLUDE_PROJECTS):
        if 'Fixture' in pattern or 'Helper' in pattern or 'InMemory' in pattern:
            print(f'  ✗ {pattern}')
    print('')
    print(f'Total default exclusions: {len(DEFAULT_EXCLUDE_PROJECTS)}')
    print('=' * 80)


def main() -> None:
    args = parse_args()

    # Handle --list-exclusions flag
    if args.list_exclusions:
        list_default_exclusions()
        return

    root = args.root.resolve()
    if not root.exists():
        raise SystemExit(f'Root path {root} does not exist.')

    ignore_names = {name.strip() for name in DEFAULT_IGNORE_NAMES}
    ignore_names.update(name.strip() for name in args.ignore_name if name)

    # Build exclude projects set
    exclude_projects: Set[str] = set()
    if not args.no_default_exclusions:
        exclude_projects.update(DEFAULT_EXCLUDE_PROJECTS)
    exclude_projects.update(pattern.strip() for pattern in args.exclude_project if pattern)

    print('=' * 80)
    print('DUPLICATE TYPE DETECTION - Enhanced with Project Exclusions')
    print('=' * 80)
    print(f'Root: {root}')
    print(f'Threshold: {args.threshold}')
    print(f'Exclude tests layer: {args.exclude_tests}')
    print(f'Project exclusions active: {len(exclude_projects)}')
    if exclude_projects and len(exclude_projects) <= 5:
        for pattern in sorted(list(exclude_projects)[:5]):
            print(f'  ✗ {pattern}')
    elif exclude_projects:
        print(f'  (Use --list-exclusions to see all {len(exclude_projects)} patterns)')
    print('=' * 80)
    print('')

    occurrences, excluded_count = collect_type_occurrences(
        root=root,
        ignore_names=ignore_names,
        include_tests=not args.exclude_tests,
        exclude_projects=exclude_projects,
    )

    if not occurrences:
        print('⚠️  No type files found under the provided root after exclusions.')
        return

    grouped = group_by_name(occurrences)
    duplicates = find_exact_duplicates(grouped)
    similar_names = find_similar_names(grouped, args.threshold)

    report_text = build_report(
        duplicates=duplicates,
        similar_names=similar_names,
        total_types=len(occurrences),
        excluded_count=excluded_count,
        exclude_projects=exclude_projects,
        max_similar=max(0, args.max_similar),
    )
    print(report_text)
    output_path = write_report(report_text, args.output)
    print(f'\n📄 Report saved to {output_path}')
    print('')
    print('✅ Analysis complete!')


if __name__ == '__main__':
    main()
