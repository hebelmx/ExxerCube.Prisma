@echo off
REM Install ExxerAI Type Database Update Scheduled Tasks
REM Runs PowerShell script as Administrator

echo.
echo ========================================
echo ExxerAI Type Database Scheduler Setup
echo ========================================
echo.
echo This will install 2 scheduled tasks:
echo   - Daily at 10:00 AM
echo   - Daily at 10:00 PM
echo.
echo Press any key to continue or Ctrl+C to cancel...
pause >nul

REM Check for admin privileges
net session >nul 2>&1
if %errorLevel% == 0 (
    echo Running with Administrator privileges...
    %SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe -ExecutionPolicy Bypass -File "%~dp0install-scheduled-tasks.ps1"
) else (
    echo.
    echo ERROR: This script requires Administrator privileges!
    echo.
    echo Please right-click and select "Run as Administrator"
    echo.
    pause
    exit /b 1
)

echo.
echo Press any key to exit...
pause >nul
