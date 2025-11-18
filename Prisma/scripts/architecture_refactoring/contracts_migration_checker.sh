#!/bin/bash
# Contracts Migration Checker Script
# Validates that DTOs have been properly migrated to Contracts project

set -e

echo "🔍 Checking Contracts Migration..."

# Check Contracts project exists
if [ ! -f "code/src/Contracts/ExxerAI.Contracts/ExxerAI.Contracts.csproj" ]; then
    echo "❌ FAIL: Contracts project not found"
    exit 1
fi

# Check expected DTO count (47 DTOs identified in audit)
EXPECTED_DTO_COUNT=47
ACTUAL_DTO_COUNT=$(find "code/src/Contracts/ExxerAI.Contracts" -name "*Dto.cs" 2>/dev/null | wc -l)
if [ "$ACTUAL_DTO_COUNT" -ne "$EXPECTED_DTO_COUNT" ]; then
    echo "❌ FAIL: Expected $EXPECTED_DTO_COUNT DTOs, found $ACTUAL_DTO_COUNT"
    echo "Missing DTOs: $((EXPECTED_DTO_COUNT - ACTUAL_DTO_COUNT))"
    exit 1
fi

# Check for broken references to Application.DTOs
BROKEN_REFS=$(grep -r "ExxerAI.Application.DTOs" "code/src" --exclude-dir=backup --exclude-dir=checkpoints 2>/dev/null || true)
if [ -n "$BROKEN_REFS" ]; then
    echo "❌ FAIL: Broken references to Application.DTOs found:"
    echo "$BROKEN_REFS"
    exit 1
fi

# Check that all DTOs have proper namespace
WRONG_NAMESPACE=$(find "code/src/Contracts/ExxerAI.Contracts" -name "*.cs" -exec grep -l "namespace ExxerAI.Application.DTOs" {} \; 2>/dev/null || true)
if [ -n "$WRONG_NAMESPACE" ]; then
    echo "❌ FAIL: DTOs with wrong namespace found:"
    echo "$WRONG_NAMESPACE"
    exit 1
fi

# Check that all DTOs have "Dto" suffix
MISSING_SUFFIX=$(find "code/src/Contracts/ExxerAI.Contracts" -name "*.cs" -exec grep -l "public class.*[^D]to" {} \; 2>/dev/null || true)
if [ -n "$MISSING_SUFFIX" ]; then
    echo "❌ FAIL: DTOs missing 'Dto' suffix found:"
    echo "$MISSING_SUFFIX"
    exit 1
fi

# Check that Contracts project compiles
if ! dotnet build "code/src/Contracts/ExxerAI.Contracts/ExxerAI.Contracts.csproj" --verbosity quiet >/dev/null 2>&1; then
    echo "❌ FAIL: Contracts project does not compile"
    exit 1
fi

echo "✅ SUCCESS: Contracts migration complete"
echo "📊 Statistics:"
echo "   - DTOs migrated: $ACTUAL_DTO_COUNT/$EXPECTED_DTO_COUNT"
echo "   - Broken references: 0"
echo "   - Compilation status: ✅"
exit 0

