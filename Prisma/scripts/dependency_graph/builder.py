"""Builds the ExxerAI dependency graph."""

from __future__ import annotations

import argparse
import json
from collections import defaultdict
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Set

from scripts.doc_quality.scanner import (
    CSharpFileParser,
    ProjectLocator,
    discover_cs_files,
    infer_layer,
    normalize_rel_path,
)


DEFAULT_OUTPUT = Path("artifacts/graph/dependency_graph.json")
RELATION_STRENGTH = {
    "inheritance": "hard",
    "implements_interface": "hard",
    "constructor_parameter": "hard",
    "property_dependency": "hard",
    "method_parameter": "hard",
}
MODIFIERS = {
    "public",
    "protected",
    "internal",
    "private",
    "static",
    "virtual",
    "override",
    "sealed",
    "partial",
    "async",
    "extern",
    "unsafe",
    "readonly",
    "new",
}


def _ensure_output_dir(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)


def _strip_generics(token: str) -> str:
    base = token.split("<", 1)[0]
    base = base.split("?", 1)[0]
    base = base.split("[", 1)[0]
    return base.strip()


def _clean_type_token(token: str) -> str:
    token = token.strip()
    if not token:
        return ""
    token = token.replace("global::", "")
    token = token.split(" where ", 1)[0]
    token = _strip_generics(token)
    return token.split(".", 0)[0] if token.endswith("::") else token


@dataclass
class TypeResolver:
    """Resolve type tokens to known type identifiers."""

    full_names: Dict[str, str]
    simple_map: Dict[str, List[str]]

    def resolve(self, token: str, namespace: str) -> Optional[str]:
        cleaned = _clean_type_token(token)
        if not cleaned:
            return None
        if cleaned in self.full_names:
            return cleaned
        if "." in cleaned:
            for type_id in self.full_names:
                if type_id == cleaned or type_id.endswith(cleaned):
                    return type_id
        simple = cleaned.split(".")[-1]
        candidates = self.simple_map.get(simple)
        if not candidates:
            return None
        if len(candidates) == 1:
            return candidates[0]
        for candidate in candidates:
            candidate_ns = ".".join(candidate.split(".")[:-1])
            if namespace and candidate_ns.startswith(namespace.split(".")[0]):
                return candidate
        return candidates[0]


def _build_resolver(type_infos: Iterable) -> TypeResolver:
    full_names: Dict[str, str] = {}
    simple_map: Dict[str, List[str]] = defaultdict(list)
    for type_info in type_infos:
        type_id = type_info.type_id()
        full_names[type_id] = type_id
        simple_map[type_info.name].append(type_id)
    return TypeResolver(full_names=full_names, simple_map=simple_map)


def _parse_parameters(param_block: str) -> List[Dict[str, str]]:
    if not param_block.strip():
        return []
    params: List[Dict[str, str]] = []
    depth = 0
    current = []
    for char in param_block:
        if char == "<":
            depth += 1
        elif char == ">":
            depth -= 1
        elif char == "," and depth == 0:
            params.append("".join(current).strip())
            current = []
            continue
        current.append(char)
    if current:
        params.append("".join(current).strip())

    results: List[Dict[str, str]] = []
    for param in params:
        if not param:
            continue
        fragment = param.split("=", 1)[0].strip()
        tokens = fragment.split()
        if not tokens:
            continue
        name = tokens[-1]
        type_token = " ".join(tokens[:-1]) if len(tokens) > 1 else tokens[0]
        if name in {"out", "ref", "in"} and len(tokens) >= 2:
            name = tokens[-2]
            type_token = " ".join(tokens[:-2])
        results.append({"name": name.strip(), "type": type_token.strip()})
    return results


def _extract_method_return(before_paren: str, member_name: str) -> Optional[str]:
    if not before_paren:
        return None
    tokens = [token for token in before_paren.strip().split() if token]
    filtered = [token for token in tokens if token not in MODIFIERS]
    if not filtered:
        return None
    if filtered[-1] == member_name:
        if len(filtered) >= 2:
            return filtered[-2]
        return None
    if len(filtered) >= 2 and filtered[-2] == member_name:
        if len(filtered) >= 3:
            return filtered[-3]
        return None
    if filtered[-1] != member_name:
        return filtered[-1]
    return None


