#!/usr/bin/env python3
"""
System and E2E Test Fixture Propagation Script
===============================================

Propagates fixture files from Vault.Integration.Test to System and E2E test projects.

Target Projects:
- System Tests (06SystemTests): EnhancedRag, Performance, Unknown
- E2E Tests (07E2ETests): E2E

Expected: 4 projects × 12 fixtures = 48 files
"""
import shutil
from pathlib import Path
import re

BASE_PATH = Path(r"F:\Dynamic\ExxerAi\ExxerAI")
VAULT_FIXTURES = BASE_PATH / "code/src/tests/05IntegrationTests/ExxerAI.Vault.Integration.Test/Fixtures"

# System test projects (06SystemTests)
SYSTEM_TESTS_PATH = BASE_PATH / "code/src/tests/06SystemTests"
SYSTEM_PROJECTS = [
    "EnhancedRag.System.Test",
    "Performance.System.Test",
    "Unknown.System.Test"
]

# E2E test projects (07E2ETests)
E2E_TESTS_PATH = BASE_PATH / "code/src/tests/07E2ETests"
E2E_PROJECTS = [
    "E2E.Test"
]

FIXTURES = [
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

def transform_namespace_system(content, project_name):
    """Transform namespace from Vault to System test project"""
    # Extract base name (e.g., "EnhancedRag" from "EnhancedRag.System.Test")
    base_name = project_name.replace(".System.Test", "").replace(".Test", "")

    # Transform: ExxerAI.Vault.Integration.Tests → ExxerAI.{BaseName}.System.Tests
    content = re.sub(
        r'namespace\s+ExxerAI\.Vault\.Integration\.Tests\.Fixtures',
        f'namespace ExxerAI.{base_name}.System.Tests.Fixtures',
        content
    )
    content = re.sub(
        r'using\s+ExxerAI\.Vault\.Integration\.Tests',
        f'using ExxerAI.{base_name}.System.Tests',
        content
    )
    return content

def transform_namespace_e2e(content):
    """Transform namespace from Vault to E2E test project"""
    # Transform: ExxerAI.Vault.Integration.Tests → ExxerAI.E2E.Tests
    content = re.sub(
        r'namespace\s+ExxerAI\.Vault\.Integration\.Tests\.Fixtures',
        'namespace ExxerAI.E2E.Tests.Fixtures',
        content
    )
    content = re.sub(
        r'using\s+ExxerAI\.Vault\.Integration\.Tests',
        'using ExxerAI.E2E.Tests',
        content
    )
    return content

def propagate_to_project(base_path, project_name, transform_func, project_type="System"):
    """Propagate fixtures to a single project"""
    target_dir = base_path / f"ExxerAI.{project_name}" / "Fixtures"
    target_dir.mkdir(parents=True, exist_ok=True)

    propagated = 0
    for fixture_file in FIXTURES:
        source_file = VAULT_FIXTURES / fixture_file
        target_file = target_dir / fixture_file

        if not source_file.exists():
            print(f"⚠️  Source not found: {fixture_file}")
            continue

        # Read and transform
        content = source_file.read_text(encoding='utf-8')
        if project_type == "E2E":
            transformed = transform_func(content)
        else:
            transformed = transform_func(content, project_name)

        # Write
        target_file.write_text(transformed, encoding='utf-8', newline='\n')
        propagated += 1
        print(f"✅ {project_type}/{project_name}/{fixture_file}")

    return propagated

def main():
    total_propagated = 0

    # Propagate to System test projects
    print("=" * 70)
    print("System Tests (06SystemTests)")
    print("=" * 70)
    for project in SYSTEM_PROJECTS:
        count = propagate_to_project(SYSTEM_TESTS_PATH, project, transform_namespace_system, "System")
        total_propagated += count

    print()

    # Propagate to E2E test projects
    print("=" * 70)
    print("E2E Tests (07E2ETests)")
    print("=" * 70)
    for project in E2E_PROJECTS:
        count = propagate_to_project(E2E_TESTS_PATH, project, transform_namespace_e2e, "E2E")
        total_propagated += count

    print()
    print("=" * 70)
    print(f"✅ Propagated {total_propagated} fixture files across {len(SYSTEM_PROJECTS) + len(E2E_PROJECTS)} projects")
    print(f"   - System Tests: {len(SYSTEM_PROJECTS)} projects × 12 fixtures = {len(SYSTEM_PROJECTS) * 12} files")
    print(f"   - E2E Tests: {len(E2E_PROJECTS)} projects × 12 fixtures = {len(E2E_PROJECTS) * 12} files")
    print("=" * 70)

if __name__ == "__main__":
    main()
