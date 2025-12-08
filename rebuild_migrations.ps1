# ============================================================
# ExxerCube Prisma - Migration Rebuild Script
# ============================================================
# This script:
# 1. Deletes old migration folders
# 2. Creates fresh migrations for both databases
# 3. Applies migrations to databases
# ============================================================

$ErrorActionPreference = "Stop"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "ExxerCube Prisma - Migration Rebuild" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# Define paths
$rootPath = "F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp"
$webUiPath = "$rootPath\03-UI\UI\ExxerCube.Prisma.Web.UI"
$infraDbPath = "$rootPath\02-Infrastructure\Infrastructure.Database"

# Migration folders
$identityMigrationsPath = "$webUiPath\Data\Migrations"
$prismaMigrationsPath = "$infraDbPath\Migrations"

# Step 1: Delete old migrations
Write-Host "Step 1: Deleting old migration folders..." -ForegroundColor Yellow

if (Test-Path $identityMigrationsPath) {
    Remove-Item -Path $identityMigrationsPath -Recurse -Force
    Write-Host "  ✓ Deleted: $identityMigrationsPath" -ForegroundColor Green
} else {
    Write-Host "  - Already clean: $identityMigrationsPath" -ForegroundColor Gray
}

if (Test-Path $prismaMigrationsPath) {
    Remove-Item -Path $prismaMigrationsPath -Recurse -Force
    Write-Host "  ✓ Deleted: $prismaMigrationsPath" -ForegroundColor Green
} else {
    Write-Host "  - Already clean: $prismaMigrationsPath" -ForegroundColor Gray
}

Write-Host ""

# Step 2: Create new migrations
Write-Host "Step 2: Creating new migrations..." -ForegroundColor Yellow

# Create Identity (PrismaID) migration
Write-Host "  Creating Identity migration (PrismaID)..." -ForegroundColor Cyan
Set-Location $webUiPath
$result = dotnet ef migrations add InitialCreate `
    --context ApplicationDbContext `
    --output-dir Data/Migrations `
    2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ Identity migration created successfully" -ForegroundColor Green
} else {
    Write-Host "  ✗ Failed to create Identity migration" -ForegroundColor Red
    Write-Host $result
    exit 1
}

Write-Host ""

# Create Application (Prisma) migration
Write-Host "  Creating Application migration (Prisma)..." -ForegroundColor Cyan
Set-Location $infraDbPath
$result = dotnet ef migrations add InitialCreate `
    --context PrismaDbContext `
    --startup-project $webUiPath `
    --output-dir Migrations `
    2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ Application migration created successfully" -ForegroundColor Green
} else {
    Write-Host "  ✗ Failed to create Application migration" -ForegroundColor Red
    Write-Host $result
    exit 1
}

Write-Host ""

# Step 3: Apply migrations
Write-Host "Step 3: Applying migrations to databases..." -ForegroundColor Yellow

# Apply Identity migration
Write-Host "  Applying Identity migration to PrismaID..." -ForegroundColor Cyan
Set-Location $webUiPath
$result = dotnet ef database update `
    --context ApplicationDbContext `
    2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ Identity migration applied successfully" -ForegroundColor Green
} else {
    Write-Host "  ✗ Failed to apply Identity migration" -ForegroundColor Red
    Write-Host $result
    exit 1
}

Write-Host ""

# Apply Application migration
Write-Host "  Applying Application migration to Prisma..." -ForegroundColor Cyan
Set-Location $infraDbPath
$result = dotnet ef database update `
    --context PrismaDbContext `
    --startup-project $webUiPath `
    2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ Application migration applied successfully" -ForegroundColor Green
} else {
    Write-Host "  ✗ Failed to apply Application migration" -ForegroundColor Red
    Write-Host $result
    exit 1
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Migration rebuild complete!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "  ✓ Deleted old migrations" -ForegroundColor Green
Write-Host "  ✓ Created fresh migrations for both databases" -ForegroundColor Green
Write-Host "  ✓ Applied migrations to PrismaID and Prisma" -ForegroundColor Green
Write-Host ""
Write-Host "You can now restart the application!" -ForegroundColor Cyan
Write-Host ""

# Return to original location
Set-Location $rootPath
