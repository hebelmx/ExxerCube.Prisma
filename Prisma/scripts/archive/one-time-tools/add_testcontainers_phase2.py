#!/usr/bin/env python3
"""
ADR-011 Phase 2: Add TestContainers + xUnit v3 Dependencies to ALL Evocative Projects
"""

import subprocess
from pathlib import Path
from typing import Dict, List


class TestContainersDependencyManager:
    """Manages TestContainers and xUnit v3 dependency additions"""

    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.integration_tests_dir = self.base_path / "code/src/tests/05IntegrationTests"

    def get_project_dependencies(self) -> Dict[str, List[str]]:
        """Define dependencies for each evocative project"""
        return {
            "ExxerAI.Signal.Integration.Test": [
                # Base TestContainers + xUnit v3
                "Testcontainers",
                "Testcontainers.XunitV3",
                # Infrastructure for analytics/monitoring
                "Testcontainers.PostgreSql",
                "Testcontainers.Redis",
            ],
            "ExxerAI.Sentinel.Integration.Test": [
                # Base TestContainers + xUnit v3
                "Testcontainers",
                "Testcontainers.XunitV3",
                # Auth services infrastructure
                "Testcontainers.PostgreSql",
            ],
            "ExxerAI.Datastream.Integration.Test": [
                # Base TestContainers + xUnit v3
                "Testcontainers",
                "Testcontainers.XunitV3",
                # Data flow: PostgreSQL + Redis
                "Testcontainers.PostgreSql",
                "Testcontainers.Redis",
            ],
            "ExxerAI.Cortex.Integration.Test": [
                # Base TestContainers + xUnit v3
                "Testcontainers",
                "Testcontainers.XunitV3",
                # AI/LLM infrastructure
                # Note: Ollama doesn't have official TestContainers, use generic container
            ],
            "ExxerAI.Gatekeeper.Integration.Test": [
                # Base TestContainers + xUnit v3
                "Testcontainers",
                "Testcontainers.XunitV3",
                # External API integration (generic containers for mocking)
            ],
            "ExxerAI.Components.Integration.Test": [
                # Already has some, verify these exist
                "Testcontainers",
                "Testcontainers.XunitV3",
                "Testcontainers.Qdrant",
                "Testcontainers.Neo4j",
                "Testcontainers.PostgreSql",
            ],
            "ExxerAI.Nexus.Integration.Test": [
                # Fix existing broken implementation
                "Testcontainers",
                "Testcontainers.XunitV3",
                "Testcontainers.Qdrant",
                "Testcontainers.Neo4j",
            ],
        }

    def add_package_reference(self, project_path: Path, package_name: str, dry_run: bool = False):
        """Add a NuGet package reference to a project"""
        csproj_file = project_path / f"{project_path.name}.csproj"

        if not csproj_file.exists():
            print(f"⚠️  Project file not found: {csproj_file}")
            return False

        cmd = ["dotnet", "add", str(csproj_file), "package", package_name]

        print(f"{'[DRY-RUN] ' if dry_run else ''}Adding {package_name} to {project_path.name}...")

        if not dry_run:
            try:
                result = subprocess.run(
                    cmd,
                    capture_output=True,
                    text=True,
                    check=False,
                    cwd=str(self.base_path)
                )

                if result.returncode == 0:
                    print(f"  ✅ Successfully added {package_name}")
                    return True
                else:
                    # Check if already exists
                    if "already has a package reference" in result.stdout or "already has a package reference" in result.stderr:
                        print(f"  ℹ️  {package_name} already exists (skipped)")
                        return True
                    else:
                        print(f"  ⚠️  Failed to add {package_name}")
                        print(f"     stdout: {result.stdout}")
                        print(f"     stderr: {result.stderr}")
                        return False
            except Exception as e:
                print(f"  ❌ Error: {e}")
                return False
        else:
            print(f"  [Would add {package_name}]")
            return True

    def process_project(self, project_name: str, dependencies: List[str], dry_run: bool = False):
        """Process a single project, adding all its dependencies"""
        project_path = self.integration_tests_dir / project_name

        if not project_path.exists():
            print(f"\n⚠️  Project directory not found: {project_name}")
            print(f"   Expected at: {project_path}")
            return False

        print(f"\n{'='*100}")
        print(f"📦 Processing: {project_name}")
        print(f"{'='*100}")
        print(f"Dependencies to add: {len(dependencies)}")
        print()

        success_count = 0
        for package in dependencies:
            if self.add_package_reference(project_path, package, dry_run):
                success_count += 1

        print()
        print(f"✅ Added {success_count}/{len(dependencies)} packages to {project_name}")
        return success_count == len(dependencies)

    def execute_phase2(self, dry_run: bool = True):
        """Execute Phase 2: Add dependencies to all projects"""
        print("=" * 100)
        print("🎯 ADR-011 PHASE 2: ADD TESTCONTAINERS + XUNIT V3 DEPENDENCIES")
        print("=" * 100)
        print(f"Mode: {'DRY-RUN (no changes)' if dry_run else 'EXECUTION (adding packages)'}")
        print()

        project_dependencies = self.get_project_dependencies()
        results = {}

        for project_name, dependencies in project_dependencies.items():
            success = self.process_project(project_name, dependencies, dry_run)
            results[project_name] = success

        # Summary
        print()
        print("=" * 100)
        print("📊 PHASE 2 SUMMARY")
        print("=" * 100)
        print()

        successful = sum(1 for v in results.values() if v)
        total = len(results)

        print(f"Projects processed: {total}")
        print(f"Successful: {successful}")
        print(f"Failed: {total - successful}")
        print()

        print("Project Status:")
        for project_name, success in results.items():
            status = "✅" if success else "❌"
            print(f"  {status} {project_name}")

        print()
        print("=" * 100)

        if dry_run:
            print()
            print("⚠️  This was a DRY-RUN. No packages were added.")
            print("💡 Run with --apply to add the packages.")
        else:
            print()
            print(f"✅ Phase 2 {'completed successfully!' if all(results.values()) else 'completed with some failures'}")
            print("📝 Next step: Build solution to verify dependencies")

        return all(results.values())


def main():
    import argparse

    parser = argparse.ArgumentParser(description="ADR-011 Phase 2: Add TestContainers dependencies")
    parser.add_argument("--base-path", default="F:/Dynamic/ExxerAi/ExxerAI", help="Base path to ExxerAI repo")
    parser.add_argument("--apply", action="store_true", help="Apply changes (default is dry-run)")

    args = parser.parse_args()

    manager = TestContainersDependencyManager(args.base_path)
    success = manager.execute_phase2(dry_run=not args.apply)

    return 0 if success else 1


if __name__ == "__main__":
    exit(main())
