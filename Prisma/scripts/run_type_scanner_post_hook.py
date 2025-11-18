#!/usr/bin/env python3
"""
Post-commit hook for ExxerAI analyzers.
Runs AFTER a commit completes, refreshes inventories (types, SUT analysis, dependency tree),
updates NuGet package intelligence, and creates a follow-up commit whenever an output changes.
"""

from __future__ import annotations

import json
import subprocess
import sys
import xml.etree.ElementTree as ET
from datetime import datetime
from pathlib import Path
from typing import Iterable, List, Sequence, Set


def run_command(args: Sequence[str], cwd: Path, timeout: int | None = None) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        args,
        capture_output=True,
        text=True,
        cwd=cwd,
        timeout=timeout,
        check=False,
    )


def get_changed_cs_files(repo_root: Path) -> List[str]:
    """Return the C# files touched by the last commit (used for incremental scans)."""
    result = run_command(
        ["git", "diff-tree", "--no-commit-id", "--name-only", "-r", "HEAD"],
        repo_root,
        timeout=10,
    )
    if result.returncode != 0:
        print("⚠️  Unable to gather changed files; continuing with analyzers.")
        return []
    files = [line.strip() for line in result.stdout.splitlines() if line.endswith(".cs")]
    return files


def get_changed_files(repo_root: Path, base_ref: str = "HEAD^", head_ref: str = "HEAD") -> List[str]:
    result = run_command(["git", "diff", "--name-only", base_ref, head_ref], repo_root, timeout=10)
    if result.returncode != 0:
        return []
    return [line.strip() for line in result.stdout.splitlines() if line.strip()]


def read_git_file(repo_root: Path, commit: str, relative_path: Path) -> str | None:
    result = run_command(
        ["git", "show", f"{commit}:{relative_path.as_posix()}"],
        repo_root,
    )
    if result.returncode != 0:
        return None
    return result.stdout


def parse_props_text(content: str) -> dict[str, str]:
    if not content:
        return {}
    root = ET.fromstring(content)
    packages: dict[str, str] = {}
    for item_group in root.findall("ItemGroup"):
        for package in item_group.findall("PackageVersion"):
            pkg_id = package.attrib.get("Include")
            version = package.attrib.get("Version")
            if pkg_id and version:
                packages[pkg_id] = version
    return packages


def parse_lock_text(content: str) -> dict[str, str]:
    if not content:
        return {}
    data = json.loads(content)
    packages: dict[str, str] = {}

    def _walk(deps: dict) -> None:
        for name, info in deps.items():
            if isinstance(info, str):
                packages[name] = info
            else:
                version = info.get("resolved") or info.get("version")
                if version:
                    packages[name] = version
                nested = info.get("dependencies")
                if isinstance(nested, dict):
                    _walk(nested)

    for framework_deps in data.get("dependencies", {}).values():
        if isinstance(framework_deps, dict):
            _walk(framework_deps)
    return packages


def diff_package_versions(old: dict[str, str], new: dict[str, str]) -> Set[str]:
    changed: Set[str] = set()
    for pkg_id, version in new.items():
        if old.get(pkg_id) != version:
            changed.add(pkg_id)
    for pkg_id in old.keys() - new.keys():
        changed.add(pkg_id)
    return changed


def did_file_change(repo_root: Path, path: Path) -> bool:
    result = run_command(["git", "status", "--porcelain", str(path)], repo_root)
    return bool(result.stdout.strip())


def limit_backups(backup_dir: Path, pattern: str, keep: int = 3) -> None:
    backups = sorted(backup_dir.glob(pattern), key=lambda p: p.stat().st_mtime)
    for obsolete in backups[:-keep]:
        obsolete.unlink(missing_ok=True)


