#!/usr/bin/env python3
"""
Analyze unresolved type errors (CS0246/CS0103) and correlate them with project dependencies.

For each error entry this script gathers:
  * existing PackageReference and ProjectReference items from the owning .csproj file
  * namespaces already imported via GlobalUsings.cs and the local file
  * lookup information from the ExxerAI type catalog (scripts/exxerai_types.json)
  * heuristic package suggestions for well-known external frameworks (xUnit, Shouldly, etc.)

The resulting report is written to JSON so that missing references can be reviewed
and added safely.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import xml.etree.ElementTree as ET
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Set


# --------------------------------------------------------------------------------------
# Data structures
# --------------------------------------------------------------------------------------

@dataclass
class ProjectContext:
    """Holds metadata for a single project."""

    name: str
    csproj_path: Path
    package_refs: List[str]
    project_refs: List[str]
    global_usings: Set[str]


@dataclass
class ErrorEntry:
    """Represents a parsed line from the error log."""

    code: str
    project: str
    file_path: Path
    line: Optional[int]
    missing_type: str


# --------------------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------------------

KNOWN_FRAMEWORK_TYPES: Dict[str, Dict[str, str]] = {
    # xUnit
    "Fact": {"namespace": "Xunit", "package": "xunit.v3"},
    "FactAttribute": {"namespace": "Xunit", "package": "xunit.v3"},
    "Theory": {"namespace": "Xunit", "package": "xunit.v3"},
    "TheoryAttribute": {"namespace": "Xunit", "package": "xunit.v3"},
    "TheoryData": {"namespace": "Xunit", "package": "xunit.v3"},
    "InlineData": {"namespace": "Xunit", "package": "xunit.v3"},
    "InlineDataAttribute": {"namespace": "Xunit", "package": "xunit.v3"},
    "MemberData": {"namespace": "Xunit", "package": "xunit.v3"},
    "MemberDataAttribute": {"namespace": "Xunit", "package": "xunit.v3"},
    "ITestOutputHelper": {"namespace": "Xunit.Abstractions", "package": "xunit.v3"},
    "Xunit": {"namespace": "Xunit", "package": "xunit.v3"},
    # Shouldly / NSubstitute / Meziantou logging
    "Shouldly": {"namespace": "Shouldly", "package": "Shouldly"},
    "NSubstitute": {"namespace": "NSubstitute", "package": "NSubstitute"},
    "Meziantou": {
        "namespace": "Meziantou.Extensions.Logging.Xunit.v3",
        "package": "Meziantou.Extensions.Logging.Xunit.v3",
    },
    # ASP.NET testing helpers
    "WebApplicationFactory": {
        "namespace": "Microsoft.AspNetCore.Mvc.Testing",
        "package": "Microsoft.AspNetCore.Mvc.Testing",
    },
    "TestServer": {
        "namespace": "Microsoft.AspNetCore.TestHost",
        "package": "Microsoft.AspNetCore.TestHost",
    },
    "HttpStatusCode": {
        "namespace": "System.Net",
        "package": "System.Net.Http",
    },
    # Testcontainers & Qdrant helpers
    "Neo4jContainer": {
        "namespace": "Testcontainers.Neoj4",
        "package": "Testcontainers.Neo4j",
    },
    "QdrantContainer": {
        "namespace": "Testcontainers.Qdrant",
        "package": "Testcontainers.Qdrant",
    },
    "QdrantClient": {
        "namespace": "Qdrant.Client",
        "package": "Qdrant.Client",
    },
}


CS_ERRORS_OF_INTEREST = {"CS0246", "CS0103"}


def find_all_csproj(base_path: Path) -> Dict[str, Path]:
    """Create a map of project name -> csproj path."""
    mapping: Dict[str, Path] = {}
    for csproj in base_path.rglob("*.csproj"):
        name = csproj.stem
        # Prefer first occurrence; if duplicates exist we warn but keep the first.
        mapping.setdefault(name, csproj)
    return mapping


def read_directory_packages(directory_packages_path: Path) -> Set[str]:
    """Return all package names declared in Directory.Packages.props."""
    if not directory_packages_path.exists():
        return set()
    root = ET.parse(directory_packages_path).getroot()
    packages: Set[str] = set()
    for elem in root.iter():
        if elem.tag.endswith("PackageVersion"):
            include = elem.attrib.get("Include")
            if include:
                packages.add(include)
    return packages


def parse_csproj(csproj_path: Path) -> ProjectContext:
    """Extract package/project references and global usings."""
    root = ET.parse(csproj_path).getroot()

    def iter_tag(tag: str) -> Iterable[ET.Element]:
        return (elem for elem in root.iter() if elem.tag.endswith(tag))

    package_refs = [
        elem.attrib["Include"]
        for elem in iter_tag("PackageReference")
        if "Include" in elem.attrib
    ]
    project_refs = [
        elem.attrib["Include"]
        for elem in iter_tag("ProjectReference")
        if "Include" in elem.attrib
    ]

    project_dir = csproj_path.parent
    global_using_candidates = [
        project_dir / "GlobalUsings.cs",
        project_dir / "globalusings.cs",
    ]
    global_usings: Set[str] = set()
    for candidate in global_using_candidates:
        if candidate.exists():
            for line in candidate.read_text(encoding="utf-8").splitlines():
                line = line.strip()
                if line.startswith("global using"):
                    namespace = (
                        line.replace("global using", "")
                        .replace("static", "")
                        .replace(";", "")
                        .strip()
                    )
                    if namespace:
                        global_usings.add(namespace)
            break  # Prefer the first match

    return ProjectContext(
        name=csproj_path.stem,
        csproj_path=csproj_path,
        package_refs=package_refs,
        project_refs=project_refs,
        global_usings=global_usings,
    )


def read_local_usings(file_path: Path) -> List[str]:
    """Return using directives defined in the specified file."""
    if not file_path.exists():
        return []
    usings: List[str] = []
    for line in file_path.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if stripped.startswith("using "):
            directive = (
                stripped.replace("using", "")
                .replace("static", "")
                .replace(";", "")
                .strip()
            )
            if directive:
                usings.append(directive)
    return usings


def sanitize_type_name(type_name: str) -> str:
    """Remove generic parameters and trailing punctuation from a type name."""
    clean = type_name.strip()
    clean = clean.replace("?", "")
    clean = re.sub(r"<.*?>", "", clean)
    clean = clean.rstrip("[]")
    clean = clean.strip("'\"`")
    return clean


def parse_error_file(error_file: Path) -> List[ErrorEntry]:
    """Parse errors from the given TSV file."""
    entries: List[ErrorEntry] = []
    with error_file.open("r", encoding="utf-8") as fh:
        reader = csv.DictReader(fh, delimiter="\t")
        for row in reader:
            code = row.get("Code")
            if code not in CS_ERRORS_OF_INTEREST:
                continue
            project = row.get("Project")
            missing_desc = row.get("Description") or ""
            missing_match = re.search(
                r"name '([^']+)' could not be found", missing_desc
            )
            missing_type = missing_match.group(1) if missing_match else missing_desc
            file_path = Path(row.get("File", ""))
            line_value = row.get("Line")
            try:
                line = int(line_value) if line_value else None
            except ValueError:
                line = None
            entries.append(
                ErrorEntry(
                    code=code,
                    project=project or "",
                    file_path=file_path,
                    line=line,
                    missing_type=sanitize_type_name(missing_type),
                )
            )
    return entries


def load_type_catalog(types_path: Path) -> Dict[str, Dict[str, str]]:
    """Load the ExxerAI type catalog (lowercase keys for case-insensitive lookup)."""
    if not types_path.exists():
        return {}
    data = json.loads(types_path.read_text(encoding="utf-8"))
    lookup: Dict[str, Dict[str, str]] = {}
    for type_name, info in data.get("type_lookup", {}).items():
        lookup[type_name.lower()] = info
    return lookup


def suggest_from_known_frameworks(type_name: str) -> Optional[Dict[str, str]]:
    """Return mapping details for well-known external types."""
    clean = sanitize_type_name(type_name)
    return KNOWN_FRAMEWORK_TYPES.get(clean)


def find_namespace_candidates(
    type_info: Optional[Dict[str, str]],
    project_context: ProjectContext,
    local_usings: List[str],
) -> Set[str]:
    """Collect namespace suggestions for the missing type."""
    namespaces: Set[str] = set()
    if type_info and type_info.get("namespace"):
        namespaces.add(type_info["namespace"])
    namespaces.update(project_context.global_usings)
    namespaces.update(local_usings)
    return namespaces


# --------------------------------------------------------------------------------------
# Main execution
# --------------------------------------------------------------------------------------

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Analyze unresolved types and correlate with project dependencies."
    )
    parser.add_argument(
        "--base-path",
        default=Path(__file__).resolve().parents[1],
        type=Path,
        help="Repository root (defaults to two levels up from this script).",
    )
    parser.add_argument(
        "--error-file",
        required=True,
        type=Path,
        help="Path to the tab-delimited error report (CS0246/CS0103).",
    )
    parser.add_argument(
        "--types-json",
        default=Path(__file__).with_name("exxerai_types.json"),
        type=Path,
        help="Path to the ExxerAI type catalog JSON.",
    )
    parser.add_argument(
        "--output",
        default=Path(__file__).with_name("foreign_type_analysis.json"),
        type=Path,
        help="Output JSON report.",
    )
    args = parser.parse_args()

    base_path = args.base_path.resolve()
    error_file = args.error_file.resolve()
    types_path = args.types_json.resolve()
    output_path = args.output.resolve()

    project_map = find_all_csproj(base_path / "code")
    directory_packages = read_directory_packages(base_path / "code" / "src" / "Directory.Packages.props")
    type_catalog = load_type_catalog(types_path)

    errors = parse_error_file(error_file)

    project_context_cache: Dict[str, ProjectContext] = {}

    report: Dict[str, List[Dict[str, object]]] = defaultdict(list)

    for error in errors:
        project_name = error.project
        csproj_path = project_map.get(project_name)
        if csproj_path is None:
            report[project_name].append(
                {
                    "error": error.code,
                    "missing_type": error.missing_type,
                    "file": str(error.file_path),
                    "line": error.line,
                    "note": "Project .csproj not found in repository.",
                }
            )
            continue

        if project_name not in project_context_cache:
            project_context_cache[project_name] = parse_csproj(csproj_path)
        context = project_context_cache[project_name]

        local_usings = read_local_usings(error.file_path)
        type_info = type_catalog.get(error.missing_type.lower())
        framework_info = suggest_from_known_frameworks(error.missing_type)
        namespaces = sorted(
            find_namespace_candidates(type_info, context, local_usings)
        )

        potential_packages: Set[str] = set()
        if type_info and type_info.get("project") and type_info["project"] != "Unknown":
            potential_packages.add(f"Project: {type_info['project']}")
        if framework_info:
            potential_packages.add(f"Package: {framework_info['package']}")
        # Guess packages with direct name match
        for package in directory_packages:
            if error.missing_type.lower() in package.lower():
                potential_packages.add(f"Package: {package}")

        report[project_name].append(
            {
                "error": error.code,
                "missing_type": error.missing_type,
                "file": str(error.file_path),
                "line": error.line,
                "existing_packages": context.package_refs,
                "existing_project_refs": context.project_refs,
                "global_usings": sorted(context.global_usings),
                "local_usings": local_usings,
                "type_catalog": type_info,
                "framework_hint": framework_info,
                "namespace_candidates": namespaces,
                "suggested_dependencies": sorted(potential_packages),
            }
        )

    output_path.write_text(
        json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    print(f"Foreign type analysis written to: {output_path}")


if __name__ == "__main__":
    main()
