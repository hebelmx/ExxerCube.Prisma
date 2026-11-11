#!/usr/bin/env pwsh
<#
.SYNOPSIS
    List and optionally kill all test runner and build processes
.DESCRIPTION
    Shows all running test/build processes and optionally kills them
#>

param(
    [switch]$Kill,  # Add -Kill flag to actually kill processes
    [switch]$Force  # Add -Force to skip confirmation
)

$ErrorActionPreference = "Continue"

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "TEST AND BUILD PROCESS SCANNER" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# List of process names to check
$ProcessNames = @(
    "msbuild",
    "dotnet",
    "VBCSCompiler",
    "csc",
    "xunit",
    "testhost",
    "vstest",
    "vstest.console",
    "datacollector",
    "CodeCoverage",
    "Microsoft.TestPlatform",
    "conhost",
    "python",
    # ReSharper test runners
    "JetBrains.ReSharper.TaskRunner",
    "JetBrains.ReSharper.TaskRunner.CLR45",
    "ReSharperTestRunner",
    "dotMemoryUnit",
    "JetBrains.Platform.Satellite",
    # Additional ReSharper processes
    "JetBrains.ReSharper.Features.XunitProvider",
    "JetBrains.ReSharper.UnitTestRunner",
    "JetBrains.ReSharper.TaskRunner.CLR4",
    "JetBrains.ReSharper.TaskRunner.CLR45",
    # Rider processes
    "rider64",
    "JetBrains.Rider.Backend"
)

$FoundProcesses = @()
$TotalCount = 0

Write-Host "🔍 Scanning for test/build processes..." -ForegroundColor Cyan
Write-Host ""

foreach ($processName in $ProcessNames) {
    $processes = Get-Process -Name $processName -ErrorAction SilentlyContinue

    if ($processes) {
        foreach ($process in $processes) {
            $TotalCount++
            $info = [PSCustomObject]@{
                Name = $process.ProcessName
                PID = $process.Id
                Memory = [math]::Round($process.WorkingSet64 / 1MB, 2)
                StartTime = try { $process.StartTime.ToString("HH:mm:ss") } catch { "N/A" }
                CPU = [math]::Round($process.TotalProcessorTime.TotalSeconds, 1)
            }
            $FoundProcesses += $info

            Write-Host "🔴 $($info.Name)" -ForegroundColor Yellow
            Write-Host "   PID: $($info.PID) | Memory: $($info.Memory) MB | CPU: $($info.CPU)s | Started: $($info.StartTime)" -ForegroundColor Gray
        }
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan

if ($TotalCount -eq 0) {
    Write-Host "✓ No test or build processes found" -ForegroundColor Green
    Write-Host ""
    exit 0
}

Write-Host "Total processes found: $TotalCount" -ForegroundColor Yellow
Write-Host ""

if (-not $Kill) {
    Write-Host "💡 To kill these processes, run:" -ForegroundColor Cyan
    Write-Host "   pwsh -File scripts/list_and_kill_test_processes.ps1 -Kill" -ForegroundColor White
    Write-Host ""
    exit 0
}

# Kill mode
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Red
Write-Host "KILLING PROCESSES" -ForegroundColor Red
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Red
Write-Host ""

if (-not $Force) {
    $confirmation = Read-Host "Kill $TotalCount process(es)? (y/N)"
    if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
        Write-Host "Cancelled." -ForegroundColor Yellow
        exit 0
    }
}

$KilledCount = 0
$FailedCount = 0

foreach ($info in $FoundProcesses) {
    try {
        $process = Get-Process -Id $info.PID -ErrorAction Stop
        Write-Host "⊗ Killing $($info.Name) (PID $($info.PID))..." -ForegroundColor Yellow
        $process.Kill()
        $process.WaitForExit(5000)
        Write-Host "  ✓ Killed" -ForegroundColor Green
        $KilledCount++
    }
    catch {
        Write-Host "  ✗ Failed: $($_.Exception.Message)" -ForegroundColor Red
        $FailedCount++
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "FINAL SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Successfully killed: $KilledCount" -ForegroundColor Green
Write-Host "Failed to kill: $FailedCount" -ForegroundColor Red
Write-Host ""

if ($KilledCount -gt 0) {
    Write-Host "💡 Tip: Wait a few seconds for file handles to be released" -ForegroundColor Cyan
    Write-Host ""
}