def detect_changed_packages(repo_root: Path) -> Set[str]:
    changed_files = set(get_changed_files(repo_root))
    if not changed_files:
        return set()

    changed_packages: Set[str] = set()
    base_commit = "HEAD^"
    props_paths = [repo_root / "code" / "src" / "Directory.Packages.props"]
    lock_paths = list((repo_root / "code" / "src").rglob("packages.lock.json"))

    for props_path in props_paths:
        if not props_path.exists():
            continue
        rel = props_path.relative_to(repo_root).as_posix()
        if rel not in changed_files:
            continue
        new_map = parse_props_text(props_path.read_text(encoding="utf-8"))
        old_content = read_git_file(repo_root, base_commit, props_path.relative_to(repo_root))
        old_map = parse_props_text(old_content) if old_content else {}
        changed_packages |= diff_package_versions(old_map, new_map)

    for lock_path in lock_paths:
        rel = lock_path.relative_to(repo_root).as_posix()
        if rel not in changed_files:
            continue
        new_map = parse_lock_text(lock_path.read_text(encoding="utf-8"))
        old_content = read_git_file(repo_root, base_commit, lock_path.relative_to(repo_root))
        old_map = parse_lock_text(old_content) if old_content else {}
        changed_packages |= diff_package_versions(old_map, new_map)

    return changed_packages


def run_type_scanner(repo_root: Path, changed_cs: List[str]) -> bool:
    if not changed_cs:
        print("✅ No C# files changed; skipping type scanner.")
        return False

    latest_db = repo_root / "scripts" / "exxerai_types_latest.json"
    backup_dir = repo_root / "scripts" / ".type_db_backups"
    backup_dir.mkdir(exist_ok=True)

    previous_db: Path | None = None
    if latest_db.exists():
        previous_db = latest_db
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        import shutil

        backup_file = backup_dir / f"exxerai_types_{timestamp}.json"
        shutil.copy2(latest_db, backup_file)
        limit_backups(backup_dir, "exxerai_types_*.json")
        print(f"  📂 Incremental mode; backup saved to {backup_file.name}")
    else:
        print("⚠️  No previous type DB found; running full scan.")

    cmd = [
        sys.executable,
        str(repo_root / "scripts" / "scan_exxerai_types.py"),
        "--base-path",
        str(repo_root),
        "--output",
        str(latest_db),
    ]
    if previous_db:
        cmd.extend(
            [
                "--incremental",
                "--previous-db",
                str(previous_db),
                "--changed-files",
                *changed_cs,
            ]
        )

    print(f"🔄 Running type scanner across {len(changed_cs)} C# file(s)...")
    result = run_command(cmd, repo_root, timeout=120)
    if result.returncode == 0 and "Scan complete" in result.stdout:
        print("✅ Type scanner completed.")
        return did_file_change(repo_root, latest_db)

    print("⚠️  Type scanner failed:")
    print("\n".join(result.stdout.splitlines()[-5:]))
    if result.stderr:
        print("\n".join(result.stderr.splitlines()[-5:]))
    return False


def run_sut_scanner(repo_root: Path) -> bool:
    output_file = repo_root / "scripts" / "test_sut_analysis_latest.json"
    backup_dir = repo_root / "scripts" / ".sut_analysis_backups"
    backup_dir.mkdir(exist_ok=True)

    if output_file.exists():
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        import shutil

        backup_file = backup_dir / f"test_sut_analysis_{timestamp}.json"
        shutil.copy2(output_file, backup_file)
        limit_backups(backup_dir, "test_sut_analysis_*.json")

    cmd = [
        sys.executable,
        str(repo_root / "scripts" / "scan_test_sut.py"),
        "--base-path",
        str(repo_root),
        "--output",
        str(output_file),
        "--mode",
        "advanced",
    ]
    result = run_command(cmd, repo_root, timeout=180)
    if result.returncode == 0:
        print("✅ SUT analysis updated.")
        return did_file_change(repo_root, output_file)

    print("⚠️  SUT scanner failed:")
    print("\n".join(result.stdout.splitlines()[-5:]))
    if result.stderr:
        print("\n".join(result.stderr.splitlines()[-5:]))
    return False


