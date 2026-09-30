#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Check for running test processes
#>

Write-Host "Checking for running test/build processes..." -ForegroundColor Cyan
Write-Host ""

$processes = Get-Process | Where-Object {
    $_.ProcessName -match 'testhost|vstest|dotnet|python|JetBrains|ReSharper|msbuild|VBCSCompiler'
}

if ($processes) {
    $processes | Select-Object ProcessName, Id, @{N='CPU(s)';E={[math]::Round($_.CPU,1)}}, @{N='Memory(MB)';E={[math]::Round($_.WorkingSet64/1MB,2)}} |
        Sort-Object 'CPU(s)' -Descending |
        Format-Table -AutoSize

    Write-Host ""
    Write-Host "Found $($processes.Count) process(es)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To kill these processes:" -ForegroundColor Cyan
    Write-Host "  pwsh -File scripts/kill_test_processes.ps1" -ForegroundColor White
} else {
    Write-Host "✓ No test or build processes running" -ForegroundColor Green
}
