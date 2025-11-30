-- =====================================================================
-- DEMO CLEANUP SCRIPT - HARD DELETES (NOT FOR PRODUCTION!)
-- =====================================================================
-- Purpose: Clean demo data between stakeholder presentations
-- WARNING: This script performs HARD DELETES, not soft deletes
-- ONLY use for demo environments, NEVER in production!
-- =====================================================================

USE Prisma;
GO

PRINT '========================================';
PRINT 'Starting Demo Data Cleanup';
PRINT 'Database: Prisma';
PRINT 'Date: ' + CONVERT(VARCHAR(20), GETDATE(), 120);
PRINT '========================================';
PRINT '';

-- Disable foreign key constraints temporarily
PRINT 'Disabling foreign key constraints...';
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
GO

-- =====================================================================
-- 1. Clean AuditRecords (Event traceability data)
-- =====================================================================
PRINT 'Cleaning AuditRecords table...';
DECLARE @AuditRecordsCount INT;
SELECT @AuditRecordsCount = COUNT(*) FROM AuditRecords;

DELETE FROM AuditRecords;

PRINT '  Deleted ' + CAST(@AuditRecordsCount AS VARCHAR(10)) + ' audit records';
PRINT '';

-- =====================================================================
-- 2. Clean FileMetadata (Downloaded files)
-- =====================================================================
PRINT 'Cleaning FileMetadata table...';
DECLARE @FileMetadataCount INT;
SELECT @FileMetadataCount = COUNT(*) FROM FileMetadata;

DELETE FROM FileMetadata;

PRINT '  Deleted ' + CAST(@FileMetadataCount AS VARCHAR(10)) + ' file metadata records';
PRINT '';

-- =====================================================================
-- 3. Clean ReviewCases (Manual review queue)
-- =====================================================================
PRINT 'Cleaning ReviewCases table...';
DECLARE @ReviewCasesCount INT;
SELECT @ReviewCasesCount = COUNT(*) FROM ReviewCases WHERE 1=1; -- Check if table exists

IF OBJECT_ID('ReviewCases', 'U') IS NOT NULL
BEGIN
    DELETE FROM ReviewCases;
    PRINT '  Deleted ' + CAST(@ReviewCasesCount AS VARCHAR(10)) + ' review cases';
END
ELSE
BEGIN
    PRINT '  ReviewCases table does not exist, skipping...';
END
PRINT '';

-- =====================================================================
-- 4. Clean ReviewDecisions (Manual review decisions)
-- =====================================================================
PRINT 'Cleaning ReviewDecisions table...';
DECLARE @ReviewDecisionsCount INT;

IF OBJECT_ID('ReviewDecisions', 'U') IS NOT NULL
BEGIN
    SELECT @ReviewDecisionsCount = COUNT(*) FROM ReviewDecisions;
    DELETE FROM ReviewDecisions;
    PRINT '  Deleted ' + CAST(@ReviewDecisionsCount AS VARCHAR(10)) + ' review decisions';
END
ELSE
BEGIN
    PRINT '  ReviewDecisions table does not exist, skipping...';
END
PRINT '';

-- =====================================================================
-- 5. Clean SLAStatus (SLA tracking data)
-- =====================================================================
PRINT 'Cleaning SLAStatus table...';
DECLARE @SLAStatusCount INT;

IF OBJECT_ID('SLAStatus', 'U') IS NOT NULL
BEGIN
    SELECT @SLAStatusCount = COUNT(*) FROM SLAStatus;
    DELETE FROM SLAStatus;
    PRINT '  Deleted ' + CAST(@SLAStatusCount AS VARCHAR(10)) + ' SLA status records';
END
ELSE
BEGIN
    PRINT '  SLAStatus table does not exist, skipping...';
END
PRINT '';

-- =====================================================================
-- 6. Re-enable foreign key constraints
-- =====================================================================
PRINT 'Re-enabling foreign key constraints...';
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
GO

-- =====================================================================
-- 7. Reset identity seeds (optional - for cleaner demo)
-- =====================================================================
PRINT 'Resetting identity seeds...';

IF OBJECT_ID('AuditRecords', 'U') IS NOT NULL
    DBCC CHECKIDENT ('AuditRecords', RESEED, 0);

IF OBJECT_ID('FileMetadata', 'U') IS NOT NULL
    DBCC CHECKIDENT ('FileMetadata', RESEED, 0);

IF OBJECT_ID('ReviewCases', 'U') IS NOT NULL
    DBCC CHECKIDENT ('ReviewCases', RESEED, 0);

IF OBJECT_ID('ReviewDecisions', 'U') IS NOT NULL
    DBCC CHECKIDENT ('ReviewDecisions', RESEED, 0);

IF OBJECT_ID('SLAStatus', 'U') IS NOT NULL
    DBCC CHECKIDENT ('SLAStatus', RESEED, 0);

PRINT '  Identity seeds reset';
PRINT '';

-- =====================================================================
-- 8. Verify cleanup
-- =====================================================================
PRINT '========================================';
PRINT 'Cleanup Summary:';
PRINT '========================================';

DECLARE @CurrentAuditRecords INT = (SELECT COUNT(*) FROM AuditRecords);
DECLARE @CurrentFileMetadata INT = (SELECT COUNT(*) FROM FileMetadata);

PRINT 'Current row counts:';
PRINT '  AuditRecords: ' + CAST(@CurrentAuditRecords AS VARCHAR(10));
PRINT '  FileMetadata: ' + CAST(@CurrentFileMetadata AS VARCHAR(10));

IF OBJECT_ID('ReviewCases', 'U') IS NOT NULL
BEGIN
    DECLARE @CurrentReviewCases INT = (SELECT COUNT(*) FROM ReviewCases);
    PRINT '  ReviewCases: ' + CAST(@CurrentReviewCases AS VARCHAR(10));
END

IF OBJECT_ID('ReviewDecisions', 'U') IS NOT NULL
BEGIN
    DECLARE @CurrentReviewDecisions INT = (SELECT COUNT(*) FROM ReviewDecisions);
    PRINT '  ReviewDecisions: ' + CAST(@CurrentReviewDecisions AS VARCHAR(10));
END

IF OBJECT_ID('SLAStatus', 'U') IS NOT NULL
BEGIN
    DECLARE @CurrentSLAStatus INT = (SELECT COUNT(*) FROM SLAStatus);
    PRINT '  SLAStatus: ' + CAST(@CurrentSLAStatus AS VARCHAR(10));
END

PRINT '';
PRINT '========================================';
PRINT 'Demo Data Cleanup Complete!';
PRINT 'Database is ready for fresh demo run';
PRINT '========================================';
GO
