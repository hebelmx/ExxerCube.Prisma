@echo off
echo ================================================================================
echo CLEARING VISUAL STUDIO AND .NET BUILD CACHES
echo ================================================================================
echo.

echo 1. Cleaning solution build outputs...
cd /d "F:\Dynamic\ExxerAi\ExxerAI\code\src"
dotnet clean
echo.

echo 2. Clearing NuGet cache...
dotnet nuget locals all --clear
echo.

echo 3. Removing bin and obj folders...
echo Removing bin folders...
for /d /r . %%d in (bin) do @if exist "%%d" (
    echo Deleting: %%d
    rd /s /q "%%d"
)

echo Removing obj folders...
for /d /r . %%d in (obj) do @if exist "%%d" (
    echo Deleting: %%d
    rd /s /q "%%d"
)
echo.

echo 4. Clearing .vs folder (Visual Studio cache)...
for /d /r . %%d in (.vs) do @if exist "%%d" (
    echo Deleting: %%d
    rd /s /q "%%d"
)
echo.

echo 5. Clearing Rider cache folders...
for /d /r . %%d in (.idea) do @if exist "%%d" (
    echo Deleting: %%d
    rd /s /q "%%d"
)
echo.

echo ================================================================================
echo CACHE CLEARING COMPLETE!
echo ================================================================================
echo.
echo Please restart Visual Studio and run:
echo   dotnet restore
echo   dotnet build
echo.
pause