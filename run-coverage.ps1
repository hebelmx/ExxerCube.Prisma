# ============================================================================
# Code Coverage Runner for ExxerCube Prisma
# ============================================================================
# Purpose: Run all tests with branch coverage collection
# Output: Cobertura XML for CI/CD + HTML reports for local viewing
# ============================================================================

param(
    [string]$Filter = "",
    [switch]$SkipBuild,
    [switch]$GenerateHtml = $true,
    [int]$Verbosity = 0  # 0=minimal, 1=normal, 2=detailed
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# ============================================================================
# Configuration
# ============================================================================

$RootDir = $PSScriptRoot
$CSharpRoot = Join-Path $RootDir "Prisma\Code\Src\CSharp"
$TestResultsDir = Join-Path $RootDir "TestResults"
$CoverageOutputDir = Join-Path $RootDir "coverage"
$RunSettingsFile = Join-Path $RootDir "coverage.runsettings"

# ============================================================================
# Banner
# ============================================================================

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  ExxerCube Prisma - Code Coverage Analysis" -ForegroundColor Cyan
Write-Host "  Branch Coverage + Cobertura XML Output" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# Cleanup Previous Results
# ============================================================================

Write-Host "[1/5] Cleaning previous coverage data..." -ForegroundColor Yellow

if (Test-Path $TestResultsDir) {
    Remove-Item $TestResultsDir -Recurse -Force
}
if (Test-Path $CoverageOutputDir) {
    Remove-Item $CoverageOutputDir -Recurse -Force
}

New-Item -ItemType Directory -Path $TestResultsDir -Force | Out-Null
New-Item -ItemType Directory -Path $CoverageOutputDir -Force | Out-Null

Write-Host "  ✓ Cleanup complete" -ForegroundColor Green

# ============================================================================
# Build Solution (optional)
# ============================================================================

if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "[2/5] Building solution..." -ForegroundColor Yellow

    Push-Location $CSharpRoot
    $buildResult = dotnet build --configuration Release --no-incremental 2>&1
    $buildExitCode = $LASTEXITCODE
    Pop-Location

    if ($buildExitCode -ne 0) {
        Write-Host "  ✗ Build failed!" -ForegroundColor Red
        Write-Host $buildResult
        exit 1
    }

    Write-Host "  ✓ Build succeeded" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[2/5] Skipping build (using existing binaries)..." -ForegroundColor Yellow
}

# ============================================================================
# Run Tests with Coverage
# ============================================================================

Write-Host ""
Write-Host "[3/5] Running tests with coverage collection..." -ForegroundColor Yellow
Write-Host "  Settings: $RunSettingsFile" -ForegroundColor Gray

Push-Location $CSharpRoot

$testCommand = @(
    "test"
    "--configuration", "Release"
    "--no-build"
    "--settings", "`"$RunSettingsFile`""
    "--results-directory", "`"$TestResultsDir`""
    "--collect:`"Code Coverage`""
)

if ($Filter) {
    $testCommand += "--filter", "`"$Filter`""
    Write-Host "  Filter: $Filter" -ForegroundColor Gray
}

switch ($Verbosity) {
    1 { $testCommand += "--verbosity", "normal" }
    2 { $testCommand += "--verbosity", "detailed" }
    default { $testCommand += "--verbosity", "minimal" }
}

Write-Host "  Running: dotnet $($testCommand -join ' ')" -ForegroundColor DarkGray
Write-Host ""

$testOutput = & dotnet @testCommand 2>&1
$testExitCode = $LASTEXITCODE

Write-Host $testOutput

Pop-Location

if ($testExitCode -ne 0) {
    Write-Host ""
    Write-Host "  ⚠ Some tests failed (exit code: $testExitCode)" -ForegroundColor Yellow
    Write-Host "  Continuing with coverage analysis..." -ForegroundColor Yellow
}

# ============================================================================
# Find Coverage File
# ============================================================================

Write-Host ""
Write-Host "[4/5] Locating coverage data..." -ForegroundColor Yellow

