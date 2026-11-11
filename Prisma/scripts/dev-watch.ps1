# Auto-build and test runner with nice output
# Watches for changes and shows results in a clean format

$Root = "F:\Dynamic\ExxerAi\ExxerAI\code\src"
Set-Location $Root

# Create a file watcher
$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $Root
$watcher.Filter = "*.cs"
$watcher.IncludeSubdirectories = $true
$watcher.EnableRaisingEvents = $true

# Keep track of build status
$script:lastBuildTime = [DateTime]::MinValue
$script:building = $false

function Show-BuildStatus {
    Clear-Host
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "  ExxerAI Auto-Builder - Watching for changes..." -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Build-Solution {
    if ($script:building) { return }
    
    # Debounce - wait 2 seconds after last change
    $timeSinceLastBuild = ([DateTime]::Now - $script:lastBuildTime).TotalSeconds
    if ($timeSinceLastBuild -lt 2) { return }
    
    $script:building = $true
    $script:lastBuildTime = [DateTime]::Now
    
    Show-BuildStatus
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Building..." -ForegroundColor Yellow
    
    # Capture build output
    $buildOutput = dotnet build --no-restore 2>&1
    $errors = $buildOutput | Select-String -Pattern "error CS"
    $warnings = $buildOutput | Select-String -Pattern "warning CS"
    
    Clear-Host
    Show-BuildStatus
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] ✅ BUILD SUCCEEDED" -ForegroundColor Green
        Write-Host ""
        if ($warnings) {
            Write-Host "Warnings: $($warnings.Count)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] ❌ BUILD FAILED" -ForegroundColor Red
        Write-Host ""
        Write-Host "Errors found:" -ForegroundColor Red
        $errors | ForEach-Object {
            Write-Host $_ -ForegroundColor Red
        }
    }
    
    Write-Host ""
    Write-Host "Press Ctrl+C to stop watching" -ForegroundColor Gray
    
    $script:building = $false
}

# Register event handlers
Register-ObjectEvent -InputObject $watcher -EventName "Changed" -Action { Build-Solution } | Out-Null
Register-ObjectEvent -InputObject $watcher -EventName "Created" -Action { Build-Solution } | Out-Null
Register-ObjectEvent -InputObject $watcher -EventName "Deleted" -Action { Build-Solution } | Out-Null
Register-ObjectEvent -InputObject $watcher -EventName "Renamed" -Action { Build-Solution } | Out-Null

# Initial build
Show-BuildStatus
Build-Solution

# Keep the script running
Write-Host "Watching for file changes. Press Ctrl+C to stop..." -ForegroundColor Yellow
try {
    while ($true) {
        Start-Sleep -Seconds 1
    }
} finally {
    # Cleanup
    $watcher.EnableRaisingEvents = $false
    $watcher.Dispose()
    Write-Host "Stopped watching." -ForegroundColor Yellow
}