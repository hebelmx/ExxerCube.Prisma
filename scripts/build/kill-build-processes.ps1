# PowerShell script to kill all MSBuild, dotnet, and related processes
# Run this script as Administrator for best results

Write-Host "Killing MSBuild, dotnet, and related processes..." -ForegroundColor Yellow

# List of process names to kill
$processNames = @(
    "msbuild",
    "dotnet",
    "VBCSCompiler",
    "csc",
    "vbc",
    "devenv",
    "MSBuild",
    "xunit.console",
    "testhost",
    "vstest.console",
    "TransformersSharp.Tests"
)

$killedCount = 0
$notFoundCount = 0

foreach ($processName in $processNames) {
    try {
        # Find all processes matching the name (case-insensitive)
        $processes = Get-Process -Name $processName -ErrorAction SilentlyContinue
        
        if ($processes) {
            foreach ($process in $processes) {
                try {
                    Write-Host "Killing process: $($process.ProcessName) (PID: $($process.Id))" -ForegroundColor Cyan
                    Stop-Process -Id $process.Id -Force -ErrorAction Stop
                    $killedCount++
                }
                catch {
                    Write-Host "Failed to kill process: $($process.ProcessName) (PID: $($process.Id)) - $($_.Exception.Message)" -ForegroundColor Red
                }
            }
        }
        else {
            Write-Host "No processes found for: $processName" -ForegroundColor Gray
            $notFoundCount++
        }
    }
    catch {
        Write-Host "Error searching for process: $processName - $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Also kill any dotnet processes that might be running tests or builds
try {
    $dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
    if ($dotnetProcesses) {
        foreach ($proc in $dotnetProcesses) {
            try {
                Write-Host "Killing dotnet process (PID: $($proc.Id))" -ForegroundColor Cyan
                Stop-Process -Id $proc.Id -Force -ErrorAction Stop
                $killedCount++
            }
            catch {
                Write-Host "Failed to kill dotnet process (PID: $($proc.Id)) - $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
}
catch {
    Write-Host "Error searching for dotnet processes: $($_.Exception.Message)" -ForegroundColor Red
}

# Wait a moment for processes to terminate
Start-Sleep -Seconds 2

# Verify processes are killed
Write-Host "`nVerifying processes are killed..." -ForegroundColor Yellow
$remainingProcesses = @()
foreach ($processName in $processNames) {
    $remaining = Get-Process -Name $processName -ErrorAction SilentlyContinue
    if ($remaining) {
        $remainingProcesses += $remaining
    }
}

if ($remainingProcesses.Count -gt 0) {
    Write-Host "`nWARNING: Some processes are still running:" -ForegroundColor Red
    foreach ($proc in $remainingProcesses) {
        Write-Host "  - $($proc.ProcessName) (PID: $($proc.Id))" -ForegroundColor Red
    }
    Write-Host "`nYou may need to run this script as Administrator or manually kill these processes." -ForegroundColor Yellow
}
else {
    Write-Host "`nAll processes have been terminated successfully!" -ForegroundColor Green
}

Write-Host "`nSummary:" -ForegroundColor Yellow
Write-Host "  Processes killed: $killedCount" -ForegroundColor Green
Write-Host "  Processes not found: $notFoundCount" -ForegroundColor Gray
Write-Host "  Remaining processes: $($remainingProcesses.Count)" -ForegroundColor $(if ($remainingProcesses.Count -eq 0) { "Green" } else { "Red" })

Write-Host "`nDone! You can now try building again." -ForegroundColor Green

