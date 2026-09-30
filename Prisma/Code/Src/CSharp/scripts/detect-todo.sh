#!/bin/bash

# Railguard Script: Detect TODO Comments
# This script scans for TODO comments in production code and fails if any are found

set -e

echo "🔍 Scanning for TODO comments in production code..."

# Count TODO comments in C# files, excluding build artifacts and documentation
TODO_COUNT=$(grep -r "TODO" . --include="*.cs" --exclude-dir=obj --exclude-dir=bin --exclude-dir=docs 2>/dev/null | wc -l || echo "0")

if [ "$TODO_COUNT" -gt 0 ]; then
    echo "❌ ERROR: Found $TODO_COUNT TODO comment(s) in production code"
    echo ""
    echo "TODO comments found:"
    grep -r "TODO" . --include="*.cs" --exclude-dir=obj --exclude-dir=bin --exclude-dir=docs || true
    echo ""
    echo "Please complete all implementations before merging to production."
    echo "If you need to leave a note for future work, use:"
    echo "  // NOTE: Future enhancement - [description]"
    echo "  // FIXME: Known issue - [description]"
    exit 1
fi

echo "✅ No TODO comments found in production code"
exit 0
