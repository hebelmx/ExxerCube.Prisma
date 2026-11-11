"""Generates the type catalog JSON artifact for ExxerAI."""

from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence
import hashlib

from .models import TypeInfo
from .scanner import (
    CSharpFileParser,
    ProjectLocator,
    DEFAULT_INCLUDE_PATTERNS,
    discover_cs_files,
    infer_layer,
    normalize_rel_path,
)

DEFAULT_OUTPUT = Path("artifacts/doclint/types_index.json")


def _ensure_output_directory(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)


def _load_previous(output_path: Path) -> Dict[str, object]:
    if not output_path.exists():
        return {}
    try:
        return json.loads(output_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return {}


def _group_types_by_file(entries: Sequence[Dict[str, object]]) -> Dict[str, List[Dict[str, object]]]:
    grouped: Dict[str, List[Dict[str, object]]] = {}
    for entry in entries:
        grouped.setdefault(entry["file"], []).append(entry)
    return grouped


def _type_entry(type_info: TypeInfo, root: Path) -> Dict[str, object]:
    doc_hash = (
        hashlib.sha1(type_info.xml_doc.raw.encode("utf-8")).hexdigest()
        if type_info.xml_doc.raw
        else None
    )
    return type_info.to_catalog_entry(root, doc_hash)


def build_type_catalog(
    *,
    root: Path,
    include_patterns: Optional[Sequence[str]] = None,
    exclude_patterns: Optional[Sequence[str]] = None,
    domain_first: bool = True,
    output_path: Optional[Path] = None,
    incremental: bool = False,
) -> Dict[str, object]:
    """Build the type catalog and optionally persist it."""
    root = root.resolve()
    files = discover_cs_files(
        root,
        include_patterns=include_patterns,
        exclude_patterns=exclude_patterns,
        domain_first=domain_first,
    )
    locator = ProjectLocator(root)

    previous = {}
    previous_fingerprints: Dict[str, str] = {}
    previous_by_file: Dict[str, List[Dict[str, object]]] = {}
    if incremental and output_path:
        previous = _load_previous(output_path)
        previous_fingerprints = previous.get("sourceFingerprints", {})
        previous_by_file = _group_types_by_file(previous.get("types", []))

    entries: List[Dict[str, object]] = []
    fingerprints: Dict[str, str] = {}

    for file_path in files:
        rel_file = normalize_rel_path(file_path, root)
        project = locator.find(file_path)
        layer = infer_layer(file_path)
        text = file_path.read_text(encoding="utf-8")
        file_hash = hashlib.sha1(text.encode("utf-8")).hexdigest()
        fingerprints[rel_file] = file_hash

        if incremental and previous_fingerprints.get(rel_file) == file_hash:
            entries.extend(previous_by_file.get(rel_file, []))
            continue

        parser = CSharpFileParser(root, file_path, project, layer)
        types, _ = parser.parse(text=text)
        entries.extend(_type_entry(type_info, root) for type_info in types)

    include_report: Sequence[str] = (
        include_patterns
        if include_patterns
        else (["code/src/**/*.cs"] if not domain_first else tuple(DEFAULT_INCLUDE_PATTERNS))
    )

    result = {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "root": str(root),
        "totalTypes": len(entries),
        "fileCount": len(files),
        "filters": {
            "include": list(include_report),
            "exclude": exclude_patterns or [],
        },
        "types": entries,
        "sourceFingerprints": fingerprints,
    }

    if output_path:
        _ensure_output_directory(output_path)
        output_path.write_text(json.dumps(result, indent=2), encoding="utf-8")

    return result


def _parse_args(argv: Optional[Sequence[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate the doc-quality type catalog artifact."
    )
    parser.add_argument(
        "--root",
        "--cwd",
        dest="root",
        default=Path.cwd(),
        type=Path,
        help="Repository root (defaults to current working directory).",
    )
    parser.add_argument(
        "--include",
        action="append",
        dest="include",
        help="Glob pattern(s) to include (defaults to Domain-focused scan).",
    )
    parser.add_argument(
        "--exclude",
        action="append",
        dest="exclude",
        help="Glob pattern(s) to exclude. Applied after built-in ignores.",
    )
    parser.add_argument(
        "--output",
        dest="output",
        type=Path,
        default=DEFAULT_OUTPUT,
        help="Output JSON path (defaults to artifacts/doclint/types_index.json).",
    )
    parser.add_argument(
        "--no-domain-first",
        dest="domain_first",
        action="store_false",
        default=True,
        help="Scan entire repo instead of prioritizing Domain globs.",
    )
    parser.add_argument(
        "--incremental",
        action="store_true",
        help="Reuse existing JSON entries when the file hash is unchanged.",
    )
    return parser.parse_args(argv)


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = _parse_args(argv)
    build_type_catalog(
        root=args.root,
        include_patterns=args.include,
        exclude_patterns=args.exclude,
        domain_first=args.domain_first,
        output_path=args.output,
        incremental=args.incremental,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
