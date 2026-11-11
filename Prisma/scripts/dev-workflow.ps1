# Development Workflow without Visual Studio
# For .NET 10 Preview Development

# Quick commands for daily development
Write-Host "ExxerAI Development Commands" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan

function Show-Menu {
    Write-Host "`n1. Build Solution" -ForegroundColor Yellow
    Write-Host "2. Run Tests" -ForegroundColor Yellow
    Write-Host "3. Run Specific Test" -ForegroundColor Yellow
    Write-Host "4. Watch & Rebuild on Changes" -ForegroundColor Yellow
    Write-Host "5. Clean Everything" -ForegroundColor Yellow
    Write-Host "6. Check for Errors Only" -ForegroundColor Yellow
    Write-Host "7. Generate Build Report" -ForegroundColor Yellow
    Write-Host "8. Exit" -ForegroundColor Yellow
}

Set-Location "F:\Dynamic\ExxerAi\ExxerAI\code\src"

do {
    Show-Menu
    $choice = Read-Host "`nSelect option"
    
    switch ($choice) {
        '1' { 
            Write-Host "`nBuilding solution..." -ForegroundColor Cyan
            dotnet build --no-restore
        }
        '2' { 
            Write-Host "`nRunning all tests..." -ForegroundColor Cyan
            dotnet test --no-build --logger "console;verbosity=normal"
        }
        '3' { 
            $testName = Read-Host "Enter test name pattern"
            Write-Host "`nRunning tests matching: $testName" -ForegroundColor Cyan
            dotnet test --no-build --filter "FullyQualifiedName~$testName"
        }
        '4' { 
            Write-Host "`nStarting file watcher (Ctrl+C to stop)..." -ForegroundColor Cyan
            dotnet watch build
        }
        '5' { 
            Write-Host "`nCleaning solution..." -ForegroundColor Cyan
            dotnet clean
            Get-ChildItem -Recurse -Directory -Include bin,obj | Remove-Item -Recurse -Force
        }
        '6' { 
            Write-Host "`nChecking for build errors..." -ForegroundColor Cyan
            dotnet build --no-restore --warnaserror 2>&1 | Select-String -Pattern "error"
        }
        '7' { 
            Write-Host "`nGenerating build report..." -ForegroundColor Cyan
            dotnet build --no-restore -bl:build.binlog
            Write-Host "Build log created: build.binlog" -ForegroundColor Green
            Write-Host "View with: msbuild build.binlog /v:diag" -ForegroundColor Yellow
        }
    }
    
    if ($choice -ne '8') {
        Write-Host "`nPress any key to continue..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    }
} while ($choice -ne '8')