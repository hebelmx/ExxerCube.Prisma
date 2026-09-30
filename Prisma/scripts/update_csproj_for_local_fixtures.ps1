#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Update .csproj files to use local Fixtures/ instead of relative paths
.DESCRIPTION
    Replaces long relative paths (..\..\..\..\Fixtures\...) with local paths (Fixtures\...)
#>

$ErrorActionPreference = "Stop"

$Projects = @(
    "Code\Src\CSharp\04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction.Teseract\ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract.csproj",
    "Code\Src\CSharp\04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction.GotOcr2\ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2.csproj",
    "Code\Src\CSharp\04-Tests\02-Infrastructure\Tests.Infrastructure.XmlExtraction\ExxerCube.Prisma.Tests.System.XmlExtraction.csproj",
    "Code\Src\CSharp\04-Tests\03-System\Tests.System\ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract.csproj",
    "Code\Src\CSharp\04-Tests\03-System\Tests.System\ExxerCube.Prisma.Tests.System.Ocr.Pipeline.csproj"
)

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "UPDATING .CSPROJ FILES TO USE LOCAL FIXTURES" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$UpdatedCount = 0

foreach ($projPath in $Projects) {
    if (-not (Test-Path $projPath)) {
        Write-Host "⊘ Not found: $projPath" -ForegroundColor Yellow
        continue
    }

    Write-Host "📝 $projPath" -ForegroundColor White

    $content = Get-Content $projPath -Raw -Encoding UTF8
    $originalContent = $content

    # Replace all variations of relative paths to Fixtures with local paths
    # Pattern: ..\..\..\..\..\..\Fixtures\ or variations with more/less levels
    $patterns = @(
        @{Old = '..\..\..\..\..\..\..\..\Fixtures\'; New = 'Fixtures\'},
        @{Old = '..\..\..\..\..\..\..\Fixtures\'; New = 'Fixtures\'},
        @{Old = '..\..\..\..\..\..\Fixtures\'; New = 'Fixtures\'},
        @{Old = '..\..\..\..\..\Fixtures\'; New = 'Fixtures\'},
        @{Old = '..\..\..\..\Fixtures\'; New = 'Fixtures\'},
        @{Old = '..\..\..\Fixtures\'; New = 'Fixtures\'},

        # Also replace Samples paths with local
        @{Old = '..\..\..\..\..\..\..\..\Samples\'; New = ''},
        @{Old = '..\..\..\..\..\..\..\Samples\'; New = ''},
        @{Old = '..\..\..\..\..\..\Samples\'; New = ''},
        @{Old = '..\..\..\..\..\Samples\'; New = ''},
        @{Old = '..\..\..\..\Samples\'; New = ''},
        @{Old = '..\..\..\Samples\'; New = ''}
    )

    foreach ($pattern in $patterns) {
        $oldPath = [regex]::Escape($pattern.Old)
        $content = $content -replace $oldPath, $pattern.New
    }

    if ($content -ne $originalContent) {
        Set-Content -Path $projPath -Value $content -Encoding UTF8 -NoNewline
        Write-Host "  ✓ Updated to use local Fixtures/" -ForegroundColor Green
        $UpdatedCount++
    } else {
        Write-Host "  - No changes needed" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Updated $UpdatedCount .csproj files" -ForegroundColor Green
Write-Host ""
Write-Host "Fixtures are now local to each project - no relative paths!" -ForegroundColor Green
Write-Host ""
