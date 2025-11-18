#!/bin/bash
# Micro-Task 1.1: Remove EF Core Attributes from ProcessingQueue
# Duration: 45 minutes
# Risk Level: LOW
# Dependencies: None
# Rollback Time: 5 minutes

set -e

echo "🔧 Micro-Task 1.1: Remove EF Core Attributes from ProcessingQueue"

# Step 1: Create backup
echo "📋 Creating backup..."
cp "code/src/Core/ExxerAI.Domain/Entities/ProcessingQueue.cs" "backup/ProcessingQueue.cs.backup"

# Step 2: Remove attributes (lines 12, 19, 25, 32, 69, 110, 126)
echo "🗑️  Removing EF Core attributes..."
sed -i '/\[Key\]/d' "code/src/Core/ExxerAI.Domain/Entities/ProcessingQueue.cs"
sed -i '/\[MaxLength/d' "code/src/Core/ExxerAI.Domain/Entities/ProcessingQueue.cs"

# Step 3: Update using statements
echo "📝 Updating using statements..."
sed -i '/using System.ComponentModel.DataAnnotations/d' "code/src/Core/ExxerAI.Domain/Entities/ProcessingQueue.cs"

# Step 4: Validate compilation
echo "🔍 Validating compilation..."
if ! dotnet build "code/src/Core/ExxerAI.Domain/ExxerAI.Domain.csproj" --verbosity quiet >/dev/null 2>&1; then
    echo "❌ Compilation failed, rolling back..."
    cp "backup/ProcessingQueue.cs.backup" "code/src/Core/ExxerAI.Domain/Entities/ProcessingQueue.cs"
    exit 1
fi

# Step 5: Verify no EF attributes remain
echo "✅ Verifying EF attributes removal..."
if grep -q "\[Key\]\|\[MaxLength\]" "code/src/Core/ExxerAI.Domain/Entities/ProcessingQueue.cs"; then
    echo "❌ EF attributes still present, rolling back..."
    cp "backup/ProcessingQueue.cs.backup" "code/src/Core/ExxerAI.Domain/Entities/ProcessingQueue.cs"
    exit 1
fi

echo "✅ Micro-Task 1.1 completed successfully"
echo "📊 Summary:"
echo "   - EF Core attributes removed"
echo "   - Using statements updated"
echo "   - Compilation verified"
echo "   - Backup created: backup/ProcessingQueue.cs.backup"

