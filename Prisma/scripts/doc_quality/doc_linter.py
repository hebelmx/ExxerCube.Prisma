"""Doc-quality heuristics that flag weak or missing XML documentation."""

from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional, Sequence

DEFAULT_CATALOG = Path("artifacts/doclint/types_index.json")
DEFAULT_USAGE = Path("artifacts/doclint/type_usage.json")
DEFAULT_OUTPUT = Path("artifacts/doclint/doc_quality_report.json")

MEANINGLESS_PHRASES = {
    "gets the value",
    "sets the value",
    "does something",
    "sets the foo",
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


def _load_json(path: Path) -> Dict[str, object]:
    if not path.exists():
        raise FileNotFoundError(f"Expected file at {path}")
    return json.loads(path.read_text(encoding="utf-8"))


def _context_for_type(type_id: str, usage_index: Dict[str, Dict[str, object]]) -> Dict[str, object]:
    usage_entry = usage_index.get(type_id, {})
    consumers = usage_entry.get("consumers", []) or []
    callers = sorted({item.get("consumerType") for item in consumers if item.get("consumerType")})
    return {
        "typeImportance": usage_entry.get("totalConsumers", 0),
        "callers": callers[:5],
    }


def _is_summary_meaningless(summary: str, name: str) -> bool:
    if not summary:
        return False
    tokens = summary.strip().split()
    if len(tokens) < 6:
        return True
    lowered = summary.lower()
    if name and name.lower() in lowered:
        return True
    return any(phrase in lowered for phrase in MEANINGLESS_PHRASES)


def _infer_return_type(member: Dict[str, object]) -> Optional[str]:
    if member.get("kind") in {"constructor", "property", "field"}:
        return None
    signature = (member.get("signature") or "").strip()
    if "(" not in signature:
        return None
    before = signature.split("(", 1)[0].strip()
    if not before:
        return None
    tokens = [token for token in before.split() if token.lower() not in MODIFIERS]
    if len(tokens) < 2:
        return None
    name = tokens[-1]
    return_type = tokens[-2]
    if name != member.get("name"):
        return_type = tokens[-1]
    if return_type == member.get("name"):
        return None
    return return_type


def _issue(
    *,
    rule: str,
    severity: str,
    message: str,
    type_entry: Dict[str, object],
    line: int,
    context: Dict[str, object],
    member: Optional[Dict[str, object]] = None,
    extras: Optional[Dict[str, object]] = None,
) -> Dict[str, object]:
    issue = {
        "rule": rule,
        "severity": severity,
        "message": message,
        "typeId": type_entry["typeId"],
        "file": type_entry["file"],
        "line": line,
        "project": type_entry.get("project"),
        "layer": type_entry.get("layer"),
        "context": context,
    }
    if member:
        issue["member"] = member.get("name")
        issue["memberKind"] = member.get("kind")
    if extras:
        issue.update(extras)
    return issue


def _evaluate_type(
    type_entry: Dict[str, object], usage_index: Dict[str, Dict[str, object]]
) -> List[Dict[str, object]]:
    issues: List[Dict[str, object]] = []
    xml_doc = type_entry.get("xmlDoc") or {}
    summary = (xml_doc.get("summary") or "").strip()
    context = _context_for_type(type_entry["typeId"], usage_index)

    if not summary:
        issues.append(
            _issue(
                rule="MissingSummary",
                severity="error",
                message="Type is missing a <summary> description.",
                type_entry=type_entry,
                line=type_entry.get("line", 0),
                context=context,
                extras={"currentSummary": summary},
            )
        )
    elif _is_summary_meaningless(summary, type_entry.get("name", "")):
        issues.append(
            _issue(
                rule="SummaryMeaningless",
                severity="warning",
                message="Summary does not explain the domain behavior.",
                type_entry=type_entry,
                line=type_entry.get("line", 0),
                context=context,
                extras={"currentSummary": summary},
            )
        )
    return issues


def _evaluate_member(
    type_entry: Dict[str, object],
    member: Dict[str, object],
    usage_index: Dict[str, Dict[str, object]],
) -> List[Dict[str, object]]:
    issues: List[Dict[str, object]] = []
    xml_doc = member.get("xmlDoc") or {}
    summary = (xml_doc.get("summary") or "").strip()
    context = _context_for_type(type_entry["typeId"], usage_index)
    line = member.get("line") or type_entry.get("line", 0)

    if not summary:
        issues.append(
            _issue(
                rule="MissingSummary",
                severity="error",
                message=f"{member.get('name')} is missing XML summary.",
                type_entry=type_entry,
                line=line,
                context=context,
                member=member,
                extras={"currentSummary": summary},
            )
        )
    elif _is_summary_meaningless(summary, member.get("name", "")):
        issues.append(
            _issue(
                rule="SummaryMeaningless",
                severity="warning",
                message="Summary is too short or repeats the member name.",
                type_entry=type_entry,
                line=line,
                context=context,
                member=member,
                extras={"currentSummary": summary},
            )
        )

    parameters = member.get("parameters") or []
    doc_params = (xml_doc.get("params") or {}) if xml_doc else {}
    missing_params = [
        param for param in parameters if not doc_params.get(param, "").strip()
    ]
    if missing_params:
        issues.append(
            _issue(
                rule="MissingParamDoc",
                severity="warning",
                message=f"Missing <param> entries for: {', '.join(missing_params)}.",
                type_entry=type_entry,
                line=line,
                context=context,
                member=member,
                extras={"missingParams": missing_params},
            )
        )

    return_type = _infer_return_type(member)
    if return_type and not (xml_doc.get("returns") or "").strip():
        issues.append(
            _issue(
                rule="MissingReturnsDoc",
                severity="info",
                message="Method returns a value but lacks a <returns> description.",
                type_entry=type_entry,
                line=line,
                context=context,
                member=member,
                extras={"returnType": return_type},
            )
        )

    return issues


def build_doc_quality_report(
    *,
    root: Path,
    catalog_data: Optional[Dict[str, object]] = None,
    catalog_path: Optional[Path] = None,
    usage_data: Optional[Dict[str, object]] = None,
    usage_path: Optional[Path] = None,
    output_path: Optional[Path] = None,
) -> Dict[str, object]:
    """Evaluate XML docs using heuristics and emit a SARIF-friendly JSON blob."""
    root = root.resolve()
    catalog = catalog_data or _load_json(catalog_path or DEFAULT_CATALOG)
    usage = usage_data or _load_json(usage_path or DEFAULT_USAGE)
    usage_index = {entry["targetType"]: entry for entry in usage.get("usages", [])}

    issues: List[Dict[str, object]] = []
    for type_entry in catalog.get("types", []):
        issues.extend(_evaluate_type(type_entry, usage_index))
        for member in type_entry.get("members", []):
            issues.extend(_evaluate_member(type_entry, member, usage_index))

    result = {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "root": str(root),
        "issueCount": len(issues),
        "issues": issues,
        "sourceCatalogGeneratedAt": catalog.get("generatedAt"),
        "sourceUsageGeneratedAt": usage.get("generatedAt"),
    }

    if output_path:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(result, indent=2), encoding="utf-8")
    return result


def _parse_args(argv: Optional[Sequence[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run doc-quality heuristics.")
    parser.add_argument(
        "--root",
        "--cwd",
        dest="root",
        type=Path,
        default=Path.cwd(),
        help="Repository root (defaults to current working directory).",
    )
    parser.add_argument(
        "--catalog",
        dest="catalog",
        type=Path,
        default=DEFAULT_CATALOG,
        help="Path to types_index.json.",
    )
    parser.add_argument(
        "--usage",
        dest="usage",
        type=Path,
        default=DEFAULT_USAGE,
        help="Path to type_usage.json.",
    )
    parser.add_argument(
        "--output",
        dest="output",
        type=Path,
        default=DEFAULT_OUTPUT,
        help="Destination for doc_quality_report.json.",
    )
    return parser.parse_args(argv)


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = _parse_args(argv)
    build_doc_quality_report(
        root=args.root,
        catalog_path=args.catalog,
        usage_path=args.usage,
        output_path=args.output,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