$coverageFiles = Get-ChildItem -Path $TestResultsDir -Filter "*.coverage" -Recurse
if ($coverageFiles.Count -eq 0) {
    Write-Host "  ✗ No coverage files found!" -ForegroundColor Red
    Write-Host "  Looking for *.coverage in: $TestResultsDir" -ForegroundColor Red
    exit 1
}

$coverageFile = $coverageFiles[0].FullName
Write-Host "  ✓ Found coverage file: $($coverageFiles[0].Name)" -ForegroundColor Green

# ============================================================================
# Install ReportGenerator (if needed)
# ============================================================================

Write-Host ""
Write-Host "[5/5] Generating coverage reports..." -ForegroundColor Yellow

$reportGeneratorTool = "reportgenerator"
$reportGeneratorVersion = "5.3.11"

$toolCheck = & dotnet tool list -g | Select-String "reportgenerator"
if (-not $toolCheck) {
    Write-Host "  Installing ReportGenerator tool..." -ForegroundColor Gray
    dotnet tool install -g dotnet-reportgenerator-globaltool --version $reportGeneratorVersion | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ✗ Failed to install ReportGenerator!" -ForegroundColor Red
        exit 1
    }
}

# ============================================================================
# Generate Cobertura XML (for CI/CD)
# ============================================================================

Write-Host "  Generating Cobertura XML..." -ForegroundColor Gray

$coberturaOutputDir = Join-Path $CoverageOutputDir "cobertura"
New-Item -ItemType Directory -Path $coberturaOutputDir -Force | Out-Null

& $reportGeneratorTool `
    "-reports:$coverageFile" `
    "-targetdir:$coberturaOutputDir" `
    "-reporttypes:Cobertura" `
    "-verbosity:Warning"

if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ Failed to generate Cobertura report!" -ForegroundColor Red
    exit 1
}

$coberturaXml = Join-Path $coberturaOutputDir "Cobertura.xml"
if (Test-Path $coberturaXml) {
    # Copy to root for easy CI/CD access
    $rootCoberturaXml = Join-Path $RootDir "coverage.xml"
    Copy-Item $coberturaXml $rootCoberturaXml -Force
    Write-Host "  ✓ Cobertura XML: coverage.xml" -ForegroundColor Green
}

# ============================================================================
# Generate HTML Report (for local viewing)
# ============================================================================

if ($GenerateHtml) {
    Write-Host "  Generating HTML report..." -ForegroundColor Gray

    $htmlOutputDir = Join-Path $CoverageOutputDir "html"

    & $reportGeneratorTool `
        "-reports:$coverageFile" `
        "-targetdir:$htmlOutputDir" `
        "-reporttypes:Html;HtmlSummary;Badges" `
        "-historydir:$htmlOutputDir\history" `
        "-verbosity:Warning"

    if ($LASTEXITCODE -eq 0) {
        $htmlReport = Join-Path $htmlOutputDir "index.html"
        Write-Host "  ✓ HTML Report: coverage\html\index.html" -ForegroundColor Green

        # Open in browser
        Write-Host ""
        Write-Host "  Opening coverage report in browser..." -ForegroundColor Gray
        Start-Process $htmlReport
    }
}

# ============================================================================
# Summary
# ============================================================================

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Coverage Analysis Complete!" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  📊 Cobertura XML: coverage.xml (CI/CD ready)" -ForegroundColor White
if ($GenerateHtml) {
    Write-Host "  🌐 HTML Report: coverage\html\index.html" -ForegroundColor White
}
Write-Host "  📁 Test Results: TestResults\" -ForegroundColor White
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Review HTML report for coverage gaps" -ForegroundColor Gray
Write-Host "  2. Add coverage.xml to your CI/CD pipeline" -ForegroundColor Gray
Write-Host "  3. Set coverage thresholds (recommend: 80% branch coverage)" -ForegroundColor Gray
Write-Host "  4. Integrate with mutation testing when available" -ForegroundColor Gray
Write-Host ""

# Exit with test result code
exit $testExitCode
