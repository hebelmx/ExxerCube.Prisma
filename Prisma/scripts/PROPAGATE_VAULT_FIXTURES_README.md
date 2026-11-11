# Vault Fixture Propagation Script

## Overview

Production-grade Python script for safely propagating fixture files from the Vault integration test project to other component test projects (Cortex, Nexus, Signal, etc.).

**Script**: `propagate_vault_fixtures.py`
**Author**: Claude Code (ExxerAI Development Team)
**Version**: 1.0.0
**Date**: 2025-11-05

## Features

### 🛡️ Safety Protocols

- **Dry-run mode by default** - No modifications without explicit `--apply` flag
- **Git safety cycle** - Automatic git add + commit before any modifications
- **Timestamped backups** - All overwritten files are backed up with timestamps
- **Verification** - File existence and checksum validation after operations
- **Rollback capability** - Backups enable manual rollback if needed
- **Error handling** - Graceful failure with detailed error messages

### 🔧 Core Functionality

- **Smart namespace transformation** - Automatically transforms `ExxerAI.Vault.Integration.Tests.Fixtures` to `ExxerAI.{Component}.Integration.Tests.Fixtures`
- **Batch processing** - Processes all fixtures across all target components
- **Selective propagation** - Only propagates predefined fixture files
- **Comprehensive reporting** - Generates both JSON and Markdown reports

## Usage

### Prerequisites

- Python 3.10 or higher
- Git repository initialized
- ExxerAI repository structure intact

### Command-Line Interface

```bash
# Dry run (default - safe, no modifications)
python scripts/propagate_vault_fixtures.py --dry-run

# Apply changes (requires confirmation)
python scripts/propagate_vault_fixtures.py --apply

# Apply and push to remote
python scripts/propagate_vault_fixtures.py --apply --push

# Verify existing fixtures only
python scripts/propagate_vault_fixtures.py --verify-only

# Custom base path
python scripts/propagate_vault_fixtures.py --apply --base-path "F:\Dynamic\ExxerAi\ExxerAI"
```

### Operation Modes

#### 1. Dry-Run Mode (Default)

```bash
python scripts/propagate_vault_fixtures.py --dry-run
```

- **Safety**: No file modifications
- **Output**: Simulates all operations and shows what would happen
- **Reports**: Generates reports showing planned operations
- **Use case**: Preview changes before applying

#### 2. Apply Mode

```bash
python scripts/propagate_vault_fixtures.py --apply
```

- **Safety**: Requires user confirmation
- **Process**:
  1. Checks git status
  2. Prompts for uncommitted changes
  3. Creates safety commit
  4. Creates backups of existing files
  5. Propagates fixtures with namespace transformation
  6. Verifies file integrity
  7. Generates reports

#### 3. Verify-Only Mode

```bash
python scripts/propagate_vault_fixtures.py --verify-only
```

- **Safety**: Read-only operation
- **Output**: Lists existing fixtures in each component project
- **Use case**: Audit current fixture distribution

## Fixture Files

The following fixtures are propagated from Vault:

### Container Fixtures

- `AutomatedFullStackContainerFixture.cs` - Full orchestration (Qdrant + Neo4j + Ollama)
- `KnowledgeStoreContainerFixture.cs` - Knowledge store orchestration (Qdrant + Neo4j)
- `AutomatedQdrantContainerFixture.cs` - Automated Qdrant container
- `AutomatedNeo4jContainerFixture.cs` - Automated Neo4j container
- `AutomatedOllamaContainerFixture.cs` - Automated Ollama container
- `QdrantContainerFixture.cs` - Persistent Qdrant container
- `Neo4jContainerFixture.cs` - Persistent Neo4j container
- `OllamaContainerFixture.cs` - Persistent Ollama container

### Support Fixtures

- `FixtureEvents.cs` - Sample document change events
- `FixtureDocumentHashGenerator.cs` - Document hash generation for testing
- `FixturePolymorphicDocumentProcessor.cs` - Polymorphic document processing
- `FixtureGoogleDriveIngestionClient.cs` - Google Drive ingestion testing

## Target Components

Fixtures are propagated to the following components:

- **Cortex** - AI/LLM integration tests
- **Nexus** - Document processing integration tests
- **Signal** - Monitoring integration tests
- **Sentinel** - Security integration tests
- **Gatekeeper** - External API integration tests
- **Datastream** - Data persistence integration tests
- **Conduit** - Messaging integration tests
- **Helix** - Knowledge graph integration tests

## Namespace Transformation

The script automatically transforms namespaces:

**Source**: `namespace ExxerAI.Vault.Integration.Tests.Fixtures`

**Target**: `namespace ExxerAI.{Component}.Integration.Tests.Fixtures`

Example:
- Vault → Cortex: `ExxerAI.Cortex.Integration.Tests.Fixtures`
- Vault → Nexus: `ExxerAI.Nexus.Integration.Tests.Fixtures`

All `using` statements referencing the source namespace are also updated.

## Git Safety Cycle

When running with `--apply`, the script follows this git safety protocol:

### 1. Status Check

```bash
git status --porcelain
```

Checks for uncommitted changes. If found, prompts user for confirmation.

### 2. Add Changes

```bash
git add .
```

Stages all changes before creating safety commit.

### 3. Create Safety Commit

```bash
git commit -m "Fixture Propagation - Safety Commit

Automated safety commit before fixture propagation operation.
Timestamp: YYYYMMDD_HHMMSS
Operation: Propagate Vault fixtures to component test projects

🤖 Generated with Claude Code"
```

### 4. Optional Push

```bash
git push  # Only with --push flag
```

## Backup Strategy

### Backup Directory Structure

