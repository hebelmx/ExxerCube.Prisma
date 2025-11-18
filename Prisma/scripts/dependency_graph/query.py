"""Query utilities for dependency graph artifacts."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Iterable, List, Optional, Sequence, Set


def find_dependents(
    *,
    graph_data: dict,
    target_id: str,
    relations: Optional[Iterable[str]] = None,
) -> List[str]:
    """Return sorted list of source nodes that depend on target."""
    relation_filter: Optional[Set[str]] = set(relations) if relations else None
    dependents = {
        edge["source"]
        for edge in graph_data.get("edges", [])
        if edge.get("target") == target_id
        and (relation_filter is None or edge.get("relation") in relation_filter)
    }
    return sorted(dependents)


def _load_graph(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def _parse_args(argv: Optional[Sequence[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Query dependency graph artifacts.")
    parser.add_argument(
        "--graph",
        type=Path,
        required=True,
        help="Path to dependency_graph.json.",
    )
    parser.add_argument(
        "--dependents",
        dest="target",
        help="Return dependents of the specified type id.",
    )
    parser.add_argument(
        "--relation",
        action="append",
        dest="relations",
        help="Filter edges by relation (can repeat).",
    )
    return parser.parse_args(argv)


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = _parse_args(argv)
    graph = _load_graph(args.graph)
    if args.target:
        dependents = find_dependents(
            graph_data=graph,
            target_id=args.target,
            relations=args.relations,
        )
        for dep in dependents:
            print(dep)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
