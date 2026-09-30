#!/usr/bin/env python3
"""
Safely detect and deduplicate duplicate PackageReference entries in C# project files.

The script focuses on projects under the evocative codebase layout:
- code/src
- code/src/tests

Features:
- Dry-run mode by default; requires --apply to modify any file.
- Creates timestamped backups before altering a project file.
- Logs every action to both stdout and an on-disk log file.
- Ensures the current repository state is captured in a git commit before changes.

Usage examples:
    python scripts/deduplicate_package_references.py
    python scripts/deduplicate_package_references.py --apply
"""

from __future__ import annotations

import argparse
import datetime as _dt
import logging
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, List, Optional, Sequence, Tuple
import xml.etree.ElementTree as ET


DEFAULT_INCLUDE_DIRS: Sequence[str] = ("code/src", "code/src/tests")
LOG_FILE_NAME = "deduplicate_package_references.log"
BACKUP_ROOT_NAME = "package_dedup_backups"
GIT_COMMIT_MESSAGE = "chore: snapshot before package reference deduplication"


class PackageDeduplicationError(RuntimeError):
    """Represents a recoverable error from the package deduplication workflow."""


@dataclass(frozen=True)
class DuplicatePackageReference:
    """Captures metadata about a duplicate package reference occurrence."""

    include: str
    version: Optional[str]
    file_path: Path
    first_occurrence_line: int
    duplicate_line: int


