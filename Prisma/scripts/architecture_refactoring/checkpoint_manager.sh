#!/bin/bash
# Checkpoint Manager Script
# Manages checkpoints for architecture refactoring tasks

set -e

CHECKPOINT_DIR="checkpoints"
CURRENT_SESSION="session_$(date +%Y%m%d_%H%M%S)"

create_checkpoint() {
    local task_id=$1
    local checkpoint_name="${CURRENT_SESSION}_${task_id}"
    
    echo "💾 Creating checkpoint: $checkpoint_name"
    
    mkdir -p "$CHECKPOINT_DIR/$checkpoint_name"
    
    # Save current state
    cp -r "code/src" "$CHECKPOINT_DIR/$checkpoint_name/" 2>/dev/null || true
    cp "code/src/ExxerAI.sln" "$CHECKPOINT_DIR/$checkpoint_name/" 2>/dev/null || true
    
    # Save metadata
    cat > "$CHECKPOINT_DIR/$checkpoint_name/metadata.json" << EOF
{
    "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
    "task_id": "$task_id",
    "session_id": "$CURRENT_SESSION",
    "git_commit": "$(git rev-parse HEAD 2>/dev/null || echo 'unknown')",
    "build_status": "$(dotnet build code/src/ExxerAI.sln --verbosity quiet >/dev/null 2>&1 && echo 'success' || echo 'failed')"
}
EOF
    
    echo "✅ Checkpoint created: $checkpoint_name"
}

restore_checkpoint() {
    local checkpoint_name=$1
    
    if [ ! -d "$CHECKPOINT_DIR/$checkpoint_name" ]; then
        echo "❌ Checkpoint not found: $checkpoint_name"
        exit 1
    fi
    
    echo "🔄 Restoring checkpoint: $checkpoint_name"
    
    # Restore state
    rm -rf "code/src"
    cp -r "$CHECKPOINT_DIR/$checkpoint_name/code/src" .
    cp "$CHECKPOINT_DIR/$checkpoint_name/ExxerAI.sln" "code/src/" 2>/dev/null || true
    
    echo "✅ Checkpoint restored: $checkpoint_name"
}

list_checkpoints() {
    echo "📋 Available checkpoints:"
    if [ -d "$CHECKPOINT_DIR" ]; then
        ls -la "$CHECKPOINT_DIR" 2>/dev/null || echo "No checkpoints found"
    else
        echo "No checkpoints found"
    fi
}

cleanup_old_checkpoints() {
    local days_to_keep=${1:-7}
    echo "🧹 Cleaning up checkpoints older than $days_to_keep days..."
    
    if [ -d "$CHECKPOINT_DIR" ]; then
        find "$CHECKPOINT_DIR" -type d -mtime +$days_to_keep -exec rm -rf {} \; 2>/dev/null || true
        echo "✅ Cleanup complete"
    else
        echo "No checkpoints to clean up"
    fi
}

case "$1" in
    "create")
        if [ -z "$2" ]; then
            echo "Usage: $0 create <task_id>"
            exit 1
        fi
        create_checkpoint "$2"
        ;;
    "restore")
        if [ -z "$2" ]; then
            echo "Usage: $0 restore <checkpoint_name>"
            exit 1
        fi
        restore_checkpoint "$2"
        ;;
    "list")
        list_checkpoints
        ;;
    "cleanup")
        cleanup_old_checkpoints "$2"
        ;;
    *)
        echo "Usage: $0 {create|restore|list|cleanup} [options]"
        echo ""
        echo "Commands:"
        echo "  create <task_id>     Create a checkpoint for the given task"
        echo "  restore <name>       Restore from the given checkpoint"
        echo "  list                 List all available checkpoints"
        echo "  cleanup [days]       Clean up checkpoints older than specified days (default: 7)"
        exit 1
        ;;
esac

