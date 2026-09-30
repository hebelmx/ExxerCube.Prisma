<#
.SYNOPSIS
    Removes tests from Tests.Infrastructure.Extraction.Teseract that violate architecture rules.

.DESCRIPTION
    Infrastructure test projects should ONLY test their own infrastructure components.
    Tests that depend on multiple infrastructure projects (cross-cutting concerns) belong in Tests.System.

    ARCHITECTURE RULE VIOLATION:
    Tests.Infrastructure.Extraction.Teseract has tests that depend on:
    - Infrastructure.Imaging (filters)
    - Infrastructure.Extraction (OCR)
    This is a SYSTEM-level integration test, NOT an infrastructure unit test.

    The "confused agent":
    1. Created tests in Tests.Infrastructure.Extraction.Teseract (WRONG - architecture violation)
    2. Copied tests to Tests.System (CORRECT - cross-cutting integration tests)
    3. Didn't delete from Tests.Infrastructure.Extraction.Teseract (FORGOT cleanup)

    This script removes the architecture violations from Tests.Infrastructure.Extraction.Teseract.
    The correct versions remain in Tests.System.

.PARAMETER DryRun
    If specified, shows what would be deleted without actually deleting files.

.EXAMPLE
    .\cleanup_architecture_violations.ps1 -DryRun
    # Shows what would be deleted

.EXAMPLE
    .\cleanup_architecture_violations.ps1
    # Performs actual deletion
#>

param(
    [switch]$DryRun = $false
)

$ErrorActionPreference = "Stop"

# Base path
$InfraTestPath = "Code\Src\CSharp\Tests.Infrastructure.Extraction.Teseract"

# Files to delete (architecture violations - cross-cutting tests in infrastructure project)
$ArchitectureViolations = @(
    "TesseractOcrExecutorTests.cs",
    "TesseractOcrExecutorDegradedTests.cs",
    "TesseractOcrExecutorEnhancedAggressiveTests.cs",
    "AnalyticalFilterE2ETests.cs",              # Uses Infrastructure.Imaging + Infrastructure.Extraction
    "PolynomialFilterE2ETests.cs",              # Uses Infrastructure.Imaging + Infrastructure.Extraction
    "OcrFixturePipelineTests.cs",               # Multi-layer pipeline test
    "TextSanitizerOcrPipelineTests.cs",         # Multi-layer pipeline test
    "TextSanitizerTests.cs"                     # May be ok, but duplicated
)

# Supporting fixture/collection files (also architecture violations)
$FixtureViolations = @(
    "TesseractCollection.cs",
    "TesseractDegradedCollection.cs",
    "TesseractEnhancedAggressiveCollection.cs",
    "TesseractEnhancedCollection.cs",
    "TesseractFixture.cs",
    "AnalyticalFilterE2ECollection.cs"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ARCHITECTURE VIOLATION CLEANUP" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "RULE: Infrastructure tests should ONLY test their own infrastructure layer." -ForegroundColor Yellow
Write-Host "VIOLATION: Tests.Infrastructure.Extraction.Teseract has cross-cutting tests." -ForegroundColor Yellow
Write-Host "SOLUTION: Delete from Infrastructure, keep in Tests.System (correct location)." -ForegroundColor Yellow
Write-Host ""

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No files will be deleted" -ForegroundColor Yellow
    Write-Host ""
}

