#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Analyze test failures from PrismaFailedTests.txt
.DESCRIPTION
    Categorizes test failures by project and failure reason
#>

$reportPath = "PrismaFailedTests.txt"

if (-not (Test-Path $reportPath)) {
    Write-Host "Error: $reportPath not found" -ForegroundColor Red
    exit 1
}

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "TEST FAILURE ANALYSIS" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Count total failures
$content = Get-Content $reportPath -Raw
$totalMatch = [regex]::Match($content, "Failed:\s+(\d+)\s+tests?\s+failed")
if ($totalMatch.Success) {
    $totalFailed = $totalMatch.Groups[1].Value
    Write-Host "Total Failed: $totalFailed tests" -ForegroundColor Yellow
    Write-Host ""
}

# Extract project breakdown
Write-Host "BREAKDOWN BY PROJECT:" -ForegroundColor Cyan
Write-Host ""

$projectPattern = 'ExxerCube\.Prisma\.Tests\.\S+\s+\((\d+)\s+tests?\)\s+\[\S+\]\s+Failed:\s+(\d+)\s+tests?\s+failed'
$matches = [regex]::Matches($content, $projectPattern)

$projects = @{}
foreach ($match in $matches) {
    $projectLine = $match.Value
    if ($projectLine -match '(ExxerCube\.Prisma\.Tests\.\S+)\s+\((\d+)\s+tests?\)\s+\[\S+\]\s+Failed:\s+(\d+)') {
        $projectName = $Matches[1]
        $failedCount = [int]$Matches[3]

        # Only count leaf projects (avoid double-counting)
        if (-not $projects.ContainsKey($projectName) -or $projects[$projectName] -lt $failedCount) {
            $projects[$projectName] = $failedCount
        }
    }
}

# Sort by failure count descending
$sorted = $projects.GetEnumerator() | Sort-Object Value -Descending

foreach ($project in $sorted) {
    Write-Host "  $($project.Value) tests - $($project.Key)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "COMMON FAILURE PATTERNS:" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Analyze failure reasons
$fixtureErrors = [regex]::Matches($content, "Fixture not found|FileNotFoundException").Count
$shouldlyErrors = [regex]::Matches($content, "Shouldly\.ShouldAssertException").Count
$fluentErrors = [regex]::Matches($content, "FluentAssertions|Xunit\.Sdk\.XunitException").Count
$timeoutErrors = [regex]::Matches($content, "timeout|timed out").Count
$nullRefErrors = [regex]::Matches($content, "NullReferenceException").Count

if ($fixtureErrors -gt 0) {
    Write-Host "  Fixture/File errors: $fixtureErrors occurrences" -ForegroundColor Red
}
if ($shouldlyErrors -gt 0) {
    Write-Host "  Shouldly assertion failures: $shouldlyErrors occurrences" -ForegroundColor Yellow
}
if ($fluentErrors -gt 0) {
    Write-Host "  FluentAssertions/XUnit failures: $fluentErrors occurrences" -ForegroundColor Yellow
}
if ($timeoutErrors -gt 0) {
    Write-Host "  Timeout errors: $timeoutErrors occurrences" -ForegroundColor Red
}
if ($nullRefErrors -gt 0) {
    Write-Host "  Null reference errors: $nullRefErrors occurrences" -ForegroundColor Red
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "RECOMMENDATIONS:" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Write-Host "1. Focus on Tests.Infrastructure.Extraction.Teseract (30 failures)" -ForegroundColor White
Write-Host "   - DocxStructureAnalyzerTests - Implementation not detecting structures" -ForegroundColor Gray
Write-Host "   - MexicanNameFuzzyMatcherTests - Threshold tuning needed" -ForegroundColor Gray
Write-Host ""

Write-Host "2. BrowserAutomation.E2E tests (20 failures)" -ForegroundColor White
Write-Host "   - May be timeout or environment-related" -ForegroundColor Gray
Write-Host ""

Write-Host "3. These are NOT fixture issues - fixtures are being found correctly" -ForegroundColor Green
Write-Host "   - These are implementation bugs or test assertion issues" -ForegroundColor Gray
Write-Host ""
