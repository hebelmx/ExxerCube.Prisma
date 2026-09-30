#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Cleans all build artifacts and cache folders from the solution.

.DESCRIPTION
    Removes bin, obj, .vs, TestResults, and other build/cache folders recursively.
    This helps resolve build issues caused by stale cached artifacts.

.PARAMETER Force
    Skip confirmation prompt and delete immediately.

.EXAMPLE
    .\Clean-BuildArtifacts.ps1
    # Prompts for confirmation before deleting

.EXAMPLE
    .\Clean-BuildArtifacts.ps1 -Force
    # Deletes without confirmation
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Force
)

# Get the script's directory (solution root)
$SolutionRoot = $PSScriptRoot

Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "Clean Build Artifacts and Cache" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host ""
Write-Host "Solution Root: $SolutionRoot" -ForegroundColor Yellow
Write-Host ""

# Define folders to clean
$FoldersToClean = @(
    'bin',
    'obj',
    '.vs',
    'TestResults',
    '.pytest_cache',
    '__pycache__',
    'node_modules',
    '.nuget'
)

# Find all matching folders
Write-Host "Scanning for build artifacts..." -ForegroundColor Yellow
$FoundFolders = @()

foreach ($folderName in $FoldersToClean) {
    $folders = Get-ChildItem -Path $SolutionRoot -Directory -Recurse -Filter $folderName -ErrorAction SilentlyContinue
    $FoundFolders += $folders
}

# Sort by full path for better display
$FoundFolders = $FoundFolders | Sort-Object FullName

if ($FoundFolders.Count -eq 0) {
    Write-Host ""
    Write-Host "No build artifacts found. Solution is already clean!" -ForegroundColor Green
    Write-Host ""
    exit 0
}

# Display what will be deleted
Write-Host ""
Write-Host "Found $($FoundFolders.Count) folders to delete:" -ForegroundColor Yellow
Write-Host ""

# Group by folder type for better display
$grouped = $FoundFolders | Group-Object Name | Sort-Object Name

foreach ($group in $grouped) {
    Write-Host "  $($group.Name): " -NoNewline -ForegroundColor Cyan
    Write-Host "$($group.Count) folders" -ForegroundColor White
}

# Calculate total size
Write-Host ""
Write-Host "Calculating total size..." -ForegroundColor Yellow
$totalSize = 0
foreach ($folder in $FoundFolders) {
    try {
        $size = (Get-ChildItem -Path $folder.FullName -Recurse -File -ErrorAction SilentlyContinue |
                 Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum
        if ($size) { $totalSize += $size }
    }
    catch {
        # Ignore errors calculating size
    }
}

$totalSizeMB = [math]::Round($totalSize / 1MB, 2)
Write-Host "Total size to free: $totalSizeMB MB" -ForegroundColor Yellow
Write-Host ""

# Confirm deletion
if (-not $Force) {
    $confirmation = Read-Host "Do you want to delete these folders? (Y/N)"
    if ($confirmation -ne 'Y' -and $confirmation -ne 'y') {
        Write-Host ""
        Write-Host "Operation cancelled." -ForegroundColor Yellow
        Write-Host ""
        exit 0
    }
}

# Delete folders
Write-Host ""
Write-Host "Deleting folders..." -ForegroundColor Yellow
Write-Host ""

$deletedCount = 0
$failedCount = 0
$failedFolders = @()

foreach ($folder in $FoundFolders) {
    $relativePath = $folder.FullName.Replace($SolutionRoot, '').TrimStart('\', '/')

    try {
        Remove-Item -Path $folder.FullName -Recurse -Force -ErrorAction Stop
        Write-Host "  ✓ " -NoNewline -ForegroundColor Green
        Write-Host "$relativePath" -ForegroundColor Gray
        $deletedCount++
    }
    catch {
        Write-Host "  ✗ " -NoNewline -ForegroundColor Red
        Write-Host "$relativePath" -ForegroundColor Gray
        Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
        $failedCount++
        $failedFolders += $relativePath
    }
}

# Summary
Write-Host ""
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host ""
Write-Host "  Deleted: " -NoNewline
Write-Host "$deletedCount folders" -ForegroundColor Green
Write-Host "  Failed:  " -NoNewline
Write-Host "$failedCount folders" -ForegroundColor $(if ($failedCount -gt 0) { 'Red' } else { 'Gray' })
Write-Host "  Freed:   " -NoNewline
Write-Host "$totalSizeMB MB" -ForegroundColor Yellow
Write-Host ""

if ($failedCount -gt 0) {
    Write-Host "Failed folders:" -ForegroundColor Red
    foreach ($failed in $failedFolders) {
        Write-Host "  - $failed" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "Tip: Close Visual Studio and all terminal windows, then run this script again." -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Done!" -ForegroundColor Green
Write-Host ""
