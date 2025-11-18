#!/bin/bash
# Micro-Task 2.1: Create ExxerAI.Contracts Project
# Duration: 45 minutes
# Risk Level: LOW
# Dependencies: None
# Rollback Time: 5 minutes

set -e

echo "🔧 Micro-Task 2.1: Create ExxerAI.Contracts Project"

# Step 1: Create project structure
echo "📁 Creating project structure..."
mkdir -p "code/src/Contracts/ExxerAI.Contracts"

# Step 2: Create project file
echo "📝 Creating project file..."
cat > "code/src/Contracts/ExxerAI.Contracts/ExxerAI.Contracts.csproj" << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  
  <ItemGroup Label="Core Framework">
    <PackageReference Include="IndQuestResults" />
  </ItemGroup>
</Project>
EOF

# Step 3: Create directory structure
echo "📁 Creating directory structure..."
mkdir -p "code/src/Contracts/ExxerAI.Contracts/"{Agents,Documents,Tasks,Swarm,Performance,Learning,Workflow,Conversation,Orchestration,Common,System,Geometry,Patterns,Optimization,Workload,Dashboard}

# Step 4: Add to solution
echo "🔗 Adding to solution..."
if ! dotnet sln "code/src/ExxerAI.sln" add "code/src/Contracts/ExxerAI.Contracts/ExxerAI.Contracts.csproj" >/dev/null 2>&1; then
    echo "❌ Failed to add project to solution"
    exit 1
fi

# Step 5: Validate project creation
echo "🔍 Validating project creation..."
if [ ! -f "code/src/Contracts/ExxerAI.Contracts/ExxerAI.Contracts.csproj" ]; then
    echo "❌ Project file not created"
    exit 1
fi

# Step 6: Validate compilation
echo "🔍 Validating compilation..."
if ! dotnet build "code/src/Contracts/ExxerAI.Contracts/ExxerAI.Contracts.csproj" --verbosity quiet >/dev/null 2>&1; then
    echo "❌ Compilation failed"
    exit 1
fi

# Step 7: Verify solution inclusion
echo "🔍 Verifying solution inclusion..."
if ! dotnet sln "code/src/ExxerAI.sln" list | grep -q "ExxerAI.Contracts"; then
    echo "❌ Project not added to solution"
    exit 1
fi

echo "✅ Micro-Task 2.1 completed successfully"
echo "📊 Summary:"
echo "   - Contracts project created"
echo "   - Directory structure created"
echo "   - Project added to solution"
echo "   - Compilation verified"
echo "   - Ready for DTO migration"

