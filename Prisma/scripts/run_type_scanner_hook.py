#!/usr/bin/env python3
"""
Pre-commit hook wrapper for ExxerAI type scanner.
Uses the pre-commit framework for cross-platform compatibility.

This script is called by pre-commit with the list of staged C# files.
"""

import sys
import subprocess
from pathlib import Path
from datetime import datetime

def main():
    """Run the type scanner in incremental mode for staged C# files."""

    # Get the list of staged C# files from pre-commit
    changed_files = sys.argv[1:]

    if not changed_files:
        print("✅ No C# files changed, skipping type database update")
        return 0

    print(f"🔍 Pre-commit: Processing {len(changed_files)} C# file(s)...")

    # Get repository root
    repo_root = Path(__file__).parent.parent

    # Paths - Use a single canonical file for incremental updates
    latest_db_file = repo_root / "scripts" / "exxerai_types_latest.json"
    backup_dir = repo_root / "scripts" / ".type_db_backups"

    # Check if previous database exists
    previous_db = None
    is_incremental = False

    if latest_db_file.exists():
        # Incremental update - use latest as both input and output
        previous_db = latest_db_file
        output_file = latest_db_file
        is_incremental = True
        print(f"  📂 Incremental mode: updating {latest_db_file.name} in-place")

        # Create backup before overwriting (optional, keep last 3)
        backup_dir.mkdir(exist_ok=True)
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        backup_file = backup_dir / f"exxerai_types_{timestamp}.json"
        import shutil
        shutil.copy2(latest_db_file, backup_file)

        # Cleanup old backups (keep last 3)
        backups = sorted(backup_dir.glob("exxerai_types_*.json"), key=lambda p: p.stat().st_mtime)
        for old_backup in backups[:-3]:
            old_backup.unlink()
    else:
        # Full scan - create initial database
        output_file = latest_db_file
        print("⚠️  No previous database found, performing full scan")

    # Build command
    scanner_script = repo_root / "scripts" / "scan_exxerai_types.py"
    cmd = [
        sys.executable,  # Use the same Python interpreter
        str(scanner_script),
        "--base-path", str(repo_root),
        "--output", str(output_file),
    ]

    if previous_db:
        print(f"  📂 Using previous database: {previous_db.name}")
        cmd.extend([
            "--incremental",
            "--previous-db", str(previous_db),
            "--changed-files"
        ])
        cmd.extend(changed_files)

    # Run scanner
    try:
        print("🔄 Running type scanner (incremental mode)...")
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            cwd=repo_root,
            timeout=120
        )

        # Check for success
        if result.returncode == 0 and "Scan complete" in result.stdout:
            print("✅ Type database updated successfully")

            # Stage the updated JSON file
            subprocess.run(
                ["git", "add", str(output_file)],
                cwd=repo_root,
                capture_output=True
            )

            print("✅ Type database staged for commit")
            print(f"  📊 Database file: {output_file.name}")

            if is_incremental:
                print(f"  💾 Backup saved to: .type_db_backups/{backup_file.name}")

            return 0
        else:
            print("⚠️  Warning: Type scanner failed, continuing with commit")
            print("Last few lines of output:")
            for line in result.stdout.splitlines()[-5:]:
                print(f"  {line}")
            if result.stderr:
                print("Errors:")
                for line in result.stderr.splitlines()[-5:]:
                    print(f"  {line}")
            return 0  # Non-blocking - allow commit to proceed

    except subprocess.TimeoutExpired:
        print("⚠️  Scanner timeout (>120s), continuing with commit")
        return 0  # Non-blocking
    except Exception as e:
        print(f"⚠️  Scanner error: {e}, continuing with commit")
        return 0  # Non-blocking

if __name__ == "__main__":
    sys.exit(main())
