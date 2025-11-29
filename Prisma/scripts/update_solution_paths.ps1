<#
.SYNOPSIS
    Updates .sln file with new project paths after folder reorganization.

.DESCRIPTION
    After moving projects to organized folders, this script updates the .sln file
    to point to the new project locations.

.PARAMETER DryRun
    Shows what would be changed without actually modifying files.

.EXAMPLE
    .\update_solution_paths.ps1 -DryRun
    # Shows the plan

.EXAMPLE
    .\update_solution_paths.ps1
    # Actually updates the .sln file
#>

param(
    [switch]$DryRun = $false
)

$ErrorActionPreference = "Stop"

$SlnPath = "Code\Src\CSharp\ExxerCube.Prisma.sln"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "UPDATE SOLUTION FILE PATHS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No files will be modified" -ForegroundColor Yellow
    Write-Host ""
}

# Define path mappings (old -> new)
$PathMappings = @(
    # Core projects
    @{ Old = "Domain\"; New = "01-Core\Domain\" }
    @{ Old = "Application\"; New = "01-Core\Application\" }

    # Infrastructure projects
    @{ Old = "Infrastructure.BrowserAutomation\"; New = "02-Infrastructure\Infrastructure.BrowserAutomation\" }
    @{ Old = "Infrastructure.Classification\"; New = "02-Infrastructure\Infrastructure.Classification\" }
    @{ Old = "Infrastructure.Database\"; New = "02-Infrastructure\Infrastructure.Database\" }
    @{ Old = "Infrastructure.Export\"; New = "02-Infrastructure\Infrastructure.Export\" }
    @{ Old = "Infrastructure.Extraction\"; New = "02-Infrastructure\Infrastructure.Extraction\" }
    @{ Old = "Infrastructure.FileStorage\"; New = "02-Infrastructure\Infrastructure.FileStorage\" }
    @{ Old = "Infrastructure.Imaging\"; New = "02-Infrastructure\Infrastructure.Imaging\" }
    @{ Old = "Infrastructure.Metrics\"; New = "02-Infrastructure\Infrastructure.Metrics\" }
    @{ Old = "Infrastructure.Python.GotOcr2\"; New = "02-Infrastructure\Infrastructure.Python.GotOcr2\" }
    @{ Old = "Infrastructure\"; New = "02-Infrastructure\Infrastructure\" }

    # UI project
    @{ Old = "UI\"; New = "03-UI\UI\" }

    # Test projects - Core
    @{ Old = "Tests.Application\"; New = "04-Tests\01-Core\Tests.Application\" }
    @{ Old = "Tests.Domain.Interfaces\"; New = "04-Tests\01-Core\Tests.Domain.Interfaces\" }
    @{ Old = "Tests.Domain\"; New = "04-Tests\01-Core\Tests.Domain\" }

    # Test projects - Infrastructure
    @{ Old = "Tests.Infrastructure.Classification\"; New = "04-Tests\02-Infrastructure\Tests.Infrastructure.Classification\" }
    @{ Old = "Tests.Infrastructure.Database\"; New = "04-Tests\02-Infrastructure\Tests.Infrastructure.Database\" }
    @{ Old = "Tests.Infrastructure.Export\"; New = "04-Tests\02-Infrastructure\Tests.Infrastructure.Export\" }
    @{ Old = "Tests.Infrastructure.Extraction.GotOcr2\"; New = "04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction.GotOcr2\" }
    @{ Old = "Tests.Infrastructure.Extraction.Teseract\"; New = "04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction.Teseract\" }
    @{ Old = "Tests.Infrastructure.Extraction\"; New = "04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction\" }
    @{ Old = "Tests.Infrastructure.FileStorage\"; New = "04-Tests\02-Infrastructure\Tests.Infrastructure.FileStorage\" }
    @{ Old = "Tests.Infrastructure.FileSystem\"; New = "04-Tests\02-Infrastructure\Tests.Infrastructure.FileSystem\" }
    @{ Old = "Tests.Infrastructure.Imaging\"; New = "04-Tests\02-Infrastructure\Tests.Infrastructure.Imaging\" }
    @{ Old = "Tests.Infrastructure.Metrics\"; New = "04-Tests\02-Infrastructure\Tests.Infrastructure.Metrics\" }
    @{ Old = "Tests.Infrastructure.Python\"; New = "04-Tests\02-Infrastructure\Tests.Infrastructure.Python\" }
    @{ Old = "Tests.Infrastructure.XmlExtraction\"; New = "04-Tests\02-Infrastructure\Tests.Infrastructure.XmlExtraction\" }

    # Test projects - System
    @{ Old = "Tests.Infrastructure.BrowserAutomation.E2E\"; New = "04-Tests\03-System\Tests.Infrastructure.BrowserAutomation.E2E\" }
    @{ Old = "Tests.System\"; New = "04-Tests\03-System\Tests.System\" }

    # Test projects - UI
    @{ Old = "Tests.UI\"; New = "04-Tests\04-UI\Tests.UI\" }

    # Test projects - E2E
    @{ Old = "Tests.EndToEnd\"; New = "04-Tests\05-E2E\Tests.EndToEnd\" }

    # Test projects - Architecture
    @{ Old = "Tests.Architecture\"; New = "04-Tests\06-Architecture\Tests.Architecture\" }

    # Console app
    @{ Old = "ConsoleApp.GotOcr2Demo\"; New = "05-ConsoleApp\ConsoleApp.GotOcr2Demo\" }

    # Testing projects
    @{ Old = "Testing\Infrastructure\"; New = "05-Testing\03-Infrastructure\Testing.Infrastructure\" }
    @{ Old = "Testing\Abstractions\"; New = "05-Testing\01-Abstractions\Testing\Abstractions\" }
    @{ Old = "Testing\Contracts\"; New = "05-Testing\01-Abstractions\Testing\Contracts\" }
    @{ Old = "Testing\Python\"; New = "05-Testing\01-Abstractions\Testing\Python\" }
)

# Read .sln file
Write-Host "📄 Reading solution file: $SlnPath" -ForegroundColor Cyan

if (!(Test-Path $SlnPath)) {
    Write-Host "❌ Solution file not found: $SlnPath" -ForegroundColor Red
    exit 1
}

$Content = Get-Content -Path $SlnPath -Raw -Encoding UTF8
$OriginalContent = $Content

# Apply path mappings
Write-Host ""
Write-Host "🔄 APPLYING PATH MAPPINGS:" -ForegroundColor Cyan

$UpdateCount = 0

# Sort mappings by old path length (descending) to avoid partial matches
$SortedMappings = $PathMappings | Sort-Object { $_.Old.Length } -Descending

foreach ($mapping in $SortedMappings) {
    $oldPath = $mapping.Old
    $newPath = $mapping.New

    # Check if old path exists in content
    if ($Content -match [regex]::Escape($oldPath)) {
        Write-Host "  ✓ $oldPath → $newPath" -ForegroundColor Green
        $Content = $Content -replace [regex]::Escape($oldPath), $newPath
        $UpdateCount++
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Paths updated: $UpdateCount" -ForegroundColor White

if ($DryRun) {
    Write-Host ""
    Write-Host "🔄 To perform actual update, run without -DryRun:" -ForegroundColor Yellow
    Write-Host "   .\scripts\update_solution_paths.ps1" -ForegroundColor Yellow
}
else {
    if ($UpdateCount -gt 0) {
        # Backup original
        $BackupPath = $SlnPath + ".backup"
        Copy-Item -Path $SlnPath -Destination $BackupPath -Force
        Write-Host ""
        Write-Host "💾 Backup created: $BackupPath" -ForegroundColor Green

        # Write updated file
        Set-Content -Path $SlnPath -Value $Content -Encoding UTF8 -NoNewline
        Write-Host "✅ Solution file updated successfully!" -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "ℹ️  No updates needed" -ForegroundColor Gray
    }
}

Write-Host ""
