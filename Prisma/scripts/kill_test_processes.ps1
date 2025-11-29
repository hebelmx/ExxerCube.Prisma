#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Kill all test runner and build processes
.DESCRIPTION
    Stops all MSBuild, dotnet, compiler, and test runner processes to release file locks
#>

$ErrorActionPreference = "Continue"

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "KILLING TEST AND BUILD PROCESSES" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# List of process names to kill
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
    "conhost",  # Sometimes test runners spawn conhost
    "python",   # For Python-based tests
    # ReSharper test runners
    "JetBrains.ReSharper.TaskRunner",
    "JetBrains.ReSharper.TaskRunner.CLR45",
    "ReSharperTestRunner",
    "dotMemoryUnit",
    "JetBrains.Platform.Satellite",
    # JetBrains profilers (dotTrace/dotMemory)
    "JetBrains.DPA.Protocol.Backend",
    "JetBrains.Dpa.Collector",
    "JetBrains.Etw.Collector.Host",
    "JetBrains.dotTrace",
    "JetBrains.dotMemory"
)

$KilledCount = 0
$TotalProcesses = 0

foreach ($processName in $ProcessNames) {
    $processes = Get-Process -Name $processName -ErrorAction SilentlyContinue

    if ($processes) {
        $count = ($processes | Measure-Object).Count
        $TotalProcesses += $count

        Write-Host "🔴 Found $count '$processName' process(es)" -ForegroundColor Yellow

        foreach ($process in $processes) {
            try {
                Write-Host "  ⊗ Killing PID $($process.Id): $($process.ProcessName)" -ForegroundColor Gray
                $process.Kill()
                $process.WaitForExit(5000)
                $KilledCount++
            }
            catch {
                Write-Host "  ⚠ Failed to kill PID $($process.Id): $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Total processes found: $TotalProcesses" -ForegroundColor Yellow
Write-Host "Successfully killed: $KilledCount" -ForegroundColor Green
Write-Host "Failed to kill: $($TotalProcesses - $KilledCount)" -ForegroundColor Red
Write-Host ""

if ($KilledCount -gt 0) {
    Write-Host "💡 Tip: Wait a few seconds for file handles to be released" -ForegroundColor Cyan
    Write-Host ""
}
