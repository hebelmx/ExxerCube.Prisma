<#
.SYNOPSIS
    Safely removes duplicate test files from Tests.System that belong in Infrastructure test projects.

.DESCRIPTION
    This script removes exact duplicates identified by the duplicate detection analysis.
    These files were copied by a "confused agent" from Tests.Infrastructure.Extraction.Teseract to Tests.System.

    Evidence:
    - Files are 100% identical (verified with diff)
    - TextSanitizerTests already has all tests SKIPPED with "Temporarily skipped to isolate XmlExtractor tests"
    - These test Infrastructure abstractions (IOcrExecutor, IImageEnhancementFilter), not system integration

    The original files remain in their correct location:
    - Tests.Infrastructure.Extraction.Teseract
    - Tests.Infrastructure.Extraction.GotOcr2

.PARAMETER DryRun
    If specified, shows what would be deleted without actually deleting files.

.EXAMPLE
    .\cleanup_test_duplicates.ps1 -DryRun
    # Shows what would be deleted

.EXAMPLE
    .\cleanup_test_duplicates.ps1
    # Performs actual deletion
#>

param(
    [switch]$DryRun = $false
)

$ErrorActionPreference = "Stop"

# Base path
$TestsSystemPath = "Code\Src\CSharp\Tests.System"

# Files to delete (confirmed duplicates from Tests.Infrastructure.Extraction.Teseract)
$DuplicateTestFiles = @(
    "TesseractOcrExecutorTests.cs",
    "TesseractOcrExecutorDegradedTests.cs",
    "TesseractOcrExecutorEnhancedAggressiveTests.cs",
    "AnalyticalFilterE2ETests.cs",
    "PolynomialFilterE2ETests.cs",
    "OcrFixturePipelineTests.cs",
    "TextSanitizerOcrPipelineTests.cs",
    "TextSanitizerTests.cs"
)

# Supporting fixture/collection files (also duplicates)
$DuplicateFixtureFiles = @(
    "TesseractCollection.cs",
    "TesseractDegradedCollection.cs",
    "TesseractEnhancedAggressiveCollection.cs",
    "TesseractEnhancedCollection.cs",
    "TesseractFixture.cs",
    "AnalyticalFilterE2ECollection.cs"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TEST DUPLICATE CLEANUP SCRIPT" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No files will be deleted" -ForegroundColor Yellow
    Write-Host ""
}

# Function to safely delete file
function Remove-DuplicateFile {
    param(
        [string]$FilePath,
        [string]$Category
    )

    if (Test-Path $FilePath) {
        $fileSize = (Get-Item $FilePath).Length
        $fileName = Split-Path $FilePath -Leaf

        if ($DryRun) {
            Write-Host "  [DRY RUN] Would delete: $fileName ($fileSize bytes)" -ForegroundColor Yellow
        }
        else {
            try {
                Remove-Item $FilePath -Force
                Write-Host "  ✓ Deleted: $fileName ($fileSize bytes)" -ForegroundColor Green
            }
            catch {
                Write-Host "  ✗ Failed to delete: $fileName - $_" -ForegroundColor Red
            }
        }
    }
    else {
        Write-Host "  ⊘ Not found (already deleted?): $(Split-Path $FilePath -Leaf)" -ForegroundColor Gray
    }
}

# Summary counters
$DeletedCount = 0
$NotFoundCount = 0
$TotalSize = 0

# Delete duplicate test files
Write-Host "📋 DUPLICATE TEST FILES:" -ForegroundColor Cyan
foreach ($file in $DuplicateTestFiles) {
    $filePath = Join-Path $TestsSystemPath $file

    if (Test-Path $filePath) {
        $TotalSize += (Get-Item $filePath).Length
        $DeletedCount++
    }
    else {
        $NotFoundCount++
    }

    Remove-DuplicateFile -FilePath $filePath -Category "Test File"
}

Write-Host ""
Write-Host "🔧 DUPLICATE FIXTURE/COLLECTION FILES:" -ForegroundColor Cyan
foreach ($file in $DuplicateFixtureFiles) {
    $filePath = Join-Path $TestsSystemPath $file

    if (Test-Path $filePath) {
        $TotalSize += (Get-Item $filePath).Length
        $DeletedCount++
    }
    else {
        $NotFoundCount++
    }

    Remove-DuplicateFile -FilePath $filePath -Category "Fixture/Collection"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "Mode: DRY RUN (no actual changes)" -ForegroundColor Yellow
}
else {
    Write-Host "Mode: LIVE (files deleted)" -ForegroundColor Green
}

Write-Host "Files that would be/were deleted: $DeletedCount" -ForegroundColor White
Write-Host "Files not found: $NotFoundCount" -ForegroundColor Gray
Write-Host "Total size freed: $($TotalSize / 1KB) KB" -ForegroundColor White

Write-Host ""
Write-Host "✅ VERIFICATION STEPS:" -ForegroundColor Cyan
Write-Host "  1. Run: dotnet test Code\Src\CSharp\Tests.System\ExxerCube.Prisma.Tests.System.csproj"
Write-Host "     Expected: Tests should pass (only DocumentIngestionIntegrationTests + XmlExtractorFixtureTests remain)"
Write-Host ""
Write-Host "  2. Run: dotnet test Code\Src\CSharp\Tests.Infrastructure.Extraction.Teseract\ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract.csproj"
Write-Host "     Expected: All deleted tests still exist here and pass"
Write-Host ""
Write-Host "  3. Check git status to review changes before committing"
Write-Host ""

if ($DryRun) {
    Write-Host "🔄 To perform actual cleanup, run without -DryRun:" -ForegroundColor Yellow
    Write-Host "   .\scripts\cleanup_test_duplicates.ps1" -ForegroundColor Yellow
}
else {
    Write-Host "✓ Cleanup complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📝 RECOMMENDED NEXT STEPS:" -ForegroundColor Cyan
    Write-Host "  1. Run the verification tests above" -ForegroundColor White
    Write-Host "  2. Review the DUPLICATE_CLEANUP_REPORT.md for details" -ForegroundColor White
    Write-Host "  3. Commit the changes with message:" -ForegroundColor White
    Write-Host "     'refactor: remove duplicate tests from Tests.System'" -ForegroundColor Gray
    Write-Host "     'These tests belong in Tests.Infrastructure.Extraction.Teseract'" -ForegroundColor Gray
}

Write-Host ""
