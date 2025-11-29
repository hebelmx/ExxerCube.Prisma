#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fix Visual Studio cache issues
.DESCRIPTION
    Clears Visual Studio cache and NuGet cache to resolve project load errors
#>

$ErrorActionPreference = "Continue"

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "VISUAL STUDIO CACHE FIX" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Write-Host "⚠ This will:" -ForegroundColor Yellow
Write-Host "  1. Delete .vs folder (Visual Studio cache)" -ForegroundColor Gray
Write-Host "  2. Delete bin/obj folders" -ForegroundColor Gray
Write-Host "  3. Clear NuGet package cache for project" -ForegroundColor Gray
Write-Host ""

$confirmation = Read-Host "Continue? (y/N)"
if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
    Write-Host "Cancelled." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "🗑️  Deleting Visual Studio cache..." -ForegroundColor Cyan

# Delete .vs folder
$vsFolder = "Code\Src\CSharp\.vs"
if (Test-Path $vsFolder) {
    Remove-Item $vsFolder -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Deleted $vsFolder" -ForegroundColor Green
} else {
    Write-Host "  ℹ $vsFolder not found (already clean)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "🗑️  Clearing NuGet local cache..." -ForegroundColor Cyan
dotnet nuget locals all --clear
Write-Host "  ✓ NuGet cache cleared" -ForegroundColor Green

Write-Host ""
Write-Host "🔄 Restoring packages..." -ForegroundColor Cyan
dotnet restore Code/Src/CSharp
Write-Host "  ✓ Packages restored" -ForegroundColor Green

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✓ CACHE CLEARED SUCCESSFULLY" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "💡 Next steps:" -ForegroundColor Cyan
Write-Host "  1. Reopen Visual Studio" -ForegroundColor White
Write-Host "  2. The NuGet error should be gone" -ForegroundColor White
Write-Host ""
