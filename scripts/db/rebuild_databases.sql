-- ============================================================
-- ExxerCube Prisma - Database Rebuild Script
-- ============================================================
-- This script drops and recreates both databases from scratch
-- Execute this script, then run the PowerShell script to create
-- and apply new migrations
-- ============================================================

USE master;
GO

-- Drop existing databases if they exist
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'Prisma')
BEGIN
    ALTER DATABASE [Prisma] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [Prisma];
    PRINT 'Dropped database: Prisma';
END
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = N'PrismaID')
BEGIN
    ALTER DATABASE [PrismaID] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [PrismaID];
    PRINT 'Dropped database: PrismaID';
END
GO

-- Create fresh databases
CREATE DATABASE [Prisma];
GO
PRINT 'Created database: Prisma';

CREATE DATABASE [PrismaID];
GO
PRINT 'Created database: PrismaID';

PRINT '';
PRINT '============================================================';
PRINT 'Database rebuild complete!';
PRINT 'Next steps:';
PRINT '1. Run rebuild_migrations.ps1 to create new migrations';
PRINT '2. Migrations will be automatically applied';
PRINT '============================================================';
