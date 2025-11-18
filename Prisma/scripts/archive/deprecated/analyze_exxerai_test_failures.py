#!/usr/bin/env python3
"""
Enhanced failure analysis utility for ExxerAI test suites.

The helper auto-discovers the latest test runner log artifacts, extracts rich
metadata per failure, and now classifies issues into curated failure kinds to
accelerate triage workflows.
"""
from __future__ import annotations

import argparse
import json
import re
import codecs
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

# Regular expression that captures the header line emitted by xUnit when a test fails.
HEADER_RE = re.compile(r"failed\s+(?P<name>.+?)\s*\((?P<duration>[^)]*)\)", re.IGNORECASE)

# Match source locations in stack traces. Handles patterns with or without ':line'.
SOURCE_RE = re.compile(r"in\s+(?P<file>.+?):(?:line\s*)?(?P<line>\d+)\b")

# Strip ANSI escape sequences that the .NET test platform emits when color is enabled.
ANSI_ESCAPE_RE = re.compile(r"\x1B\[[0-?]*[ -/]*[@-~]")

@dataclass
class FailureRecord:
    """Structured representation of a single failing test."""

    suite_key: str
    full_name: str
    duration: str
    message: str
    details: str
    source_file: Optional[str]
    source_line: Optional[int]
    test_class: str
    test_method: str
    category: str
    area: str
    failure_kind: str


def classify_failure_kind(message: str, details: str) -> str:
    """
    Categorise a failure using heuristics tuned to the ExxerAI test corpus.

    The detection favours actionable buckets (e.g., Docker availability,
    assertion mismatches, configuration gaps) so downstream analysis can focus
    on remediation playbooks rather than raw error text.
    """
    combined = f"{message}\n{details}".lower()

    if any(keyword in combined for keyword in ("docker", "testcontainers")):
        return "DockerUnavailable"
    if any(keyword in combined for keyword in ("qdrant", "neo4j", "knowledge store")):
        return "KnowledgeStoreUnavailable"
    if "shouldly.shouldassertexception" in combined or (
        "should be" in combined and "but was" in combined
    ):
        return "AssertionMismatch"
    if "timeout" in combined or "hang" in combined:
        return "Timeout"
    if any(phrase in combined for phrase in ("no connection could be made", "actively refused")):
        return "ConnectionRefused"
    if "configuration" in combined and any(term in combined for term in ("missing", "not configured")):
        return "ConfigurationMissing"
    if "result<" in combined and "withfailure" in combined:
        return "ResultFailure"
    if "operationcanceledexception" in combined:
        return "Cancellation"

    return "Other"


def normalize_spaced_text(text: str) -> str:
    """
    Some historical runners emitted spaced-out characters (e.g., ``f a i l e d``).
    The normalisation keeps the logic backward compatible while leaving modern
    logs untouched.
    """
    normalized = re.sub(r"(\b\w)\s+(?=\w\s)", r"\1", text)
    return re.sub(r"\s+\n", "\n", normalized)


def strip_ansi_sequences(text: str) -> str:
    """Remove ANSI escape sequences (color/highlight codes) from the input."""
    return ANSI_ESCAPE_RE.sub("", text)


def discover_latest_log(project_dir: Path) -> Optional[Path]:
    """
    Return the most recent *.log file produced under the project's TestResults
    directory. The search spans Debug/Release and multiple target frameworks.
    """
    candidates: List[Tuple[float, Path]] = []
    for configuration in ("Debug", "Release"):
        base = project_dir / "bin" / configuration
        if not base.exists():
            continue
        for framework_dir in base.glob("net*/TestResults"):
            for log_file in framework_dir.glob("*.log"):
                try:
                    candidates.append((log_file.stat().st_mtime, log_file))
                except OSError:
                    continue
    if not candidates:
        return None
    _, latest_path = max(candidates, key=lambda item: item[0])
    return latest_path


def segment_blocks(lines: Sequence[str], start_index: int) -> Tuple[int, List[str]]:
    """
    Collect the indented block that follows a failure header. The block spans
    contiguous whitespace-prefixed lines (error message, stack trace, outputs).
    """
    collected: List[str] = []
    index = start_index
    while index < len(lines):
        current = lines[index]
        if not current.startswith((" ", "\t")) and current.strip():
            break
        collected.append(current.rstrip())
        index += 1
    return index, collected


