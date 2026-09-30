#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fix fixture paths in test .csproj files after folder reorganization
.DESCRIPTION
    Updates relative paths to fixtures that broke after moving projects into organized folders
#>

$ErrorActionPreference = "Stop"

# Projects that need fixture path updates and their new depth
$ProjectFixtures = @{
    # Infrastructure tests (5 levels up: project -> 02-Infrastructure -> 04-Tests -> CSharp -> Src -> Code)
    "04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction.Teseract\ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract.csproj" = 5
    "04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction.GotOcr2\ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2.csproj" = 5
    "04-Tests\02-Infrastructure\Tests.Infrastructure.XmlExtraction\ExxerCube.Prisma.Tests.System.XmlExtraction.csproj" = 5

    # System tests (5 levels up: project -> 03-System -> 04-Tests -> CSharp -> Src -> Code)
    "04-Tests\03-System\Tests.System\ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract.csproj" = 5
}

$BaseDir = "Code\Src\CSharp"
$UpdatedCount = 0

Write-Host "Fixing fixture paths in test .csproj files..." -ForegroundColor Cyan

foreach ($proj in $ProjectFixtures.Keys) {
    $projPath = Join-Path $BaseDir $proj

    if (-not (Test-Path $projPath)) {
        Write-Host "  ⊘ Not found: $proj" -ForegroundColor Yellow
        continue
    }

    $depth = $ProjectFixtures[$proj]
    $oldPath = "..\..\..\..\"  # 4 levels (old)
    $newPath = ".." * $depth + "\"  # Generate correct path
    $newPath = $newPath -replace '(\\\.\.){' + $depth + '}\\', ($oldPath -replace '\\', '\\')

    # Actually, let's be explicit about the replacement
    $oldFixturePath = "..\..\..\..\Fixtures"
    $oldSamplesPath = "..\..\..\..\Samples"

    $newFixturePath = ("..\\" * $depth) + "Fixtures"
    $newSamplesPath = ("..\\" * $depth) + "Samples"

    # Remove trailing backslash
    $newFixturePath = $newFixturePath.TrimEnd('\')
    $newSamplesPath = $newSamplesPath.TrimEnd('\')

    Write-Host "  Updating: $proj" -ForegroundColor White
    Write-Host "    Old: $oldFixturePath" -ForegroundColor Gray
    Write-Host "    New: $newFixturePath" -ForegroundColor Green

    $content = Get-Content $projPath -Raw -Encoding UTF8
    $originalContent = $content

    # Replace fixture paths
    $content = $content -replace [regex]::Escape($oldFixturePath), $newFixturePath
    $content = $content -replace [regex]::Escape($oldSamplesPath), $newSamplesPath

    if ($content -ne $originalContent) {
        Set-Content -Path $projPath -Value $content -Encoding UTF8 -NoNewline
        $UpdatedCount++
        Write-Host "    ✓ Updated" -ForegroundColor Green
    } else {
        Write-Host "    - No changes needed" -ForegroundColor Gray
    }
}

Write-Host "`n✓ Updated $UpdatedCount .csproj files" -ForegroundColor Green
