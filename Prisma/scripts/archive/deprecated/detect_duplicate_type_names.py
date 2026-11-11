#!/usr/bin/env python3
"""Detect duplicated or suspiciously similar type names across the ExxerAI solution."""
import argparse
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
            'across projects and layers.'
        )
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


def collect_type_occurrences(
    root: Path,
    ignore_names: Set[str],
    include_tests: bool,
) -> List[TypeOccurrence]:
    occurrences: List[TypeOccurrence] = []
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
    return occurrences


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
    max_similar: int,
) -> str:
    lines: List[str] = []
    lines.append(f'Total type files scanned: {total_types}')
    lines.append(f'Exact duplicates across projects/namespaces: {len(duplicates)}')
    lines.append(f'Potentially similar names (ratio >= threshold): {len(similar_names)}')
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
        lines.append('No exact duplicates detected across distinct projects/namespaces.')
        lines.append('')

    if similar_names:
        lines.append('=== Similar names ===')
        for ratio, left, right, left_occ, right_occ in similar_names[:max_similar]:
            lines.append(f'- {left} <-> {right} (ratio={ratio:.2f})')
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
        lines.append('No similar type names surpassed the configured threshold.')

    return '\n'.join(lines)


def write_report(report: str, destination: Path) -> Path:
    output_path = destination if destination.is_absolute() else Path.cwd() / destination
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(report + '\n', encoding='utf-8')
    return output_path

def main() -> None:
    args = parse_args()
    root = args.root.resolve()
    if not root.exists():
        raise SystemExit(f'Root path {root} does not exist.')

    ignore_names = {name.strip() for name in DEFAULT_IGNORE_NAMES}
    ignore_names.update(name.strip() for name in args.ignore_name if name)

    occurrences = collect_type_occurrences(
        root=root,
        ignore_names=ignore_names,
        include_tests=not args.exclude_tests,
    )

    if not occurrences:
        print('No type files found under the provided root.')
        return

    grouped = group_by_name(occurrences)
    duplicates = find_exact_duplicates(grouped)
    similar_names = find_similar_names(grouped, args.threshold)

    report_text = build_report(
        duplicates=duplicates,
        similar_names=similar_names,
        total_types=len(occurrences),
        max_similar=max(0, args.max_similar),
    )
    print(report_text)
    output_path = write_report(report_text, args.output)
    print(f'\nReport saved to {output_path}')


if __name__ == '__main__':
    main()




