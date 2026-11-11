"""
Batch fix script for test errors:
1. Convert Assert.* to Shouldly
2. Add ConfigureAwait(false) to test awaits
3. Add missing XML comments
"""

import re
from pathlib import Path

# Error files from Errors.txt
assert_errors = [
    ("code/src/tests/04IntegrationTests/ExxerAI.Sentinel.Integration.Tests/PlaceholderTests.cs", [43, 23, 24, 42]),
    ("code/src/tests/05SystemTests/ExxerAI.Performance.System.Tests/Support/DockerDesktopServiceManager.cs", [40, 45]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Sentinel.Integration.Tests/Support/DockerDesktopServiceManager.cs", [40, 45]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Vault.Integration.Tests/DocumentIngestionToVectorTests.cs", [37]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Vault.Integration.Tests/HybridKnowledgeServiceIntegrationTests.cs", [56, 81, 495]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Vault.Integration.Tests/KnowledgeStoreContainerVerificationTests.cs", [50, 86, 110]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Vault.Integration.Tests/Neo4jGraphStoreIntegrationTests.cs", [39, 65]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Vault.Integration.Tests/OllamaToQdrantIntegrationTests.cs", [44, 49]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Vault.Integration.Tests/QdrantConnectionTest.cs", [38, 46, 66]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Vault.Integration.Tests/Support/DockerDesktopServiceManager.cs", [40, 45]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Conduit.Integration.Tests/Support/DockerDesktopServiceManager.cs", [40, 45]),
    ("code/src/tests/05SystemTests/ExxerAI.EnhancedRag.System.Tests/Support/DockerDesktopServiceManager.cs", [40, 45]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Nexus.Integration.Tests/DocumentProcessing/EnhancedDocumentProcessingIntegrationTests.cs", [503]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Nexus.Integration.Tests/Support/DockerDesktopServiceManager.cs", [40, 45]),
    ("code/src/tests/09ArchitectureTests/ExxerAI.Architecture.Tests/EnumerationTestsFlowStatus.cs", [18, 19, 32, 33]),
    ("code/src/tests/09ArchitectureTests/ExxerAI.Architecture.Tests/EnumModelTests.cs", [30, 56]),
    ("code/src/tests/09ArchitectureTests/ExxerAI.Architecture.Tests/ServiceDiscoveryTests.cs", [19]),
    ("code/src/tests/09ArchitectureTests/ExxerAI.Architecture.Tests/temp_service_discovery.cs", [40]),
    ("code/src/tests/09ArchitectureTests/ExxerAI.Architecture.Tests/ValidationTests.cs", [21]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Cortex.Integration.Tests/OllamaEmbeddingGeneratorIntegrationTests.cs", [47]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Cortex.Integration.Tests/OllamaProviderIntegrationTests.cs", [48]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Cortex.Integration.Tests/Support/DockerDesktopServiceManager.cs", [40, 45]),
    ("code/src/tests/06E2ETests/ExxerAI.E2E.Tests/PlaywrightSmoke.cs", [21]),
    ("code/src/tests/06E2ETests/ExxerAI.E2E.Tests/Support/DockerDesktopServiceManager.cs", [40, 45]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Gatekeeper.Integration.Tests/GoogleDriveLiveTestContext.cs", [184]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Gatekeeper.Integration.Tests/Support/DockerDesktopServiceManager.cs", [40, 45]),
    ("code/src/tests/06E2ETests/ExxerAI.KPI.Dashboard.Tests/EndToEnd/OllamaToQdrantIntegrationTests.cs", [44, 49]),
    ("code/src/tests/06E2ETests/ExxerAI.KPI.Dashboard.Tests/Support/DockerDesktopServiceManager.cs", [40, 45]),
    ("code/src/tests/04IntegrationTests/ExxerAI.Signal.Integration.Tests/Support/DockerDesktopServiceManager.cs", [40, 45]),
    ("code/src/tests/02UnitTests/ExxerAI.UI.Tests/WebTests/CounterPageTest.cs", [57, 64]),
]

configureawait_errors = [
    ("code/src/tests/02UnitTests/ExxerAI.Migration.Tests/DataMigrationIntegrityTests.cs", [64, 74, 129, 140, 181, 192, 232, 242, 282, 292]),
    ("code/src/tests/02UnitTests/ExxerAI.Migration.Tests/MigrationStrategyTests.cs", [60, 69, 103, 113, 151, 161, 196, 206, 235, 242]),
]

def convert_assert_to_shouldly(content):
    """Convert common Assert patterns to Shouldly"""
    patterns = [
        # Assert.True/False
        (r'Assert\.True\(([^)]+)\)', r'\1.ShouldBeTrue()'),
        (r'Assert\.False\(([^)]+)\)', r'\1.ShouldBeFalse()'),
        # Assert.Equal/NotEqual
        (r'Assert\.Equal\(([^,]+),\s*([^)]+)\)', r'\2.ShouldBe(\1)'),
        (r'Assert\.NotEqual\(([^,]+),\s*([^)]+)\)', r'\2.ShouldNotBe(\1)'),
        # Assert.Null/NotNull
        (r'Assert\.Null\(([^)]+)\)', r'\1.ShouldBeNull()'),
        (r'Assert\.NotNull\(([^)]+)\)', r'\1.ShouldNotBeNull()'),
        # Assert.Empty/NotEmpty
        (r'Assert\.Empty\(([^)]+)\)', r'\1.ShouldBeEmpty()'),
        (r'Assert\.NotEmpty\(([^)]+)\)', r'\1.ShouldNotBeEmpty()'),
        # Assert.Contains/DoesNotContain
        (r'Assert\.Contains\(([^,]+),\s*([^)]+)\)', r'\2.ShouldContain(\1)'),
        (r'Assert\.DoesNotContain\(([^,]+),\s*([^)]+)\)', r'\2.ShouldNotContain(\1)'),
        # Assert.Throws
        (r'Assert\.Throws<([^>]+)>\(([^)]+)\)', r'Should.Throw<\1>(\2)'),
        # Assert.IsType/IsNotType
        (r'Assert\.IsType<([^>]+)>\(([^)]+)\)', r'\2.ShouldBeOfType<\1>()'),
        # Assert.Same/NotSame
        (r'Assert\.Same\(([^,]+),\s*([^)]+)\)', r'\2.ShouldBeSameAs(\1)'),
        (r'Assert\.NotSame\(([^,]+),\s*([^)]+)\)', r'\2.ShouldNotBeSameAs(\1)'),
    ]

    for pattern, replacement in patterns:
        content = re.sub(pattern, replacement, content)

    return content

def add_configureawait(content):
    """Add ConfigureAwait(false) to await expressions"""
    # Match await expressions without ConfigureAwait
    pattern = r'await\s+([^\s;]+(?:\([^)]*\)[^;]*?))(?!\s*\.ConfigureAwait)'
    replacement = r'await \1.ConfigureAwait(false)'
    return re.sub(pattern, replacement, content)

def process_file(filepath, line_numbers, fix_type):
    """Process a single file"""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()

        original_content = content

        if fix_type == 'assert':
            content = convert_assert_to_shouldly(content)
        elif fix_type == 'configureawait':
            content = add_configureawait(content)

        if content != original_content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(content)
            return True
        return False
    except Exception as e:
        print(f"Error processing {filepath}: {e}")
        return False

def main():
    base_path = Path("F:/Dynamic/ExxerAi/ExxerAI")

    fixed_count = 0

    # Fix Assert errors
    print("Converting Assert to Shouldly...")
    for rel_path, lines in assert_errors:
        full_path = base_path / rel_path
        if full_path.exists():
            if process_file(full_path, lines, 'assert'):
                fixed_count += 1
                print(f"  ✓ {rel_path}")
        else:
            print(f"  ✗ Not found: {rel_path}")

    # Fix ConfigureAwait errors
    print("\nAdding ConfigureAwait(false)...")
    for rel_path, lines in configureawait_errors:
        full_path = base_path / rel_path
        if full_path.exists():
            if process_file(full_path, lines, 'configureawait'):
                fixed_count += 1
                print(f"  ✓ {rel_path}")
        else:
            print(f"  ✗ Not found: {rel_path}")

    print(f"\n✅ Fixed {fixed_count} files")

if __name__ == '__main__':
    main()
