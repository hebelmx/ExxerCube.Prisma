@echo off
REM ExxerAI Infrastructure Split Migration - Batch Executor
REM Dependency-safe migration of 325 files to 7 specialized projects

echo ===============================================================
echo ExxerAI Infrastructure Split Migration
echo ===============================================================
echo.

REM Get base directory (assuming script is in scripts/migration/)
set BASE_DIR=%~dp0..\..
set PYTHON_SCRIPT=%~dp0infrastructure_migration_executor.py

echo Base Directory: %BASE_DIR%
echo Python Script: %PYTHON_SCRIPT%
echo.

REM Check if Python is available
python --version >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Python not found in PATH
    echo Please install Python 3.8+ and ensure it's in your PATH
    pause
    exit /b 1
)

REM Parse command line arguments
set DRY_RUN=
set REPORT_ONLY=
set MODE=FULL

:parse_args
if "%1"=="" goto :args_done
if /i "%1"=="--dry-run" (
    set DRY_RUN=--dry-run
    set MODE=DRY_RUN
    shift
    goto :parse_args
)
if /i "%1"=="--report-only" (
    set REPORT_ONLY=--report-only
    set MODE=REPORT_ONLY
    shift
    goto :parse_args
)
if /i "%1"=="--help" (
    goto :show_help
)
shift
goto :parse_args

:args_done

echo Mode: %MODE%
echo.

REM Create migration directories if they don't exist
if not exist "%BASE_DIR%\docs\migration" mkdir "%BASE_DIR%\docs\migration"
if not exist "%BASE_DIR%\docs\migration\logs" mkdir "%BASE_DIR%\docs\migration\logs"

REM Execute migration based on mode
if "%REPORT_ONLY%"=="--report-only" (
    echo ===============================================================
    echo GENERATING CLASSIFICATION REPORT
    echo ===============================================================
    echo.
    python "%PYTHON_SCRIPT%" --base-path "%BASE_DIR%" %REPORT_ONLY%
    goto :show_results
)

if "%DRY_RUN%"=="--dry-run" (
    echo ===============================================================
    echo DRY RUN MODE - NO CHANGES WILL BE MADE
    echo ===============================================================
    echo.
    echo This will analyze the migration without making any changes.
    echo.
    choice /c YN /m "Continue with dry run"
    if errorlevel 2 goto :cancelled
) else (
    echo ===============================================================
    echo LIVE MIGRATION MODE - CHANGES WILL BE MADE
    echo ===============================================================
    echo.
    echo WARNING: This will modify your project structure!
    echo.
    echo Before proceeding, ensure:
    echo   1. You have committed all changes to git
    echo   2. You have a backup of your project
    echo   3. All tests are currently passing
    echo.
    choice /c YN /m "Continue with live migration"
    if errorlevel 2 goto :cancelled
)

echo.
echo ===============================================================
echo EXECUTING MIGRATION
echo ===============================================================
echo.

REM Execute the migration
python "%PYTHON_SCRIPT%" --base-path "%BASE_DIR%" %DRY_RUN% %REPORT_ONLY%

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ===============================================================
    echo MIGRATION COMPLETED SUCCESSFULLY
    echo ===============================================================
    goto :show_results
) else (
    echo.
    echo ===============================================================
    echo MIGRATION FAILED
    echo ===============================================================
    echo Check the log files in docs/migration/logs/ for details
    pause
    exit /b 1
)

:show_results
echo.
echo Results:
if exist "%BASE_DIR%\docs\migration\infrastructure_classification_report.json" (
    echo   - Classification report: docs/migration/infrastructure_classification_report.json
)
if exist "%BASE_DIR%\docs\migration\logs" (
    echo   - Migration logs: docs/migration/logs/
)
echo   - Migration plan: docs/migration/INFRASTRUCTURE_SPLIT_MIGRATION_PLAN.md
echo.

if "%MODE%"=="FULL" (
    echo Next steps:
    echo   1. Run tests: dotnet test
    echo   2. Verify build: dotnet build
    echo   3. Check project references
    echo   4. Update CI/CD configurations
    echo.
)

echo Migration process completed.
pause
exit /b 0

:cancelled
echo.
echo Migration cancelled by user.
pause
exit /b 0

:show_help
echo.
echo Usage: %~nx0 [OPTIONS]
echo.
echo OPTIONS:
echo   --dry-run      Perform analysis without making changes
echo   --report-only  Generate classification report only
echo   --help         Show this help message
echo.
echo Examples:
echo   %~nx0                    Full migration (makes changes)
echo   %~nx0 --dry-run          Analyze migration plan
echo   %~nx0 --report-only      Generate file classification report
echo.
pause
exit /b 0