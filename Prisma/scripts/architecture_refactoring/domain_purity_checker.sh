#!/bin/bash
# Domain Purity Checker Script
# Validates that the Domain layer maintains architectural purity

set -e

echo "🔍 Checking Domain Layer Purity..."

# Check for EF Core attributes
EF_ATTRIBUTES=$(find "code/src/Core/ExxerAI.Domain" -name "*.cs" -exec grep -l "\[Key\]\|\[MaxLength\]\|\[Required\]" {} \; 2>/dev/null || true)
if [ -n "$EF_ATTRIBUTES" ]; then
    echo "❌ FAIL: EF Core attributes found in Domain layer:"
    echo "$EF_ATTRIBUTES"
    exit 1
fi

# Check for infrastructure dependencies
INFRA_DEPS=$(dotnet list "code/src/Core/ExxerAI.Domain/ExxerAI.Domain.csproj" package 2>/dev/null | grep -E "(EntityFramework|AspNetCore|Google.Apis)" || true)
if [ -n "$INFRA_DEPS" ]; then
    echo "❌ FAIL: Infrastructure dependencies found in Domain:"
    echo "$INFRA_DEPS"
    exit 1
fi

# Check for logging dependencies
LOGGING_DEPS=$(dotnet list "code/src/Core/ExxerAI.Domain/ExxerAI.Domain.csproj" package 2>/dev/null | grep -i logging || true)
if [ -n "$LOGGING_DEPS" ]; then
    echo "❌ FAIL: Logging dependencies found in Domain:"
    echo "$LOGGING_DEPS"
    exit 1
fi

# Check for only approved dependencies
APPROVED_DEPS=$(dotnet list "code/src/Core/ExxerAI.Domain/ExxerAI.Domain.csproj" package 2>/dev/null | grep -E "(IndQuestResults|MathNet.Numerics|System.Private.CoreLib)" || true)
ALL_DEPS=$(dotnet list "code/src/Core/ExxerAI.Domain/ExxerAI.Domain.csproj" package 2>/dev/null | grep -v "Package" | grep -v "---" | wc -l)
if [ "$ALL_DEPS" -gt 3 ]; then
    echo "❌ FAIL: Too many dependencies in Domain layer"
    echo "Expected: IndQuestResults, MathNet.Numerics, System.Private.CoreLib"
    echo "Found: $ALL_DEPS dependencies"
    exit 1
fi

echo "✅ SUCCESS: Domain layer is pure"
exit 0

