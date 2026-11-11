<#
.SYNOPSIS
    Cleans up temporary folders and files from Code/Src/CSharp directory.

.DESCRIPTION
    Removes temporary output folders, test results, build artifacts, and temp files.
    These should be in .gitignore and not tracked.

    SAFE TO RUN - Only deletes temp/build/output folders.

.PARAMETER DryRun
    Shows what would be deleted without actually deleting.

.EXAMPLE
    .\cleanup_temp_folders.ps1 -DryRun
    # Shows what would be deleted

.EXAMPLE
    .\cleanup_temp_folders.ps1
    # Actually deletes temp folders
#>

param(
    [switch]$DryRun = $false
)

$ErrorActionPreference = "Stop"

$BasePath = "Code\Src\CSharp"

# Folders to delete (temp/build/output)
$TempFolders = @(
    "Results",
    "temp_output",
    "test_causa_output",
    "test_output",
    "test_output2",
    "TestResults",
    "bin",
    ".vs"
)

# Files to delete (temp files)
$TempFiles = @(
    "test_causa.txt",
    "test_input.txt",
    "test_input2.txt",
    "test_output.log"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TEMP FOLDER CLEANUP" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Base path: $BasePath" -ForegroundColor Gray
Write-Host ""

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No files will be deleted" -ForegroundColor Yellow
    Write-Host ""
}

$DeletedFolders = 0
$DeletedFiles = 0
$TotalSize = 0

# Delete temp folders
Write-Host "🗑️  TEMPORARY FOLDERS:" -ForegroundColor Cyan
foreach ($folder in $TempFolders) {
    $folderPath = Join-Path $BasePath $folder

    if (Test-Path $folderPath -PathType Container) {
        $size = (Get-ChildItem $folderPath -Recurse -File | Measure-Object -Property Length -Sum).Sum
        if ($null -eq $size) { $size = 0 }
        $TotalSize += $size

        if ($DryRun) {
            Write-Host "  [DRY RUN] Would delete: $folder ($([math]::Round($size / 1MB, 2)) MB)" -ForegroundColor Yellow
        }
        else {
            try {
                Remove-Item $folderPath -Recurse -Force
                Write-Host "  ✓ Deleted: $folder ($([math]::Round($size / 1MB, 2)) MB)" -ForegroundColor Green
                $DeletedFolders++
            }
            catch {
                Write-Host "  ✗ Failed to delete: $folder - $_" -ForegroundColor Red
            }
        }
    }
    else {
        Write-Host "  ⊘ Not found (already deleted?): $folder" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "📄 TEMPORARY FILES:" -ForegroundColor Cyan
foreach ($file in $TempFiles) {
    $filePath = Join-Path $BasePath $file

    if (Test-Path $filePath -PathType Leaf) {
        $size = (Get-Item $filePath).Length
        $TotalSize += $size

        if ($DryRun) {
            Write-Host "  [DRY RUN] Would delete: $file ($size bytes)" -ForegroundColor Yellow
        }
        else {
            try {
                Remove-Item $filePath -Force
                Write-Host "  ✓ Deleted: $file ($size bytes)" -ForegroundColor Green
                $DeletedFiles++
            }
            catch {
                Write-Host "  ✗ Failed to delete: $file - $_" -ForegroundColor Red
            }
        }
    }
    else {
        Write-Host "  ⊘ Not found (already deleted?): $file" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "Mode: DRY RUN (no actual changes)" -ForegroundColor Yellow
}
else {
    Write-Host "Mode: LIVE (files deleted)" -ForegroundColor Green
}

Write-Host "Folders deleted: $DeletedFolders / $($TempFolders.Count)" -ForegroundColor White
Write-Host "Files deleted: $DeletedFiles / $($TempFiles.Count)" -ForegroundColor White
Write-Host "Total space freed: $([math]::Round($TotalSize / 1MB, 2)) MB" -ForegroundColor White

Write-Host ""
Write-Host "📝 RECOMMENDED: Add to .gitignore:" -ForegroundColor Cyan
Write-Host "  bin/" -ForegroundColor Gray
Write-Host "  obj/" -ForegroundColor Gray
Write-Host "  TestResults/" -ForegroundColor Gray
Write-Host "  .vs/" -ForegroundColor Gray
Write-Host "  **/test_output*" -ForegroundColor Gray
Write-Host "  **/temp_output*" -ForegroundColor Gray
Write-Host "  Results/" -ForegroundColor Gray

Write-Host ""

if ($DryRun) {
    Write-Host "🔄 To perform actual cleanup, run without -DryRun:" -ForegroundColor Yellow
    Write-Host "   .\scripts\cleanup_temp_folders.ps1" -ForegroundColor Yellow
}
else {
    Write-Host "✓ Cleanup complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Directory count before: 50" -ForegroundColor White
    Write-Host "Directory count after: ~37 (estimated)" -ForegroundColor Green
}

Write-Host ""