def extract_primary_message(block_lines: Iterable[str]) -> str:
    """
    Heuristically pick the first meaningful error message line. Skip stack frames
    and console output headers.
    """
    for raw in block_lines:
        stripped = raw.strip()
        lowered = stripped.lower()
        if not stripped:
            continue
        if stripped.startswith("at "):
            continue
        if lowered.startswith(("standard output", "error output")):
            continue
        if " : " in stripped:
            return stripped
    # Fallback to the first non-empty line.
    for raw in block_lines:
        stripped = raw.strip()
        if stripped:
            return stripped
    return ""


def extract_source_location(block_lines: Iterable[str]) -> Tuple[Optional[str], Optional[int]]:
    """Parse the first stack-frame that references a source file."""
    for raw in block_lines:
        stripped = raw.strip()
        if not stripped.startswith("at "):
            continue
        match = SOURCE_RE.search(stripped)
        if match:
            file_path = match.group("file")
            try:
                line_number = int(match.group("line"))
            except (TypeError, ValueError):
                line_number = None
            return file_path, line_number
    return None, None


def derive_structure(full_name: str, suite_segments: Sequence[str]) -> Tuple[str, str, str, str]:
    """
    Split the fully-qualified test name into class/method metadata and the logical
    category path (e.g., SemanticSearch.VectorSearch.Unit).
    """
    parts = full_name.split(".")
    if len(parts) < 2:
        return "", full_name, "", ""

    method = parts[-1]
    test_class = parts[-2] if len(parts) >= 2 else ""

    start_index = 0
    for idx in range(len(parts)):
        window = parts[idx : idx + len(suite_segments)]
        if window == list(suite_segments):
            start_index = idx + len(suite_segments)
            break

    category_parts = parts[start_index:-2] if start_index <= len(parts) - 2 else []
    category = ".".join(category_parts) if category_parts else "Root"
    area = category_parts[0] if category_parts else "Root"
    return category, area, test_class, method


def parse_failures(content: str, suite_key: str, suite_segments: Sequence[str]) -> List[FailureRecord]:
    """Transform raw log content into structured failure records."""
    sanitized = strip_ansi_sequences(content)
    normalized = normalize_spaced_text(sanitized)
    lines = normalized.splitlines()
    index = 0
    failures: List[FailureRecord] = []

    while index < len(lines):
        line = lines[index]
        header = HEADER_RE.match(line)
        if not header:
            index += 1
            continue

        full_name = header.group("name").strip()
        duration = header.group("duration").strip()
        index, block_lines = segment_blocks(lines, index + 1)
        message = extract_primary_message(block_lines)
        source_file, source_line = extract_source_location(block_lines)
        details = "\n".join(block_lines).strip()
        failure_kind = classify_failure_kind(message, details)
        category, area, test_class, method = derive_structure(full_name, suite_segments)

        failures.append(
            FailureRecord(
                suite_key=suite_key,
                full_name=full_name,
                duration=duration,
                message=message,
                details=details,
                source_file=source_file,
                source_line=source_line,
                test_class=test_class,
                test_method=method,
                category=category,
                area=area,
                failure_kind=failure_kind,
            )
        )

    return failures


def summarise_failures(failures: Sequence[FailureRecord]) -> Dict[str, object]:
    """Build summary statistics suitable for reporting or JSON export."""
    by_area: Dict[str, List[FailureRecord]] = defaultdict(list)
    by_category: Dict[str, List[FailureRecord]] = defaultdict(list)
    error_kind_counter: Counter = Counter()

    for record in failures:
        by_area[record.area].append(record)
        by_category[record.category].append(record)
        error_kind_counter[record.failure_kind] += 1

    top_area = max(by_area.items(), key=lambda item: len(item[1]))[0] if by_area else None

    top_failure_kind = (
        max(error_kind_counter.items(), key=lambda item: item[1])[0] if error_kind_counter else None
    )

    return {
        "total_failures": len(failures),
        "areas": {area: len(items) for area, items in sorted(by_area.items())},
        "categories": {category: len(items) for category, items in sorted(by_category.items())},
        "top_area": top_area,
        "failure_kinds": dict(error_kind_counter),
        "top_failure_kind": top_failure_kind,
    }


