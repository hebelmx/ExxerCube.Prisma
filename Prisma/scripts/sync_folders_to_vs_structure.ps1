<#
.SYNOPSIS
    Reorganizes physical folder structure to match Visual Studio solution structure.

.DESCRIPTION
    Your VS solution has a clean, organized structure with folders like:
    - 01 Core
    - 02 Infrastructure
    - 04 Tests/01 Core, 04 Tests/02 Infrastructure, etc.

    But the physical folders are flat (50 directories mixed together).

    This script creates the same folder structure physically as you see in Visual Studio.

.PARAMETER DryRun
    Shows what would be done without actually moving files.

.EXAMPLE
    .\sync_folders_to_vs_structure.ps1 -DryRun
    # Shows the plan

.EXAMPLE
    .\sync_folders_to_vs_structure.ps1
    # Actually reorganizes folders
#>

param(
    [switch]$DryRun = $false
)

$ErrorActionPreference = "Stop"

$BasePath = "Code\Src\CSharp"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SYNC PHYSICAL FOLDERS TO VS STRUCTURE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No files will be moved" -ForegroundColor Yellow
    Write-Host ""
}

# Define the target structure based on VS solution
$Structure = @{
    "01-Core" = @(
        "Domain",
        "Application"
    )
    "02-Infrastructure" = @(
        "Infrastructure",
        "Infrastructure.BrowserAutomation",
        "Infrastructure.Classification",
        "Infrastructure.Database",
        "Infrastructure.Export",
        "Infrastructure.Extraction",
        "Infrastructure.FileStorage",
        "Infrastructure.Imaging",
        "Infrastructure.Metrics",
        "Infrastructure.Python.GotOcr2"
    )
    "03-UI" = @(
        "UI"
    )
    "04-Tests\01-Core" = @(
        "Tests.Application",
        "Tests.Domain",
        "Tests.Domain.Interfaces"
    )
    "04-Tests\02-Infrastructure" = @(
        "Tests.Infrastructure.Classification",
        "Tests.Infrastructure.Database",
        "Tests.Infrastructure.Export",
        "Tests.Infrastructure.Extraction",
        "Tests.Infrastructure.Extraction.GotOcr2",
        "Tests.Infrastructure.Extraction.Teseract",
        "Tests.Infrastructure.FileStorage",
        "Tests.Infrastructure.FileSystem",
        "Tests.Infrastructure.Imaging",
        "Tests.Infrastructure.Metrics",
        "Tests.Infrastructure.Python",
        "Tests.Infrastructure.XmlExtraction"
    )
    "04-Tests\03-System" = @(
        "Tests.Infrastructure.BrowserAutomation.E2E",
        "Tests.System"
    )
    "04-Tests\04-UI" = @(
        "Tests.UI"
    )
    "04-Tests\05-E2E" = @(
        "Tests.EndToEnd"
    )
    "04-Tests\06-Architecture" = @(
        "Tests.Architecture"
    )
    "05-ConsoleApp" = @(
        "ConsoleApp.GotOcr2Demo"
    )
    "05-Testing\01-Abstractions" = @(
        "Testing"  # Will become Testing/Abstractions
    )
    "05-Testing\03-Infrastructure" = @(
        "Testing.Infrastructure"  # Will become Testing/Infrastructure
    )
}

# Create target folders
Write-Host "📁 CREATING TARGET FOLDER STRUCTURE:" -ForegroundColor Cyan
foreach ($folder in $Structure.Keys | Sort-Object) {
    $targetPath = Join-Path $BasePath $folder

    if ($DryRun) {
        if (!(Test-Path $targetPath)) {
            Write-Host "  [DRY RUN] Would create: $folder" -ForegroundColor Yellow
        }
    }
    else {
        if (!(Test-Path $targetPath)) {
            New-Item -Path $targetPath -ItemType Directory -Force | Out-Null
            Write-Host "  ✓ Created: $folder" -ForegroundColor Green
        }
        else {
            Write-Host "  ⊘ Already exists: $folder" -ForegroundColor Gray
        }
    }
}

Write-Host ""
Write-Host "📦 MOVING PROJECTS TO ORGANIZED FOLDERS:" -ForegroundColor Cyan

$MovedCount = 0
$SkippedCount = 0

foreach ($targetFolder in $Structure.Keys | Sort-Object) {
    $projects = $Structure[$targetFolder]

    foreach ($project in $projects) {
        $sourcePath = Join-Path $BasePath $project
        $targetPath = Join-Path $BasePath (Join-Path $targetFolder $project)

        if (Test-Path $sourcePath) {
            if ($DryRun) {
                Write-Host "  [DRY RUN] Would move:" -ForegroundColor Yellow
                Write-Host "    From: $project" -ForegroundColor Gray
                Write-Host "    To: $targetFolder\$project" -ForegroundColor Gray
            }
            else {
                try {
                    Move-Item -Path $sourcePath -Destination $targetPath -Force
                    Write-Host "  ✓ Moved: $project -> $targetFolder\" -ForegroundColor Green
                    $MovedCount++
                }
                catch {
                    Write-Host "  ✗ Failed to move: $project - $_" -ForegroundColor Red
                }
            }
        }
        else {
            Write-Host "  ⊘ Not found: $project" -ForegroundColor Gray
            $SkippedCount++
        }
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "Mode: DRY RUN (no actual changes)" -ForegroundColor Yellow
}
else {
    Write-Host "Mode: LIVE (files moved)" -ForegroundColor Green
}

Write-Host "Projects moved: $MovedCount" -ForegroundColor White
Write-Host "Projects skipped: $SkippedCount" -ForegroundColor Gray

Write-Host ""
Write-Host "⚠️  IMPORTANT: After moving, you MUST:" -ForegroundColor Yellow
Write-Host "  1. Update all <ProjectReference> paths in .csproj files" -ForegroundColor White
Write-Host "  2. Update the .sln file project paths" -ForegroundColor White
Write-Host "  3. OR Use Visual Studio to resync the solution" -ForegroundColor White
Write-Host ""
Write-Host "🔧 EASIEST APPROACH:" -ForegroundColor Cyan
Write-Host "  1. Open solution in Visual Studio" -ForegroundColor White
Write-Host "  2. Right-click solution -> 'Sync with File System'" -ForegroundColor White
Write-Host "  3. Or remove and re-add projects to solution folders" -ForegroundColor White

Write-Host ""

if ($DryRun) {
    Write-Host "🔄 To perform actual reorganization, run without -DryRun:" -ForegroundColor Yellow
    Write-Host "   .\scripts\sync_folders_to_vs_structure.ps1" -ForegroundColor Yellow
}
else {
    Write-Host "✓ Physical folder structure now matches VS structure!" -ForegroundColor Green
}

Write-Host ""
