<#
.SYNOPSIS
    Updates .sln file with new project paths after folder reorganization (v2).

.DESCRIPTION
    More precise version that matches full project path patterns in .sln files
    to avoid double-replacement issues.

.PARAMETER DryRun
    Shows what would be changed without actually modifying files.
#>

param(
    [switch]$DryRun = $false
)

$ErrorActionPreference = "Stop"

$SlnPath = "Code\Src\CSharp\ExxerCube.Prisma.sln"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "UPDATE SOLUTION FILE PATHS (V2)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No files will be modified" -ForegroundColor Yellow
    Write-Host ""
}

# Define EXACT path mappings (full project paths)
$PathMappings = @{
    # Core projects
    'Application\ExxerCube.Prisma.Application.csproj' = '01-Core\Application\ExxerCube.Prisma.Application.csproj'
    'Domain\ExxerCube.Prisma.Domain.csproj' = '01-Core\Domain\ExxerCube.Prisma.Domain.csproj'

    # Infrastructure projects
    'Infrastructure\ExxerCube.Prisma.Infrastructure.csproj' = '02-Infrastructure\Infrastructure\ExxerCube.Prisma.Infrastructure.csproj'
    'Infrastructure.BrowserAutomation\ExxerCube.Prisma.Infrastructure.BrowserAutomation.csproj' = '02-Infrastructure\Infrastructure.BrowserAutomation\ExxerCube.Prisma.Infrastructure.BrowserAutomation.csproj'
    'Infrastructure.Classification\ExxerCube.Prisma.Infrastructure.Classification.csproj' = '02-Infrastructure\Infrastructure.Classification\ExxerCube.Prisma.Infrastructure.Classification.csproj'
    'Infrastructure.Database\ExxerCube.Prisma.Infrastructure.Database.csproj' = '02-Infrastructure\Infrastructure.Database\ExxerCube.Prisma.Infrastructure.Database.csproj'
    'Infrastructure.Export\ExxerCube.Prisma.Infrastructure.Export.csproj' = '02-Infrastructure\Infrastructure.Export\ExxerCube.Prisma.Infrastructure.Export.csproj'
    'Infrastructure.Extraction\ExxerCube.Prisma.Infrastructure.Extraction.csproj' = '02-Infrastructure\Infrastructure.Extraction\ExxerCube.Prisma.Infrastructure.Extraction.csproj'
    'Infrastructure.FileStorage\ExxerCube.Prisma.Infrastructure.FileStorage.csproj' = '02-Infrastructure\Infrastructure.FileStorage\ExxerCube.Prisma.Infrastructure.FileStorage.csproj'
    'Infrastructure.Imaging\ExxerCube.Prisma.Infrastructure.Imaging.csproj' = '02-Infrastructure\Infrastructure.Imaging\ExxerCube.Prisma.Infrastructure.Imaging.csproj'
    'Infrastructure.Metrics\ExxerCube.Prisma.Infrastructure.Metrics.csproj' = '02-Infrastructure\Infrastructure.Metrics\ExxerCube.Prisma.Infrastructure.Metrics.csproj'
    'Infrastructure.Python.GotOcr2\ExxerCube.Prisma.Infrastructure.Python.GotOcr2.csproj' = '02-Infrastructure\Infrastructure.Python.GotOcr2\ExxerCube.Prisma.Infrastructure.Python.GotOcr2.csproj'

    # UI project
    'UI\ExxerCube.Prisma.Web.UI.csproj' = '03-UI\UI\ExxerCube.Prisma.Web.UI.csproj'

    # Test projects - Core
    'Tests.Application\ExxerCube.Prisma.Tests.Application.csproj' = '04-Tests\01-Core\Tests.Application\ExxerCube.Prisma.Tests.Application.csproj'
    'Tests.Domain\ExxerCube.Prisma.Tests.Domain.csproj' = '04-Tests\01-Core\Tests.Domain\ExxerCube.Prisma.Tests.Domain.csproj'
    'Tests.Domain.Interfaces\ExxerCube.Prisma.Tests.Domain.Interfaces.csproj' = '04-Tests\01-Core\Tests.Domain.Interfaces\ExxerCube.Prisma.Tests.Domain.Interfaces.csproj'

    # Test projects - Infrastructure
    'Tests.Infrastructure.Classification\ExxerCube.Prisma.Tests.Infrastructure.Classification.csproj' = '04-Tests\02-Infrastructure\Tests.Infrastructure.Classification\ExxerCube.Prisma.Tests.Infrastructure.Classification.csproj'
    'Tests.Infrastructure.Database\ExxerCube.Prisma.Tests.Infrastructure.Database.csproj' = '04-Tests\02-Infrastructure\Tests.Infrastructure.Database\ExxerCube.Prisma.Tests.Infrastructure.Database.csproj'
    'Tests.Infrastructure.Export\ExxerCube.Prisma.Tests.Infrastructure.Export.csproj' = '04-Tests\02-Infrastructure\Tests.Infrastructure.Export\ExxerCube.Prisma.Tests.Infrastructure.Export.csproj'
    'Tests.Infrastructure.Extraction\ExxerCube.Prisma.Tests.Infrastructure.Extraction.csproj' = '04-Tests\02-Infrastructure\Tests.Infrastructure\ExxerCube.Prisma.Tests.Infrastructure.Extraction.csproj'
    'Tests.Infrastructure.Extraction.GotOcr2\ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2.csproj' = '04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction.GotOcr2\ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2.csproj'
    'Tests.Infrastructure.Extraction.Teseract\ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract.csproj' = '04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction.Teseract\ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract.csproj'
    'Tests.Infrastructure.FileStorage\ExxerCube.Prisma.Tests.Infrastructure.FileStorage.csproj' = '04-Tests\02-Infrastructure\Tests.Infrastructure.FileStorage\ExxerCube.Prisma.Tests.Infrastructure.FileStorage.csproj'
    'Tests.Infrastructure.FileSystem\ExxerCube.Prisma.Tests.Infrastructure.FileSystem.csproj' = '04-Tests\02-Infrastructure\Tests.Infrastructure.FileSystem\ExxerCube.Prisma.Tests.Infrastructure.FileSystem.csproj'
    'Tests.Infrastructure.Imaging\ExxerCube.Prisma.Tests.Infrastructure.Imaging.csproj' = '04-Tests\02-Infrastructure\Tests.Infrastructure.Imaging\ExxerCube.Prisma.Tests.Infrastructure.Imaging.csproj'
    'Tests.Infrastructure.Metrics\ExxerCube.Prisma.Tests.Infrastructure.Metrics.csproj' = '04-Tests\02-Infrastructure\Tests.Infrastructure.Metrics\ExxerCube.Prisma.Tests.Infrastructure.Metrics.csproj'
    'Tests.Infrastructure.Python\ExxerCube.Prisma.Tests.Infrastructure.Python.csproj' = '04-Tests\02-Infrastructure\Tests.Infrastructure.Python\ExxerCube.Prisma.Tests.Infrastructure.Python.csproj'
    'Tests.Infrastructure.XmlExtraction\ExxerCube.Prisma.Tests.System.XmlExtraction.csproj' = '04-Tests\02-Infrastructure\Tests.Infrastructure.XmlExtraction\ExxerCube.Prisma.Tests.System.XmlExtraction.csproj'

    # Test projects - System
    'Tests.Infrastructure.BrowserAutomation.E2E\ExxerCube.Prisma.Tests.System.BrowserAutomation.E2E.csproj' = '04-Tests\03-System\Tests.Infrastructure.BrowserAutomation.E2E\ExxerCube.Prisma.Tests.System.BrowserAutomation.E2E.csproj'
    'Tests.System\ExxerCube.Prisma.Tests.System.Ocr.Pipeline.csproj' = '04-Tests\03-System\Tests.System\ExxerCube.Prisma.Tests.System.Ocr.Pipeline.csproj'

    # Test projects - UI
    'Tests.UI\ExxerCube.Prisma.Tests.UI.csproj' = '04-Tests\04-UI\Tests.UI\ExxerCube.Prisma.Tests.UI.csproj'

    # Test projects - E2E
    'Tests.EndToEnd\ExxerCube.Prisma.Tests.EndToEnd.csproj' = '04-Tests\05-E2E\Tests.EndToEnd\ExxerCube.Prisma.Tests.EndToEnd.csproj'

    # Test projects - Architecture
    'Tests.Architecture\ExxerCube.Prisma.Tests.Architecture.csproj' = '04-Tests\06-Architecture\Tests.Architecture\ExxerCube.Prisma.Tests.Architecture.csproj'

    # Console app
    'ConsoleApp.GotOcr2Demo\ExxerCube.Prisma.ConsoleApp.GotOcr2Demo.csproj' = '05-ConsoleApp\ConsoleApp.GotOcr2Demo\ExxerCube.Prisma.ConsoleApp.GotOcr2Demo.csproj'

    # Testing projects
    'Testing\Abstractions\ExxerCube.Prisma.Testing.Abstractions.csproj' = '05-Testing\01-Abstractions\Testing\Abstractions\ExxerCube.Prisma.Testing.Abstractions.csproj'
    'Testing\Contracts\ExxerCube.Prisma.Testing.Contracts.csproj' = '05-Testing\01-Abstractions\Testing\Contracts\ExxerCube.Prisma.Testing.Contracts.csproj'
    'Testing\Infrastructure\ExxerCube.Prisma.Testing.Infrastructure.csproj' = '05-Testing\03-Infrastructure\Testing.Infrastructure\ExxerCube.Prisma.Testing.Infrastructure.csproj'
    'Testing\Python\ExxerCube.Prisma.Testing.Python.csproj' = '05-Testing\01-Abstractions\Testing\Python\ExxerCube.Prisma.Testing.Python.csproj'
}

# Read .sln file
Write-Host "📄 Reading solution file: $SlnPath" -ForegroundColor Cyan

if (!(Test-Path $SlnPath)) {
    Write-Host "❌ Solution file not found: $SlnPath" -ForegroundColor Red
    exit 1
}

$Content = Get-Content -Path $SlnPath -Raw -Encoding UTF8
$OriginalContent = $Content

# Apply path mappings - use exact string replacement
Write-Host ""
Write-Host "🔄 APPLYING PATH MAPPINGS:" -ForegroundColor Cyan

$UpdateCount = 0

foreach ($oldPath in $PathMappings.Keys) {
    $newPath = $PathMappings[$oldPath]

    # Use simple string replacement (not regex) to avoid escaping issues
    if ($Content.Contains($oldPath)) {
        Write-Host "  ✓ $oldPath" -ForegroundColor Green
        Write-Host "    → $newPath" -ForegroundColor Gray
        $Content = $Content.Replace($oldPath, $newPath)
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
    Write-Host "   .\scripts\update_solution_paths_v2.ps1" -ForegroundColor Yellow
}
else {
    if ($UpdateCount -gt 0) {
        # Backup original
        $BackupPath = $SlnPath + ".backup2"
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
