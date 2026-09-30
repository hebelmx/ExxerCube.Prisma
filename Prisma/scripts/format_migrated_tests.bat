@echo off
REM Quick script to format only migrated test projects
REM "CLEAN CODE STARTS WITH CLEAN TESTS!"

echo ============================================================
echo Formatting Migrated Test Projects (5 passes each)
echo ============================================================

python "%~dp0format_all_projects.py" --projects "*.Adapter.Test" --passes 5 --delay 1.5
if %ERRORLEVEL% NEQ 0 goto :error

python "%~dp0format_all_projects.py" --projects "*.Integration.Test" --passes 5 --delay 1.5
if %ERRORLEVEL% NEQ 0 goto :error

python "%~dp0format_all_projects.py" --projects "*.System.Test" --passes 5 --delay 1.5
if %ERRORLEVEL% NEQ 0 goto :error

echo.
echo ============================================================
echo All test projects formatted successfully!
echo ============================================================
goto :end

:error
echo.
echo ERROR: Formatting failed!
exit /b 1

:end