def run_dependency_builder(repo_root: Path) -> bool:
    output_file = repo_root / "scripts" / "type_dependency_tree.json"
    backup_dir = repo_root / "scripts" / ".dependency_tree_backups"
    backup_dir.mkdir(exist_ok=True)

    if output_file.exists():
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        import shutil

        backup_file = backup_dir / f"type_dependency_tree_{timestamp}.json"
        shutil.copy2(output_file, backup_file)
        limit_backups(backup_dir, "type_dependency_tree_*.json")

    cmd = [
        sys.executable,
        str(repo_root / "scripts" / "build_dependency_tree.py"),
        "--base-path",
        str(repo_root),
        "--output",
        str(output_file.relative_to(repo_root)),
    ]
    result = run_command(cmd, repo_root, timeout=180)
    if result.returncode == 0:
        print("✅ Dependency tree rebuilt.")
        return did_file_change(repo_root, output_file)

    print("⚠️  Dependency builder failed:")
    print("\n".join(result.stdout.splitlines()[-5:]))
    if result.stderr:
        print("\n".join(result.stderr.splitlines()[-5:]))
    return False


def run_package_crawler_v2(repo_root: Path, packages: Set[str]) -> None:
    if not packages:
        return

    output_dir = repo_root / "artifacts" / "externals_v2"
    cache_dir = repo_root / ".nupkg-cache"
    lock_paths = list((repo_root / "code" / "src").rglob("packages.lock.json"))
    props_path = repo_root / "code" / "src" / "Directory.Packages.props"

    cmd = [
        sys.executable,
        "-m",
        "scripts.package_crawler_v2.collector",
        "--output",
        str(output_dir),
        "--cache",
        str(cache_dir),
    ]
    for lock_path in lock_paths:
        cmd.extend(["--packages-lock", str(lock_path)])
    if props_path.exists():
        cmd.extend(["--directory-props", str(props_path)])
    for package in sorted(packages):
        cmd.extend(["--package", package])

    print(f"🔄 Updating NuGet metadata for {len(packages)} package(s)...")
    result = run_command(cmd, repo_root, timeout=600)
    if result.returncode == 0:
        print("✅ NuGet package artifacts updated (v2).")
    else:
        print("⚠️  NuGet crawler v2 failed:")
        print("\n".join(result.stdout.splitlines()[-5:]))
        if result.stderr:
            print("\n".join(result.stderr.splitlines()[-5:]))


def stage_and_commit(repo_root: Path, files: Iterable[Path]) -> None:
    staged = list(files)
    for file_path in staged:
        run_command(["git", "add", str(file_path)], repo_root)

    if not staged:
        return

    commit_msg = (
        "chore: Update analyzer inventories (post-commit)\n\n"
        "Auto-generated by post-commit scanners"
    )
    run_command(["git", "commit", "--no-verify", "-m", commit_msg], repo_root)
    print("✅ Analyzer outputs committed.")


def main() -> int:
    print("🔍 Post-commit: running analyzers...")
    repo_root = Path(__file__).parent.parent

    changed_cs = get_changed_cs_files(repo_root)
    changed_packages = detect_changed_packages(repo_root)
    if not changed_cs:
        print("✅ No C# changes detected; skipping analyzers.")
        if changed_packages:
            run_package_crawler_v2(repo_root, changed_packages)
        return 0

    updated_files: List[Path] = []

    if run_type_scanner(repo_root, changed_cs):
        updated_files.append(repo_root / "scripts" / "exxerai_types_latest.json")

    if run_sut_scanner(repo_root):
        updated_files.append(repo_root / "scripts" / "test_sut_analysis_latest.json")

    if run_dependency_builder(repo_root):
        updated_files.append(repo_root / "scripts" / "type_dependency_tree.json")

    if changed_packages:
        run_package_crawler_v2(repo_root, changed_packages)

    if updated_files:
        print(f"📝 {len(updated_files)} analyzer output(s) changed; creating follow-up commit.")
        stage_and_commit(repo_root, updated_files)
    else:
        print("ℹ️  Analyzer outputs unchanged.")

    return 0


if __name__ == "__main__":
    sys.exit(main())
