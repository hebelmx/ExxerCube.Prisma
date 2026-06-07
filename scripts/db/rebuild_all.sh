#!/bin/bash
# ============================================================
# ExxerCube Prisma - Complete Database Rebuild (Bash version)
# ============================================================

set -e

echo "============================================================"
echo "ExxerCube Prisma - Complete Database Rebuild"
echo "============================================================"
echo ""

ROOT_PATH="F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Code/Src/CSharp"
WEB_UI_PATH="$ROOT_PATH/03-UI/UI/ExxerCube.Prisma.Web.UI"
INFRA_DB_PATH="$ROOT_PATH/02-Infrastructure/Infrastructure.Database"

# Step 1: Execute SQL script to drop and recreate databases
echo "Step 1: Dropping and recreating databases..."
echo "Please execute this SQL script manually in SSMS or sqlcmd:"
echo "  F:\\Dynamic\\ExxerCubeBanamex\\ExxerCube.Prisma\\rebuild_databases.sql"
echo ""
read -p "Press ENTER after you've executed the SQL script..."
echo ""

# Step 2: Delete old migrations
echo "Step 2: Deleting old migration folders..."

if [ -d "$WEB_UI_PATH/Data/Migrations" ]; then
    rm -rf "$WEB_UI_PATH/Data/Migrations"
    echo "  ✓ Deleted: Identity migrations"
else
    echo "  - Already clean: Identity migrations"
fi

if [ -d "$INFRA_DB_PATH/Migrations" ]; then
    rm -rf "$INFRA_DB_PATH/Migrations"
    echo "  ✓ Deleted: Application migrations"
else
    echo "  - Already clean: Application migrations"
fi

echo ""

# Step 3: Create new migrations
echo "Step 3: Creating new migrations..."

# Create Identity migration
echo "  Creating Identity migration (PrismaID)..."
cd "$WEB_UI_PATH"
dotnet ef migrations add InitialCreate \
    --context ApplicationDbContext \
    --output-dir Data/Migrations
echo "  ✓ Identity migration created"

echo ""

# Create Application migration
echo "  Creating Application migration (Prisma)..."
cd "$INFRA_DB_PATH"
dotnet ef migrations add InitialCreate \
    --context PrismaDbContext \
    --startup-project "$WEB_UI_PATH" \
    --output-dir Migrations
echo "  ✓ Application migration created"

echo ""

# Step 4: Apply migrations
echo "Step 4: Applying migrations to databases..."

# Apply Identity migration
echo "  Applying Identity migration to PrismaID..."
cd "$WEB_UI_PATH"
dotnet ef database update --context ApplicationDbContext
echo "  ✓ Identity migration applied"

echo ""

# Apply Application migration
echo "  Applying Application migration to Prisma..."
cd "$INFRA_DB_PATH"
dotnet ef database update \
    --context PrismaDbContext \
    --startup-project "$WEB_UI_PATH"
echo "  ✓ Application migration applied"

echo ""
echo "============================================================"
echo "Database rebuild complete!"
echo "============================================================"
echo ""
echo "Summary:"
echo "  ✓ Dropped and recreated databases"
echo "  ✓ Deleted old migrations"
echo "  ✓ Created fresh migrations for both databases"
echo "  ✓ Applied migrations to PrismaID and Prisma"
echo ""
echo "You can now restart the application!"
echo ""
