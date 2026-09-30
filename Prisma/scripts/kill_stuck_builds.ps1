#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Kill stuck build processes (safe - won't kill Visual Studio)
#>

Write-Host "Killing stuck build processes (MSBuild, VBCSCompiler, dotnet)..." -ForegroundColor Yellow
Write-Host ""

$ProcessNames = @("MSBuild", "VBCSCompiler", "dotnet", "csc")
$KilledCount = 0

foreach ($processName in $ProcessNames) {
    $processes = Get-Process -Name $processName -ErrorAction SilentlyContinue
    if ($processes) {
        foreach ($process in $processes) {
            Write-Host "⊗ Killing $($process.ProcessName) (PID $($process.Id))..." -ForegroundColor Yellow
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            $KilledCount++
        }
    }
}

Start-Sleep -Seconds 2

Write-Host ""
Write-Host "✓ Killed $KilledCount build process(es)" -ForegroundColor Green
Write-Host ""
Write-Host "Visual Studio is safe - it will restart build processes when needed." -ForegroundColor Cyan
