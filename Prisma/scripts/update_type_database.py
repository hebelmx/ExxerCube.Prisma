#!/usr/bin/env python3
"""
Type Database Updater - Scheduled task to keep type database current
Run this script daily via cron/task scheduler to maintain fresh type metadata.

Features:
- Updates type database with latest codebase scan
- Maintains history (keeps last 10 scans)
- Reports statistics on types added/removed/modified
- Can be run manually or automated

Usage:
    python scripts/update_type_database.py
    python scripts/update_type_database.py --keep-history 5
    python scripts/update_type_database.py --verbose

Scheduling:
    # Linux/Mac (crontab -e)
    0 2 * * * cd /path/to/ExxerAI && python scripts/update_type_database.py

    # Windows (Task Scheduler)
    Program: python
    Arguments: F:\Dynamic\ExxerAi\ExxerAI\scripts\update_type_database.py
    Start in: F:\Dynamic\ExxerAi\ExxerAI

Author: Claude Code Agent
Date: 2025-11-08
"""

import subprocess
import json
from pathlib import Path
from datetime import datetime
import argparse
import sys


class TypeDatabaseUpdater:
    """Manages automated updates of the type database."""

    def __init__(self, base_path: Path, keep_history: int = 10):
        self.base_path = Path(base_path)
        self.scripts_dir = self.base_path / "scripts"
        self.keep_history = keep_history

    def find_existing_databases(self):
        """Find all existing type database files."""
        return sorted(
            self.scripts_dir.glob("exxerai_types_*.json"),
            key=lambda p: p.stat().st_mtime,
            reverse=True  # Most recent first
        )

    def load_type_counts(self, json_file: Path) -> dict:
        """Load and count types from a JSON file."""
        try:
            with open(json_file, 'r', encoding='utf-8') as f:
                data = json.load(f)

            stats = data.get('statistics', {})
            return {
                'total_types': stats.get('total_types', 0),
                'total_projects': stats.get('total_projects', 0),
                'total_files': stats.get('total_files_scanned', 0),
                'scan_date': data.get('scan_date', 'unknown')
            }
        except Exception as e:
            print(f"⚠️  Warning: Could not read {json_file.name}: {e}")
            return {}

    def run_scanner(self) -> Path:
        """Run the type scanner and return path to new JSON file."""
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_file = self.scripts_dir / f"exxerai_types_{timestamp}.json"

        print(f"🔄 Running type scanner...")
        print(f"   Base path: {self.base_path}")
        print(f"   Output: {output_file.name}")

        try:
            result = subprocess.run(
                [
                    sys.executable,
                    str(self.scripts_dir / "scan_exxerai_types.py"),
                    "--base-path", str(self.base_path),
                    "--output", str(output_file)
                ],
                capture_output=True,
                text=True,
                check=True
            )

            print("✅ Type scanner completed successfully")
            return output_file

        except subprocess.CalledProcessError as e:
            print(f"❌ Error running type scanner:")
            print(e.stderr)
            raise

    def create_latest_symlink(self, latest_file: Path):
        """Create/update symlink to latest type database."""
        symlink = self.scripts_dir / "exxerai_types_latest.json"

        # Remove existing symlink if it exists
        if symlink.exists() or symlink.is_symlink():
            symlink.unlink()

        # Create new symlink (relative path)
        try:
            symlink.symlink_to(latest_file.name)
            print(f"✅ Updated symlink: {symlink.name} -> {latest_file.name}")
        except OSError:
            # Fallback for Windows without symlink permissions
            print(f"⚠️  Could not create symlink, copying file instead")
            import shutil
            shutil.copy2(latest_file, symlink)

    def cleanup_old_databases(self):
        """Remove old type database files, keeping only recent history."""
        databases = self.find_existing_databases()

        if len(databases) <= self.keep_history:
            print(f"✅ History count: {len(databases)} (limit: {self.keep_history})")
            return

        to_remove = databases[self.keep_history:]
        print(f"🗑️  Cleaning up {len(to_remove)} old database(s)...")

        for db_file in to_remove:
            try:
                db_file.unlink()
                print(f"   Removed: {db_file.name}")
            except Exception as e:
                print(f"   ⚠️  Could not remove {db_file.name}: {e}")

    def compare_databases(self, old_file: Path, new_file: Path):
        """Compare two database files and report differences."""
        print("\n📊 Database Comparison:")

        old_stats = self.load_type_counts(old_file)
        new_stats = self.load_type_counts(new_file)

        if not old_stats or not new_stats:
            print("   ⚠️  Could not load statistics")
            return

        # Calculate differences
        type_diff = new_stats['total_types'] - old_stats['total_types']
        project_diff = new_stats['total_projects'] - old_stats['total_projects']
        file_diff = new_stats['total_files'] - old_stats['total_files']

        # Display changes
        print(f"   Previous scan: {old_stats['scan_date']}")
        print(f"   Current scan:  {new_stats['scan_date']}")
        print()
        print(f"   Types:    {old_stats['total_types']:,} → {new_stats['total_types']:,} ({type_diff:+,})")
        print(f"   Projects: {old_stats['total_projects']} → {new_stats['total_projects']} ({project_diff:+})")
        print(f"   Files:    {old_stats['total_files']:,} → {new_stats['total_files']:,} ({file_diff:+,})")

    def update(self, verbose: bool = False):
        """Perform full update cycle."""
        print("=" * 60)
        print("🔄 Type Database Update")
        print("=" * 60)
        print()

        # Find previous database
        existing_databases = self.find_existing_databases()
        previous_db = existing_databases[0] if existing_databases else None

        if previous_db and verbose:
            print(f"📂 Previous database: {previous_db.name}")
            print()

        # Run scanner
        new_db = self.run_scanner()

        # Compare with previous
        if previous_db:
            self.compare_databases(previous_db, new_db)
            print()

        # Create symlink
        self.create_latest_symlink(new_db)

        # Cleanup old files
        self.cleanup_old_databases()

        print()
        print("=" * 60)
        print("✅ Type Database Update Complete")
        print("=" * 60)


def main():
    parser = argparse.ArgumentParser(
        description='Update ExxerAI type database',
        formatter_class=argparse.RawDescriptionHelpFormatter
    )

    parser.add_argument('--base-path', default='.',
                       help='Base path of ExxerAI project (default: current directory)')
    parser.add_argument('--keep-history', type=int, default=10,
                       help='Number of historical databases to keep (default: 10)')
    parser.add_argument('--verbose', '-v', action='store_true',
                       help='Verbose output')

    args = parser.parse_args()

    try:
        updater = TypeDatabaseUpdater(args.base_path, args.keep_history)
        updater.update(args.verbose)
        sys.exit(0)

    except Exception as e:
        print(f"\n❌ Error: {e}")
        if args.verbose:
            import traceback
            traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
