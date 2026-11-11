#!/usr/bin/env python3
"""Identify test folders that only contain bin/obj outputs and optionally quarantine them.

The script defaults to a dry run. Pass --apply to move matching folders into a
rot-candidates directory for later deletion.
"""

from __future__ import annotations

import argparse
import json
import pathlib
import shutil
import sys
from datetime import datetime
from typing import Iterable, List, Tuple

SCRIPT_PATH = pathlib.Path(__file__).resolve()
REPO_ROOT = SCRIPT_PATH.parents[1]
DEFAULT_TESTS_ROOT = REPO_ROOT / "code" / "src" / "tests"
if not DEFAULT_TESTS_ROOT.exists():
    DEFAULT_TESTS_ROOT = SCRIPT_PATH.parent

ALLOWED_CHILDREN = {"bin", "obj"}
DEFAULT_QUARANTINE = "rot-candidates-to-delete"
GLOBAL_QUARANTINE = REPO_ROOT / "quarantine" / "tests"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Detect folders under the tests root that only contain bin/obj "
            "build artifacts and move them into a quarantine folder."
        )
    )
    parser.add_argument(
        "--root",
        type=pathlib.Path,
        default=DEFAULT_TESTS_ROOT,
        help=f"Root directory to inspect (defaults to {DEFAULT_TESTS_ROOT}).",
    )
    parser.add_argument(
        "--apply",
        action="store_true",
        help="Actually move the folders. Without this flag the script only prints matches.",
    )
    parser.add_argument(
        "--quarantine",
        type=pathlib.Path,
        default=None,
        help="Destination for moved folders (defaults to <root>/rot-candidates-to-delete).",
    )
    parser.add_argument(
        "--recursive",
        action="store_true",
        help="Scan subdirectories recursively instead of only the first level.",
    )
    parser.add_argument(
        "--manifest",
        type=pathlib.Path,
        default=None,
        help="Write a JSON manifest of moves (defaults to <quarantine>/moves-YYYYmmdd-HHMMSS.json).",
    )
    parser.add_argument(
        "--global-quarantine",
        type=pathlib.Path,
        default=GLOBAL_QUARANTINE,
        help="Root-level quarantine directory (defaults to <repo>/quarantine/tests).",
    )
    return parser.parse_args()


def iter_directories(root: pathlib.Path, recursive: bool) -> Iterable[pathlib.Path]:
    if recursive:
        for path in root.rglob("*"):
            if path.is_dir():
                yield path
    else:
        for child in root.iterdir():
            if child.is_dir():
                yield child


def is_bin_obj_only(directory: pathlib.Path) -> bool:
    try:
        children = [child for child in directory.iterdir() if not child.name.startswith(".")]
    except PermissionError:
        print(f"[warn] Permission denied while scanning {directory}. Skipping.")
        return False
    if not children:
        return False
    return all(child.is_dir() and child.name in ALLOWED_CHILDREN for child in children)


def move_directory(source: pathlib.Path, destination_root: pathlib.Path) -> pathlib.Path:
    destination_root.mkdir(parents=True, exist_ok=True)
    destination = destination_root / source.name
    counter = 1
    while destination.exists():
        destination = destination_root / f"{source.name}-{counter}"
        counter += 1
    shutil.move(str(source), str(destination))
    return destination


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    if not root.is_dir():
        print(f"[error] Root {root} does not exist or is not a directory.", file=sys.stderr)
        return 1

    default_quarantine = (root / DEFAULT_QUARANTINE).resolve()
    quarantine = (
        args.quarantine.resolve()
        if args.quarantine
        else default_quarantine
    )
    matches: List[pathlib.Path] = []
    empty_dirs: List[pathlib.Path] = []
    for directory in iter_directories(root, args.recursive):
        if str(directory).startswith(str(quarantine)):
            continue
        try:
            if not any(directory.iterdir()):
                empty_dirs.append(directory)
                continue
        except PermissionError:
            print(f"[warn] Permission denied while enumerating {directory}. Skipping.")
            continue
        if is_bin_obj_only(directory):
            matches.append(directory)

    if not matches and not empty_dirs:
        print("No bin/obj-only or empty directories found.")
        return 0

    if matches:
        print("Detected bin/obj-only directories:")
        for match in matches:
            print(f"  - {match}")
    if empty_dirs:
        print("\nDetected empty directories:")
        for directory in empty_dirs:
            print(f"  - {directory}")

    if not args.apply:
        print("\nDry run only. Re-run with --apply to move these directories into quarantine.")
        return 0

    manifest_entries: List[Tuple[str, str, str]] = []

    def safe_move(path: pathlib.Path, label: str, destination_root: pathlib.Path) -> None:
        try:
            new_location = move_directory(path, destination_root)
        except PermissionError:
            print(f"[warn] Locked or inaccessible: {path} ({label}). Skipped.")
            return
        print(f"Moved {path} -> {new_location}")
        manifest_entries.append((label, str(path), str(new_location)))

    for match in matches:
        safe_move(match, "bin/obj-only", quarantine)
    for directory in empty_dirs:
        safe_move(directory, "empty", args.global_quarantine.resolve())

    if manifest_entries:
        manifest_root = args.manifest or (
            quarantine / f"moves-{datetime.now().strftime('%Y%m%d-%H%M%S')}.json"
        )
        manifest_root.parent.mkdir(parents=True, exist_ok=True)
        data = [
            {"category": label, "source": source, "destination": destination}
            for label, source, destination in manifest_entries
        ]
        try:
            with open(manifest_root, "w", encoding="utf-8") as handle:
                json.dump(data, handle, indent=2)
            print(f"\nWrote move manifest to {manifest_root}")
        except OSError as exc:
            print(f"[warn] Failed to write manifest {manifest_root}: {exc}")

    print("\nCompleted moves.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
