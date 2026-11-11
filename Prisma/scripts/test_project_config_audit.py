#!/usr/bin/env python3
"""
Lightweight audit helper for ExxerAI test project files.

This script only inspects files; it never mutates XML. It reports which
`.csproj` files under `code/src/tests` are missing required pieces from the
XUnit v3 Universal Configuration pattern (packages, analyzers, global usings).
"""

from __future__ import annotations

import argparse
import json
import argparse
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, List, Optional
from datetime import datetime

REPO_ROOT = Path(__file__).resolve().parents[1]
TEST_ROOT = REPO_ROOT / "code" / "src" / "tests"


@dataclass
class Finding:
    """Represents a missing configuration element."""

    name: str
    anchor_line: Optional[int]


def locate_line(lines: List[str], needles: Iterable[str]) -> Optional[int]:
    """Return the (1-based) line number containing any of the provided needles."""
    lowercase_needles = [needle.lower() for needle in needles]
    for idx, line in enumerate(lines, start=1):
        lower_line = line.lower()
        for needle in lowercase_needles:
            if needle in lower_line:
                return idx
    return None


def audit_project(path: Path) -> List[Finding]:
    text = path.read_text(encoding="utf-8", errors="ignore")
    lines = text.splitlines()
    text_lower = text.lower()

    xunit_group_line = locate_line(
        lines,
        [
            '<itemgroup label="xunit v3 universal configuration"',
            '<itemgroup label="xunit v3"',
        ],
    )
    runner_line = locate_line(lines, ['<packagereference include="xunit.runner.visualstudio"'])
    mtp_group_line = locate_line(lines, ['<itemgroup label="microsoft testing platform"'])
    global_usings_line = locate_line(lines, ['<itemgroup label="global usings"'])
    time_testing_line = locate_line(lines, ['<itemgroup label="time testing"'])

    findings: List[Finding] = []

    def check(substring: str, name: str, anchor: Optional[int]):
        if substring.lower() not in text_lower:
            findings.append(Finding(name=name, anchor_line=anchor))

    check('<PackageReference Include="xunit.v3.core"', "Add xunit.v3.core reference", xunit_group_line)
    check('<PackageReference Include="xunit.v3.runner.inproc.console"', "Add xunit.v3.runner.inproc.console reference", xunit_group_line)
    check('<PackageReference Include="xunit.v3.runner.msbuild"', "Add xunit.v3.runner.msbuild reference", xunit_group_line)
    if (
        '<packagereference include="xunit.runner.visualstudio" />' in text_lower
        or '<packagereference include="xunit.runner.visualstudio"/>' in text_lower
    ):
        findings.append(Finding(name="Expand xunit.runner.visualstudio node with PrivateAssets/IncludeAssets", anchor_line=runner_line))
    check('<PackageReference Include="Microsoft.Testing.Extensions.VSTestBridge"', "Add Microsoft.Testing.Extensions.VSTestBridge reference", mtp_group_line)
    check('<PackageReference Include="Microsoft.Testing.Extensions.HangDump"', "Add Microsoft.Testing.Extensions.HangDump reference", mtp_group_line)
    check('<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing"', "Add Microsoft.Extensions.TimeProvider.Testing reference", time_testing_line or xunit_group_line)
    if '<using include="system' not in text_lower and '<using include="system.' not in text_lower:
        findings.append(Finding(name="Restore System.* global usings block", anchor_line=global_usings_line))

    return findings


def main() -> None:
    parser = argparse.ArgumentParser(description="Audit test project .csproj files for universal XUnit v3 compliance.")
    parser.add_argument(
        "--json",
        action="store_true",
        help="Emit JSON instead of human-readable text."
    )
    parser.add_argument(
        "--write-files",
        action="store_true",
        help="Write timestamped text + JSON reports to disk."
    )
    parser.add_argument(
        "--output-dir",
        default="scripts/audit_logs",
        help="Directory for timestamped reports (default: scripts/audit_logs)."
    )
    args = parser.parse_args()

    projects = sorted(TEST_ROOT.rglob("*.csproj"))
    payload = []
    text_lines: List[str] = []

    for project in projects:
        findings = audit_project(project)
        if not findings:
            continue
        rel_path = project.relative_to(REPO_ROOT)
        payload.append(
            {
                "path": str(rel_path).replace("\\", "/"),
                "findings": [
                    {
                        "name": finding.name,
                        "line": finding.anchor_line,
                    }
                    for finding in findings
                ],
            }
        )
        text_lines.append(str(rel_path).replace("\\", "/"))
        for finding in findings:
            anchor = f"line {finding.anchor_line}" if finding.anchor_line else "line ?"
            text_lines.append(f"  - {finding.name} ({anchor})")

    if args.json:
        print(json.dumps(payload, indent=2))
    else:
        if text_lines:
            print("\n".join(text_lines))

    if args.write_files:
        timestamp = datetime.utcnow().strftime("%Y%m%d_%H%M%S")
        out_dir = (Path(args.output_dir) if args.output_dir else Path("scripts/audit_logs")).resolve()
        out_dir.mkdir(parents=True, exist_ok=True)
        text_path = out_dir / f"test_project_config_audit_{timestamp}.txt"
        json_path = out_dir / f"test_project_config_audit_{timestamp}.json"
        text_content = "\n".join(text_lines)
        text_path.write_text(text_content + ("\n" if text_content else ""), encoding="utf-8")
        json_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
        # Preserve the "current" snapshot alongside the legacy audit files.
        legacy_text = Path("scripts/test_project_config_audit_output.txt")
        legacy_json = Path("scripts/test_project_config_audit_output.json")
        legacy_text.write_text(text_content + ("\n" if text_content else ""), encoding="utf-8")
        legacy_json.write_text(json.dumps(payload, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
