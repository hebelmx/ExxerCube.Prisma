#!/bin/bash

# Railguard Script: Detect Placeholder Implementations
# This script scans for placeholder implementations and fails if any are found

set -e

echo "🔍 Scanning for placeholder implementations..."

# Define patterns that indicate placeholder implementations
declare -a PATTERNS=(
    "return.*Success.*placeholder"
    "return.*Success.*static"
    "return.*Success.*hardcoded"
    "return.*Success.*EXP-2024-001"
    "return.*Success.*Civil"
    "return.*Success.*Compensación"
    "return.*Success.*2024-01-15"
    "return.*Success.*1000.00m"
    "return.*Success.*MXN"
    "Task\.FromResult.*Success.*placeholder"
    "Task\.FromResult.*Success.*static"
    "Task\.FromResult.*Success.*hardcoded"
)

FOUND_PLACEHOLDERS=false

for pattern in "${PATTERNS[@]}"; do
    # Search for the pattern in C# files, excluding build artifacts
    MATCHES=$(grep -r "$pattern" . --include="*.cs" --exclude-dir=obj --exclude-dir=bin 2>/dev/null || true)
    
    if [ -n "$MATCHES" ]; then
        if [ "$FOUND_PLACEHOLDERS" = false ]; then
            echo "❌ ERROR: Placeholder implementations detected:"
            echo ""
            FOUND_PLACEHOLDERS=true
        fi
        
        echo "Pattern: $pattern"
        echo "$MATCHES"
        echo ""
    fi
done

if [ "$FOUND_PLACEHOLDERS" = true ]; then
    echo "Please implement production functionality instead of using placeholders."
    echo "All integrations should use actual Python modules and production data."
    exit 1
fi

echo "✅ No placeholder implementations found"
exit 0
