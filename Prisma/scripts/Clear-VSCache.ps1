# Clear Visual Studio and .NET Build Caches
# Run as Administrator for best results

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "CLEARING VISUAL STUDIO AND .NET BUILD CACHES" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host ""

# Get the solution directory
$solutionDir = "F:\Dynamic\ExxerAi\ExxerAI\code\src"
Set-Location $solutionDir

# 1. Clean solution
Write-Host "1. Cleaning solution build outputs..." -ForegroundColor Yellow
try {
    dotnet clean ExxerAI.sln
} catch {
    Write-Host "   Warning: Failed to clean solution" -ForegroundColor Red
}
Write-Host ""

# 2. Clear NuGet caches
Write-Host "2. Clearing NuGet caches..." -ForegroundColor Yellow
try {
    dotnet nuget locals all --clear
} catch {
    Write-Host "   Warning: Failed to clear NuGet cache" -ForegroundColor Red
}
Write-Host ""

# 3. Remove bin and obj folders
Write-Host "3. Removing bin and obj folders..." -ForegroundColor Yellow
$foldersToDelete = @("bin", "obj")
$deletedCount = 0

foreach ($folder in $foldersToDelete) {
    Get-ChildItem -Path $solutionDir -Directory -Recurse -Filter $folder | ForEach-Object {
        try {
            Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction Stop
            Write-Host "   Deleted: $($_.FullName)" -ForegroundColor Green
            $deletedCount++
        } catch {
            Write-Host "   Failed to delete: $($_.FullName)" -ForegroundColor Red
        }
    }
}
Write-Host "   Removed $deletedCount folders" -ForegroundColor Green
Write-Host ""

# 4. Clear .vs folder (Visual Studio cache)
Write-Host "4. Clearing .vs folders (Visual Studio cache)..." -ForegroundColor Yellow
Get-ChildItem -Path $solutionDir -Directory -Recurse -Force -Filter ".vs" | ForEach-Object {
    try {
        Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction Stop
        Write-Host "   Deleted: $($_.FullName)" -ForegroundColor Green
    } catch {
        Write-Host "   Failed to delete: $($_.FullName)" -ForegroundColor Red
    }
}
Write-Host ""

# 5. Clear Visual Studio Component Model Cache
Write-Host "5. Clearing Visual Studio Component Model Cache..." -ForegroundColor Yellow
$vsVersions = @("17.0", "16.0", "15.0")
foreach ($version in $vsVersions) {
    $cachePath = "$env:LOCALAPPDATA\Microsoft\VisualStudio\${version}*\ComponentModelCache"
    if (Test-Path $cachePath) {
        Get-ChildItem -Path $cachePath -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction Stop
                Write-Host "   Cleared cache for VS $version" -ForegroundColor Green
            } catch {
                Write-Host "   Failed to clear cache for VS $version" -ForegroundColor Red
            }
        }
    }
}
Write-Host ""

# 6. Clear MSBuild cache
Write-Host "6. Clearing MSBuild cache..." -ForegroundColor Yellow
$msbuildCache = "$env:LOCALAPPDATA\Microsoft\MSBuild"
if (Test-Path $msbuildCache) {
    try {
        Get-ChildItem -Path $msbuildCache -Filter "*.cache" -Recurse | Remove-Item -Force
        Write-Host "   MSBuild cache cleared" -ForegroundColor Green
    } catch {
        Write-Host "   Failed to clear MSBuild cache" -ForegroundColor Red
    }
}
Write-Host ""

# 7. Clear Temp ASP.NET Files
Write-Host "7. Clearing Temporary ASP.NET Files..." -ForegroundColor Yellow
$tempAspNetFiles = @(
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\Temporary ASP.NET Files",
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files"
)
foreach ($path in $tempAspNetFiles) {
    if (Test-Path $path) {
        try {
            Get-ChildItem -Path $path -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
            Write-Host "   Cleared: $path" -ForegroundColor Green
        } catch {
            Write-Host "   No permission to clear: $path (may need admin rights)" -ForegroundColor Yellow
        }
    }
}
Write-Host ""

# 8. Clear Rider caches (if exists)
Write-Host "8. Clearing JetBrains Rider caches..." -ForegroundColor Yellow
Get-ChildItem -Path $solutionDir -Directory -Recurse -Force -Filter ".idea" | ForEach-Object {
    try {
        Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction Stop
        Write-Host "   Deleted: $($_.FullName)" -ForegroundColor Green
    } catch {
        Write-Host "   Failed to delete: $($_.FullName)" -ForegroundColor Red
    }
}
Write-Host ""

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "CACHE CLEARING COMPLETE!" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Restart Visual Studio" -ForegroundColor White
Write-Host "2. Run: dotnet restore" -ForegroundColor White
Write-Host "3. Run: dotnet build" -ForegroundColor White
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")