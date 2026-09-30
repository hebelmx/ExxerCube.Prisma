# ============================================================================
# Code Coverage for CI/CD Pipeline
# ============================================================================
# Purpose: Run coverage analysis in CI/CD environment
# Output: coverage.xml (Cobertura format) for pipeline integration
# ============================================================================

param(
    [int]$MinimumBranchCoverage = 0,  # Set to 0 initially, increase as coverage improves
    [int]$MinimumLineCoverage = 0
)

$ErrorActionPreference = "Stop"

Write-Host "======================================"
Write-Host "  CI/CD Coverage Analysis"
Write-Host "======================================"
Write-Host ""

# Run coverage without HTML report
& "$PSScriptRoot\run-coverage.ps1" -GenerateHtml:$false -SkipBuild:$false -Verbosity 0

$coverageExitCode = $LASTEXITCODE

# Check if coverage.xml was generated
$coverageXml = Join-Path $PSScriptRoot "coverage.xml"
if (-not (Test-Path $coverageXml)) {
    Write-Host "ERROR: coverage.xml not found!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✓ Coverage XML generated: coverage.xml"
Write-Host ""

# Parse coverage metrics (if thresholds are set)
if ($MinimumBranchCoverage -gt 0 -or $MinimumLineCoverage -gt 0) {
    Write-Host "Checking coverage thresholds..."

    [xml]$coverageData = Get-Content $coverageXml

    $lineCoverage = [double]$coverageData.coverage.'line-rate' * 100
    $branchCoverage = [double]$coverageData.coverage.'branch-rate' * 100

    Write-Host "  Line Coverage: $([math]::Round($lineCoverage, 2))%"
    Write-Host "  Branch Coverage: $([math]::Round($branchCoverage, 2))%"
    Write-Host ""

    $failed = $false

    if ($lineCoverage -lt $MinimumLineCoverage) {
        Write-Host "✗ Line coverage ($([math]::Round($lineCoverage, 2))%) below threshold ($MinimumLineCoverage%)" -ForegroundColor Red
        $failed = $true
    }

    if ($branchCoverage -lt $MinimumBranchCoverage) {
        Write-Host "✗ Branch coverage ($([math]::Round($branchCoverage, 2))%) below threshold ($MinimumBranchCoverage%)" -ForegroundColor Red
        $failed = $true
    }

    if ($failed) {
        exit 1
    }

    Write-Host "✓ Coverage thresholds met!" -ForegroundColor Green
}

Write-Host ""
Write-Host "Coverage analysis complete. Upload coverage.xml to your CI/CD platform."
Write-Host ""

exit $coverageExitCode
