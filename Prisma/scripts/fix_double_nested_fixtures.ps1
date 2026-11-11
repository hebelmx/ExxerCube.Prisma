#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fix double-nested PRP1_Degraded fixtures in test projects
.DESCRIPTION
    Moves fixtures from Fixtures/PRP1_Degraded/PRP1_Degraded/... to Fixtures/PRP1_Degraded/...
#>

$ErrorActionPreference = "Stop"

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "FIXING DOUBLE-NESTED FIXTURES" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$TestProjects = @(
    "Code\Src\CSharp\04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction.Teseract",
    "Code\Src\CSharp\04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction.GotOcr2",
    "Code\Src\CSharp\04-Tests\03-System\Tests.System",
    "Code\Src\CSharp\04-Tests\02-Infrastructure\Tests.Infrastructure.XmlExtraction"
)

$FixedCount = 0

foreach ($projectPath in $TestProjects) {
    $doubleNestedPath = Join-Path $projectPath "Fixtures\PRP1_Degraded\PRP1_Degraded"

    if (-not (Test-Path $doubleNestedPath)) {
        Write-Host "⊘ No double-nested fixtures in: $projectPath" -ForegroundColor Gray
        continue
    }

    Write-Host "📦 $projectPath" -ForegroundColor Cyan

    # Move contents from double-nested to correct location
    $correctPath = Join-Path $projectPath "Fixtures\PRP1_Degraded"

    # Get all items from the double-nested directory
    $items = Get-ChildItem -Path $doubleNestedPath -Recurse -File

    foreach ($item in $items) {
        # Calculate the relative path from the double-nested directory
        $relativePath = $item.FullName.Substring($doubleNestedPath.Length + 1)

        # Construct the destination path
        $destPath = Join-Path $correctPath $relativePath
        $destDir = Split-Path -Parent $destPath

        # Create destination directory if it doesn't exist
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }

        # Move the file
        Move-Item -Path $item.FullName -Destination $destPath -Force
        Write-Host "  ✓ Moved: $relativePath" -ForegroundColor Green
        $FixedCount++
    }

    # Remove the now-empty double-nested directory
    Remove-Item -Path $doubleNestedPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  🗑 Removed double-nested directory" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Fixed $FixedCount fixture files" -ForegroundColor Green
Write-Host ""
