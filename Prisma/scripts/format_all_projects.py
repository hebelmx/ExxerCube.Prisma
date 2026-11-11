#!/usr/bin/env python3
"""
ExxerAI Multi-Pass dotnet format Script
Applies dotnet format to all projects with 5 passes each for complete formatting.

"CLEAN CODE STARTS WITH CLEAN TESTS!"

Usage:
    python format_all_projects.py --dry-run          # Show what would be formatted
    python format_all_projects.py                     # Format all projects (5 passes)
    python format_all_projects.py --passes 3          # Custom number of passes
    python format_all_projects.py --projects "*.Test" # Only test projects
    python format_all_projects.py --delay 2           # Custom delay between passes
"""

import os
import sys
import time
import subprocess
import argparse
from pathlib import Path
from datetime import datetime
from typing import List, Dict

# Configuration
BASE_PATH = r"F:\Dynamic\ExxerAi\ExxerAI"
LOG_FILE = os.path.join(BASE_PATH, "scripts", "logs", f"format_all_{datetime.now().strftime('%Y-%m-%d_%H-%M-%S')}.log")
DEFAULT_PASSES = 5
DEFAULT_DELAY = 1.5  # seconds between passes

class FormatManager:
    def __init__(self, dry_run: bool = False, passes: int = DEFAULT_PASSES, delay: float = DEFAULT_DELAY):
        self.dry_run = dry_run
        self.passes = passes
        self.delay = delay
        self.log_file = LOG_FILE
        self.statistics = {
            "total_projects": 0,
            "formatted_projects": 0,
            "failed_projects": 0,
            "total_passes": 0,
            "total_time": 0
        }
        os.makedirs(os.path.dirname(self.log_file), exist_ok=True)

    def log(self, message: str, level: str = "INFO"):
        """Log message to console and file"""
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        log_entry = f"[{timestamp}] [{level}] {message}\n"
        print(log_entry.strip())
        with open(self.log_file, "a", encoding="utf-8") as f:
            f.write(log_entry)

    def find_all_projects(self, pattern: str = "*.csproj") -> List[str]:
        """Find all .csproj files in the solution"""
        self.log("Discovering all .csproj files...", "INFO")

        projects = []
        code_path = os.path.join(BASE_PATH, "code")

        for root, dirs, files in os.walk(code_path):
            # Skip artifacts, bin, obj directories
            dirs[:] = [d for d in dirs if d not in ['artifacts', 'bin', 'obj', 'node_modules', '.git']]

            for file in files:
                if file.endswith(".csproj"):
                    # Apply pattern filter if specified
                    if pattern != "*.csproj":
                        if not self._matches_pattern(file, pattern):
                            continue

                    project_path = os.path.join(root, file)
                    projects.append(project_path)

        projects.sort()  # Sort for consistent ordering
        self.log(f"Found {len(projects)} projects", "INFO")
        return projects

    def _matches_pattern(self, filename: str, pattern: str) -> bool:
        """Simple pattern matching for project filtering"""
        if pattern == "*.csproj":
            return True

        # Remove *.csproj extension from pattern
        pattern = pattern.replace("*.csproj", "").replace("*", "")

        return pattern.lower() in filename.lower()

    def format_project(self, project_path: str, pass_number: int) -> bool:
        """Run dotnet format on a single project"""
        project_name = os.path.basename(project_path)

        if self.dry_run:
            self.log(f"[DRY-RUN] Would format: {project_name} (Pass {pass_number}/{self.passes})", "INFO")
            return True

        self.log(f"Formatting: {project_name} (Pass {pass_number}/{self.passes})", "INFO")

        try:
            # Run dotnet format with comprehensive options
            result = subprocess.run(
                [
                    "dotnet", "format",
                    project_path,
                    "--verbosity", "quiet",
                    "--verify-no-changes",
                    "--no-restore"
                ],
                cwd=os.path.dirname(project_path),
                capture_output=True,
                text=True,
                timeout=120  # 2 minute timeout per pass
            )

            # dotnet format returns 0 if no changes or changes applied successfully
            # Returns 2 if --verify-no-changes and changes were needed
            if result.returncode in [0, 2]:
                if result.returncode == 2:
                    self.log(f"  Changes applied on pass {pass_number}", "INFO")
                else:
                    self.log(f"  No changes needed on pass {pass_number}", "INFO")
                return True
            else:
                self.log(f"  Format failed: {result.stderr}", "ERROR")
                return False

        except subprocess.TimeoutExpired:
            self.log(f"  Timeout formatting {project_name} (pass {pass_number})", "ERROR")
            return False
        except Exception as e:
            self.log(f"  Error formatting {project_name}: {e}", "ERROR")
            return False

    def format_with_passes(self, project_path: str) -> bool:
        """Format a project with multiple passes"""
        project_name = os.path.basename(project_path)
        self.log("=" * 80, "INFO")
        self.log(f"Processing: {project_name}", "INFO")

        success = True
        for pass_num in range(1, self.passes + 1):
            if not self.format_project(project_path, pass_num):
                success = False
                break

            self.statistics["total_passes"] += 1

            # Delay between passes (except after last pass)
            if pass_num < self.passes and not self.dry_run:
                self.log(f"  Waiting {self.delay}s before next pass...", "INFO")
                time.sleep(self.delay)

        if success:
            self.statistics["formatted_projects"] += 1
            self.log(f"✓ Completed: {project_name}", "INFO")
        else:
            self.statistics["failed_projects"] += 1
            self.log(f"✗ Failed: {project_name}", "ERROR")

        return success

    def run(self, pattern: str = "*.csproj"):
        """Execute formatting on all projects"""
        start_time = time.time()

        self.log("=" * 80, "INFO")
        self.log("ExxerAI Multi-Pass dotnet format", "INFO")
        self.log(f"Mode: {'DRY-RUN' if self.dry_run else 'APPLY'}", "INFO")
        self.log(f"Passes per project: {self.passes}", "INFO")
        self.log(f"Delay between passes: {self.delay}s", "INFO")
        self.log("=" * 80, "INFO")

        # Find all projects
        projects = self.find_all_projects(pattern)
        self.statistics["total_projects"] = len(projects)

        if not projects:
            self.log("No projects found!", "WARNING")
            return

        # Format each project
        for i, project_path in enumerate(projects, 1):
            self.log(f"\n[{i}/{len(projects)}]", "INFO")
            self.format_with_passes(project_path)

            # Small delay between projects
            if i < len(projects) and not self.dry_run:
                time.sleep(0.5)

        # Calculate statistics
        self.statistics["total_time"] = time.time() - start_time

        # Print summary
        self.print_summary()

    def print_summary(self):
        """Print execution summary"""
        self.log("", "INFO")
        self.log("=" * 80, "INFO")
        self.log("FORMATTING SUMMARY", "INFO")
        self.log("=" * 80, "INFO")
        self.log(f"Total projects found: {self.statistics['total_projects']}", "INFO")
        self.log(f"Successfully formatted: {self.statistics['formatted_projects']}", "INFO")
        self.log(f"Failed: {self.statistics['failed_projects']}", "INFO")
        self.log(f"Total passes executed: {self.statistics['total_passes']}", "INFO")
        self.log(f"Total time: {self.statistics['total_time']:.2f} seconds", "INFO")

        if self.dry_run:
            self.log("", "INFO")
            self.log("DRY-RUN COMPLETE: No actual formatting was performed", "INFO")
            self.log("Run without --dry-run to apply formatting", "INFO")
        else:
            self.log("", "INFO")
            self.log("FORMATTING COMPLETE!", "INFO")
            self.log('"CLEAN CODE STARTS WITH CLEAN TESTS!"', "INFO")

        self.log("=" * 80, "INFO")
        self.log(f"Log file: {self.log_file}", "INFO")

