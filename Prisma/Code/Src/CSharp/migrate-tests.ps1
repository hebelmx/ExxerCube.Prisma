# Script to migrate test files to new separated test projects
# This script copies files and updates namespaces/usings as needed

$ErrorActionPreference = "Stop"

# Application Tests
Write-Host "Migrating Application tests..."
$appSource = "Tests\Application\Services"
$appDest = "Tests.Application\Services"
if (Test-Path $appSource) {
    New-Item -ItemType Directory -Force -Path $appDest | Out-Null
    Get-ChildItem -Path $appSource -Filter "*.cs" | ForEach-Object {
        $content = Get-Content $_.FullName -Raw
        # Update namespace if needed (should already be correct)
        $content = $content -replace "namespace ExxerCube\.Prisma\.Tests\.Application", "namespace ExxerCube.Prisma.Tests.Application"
        # Update TestData references
        $content = $content -replace "ExxerCube\.Prisma\.Tests\.TestData", "ExxerCube.Prisma.Testing.Infrastructure.TestData"
        Set-Content -Path (Join-Path $appDest $_.Name) -Value $content -NoNewline
        Write-Host "  Copied $($_.Name)"
    }
}

# Infrastructure.Classification Tests
Write-Host "Migrating Infrastructure.Classification tests..."
$classSource = "Tests\Infrastructure.Classification"
$classDest = "Tests.Infrastructure.Classification"
if (Test-Path $classSource) {
    Get-ChildItem -Path $classSource -Filter "*.cs" -Recurse | ForEach-Object {
        $relativePath = $_.FullName.Substring((Resolve-Path $classSource).Path.Length + 1)
        $destPath = Join-Path $classDest (Split-Path $relativePath -Parent)
        New-Item -ItemType Directory -Force -Path $destPath | Out-Null
        $content = Get-Content $_.FullName -Raw
        $content = $content -replace "ExxerCube\.Prisma\.Tests\.TestData", "ExxerCube.Prisma.Testing.Infrastructure.TestData"
        Set-Content -Path (Join-Path $classDest $relativePath) -Value $content -NoNewline
        Write-Host "  Copied $relativePath"
    }
}

Write-Host "Migration complete!"

