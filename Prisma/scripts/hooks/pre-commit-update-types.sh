#!/usr/bin/env bash
# Pre-commit hook to auto-update type database
# Keeps exxerai_types.json always current with codebase changes
#
# Installation:
#   cp scripts/hooks/pre-commit-update-types.sh .git/hooks/pre-commit
#   chmod +x .git/hooks/pre-commit
#
# Author: Claude Code Agent
# Date: 2025-11-08

set -e

echo "🔍 Pre-commit: Checking for C# file changes..."

# Check if any .cs files were modified
CS_FILES_CHANGED=$(git diff --cached --name-only --diff-filter=ACM | grep '\.cs$' || true)

if [ -z "$CS_FILES_CHANGED" ]; then
    echo "✅ No C# files changed, skipping type database update"
    exit 0
fi

echo "📝 C# files changed, updating type database..."

# Get repository root
REPO_ROOT=$(git rev-parse --show-toplevel)
cd "$REPO_ROOT"

# Check if Python is available
if ! command -v python &> /dev/null && ! command -v python3 &> /dev/null; then
    echo "⚠️  Warning: Python not found, skipping type database update"
    exit 0
fi

# Use python3 if available, otherwise python
PYTHON_CMD=$(command -v python3 || command -v python)

# Update type database
echo "🔄 Running type scanner..."
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
OUTPUT_FILE="scripts/exxerai_types_${TIMESTAMP}.json"

if $PYTHON_CMD scripts/scan_exxerai_types.py --base-path . --output "$OUTPUT_FILE" 2>&1 | grep -q "Scan complete"; then
    echo "✅ Type database updated: $OUTPUT_FILE"

    # Create/update symlink to latest
    cd scripts
    rm -f exxerai_types_latest.json
    ln -sf "$(basename "$OUTPUT_FILE")" exxerai_types_latest.json

    # Add the new JSON file to the commit
    git add "$OUTPUT_FILE" exxerai_types_latest.json

    # Clean up old JSON files (keep last 5)
    cd "$REPO_ROOT/scripts"
    ls -t exxerai_types_*.json | tail -n +6 | xargs rm -f 2>/dev/null || true

    echo "✅ Type database staged for commit"
else
    echo "⚠️  Warning: Type scanner failed, continuing with commit"
fi

exit 0