def main():
    parser = argparse.ArgumentParser(
        description="ExxerAI Multi-Pass dotnet format Script",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Dry run (see what would be formatted)
  python format_all_projects.py --dry-run

  # Format all projects with 5 passes
  python format_all_projects.py

  # Format only test projects
  python format_all_projects.py --projects "*.Test"

  # Custom passes and delay
  python format_all_projects.py --passes 3 --delay 2

  # Integration test projects only
  python format_all_projects.py --projects "*.Integration.Test"
"""
    )

    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Show what would be formatted without making changes"
    )

    parser.add_argument(
        "--passes",
        type=int,
        default=DEFAULT_PASSES,
        help=f"Number of formatting passes per project (default: {DEFAULT_PASSES})"
    )

    parser.add_argument(
        "--delay",
        type=float,
        default=DEFAULT_DELAY,
        help=f"Delay in seconds between passes (default: {DEFAULT_DELAY})"
    )

    parser.add_argument(
        "--projects",
        type=str,
        default="*.csproj",
        help="Project pattern filter (e.g., '*.Test', '*.Integration.Test')"
    )

    args = parser.parse_args()

    # Validate arguments
    if args.passes < 1:
        print("Error: --passes must be at least 1")
        return 1

    if args.delay < 0:
        print("Error: --delay must be non-negative")
        return 1

    # Create and run formatter
    formatter = FormatManager(
        dry_run=args.dry_run,
        passes=args.passes,
        delay=args.delay
    )

    formatter.run(pattern=args.projects)

    return 0 if formatter.statistics["failed_projects"] == 0 else 1

if __name__ == "__main__":
    sys.exit(main())