```
backups/
└── fixture_propagation_YYYYMMDD_HHMMSS/
    └── code/
        └── src/
            └── tests/
                └── 05IntegrationTests/
                    ├── ExxerAI.Cortex.Integration.Test/
                    │   └── Fixtures/
                    │       └── [backed up files]
                    ├── ExxerAI.Nexus.Integration.Test/
                    │   └── Fixtures/
                    │       └── [backed up files]
                    └── ...
```

### Backup Metadata

Each backup includes:
- Original file path (preserved in backup structure)
- SHA256 checksum (recorded in report)
- Timestamp (in backup directory name)

## Reporting

### JSON Report

**Location**: `scripts/propagation_report_YYYYMMDD_HHMMSS.json`

**Contents**:
```json
{
  "timestamp": "YYYYMMDD_HHMMSS",
  "mode": "apply",
  "base_path": "F:/Dynamic/ExxerAi/ExxerAI",
  "summary": {
    "total_mappings": 96,
    "successful": 94,
    "failed": 2,
    "components_updated": 8
  },
  "results": [
    {
      "success": true,
      "fixture_mapping": {
        "source_path": "...",
        "target_path": "...",
        "source_namespace": "...",
        "target_namespace": "...",
        "file_name": "...",
        "target_component": "Cortex"
      },
      "backup_path": "...",
      "checksum_before": "abc123...",
      "checksum_after": "def456..."
    }
  ],
  "errors": []
}
```

### Markdown Report

**Location**: `docs/reports/fixture_propagation_report_YYYYMMDD_HHMMSS.md`

**Contents**:
- Summary statistics
- Successful propagations (grouped by component)
- Failed propagations (with error messages)
- Complete fixture file list

## Error Handling

### Common Errors

#### 1. Source Fixture Not Found

**Error**: `Source fixture not found: {filename}`

**Solution**: Ensure fixture exists in `ExxerAI.Vault.Integration.Test/Fixtures/`

#### 2. Git Status Check Failed

**Error**: `Cannot proceed without clean git status check`

**Solution**: Ensure git is installed and repository is initialized

#### 3. Target Directory Creation Failed

**Error**: `Failed to create target directory`

**Solution**: Check file system permissions

#### 4. Namespace Transformation Failed

**Error**: `Failed to transform namespace`

**Solution**: Verify source file has correct namespace format

### Rollback Procedure

If you need to rollback changes:

1. **Identify backup directory**:
   ```
   backups/fixture_propagation_YYYYMMDD_HHMMSS/
   ```

2. **Restore files manually**:
   ```bash
   # Copy backed up files back to original locations
   cp -r backups/fixture_propagation_YYYYMMDD_HHMMSS/code/* code/
   ```

3. **Or use git**:
   ```bash
   # Revert to commit before propagation
   git reset --hard HEAD~1
   ```

## Advanced Usage

### Custom Base Path

If running from a different directory:

```bash
python propagate_vault_fixtures.py --apply --base-path "/path/to/ExxerAI"
```

### Integration with CI/CD

For automated pipelines:

```bash
# Non-interactive dry-run (for CI validation)
python propagate_vault_fixtures.py --dry-run

# Non-interactive apply (requires setting up auto-confirmation)
# NOT RECOMMENDED - use dry-run for validation instead
```

### Debugging

Enable verbose logging by modifying the script:

```python
# In _log method, add DEBUG level output
self._log(f"Debug info: {message}", "DEBUG")
```

## Limitations

1. **Manual namespace references**: Only transforms `namespace` declarations and `using` statements. Manual namespace references in comments or strings are not modified.

2. **Component detection**: Only processes components with existing integration test projects.

3. **Fixture selection**: Only propagates predefined fixture files (see FIXTURE_FILES list).

4. **One-way propagation**: Only propagates from Vault to components, not vice versa.

## Best Practices

### 1. Always Dry-Run First

```bash
# Step 1: Dry run to preview
python scripts/propagate_vault_fixtures.py --dry-run

# Step 2: Review output

# Step 3: Apply if satisfied
python scripts/propagate_vault_fixtures.py --apply
```

### 2. Review Reports

After propagation, review both JSON and Markdown reports to verify:
- Correct number of files propagated
- No unexpected failures
- Checksums changed (indicating successful writes)

### 3. Test After Propagation

```bash
# Build solution to check for errors
dotnet build

# Run tests to verify fixtures work
dotnet test
```

### 4. Commit Separately

The script creates a safety commit, but you should create a separate commit for the actual propagation:

```bash
git add code/src/tests/05IntegrationTests/
git commit -m "Propagate Vault fixtures to component integration tests

- Copied 12 fixture files to 8 components
- Transformed namespaces appropriately
- Verified file integrity

🤖 Generated with Claude Code"
```

## Troubleshooting

### Issue: "Vault test project not found"

**Cause**: Script cannot locate Vault integration test project

**Solution**:
1. Verify you're in the correct directory
2. Check path: `code/src/tests/05IntegrationTests/ExxerAI.Vault.Integration.Test/`
3. Use `--base-path` flag with absolute path

### Issue: "Git safety cycle failed"

**Cause**: Git command execution failed

**Solution**:
1. Ensure git is installed: `git --version`
2. Check repository is initialized: `git status`
3. Verify git configuration: `git config --list`

### Issue: "No fixture mappings found"

**Cause**: No target components detected or source fixtures missing

**Solution**:
1. Run with `--verify-only` to check existing components
2. Verify Vault fixtures exist in source directory
3. Check component integration test projects exist

## Support

For issues or questions:

1. Review this README
2. Check generated reports for detailed error messages
3. Examine backup directory structure
4. Consult CLAUDE.md for ExxerAI development guidelines

## License

Part of the ExxerAI project. All rights reserved.

---

🤖 Generated with Claude Code
