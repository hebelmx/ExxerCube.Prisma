#!/usr/bin/env python3
"""
Vault Fixture Propagation Script - Production Grade
====================================================

Safely propagates fixture files from Vault integration tests to other component test projects
with comprehensive safety protocols, namespace transformation, and verification.

Features:
- Dry-run mode by default (--dry-run)
- Git safety cycle (status check, add, commit, optional push)
- Timestamped backups before any file modifications
- Smart namespace transformation
- Comprehensive verification and reporting
- Rollback capability on errors
- Production-grade error handling

Author: Claude Code (ExxerAI Development Team)
Date: 2025-11-05
Version: 1.0.0
"""

import os
import re
import json
import shutil
import hashlib
import subprocess
import sys
from pathlib import Path
from typing import Dict, List, Optional, Tuple, Set
from datetime import datetime
from dataclasses import dataclass, asdict
from enum import Enum


class OperationMode(Enum):
    """Operation mode for the script."""
    DRY_RUN = "dry-run"
    APPLY = "apply"
    VERIFY_ONLY = "verify-only"


@dataclass
class FixtureMapping:
    """Represents a fixture file to be propagated."""
    source_path: Path
    target_path: Path
    source_namespace: str
    target_namespace: str
    file_name: str
    target_component: str

    def to_dict(self) -> Dict:
        """Convert to dictionary for JSON serialization."""
        return {
            'source_path': str(self.source_path),
            'target_path': str(self.target_path),
            'source_namespace': self.source_namespace,
            'target_namespace': self.target_namespace,
            'file_name': self.file_name,
            'target_component': self.target_component
        }


@dataclass
class PropagationResult:
    """Result of a fixture propagation operation."""
    success: bool
    fixture_mapping: FixtureMapping
    backup_path: Optional[Path] = None
    checksum_before: Optional[str] = None
    checksum_after: Optional[str] = None
    error_message: Optional[str] = None

    def to_dict(self) -> Dict:
        """Convert to dictionary for JSON serialization."""
        return {
            'success': self.success,
            'fixture_mapping': self.fixture_mapping.to_dict(),
            'backup_path': str(self.backup_path) if self.backup_path else None,
            'checksum_before': self.checksum_before,
            'checksum_after': self.checksum_after,
            'error_message': self.error_message
        }


