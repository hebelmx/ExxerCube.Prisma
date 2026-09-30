#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Analyze missing fixtures from test failures and generate recovery plan
.DESCRIPTION
    Parses PrismaFailedTests.txt to extract missing fixture paths, checks what exists,
    and generates a comprehensive recovery plan
#>

$ErrorActionPreference = "Stop"

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "ANALYZING MISSING FIXTURES FROM TEST FAILURES" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Read the failure report
$reportPath = "PrismaFailedTests.txt"
if (-not (Test-Path $reportPath)) {
    Write-Host "⊘ Report not found: $reportPath" -ForegroundColor Red
    exit 1
}

$content = Get-Content $reportPath -Raw

# Extract all missing fixture paths
$missingFixtures = @{}
$patterns = @(
    'Degraded image not found: (.+\.png)',
    'Fixture not found: (.+\.xml)',
    'Fixture not found: (.+\.pdf)'
)

foreach ($pattern in $patterns) {
    $matches = [regex]::Matches($content, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    foreach ($match in $matches) {
        $path = $match.Groups[1].Value

        # Extract the project name and fixture relative path
        if ($path -match '\\ExxerCube\.Prisma\.([^\\]+)\\[^\\]+\\net10\.0\\(.+)') {
            $projectShortName = $Matches[1]
            $fixturePath = $Matches[2]

            if (-not $missingFixtures.ContainsKey($projectShortName)) {
                $missingFixtures[$projectShortName] = @{}
            }
            $missingFixtures[$projectShortName][$fixturePath] = $path
        }
    }
}

Write-Host "📊 MISSING FIXTURES SUMMARY" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$totalMissing = 0
foreach ($project in $missingFixtures.Keys | Sort-Object) {
    $count = $missingFixtures[$project].Count
    $totalMissing += $count
    Write-Host "📦 $project : $count missing fixtures" -ForegroundColor White
}

Write-Host ""
Write-Host "Total missing fixtures: $totalMissing" -ForegroundColor Yellow
Write-Host ""

# Analyze each project and check what exists in source
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "DETAILED ANALYSIS BY PROJECT" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$recoveryPlan = @()

foreach ($project in $missingFixtures.Keys | Sort-Object) {
    Write-Host "📦 $project" -ForegroundColor Cyan
    Write-Host "─────────────────────────────────────────────────────" -ForegroundColor Gray

    # Find the project directory
    $projectDirs = Get-ChildItem -Path "Code\Src\CSharp" -Recurse -Directory -Filter "*$project*" -ErrorAction SilentlyContinue

    if ($projectDirs.Count -eq 0) {
        Write-Host "  ⚠ Project directory not found" -ForegroundColor Red
        continue
    }

    $projectDir = $projectDirs[0].FullName
    Write-Host "  📂 $projectDir" -ForegroundColor Gray

    # Check each missing fixture
    foreach ($fixturePath in $missingFixtures[$project].Keys) {
        $fullPath = $missingFixtures[$project][$fixturePath]

        # Check if it exists in project source
        $sourceFixturePath = Join-Path $projectDir $fixturePath

        if (Test-Path $sourceFixturePath) {
            Write-Host "  ✓ EXISTS in source: $fixturePath" -ForegroundColor Green
        } else {
            Write-Host "  ✗ MISSING from source: $fixturePath" -ForegroundColor Red

            # Check if it exists in Fixtures/ at root
            $rootFixturePath = $fixturePath -replace '^Fixtures\\', 'Fixtures\'
            if (Test-Path $rootFixturePath) {
                Write-Host "    → Found in root: $rootFixturePath" -ForegroundColor Yellow
                $recoveryPlan += @{
                    Project = $project
                    ProjectDir = $projectDir
                    SourcePath = $rootFixturePath
                    DestPath = $sourceFixturePath
                    FixturePath = $fixturePath
                }
            } else {
                Write-Host "    → NOT FOUND in root either" -ForegroundColor Red
            }
        }
    }
    Write-Host ""
}

# Generate recovery plan JSON
$planPath = "fixture_recovery_plan.json"
$recoveryPlan | ConvertTo-Json -Depth 10 | Set-Content $planPath -Encoding UTF8
Write-Host "📄 Recovery plan saved to: $planPath" -ForegroundColor Green
Write-Host ""

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Total missing fixtures: $totalMissing" -ForegroundColor Yellow
Write-Host "Recoverable from root: $($recoveryPlan.Count)" -ForegroundColor Green
Write-Host ""
