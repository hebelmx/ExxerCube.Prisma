# =====================================================================
# DEMO CLEANUP RUNNER - Execute SQL cleanup script
# =====================================================================
# Purpose: Run demo data cleanup between stakeholder presentations
# WARNING: This performs HARD DELETES, ONLY for demo environments!
# =====================================================================

param(
    [string]$Server = "DESKTOP-FB2ES22\SQL2022",
    [string]$Database = "Prisma",
    [switch]$Confirm = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Demo Data Cleanup Runner" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Server  : $Server" -ForegroundColor Yellow
Write-Host "Database: $Database" -ForegroundColor Yellow
Write-Host ""

# Safety confirmation
if (-not $Confirm) {
    Write-Host "WARNING: This will PERMANENTLY DELETE all demo data!" -ForegroundColor Red
    Write-Host "This action CANNOT be undone!" -ForegroundColor Red
    Write-Host ""
    $response = Read-Host "Type 'DELETE' to confirm"

    if ($response -ne "DELETE") {
        Write-Host "Cleanup cancelled by user" -ForegroundColor Yellow
        exit 0
    }
}

Write-Host ""
Write-Host "Executing cleanup script..." -ForegroundColor Green

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SqlScript = Join-Path $ScriptDir "cleanup-demo-data.sql"

# Verify script exists
if (-not (Test-Path $SqlScript)) {
    Write-Host "ERROR: SQL script not found at: $SqlScript" -ForegroundColor Red
    exit 1
}

try {
    # Execute SQL script using sqlcmd
    Write-Host "Running SQL cleanup script..." -ForegroundColor Cyan

    $output = sqlcmd -S $Server -d $Database -i $SqlScript -E

    Write-Host $output

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Cleanup completed successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "ERROR: Cleanup failed!" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
