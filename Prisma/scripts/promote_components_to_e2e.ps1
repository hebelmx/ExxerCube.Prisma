#!/usr/bin/env pwsh
# Promote Components.Integration.Test from Layer 05 to Layer 07 E2E

$ErrorActionPreference = "Stop"

$SOURCE = "F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\05IntegrationTests\ExxerAI.Components.Integration.Test"
$DEST = "F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\07E2ETests\ExxerAI.Components.Integration.Test"

Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "Components.Integration.Test Promotion to Layer 07 E2E" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan

# Step 1: Verify source exists
if (-not (Test-Path $SOURCE)) {
    Write-Host "ERROR: Source directory not found: $SOURCE" -ForegroundColor Red
    exit 1
}

Write-Host "`n1. Creating target directory..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path (Split-Path $DEST) | Out-Null

# Step 2: Use robocopy for reliable file move (handles locks better)
Write-Host "2. Moving files..." -ForegroundColor Yellow
robocopy $SOURCE $DEST /E /MOVE /NFL /NDL /NJH /NJS
if ($LASTEXITCODE -gt 7) {
    Write-Host "ERROR: Robocopy failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit 1
}

# Step 3: Update solution file
Write-Host "3. Updating solution file..." -ForegroundColor Yellow
$slnPath = "F:\Dynamic\ExxerAi\ExxerAI\code\src\ExxerAI.sln"
$slnContent = Get-Content $slnPath -Raw
$slnContent = $slnContent -replace 'tests\\05IntegrationTests\\ExxerAI\.Components\.Integration\.Test', 'tests\07E2ETests\ExxerAI.Components.Integration.Test'
Set-Content $slnPath $slnContent -NoNewline

# Step 4: Update Cortex project reference
Write-Host "4. Updating Cortex.Integration.Test reference..." -ForegroundColor Yellow
$cortexProj = "F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\05IntegrationTests\ExxerAI.Cortex.Integration.Test\ExxerAI.Cortex.Integration.Test.csproj"
$cortexContent = Get-Content $cortexProj -Raw
$cortexContent = $cortexContent -replace '\.\.\\ExxerAI\.Components\.Integration\.Test\\ExxerAI\.Components\.Integration\.Test\.csproj', '..\..\07E2ETests\ExxerAI.Components.Integration.Test\ExxerAI.Components.Integration.Test.csproj'
Set-Content $cortexProj $cortexContent -NoNewline

# Step 5: Update Nexus project reference
Write-Host "5. Updating Nexus.Integration.Test reference..." -ForegroundColor Yellow
$nexusProj = "F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\05IntegrationTests\ExxerAI.Nexus.Integration.Test\ExxerAI.Nexus.Integration.Test.csproj"
$nexusContent = Get-Content $nexusProj -Raw
$nexusContent = $nexusContent -replace '\.\.\\ExxerAI\.Components\.Integration\.Test\\ExxerAI\.Components\.Integration\.Test\.csproj', '..\..\07E2ETests\ExxerAI.Components.Integration.Test\ExxerAI.Components.Integration.Test.csproj'
Set-Content $nexusProj $nexusContent -NoNewline

Write-Host "`n" + ("=" * 70) -ForegroundColor Green
Write-Host "SUCCESS: Components.Integration.Test promoted to Layer 07 E2E" -ForegroundColor Green
Write-Host "=" * 70 -ForegroundColor Green

Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "1. Run: dotnet build code/src/ExxerAI.sln --no-restore" -ForegroundColor White
Write-Host "2. Verify build succeeds" -ForegroundColor White
Write-Host "3. Git add and commit changes" -ForegroundColor White
