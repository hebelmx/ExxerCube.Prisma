@echo off
REM Enhanced Smart Dependency Fix Workflow
REM Analyzes CS0246 and CS0103 errors and fixes GlobalUsings.cs

echo ===============================================
echo Enhanced Smart Dependency Fix Workflow
echo ===============================================
echo.

REM Set paths
set BASE_PATH=F:\Dynamic\ExxerAi\ExxerAI
set ERROR_FILE=%BASE_PATH%\Errors\CS0246.txt
set ANALYSIS_REPORT=%BASE_PATH%\scripts\enhanced_dependency_analysis.json

REM Step 1: Run enhanced analyzer
echo Step 1: Analyzing dependencies...
echo Running: analyze_dependencies_smart_v2.py
python "%BASE_PATH%\scripts\analyze_dependencies_smart_v2.py" --base-path "%BASE_PATH%" --error-file "%ERROR_FILE%" --output "%ANALYSIS_REPORT%"

if errorlevel 1 (
    echo ERROR: Analysis failed!
    exit /b 1
)

echo.
echo Analysis complete. Report saved to: %ANALYSIS_REPORT%
echo.

REM Step 2: Run fixer in dry-run mode
echo Step 2: Running fixer in DRY-RUN mode...
echo Running: fix_dependencies_smart_v2.py --dry-run
python "%BASE_PATH%\scripts\fix_dependencies_smart_v2.py" --base-path "%BASE_PATH%" --report "%ANALYSIS_REPORT%" --dry-run

if errorlevel 1 (
    echo ERROR: Dry-run failed!
    exit /b 1
)

echo.
echo ===============================================
echo Dry-run complete. Review the changes above.
echo.
echo To APPLY the changes, run:
echo   python scripts\fix_dependencies_smart_v2.py --apply
echo.
echo Or use this batch file with --apply parameter:
echo   run_smart_dependency_fix.bat --apply
echo ===============================================

REM Check if user wants to apply
if "%1"=="--apply" (
    echo.
    echo APPLYING CHANGES...
    python "%BASE_PATH%\scripts\fix_dependencies_smart_v2.py" --base-path "%BASE_PATH%" --report "%ANALYSIS_REPORT%" --apply
)

pause