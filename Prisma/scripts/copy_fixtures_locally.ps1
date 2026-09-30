#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Copy fixtures locally into each test project so they're self-contained
.DESCRIPTION
    Copies fixtures from root Fixtures/ into each test project's own Fixtures/ folder.
    Each project will have its own copy and won't depend on relative paths.
#>

$ErrorActionPreference = "Stop"

$RootFixtures = "Fixtures"
$ProjectFixtures = @{
    # Infrastructure tests need PRP1_Degraded, PRP1 XML/PDF, and requirements.txt
    "Code\Src\CSharp\04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction.Teseract" = @(
        @{Source = "Fixtures\PRP1_Degraded"; Dest = "Fixtures\PRP1_Degraded"},
        @{Source = "Fixtures\PRP1\*.xml"; Dest = "Fixtures\PRP1"; Files = $true},
        @{Source = "Fixtures\PRP1\*.pdf"; Dest = "Fixtures\PRP1"; Files = $true},
        @{Source = "Samples\GotOcr2Sample\PythonOcrLib\requirements.txt"; Dest = "."; Files = $true}
    )

    "Code\Src\CSharp\04-Tests\02-Infrastructure\Tests.Infrastructure.Extraction.GotOcr2" = @(
        @{Source = "Fixtures\PRP1_Degraded"; Dest = "Fixtures\PRP1_Degraded"},
        @{Source = "Fixtures\PRP1\*.xml"; Dest = "Fixtures\PRP1"; Files = $true},
        @{Source = "Fixtures\PRP1\*.pdf"; Dest = "Fixtures\PRP1"; Files = $true},
        @{Source = "Samples\GotOcr2Sample\PythonOcrLib\requirements.txt"; Dest = "."; Files = $true}
    )

    "Code\Src\CSharp\04-Tests\02-Infrastructure\Tests.Infrastructure.XmlExtraction" = @(
        @{Source = "Fixtures\PRP1\*.xml"; Dest = "Fixtures\PRP1"; Files = $true},
        @{Source = "Fixtures\PRP1\*.pdf"; Dest = "Fixtures\PRP1"; Files = $true}
    )

    "Code\Src\CSharp\04-Tests\03-System\Tests.System" = @(
        @{Source = "Fixtures\PRP1_Degraded"; Dest = "Fixtures\PRP1_Degraded"},
        @{Source = "Fixtures\PRP1\*.xml"; Dest = "Fixtures\PRP1"; Files = $true},
        @{Source = "Fixtures\PRP1\*.pdf"; Dest = "Fixtures\PRP1"; Files = $true},
        @{Source = "Samples\GotOcr2Sample\PythonOcrLib\requirements.txt"; Dest = "."; Files = $true}
    )

    "Code\Src\CSharp\04-Tests\03-System\Tests.Infrastructure.BrowserAutomation.E2E" = @(
        @{Source = "Fixtures\PRP1\*.xml"; Dest = "Fixtures\PRP1"; Files = $true},
        @{Source = "Fixtures\PRP1\*.pdf"; Dest = "Fixtures\PRP1"; Files = $true}
    )
}

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "COPYING FIXTURES LOCALLY TO TEST PROJECTS" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$TotalCopied = 0

foreach ($project in $ProjectFixtures.Keys) {
    Write-Host "📁 $project" -ForegroundColor Yellow

    if (-not (Test-Path $project)) {
        Write-Host "  ⊘ Project directory not found, skipping" -ForegroundColor Red
        continue
    }

    $fixtures = $ProjectFixtures[$project]

    foreach ($fixture in $fixtures) {
        $source = $fixture.Source
        $dest = Join-Path $project $fixture.Dest

        # Create destination directory
        $destDir = if ($fixture.Files) { $dest } else { $dest }
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }

        if ($fixture.Files) {
            # Copy specific files (wildcard pattern)
            $files = Get-Item $source -ErrorAction SilentlyContinue
            if ($files) {
                foreach ($file in $files) {
                    Copy-Item $file.FullName -Destination $dest -Force
                    Write-Host "  ✓ Copied: $($file.Name) -> $dest" -ForegroundColor Green
                    $TotalCopied++
                }
            } else {
                Write-Host "  ⊘ Not found: $source" -ForegroundColor Gray
            }
        } else {
            # Copy entire directory recursively
            if (Test-Path $source) {
                Copy-Item $source -Destination $dest -Recurse -Force
                $fileCount = (Get-ChildItem $dest -Recurse -File).Count
                Write-Host "  ✓ Copied: $source -> $dest ($fileCount files)" -ForegroundColor Green
                $TotalCopied += $fileCount
            } else {
                Write-Host "  ⊘ Not found: $source" -ForegroundColor Gray
            }
        }
    }

    Write-Host ""
}

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Total files copied: $TotalCopied" -ForegroundColor Green
Write-Host ""
Write-Host "Next step: Update .csproj files to use local Fixtures/" -ForegroundColor Yellow
Write-Host ""
