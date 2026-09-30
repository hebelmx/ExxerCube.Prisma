# PowerShell script to kill all MSBuild, dotnet, and related processes (Admin version)
# Run this script as Administrator for best results
# Usage: Right-click PowerShell -> Run as Administrator -> .\kill-build-processes-admin.ps1

# Check if running as administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "WARNING: Not running as Administrator. Some processes may not be killed." -ForegroundColor Yellow
    Write-Host "For best results, run this script as Administrator." -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Killing MSBuild, dotnet, and related processes..." -ForegroundColor Yellow
Write-Host ""

# Get all processes and filter
$allProcesses = Get-Process -ErrorAction SilentlyContinue

# Process names to kill (case-insensitive matching)
$processPatterns = @(
    "*msbuild*",
    "*dotnet*",
    "*VBCSCompiler*",
    "*csc*",
    "*xunit*",
    "*testhost*",
    "*vstest*",
    "*TransformersSharp*"
)

$killedProcesses = @()
$failedProcesses = @()

foreach ($pattern in $processPatterns) {
    $matchingProcesses = $allProcesses | Where-Object { $_.ProcessName -like $pattern }
    
    foreach ($proc in $matchingProcesses) {
        try {
            Write-Host "Killing: $($proc.ProcessName) (PID: $($proc.Id))" -ForegroundColor Cyan
            Stop-Process -Id $proc.Id -Force -ErrorAction Stop
            $killedProcesses += $proc
            Start-Sleep -Milliseconds 100
        }
        catch {
            Write-Host "  Failed: $($proc.ProcessName) (PID: $($proc.Id)) - $($_.Exception.Message)" -ForegroundColor Red
            $failedProcesses += $proc
        }
    }
}

# Wait for processes to fully terminate
Write-Host ""
Write-Host "Waiting for processes to terminate..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

# Final check - kill any remaining processes
Write-Host "Performing final cleanup..." -ForegroundColor Yellow
$allProcesses = Get-Process -ErrorAction SilentlyContinue
foreach ($pattern in $processPatterns) {
    $remaining = $allProcesses | Where-Object { $_.ProcessName -like $pattern }
    foreach ($proc in $remaining) {
        try {
            Write-Host "Force killing remaining: $($proc.ProcessName) (PID: $($proc.Id))" -ForegroundColor Yellow
            Stop-Process -Id $proc.Id -Force -ErrorAction Stop
            $killedProcesses += $proc
        }
        catch {
            Write-Host "  Still failed: $($proc.ProcessName) (PID: $($proc.Id))" -ForegroundColor Red
            $failedProcesses += $proc
        }
    }
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Successfully killed: $($killedProcesses.Count) processes" -ForegroundColor Green
if ($failedProcesses.Count -gt 0) {
    Write-Host "Failed to kill: $($failedProcesses.Count) processes" -ForegroundColor Red
    foreach ($proc in $failedProcesses) {
        Write-Host "  - $($proc.ProcessName) (PID: $($proc.Id))" -ForegroundColor Red
    }
}
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($failedProcesses.Count -eq 0) {
    Write-Host "All processes killed successfully! You can now build." -ForegroundColor Green
}
else {
    Write-Host "Some processes could not be killed. Try:" -ForegroundColor Yellow
    Write-Host "  1. Run this script as Administrator" -ForegroundColor Yellow
    Write-Host "  2. Close Visual Studio/VS Code" -ForegroundColor Yellow
    Write-Host "  3. Manually kill remaining processes from Task Manager" -ForegroundColor Yellow
}

