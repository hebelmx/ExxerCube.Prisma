#!/usr/bin/env python3
"""Normalize test folder names and Visual Studio paths to match solution folders."""

from __future__ import annotations

import argparse
import pathlib
import shutil
import sys
from datetime import datetime
from typing import Iterable, List, Tuple

SCRIPT_PATH = pathlib.Path(__file__).resolve()
REPO_ROOT = SCRIPT_PATH.parents[1]
TESTS_ROOT = REPO_ROOT / "code" / "src" / "tests"
SLN_PATH = REPO_ROOT / "code" / "src" / "ExxerAI.sln"
QUARANTINE_DIR = TESTS_ROOT / "rot-candidates-to-delete"

FOLDER_MAP = [
    ("00Domain", "00DomainTests"),
    ("01Application", "01ApplicationTests"),
    ("08Standalone", "08StandaloneTests"),
    ("09Architecture", "09ArchitectureTests"),
    ("10TestsInfrastructure", "10TestInfrastructure"),
]

EXPECTED_DIRS = {
    "00DomainTests",
    "01ApplicationTests",
    "02UnitTests",
    "03AdapterTests",
    "04IntegrationTests",
    "05SystemTests",
    "06E2ETests",
    "07UITests",
    "08StandaloneTests",
    "09ArchitectureTests",
    "10TestInfrastructure",
    "rot-candidates-to-delete",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Rename test directories and update the solution to match virtual folders."
    )
    parser.add_argument(
        "--apply",
        action="store_true",
        help="Perform the renames and overwrite ExxerAI.sln (with backup). Default is dry-run.",
    )
    parser.add_argument(
        "--practice-sln",
        type=pathlib.Path,
        default=SLN_PATH.with_suffix(".sln.practice"),
        help="Path to write the practice solution file during dry-run.",
    )
    parser.add_argument(
        "--backup-sln",
        type=pathlib.Path,
        default=None,
        help="Backup path when applying changes. Defaults to ExxerAI.sln.bak.<timestamp>.",
    )
    return parser.parse_args()


def is_bin_obj_only(directory: pathlib.Path) -> bool:
    try:
        entries = [child for child in directory.iterdir() if not child.name.startswith(".")]
    except PermissionError:
        print(f"[warn] Permission denied while inspecting {directory}")
        return False
    if not entries:
        return False
    return all(child.is_dir() and child.name in {"bin", "obj"} for child in entries)


def plan_folder_moves() -> List[Tuple[pathlib.Path, pathlib.Path]]:
    moves: List[Tuple[pathlib.Path, pathlib.Path]] = []
    for src_name, dst_name in FOLDER_MAP:
        src = TESTS_ROOT / src_name
        dst = TESTS_ROOT / dst_name
        if not src.exists():
            continue
        if dst.exists():
            print(f"[warn] Destination already exists, skipping rename: {dst}")
            continue
        moves.append((src, dst))
    return moves


def plan_quarantine() -> List[Tuple[pathlib.Path, pathlib.Path]]:
    moves: List[Tuple[pathlib.Path, pathlib.Path]] = []
    for child in TESTS_ROOT.iterdir():
        if not child.is_dir():
            continue
        if child.name in EXPECTED_DIRS:
            continue
        if is_bin_obj_only(child):
            dest = QUARANTINE_DIR / child.name
            counter = 1
            while dest.exists():
                dest = QUARANTINE_DIR / f"{child.name}-{counter}"
                counter += 1
            moves.append((child, dest))
    return moves


def apply_moves(moves: Iterable[Tuple[pathlib.Path, pathlib.Path]]) -> None:
    for src, dst in moves:
        dst.parent.mkdir(parents=True, exist_ok=True)
        print(f"[move] {src} -> {dst}")
        shutil.move(str(src), str(dst))


def update_solution_content(content: str) -> str:
    updated = content
    for src, dst in FOLDER_MAP:
        updated = updated.replace(f"tests\\{src}\\", f"tests\\{dst}\\")
        updated = updated.replace(f"tests/{src}/", f"tests/{dst}/")
    return updated


def write_solution(content: str, path: pathlib.Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def main() -> int:
    args = parse_args()

    if not TESTS_ROOT.exists():
        print(f"[error] tests root does not exist: {TESTS_ROOT}", file=sys.stderr)
        return 1
    if not SLN_PATH.exists():
        print(f"[error] solution file not found: {SLN_PATH}", file=sys.stderr)
        return 1

    rename_moves = plan_folder_moves()
    quarantine_moves = plan_quarantine()

    sln_content = SLN_PATH.read_text(encoding="utf-8")
    new_sln_content = update_solution_content(sln_content)

    if not args.apply:
        print("=== Dry Run ===")
        if rename_moves:
            print("\nPlanned renames:")
            for src, dst in rename_moves:
                print(f"  {src} -> {dst}")
        else:
            print("\nNo renames required.")

        if quarantine_moves:
            print("\nBin/obj-only directories to quarantine:")
            for src, dst in quarantine_moves:
                print(f"  {src} -> {dst}")
        else:
            print("\nNo bin/obj-only directories detected outside the expected tree.")

        if new_sln_content != sln_content:
            practice_path = args.practice_sln.resolve()
            write_solution(new_sln_content, practice_path)
            print(f"\nWrote practice solution with updated paths: {practice_path}")
        else:
            print("\nSolution paths already match desired folders. No practice file written.")
        print("\nRe-run with --apply after verifying the practice file.")
        return 0

    # Apply changes
    if rename_moves:
        apply_moves(rename_moves)
    else:
        print("No directories required renaming.")

    if quarantine_moves:
        apply_moves(quarantine_moves)
    else:
        print("No bin/obj-only directories needed quarantine.")

    backup_path = (
        args.backup_sln.resolve()
        if args.backup_sln
        else SLN_PATH.with_suffix(f".sln.bak.{datetime.now().strftime('%Y%m%d-%H%M%S')}")
    )
    if not backup_path.exists():
        shutil.copy2(SLN_PATH, backup_path)
        print(f"Created solution backup: {backup_path}")
    write_solution(new_sln_content, SLN_PATH)
    print(f"Updated solution paths in {SLN_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
