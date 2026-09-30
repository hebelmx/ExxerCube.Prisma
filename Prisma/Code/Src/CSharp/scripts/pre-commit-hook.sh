#!/bin/bash

# Pre-commit Hook: Quality Railguards
# This hook runs before each commit to ensure code quality

set -e

echo "🔍 Running pre-commit quality checks..."

# Get the list of staged C# files
STAGED_CS_FILES=$(git diff --cached --name-only --diff-filter=ACM | grep '\.cs$' || true)

if [ -z "$STAGED_CS_FILES" ]; then
    echo "✅ No C# files staged for commit"
    exit 0
fi

echo "📁 Checking staged C# files:"
echo "$STAGED_CS_FILES"
echo ""

# Check for TODO comments in staged files
TODO_FOUND=false
for file in $STAGED_CS_FILES; do
    if git diff --cached "$file" | grep -q "TODO"; then
        if [ "$TODO_FOUND" = false ]; then
            echo "❌ TODO comments found in staged files:"
            TODO_FOUND=true
        fi
        echo "  - $file"
    fi
done

if [ "$TODO_FOUND" = true ]; then
    echo ""
    echo "Please complete all implementations before committing."
    echo "If you need to leave a note for future work, use:"
    echo "  // NOTE: Future enhancement - [description]"
    echo "  // FIXME: Known issue - [description]"
    exit 1
fi

# Check for placeholder patterns in staged files
declare -a PLACEHOLDER_PATTERNS=(
    "return.*Success.*placeholder"
    "return.*Success.*static"
    "return.*Success.*hardcoded"
    "return.*Success.*EXP-2024-001"
    "return.*Success.*Civil"
    "return.*Success.*Compensación"
    "return.*Success.*2024-01-15"
    "return.*Success.*1000.00m"
    "Task\.FromResult.*Success.*placeholder"
)

PLACEHOLDER_FOUND=false
for pattern in "${PLACEHOLDER_PATTERNS[@]}"; do
    for file in $STAGED_CS_FILES; do
        if git diff --cached "$file" | grep -q "$pattern"; then
            if [ "$PLACEHOLDER_FOUND" = false ]; then
                echo "❌ Placeholder implementations found in staged files:"
                PLACEHOLDER_FOUND=true
            fi
            echo "  - $file (pattern: $pattern)"
        fi
    done
done

if [ "$PLACEHOLDER_FOUND" = true ]; then
    echo ""
    echo "Please implement production functionality instead of using placeholders."
    echo "All integrations should use actual Python modules and production data."
    exit 1
fi

# Check for NSubstitute usage in integration tests
NSUBSTITUTE_FOUND=false
for file in $STAGED_CS_FILES; do
    if [[ "$file" == Tests/* ]] && git diff --cached "$file" | grep -q "NSubstitute"; then
        if [ "$NSUBSTITUTE_FOUND" = false ]; then
            echo "❌ NSubstitute found in integration tests:"
            NSUBSTITUTE_FOUND=true
        fi
        echo "  - $file"
    fi
done

if [ "$NSUBSTITUTE_FOUND" = true ]; then
    echo ""
    echo "Integration tests must use production modules, not placeholders."
    echo "Please replace NSubstitute with actual Python module calls."
    exit 1
fi

# Check for missing XML documentation in public APIs
MISSING_DOCS=false
for file in $STAGED_CS_FILES; do
    # Check if file contains public classes/methods without XML documentation
    if git diff --cached "$file" | grep -q "public.*class\|public.*interface\|public.*method" && \
       ! git diff --cached "$file" | grep -q "/// <summary>"; then
        if [ "$MISSING_DOCS" = false ]; then
            echo "⚠️  Missing XML documentation in public APIs:"
            MISSING_DOCS=true
        fi
        echo "  - $file"
    fi
done

if [ "$MISSING_DOCS" = true ]; then
    echo ""
    echo "Please add XML documentation for all public APIs."
    echo "Example:"
    echo "  /// <summary>"
    echo "  /// Description of the method."
    echo "  /// </summary>"
    echo "  /// <param name=\"param\">Parameter description.</param>"
    echo "  /// <returns>Return value description.</returns>"
    # Don't exit 1 for missing docs, just warn
fi

echo "✅ Pre-commit checks passed"
echo "🚀 Ready to commit!"
exit 0