def parse_arguments(argv: Optional[Sequence[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Detect and remove duplicate PackageReference entries in .csproj files safely."
    )
    parser.add_argument(
        "--base-path",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="Repository root (defaults to two levels above this script).",
    )
    parser.add_argument(
        "--include",
        type=str,
        nargs="*",
        default=list(DEFAULT_INCLUDE_DIRS),
        help="Relative directories (from base-path) to scan for project files.",
    )
    parser.add_argument(
        "--apply",
        action="store_true",
        help="Apply deduplication changes. Without this flag the script only reports duplicates.",
    )
    parser.add_argument(
        "--log-file",
        type=Path,
        default=Path(__file__).with_name(LOG_FILE_NAME),
        help="Path to the log file (defaults to scripts/deduplicate_package_references.log).",
    )
    parser.add_argument(
        "--skip-precommit",
        action="store_true",
        help="Skip the safety git add/commit step (not recommended).",
    )
    return parser.parse_args(argv)


def configure_logging(log_file: Path) -> None:
    log_file.parent.mkdir(parents=True, exist_ok=True)
    formatter = logging.Formatter("%(asctime)s [%(levelname)s] %(message)s")
    handlers: List[logging.Handler] = [
        logging.StreamHandler(sys.stdout),
        logging.FileHandler(log_file, encoding="utf-8"),
    ]

    root_logger = logging.getLogger()
    root_logger.setLevel(logging.INFO)
    for handler in root_logger.handlers[:]:
        root_logger.removeHandler(handler)
    for handler in handlers:
        handler.setFormatter(formatter)
        root_logger.addHandler(handler)


def run_git_command(args: Sequence[str], cwd: Path) -> subprocess.CompletedProcess[str]:
    completed = subprocess.run(
        ["git", *args],
        cwd=str(cwd),
        text=True,
        capture_output=True,
        check=False,
    )
    if completed.returncode != 0:
        raise PackageDeduplicationError(
            f"git {' '.join(args)} failed with exit code {completed.returncode}: {completed.stderr.strip()}"
        )
    return completed


def ensure_precommit(base_path: Path) -> None:
    """Create a safety commit capturing the current working tree state."""
    logging.info("Running pre-commit safety check.")
    status = run_git_command(["status", "--porcelain"], base_path)
    if not status.stdout.strip():
        logging.info("Working tree already clean; no pre-commit snapshot required.")
        return

    logging.info("Staging all current changes before running deduplication.")
    run_git_command(["add", "."], base_path)
    timestamp = _dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    commit_message = f"{GIT_COMMIT_MESSAGE} ({timestamp})"
    logging.info("Creating safety commit: %s", commit_message)
    run_git_command(["commit", "-m", commit_message], base_path)


def discover_project_files(base_path: Path, include_dirs: Sequence[str]) -> List[Path]:
    discovered: List[Path] = []
    for relative in include_dirs:
        candidate = (base_path / relative).resolve()
        if not candidate.exists():
            logging.warning("Skipping missing directory: %s", candidate)
            continue
        discovered.extend(candidate.rglob("*.csproj"))
    return sorted(set(discovered))


def element_signature(element: ET.Element) -> Tuple[str, Tuple[Tuple[str, str], ...], Tuple[Tuple[str, Tuple[Tuple[str, str], ...], str], ...]]:
    include_value = element.attrib.get("Include") or element.attrib.get("Update") or ""
    include_signature = include_value.lower()
    sorted_attributes = tuple(sorted((k, v) for k, v in element.attrib.items() if k not in {"Include", "Update"}))

    children_signature: List[Tuple[str, Tuple[Tuple[str, str], ...], str]] = []
    for child in element:
        child_attributes = tuple(sorted(child.attrib.items()))
        child_text = (child.text or "").strip()
        children_signature.append((child.tag, child_attributes, child_text))

    return include_signature, sorted_attributes, tuple(children_signature)


def extract_version(element: ET.Element) -> Optional[str]:
    version_attr = element.attrib.get("Version")
    if version_attr:
        return version_attr.strip()
    version_child = element.find("Version")
    if version_child is not None and version_child.text:
        return version_child.text.strip()
    return None


def detect_duplicates(project_file: Path) -> List[DuplicatePackageReference]:
    tree = ET.parse(project_file)
    root = tree.getroot()
    namespace_prefix = extract_namespace_prefix(root)
    item_group_tag = f"{namespace_prefix}ItemGroup"
    package_reference_tag = f"{namespace_prefix}PackageReference"

    duplicates: List[DuplicatePackageReference] = []
    seen_signatures: dict[
        Tuple[str, Tuple[Tuple[str, str], ...], Tuple[Tuple[str, Tuple[Tuple[str, str], ...], str], ...]], ET.Element
    ] = {}

    for item_group in root.findall(item_group_tag):
        for package_reference in list(item_group.findall(package_reference_tag)):
            signature = element_signature(package_reference)
            if not signature[0]:
                continue

            if signature in seen_signatures:
                original = seen_signatures[signature]
                duplicate = package_reference
                include_name = package_reference.attrib.get("Include") or package_reference.attrib.get("Update") or "UNKNOWN"
                version = extract_version(package_reference)
                duplicates.append(
                    DuplicatePackageReference(
                        include=include_name,
                        version=version,
                        file_path=project_file,
                        first_occurrence_line=getattr(original, "sourceline", -1) or -1,
                        duplicate_line=getattr(duplicate, "sourceline", -1) or -1,
                    )
                )
                continue

            seen_signatures[signature] = package_reference

    return duplicates


def remove_duplicates(project_file: Path, duplicates: Sequence[DuplicatePackageReference]) -> bool:
    tree = ET.parse(project_file)
    root = tree.getroot()
    namespace_prefix = extract_namespace_prefix(root)
    item_group_tag = f"{namespace_prefix}ItemGroup"
    package_reference_tag = f"{namespace_prefix}PackageReference"
    duplicates_found = False
    seen_signatures: dict[
        Tuple[str, Tuple[Tuple[str, str], ...], Tuple[Tuple[str, Tuple[Tuple[str, str], ...], str], ...]], ET.Element
    ] = {}

    for item_group in root.findall(item_group_tag):
        for package_reference in list(item_group.findall(package_reference_tag)):
            signature = element_signature(package_reference)
            if not signature[0]:
                continue

            if signature in seen_signatures:
                item_group.remove(package_reference)
                duplicates_found = True
                continue

            seen_signatures[signature] = package_reference

    if not duplicates_found:
        return False

    tree.write(project_file, encoding="utf-8", xml_declaration=True)
    return True


def create_backup(project_file: Path, backup_root: Path, repository_root: Path) -> Path:
    relative_path = project_file.resolve().relative_to(repository_root.resolve())
    backup_path = backup_root / relative_path
    backup_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(project_file, backup_path)
    return backup_path


def prepare_backup_root(script_path: Path) -> Path:
    timestamp = _dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    backup_root = script_path.with_name(BACKUP_ROOT_NAME) / timestamp
    backup_root.mkdir(parents=True, exist_ok=True)
    return backup_root


def extract_namespace_prefix(element: ET.Element) -> str:
    if element.tag.startswith("{"):
        namespace = element.tag.split("}", 1)[0][1:]
        return f"{{{namespace}}}"
    return ""


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = parse_arguments(argv)
    base_path = args.base_path.resolve()
    configure_logging(args.log_file.resolve())

    logging.info("Starting PackageReference deduplication (apply=%s).", args.apply)
    logging.info("Base path: %s", base_path)

    if not base_path.exists():
        logging.error("Base path does not exist: %s", base_path)
        return 1

    if not args.skip_precommit and args.apply:
        ensure_precommit(base_path)
    elif args.skip_precommit:
        logging.warning("Pre-commit snapshot skipped at user request.")

    project_files = discover_project_files(base_path, args.include)
    if not project_files:
        logging.warning("No project files found under the specified include directories.")
        return 0

    total_duplicates = 0
    duplicates_by_file: dict[Path, List[DuplicatePackageReference]] = {}
    for project_file in project_files:
        duplicates = detect_duplicates(project_file)
        if not duplicates:
            continue
        duplicates_by_file[project_file] = duplicates
        total_duplicates += len(duplicates)
        for duplicate in duplicates:
            logging.info(
                "Duplicate detected: %s (Version=%s) in %s [first line: %s, duplicate line: %s]",
                duplicate.include,
                duplicate.version or "unspecified",
                duplicate.file_path,
                duplicate.first_occurrence_line,
                duplicate.duplicate_line,
            )

    if total_duplicates == 0:
        logging.info("No duplicate PackageReference entries found.")
        return 0

    logging.info("Identified %s duplicate PackageReference entries across %s project files.", total_duplicates, len(duplicates_by_file))

    if not args.apply:
        logging.info("Dry run complete. Rerun with --apply to remove duplicates.")
        return 0

    backup_root = prepare_backup_root(Path(__file__).resolve())
    logging.info("Backing up modified files to: %s", backup_root)

    modified_files = 0
    for project_file, duplicates in duplicates_by_file.items():
        backup_path = create_backup(project_file, backup_root, base_path)
        logging.info("Created backup for %s at %s", project_file, backup_path)
        if remove_duplicates(project_file, duplicates):
            modified_files += 1
            logging.info("Removed %s duplicate entries from %s", len(duplicates), project_file)
        else:
            logging.info("No changes written to %s (duplicates resolved externally).", project_file)

    logging.info("Deduplication complete. Modified %s project files.", modified_files)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except PackageDeduplicationError as exc:
        logging.error("%s", exc)
        raise SystemExit(1) from exc
    except Exception as unexpected:
        logging.exception("Unexpected failure: %s", unexpected)
        raise SystemExit(1) from unexpected
