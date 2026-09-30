#!/bin/bash
# Application Cleanliness Checker Script
# Validates that the Application layer maintains architectural cleanliness

set -e

echo "🔍 Checking Application Layer Cleanliness..."

# Check for infrastructure packages
INFRA_PACKAGES=$(dotnet list "code/src/Core/ExxerAI.Application/ExxerAI.Application.csproj" package 2>/dev/null | grep -E "(Google.Apis|PdfPig|SignalR|OllamaSharp|DocumentFormat.OpenXml|NPOI|SixLabors.ImageSharp)" || true)
if [ -n "$INFRA_PACKAGES" ]; then
    echo "❌ FAIL: Infrastructure packages found in Application:"
    echo "$INFRA_PACKAGES"
    exit 1
fi

# Check for DTOs in Application layer
DTO_COUNT=$(find "code/src/Core/ExxerAI.Application/DTOs" -name "*.cs" 2>/dev/null | wc -l)
if [ "$DTO_COUNT" -gt 0 ]; then
    echo "❌ FAIL: $DTO_COUNT DTOs still in Application layer"
    echo "All DTOs should be moved to Contracts project"
    exit 1
fi

# Check for infrastructure-specific interfaces
INFRA_INTERFACES=$(find "code/src/Core/ExxerAI.Application/Interfaces" -name "*.cs" -exec grep -l "Google.Apis\|PdfPig\|SignalR" {} \; 2>/dev/null || true)
if [ -n "$INFRA_INTERFACES" ]; then
    echo "❌ FAIL: Infrastructure-specific interfaces found in Application:"
    echo "$INFRA_INTERFACES"
    exit 1
fi

# Check for only approved dependencies
APPROVED_DEPS="Microsoft.Extensions.Logging|Microsoft.Extensions.DependencyInjection|Microsoft.Extensions.Hosting|Microsoft.Extensions.Caching.Memory|IndQuestResults"
UNAPPROVED_DEPS=$(dotnet list "code/src/Core/ExxerAI.Application/ExxerAI.Application.csproj" package 2>/dev/null | grep -v -E "($APPROVED_DEPS|Package|---)" || true)
if [ -n "$UNAPPROVED_DEPS" ]; then
    echo "❌ FAIL: Unapproved dependencies found in Application:"
    echo "$UNAPPROVED_DEPS"
    exit 1
fi

echo "✅ SUCCESS: Application layer is clean"
exit 0

