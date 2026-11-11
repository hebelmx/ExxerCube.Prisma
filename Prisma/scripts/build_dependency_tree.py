#!/usr/bin/env python3
"""
Builds a lightweight dependency tree (namespace + using directives) for ExxerAI.
Outputs JSON to scripts/type_dependency_tree.json for downstream architecture tooling.
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import Dict, Set

USING_PATTERN = re.compile(r"^\s*using\s+([A-Za-z0-9_.]+)\s*;", re.MULTILINE)
NAMESPACE_PATTERN = re.compile(r"\bnamespace\s+([A-Za-z0-9_.]+)")
CLASS_PATTERN = re.compile(r"\bclass\s+([A-Za-z0-9_]+)")

SKIP_DIRS = {"bin", "obj", ".git", ".vs", "artifacts", "__pycache__"}


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIRS for part in path.parts)


def scan_file(path: Path) -> dict | None:
    try:
        text = path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return None

    namespace_match = NAMESPACE_PATTERN.search(text)
    class_match = CLASS_PATTERN.search(text)
    if not namespace_match or not class_match:
        return None

    namespace = namespace_match.group(1)
    class_name = class_match.group(1)
    usings: Set[str] = set(USING_PATTERN.findall(text))

    return {
        "identifier": f"{namespace}.{class_name}",
        "namespace": namespace,
        "class": class_name,
        "usings": sorted(usings),
        "file": str(path),
    }


def build_tree(src_root: Path) -> Dict[str, dict]:
    tree: Dict[str, dict] = {}
    for path in src_root.rglob("*.cs"):
        if should_skip(path):
            continue
        record = scan_file(path)
        if record:
            key = record["identifier"]
            record["file"] = str(Path(record["file"]).relative_to(src_root.parent))
            tree[key] = record
    return tree


def main() -> None:
    parser = argparse.ArgumentParser(description="Build namespace dependency tree.")
    parser.add_argument("--base-path", default=".", help="Repository root")
    parser.add_argument("--output", default="scripts/type_dependency_tree.json", help="Output JSON file")
    args = parser.parse_args()

    repo_root = Path(args.base_path).resolve()
    src_root = repo_root / "code" / "src"

    print(f"🔍 Building dependency tree from {src_root}...")
    tree = build_tree(src_root)

    output_path = repo_root / args.output
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(tree, indent=2), encoding="utf-8")

    print(f"✅ Dependency tree written to {output_path} ({len(tree)} entries)")


if __name__ == "__main__":
    main()
