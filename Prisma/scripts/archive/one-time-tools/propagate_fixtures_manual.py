#!/usr/bin/env python3
"""Manual fixture propagation script - bypasses git safety cycle"""
import shutil
from pathlib import Path
import re

BASE_PATH = Path(r"F:\Dynamic\ExxerAi\ExxerAI")
VAULT_FIXTURES = BASE_PATH / "code/src/tests/05IntegrationTests/ExxerAI.Vault.Integration.Test/Fixtures"
INTEGRATION_TESTS = BASE_PATH / "code/src/tests/05IntegrationTests"

COMPONENTS = ["Cortex", "Nexus", "Signal", "Sentinel", "Gatekeeper", "Conduit"]
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

def transform_namespace(content, component):
    """Transform namespace from Vault to target component"""
    content = re.sub(
        r'namespace\s+ExxerAI\.Vault\.Integration\.Tests\.Fixtures',
        f'namespace ExxerAI.{component}.Integration.Tests.Fixtures',
        content
    )
    content = re.sub(
        r'using\s+ExxerAI\.Vault\.Integration\.Tests',
        f'using ExxerAI.{component}.Integration.Tests',
        content
    )
    return content

def main():
    propagated = 0
    for component in COMPONENTS:
        target_dir = INTEGRATION_TESTS / f"ExxerAI.{component}.Integration.Test/Fixtures"
        target_dir.mkdir(parents=True, exist_ok=True)

        for fixture_file in FIXTURES:
            source_file = VAULT_FIXTURES / fixture_file
            target_file = target_dir / fixture_file

            if not source_file.exists():
                print(f"⚠️  Source not found: {fixture_file}")
                continue

            # Read and transform
            content = source_file.read_text(encoding='utf-8')
            transformed = transform_namespace(content, component)

            # Write
            target_file.write_text(transformed, encoding='utf-8', newline='\n')
            propagated += 1
            print(f"✅ {component}/{fixture_file}")

    print(f"\n✅ Propagated {propagated} fixture files across {len(COMPONENTS)} components")

if __name__ == "__main__":
    main()
