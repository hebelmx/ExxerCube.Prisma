#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fixes missing/misplaced project issues

.DESCRIPTION
    1. Removes phantom BrowserAutomation.E2E reference
    2. Moves Teseract project to correct location
#>

$SolutionRoot = $PSScriptRoot

Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "Fixing Project Issues" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host ""

# Issue #1: Remove phantom BrowserAutomation.E2E reference from solution
Write-Host "Issue #1: Removing phantom BrowserAutomation.E2E reference..." -ForegroundColor Yellow

$slnFile = Join-Path $SolutionRoot "ExxerCube.Prisma.sln"
$slnContent = Get-Content $slnFile -Raw

# Remove the project entry and its EndProject line
$slnContent = $slnContent -replace '(?m)^Project\([^)]+\) = "ExxerCube\.Prisma\.Tests\.System\.BrowserAutomation\.E2E"[^\r\n]+[\r\n]+EndProject[\r\n]+', ''

# Also remove from NestedProjects if it exists
$slnContent = $slnContent -replace '(?m)^\s*\{92D51F68-CF50-4B8E-A3D0-0C93652FBD97\}[^\r\n]+[\r\n]+', ''

Set-Content -Path $slnFile -Value $slnContent -NoNewline
Write-Host "  ✓ Removed BrowserAutomation.E2E from solution" -ForegroundColor Green
Write-Host ""

# Issue #2: Move Teseract project file to correct location
Write-Host "Issue #2: Moving Teseract project to correct location..." -ForegroundColor Yellow

$wrongLocation = Join-Path $SolutionRoot "08 Tests\05 System\Tests.System"
$correctFolder = Join-Path $SolutionRoot "08 Tests\02 Infrastructure\Tests.Infrastructure.Extraction.Teseract"
$tesseractCsproj = Join-Path $wrongLocation "ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract.csproj"

if (Test-Path $tesseractCsproj) {
    # Check if correct folder exists
    if (-not (Test-Path $correctFolder)) {
        Write-Host "  Creating folder: $correctFolder" -ForegroundColor Yellow
        New-Item -Path $correctFolder -ItemType Directory -Force | Out-Null
    }

    # Move the project file
    $destination = Join-Path $correctFolder "ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract.csproj"
    Write-Host "  Moving: $(Split-Path $tesseractCsproj -Leaf)" -ForegroundColor Yellow
    Write-Host "  From: 08 Tests\05 System\Tests.System\" -ForegroundColor Gray
    Write-Host "  To:   08 Tests\02 Infrastructure\Tests.Infrastructure.Extraction.Teseract\" -ForegroundColor Gray

    Move-Item -Path $tesseractCsproj -Destination $destination -Force
    Write-Host "  ✓ Moved Teseract project file" -ForegroundColor Green
}
else {
    Write-Host "  ℹ Teseract project file not found (may have been moved already)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host ""
Write-Host "Fixed issues:" -ForegroundColor Green
Write-Host "  1. Removed phantom BrowserAutomation.E2E reference" -ForegroundColor White
Write-Host "  2. Moved Teseract project to correct location" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Open solution in Visual Studio" -ForegroundColor White
Write-Host "  2. Reload all projects" -ForegroundColor White
Write-Host "  3. Clean and rebuild solution" -ForegroundColor White
Write-Host ""
Write-Host "Done!" -ForegroundColor Green
Write-Host ""
