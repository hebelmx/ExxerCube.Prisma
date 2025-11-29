#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fix fixture paths in test .csproj files to use correct depth (6 levels to reach root Fixtures/)
.DESCRIPTION
    Updates relative paths: 5 levels up -> 6 levels up to reach Prisma root Fixtures folder
#>

$ErrorActionPreference = "Stop"

$ProjectsToFix = @(
    "04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction.Teseract\ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract.csproj",
    "04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction.GotOcr2\ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2.csproj",
    "04-Tests\02-Infrastructure\Tests.Infrastructure.XmlExtraction\ExxerCube.Prisma.Tests.System.XmlExtraction.csproj",
    "04-Tests\03-System\Tests.System\ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract.csproj"
)

$BaseDir = "Code\Src\CSharp"
$UpdatedCount = 0

# Replace 5 backslash-pairs with 6 (for paths that were incorrectly updated)
$oldPath5 = "..\\..\\..\\..\\..\"
$newPath6 = "..\..\..\..\..\..\"

Write-Host "Fixing fixture paths (5 levels -> 6 levels to reach root)..." -ForegroundColor Cyan

foreach ($proj in $ProjectsToFix) {
    $projPath = Join-Path $BaseDir $proj

    if (-not (Test-Path $projPath)) {
        Write-Host "  ⊘ Not found: $proj" -ForegroundColor Yellow
        continue
    }

    Write-Host "  Updating: $proj" -ForegroundColor White

    $content = Get-Content $projPath -Raw -Encoding UTF8
    $originalContent = $content

    # Fix paths that have 5 levels to use 6 levels
    $content = $content -replace [regex]::Escape($oldPath5), $newPath6

    if ($content -ne $originalContent) {
        Set-Content -Path $projPath -Value $content -Encoding UTF8 -NoNewline
        $UpdatedCount++
        Write-Host "    ✓ Updated (5 -> 6 levels)" -ForegroundColor Green
    } else {
        Write-Host "    - No changes needed" -ForegroundColor Gray
    }
}

Write-Host "`n✓ Updated $UpdatedCount .csproj files" -ForegroundColor Green