# Function to safely delete file
function Remove-ArchitectureViolation {
    param(
        [string]$FilePath,
        [string]$Reason
    )

    if (Test-Path $FilePath) {
        $fileSize = (Get-Item $FilePath).Length
        $fileName = Split-Path $FilePath -Leaf

        if ($DryRun) {
            Write-Host "  [DRY RUN] Would delete: $fileName" -ForegroundColor Yellow
            Write-Host "            Size: $fileSize bytes" -ForegroundColor Gray
            Write-Host "            Reason: $Reason" -ForegroundColor Gray
        }
        else {
            try {
                Remove-Item $FilePath -Force
                Write-Host "  ✓ Deleted: $fileName" -ForegroundColor Green
                Write-Host "    Reason: $Reason" -ForegroundColor Gray
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

# Delete architecture violation test files
Write-Host "🚫 ARCHITECTURE VIOLATION TEST FILES:" -ForegroundColor Cyan
Write-Host "   (Cross-cutting tests that belong in Tests.System)" -ForegroundColor Gray
Write-Host ""

foreach ($file in $ArchitectureViolations) {
    $filePath = Join-Path $InfraTestPath $file

    $reason = switch ($file) {
        { $_ -like "*E2E*" } { "E2E test crossing Infrastructure.Imaging + Infrastructure.Extraction boundaries" }
        { $_ -like "*Pipeline*" } { "Multi-layer pipeline test (system-level integration)" }
        { $_ -like "*OcrExecutor*" } { "May use cross-cutting concerns (fixtures from multiple infra layers)" }
        default { "Duplicated cross-cutting test" }
    }

    if (Test-Path $filePath) {
        $TotalSize += (Get-Item $filePath).Length
        $DeletedCount++
    }
    else {
        $NotFoundCount++
    }

    Remove-ArchitectureViolation -FilePath $filePath -Reason $reason
}

Write-Host ""
Write-Host "🔧 FIXTURE/COLLECTION FILES (supporting architecture violations):" -ForegroundColor Cyan
foreach ($file in $FixtureViolations) {
    $filePath = Join-Path $InfraTestPath $file

    if (Test-Path $filePath) {
        $TotalSize += (Get-Item $filePath).Length
        $DeletedCount++
    }
    else {
        $NotFoundCount++
    }

    Remove-ArchitectureViolation -FilePath $filePath -Reason "Supports cross-cutting tests"
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
Write-Host "Total size freed: $([math]::Round($TotalSize / 1KB, 2)) KB" -ForegroundColor White

Write-Host ""
Write-Host "✅ VERIFICATION STEPS:" -ForegroundColor Cyan
Write-Host "  1. Check Tests.Infrastructure.Extraction.Teseract project references:" -ForegroundColor White
Write-Host "     Should ONLY reference Infrastructure.Extraction (no Imaging, no other infra)" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. Run: dotnet test Code\Src\CSharp\Tests.Infrastructure.Extraction.Teseract\..." -ForegroundColor White
Write-Host "     Expected: Should compile and run (only unit tests remain)" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Run: dotnet test Code\Src\CSharp\Tests.System\..." -ForegroundColor White
Write-Host "     Expected: Cross-cutting integration tests still exist and run here" -ForegroundColor Gray
Write-Host ""
Write-Host "  4. Verify architecture compliance:" -ForegroundColor White
Write-Host "     Tests.Infrastructure.* projects should have NO cross-project test dependencies" -ForegroundColor Gray
Write-Host ""

if ($DryRun) {
    Write-Host "🔄 To perform actual cleanup, run without -DryRun:" -ForegroundColor Yellow
    Write-Host "   .\scripts\cleanup_architecture_violations.ps1" -ForegroundColor Yellow
}
else {
    Write-Host "✓ Architecture violation cleanup complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📝 RECOMMENDED NEXT STEPS:" -ForegroundColor Cyan
    Write-Host "  1. Remove Infrastructure.Imaging reference from Tests.Infrastructure.Extraction.Teseract.csproj" -ForegroundColor White
    Write-Host "  2. Run tests to verify no compilation errors" -ForegroundColor White
    Write-Host "  3. Commit with message:" -ForegroundColor White
    Write-Host "     'refactor: remove architecture violations from Tests.Infrastructure.Extraction.Teseract'" -ForegroundColor Gray
    Write-Host "     'Cross-cutting tests moved to Tests.System (correct location)'" -ForegroundColor Gray
}

Write-Host ""
