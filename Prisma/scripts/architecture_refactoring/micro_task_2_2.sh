#!/bin/bash
# Micro-Task 2.2: Migrate SystemMetrics DTO (HIGH PRIORITY)
# Duration: 60 minutes
# Risk Level: HIGH
# Dependencies: Micro-Task 2.1
# Rollback Time: 10 minutes

set -e

echo "🔧 Micro-Task 2.2: Migrate SystemMetrics DTO"

# Step 1: Create backup
echo "📋 Creating backup..."
if [ -f "code/src/Core/ExxerAI.Application/DTOs/SystemMetrics.cs" ]; then
    cp "code/src/Core/ExxerAI.Application/DTOs/SystemMetrics.cs" "backup/SystemMetrics.cs.backup"
else
    echo "❌ SystemMetrics.cs not found in Application/DTOs"
    exit 1
fi

# Step 2: Copy to Contracts with new name
echo "📝 Copying to Contracts..."
cp "code/src/Core/ExxerAI.Application/DTOs/SystemMetrics.cs" "code/src/Contracts/ExxerAI.Contracts/System/SystemMetricsDto.cs"

# Step 3: Update namespace and class name
echo "📝 Updating namespace and class name..."
sed -i 's/namespace ExxerAI.Application.DTOs/namespace ExxerAI.Contracts.System/' "code/src/Contracts/ExxerAI.Contracts/System/SystemMetricsDto.cs"
sed -i 's/public class SystemMetrics/public class SystemMetricsDto/' "code/src/Contracts/ExxerAI.Contracts/System/SystemMetricsDto.cs"

# Step 4: Update all consumers (15 files identified)
echo "🔄 Updating consumers..."
find "code/src" -name "*.cs" -exec grep -l "SystemMetrics" {} \; | while read file; do
    echo "   Updating $file"
    # Update using statements
    sed -i 's/using ExxerAI.Application.DTOs;/using ExxerAI.Contracts.System;/' "$file"
    # Update type references
    sed -i 's/SystemMetrics/SystemMetricsDto/g' "$file"
done

# Step 5: Remove original file
echo "🗑️  Removing original file..."
rm "code/src/Core/ExxerAI.Application/DTOs/SystemMetrics.cs"

# Step 6: Validate migration
echo "🔍 Validating migration..."
if [ ! -f "code/src/Contracts/ExxerAI.Contracts/System/SystemMetricsDto.cs" ]; then
    echo "❌ SystemMetricsDto not created"
    exit 1
fi

if [ -f "code/src/Core/ExxerAI.Application/DTOs/SystemMetrics.cs" ]; then
    echo "❌ Original SystemMetrics still exists"
    exit 1
fi

# Step 7: Validate compilation
echo "🔍 Validating compilation..."
if ! dotnet build "code/src/ExxerAI.sln" --verbosity quiet >/dev/null 2>&1; then
    echo "❌ Compilation failed, rolling back..."
    cp "backup/SystemMetrics.cs.backup" "code/src/Core/ExxerAI.Application/DTOs/SystemMetrics.cs"
    rm -f "code/src/Contracts/ExxerAI.Contracts/System/SystemMetricsDto.cs"
    exit 1
fi

# Step 8: Verify no broken references
echo "🔍 Verifying no broken references..."
if grep -r "SystemMetrics" "code/src" --exclude-dir=backup --exclude-dir=checkpoints | grep -v "SystemMetricsDto"; then
    echo "❌ Broken references found"
    exit 1
fi

echo "✅ Micro-Task 2.2 completed successfully"
echo "📊 Summary:"
echo "   - SystemMetrics migrated to SystemMetricsDto"
echo "   - Namespace updated to ExxerAI.Contracts.System"
echo "   - All consumers updated"
echo "   - Original file removed"
echo "   - Compilation verified"
echo "   - Backup created: backup/SystemMetrics.cs.backup"