def _extract_edges_for_type(
    type_info,
    resolver: TypeResolver,
    relations_filter: Optional[Set[str]],
    root: Path,
) -> List[Dict[str, object]]:
    edges: List[Dict[str, object]] = []
    rel_path = type_info.relative_file(root)
    namespace = type_info.namespace

    def allow(relation: str) -> bool:
        return not relations_filter or relation in relations_filter

    for base in type_info.bases:
        target = resolver.resolve(base, namespace)
        if not target or not allow("inheritance"):
            continue
        relation = "implements_interface" if base.strip().startswith("I") else "inheritance"
        if not allow(relation):
            continue
        edges.append(
            {
                "source": type_info.type_id(),
                "target": target,
                "relation": relation,
                "strength": RELATION_STRENGTH.get(relation, "hard"),
                "file": rel_path,
                "line": type_info.line,
                "member": None,
            }
        )

    for member in type_info.members:
        if member.kind == "constructor" and allow("constructor_parameter"):
            signature = member.signature
            params_part = ""
            if "(" in signature and ")" in signature:
                params_part = signature.split("(", 1)[1].split(")", 1)[0]
            for param in _parse_parameters(params_part):
                target = resolver.resolve(param["type"], namespace)
                if not target:
                    continue
                edges.append(
                    {
                        "source": type_info.type_id(),
                        "target": target,
                        "relation": "constructor_parameter",
                        "strength": "hard",
                        "file": rel_path,
                        "line": member.line,
                        "member": member.name,
                    }
                )

        if member.kind == "method" and allow("method_parameter"):
            params_part = ""
            signature = member.signature
            if "(" in signature and ")" in signature:
                before, after = signature.split("(", 1)
                params_part = after.split(")", 1)[0]
            for param in _parse_parameters(params_part):
                target = resolver.resolve(param["type"], namespace)
                if not target:
                    continue
                edges.append(
                    {
                        "source": type_info.type_id(),
                        "target": target,
                        "relation": "method_parameter",
                        "strength": "hard",
                        "file": rel_path,
                        "line": member.line,
                        "member": member.name,
                    }
                )

        if member.kind == "property" and allow("property_dependency"):
            signature = member.signature.split("{", 1)[0].strip()
            tokens = signature.split()
            if len(tokens) >= 2:
                type_token = tokens[-2]
                target = resolver.resolve(type_token, namespace)
                if target:
                    edges.append(
                        {
                            "source": type_info.type_id(),
                            "target": target,
                            "relation": "property_dependency",
                            "strength": "hard",
                            "file": rel_path,
                            "line": member.line,
                            "member": member.name,
                        }
                    )

    return edges


def build_dependency_graph(
    *,
    root: Path,
    include_patterns: Optional[Sequence[str]] = None,
    exclude_patterns: Optional[Sequence[str]] = None,
    domain_first: bool = True,
    output_path: Optional[Path] = None,
    relations: Optional[Iterable[str]] = None,
) -> Dict[str, object]:
    """Build the dependency graph JSON artifact."""
    root = root.resolve()
    files = discover_cs_files(
        root,
        include_patterns=include_patterns,
        exclude_patterns=exclude_patterns,
        domain_first=domain_first,
    )
    locator = ProjectLocator(root)
    type_infos = []
    for file_path in files:
        project = locator.find(file_path)
        layer = infer_layer(file_path)
        parser = CSharpFileParser(root, file_path, project, layer)
        parsed, _ = parser.parse()
        type_infos.extend(parsed)

    resolver = _build_resolver(type_infos)
    relations_filter = set(relations) if relations else None

    nodes = []
    node_ids = set()
    for type_info in type_infos:
        node_id = type_info.type_id()
        if node_id in node_ids:
            continue
        node_ids.add(node_id)
        nodes.append(
            {
                "id": node_id,
                "name": type_info.name,
                "namespace": type_info.namespace,
                "project": type_info.project,
                "layer": type_info.layer,
                "kind": type_info.kind,
                "file": type_info.relative_file(root),
                "line": type_info.line,
            }
        )

    edges = []
    seen_edges: Set[tuple] = set()
    for type_info in type_infos:
        for edge in _extract_edges_for_type(type_info, resolver, relations_filter, root):
            key = (edge["source"], edge["target"], edge["relation"], edge["line"])
            if key in seen_edges:
                continue
            seen_edges.add(key)
            edges.append(edge)

    result = {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "root": str(root),
        "nodeCount": len(nodes),
        "edgeCount": len(edges),
        "nodes": nodes,
        "edges": edges,
    }

    if output_path:
        _ensure_output_dir(output_path)
        output_path.write_text(json.dumps(result, indent=2), encoding="utf-8")

    return result


def _parse_args(argv: Optional[Sequence[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build the ExxerAI dependency graph.")
    parser.add_argument(
        "--root",
        "--cwd",
        dest="root",
        type=Path,
        default=Path.cwd(),
        help="Repository root (defaults to current directory).",
    )
    parser.add_argument(
        "--include",
        action="append",
        dest="include",
        help="Glob pattern(s) to include.",
    )
    parser.add_argument(
        "--exclude",
        action="append",
        dest="exclude",
        help="Glob pattern(s) to exclude.",
    )
    parser.add_argument(
        "--no-domain-first",
        dest="domain_first",
        action="store_false",
        default=True,
        help="Scan all folders instead of Domain-first patterns.",
    )
    parser.add_argument(
        "--relations",
        action="append",
        dest="relations",
        help="Relation filters (e.g., constructor_parameter). Default: all.",
    )
    parser.add_argument(
        "--output",
        dest="output",
        type=Path,
        default=DEFAULT_OUTPUT,
        help="Output JSON path.",
    )
    return parser.parse_args(argv)


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = _parse_args(argv)
    build_dependency_graph(
        root=args.root,
        include_patterns=args.include,
        exclude_patterns=args.exclude,
        domain_first=args.domain_first,
        relations=args.relations,
        output_path=args.output,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
