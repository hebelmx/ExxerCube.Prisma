"""Builds the type usage/dependency graph."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

from .models import TypeInfo
from .scanner import (
    CSharpFileParser,
    ProjectLocator,
    discover_cs_files,
    infer_layer,
    normalize_rel_path,
)

DEFAULT_OUTPUT = Path("artifacts/doclint/type_usage.json")
DEFAULT_CATALOG = Path("artifacts/doclint/types_index.json")


def _sanitize_type_name(name: str) -> str:
    base = name.split("<", 1)[0]
    return base.strip()


def _load_catalog(path: Path) -> Dict[str, object]:
    if not path.exists():
        raise FileNotFoundError(
            f"Type catalog not found at {path}. Run type_catalog.py first."
        )
    return json.loads(path.read_text(encoding="utf-8"))


def _compile_pattern(names: Iterable[str]) -> Optional[re.Pattern]:
    tokens = [re.escape(name) for name in names if name]
    if not tokens:
        return None
    union = "|".join(sorted(tokens, key=len, reverse=True))
    return re.compile(rf"\b(?P<type>{union})\b")


def _locate_type(types: List[TypeInfo], line_number: int) -> Optional[TypeInfo]:
    for type_info in reversed(types):
        start = type_info.line
        end = type_info.end_line or 10**9
        if start <= line_number <= end:
            return type_info
    return None


def _locate_member(type_info: TypeInfo, line_number: int) -> Optional[Dict[str, object]]:
    if not type_info.members:
        return None
    members = sorted(type_info.members, key=lambda member: member.line)
    for idx, member in enumerate(members):
        next_line = (
            members[idx + 1].line if idx + 1 < len(members) else type_info.end_line or 10**9
        )
        if member.line <= line_number < next_line:
            return {
                "name": member.name,
                "kind": member.kind,
                "line": member.line,
            }
    return None


def _classify_relation(line: str, type_name: str) -> str:
    stripped = line.strip()
    lowered = stripped.lower()

    if stripped.startswith("using "):
        return "using"
    if stripped.startswith("["):
        return "attribute"
    if any(stripped.startswith(prefix) for prefix in ("class ", "record ", "interface ")):
        if ":" in stripped and type_name in stripped.split(":", 1)[1]:
            return "inheritance"
    if f"new {type_name}" in stripped or f"new {type_name}<" in stripped:
        return "instantiation"
    if f"{type_name}." in stripped:
        return "member_access"
    if "(" in stripped and ")" in stripped:
        params_block = stripped.split("(", 1)[1].split(")")[0]
        if type_name in params_block:
            return "parameter"
    if "{" in stripped and ("get;" in stripped or "set;" in stripped):
        before = stripped.split("{", 1)[0]
        if type_name in before:
            return "property"
    if "=" in stripped:
        before = stripped.split("=", 1)[0]
        if type_name in before:
            return "field"
    if type_name in stripped.split():
        return "reference"
    return "reference"


def _default_include(domain_first: bool) -> Sequence[str]:
    return (
        ("code/src/**/*Domain*/**/*.cs",)
        if domain_first
        else ("code/src/**/*.cs",)
    )


def build_type_usage(
    *,
    root: Path,
    catalog_data: Optional[Dict[str, object]] = None,
    catalog_path: Optional[Path] = None,
    include_patterns: Optional[Sequence[str]] = None,
    exclude_patterns: Optional[Sequence[str]] = None,
    domain_first: bool = False,
    output_path: Optional[Path] = None,
) -> Dict[str, object]:
    """Build the usage graph from the supplied type catalog data."""
    root = root.resolve()

    if catalog_data is None:
        catalog_path = catalog_path or DEFAULT_CATALOG
        catalog_data = _load_catalog(catalog_path)

    target_entries: List[Dict[str, object]] = list(catalog_data.get("types", []))
    name_map: Dict[str, List[Dict[str, object]]] = {}
    for entry in target_entries:
        name = _sanitize_type_name(entry.get("name", ""))
        if not name:
            continue
        name_map.setdefault(name, []).append(entry)

    pattern = _compile_pattern(name_map.keys())
    if not pattern:
        result = {
            "generatedAt": datetime.now(timezone.utc).isoformat(),
            "root": str(root),
            "totalTargets": 0,
            "totalEdges": 0,
            "usages": [],
            "filters": {
                "include": list(include_patterns or _default_include(domain_first)),
                "exclude": exclude_patterns or [],
            },
        }
        if output_path:
            output_path.parent.mkdir(parents=True, exist_ok=True)
            output_path.write_text(json.dumps(result, indent=2), encoding="utf-8")
        return result

    files = discover_cs_files(
        root,
        include_patterns=include_patterns,
        exclude_patterns=exclude_patterns,
        domain_first=domain_first,
    )
    locator = ProjectLocator(root)

    usage_map: Dict[str, List[Dict[str, object]]] = {
        entry["typeId"]: [] for entry in target_entries
    }
    seen_keys: set[Tuple[str, str, int, str]] = set()

    for file_path in files:
        text = file_path.read_text(encoding="utf-8")
        project = locator.find(file_path)
        layer = infer_layer(file_path)
        parser = CSharpFileParser(root, file_path, project, layer)
        type_infos, _ = parser.parse(text=text)
        if not type_infos:
            continue

        rel_file = normalize_rel_path(file_path, root)
        lines = text.splitlines()
        for idx, line in enumerate(lines, start=1):
            consumer_type = _locate_type(type_infos, idx)
            if not consumer_type:
                continue

            for match in pattern.finditer(line):
                matched_name = match.group("type")
                for target in name_map.get(matched_name, []):
                    target_id = target["typeId"]
                    consumer_id = consumer_type.type_id()
                    if target_id == consumer_id:
                        continue
                    relation = _classify_relation(line.strip(), matched_name)
                    key = (target_id, consumer_id, idx, relation)
                    if key in seen_keys:
                        continue
                    seen_keys.add(key)
                    member_info = _locate_member(consumer_type, idx)
                    usage_map[target_id].append(
                        {
                            "consumerType": consumer_id,
                            "consumerMember": member_info["name"] if member_info else None,
                            "memberKind": member_info["kind"] if member_info else None,
                            "relation": relation,
                            "line": idx,
                            "file": rel_file,
                            "snippet": line.strip(),
                        }
                    )

    usages: List[Dict[str, object]] = []
    total_edges = 0
    for entry in sorted(target_entries, key=lambda item: item["typeId"]):
        consumers = usage_map.get(entry["typeId"], [])
        total_edges += len(consumers)
        usages.append(
            {
                "targetType": entry["typeId"],
                "targetProject": entry.get("project"),
                "targetLayer": entry.get("layer"),
                "consumers": consumers,
                "totalConsumers": len(consumers),
            }
        )

    result = {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "root": str(root),
        "totalTargets": len(usages),
        "totalEdges": total_edges,
        "filters": {
            "include": list(include_patterns or _default_include(domain_first)),
            "exclude": exclude_patterns or [],
        },
        "usages": usages,
        "catalogGeneratedAt": catalog_data.get("generatedAt"),
    }

    if output_path:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(result, indent=2), encoding="utf-8")

    return result


def _parse_args(argv: Optional[Sequence[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate the type usage graph.")
    parser.add_argument(
        "--root",
        "--cwd",
        dest="root",
        type=Path,
        default=Path.cwd(),
        help="Repository root (defaults to current directory).",
    )
    parser.add_argument(
        "--catalog",
        dest="catalog",
        type=Path,
        default=DEFAULT_CATALOG,
        help="Path to types_index.json.",
    )
    parser.add_argument(
        "--include",
        action="append",
        dest="include",
        help="Glob pattern(s) of files to scan for consumers.",
    )
    parser.add_argument(
        "--exclude",
        action="append",
        dest="exclude",
        help="Glob pattern(s) to ignore.",
    )
    parser.add_argument(
        "--domain-first",
        dest="domain_first",
        action="store_true",
        help="Limit consumer scan to Domain folders.",
    )
    parser.add_argument(
        "--output",
        dest="output",
        type=Path,
        default=DEFAULT_OUTPUT,
        help="Output JSON path (defaults to artifacts/doclint/type_usage.json).",
    )
    return parser.parse_args(argv)


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = _parse_args(argv)
    build_type_usage(
        root=args.root,
        catalog_path=args.catalog,
        include_patterns=args.include,
        exclude_patterns=args.exclude,
        domain_first=args.domain_first,
        output_path=args.output,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
