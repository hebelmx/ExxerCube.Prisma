# Install Playwright browsers for testing
# This script installs the required browsers for Playwright end-to-end tests

Write-Host "Installing Playwright browsers..." -ForegroundColor Cyan

# Get the project directory
$projectDir = Split-Path -Parent $PSScriptRoot
$testProjectDir = $PSScriptRoot

# Try to find the output directory
$outputDir = Join-Path $testProjectDir "bin\Debug\net10.0"
if (-not (Test-Path $outputDir)) {
    $outputDir = Join-Path $testProjectDir "bin\Debug\net*"
    $outputDir = (Get-Item $outputDir | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

if (Test-Path $outputDir) {
    $playwrightScript = Join-Path $outputDir "playwright.ps1"
    if (Test-Path $playwrightScript) {
        Write-Host "Found playwright.ps1 at: $playwrightScript" -ForegroundColor Green
        & $playwrightScript install chromium
        exit $LASTEXITCODE
    }
}

# Fallback: Use global Playwright CLI if available
Write-Host "Attempting to use global Playwright CLI..." -ForegroundColor Yellow
try {
    playwright install chromium
    Write-Host "Playwright browsers installed successfully!" -ForegroundColor Green
    exit 0
} catch {
    Write-Host "Error installing Playwright browsers: $_" -ForegroundColor Red
    Write-Host "Please run: dotnet tool install --global Microsoft.Playwright.CLI" -ForegroundColor Yellow
    Write-Host "Then run: playwright install chromium" -ForegroundColor Yellow
    exit 1
}