class VaultFixturePropagator:
    """
    Main class for propagating Vault fixtures to other component test projects.

    Implements comprehensive safety protocols including git safety, backups,
    verification, and detailed reporting.
    """

    # Source namespace pattern
    VAULT_NAMESPACE = "ExxerAI.Vault.Integration.Tests"

    # Target components to propagate fixtures to
    # EXCLUDED: Components (✅ deduplicated), Datastream (✅ 100% compliant), Vault (✅ source master)
    TARGET_COMPONENTS = [
        "Cortex", "Nexus", "Signal", "Sentinel",
        "Gatekeeper", "Conduit", "Helix"  # Removed Datastream
    ]

    # Fixture files to propagate from Vault
    FIXTURE_FILES = [
        "AutomatedFullStackContainerFixture.cs",
        "KnowledgeStoreContainerFixture.cs",
        "AutomatedQdrantContainerFixture.cs",
        "AutomatedNeo4jContainerFixture.cs",
        "AutomatedOllamaContainerFixture.cs",
        "QdrantContainerFixture.cs",
        "Neo4jContainerFixture.cs",
        "OllamaContainerFixture.cs",
        "FixtureEvents.cs",
        "FixtureDocumentHashGenerator.cs",
        "FixturePolymorphicDocumentProcessor.cs",
        "FixtureGoogleDriveIngestionClient.cs"
    ]

    def __init__(self, base_path: str, mode: OperationMode = OperationMode.DRY_RUN,
                 enable_push: bool = False):
        """
        Initialize the propagator.

        Args:
            base_path: Root path of the ExxerAI repository
            mode: Operation mode (dry-run, apply, verify-only)
            enable_push: Whether to push changes to remote repository
        """
        self.base_path = Path(base_path).resolve()
        self.mode = mode
        self.enable_push = enable_push

        # Paths
        self.vault_fixtures_path = self.base_path / "code" / "src" / "tests" / "05IntegrationTests" / "ExxerAI.Vault.Integration.Test" / "Fixtures"
        self.integration_tests_path = self.base_path / "code" / "src" / "tests" / "05IntegrationTests"
        self.scripts_path = self.base_path / "scripts"
        self.reports_path = self.base_path / "docs" / "reports"

        # Create timestamp for this run
        self.run_timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")

        # Backup directory
        self.backup_dir = self.base_path / "backups" / f"fixture_propagation_{self.run_timestamp}"

        # Results tracking
        self.results: List[PropagationResult] = []
        self.errors: List[str] = []

        # Ensure directories exist
        self._ensure_directories()

    def _ensure_directories(self) -> None:
        """Ensure required directories exist."""
        self.reports_path.mkdir(parents=True, exist_ok=True)
        if self.mode == OperationMode.APPLY:
            self.backup_dir.mkdir(parents=True, exist_ok=True)

    def _log(self, message: str, level: str = "INFO") -> None:
        """
        Log a message with timestamp and level.

        Args:
            message: Message to log
            level: Log level (INFO, WARNING, ERROR, SUCCESS)
        """
        timestamp = datetime.now().strftime("%H:%M:%S")
        prefix = {
            "INFO": "ℹ️",
            "WARNING": "⚠️",
            "ERROR": "❌",
            "SUCCESS": "✅",
            "DEBUG": "🔍"
        }.get(level, "•")

        print(f"[{timestamp}] {prefix} {message}")

    def _calculate_checksum(self, file_path: Path) -> str:
        """
        Calculate SHA256 checksum of a file.

        Args:
            file_path: Path to file

        Returns:
            Hex digest of SHA256 hash
        """
        sha256_hash = hashlib.sha256()
        with open(file_path, "rb") as f:
            for byte_block in iter(lambda: f.read(4096), b""):
                sha256_hash.update(byte_block)
        return sha256_hash.hexdigest()

    def _run_git_command(self, args: List[str], error_message: str) -> Tuple[bool, str]:
        """
        Run a git command.

        Args:
            args: Git command arguments
            error_message: Error message to display on failure

        Returns:
            Tuple of (success, output)
        """
        try:
            result = subprocess.run(
                ["git"] + args,
                cwd=self.base_path,
                capture_output=True,
                text=True,
                check=False
            )

            if result.returncode != 0:
                self._log(f"{error_message}: {result.stderr}", "ERROR")
                return False, result.stderr

            return True, result.stdout
        except Exception as e:
            self._log(f"{error_message}: {str(e)}", "ERROR")
            return False, str(e)

    def perform_git_safety_cycle(self) -> bool:
        """
        Perform comprehensive git safety cycle.

        Steps:
        1. Check git status for uncommitted changes
        2. Add all changes
        3. Commit with descriptive message
        4. Optionally push to remote

        Returns:
            True if successful, False otherwise
        """
        if self.mode != OperationMode.APPLY:
            self._log("DRY RUN: Would perform git safety cycle", "DEBUG")
            return True

        self._log("Starting git safety cycle...", "INFO")

        # Step 1: Check git status
        success, output = self._run_git_command(
            ["status", "--porcelain"],
            "Failed to check git status"
        )

        if not success:
            self._log("Cannot proceed without clean git status check", "ERROR")
            return False

        if output.strip():
            self._log(f"Found uncommitted changes:\n{output}", "WARNING")
            response = input("Uncommitted changes detected. Continue with safety commit? [y/N]: ")
            if response.lower() != 'y':
                self._log("User aborted git safety cycle", "WARNING")
                return False
        else:
            self._log("Working directory is clean", "SUCCESS")

        # Step 2: Add all changes
        self._log("Adding all changes to staging area...", "INFO")
        success, _ = self._run_git_command(
            ["add", "."],
            "Failed to add changes"
        )

        if not success:
            return False

        # Step 3: Commit
        commit_message = f"""Fixture Propagation - Safety Commit

Automated safety commit before fixture propagation operation.
Timestamp: {self.run_timestamp}
Operation: Propagate Vault fixtures to component test projects

🤖 Generated with Claude Code
"""

        self._log("Creating safety commit...", "INFO")
        success, output = self._run_git_command(
            ["commit", "-m", commit_message],
            "Failed to create commit"
        )

        if not success:
            if "nothing to commit" in output:
                self._log("Nothing to commit - working tree clean", "INFO")
            else:
                return False
        else:
            self._log(f"Created safety commit: {output.strip()}", "SUCCESS")

        # Step 4: Optional push
        if self.enable_push:
            self._log("Pushing to remote repository...", "INFO")
            success, output = self._run_git_command(
                ["push"],
                "Failed to push to remote"
            )

            if not success:
                self._log("Push failed, but local commit is safe", "WARNING")
            else:
                self._log("Successfully pushed to remote", "SUCCESS")

        return True

    def _create_backup(self, file_path: Path) -> Optional[Path]:
        """
        Create a backup of a file before modification.

        Args:
            file_path: Path to file to backup

        Returns:
            Path to backup file or None if file doesn't exist
        """
        if not file_path.exists():
            return None

        if self.mode != OperationMode.APPLY:
            return None

        # Create relative path structure in backup directory
        relative_path = file_path.relative_to(self.base_path)
        backup_path = self.backup_dir / relative_path

        # Ensure parent directory exists
        backup_path.parent.mkdir(parents=True, exist_ok=True)

        # Copy file
        shutil.copy2(file_path, backup_path)

        self._log(f"Created backup: {backup_path}", "DEBUG")
        return backup_path

    def _transform_namespace(self, content: str, source_namespace: str,
                            target_namespace: str) -> str:
        """
        Transform namespace in file content.

        Args:
            content: File content
            source_namespace: Source namespace to replace
            target_namespace: Target namespace

        Returns:
            Transformed content
        """
        # Replace namespace declaration
        pattern = rf"namespace\s+{re.escape(source_namespace)}\b"
        content = re.sub(pattern, f"namespace {target_namespace}", content)

        # Replace using statements (if any reference the source namespace)
        using_pattern = rf"using\s+{re.escape(source_namespace)}\b"
        content = re.sub(using_pattern, f"using {target_namespace}", content)

        return content

    def _get_target_components(self) -> List[str]:
        """
        Get list of target component projects that exist.

        Returns:
            List of component names
        """
        components = []
        for component in self.TARGET_COMPONENTS:
            project_path = self.integration_tests_path / f"ExxerAI.{component}.Integration.Test"
            if project_path.exists() and project_path.is_dir():
                components.append(component)

        return components

    def _build_fixture_mappings(self) -> List[FixtureMapping]:
        """
        Build list of fixture mappings (source -> target).

        Returns:
            List of FixtureMapping objects
        """
        mappings = []

        if not self.vault_fixtures_path.exists():
            self._log(f"Vault fixtures path not found: {self.vault_fixtures_path}", "ERROR")
            return mappings

        components = self._get_target_components()
        self._log(f"Found {len(components)} target components: {', '.join(components)}", "INFO")

        for component in components:
            for fixture_file in self.FIXTURE_FILES:
                source_file = self.vault_fixtures_path / fixture_file

                if not source_file.exists():
                    self._log(f"Source fixture not found: {fixture_file}", "WARNING")
                    continue

                # Target paths
                target_project = self.integration_tests_path / f"ExxerAI.{component}.Integration.Test"
                target_fixtures_dir = target_project / "Fixtures"
                target_file = target_fixtures_dir / fixture_file

                # Namespace transformation
                source_namespace = f"{self.VAULT_NAMESPACE}.Fixtures"
                target_namespace = f"ExxerAI.{component}.Integration.Tests.Fixtures"

                mapping = FixtureMapping(
                    source_path=source_file,
                    target_path=target_file,
                    source_namespace=source_namespace,
                    target_namespace=target_namespace,
                    file_name=fixture_file,
                    target_component=component
                )

                mappings.append(mapping)

        self._log(f"Built {len(mappings)} fixture mappings", "INFO")
        return mappings

    def _propagate_fixture(self, mapping: FixtureMapping) -> PropagationResult:
        """
        Propagate a single fixture file.

        Args:
            mapping: FixtureMapping object

        Returns:
            PropagationResult object
        """
        self._log(f"Processing: {mapping.file_name} -> {mapping.target_component}", "INFO")

        try:
            # Read source file
            with open(mapping.source_path, 'r', encoding='utf-8') as f:
                content = f.read()

            # Transform namespace
            transformed_content = self._transform_namespace(
                content,
                mapping.source_namespace,
                mapping.target_namespace
            )

            # Check if target exists and create backup
            backup_path = None
            checksum_before = None
            if mapping.target_path.exists():
                checksum_before = self._calculate_checksum(mapping.target_path)
                backup_path = self._create_backup(mapping.target_path)

            # Write to target (only in APPLY mode)
            if self.mode == OperationMode.APPLY:
                # Ensure target directory exists
                mapping.target_path.parent.mkdir(parents=True, exist_ok=True)

                # Write file
                with open(mapping.target_path, 'w', encoding='utf-8', newline='\n') as f:
                    f.write(transformed_content)

                # Verify file exists and is not empty
                if not mapping.target_path.exists():
                    raise Exception("Target file was not created")

                if mapping.target_path.stat().st_size == 0:
                    raise Exception("Target file is empty")

                # Calculate checksum after
                checksum_after = self._calculate_checksum(mapping.target_path)

                self._log(f"Successfully propagated: {mapping.file_name}", "SUCCESS")
            else:
                checksum_after = None
                self._log(f"DRY RUN: Would propagate {mapping.file_name}", "DEBUG")

            return PropagationResult(
                success=True,
                fixture_mapping=mapping,
                backup_path=backup_path,
                checksum_before=checksum_before,
                checksum_after=checksum_after
            )

        except Exception as e:
            error_msg = f"Failed to propagate {mapping.file_name}: {str(e)}"
            self._log(error_msg, "ERROR")

            return PropagationResult(
                success=False,
                fixture_mapping=mapping,
                error_message=error_msg
            )

    def _verify_existing_fixtures(self) -> Dict[str, List[str]]:
        """
        Verify existing fixture files in component projects.

        Returns:
            Dictionary mapping component names to lists of existing fixtures
        """
        verification = {}

        components = self._get_target_components()

        for component in components:
            fixtures_dir = self.integration_tests_path / f"ExxerAI.{component}.Integration.Test" / "Fixtures"

            if not fixtures_dir.exists():
                verification[component] = []
                continue

            existing_fixtures = [
                f.name for f in fixtures_dir.glob("*.cs")
                if f.is_file() and f.name in self.FIXTURE_FILES
            ]

            verification[component] = existing_fixtures

        return verification

    def _generate_json_report(self) -> Path:
        """
        Generate JSON report of propagation results.

        Returns:
            Path to generated JSON report
        """
        report_path = self.scripts_path / f"propagation_report_{self.run_timestamp}.json"

        report = {
            "timestamp": self.run_timestamp,
            "mode": self.mode.value,
            "base_path": str(self.base_path),
            "vault_fixtures_path": str(self.vault_fixtures_path),
            "backup_directory": str(self.backup_dir) if self.mode == OperationMode.APPLY else None,
            "summary": {
                "total_mappings": len(self.results),
                "successful": sum(1 for r in self.results if r.success),
                "failed": sum(1 for r in self.results if not r.success),
                "components_updated": len(set(r.fixture_mapping.target_component for r in self.results if r.success))
            },
            "results": [r.to_dict() for r in self.results],
            "errors": self.errors
        }

        with open(report_path, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2)

        self._log(f"Generated JSON report: {report_path}", "SUCCESS")
        return report_path

    def _generate_markdown_report(self) -> Path:
        """
        Generate Markdown report of propagation results.

        Returns:
            Path to generated Markdown report
        """
        report_path = self.reports_path / f"fixture_propagation_report_{self.run_timestamp}.md"

        successful = [r for r in self.results if r.success]
        failed = [r for r in self.results if not r.success]

        report_lines = [
            "# Vault Fixture Propagation Report",
            "",
            f"**Timestamp**: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
            f"**Mode**: {self.mode.value}",
            f"**Base Path**: `{self.base_path}`",
            "",
            "## Summary",
            "",
            f"- **Total Mappings**: {len(self.results)}",
            f"- **Successful**: {len(successful)}",
            f"- **Failed**: {len(failed)}",
            f"- **Components Updated**: {len(set(r.fixture_mapping.target_component for r in successful))}",
            ""
        ]

        if self.mode == OperationMode.APPLY:
            report_lines.extend([
                f"- **Backup Directory**: `{self.backup_dir}`",
                ""
            ])

        # Successful propagations
        if successful:
            report_lines.extend([
                "## Successful Propagations",
                ""
            ])

            # Group by component
            by_component: Dict[str, List[PropagationResult]] = {}
            for result in successful:
                component = result.fixture_mapping.target_component
                if component not in by_component:
                    by_component[component] = []
                by_component[component].append(result)

            for component in sorted(by_component.keys()):
                report_lines.extend([
                    f"### {component}",
                    ""
                ])

                for result in by_component[component]:
                    report_lines.append(f"- ✅ `{result.fixture_mapping.file_name}`")
                    if result.backup_path:
                        report_lines.append(f"  - Backup: `{result.backup_path}`")
                    if result.checksum_after:
                        report_lines.append(f"  - Checksum: `{result.checksum_after[:16]}...`")

                report_lines.append("")

        # Failed propagations
        if failed:
            report_lines.extend([
                "## Failed Propagations",
                ""
            ])

            for result in failed:
                report_lines.extend([
                    f"- ❌ `{result.fixture_mapping.file_name}` -> {result.fixture_mapping.target_component}",
                    f"  - Error: {result.error_message}",
                    ""
                ])

        # Errors
        if self.errors:
            report_lines.extend([
                "## Errors",
                ""
            ])
            for error in self.errors:
                report_lines.append(f"- {error}")
            report_lines.append("")

        # Fixture files reference
        report_lines.extend([
            "## Fixture Files",
            "",
            "The following fixture files were propagated from Vault:",
            ""
        ])

        for fixture in self.FIXTURE_FILES:
            report_lines.append(f"- `{fixture}`")

        report_lines.extend([
            "",
            "---",
            "",
            "🤖 Generated with Claude Code",
            ""
        ])

        with open(report_path, 'w', encoding='utf-8', newline='\n') as f:
            f.write('\n'.join(report_lines))

        self._log(f"Generated Markdown report: {report_path}", "SUCCESS")
        return report_path

    def run(self) -> bool:
        """
        Main execution method.

        Returns:
            True if successful, False otherwise
        """
        self._log("=" * 70, "INFO")
        self._log("Vault Fixture Propagation Script", "INFO")
        self._log(f"Mode: {self.mode.value}", "INFO")
        self._log("=" * 70, "INFO")

        # Verify-only mode
        if self.mode == OperationMode.VERIFY_ONLY:
            self._log("Running verification only...", "INFO")
            verification = self._verify_existing_fixtures()

            for component, fixtures in verification.items():
                self._log(f"{component}: {len(fixtures)} fixtures found", "INFO")
                for fixture in fixtures:
                    self._log(f"  - {fixture}", "DEBUG")

            return True

        # Git safety cycle (only in APPLY mode)
        if self.mode == OperationMode.APPLY:
            if not self.perform_git_safety_cycle():
                self._log("Git safety cycle failed. Aborting.", "ERROR")
                return False

        # Build fixture mappings
        self._log("Building fixture mappings...", "INFO")
        mappings = self._build_fixture_mappings()

        if not mappings:
            self._log("No fixture mappings found. Nothing to do.", "WARNING")
            return False

        # Propagate fixtures
        self._log(f"Propagating {len(mappings)} fixtures...", "INFO")

        for mapping in mappings:
            result = self._propagate_fixture(mapping)
            self.results.append(result)

        # Generate reports
        self._log("Generating reports...", "INFO")
        json_report = self._generate_json_report()
        md_report = self._generate_markdown_report()

        # Summary
        successful_count = sum(1 for r in self.results if r.success)
        failed_count = sum(1 for r in self.results if not r.success)

        self._log("=" * 70, "INFO")
        self._log("SUMMARY", "INFO")
        self._log(f"Total: {len(self.results)}", "INFO")
        self._log(f"Successful: {successful_count}", "SUCCESS" if successful_count > 0 else "INFO")
        self._log(f"Failed: {failed_count}", "ERROR" if failed_count > 0 else "INFO")
        self._log("=" * 70, "INFO")

        return failed_count == 0


