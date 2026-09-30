#!/bin/bash
# Emergency Rollback Script
# Provides emergency rollback capabilities for architecture refactoring

set -e

TASK_ID=$1
CHECKPOINT_NAME="session_*_${TASK_ID}"

echo "🚨 EMERGENCY ROLLBACK for Task: $TASK_ID"

# Find latest checkpoint for this task
LATEST_CHECKPOINT=$(ls -t checkpoints/ 2>/dev/null | grep "$CHECKPOINT_NAME" | head -1 || true)

if [ -z "$LATEST_CHECKPOINT" ]; then
    echo "❌ No checkpoint found for task $TASK_ID"
    echo "Attempting full rollback to last known good state..."
    
    # Check if we're in a git repository
    if [ -d ".git" ]; then
        echo "🔄 Restoring from git..."
        git reset --hard HEAD
        git clean -fd
        echo "✅ Git rollback complete"
    else
        echo "❌ No git repository found and no checkpoints available"
        echo "Manual intervention required"
        exit 1
    fi
else
    echo "🔄 Restoring from checkpoint: $LATEST_CHECKPOINT"
    ./checkpoint_manager.sh restore "$LATEST_CHECKPOINT"
    echo "✅ Checkpoint rollback complete"
fi

# Verify rollback success
echo "🔍 Verifying rollback..."
if dotnet build "code/src/ExxerAI.sln" --verbosity quiet >/dev/null 2>&1; then
    echo "✅ Rollback verification successful"
    
    # Run tests to ensure everything is working
    echo "🧪 Running test suite..."
    if dotnet test "code/src" --verbosity quiet --no-build >/dev/null 2>&1; then
        echo "✅ Test suite passing after rollback"
    else
        echo "⚠️  Test suite failing after rollback - manual intervention may be required"
    fi
else
    echo "❌ Rollback verification failed - manual intervention required"
    exit 1
fi

echo "🎉 Emergency rollback completed successfully"

