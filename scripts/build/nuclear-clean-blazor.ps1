# Nuclear Clean Script for Blazor Source Generation Issues
# Seeks and destroys all build artifacts, caches, and generated files
# Run from repository root

param(
    [switch]$Force = $false
)

Write-Host "🔥 Nuclear Clean Script - Blazor Source Generation Fix" -ForegroundColor Red
Write-Host "=================================================" -ForegroundColor Red
Write-Host ""

# Check if running from correct location
if (-not (Test-Path "Prisma")) {
    Write-Host "❌ ERROR: Must run from repository root (parent of Prisma folder)" -ForegroundColor Red
    exit 1
}

# Confirm with user unless -Force is specified
if (-not $Force) {
    Write-Host "⚠️  This will delete:" -ForegroundColor Yellow
    Write-Host "  - All bin/ and obj/ folders" -ForegroundColor Yellow
    Write-Host "  - All .vs/ folders (Visual Studio cache)" -ForegroundColor Yellow
    Write-Host "  - NuGet package caches" -ForegroundColor Yellow
    Write-Host "  - MSBuild binary logs" -ForegroundColor Yellow
    Write-Host "  - Razor/Blazor generated files" -ForegroundColor Yellow
    Write-Host ""
    $confirm = Read-Host "Continue? (yes/no)"
    if ($confirm -ne "yes") {
        Write-Host "Aborted." -ForegroundColor Yellow
        exit 0
    }
}

# Function to safely remove directory
function Remove-SafeDirectory {
    param([string]$Path)
    if (Test-Path $Path) {
        try {
            Write-Host "🗑️  Removing: $Path" -ForegroundColor Cyan
            Remove-Item -Path $Path -Recurse -Force -ErrorAction Stop
            Write-Host "   ✅ Removed" -ForegroundColor Green
        } catch {
            Write-Host "   ⚠️  Failed: $_" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "Step 1: Killing processes that might lock files..." -ForegroundColor Magenta
Write-Host "=================================================" -ForegroundColor Magenta

# Try to close Visual Studio processes (optional)
$vsProcesses = Get-Process | Where-Object { $_.ProcessName -match "devenv|ServiceHub|MSBuild" }
if ($vsProcesses) {
    Write-Host "⚠️  Found Visual Studio processes. Please close Visual Studio manually." -ForegroundColor Yellow
    Write-Host "   Processes: $($vsProcesses.ProcessName -join ', ')" -ForegroundColor Yellow
    if (-not $Force) {
        Read-Host "Press Enter when Visual Studio is closed"
    }
}

Write-Host ""
Write-Host "Step 2: Removing bin/ folders..." -ForegroundColor Magenta
Write-Host "=================================================" -ForegroundColor Magenta

Get-ChildItem -Path "." -Directory -Filter "bin" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
    Remove-SafeDirectory -Path $_.FullName
}

Write-Host ""
Write-Host "Step 3: Removing obj/ folders..." -ForegroundColor Magenta
Write-Host "=================================================" -ForegroundColor Magenta

Get-ChildItem -Path "." -Directory -Filter "obj" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
    Remove-SafeDirectory -Path $_.FullName
}

Write-Host ""
Write-Host "Step 4: Removing .vs/ folders (Visual Studio cache)..." -ForegroundColor Magenta
Write-Host "=================================================" -ForegroundColor Magenta

Get-ChildItem -Path "." -Directory -Filter ".vs" -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
    Remove-SafeDirectory -Path $_.FullName
}

Write-Host ""
Write-Host "Step 5: Removing MSBuild binary logs..." -ForegroundColor Magenta
Write-Host "=================================================" -ForegroundColor Magenta

Get-ChildItem -Path "." -Filter "*.binlog" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "🗑️  Removing: $($_.FullName)" -ForegroundColor Cyan
    Remove-Item -Path $_.FullName -Force
    Write-Host "   ✅ Removed" -ForegroundColor Green
}

Write-Host ""
Write-Host "Step 6: Clearing NuGet caches..." -ForegroundColor Magenta
Write-Host "=================================================" -ForegroundColor Magenta

Write-Host "🗑️  Clearing all NuGet caches..." -ForegroundColor Cyan
try {
    dotnet nuget locals all --clear
    Write-Host "   ✅ NuGet caches cleared" -ForegroundColor Green
} catch {
    Write-Host "   ⚠️  Failed to clear NuGet caches: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Step 7: Removing Razor/Blazor generated files..." -ForegroundColor Magenta
Write-Host "=================================================" -ForegroundColor Magenta

# Look for common Blazor generated file patterns
Get-ChildItem -Path "." -Filter "*.razor.g.cs" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "🗑️  Removing generated: $($_.FullName)" -ForegroundColor Cyan
    Remove-Item -Path $_.FullName -Force
    Write-Host "   ✅ Removed" -ForegroundColor Green
}

Write-Host ""
Write-Host "Step 8: Removing BuildArtifacts folder..." -ForegroundColor Magenta
Write-Host "=================================================" -ForegroundColor Magenta

Remove-SafeDirectory -Path "BuildArtifacts"

Write-Host ""
Write-Host "Step 9: Running dotnet clean..." -ForegroundColor Magenta
Write-Host "=================================================" -ForegroundColor Magenta

try {
    Push-Location "Prisma"
    dotnet clean --verbosity quiet
    Write-Host "✅ dotnet clean completed" -ForegroundColor Green
    Pop-Location
} catch {
    Write-Host "⚠️  dotnet clean failed: $_" -ForegroundColor Yellow
    Pop-Location
}

Write-Host ""
Write-Host "✅ Nuclear Clean Complete!" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Restart Visual Studio (if you were using it)" -ForegroundColor White
Write-Host "2. Run: dotnet restore" -ForegroundColor White
Write-Host "3. Run: dotnet build" -ForegroundColor White
Write-Host "4. If issues persist, check for .razor.cs code-behind conflicts" -ForegroundColor White
Write-Host ""
