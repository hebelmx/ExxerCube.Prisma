# Bulk migration script for test files
$ErrorActionPreference = "Stop"

Write-Host "Starting test file migration..." -ForegroundColor Green

# Function to copy and update file
function Copy-TestFile {
    param(
        [string]$SourcePath,
        [string]$DestPath,
        [string]$OldNamespace = "",
        [string]$NewNamespace = ""
    )
    
    if (Test-Path $SourcePath) {
        $destDir = Split-Path $DestPath -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Force -Path $destDir | Out-Null
        }
        
        $content = Get-Content $SourcePath -Raw -ErrorAction SilentlyContinue
        if ($content) {
            # Update TestData namespace references
            $content = $content -replace "ExxerCube\.Prisma\.Tests\.TestData", "ExxerCube.Prisma.Testing.Infrastructure.TestData"
            $content = $content -replace "global using ExxerCube\.Prisma\.Tests\.TestData", "global using ExxerCube.Prisma.Testing.Infrastructure.TestData"
            
            # Update namespace if specified
            if ($OldNamespace -and $NewNamespace) {
                $content = $content -replace [regex]::Escape($OldNamespace), $NewNamespace
            }
            
            Set-Content -Path $DestPath -Value $content -NoNewline -ErrorAction SilentlyContinue
            Write-Host "  ✓ Copied $(Split-Path $SourcePath -Leaf)" -ForegroundColor Gray
        }
    }
}

# Application Tests
Write-Host "`nMigrating Application tests..." -ForegroundColor Yellow
$appSource = "Tests\Application\Services"
$appDest = "Tests.Application\Services"
if (Test-Path $appSource) {
    Get-ChildItem -Path $appSource -Filter "*.cs" | ForEach-Object {
        Copy-TestFile -SourcePath $_.FullName -DestPath (Join-Path $appDest $_.Name)
    }
}

# Infrastructure.Classification Tests
Write-Host "`nMigrating Infrastructure.Classification tests..." -ForegroundColor Yellow
$classSource = "Tests\Infrastructure.Classification"
$classDest = "Tests.Infrastructure.Classification"
if (Test-Path $classSource) {
    Get-ChildItem -Path $classSource -Filter "*.cs" | ForEach-Object {
        Copy-TestFile -SourcePath $_.FullName -DestPath (Join-Path $classDest $_.Name)
    }
}

# Infrastructure.Export Tests
Write-Host "`nMigrating Infrastructure.Export tests..." -ForegroundColor Yellow
$exportSource = "Tests\Infrastructure.Export"
$exportDest = "Tests.Infrastructure.Export"
if (Test-Path $exportSource) {
    Get-ChildItem -Path $exportSource -Filter "*.cs" | ForEach-Object {
        Copy-TestFile -SourcePath $_.FullName -DestPath (Join-Path $exportDest $_.Name)
    }
}

# Infrastructure.Extraction Tests
Write-Host "`nMigrating Infrastructure.Extraction tests..." -ForegroundColor Yellow
$extractSource = "Tests\Infrastructure.Extraction"
$extractDest = "Tests.Infrastructure.Extraction"
if (Test-Path $extractSource) {
    Get-ChildItem -Path $extractSource -Filter "*.cs" | ForEach-Object {
        Copy-TestFile -SourcePath $_.FullName -DestPath (Join-Path $extractDest $_.Name)
    }
}

# Infrastructure.Database Tests
Write-Host "`nMigrating Infrastructure.Database tests..." -ForegroundColor Yellow
$dbSource = "Tests\Infrastructure\Database"
$dbDest = "Tests.Infrastructure.Database"
if (Test-Path $dbSource) {
    Get-ChildItem -Path $dbSource -Filter "*.cs" -Recurse | ForEach-Object {
        $relativePath = $_.FullName.Substring((Resolve-Path $dbSource).Path.Length + 1)
        Copy-TestFile -SourcePath $_.FullName -DestPath (Join-Path $dbDest $relativePath)
    }
}

# Infrastructure.FileSystem Tests
Write-Host "`nMigrating Infrastructure.FileSystem tests..." -ForegroundColor Yellow
$fsSource = "Tests\Infrastructure\FileSystem"
$fsDest = "Tests.Infrastructure.FileSystem"
if (Test-Path $fsSource) {
    Get-ChildItem -Path $fsSource -Filter "*.cs" | ForEach-Object {
        Copy-TestFile -SourcePath $_.FullName -DestPath (Join-Path $fsDest $_.Name)
    }
}

# Infrastructure.FileStorage Tests
Write-Host "`nMigrating Infrastructure.FileStorage tests..." -ForegroundColor Yellow
$storageSource = "Tests\Infrastructure\FileStorage"
$storageDest = "Tests.Infrastructure.FileSystem"  # FileStorage goes to FileSystem project
if (Test-Path $storageSource) {
    Get-ChildItem -Path $storageSource -Filter "*.cs" | ForEach-Object {
        Copy-TestFile -SourcePath $_.FullName -DestPath (Join-Path $storageDest $_.Name)
    }
}

# Infrastructure.Python Tests
Write-Host "`nMigrating Infrastructure.Python tests..." -ForegroundColor Yellow
$pythonSource = "Tests\Infrastructure\Python"
$pythonDest = "Tests.Infrastructure.Python"
if (Test-Path $pythonSource) {
    Get-ChildItem -Path $pythonSource -Filter "*.cs" -Recurse | ForEach-Object {
        $relativePath = $_.FullName.Substring((Resolve-Path $pythonSource).Path.Length + 1)
        $destDir = Split-Path (Join-Path $pythonDest $relativePath) -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Force -Path $destDir | Out-Null
        }
        Copy-TestFile -SourcePath $_.FullName -DestPath (Join-Path $pythonDest $relativePath)
    }
}

Write-Host "`nMigration complete!" -ForegroundColor Green