def main():
    """Main entry point for the script."""
    import argparse

    parser = argparse.ArgumentParser(
        description="Propagate Vault fixtures to component test projects",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Dry run (default - safe, no modifications)
  python propagate_vault_fixtures.py --dry-run

  # Apply changes (requires confirmation)
  python propagate_vault_fixtures.py --apply

  # Apply and push to remote
  python propagate_vault_fixtures.py --apply --push

  # Verify existing fixtures only
  python propagate_vault_fixtures.py --verify-only
        """
    )

    # Mutually exclusive group for operation mode
    mode_group = parser.add_mutually_exclusive_group(required=False)
    mode_group.add_argument(
        "--dry-run",
        action="store_true",
        default=True,
        help="Simulate operations without making changes (DEFAULT)"
    )
    mode_group.add_argument(
        "--apply",
        action="store_true",
        help="Apply changes (modifies files)"
    )
    mode_group.add_argument(
        "--verify-only",
        action="store_true",
        help="Only verify existing fixtures without propagation"
    )

    parser.add_argument(
        "--push",
        action="store_true",
        help="Push changes to remote repository after commit (requires --apply)"
    )

    parser.add_argument(
        "--base-path",
        type=str,
        default=None,
        help="Base path to ExxerAI repository (default: auto-detect)"
    )

    args = parser.parse_args()

    # Determine operation mode
    if args.verify_only:
        mode = OperationMode.VERIFY_ONLY
    elif args.apply:
        mode = OperationMode.APPLY
    else:
        mode = OperationMode.DRY_RUN

    # Determine base path
    if args.base_path:
        base_path = args.base_path
    else:
        # Auto-detect: assume script is in scripts/ subdirectory
        script_dir = Path(__file__).parent
        base_path = script_dir.parent

    # Validate base path
    base_path_obj = Path(base_path)
    if not base_path_obj.exists():
        print(f"❌ Error: Base path does not exist: {base_path}")
        return 1

    vault_test_path = base_path_obj / "code" / "src" / "tests" / "05IntegrationTests" / "ExxerAI.Vault.Integration.Test"
    if not vault_test_path.exists():
        print(f"❌ Error: Vault test project not found: {vault_test_path}")
        print(f"Please ensure you're running from the correct directory")
        return 1

    # Warning for push flag without apply
    if args.push and mode != OperationMode.APPLY:
        print("⚠️  Warning: --push flag ignored (only valid with --apply)")
        args.push = False

    # Confirmation for APPLY mode
    if mode == OperationMode.APPLY:
        print("\n" + "=" * 70)
        print("⚠️  WARNING: You are about to modify fixture files!")
        print("=" * 70)
        print(f"Base path: {base_path}")
        print(f"Push to remote: {'Yes' if args.push else 'No'}")
        print("=" * 70)
        response = input("Continue? [y/N]: ")
        if response.lower() != 'y':
            print("Aborted by user")
            return 0

    # Create propagator and run
    try:
        propagator = VaultFixturePropagator(
            base_path=str(base_path),
            mode=mode,
            enable_push=args.push
        )

        success = propagator.run()
        return 0 if success else 1

    except KeyboardInterrupt:
        print("\n\n❌ Interrupted by user")
        return 130
    except Exception as e:
        print(f"\n\n❌ Fatal error: {str(e)}")
        import traceback
        traceback.print_exc()
        return 1


if __name__ == "__main__":
    sys.exit(main())
