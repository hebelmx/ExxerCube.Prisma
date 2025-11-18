#!/usr/bin/env python3
"""
Nexus Adapter Test Split Script - Automated test project reorganization
Splits monolithic ExxerAI.Nexus.Adapter.Tests into 4 technology-specific projects.

Based on ADR-014: Nexus Adapter Test Project Split Strategy

Target Projects:
1. ExxerAI.Axis.Adapter.Tests.NPOI (31 tests) - Legacy Office formats
2. ExxerAI.Axis.Adapter.Tests.OpenXml (30 tests) - Modern Office formats
3. ExxerAI.Axis.Adapter.Tests.PdfPig (23 tests) - PDF processing
4. ExxerAI.Axis.Adapter.Tests.DocumentProcessing (13 tests) - Core processing

Usage:
    python scripts/split_nexus_adapter_tests.py --dry-run
    python scripts/split_nexus_adapter_tests.py --apply

Author: Claude Code Agent
Date: 2025-11-08
"""

import argparse
import json
import shutil
from pathlib import Path
from typing import Dict, List, Set
import sys


class NexusAdapterSplitter:
    """Automates splitting Nexus.Adapter.Tests into technology-specific projects."""

    def __init__(self, base_path: Path, dry_run: bool = True):
        self.base_path = Path(base_path)
        self.dry_run = dry_run
        self.tests_path = self.base_path / "code" / "src" / "tests"

        # Source project
        self.source_project = self.tests_path / "04AdapterTests" / "ExxerAI.Nexus.Adapter.Tests"

        # Target projects configuration
        self.target_projects = {
            "NPOI": {
                "name": "ExxerAI.Axis.Adapter.NPOI.Tests",
                "path": self.tests_path / "04AdapterTests" / "ExxerAI.Axis.Adapter.NPOI.Tests",
                "description": "Legacy Office format adapter tests (NPOI)",
                "files": [
                    "NPOIAdapterTests.cs"
                ],
                "namespace": "ExxerAI.Axis.Adapter.NPOI.Tests",
                "test_count": 31
            },
            "OpenXml": {
                "name": "ExxerAI.Axis.Adapter.OpenXml.Tests",
                "path": self.tests_path / "04AdapterTests" / "ExxerAI.Axis.Adapter.OpenXml.Tests",
                "description": "Modern Office format adapter tests (OpenXml)",
                "files": [
                    "OpenXmlAdapterTests.cs"
                ],
                "namespace": "ExxerAI.Axis.Adapter.OpenXml.Tests",
                "test_count": 30
            },
            "PdfPig": {
                "name": "ExxerAI.Axis.Adapter.PdfPig.Tests",
                "path": self.tests_path / "04AdapterTests" / "ExxerAI.Axis.Adapter.PdfPig.Tests",
                "description": "PDF processing adapter tests (PdfPig)",
                "files": [
                    "PdfPigAdapterTests.cs"
                ],
                "namespace": "ExxerAI.Axis.Adapter.PdfPig.Tests",
                "test_count": 23
            },
            "DocumentProcessing": {
                "name": "ExxerAI.Axis.Adapter.DocumentProcessing.Tests",
                "path": self.tests_path / "04AdapterTests" / "ExxerAI.Axis.Adapter.DocumentProcessing.Tests",
                "description": "Core document processing adapter tests",
                "files": [
                    "DocumentProcessing/DocumentProcessingAdapterTests.cs",
                    "DocumentProcessing/DocumentProcessingAdaptersIntegrationTests.cs",
                    "DocumentProcessing/DocumentProcessingFactoryIntegrationTests.cs"
                ],
                "namespace": "ExxerAI.Axis.Adapter.DocumentProcessing.Tests",
                "test_count": 13,
                "copy_shared": True  # Copy shared test helpers
            }
        }

        # Shared test helper files (copied to DocumentProcessing project)
        self.shared_files = [
            "DocumentIngestionResult.cs",
            "TestDocumentIngestionRegistration.cs",
            "Repositories/InMemoryConversationRepository.cs",
            "Repositories/MockConversationRepository.cs",
            "Repositories/MockLanguageModelRepository.cs"
        ]

        self.operations = []

    def generate_csproj(self, project_key: str) -> str:
        """Generate .csproj file content for a target project."""
        config = self.target_projects[project_key]

        return f'''<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <RootNamespace>{config["namespace"]}</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <!-- Testing Framework -->
    <PackageReference Include="xunit" Version="3.0.0-beta.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0-beta.1">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="1.5.0-preview.24575.1" />

    <!-- Assertion & Mocking -->
    <PackageReference Include="Shouldly" Version="4.3.0" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />

    <!-- Logging -->
    <PackageReference Include="Meziantou.Extensions.Logging.Xunit" Version="1.0.17" />
  </ItemGroup>

  <ItemGroup>
    <!-- Project References -->
    <ProjectReference Include="..\\..\\..\\Infrastructure\\ExxerAI.Nexus\\ExxerAI.Nexus.csproj" />
    <ProjectReference Include="..\\..\\..\\Infrastructure\\ExxerAI.Axis\\ExxerAI.Axis.csproj" />
    <ProjectReference Include="..\\..\\..\\Core\\ExxerAI.Domain\\ExxerAI.Domain.csproj" />
  </ItemGroup>

</Project>
'''

    def generate_globalusings(self, project_key: str) -> str:
        """Generate GlobalUsings.cs for a target project."""
        config = self.target_projects[project_key]

        return f'''// Global usings for {config["name"]}
// Auto-generated by split_nexus_adapter_tests.py

global using Xunit;
global using Shouldly;
global using NSubstitute;
global using Microsoft.Extensions.Logging;
global using ExxerAI.Domain.ValueObjects;
global using ExxerAI.Domain.Entities;
global using ExxerAI.Axioms.Results;
global using ExxerAI.Axis.Extensions.Logging;
'''

    def plan_operations(self):
        """Plan all operations for the split."""
        self.operations = []

        for key, config in self.target_projects.items():
            target_path = config["path"]

            # Operation 1: Create project directory
            self.operations.append({
                "type": "create_dir",
                "path": target_path,
                "description": f"Create project directory: {config['name']}"
            })

            # Operation 2: Create .csproj file
            csproj_path = target_path / f"{config['name']}.csproj"
            self.operations.append({
                "type": "create_file",
                "path": csproj_path,
                "content": self.generate_csproj(key),
                "description": f"Create project file: {csproj_path.name}"
            })

            # Operation 3: Create GlobalUsings.cs
            global_usings_path = target_path / "GlobalUsings.cs"
            self.operations.append({
                "type": "create_file",
                "path": global_usings_path,
                "content": self.generate_globalusings(key),
                "description": f"Create GlobalUsings.cs"
            })

            # Operation 4: Move test files
            for file_rel_path in config["files"]:
                source_file = self.source_project / file_rel_path
                target_file = target_path / Path(file_rel_path).name  # Flatten structure

                self.operations.append({
                    "type": "move_file",
                    "source": source_file,
                    "target": target_file,
                    "namespace_old": "ExxerAI.Nexus.Adapter.Tests",
                    "namespace_new": config["namespace"],
                    "description": f"Move {Path(file_rel_path).name}"
                })

            # Operation 5: Copy shared test helpers (if specified)
            if config.get("copy_shared", False):
                # Create subdirectories for shared helpers
                for subdir in ["Repositories"]:
                    subdir_path = target_path / subdir
                    self.operations.append({
                        "type": "create_dir",
                        "path": subdir_path,
                        "description": f"Create {subdir} directory"
                    })

                for shared_file in self.shared_files:
                    source_file = self.source_project / shared_file
                    target_file = target_path / shared_file

                    self.operations.append({
                        "type": "copy_file",
                        "source": source_file,
                        "target": target_file,
                        "namespace_old": "ExxerAI.Nexus.Adapter.Tests",
                        "namespace_new": config["namespace"],
                        "description": f"Copy shared helper: {Path(shared_file).name}"
                    })

    def update_namespace(self, file_path: Path, old_namespace: str, new_namespace: str) -> str:
        """Read file, update namespace, return new content."""
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()

        # Update namespace declaration
        updated = content.replace(
            f"namespace {old_namespace}",
            f"namespace {new_namespace}"
        )

        return updated

    def execute_operations(self):
        """Execute or simulate all planned operations."""
        print("=" * 80)
        print(f"{'DRY RUN - ' if self.dry_run else ''}Nexus Adapter Test Split")
        print("=" * 80)
        print()

        if self.dry_run:
            print("🔍 DRY RUN MODE - No changes will be made")
        else:
            print("⚠️  APPLYING CHANGES")
        print()

        stats = {
            "dirs_created": 0,
            "files_created": 0,
            "files_moved": 0,
            "namespaces_updated": 0
        }

        for i, op in enumerate(self.operations, 1):
            op_type = op["type"]

            print(f"[{i}/{len(self.operations)}] {op['description']}")

            if op_type == "create_dir":
                if not self.dry_run:
                    op["path"].mkdir(parents=True, exist_ok=True)
                print(f"    📁 {op['path'].relative_to(self.base_path)}")
                stats["dirs_created"] += 1

            elif op_type == "create_file":
                if not self.dry_run:
                    op["path"].parent.mkdir(parents=True, exist_ok=True)
                    with open(op["path"], 'w', encoding='utf-8') as f:
                        f.write(op["content"])
                print(f"    📄 {op['path'].relative_to(self.base_path)}")
                stats["files_created"] += 1

            elif op_type == "move_file":
                if op["source"].exists():
                    if not self.dry_run:
                        # Update namespace and move
                        updated_content = self.update_namespace(
                            op["source"],
                            op["namespace_old"],
                            op["namespace_new"]
                        )
                        op["target"].parent.mkdir(parents=True, exist_ok=True)
                        with open(op["target"], 'w', encoding='utf-8') as f:
                            f.write(updated_content)
                        op["source"].unlink()

                    print(f"    📦 {op['source'].relative_to(self.source_project)}")
                    print(f"    → {op['target'].relative_to(self.tests_path)}")
                    print(f"    🔄 {op['namespace_old']} → {op['namespace_new']}")
                    stats["files_moved"] += 1
                    stats["namespaces_updated"] += 1
                else:
                    print(f"    ⚠️  Source file not found: {op['source']}")

            elif op_type == "copy_file":
                if op["source"].exists():
                    if not self.dry_run:
                        # Update namespace and copy (keep original)
                        updated_content = self.update_namespace(
                            op["source"],
                            op["namespace_old"],
                            op["namespace_new"]
                        )
                        op["target"].parent.mkdir(parents=True, exist_ok=True)
                        with open(op["target"], 'w', encoding='utf-8') as f:
                            f.write(updated_content)

                    print(f"    📋 {op['source'].relative_to(self.source_project)}")
                    print(f"    → {op['target'].relative_to(self.tests_path)}")
                    print(f"    🔄 {op['namespace_old']} → {op['namespace_new']}")
                    stats["files_created"] += 1
                    stats["namespaces_updated"] += 1
                else:
                    print(f"    ⚠️  Source file not found: {op['source']}")

            print()

        # Summary
        print("=" * 80)
        print("📊 Summary")
        print("=" * 80)
        print()

        for key, config in self.target_projects.items():
            print(f"✅ {config['name']}")
            print(f"   Tests: {config['test_count']}")
            print(f"   Files: {len(config['files'])}")
            print()

        print(f"Total Operations:")
        print(f"  Directories created: {stats['dirs_created']}")
        print(f"  Files created: {stats['files_created']}")
        print(f"  Files moved: {stats['files_moved']}")
        print(f"  Namespaces updated: {stats['namespaces_updated']}")
        print()

        if self.dry_run:
            print("🔍 DRY RUN COMPLETE - No actual changes made")
            print()
            print("To apply these changes, run:")
            print(f"  python {Path(__file__).name} --apply")
        else:
            print("✅ SPLIT COMPLETE")
            print()
            print("Next steps:")
            print("  1. Build all new projects")
            print("  2. Run tests to verify")
            print("  3. Commit changes")

    def run(self):
        """Execute the full split workflow."""
        print(f"Base path: {self.base_path}")
        print(f"Source project: {self.source_project}")
        print()

        if not self.source_project.exists():
            print(f"❌ Source project not found: {self.source_project}")
            sys.exit(1)

        self.plan_operations()
        self.execute_operations()


def main():
    parser = argparse.ArgumentParser(
        description='Split Nexus.Adapter.Tests into technology-specific projects',
        formatter_class=argparse.RawDescriptionHelpFormatter
    )

    parser.add_argument('--base-path', default='.',
                       help='Base path of ExxerAI project (default: current directory)')
    parser.add_argument('--dry-run', action='store_true',
                       help='Preview changes without applying them (default)')
    parser.add_argument('--apply', action='store_true',
                       help='Apply changes (execute the split)')

    args = parser.parse_args()

    # Default to dry-run if neither specified
    dry_run = not args.apply

    try:
        splitter = NexusAdapterSplitter(args.base_path, dry_run=dry_run)
        splitter.run()
        sys.exit(0)

    except Exception as e:
        print(f"\n❌ Error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
