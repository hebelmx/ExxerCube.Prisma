#!/usr/bin/env python3
"""
Aligns physical test folder names with Visual Studio solution folder labels.

- Renames the numbered test directories (03UnitTests -> 02UnitTests, etc.).
- Rewrites `code/src/ExxerAI.sln` project paths so they match the new folders.
- Supports dry-run mode (default) with detailed logging.

Usage examples:
  python scripts/align_test_folders.py --dry-run
  python scripts/align_test_folders.py --apply
"""

from __future__ import annotations

import argparse
import datetime as dt
import os
from pathlib import Path
from typing import List, Tuple

REPO_ROOT = Path(__file__).resolve().parents[1]
CODE_ROOT = REPO_ROOT / "code" / "src"
SLN_PATH = CODE_ROOT / "ExxerAI.sln"
LOG_DIR = REPO_ROOT / "scripts" / "logs"

FOLDER_MAPPINGS: List[Tuple[str, str]] = [
    ("tests/03UnitTests", "tests/02UnitTests"),
    ("tests/04AdapterTests", "tests/03AdapterTests"),
    ("tests/05IntegrationTests", "tests/04IntegrationTests"),
    ("tests/06SystemTests", "tests/05SystemTests"),
    ("tests/07E2ETests", "tests/06E2ETests"),
    ("tests/08UITests", "tests/07UITests"),
    ("tests/09Standalone", "tests/08Standalone"),
    ("tests/10Architecture", "tests/09Architecture"),
    ("tests/10 Tests Infrastructure", "tests/10TestsInfrastructure"),
]


def rel_to_path(rel: str) -> Path:
    parts = rel.split("/")
    return CODE_ROOT.joinpath(*parts)


def format_path(path: Path) -> str:
    return str(path.relative_to(REPO_ROOT))


def rename_directories(dry_run: bool, log: List[str]) -> None:
    for old_rel, new_rel in FOLDER_MAPPINGS:
        old_path = rel_to_path(old_rel)
        new_path = rel_to_path(new_rel)

        if not old_path.exists():
            log.append(f"[SKIP] {format_path(old_path)} does not exist")
            continue

        if new_path.exists():
            log.append(f"[WARN] {format_path(new_path)} already exists; cannot rename {format_path(old_path)}")
            continue

        if dry_run:
            log.append(f"[DRY] Would rename {format_path(old_path)} -> {format_path(new_path)}")
        else:
            new_path.parent.mkdir(parents=True, exist_ok=True)
            try:
                old_path.rename(new_path)
                log.append(f"[DONE] Renamed {format_path(old_path)} -> {format_path(new_path)}")
            except OSError as exc:
                log.append(f"[ERR ] Failed to rename {format_path(old_path)} -> {format_path(new_path)}: {exc}")


def update_solution_file(dry_run: bool, log: List[str]) -> None:
    if not SLN_PATH.exists():
        log.append(f"[ERR ] Solution file missing: {format_path(SLN_PATH)}")
        return

    text = SLN_PATH.read_text(encoding="utf-8")
    original_text = text
    total_replacements = 0

    for old_rel, new_rel in FOLDER_MAPPINGS:
        old_win = old_rel.replace("/", "\\")
        new_win = new_rel.replace("/", "\\")
        old_nix = old_rel
        new_nix = new_rel

        count_win = text.count(old_win)
        count_nix = text.count(old_nix)
        total_replacements += count_win + count_nix

        text = text.replace(old_win, new_win)
        text = text.replace(old_nix, new_nix)

        log.append(
            f"[SLN ] {old_rel} -> {new_rel} (backslash matches: {count_win}, slash matches: {count_nix})"
        )

    if total_replacements == 0:
        log.append("[WARN] No paths in solution file matched the mappings.")
        return

    if dry_run:
        log.append(f"[DRY] Would update solution file ({total_replacements} replacements).")
    else:
        SLN_PATH.write_text(text, encoding="utf-8")
        log.append(f"[DONE] Updated solution file ({total_replacements} replacements).")


def write_log(log: List[str], output_path: Path | None) -> None:
    if output_path is None:
        return
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text("\n".join(log) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description="Align test folder names with solution structure.")
    parser.add_argument(
        "--apply",
        action="store_true",
        help="Execute the renames/updates. Default is dry-run.",
    )
    parser.add_argument(
        "--log-file",
        help="Custom log file path. Defaults to scripts/logs/align_test_folders_<timestamp>.log",
    )
    args = parser.parse_args()
    dry_run = not args.apply

    log: List[str] = []
    mode_label = "DRY-RUN" if dry_run else "APPLY"
    log.append(f"=== align_test_folders ({mode_label}) ===")

    rename_directories(dry_run, log)
    update_solution_file(dry_run, log)

    timestamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%d_%H%M%S")
    default_log_path = LOG_DIR / f"align_test_folders_{timestamp}.log"
    log_path = Path(args.log_file) if args.log_file else default_log_path
    write_log(log, log_path)

    print(f"Log written to: {format_path(log_path)}")
    print("\n".join(log))


if __name__ == "__main__":
    main()
