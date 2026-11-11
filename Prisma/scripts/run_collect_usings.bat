@echo off
REM Batch script to run the collect_using_statements.py script with various options

echo === ExxerAI Using Statement Collector ===
echo.

REM Check if Python is installed
python --version >nul 2>&1
if errorlevel 1 (
    echo Error: Python is not installed or not in PATH
    exit /b 1
)

REM Navigate to scripts directory
cd /d "%~dp0"

echo Available commands:
echo   1. Collect all using statements (saves to JSON)
echo   2. Generate GlobalUsing.cs preview (dry run)
echo   3. Deploy to test projects (dry run)
echo   4. Run all steps (dry run)
echo   5. Run all steps (ACTUAL - will modify files)
echo   6. Exit
echo.

:menu
set /p choice="Enter your choice (1-6): "

if "%choice%"=="1" (
    echo.
    echo Collecting using statements...
    python collect_using_statements.py --step collect
    pause
    goto menu
)

if "%choice%"=="2" (
    echo.
    echo Generating GlobalUsing.cs preview...
    python collect_using_statements.py --step generate --dry-run
    pause
    goto menu
)

if "%choice%"=="3" (
    echo.
    echo Deploying to test projects (dry run)...
    python collect_using_statements.py --step deploy --dry-run
    pause
    goto menu
)

if "%choice%"=="4" (
    echo.
    echo Running all steps (dry run)...
    python collect_using_statements.py --step all --dry-run
    pause
    goto menu
)

if "%choice%"=="5" (
    echo.
    echo WARNING: This will modify files in your test projects!
    set /p confirm="Are you sure? (yes/no): "
    if /i "%confirm%"=="yes" (
        echo.
        echo Running all steps (ACTUAL)...
        python collect_using_statements.py --step all
        echo.
        echo Operation completed!
    ) else (
        echo Operation cancelled.
    )
    pause
    goto menu
)

if "%choice%"=="6" (
    exit /b 0
)

echo Invalid choice. Please try again.
goto menu