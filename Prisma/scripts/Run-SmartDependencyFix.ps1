# Enhanced Smart Dependency Fix Workflow
# Analyzes CS0246 and CS0103 errors and fixes GlobalUsings.cs

param(
    [switch]$Apply,
    [string]$BasePath = "F:\Dynamic\ExxerAi\ExxerAI",
    [string]$ErrorFile = "F:\Dynamic\ExxerAi\ExxerAI\Errors\CS0246.txt"
)

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Enhanced Smart Dependency Fix Workflow" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""

# Set paths
$AnalysisReport = Join-Path $BasePath "scripts\enhanced_dependency_analysis.json"
$AnalyzerScript = Join-Path $BasePath "scripts\analyze_dependencies_smart_v2.py"
$FixerScript = Join-Path $BasePath "scripts\fix_dependencies_smart_v2.py"

# Step 1: Run enhanced analyzer
Write-Host "Step 1: Analyzing dependencies..." -ForegroundColor Yellow
Write-Host "Running: analyze_dependencies_smart_v2.py" -ForegroundColor Gray

$analyzerArgs = @(
    $AnalyzerScript,
    "--base-path", $BasePath,
    "--error-file", $ErrorFile,
    "--output", $AnalysisReport
)

$analyzerResult = Start-Process -FilePath "python" -ArgumentList $analyzerArgs -Wait -NoNewWindow -PassThru

if ($analyzerResult.ExitCode -ne 0) {
    Write-Host "ERROR: Analysis failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Analysis complete. Report saved to: $AnalysisReport" -ForegroundColor Green
Write-Host ""

# Step 2: Run fixer
if ($Apply) {
    Write-Host "Step 2: APPLYING CHANGES..." -ForegroundColor Yellow
    Write-Host "Running: fix_dependencies_smart_v2.py --apply" -ForegroundColor Gray
    
    # Show warning
    Write-Host ""
    Write-Host "WARNING: This will:" -ForegroundColor Red
    Write-Host "  - Check git status and create safety commit if needed" -ForegroundColor Yellow
    Write-Host "  - Modify GlobalUsings.cs files across the solution" -ForegroundColor Yellow
    Write-Host "  - Create backups of all modified files" -ForegroundColor Yellow
    Write-Host ""
    
    $fixerArgs = @(
        $FixerScript,
        "--base-path", $BasePath,
        "--report", $AnalysisReport,
        "--apply"
    )
} else {
    Write-Host "Step 2: Running fixer in DRY-RUN mode..." -ForegroundColor Yellow
    Write-Host "Running: fix_dependencies_smart_v2.py --dry-run" -ForegroundColor Gray
    
    $fixerArgs = @(
        $FixerScript,
        "--base-path", $BasePath,
        "--report", $AnalysisReport,
        "--dry-run"
    )
}

$fixerResult = Start-Process -FilePath "python" -ArgumentList $fixerArgs -Wait -NoNewWindow -PassThru

if ($fixerResult.ExitCode -ne 0) {
    Write-Host "ERROR: Fixer failed!" -ForegroundColor Red
    exit 1
}

if (-not $Apply) {
    Write-Host ""
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host "Dry-run complete. Review the changes above." -ForegroundColor Green
    Write-Host ""
    Write-Host "To APPLY the changes, run:" -ForegroundColor Yellow
    Write-Host "  .\Run-SmartDependencyFix.ps1 -Apply" -ForegroundColor White
    Write-Host ""
    Write-Host "Or run the Python script directly:" -ForegroundColor Yellow
    Write-Host "  python scripts\fix_dependencies_smart_v2.py --apply" -ForegroundColor White
    Write-Host "===============================================" -ForegroundColor Cyan
} else {
    Write-Host ""
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host "Changes applied successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Run 'dotnet build' to verify fixes" -ForegroundColor White
    Write-Host "  2. Review the modified GlobalUsings.cs files" -ForegroundColor White
    Write-Host "  3. Test your application" -ForegroundColor White
    Write-Host "  4. Commit the changes when satisfied" -ForegroundColor White
    Write-Host "===============================================" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")