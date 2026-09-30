# ExxerAI Architecture Refactoring Scripts

This directory contains all the scripts and tools needed to execute the ExxerAI architecture refactoring plan.

## Overview

The refactoring plan transforms ExxerAI from its current architectural violations to a pristine hexagonal architecture through a series of micro-tasks with comprehensive safety mechanisms.

## Scripts

### Core Execution Scripts

- **`master_execution_script.sh`** - Main orchestration script for executing micro-tasks
- **`checkpoint_manager.sh`** - Manages checkpoints for rollback capabilities
- **`emergency_rollback.sh`** - Emergency rollback procedures

### Validation Scripts

- **`domain_purity_checker.sh`** - Validates Domain layer architectural purity
- **`application_cleanliness_checker.sh`** - Validates Application layer cleanliness
- **`contracts_migration_checker.sh`** - Validates DTO migration to Contracts

### Micro-Task Scripts

- **`micro_task_1_1.sh`** - Remove EF Core attributes from ProcessingQueue
- **`micro_task_1_2.sh`** - Create EF Core configuration for ProcessingQueue
- **`micro_task_2_1.sh`** - Create ExxerAI.Contracts project
- **`micro_task_2_2.sh`** - Migrate SystemMetrics DTO
- **`micro_task_3_1.sh`** - Remove Google APIs from Application

## Usage

### Prerequisites

1. Ensure you're in the ExxerAI root directory
2. Make all scripts executable: `chmod +x scripts/architecture_refactoring/*.sh`
3. Ensure .NET 10 SDK is installed
4. Ensure all tests are passing: `dotnet test code/src`

### Basic Execution

```bash
# Execute a micro-task
./scripts/architecture_refactoring/master_execution_script.sh <phase> <task_id> [dry_run]

# Examples:
./scripts/architecture_refactoring/master_execution_script.sh 1 1 false
./scripts/architecture_refactoring/master_execution_script.sh 2 1 true  # Dry run
```

### Validation

```bash
# Check domain purity
./scripts/architecture_refactoring/domain_purity_checker.sh

# Check application cleanliness
./scripts/architecture_refactoring/application_cleanliness_checker.sh

# Check contracts migration
./scripts/architecture_refactoring/contracts_migration_checker.sh
```

### Checkpoint Management

```bash
# Create a checkpoint
./scripts/architecture_refactoring/checkpoint_manager.sh create <task_id>

# List checkpoints
./scripts/architecture_refactoring/checkpoint_manager.sh list

# Restore from checkpoint
./scripts/architecture_refactoring/checkpoint_manager.sh restore <checkpoint_name>

# Cleanup old checkpoints
./scripts/architecture_refactoring/checkpoint_manager.sh cleanup [days]
```

### Emergency Procedures

```bash
# Emergency rollback
./scripts/architecture_refactoring/emergency_rollback.sh <task_id>
```

## Safety Features

### Automated Validation
- Pre-execution environment validation
- Post-task completion validation
- Compilation verification
- Test suite validation

### Checkpoint System
- Automatic checkpoint creation after each task
- Session-safe execution with recovery capabilities
- Metadata tracking for each checkpoint

### Rollback Mechanisms
- Individual task rollback
- Full session rollback
- Git-based rollback as fallback
- Automated verification after rollback

## Architecture Phases

### Phase 1: Domain Purity (12 micro-tasks)
- Remove EF Core attributes from domain entities
- Remove logging dependencies from domain
- Create EF Core configurations
- Extract domain events

### Phase 2: DTO Migration (47 micro-tasks)
- Create Contracts project
- Migrate DTOs systematically
- Update all consumers
- Validate migration completeness

### Phase 3: Infrastructure Relocation (18 micro-tasks)
- Remove infrastructure packages from Application
- Create infrastructure adapter projects
- Implement port/adapter patterns
- Update service registrations

## Risk Mitigation

### Low-Risk Tasks
- Configuration file creation
- Project structure creation
- Documentation updates

### Medium-Risk Tasks
- DTO migration (with bridge patterns)
- Interface updates
- Service registration changes

### High-Risk Tasks
- Infrastructure package removal
- Consumer reference updates
- Breaking API changes

## Success Criteria

- ✅ Zero compilation errors
- ✅ Zero compilation warnings
- ✅ 100% test suite passing
- ✅ Domain layer purity validated
- ✅ Application layer cleanliness validated
- ✅ All DTOs migrated to Contracts
- ✅ All infrastructure packages relocated

## Troubleshooting

### Common Issues

1. **Compilation Failures**
   - Check for missing using statements
   - Verify project references
   - Run emergency rollback if needed

2. **Test Failures**
   - Check for broken references
   - Verify DTO migrations
   - Update test data if needed

3. **Checkpoint Issues**
   - Verify checkpoint directory permissions
   - Check available disk space
   - Use git-based rollback as fallback

### Getting Help

1. Check the validation scripts for specific error messages
2. Review the backup files in the `backup/` directory
3. Use the emergency rollback procedures
4. Consult the detailed architecture audit document

## Best Practices

1. **Always run dry-run first** for high-risk tasks
2. **Create checkpoints** before each major change
3. **Validate after each task** using the validation scripts
4. **Keep backups** of critical files
5. **Test thoroughly** after each phase completion

## Monitoring

- Monitor compilation status after each task
- Track test suite results
- Watch for performance regressions
- Validate architecture boundaries

This script collection provides a comprehensive, safe, and automated approach to executing the ExxerAI architecture refactoring with minimal risk and maximum reliability.

