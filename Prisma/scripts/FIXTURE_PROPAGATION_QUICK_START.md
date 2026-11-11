# Fixture Propagation - Quick Start Guide

## 🚀 Quick Start (3 Steps)

### 1. Preview Changes (Safe - No Modifications)

```bash
python scripts/propagate_vault_fixtures.py --dry-run
```

**What happens**:
- ✅ Shows what files would be propagated
- ✅ Shows namespace transformations
- ✅ Generates preview reports
- ❌ Does NOT modify any files

### 2. Verify Current State

```bash
python scripts/propagate_vault_fixtures.py --verify-only
```

**What happens**:
- Shows which fixtures already exist in each component
- Read-only operation
- No git operations

### 3. Apply Changes (Modifies Files)

```bash
python scripts/propagate_vault_fixtures.py --apply
```

**What happens**:
- ⚠️ Prompts for confirmation
- Creates git safety commit
- Creates timestamped backups
- Propagates fixtures with namespace transformation
- Generates detailed reports

## 📋 Common Workflows

### Workflow 1: First-Time Propagation

```bash
# Step 1: Check current state
python scripts/propagate_vault_fixtures.py --verify-only

# Step 2: Preview what will happen
python scripts/propagate_vault_fixtures.py --dry-run

# Step 3: Apply changes
python scripts/propagate_vault_fixtures.py --apply

# Step 4: Verify build
dotnet build

# Step 5: Run tests
dotnet test
```

### Workflow 2: Update Existing Fixtures

```bash
# Step 1: Dry run to see what changed
python scripts/propagate_vault_fixtures.py --dry-run

# Step 2: Apply updates
python scripts/propagate_vault_fixtures.py --apply

# Step 3: Review changes
git diff

# Step 4: Create descriptive commit
git add .
git commit -m "Update component fixtures from Vault"
```

### Workflow 3: Safe Experimentation

```bash
# Step 1: Create feature branch
git checkout -b experiment/fixture-propagation

# Step 2: Apply changes
python scripts/propagate_vault_fixtures.py --apply

# Step 3: Test
dotnet test

# Step 4: If successful, merge
git checkout MeganV
git merge experiment/fixture-propagation

# Step 5: If unsuccessful, abandon
git checkout MeganV
git branch -D experiment/fixture-propagation
```

## 🎯 What Gets Propagated

### From
```
code/src/tests/05IntegrationTests/
└── ExxerAI.Vault.Integration.Test/
    └── Fixtures/
        ├── AutomatedFullStackContainerFixture.cs
        ├── KnowledgeStoreContainerFixture.cs
        ├── AutomatedQdrantContainerFixture.cs
        ├── AutomatedNeo4jContainerFixture.cs
        ├── AutomatedOllamaContainerFixture.cs
        ├── QdrantContainerFixture.cs
        ├── Neo4jContainerFixture.cs
        ├── OllamaContainerFixture.cs
        ├── FixtureEvents.cs
        ├── FixtureDocumentHashGenerator.cs
        ├── FixturePolymorphicDocumentProcessor.cs
        └── FixtureGoogleDriveIngestionClient.cs
```

### To
```
code/src/tests/05IntegrationTests/
├── ExxerAI.Cortex.Integration.Test/Fixtures/
├── ExxerAI.Nexus.Integration.Test/Fixtures/
├── ExxerAI.Signal.Integration.Test/Fixtures/
├── ExxerAI.Sentinel.Integration.Test/Fixtures/
├── ExxerAI.Gatekeeper.Integration.Test/Fixtures/
├── ExxerAI.Datastream.Integration.Test/Fixtures/
├── ExxerAI.Conduit.Integration.Test/Fixtures/
└── ExxerAI.Helix.Integration.Test/Fixtures/
```

## 🔄 Namespace Transformation

**Automatic transformation**:

```csharp
// SOURCE (Vault)
namespace ExxerAI.Vault.Integration.Tests.Fixtures
{
    public class AutomatedFullStackContainerFixture { }
}

// TARGET (Cortex)
namespace ExxerAI.Cortex.Integration.Tests.Fixtures
{
    public class AutomatedFullStackContainerFixture { }
}
```

## 📊 Reports Generated

### JSON Report
**Location**: `scripts/propagation_report_YYYYMMDD_HHMMSS.json`

**Use for**:
- Automated processing
- CI/CD integration
- Detailed analysis

### Markdown Report
**Location**: `docs/reports/fixture_propagation_report_YYYYMMDD_HHMMSS.md`

**Use for**:
- Human review
- Documentation
- Audit trail

## 🛡️ Safety Features

### ✅ Enabled by Default

- **Dry-run mode** - Must explicitly use `--apply`
- **User confirmation** - Prompts before modifying files
- **Git safety commit** - Automatic commit before changes
- **Timestamped backups** - All overwritten files backed up
- **Checksum verification** - Validates file integrity
- **Detailed logging** - Every operation logged

### 🚫 Manual Rollback

If something goes wrong:

```bash
# Option 1: Git reset (recommended)
git reset --hard HEAD~1

# Option 2: Manual restoration from backups
# Backups are in: backups/fixture_propagation_YYYYMMDD_HHMMSS/
```

## ⚠️ Important Notes

### Before Running

1. ✅ Ensure you're on the correct branch
2. ✅ Commit or stash any uncommitted work
3. ✅ Run dry-run first to preview changes
4. ✅ Review generated reports

### After Running

1. ✅ Review git diff to verify changes
2. ✅ Run `dotnet build` to check for errors
3. ✅ Run `dotnet test` to verify fixtures work
4. ✅ Create descriptive commit message

## 🔍 Verification Checklist

After propagation, verify:

- [ ] All target components received fixtures
- [ ] Namespaces correctly transformed
- [ ] No build errors introduced
- [ ] Tests still pass
- [ ] Reports show all successful
- [ ] Git history clean

## 💡 Pro Tips

### Tip 1: Use Verify-Only Regularly

```bash
# Quick check of fixture distribution
python scripts/propagate_vault_fixtures.py --verify-only
```

### Tip 2: Compare Reports

Save dry-run reports and compare with actual results:

```bash
# Dry run
python scripts/propagate_vault_fixtures.py --dry-run

# Apply
python scripts/propagate_vault_fixtures.py --apply

# Compare reports to verify consistency
```

### Tip 3: Test Single Component First

Modify script temporarily to test on one component:

```python
# In script, modify TARGET_COMPONENTS
TARGET_COMPONENTS = ["Cortex"]  # Test on Cortex only
```

## 🆘 Troubleshooting

### "No fixture mappings found"

**Cause**: Target components not detected

**Fix**:
```bash
# Check components exist
ls code/src/tests/05IntegrationTests/ExxerAI.*.Integration.Test/
```

### "Git safety cycle failed"

**Cause**: Uncommitted changes or git issues

**Fix**:
```bash
# Check status
git status

# Commit or stash changes
git stash
# or
git add . && git commit -m "WIP"
```

### "Source fixture not found"

**Cause**: Missing fixture in Vault

**Fix**:
```bash
# Verify Vault fixtures
ls code/src/tests/05IntegrationTests/ExxerAI.Vault.Integration.Test/Fixtures/
```

## 📚 Additional Resources

- **Full Documentation**: `scripts/PROPAGATE_VAULT_FIXTURES_README.md`

---

🤖 Generated with Claude Code
