# Database Rebuild Guide

This guide explains how to completely rebuild the ExxerCube Prisma databases from scratch.

## Files Created

1. **rebuild_databases.sql** - SQL script to drop and recreate both databases
2. **rebuild_migrations.ps1** - PowerShell script to delete old migrations, create new ones, and apply them
3. **rebuild_all.sh** - Bash script that does everything (requires manual SQL execution)
4. **fix_template_schema.sql** - Previous fix for Templates table (now obsolete)

## What Gets Fixed

✅ **Ambiguous Routes** - Deleted duplicate `SystemFlowExternal.razor`
✅ **Database Schema** - Fresh migrations eliminate all schema issues
✅ **EF Core Warnings** - New migrations will include proper value comparers
✅ **Templates Table** - Correct schema with string TemplateId

## Quick Start (Recommended)

### Option A: PowerShell (Easiest)

```powershell
# 1. Execute SQL script in SSMS or sqlcmd
sqlcmd -S DESKTOP-FB2ES22\SQL2022 -E -i "F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\rebuild_databases.sql"

# 2. Run PowerShell script to handle migrations
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma
.\rebuild_migrations.ps1
```

### Option B: Manual Steps

```bash
# 1. Execute SQL script
# Open rebuild_databases.sql in SSMS and execute it

# 2. Delete old migrations
rm -rf "F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Code/Src/CSharp/03-UI/UI/ExxerCube.Prisma.Web.UI/Data/Migrations"
rm -rf "F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Code/Src/CSharp/02-Infrastructure/Infrastructure.Database/Migrations"

# 3. Create Identity migration
cd "F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Code/Src/CSharp/03-UI/UI/ExxerCube.Prisma.Web.UI"
dotnet ef migrations add InitialCreate --context ApplicationDbContext --output-dir Data/Migrations

# 4. Create Application migration
cd "F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Code/Src/CSharp/02-Infrastructure/Infrastructure.Database"
dotnet ef migrations add InitialCreate --context PrismaDbContext --startup-project "../../../03-UI/UI/ExxerCube.Prisma.Web.UI" --output-dir Migrations

# 5. Apply Identity migration
cd "F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Code/Src/CSharp/03-UI/UI/ExxerCube.Prisma.Web.UI"
dotnet ef database update --context ApplicationDbContext

# 6. Apply Application migration
cd "F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Code/Src/CSharp/02-Infrastructure/Infrastructure.Database"
dotnet ef database update --context PrismaDbContext --startup-project "../../../03-UI/UI/ExxerCube.Prisma.Web.UI"
```

## Database Details

### Databases
- **PrismaID** - ASP.NET Identity tables (Users, Roles, etc.)
- **Prisma** - Application tables (Files, Audit, SLA, etc.)

### DbContexts
- **ApplicationDbContext** - Manages PrismaID database
- **PrismaDbContext** - Manages Prisma database

## Verification

After running the rebuild:

```bash
# Check that migrations were created
ls "F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Code/Src/CSharp/03-UI/UI/ExxerCube.Prisma.Web.UI/Data/Migrations"
ls "F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Code/Src/CSharp/02-Infrastructure/Infrastructure.Database/Migrations"

# Check database tables in SQL Server
sqlcmd -S DESKTOP-FB2ES22\SQL2022 -E -Q "USE Prisma; SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME;"
sqlcmd -S DESKTOP-FB2ES22\SQL2022 -E -Q "USE PrismaID; SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME;"
```

## Troubleshooting

### EF Core tools not installed?
```bash
dotnet tool install --global dotnet-ef
```

### Permission errors?
Run PowerShell as Administrator

### SQL Server connection issues?
- Verify server name: `DESKTOP-FB2ES22\SQL2022`
- Check Windows Authentication is enabled
- Ensure SQL Server service is running

## After Rebuild

1. **Restart the application** - The app should start without errors
2. **Create admin user** - Registration will work without email confirmation
3. **Test all pages** - Authorization is currently disabled for testing
4. **Check logs** - No more InvalidCastException, ambiguous route errors, or "Invalid object name 'Templates'" errors

## Template Migration (Additional Step)

If you encounter "Invalid object name 'Templates'" errors after the rebuild, you may need to apply the Template migration separately:

```bash
# Option 1: Generate and execute SQL script (recommended if DI errors occur)
cd "F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Code/Src/CSharp/02-Infrastructure/Infrastructure.Export.Adaptive"

# Generate SQL script
dotnet ef migrations script --context TemplateDbContext --startup-project "../../03-UI/UI/ExxerCube.Prisma.Web.UI" --idempotent --output template_migration.sql

# Execute via PowerShell (Windows)
powershell.exe -Command "Invoke-Sqlcmd -ServerInstance 'DESKTOP-FB2ES22\SQL2022' -Database 'Prisma' -InputFile 'template_migration.sql'"

# Option 2: Direct migration update (if DI container works)
dotnet ef database update --context TemplateDbContext --startup-project "../../03-UI/UI/ExxerCube.Prisma.Web.UI"
```

**Verify Templates tables:**
```powershell
powershell.exe -Command "Invoke-Sqlcmd -ServerInstance 'DESKTOP-FB2ES22\SQL2022' -Database 'Prisma' -Query 'SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE=''BASE TABLE'' AND TABLE_NAME IN (''Templates'', ''FieldMapping'') ORDER BY TABLE_NAME;'"
```

Expected output: FieldMapping, Templates

## Re-enable Authorization (Later)

When ready to re-enable authorization:

1. Edit `Components/Routes.razor`
2. Uncomment `AuthorizeRouteView`
3. Comment out `RouteView`
4. Set `RequireConfirmedAccount = true` in Program.cs (if needed)
