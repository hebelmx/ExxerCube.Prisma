#!/bin/bash

# Railguard Script: Validate Integration Tests
# This script ensures integration tests use production modules instead of placeholders

set -e

echo "🔍 Validating integration tests..."

# Check for NSubstitute usage in integration tests (excluding comments)
NSUBSTITUTE_MATCHES=$(grep -r "NSubstitute" Tests/ --include="*.cs" 2>/dev/null | grep -v "//.*NSubstitute" | grep -v "///.*NSubstitute" || true)

if [ -n "$NSUBSTITUTE_MATCHES" ]; then
    echo "❌ ERROR: NSubstitute found in integration tests"
    echo ""
    echo "NSubstitute usage found:"
    echo "$NSUBSTITUTE_MATCHES"
    echo ""
    echo "Integration tests must use production modules, not placeholders."
    echo "Please replace NSubstitute with actual Python module calls."
    exit 1
fi

# Check for integration test coverage
INTEGRATION_TESTS=$(find Tests/ -name "*.cs" -exec grep -l "Category.*Integration" {} \; 2>/dev/null | wc -l || echo "0")

if [ "$INTEGRATION_TESTS" -eq 0 ]; then
    echo "⚠️  WARNING: No integration tests found"
    echo "   Consider adding integration tests for production integrations"
    echo "   Integration tests should use: [Trait(\"Category\", \"Integration\")]"
else
    echo "✅ Found $INTEGRATION_TESTS integration test(s)"
fi

# Check for placeholder patterns in integration tests
declare -a PLACEHOLDER_PATTERNS=(
    "return.*Success.*placeholder"
    "return.*Success.*static"
    "return.*Success.*hardcoded"
    "Task\.FromResult.*Success.*placeholder"
)

FOUND_PLACEHOLDERS_IN_TESTS=false

for pattern in "${PLACEHOLDER_PATTERNS[@]}"; do
    MATCHES=$(grep -r "$pattern" Tests/ --include="*.cs" 2>/dev/null || true)
    
    if [ -n "$MATCHES" ]; then
        if [ "$FOUND_PLACEHOLDERS_IN_TESTS" = false ]; then
            echo "❌ ERROR: Placeholder implementations found in integration tests:"
            echo ""
            FOUND_PLACEHOLDERS_IN_TESTS=true
        fi
        
        echo "Pattern: $pattern"
        echo "$MATCHES"
        echo ""
    fi
done

if [ "$FOUND_PLACEHOLDERS_IN_TESTS" = true ]; then
    echo "Integration tests should use production modules, not placeholders."
    echo "Please implement actual integration testing with Python modules."
    exit 1
fi

echo "✅ Integration tests validated successfully"
exit 0
