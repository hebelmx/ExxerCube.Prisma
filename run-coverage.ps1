# ============================================================================
# Code Coverage Runner for ExxerCube Prisma (.NET 10.0)
# ============================================================================
# Purpose: Run all tests with coverlet coverage collection
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

# ============================================================================
# Banner
# ============================================================================

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  ExxerCube Prisma - Code Coverage Analysis" -ForegroundColor Cyan
Write-Host "  Using Coverlet (XPlat Code Coverage)" -ForegroundColor Cyan
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
# Run Tests with Coverage (Using Coverlet)
# ============================================================================

Write-Host ""
Write-Host "[3/5] Running tests with coverlet coverage collection..." -ForegroundColor Yellow

Push-Location $CSharpRoot

# Build command arguments
$verbosityLevel = switch ($Verbosity) {
    1 { "normal" }
    2 { "detailed" }
    default { "minimal" }
}

# Construct command with coverlet arguments
$cmdArgs = @(
    "test"
    "--configuration"
    "Release"
    "--no-build"
    "--results-directory"
    $TestResultsDir
    "--collect"
    "XPlat Code Coverage"
    "--verbosity"
    $verbosityLevel
)

# Add coverlet settings via runsettings
$cmdArgs += "--"
$cmdArgs += "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,opencover"

if ($Filter) {
    # Insert filter before --
    $cmdArgs = @(
        "test"
        "--configuration"
        "Release"
        "--no-build"
        "--results-directory"
        $TestResultsDir
        "--collect"
        "XPlat Code Coverage"
        "--filter"
        $Filter
        "--verbosity"
        $verbosityLevel
        "--"
        "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,opencover"
    )
    Write-Host "  Filter: $Filter" -ForegroundColor Gray
}

Write-Host "  Running tests with coverlet..." -ForegroundColor DarkGray
Write-Host ""

$testOutput = & dotnet @cmdArgs 2>&1
$testExitCode = $LASTEXITCODE

Write-Host $testOutput

Pop-Location

if ($testExitCode -ne 0) {
    Write-Host ""
    Write-Host "  ⚠ Some tests failed (exit code: $testExitCode)" -ForegroundColor Yellow
    Write-Host "  Continuing with coverage analysis..." -ForegroundColor Yellow
}

# ============================================================================
# Find Coverage Files
# ============================================================================

Write-Host ""
Write-Host "[4/5] Locating coverage data..." -ForegroundColor Yellow

# Coverlet creates coverage.cobertura.xml files in TestResults subdirectories
$coverageFiles = Get-ChildItem -Path $TestResultsDir -Filter "coverage.cobertura.xml" -Recurse

if ($coverageFiles.Count -eq 0) {
    Write-Host "  ✗ No coverage files found!" -ForegroundColor Red
    Write-Host "  Looking for coverage.cobertura.xml in: $TestResultsDir" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  - Ensure coverlet.collector is installed in test projects" -ForegroundColor Gray
    Write-Host "  - Check that tests actually ran successfully" -ForegroundColor Gray
    exit 1
}

Write-Host "  ✓ Found $($coverageFiles.Count) coverage file(s)" -ForegroundColor Green

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
# Merge Coverage Files and Generate Reports
# ============================================================================

# Create a list of all coverage files for ReportGenerator
$coverageFilePaths = ($coverageFiles | ForEach-Object { $_.FullName }) -join ";"

Write-Host "  Generating merged Cobertura XML..." -ForegroundColor Gray

$coberturaOutputDir = Join-Path $CoverageOutputDir "merged"
New-Item -ItemType Directory -Path $coberturaOutputDir -Force | Out-Null

& $reportGeneratorTool `
    "-reports:$coverageFilePaths" `
    "-targetdir:$coberturaOutputDir" `
    "-reporttypes:Cobertura" `
    "-verbosity:Warning"

if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ Failed to generate Cobertura report!" -ForegroundColor Red
    exit 1
}

# Copy merged Cobertura XML to root for CI/CD
$mergedCobertura = Join-Path $coberturaOutputDir "Cobertura.xml"
if (Test-Path $mergedCobertura) {
    $rootCoberturaXml = Join-Path $RootDir "coverage.xml"
    Copy-Item $mergedCobertura $rootCoberturaXml -Force
    Write-Host "  ✓ Cobertura XML: coverage.xml" -ForegroundColor Green
}

# ============================================================================
# Generate HTML Report (for local viewing)
# ============================================================================

if ($GenerateHtml) {
    Write-Host "  Generating HTML report..." -ForegroundColor Gray

    $htmlOutputDir = Join-Path $CoverageOutputDir "html"

    & $reportGeneratorTool `
        "-reports:$coverageFilePaths" `
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
Write-Host "  3. Set coverage thresholds (recommend: 75%+ branch coverage)" -ForegroundColor Gray
Write-Host "  4. Write tests for RED (uncovered) branches" -ForegroundColor Gray
Write-Host ""

# Exit with test result code
exit $testExitCode
