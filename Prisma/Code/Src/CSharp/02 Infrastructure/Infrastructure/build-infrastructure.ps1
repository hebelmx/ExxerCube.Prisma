#!/usr/bin/env pwsh
# Build script for ExxerCube.Prisma.Infrastructure project
# Automatically sets PYTHONPATH for CSnakes code generation

$ErrorActionPreference = "Stop"

Write-Host "Building ExxerCube.Prisma.Infrastructure..." -ForegroundColor Green

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$solutionRoot = Split-Path -Parent $projectRoot

# Set PYTHONPATH to include ocr_modules for CSnakes code generation
$ocrModulesPath = Join-Path $solutionRoot "Python" "prisma-ocr-pipeline" "src"

if (Test-Path $ocrModulesPath) {
    $currentPythonPath = $env:PYTHONPATH
    if ($currentPythonPath) {
        $env:PYTHONPATH = "$ocrModulesPath;$currentPythonPath"
    } else {
        $env:PYTHONPATH = $ocrModulesPath
    }
    Write-Host "PYTHONPATH set to: $env:PYTHONPATH" -ForegroundColor Cyan
} else {
    Write-Warning "ocr_modules path not found at: $ocrModulesPath"
    Write-Warning "CSnakes code generation may fail if ocr_modules is not available"
}

# Project file path
$projectFile = Join-Path $scriptDir "ExxerCube.Prisma.Infrastructure.csproj"

# Build parameters
$buildParams = @()
if ($args.Count -gt 0) {
    $buildParams = $args
} else {
    $buildParams = @("--verbosity", "minimal")
}

# Clean first
Write-Host "Cleaning project..." -ForegroundColor Yellow
dotnet clean $projectFile --verbosity minimal | Out-Null

# Build
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build $projectFile @buildParams

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build completed successfully!" -ForegroundColor Green
} else {
    Write-Host "Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

