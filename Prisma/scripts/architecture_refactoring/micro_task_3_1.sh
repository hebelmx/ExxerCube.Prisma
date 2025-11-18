#!/bin/bash
# Micro-Task 3.1: Remove Google APIs from Application
# Duration: 45 minutes
# Risk Level: HIGH
# Dependencies: None
# Rollback Time: 10 minutes

set -e

echo "🔧 Micro-Task 3.1: Remove Google APIs from Application"

# Step 1: Create backup
echo "📋 Creating backup..."
cp "code/src/Core/ExxerAI.Application/ExxerAI.Application.csproj" "backup/ExxerAI.Application.csproj.backup"

# Step 2: Remove Google API package references
echo "🗑️  Removing Google API package references..."
sed -i '/<PackageReference Include="Google.Apis.Drive.v3" \/>/d' "code/src/Core/ExxerAI.Application/ExxerAI.Application.csproj"
sed -i '/<PackageReference Include="Google.Apis.Auth" \/>/d' "code/src/Core/ExxerAI.Application/ExxerAI.Application.csproj"
sed -i '/<PackageReference Include="Google.Apis.Core" \/>/d' "code/src/Core/ExxerAI.Application/ExxerAI.Application.csproj"

# Step 3: Remove entire Google APIs ItemGroup
echo "🗑️  Removing Google APIs ItemGroup..."
sed -i '/<!-- ============================================================================ -->/,/<!-- ============================================================================ -->/d' "code/src/Core/ExxerAI.Application/ExxerAI.Application.csproj"

# Step 4: Create Infrastructure.GoogleDrive project
echo "📁 Creating Infrastructure.GoogleDrive project..."
mkdir -p "code/src/Infrastructure/ExxerAI.Infrastructure.GoogleDrive"

cat > "code/src/Infrastructure/ExxerAI.Infrastructure.GoogleDrive/ExxerAI.Infrastructure.GoogleDrive.csproj" << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  
  <ItemGroup Label="Google APIs">
    <PackageReference Include="Google.Apis.Drive.v3" />
    <PackageReference Include="Google.Apis.Auth" />
    <PackageReference Include="Google.Apis.Core" />
  </ItemGroup>
  
  <ItemGroup>
    <ProjectReference Include="..\..\Core\ExxerAI.Application\ExxerAI.Application.csproj" />
    <ProjectReference Include="..\..\Core\ExxerAI.Domain\ExxerAI.Domain.csproj" />
  </ItemGroup>
</Project>
EOF

# Step 5: Add to solution
echo "🔗 Adding to solution..."
if ! dotnet sln "code/src/ExxerAI.sln" add "code/src/Infrastructure/ExxerAI.Infrastructure.GoogleDrive/ExxerAI.Infrastructure.GoogleDrive.csproj" >/dev/null 2>&1; then
    echo "❌ Failed to add project to solution"
    exit 1
fi

# Step 6: Validate package removal
echo "🔍 Validating package removal..."
if grep -qi "google.apis" "code/src/Core/ExxerAI.Application/ExxerAI.Application.csproj"; then
    echo "❌ Google APIs still present in Application project"
    exit 1
fi

# Step 7: Validate new project creation
echo "🔍 Validating new project creation..."
if [ ! -f "code/src/Infrastructure/ExxerAI.Infrastructure.GoogleDrive/ExxerAI.Infrastructure.GoogleDrive.csproj" ]; then
    echo "❌ GoogleDrive infrastructure project not created"
    exit 1
fi

# Step 8: Validate compilation
echo "🔍 Validating compilation..."
if ! dotnet build "code/src/Infrastructure/ExxerAI.Infrastructure.GoogleDrive/ExxerAI.Infrastructure.GoogleDrive.csproj" --verbosity quiet >/dev/null 2>&1; then
    echo "❌ GoogleDrive project compilation failed"
    exit 1
fi

if ! dotnet build "code/src/Core/ExxerAI.Application/ExxerAI.Application.csproj" --verbosity quiet >/dev/null 2>&1; then
    echo "❌ Application project compilation failed"
    exit 1
fi

echo "✅ Micro-Task 3.1 completed successfully"
echo "📊 Summary:"
echo "   - Google APIs removed from Application"
echo "   - Infrastructure.GoogleDrive project created"
echo "   - Project added to solution"
echo "   - Compilation verified"
echo "   - Backup created: backup/ExxerAI.Application.csproj.backup"

