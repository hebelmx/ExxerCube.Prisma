#!/bin/bash
# Master Execution Script for Architecture Refactoring
# Orchestrates the execution of micro-tasks with safety checks

set -e

# Configuration
PHASE=$1
TASK_ID=$2
DRY_RUN=${3:-false}

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

log() {
    echo -e "${BLUE}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1"
}

success() {
    echo -e "${GREEN}✅ $1${NC}"
}

warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

error() {
    echo -e "${RED}❌ $1${NC}"
}

# Pre-execution validation
validate_environment() {
    log "Validating environment..."
    
    # Check if we're in the right directory
    if [ ! -f "code/src/ExxerAI.sln" ]; then
        error "Not in ExxerAI root directory"
        exit 1
    fi
    
    # Check if git is clean
    if [ -n "$(git status --porcelain 2>/dev/null)" ]; then
        warning "Git working directory is not clean"
        if [ "$DRY_RUN" = "false" ]; then
            read -p "Continue anyway? (y/N): " -n 1 -r
            echo
            if [[ ! $REPLY =~ ^[Yy]$ ]]; then
                exit 1
            fi
        fi
    fi
    
    # Check if all tests pass
    log "Running test suite..."
    if ! dotnet test "code/src" --verbosity quiet --no-build >/dev/null 2>&1; then
        error "Test suite is not passing"
        exit 1
    fi
    
    success "Environment validation complete"
}

# Create backup
create_backup() {
    log "Creating backup..."
    mkdir -p "backup/$(date +%Y%m%d_%H%M%S)"
    cp -r "code/src" "backup/$(date +%Y%m%d_%H%M%S)/"
    success "Backup created"
}

# Execute micro-task
execute_micro_task() {
    local phase=$1
    local task_id=$2
    
    log "Executing Phase $phase, Task $task_id"
    
    # Create checkpoint
    ./checkpoint_manager.sh create "${phase}_${task_id}"
    
    # Execute task-specific script
    local script_name="micro_task_${phase}_${task_id}.sh"
    if [ -f "$script_name" ]; then
        if [ "$DRY_RUN" = "true" ]; then
            log "DRY RUN: Would execute $script_name"
        else
            chmod +x "$script_name"
            ./"$script_name"
        fi
    else
        error "Task script not found: $script_name"
        exit 1
    fi
    
    # Validate task completion
    validate_task_completion "$phase" "$task_id"
    
    success "Phase $phase, Task $task_id completed successfully"
}

# Validate task completion
validate_task_completion() {
    local phase=$1
    local task_id=$2
    
    log "Validating task completion..."
    
    case "$phase" in
        "1")
            chmod +x ./domain_purity_checker.sh
            ./domain_purity_checker.sh
            ;;
        "2")
            chmod +x ./contracts_migration_checker.sh
            ./contracts_migration_checker.sh
            ;;
        "3")
            chmod +x ./application_cleanliness_checker.sh
            ./application_cleanliness_checker.sh
            ;;
        *)
            error "Unknown phase: $phase"
            exit 1
            ;;
    esac
    
    success "Task validation complete"
}

# Main execution
main() {
    log "Starting ExxerAI Architecture Refactoring"
    log "Phase: $PHASE, Task: $TASK_ID, Dry Run: $DRY_RUN"
    
    if [ -z "$PHASE" ] || [ -z "$TASK_ID" ]; then
        error "Usage: $0 <phase> <task_id> [dry_run]"
        echo "Example: $0 1 1 false"
        exit 1
    fi
    
    validate_environment
    create_backup
    
    if [ "$DRY_RUN" = "false" ]; then
        execute_micro_task "$PHASE" "$TASK_ID"
    else
        log "DRY RUN MODE: No changes will be made"
    fi
    
    success "Execution complete"
}

# Run main function
main "$@"