def load_log_content(path: Path) -> str:
    try:
        raw = path.read_bytes()
    except FileNotFoundError as exc:
        raise FileNotFoundError(f"Log file not found: {path}") from exc

    if raw.startswith(codecs.BOM_UTF16_LE):
        return raw.decode("utf-16-le", errors="ignore")
    if raw.startswith(codecs.BOM_UTF16_BE):
        return raw.decode("utf-16-be", errors="ignore")
    if raw.startswith(codecs.BOM_UTF8):
        return raw.decode("utf-8", errors="ignore")
    return raw.decode("utf-8", errors="ignore")


def resolve_suite_log(
    *, display_name: str, suite_key: str, project_dir: Path, override_path: Optional[str]
) -> Tuple[str, Path]:
    if override_path:
        return suite_key, Path(override_path)

    discovered = discover_latest_log(project_dir)
    if not discovered:
        raise FileNotFoundError(
            f"Unable to locate a TestResults log for {display_name}. "
            f"Expected to find one under {project_dir / 'bin' / '<Config>' / 'net*' / 'TestResults'}."
        )
    return suite_key, discovered


def main() -> None:
    repo_root = Path(__file__).resolve().parents[1]
    tests_root = repo_root / "code" / "src" / "tests"

    parser = argparse.ArgumentParser(description="Extract and summarise failing tests for ExxerAI suites.")
    parser.add_argument("--integration-log", help="Explicit path to the integration TestResults log.")
    parser.add_argument("--infrastructure-log", help="Explicit path to the infrastructure TestResults log.")
    parser.add_argument("--export-json", help="Optional path to persist structured failure data as JSON.")
    args = parser.parse_args()

    suites = [
        {
            "display": "Integration Tests",
            "key": "integration",
            "segments": ["IntegrationTests"],
            "project_dir": tests_root / "ExxerAI.IntegrationTests",
            "override": args.integration_log,
        },
        {
            "display": "Infrastructure Tests",
            "key": "infrastructure",
            "segments": ["Infrastructure", "Tests"],
            "project_dir": tests_root / "ExxerAI.Infrastructure.Tests",
            "override": args.infrastructure_log,
        },
    ]

    all_results: Dict[str, Dict[str, object]] = {}

    for suite in suites:
        display = suite["display"]
        key, log_path = resolve_suite_log(
            display_name=display,
            suite_key=suite["key"],
            project_dir=suite["project_dir"],
            override_path=suite["override"],
        )

        print(f"[*] Analyzing {display}")
        print("=" * 80)
        print(f"    Log file: {log_path}")

        content = load_log_content(log_path)
        failures = parse_failures(content, key, suite["segments"])

        if not failures:
            print(f"[!] No failures discovered in {display}.")
            print()
            all_results[key] = {"log_path": str(log_path), "failures": [], "summary": summarise_failures(failures)}
            continue

        summary = summarise_failures(failures)
        all_results[key] = {
            "log_path": str(log_path),
            "failures": [record.__dict__ for record in failures],
            "summary": summary,
        }

        print(f"[+] Total failures: {summary['total_failures']}")
        print(f"[+] Areas ({len(summary['areas'])}):")
        for area, count in summary["areas"].items():
            marker = " (most impacted)" if summary["top_area"] == area else ""
            print(f"    - {area}: {count}{marker}")

        print(f"[+] Failure kinds:")
        failure_kinds = summary["failure_kinds"]
        if failure_kinds:
            for failure_kind, count in failure_kinds.items():
                marker = " (dominant)" if summary["top_failure_kind"] == failure_kind else ""
                print(f"    - {failure_kind}: {count}{marker}")
        else:
            print("    - None detected")

        print(f"[+] Sample failures:")
        for record in failures[:5]:
            location = f"{record.source_file}:{record.source_line}" if record.source_file else "n/a"
            print(f"    - {record.test_class}.{record.test_method} [{location}] ({record.failure_kind})")
            print(f"      {record.message}")

        print()

    if args.export_json:
        export_path = Path(args.export_json)
        export_path.parent.mkdir(parents=True, exist_ok=True)
        export_path.write_text(json.dumps(all_results, indent=2), encoding="utf-8")
        print(f"[*] Structured data exported to {export_path}")


if __name__ == "__main__":
    main()
